using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// Frozen Arc Relay stock mind v0. Commander choices arrive as separately
/// hashed evaluation data; the engine below is held byte-identical across
/// sheet-space experiments.
/// </summary>
public sealed class ArcRelayStockMind : IGenericMindBot
{
    private static readonly ProjectileHeading[] Headings =
        Enum.GetValues<ProjectileHeading>();

    private readonly Dictionary<int, int> _outboundIndex = [];
    private readonly Dictionary<int, int> _returnIndex = [];
    private readonly Dictionary<int, int> _blockedTicks = [];
    private readonly Dictionary<int, bool> _carrying = [];
    private readonly Dictionary<string, bool> _triggerWasActive = [];
    private readonly Dictionary<string, int> _gambitCooldownUntil = [];
    private readonly Dictionary<GenericActorContext.ArcRelayCoreId, ActorIdentity>
        _lastCarrierByCore = [];
    private readonly Dictionary<GenericActorContext.ArcRelayCoreId, ActorIdentity>
        _previousCarrierByCore = [];

    private GenericActorResolvedMatchContract? _contract;
    private StockSheet? _sheet;
    private int _teamId;
    private bool _mirror;
    private string? _activeGambitId;
    private int _activeGambitUntil;
    private int? _handledPulseTick;
    private int _lastLiveCount = 8;

    public void StartMatch(MindStart start)
    {
        _sheet = StockSheet.Load(start.EvaluationData);
        if (!string.Equals(
                start.Contract.Map.MapId,
                Sheet.MapId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Sheet {Sheet.SheetId} targets {Sheet.MapId}, not "
                + start.Contract.Map.MapId + ".");
        }
        if (start.Contract.Rules.GameMode
            is not GenericActorRulesContract.ArcRelayGameMode)
        {
            throw new InvalidOperationException(
                "ArcRelayStockMind requires the Arc Relay mode.");
        }

        _contract = start.Contract;
        _teamId = start.TeamId;
        Position reactor = OwnReactor(start.Contract, start.ParticipantId);
        _mirror = reactor.X > (start.Contract.Map.Width - 1) / 2;
        foreach (UnitPlan plan in Sheet.Units)
        {
            _outboundIndex[plan.UnitId] = 0;
            _returnIndex[plan.UnitId] = 0;
            _blockedTicks[plan.UnitId] = 0;
            _carrying[plan.UnitId] = false;
        }
        foreach (GambitPlan gambit in Sheet.Gambits)
        {
            _triggerWasActive[gambit.Id] = false;
            _gambitCooldownUntil[gambit.Id] = 0;
        }
    }

    public void Think(MindContext mind)
    {
        GenericActorResolvedMatchContract contract = _contract
            ?? throw new InvalidOperationException("StartMatch was not called.");
        GenericActorContext.ModeObservationState.ArcRelay arc = Arc(mind);

        ObserveCoreCarriers(arc);
        ObserveRouteProgress(mind, arc);
        UpdateGambit(mind, arc);

        var claims = mind.Bodies.Select(body => body.Position).ToHashSet();
        Position reactor = arc.Reactors.Single(value => value.TeamId == _teamId)
            .Position;
        HashSet<ActorIdentity> ownCarriers = arc.VisibleCores
            .Where(core => core.CarrierActorId?.TeamId == _teamId)
            .Select(core => core.CarrierActorId!)
            .ToHashSet();
        HashSet<ActorIdentity> enemyCarriers = arc.VisibleCores
            .Where(core => core.CarrierActorId is { } carrier
                && carrier.TeamId != _teamId)
            .Select(core => core.CarrierActorId!)
            .ToHashSet();

        foreach (MindBody body in mind.Bodies.OrderBy(value => value.UnitId))
        {
            UnitPlan plan = Sheet.Units.Single(value =>
                value.UnitId == body.UnitId);
            string role = EffectiveRole(plan);
            if (!string.Equals(body.RoleTag, role, StringComparison.Ordinal))
                body.SetRole(role);

            bool carries = ownCarriers.Contains(body.ActorId);
            GenericActorContext.ArcRelayCoreState? carriedCore = carries
                ? arc.VisibleCores.Single(core =>
                    core.CarrierActorId == body.ActorId)
                : null;
            Position goal = Goal(
                mind,
                arc,
                body,
                plan,
                role,
                carries,
                ownCarriers,
                enemyCarriers,
                reactor);

            if (carries
                && body.Health <= Sheet.Carrier.HandoffHealthAtOrBelow
                && TryHandoff(mind, body, reactor, carriedCore!))
            {
                continue;
            }
            if (TrySignature(contract, mind, body, goal, enemyCarriers, carries))
                continue;
            if (TryShoot(contract, mind, body, enemyCarriers))
                continue;
            if (TryMove(contract, mind, body, goal, claims))
                continue;
            body.Hold(carries ? "holding core route" : "holding assignment");
        }

        _lastLiveCount = mind.Bodies.Length;
    }

