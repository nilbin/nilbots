using BotArena.Sdk;

/// <summary>
/// Direct executor for Arc Relay Orders v2. The authored tree decides whether
/// a body moves, fights, uses its signature, hands off, or drops. This class
/// owns deterministic target resolution and micro only; it has no doctrine,
/// custody, group, operation, engagement, mode, or rung plane.
/// </summary>
public sealed class ArcRelayTacticalPlaybookMind : IGenericMindBot
{
    private const int RouteWaypointSlack = 1;
    private readonly OrderTreeRuntime _orders = new();
    private readonly TacticalRouteGovernor _routes = new();
    private readonly TacticalTrafficGovernor _traffic = new();
    private readonly TacticalOscillationGovernor _oscillation = new();
    private readonly Dictionary<int, RouteCursor> _routeCursors = [];
    private readonly Dictionary<int, RouteCursor> _ingressCursors = [];
    private readonly TacticalIngressGovernor _ingress = new();
    private readonly Dictionary<int, string> _selectedPaths = [];
    private readonly Dictionary<int, int> _stationaryTicks = [];
    private readonly Dictionary<int, (ActorIdentity Actor, int Level)> _levels = [];
    private readonly Dictionary<int, int> _pendingInvest = [];
    private readonly Dictionary<int, (ActorIdentity Actor, int Spent)> _invested = [];
    private readonly Dictionary<int, PendingOneShot> _pendingOneShots = [];
    private readonly Dictionary<(ActorIdentity Actor, string Path), FightAnchor>
        _fightAnchors = [];
    private readonly Dictionary<string, KnownCore> _knownLooseCores =
        new(StringComparer.Ordinal);
    private readonly Dictionary<ActorIdentity, RememberedEnemy> _rememberedEnemies = [];
    private readonly HashSet<string> _processedEvents = new(StringComparer.Ordinal);
    private readonly Dictionary<ActorIdentity, int?> _firingApproachDistances = [];
    private readonly Dictionary<FiringEpisodeKey, int[]> _firingEpisodeFields = [];
    private readonly Dictionary<FiringGroundKey, Position[]> _firingGround = [];
    private readonly OrdersEvidenceLedger _evidence = new();

    private GenericActorResolvedMatchContract? _contract;
    private OrdersPackage? _package;
    private IArenaStepper _stepper = new SquadArenaPlanner();
    private SquadArenaPlanner? _squadPlanner;
    private Position _ownReactor;
    private Position _enemyReactor;
    private Position[] _healTiles = [];
    private bool _mirrored;
    private int _teamId;
    private int _lastOwnCharge;
    private int _lastObjectiveProgressTick;

    public void StartMatch(MindStart start)
    {
        _contract = start.Contract;
        _teamId = start.TeamId;
        _evidence.Reset();
        if (start.Contract.ModeMapBinding is not
            GenericActorResolvedMatchContract.ArcRelayModeMapBinding binding)
        {
            throw new InvalidOperationException("OrdersMind requires Arc Relay.");
        }
        GenericActorResolvedMatchContract.ParticipantRegionAssignment own =
            start.Contract.ParticipantRegionAssignments.Single(value =>
                value.ParticipantId == start.ParticipantId
                && string.Equals(value.RegionRoleId,
                    binding.ReactorRegionRoleId, StringComparison.Ordinal));
        _ownReactor = start.Contract.Map.Regions.Single(value =>
                string.Equals(value.RegionId, own.MapRegionId,
                    StringComparison.Ordinal))
            .Tiles.Single();
        _enemyReactor = start.Contract.Map.Regions
            .Where(value => value.Kind
                == GenericActorMapContract.RegionKind.Objective)
            .Where(value => value.Tiles.Length == 1
                && value.Tiles[0] != _ownReactor)
            .Where(value => value.RegionId.Contains(
                "reactor", StringComparison.OrdinalIgnoreCase))
            .OrderBy(value => value.RegionId, StringComparer.Ordinal)
            .Select(value => value.Tiles[0])
            .First();
        _healTiles = start.Contract.Map.Regions
            .Where(region => region.RegionId.StartsWith(
                "heal-", StringComparison.Ordinal))
            .SelectMany(region => region.Tiles)
            .OrderBy(value => value.Y)
            .ThenBy(value => value.X)
            .ToArray();
        _mirrored = _ownReactor.X > (start.Contract.Map.Width - 1) / 2;
        _package = OrdersPackage.Load(
            start.EvaluationData, start.Contract, _ownReactor);
        _squadPlanner = _package.StepperMode == "coordinated"
            ? new SquadArenaPlanner()
            : null;
        _stepper = _squadPlanner is not null
            ? _squadPlanner
            : new GreedyArenaStepper();
        ValidateComposition(start.Contract, start.TeamId, _package);
    }

    public void Think(MindContext mind)
    {
        GenericActorResolvedMatchContract contract = _contract
            ?? throw new InvalidOperationException("StartMatch was not called.");
        OrdersPackage package = _package
            ?? throw new InvalidOperationException("Sheet was not loaded.");
        if (mind.Mode is not GenericActorContext.ModeObservationState.ArcRelay arc)
        {
            foreach (MindBody body in mind.Bodies)
                body.Hold("orders:unsupported-mode");
            return;
        }

        // Conditions, target classification and joint-intent construction can
        // ask the same exact path question several times in one Think. The
        // observation is frozen for the tick, so calculate each answer once.
        _firingApproachDistances.Clear();
        _firingEpisodeFields.Clear();
        _firingGround.Clear();

        UpdateMemory(contract, mind, arc);
        _evidence.Observe(_teamId, mind);
        ResolvePreviousOneShots(mind);
        UpdateMotionMemory(mind);
        IReadOnlySet<ActorIdentity> live = mind.Bodies
            .Select(body => body.ActorId).ToHashSet();
        _orders.Prune(live);

        Dictionary<ActorIdentity, GenericActorContext.ArcRelayCoreState> carried =
            arc.VisibleCores
                .Where(core => core.Disposition
                    == GenericActorContext.ArcRelayCoreDisposition.Carried
                    && core.CarrierActorId is not null)
                .ToDictionary(core => core.CarrierActorId!, core => core);

        var proposalsByBody = new Dictionary<int, IReadOnlyList<Proposal>>();
        foreach (MindBody body in mind.Bodies.OrderBy(value => value.UnitId))
        {
            OrdersPackage.Role role = package.RolesByUnit[body.UnitId];
            body.SetRole(role.Id.Length <= 24 ? role.Id : role.Id[..24]);
            IReadOnlyList<OrderTreeRuntime.EligibleLeaf> leaves = _orders.Eligible(
                body.ActorId,
                role,
                mind.Tick,
                (expression, orderTicks) => Evaluate(
                    contract, mind, arc, package, body, role,
                    expression, orderTicks, carried));
            List<Proposal> proposals = BuildProposals(
                contract, mind, arc, package, body, role, leaves, carried);
            proposalsByBody.Add(body.UnitId, proposals);
        }
        Dictionary<int, Proposal> selected = Allocate(proposalsByBody);
        ReconcileSelections(mind, selected);
        Dictionary<int, MovePlan> movePlans = PrepareMovePlans(
            contract, mind, package, selected);
        // A follow rung promises that its leader owns the front of the file.
        // Remove the leader's committed next tile from the follower's
        // standoff choices before joint planning; EPIBT can then move the
        // follower aside instead of treating its current wait as immutable.
        KeepFollowersBehindLeaders(contract, movePlans, selected);
        _traffic.Observe(
            mind.Tick,
            BuildTrafficSamples(contract, mind, selected, movePlans));
        _oscillation.Observe(
            mind.Tick,
            mind.Bodies.Select(body => new TacticalOscillationGovernor.Sample(
                body.ActorId,
                body.Position,
                body.PreviousPosition,
                body.MovedLastTick)).ToArray());
        ArenaBasics.Claims claims = ArenaBasics.Claims.ForTick(
            contract, mind, _stepper, _oscillation);
        foreach (GenericActorContext.ArcRelayPendingStrike strike in arc.PendingStrikes)
        {
            if (strike.Shooter.TeamId == _teamId)
                claims.Root(strike.Shooter.UnitId);
        }
        RankBodies(mind, claims);
        IReadOnlyDictionary<ActorIdentity, string> arcTossCatchers =
            ResolveArcTossCatchReservations(contract, mind, arc, selected);
        if (_squadPlanner is not null)
        {
            _squadPlanner.PlanTeam(
                BuildSquadMovementIntents(
                    contract, mind, package, selected, movePlans, claims,
                    carried, arcTossCatchers),
                claims);
        }
        else
            PublishTrafficRequests(contract, mind, movePlans, claims);

        foreach (MindBody body in mind.Bodies
                     .OrderBy(value => claims.RankOf(value.UnitId))
                     .ThenBy(value => selected.GetValueOrDefault(value.UnitId)?.Priority
                         ?? int.MaxValue)
                     .ThenBy(value => value.UnitId))
        {
            OrdersPackage.Role role = package.RolesByUnit[body.UnitId];
            if (arcTossCatchers.TryGetValue(
                    body.ActorId, out string? catchReason))
            {
                body.Hold($"orders:arc-toss-catch:{catchReason}");
                continue;
            }
            if (TryInvest(contract, mind, body, role, carried))
                continue;
            string prefix = selected.TryGetValue(body.UnitId, out Proposal? proposal)
                ? $"orders:{proposal.Leaf.Node.Path}"
                : $"orders:{role.Id}";
            string movementPrefix = $"{prefix}"
                + $":urgency-{claims.PriorityTrace(body.UnitId)}";
            if (_squadPlanner is null && ArenaBasics.TryMakeWay(
                    contract, mind, body, claims,
                    $"{movementPrefix}:make-way"))
                continue;
            if (ArenaBasics.TryEvade(
                    contract, mind, body, claims,
                    _stepper.Trace(body.UnitId)))
                continue;
            if (proposal is null)
            {
                if (TryRestingYield(contract, mind, body, claims, prefix))
                    continue;
                body.Hold($"{prefix}:no-eligible-order");
                continue;
            }
            Execute(
                contract, mind, arc, package, body, proposal,
                movePlans.GetValueOrDefault(body.UnitId), claims, carried);
        }
    }

