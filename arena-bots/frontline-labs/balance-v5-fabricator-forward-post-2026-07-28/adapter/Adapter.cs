using BotArena.Sdk;

/// <summary>
/// A bounded, contract-driven Adapter. Each body changes posture from the
/// current score, objective, visible force balance, and legal capabilities.
/// </summary>
public sealed class Adapter : IGenericActorBot
{
    private static readonly Direction[] Directions =
    [
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West,
    ];

    private GenericActorResolvedMatchContract? _contract;
    private GenericActorResolvedMatchContract.FrontlineModeMapBinding?
        _frontline;
    private readonly Dictionary<string, FormData> _forms =
        new(StringComparer.Ordinal);
    private ObjectiveData[] _objectives = [];
    private bool[] _walkable = [];
    private int[] _seen = [];
    private int[] _blocked = [];
    private int[] _queue = [];
    private Direction[] _firstStep = [];
    private int _seenGeneration;
    private int _blockedGeneration;
    private int _width;
    private int _height;
    private int _teamId;
    private int _maxTicks;
    private int _captureThreshold = 1;
    private Direction _authoredForward;
    private string? _rankingChannel;
    private bool _lowerScoreIsBetter;
    private string[] _waitActionIds = [];
    private string[] _moveActionIds = [];
    private string[] _rotateActionIds = [];
    private string[] _mobileAttackActionIds = [];
    private string[] _absoluteAttackActionIds = [];
    private string[] _fabricateActionIds = [];
    private string[] _splitActionIds = [];

    public void StartLife(GenericActorMatchStart start)
    {
        GenericActorResolvedMatchContract contract = start.Contract;
        _contract = contract;
        _frontline = contract.ModeMapBinding
            as GenericActorResolvedMatchContract.FrontlineModeMapBinding;
        _teamId = start.ActorId.TeamId;
        _maxTicks = contract.Rules.Limits.MaxTicks;
        _authoredForward = Direction.North;

        foreach (GenericActorResolvedMatchContract
            .ParticipantRegionAssignment assignment
            in contract.ParticipantRegionAssignments)
        {
            if (assignment.ParticipantId == start.ParticipantId)
            {
                _authoredForward = assignment.Facing;
                break;
            }
        }

        if (contract.Rules.GameMode
            is GenericActorRulesContract.FrontlineGameMode gameMode)
        {
            _captureThreshold = Math.Max(1, gameMode.Capture.Threshold);
        }

        if (!contract.Rules.GameMode.Victory.TimeoutRanking.IsEmpty)
        {
            GenericActorRulesContract.ScoreRanking ranking =
                contract.Rules.GameMode.Victory.TimeoutRanking[0];
            _rankingChannel = ranking.Channel;
            _lowerScoreIsBetter =
                ranking.Direction.Contains(
                    "ascending",
                    StringComparison.OrdinalIgnoreCase)
                || ranking.Direction.Contains(
                    "lower",
                    StringComparison.OrdinalIgnoreCase);
        }

        CacheForms(contract);
        CacheActions(contract);
        CacheMap(contract);
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        GenericActorResolvedMatchContract? contract = _contract;
        if (contract is null)
            return Wait(context, "contract unavailable");
        if (context.Self.PendingSameLifeTransition is not null)
            return Wait(context, "transition pending");

        if (context.Mode
                is not GenericActorContext.ModeObservationState.Frontline
                    mode
            || _frontline is null
            || mode.ActivePositionIndex < 0
            || mode.ActivePositionIndex >= _objectives.Length)
        {
            return TryAttack(context, null)
                ?? Wait(context, "objective unavailable");
        }

        ObjectiveData objective = _objectives[mode.ActivePositionIndex];
        Situation situation = Assess(context, mode, objective);

        GenericActorDecision? decision = TryAttack(context, objective);
        if (decision is not null)
            return decision;

        decision = TryFabricate(context, situation);
        if (decision is not null)
            return decision;

        decision = TrySplit(context, situation);
        if (decision is not null)
            return decision;

        return Navigate(context, objective, situation);
    }

