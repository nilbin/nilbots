using BotArena.Sdk;

/// <summary>
/// A contract-driven positional reference. It spends its action on a safe
/// two-step objective breach before the suppression projectile reaches its
/// final public state, then falls back to immediate evasion, mixed one-bend
/// fire, and ordinary objective pathing.
/// </summary>
public sealed class InitiativePlanner : IGenericActorBot
{
    private static readonly Direction[] Directions =
    [
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West,
    ];

    private GenericActorResolvedMatchContract? _contract;

    public void StartLife(GenericActorMatchStart start)
    {
        _contract = start.Contract;
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        GenericActorResolvedMatchContract contract = _contract
            ?? throw new InvalidOperationException(
                "StartLife was not called.");
        Position[] objective = ActiveObjectiveTiles(contract, context);

        return TryFabricate(contract, context)
            ?? TryTimedEntry(contract, context, objective)
            ?? TryImmediateEvasion(contract, context, objective)
            ?? TryMixedShot(contract, context)
            ?? TryFaceEnemy(contract, context)
            ?? TryMoveToward(contract, context, objective, "objective path")
            ?? Wait(context, "preserving position");
    }

    private static GenericActorDecision? TryTimedEntry(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context,
        IReadOnlyCollection<Position> objective)
    {
        if (objective.Count == 0
            || objective.Contains(context.Self.Position)
            || DistanceTo(
                context.Self.Position,
                objective) > 2)
        {
            return null;
        }

        return TryMoveToward(
            contract,
            context,
            objective,
            "initiative: enter before last public projectile state",
            rejectNextStraightThreat: true);
    }

    private static GenericActorDecision? TryImmediateEvasion(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context,
        IReadOnlyCollection<Position> objective)
    {
        GenericActorContext.ObservedProjectile[] hostile =
        [
            .. (context.VisibleProjectiles ?? [])
                .Where(projectile =>
                    projectile.OwnerTeamId
                        != context.Self.ActorId.TeamId),
        ];
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

        HashSet<Position> occupied = Occupied(context);
        bool onObjective = objective.Contains(context.Self.Position);
        var candidates = constraint.AllowedValues
            .Where(Directions.Contains)
            .Select(direction => (
                Direction: direction,
                Destination: Offset(context.Self.Position, direction)))
            .Where(candidate =>
                CanEnter(contract.Map, candidate.Destination, occupied))
            .Where(candidate =>
                !hostile.Any(projectile =>
                    ReachesOnNextAdvance(
                        projectile,
                        candidate.Destination)))
            .OrderByDescending(candidate =>
                !onObjective || objective.Contains(candidate.Destination))
            .ThenBy(candidate => DistanceTo(
                candidate.Destination,
                objective))
            .ThenBy(candidate => candidate.Direction)
            .ToArray();
        if (candidates.Length == 0)
            return null;

        var selected = candidates[0];
        return Move(
            move,
            selected.Direction,
            $"evade while valuing objective via {selected.Direction}");
    }