    /// <summary>
    /// Project the complete selected Orders tree into physical intents before
    /// command one. This is the only seam between sheet policy and squad
    /// geometry: the planner may realize these intents, never replace them.
    /// </summary>
    private SquadMovementIntent[] BuildSquadMovementIntents(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        OrdersPackage package,
        IReadOnlyDictionary<int, Proposal> selected,
        IReadOnlyDictionary<int, MovePlan> movePlans,
        ArenaBasics.Claims claims,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried,
        IReadOnlyDictionary<ActorIdentity, string> arcTossCatchers)
    {
        var result = new List<SquadMovementIntent>(mind.Bodies.Length);
        foreach (MindBody body in mind.Bodies.OrderBy(value => value.UnitId))
        {
            TacticalMovementPriority priority = _traffic.Priority(body.UnitId);
            Proposal? proposal = selected.GetValueOrDefault(body.UnitId);
            string key = proposal?.Leaf.Node.Path ?? "no-order";
            SquadMovementIntent Hold(
                string trace,
                bool pinned = false) => new(
                body,
                key,
                $"hold:u{body.UnitId}",
                [body.Position],
                null,
                priority,
                Pinned: pinned,
                PrefersHold: true,
                CanStage: false,
                LeaderUnitId: null,
                RouteClass: SquadRouteClass.Ordinary,
                trace);

            if (arcTossCatchers.TryGetValue(
                    body.ActorId, out string? catchReason))
            {
                result.Add(Hold(
                    $"arc-toss-catch:{catchReason}", pinned: true));
                continue;
            }
            if (claims.IsRooted(body.UnitId))
            {
                result.Add(Hold("pending-strike-root", pinned: true));
                continue;
            }
            OrdersPackage.Role role = package.RolesByUnit[body.UnitId];
            if (WillInvest(contract, mind, body, role, carried))
            {
                result.Add(Hold("upgrade-commit", pinned: true));
                continue;
            }
            Position[] evade = ArenaBasics.SafeEvasionGoals(
                contract, mind, body);
            if (evade.Length > 0)
            {
                result.Add(new SquadMovementIntent(
                    body,
                    $"evade:{body.ActorId}",
                    $"evade:{body.ActorId}",
                    evade,
                    null,
                    priority,
                    Pinned: false,
                    PrefersHold: false,
                    CanStage: false,
                    LeaderUnitId: null,
                    RouteClass: SquadRouteClass.Ordinary,
                    "projectile-evasion"));
                continue;
            }
            if (proposal is null)
            {
                result.Add(Hold("no-eligible-order"));
                continue;
            }

            switch (proposal.Leaf.Node.Action)
            {
                case OrdersPackage.MoveAction move:
                {
                    MovePlan? plan = movePlans.GetValueOrDefault(body.UnitId);
                    if (plan is null)
                    {
                        result.Add(Hold("move-at-target"));
                        break;
                    }
                    int? leader = move.Kind == "follow"
                        && proposal.Target.Actor is ActorIdentity actor
                            ? actor.UnitId
                            : null;
                    result.Add(new SquadMovementIntent(
                        body,
                        key,
                        $"move:{move.Kind}:{proposal.Target.Key}",
                        plan.Goals,
                        plan.Course?.Next,
                        priority,
                        Pinned: false,
                        PrefersHold: plan.Arrived
                            || plan.Goals.Contains(body.Position),
                        CanStage: IsSingleServiceMoveTarget(
                            move, proposal.Target.Key),
                        LeaderUnitId: leader,
                        RouteClass: carried.ContainsKey(body.ActorId)
                            && move.Kind == "to"
                            && string.Equals(
                                proposal.Target.Key,
                                "reactor:own",
                                StringComparison.Ordinal)
                                ? SquadRouteClass.LoadedReturn
                                : SquadRouteClass.Ordinary,
                        $"move:{proposal.Target.Key}"));
                    break;
                }
                case OrdersPackage.FightAction fight:
                {
                    GenericActorContext.ObservedEnemyState? target = mind.Enemies
                        .FirstOrDefault(value => value.ActorId
                            == proposal.Target.Actor);
                    bool deployment = proposal.Target.Key.StartsWith(
                        "deployment:", StringComparison.Ordinal);
                    if (target is null && !deployment)
                    {
                        result.Add(Hold("fight-target-lost"));
                        break;
                    }
                    Position targetPosition = target?.Position
                        ?? proposal.Target.Position;
                    string targetKey = target is null
                        ? proposal.Target.Key
                        : target.ActorId.ToString();
                    Position anchor = FightEpisodeAnchor(
                        body, proposal.Leaf);
                    if (target is not null
                        && fight.Style == "kite"
                        && targetPosition.ChebyshevDistance(body.Position) <= 2)
                    {
                        Position[] away = ArenaBasics.AwayGoals(
                            contract, body, [targetPosition]);
                        if (away.Length > 0)
                        {
                            result.Add(new SquadMovementIntent(
                                body,
                                key,
                                $"fight-kite:{targetKey}",
                                away,
                                null,
                                priority,
                                Pinned: false,
                                PrefersHold: false,
                                CanStage: false,
                                LeaderUnitId: null,
                                RouteClass: SquadRouteClass.Ordinary,
                                $"kite:{targetKey}"));
                            break;
                        }
                    }
                    if (ArenaBasics.CommitsAttackPositionThisTick(
                            contract, mind, body, targetPosition))
                    {
                        result.Add(Hold(
                            $"fight-commit:{targetKey}", pinned: true));
                        break;
                    }
                    if (ArenaBasics.CanAimAtPosition(
                            contract, body, targetPosition)
                        || fight.Style == "hold")
                    {
                        result.Add(Hold($"fight-position:{targetKey}"));
                        break;
                    }
                    bool inLeash = fight.ChaseLimit == 0
                        ? body.Position == anchor
                        : ArenaBasics.StaticDistance(
                            contract.Map, body.Position, anchor)
                            <= fight.ChaseLimit;
                    if (!inLeash || fight.Style != "advance")
                    {
                        result.Add(Hold($"fight-leash:{targetKey}"));
                        break;
                    }
                    Position[] firing = FiringContributionGoals(
                        contract, mind, body, targetPosition, anchor,
                        fight.ChaseLimit);
                    if (firing.Length == 0)
                    {
                        result.Add(Hold($"fight-no-firing-ground:{targetKey}"));
                        break;
                    }
                    result.Add(new SquadMovementIntent(
                        body,
                        key,
                        $"fight:{targetKey}",
                        firing,
                        null,
                        priority,
                        Pinned: false,
                        PrefersHold: false,
                        CanStage: true,
                        LeaderUnitId: null,
                        RouteClass: SquadRouteClass.Ordinary,
                        $"fight-close:{targetKey}"));
                    break;
                }
                default:
                    result.Add(Hold("non-movement-action", pinned: true));
                    break;
            }
        }
        return result.ToArray();
    }

    /// <summary>
    /// Wells and healers are one-tile services, not eight identical
    /// destinations. When several bodies select one, the joint assignment
    /// gives one body the service tile and stages the rest on distinct nearby
    /// contribution tiles. This prevents patients from repeatedly evicting
    /// one another before a healing tick. A reactor is intentionally excluded:
    /// a carrier cannot bank from a surrounding contribution tile, so every
    /// loaded body must retain the exact reactor as its goal and let EPIBT
    /// serialize the queue through normal right-of-way.
    /// </summary>
    private static bool IsSingleServiceMoveTarget(
        OrdersPackage.MoveAction move,
        string targetKey) => move.Kind == "to"
            && (targetKey.StartsWith("well:", StringComparison.Ordinal)
                || targetKey.StartsWith("heal:", StringComparison.Ordinal));

    public void EndMatch(MindEnd end) => _ = end;

    private List<Proposal> BuildProposals(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        OrdersPackage package,
        MindBody body,
        OrdersPackage.Role role,
        IReadOnlyList<OrderTreeRuntime.EligibleLeaf> leaves,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        var result = new List<Proposal>();
        string? skippedFight = null;
        for (int priority = 0; priority < leaves.Count; priority++)
        {
            OrderTreeRuntime.EligibleLeaf leaf = leaves[priority];
            ResolvedTarget[] targets = ResolveTargets(
                contract, mind, arc, package, body, role, leaf, carried,
                out string? decline);
            if (decline is not null)
                skippedFight = decline;
            foreach (ResolvedTarget target in targets)
            {
                string? scope = leaf.Node.Capacity?.Id;
                int limit = leaf.Node.Capacity is null
                    ? 8
                    : leaf.Node.Capacity.InlineValue
                        ?? package.Capacities[leaf.Node.Capacity.Id];
                int distance = ArenaBasics.StaticDistance(
                        contract.Map, body.Position, target.Position)
                    ?? int.MaxValue;
                result.Add(new Proposal(
                    body, role, leaf, priority, target, scope, limit, distance,
                    skippedFight));
            }
        }
        return result;
    }

    private ResolvedTarget[] ResolveTargets(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        OrdersPackage package,
        MindBody body,
        OrdersPackage.Role role,
        OrderTreeRuntime.EligibleLeaf leaf,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried,
        out string? decline)
    {
        decline = null;
        OrdersPackage.OrderNode node = leaf.Node;
        switch (node.Action)
        {
            case OrdersPackage.MoveAction move:
            {
                ResolvedTarget[] targets = ResolveMoveTargets(
                    contract, mind, arc, package, body, move, carried);
                if (targets.Length == 0
                    && move.Kind == "follow"
                    && move.TargetKind == "selector")
                {
                    decline = $"after-{node.Id}-no-experienced-ally";
                }
                return targets;
            }
            case OrdersPackage.FightAction fight:
                return ResolveEnemyTargets(
                    contract, mind, arc, body, leaf, fight, out decline);
            case OrdersPackage.UseAction use:
            {
                ResolvedTarget[] targets = ResolveUseTargets(
                    contract, mind, package, body, use, carried);
                if (targets.Length == 0
                    && (use.TargetKind is "role" or "selector"
                        || use.Target == "veteran"))
                {
                    decline = use.TargetKind == "role"
                        ? $"after-{node.Id}-no-live-role-{use.Target}"
                        : use.Target == "veteran"
                            ? $"after-{node.Id}-no-visible-veteran"
                            : $"after-{node.Id}-no-experienced-ally";
                }
                return targets;
            }
            case OrdersPackage.HandoffAction handoff:
                return ResolveHandoffTargets(
                    contract, mind, body, handoff, package, carried);
            case OrdersPackage.DropAction:
                if (!carried.TryGetValue(body.ActorId, out var core)
                    || body.Action("drop-core") is not { Available: true })
                    return [];
                return [new ResolvedTarget(
                    $"core:{CoreKey(core.CoreId)}", body.Position,
                    [body.Position], null, CoreKey(core.CoreId))];
            default:
                return [];
        }
    }

    private ResolvedTarget[] ResolveMoveTargets(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        OrdersPackage package,
        MindBody body,
        OrdersPackage.MoveAction move,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        if (move.Kind == "patrol")
        {
            Position[] points = package.RoutePoints(move.Target);
            Position nearest = points
                .OrderBy(point => ArenaBasics.StaticDistance(
                        contract.Map, body.Position, point) ?? int.MaxValue)
                .ThenBy(point => point.Y)
                .ThenBy(point => point.X)
                .First();
            return [new ResolvedTarget(
                $"route:{move.Target}", nearest, points, null, null)];
        }
        if (move.Kind == "follow")
        {
            IEnumerable<MindBody> livingAllies = mind.Bodies
                .Where(candidate => candidate.UnitId != body.UnitId);
            IOrderedEnumerable<MindBody> candidates;
            if (move.TargetKind == "role")
            {
                candidates = livingAllies
                    .Where(candidate => package.RolesByUnit[candidate.UnitId].Id
                        == move.Target)
                    .OrderBy(candidate => ArenaBasics.StaticDistance(
                        contract.Map, body.Position, candidate.Position)
                        ?? int.MaxValue)
                    .ThenBy(candidate => candidate.UnitId);
            }
            else
            {
                if (ArenaBasics.ArcRules(contract)?.VeteranCommand != true)
                    return [];
                candidates = livingAllies
                    .Where(candidate => CurrentLevel(candidate) > 1)
                    .OrderByDescending(CurrentLevel)
                    .ThenBy(candidate => ArenaBasics.StaticDistance(
                        contract.Map, body.Position, candidate.Position)
                        ?? int.MaxValue)
                    .ThenBy(candidate => candidate.UnitId);
            }
            return candidates
                .Select(candidate => new ResolvedTarget(
                    $"body:{candidate.ActorId}",
                    candidate.Position,
                    TacticalFollowPlanner.StandoffGoals(
                        candidate.Position,
                        tile => ArenaBasics.IsLegalTerrainStep(
                            contract.Map, candidate.Position, tile)),
                    candidate.ActorId,
                    null,
                    move.TargetKind == "selector"
                        ? $"most-experienced-level-{CurrentLevel(candidate)}"
                        : $"role-target-{move.Target}"))
                .Where(target => target.Goals.Length > 0)
                .ToArray();
        }
        if (move.TargetKind == "dynamic")
        {
            if (move.Target == "own-reactor")
                return [new ResolvedTarget(
                    "reactor:own", _ownReactor, [_ownReactor], null, null)];
            if (move.Target == "nearest-heal-zone")
                return _healTiles
                    .OrderBy(tile => ArenaBasics.StaticDistance(
                            contract.Map, body.Position, tile) ?? int.MaxValue)
                    .ThenBy(tile => tile.Y)
                    .ThenBy(tile => tile.X)
                    .Select(tile => new ResolvedTarget(
                        $"heal:{tile.X}:{tile.Y}", tile, [tile], null, null))
                    .ToArray();
            if (move.Target is "nearest-known-loose-core"
                    or "highest-bank-value-known-loose-core")
            {
                if (carried.ContainsKey(body.ActorId))
                    return [];
                HashSet<Position> reserved = mind.VisibleTiles
                    .Where(tile => tile.SpawnReservation is not null)
                    .Select(tile => tile.Position)
                    .ToHashSet();
                IEnumerable<KnownCore> candidates = _knownLooseCores.Values
                    // A body must occupy the Core's exact tile to collect it.
                    // A lifecycle reservation is authoritative terrain for
                    // this purpose; standing beside it only strands every
                    // authorized collector on a task it cannot complete.
                    .Where(core => !reserved.Contains(core.Position));
                IOrderedEnumerable<KnownCore> ordered = move.Target
                    == "highest-bank-value-known-loose-core"
                    ? candidates
                        .OrderByDescending(core => CoreBankValue(
                            contract, arc, core.CoreId.SourceWellId))
                        .ThenBy(core => ArenaBasics.StaticDistance(
                                contract.Map, body.Position, core.Position)
                            ?? int.MaxValue)
                    : candidates.OrderBy(core => ArenaBasics.StaticDistance(
                            contract.Map, body.Position, core.Position)
                        ?? int.MaxValue);
                return ordered
                    .ThenBy(core => core.Key, StringComparer.Ordinal)
                    .Select(core => new ResolvedTarget(
                        $"core:{core.Key}", core.Position, [core.Position],
                        null, core.Key))
                    .ToArray();
            }
        }
        if (move.TargetKind == "zone")
        {
            Position[] goals = PostureGoals(
                package.ZoneTiles(move.Target, contract.Map), move.Posture);
            Position nearest = Nearest(contract.Map, body.Position, goals);
            return [new ResolvedTarget(
                $"zone:{move.Target}", nearest, goals, null, null)];
        }
        if (move.TargetKind == "anchor")
        {
            Position target = package.AnchorPosition(move.Target);
            return [new ResolvedTarget(
                $"anchor:{move.Target}", target, [target], null, null)];
        }
        if (move.TargetKind == "adjacentTo")
        {
            Position target = package.AnchorPosition(move.Target);
            Position[] goals = AdjacentStandTiles(contract.Map, target);
            if (goals.Length == 0)
                return [];
            Position nearest = Nearest(contract.Map, body.Position, goals);
            return [new ResolvedTarget(
                $"adjacent-to:{move.Target}", nearest, goals, null, null)];
        }
        if (move.TargetKind == "well")
        {
            GenericActorContext.ArcRelayWellState well = ResolveWell(
                contract, arc, move.Target);
            return [new ResolvedTarget(
                $"well:{well.WellId}", well.Position, [well.Position], null, null)];
        }
        return [];
    }