    private Situation Assess(
        GenericActorContext context,
        GenericActorContext.ModeObservationState.Frontline mode,
        ObjectiveData objective)
    {
        int ownMobileBodies =
            ObjectiveWeight(context.Self.FormId) > 0 ? 1 : 0;
        int alliedMobileOnObjective = 0;
        foreach (GenericActorContext.ObservedAllyState ally
            in context.Allies)
        {
            if (ObjectiveWeight(ally.FormId) <= 0)
                continue;
            ownMobileBodies++;
            if (IsMarked(objective.Tiles, ally.Position))
                alliedMobileOnObjective++;
        }

        int ownObjectiveBodies = alliedMobileOnObjective;
        if (ObjectiveWeight(context.Self.FormId) > 0
            && IsMarked(objective.Tiles, context.Self.Position))
        {
            ownObjectiveBodies++;
        }

        int visibleEnemyMobileBodies = 0;
        int visibleEnemyObjectiveBodies = 0;
        foreach (GenericActorContext.ObservedEnemyState enemy
            in context.Enemies)
        {
            if (ObjectiveWeight(enemy.FormId) <= 0)
                continue;
            visibleEnemyMobileBodies++;
            if (IsMarked(objective.Tiles, enemy.Position))
                visibleEnemyObjectiveBodies++;
        }

        long scoreMargin = ScoreMargin(context);
        bool ownClaim = mode.ClaimingTeamId == _teamId;
        bool enemyClaim = mode.ClaimingTeamId is int claimant
            && claimant != _teamId;
        bool late = Math.Max(0, _maxTicks - context.Tick)
            <= _width + _captureThreshold;
        bool secure = ownClaim
            && mode.CaptureProgress
                >= Math.Max(1, _captureThreshold / 2)
            && scoreMargin >= 0
            && visibleEnemyObjectiveBodies == 0;
        return new Situation(
            scoreMargin,
            ownClaim,
            enemyClaim,
            secure,
            late,
            ownMobileBodies,
            alliedMobileOnObjective,
            ownObjectiveBodies,
            visibleEnemyMobileBodies,
            visibleEnemyObjectiveBodies);
    }

    private long ScoreMargin(GenericActorContext context)
    {
        if (_rankingChannel is null)
            return 0;

        bool foundOwn = false;
        long own = 0;
        bool foundOpponent = false;
        long strongestOpponent = 0;
        foreach (GenericActorContext.TeamScoreState team
            in context.Scoreboard.Teams)
        {
            long value = 0;
            foreach (GenericActorContext.ScoreValue score in team.Scores)
            {
                if (string.Equals(
                    score.Channel,
                    _rankingChannel,
                    StringComparison.Ordinal))
                {
                    value = score.Value;
                    break;
                }
            }

            if (team.TeamId == _teamId)
            {
                own = value;
                foundOwn = true;
                continue;
            }
            if (!team.Eligible)
                continue;
            if (!foundOpponent
                || (_lowerScoreIsBetter
                    ? value < strongestOpponent
                    : value > strongestOpponent))
            {
                strongestOpponent = value;
                foundOpponent = true;
            }
        }

        if (!foundOwn || !foundOpponent)
            return 0;
        return _lowerScoreIsBetter
            ? strongestOpponent - own
            : own - strongestOpponent;
    }

