using BotArena.Sdk;

/// <summary>
/// Builds durable numerical advantage, then distributes mobile bodies between
/// objective control and surrounding firing lanes.
/// </summary>
public sealed class Fabricator : IGenericActorBot
{
    private static readonly Direction[] CardinalDirections =
    [
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West,
    ];
    private static readonly HashSet<Position> EmptyPositions = [];

    private GenericActorResolvedMatchContract? _contract;
    private GenericActorResolvedMatchContract.FrontlineModeMapBinding?
        _frontline;
    private ActorIdentity? _actorId;
    private Position? _homeSpawn;
    private HashSet<Position> _homePad = [];

    public void StartLife(GenericActorMatchStart start)
    {
        _contract = start.Contract;
        _frontline = start.Contract.ModeMapBinding
            as GenericActorResolvedMatchContract.FrontlineModeMapBinding;
        _actorId = start.ActorId;
        _homeSpawn = FindHomeSpawn(start);
        _homePad = FindHomePad(start.Contract.Map, _homeSpawn);
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        GenericActorResolvedMatchContract? contract = _contract;
        if (contract is null
            || _frontline is null
            || context.Mode is not
                GenericActorContext.ModeObservationState.Frontline frontline
            || !TryObjective(contract, _frontline, frontline, out var objective))
        {
            return Fallback(context, "contract-safe fallback");
        }

        if (context.Self.PendingSameLifeTransition is not null)
            return Fallback(context, "finishing transition");

        List<GenericActorActionArgument.UnitTarget> readyTargets =
            ReadyFabricationTargets(contract, context);
        bool fabricationWorthwhile = readyTargets.Count > 0
            && FabricationWorthwhile(
                contract,
                context,
                objective,
                readyTargets.Count);

        if (fabricationWorthwhile)
        {
            GenericActorDecision? fabricate = Fabricate(context, readyTargets);
            if (fabricate is not null)
                return fabricate;

            if (!ShouldFinishCapture(contract, context, frontline, objective))
            {
                GenericActorDecision? coveringFire =
                    MobileLineFire(contract, context);
                if (coveringFire is not null)
                    return coveringFire;

                GenericActorDecision? returnHome =
                    MoveTowardHome(contract.Map, context);
                if (returnHome is not null)
                    return returnHome;
            }
        }

        GenericActorDecision? turretFire = TurretFire(context);
        if (turretFire is not null)
            return turretFire;

        GenericActorDecision? mobileFire = MobileLineFire(contract, context);
        if (mobileFire is not null)
            return mobileFire;

        if (!HasFabricationPath(contract)
            && ReplicationWorthwhile(contract, context, objective))
        {
            GenericActorDecision? split = WithoutArguments(
                context,
                "split",
                "replicating where fabrication is unavailable");
            if (split is not null)
                return split;
        }

        Position? formationGoal = FormationGoal(
            contract,
            _frontline,
            context,
            frontline,
            objective);
        if (formationGoal is Position goal)
        {
            GenericActorDecision? move = MoveToward(
                contract.Map,
                context,
                new HashSet<Position> { goal },
                $"taking mobile crossfire post {goal}");
            if (move is not null)
                return move;
        }

        Direction? watchDirection = EnemyApproachDirection(
            contract,
            _frontline,
            frontline,
            objective,
            context.Self.ActorId.TeamId);
        if (watchDirection is Direction direction
            && direction != context.Self.Facing)
        {
            GenericActorDecision? rotate = WithDirection(
                context,
                "rotate",
                direction,
                $"watching {direction} approach");
            if (rotate is not null)
                return rotate;
        }

        return Fallback(context, $"holding {objective.RegionId}");
    }