    /// <summary>
    /// Arc Toss names an allied body but transmits a position target through
    /// the engine. The recipient therefore owns its pre-tick tile until the
    /// Core lands: moving it as an ordinary concurrent intent would turn a
    /// valid pass into a loose Core. The tell reservation comes from this
    /// tick's selected order; later flight ticks come from public Core state.
    /// </summary>
    private static IReadOnlyDictionary<ActorIdentity, string>
        ResolveArcTossCatchReservations(
            GenericActorResolvedMatchContract contract,
            MindContext mind,
            GenericActorContext.ModeObservationState.ArcRelay arc,
            IReadOnlyDictionary<int, Proposal> selected)
    {
        var result = new Dictionary<ActorIdentity, string>();
        GenericActorRulesContract.ArcRelayGameMode? rules =
            ArenaBasics.ArcRules(contract);
        foreach (Proposal proposal in selected.Values
                     .OrderBy(value => value.Body.UnitId))
        {
            if (proposal.Leaf.Node.Action is not OrdersPackage.UseAction use
                || proposal.Target.Actor is not ActorIdentity receiver)
            {
                continue;
            }
            GenericActorRulesContract.ArcRelaySignature? signature =
                rules?.Signatures.FirstOrDefault(value =>
                    value.SignatureId == use.Skill || value.Kind == use.Skill);
            if (signature?.Kind == "arc-toss")
            {
                result[receiver] = $"tell-from-{proposal.Body.ActorId}";
            }
        }

        foreach (GenericActorContext.ArcRelayCoreState core in arc.VisibleCores
                     .Where(value => value.Disposition
                            == GenericActorContext.ArcRelayCoreDisposition.InFlight
                        && value.FlightTarget is not null)
                     .OrderBy(value => CoreKey(value.CoreId),
                         StringComparer.Ordinal))
        {
            MindBody? catcher = mind.Bodies
                .Where(body => body.Position == core.FlightTarget!.Value)
                .OrderBy(body => body.UnitId)
                .FirstOrDefault();
            if (catcher is not null)
            {
                result.TryAdd(
                    catcher.ActorId,
                    $"flight-{CoreKey(core.CoreId)}-until-"
                        + $"{core.FlightCompletesAtTick}");
            }
        }
        return result;
    }

    private ResolvedTarget[] ResolveEnemyTargets(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        OrderTreeRuntime.EligibleLeaf leaf,
        OrdersPackage.FightAction fight,
        out string? decline)
    {
        decline = null;
        HashSet<ActorIdentity> carriers = arc.VisibleCores
            .Where(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Carried
                && core.CarrierActorId?.TeamId != _teamId)
            .Select(core => core.CarrierActorId!)
            .ToHashSet();
        HashSet<ActorIdentity> marked = arc.VisibleSignatures
            .Where(signature => signature.SignatureId == "target-paint"
                && signature.TargetActorId is not null)
            .Select(signature => signature.TargetActorId!)
            .ToHashSet();
        var ordered = new List<ResolvedTarget>();
        bool veteranUnavailable = false;
        foreach (string selector in fight.Targets)
        {
            if (selector == "deployment")
            {
                if (ArenaBasics.ArcRules(contract)?.DurableDeployments == true)
                {
                    foreach (GenericActorContext.ArcRelaySignatureState deployment
                             in arc.VisibleSignatures
                                 .Where(value => value.OwnerTeamId != _teamId
                                     && value.Phase == GenericActorContext
                                         .ArcRelaySignaturePhase.Active
                                     && IsDeploymentKind(value.Kind)
                                     && ArenaBasics.CanAimAtPosition(
                                         contract, body, value.Positions[0]))
                                 .OrderBy(value => ArenaBasics.StaticDistance(
                                         contract.Map,
                                         body.Position,
                                         value.Positions[0])
                                     ?? int.MaxValue)
                                 .ThenBy(value => value.OperationId,
                                     StringComparer.Ordinal))
                    {
                        var target = new ResolvedTarget(
                            $"deployment:{deployment.OperationId}",
                            deployment.Positions[0],
                            [deployment.Positions[0]],
                            null,
                            null,
                            $"deployment-{deployment.RemainingCapacity}-of-"
                                + deployment.MaximumCapacity);
                        if (ordered.All(existing => existing.Key != target.Key))
                            ordered.Add(target);
                    }
                }
                continue;
            }
            IEnumerable<GenericActorContext.ObservedEnemyState> candidates =
                selector switch
                {
                    "carrier" => mind.Enemies.Where(enemy =>
                        carriers.Contains(enemy.ActorId)),
                    "weakest" => mind.Enemies.OrderBy(enemy => enemy.Health)
                        .ThenBy(enemy => enemy.ActorId),
                    "nearest" => mind.Enemies.OrderBy(enemy =>
                            ArenaBasics.StaticDistance(
                                contract.Map, body.Position, enemy.Position)
                            ?? int.MaxValue)
                        .ThenBy(enemy => enemy.ActorId),
                    "medic" => mind.Enemies.Where(enemy =>
                            enemy.ClassId == "patchbay")
                        .OrderBy(enemy => enemy.ActorId),
                    "marked" => mind.Enemies.Where(enemy =>
                            marked.Contains(enemy.ActorId))
                        .OrderBy(enemy => enemy.ActorId),
                    "veteran" when ArenaBasics.ArcRules(contract)
                            ?.VeteranCommand == true => mind.Enemies
                        .Where(enemy => enemy.VeterancyLevel > 1)
                        .OrderByDescending(enemy => enemy.VeterancyLevel)
                        .ThenBy(enemy => ArenaBasics.StaticDistance(
                                contract.Map, body.Position, enemy.Position)
                            ?? int.MaxValue)
                        .ThenBy(enemy => enemy.ActorId),
                    _ => [],
                };
            GenericActorContext.ObservedEnemyState[] selectedCandidates =
                candidates.ToArray();
            if (selector == "veteran" && selectedCandidates.Length == 0)
                veteranUnavailable = true;
            foreach (var candidate in selectedCandidates)
            {
                var target = new ResolvedTarget(
                    $"enemy:{candidate.ActorId}",
                    candidate.Position,
                    [candidate.Position],
                    candidate.ActorId,
                    null,
                    selector == "veteran"
                        ? $"veteran-level-{candidate.VeterancyLevel}"
                        : null);
                if (ordered.All(existing => existing.Key != target.Key))
                    ordered.Add(target);
            }
        }
        if (ordered.Count == 0)
        {
            if (veteranUnavailable)
                decline = $"after-{leaf.Node.Id}-no-visible-veteran";
            return [];
        }

        // A selector is a preference, not a command to stare at that target
        // forever. Keep the authored order, but place targets this body can
        // shoot or reach inside this fight episode ahead of targets outside
        // its leash. If none is actionable, this leaf yields for the tick so
        // the next authored order can run. Its lifecycle-owned anchor remains
        // intact, so yielding cannot reset the leash and create an unbounded
        // one-tile-at-a-time chase.
        var classified = ordered.Select((target, index) => new
        {
            Target = target,
            Index = index,
            Actionable = CanActOnFightTarget(
                contract,
                mind,
                body,
                leaf,
                fight,
                target.Position,
                target.Actor is not null),
        }).ToArray();
        if (classified.All(value => !value.Actionable))
        {
            decline = $"after-{leaf.Node.Id}-no-actionable-target";
            return [];
        }
        if (classified.All(value => value.Actionable))
        {
            return veteranUnavailable
                ? ordered.Select(target => target with
                    {
                        Trace = target.Trace is null
                            ? "veteran-unresolved-fallback"
                            : $"veteran-unresolved-fallback:{target.Trace}",
                    }).ToArray()
                : ordered.ToArray();
        }

        ResolvedTarget[] actionable = classified
            .Where(value => value.Actionable)
            .Select(value =>
            {
                string? skipped = classified
                    .Take(value.Index)
                    .FirstOrDefault(candidate => !candidate.Actionable)
                    ?.Target.Key;
                string? targetTrace = skipped is string key
                    ? $"target-fallback-from-{key}"
                    : null;
                if (veteranUnavailable)
                {
                    targetTrace = targetTrace is null
                        ? "veteran-unresolved-fallback"
                        : $"veteran-unresolved-fallback:{targetTrace}";
                }
                else if (value.Target.Trace is string ownTrace)
                {
                    targetTrace = targetTrace is null
                        ? ownTrace
                        : $"{targetTrace}:{ownTrace}";
                }
                return value.Target with { Trace = targetTrace };
            })
            .ToArray();
        return actionable;
    }

    private bool CanActOnFightTarget(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        OrderTreeRuntime.EligibleLeaf leaf,
        OrdersPackage.FightAction fight,
        Position target,
        bool bodyTarget)
    {
        if (ArenaBasics.CanAimAtPosition(contract, body, target))
            return true;
        if (bodyTarget
            && fight.Style == "kite"
            && body.Position.ChebyshevDistance(target) <= 2)
        {
            return true;
        }
        if (fight.Style == "hold")
            return false;

        Position anchor = FightEpisodeAnchor(body, leaf);
        int? bodyFromAnchor = ArenaBasics.StaticDistance(
            contract.Map, body.Position, anchor);
        bool insideLeash = fight.ChaseLimit == 0
            ? body.Position == anchor
            : bodyFromAnchor is int used && used <= fight.ChaseLimit;
        if (!insideLeash)
            return false;
        return FiringContributionGoals(
            contract, mind, body, target, anchor,
            fight.ChaseLimit).Length > 0;
    }

