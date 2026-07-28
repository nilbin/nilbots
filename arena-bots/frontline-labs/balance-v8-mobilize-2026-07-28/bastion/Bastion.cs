using BotArena.Sdk;

/// <summary>
/// Bastion deliberately turns the first compatible fabricated slot into an
/// objective-adjacent Anchor. The Prime and every other mobile body continue
/// applying objective pressure.
/// </summary>
public sealed class Bastion : IGenericActorBot
{
    private GenericActorResolvedMatchContract? _contract;
    private GenericActorResolvedMatchContract.FrontlineModeMapBinding?
        _frontline;
    private int _participantId;
    private int _teamId;
    private bool _isDesignatedAnchor;
    private int _anchoredPositionIndex = -1;
    private HashSet<int> _fabricationSlotIds = [];
    private HashSet<Position> _homeTiles = [];

    public void StartLife(GenericActorMatchStart start)
    {
        _contract = start.Contract;
        _frontline =
            start.Contract.ModeMapBinding
                as GenericActorResolvedMatchContract
                    .FrontlineModeMapBinding;
        _participantId = start.ParticipantId;
        _teamId = start.ActorId.TeamId;
        _anchoredPositionIndex = -1;

        GenericActorRulesContract.BoundedChildFabricationTransition[]
            fabricationTransitions = start.Contract.Rules
                .FabricationTransitions
                .OfType<
                    GenericActorRulesContract
                        .BoundedChildFabricationTransition>()
                .Where(transition => string.Equals(
                    transition.ActionId,
                    "fabricate",
                    StringComparison.Ordinal))
                .ToArray();
        HashSet<string> fabricatedForms = fabricationTransitions
            .Select(transition => transition.OutputFormId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<int> initiallyOccupiedSlots = start.Contract.Topology
            .InitialLives
            .Where(life => life.TeamId == _teamId)
            .Select(life => life.UnitId)
            .ToHashSet();

        _fabricationSlotIds = start.Contract.LifecycleAssignments
            .Where(assignment =>
                assignment.TeamId == _teamId
                && !initiallyOccupiedSlots.Contains(assignment.UnitId)
                && assignment.AllowedFormIds.Any(fabricatedForms.Contains))
            .Select(assignment => assignment.UnitId)
            .ToHashSet();

        int[] orderedFabricationSlots = _fabricationSlotIds
            .Order()
            .ToArray();
        _isDesignatedAnchor =
            start.Origin.Reason
                == GenericActorMatchStart.SpawnReason.Fabrication
            && orderedFabricationSlots.Length > 0
            && start.ActorId.UnitId == orderedFabricationSlots[0];

        HashSet<string> sourceRegionRoles = fabricationTransitions
            .Select(transition => transition.SourceRegionRoleId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> homeRegionIds = start.Contract
            .ParticipantRegionAssignments
            .Where(assignment =>
                assignment.ParticipantId == _participantId
                && sourceRegionRoles.Contains(assignment.RegionRoleId))
            .Select(assignment => assignment.MapRegionId)
            .ToHashSet(StringComparer.Ordinal);

        _homeTiles = start.Contract.Map.Regions
            .Where(region => homeRegionIds.Contains(region.RegionId))
            .SelectMany(region => region.Tiles)
            .ToHashSet();

        if (_homeTiles.Count == 0)
        {
            Dictionary<string, Position> spawnPositions = start.Contract
                .InitialDeployment.Spawns
                .ToDictionary(
                    spawn => spawn.SpawnId,
                    spawn => spawn.Position,
                    StringComparer.Ordinal);
            foreach (
                GenericActorResolvedMatchContract.InitialLifeDeployment life
                in start.Contract.InitialDeployment.Lives.Where(
                    life => life.TeamId == _teamId))
            {
                if (spawnPositions.TryGetValue(
                    life.SpawnId,
                    out Position position))
                {
                    _homeTiles.Add(position);
                }
            }
        }
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        GenericActorResolvedMatchContract? contract = _contract;
        if (contract is null)
            return Fallback(context, "missing life-start state");

        if (context.Self.PendingSameLifeTransition is not null)
            return Fallback(context, "completing same-life transition");

        GenericActorContext.ObservedEnemyState? nearestEnemy =
            context.Enemies
                .OrderBy(enemy =>
                    context.Self.Position.ChebyshevDistance(enemy.Position))
                .ThenBy(enemy => enemy.ActorId)
                .FirstOrDefault();

        if (TryMobilize(context, out GenericActorDecision? mobilize))
            return mobilize!;

        if (TryTurretFire(context, out GenericActorDecision? turretFire))
            return turretFire!;

        if (nearestEnemy is not null
            && IsClearCardinalTarget(
                contract.Map,
                context.Self.Position,
                nearestEnemy.Position,
                context.Self.Facing)
            && TryWithoutArguments(
                context,
                "shoot",
                $"guarding lane toward {nearestEnemy.Position}",
                out GenericActorDecision? mobileFire))
        {
            return mobileFire!;
        }

        if (TryFabricate(context, out GenericActorDecision? fabrication))
            return fabrication!;

        HashSet<Position> occupied = OccupiedTiles(context);
        if (ShouldReturnForFabrication(context))
        {
            if (_homeTiles.Contains(context.Self.Position))
                return Fallback(context, "holding home pad for fabrication");

            Direction? homeStep = FindFirstStep(
                contract.Map,
                context.Self.Position,
                _homeTiles,
                occupied,
                AllowedDirections(context, "move"));
            if (homeStep is Direction direction
                && TryDirection(
                    context,
                    "move",
                    direction,
                    "returning to the home pad for a Ready child",
                    out GenericActorDecision? returnMove))
            {
                return returnMove!;
            }
        }

        if (TryObjective(
            context,
            out GenericActorMapContract.Region? objective,
            out int activePositionIndex))
        {
            if (_isDesignatedAnchor
                && TryAnchorDuty(
                    context,
                    objective!,
                    activePositionIndex,
                    occupied,
                    out GenericActorDecision? anchorDecision))
            {
                return anchorDecision!;
            }

            Direction? objectiveStep = FindFirstStep(
                contract.Map,
                context.Self.Position,
                objective!.Tiles.ToHashSet(),
                occupied,
                AllowedDirections(context, "move"));
            if (objectiveStep is Direction direction
                && TryDirection(
                    context,
                    "move",
                    direction,
                    $"pressuring {objective.RegionId}",
                    out GenericActorDecision? advance))
            {
                return advance!;
            }

            Position? facingTarget = nearestEnemy?.Position
                ?? ForwardObjectiveCenter(activePositionIndex);
            Direction? desiredFacing = facingTarget is Position target
                ? DirectionToward(context.Self.Position, target)
                : null;
            if (desiredFacing is Direction facing
                && facing != context.Self.Facing
                && TryDirection(
                    context,
                    "rotate",
                    facing,
                    $"watching over {objective.RegionId}",
                    out GenericActorDecision? rotation))
            {
                return rotation!;
            }

            return Fallback(context, $"holding {objective.RegionId}");
        }

        return Fallback(context, "no compatible active objective");
    }

    private bool TryMobilize(
        GenericActorContext context,
        out GenericActorDecision? decision)
    {
        decision = null;
        if (!_isDesignatedAnchor
            || _anchoredPositionIndex < 0
            || !TryObjective(
                context,
                out GenericActorMapContract.Region? objective,
                out int activePositionIndex)
            || activePositionIndex == _anchoredPositionIndex)
        {
            return false;
        }

        GenericActorResolvedMatchContract? contract = _contract;
        bool hasDeclaredRoute = contract?.Rules.SameLifeTransitions
            .OfType<GenericActorRulesContract.FormTransition>()
            .Any(transition =>
                string.Equals(
                    transition.ActionId,
                    "mobilize",
                    StringComparison.Ordinal)
                && string.Equals(
                    transition.SourceFormId,
                    context.Self.FormId,
                    StringComparison.Ordinal)) == true;
        return hasDeclaredRoute
            && TryWithoutArguments(
                context,
                "mobilize",
                $"leaving obsolete post for {objective!.RegionId}",
                out decision);
    }

    private bool TryAnchorDuty(
        GenericActorContext context,
        GenericActorMapContract.Region objective,
        int activePositionIndex,
        IReadOnlySet<Position> occupied,
        out GenericActorDecision? decision)
    {
        decision = null;
        GenericActorResolvedMatchContract? contract = _contract;
        if (contract is null)
            return false;

        GenericActorRulesContract.FormTransition? transition = contract.Rules
            .SameLifeTransitions
            .OfType<GenericActorRulesContract.FormTransition>()
            .Where(candidate =>
                string.Equals(
                    candidate.ActionId,
                    "transform",
                    StringComparison.Ordinal)
                && string.Equals(
                    candidate.SourceFormId,
                    context.Self.FormId,
                    StringComparison.Ordinal))
            .OrderBy(candidate => candidate.TargetFormId, StringComparer.Ordinal)
            .FirstOrDefault();
        GenericActorActionLegality? transform = context.Action("transform");
        if (transition is null || transform is not { AllowedByForm: true })
            return false;

        HashSet<Position> posts = AnchorPosts(
            contract.Map,
            transition,
            objective,
            activePositionIndex);
        if (posts.Count == 0)
            return false;

        if (posts.Contains(context.Self.Position))
        {
            bool started = TryFormTarget(
                context,
                "transform",
                transition.TargetFormId,
                $"Anchoring beside {objective.RegionId}",
                out decision);
            if (started)
                _anchoredPositionIndex = activePositionIndex;
            return started;
        }

        Direction? step = FindFirstStep(
            contract.Map,
            context.Self.Position,
            posts,
            occupied,
            AllowedDirections(context, "move"));
        return step is Direction direction
            && TryDirection(
                context,
                "move",
                direction,
                $"taking a forward denial post beside {objective.RegionId}",
                out decision);
    }

    private HashSet<Position> AnchorPosts(
        GenericActorMapContract map,
        GenericActorRulesContract.FormTransition transition,
        GenericActorMapContract.Region objective,
        int activePositionIndex)
    {
        var candidates = new List<(Position Position, int Objective, int Forward)>();
        GenericActorMapContract.Region? forwardRegion =
            ForwardObjective(activePositionIndex);

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var position = new Position(x, y);
                if (!IsOpen(map, position)
                    || objective.Tiles.Contains(position)
                    || !TransitionTagsAllow(map, transition, position))
                {
                    continue;
                }

                int objectiveDistance = objective.Tiles
                    .Min(tile => tile.ChebyshevDistance(position));
                int forwardDistance = forwardRegion is null
                    ? 0
                    : forwardRegion.Tiles.Min(
                        tile => tile.ChebyshevDistance(position));
                candidates.Add((
                    position,
                    objectiveDistance,
                    forwardDistance));
            }
        }

        if (candidates.Count == 0)
            return [];

        int closestObjective = candidates.Min(candidate => candidate.Objective);
        int closestForward = candidates
            .Where(candidate => candidate.Objective == closestObjective)
            .Min(candidate => candidate.Forward);
        return candidates
            .Where(candidate =>
                candidate.Objective == closestObjective
                && candidate.Forward == closestForward)
            .Select(candidate => candidate.Position)
            .ToHashSet();
    }