    private static bool TryObjective(
        GenericActorResolvedMatchContract contract,
        GenericActorResolvedMatchContract.FrontlineModeMapBinding binding,
        GenericActorContext.ModeObservationState.Frontline frontline,
        out GenericActorMapContract.Region objective)
    {
        objective = null!;
        if (frontline.ActivePositionIndex < 0
            || frontline.ActivePositionIndex
                >= binding.OrderedObjectiveRegionIds.Length)
        {
            return false;
        }

        string regionId =
            binding.OrderedObjectiveRegionIds[frontline.ActivePositionIndex];
        GenericActorMapContract.Region? match = contract.Map.Regions
            .FirstOrDefault(region =>
                string.Equals(
                    region.RegionId,
                    regionId,
                    StringComparison.Ordinal));
        if (match is null || match.Tiles.Length == 0)
            return false;

        objective = match;
        return true;
    }

    private static List<GenericActorActionArgument.UnitTarget>
        ReadyFabricationTargets(
            GenericActorResolvedMatchContract contract,
            GenericActorContext context)
    {
        HashSet<string> outputForms = contract.Rules.FabricationTransitions
            .Where(transition =>
                string.Equals(
                    transition.ActionId,
                    "fabricate",
                    StringComparison.Ordinal))
            .OfType<
                GenericActorRulesContract
                    .BoundedChildFabricationTransition>()
            .Select(transition => transition.OutputFormId)
            .ToHashSet(StringComparer.Ordinal);
        if (outputForms.Count == 0)
            return [];

        int teamId = context.Self.ActorId.TeamId;
        return context.TeamUnits
            .Where(slot =>
                slot.TeamId == teamId
                && slot.State is GenericActorContext.UnitSlotState.Ready)
            .Where(slot =>
                contract.LifecycleAssignments.Any(assignment =>
                    assignment.TeamId == slot.TeamId
                    && assignment.UnitId == slot.UnitId
                    && assignment.AllowedFormIds.Any(outputForms.Contains)))
            .OrderBy(slot => slot.UnitId)
            .Select(slot =>
                new GenericActorActionArgument.UnitTarget(
                    slot.TeamId,
                    slot.UnitId))
            .ToList();
    }

    private bool FabricationWorthwhile(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context,
        GenericActorMapContract.Region objective,
        int readyTargetCount)
    {
        if (readyTargetCount <= 0)
            return false;

        int remainingTicks = contract.Rules.Limits.MaxTicks - context.Tick;
        int travelHome = _homePad.Count > 0
            ? ShortestDistance(
                contract.Map,
                context.Self.Position,
                _homePad)
            : 0;
        int travelBack = _homePad.Count > 0
            ? ShortestDistance(
                contract.Map,
                _homePad,
                objective.Tiles.ToHashSet())
            : 0;
        if (travelHome == int.MaxValue || travelBack == int.MaxValue)
            return false;

        int windup = contract.Rules.FabricationTransitions
            .Where(transition =>
                string.Equals(
                    transition.ActionId,
                    "fabricate",
                    StringComparison.Ordinal))
            .OfType<
                GenericActorRulesContract
                    .BoundedChildFabricationTransition>()
            .Where(transition =>
                transition.SourceFormIds.Contains(
                    context.Self.FormId,
                    StringComparer.Ordinal))
            .Select(transition => transition.Delay.DurationTicks)
            .DefaultIfEmpty(0)
            .Max();
        int usefulControlWindow =
            contract.Rules.GameMode is
                GenericActorRulesContract.FrontlineGameMode mode
                ? mode.Capture.Threshold
                : 0;

        long investment = (long)travelHome
            + windup
            + travelBack
            + usefulControlWindow;
        return remainingTicks > investment;
    }