    private GenericActorDecision? TryAttack(
        GenericActorContext context,
        ObjectiveData? objective)
    {
        if (!_forms.TryGetValue(
                context.Self.FormId,
                out FormData? form)
            || form.Attack is null
            || context.Enemies.IsEmpty)
        {
            return null;
        }

        GenericActorActionLegality? absolute =
            FindAvailable(context, _absoluteAttackActionIds);
        GenericActorActionLegality.ArgumentConstraint
            .ProjectileHeadingConstraint? headingConstraint =
            Constraint<GenericActorActionLegality.ArgumentConstraint
                .ProjectileHeadingConstraint>(absolute);
        if (absolute is not null && headingConstraint is not null)
        {
            GenericActorContext.ObservedEnemyState? target = null;
            ProjectileHeading selectedHeading = ProjectileHeading.North;
            foreach (GenericActorContext.ObservedEnemyState enemy
                in context.Enemies)
            {
                ProjectileHeading? heading = RayHeading(
                    context.Self.Position,
                    enemy.Position);
                if (heading is not ProjectileHeading value
                    || !Contains(
                        headingConstraint.AllowedValues,
                        value)
                    || !CanHit(
                        context.Self.Position,
                        enemy.Position,
                        value,
                        form.Attack.Projectile.MaxTravelTiles))
                {
                    continue;
                }
                if (target is null
                    || BetterTarget(
                        enemy,
                        target,
                        context.Self.Position,
                        objective))
                {
                    target = enemy;
                    selectedHeading = value;
                }
            }
            if (target is not null)
            {
                return Choose(
                    absolute,
                    $"absolute fire at {target.Position}",
                    new GenericActorActionArgument
                        .ProjectileHeadingArgument(selectedHeading));
            }
        }

        GenericActorActionLegality? shoot =
            FindAvailable(context, _mobileAttackActionIds);
        if (shoot is null)
            return null;

        GenericActorActionLegality.ArgumentConstraint.ShotProgramConstraint?
            programConstraint =
            Constraint<GenericActorActionLegality.ArgumentConstraint
                .ShotProgramConstraint>(shoot);
        GenericActorContext.ObservedEnemyState? selected = null;
        int selectedOffset = 0;
        bool selectedNeedsProgram = false;
        foreach (GenericActorContext.ObservedEnemyState enemy
            in context.Enemies)
        {
            ProjectileHeading? heading = RayHeading(
                context.Self.Position,
                enemy.Position);
            if (heading is not ProjectileHeading value
                || !CanHit(
                    context.Self.Position,
                    enemy.Position,
                    value,
                    form.Attack.Projectile.MaxTravelTiles))
            {
                continue;
            }

            int aimOffset = SignedHeadingDifference(
                context.Self.Facing.ToProjectileHeading(),
                value);
            bool needsProgram = aimOffset != 0;
            GenericActorRulesContract.ShotProgramDefinition program =
                form.Attack.ShotProgram;
            if (needsProgram
                && (programConstraint?.Allowed != true
                    || !program.Enabled
                    || aimOffset < program.MinInitialAimSteps
                    || aimOffset > program.MaxInitialAimSteps))
            {
                continue;
            }

            if (selected is null
                || BetterTarget(
                    enemy,
                    selected,
                    context.Self.Position,
                    objective))
            {
                selected = enemy;
                selectedOffset = aimOffset;
                selectedNeedsProgram = needsProgram;
            }
        }

        if (selected is null)
            return null;
        if (!selectedNeedsProgram)
        {
            return Choose(
                shoot,
                $"straight fire at {selected.Position}");
        }

        GenericActorRulesContract.AimOnlyShotProgramValue aimOnly =
            form.Attack.ShotProgram.AimOnlyProgram;
        return Choose(
            shoot,
            $"offset fire at {selected.Position}",
            new GenericActorActionArgument.ShotProgramArgument(
                new ShotProgram(
                    selectedOffset,
                    aimOnly.BendDirection,
                    aimOnly.BendAfterTiles,
                    aimOnly.BendEveryTiles,
                    aimOnly.BendCount)));
    }