    private static bool TransitionTagsAllow(
        GenericActorMapContract map,
        GenericActorRulesContract.FormTransition transition,
        Position position)
    {
        HashSet<GenericActorMapContract.TileTagKind> present = map.TileTags
            .Where(tag => tag.Tiles.Contains(position))
            .Select(tag => tag.Kind)
            .ToHashSet();
        return transition.Placement.RequiredTileTags.All(present.Contains)
            && !transition.Placement.ForbiddenTileTags.Any(present.Contains);
    }

    private bool TryObjective(
        GenericActorContext context,
        out GenericActorMapContract.Region? objective,
        out int activePositionIndex)
    {
        objective = null;
        activePositionIndex = -1;
        if (_contract is null
            || _frontline is null
            || context.Mode
                is not GenericActorContext.ModeObservationState.Frontline mode
            || mode.ActivePositionIndex < 0
            || mode.ActivePositionIndex
                >= _frontline.OrderedObjectiveRegionIds.Length)
        {
            return false;
        }

        activePositionIndex = mode.ActivePositionIndex;
        string regionId =
            _frontline.OrderedObjectiveRegionIds[activePositionIndex];
        objective = _contract.Map.Regions.FirstOrDefault(region =>
            string.Equals(
                region.RegionId,
                regionId,
                StringComparison.Ordinal));
        return objective is not null;
    }