    private static bool ShouldFinishCapture(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context,
        GenericActorContext.ModeObservationState.Frontline frontline,
        GenericActorMapContract.Region objective)
    {
        if (frontline.ClaimingTeamId != context.Self.ActorId.TeamId
            || frontline.CaptureProgress <= 0
            || !objective.Tiles.Contains(context.Self.Position)
            || contract.Rules.GameMode is not
                GenericActorRulesContract.FrontlineGameMode mode)
        {
            return false;
        }

        bool anotherController = context.Allies.Any(ally =>
            objective.Tiles.Contains(ally.Position)
            && ObjectiveWeight(contract, ally.FormId) > 0);
        if (anotherController)
            return false;

        int remainingProgress = Math.Max(
            0,
            mode.Capture.Threshold - frontline.CaptureProgress);
        int gain = Math.Max(1, mode.Capture.GainPerSoleTeamTick);
        int ticksToFinish = (remainingProgress + gain - 1) / gain;
        return ticksToFinish <= mode.Capture.Threshold;
    }

    private static GenericActorDecision? Fabricate(
        GenericActorContext context,
        IReadOnlyCollection<GenericActorActionArgument.UnitTarget>
            preferredTargets)
    {
        GenericActorActionLegality? legality = Available(
            context,
            "fabricate");
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            constraint = legality?.Constraints
                .OfType<
                    GenericActorActionLegality.ArgumentConstraint
                        .UnitTargetConstraint>()
                .FirstOrDefault();
        if (legality is null || constraint is null)
            return null;

        GenericActorActionArgument.UnitTarget? target =
            constraint.AllowedValues
                .Where(preferredTargets.Contains)
                .OrderBy(value => value.TeamId)
                .ThenBy(value => value.UnitId)
                .Cast<GenericActorActionArgument.UnitTarget?>()
                .FirstOrDefault();
        if (target is not { } selected)
            return null;

        return new GenericActorDecision(
            legality.ActionId,
            legality.ActionCode,
            [new GenericActorActionArgument.UnitTargetArgument(selected)],
            $"fabricating ready slot {selected.TeamId}:{selected.UnitId}");
    }

    private GenericActorDecision? MoveTowardHome(
        GenericActorMapContract map,
        GenericActorContext context)
    {
        if (_homePad.Count == 0)
            return null;

        return MoveToward(
            map,
            context,
            _homePad,
            "returning to protected pad to fabricate");
    }

    private static GenericActorDecision? MoveToward(
        GenericActorMapContract map,
        GenericActorContext context,
        IReadOnlySet<Position> goals,
        string debug)
    {
        if (goals.Contains(context.Self.Position))
            return null;

        HashSet<Position> occupied = context.Allies
            .Select(ally => ally.Position)
            .Concat(context.Enemies.Select(enemy => enemy.Position))
            .ToHashSet();
        if (context.VisibleProjectiles is { } projectiles)
        {
            occupied.UnionWith(
                projectiles.Select(projectile => projectile.Position));
        }

        Direction? step = FindFirstStep(
            map,
            context.Self.Position,
            goals,
            occupied);
        return step is Direction direction
            ? WithDirection(context, "move", direction, debug)
            : null;
    }

    private static GenericActorDecision? MobileLineFire(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        GenericActorActionLegality? shoot = Available(context, "shoot");
        if (shoot is null)
            return null;

        int range = AttackRange(contract, context.Self.FormId);
        List<(GenericActorContext.ObservedEnemyState Enemy,
            Direction Direction)> targets = context.Enemies
            .Select(enemy =>
                (Enemy: enemy,
                    Direction: AlignedDirection(
                        context.Self.Position,
                        enemy.Position)))
            .Where(candidate => candidate.Direction is not null)
            .Select(candidate =>
                (candidate.Enemy, candidate.Direction!.Value))
            .Where(candidate =>
                CardinalDistance(
                    context.Self.Position,
                    candidate.Enemy.Position) <= range)
            .Where(candidate =>
                ClearCardinalRay(
                    contract.Map,
                    context.Self.Position,
                    candidate.Enemy.Position))
            .OrderBy(candidate => candidate.Enemy.Health)
            .ThenBy(candidate =>
                CardinalDistance(
                    context.Self.Position,
                    candidate.Enemy.Position))
            .ThenBy(candidate => candidate.Enemy.ActorId)
            .ToList();
        if (targets.Count == 0)
            return null;

        int firingRank = TeamBodyRank(context);
        var target = targets[firingRank % targets.Count];
        if (target.Direction == context.Self.Facing)
        {
            return new GenericActorDecision(
                shoot.ActionId,
                shoot.ActionCode,
                [],
                $"crossfire at {target.Enemy.ActorId}");
        }

        return WithDirection(
            context,
            "rotate",
            target.Direction,
            $"aligning on {target.Enemy.ActorId}");
    }

