using BotArena.Sdk;

/// <summary>
/// Contract-driven tactical building blocks for the generated starter.
/// Keep or replace them as the bot develops; strategy belongs in BOTNAME.Tick.
/// </summary>
internal static class ArenaBasics
{
    private static readonly Direction[] Directions =
    [
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West,
    ];
    private static readonly HashSet<Position> NoPositions = [];

    public static GenericActorDecision? TryFabricateReady(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        HashSet<string> actionIds = contract.Rules.Actions
            .Where(action =>
                action.Kind
                    == GenericActorRulesContract.ActionKind.Fabrication)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        GenericActorActionLegality? action = context.ActionLegalities
            .Where(candidate =>
                candidate.Available
                && actionIds.Contains(candidate.ActionId))
            .OrderBy(candidate => candidate.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint>()
                .SingleOrDefault();
        if (action is null || targets is null || targets.AllowedValues.IsEmpty)
            return null;

        GenericActorActionArgument.UnitTarget target =
            targets.AllowedValues
                .OrderBy(value => value.TeamId)
                .ThenBy(value => value.UnitId)
                .First();
        return new GenericActorDecision(
            action.ActionId,
            action.ActionCode,
            [
                new GenericActorActionArgument.UnitTargetArgument(target),
            ],
            $"activating companion {target.TeamId}:{target.UnitId}");
    }

    public static GenericActorDecision? TryDodge(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        GenericActorContext.ObservedProjectile[] hostile =
            context.VisibleProjectiles
                ?.Where(projectile =>
                    projectile.OwnerTeamId
                        != context.Self.ActorId.TeamId)
                .OrderBy(projectile => projectile.ProjectileId)
                .ToArray()
            ?? [];
        if (!hostile.Any(projectile =>
                ReachesWithinAdvances(
                    projectile,
                    context.Self.Position,
                    maxAdvances: 2)))
        {
            return null;
        }

        GenericActorActionLegality? move = AvailableAction(
            contract,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            constraint = move?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        if (move is null || constraint is null)
            return null;

        HashSet<Position> occupied = Occupied(context, hostile);
        Position[] objectiveTiles = ActiveObjectiveTiles(contract, context);
        bool holdingObjective =
            objectiveTiles.Contains(context.Self.Position);
        Direction[] preference = OrderedDirections(contract, context);
        Direction? selected = constraint.AllowedValues
            .Where(direction => Directions.Contains(direction))
            .Select(direction =>
            {
                (int dx, int dy) = direction.Vector();
                return (
                    Direction: direction,
                    Destination: context.Self.Position.Offset(dx, dy));
            })
            .Where(candidate =>
                CanEnter(contract.Map, candidate.Destination, occupied)
                && !hostile.Any(projectile =>
                    ReachesWithinAdvances(
                        projectile,
                        candidate.Destination,
                        maxAdvances: 2)))
            .OrderByDescending(candidate =>
                !holdingObjective
                || objectiveTiles.Contains(candidate.Destination))
            .ThenBy(candidate =>
                DistanceToObjective(
                    candidate.Destination,
                    objectiveTiles))
            .ThenByDescending(candidate =>
                hostile.Min(projectile =>
                    candidate.Destination.ChebyshevDistance(
                        projectile.Position)))
            .ThenBy(candidate =>
                Array.IndexOf(preference, candidate.Direction))
            .Select(candidate => (Direction?)candidate.Direction)
            .FirstOrDefault();
        if (selected is not Direction direction)
            return null;

        return new GenericActorDecision(
            move.ActionId,
            move.ActionCode,
            [
                new GenericActorActionArgument.DirectionArgument(direction),
            ],
            $"dodging imminent projectile toward {direction}");
    }

    public static GenericActorDecision? TryDirectShot(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(candidate =>
                string.Equals(
                    candidate.Id,
                    context.Self.FormId,
                    StringComparison.Ordinal));
        GenericActorRulesContract.AttackProfile? attack =
            form?.AttackProfileId is string attackProfileId
                ? contract.Rules.AttackProfiles.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.Id,
                        attackProfileId,
                        StringComparison.Ordinal))
                : null;
        if (attack is null || context.Enemies.IsEmpty)
            return null;

        HashSet<string> actionIds = contract.Rules.Actions
            .Where(action =>
                action.Kind == GenericActorRulesContract.ActionKind.Attack)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        GenericActorActionLegality[] actions = context.ActionLegalities
            .Where(action =>
                action.Available
                && actionIds.Contains(action.ActionId))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .ToArray();

        foreach (GenericActorContext.ObservedEnemyState target
                 in context.Enemies
                     .OrderBy(enemy => enemy.Health)
                     .ThenBy(enemy =>
                         context.Self.Position.ChebyshevDistance(
                             enemy.Position))
                     .ThenBy(enemy => enemy.ActorId))
        {
            if (!TryRay(
                    context.Self.Position,
                    target.Position,
                    out ProjectileHeading heading,
                    out int distance)
                || distance > attack.Projectile.MaxTravelTiles
                || !ClearRay(
                    contract.Map,
                    context.Self.Position,
                    target.Position,
                    attack.Projectile.DiagonalCornersMustBeClear))
            {
                continue;
            }

            foreach (GenericActorActionLegality action in actions)
            {
                GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint? headings =
                    action.Constraints
                        .OfType<GenericActorActionLegality.ArgumentConstraint
                            .ProjectileHeadingConstraint>()
                        .SingleOrDefault();
                if (headings is not null
                    && headings.AllowedValues.Contains(heading))
                {
                    return new GenericActorDecision(
                        action.ActionId,
                        action.ActionCode,
                        [
                            new GenericActorActionArgument
                                .ProjectileHeadingArgument(heading),
                        ],
                        $"direct fire at {target.ActorId}");
                }

                int aimOffset = SignedHeadingDifference(
                    context.Self.Facing.ToProjectileHeading(),
                    heading);
                if (aimOffset < attack.ShotProgram.MinInitialAimSteps
                    || aimOffset > attack.ShotProgram.MaxInitialAimSteps)
                {
                    continue;
                }
                // A facing-aligned shot needs no payload when programs are
                // optional — or when they are disabled entirely, which is
                // how straight-only chassis (their attack action declares no
                // parameters at all) fire every shot.
                if (aimOffset == 0
                    && (attack.ShotProgram.PayloadOptional
                        || !attack.ShotProgram.Enabled))
                {
                    return GenericActorDecision.WithoutArguments(
                        action.ActionId,
                        action.ActionCode,
                        $"straight fire at {target.ActorId}");
                }

                GenericActorActionLegality.ArgumentConstraint
                    .ShotProgramConstraint? programs =
                    action.Constraints
                        .OfType<GenericActorActionLegality.ArgumentConstraint
                            .ShotProgramConstraint>()
                        .SingleOrDefault();
                if (programs is not { Allowed: true }
                    || !attack.ShotProgram.Enabled)
                {
                    continue;
                }

                GenericActorRulesContract.AimOnlyShotProgramValue aimOnly =
                    attack.ShotProgram.AimOnlyProgram;
                return new GenericActorDecision(
                    action.ActionId,
                    action.ActionCode,
                    [
                        new GenericActorActionArgument.ShotProgramArgument(
                            new ShotProgram(
                                aimOffset,
                                aimOnly.BendDirection,
                                aimOnly.BendAfterTiles,
                                aimOnly.BendEveryTiles,
                                aimOnly.BendCount)),
                    ],
                    $"aimed direct fire at {target.ActorId}");
            }
        }
        return null;
    }