    private static bool IsDeploymentKind(string kind) => kind is
        "prism-wall" or "trip-node" or "hardlight-block" or "sentinel-seed";

    private ResolvedTarget[] ResolveUseTargets(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        OrdersPackage package,
        MindBody body,
        OrdersPackage.UseAction use,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        GenericActorRulesContract.ArcRelaySignature? signature =
            ArenaBasics.ArcRules(contract)?.Signatures.FirstOrDefault(value =>
                value.SignatureId == use.Skill || value.Kind == use.Skill);
        if (signature is null
            || body.Action(signature.ActionId) is not { Available: true })
            return [];
        // A mixed-class role may state one skill order for each class it casts.
        // Applicability belongs to the body: an unrelated class falls through
        // to its next authored order instead of forcing an artificial role split.
        if (signature.ClassId != body.ClassId)
            return [];
        if (use.TargetKind == "named" && use.Target == "self")
        {
            var self = new ResolvedTarget(
                $"body:{body.ActorId}", body.Position, [body.Position],
                body.ActorId, null);
            return CanExecuteUse(contract, body, self, use) ? [self] : [];
        }
        if (use.TargetKind == "named" && use.Target == "own-reactor")
        {
            var reactor = new ResolvedTarget(
                "reactor:own", _ownReactor, [_ownReactor], null, null);
            return CanExecuteUse(contract, body, reactor, use) ? [reactor] : [];
        }
        Position? exactPlacement = use.AtAnchor is string anchor
            ? package.AnchorPosition(anchor)
            : null;
        IEnumerable<(ActorIdentity Actor, Position Position, int Health,
            int Level, string? Trace)> values =
            use.TargetKind switch
            {
                "role" => mind.Bodies
                    .Where(candidate => candidate.UnitId != body.UnitId
                        && package.RolesByUnit[candidate.UnitId].Id
                            == use.Target)
                    .OrderBy(candidate => ArenaBasics.StaticDistance(
                            contract.Map, body.Position, candidate.Position)
                        ?? int.MaxValue)
                    .ThenBy(candidate => candidate.UnitId)
                    .Select(candidate => (candidate.ActorId,
                        candidate.Position, candidate.Health,
                        CurrentLevel(candidate),
                        (string?)$"role-target-{use.Target}")),
                "selector" when ArenaBasics.ArcRules(contract)
                        ?.VeteranCommand == true => mind.Bodies
                    .Where(candidate => candidate.UnitId != body.UnitId
                        && CurrentLevel(candidate) > 1)
                    .OrderByDescending(CurrentLevel)
                    .ThenBy(candidate => ArenaBasics.StaticDistance(
                            contract.Map, body.Position, candidate.Position)
                        ?? int.MaxValue)
                    .ThenBy(candidate => candidate.UnitId)
                    .Select(candidate => (candidate.ActorId,
                        candidate.Position, candidate.Health,
                        CurrentLevel(candidate),
                        OptionalTrace($"most-experienced-level-"
                            + CurrentLevel(candidate)))),
                "named" => use.Target switch
                {
                "nearest-ally" => mind.Bodies
                    .Where(candidate => candidate.UnitId != body.UnitId)
                    .OrderBy(candidate => ArenaBasics.StaticDistance(
                            contract.Map, body.Position, candidate.Position)
                        ?? int.MaxValue)
                    .ThenBy(candidate => candidate.UnitId)
                    .Select(candidate => (candidate.ActorId,
                        candidate.Position, candidate.Health,
                        CurrentLevel(candidate), (string?)null)),
                "weakest-ally" => mind.Bodies
                    .Where(candidate => candidate.UnitId != body.UnitId)
                    .OrderBy(candidate => candidate.Health)
                    .ThenBy(candidate => candidate.UnitId)
                    .Select(candidate => (candidate.ActorId,
                        candidate.Position, candidate.Health,
                        CurrentLevel(candidate), (string?)null)),
                "healthiest-ally" => mind.Bodies
                    .Where(candidate => candidate.UnitId != body.UnitId)
                    .OrderByDescending(candidate => candidate.Health)
                    .ThenBy(candidate => candidate.UnitId)
                    .Select(candidate => (candidate.ActorId,
                        candidate.Position, candidate.Health,
                        CurrentLevel(candidate), (string?)null)),
                "nearest-ally-toward-own-reactor" => mind.Bodies
                    .Where(candidate => candidate.UnitId != body.UnitId
                        && !carried.ContainsKey(candidate.ActorId)
                        && (ArenaBasics.StaticDistance(
                                contract.Map, candidate.Position, _ownReactor)
                            ?? int.MaxValue) < (ArenaBasics.StaticDistance(
                                contract.Map, body.Position, _ownReactor)
                            ?? int.MaxValue))
                    .OrderBy(candidate => ArenaBasics.StaticDistance(
                            contract.Map, body.Position, candidate.Position)
                        ?? int.MaxValue)
                    .ThenBy(candidate => candidate.UnitId)
                    .Select(candidate => (candidate.ActorId,
                        candidate.Position, candidate.Health,
                        CurrentLevel(candidate), (string?)null)),
                "nearest-enemy" => mind.Enemies
                    .OrderBy(candidate => ArenaBasics.StaticDistance(
                            contract.Map, body.Position, candidate.Position)
                        ?? int.MaxValue)
                    .ThenBy(candidate => candidate.ActorId)
                    .Select(candidate => (candidate.ActorId,
                        candidate.Position, candidate.Health,
                        candidate.VeterancyLevel, (string?)null)),
                "weakest-enemy" => mind.Enemies
                    .OrderBy(candidate => candidate.Health)
                    .ThenBy(candidate => candidate.ActorId)
                    .Select(candidate => (candidate.ActorId,
                        candidate.Position, candidate.Health,
                        candidate.VeterancyLevel, (string?)null)),
                "enemy-carrier" => mind.Enemies.Where(candidate =>
                        _rememberedEnemies.GetValueOrDefault(candidate.ActorId)
                            ?.Carrier == true)
                    .OrderBy(candidate => candidate.ActorId)
                    .Select(candidate => (candidate.ActorId,
                        candidate.Position, candidate.Health,
                        candidate.VeterancyLevel, (string?)null)),
                "veteran" when ArenaBasics.ArcRules(contract)
                        ?.VeteranCommand == true => mind.Enemies
                    .Where(candidate => candidate.VeterancyLevel > 1)
                    .OrderByDescending(candidate => candidate.VeterancyLevel)
                    .ThenBy(candidate => ArenaBasics.StaticDistance(
                            contract.Map, body.Position, candidate.Position)
                        ?? int.MaxValue)
                    .ThenBy(candidate => candidate.ActorId)
                    .Select(candidate => (candidate.ActorId,
                        candidate.Position, candidate.Health,
                        candidate.VeterancyLevel,
                        OptionalTrace($"veteran-level-"
                            + candidate.VeterancyLevel))),
                _ => [],
                },
                _ => [],
            };
        return values.Select(value => new ResolvedTarget(
            $"body:{value.Actor}", value.Position, [value.Position],
            value.Actor, null, value.Trace))
            .Where(target => CanExecuteUse(
                contract, body, target, use, exactPlacement))
            .ToArray();
    }

    private static string? OptionalTrace(string value) => value;

    private static bool CanExecuteUse(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        ResolvedTarget target,
        OrdersPackage.UseAction use,
        Position? exactPlacement = null)
    {
        GenericActorRulesContract.ArcRelaySignature? signature =
            ArenaBasics.ArcRules(contract)?.Signatures.FirstOrDefault(value =>
                value.SignatureId == use.Skill || value.Kind == use.Skill);
        if (signature is null || signature.ClassId != body.ClassId)
            return false;
        GenericActorActionLegality? action = body.Action(signature.ActionId);
        if (action is not { Available: true })
            return false;
        bool unit = action.Constraints.OfType<GenericActorActionLegality
            .ArgumentConstraint.UnitTargetConstraint>().Any();
        bool heading = action.Constraints.OfType<GenericActorActionLegality
            .ArgumentConstraint.ProjectileHeadingConstraint>().Any();
        bool direction = action.Constraints.OfType<GenericActorActionLegality
            .ArgumentConstraint.DirectionConstraint>().Any();
        bool position = action.Constraints.OfType<GenericActorActionLegality
            .ArgumentConstraint.PositionTargetConstraint>().Any();
        if (unit && heading && target.Actor is ActorIdentity locked)
            return ArenaBasics.CanUseLockedLineSignature(
                contract, body, signature.Kind, locked, target.Position);
        if (unit && target.Actor is ActorIdentity actor)
            return ArenaBasics.CanUseUnitSignature(
                contract, body, signature.Kind, actor);
        if (heading)
            return ArenaBasics.CanUseHeadingSignature(
                contract, body, signature.Kind, target.Position);
        if (direction)
            return ArenaBasics.CanUseDirectionSignature(
                contract, body, signature.Kind);
        if (position)
        {
            if (exactPlacement is Position placement)
            {
                return action.Constraints.OfType<GenericActorActionLegality
                    .ArgumentConstraint.PositionTargetConstraint>()
                    .Single().AllowedValues.Contains(placement);
            }
            // Arc Toss is a pass, not a blind throw in an ally's general
            // direction. A named receiver must be an exact legal landing tile
            // so the engine can complete the catch authoritatively.
            if (signature.Kind == "arc-toss"
                && target.Actor is not null
                && action.Constraints.OfType<GenericActorActionLegality
                    .ArgumentConstraint.PositionTargetConstraint>()
                    .Single().AllowedValues.Contains(target.Position) == false)
            {
                return false;
            }
            return ArenaBasics.CanUsePositionSignature(
                contract, body, signature.Kind);
        }
        return ArenaBasics.CanUseParameterlessSignature(
            contract, body, signature.Kind);
    }

    private ResolvedTarget[] ResolveHandoffTargets(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        OrdersPackage.HandoffAction handoff,
        OrdersPackage package,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        if (!carried.ContainsKey(body.ActorId)
            || body.Action("handoff-core") is not { Available: true })
            return [];
        IEnumerable<MindBody> candidates = mind.Bodies.Where(candidate =>
            candidate.UnitId != body.UnitId
            && !carried.ContainsKey(candidate.ActorId)
            && candidate.Position.ChebyshevDistance(body.Position) <= 1);
        if (handoff.TargetKind == "role")
            candidates = candidates.Where(candidate =>
                package.RolesByUnit[candidate.UnitId].Id == handoff.Target);
        if (handoff.Target == "nearest-ally-toward-own-reactor")
        {
            int sourceDistance = ArenaBasics.StaticDistance(
                    contract.Map, body.Position, _ownReactor) ?? int.MaxValue;
            candidates = candidates.Where(candidate =>
                (ArenaBasics.StaticDistance(
                    contract.Map, candidate.Position, _ownReactor)
                    ?? int.MaxValue) < sourceDistance);
        }
        return candidates
            .OrderBy(candidate => ArenaBasics.StaticDistance(
                    contract.Map, candidate.Position, _ownReactor)
                ?? int.MaxValue)
            .ThenBy(candidate => candidate.UnitId)
            .Select(candidate => new ResolvedTarget(
                $"body:{candidate.ActorId}", candidate.Position,
                [candidate.Position], candidate.ActorId, null))
            .ToArray();
    }