    private static GenericActorDecision? TurretFire(
        GenericActorContext context)
    {
        GenericActorActionLegality? legality = Available(
            context,
            "shoot-direction");
        GenericActorActionLegality.ArgumentConstraint
            .ProjectileHeadingConstraint? constraint =
                legality?.Constraints
                    .OfType<
                        GenericActorActionLegality.ArgumentConstraint
                            .ProjectileHeadingConstraint>()
                    .FirstOrDefault();
        if (legality is null || constraint is null || context.Enemies.Length == 0)
            return null;

        List<(GenericActorContext.ObservedEnemyState Enemy,
            ProjectileHeading Heading)> targets = context.Enemies
            .Select(enemy =>
                (Enemy: enemy,
                    Heading: ExactHeading(
                        context.Self.Position,
                        enemy.Position)))
            .Where(candidate => candidate.Heading is not null)
            .Select(candidate =>
                (Enemy: candidate.Enemy,
                    Heading: candidate.Heading!.Value))
            .Where(candidate =>
                constraint.AllowedValues.Contains(candidate.Heading))
            .OrderBy(candidate => candidate.Enemy.Health)
            .ThenBy(candidate =>
                context.Self.Position.ChebyshevDistance(
                    candidate.Enemy.Position))
            .ThenBy(candidate => candidate.Enemy.ActorId)
            .ToList();
        if (targets.Count == 0)
            return null;

        var target = targets[TeamBodyRank(context) % targets.Count];
        return new GenericActorDecision(
            legality.ActionId,
            legality.ActionCode,
            [
                new GenericActorActionArgument.ProjectileHeadingArgument(
                    target.Heading),
            ],
            $"absolute crossfire at {target.Enemy.ActorId}");
    }