    public static GenericActorDecision? TryAdvanceToActiveObjective(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context,
        IEnumerable<Position>? temporarilyBlocked = null)
    {
        Position[] goals = ActiveObjectiveTiles(contract, context);
        if (goals.Length == 0
            || goals.Contains(context.Self.Position))
        {
            return null;
        }

        GenericActorActionLegality? move = AvailableAction(
            contract,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            constraint = move?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        if (move is null || constraint is null)
            return null;

        // Hostile projectiles always block or hit. Whether ALLIED bolts do is
        // the contract's allied-contact policy — read it rather than assume
        // pass-through, so this helper survives a future collision arm.
        bool alliedBoltsPass = contract.Rules.Collisions
            .AlliedProjectileContact
            .Contains("pass-through", StringComparison.Ordinal);
        HashSet<Position> occupied = Occupied(
            context,
            (context.VisibleProjectiles ?? [])
                .Where(projectile =>
                    !alliedBoltsPass
                    || projectile.OwnerTeamId
                        != context.Self.ActorId.TeamId));
        if (temporarilyBlocked is not null)
            occupied.UnionWith(temporarilyBlocked);
        if (
            context.Self.PreviousActionResolution
                is
                {
                    Outcome:
                        GenericActorActionResolution.ActionOutcome.Blocked,
                } previous
            && previous.AcceptedAction.Arguments
                .OfType<GenericActorActionArgument.DirectionArgument>()
                .SingleOrDefault()
                is { } blockedDirection)
        {
            (int dx, int dy) = blockedDirection.Value.Vector();
            occupied.Add(
                context.Self.Position.Offset(dx, dy));
        }
        Direction? step = FindFirstStep(
            contract.Map,
            context.Self.Position,
            goals.ToHashSet(),
            occupied,
            constraint.AllowedValues.ToHashSet(),
            OrderedDirections(contract, context));
        if (step is not Direction direction)
            return null;

        return new GenericActorDecision(
            move.ActionId,
            move.ActionCode,
            [
                new GenericActorActionArgument.DirectionArgument(direction),
            ],
            $"advancing toward active objective via {direction}");
    }

    public static GenericActorDecision Wait(
        GenericActorContext context,
        string reason)
    {
        // Never throw: a deliberate fault is a protocol violation, while a
        // Blocked outcome is safe. Prefer an available wait, then an
        // unavailable one, then any available parameterless action, and as
        // a last resort the first declared action.
        GenericActorActionLegality? wait = context.ActionLegalities
            .FirstOrDefault(action =>
                action.Available
                && string.Equals(
                    action.ActionId,
                    "wait",
                    StringComparison.Ordinal))
            ?? context.ActionLegalities
                .FirstOrDefault(action =>
                    string.Equals(
                        action.ActionId,
                        "wait",
                        StringComparison.Ordinal))
            ?? context.ActionLegalities
                .FirstOrDefault(action =>
                    action.Available && action.Constraints.IsEmpty)
            ?? context.ActionLegalities.First();
        return GenericActorDecision.WithoutArguments(
            wait.ActionId,
            wait.ActionCode,
            reason);
    }

    private static GenericActorActionLegality? AvailableAction(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context,
        GenericActorRulesContract.ActionKind kind)
    {
        HashSet<string> actionIds = contract.Rules.Actions
            .Where(action => action.Kind == kind)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        return context.ActionLegalities
            .Where(action =>
                action.Available
                && actionIds.Contains(action.ActionId))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static Position[] ActiveObjectiveTiles(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        if (context.Mode
                is not GenericActorContext.ModeObservationState.Frontline mode
            || contract.ModeMapBinding
                is not GenericActorResolvedMatchContract
                    .FrontlineModeMapBinding binding
            || mode.ActivePositionIndex < 0
            || mode.ActivePositionIndex
                >= binding.OrderedObjectiveRegionIds.Length)
        {
            return [];
        }

        string regionId =
            binding.OrderedObjectiveRegionIds[mode.ActivePositionIndex];
        return contract.Map.Regions
            .FirstOrDefault(region =>
                string.Equals(
                    region.RegionId,
                    regionId,
                    StringComparison.Ordinal))
            ?.Tiles
            .ToArray()
            ?? [];
    }

    private static HashSet<Position> Occupied(
        GenericActorContext context,
        IEnumerable<GenericActorContext.ObservedProjectile> projectiles) =>
        context.Allies
            .Select(ally => ally.Position)
            .Concat(context.Enemies.Select(enemy => enemy.Position))
            .Concat(projectiles.Select(projectile => projectile.Position))
            .ToHashSet();

    private static Direction? FindFirstStep(
        GenericActorMapContract map,
        Position start,
        IReadOnlySet<Position> goals,
        IReadOnlySet<Position> occupied,
        IReadOnlySet<Direction> allowedFirstSteps,
        Direction[]? searchOrder = null)
    {
        // Iteration order is the tie-break for equal-length paths — an
        // absolute order here is the same systematic side bias as an
        // absolute movement preference. Callers pass OrderedDirections.
        Direction[] order = searchOrder ?? Directions;
        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Position, Direction First)>();
        foreach (Direction direction in order)
        {
            if (!allowedFirstSteps.Contains(direction))
                continue;
            Position next = Offset(start, direction);
            if (!CanEnter(map, next, occupied)
                || !visited.Add(next))
            {
                continue;
            }
            if (goals.Contains(next))
                return direction;
            queue.Enqueue((next, direction));
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (Direction direction in order)
            {
                Position next = Offset(current.Position, direction);
                if (!CanEnter(map, next, occupied)
                    || !visited.Add(next))
                {
                    continue;
                }
                if (goals.Contains(next))
                    return current.First;
                queue.Enqueue((next, current.First));
            }
        }
        return null;
    }

    private static bool ReachesWithinAdvances(
        GenericActorContext.ObservedProjectile projectile,
        Position target,
        int maxAdvances)
    {
        if (!TryRay(
                projectile.Position,
                target,
                out ProjectileHeading heading,
                out int distance)
            || heading != projectile.Heading)
        {
            return false;
        }
        return distance <= Math.Min(
            projectile.TilesPerAdvance * maxAdvances,
            projectile.RemainingTiles);
    }

    private static bool TryRay(
        Position source,
        Position target,
        out ProjectileHeading heading,
        out int distance)
    {
        int dx = target.X - source.X;
        int dy = target.Y - source.Y;
        distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (distance == 0
            || dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy))
        {
            heading = default;
            return false;
        }

        (int StepX, int StepY) step = (Math.Sign(dx), Math.Sign(dy));
        heading = step switch
        {
            (0, -1) => ProjectileHeading.North,
            (1, -1) => ProjectileHeading.NorthEast,
            (1, 0) => ProjectileHeading.East,
            (1, 1) => ProjectileHeading.SouthEast,
            (0, 1) => ProjectileHeading.South,
            (-1, 1) => ProjectileHeading.SouthWest,
            (-1, 0) => ProjectileHeading.West,
            (-1, -1) => ProjectileHeading.NorthWest,
            _ => default,
        };
        return true;
    }