    private GenericActorDecision? TryFabricate(
        GenericActorContext context,
        Situation situation)
    {
        GenericActorActionLegality? action =
            FindAvailable(context, _fabricateActionIds);
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            constraint =
            Constraint<GenericActorActionLegality.ArgumentConstraint
                .UnitTargetConstraint>(action);
        if (action is null || constraint is null)
            return null;

        bool protectLateLead = situation.Late
            && situation.Secure
            && situation.OwnMobileBodies
                > situation.VisibleEnemyMobileBodies;
        if (protectLateLead)
            return null;

        GenericActorActionArgument.UnitTarget? selected = null;
        foreach (GenericActorActionArgument.UnitTarget candidate
            in constraint.AllowedValues)
        {
            if (candidate.TeamId != _teamId
                || !IsReady(context, candidate))
            {
                continue;
            }
            if (selected is null
                || candidate.UnitId < selected.Value.UnitId)
            {
                selected = candidate;
            }
        }

        return selected is GenericActorActionArgument.UnitTarget target
            ? Choose(
                action,
                $"fabricating ready slot {target.UnitId}",
                new GenericActorActionArgument.UnitTargetArgument(
                    target))
            : null;
    }

    private GenericActorDecision? TrySplit(
        GenericActorContext context,
        Situation situation)
    {
        GenericActorActionLegality? action =
            FindAvailable(context, _splitActionIds);
        if (action is null || !action.Constraints.IsEmpty)
            return null;

        bool objectiveEmergency = situation.EnemyClaim
            && situation.VisibleEnemyObjectiveBodies
                >= Math.Max(1, situation.OwnObjectiveBodies);
        bool forceDeficit =
            situation.VisibleEnemyMobileBodies
                > situation.OwnMobileBodies;
        bool comeback = situation.ScoreMargin < 0
            && situation.AlliedMobileOnObjective == 0;
        bool lastPush = situation.Late && !situation.OwnClaim;
        return objectiveEmergency || forceDeficit || comeback || lastPush
            ? Choose(action, "splitting for territorial recovery")
            : null;
    }

    private GenericActorDecision Navigate(
        GenericActorContext context,
        ObjectiveData objective,
        Situation situation)
    {
        if (ObjectiveWeight(context.Self.FormId) <= 0)
        {
            return Face(context, _authoredForward)
                ?? Wait(context, "holding ranged support");
        }

        bool useSupport = situation.Secure
            && situation.AlliedMobileOnObjective > 0
            && !situation.EnemyClaim
            && objective.HasSupport;
        bool[] goals = useSupport
            ? objective.Support
            : objective.Tiles;
        if (IsMarked(goals, context.Self.Position))
        {
            return Face(context, DesiredFacing(context))
                ?? Wait(
                    context,
                    useSupport
                        ? "screening active objective"
                        : "holding active objective");
        }

        GenericActorActionLegality? move =
            FindAvailable(context, _moveActionIds);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions =
            Constraint<GenericActorActionLegality.ArgumentConstraint
                .DirectionConstraint>(move);
        if (move is not null && directions is not null)
        {
            MarkOccupied(context);
            Direction? step = FindFirstStep(
                context.Self.Position,
                goals,
                directions);
            if (step is Direction direction)
            {
                return Choose(
                    move,
                    useSupport
                        ? "moving to objective screen"
                        : "moving to active objective",
                    new GenericActorActionArgument.DirectionArgument(
                        direction));
            }
        }

        return Face(context, DesiredFacing(context))
            ?? Wait(context, "route temporarily blocked");
    }

    private Direction DesiredFacing(GenericActorContext context)
    {
        GenericActorContext.ObservedEnemyState? nearest = null;
        int nearestDistance = int.MaxValue;
        foreach (GenericActorContext.ObservedEnemyState enemy
            in context.Enemies)
        {
            int distance =
                context.Self.Position.ChebyshevDistance(enemy.Position);
            if (distance < nearestDistance
                || distance == nearestDistance
                    && (nearest is null
                        || enemy.ActorId.CompareTo(nearest.ActorId) < 0))
            {
                nearest = enemy;
                nearestDistance = distance;
            }
        }
        return nearest is null
            ? _authoredForward
            : CardinalBearing(
                context.Self.Position,
                nearest.Position,
                _authoredForward);
    }