    private static Position? FormationGoal(
        GenericActorResolvedMatchContract contract,
        GenericActorResolvedMatchContract.FrontlineModeMapBinding binding,
        GenericActorContext context,
        GenericActorContext.ModeObservationState.Frontline frontline,
        GenericActorMapContract.Region objective)
    {
        List<Body> bodies = context.Allies
            .Select(ally =>
                new Body(ally.ActorId, ally.FormId, ally.Position))
            .Append(
                new Body(
                    context.Self.ActorId,
                    context.Self.FormId,
                    context.Self.Position))
            .Where(body =>
                ObjectiveWeight(contract, body.FormId) > 0
                && FormAllows(contract, body.FormId, "move"))
            .OrderBy(body =>
                ShortestDistance(
                    contract.Map,
                    body.Position,
                    objective.Tiles.ToHashSet()))
            .ThenBy(body => body.ActorId)
            .ToList();
        int selfIndex = bodies.FindIndex(body =>
            body.ActorId == context.Self.ActorId);
        if (selfIndex < 0)
            return null;

        HashSet<Position> objectiveTiles = objective.Tiles
            .Where(position =>
                CanEnter(contract.Map, position, EmptyPositions))
            .ToHashSet();
        if (objectiveTiles.Count == 0)
            return null;

        HashSet<Position> perimeter = objectiveTiles
            .SelectMany(position =>
                CardinalDirections.Select(direction =>
                    Offset(position, direction)))
            .Where(position =>
                !objectiveTiles.Contains(position)
                && CanEnter(
                    contract.Map,
                    position,
                    EmptyPositions))
            .ToHashSet();
        GenericActorResolvedMatchContract.FrontlineTeamAdvance? advance =
            binding.TeamAdvances.FirstOrDefault(candidate =>
                candidate.TeamId == context.Self.ActorId.TeamId);
        GenericActorMapContract.Region? nextObjective = advance is null
            ? null
            : ObjectiveAt(
                contract,
                binding,
                frontline.ActivePositionIndex
                    + advance.ObjectiveIndexDelta);
        HashSet<Position> nextTiles = nextObjective?.Tiles
            .Where(position => CanEnter(
                contract.Map,
                position,
                EmptyPositions))
            .ToHashSet() ?? [];
        bool secureClaim =
            contract.Rules.GameMode is
                GenericActorRulesContract.FrontlineGameMode mode
            && frontline.ClaimingTeamId == context.Self.ActorId.TeamId
            && frontline.CaptureProgress
                >= Math.Max(1, mode.Capture.Threshold / 2)
            && bodies.Count > 1
            && bodies.Any(body =>
                objectiveTiles.Contains(body.Position))
            && !context.Enemies.Any(enemy =>
                objectiveTiles.Contains(enemy.Position)
                && ObjectiveWeight(contract, enemy.FormId) > 0)
            && nextTiles.Count > 0;
        int forwardIndex = secureClaim
            ? bodies.FindIndex(body =>
                body.ActorId != bodies[0].ActorId
                && !FormAllows(contract, body.FormId, "fabricate"))
            : -1;
        if (secureClaim && forwardIndex < 0)
            forwardIndex = 1;

        var assignments = new Dictionary<ActorIdentity, Position>();
        var used = new HashSet<Position>();
        for (int index = 0; index < bodies.Count; index++)
        {
            Body body = bodies[index];
            bool forwardSurplus = index == forwardIndex;
            IEnumerable<Position> candidates = index == 0
                ? objectiveTiles
                : forwardSurplus
                    ? perimeter
                    : perimeter.Concat(objectiveTiles);
            Position? selected = candidates
                .Where(candidate => !used.Contains(candidate))
                .OrderBy(candidate =>
                    forwardSurplus
                        ? ShortestDistance(
                            contract.Map,
                            candidate,
                            nextTiles)
                        : 0)
                .ThenByDescending(candidate =>
                    used.Count == 0
                        ? 0
                        : used.Min(other =>
                            ManhattanDistance(candidate, other)))
                .ThenBy(candidate =>
                    ShortestDistance(
                        contract.Map,
                        body.Position,
                        new HashSet<Position> { candidate }))
                .ThenBy(candidate => candidate.Y)
                .ThenBy(candidate => candidate.X)
                .Cast<Position?>()
                .FirstOrDefault();
            if (selected is not Position position)
                break;

            assignments[body.ActorId] = position;
            used.Add(position);
        }

        return assignments.TryGetValue(context.Self.ActorId, out Position goal)
            ? goal
            : null;
    }

    private static Direction? EnemyApproachDirection(
        GenericActorResolvedMatchContract contract,
        GenericActorResolvedMatchContract.FrontlineModeMapBinding binding,
        GenericActorContext.ModeObservationState.Frontline frontline,
        GenericActorMapContract.Region objective,
        int teamId)
    {
        GenericActorResolvedMatchContract.FrontlineTeamAdvance? advance =
            binding.TeamAdvances.FirstOrDefault(team =>
                team.TeamId == teamId);
        if (advance is null)
            return null;

        int nextIndex =
            frontline.ActivePositionIndex + advance.ObjectiveIndexDelta;
        GenericActorMapContract.Region? next = ObjectiveAt(
            contract,
            binding,
            nextIndex);
        if (next is not null)
            return DominantDirection(Centre(objective), Centre(next));

        int priorIndex =
            frontline.ActivePositionIndex - advance.ObjectiveIndexDelta;
        GenericActorMapContract.Region? prior = ObjectiveAt(
            contract,
            binding,
            priorIndex);
        return prior is null
            ? null
            : DominantDirection(Centre(prior), Centre(objective));
    }