    private static bool ClearRay(
        GenericActorMapContract map,
        Position source,
        Position target,
        bool strictDiagonalCorners)
    {
        int stepX = Math.Sign(target.X - source.X);
        int stepY = Math.Sign(target.Y - source.Y);
        Position cursor = source;
        while (cursor != target)
        {
            Position next = cursor.Offset(stepX, stepY);
            if (next != target && !CanEnter(map, next, NoPositions))
                return false;
            if (strictDiagonalCorners
                && stepX != 0
                && stepY != 0
                && (
                    !CanEnter(
                        map,
                        cursor.Offset(stepX, 0),
                        NoPositions)
                    || !CanEnter(
                        map,
                        cursor.Offset(0, stepY),
                        NoPositions)
                ))
            {
                return false;
            }
            cursor = next;
        }
        return true;
    }

    private static bool CanEnter(
        GenericActorMapContract map,
        Position position,
        IReadOnlySet<Position> occupied) =>
        position.X >= 0
        && position.Y >= 0
        && position.X < map.Width
        && position.Y < map.Height
        && map.TileRows[position.Y][position.X] != '#'
        && !occupied.Contains(position);

    private static Position Offset(
        Position position,
        Direction direction)
    {
        (int dx, int dy) = direction.Vector();
        return position.Offset(dx, dy);
    }