    private static GenericActorDecision? TryMixedShot(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(candidate =>
                candidate.Id == context.Self.FormId);
        GenericActorRulesContract.AttackProfile? profile =
            form?.AttackProfileId is string profileId
                ? contract.Rules.AttackProfiles.FirstOrDefault(candidate =>
                    candidate.Id == profileId)
                : null;
        GenericActorActionLegality? action = AvailableAction(
            contract,
            context,
            GenericActorRulesContract.ActionKind.Attack);
        if (profile is null || action is null)
            return null;

        GenericActorContext.ObservedEnemyState? target = context.Enemies
            .OrderBy(enemy => enemy.Health)
            .ThenBy(enemy => context.Self.Position.ChebyshevDistance(
                enemy.Position))
            .ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault(enemy => IsForwardAligned(
                context.Self.Position,
                context.Self.Facing,
                enemy.Position));
        if (target is null)
            return null;

        int distance = context.Self.Position.ChebyshevDistance(
            target.Position);
        if (distance > profile.Projectile.MaxTravelTiles)
            return null;

        GenericActorActionLegality.ArgumentConstraint.ShotProgramConstraint?
            programConstraint = action.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ShotProgramConstraint>()
                .SingleOrDefault();
        bool oneBendAvailable = programConstraint is { Allowed: true }
            && profile.ShotProgram.Enabled
            && profile.ShotProgram.MinInitialAimSteps <= 0
            && profile.ShotProgram.MaxInitialAimSteps >= 0
            && profile.ShotProgram.MinBendCount <= 1
            && profile.ShotProgram.MaxBendCount >= 1
            && distance >= 2;
        int bendDirection = oneBendAvailable
            ? context.Random.NextInt(0, 3) - 1
            : 0;
        if (bendDirection == 0)
        {
            if (!profile.ShotProgram.PayloadOptional)
                return null;
            return GenericActorDecision.WithoutArguments(
                action.ActionId,
                action.ActionCode,
                "mixed fire: straight");
        }
        if (!profile.ShotProgram.AllowedCurvedBendDirections.Contains(
                bendDirection))
        {
            return null;
        }

        int bendAfter = Math.Clamp(
            distance - 1,
            profile.ShotProgram.MinBendAfterTiles,
            profile.ShotProgram.MaxBendAfterTiles);
        var program = new ShotProgram(
            InitialAimOffset: 0,
            BendDirection: bendDirection,
            BendAfterTiles: bendAfter,
            BendEveryTiles: profile.ShotProgram.MinBendEveryTiles,
            BendCount: 1);
        IReadOnlyList<Position> path = ShotPaths.Preview(
            context.Self.Position,
            context.Self.Facing,
            program,
            profile.Projectile.MaxTravelTiles,
            position => IsWall(contract.Map, position));
        if (path.Count <= bendAfter)
        {
            return profile.ShotProgram.PayloadOptional
                ? GenericActorDecision.WithoutArguments(
                    action.ActionId,
                    action.ActionCode,
                    "mixed fire: curve blocked, use straight")
                : null;
        }

        return new GenericActorDecision(
            action.ActionId,
            action.ActionCode,
            [
                new GenericActorActionArgument.ShotProgramArgument(program),
            ],
            $"mixed fire: bend {bendDirection:+#;-#} after {bendAfter}");
    }

    private static GenericActorDecision? TryFaceEnemy(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        GenericActorContext.ObservedEnemyState? target = context.Enemies
            .OrderBy(enemy => context.Self.Position.ChebyshevDistance(
                enemy.Position))
            .ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault();
        if (target is null
            || !TryCardinalDirection(
                context.Self.Position,
                target.Position,
                out Direction direction)
            || direction == context.Self.Facing)
        {
            return null;
        }

        GenericActorActionLegality? action = AvailableAction(
            contract,
            context,
            GenericActorRulesContract.ActionKind.Rotation);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            constraint = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        if (action is null
            || constraint is null
            || !constraint.AllowedValues.Contains(direction))
        {
            return null;
        }

        return new GenericActorDecision(
            action.ActionId,
            action.ActionCode,
            [
                new GenericActorActionArgument.DirectionArgument(direction),
            ],
            $"face visible enemy {direction}");
    }