    private static GenericActorMapContract.Region? ObjectiveAt(
        GenericActorResolvedMatchContract contract,
        GenericActorResolvedMatchContract.FrontlineModeMapBinding binding,
        int index)
    {
        if (index < 0 || index >= binding.OrderedObjectiveRegionIds.Length)
            return null;

        string regionId = binding.OrderedObjectiveRegionIds[index];
        return contract.Map.Regions.FirstOrDefault(region =>
            string.Equals(
                region.RegionId,
                regionId,
                StringComparison.Ordinal));
    }

    private static Position Centre(GenericActorMapContract.Region region) =>
        new(
            region.Tiles.Sum(position => position.X)
                / Math.Max(1, region.Tiles.Length),
            region.Tiles.Sum(position => position.Y)
                / Math.Max(1, region.Tiles.Length));

    private static Direction? DominantDirection(
        Position from,
        Position target)
    {
        int dx = target.X - from.X;
        int dy = target.Y - from.Y;
        if (Math.Abs(dx) >= Math.Abs(dy) && dx != 0)
            return dx < 0 ? Direction.West : Direction.East;
        if (dy != 0)
            return dy < 0 ? Direction.North : Direction.South;
        return null;
    }

    private static bool HasFabricationPath(
        GenericActorResolvedMatchContract contract) =>
        contract.Rules.Actions.Any(action =>
            string.Equals(action.Id, "fabricate", StringComparison.Ordinal))
        && contract.Rules.FabricationTransitions.Any(transition =>
            string.Equals(
                transition.ActionId,
                "fabricate",
                StringComparison.Ordinal));

    private static bool ReplicationWorthwhile(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context,
        GenericActorMapContract.Region objective)
    {
        int remainingTicks = contract.Rules.Limits.MaxTicks - context.Tick;
        int travel = ShortestDistance(
            contract.Map,
            context.Self.Position,
            objective.Tiles.ToHashSet());
        int windup = contract.Rules.ReplicationTransitions
            .Where(transition =>
                string.Equals(
                    transition.ActionId,
                    "split",
                    StringComparison.Ordinal))
            .OfType<
                GenericActorRulesContract.SplitReplicationTransition>()
            .Select(transition => transition.Windup.DurationTicks)
            .DefaultIfEmpty(0)
            .Max();
        return travel != int.MaxValue
            && remainingTicks > travel + windup
            && context.Self.Health > 1;
    }

    private static int ObjectiveWeight(
        GenericActorResolvedMatchContract contract,
        string formId) =>
        contract.Rules.Forms.FirstOrDefault(form =>
            string.Equals(form.Id, formId, StringComparison.Ordinal))
            ?.ObjectiveWeight ?? 0;

    private static bool FormAllows(
        GenericActorResolvedMatchContract contract,
        string formId,
        string actionId) =>
        contract.Rules.Forms.FirstOrDefault(form =>
            string.Equals(form.Id, formId, StringComparison.Ordinal))
            ?.AllowedActionIds.Contains(actionId, StringComparer.Ordinal)
            ?? false;

    private static int AttackRange(
        GenericActorResolvedMatchContract contract,
        string formId)
    {
        string? attackProfileId = contract.Rules.Forms
            .FirstOrDefault(form =>
                string.Equals(form.Id, formId, StringComparison.Ordinal))
            ?.AttackProfileId;
        return contract.Rules.AttackProfiles
            .FirstOrDefault(profile =>
                string.Equals(
                    profile.Id,
                    attackProfileId,
                    StringComparison.Ordinal))
            ?.Projectile.MaxTravelTiles ?? 0;
    }

    private static int TeamBodyRank(GenericActorContext context) =>
        context.Allies
            .Select(ally => ally.ActorId)
            .Append(context.Self.ActorId)
            .Order()
            .ToList()
            .FindIndex(actorId => actorId == context.Self.ActorId);