    private static int DistanceToObjective(
        Position position,
        IReadOnlyCollection<Position> objectiveTiles) =>
        objectiveTiles.Count == 0
            ? 0
            : objectiveTiles.Min(position.ChebyshevDistance);

    private static int SignedHeadingDifference(
        ProjectileHeading from,
        ProjectileHeading to)
    {
        int difference = ((int)to - (int)from + 8) % 8;
        return difference > 4 ? difference - 8 : difference;
    }

    /// <summary>
    /// This team's advance direction as a map heading, derived from the
    /// ordered objective regions and the team's declared index direction.
    /// Null when the mode declares no advance (deathmatch, or degenerate
    /// geometry).
    /// </summary>
    public static Direction? AdvanceDirection(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        if (contract.ModeMapBinding
            is not GenericActorResolvedMatchContract
                .FrontlineModeMapBinding binding
            || binding.OrderedObjectiveRegionIds.Length < 2)
        {
            return null;
        }
        var advance = binding.TeamAdvances.FirstOrDefault(entry =>
            entry.TeamId == context.Self.ActorId.TeamId);
        if (advance is null)
            return null;

        (double X, double Y)? Centroid(string regionId)
        {
            var region = contract.Map.Regions.FirstOrDefault(entry =>
                string.Equals(
                    entry.RegionId, regionId, StringComparison.Ordinal));
            if (region is null || region.Tiles.IsEmpty)
                return null;
            return (
                region.Tiles.Average(tile => (double)tile.X),
                region.Tiles.Average(tile => (double)tile.Y));
        }

        (double X, double Y)? first =
            Centroid(binding.OrderedObjectiveRegionIds[0]);
        (double X, double Y)? last = Centroid(
            binding.OrderedObjectiveRegionIds[^1]);
        if (first is not { } from || last is not { } to)
            return null;
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        if (advance.ObjectiveIndexDelta < 0)
        {
            dx = -dx;
            dy = -dy;
        }
        if (dx == 0 && dy == 0)
            return null;
        return Math.Abs(dx) >= Math.Abs(dy)
            ? dx > 0 ? Direction.East : Direction.West
            : dy > 0 ? Direction.South : Direction.North;
    }