    private static GenericActorDecision? TryFabricate(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        GenericActorActionLegality? action = AvailableAction(
            contract,
            context,
            GenericActorRulesContract.ActionKind.Fabrication);
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint>()
                .SingleOrDefault();
        if (action is null
            || targets is null
            || targets.AllowedValues.IsEmpty)
        {
            return null;
        }

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
            $"activate companion {target.TeamId}:{target.UnitId}");
    }

    private static GenericActorDecision? TryMoveToward(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context,
        IReadOnlyCollection<Position> goals,
        string reason,
        bool rejectNextStraightThreat = false)
    {
        if (goals.Count == 0 || goals.Contains(context.Self.Position))
            return null;

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

        HashSet<Position> occupied = Occupied(context);
        GenericActorContext.ObservedProjectile[] hostile =
        [
            .. (context.VisibleProjectiles ?? [])
                .Where(projectile =>
                    projectile.OwnerTeamId
                        != context.Self.ActorId.TeamId),
        ];
        Direction? direction = FindFirstStep(
            contract.Map,
            context.Self.Position,
            goals.ToHashSet(),
            occupied,
            constraint.AllowedValues.ToHashSet(),
            destination =>
                !rejectNextStraightThreat
                || !hostile.Any(projectile =>
                    ReachesOnNextAdvance(projectile, destination)));
        return direction is { } step
            ? Move(move, step, reason)
            : null;
    }

    private static Direction? FindFirstStep(
        GenericActorMapContract map,
        Position start,
        IReadOnlySet<Position> goals,
        IReadOnlySet<Position> occupied,
        IReadOnlySet<Direction> allowedFirstSteps,
        Func<Position, bool> firstStepPredicate)
    {
        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Position, Direction First)>();
        foreach (Direction direction in Directions)
        {
            if (!allowedFirstSteps.Contains(direction))
                continue;
            Position next = Offset(start, direction);
            if (!CanEnter(map, next, occupied)
                || !firstStepPredicate(next)
                || !visited.Add(next))
            {
                continue;
            }
            if (goals.Contains(next))
                return direction;
            queue.Enqueue((next, direction));
        }

        while (queue.TryDequeue(out var current))
        {
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

    private static GenericActorActionLegality? AvailableAction(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context,
        GenericActorRulesContract.ActionKind kind)
    {
        HashSet<string> ids = contract.Rules.Actions
            .Where(action => action.Kind == kind)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        return context.ActionLegalities
            .Where(action =>
                action.Available && ids.Contains(action.ActionId))
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
                region.RegionId == regionId)
            ?.Tiles
            .ToArray()
            ?? [];
    }

    private static HashSet<Position> Occupied(
        GenericActorContext context) =>
        context.Allies
            .Select(ally => ally.Position)
            .Concat(context.Enemies.Select(enemy => enemy.Position))
            .Concat((context.VisibleProjectiles ?? [])
                .Select(projectile => projectile.Position))
            .ToHashSet();

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

        heading = (Math.Sign(dx), Math.Sign(dy)) switch
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

    private static bool TryCardinalDirection(
        Position source,
        Position target,
        out Direction direction)
    {
        int dx = target.X - source.X;
        int dy = target.Y - source.Y;
        if (dx == 0 && dy != 0)
        {
            direction = dy < 0 ? Direction.North : Direction.South;
            return true;
        }
        if (dy == 0 && dx != 0)
        {
            direction = dx < 0 ? Direction.West : Direction.East;
            return true;
        }
        direction = default;
        return false;
    }

    private static bool IsForwardAligned(
        Position source,
        Direction facing,
        Position target)
    {
        (int dx, int dy) = facing.Vector();
        int targetX = target.X - source.X;
        int targetY = target.Y - source.Y;
        return dx == 0
            ? targetX == 0 && Math.Sign(targetY) == dy
            : targetY == 0 && Math.Sign(targetX) == dx;
    }

    private static bool CanEnter(
        GenericActorMapContract map,
        Position position,
        IReadOnlySet<Position> occupied) =>
        !IsWall(map, position) && !occupied.Contains(position);

    private static bool IsWall(
        GenericActorMapContract map,
        Position position) =>
        position.X < 0
        || position.Y < 0
        || position.X >= map.Width
        || position.Y >= map.Height
        || map.TileRows[position.Y][position.X] == '#';

    private static Position Offset(
        Position position,
        Direction direction)
    {
        (int dx, int dy) = direction.Vector();
        return position.Offset(dx, dy);
    }

    private static int DistanceTo(
        Position position,
        IReadOnlyCollection<Position> targets) =>
        targets.Count == 0
            ? 0
            : targets.Min(position.ChebyshevDistance);

    private static GenericActorDecision Move(
        GenericActorActionLegality action,
        Direction direction,
        string reason) =>
        new(
            action.ActionId,
            action.ActionCode,
            [
                new GenericActorActionArgument.DirectionArgument(direction),
            ],
            reason);

    private static GenericActorDecision Wait(
        GenericActorContext context,
        string reason)
    {
        GenericActorActionLegality wait = context.ActionLegalities
            .Single(action =>
                action.Available && action.ActionId == "wait");
        return GenericActorDecision.WithoutArguments(
            wait.ActionId,
            wait.ActionCode,
            reason);
    }
}