    private Position? FindHomeSpawn(GenericActorMatchStart start)
    {
        GenericActorResolvedMatchContract.LifecycleAssignment? lifecycle =
            start.Contract.LifecycleAssignments.FirstOrDefault(assignment =>
                assignment.TeamId == start.ActorId.TeamId
                && assignment.UnitId == start.ActorId.UnitId);
        string? spawnId = lifecycle?.AssignedRespawnSpawnId
            ?? start.Contract.InitialDeployment.Lives
                .FirstOrDefault(life =>
                    life.TeamId == start.ActorId.TeamId
                    && life.UnitId == start.ActorId.UnitId)
                ?.SpawnId;
        return start.Contract.InitialDeployment.Spawns
            .FirstOrDefault(spawn =>
                string.Equals(
                    spawn.SpawnId,
                    spawnId,
                    StringComparison.Ordinal))
            ?.Position;
    }

    private static HashSet<Position> FindHomePad(
        GenericActorMapContract map,
        Position? homeSpawn)
    {
        HashSet<Position> protectedTiles = map.TileTags
            .Where(tag =>
                tag.Kind
                == GenericActorMapContract.TileTagKind.SpawnProtected)
            .SelectMany(tag => tag.Tiles)
            .ToHashSet();
        if (homeSpawn is not Position spawn)
            return [];

        if (!protectedTiles.Contains(spawn))
        {
            Position? closest = protectedTiles
                .OrderBy(position => ManhattanDistance(position, spawn))
                .ThenBy(position => position.Y)
                .ThenBy(position => position.X)
                .Cast<Position?>()
                .FirstOrDefault();
            if (closest is not Position tile)
                return [spawn];
            spawn = tile;
        }

        var component = new HashSet<Position> { spawn };
        var queue = new Queue<Position>();
        queue.Enqueue(spawn);
        while (queue.Count > 0)
        {
            Position current = queue.Dequeue();
            foreach (Direction direction in CardinalDirections)
            {
                Position next = Offset(current, direction);
                if (protectedTiles.Contains(next) && component.Add(next))
                    queue.Enqueue(next);
            }
        }

        return component;
    }

    private static GenericActorDecision? WithoutArguments(
        GenericActorContext context,
        string actionId,
        string debug)
    {
        GenericActorActionLegality? legality = Available(context, actionId);
        return legality is null
            ? null
            : GenericActorDecision.WithoutArguments(
                legality.ActionId,
                legality.ActionCode,
                debug);
    }

