using BotArena.Sdk;

/// <summary>
/// Selects one visible opponent, predicts its next objective-seeking step,
/// and commits a straight or single-bend mobile projectile through the
/// opponent's current or predicted tile.
/// </summary>
public sealed class CurvePredictor : IGenericActorBot
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

        if (context.Self.PendingSameLifeTransition is not null)
            return ArenaBasics.Wait(context, "waiting for transition");

        return ArenaBasics.TryDodge(contract, context)
            ?? TryPredictiveShot(contract, context)
            ?? ArenaBasics.TryFabricateReady(contract, context)
            ?? TryOrientForPrediction(contract, context)
            ?? ArenaBasics.TryAdvanceToActiveObjective(contract, context)
            ?? ArenaBasics.Wait(context, "holding objective pressure");
    }

    private static GenericActorDecision? TryPredictiveShot(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        GenericActorActionLegality? action = MobileShotAction(context);
        GenericActorRulesContract.AttackProfile? attack =
            AttackProfile(contract, context.Self.FormId);
        Position[] objectiveTiles = ActiveObjectiveTiles(contract, context);
        GenericActorContext.ObservedEnemyState? target =
            SelectTarget(contract, context, objectiveTiles);
        if (action is null || attack is null || target is null)
            return null;

        Position predicted = PredictNextPosition(
            contract,
            context,
            target,
            objectiveTiles);
        ShotPlan? plan = BestPlan(
            contract,
            context.Self.Position,
            context.Self.Facing,
            attack,
            target.Position,
            predicted);
        if (plan is null)
            return null;

        string debug = plan.PredictedHit
            ? $"committing {plan.Label} through predicted {predicted}"
            : $"committing {plan.Label} through current {target.Position}";
        if (!plan.Bent && attack.ShotProgram.PayloadOptional)
        {
            return GenericActorDecision.WithoutArguments(
                action.ActionId,
                action.ActionCode,
                debug);
        }

        GenericActorActionLegality.ArgumentConstraint.ShotProgramConstraint?
            programs = action.Constraints
                .OfType<
                    GenericActorActionLegality.ArgumentConstraint
                        .ShotProgramConstraint>()
                .SingleOrDefault();
        if (programs is not { Allowed: true })
            return null;

        return new GenericActorDecision(
            action.ActionId,
            action.ActionCode,
            [
                new GenericActorActionArgument.ShotProgramArgument(
                    plan.Program),
            ],
            debug);
    }

    private static GenericActorDecision? TryOrientForPrediction(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        if (MobileShotAction(context, requireAvailable: false) is null)
            return null;

        GenericActorRulesContract.AttackProfile? attack =
            AttackProfile(contract, context.Self.FormId);
        Position[] objectiveTiles = ActiveObjectiveTiles(contract, context);
        GenericActorContext.ObservedEnemyState? target =
            SelectTarget(contract, context, objectiveTiles);
        GenericActorActionLegality? rotate = context.ActionLegalities
            .Where(action => action.Available)
            .FirstOrDefault(action =>
                action.Constraints.Any(constraint =>
                    constraint
                        is GenericActorActionLegality.ArgumentConstraint
                            .DirectionConstraint)
                && string.Equals(
                    action.ActionId,
                    "rotate",
                    StringComparison.Ordinal));
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            allowed = rotate?.Constraints
                .OfType<
                    GenericActorActionLegality.ArgumentConstraint
                        .DirectionConstraint>()
                .SingleOrDefault();
        if (attack is null
            || target is null
            || rotate is null
            || allowed is null)
        {
            return null;
        }

        Position predicted = PredictNextPosition(
            contract,
            context,
            target,
            objectiveTiles);
        var orientation = allowed.AllowedValues
            .Where(direction => direction != context.Self.Facing)
            .Select(direction => new
            {
                Direction = direction,
                Plan = BestPlan(
                    contract,
                    context.Self.Position,
                    direction,
                    attack,
                    target.Position,
                    predicted),
            })
            .Where(candidate => candidate.Plan is not null)
            .OrderByDescending(candidate =>
                candidate.Plan!.PredictedHit)
            .ThenByDescending(candidate => candidate.Plan!.Bent)
            .ThenBy(candidate => candidate.Plan!.ImpactIndex)
            .ThenBy(candidate => candidate.Direction)
            .FirstOrDefault();
        if (orientation is null)
            return null;

        return new GenericActorDecision(
            rotate.ActionId,
            rotate.ActionCode,
            [
                new GenericActorActionArgument.DirectionArgument(
                    orientation.Direction),
            ],
            $"orienting for {orientation.Plan!.Label} at {predicted}");
    }

    private static GenericActorContext.ObservedEnemyState? SelectTarget(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context,
        IReadOnlyCollection<Position> objectiveTiles) =>
        context.Enemies
            .Where(enemy => ObjectiveWeight(contract, enemy.FormId) > 0)
            .OrderByDescending(enemy =>
                objectiveTiles.Contains(enemy.Position))
            .ThenBy(enemy =>
                DistanceToObjective(enemy.Position, objectiveTiles))
            .ThenBy(enemy =>
                context.Self.Position.ChebyshevDistance(enemy.Position))
            .ThenBy(enemy => enemy.Health)
            .ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault()
        ?? context.Enemies
            .OrderBy(enemy =>
                context.Self.Position.ChebyshevDistance(enemy.Position))
            .ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault();

    private static Position PredictNextPosition(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context,
        GenericActorContext.ObservedEnemyState target,
        IReadOnlyCollection<Position> objectiveTiles)
    {
        if (target.PendingSameLifeTransition is not null
            || objectiveTiles.Count == 0
            || objectiveTiles.Contains(target.Position)
            || ObjectiveWeight(contract, target.FormId) <= 0)
        {
            return target.Position;
        }

        HashSet<Position> occupied = context.Allies
            .Select(ally => ally.Position)
            .Append(context.Self.Position)
            .Concat(
                context.Enemies
                    .Where(enemy => enemy.ActorId != target.ActorId)
                    .Select(enemy => enemy.Position))
            .Concat(
                context.VisibleProjectiles
                    ?.Select(projectile => projectile.Position)
                ?? [])
            .ToHashSet();
        Direction? step = FindFirstStep(
            contract.Map,
            target.Position,
            objectiveTiles.ToHashSet(),
            occupied);
        if (step is not Direction direction)
            return target.Position;

        var (dx, dy) = direction.Vector();
        return target.Position.Offset(dx, dy);
    }

    private static ShotPlan? BestPlan(
        GenericActorResolvedMatchContract contract,
        Position origin,
        Direction facing,
        GenericActorRulesContract.AttackProfile attack,
        Position current,
        Position predicted)
    {
        var candidates = new List<ShotPlan>();
        AddPlan(
            candidates,
            contract.Map,
            origin,
            facing,
            attack,
            ShotProgram.Straight,
            bent: false,
            current,
            predicted,
            "straight shot");

        GenericActorRulesContract.ShotProgramDefinition limits =
            attack.ShotProgram;
        if (limits.Enabled
            && limits.HeadingSectors == 8
            && limits.BendStepSectors == 1
            && limits.MinInitialAimSteps <= 0
            && limits.MaxInitialAimSteps >= 0
            && limits.MinBendCount <= 1
            && limits.MaxBendCount >= 1)
        {
            int firstBend = Math.Max(1, limits.MinBendAfterTiles);
            int lastBend = Math.Min(4, limits.MaxBendAfterTiles);
            int bendEvery = Math.Max(1, limits.MinBendEveryTiles);
            if (bendEvery <= limits.MaxBendEveryTiles)
            {
                foreach (int bendDirection
                    in limits.AllowedCurvedBendDirections
                        .Where(direction => direction is -1 or 1)
                        .Order())
                {
                    for (int bendAfter = firstBend;
                         bendAfter <= lastBend;
                         bendAfter++)
                    {
                        ShotProgram program = new(
                            InitialAimOffset: 0,
                            BendDirection: bendDirection,
                            BendAfterTiles: bendAfter,
                            BendEveryTiles: bendEvery,
                            BendCount: 1);
                        AddPlan(
                            candidates,
                            contract.Map,
                            origin,
                            facing,
                            attack,
                            program,
                            bent: true,
                            current,
                            predicted,
                            $"one-bend shot {bendDirection:+#;-#} after {bendAfter}");
                    }
                }
            }
        }

        bool distinctPrediction = predicted != current;
        return candidates
            .OrderByDescending(candidate =>
                distinctPrediction && candidate.PredictedHit)
            .ThenByDescending(candidate =>
                distinctPrediction
                && candidate.PredictedHit
                && candidate.Bent)
            .ThenBy(candidate => candidate.ImpactIndex)
            .ThenBy(candidate => candidate.Bent)
            .ThenBy(candidate => candidate.Program.BendAfterTiles)
            .ThenBy(candidate => candidate.Program.BendDirection)
            .FirstOrDefault();
    }

    private static void AddPlan(
        ICollection<ShotPlan> candidates,
        GenericActorMapContract map,
        Position origin,
        Direction facing,
        GenericActorRulesContract.AttackProfile attack,
        ShotProgram program,
        bool bent,
        Position current,
        Position predicted,
        string label)
    {
        IReadOnlyList<Position> path = ShotPaths.Preview(
            origin,
            facing,
            program,
            attack.Projectile.MaxTravelTiles,
            position => IsWall(map, position));
        int predictedIndex = IndexOf(path, predicted);
        int currentIndex = IndexOf(path, current);
        bool predictedHit = predictedIndex >= 0;
        int impactIndex = predictedHit
            ? predictedIndex
            : currentIndex;
        if (impactIndex < 0)
            return;

        candidates.Add(
            new ShotPlan(
                program,
                bent,
                predictedHit,
                impactIndex,
                label));
    }

    private static GenericActorActionLegality? MobileShotAction(
        GenericActorContext context,
        bool requireAvailable = true) =>
        context.ActionLegalities
            .Where(action => !requireAvailable || action.Available)
            .Where(action =>
                action.Constraints.Any(constraint =>
                    constraint
                        is GenericActorActionLegality.ArgumentConstraint
                            .ShotProgramConstraint))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();

    private static GenericActorRulesContract.AttackProfile? AttackProfile(
        GenericActorResolvedMatchContract contract,
        string formId)
    {
        string? profileId = contract.Rules.Forms
            .FirstOrDefault(form =>
                string.Equals(
                    form.Id,
                    formId,
                    StringComparison.Ordinal))
            ?.AttackProfileId;
        return profileId is null
            ? null
            : contract.Rules.AttackProfiles.FirstOrDefault(profile =>
                string.Equals(
                    profile.Id,
                    profileId,
                    StringComparison.Ordinal));
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

    private static Direction? FindFirstStep(
        GenericActorMapContract map,
        Position start,
        IReadOnlySet<Position> goals,
        IReadOnlySet<Position> occupied)
    {
        if (goals.Contains(start))
            return null;

        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Position, Direction First)>();
        foreach (Direction direction in Directions)
        {
            Position next = Offset(start, direction);
            if (!CanEnter(map, next, occupied) || !visited.Add(next))
                continue;
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

    private static int IndexOf(
        IReadOnlyList<Position> path,
        Position target)
    {
        for (int index = 0; index < path.Count; index++)
        {
            if (path[index] == target)
                return index;
        }
        return -1;
    }

    private static bool CanEnter(
        GenericActorMapContract map,
        Position position,
        IReadOnlySet<Position> occupied) =>
        !IsWall(map, position)
        && !occupied.Contains(position);

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
        var (dx, dy) = direction.Vector();
        return position.Offset(dx, dy);
    }

    private static int DistanceToObjective(
        Position position,
        IReadOnlyCollection<Position> objectiveTiles) =>
        objectiveTiles.Count == 0
            ? int.MaxValue
            : objectiveTiles.Min(position.ChebyshevDistance);

    private sealed record ShotPlan(
        ShotProgram Program,
        bool Bent,
        bool PredictedHit,
        int ImpactIndex,
        string Label);
}
