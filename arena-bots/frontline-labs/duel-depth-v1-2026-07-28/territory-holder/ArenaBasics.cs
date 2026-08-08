using BotArena.Sdk;

/// <summary>
/// Contract-driven tactical building blocks for the generated starter.
/// Keep or replace them as the bot develops; strategy belongs in TerritoryHolder.Tick.
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
                ReachesOnNextAdvance(
                    projectile,
                    context.Self.Position)))
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
                && (
                    !holdingObjective
                    || objectiveTiles.Contains(candidate.Destination)
                )
                && !hostile.Any(projectile =>
                    ReachesOnNextAdvance(
                        projectile,
                        candidate.Destination)))
            .OrderBy(candidate =>
                DistanceToObjective(
                    candidate.Destination,
                    objectiveTiles))
            .ThenByDescending(candidate =>
                hostile.Min(projectile =>
                    candidate.Destination.ChebyshevDistance(
                        projectile.Position)))
            .ThenBy(candidate => candidate.Direction)
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

    public static GenericActorDecision? TryTerritorialRiskResponse(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        GenericActorContext.ObservedProjectile[] imminent =
            context.VisibleProjectiles
                ?.Where(projectile =>
                    projectile.OwnerTeamId
                        != context.Self.ActorId.TeamId
                    && ReachesOnNextAdvance(
                        projectile,
                        context.Self.Position))
                .OrderBy(projectile => projectile.ProjectileId)
                .ToArray()
            ?? [];
        if (imminent.Length == 0)
            return null;

        int worstDeclaredHit = contract.Rules.AttackProfiles
            .Select(profile => profile.Projectile.DamagePerHit)
            .DefaultIfEmpty(int.MaxValue)
            .Max();
        bool canAbsorbOne =
            context.Self.Health > worstDeclaredHit;
        if (imminent.Length == 1
            && canAbsorbOne
            && IsSoleMobileObjectiveHolder(contract, context))
        {
            return TryDirectShot(contract, context)
                ?? Wait(
                    context,
                    "holding objective through one survivable projectile");
        }

        return TryDodge(contract, context);
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
                if (aimOffset != 0)
                    continue;
                if (attack.ShotProgram.PayloadOptional)
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
                    || !attack.ShotProgram.Enabled
                    || attack.ShotProgram.MinInitialAimSteps > 0
                    || attack.ShotProgram.MaxInitialAimSteps < 0)
                {
                    continue;
                }

                return new GenericActorDecision(
                    action.ActionId,
                    action.ActionCode,
                    [
                        new GenericActorActionArgument.ShotProgramArgument(
                            ShotProgram.Straight),
                    ],
                    $"straight fire at {target.ActorId}");
            }
        }
        return null;
    }

    public static GenericActorDecision? TryAdvanceToActiveObjective(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
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

        HashSet<Position> occupied = Occupied(
            context,
            context.VisibleProjectiles ?? []);
        Direction? step = FindFirstStep(
            contract.Map,
            context.Self.Position,
            goals.ToHashSet(),
            occupied,
            constraint.AllowedValues.ToHashSet());
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
        GenericActorActionLegality wait = context.ActionLegalities
            .Where(action => action.Available)
            .FirstOrDefault(action =>
                string.Equals(
                    action.ActionId,
                    "wait",
                    StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "No available wait action.");
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
        IReadOnlySet<Direction> allowedFirstSteps)
    {
        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Position, Direction First)>();
        foreach (Direction direction in Directions)
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
            foreach (Direction direction in Directions)
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

    private static bool ReachesOnNextAdvance(
        GenericActorContext.ObservedProjectile projectile,
        Position target)
    {
        if (projectile.TicksUntilAdvance != 1
            || !TryRay(
                projectile.Position,
                target,
                out ProjectileHeading heading,
                out int distance)
            || heading != projectile.Heading)
        {
            return false;
        }
        return distance <= Math.Min(
            projectile.TilesPerAdvance,
            projectile.RemainingTiles);
    }

    private static bool IsSoleMobileObjectiveHolder(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        if (context.Mode
                is not GenericActorContext.ModeObservationState.Frontline mode
            || context.Tick < mode.ControlResumesAtTick)
        {
            return false;
        }

        Position[] objectiveTiles =
            ActiveObjectiveTiles(contract, context);
        if (!objectiveTiles.Contains(context.Self.Position)
            || ObjectiveWeight(contract, context.Self.FormId) <= 0)
        {
            return false;
        }

        return !context.Allies.Any(ally =>
            objectiveTiles.Contains(ally.Position)
            && ObjectiveWeight(contract, ally.FormId) > 0);
    }

    private static int ObjectiveWeight(
        GenericActorResolvedMatchContract contract,
        string formId) =>
        contract.Rules.Forms
            .FirstOrDefault(form =>
                string.Equals(
                    form.Id,
                    formId,
                    StringComparison.Ordinal))
            ?.ObjectiveWeight
        ?? 0;

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
}