    private GenericActorMapContract.Region? ForwardObjective(int activeIndex)
    {
        if (_contract is null || _frontline is null)
            return null;

        GenericActorResolvedMatchContract.FrontlineTeamAdvance? advance =
            _frontline.TeamAdvances.FirstOrDefault(
                candidate => candidate.TeamId == _teamId);
        if (advance is null)
            return null;

        int forwardIndex = activeIndex + advance.ObjectiveIndexDelta;
        if (forwardIndex < 0
            || forwardIndex >= _frontline.OrderedObjectiveRegionIds.Length)
        {
            return null;
        }

        string regionId = _frontline.OrderedObjectiveRegionIds[forwardIndex];
        return _contract.Map.Regions.FirstOrDefault(region =>
            string.Equals(
                region.RegionId,
                regionId,
                StringComparison.Ordinal));
    }

    private Position? ForwardObjectiveCenter(int activeIndex)
    {
        GenericActorMapContract.Region? region = ForwardObjective(activeIndex);
        if (region is null || region.Tiles.Length == 0)
            return null;
        return new Position(
            (int)region.Tiles.Average(tile => tile.X),
            (int)region.Tiles.Average(tile => tile.Y));
    }

    private bool ShouldReturnForFabrication(GenericActorContext context)
    {
        GenericActorActionLegality? fabricate = context.Action("fabricate");
        return fabricate is { AllowedByForm: true }
            && _homeTiles.Count > 0
            && context.TeamUnits.Any(slot =>
                slot.TeamId == _teamId
                && _fabricationSlotIds.Contains(slot.UnitId)
                && slot.State is GenericActorContext.UnitSlotState.Ready);
    }