    private GenericActorDecision? Face(
        GenericActorContext context,
        Direction direction)
    {
        if (context.Self.Facing == direction)
            return null;

        GenericActorActionLegality? rotate =
            FindAvailable(context, _rotateActionIds);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            constraint =
            Constraint<GenericActorActionLegality.ArgumentConstraint
                .DirectionConstraint>(rotate);
        return rotate is not null
            && constraint is not null
            && Contains(constraint.AllowedValues, direction)
                ? Choose(
                    rotate,
                    $"facing {direction}",
                    new GenericActorActionArgument.DirectionArgument(
                        direction))
                : null;
    }

    private Direction? FindFirstStep(
        Position start,
        bool[] goals,
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint
            allowed)
    {
        int startIndex = Index(start);
        if (startIndex < 0 || goals[startIndex])
            return null;

        int generation = NextGeneration(
            _seen,
            ref _seenGeneration);
        _seen[startIndex] = generation;
        int head = 0;
        int tail = 0;

        foreach (Direction direction in Directions)
        {
            if (!Contains(allowed.AllowedValues, direction))
                continue;
            Position next = Offset(start, direction);
            int index = Index(next);
            if (!CanEnter(index)
                || _seen[index] == generation)
            {
                continue;
            }
            _seen[index] = generation;
            _firstStep[index] = direction;
            if (goals[index])
                return direction;
            _queue[tail++] = index;
        }

        while (head < tail)
        {
            int current = _queue[head++];
            Position position =
                new(current % _width, current / _width);
            foreach (Direction direction in Directions)
            {
                int next = Index(Offset(position, direction));
                if (!CanEnter(next)
                    || _seen[next] == generation)
                {
                    continue;
                }
                _seen[next] = generation;
                _firstStep[next] = _firstStep[current];
                if (goals[next])
                    return _firstStep[next];
                _queue[tail++] = next;
            }
        }
        return null;
    }

    private void MarkOccupied(GenericActorContext context)
    {
        int generation = NextGeneration(
            _blocked,
            ref _blockedGeneration);
        foreach (GenericActorContext.ObservedAllyState ally
            in context.Allies)
        {
            MarkBlocked(ally.Position, generation);
        }
        foreach (GenericActorContext.ObservedEnemyState enemy
            in context.Enemies)
        {
            MarkBlocked(enemy.Position, generation);
        }
        if (context.VisibleProjectiles is { } projectiles)
        {
            foreach (GenericActorContext.ObservedProjectile projectile
                in projectiles)
            {
                MarkBlocked(projectile.Position, generation);
            }
        }
    }

    private void MarkBlocked(Position position, int generation)
    {
        int index = Index(position);
        if (index >= 0)
            _blocked[index] = generation;
    }

    private bool CanEnter(int index) =>
        index >= 0
        && _walkable[index]
        && _blocked[index] != _blockedGeneration;

    private bool CanHit(
        Position from,
        Position target,
        ProjectileHeading heading,
        int maximumRange)
    {
        int distance = from.ChebyshevDistance(target);
        if (distance <= 0 || distance > maximumRange)
            return false;

        Position position = from;
        var (dx, dy) = heading.Vector();
        for (int step = 0; step < distance; step++)
        {
            Position next = position.Offset(dx, dy);
            if (!IsOpen(next))
                return false;
            if (dx != 0 && dy != 0
                && (!IsOpen(position.Offset(dx, 0))
                    || !IsOpen(position.Offset(0, dy))))
            {
                return false;
            }
            position = next;
        }
        return position == target;
    }