    private static Dictionary<int, Proposal> Allocate(
        IReadOnlyDictionary<int, IReadOnlyList<Proposal>> proposalsByBody) =>
        TacticalTargetCapacityAllocator.Allocate(
            proposalsByBody.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<
                    TacticalTargetCapacityAllocator.Candidate<Proposal>>)pair.Value
                    .Select(proposal => new
                        TacticalTargetCapacityAllocator.Candidate<Proposal>(
                            proposal,
                            proposal.CapacityScope,
                            proposal.Target.Key,
                            proposal.CapacityLimit,
                            proposal.Distance))
                    .ToArray()));

    private Dictionary<int, MovePlan> PrepareMovePlans(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        OrdersPackage package,
        IReadOnlyDictionary<int, Proposal> selected)
    {
        var result = new Dictionary<int, MovePlan>();
        foreach ((int unitId, Proposal proposal) in selected)
        {
            if (proposal.Leaf.Node.Action is OrdersPackage.FightAction fight)
            {
                bool targetExists = proposal.Target.Actor is ActorIdentity actor
                    ? mind.Enemies.Any(value => value.ActorId == actor)
                    : proposal.Target.Key.StartsWith(
                        "deployment:", StringComparison.Ordinal);
                if (!targetExists || fight.Style == "hold"
                    || ArenaBasics.CanAimAtPosition(
                        contract, proposal.Body, proposal.Target.Position))
                {
                    continue;
                }
                Position anchor = FightEpisodeAnchor(
                    proposal.Body, proposal.Leaf);
                bool inLeash = fight.ChaseLimit == 0
                    ? proposal.Body.Position == anchor
                    : ArenaBasics.StaticDistance(
                        contract.Map, proposal.Body.Position, anchor)
                        <= fight.ChaseLimit;
                if (inLeash && fight.Style == "advance")
                {
                    result.Add(unitId, new MovePlan(
                        proposal.Leaf.Node.Path,
                        proposal.Target.Position,
                        [proposal.Target.Position],
                        null,
                        Arrived: false));
                }
                continue;
            }
            if (proposal.Leaf.Node.Action is not OrdersPackage.MoveAction move)
                continue;
            MindBody body = proposal.Body;
            string routeState = proposal.Leaf.Node.Path;
            if (move.Via is string ingress
                && _ingress.NeedsTraversal(
                    body.ActorId,
                    proposal.Leaf.Node.Path,
                    proposal.Leaf.ActivatedTick,
                    ingress))
            {
                MovePlan ingressPlan = RoutePlan(
                    contract, mind, package, body, proposal, ingress,
                    $"{proposal.Leaf.Node.Path}:via:{ingress}", loop: false,
                    _ingressCursors);
                if (ingressPlan.Course?.Disposition
                    == TacticalRouteGovernor.CourseDisposition.NoPath)
                {
                    _ingressCursors.Remove(unitId);
                    _ingress.Reset(unitId);
                    _routes.Reset(unitId);
                }
                if (!ingressPlan.Arrived)
                {
                    result.Add(unitId, ingressPlan);
                    continue;
                }
                _ingressCursors.Remove(unitId);
                _ingress.Complete(
                    body.ActorId,
                    proposal.Leaf.Node.Path,
                    proposal.Leaf.ActivatedTick,
                    ingress);
            }
            if (move.Kind == "patrol")
            {
                result.Add(unitId, RoutePlan(
                    contract, mind, package, body, proposal, move.Target,
                    proposal.Leaf.Node.Path, loop: true));
                continue;
            }
            Position[] goals = PostureGoals(
                proposal.Target.Goals, move.Posture);
            Position goal = Nearest(contract.Map, body.Position, goals);
            result.Add(unitId, new MovePlan(
                proposal.Leaf.Node.Path, goal, goals, null,
                goals.Contains(body.Position)));
        }
        return result;
    }

    /// <summary>
    /// A follower consumes its leader's movement plan rather than competing
    /// with it. ResolveTargets already excluded the occupied leader tile;
    /// this second phase excludes the leader's committed next course tile.
    /// The leader therefore owns the front of the file and the follower
    /// plans against the remaining adjacent standoff goals.
    /// </summary>
    private static void KeepFollowersBehindLeaders(
        GenericActorResolvedMatchContract contract,
        Dictionary<int, MovePlan> movePlans,
        IReadOnlyDictionary<int, Proposal> selected)
    {
        foreach ((int unitId, Proposal proposal) in selected.OrderBy(value =>
                     value.Key))
        {
            if (proposal.Leaf.Node.Action is not OrdersPackage.MoveAction
                    { Kind: "follow" }
                || proposal.Target.Actor is not ActorIdentity leader
                || !movePlans.TryGetValue(unitId, out MovePlan? follower)
                || !movePlans.TryGetValue(leader.UnitId, out MovePlan? lead))
            {
                continue;
            }
            Position? leadNext = lead.Course?.Next;
            if (leadNext is null || !follower.Goals.Contains(leadNext.Value))
                continue;
            Position[] goals = follower.Goals
                .Where(goal => goal != leadNext.Value)
                .ToArray();
            if (goals.Length == 0)
                continue;
            Position goal = Nearest(
                contract.Map, proposal.Body.Position, goals);
            movePlans[unitId] = follower with
            {
                Goal = goal,
                Goals = goals,
                Arrived = goals.Contains(proposal.Body.Position),
            };
        }
    }

    private MovePlan RoutePlan(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        OrdersPackage package,
        MindBody body,
        Proposal proposal,
        string routeId,
        string stateId,
        bool loop,
        Dictionary<int, RouteCursor>? cursors = null)
    {
        cursors ??= _routeCursors;
        Position[] points = package.RoutePoints(routeId);
        RouteCursor state = cursors.GetValueOrDefault(body.UnitId)
            ?? RouteCursor.For(body.ActorId, stateId);
        if (state.Actor != body.ActorId || state.StateId != stateId)
            state = RouteCursor.For(body.ActorId, stateId);
        if (!state.Initialized)
        {
            state.Index = loop
                ? Enumerable.Range(0, points.Length)
                    .OrderBy(index => ArenaBasics.StaticDistance(
                            contract.Map, body.Position, points[index])
                        ?? int.MaxValue)
                    .ThenBy(index => index)
                    .First()
                : 0;
            state.Initialized = true;
            state.BestDistance = int.MaxValue;
        }
        Position waypoint = points[state.Index];
        int distance = ArenaBasics.StaticDistance(
                contract.Map, body.Position, waypoint)
            ?? int.MaxValue;
        int arrivalRadius = state.Index + 1 < points.Length
            ? TacticalRouteGovernor.EffectiveArrivalRadius(
                RouteWaypointSlack, waypoint, points[state.Index + 1])
            : loop
                ? TacticalRouteGovernor.EffectiveArrivalRadius(
                    RouteWaypointSlack, waypoint, points[0])
                : RouteWaypointSlack;
        bool reached = TacticalRouteGovernor.ReachedWaypoint(
            distance, state.BestDistance, arrivalRadius);
        state.BestDistance = Math.Min(state.BestDistance, distance);
        if (reached)
        {
            if (state.Index + 1 < points.Length)
            {
                state.Index++;
                state.BestDistance = int.MaxValue;
            }
            else if (loop)
            {
                state.Index = 0;
                state.Lap++;
                state.BestDistance = int.MaxValue;
            }
            else
            {
                cursors[body.UnitId] = state;
                return new MovePlan(
                    proposal.Leaf.Node.Path, body.Position, [body.Position],
                    null, Arrived: true);
            }
        }
        Position destination = points[state.Index];
        HashSet<Position> obstacles = ArenaBasics.PersistentRouteObstacles(
            contract, mind, body.ActorId.TeamId);
        TacticalRouteGovernor.CourseStep course = _routes.Resolve(
            body.ActorId,
            stateId,
            routeId,
            state.Lap,
            state.Index,
            body.Position,
            destination,
            () => ArenaBasics.StaticRouteCourse(
                contract, body, destination, obstacles, _mirrored),
            tile => (tile != destination && obstacles.Contains(tile))
                || !ArenaBasics.IsLegalTerrainStep(
                    contract.Map, body.Position, tile));
        cursors[body.UnitId] = state;
        return new MovePlan(
            proposal.Leaf.Node.Path,
            destination,
            [destination],
            course,
            Arrived: false);
    }

    private TacticalTrafficGovernor.Sample[] BuildTrafficSamples(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        IReadOnlyDictionary<int, Proposal> selected,
        IReadOnlyDictionary<int, MovePlan> movePlans)
    {
        return mind.Bodies.OrderBy(value => value.UnitId).Select(body =>
        {
            MovePlan? plan = movePlans.GetValueOrDefault(body.UnitId);
            Position goal = plan?.Goal ?? body.Position;
            Position? first = plan?.Course?.Next
                ?? (plan is null ? null : ArenaBasics.StaticFirstStep(
                    contract, mind, body, goal));
            bool blocked = first is Position wanted
                && mind.Bodies.Any(other => other.UnitId != body.UnitId
                    && other.Position == wanted);
            Position? previousBlock = _traffic.AlliedBlockTile(body.UnitId);
            bool previousBlockStillOccupied = previousBlock is Position held
                && mind.Bodies.Any(other => other.UnitId != body.UnitId
                    && other.Position == held);
            Position? alliedBlockTile = blocked
                ? first
                : previousBlockStillOccupied
                    ? previousBlock
                    : null;
            return new TacticalTrafficGovernor.Sample(
                body.ActorId,
                selected.GetValueOrDefault(body.UnitId)?.Leaf.Node.Path
                    ?? "no-order",
                goal,
                ArenaBasics.StaticDistance(contract.Map, body.Position, goal),
                AttemptedMovementLastTick(contract, body),
                blocked,
                MayAge: plan is not null && !plan.Arrived,
                Urgency: selected.GetValueOrDefault(body.UnitId)?.Leaf.Node.Urgency
                    ?? TacticalMovementUrgency.Normal,
                ActivatedTick: selected.GetValueOrDefault(body.UnitId)
                    ?.Leaf.ActivatedTick ?? mind.Tick,
                AlliedBlockTile: alliedBlockTile);
        }).ToArray();
    }

    private void RankBodies(
        MindContext mind,
        ArenaBasics.Claims claims)
    {
        foreach (MindBody body in mind.Bodies)
            claims.Rank(_traffic.Priority(body.UnitId));
    }

    private static void PublishTrafficRequests(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        IReadOnlyDictionary<int, MovePlan> movePlans,
        ArenaBasics.Claims claims)
    {
        foreach ((int unitId, MovePlan plan) in movePlans)
        {
            MindBody body = mind.Bodies.Single(value => value.UnitId == unitId);
            if (claims.IsRooted(unitId))
                continue;
            Position? first = plan.Course?.Next
                ?? ArenaBasics.StaticFirstStep(contract, mind, body, plan.Goal);
            if (first is not Position wanted)
                continue;
            claims.Demand(wanted, unitId);
            claims.ReserveLane(wanted, unitId);
            ArenaBasics.DemandClearanceChain(
                contract, mind, body, wanted, claims);
        }
    }

    private void Execute(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        OrdersPackage package,
        MindBody body,
        Proposal proposal,
        MovePlan? movePlan,
        ArenaBasics.Claims claims,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        string reason = $"orders:{proposal.Leaf.Node.Path}";
        if (proposal.Leaf.EvidenceTrace is string evidenceTrace)
            reason += $":evidence[{evidenceTrace}]";
        if (proposal.SelectionTrace is string selectionTrace)
            reason += $":{selectionTrace}";
        if (proposal.Leaf.Node.Action is not OrdersPackage.FightAction
            && proposal.Target.Trace is string targetTrace)
        {
            reason += $":{targetTrace}";
        }
        string urgency = $"urgency-{claims.PriorityTrace(body.UnitId)}";
        switch (proposal.Leaf.Node.Action)
        {
            case OrdersPackage.MoveAction:
                ExecuteMove(
                    contract, mind, package, body, proposal, movePlan!, claims,
                    reason, urgency,
                    carried.ContainsKey(body.ActorId));
                return;
            case OrdersPackage.FightAction fight:
                ExecuteFight(
                    contract, mind, arc, body, proposal, fight, claims, reason);
                return;
            case OrdersPackage.UseAction use:
                if (ExecuteUse(
                        contract, package, body, proposal.Target, use, reason))
                    RememberOneShot(body, proposal, use.Skill);
                else
                    body.Hold($"{reason}:use-unavailable");
                return;
            case OrdersPackage.HandoffAction:
                if (proposal.Target.Actor is ActorIdentity receiver
                    && body.Action("handoff-core") is { Available: true } action)
                {
                    body.Command(
                        action.ActionId,
                        action.ActionCode,
                        [new GenericActorActionArgument.UnitTargetArgument(
                            new GenericActorActionArgument.UnitTarget(
                                receiver.TeamId, receiver.UnitId))],
                        $"{reason}:handoff:{receiver}");
                    RememberOneShot(body, proposal, action.ActionId);
                }
                else
                    body.Hold($"{reason}:handoff-no-target");
                return;
            case OrdersPackage.DropAction:
                if (body.Action("drop-core") is { Available: true } drop)
                {
                    body.Command(drop.ActionId, drop.ActionCode, [],
                        $"{reason}:drop");
                    RememberOneShot(body, proposal, drop.ActionId);
                }
                else
                    body.Hold($"{reason}:drop-unavailable");
                return;
            default:
                body.Hold($"{reason}:unknown-action");
                return;
        }
    }

    private void ExecuteMove(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        OrdersPackage package,
        MindBody body,
        Proposal proposal,
        MovePlan plan,
        ArenaBasics.Claims claims,
        string reason,
        string urgency,
        bool carrying)
    {
        string movement = $"{reason}:march@{plan.Goal.X},{plan.Goal.Y}"
            + $":{urgency}:{_stepper.Trace(body.UnitId)}";
        if (plan.Arrived || plan.Goals.Contains(body.Position))
        {
            if (TryRestingYield(contract, mind, body, claims, movement))
                return;
            OrdersPackage.MoveAction move =
                (OrdersPackage.MoveAction)proposal.Leaf.Node.Action!;
            if (move.FaceAnchor is string anchorId)
            {
                Position anchor = package.AnchorPosition(anchorId);
                string facing = $"{movement}:at-target:face-anchor:{anchorId}"
                    + $":face@{anchor.X},{anchor.Y}";
                Direction desired = ArenaBasics.FacingTowardAnchor(
                    body.Facing, body.Position, anchor, _mirrored);
                if (desired != body.Facing
                    && ArenaBasics.TryRotate(contract, body, desired, facing))
                {
                    return;
                }
                body.Hold($"{movement}:at-target:facing-anchor:{anchorId}"
                    + $":face@{anchor.X},{anchor.Y}");
                return;
            }
            body.Hold($"{movement}:at-target:{proposal.Target.Key}");
            return;
        }
        if (_squadPlanner?.IsAtContribution(body.UnitId) == true)
        {
            if (TryRestingYield(contract, mind, body, claims, movement))
                return;
            body.Hold($"{movement}:at-contribution:{proposal.Target.Key}");
            return;
        }
        // A carried Core can relocate only on its published cadence. This is
        // an intentional rules hold, not failed pathing. Name it before the
        // generic movement wrapper so the viewer never presents a planned
        // route as mysteriously blocked on the intervening tick.
        if (carrying
            && proposal.Target.Key == "reactor:own"
            && ArenaBasics.CarriedCore(mind, body.ActorId) is { } core
            && mind.Tick < core.NextRelocationTick)
        {
            body.Hold($"{movement}:carry-relocation-cooldown-until-"
                + core.NextRelocationTick);
            return;
        }
        if (_squadPlanner is null && plan.Course?.Next is Position step)
        {
            bool occupied = mind.Bodies.Any(candidate =>
                    candidate.UnitId != body.UnitId
                    && candidate.Position == step)
                || claims.Committed.Contains(step);
            if (occupied && _stationaryTicks.GetValueOrDefault(body.UnitId) >= 2)
            {
                HashSet<Position> blocked = ArenaBasics.BlockedNow(
                    contract, mind, body, claims, avoidHostileProjectiles: false);
                blocked.Remove(body.Position);
                TacticalRouteGovernor.CourseStep detour = _routes.TryTrafficDetour(
                    body.ActorId,
                    body.Position,
                    maximumExtraSteps: 2,
                    rejoin => blocked.Contains(rejoin)
                        ? null
                        : ArenaBasics.StaticRouteCourse(
                            contract, body, rejoin, blocked, _mirrored));
                if (detour.Next is Position alternate
                    && detour.Trace.StartsWith(
                        "route-detour-commit-", StringComparison.Ordinal)
                    && ArenaBasics.TryMoveDirect(
                        contract, mind, body, alternate, claims,
                        $"{movement}:{detour.Trace}"))
                    return;
            }
            if (ArenaBasics.TryMoveDirect(
                    contract, mind, body, step, claims,
                    $"{movement}:{plan.Course.Trace}",
                    carrying
                        ? TacticalMovementAuthority.Safety
                        : TacticalMovementAuthority.Tactical))
                return;
            body.Hold($"{movement}:{plan.Course.Trace}:blocked");
            return;
        }
        bool moved = carrying && proposal.Target.Key == "reactor:own"
            ? ArenaBasics.TryMoveRouted(
                contract, mind, body, plan.Goal, claims,
                $"{movement}:carry-home")
            : ArenaBasics.TryMoveToward(
                contract, mind, body, plan.Goals, claims,
                $"{movement}:move:{proposal.Target.Key}");
        if (!moved)
            body.Hold($"{movement}:move-blocked:{proposal.Target.Key}");
    }

    private void ExecuteFight(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        Proposal proposal,
        OrdersPackage.FightAction fight,
        ArenaBasics.Claims claims,
        string reason)
    {
        GenericActorContext.ObservedEnemyState? target = mind.Enemies
            .FirstOrDefault(value => value.ActorId == proposal.Target.Actor);
        bool deployment = proposal.Target.Key.StartsWith(
                "deployment:", StringComparison.Ordinal)
            && arc.VisibleSignatures.Any(value =>
                $"deployment:{value.OperationId}" == proposal.Target.Key);
        if (target is null && !deployment)
        {
            if (TryRestingYield(contract, mind, body, claims, reason))
                return;
            body.Hold($"{reason}:target-lost");
            return;
        }
        if (proposal.Target.Trace is string trace)
            reason += $":{trace}";
        Position targetPosition = target?.Position ?? proposal.Target.Position;
        string targetKey = target is null
            ? proposal.Target.Key
            : target.ActorId.ToString();
        Position anchor = FightEpisodeAnchor(body, proposal.Leaf);
        bool inLeash = fight.ChaseLimit == 0
            ? body.Position == anchor
            : ArenaBasics.StaticDistance(
                contract.Map, body.Position, anchor) <= fight.ChaseLimit;

        string physical = _stepper.Trace(body.UnitId);
        if (target is not null
            && fight.Style == "kite"
            && targetPosition.ChebyshevDistance(body.Position) <= 2
            && ArenaBasics.TryStepAway(
                contract, mind, body, [targetPosition], claims,
                $"{reason}:kite:{targetKey}:{physical}"))
            return;
        if (ArenaBasics.TryShootAtPosition(
                contract,
                mind,
                body,
                targetPosition,
                $"{reason}:fight:{targetKey}",
                target?.ActorId))
            return;
        if (ArenaBasics.CanAimAtPosition(contract, body, targetPosition))
        {
            if (TryRestingYield(contract, mind, body, claims, reason))
                return;
            body.Hold($"{reason}:cadence:{targetKey}");
            return;
        }
        if (fight.Style != "hold" && inLeash
            && ArenaBasics.TryMoveToward(
                contract, mind, body, [targetPosition], claims,
                $"{reason}:close:{targetKey}:{physical}"))
            return;
        if (!TryRestingYield(contract, mind, body, claims, reason))
        {
            body.Hold($"{reason}:hold:{targetKey}"
                + (inLeash ? "" : ":chase-limit")
                + $":{physical}");
        }
    }

    /// <summary>
    /// EPIBT may move, or first turn, an otherwise resting body when a
    /// higher-priority operation needs its ground. This is physical yielding
    /// only: the body keeps its authored rung and the next tick replans that
    /// same intent.
    /// Rooted actions and skill/handoff/drop commands never enter this path.
    /// </summary>
    private bool TryRestingYield(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        ArenaBasics.Claims claims,
        string reason)
    {
        return _squadPlanner is not null
            && _squadPlanner.TryGetRestingYield(
                body.UnitId, out Position destination, out string trace)
            && ArenaBasics.TryMoveDirect(
                contract,
                mind,
                body,
                destination,
                claims,
                $"{reason}:{trace}",
                TacticalMovementAuthority.Clearance);
    }

    private static bool ExecuteUse(
        GenericActorResolvedMatchContract contract,
        OrdersPackage package,
        MindBody body,
        ResolvedTarget target,
        OrdersPackage.UseAction use,
        string reason)
    {
        GenericActorRulesContract.ArcRelaySignature? signature =
            ArenaBasics.ArcRules(contract)?.Signatures.FirstOrDefault(value =>
                value.SignatureId == use.Skill || value.Kind == use.Skill);
        if (signature is null)
            return false;
        GenericActorActionLegality? action = body.Action(signature.ActionId);
        if (action is not { Available: true })
            return false;
        bool unit = action.Constraints.OfType<GenericActorActionLegality
            .ArgumentConstraint.UnitTargetConstraint>().Any();
        bool heading = action.Constraints.OfType<GenericActorActionLegality
            .ArgumentConstraint.ProjectileHeadingConstraint>().Any();
        bool direction = action.Constraints.OfType<GenericActorActionLegality
            .ArgumentConstraint.DirectionConstraint>().Any();
        bool position = action.Constraints.OfType<GenericActorActionLegality
            .ArgumentConstraint.PositionTargetConstraint>().Any();
        string resolved = use.AtAnchor is string traceAnchor
            ? $"anchor:{traceAnchor}:trigger-{target.Key}"
            : target.Key;
        string trace = $"{reason}:use:{use.Skill}:{resolved}";
        if (unit && heading && target.Actor is ActorIdentity locked)
            return ArenaBasics.TryLockedLineSignature(
                contract, body, signature.Kind, locked, target.Position, trace);
        if (unit && target.Actor is ActorIdentity actor)
            return ArenaBasics.TryUnitSignature(
                contract, body, signature.Kind, actor, trace);
        if (heading)
            return ArenaBasics.TryHeadingSignature(
                contract, body, signature.Kind, target.Position, trace);
        if (direction)
            return ArenaBasics.TryDirectionSignature(
                contract, body, signature.Kind, target.Position, trace);
        if (position)
        {
            Position? anchored = use.AtAnchor is string placementAnchor
                ? package.AnchorPosition(placementAnchor)
                : null;
            Func<Position, bool>? exactReceiver = anchored is Position placement
                ? candidate => candidate == placement
                : signature.Kind == "arc-toss" && target.Actor is not null
                    ? candidate => candidate == target.Position
                    : null;
            return ArenaBasics.TryPositionSignature(
                contract, body, signature.Kind, target.Position, trace,
                exactReceiver);
        }
        return ArenaBasics.TryParameterlessSignature(
            contract, body, signature.Kind, trace);
    }

    private OrderTreeRuntime.ConditionEvaluation Evaluate(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        OrdersPackage package,
        MindBody body,
        OrdersPackage.Role role,
        OrdersPackage.ConditionExpression expression,
        int orderTicks,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        if (expression.Names.Length == 1 && expression.Names[0] == "always")
            return OrderTreeRuntime.ConditionEvaluation.True;
        var results = expression.Names.Select(name =>
        {
            if (name == "always")
                return OrderTreeRuntime.ConditionEvaluation.True;
            OrdersPackage.Condition condition = package.Conditions[name];
            int actual;
            string? trace = null;
            if (condition.WindowTicks is not null || condition.Since is not null)
            {
                OrdersEvidenceLedger.EvidenceRead read = _evidence.Read(
                    package, contract.Map, condition, mind.Tick, orderTicks);
                actual = read.Actual;
                trace = read.Trace;
            }
            else
            {
                actual = Fact(
                    contract, mind, arc, package, body, role, condition,
                    orderTicks, carried);
            }
            return new OrderTreeRuntime.ConditionEvaluation(
                Compare(actual, condition.Operator, condition.Value), trace);
        }).ToArray();
        bool value = expression.Kind == "any"
            ? results.Any(result => result.Value)
            : results.All(result => result.Value);
        string? evidence = string.Join(
            "+",
            results.Where(result => result.Trace is not null)
                .Select(result => result.Trace));
        return new OrderTreeRuntime.ConditionEvaluation(
            value,
            string.IsNullOrEmpty(evidence) ? null : evidence);
    }

    private int Fact(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        OrdersPackage package,
        MindBody body,
        OrdersPackage.Role role,
        OrdersPackage.Condition condition,
        int orderTicks,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        string fact = condition.Fact;
        if (fact == "self-health-percent")
            return body.Health * 100 / Math.Max(1, MaxHealth(contract, body));
        if (fact == "self-level")
            return CurrentLevel(body);
        if (fact == "self-carrying")
            return carried.ContainsKey(body.ActorId) ? 1 : 0;
        if (fact == "self-carried-core-bank-value")
        {
            return carried.TryGetValue(body.ActorId, out var carriedCore)
                ? CoreBankValue(
                    contract, arc, carriedCore.CoreId.SourceWellId)
                : 0;
        }
        if (fact == "self-stuck-ticks")
            return _stationaryTicks.GetValueOrDefault(body.UnitId);
        if (fact == "self-order-ticks")
            return orderTicks;
        if (fact == "self-in-zone")
            return package.Contains(condition.Zone!, body.Position) ? 1 : 0;
        if (fact == "self-nearest-visible-enemy-distance")
        {
            // Distance means work needed to JOIN the fight: the shortest
            // legal movement path from this body to a firing tile. Durable
            // known blockers count; allied and hostile bodies are traffic.
            // No reachable firing tile must fail every authored upper bound.
            return NearestFiringApproachDistance(
                contract, mind, body) ?? 32;
        }
        if (fact == "signature-ready")
        {
            GenericActorRulesContract.ArcRelaySignature? signature =
                ArenaBasics.ArcRules(contract)?.Signatures.FirstOrDefault(value =>
                    value.ClassId == body.ClassId);
            return signature is not null
                && body.Action(signature.ActionId) is { Available: true } ? 1 : 0;
        }
        if (fact is "deployment-active" or "deployment-lifetime-percent"
                or "deployment-durability-percent")
        {
            GenericActorContext.ArcRelaySignatureState? deployment =
                OwnedDeployment(arc, body);
            if (deployment is null)
                return 0;
            if (fact == "deployment-active")
                return 1;
            if (fact == "deployment-durability-percent")
            {
                return deployment.MaximumCapacity <= 0
                    ? 100
                    : Math.Clamp(
                        deployment.RemainingCapacity * 100
                            / deployment.MaximumCapacity,
                        1,
                        100);
            }
            if (deployment.EndsAtTick is not int ends)
                return 100;
            int duration = Math.Max(1, ends - deployment.StartedTick);
            return Math.Clamp(
                Math.Max(0, ends - mind.Tick) * 100 / duration,
                1,
                100);
        }
        if (fact.StartsWith("role-", StringComparison.Ordinal))
        {
            MindBody[] members = mind.Bodies.Where(candidate =>
                    package.RolesByUnit[candidate.UnitId].Id == condition.Role)
                .ToArray();
            if (fact == "role-live-count")
                return members.Length;
            if (fact == "role-highest-level")
                return members.Length == 0 ? 0 : members.Max(CurrentLevel);
            if (fact == "role-lowest-health-percent")
                return members.Length == 0 ? 100 : members.Min(candidate =>
                    candidate.Health * 100
                    / Math.Max(1, MaxHealth(contract, candidate)));
            return members.Count(candidate =>
                package.Contains(condition.Zone!, candidate.Position));
        }
        if (fact == "live-friendlies")
            return mind.Bodies.Length;
        if (fact == "live-enemies")
            return WaveTeam(arc, enemy: true)?.LiveBodies
                ?? mind.Enemies.Length;
        if (fact == "friendly-respawns-next-wave")
            return WaveTeam(arc, enemy: false)?.RespawnsNextWave ?? 0;
        if (fact == "enemy-respawns-next-wave")
            return WaveTeam(arc, enemy: true)?.RespawnsNextWave ?? 0;
        if (fact == "ticks-until-respawn-wave")
            return arc.NextRespawnWaveTick is int nextWave
                ? Math.Max(0, nextWave - mind.Tick)
                : int.MaxValue;
        if (fact == "friendly-carriers")
            return carried.Keys.Count(actor => actor.TeamId == _teamId);
        if (fact == "visible-enemy-carriers")
            return arc.VisibleCores.Count(core =>
                core.CarrierActorId?.TeamId != _teamId
                && core.Disposition
                    == GenericActorContext.ArcRelayCoreDisposition.Carried);
        if (fact == "known-enemy-carriers")
            return _rememberedEnemies.Values.Count(enemy => enemy.Carrier
                && mind.Tick - enemy.Tick <= condition.FreshnessTicks);
        if (fact == "known-loose-cores")
            return _knownLooseCores.Count;
        if (fact == "visible-loose-cores")
            return arc.VisibleCores.Count(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Loose);
        if (fact == "secured-cores")
            return carried.Keys.Count(actor => actor.TeamId == _teamId);
        GenericActorContext.ArcRelayReactorState own = arc.Reactors.Single(value =>
            value.TeamId == _teamId);
        if (fact == "reactor-integrity")
            return own.IntegritySegments;
        if (fact == "reactor-charge")
            return own.ChargePips;
        if (fact == "enemy-reactor-charge")
            return arc.Reactors.Single(value => value.TeamId != _teamId)
                .ChargePips;
        if (fact == "reactor-pulses")
        {
            long pulses = mind.Scoreboard.Teams
                .Single(value => value.TeamId == _teamId)
                .Scores.Single(value => value.Channel == "pulses").Value;
            return checked((int)pulses);
        }
        if (fact == "ticks-without-objective-progress")
            return mind.Tick - _lastObjectiveProgressTick;
        if (fact == "friendlies-in-zone")
            return mind.Bodies.Count(candidate =>
                package.Contains(condition.Zone!, candidate.Position));
        if (fact == "visible-enemies-in-zone")
            return mind.Enemies.Count(enemy =>
                package.Contains(condition.Zone!, enemy.Position));
        if (fact == "remembered-enemies-in-zone")
            return _rememberedEnemies.Values.Count(enemy =>
                mind.Tick - enemy.Tick <= condition.FreshnessTicks
                && package.Contains(condition.Zone!, enemy.Position));
        if (fact == "known-loose-cores-in-zone")
            return _knownLooseCores.Values.Count(core =>
                package.Contains(condition.Zone!, core.Position));
        GenericActorContext.ArcRelayWellState well = ResolveWell(
            contract, arc, condition.Well!);
        if (fact == "well-has-outstanding-core")
            return well.OutstandingCoreId is null ? 0 : 1;
        if (fact == "well-ticks-until-birth")
            return well.NextScheduledBirthTick is int birth
                ? Math.Max(0, birth - mind.Tick)
                : int.MaxValue;
        return own.FilledSocketWellIds.Contains(well.WellId,
            StringComparer.Ordinal) ? 1 : 0;
    }

    private static bool Compare(int actual, string op, int expected) => op switch
    {
        "at-least" => actual >= expected,
        "at-most" => actual <= expected,
        "equals" => actual == expected,
        "less-than" => actual < expected,
        "greater-than" => actual > expected,
        _ => false,
    };

    private void UpdateMemory(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc)
    {
        HashSet<ActorIdentity> visibleCarriers = arc.VisibleCores
            .Where(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Carried
                && core.CarrierActorId?.TeamId != _teamId)
            .Select(core => core.CarrierActorId!)
            .ToHashSet();
        foreach (var enemy in mind.Enemies)
            _rememberedEnemies[enemy.ActorId] = new RememberedEnemy(
                enemy.Position, mind.Tick, visibleCarriers.Contains(enemy.ActorId));

        foreach (GenericActorContext.ArcRelayCoreState core in arc.VisibleCores)
        {
            string key = CoreKey(core.CoreId);
            if (core.Disposition == GenericActorContext.ArcRelayCoreDisposition.Loose)
                _knownLooseCores[key] = new KnownCore(
                    key, core.CoreId, core.Position, mind.Tick);
            else
                _knownLooseCores.Remove(key);
        }
        foreach (GenericActorContext.ObservedEvent observed in mind.VisibleEvents)
        {
            if (!_processedEvents.Add(observed.EventHandle)
                || observed.Payload is not GenericActorContext.EventPayload.ArcRelay mode)
                continue;
            switch (mode.Fact)
            {
                case GenericActorContext.ArcRelayEvent.CoreBorn born:
                    Remember(born.CoreId, born.Position, observed.SourceTick);
                    break;
                case GenericActorContext.ArcRelayEvent.CoreRipened ripened:
                    Remember(ripened.CoreId, ripened.Position, observed.SourceTick);
                    break;
                case GenericActorContext.ArcRelayEvent.CoreDropped dropped:
                    Remember(dropped.CoreId, dropped.Position, observed.SourceTick);
                    break;
                case GenericActorContext.ArcRelayEvent.CoreRelocated relocated
                    when relocated.CarrierActorId is null:
                    Remember(relocated.CoreId, relocated.To, observed.SourceTick);
                    break;
                case GenericActorContext.ArcRelayEvent.CorePickedUp picked:
                    _knownLooseCores.Remove(CoreKey(picked.CoreId));
                    break;
                case GenericActorContext.ArcRelayEvent.CoreHandedOff handed:
                    _knownLooseCores.Remove(CoreKey(handed.CoreId));
                    break;
                case GenericActorContext.ArcRelayEvent.CoreBanked banked:
                    _knownLooseCores.Remove(CoreKey(banked.CoreId));
                    _lastObjectiveProgressTick = observed.SourceTick;
                    break;
                case GenericActorContext.ArcRelayEvent.LeveledUp level
                    when level.ActorId.TeamId == _teamId:
                    _levels[level.ActorId.UnitId] = (level.ActorId, level.Level);
                    _pendingInvest[level.ActorId.UnitId] =
                        _pendingInvest.GetValueOrDefault(level.ActorId.UnitId) + 1;
                    break;
            }
        }
        HashSet<Position> visible = mind.VisibleTiles.Select(tile => tile.Position)
            .ToHashSet();
        HashSet<Position> looseVisible = arc.VisibleCores
            .Where(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Loose)
            .Select(core => core.Position).ToHashSet();
        foreach (string stale in _knownLooseCores
                     .Where(value => visible.Contains(value.Value.Position)
                         && !looseVisible.Contains(value.Value.Position))
                     .Select(value => value.Key).ToArray())
            _knownLooseCores.Remove(stale);

        int charge = arc.Reactors.Single(value => value.TeamId == _teamId).ChargePips;
        if (charge != _lastOwnCharge)
        {
            _lastOwnCharge = charge;
            _lastObjectiveProgressTick = mind.Tick;
        }
    }

    private void Remember(
        GenericActorContext.ArcRelayCoreId core,
        Position position,
        int tick)
    {
        string key = CoreKey(core);
        _knownLooseCores[key] = new KnownCore(key, core, position, tick);
    }

    private void ResolvePreviousOneShots(MindContext mind)
    {
        foreach (MindBody body in mind.Bodies)
        {
            if (!_pendingOneShots.TryGetValue(body.UnitId, out PendingOneShot? pending)
                || pending.Actor != body.ActorId)
                continue;
            GenericActorActionResolution? resolution = body.PreviousActionResolution;
            if (resolution?.SubmittedAction?.ActionId == pending.ActionId
                && resolution.Outcome
                    == GenericActorActionResolution.ActionOutcome.Success)
            {
                _orders.Complete(body.ActorId, pending.Node, mind.Tick - 1);
            }
            _pendingOneShots.Remove(body.UnitId);
        }
    }

    private void RememberOneShot(MindBody body, Proposal proposal, string actionId) =>
        _pendingOneShots[body.UnitId] = new PendingOneShot(
            body.ActorId, proposal.Leaf.Node, actionId);

    private void UpdateMotionMemory(MindContext mind)
    {
        HashSet<int> live = mind.Bodies.Select(body => body.UnitId).ToHashSet();
        foreach (int stale in _stationaryTicks.Keys.Where(value => !live.Contains(value))
                     .ToArray())
            _stationaryTicks.Remove(stale);
        foreach (MindBody body in mind.Bodies)
        {
            _stationaryTicks[body.UnitId] = body.MovedLastTick
                ? 0
                : _stationaryTicks.GetValueOrDefault(body.UnitId) + 1;
        }
    }

    private void ReconcileSelections(
        MindContext mind,
        IReadOnlyDictionary<int, Proposal> selected)
    {
        HashSet<int> live = mind.Bodies.Select(body => body.UnitId).ToHashSet();
        HashSet<ActorIdentity> liveActors = mind.Bodies
            .Select(body => body.ActorId).ToHashSet();
        foreach (int stale in _selectedPaths.Keys.Where(value => !live.Contains(value))
                     .ToArray())
        {
            _selectedPaths.Remove(stale);
            _routeCursors.Remove(stale);
            _ingressCursors.Remove(stale);
            _ingress.Reset(stale);
            _routes.Reset(stale);
        }
        foreach ((ActorIdentity actor, string path) in _fightAnchors.Keys
                     .Where(key => !liveActors.Contains(key.Actor)).ToArray())
            _fightAnchors.Remove((actor, path));
        foreach (MindBody body in mind.Bodies)
        {
            string path = selected.GetValueOrDefault(body.UnitId)?.Leaf.Node.Path
                ?? "";
            if (!_selectedPaths.TryGetValue(body.UnitId, out string? prior)
                || prior != path)
            {
                _routeCursors.Remove(body.UnitId);
                _routes.Reset(body.UnitId);
            }
            _selectedPaths[body.UnitId] = path;
        }
    }

    private Position FightEpisodeAnchor(
        MindBody body,
        OrderTreeRuntime.EligibleLeaf leaf)
    {
        (ActorIdentity, string) key = (body.ActorId, leaf.Node.Path);
        FightAnchor state = _fightAnchors.GetValueOrDefault(key)
            ?? new FightAnchor(body.Position, leaf.ActivatedTick);
        if (state.ActivatedTick != leaf.ActivatedTick)
            state = new FightAnchor(body.Position, leaf.ActivatedTick);
        _fightAnchors[key] = state;
        return state.Position;
    }

    private int? NearestFiringApproachDistance(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body)
    {
        if (_firingApproachDistances.TryGetValue(
                body.ActorId, out int? cached))
            return cached;
        int? distance = ArenaBasics.NearestFiringApproachDistance(
            contract, mind, body);
        _firingApproachDistances[body.ActorId] = distance;
        return distance;
    }

    private Position[] FiringContributionGoals(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Position target,
        Position anchor,
        int chaseLimit)
    {
        var key = new FiringGroundKey(
            body.ActorId, target, anchor, chaseLimit);
        if (_firingGround.TryGetValue(key, out Position[]? cached))
            return cached;
        var episode = new FiringEpisodeKey(body.ActorId, anchor, chaseLimit);
        if (!_firingEpisodeFields.TryGetValue(
                episode, out int[]? reachable))
        {
            reachable = ArenaBasics.FiringEpisodeReachability(
                contract, mind, body, anchor, chaseLimit);
            _firingEpisodeFields[episode] = reachable;
        }
        Position[] goals = ArenaBasics.FiringContributionGoals(
            contract, body, target, reachable);
        _firingGround[key] = goals;
        return goals;
    }

    private bool TryInvest(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        OrdersPackage.Role role,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        if (!WillInvest(contract, mind, body, role, carried)
            || body.Action("invest") is not { Available: true } invest)
            return false;
        (ActorIdentity Actor, int Spent) state = _invested.GetValueOrDefault(
            body.UnitId, (body.ActorId, 0));
        if (state.Actor != body.ActorId)
            state = (body.ActorId, 0);
        string track = role.UpgradePath[Math.Min(
            state.Spent, role.UpgradePath.Length - 1)];
        body.Command(
            invest.ActionId,
            invest.ActionCode,
            [new GenericActorActionArgument.UpgradeTrackArgument(track)],
            $"orders:{role.Id}:upgrade:{track}");
        _invested[body.UnitId] = (body.ActorId, state.Spent + 1);
        _pendingInvest[body.UnitId]--;
        return true;
    }

    private bool WillInvest(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        OrdersPackage.Role role,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        _ = contract;
        _ = role;
        return _pendingInvest.GetValueOrDefault(body.UnitId) > 0
            && !carried.ContainsKey(body.ActorId)
            && !mind.Enemies.Any(enemy =>
                enemy.Position.ChebyshevDistance(body.Position) <= 5)
            && body.Action("invest") is { Available: true };
    }

    private int CurrentLevel(MindBody body) =>
        _levels.TryGetValue(body.UnitId, out (ActorIdentity Actor, int Level) level)
            && level.Actor == body.ActorId ? level.Level : 1;

    private Position[] PostureGoals(Position[] goals, string? posture)
        => TacticalPosturePlanner.Resolve(
            goals, posture, _ownReactor, _enemyReactor);

    private static Position[] AdjacentStandTiles(
        GenericActorMapContract map,
        Position anchor)
    {
        var result = new List<Position>(8);
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dy == 0)
                continue;
            Position candidate = anchor.Offset(dx, dy);
            if (ArenaBasics.CanEnter(map, candidate))
                result.Add(candidate);
        }
        return result.OrderBy(point => point.Y).ThenBy(point => point.X)
            .ToArray();
    }

    private static Position Nearest(
        GenericActorMapContract map,
        Position from,
        IReadOnlyCollection<Position> targets) => targets
        .OrderBy(target => ArenaBasics.StaticDistance(map, from, target)
            ?? int.MaxValue)
        .ThenBy(target => target.Y)
        .ThenBy(target => target.X)
        .First();

    private GenericActorContext.ArcRelayWellState ResolveWell(
        GenericActorResolvedMatchContract contract,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        string name)
    {
        if (name is "north" or "centre" or "south")
            return arc.Wells.Single(well => string.Equals(
                well.WellId, name, StringComparison.Ordinal));
        IEnumerable<GenericActorContext.ArcRelayWellState> ordered = arc.Wells
            .OrderBy(well => ArenaBasics.StaticDistance(
                    contract.Map, _ownReactor, well.Position) ?? int.MaxValue)
            .ThenBy(well => well.WellId, StringComparer.Ordinal);
        return name == "own" ? ordered.First() : ordered.Last();
    }

    private int CoreBankValue(
        GenericActorResolvedMatchContract contract,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        string sourceWellId)
    {
        GenericActorRulesContract.ArcRelayGameMode? rules =
            ArenaBasics.ArcRules(contract);
        if (rules is null)
            return 1;
        GenericActorContext.ArcRelayWellState well = arc.Wells.Single(value =>
            value.WellId == sourceWellId);
        int own = ArenaBasics.StaticDistance(
            contract.Map, _ownReactor, well.Position) ?? int.MaxValue;
        int enemy = ArenaBasics.StaticDistance(
            contract.Map, _enemyReactor, well.Position) ?? int.MaxValue;
        if (own == enemy)
            return rules.CentreWellCoreCharge;
        return own < enemy
            ? rules.OwnWellCoreCharge
            : rules.EnemyWellCoreCharge;
    }

    private GenericActorContext.ArcRelayRespawnWaveTeamState? WaveTeam(
        GenericActorContext.ModeObservationState.ArcRelay arc,
        bool enemy) => arc.RespawnWaveTeams.SingleOrDefault(value =>
            enemy ? value.TeamId != _teamId : value.TeamId == _teamId);

    private GenericActorContext.ArcRelaySignatureState? OwnedDeployment(
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body) => arc.VisibleSignatures
        .Where(value => value.OwnerActorId.TeamId == body.ActorId.TeamId
            && value.OwnerActorId.UnitId == body.ActorId.UnitId
            && value.Phase == GenericActorContext.ArcRelaySignaturePhase.Active
            && IsDeploymentKind(value.Kind))
        .OrderBy(value => value.OperationId, StringComparer.Ordinal)
        .FirstOrDefault();

    private static int MaxHealth(
        GenericActorResolvedMatchContract contract,
        MindBody body) => contract.Rules.Forms.Single(value =>
            value.Id == body.FormId).MaxHealth;

    private static bool AttemptedMovementLastTick(
        GenericActorResolvedMatchContract contract,
        MindBody body)
    {
        string? actionId = body.PreviousActionResolution?.AcceptedAction.ActionId;
        return actionId is not null
            && contract.Rules.Actions.Any(action =>
                action.Id == actionId
                && action.Kind == GenericActorRulesContract.ActionKind.Movement);
    }

    private static void ValidateComposition(
        GenericActorResolvedMatchContract contract,
        int teamId,
        OrdersPackage package)
    {
        string[] actual = contract.Topology.UnitSlots
            .Where(slot => slot.TeamId == teamId)
            .OrderBy(slot => slot.UnitId)
            .Select(slot => slot.ClassId ?? "")
            .ToArray();
        string[] expected = package.Roles
            .SelectMany(role => role.Classes).ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidDataException(
                "Arc Relay sheet composition does not match the resolved team.");
    }

    private static string CoreKey(GenericActorContext.ArcRelayCoreId id) =>
        $"{id.SourceWellId}:{id.SourceOrdinal}";

    private sealed record Proposal(
        MindBody Body,
        OrdersPackage.Role Role,
        OrderTreeRuntime.EligibleLeaf Leaf,
        int Priority,
        ResolvedTarget Target,
        string? CapacityScope,
        int CapacityLimit,
        int Distance,
        string? SelectionTrace);

    private sealed record ResolvedTarget(
        string Key,
        Position Position,
        Position[] Goals,
        ActorIdentity? Actor,
        string? CoreKey,
        string? Trace = null);

    private sealed record MovePlan(
        string Path,
        Position Goal,
        Position[] Goals,
        TacticalRouteGovernor.CourseStep? Course,
        bool Arrived);

    private sealed class RouteCursor
    {
        private RouteCursor(ActorIdentity actor, string stateId)
        {
            Actor = actor;
            StateId = stateId;
        }

        public ActorIdentity Actor { get; }
        public string StateId { get; }
        public int Index { get; set; }
        public int Lap { get; set; }
        public bool Initialized { get; set; }
        public int BestDistance { get; set; } = int.MaxValue;
        public static RouteCursor For(ActorIdentity actor, string stateId) =>
            new(actor, stateId);
    }

    private sealed record PendingOneShot(
        ActorIdentity Actor,
        OrdersPackage.OrderNode Node,
        string ActionId);
    private readonly record struct FiringGroundKey(
        ActorIdentity Actor,
        Position Target,
        Position Anchor,
        int ChaseLimit);
    private readonly record struct FiringEpisodeKey(
        ActorIdentity Actor,
        Position Anchor,
        int ChaseLimit);
    private sealed record FightAnchor(Position Position, int ActivatedTick);
    private sealed record KnownCore(
        string Key,
        GenericActorContext.ArcRelayCoreId CoreId,
        Position Position,
        int SeenTick);
    private sealed record RememberedEnemy(Position Position, int Tick, bool Carrier);
}