    private bool TryFabricate(
        GenericActorContext context,
        out GenericActorDecision? decision)
    {
        decision = null;
        GenericActorActionLegality? action = context.Action("fabricate");
        if (action is not { Available: true })
            return false;

        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = action.Constraints
                .OfType<
                    GenericActorActionLegality.ArgumentConstraint
                        .UnitTargetConstraint>()
                .SingleOrDefault();
        if (targets is null)
            return false;

        HashSet<int> readySlots = context.TeamUnits
            .Where(slot =>
                slot.TeamId == _teamId
                && slot.State is GenericActorContext.UnitSlotState.Ready)
            .Select(slot => slot.UnitId)
            .ToHashSet();
        GenericActorActionArgument.UnitTarget? target =
            targets.AllowedValues
                .Where(candidate =>
                    candidate.TeamId == _teamId
                    && _fabricationSlotIds.Contains(candidate.UnitId)
                    && readySlots.Contains(candidate.UnitId))
                .OrderBy(candidate => candidate.UnitId)
                .Cast<GenericActorActionArgument.UnitTarget?>()
                .FirstOrDefault();
        if (target is null)
            return false;

        decision = new GenericActorDecision(
            action.ActionId,
            action.ActionCode,
            [
                new GenericActorActionArgument.UnitTargetArgument(
                    target.Value),
            ],
            $"fabricating mobile slot {target.Value.UnitId}");
        return true;
    }

    private bool TryTurretFire(
        GenericActorContext context,
        out GenericActorDecision? decision)
    {
        decision = null;
        GenericActorActionLegality? action =
            context.Action("shoot-direction");
        if (action is not { Available: true } || context.Enemies.Length == 0)
            return false;

        GenericActorActionLegality.ArgumentConstraint
            .ProjectileHeadingConstraint? headings = action.Constraints
                .OfType<
                    GenericActorActionLegality.ArgumentConstraint
                        .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        if (headings is null || headings.AllowedValues.Length == 0)
            return false;

        var aimed = context.Enemies
            .Select(enemy => new
            {
                Enemy = enemy,
                Heading = Heading(context.Self.Position, enemy.Position),
                Error = HeadingError(context.Self.Position, enemy.Position),
                Distance = context.Self.Position.ChebyshevDistance(
                    enemy.Position),
            })
            .Where(candidate =>
                headings.AllowedValues.Contains(candidate.Heading))
            .OrderBy(candidate => candidate.Error)
            .ThenBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Enemy.ActorId)
            .FirstOrDefault();
        if (aimed is null)
            return false;