    private bool BetterTarget(
        GenericActorContext.ObservedEnemyState candidate,
        GenericActorContext.ObservedEnemyState current,
        Position origin,
        ObjectiveData? objective)
    {
        bool candidateOnObjective = objective is not null
            && IsMarked(objective.Tiles, candidate.Position);
        bool currentOnObjective = objective is not null
            && IsMarked(objective.Tiles, current.Position);
        if (candidateOnObjective != currentOnObjective)
            return candidateOnObjective;
        bool candidatePending =
            candidate.PendingSameLifeTransition is not null;
        bool currentPending =
            current.PendingSameLifeTransition is not null;
        if (candidatePending != currentPending)
            return candidatePending;
        if (candidate.Health != current.Health)
            return candidate.Health < current.Health;
        int candidateDistance =
            origin.ChebyshevDistance(candidate.Position);
        int currentDistance =
            origin.ChebyshevDistance(current.Position);
        return candidateDistance != currentDistance
            ? candidateDistance < currentDistance
            : candidate.ActorId.CompareTo(current.ActorId) < 0;
    }

    private bool IsReady(
        GenericActorContext context,
        GenericActorActionArgument.UnitTarget target)
    {
        foreach (GenericActorContext.ObservedUnitSlot slot
            in context.TeamUnits)
        {
            if (slot.TeamId == target.TeamId
                && slot.UnitId == target.UnitId)
            {
                return slot.State
                    is GenericActorContext.UnitSlotState.Ready;
            }
        }
        return false;
    }

    private int ObjectiveWeight(string formId) =>
        _forms.TryGetValue(formId, out FormData? form)
            ? form.ObjectiveWeight
            : 0;

    private GenericActorDecision Wait(
        GenericActorContext context,
        string debug)
    {
        GenericActorActionLegality? wait =
            FindAction(context, _waitActionIds, requireAvailable: false);
        if (wait is not null)
        {
            return GenericActorDecision.WithoutArguments(
                wait.ActionId,
                wait.ActionCode,
                debug);
        }

        foreach (GenericActorActionLegality action
            in context.ActionLegalities)
        {
            if (action.Available && action.Constraints.IsEmpty)
            {
                return GenericActorDecision.WithoutArguments(
                    action.ActionId,
                    action.ActionCode,
                    debug);
            }
        }

        GenericActorActionLegality fallback =
            context.ActionLegalities[0];
        return GenericActorDecision.WithoutArguments(
            fallback.ActionId,
            fallback.ActionCode,
            debug);
    }

    private static GenericActorDecision Choose(
        GenericActorActionLegality action,
        string debug,
        params GenericActorActionArgument[] arguments) =>
        new(
            action.ActionId,
            action.ActionCode,
            arguments,
            debug);

    private static T? Constraint<T>(
        GenericActorActionLegality? action)
        where T : GenericActorActionLegality.ArgumentConstraint
    {
        if (action is null)
            return null;
        foreach (GenericActorActionLegality.ArgumentConstraint constraint
            in action.Constraints)
        {
            if (constraint is T typed)
                return typed;
        }
        return null;
    }

    private static GenericActorActionLegality? FindAvailable(
        GenericActorContext context,
        string[] actionIds) =>
        FindAction(context, actionIds, requireAvailable: true);

    private static GenericActorActionLegality? FindAction(
        GenericActorContext context,
        string[] actionIds,
        bool requireAvailable)
    {
        foreach (GenericActorActionLegality action
            in context.ActionLegalities)
        {
            if (requireAvailable && !action.Available)
                continue;
            foreach (string actionId in actionIds)
            {
                if (string.Equals(
                    action.ActionId,
                    actionId,
                    StringComparison.Ordinal))
                {
                    return action;
                }
            }
        }
        return null;
    }

    private void CacheForms(
        GenericActorResolvedMatchContract contract)
    {
        _forms.Clear();
        foreach (GenericActorRulesContract.Form form
            in contract.Rules.Forms)
        {
            GenericActorRulesContract.AttackProfile? attack = null;
            if (form.AttackProfileId is not null)
            {
                foreach (GenericActorRulesContract.AttackProfile candidate
                    in contract.Rules.AttackProfiles)
                {
                    if (string.Equals(
                        candidate.Id,
                        form.AttackProfileId,
                        StringComparison.Ordinal))
                    {
                        attack = candidate;
                        break;
                    }
                }
            }
            _forms[form.Id] = new FormData(
                form.Id,
                form.ObjectiveWeight,
                attack);
        }
    }