    /// <summary>
    /// A mirror-fair direction preference: advance first, retreat last, and
    /// the two perpendiculars ordered by the per-life deterministic random
    /// stream. An absolute order (always prefer East) gives the east-advancing
    /// team a systematic edge on a mirror-symmetric map — measured as a
    /// 40-of-40 side sweep in the wave-1 factorial — because both teams share
    /// the same absolute preference. Randomizing residual ties converts that
    /// bias into seed noise, which mirrored accounting can wash out.
    /// </summary>
    public static Direction[] OrderedDirections(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        Direction forward =
            AdvanceDirection(contract, context) ?? context.Self.Facing;
        Direction backward = Opposite(forward);
        Direction[] laterals =
            Directions.Where(direction =>
                direction != forward && direction != backward)
            .ToArray();
        if (laterals.Length == 2 && context.Random.NextBool())
            (laterals[0], laterals[1]) = (laterals[1], laterals[0]);
        return [forward, .. laterals, backward];
    }

    private static Direction Opposite(Direction direction) =>
        direction switch
        {
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.East => Direction.West,
            _ => Direction.East,
        };

    /// <summary>
    /// The class of the team controlling <paramref name="teamId"/>, derived
    /// from the contract: class chassis name every form
    /// <c>&lt;class&gt;-&lt;role&gt;</c> (the pinned convention), so the
    /// shared prefix of a team's slot forms is its class. Returns null on
    /// contracts without class chassis (the base arms) — write doctrine
    /// against the stats and routes either way; a typed classId replaces
    /// this helper's body in a later contract generation.
    /// </summary>
    public static string? ClassOf(
        GenericActorResolvedMatchContract contract,
        int teamId)
    {
        string[] prefixes = contract.LifecycleAssignments
            .Where(assignment => assignment.TeamId == teamId)
            .SelectMany(assignment => assignment.AllowedFormIds)
            .Select(formId => formId.Split('-')[0])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (prefixes.Length != 1)
            return null;
        string prefix = prefixes[0];
        bool isBaseCatalog = contract.Rules.Forms.Any(form =>
            string.Equals(form.Id, "prime-mobile", StringComparison.Ordinal));
        return isBaseCatalog ? null : prefix;
    }

    /// <summary>
    /// Contract capabilities a doctrine usually branches on, gathered in one
    /// place so bots read the contract instead of assuming an arm: whether
    /// this form's attack takes shot programs, whether any same-life route
    /// leads into a zero-weight (fortified) form, whether an explicit
    /// fabrication route exists for this form, and this slot's unlock tick.
    /// </summary>
    public static (
        bool ShotPrograms,
        bool AnchorRoute,
        bool FabricationRoute,
        int? UnlockTick)
        Capabilities(
            GenericActorResolvedMatchContract contract,
            GenericActorContext context)
    {
        string formId = context.Self.FormId;
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(candidate =>
                string.Equals(
                    candidate.Id, formId, StringComparison.Ordinal));
        GenericActorRulesContract.AttackProfile? attack =
            form?.AttackProfileId is string attackId
                ? contract.Rules.AttackProfiles.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.Id, attackId, StringComparison.Ordinal))
                : null;
        HashSet<string> fortifiedForms = contract.Rules.Forms
            .Where(candidate => candidate.ObjectiveWeight == 0)
            .Select(candidate => candidate.Id)
            .ToHashSet(StringComparer.Ordinal);
        bool anchorRoute = contract.Rules.SameLifeTransitions
            .OfType<GenericActorRulesContract.FormTransition>()
            .Any(transition =>
                string.Equals(
                    transition.SourceFormId,
                    formId,
                    StringComparison.Ordinal)
                && fortifiedForms.Contains(transition.TargetFormId));
        bool fabricationRoute = contract.Rules.FabricationTransitions
            .OfType<GenericActorRulesContract
                .BoundedChildFabricationTransition>()
            .Any(transition =>
                transition.SourceFormIds.Contains(formId));
        int? unlockTick = contract.LifecycleAssignments
            .FirstOrDefault(assignment =>
                assignment.TeamId == context.Self.ActorId.TeamId
                && assignment.UnitId == context.Self.ActorId.UnitId)
            ?.UnlockTick;
        return (
            attack?.ShotProgram.Enabled == true,
            anchorRoute,
            fabricationRoute,
            unlockTick);
    }
}