        decision = new GenericActorDecision(
            action.ActionId,
            action.ActionCode,
            [
                new GenericActorActionArgument.ProjectileHeadingArgument(
                    aimed.Heading),
            ],
            $"denial fire toward {aimed.Enemy.Position}");
        return true;
    }

    private static int HeadingError(Position from, Position target)
    {
        int dx = Math.Abs(target.X - from.X);
        int dy = Math.Abs(target.Y - from.Y);
        return Math.Min(Math.Min(dx, dy), Math.Abs(dx - dy));
    }

    private static ProjectileHeading Heading(Position from, Position target)
    {
        int dx = Math.Sign(target.X - from.X);
        int dy = Math.Sign(target.Y - from.Y);
        return (dx, dy) switch
        {
            (0, -1) => ProjectileHeading.North,
            (1, -1) => ProjectileHeading.NorthEast,
            (1, 0) => ProjectileHeading.East,
            (1, 1) => ProjectileHeading.SouthEast,
            (0, 1) => ProjectileHeading.South,
            (-1, 1) => ProjectileHeading.SouthWest,
            (-1, 0) => ProjectileHeading.West,
            (-1, -1) => ProjectileHeading.NorthWest,
            _ => ProjectileHeading.North,
        };
    }

    private static Direction? DirectionToward(Position from, Position target)
    {
        int dx = target.X - from.X;
        int dy = target.Y - from.Y;
        if (dx == 0 && dy == 0)
            return null;
        if (Math.Abs(dx) >= Math.Abs(dy))
            return dx >= 0 ? Direction.East : Direction.West;
        return dy >= 0 ? Direction.South : Direction.North;
    }

    private static bool IsClearCardinalTarget(
        GenericActorMapContract map,
        Position from,
        Position target,
        Direction facing)
    {
        if (!IsAhead(from, target, facing))
            return false;

        var (dx, dy) = facing.Vector();
        Position cursor = from.Offset(dx, dy);
        while (cursor != target)
        {
            if (!IsOpen(map, cursor))
                return false;
            cursor = cursor.Offset(dx, dy);
        }
        return true;
    }

    private static bool IsAhead(
        Position from,
        Position target,
        Direction facing) =>
        facing switch
        {
            Direction.North =>
                target.X == from.X && target.Y < from.Y,
            Direction.East =>
                target.Y == from.Y && target.X > from.X,
            Direction.South =>
                target.X == from.X && target.Y > from.Y,
            Direction.West =>
                target.Y == from.Y && target.X < from.X,
            _ => false,
        };

    private static HashSet<Position> OccupiedTiles(
        GenericActorContext context)
    {
        IEnumerable<Position> actors = context.Allies
            .Select(ally => ally.Position)
            .Concat(context.Enemies.Select(enemy => enemy.Position));
        IEnumerable<Position> hostileProjectiles =
            context.VisibleProjectiles is { } projectiles
                ? projectiles
                    .Where(projectile =>
                        projectile.OwnerTeamId
                            != context.Self.ActorId.TeamId)
                    .Select(projectile => projectile.Position)
                : [];
        return actors.Concat(hostileProjectiles).ToHashSet();
    }

    private static IReadOnlySet<Direction> AllowedDirections(
        GenericActorContext context,
        string actionId)
    {
        GenericActorActionLegality? action = context.Action(actionId);
        return action?.Constraints
            .OfType<
                GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
            .SingleOrDefault()
            ?.AllowedValues
            .ToHashSet()
            ?? new HashSet<Direction>();
    }

    private static Direction? FindFirstStep(
        GenericActorMapContract map,
        Position start,
        IReadOnlySet<Position> goals,
        IReadOnlySet<Position> occupied,
        IReadOnlySet<Direction> allowedFirstSteps)
    {
        if (goals.Contains(start) || allowedFirstSteps.Count == 0)
            return null;

        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Position, Direction First)>();
        foreach (Direction direction in Enum.GetValues<Direction>())
        {
            if (!allowedFirstSteps.Contains(direction))
                continue;
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
            foreach (Direction direction in Enum.GetValues<Direction>())
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

    private static bool CanEnter(
        GenericActorMapContract map,
        Position position,
        IReadOnlySet<Position> occupied) =>
        IsOpen(map, position) && !occupied.Contains(position);

    private static bool IsOpen(
        GenericActorMapContract map,
        Position position) =>
        position.X >= 0
        && position.Y >= 0
        && position.X < map.Width
        && position.Y < map.Height
        && map.TileRows[position.Y][position.X] != '#';

    private static Position Offset(
        Position position,
        Direction direction)
    {
        var (dx, dy) = direction.Vector();
        return position.Offset(dx, dy);
    }

    private static bool TryWithoutArguments(
        GenericActorContext context,
        string actionId,
        string debug,
        out GenericActorDecision? decision)
    {
        decision = null;
        GenericActorActionLegality? selected = context.Action(actionId);
        if (selected is not { Available: true }
            || selected.Constraints.Any(constraint =>
                constraint
                    is not GenericActorActionLegality.ArgumentConstraint
                        .ShotProgramConstraint))
        {
            return false;
        }

        decision = GenericActorDecision.WithoutArguments(
            selected.ActionId,
            selected.ActionCode,
            debug);
        return true;
    }

    private static bool TryDirection(
        GenericActorContext context,
        string actionId,
        Direction direction,
        string debug,
        out GenericActorDecision? decision)
    {
        decision = null;
        GenericActorActionLegality? selected = context.Action(actionId);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            constraint = selected?.Constraints
                .OfType<
                    GenericActorActionLegality.ArgumentConstraint
                        .DirectionConstraint>()
                .SingleOrDefault();
        if (selected is not { Available: true }
            || constraint is null
            || !constraint.AllowedValues.Contains(direction))
        {
            return false;
        }

        decision = new GenericActorDecision(
            selected.ActionId,
            selected.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(direction)],
            debug);
        return true;
    }

    private static bool TryFormTarget(
        GenericActorContext context,
        string actionId,
        string formId,
        string debug,
        out GenericActorDecision? decision)
    {
        decision = null;
        GenericActorActionLegality? selected = context.Action(actionId);
        GenericActorActionLegality.ArgumentConstraint.FormTargetConstraint?
            constraint = selected?.Constraints
                .OfType<
                    GenericActorActionLegality.ArgumentConstraint
                        .FormTargetConstraint>()
                .SingleOrDefault();
        if (selected is not { Available: true }
            || constraint is null
            || !constraint.AllowedFormIds.Contains(
                formId,
                StringComparer.Ordinal))
        {
            return false;
        }

        decision = new GenericActorDecision(
            selected.ActionId,
            selected.ActionCode,
            [new GenericActorActionArgument.FormTargetArgument(formId)],
            debug);
        return true;
    }

    private static GenericActorDecision Fallback(
        GenericActorContext context,
        string debug)
    {
        GenericActorActionLegality? wait = context.Action("wait");
        if (wait is { Available: true } && wait.Constraints.Length == 0)
        {
            return GenericActorDecision.WithoutArguments(
                wait.ActionId,
                wait.ActionCode,
                debug);
        }

        foreach (GenericActorActionLegality action in
                 context.ActionLegalities.Where(candidate => candidate.Available))
        {
            var arguments = new List<GenericActorActionArgument>();
            bool supported = true;
            foreach (GenericActorActionLegality.ArgumentConstraint constraint
                     in action.Constraints)
            {
                switch (constraint)
                {
                    case GenericActorActionLegality.ArgumentConstraint
                        .ShotProgramConstraint:
                        break;
                    case GenericActorActionLegality.ArgumentConstraint
                        .DirectionConstraint directions
                        when directions.AllowedValues.Length > 0:
                        arguments.Add(
                            new GenericActorActionArgument.DirectionArgument(
                                directions.AllowedValues[0]));
                        break;
                    case GenericActorActionLegality.ArgumentConstraint
                        .UnitTargetConstraint targets
                        when targets.AllowedValues.Length > 0:
                        arguments.Add(
                            new GenericActorActionArgument.UnitTargetArgument(
                                targets.AllowedValues[0]));
                        break;
                    case GenericActorActionLegality.ArgumentConstraint
                        .FormTargetConstraint forms
                        when forms.AllowedFormIds.Length > 0:
                        arguments.Add(
                            new GenericActorActionArgument.FormTargetArgument(
                                forms.AllowedFormIds[0]));
                        break;
                    case GenericActorActionLegality.ArgumentConstraint
                        .ProjectileHeadingConstraint headings
                        when headings.AllowedValues.Length > 0:
                        arguments.Add(
                            new GenericActorActionArgument
                                .ProjectileHeadingArgument(
                                    headings.AllowedValues[0]));
                        break;
                    default:
                        supported = false;
                        break;
                }
            }

            if (supported)
            {
                return new GenericActorDecision(
                    action.ActionId,
                    action.ActionCode,
                    arguments,
                    debug);
            }
        }

        throw new InvalidOperationException(
            "The host supplied no executable generic action.");
    }
}