    private void CacheActions(
        GenericActorResolvedMatchContract contract)
    {
        var waits = new List<string>();
        var moves = new List<string>();
        var rotates = new List<string>();
        var mobileAttacks = new List<string>();
        var absoluteAttacks = new List<string>();
        foreach (GenericActorRulesContract.ActionDefinition action
            in contract.Rules.Actions)
        {
            switch (action.Kind)
            {
                case GenericActorRulesContract.ActionKind.Wait:
                    AddUnique(waits, action.Id);
                    break;
                case GenericActorRulesContract.ActionKind.Movement:
                    AddUnique(moves, action.Id);
                    break;
                case GenericActorRulesContract.ActionKind.Rotation:
                    AddUnique(rotates, action.Id);
                    break;
                case GenericActorRulesContract.ActionKind.Attack:
                    bool absolute = false;
                    foreach (GenericActorRulesContract.ActionParameterKind kind
                        in action.ParameterKinds)
                    {
                        if (kind
                            == GenericActorRulesContract.ActionParameterKind
                                .ProjectileHeading)
                        {
                            absolute = true;
                            break;
                        }
                    }
                    AddUnique(
                        absolute ? absoluteAttacks : mobileAttacks,
                        action.Id);
                    break;
            }
        }

        var fabricates = new List<string>();
        foreach (GenericActorRulesContract.FabricationTransition transition
            in contract.Rules.FabricationTransitions)
        {
            AddUnique(fabricates, transition.ActionId);
        }
        var splits = new List<string>();
        foreach (GenericActorRulesContract.ReplicationTransition transition
            in contract.Rules.ReplicationTransitions)
        {
            AddUnique(splits, transition.ActionId);
        }

        _waitActionIds = waits.ToArray();
        _moveActionIds = moves.ToArray();
        _rotateActionIds = rotates.ToArray();
        _mobileAttackActionIds = mobileAttacks.ToArray();
        _absoluteAttackActionIds = absoluteAttacks.ToArray();
        _fabricateActionIds = fabricates.ToArray();
        _splitActionIds = splits.ToArray();
    }