    public void EndMatch(MindEnd end) => _ = end;

    private void ObserveCoreCarriers(
        GenericActorContext.ModeObservationState.ArcRelay arc)
    {
        foreach (GenericActorContext.ArcRelayCoreState core in arc.VisibleCores)
        {
            if (core.CarrierActorId is not { } carrier)
                continue;
            if (_lastCarrierByCore.TryGetValue(core.CoreId, out var last)
                && last != carrier)
            {
                _previousCarrierByCore[core.CoreId] = last;
            }
            _lastCarrierByCore[core.CoreId] = carrier;
        }
    }

    private void ObserveRouteProgress(
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc)
    {
        HashSet<ActorIdentity> carriers = arc.VisibleCores
            .Where(core => core.CarrierActorId?.TeamId == _teamId)
            .Select(core => core.CarrierActorId!)
            .ToHashSet();
        foreach (MindBody body in mind.Bodies)
        {
            bool carries = carriers.Contains(body.ActorId);
            if (body.LifeStartedTick == mind.Tick)
            {
                _outboundIndex[body.UnitId] = 0;
                _returnIndex[body.UnitId] = 0;
                _blockedTicks[body.UnitId] = 0;
            }
            if (carries && !_carrying[body.UnitId])
                _returnIndex[body.UnitId] = 0;
            if (!carries && _carrying[body.UnitId])
                _outboundIndex[body.UnitId] = 0;
            _carrying[body.UnitId] = carries;

            if (body.PreviousActionResolution?.Outcome
                == GenericActorActionResolution.ActionOutcome.Blocked)
            {
                _blockedTicks[body.UnitId]++;
            }
            else if (body.MovedLastTick)
            {
                _blockedTicks[body.UnitId] = 0;
            }
        }
    }

    private void UpdateGambit(
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc)
    {
        bool newPulse = arc.LatestPulseTick is int pulseTick
            && pulseTick != _handledPulseTick;
        bool ownPulse = newPulse && arc.LatestPulseTeamId == _teamId;
        bool enemyPulse = newPulse && arc.LatestPulseTeamId != _teamId;
        bool doubleEnemyPossession = arc.VisibleCores.Count(core =>
            core.CarrierActorId is { } carrier && carrier.TeamId != _teamId) >= 2;
        bool wipe = mind.Bodies.IsEmpty && _lastLiveCount > 0;
        if (_activeGambitId is not null && mind.Tick >= _activeGambitUntil)
            _activeGambitId = null;

        var conditions = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["after-enemy-pulse"] = enemyPulse,
            ["double-enemy-possession"] = doubleEnemyPossession,
            ["after-own-pulse"] = ownPulse,
            ["wipe"] = wipe,
            ["route-failure"] = _blockedTicks.Values.Any(value =>
                value >= Sheet.Carrier.RouteFailureTicks),
        };

