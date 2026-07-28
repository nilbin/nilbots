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

    public static GenericActorDecision? TryInitiativeAdvance(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        Position[] goals = ActiveObjectiveTiles(contract, context);
        if (goals.Length == 0 || goals.Contains(context.Self.Position))
            return null;

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
        int currentDistance = DistanceToObjective(
            context.Self.Position,
            goals);
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
                && DistanceToObjective(candidate.Destination, goals)
                    < currentDistance
                && !hostile.Any(projectile =>
                    ReachesWithinAdvances(
                        projectile,
                        candidate.Destination,
                        maxAdvances: 1)))
            .OrderBy(candidate =>
                DistanceToObjective(candidate.Destination, goals))
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
            $"taking objective initiative toward {direction}");
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
                if (aimOffset == 0
                    && attack.ShotProgram.PayloadOptional)
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

    public static GenericActorDecision? TryCurvedShot(
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
        if (attack is null
            || !attack.ShotProgram.Enabled
            || context.Enemies.IsEmpty)
        {
            return null;
        }

        HashSet<string> actionIds = contract.Rules.Actions
            .Where(action =>
                action.Kind == GenericActorRulesContract.ActionKind.Attack)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        GenericActorActionLegality[] actions = context.ActionLegalities
            .Where(action =>
                action.Available
                && actionIds.Contains(action.ActionId)
                && action.Constraints
                    .OfType<GenericActorActionLegality.ArgumentConstraint
                        .ShotProgramConstraint>()
                    .Any(constraint => constraint.Allowed))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .ToArray();
        if (actions.Length == 0)
            return null;

        var candidates = new List<CurvedShotCandidate>();
        foreach (GenericActorContext.ObservedEnemyState target
                 in context.Enemies)
        {
            foreach (int initialAim in Enumerable.Range(
                         attack.ShotProgram.MinInitialAimSteps,
                         attack.ShotProgram.MaxInitialAimSteps
                            - attack.ShotProgram.MinInitialAimSteps + 1))
            {
                foreach (int bendDirection in attack.ShotProgram
                             .AllowedCurvedBendDirections)
                {
                    foreach (int bendAfter in Enumerable.Range(
                                 attack.ShotProgram.MinBendAfterTiles,
                                 attack.ShotProgram.MaxBendAfterTiles
                                    - attack.ShotProgram
                                        .MinBendAfterTiles + 1))
                    {
                        foreach (int bendEvery in Enumerable.Range(
                                     attack.ShotProgram
                                         .MinBendEveryTiles,
                                     attack.ShotProgram
                                         .MaxBendEveryTiles
                                        - attack.ShotProgram
                                            .MinBendEveryTiles + 1))
                        {
                            foreach (int bendCount in Enumerable.Range(
                                         attack.ShotProgram.MinBendCount,
                                         attack.ShotProgram.MaxBendCount
                                            - attack.ShotProgram
                                                .MinBendCount + 1))
                            {
                                var program = new ShotProgram(
                                    initialAim,
                                    bendDirection,
                                    bendAfter,
                                    bendEvery,
                                    bendCount);
                                IReadOnlyList<Position> path =
                                    ShotPaths.Preview(
                                        context.Self.Position,
                                        context.Self.Facing,
                                        program,
                                        attack.Projectile.MaxTravelTiles,
                                        position => IsWall(
                                            contract.Map,
                                            position));
                                int hitIndex = path
                                    .Select((position, index) =>
                                        (position, index))
                                    .Where(item =>
                                        item.position == target.Position)
                                    .Select(item => item.index)
                                    .DefaultIfEmpty(-1)
                                    .First();
                                if (hitIndex < 0)
                                    continue;
                                candidates.Add(
                                    new CurvedShotCandidate(
                                        actions[0],
                                        target.ActorId.ToString(),
                                        target.Health,
                                        program,
                                        hitIndex));
                            }
                        }
                    }
                }
            }
        }

        CurvedShotCandidate? selected = candidates
            .OrderBy(candidate => candidate.TargetHealth)
            .ThenBy(candidate => candidate.HitIndex)
            .ThenBy(candidate =>
                Math.Abs(candidate.Program.InitialAimOffset))
            .ThenBy(candidate => candidate.Program.BendCount)
            .ThenBy(candidate => candidate.Program.BendAfterTiles)
            .ThenBy(candidate => candidate.Program.BendDirection)
            .FirstOrDefault();
        if (selected is null)
            return null;

        return new GenericActorDecision(
            selected.Action.ActionId,
            selected.Action.ActionCode,
            [
                new GenericActorActionArgument.ShotProgramArgument(
                    selected.Program),
            ],
            $"previewed curved intercept at {selected.Target}");
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

        HashSet<Position> occupied = Occupied(
            context,
            context.VisibleProjectiles ?? []);
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

    private static bool IsWall(
        GenericActorMapContract map,
        Position position) =>
        position.X < 0
        || position.Y < 0
        || position.X >= map.Width
        || position.Y >= map.Height
        || map.TileRows[position.Y][position.X] == '#';

    private sealed record CurvedShotCandidate(
        GenericActorActionLegality Action,
        string Target,
        int TargetHealth,
        ShotProgram Program,
        int HitIndex);
}