    private static GenericActorDecision? WithDirection(
        GenericActorContext context,
        string actionId,
        Direction direction,
        string debug)
    {
        GenericActorActionLegality? legality = Available(context, actionId);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            constraint = legality?.Constraints
                .OfType<
                    GenericActorActionLegality.ArgumentConstraint
                        .DirectionConstraint>()
                .FirstOrDefault();
        if (legality is null
            || constraint is null
            || !constraint.AllowedValues.Contains(direction))
        {
            return null;
        }

        return new GenericActorDecision(
            legality.ActionId,
            legality.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(direction)],
            debug);
    }

    private static GenericActorActionLegality? Available(
        GenericActorContext context,
        string actionId) =>
        context.Action(actionId) is { Available: true } legality
            ? legality
            : null;

    private static GenericActorDecision Fallback(
        GenericActorContext context,
        string debug)
    {
        GenericActorDecision? wait = WithoutArguments(
            context,
            "wait",
            debug);
        if (wait is not null)
            return wait;

        foreach (string actionId in new[] { "rotate", "move" })
        {
            GenericActorActionLegality? legality = Available(
                context,
                actionId);
            Direction? direction = legality?.Constraints
                .OfType<
                    GenericActorActionLegality.ArgumentConstraint
                        .DirectionConstraint>()
                .SelectMany(constraint => constraint.AllowedValues)
                .Cast<Direction?>()
                .FirstOrDefault();
            if (direction is Direction selected)
            {
                GenericActorDecision? decision = WithDirection(
                    context,
                    actionId,
                    selected,
                    $"{debug}; wait unavailable");
                if (decision is not null)
                    return decision;
            }
        }

        GenericActorActionLegality? parameterless =
            context.ActionLegalities.FirstOrDefault(action =>
                action.Available && action.Constraints.Length == 0);
        if (parameterless is not null)
        {
            return GenericActorDecision.WithoutArguments(
                parameterless.ActionId,
                parameterless.ActionCode,
                $"{debug}; catalog fallback");
        }

        GenericActorActionLegality lastResort =
            context.ActionLegalities.First();
        return GenericActorDecision.WithoutArguments(
            lastResort.ActionId,
            lastResort.ActionCode,
            $"{debug}; no action advertised available");
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
        foreach (Direction direction in CardinalDirections)
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
            foreach (Direction direction in CardinalDirections)
            {
                Position next = Offset(current.Position, direction);
                if (!CanEnter(map, next, occupied) || !visited.Add(next))
                    continue;
                if (goals.Contains(next))
                    return current.First;
                queue.Enqueue((next, current.First));
            }
        }

        return null;
    }

    private static int ShortestDistance(
        GenericActorMapContract map,
        Position start,
        IReadOnlySet<Position> goals)
    {
        if (goals.Contains(start))
            return 0;

        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Position, int Distance)>();
        queue.Enqueue((start, 0));
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (Direction direction in CardinalDirections)
            {
                Position next = Offset(current.Position, direction);
                if (!CanEnter(map, next, EmptyPositions)
                    || !visited.Add(next))
                    continue;
                if (goals.Contains(next))
                    return current.Distance + 1;
                queue.Enqueue((next, current.Distance + 1));
            }
        }

        return int.MaxValue;
    }

    private static int ShortestDistance(
        GenericActorMapContract map,
        IReadOnlySet<Position> starts,
        IReadOnlySet<Position> goals) =>
        starts.Select(start => ShortestDistance(map, start, goals))
            .DefaultIfEmpty(int.MaxValue)
            .Min();

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
        var (dx, dy) = direction.Vector();
        return position.Offset(dx, dy);
    }

    private static Direction? AlignedDirection(
        Position from,
        Position target)
    {
        if (target.X == from.X)
            return target.Y < from.Y ? Direction.North : Direction.South;
        if (target.Y == from.Y)
            return target.X < from.X ? Direction.West : Direction.East;
        return null;
    }

    private static ProjectileHeading? ExactHeading(
        Position from,
        Position target)
    {
        int dx = target.X - from.X;
        int dy = target.Y - from.Y;
        if (dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy))
            return null;
        if (dx == 0 && dy == 0)
            return null;

        return (Math.Sign(dx), Math.Sign(dy)) switch
        {
            (0, -1) => ProjectileHeading.North,
            (1, -1) => ProjectileHeading.NorthEast,
            (1, 0) => ProjectileHeading.East,
            (1, 1) => ProjectileHeading.SouthEast,
            (0, 1) => ProjectileHeading.South,
            (-1, 1) => ProjectileHeading.SouthWest,
            (-1, 0) => ProjectileHeading.West,
            (-1, -1) => ProjectileHeading.NorthWest,
            _ => null,
        };
    }

    private static bool ClearCardinalRay(
        GenericActorMapContract map,
        Position from,
        Position target)
    {
        Direction? direction = AlignedDirection(from, target);
        if (direction is null)
            return false;

        Position current = Offset(from, direction.Value);
        while (current != target)
        {
            if (!CanEnter(map, current, EmptyPositions))
                return false;
            current = Offset(current, direction.Value);
        }
        return true;
    }

    private static int CardinalDistance(Position left, Position right) =>
        Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);

    private static int ManhattanDistance(Position left, Position right) =>
        Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);

    private sealed record Body(
        ActorIdentity ActorId,
        string FormId,
        Position Position);
}