        if (_activeGambitId is null)
        {
            foreach (GambitPlan gambit in Sheet.Gambits)
            {
                bool active = conditions[gambit.Trigger];
                bool risingEdge = active && !_triggerWasActive[gambit.Id];
                if (!risingEdge
                    || mind.Tick < _gambitCooldownUntil[gambit.Id])
                {
                    continue;
                }
                _activeGambitId = gambit.Id;
                _activeGambitUntil = mind.Tick + gambit.DurationTicks;
                _gambitCooldownUntil[gambit.Id] =
                    mind.Tick + gambit.CooldownTicks;
                break;
            }
        }

        foreach (GambitPlan gambit in Sheet.Gambits)
            _triggerWasActive[gambit.Id] = conditions[gambit.Trigger];
        if (newPulse)
            _handledPulseTick = arc.LatestPulseTick;
    }

    private string EffectiveRole(UnitPlan plan)
    {
        GambitPlan? gambit = ActiveGambit();
        return gambit is not null && Applies(gambit, plan)
            ? gambit.RoleOverride
            : plan.Role;
    }

    private static bool Applies(GambitPlan gambit, UnitPlan plan) =>
        gambit.ScopeRoles.Contains(plan.Role, StringComparer.Ordinal);

    private GambitPlan? ActiveGambit() => _activeGambitId is null
        ? null
        : Sheet.Gambits.Single(value =>
            string.Equals(value.Id, _activeGambitId, StringComparison.Ordinal));

    private Position Goal(
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        UnitPlan plan,
        string role,
        bool carries,
        IReadOnlySet<ActorIdentity> ownCarriers,
        IReadOnlySet<ActorIdentity> enemyCarriers,
        Position reactor)
    {
        if (carries)
            return NextPathGoal(body, plan.ReturnPath, _returnIndex, reactor);

        GambitPlan? gambit = ActiveGambit();
        if (gambit is not null && Applies(gambit, plan))
        {
            Position rally = Closest(
                body.Position,
                Sheet.RallyLines[gambit.RallyLine]
                    .Select(Mirror).ToArray());
            if (body.Position.ChebyshevDistance(rally) > 1)
                return rally;
        }

        GenericActorContext.ObservedEnemyState? enemyCarrier = mind.Enemies
            .Where(enemy => enemyCarriers.Contains(enemy.ActorId))
            .OrderBy(enemy => body.Position.ChebyshevDistance(enemy.Position))
            .ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault();
        if (role == "intercept"
            && Sheet.Interception.FocusEnemyCarrier
            && enemyCarrier is not null)
            return enemyCarrier.Position;

        if (role == "screen")
        {
            MindBody? partner = mind.Body(plan.PartnerUnitId);
            MindBody? protectedBody = mind.Bodies
                .Where(candidate => ownCarriers.Contains(candidate.ActorId))
                .OrderBy(candidate => candidate.UnitId == plan.PartnerUnitId ? 0 : 1)
                .ThenBy(candidate =>
                    body.Position.ChebyshevDistance(candidate.Position))
                .ThenBy(candidate => candidate.UnitId)
                .FirstOrDefault()
                ?? partner;
            if (protectedBody is not null)
            {
                if (body.Position.ChebyshevDistance(protectedBody.Position)
                    <= Sheet.Escort.FollowDistance)
                {
                    return body.Position;
                }
                Position[] screen = Adjacent(protectedBody.Position)
                    .Where(value => !IsWall(_contract!.Map, value))
                    .ToArray();
                if (screen.Length > 0)
                    return Closest(body.Position, screen);
            }
        }

        Zone theater = Sheet.Zones[plan.Theater];
        GenericActorContext.ArcRelayCoreState? loose = arc.VisibleCores
            .Where(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Loose)
            .Where(core => !Sheet.Carrier.PreferAssignedTheater
                || theater.Contains(Unmirror(core.Position)))
            .OrderBy(core => body.Position.ChebyshevDistance(core.Position))
            .ThenBy(core => core.CoreId.SourceWellId, StringComparer.Ordinal)
            .ThenBy(core => core.CoreId.SourceOrdinal)
            .FirstOrDefault();
        if (loose is not null && role is "carrier" or "reserve" or "intercept")
            return loose.Position;

        if (role == "intercept" && Sheet.Interception.LooseCoreFallback)
        {
            loose = arc.VisibleCores
                .Where(core => core.Disposition
                    == GenericActorContext.ArcRelayCoreDisposition.Loose)
                .OrderBy(core => body.Position.ChebyshevDistance(core.Position))
                .ThenBy(core => core.CoreId.SourceWellId, StringComparer.Ordinal)
                .ThenBy(core => core.CoreId.SourceOrdinal)
                .FirstOrDefault();
            if (loose is not null)
                return loose.Position;
        }

        return NextPathGoal(
            body,
            plan.OutboundPath,
            _outboundIndex,
            WellFor(arc, plan.Theater));
    }

    private Position NextPathGoal(
        MindBody body,
        Position[] canonicalPath,
        Dictionary<int, int> indexes,
        Position fallback)
    {
        int index = indexes[body.UnitId];
        while (index < canonicalPath.Length
               && body.Position.ChebyshevDistance(Mirror(canonicalPath[index])) <= 1)
        {
            index++;
        }
        indexes[body.UnitId] = index;
        return index < canonicalPath.Length
            ? Mirror(canonicalPath[index])
            : fallback;
    }

    private bool TryHandoff(
        MindContext mind,
        MindBody body,
        Position reactor,
        GenericActorContext.ArcRelayCoreState core)
    {
        GenericActorActionLegality? handoff = body.Action("handoff-core");
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = handoff?.Constraints.OfType<GenericActorActionLegality
                .ArgumentConstraint.UnitTargetConstraint>().SingleOrDefault();
        if (handoff is not { Available: true }
            || targets is null
            || targets.AllowedValues.IsEmpty)
        {
            return false;
        }
        ActorIdentity? previous = _previousCarrierByCore.GetValueOrDefault(
            core.CoreId);
        GenericActorActionArgument.UnitTarget? target = targets.AllowedValues
            .Where(value => previous is null
                || value.TeamId != previous.TeamId
                || value.UnitId != previous.UnitId)
            .OrderBy(value => mind.Body(value.UnitId)?.Position
                .ChebyshevDistance(reactor) ?? int.MaxValue)
            .ThenBy(value => value.UnitId)
            .Select(value =>
                (GenericActorActionArgument.UnitTarget?)value)
            .FirstOrDefault();
        if (target is null)
            return false;
        body.Command(
            handoff,
            new GenericActorActionArgument.UnitTargetArgument(target.Value));
        return true;
    }

    private static bool TrySignature(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Position goal,
        IReadOnlySet<ActorIdentity> enemyCarriers,
        bool carries)
    {
        HashSet<string> ids = contract.Rules.Actions
            .Where(action => action.Kind
                == GenericActorRulesContract.ActionKind.Signature)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        GenericActorActionLegality? signature = body.ActionLegalities
            .Where(action => action.Available && ids.Contains(action.ActionId))
            .OrderBy(action => action.ActionCode)
            .FirstOrDefault();
        if (signature is null || mind.Tick % 3 != body.UnitId % 3)
            return false;
        if (string.Equals(signature.ActionId, "arc-toss", StringComparison.Ordinal)
            && !carries)
        {
            return false;
        }

        List<GenericActorActionArgument>? arguments = Arguments(
            signature,
            mind,
            body,
            goal,
            enemyCarriers);
        if (arguments is null)
            return false;
        body.Command(signature, [.. arguments]);
        return true;
    }

    private static List<GenericActorActionArgument>? Arguments(
        GenericActorActionLegality action,
        MindContext mind,
        MindBody body,
        Position goal,
        IReadOnlySet<ActorIdentity> enemyCarriers)
    {
        var result = new List<GenericActorActionArgument>();
        foreach (GenericActorActionLegality.ArgumentConstraint constraint
                 in action.Constraints)
        {
            switch (constraint)
            {
                case GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint value:
                    if (value.AllowedValues.IsEmpty)
                        return null;
                    result.Add(new GenericActorActionArgument.DirectionArgument(
                        ClosestDirection(body.Position, goal, value.AllowedValues)));
                    break;
                case GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint value:
                    if (value.AllowedValues.IsEmpty)
                        return null;
                    result.Add(new GenericActorActionArgument
                        .ProjectileHeadingArgument(
                            ClosestHeading(body.Position, goal, value.AllowedValues)));
                    break;
                case GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint value:
                    if (value.AllowedValues.IsEmpty)
                        return null;
                    GenericActorActionArgument.UnitTarget target =
                        value.AllowedValues
                            .OrderBy(candidate => TargetRank(
                                mind, candidate, enemyCarriers))
                            .ThenBy(candidate => candidate.TeamId)
                            .ThenBy(candidate => candidate.UnitId)
                            .First();
                    result.Add(new GenericActorActionArgument.UnitTargetArgument(
                        target));
                    break;
                case GenericActorActionLegality.ArgumentConstraint
                    .PositionTargetConstraint value:
                    if (value.AllowedValues.IsEmpty)
                        return null;
                    IEnumerable<Position> positionTargets = value.AllowedValues;
                    if (positionTargets.Any(position =>
                            position != body.Position))
                    {
                        // Survey Flare's transport shape is [source,target];
                        // choosing source would make that shape degenerate.
                        positionTargets = positionTargets.Where(position =>
                            position != body.Position);
                    }
                    result.Add(new GenericActorActionArgument
                        .PositionTargetArgument(positionTargets
                            .OrderBy(position =>
                                position.ChebyshevDistance(goal))
                            .ThenBy(position => position.Y)
                            .ThenBy(position => position.X)
                            .First()));
                    break;
                case GenericActorActionLegality.ArgumentConstraint
                    .FormTargetConstraint value:
                    if (value.AllowedFormIds.IsEmpty)
                        return null;
                    result.Add(new GenericActorActionArgument.FormTargetArgument(
                        value.AllowedFormIds[0]));
                    break;
                case GenericActorActionLegality.ArgumentConstraint
                    .ShotProgramConstraint value when value.Allowed:
                    result.Add(new GenericActorActionArgument.ShotProgramArgument(
                        ShotProgram.Straight));
                    break;
                default:
                    return null;
            }
        }
        return result;
    }

    private static int TargetRank(
        MindContext mind,
        GenericActorActionArgument.UnitTarget candidate,
        IReadOnlySet<ActorIdentity> enemyCarriers)
    {
        GenericActorContext.ObservedEnemyState? enemy = mind.Enemies
            .FirstOrDefault(value => value.ActorId.TeamId == candidate.TeamId
                && value.ActorId.UnitId == candidate.UnitId);
        if (enemy is not null)
            return enemyCarriers.Contains(enemy.ActorId) ? 0 : 1;
        MindBody? ally = mind.Body(candidate.UnitId);
        return ally is null ? 4 : 2 + ally.Health;
    }

    private static bool TryShoot(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        IReadOnlySet<ActorIdentity> enemyCarriers)
    {
        GenericActorActionLegality? shoot = contract.Rules.Actions
            .Where(action => action.Kind
                == GenericActorRulesContract.ActionKind.Attack)
            .Select(action => body.Action(action.Id))
            .FirstOrDefault(action => action is { Available: true });
        GenericActorActionLegality.ArgumentConstraint
            .ProjectileHeadingConstraint? headings = shoot?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>().SingleOrDefault();
        if (shoot is null || headings is null)
            return false;

        GenericActorRulesContract.Form form = contract.Rules.Forms.Single(value =>
            string.Equals(value.Id, body.FormId, StringComparison.Ordinal));
        int range = form.AttackProfileId is null
            ? 0
            : contract.Rules.AttackProfiles.Single(value =>
                string.Equals(value.Id, form.AttackProfileId,
                    StringComparison.Ordinal)).Projectile.MaxTravelTiles;
        foreach (GenericActorContext.ObservedEnemyState enemy in mind.Enemies
                     .OrderByDescending(value => enemyCarriers.Contains(value.ActorId))
                     .ThenBy(value => body.Position.ChebyshevDistance(value.Position))
                     .ThenBy(value => value.ActorId))
        {
            if (body.Position.ChebyshevDistance(enemy.Position) > range
                || !TryExactHeading(body.Position, enemy.Position,
                    out ProjectileHeading heading)
                || !headings.AllowedValues.Contains(heading))
            {
                continue;
            }
            body.Command(
                shoot,
                new GenericActorActionArgument.ProjectileHeadingArgument(heading));
            return true;
        }
        return false;
    }

    private bool TryMove(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Position goal,
        HashSet<Position> claims)
    {
        if (body.Position == goal)
            return false;
        GenericActorActionLegality? move = contract.Rules.Actions
            .Where(action => action.Kind
                == GenericActorRulesContract.ActionKind.Movement)
            .Select(action => body.Action(action.Id))
            .FirstOrDefault(action => action is { Available: true });
        GenericActorActionLegality.ArgumentConstraint
            .ProjectileHeadingConstraint? allowed = move?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>().SingleOrDefault();
        if (move is null || allowed is null || allowed.AllowedValues.IsEmpty)
            return TryRotate(contract, body, goal);

        HashSet<Position> occupied = claims
            .Concat(mind.Allies.Select(value => value.Position))
            .Concat(mind.Enemies.Select(value => value.Position))
            .Concat(mind.VisibleTiles
                .Where(value => value.SpawnReservation is not null)
                .Select(value => value.Position))
            .ToHashSet();
        occupied.Remove(body.Position);
        foreach (GenericActorContext.ObservedProjectile projectile
                 in mind.VisibleProjectiles ?? [])
        {
            if (projectile.OwnerTeamId != _teamId)
                occupied.Add(projectile.Position);
        }

        ProjectileHeading? step = FirstStep(
            contract.Map,
            body.Position,
            goal,
            occupied,
            SearchOrder());
        if (step is null)
            return false;
        if (!allowed.AllowedValues.Contains(step.Value))
            return TryRotate(contract, body, CardinalForStep(step.Value, goal,
                body.Position));

        (int dx, int dy) = step.Value.Vector();
        Position destination = body.Position.Offset(dx, dy);
        claims.Add(destination);
        body.Command(
            move,
            new GenericActorActionArgument.ProjectileHeadingArgument(step.Value));
        return true;
    }

    private static bool TryRotate(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Position goal)
    {
        Direction desired = ClosestDirection(
            body.Position,
            goal,
            Enum.GetValues<Direction>());
        return TryRotate(contract, body, desired);
    }

    private static bool TryRotate(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Direction desired)
    {
        GenericActorActionLegality? rotate = contract.Rules.Actions
            .Where(action => action.Kind
                == GenericActorRulesContract.ActionKind.Rotation)
            .Select(action => body.Action(action.Id))
            .FirstOrDefault(action => action is { Available: true });
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            allowed = rotate?.Constraints.OfType<GenericActorActionLegality
                .ArgumentConstraint.DirectionConstraint>().SingleOrDefault();
        if (rotate is null || allowed is null || allowed.AllowedValues.IsEmpty)
            return false;
        Direction direction = allowed.AllowedValues
            .OrderBy(value => value == desired ? 0 : 1)
            .ThenBy(value => (int)value)
            .First();
        if (direction == body.Facing)
            return false;
        body.Command(
            rotate,
            new GenericActorActionArgument.DirectionArgument(direction));
        return true;
    }

    private static Direction CardinalForStep(
        ProjectileHeading heading,
        Position goal,
        Position from) => heading switch
    {
        ProjectileHeading.North => Direction.North,
        ProjectileHeading.East => Direction.East,
        ProjectileHeading.South => Direction.South,
        ProjectileHeading.West => Direction.West,
        _ => Math.Abs(goal.X - from.X) >= Math.Abs(goal.Y - from.Y)
            ? goal.X >= from.X ? Direction.East : Direction.West
            : goal.Y >= from.Y ? Direction.South : Direction.North,
    };

    private ProjectileHeading[] SearchOrder() => _mirror
        ?
        [
            ProjectileHeading.West,
            ProjectileHeading.NorthWest,
            ProjectileHeading.SouthWest,
            ProjectileHeading.North,
            ProjectileHeading.South,
            ProjectileHeading.NorthEast,
            ProjectileHeading.SouthEast,
            ProjectileHeading.East,
        ]
        :
        [
            ProjectileHeading.East,
            ProjectileHeading.NorthEast,
            ProjectileHeading.SouthEast,
            ProjectileHeading.North,
            ProjectileHeading.South,
            ProjectileHeading.NorthWest,
            ProjectileHeading.SouthWest,
            ProjectileHeading.West,
        ];

    private static ProjectileHeading? FirstStep(
        GenericActorMapContract map,
        Position start,
        Position target,
        IReadOnlySet<Position> occupied,
        ProjectileHeading[] order)
    {
        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Position, ProjectileHeading First)>();
        foreach (ProjectileHeading heading in Ordered(order, start, target))
        {
            Position next = Offset(start, heading);
            if (!CanEnter(map, start, next) || occupied.Contains(next)
                || !visited.Add(next))
            {
                continue;
            }
            if (next == target)
                return heading;
            queue.Enqueue((next, heading));
        }

        while (queue.Count > 0)
        {
            (Position current, ProjectileHeading first) = queue.Dequeue();
            foreach (ProjectileHeading heading in Ordered(order, current, target))
            {
                Position next = Offset(current, heading);
                if (!CanEnter(map, current, next) || !visited.Add(next))
                    continue;
                if (next == target)
                    return first;
                queue.Enqueue((next, first));
            }
        }
        return null;
    }

    private static IEnumerable<ProjectileHeading> Ordered(
        IEnumerable<ProjectileHeading> baseOrder,
        Position from,
        Position target) => baseOrder
            .OrderBy(heading => Offset(from, heading)
                .ChebyshevDistance(target));

    private static bool CanEnter(
        GenericActorMapContract map,
        Position from,
        Position to)
    {
        if (IsWall(map, to))
            return false;
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        return dx == 0 || dy == 0
            || (!IsWall(map, from.Offset(dx, 0))
                && !IsWall(map, from.Offset(0, dy)));
    }

    private static bool IsWall(GenericActorMapContract map, Position position) =>
        position.X < 0 || position.Y < 0
        || position.X >= map.Width || position.Y >= map.Height
        || map.TileRows[position.Y][position.X] == '#';

    private Position Mirror(Position position) => _mirror
        ? new Position(_contract!.Map.Width - 1 - position.X, position.Y)
        : position;

    private Position Unmirror(Position position) => Mirror(position);

    private StockSheet Sheet => _sheet
        ?? throw new InvalidOperationException("StartMatch was not called.");

    private static Position Offset(Position position, ProjectileHeading heading)
    {
        (int dx, int dy) = heading.Vector();
        return position.Offset(dx, dy);
    }

    private static Position Closest(Position from, IEnumerable<Position> values) =>
        values.OrderBy(value => from.ChebyshevDistance(value))
            .ThenBy(value => value.Y)
            .ThenBy(value => value.X)
            .First();

    private static IEnumerable<Position> Adjacent(Position position) =>
        Headings.Select(heading => Offset(position, heading));

    private static Direction ClosestDirection(
        Position from,
        Position to,
        IEnumerable<Direction> allowed)
    {
        Direction desired;
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        if (Math.Abs(dx) >= Math.Abs(dy))
            desired = dx >= 0 ? Direction.East : Direction.West;
        else
            desired = dy >= 0 ? Direction.South : Direction.North;
        return allowed.OrderBy(value => value == desired ? 0 : 1)
            .ThenBy(value => (int)value).First();
    }

    private static ProjectileHeading ClosestHeading(
        Position from,
        Position to,
        IEnumerable<ProjectileHeading> allowed)
    {
        int dx = Math.Sign(to.X - from.X);
        int dy = Math.Sign(to.Y - from.Y);
        ProjectileHeading desired = (dx, dy) switch
        {
            (0, < 0) => ProjectileHeading.North,
            (> 0, < 0) => ProjectileHeading.NorthEast,
            (> 0, 0) => ProjectileHeading.East,
            (> 0, > 0) => ProjectileHeading.SouthEast,
            (0, > 0) => ProjectileHeading.South,
            (< 0, > 0) => ProjectileHeading.SouthWest,
            (< 0, 0) => ProjectileHeading.West,
            (< 0, < 0) => ProjectileHeading.NorthWest,
            _ => ProjectileHeading.North,
        };
        return allowed.OrderBy(value => OctantDistance(value, desired))
            .ThenBy(value => (int)value).First();
    }

    private static int OctantDistance(
        ProjectileHeading left,
        ProjectileHeading right)
    {
        int distance = Math.Abs((int)left - (int)right);
        return Math.Min(distance, 8 - distance);
    }

    private static bool TryExactHeading(
        Position from,
        Position to,
        out ProjectileHeading heading)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        bool aligned = dx == 0 || dy == 0 || Math.Abs(dx) == Math.Abs(dy);
        heading = ClosestHeading(from, to, Headings);
        return aligned && (dx != 0 || dy != 0);
    }

    private Position WellFor(
        GenericActorContext.ModeObservationState.ArcRelay arc,
        string theater)
    {
        string id = theater switch
        {
            "north" => "north",
            "centre" => "centre",
            "south" => "south",
            _ => throw new InvalidOperationException(
                $"Unknown theater '{theater}'."),
        };
        return arc.Wells.Single(value =>
            string.Equals(value.WellId, id, StringComparison.Ordinal)).Position;
    }

    private static Position OwnReactor(
        GenericActorResolvedMatchContract contract,
        int participantId)
    {
        GenericActorResolvedMatchContract.ArcRelayModeMapBinding binding =
            contract.ModeMapBinding as GenericActorResolvedMatchContract
                .ArcRelayModeMapBinding
            ?? throw new InvalidOperationException("Missing Arc Relay map binding.");
        string regionId = contract.ParticipantRegionAssignments.Single(value =>
                value.ParticipantId == participantId
                && string.Equals(value.RegionRoleId,
                    binding.ReactorRegionRoleId, StringComparison.Ordinal))
            .MapRegionId;
        return contract.Map.Regions.Single(value =>
                string.Equals(value.RegionId, regionId, StringComparison.Ordinal))
            .Tiles.Single();
    }

    private static GenericActorContext.ModeObservationState.ArcRelay Arc(
        MindContext mind) => mind.Mode as GenericActorContext
            .ModeObservationState.ArcRelay
        ?? throw new InvalidOperationException("Expected Arc Relay state.");
}

internal sealed record UnitPlan(
    int UnitId,
    string Theater,
    string Role,
    int PartnerUnitId,
    Position[] OutboundPath,
    Position[] ReturnPath);

internal readonly record struct Zone(int MinX, int MinY, int MaxX, int MaxY)
{
    internal bool Contains(Position position) =>
        position.X >= MinX && position.X <= MaxX
        && position.Y >= MinY && position.Y <= MaxY;
}

internal sealed record CarrierPolicy(
    int HandoffHealthAtOrBelow,
    bool PreferAssignedTheater,
    int RouteFailureTicks);

internal sealed record EscortPolicy(int FollowDistance, bool FocusEnemyCarrier);

internal sealed record InterceptionPolicy(
    bool FocusEnemyCarrier,
    bool LooseCoreFallback);

internal sealed record GambitPlan(
    int Priority,
    string Id,
    string Trigger,
    int DurationTicks,
    int CooldownTicks,
    string[] ScopeRoles,
    string RoleOverride,
    string RallyLine);