    private void CacheMap(
        GenericActorResolvedMatchContract contract)
    {
        _width = contract.Map.Width;
        _height = contract.Map.Height;
        int cellCount = checked(_width * _height);
        _walkable = new bool[cellCount];
        _seen = new int[cellCount];
        _blocked = new int[cellCount];
        _queue = new int[cellCount];
        _firstStep = new Direction[cellCount];

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                _walkable[y * _width + x] =
                    contract.Map.TileRows[y][x] != '#';
            }
        }

        var forbidden = new bool[cellCount];
        foreach (GenericActorMapContract.TileTag tag
            in contract.Map.TileTags)
        {
            if (tag.Kind
                != GenericActorMapContract.TileTagKind
                    .TransitionPlacementForbidden)
            {
                continue;
            }
            foreach (Position tile in tag.Tiles)
            {
                int index = Index(tile);
                if (index >= 0)
                    forbidden[index] = true;
            }
        }

        if (_frontline is null)
        {
            _objectives = [];
            return;
        }

        _objectives =
            new ObjectiveData[
                _frontline.OrderedObjectiveRegionIds.Length];
        for (int objectiveIndex = 0;
            objectiveIndex < _objectives.Length;
            objectiveIndex++)
        {
            var tiles = new bool[cellCount];
            string regionId =
                _frontline.OrderedObjectiveRegionIds[objectiveIndex];
            foreach (GenericActorMapContract.Region region
                in contract.Map.Regions)
            {
                if (!string.Equals(
                    region.RegionId,
                    regionId,
                    StringComparison.Ordinal))
                {
                    continue;
                }
                foreach (Position tile in region.Tiles)
                {
                    int index = Index(tile);
                    if (index >= 0)
                        tiles[index] = true;
                }
                break;
            }

            var support = new bool[cellCount];
            bool hasSupport = false;
            for (int index = 0; index < tiles.Length; index++)
            {
                if (!tiles[index])
                    continue;
                Position tile = new(
                    index % _width,
                    index / _width);
                foreach (Direction direction in Directions)
                {
                    int candidate = Index(Offset(tile, direction));
                    if (candidate >= 0
                        && _walkable[candidate]
                        && !tiles[candidate]
                        && !forbidden[candidate])
                    {
                        support[candidate] = true;
                        hasSupport = true;
                    }
                }
            }
            _objectives[objectiveIndex] =
                new ObjectiveData(tiles, support, hasSupport);
        }
    }

    private int Index(Position position) =>
        position.X < 0
        || position.Y < 0
        || position.X >= _width
        || position.Y >= _height
            ? -1
            : position.Y * _width + position.X;

    private bool IsOpen(Position position)
    {
        int index = Index(position);
        return index >= 0 && _walkable[index];
    }

    private bool IsMarked(bool[] marks, Position position)
    {
        int index = Index(position);
        return index >= 0 && marks[index];
    }

    private static int NextGeneration(
        int[] marks,
        ref int generation)
    {
        if (generation == int.MaxValue)
        {
            Array.Clear(marks, 0, marks.Length);
            generation = 1;
        }
        else
        {
            generation++;
        }
        return generation;
    }

    private static void AddUnique(
        List<string> values,
        string value)
    {
        foreach (string existing in values)
        {
            if (string.Equals(
                existing,
                value,
                StringComparison.Ordinal))
            {
                return;
            }
        }
        values.Add(value);
    }

    private static bool Contains<T>(
        IEnumerable<T> values,
        T expected)
        where T : struct, Enum
    {
        foreach (T value in values)
        {
            if (EqualityComparer<T>.Default.Equals(
                value,
                expected))
            {
                return true;
            }
        }
        return false;
    }

    private static Position Offset(
        Position position,
        Direction direction)
    {
        var (dx, dy) = direction.Vector();
        return position.Offset(dx, dy);
    }

    private static ProjectileHeading? RayHeading(
        Position from,
        Position target)
    {
        int dx = target.X - from.X;
        int dy = target.Y - from.Y;
        if (dx == 0 && dy == 0)
            return null;
        if (dx != 0
            && dy != 0
            && Math.Abs(dx) != Math.Abs(dy))
        {
            return null;
        }

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

    private static int SignedHeadingDifference(
        ProjectileHeading from,
        ProjectileHeading to)
    {
        int difference = ((int)to - (int)from + 8) % 8;
        return difference > 4
            ? difference - 8
            : difference;
    }

    private static Direction CardinalBearing(
        Position from,
        Position target,
        Direction tieBreak)
    {
        int dx = target.X - from.X;
        int dy = target.Y - from.Y;
        if (Math.Abs(dx) > Math.Abs(dy))
            return dx >= 0 ? Direction.East : Direction.West;
        if (Math.Abs(dy) > Math.Abs(dx))
            return dy >= 0 ? Direction.South : Direction.North;
        return tieBreak;
    }

    private sealed record FormData(
        string Id,
        int ObjectiveWeight,
        GenericActorRulesContract.AttackProfile? Attack);

    private sealed record ObjectiveData(
        bool[] Tiles,
        bool[] Support,
        bool HasSupport);

    private readonly record struct Situation(
        long ScoreMargin,
        bool OwnClaim,
        bool EnemyClaim,
        bool Secure,
        bool Late,
        int OwnMobileBodies,
        int AlliedMobileOnObjective,
        int OwnObjectiveBodies,
        int VisibleEnemyMobileBodies,
        int VisibleEnemyObjectiveBodies);
}
