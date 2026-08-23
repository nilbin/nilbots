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
    // Consecutive fully-blocked presses per body for one move order: the
    // signal that a march is wedged against something no queue will clear
    // (often bodies its vision cone cannot see), scoped to the order path so
    // a fresh order always presses anew. Cleared by any successful move.
    private readonly Dictionary<int, (string Path, int Ticks)> _wedgedPresses = [];
    private readonly Dictionary<int, (ActorIdentity? Actor, int Level)> _levels = [];
    private readonly Dictionary<int, (ActorIdentity? Actor, int Points)>
        _pendingInvest = [];
    private readonly Dictionary<int, (ActorIdentity? Actor, int Spent)> _invested = [];
    private readonly Dictionary<int, PendingOneShot> _pendingOneShots = [];
    private readonly Dictionary<int, PendingSoloToss> _pendingSoloTosses = [];
    private readonly Dictionary<ActorIdentity, int> _lastSignatureUseTick = [];
    private readonly Dictionary<(ActorIdentity Actor, string Path), FightAnchor>
        _fightAnchors = [];
    private readonly Dictionary<string, KnownCore> _knownLooseCores =
        new(StringComparer.Ordinal);
    private readonly Dictionary<ActorIdentity, RememberedEnemy> _rememberedEnemies = [];
    private readonly HashSet<string> _processedEvents = new(StringComparer.Ordinal);
    private readonly Dictionary<(ActorIdentity Actor, string? Zone), int?>
        _firingApproachDistances = [];
    private readonly Dictionary<ActorIdentity, int[]> _walkingDistanceFields = [];
    private readonly Dictionary<FiringEpisodeKey, int[]> _firingEpisodeFields = [];
    private readonly Dictionary<FiringGroundKey, Position[]> _firingGround = [];
    private readonly OrdersEvidenceLedger _evidence = new();
    private readonly Dictionary<int, ScanCommitment> _scanCommitments = [];
    // The previous decision's selected Fight leaves are the coordinator's
    // deterministic view of "currently fighting". Program conditions run
    // before this tick's leaves are selected, so this intentionally carries
    // the ordinary one-decision-boundary delay instead of inventing a second
    // combat state machine.
    private TacticalZoneFightState.ActiveFight[] _activeFights = [];

    private GenericActorResolvedMatchContract? _contract;
    private OrdersPackage? _package;
    private TacticsProgramCoordinator? _coordinator;
    // The tick's decision-reason prefix from the squad program: the active
    // "t1/p2 " pair, plus a "!marker " on transition/abort/select ticks.
    // Empty on legacy (no-program) sheets so their reasons stay byte-stable.
    private string _programPrefix = "";
    private CustodyLatch _custodyLatch = new(teamId: 0);

    /// <summary>The attribution prefix for one body: a latched carrier
    /// reports the phase that began its carry (that is whose leaves it is
    /// running); everyone else reports the squad's active pair.</summary>
    private string ProgramPrefix(int unitId)
    {
        if (_programPrefix.Length == 0)
            return _programPrefix;
        CustodyLatch.Latch? latch = _custodyLatch.For(unitId);
        return latch is null ? _programPrefix : $"{latch.PhaseTag} ";
    }
    private IArenaStepper _stepper = new SquadArenaPlanner();
    private SquadArenaPlanner? _squadPlanner;
    private Position _ownReactor;
    private Position[] _ownReactorApproach = [];
    private Position _enemyReactor;
    private Position[] _healTiles = [];
    private bool _mirrored;
    private bool _retainsVeterancy;
    private int _teamId;
    private int _lastOwnCharge;
    private int _lastObjectiveProgressTick;

    public void StartMatch(MindStart start)
    {
        _contract = start.Contract;
        _teamId = start.TeamId;
        _retainsVeterancy = ArenaBasics.ArcRules(start.Contract)
            ?.VeterancyDeathRetentionPercent > 0;
        _custodyLatch = new CustodyLatch(_teamId);
        _pendingSoloTosses.Clear();
        _evidence.Reset();
        _activeFights = [];
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
        _ownReactorApproach = AdjacentEnterableTiles(
            start.Contract.Map, _ownReactor);
        _mirrored = _ownReactor.X > (start.Contract.Map.Width - 1) / 2;
        _package = OrdersPackage.Load(
            start.EvaluationData, start.Contract, _ownReactor);
        _coordinator = _package.Program is null
            ? null
            : new TacticsProgramCoordinator(_package.Program);
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
        _walkingDistanceFields.Clear();
        _firingEpisodeFields.Clear();
        _firingGround.Clear();

        UpdateMemory(contract, mind, arc);
        ReconcileSoloTosses(mind, arc);
        _evidence.Observe(_teamId, mind);
        if (ArenaBasics.ArcState(mind) is { } observedArc)
            _evidence.ObserveWorkshops(_teamId, mind.Tick, observedArc.Workshops);
        ResolvePreviousOneShots(contract, mind);
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

        // The squad program steps once, before any body evaluates: it owns
        // which tactic/phase tags are lit this tick. Its cases evaluate at
        // squad level (no body — validation bans body-scoped facts there).
        TacticsProgramCoordinator.View? program = _coordinator?.Step(
            mind.Tick,
            (expression, anchorTicks) => Evaluate(
                contract, mind, arc, package, body: null, role: null,
                expression, anchorTicks, carried));
        _programPrefix = program is null
            ? ""
            : (program.TacticId is null
                    ? "idle"
                    : $"{program.TacticId}/{program.PhaseId}")
                + (program.Marker is string marker ? $"!{marker}" : "")
                + " ";
        // Custody provenance is team-local and Core-local. The coordinator's
        // pickup snapshot names the phase that owned the preceding world
        // step, even when the pickup event also advances the squad this tick.
        // Drop, pass, death and temporary enemy possession change only the
        // active carrier; the public Well lifecycle resolves the provenance.
        _custodyLatch.Observe(
            arc.VisibleCores.Select(core =>
                    new CustodyLatch.CoreObservation(
                        core.CoreId, core.CarrierActorId))
                .ToArray(),
            arc.Wells
                .Where(well => well.OutstandingCoreId is not null)
                .Select(well => well.OutstandingCoreId!)
                .ToArray(),
            program?.PickupTacticId,
            program?.PickupPhaseId,
            program?.PickupCustodyGroups ?? []);

        var proposalsByBody = new Dictionary<int, IReadOnlyList<Proposal>>();
        var eligibilityByBody = new Dictionary<int, string?>();
        foreach (MindBody body in mind.Bodies.OrderBy(value => value.UnitId))
        {
            OrdersPackage.Role role = package.RolesByUnit[body.UnitId];
            body.SetRole(role.Id.Length <= 24 ? role.Id : role.Id[..24]);
            OrderTreeRuntime.EligibilityResult eligibility = _orders.Eligible(
                body.ActorId,
                role,
                mind.Tick,
                (expression, orderTicks) => Evaluate(
                    contract, mind, arc, package, body, role,
                    expression, orderTicks, carried),
                _custodyLatch.Augment(body.UnitId, program?.EnabledTags));
            List<Proposal> proposals = BuildProposals(
                contract, mind, arc, package, body, role,
                eligibility.Leaves, carried);
            string? eligibilityTrace = EligibilityTrace(
                eligibility.IneligibleRoots);
            eligibilityByBody[body.UnitId] = eligibilityTrace;
            if (eligibilityTrace is not null)
            {
                proposals = proposals.Select(proposal => proposal with
                {
                    EligibilityTrace = eligibilityTrace,
                }).ToList();
            }
            proposalsByBody.Add(body.UnitId, proposals);
        }
        Dictionary<int, Proposal> selected = Allocate(proposalsByBody);
        ReconcileSelections(mind, selected);
        TacticalZoneFightState.ActiveFight[] nextActiveFights = selected.Values
            .Where(value => value.Leaf.Node.Action
                is OrdersPackage.FightAction)
            .Select(value => new TacticalZoneFightState.ActiveFight(
                value.Body.ActorId,
                value.Body.Position,
                value.Target.Position,
                value.Leaf.Node.Path))
            .OrderBy(value => value.Fighter.UnitId)
            .ToArray();
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
                body.Hold($"orders:{ProgramPrefix(body.UnitId)}arc-toss-catch:{catchReason}");
                continue;
            }
            if (TryInvest(contract, mind, body, role, carried))
                continue;
            string prefix = selected.TryGetValue(body.UnitId, out Proposal? proposal)
                ? $"orders:{ProgramPrefix(body.UnitId)}{proposal.Leaf.Node.Path}"
                : $"orders:{ProgramPrefix(body.UnitId)}{role.Id}";
            if ((proposal?.EligibilityTrace
                    ?? eligibilityByBody.GetValueOrDefault(body.UnitId))
                is string eligibilityTrace)
                prefix += $":eligibility[{eligibilityTrace}]";
            string movementPrefix = $"{prefix}"
                + $":urgency-{claims.PriorityTrace(body.UnitId)}";
            if (_squadPlanner is null && ArenaBasics.TryMakeWay(
                    contract, mind, body, claims,
                    $"{movementPrefix}:make-way"))
                continue;
            if (ArenaBasics.TryEvade(
                    contract, mind, body, claims,
                    _stepper.Trace(body.UnitId),
                    preserveImmediateEngagement:
                        proposal?.Leaf.Node.Action
                            is OrdersPackage.FightAction))
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
        _activeFights = nextActiveFights;
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
                        if (move.Kind == "to"
                            && move.TargetKind == "dynamic"
                            && move.Target == "current-position")
                        {
                            result.Add(Hold(
                                "signature-commitment-hold", pinned: true));
                            break;
                        }
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
                        bool needsWindupMargin = target is not null
                            && NeedsWindupClosingMargin(
                                contract, mind, body, target.ActorId,
                                targetPosition, fight);
                        bool outsidePreferredRange = fight.PreferredRange is int
                                preferredRange
                            && body.Position.ChebyshevDistance(targetPosition)
                                > preferredRange;
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
                        if (!needsWindupMargin && !outsidePreferredRange
                            && ArenaBasics.CommitsAttackPositionThisTick(
                                contract, mind, body, targetPosition))
                        {
                            result.Add(Hold(
                                $"fight-commit:{targetKey}", pinned: true));
                            break;
                        }
                        if (!needsWindupMargin && !outsidePreferredRange
                            && ArenaBasics.CanAimAtPositionWithClearFriendlyLine(
                                contract, mind, body, targetPosition))
                        {
                            result.Add(Hold(
                                $"fight-position:{targetKey}",
                                pinned: fight.Style == "hold"));
                            break;
                        }
                        string holdMotive = "";
                        bool holdClose = fight.Style == "hold"
                            && HoldMayClose(
                                contract, mind, body, target?.ActorId,
                                out holdMotive);
                        bool inLeash = fight.ChaseLimit == 0
                            ? body.Position == anchor
                            : ArenaBasics.StaticDistance(
                                contract.Map, body.Position, anchor)
                                <= fight.ChaseLimit;
                        if (!inLeash
                            || fight.Style != "advance" && !holdClose)
                        {
                            result.Add(Hold($"fight-leash:{targetKey}"));
                            break;
                        }
                        Position[] firing = FiringContributionGoals(
                            contract, mind, body, targetPosition, anchor,
                            fight.ChaseLimit);
                        firing = PreferredRangeGoals(
                            fight, targetPosition, firing);
                        if (needsWindupMargin)
                        {
                            firing = WindupBufferedFiringGoals(
                                contract, body, targetPosition, firing);
                        }
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
                            holdClose
                                ? $"hold-{holdMotive}-close:{targetKey}"
                                : $"fight-close:{targetKey}"));
                        break;
                    }
                case OrdersPackage.ScanAction scan:
                    {
                        MovePlan? plan = movePlans.GetValueOrDefault(body.UnitId);
                        if (plan is null || plan.Arrived)
                        {
                            result.Add(Hold(
                                $"scan-view:{scan.Zone}:{proposal.Target.Key}",
                                pinned: true));
                            break;
                        }
                        result.Add(new SquadMovementIntent(
                            body,
                            key,
                            $"scan:{scan.Zone}:{proposal.Target.Key}",
                            plan.Goals,
                            null,
                            priority,
                            Pinned: false,
                            PrefersHold: false,
                            CanStage: true,
                            LeaderUnitId: null,
                            RouteClass: SquadRouteClass.Ordinary,
                            $"scan-move:{scan.Zone}:{proposal.Target.Key}"));
                        break;
                    }
                case OrdersPackage.UseAction use:
                    {
                        MovePlan? plan = movePlans.GetValueOrDefault(body.UnitId);
                        if (plan is null || plan.Arrived)
                        {
                            result.Add(Hold(
                                $"signature:{use.Skill}:{proposal.Target.Key}",
                                pinned: true));
                            break;
                        }
                        result.Add(new SquadMovementIntent(
                            body,
                            key,
                            $"signature-approach:{use.Skill}:{proposal.Target.Key}",
                            plan.Goals,
                            null,
                            priority,
                            Pinned: false,
                            PrefersHold: false,
                            CanStage: true,
                            LeaderUnitId: null,
                            RouteClass: SquadRouteClass.Ordinary,
                            $"signature-approach:{use.Skill}:{proposal.Target.Key}"));
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
    /// Wells, healers, and Workshops are one-tile services, not eight identical
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
                || targetKey.StartsWith("heal:", StringComparison.Ordinal)
                || targetKey.StartsWith("workshop:", StringComparison.Ordinal));

    private static Position[] AdjacentEnterableTiles(
        GenericActorMapContract map,
        Position tile)
    {
        var ring = new List<Position>();
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;
                var candidate = new Position(tile.X + dx, tile.Y + dy);
                if (ArenaBasics.CanEnter(map, candidate))
                    ring.Add(candidate);
            }
        }
        return [.. ring
            .OrderBy(value => value.Y)
            .ThenBy(value => value.X)];
    }

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
                bool scan = leaf.Node.Action is OrdersPackage.ScanAction;
                string? scope = scan
                    ? $"scan-zone:{((OrdersPackage.ScanAction)leaf.Node.Action!).Zone}"
                    : leaf.Node.Capacity?.Id;
                int limit = scan
                    ? 1
                    : leaf.Node.Capacity is null
                    ? 8
                    : leaf.Node.Capacity.InlineValue
                        ?? package.Capacities[leaf.Node.Capacity.Id];
                int distance = ArenaBasics.StaticDistance(
                        contract.Map, body.Position, target.Position)
                    ?? int.MaxValue;
                distance = target.WalkingDistance ?? distance;
                result.Add(new Proposal(
                    body, role, leaf, priority, target, scope, limit,
                    scan ? 2 : null, distance,
                    CombineTrace(
                        skippedFight,
                        leaf.Node.Selection == "highest-veteran"
                            ? $"selected-highest-veteran-level-{CurrentLevel(body)}"
                            : null)));
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
                        decline = move.Target == "nearest-friendly-core-carrier"
                            ? $"after-{node.Id}-no-qualifying-friendly-core-carrier"
                            : $"after-{node.Id}-no-experienced-ally";
                    }
                    return targets;
                }
            case OrdersPackage.FightAction fight:
                return ResolveEnemyTargets(
                    contract, mind, arc, package, body, leaf, fight, out decline);
            case OrdersPackage.ScanAction scan:
                return ResolveScanTargets(
                    contract, mind, package, body, leaf, scan);
            case OrdersPackage.UseAction use:
                {
                    ResolvedTarget[] targets = ResolveUseTargets(
                        contract, mind, arc, package, body, use, carried,
                        out string? useDecline);
                    if (targets.Length == 0 && useDecline is not null)
                        decline = $"after-{node.Id}-{useDecline}";
                    if (targets.Length == 0
                        && decline is null
                        && (use.TargetKind is "role" or "selector"
                            || use.Target == "veteran"))
                    {
                        decline = use.AheadVia is string route
                            ? $"after-{node.Id}-no-forward-role-{use.Target}"
                                + $"-via-{route}"
                                + ForwardRouteDeclineTrace(
                                    contract, mind, package, body, use, route)
                            : use.TargetKind == "role"
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

    private ResolvedTarget[] ResolveScanTargets(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        OrdersPackage package,
        MindBody body,
        OrderTreeRuntime.EligibleLeaf leaf,
        OrdersPackage.ScanAction scan)
    {
        Position[] zoneTiles = package.ZoneTiles(scan.Zone, contract.Map);
        OrdersEvidenceLedger.ClearanceRead clearance =
            _evidence.ReadClearance(
                zoneTiles,
                position => package.Contains(scan.Zone, position),
                mind.Tick,
                scan.FreshWithinTicks);
        if (clearance.Cleared || clearance.StaleTiles.Length == 0)
            return [];
        int[] distances = WalkingDistanceField(contract, mind, body);
        TacticalScanPlanner.ViewTarget[] targets = TacticalScanPlanner.Targets(
            contract,
            body,
            clearance.StaleTiles,
            position => ArenaBasics.DistanceFromField(
                contract.Map, distances, position),
            _scanCommitments.TryGetValue(
                body.UnitId, out ScanCommitment? held)
            && held.Actor == body.ActorId
            && held.Path == leaf.Node.Path
                ? held.TargetKey
                : null);
        return targets.Select(target => new ResolvedTarget(
                $"scan:{scan.Zone}:{target.Key}",
                target.Position,
                [target.Position],
                null,
                null,
                $"scan-{scan.Zone}-useful-{target.UsefulTileCount}",
                target.WalkingDistance,
                target.Facing))
            .ToArray();
    }

    private string ForwardRouteDeclineTrace(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        OrdersPackage package,
        MindBody body,
        OrdersPackage.UseAction use,
        string routeId)
    {
        if (use.TargetKind != "role")
            return "";
        string[] reads = mind.Bodies
            .Where(candidate => candidate.UnitId != body.UnitId
                && package.RolesByUnit[candidate.UnitId].Id == use.Target)
            .OrderBy(candidate => candidate.UnitId)
            .Select(candidate =>
            {
                int? gain = ForwardRouteGain(
                    contract,
                    package,
                    body,
                    candidate.ActorId,
                    candidate.Position,
                    routeId);
                return $"-u{candidate.UnitId}-gain-{gain?.ToString() ?? "na"}";
            })
            .ToArray();
        return reads.Length == 0 ? "-no-live-candidate" : string.Concat(reads);
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
            if (move.TargetKind is "role" or "role-highest-veteran")
            {
                IEnumerable<MindBody> roleMembers = livingAllies.Where(candidate =>
                    package.RolesByUnit[candidate.UnitId].Id == move.Target);
                candidates = move.TargetKind == "role-highest-veteran"
                    ? roleMembers.OrderByDescending(CurrentLevel)
                        .ThenBy(candidate => ArenaBasics.StaticDistance(
                            contract.Map, body.Position, candidate.Position)
                            ?? int.MaxValue)
                        .ThenBy(candidate => candidate.UnitId)
                    : roleMembers.OrderBy(candidate => ArenaBasics.StaticDistance(
                            contract.Map, body.Position, candidate.Position)
                            ?? int.MaxValue)
                        .ThenBy(candidate => candidate.UnitId);
            }
            else if (move.Target == "nearest-friendly-core-carrier")
            {
                int[] distances = WalkingDistanceField(contract, mind, body);
                HashSet<string>? roleFilter = move.Roles is { Length: > 0 }
                    ? move.Roles.ToHashSet(StringComparer.Ordinal) : null;
                string? sourceFilter = move.Well is null ? null
                    : ResolveWell(contract, arc, move.Well).WellId;
                candidates = livingAllies
                    .Where(candidate => carried.TryGetValue(
                        candidate.ActorId, out var core)
                        && (roleFilter is null || roleFilter.Contains(
                            package.RolesByUnit[candidate.UnitId].Id))
                        && (sourceFilter is null
                            || core.CoreId.SourceWellId == sourceFilter)
                        && (move.CoreBankValue is null
                            || move.CoreBankValue.Matches(CoreBankValue(
                                contract, arc, core.CoreId.SourceWellId))))
                    .OrderBy(candidate => ArenaBasics.DistanceFromField(
                            contract.Map, distances, candidate.Position)
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
                        ? move.Target == "nearest-friendly-core-carrier"
                            ? $"friendly-core-carrier-value-"
                                + CoreBankValue(
                                    contract,
                                    arc,
                                    carried[candidate.ActorId]
                                        .CoreId.SourceWellId)
                                + (move.Roles is { Length: > 0 }
                                    ? "-roles-" + string.Join(
                                        ",", move.Roles.Order(StringComparer.Ordinal))
                                    : "-roles-any")
                                + (move.Well is string scopedWell
                                    ? $"-well-{scopedWell}"
                                    : "")
                            : $"most-experienced-level-{CurrentLevel(candidate)}"
                        : move.TargetKind == "role-highest-veteran"
                            ? $"role-highest-veteran-{move.Target}-level-{CurrentLevel(candidate)}"
                            : $"role-target-{move.Target}",
                    move.Target == "nearest-friendly-core-carrier"
                        ? ArenaBasics.DistanceFromField(
                            contract.Map,
                            WalkingDistanceField(contract, mind, body),
                            candidate.Position)
                        : null))
                .Where(target => target.Goals.Length > 0)
                .ToArray();
        }
        if (move.TargetKind == "dynamic")
        {
            if (move.Target == "own-tossed-core")
            {
                if (!_pendingSoloTosses.TryGetValue(
                        body.UnitId, out PendingSoloToss? pending)
                    || pending.Actor != body.ActorId
                    || carried.ContainsKey(body.ActorId))
                {
                    return [];
                }
                int? distance = ArenaBasics.DistanceFromField(
                    contract.Map,
                    WalkingDistanceField(contract, mind, body),
                    pending.Target);
                if (distance is null)
                    return [];
                return [new ResolvedTarget(
                    $"core:{CoreKey(pending.CoreId)}",
                    pending.Target,
                    [pending.Target],
                    null,
                    CoreKey(pending.CoreId),
                    $"reclaim-own-toss-distance-{distance}",
                    distance)];
            }
            if (move.Target == "own-reactor")
            {
                // Banking consumes a Core only while the carrier STANDS on
                // the reactor tile, which makes that tile shared bank
                // infrastructure: an empty-handed body ordered home holds
                // the approach ring instead, so a parked returner can never
                // blockade its own team's deliveries.
                if (carried.ContainsKey(body.ActorId)
                    || _ownReactorApproach.Length == 0)
                {
                    return [new ResolvedTarget(
                        "reactor:own", _ownReactor, [_ownReactor],
                        null, null)];
                }
                return [new ResolvedTarget(
                    "reactor:own-approach", _ownReactor,
                    _ownReactorApproach, null, null)];
            }
            if (move.Target == "current-position")
                return [new ResolvedTarget(
                    "hold:current-position", body.Position,
                    [body.Position], null, null,
                    "signature-commitment-hold")];
            if (move.Target == "nearest-heal-zone")
            {
                // The service anchor: an authored `near` zone makes
                // "nearest" mean nearest to the PHASE'S ground rather than
                // to each body — a squad regrouping at authored ground
                // shares one heal tile instead of scattering to whichever
                // is closest to where each bot happens to stand.
                Position reference = move.Near is string nearZone
                    ? package.ZoneCenter(nearZone)
                    : body.Position;
                // Bounded recovery is a new player-ready primitive, but an
                // unqualified historical target is a frozen behavior shape.
                // Keep its former static-distance ordering byte-for-byte and
                // only pay the legal-walking-distance semantics when the
                // sheet actually authors a bound.
                if (move.WithinWalkingSteps is null)
                {
                    return _healTiles
                        .OrderBy(tile => ArenaBasics.StaticDistance(
                                contract.Map, reference, tile)
                            ?? int.MaxValue)
                        .ThenBy(tile => tile.Y)
                        .ThenBy(tile => tile.X)
                        .Select(tile => new ResolvedTarget(
                            $"heal:{tile.X}:{tile.Y}",
                            tile,
                            [tile],
                            null,
                            null))
                        .ToArray();
                }
                int[] distances = WalkingDistanceField(contract, mind, body);
                return _healTiles
                    .Select(tile => new
                    {
                        Tile = tile,
                        Distance = ArenaBasics.DistanceFromField(
                            contract.Map, distances, tile),
                    })
                    .Where(candidate => candidate.Distance is int steps
                        && (move.WithinWalkingSteps is null
                            || steps <= move.WithinWalkingSteps))
                    .OrderBy(candidate => candidate.Distance)
                    .ThenBy(candidate => candidate.Tile.Y)
                    .ThenBy(candidate => candidate.Tile.X)
                    .Select(candidate => new ResolvedTarget(
                        $"heal:{candidate.Tile.X}:{candidate.Tile.Y}",
                        candidate.Tile,
                        [candidate.Tile],
                        null,
                        null,
                        WalkingDistance: candidate.Distance))
                    .ToArray();
            }
            if (move.Target is "nearest-visible-loose-core"
                    or "highest-bank-value-visible-loose-core")
            {
                if (carried.ContainsKey(body.ActorId))
                    return [];
                VisibleCoreCandidate[] candidates = VisibleLooseCoreCandidates(
                    contract, mind, arc, body, move.CoreBankValue,
                    move.Well is null
                        ? null
                        : ResolveWell(contract, arc, move.Well).WellId,
                    move.WithinWalkingSteps);
                IOrderedEnumerable<VisibleCoreCandidate> ordered = move.Target
                    == "highest-bank-value-visible-loose-core"
                        ? candidates
                            .OrderByDescending(candidate => candidate.BankValue)
                            .ThenBy(candidate => candidate.Distance)
                        : candidates
                            .OrderBy(candidate => candidate.Distance);
                return ordered
                    .ThenBy(candidate => candidate.Key, StringComparer.Ordinal)
                    .Select(candidate => new ResolvedTarget(
                        $"core:{candidate.Key}",
                        candidate.Core.Position,
                        [candidate.Core.Position],
                        null,
                        candidate.Key,
                        $"visible-core-value-{candidate.BankValue}-distance-"
                            + candidate.Distance,
                        candidate.Distance))
                    .ToArray();
            }
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
        if (move.TargetKind == "workshop")
        {
            GenericActorContext.ArcRelayVeterancyWorkshopState[] workshops =
                ResolveWorkshops(contract, arc, move.Target);
            if (workshops.Length == 0)
                return [];
            // 'either' picks deterministically. A body already on a training
            // tile never abandons its own channel. Everyone else takes a ready
            // room first, then the dormant room that opens soonest, and only
            // then queues behind an occupied/contested room. This distributes
            // simultaneous trainees without reservations or a second scheduler.
            GenericActorContext.ArcRelayVeterancyWorkshopState chosen =
                workshops
                    .OrderByDescending(value =>
                        value.TrainingTile == body.Position)
                    .ThenByDescending(value => value.Phase
                        == GenericActorContext
                            .ArcRelayVeterancyWorkshopPhase.Ready)
                    .ThenBy(value => value.Phase == GenericActorContext
                            .ArcRelayVeterancyWorkshopPhase.Dormant
                        ? value.ReadyAtTick ?? int.MaxValue
                        : int.MaxValue)
                    .ThenBy(value => ArenaBasics.StaticDistance(
                            contract.Map, body.Position, value.TrainingTile)
                        ?? int.MaxValue)
                    .ThenBy(value => value.WorkshopId, StringComparer.Ordinal)
                    .First();
            return [new ResolvedTarget(
                $"workshop:{chosen.WorkshopId}",
                chosen.TrainingTile,
                [chosen.TrainingTile],
                null,
                null)];
        }
        return [];
    }

    /// <summary>North or south names a global room; own/enemy selects the
    /// room nearest/farthest from this team's own Well; either reads both.
    /// Empty on a ruleset without Workshops, so every workshop fact reads
    /// zero there.</summary>
    private GenericActorContext.ArcRelayVeterancyWorkshopState[]
        ResolveWorkshops(
            GenericActorResolvedMatchContract contract,
            GenericActorContext.ModeObservationState.ArcRelay arc,
            string selector)
    {
        if (selector == "either")
            return [.. arc.Workshops];
        if (selector is "north" or "south")
        {
            return [.. arc.Workshops.Where(value => string.Equals(
                value.WorkshopId, selector, StringComparison.Ordinal))];
        }
        if (arc.Workshops.Length == 0)
            return [];

        Position ownWell = ResolveWell(contract, arc, "own").Position;
        GenericActorContext.ArcRelayVeterancyWorkshopState[] ordered =
        [
            .. arc.Workshops
                .OrderBy(value => ArenaBasics.StaticDistance(
                        contract.Map, ownWell, value.TrainingTile)
                    ?? int.MaxValue)
                .ThenBy(value => value.WorkshopId, StringComparer.Ordinal),
        ];
        return selector == "own" ? [ordered[0]] : [ordered[^1]];
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
        OrdersPackage package,
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
                                     && (fight.Zone is null
                                         || value.Positions.Any(position =>
                                             package.Contains(
                                                 fight.Zone, position)))
                                     && ArenaBasics
                                         .CanAimAtPositionWithClearFriendlyLine(
                                             contract, mind, body,
                                             value.Positions[0]))
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
                candidates
                    .Where(candidate => fight.Zone is null
                        || package.Contains(fight.Zone, candidate.Position))
                    .ToArray();
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
                target.Actor),
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
        ActorIdentity? targetActor)
    {
        bool needsWindupMargin = targetActor is not null
            && NeedsWindupClosingMargin(
                contract, mind, body, targetActor, target, fight);
        // Custody defence is opportunistic, not a second destination. A
        // carrier may spend a tick firing or turning toward an immediate
        // threat, but on attack cadence (or when the threat is out of range)
        // this leaf yields to the banking move below it. Previously the broad
        // geometric CanAim check kept the Fight leaf selected while the shot
        // helper deliberately refused to rotate carriers, parking one Core in
        // place indefinitely.
        if (fight.Style == "hold"
            && ArenaBasics.CarriedCore(mind, body.ActorId) is not null)
        {
            return !needsWindupMargin
                && ArenaBasics.CommitsAttackPositionThisTick(
                    contract,
                    mind,
                    body,
                    target,
                    allowCarrierRotation: true);
        }
        bool outsidePreferredRange = fight.PreferredRange is int preferredRange
            && body.Position.ChebyshevDistance(target) > preferredRange;
        if (!needsWindupMargin && !outsidePreferredRange
            && ArenaBasics.CanAimAtPositionWithClearFriendlyLine(
                contract, mind, body, target))
            return true;
        if (targetActor is not null
            && fight.Style == "kite"
            && body.Position.ChebyshevDistance(target) <= 2)
        {
            return true;
        }
        if (fight.Style == "hold"
            && !HoldMayClose(
                contract, mind, body, targetActor, out _))
            return false;

        Position anchor = FightEpisodeAnchor(body, leaf);
        int? bodyFromAnchor = ArenaBasics.StaticDistance(
            contract.Map, body.Position, anchor);
        bool insideLeash = fight.ChaseLimit == 0
            ? body.Position == anchor
            : bodyFromAnchor is int used && used <= fight.ChaseLimit;
        if (!insideLeash)
            return false;
        Position[] firing = FiringContributionGoals(
            contract, mind, body, target, anchor,
            fight.ChaseLimit);
        firing = PreferredRangeGoals(fight, target, firing);
        if (needsWindupMargin)
        {
            firing = WindupBufferedFiringGoals(
                contract, body, target, firing);
        }
        return firing.Length > 0;
    }

    /// <summary>
    /// Hold owns a bounded defence leash rather than a universal chase. It
    /// closes only on a visible Core carrier or on a visible gun that can hit
    /// the holder from its present tile while the holder cannot answer. Every
    /// other out-of-range enemy is allowed to leave without dragging the
    /// defender away from its authored ground.
    /// </summary>
    private static bool HoldMayClose(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        ActorIdentity? targetActor,
        out string motive)
    {
        motive = "";
        if (targetActor is null)
            return false;
        GenericActorContext.ObservedEnemyState? target = mind.Enemies
            .FirstOrDefault(value => value.ActorId == targetActor);
        if (target is null)
            return false;
        if (ArenaBasics.CarriedCore(mind, target.ActorId) is not null)
        {
            motive = "carrier";
            return true;
        }
        if (ArenaBasics.CanAimFrom(
                contract,
                target.FormId,
                target.Position,
                body.Position))
        {
            motive = "counter-range";
            return true;
        }
        return false;
    }

    private static bool IsDeploymentKind(string kind) => kind is
        "prism-wall" or "trip-node" or "hardlight-block" or "sentinel-seed";

    private ResolvedTarget[] ResolveUseTargets(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        OrdersPackage package,
        MindBody body,
        OrdersPackage.UseAction use,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried,
        out string? decline)
    {
        decline = null;
        GenericActorRulesContract.ArcRelaySignature? signature =
            ArenaBasics.ArcRules(contract)?.Signatures.FirstOrDefault(value =>
                value.SignatureId == use.Skill || value.Kind == use.Skill);
        if (signature is null
            || body.Action(signature.ActionId) is not { Available: true })
        {
            decline = signature is null
                ? $"signature-{use.Skill}-unknown"
                : $"signature-{use.Skill}-unavailable";
            return [];
        }
        // A mixed-class role may state one skill order for each class it casts.
        // Applicability belongs to the body: an unrelated class falls through
        // to its next authored order instead of forcing an artificial role split.
        if (signature.ClassId != body.ClassId)
        {
            decline = $"signature-{use.Skill}-not-owned-by-{body.ClassId}";
            return [];
        }
        // Smart Rail is suppressive lane fire, not a hidden single-target
        // order. Score every currently legal eight-way ray and submit the
        // best heading without a UnitTarget so the authoritative Rail keeps
        // its full wall-bounded range and pierces every enemy on that line.
        // Explicit/manual named-target uses retain their lock semantics.
        if (use.Opportunity == "rail-visible-enemy")
        {
            return ResolveSmartRailTargets(
                contract, mind, arc, body, signature, out decline);
        }
        if (ArenaBasics.ArcRules(contract)?.SignatureGrammarVersion >= 4
            && use.Opportunity is "pull-enemy-carrier"
                or "paint-supported-enemy")
        {
            return ResolvePriorityEnemySignatureTargets(
                contract, mind, arc, body, use, signature, out decline);
        }
        if (use.TargetKind == "ray")
        {
            ResolvedTarget? ray = ResolveAuthoredRayUse(
                contract, mind, arc, package, body, use, out decline);
            return ray is null ? [] : [ray];
        }
        if (use.TargetKind == "route")
        {
            ResolvedTarget? forward = ResolveForwardRouteUse(
                contract, mind, package, body, signature, use,
                out decline);
            return forward is null ? [] : [forward];
        }
        Position? exactPlacement = use.AtAnchor is string anchor
            ? package.AnchorPosition(anchor)
            : null;
        if (use.TargetKind == "named" && use.Target == "self")
        {
            Position targetPosition = exactPlacement ?? body.Position;
            var self = new ResolvedTarget(
                use.AtAnchor is string placementAnchor
                    ? $"anchor:{placementAnchor}"
                    : $"body:{body.ActorId}",
                targetPosition,
                [targetPosition],
                body.ActorId, null);
            bool executable = CanExecuteUse(
                contract, body, self, use, exactPlacement);
            if (!executable
                && !CanApproachExactDeployment(signature, use, exactPlacement))
            {
                decline = $"signature-{use.Skill}-illegal-at-self";
                return [];
            }
            if (!SignatureOpportunityAllows(
                    contract, mind, arc, body, use, self,
                    exactPlacement, out decline))
                return [];
            return [self];
        }
        if (use.TargetKind == "named" && use.Target == "own-reactor")
        {
            if (use.Opportunity == "transfer-forward")
            {
                ResolvedTarget? forward = ResolveSoloForwardArcToss(
                    contract, mind, arc, body, use, carried, out decline);
                return forward is null ? [] : [forward];
            }
            var reactor = new ResolvedTarget(
                "reactor:own", _ownReactor, [_ownReactor], null, null);
            if (!CanExecuteUse(
                    contract, body, reactor, use, exactPlacement))
            {
                decline = $"signature-{use.Skill}-illegal-at-own-reactor";
                return [];
            }
            if (!SignatureOpportunityAllows(
                    contract, mind, arc, body, use, reactor,
                    exactPlacement, out decline))
                return [];
            return [reactor];
        }
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
                    "nearest-friendly-core-carrier-behind" => mind.Bodies
                        .Where(candidate => candidate.UnitId != body.UnitId
                            && carried.ContainsKey(candidate.ActorId)
                            && (ArenaBasics.StaticDistance(
                                    contract.Map, candidate.Position, _ownReactor)
                                ?? int.MaxValue) >= (ArenaBasics.StaticDistance(
                                    contract.Map, body.Position, _ownReactor)
                                ?? int.MaxValue) + 2)
                        .OrderBy(candidate => ArenaBasics.StaticDistance(
                                contract.Map, body.Position, candidate.Position)
                            ?? int.MaxValue)
                        .ThenBy(candidate => candidate.UnitId)
                        .Select(candidate => (candidate.ActorId,
                            candidate.Position, candidate.Health,
                            CurrentLevel(candidate),
                            (string?)"carrier-behind")),
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
        if (use.AheadVia is string aheadVia)
        {
            values = values
                .Select(value => (
                    Value: value,
                    Gain: ForwardRouteGain(
                        contract,
                        package,
                        body,
                        value.Actor,
                        value.Position,
                        aheadVia)))
                .Where(candidate => candidate.Gain is int gain
                    && gain >= use.MinimumForwardTiles)
                .Select(candidate => (
                    candidate.Value.Actor,
                    candidate.Value.Position,
                    candidate.Value.Health,
                    candidate.Value.Level,
                    (string?)($"{candidate.Value.Trace}:ahead-via-{aheadVia}"
                        + $"-gain-{candidate.Gain}")));
        }
        ResolvedTarget[] candidates = values.Select(value => new ResolvedTarget(
            $"body:{value.Actor}", value.Position, [value.Position],
            value.Actor, null, value.Trace)).ToArray();
        ResolvedTarget[] legal = candidates
            .Where(target => CanExecuteUse(
                contract, body, target, use, exactPlacement))
            .ToArray();
        if (legal.Length == 0 && use.TargetKind == "named")
        {
            decline = candidates.Length == 0
                ? $"signature-{use.Skill}-no-target-{use.Target}"
                : $"signature-{use.Skill}-no-legal-target-{use.Target}";
            return [];
        }
        var useful = new List<ResolvedTarget>();
        string? opportunityDecline = null;
        foreach (ResolvedTarget target in legal)
        {
            if (SignatureOpportunityAllows(
                    contract, mind, arc, body, use, target,
                    exactPlacement, out string? candidateDecline))
            {
                useful.Add(target);
            }
            else
            {
                opportunityDecline ??= candidateDecline;
            }
        }
        if (useful.Count == 0 && opportunityDecline is not null)
            decline = opportunityDecline;
        return useful.ToArray();
    }

    private ResolvedTarget? ResolveAuthoredRayUse(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        OrdersPackage package,
        MindBody body,
        OrdersPackage.UseAction use,
        out string? decline)
    {
        decline = null;
        Position[] ray = package.RoutePoints(use.Target);
        if (ray.Length < 2)
        {
            decline = $"signature-{use.Skill}-ray-needs-two-points";
            return null;
        }
        int? toSource = ArenaBasics.StaticDistance(
            contract.Map, body.Position, ray[0]);
        if (toSource is null or > 1)
        {
            decline = $"signature-{use.Skill}-not-at-ray-source";
            return null;
        }
        var target = new ResolvedTarget(
            $"authored-ray:{use.Target}",
            ray[^1],
            [ray[^1]],
            null,
            null,
            $"authored-ray-{use.Target}");
        if (!CanExecuteUse(contract, body, target, use))
        {
            decline = $"signature-{use.Skill}-ray-illegal";
            return null;
        }
        return SignatureOpportunityAllows(
            contract, mind, arc, body, use, target,
            exactPlacement: null, out decline)
                ? target
                : null;
    }

    private ResolvedTarget[] ResolvePriorityEnemySignatureTargets(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        OrdersPackage.UseAction use,
        GenericActorRulesContract.ArcRelaySignature signature,
        out string? decline)
    {
        decline = null;
        HashSet<ActorIdentity> carriers = arc.VisibleCores
            .Where(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Carried
                && core.CarrierActorId?.TeamId != _teamId)
            .Select(core => core.CarrierActorId!)
            .ToHashSet();
        bool paint = use.Opportunity == "paint-supported-enemy";
        IEnumerable<GenericActorContext.ObservedEnemyState> candidates =
            mind.Enemies
                .Where(enemy =>
                {
                    var candidate = new ResolvedTarget(
                        $"body:{enemy.ActorId}", enemy.Position,
                        [enemy.Position], enemy.ActorId, null,
                        $"priority-signature-{use.Skill}");
                    return CanExecuteUse(contract, body, candidate, use);
                })
                .OrderBy(enemy => carriers.Contains(enemy.ActorId)
                    ? 0
                    : paint && enemy.VeterancyLevel > 1 ? 1 : 2)
                .ThenByDescending(enemy => paint && !carriers.Contains(enemy.ActorId)
                    ? enemy.VeterancyLevel
                    : 0)
                .ThenBy(enemy => enemy.Health)
                .ThenBy(enemy => ArenaBasics.StaticDistance(
                        contract.Map, body.Position, enemy.Position)
                    ?? int.MaxValue)
                .ThenBy(enemy => enemy.ActorId);
        string? lastDecline = null;
        foreach (GenericActorContext.ObservedEnemyState enemy in candidates)
        {
            var target = new ResolvedTarget(
                $"body:{enemy.ActorId}", enemy.Position,
                [enemy.Position], enemy.ActorId, null,
                $"priority-signature-{use.Skill}");
            if (SignatureOpportunityAllows(
                    contract, mind, arc, body, use, target,
                    exactPlacement: null, out string? rejected))
                return [target];
            lastDecline = rejected;
        }
        decline = lastDecline ?? $"signature-{signature.SignatureId}-no-target";
        return [];
    }

    /// <summary>
    /// The Relay default is a solo carry primitive, not an implicit escort:
    /// choose the farthest exact legal landing that materially shortens the
    /// carrier's traversable route home. An ordinary reclaim move leaf owns
    /// the continuation after the Core leaves the body.
    /// </summary>
    private ResolvedTarget? ResolveSoloForwardArcToss(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        OrdersPackage.UseAction use,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried,
        out string? decline)
    {
        decline = null;
        if (!carried.TryGetValue(body.ActorId, out var core))
        {
            decline = "signature-arc-toss-no-carried-core";
            return null;
        }
        GenericActorRulesContract.ArcRelaySignature? signature =
            ArenaBasics.Signature(contract, "arc-toss");
        GenericActorActionLegality? action = signature is null
            ? null
            : body.Action(signature.ActionId);
        GenericActorActionLegality.ArgumentConstraint.PositionTargetConstraint?
            targets = action?.Constraints.OfType<GenericActorActionLegality
                .ArgumentConstraint.PositionTargetConstraint>()
                .SingleOrDefault();
        if (action is not { Available: true } || targets is null)
        {
            decline = "signature-arc-toss-unavailable";
            return null;
        }
        HashSet<Position> persistent = ArenaBasics.PersistentRouteObstacles(
            contract, mind, body.ActorId.TeamId);
        int[] home = ArenaBasics.StaticRouteDistanceField(
            contract, body, _ownReactor, persistent);
        int? current = ArenaBasics.DistanceFromField(
            contract.Map, home, body.Position);
        if (current is null)
        {
            decline = "signature-arc-toss-no-home-route";
            return null;
        }
        HashSet<Position> occupied = mind.Bodies
            .Where(candidate => candidate.ActorId != body.ActorId)
            .Select(candidate => candidate.Position)
            .Concat(mind.Enemies.Select(candidate => candidate.Position))
            .ToHashSet();
        HashSet<Position> coreTiles = arc.VisibleCores
            .Where(candidate => candidate.CoreId != core.CoreId)
            .Select(candidate => candidate.FlightTarget ?? candidate.Position)
            .ToHashSet();
        var option = targets.AllowedValues
            .Where(candidate => candidate != _ownReactor
                && !occupied.Contains(candidate)
                && !coreTiles.Contains(candidate)
                && ArcTossLandsExactly(
                    contract.Map, body.Position, candidate)
                && mind.Enemies.All(enemy =>
                    enemy.Position.ChebyshevDistance(candidate) > 1))
            .Select(candidate => new
            {
                Position = candidate,
                Remaining = ArenaBasics.DistanceFromField(
                    contract.Map, home, candidate),
            })
            .Where(candidate => candidate.Remaining is int remaining
                && current.Value - remaining >= 2)
            .OrderByDescending(candidate =>
                current.Value - candidate.Remaining!.Value)
            .ThenBy(candidate => candidate.Remaining)
            .ThenBy(candidate => ArenaBasics.FrameY(
                candidate.Position, _mirrored))
            .ThenBy(candidate => ArenaBasics.FrameX(
                candidate.Position, _mirrored))
            .FirstOrDefault();
        if (option is null)
        {
            decline = "signature-arc-toss-no-safe-forward-landing";
            return null;
        }
        int gain = current.Value - option.Remaining!.Value;
        var target = new ResolvedTarget(
            $"solo-forward:{option.Position.X},{option.Position.Y}",
            option.Position,
            [option.Position],
            null,
            CoreKey(core.CoreId),
            $"solo-forward-gain-{gain}");
        if (!CanExecuteUse(contract, body, target, use))
        {
            decline = "signature-arc-toss-forward-landing-illegal";
            return null;
        }
        return target;
    }

    /// <summary>
    /// Generated Tactics uses carry one catalog-derived opportunity token.
    /// The runtime interprets that small stable vocabulary instead of
    /// branching on skill ids. Manual Orders omit it and retain their exact
    /// authored semantics.
    /// </summary>
    private bool SignatureOpportunityAllows(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        OrdersPackage.UseAction use,
        ResolvedTarget target,
        Position? exactPlacement,
        out string? decline)
    {
        decline = null;
        if (use.Opportunity is null)
            return true;
        Position place = exactPlacement ?? target.Position;
        int Nearby(Position first, Position second) =>
            Math.Max(Math.Abs(first.X - second.X), Math.Abs(first.Y - second.Y));
        int CrossDistance(Position first, Position second) =>
            Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);
        bool durablePresent = OwnedDeployment(arc, body) is { } deployment
            && deployment.RemainingCapacity > 0;
        switch (use.Opportunity)
        {
            case "route-forward":
            case "transfer-forward":
            case "rail-visible-enemy":
            case "pull-enemy-carrier":
                return true;
            case "screen-threatened-lane":
                {
                    if (durablePresent)
                        return RejectSignatureOpportunity(
                            use, "useful-screen-still-active", out decline);
                    MindBody[] protectedBodies = mind.Bodies.Where(ally =>
                            ally.UnitId == body.UnitId
                            || ArenaBasics.CarriedCore(mind, ally.ActorId) is not null
                            || Nearby(ally.Position, body.Position) <= 1)
                        .ToArray();
                    return mind.Enemies.Any(enemy => protectedBodies.Any(ally =>
                            ArenaBasics.CanAimFrom(
                                contract, enemy.FormId, enemy.Position, ally.Position)))
                        || RejectSignatureOpportunity(
                            use, "no-threatening-lane", out decline);
                }
            case "repair-damaged-ally":
                {
                    MindBody? ally = target.Actor is ActorIdentity actor
                        ? mind.Bodies.FirstOrDefault(candidate =>
                            candidate.ActorId == actor)
                        : null;
                    return ally is not null && ally.Health < MaxHealth(contract, ally)
                        || RejectSignatureOpportunity(
                            use, "target-not-damaged", out decline);
                }
            case "reveal-smoke":
                return ArenaBasics.ArcRules(contract)
                            ?.SignatureGrammarVersion >= 4
                        && use.AtAnchor is not null
                    || arc.VisibleSignatures.Any(value =>
                        value.Kind.Contains("smoke", StringComparison.Ordinal)
                        && value.Positions.Any(position => Nearby(position, place) <= 2))
                    || RejectSignatureOpportunity(
                        use, "no-hostile-smoke-at-mark", out decline);
            case "strike-occupied-anchor":
                return mind.Enemies.Any(enemy => CrossDistance(enemy.Position, place) <= 1)
                    || RejectSignatureOpportunity(
                        use, "no-enemy-in-cross", out decline);
            case "deploy-unoccupied-anchor":
            case "block-unoccupied-choke":
                if (durablePresent)
                    return RejectSignatureOpportunity(
                        use, "useful-deployment-still-active", out decline);
                return !mind.Bodies.Any(value => value.Position == place)
                        && !mind.Enemies.Any(value => value.Position == place)
                        && !arc.VisibleSignatures.Any(value =>
                            value.RemainingCapacity > 0
                            && value.Positions.Contains(place))
                    || RejectSignatureOpportunity(
                        use, "deployment-anchor-occupied", out decline);
            case "seed-after-team-contact":
                // A plotted Nest mark means maintain this emplacement. The
                // exact authored ground is itself sufficient intent; contact
                // remains the usefulness gate only for an unmarked/raw use.
                if (use.AtAnchor is null && mind.Enemies.Length == 0)
                    return RejectSignatureOpportunity(
                        use, "no-team-contact", out decline);
                if (durablePresent)
                    return RejectSignatureOpportunity(
                        use, "useful-deployment-still-active", out decline);
                return use.AtAnchor is null
                        || !mind.Bodies.Any(value => value.Position == place)
                            && !mind.Enemies.Any(value => value.Position == place)
                            && !arc.VisibleSignatures.Any(value =>
                                value.RemainingCapacity > 0
                                && value.Positions.Contains(place))
                    || RejectSignatureOpportunity(
                        use, "deployment-anchor-occupied", out decline);
            case "suppress-hostile-signature":
                return arc.VisibleSignatures.Any(value =>
                        value.OwnerTeamId != _teamId && !value.Suppressed
                        && value.Positions.Any(position =>
                            Nearby(position, body.Position) <= 4))
                    || RejectSignatureOpportunity(
                        use, "no-hostile-signature-value", out decline);
            case "rescue-endangered-ally":
                {
                    MindBody? ally = target.Actor is ActorIdentity actor
                        ? mind.Bodies.FirstOrDefault(candidate =>
                            candidate.ActorId == actor)
                        : null;
                    return ally is not null
                        && (ally.Health * 2 <= MaxHealth(contract, ally)
                            || ArenaBasics.CarriedCore(mind, ally.ActorId) is not null)
                        || RejectSignatureOpportunity(
                            use, "ally-not-endangered", out decline);
                }
            case "advance-friendly-carrier":
                {
                    MindBody? carrier = target.Actor is ActorIdentity actor
                        ? mind.Bodies.FirstOrDefault(candidate =>
                            candidate.ActorId == actor)
                        : null;
                    int sourceHome = ArenaBasics.StaticDistance(
                        contract.Map, body.Position, _ownReactor) ?? int.MaxValue;
                    int carrierHome = carrier is null
                        ? int.MinValue
                        : ArenaBasics.StaticDistance(
                            contract.Map, carrier.Position, _ownReactor)
                            ?? int.MinValue;
                    bool safeLanding = !mind.Enemies.Any(enemy =>
                        ArenaBasics.CanAimFrom(
                            contract,
                            enemy.FormId,
                            enemy.Position,
                            body.Position));
                    return (carrier is not null
                        && ArenaBasics.CarriedCore(mind, carrier.ActorId) is not null
                        && carrierHome >= sourceHome + 2
                        && safeLanding)
                        || RejectSignatureOpportunity(
                            use, "no-safe-two-step-carrier-gain", out decline);
                }
            case "paint-supported-enemy":
                return target.Actor is not null
                    && (ArenaBasics.CanAimAtPositionWithClearFriendlyLine(
                            contract, mind, body, target.Position)
                        || mind.Bodies.Any(ally => ally.UnitId != body.UnitId
                            && ArenaBasics.CanAimFrom(
                                contract, ally.FormId, ally.Position,
                                target.Position)))
                    || RejectSignatureOpportunity(
                        use, "no-allied-follow-up", out decline);
            case "burst-crowd":
                return (ArenaBasics.ArcRules(contract)
                            ?.SignatureGrammarVersion >= 4
                        ? mind.Enemies.Any(enemy =>
                            Nearby(enemy.Position, body.Position) == 1)
                        : mind.Enemies.Count(enemy =>
                            Nearby(enemy.Position, body.Position) <= 3) >= 2)
                    || RejectSignatureOpportunity(
                        use, "no-adjacent-target", out decline);
            case "smoke-threatened-crossing":
                return mind.Enemies.Any(enemy =>
                        ArenaBasics.CanAimFrom(
                            contract, enemy.FormId, enemy.Position, place))
                    || RejectSignatureOpportunity(
                        use, "crossing-not-threatened", out decline);
            default:
                return RejectSignatureOpportunity(
                    use, "unknown-policy", out decline);
        }
    }

    private static bool RejectSignatureOpportunity(
        OrdersPackage.UseAction use,
        string reason,
        out string? decline)
    {
        decline = $"signature-{use.Skill}-opportunity-{reason}";
        return false;
    }

    private ResolvedTarget[] ResolveSmartRailTargets(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        GenericActorRulesContract.ArcRelaySignature signature,
        out string? decline)
    {
        decline = null;
        GenericActorActionLegality? action = body.Action(signature.ActionId);
        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = action?.Constraints.OfType<GenericActorActionLegality
                .ArgumentConstraint.ProjectileHeadingConstraint>()
                .SingleOrDefault();
        if (action is not { Available: true }
            || headings is null
            || signature.Range is not int range)
        {
            decline = "signature-rail-line-unavailable";
            return [];
        }

        var carrierValues = arc.VisibleCores
            .Where(core => core.Disposition
                    == GenericActorContext.ArcRelayCoreDisposition.Carried
                && core.CarrierActorId?.TeamId != _teamId)
            .GroupBy(core => core.CarrierActorId!)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(core => CoreBankValue(
                    contract, arc, core.CoreId.SourceWellId)));
        Dictionary<Position, GenericActorContext.ArcRelaySignatureState>
            deployments = arc.VisibleSignatures
                .Where(value => value.OwnerTeamId != _teamId
                    && value.Phase == GenericActorContext
                        .ArcRelaySignaturePhase.Active
                    && !value.Suppressed
                    && value.RemainingCapacity > 0
                    && IsDeploymentKind(value.Kind))
                .SelectMany(value => value.Positions.Select(position =>
                    (Position: position, Deployment: value)))
                .GroupBy(value => value.Position)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(value =>
                            value.Deployment.OperationId,
                            StringComparer.Ordinal)
                        .First().Deployment);
        int damage = signature.Damage ?? 0;
        HashSet<Position> alliedBodies = string.Equals(
                contract.Rules.Collisions.AlliedProjectileContact,
                "block-without-damage",
                StringComparison.Ordinal)
            ? ArenaBasics.Friendlies(mind)
            : [];
        alliedBodies.Remove(body.Position);
        var lanes = new List<(ResolvedTarget Target, int Value, int Hits,
            int CarrierValue, int Kills, ProjectileHeading Heading)>();
        foreach (ProjectileHeading heading in headings.AllowedValues)
        {
            Position[] ray = FullRailRay(
                contract.Map, body.Position, heading, range);
            if (ray.Length == 0)
                continue;
            int deploymentIndex = Array.FindIndex(
                ray, position => deployments.ContainsKey(position));
            int alliedIndex = Array.FindIndex(
                ray, alliedBodies.Contains);
            int effectiveDeploymentIndex = deploymentIndex >= 0
                && (alliedIndex < 0 || deploymentIndex < alliedIndex)
                    ? deploymentIndex
                    : -1;
            int damagingLength = ray.Length;
            if (effectiveDeploymentIndex >= 0)
                damagingLength = effectiveDeploymentIndex + 1;
            if (alliedIndex >= 0)
                damagingLength = Math.Min(damagingLength, alliedIndex);
            Position[] damaging = ray[..damagingLength];
            HashSet<Position> tiles = damaging.ToHashSet();
            GenericActorContext.ObservedEnemyState[] struck = mind.Enemies
                .Where(enemy => tiles.Contains(enemy.Position))
                .ToArray();
            int carrierValue = struck.Sum(enemy =>
                carrierValues.GetValueOrDefault(enemy.ActorId));
            int kills = damage <= 0
                ? 0
                : struck.Count(enemy => enemy.Health <= damage);
            int veterans = struck.Sum(enemy =>
                Math.Max(0, enemy.VeterancyLevel - 1));
            int constructValue = effectiveDeploymentIndex < 0
                ? 0
                : 50 + (deployments[ray[effectiveDeploymentIndex]]
                        .RemainingCapacity
                        <= damage
                    ? 30
                    : 0);
            int value = struck.Length * 100
                + carrierValue * 150
                + kills * 60
                + veterans * 10
                + constructValue;
            if (value == 0)
                continue;
            (int dx, int dy) = heading.Vector();
            Position aim = body.Position.Offset(dx, dy);
            lanes.Add((
                new ResolvedTarget(
                    $"rail-lane:{heading}",
                    aim,
                    [aim],
                    null,
                    null,
                    $"full-ray-value-{value}-hits-{struck.Length}"
                        + $"-carrier-{carrierValue}-kills-{kills}"),
                value,
                struck.Length,
                carrierValue,
                kills,
                heading));
        }
        if (lanes.Count == 0)
        {
            decline = "signature-rail-line-no-valuable-visible-lane";
            return [];
        }
        return lanes
            .OrderByDescending(value => value.Value)
            .ThenByDescending(value => value.Hits)
            .ThenByDescending(value => value.CarrierValue)
            .ThenByDescending(value => value.Kills)
            .ThenBy(value => ArenaBasics.FrameHeading(
                value.Heading, _mirrored))
            .Select(value => value.Target)
            .ToArray();
    }

    private static Position[] FullRailRay(
        GenericActorMapContract map,
        Position source,
        ProjectileHeading heading,
        int range)
    {
        var result = new List<Position>(range);
        (int dx, int dy) = heading.Vector();
        for (int step = 1; step <= range; step++)
        {
            Position tile = source.Offset(dx * step, dy * step);
            if (!ArenaBasics.CanEnter(map, tile))
                break;
            result.Add(tile);
        }
        return result.ToArray();
    }

    private ResolvedTarget? ResolveForwardRouteUse(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        OrdersPackage package,
        MindBody body,
        GenericActorRulesContract.ArcRelaySignature signature,
        OrdersPackage.UseAction use,
        out string? decline)
    {
        decline = null;
        Position[] points = package.RoutePoints(use.Target);
        if (points.Length < 2 || points[0] == points[^1])
        {
            decline = $"route-{use.Target}-not-terminal";
            return null;
        }
        TacticalRouteGovernor.ForwardCourseRead read = _routes.ReadForwardCourse(
            body.ActorId, use.Target, body.Position);
        if (read.Tiles.Length < 2)
        {
            decline = $"route-{use.Target}-{read.Trace}";
            return null;
        }
        GenericActorActionLegality? action = body.Action(signature.ActionId);
        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = action?.Constraints.OfType<GenericActorActionLegality
                .ArgumentConstraint.ProjectileHeadingConstraint>()
                .SingleOrDefault();
        if (action is not { Available: true } || headings is null)
        {
            decline = $"route-{use.Target}-signature-heading-unavailable";
            return null;
        }
        int maximum = Math.Max(1, signature.MaxTiles ?? signature.Range ?? 1);
        bool finalWaypoint = read.Waypoint == points.Length - 1;
        HashSet<Position> occupied = mind.Bodies
            .Where(candidate => candidate.ActorId != body.ActorId)
            .Select(candidate => candidate.Position)
            .Concat(mind.Enemies.Select(candidate => candidate.Position))
            .ToHashSet();
        TacticalRouteForwardPlanner.Resolution resolution =
            TacticalRouteForwardPlanner.Resolve(
                read.Tiles,
                maximum,
                use.MinimumForwardTiles,
                finalWaypoint,
                headings.AllowedValues.ToHashSet(),
                occupied.Contains);
        if (!resolution.Accepted)
        {
            decline = $"route-{use.Target}-{resolution.Trace}";
            return null;
        }
        Position destination = resolution.Destination!.Value;
        var target = new ResolvedTarget(
            $"route-forward:{use.Target}:{destination.X},{destination.Y}",
            destination,
            [destination],
            null,
            null,
            $"route-{use.Target}-{resolution.Trace}");
        if (!CanExecuteUse(contract, body, target, use))
        {
            decline = $"route-{use.Target}-signature-legality-declined";
            return null;
        }
        return target;
    }

    /// <summary>
    /// Measures a prospective Arc Toss against the same ordered route both
    /// Relays are traversing. This deliberately uses authored route progress:
    /// geometric or direct-to-reactor distance can call a receiver "ahead"
    /// while it is actually behind on a long authored corridor. A body that
    /// owns a route cursor uses it. An empty runner may wait on a rendezvous
    /// immediately beside the course, so it does not block the carrier. That
    /// post projects to the nearest route waypoint; a distant or genuinely
    /// ambiguous projection is intentionally refused.
    /// </summary>
    private int? ForwardRouteGain(
        GenericActorResolvedMatchContract contract,
        OrdersPackage package,
        MindBody source,
        ActorIdentity target,
        Position targetPosition,
        string routeId)
    {
        Position[] points = package.RoutePoints(routeId);
        int? sourceRemaining = RemainingRouteDistance(
            contract, source.Position, source.UnitId, routeId, points);
        int? targetRemaining = RemainingRouteDistance(
            contract, targetPosition, target.UnitId, routeId, points);
        return sourceRemaining is int from && targetRemaining is int to
            ? from - to
            : null;
    }

    private int? RemainingRouteDistance(
        GenericActorResolvedMatchContract contract,
        Position position,
        int unitId,
        string routeId,
        IReadOnlyList<Position> points)
    {
        if (points.Count > 0 && position == points[^1])
            return 0;

        int nextIndex;
        int initialDistance;
        int[] exactIndexes = points
            .Select((point, index) => (point, index))
            .Where(value => value.point == position)
            .Select(value => value.index)
            .ToArray();
        var projections = points.Select((point, index) => new
        {
            Index = index,
            Distance = ArenaBasics.StaticDistance(
                    contract.Map, position, point),
        })
            .Where(value => value.Distance is >= 0 and <= 1)
            .ToArray();
        if (exactIndexes.Length == 1)
        {
            // Arrival hysteresis may advance a route cursor while the body is
            // still standing on the preceding authored waypoint. For Toss
            // progress the body's exact tile is authoritative; otherwise a
            // carrier can reach its authored source and be measured as if it
            // had already passed the receiver.
            nextIndex = exactIndexes[0];
            initialDistance = 0;
        }
        else if (exactIndexes.Length > 1)
        {
            return null;
        }
        else if (projections.Length > 0)
        {
            int nearestDistance = projections.Min(value => value.Distance)!.Value;
            int[] nearest = projections
                .Where(value => value.Distance == nearestDistance)
                .Select(value => value.Index)
                .ToArray();
            // An exact repeated waypoint or two distant parts of a route
            // touching the same post do not define one predictable progress
            // point. Consecutive corner waypoints are one local course; the
            // later one is the conservative forward projection.
            if (nearest.Length > 1
                && (nearestDistance == 0 || nearest.Max() - nearest.Min() > 1))
            {
                return null;
            }
            nextIndex = nearest.Max();
            // This is route progress, not physical travel remaining. A
            // receiver waiting in a one-tile passing pocket projects onto the
            // adjacent course tile; charging that lateral step here would say
            // an immediately-ahead receiver made zero forward progress and
            // force it back onto the one-tile route where it blocks the
            // carrier. Reachability is already proven by the <= 1 projection
            // above, so only distance along the authored route belongs in the
            // aheadVia comparison.
            initialDistance = RouteProjectionOffset(nearestDistance);
        }
        else if (_routeCursors.TryGetValue(unitId, out RouteCursor? cursor)
            && cursor.Initialized
            && cursor.Lap == 0
            && string.Equals(cursor.RouteId, routeId, StringComparison.Ordinal)
            && cursor.Index >= 0
            && cursor.Index < points.Count)
        {
            int? toCurrent = ArenaBasics.StaticDistance(
                contract.Map, position, points[cursor.Index]);
            if (toCurrent is null)
                return null;
            nextIndex = cursor.Index;
            initialDistance = toCurrent.Value;
        }
        else
        {
            return null;
        }

        int remaining = initialDistance;
        for (int index = nextIndex; index + 1 < points.Count; index++)
        {
            int? leg = ArenaBasics.StaticDistance(
                contract.Map, points[index], points[index + 1]);
            if (leg is null)
                return null;
            remaining = checked(remaining + leg.Value);
        }
        return remaining;
    }

    internal static int RouteProjectionOffset(int distanceToCourse)
    {
        if (distanceToCourse is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(distanceToCourse));
        return 0;
    }

    private static string? OptionalTrace(string value) => value;

    private static string? CombineTrace(string? first, string? second) =>
        first is null ? second : second is null ? first : $"{first}:{second}";

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
            // Both receiver passes and solo forward Tosses name an exact
            // landing. Never let the generic nearest-position helper silently
            // clip or substitute a different legal tile.
            if (signature.Kind == "arc-toss"
                && (!action.Constraints.OfType<GenericActorActionLegality
                        .ArgumentConstraint.PositionTargetConstraint>()
                    .Single().AllowedValues.Contains(target.Position)
                    || !ArcTossLandsExactly(
                        contract.Map, body.Position, target.Position)))
            {
                return false;
            }
            return ArenaBasics.CanUsePositionSignature(
                contract, body, signature.Kind);
        }
        return ArenaBasics.CanUseParameterlessSignature(
            contract, body, signature.Kind);
    }

    private static bool CanApproachExactDeployment(
        GenericActorRulesContract.ArcRelaySignature signature,
        OrdersPackage.UseAction use,
        Position? exactPlacement) => exactPlacement is not null
        && use.AtAnchor is not null
        && IsDeploymentKind(signature.Kind);

    private static bool ArcTossLandsExactly(
        GenericActorMapContract map,
        Position source,
        Position requested)
    {
        int deltaX = requested.X - source.X;
        int deltaY = requested.Y - source.Y;
        if (deltaX != 0 && deltaY != 0
            && Math.Abs(deltaX) != Math.Abs(deltaY))
        {
            return false;
        }
        int dx = Math.Sign(deltaX);
        int dy = Math.Sign(deltaY);
        int distance = Math.Max(Math.Abs(deltaX), Math.Abs(deltaY));
        Position current = source;
        for (int step = 0; step < distance; step++)
        {
            current = current.Offset(dx, dy);
            if (!ArenaBasics.CanEnter(map, current))
                return false;
        }
        return current == requested;
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
        if (handoff.Roles is { Length: > 0 })
            candidates = candidates.Where(candidate => HandoffAllowsRole(
                handoff, package.RolesByUnit[candidate.UnitId].Id));
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

    internal static bool HandoffAllowsRole(
        OrdersPackage.HandoffAction handoff,
        string roleId) => handoff.Roles is not { Length: > 0 }
        || handoff.Roles.Contains(roleId, StringComparer.Ordinal);

    private Dictionary<int, Proposal> Allocate(
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
                            proposal.Leaf.Node.Action is OrdersPackage.ScanAction
                                ? $"view:{proposal.Target.Position.X},"
                                    + proposal.Target.Position.Y
                                : proposal.Target.Key,
                            proposal.CapacityLimit,
                            proposal.Distance,
                            proposal.Leaf.Node.Selection,
                            CurrentLevel(proposal.Body),
                            proposal.CapacityScopeLimit))
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
                bool needsWindupMargin = proposal.Target.Actor
                        is ActorIdentity targetActor
                    && NeedsWindupClosingMargin(
                        contract,
                        mind,
                        proposal.Body,
                        targetActor,
                        proposal.Target.Position,
                        fight);
                if (!targetExists
                    || !needsWindupMargin
                        && ArenaBasics.CanAimAtPositionWithClearFriendlyLine(
                            contract,
                            mind,
                            proposal.Body,
                            proposal.Target.Position))
                {
                    continue;
                }
                bool holdClose = fight.Style == "hold"
                    && HoldMayClose(
                        contract,
                        mind,
                        proposal.Body,
                        proposal.Target.Actor,
                        out _);
                Position anchor = FightEpisodeAnchor(
                    proposal.Body, proposal.Leaf);
                bool inLeash = fight.ChaseLimit == 0
                    ? proposal.Body.Position == anchor
                    : ArenaBasics.StaticDistance(
                        contract.Map, proposal.Body.Position, anchor)
                        <= fight.ChaseLimit;
                if (inLeash && (fight.Style == "advance" || holdClose))
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
            if (proposal.Leaf.Node.Action is OrdersPackage.ScanAction)
            {
                result.Add(unitId, new MovePlan(
                    proposal.Leaf.Node.Path,
                    proposal.Target.Position,
                    [proposal.Target.Position],
                    null,
                    Arrived: proposal.Body.Position
                        == proposal.Target.Position));
                continue;
            }
            if (proposal.Leaf.Node.Action is OrdersPackage.UseAction use
                && DeploymentApproachGoals(
                    contract, package, proposal.Body, proposal.Target, use)
                    is { Length: > 0 } approach)
            {
                Position nearest = Nearest(
                    contract.Map, proposal.Body.Position, approach);
                result.Add(unitId, new MovePlan(
                    proposal.Leaf.Node.Path,
                    nearest,
                    approach,
                    null,
                    Arrived: approach.Contains(proposal.Body.Position)));
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
            bool standing = goals.Contains(body.Position);
            // Course toward a goal tile nobody holds: aiming at the nearest
            // goal regardless of occupancy parks the body against a resting
            // squadmate's tile and orbits it there, even while other goal
            // tiles sit free around the ring.
            Position[] open = goals.Where(value =>
                    value == body.Position
                    || (!mind.Bodies.Any(other =>
                            other.UnitId != body.UnitId
                            && other.Position == value)
                        && !mind.Enemies.Any(enemy =>
                            enemy.Position == value)))
                .ToArray();
            Position goal = Nearest(
                contract.Map, body.Position, open.Length > 0 ? open : goals);
            // And a fully held goal set settles its overflow: standing
            // beside it beats circling for a tile that cannot free up.
            // Single goals (posts, anchors) keep their exact-tile contract.
            bool crowded = !standing
                && goals.Length > 1
                && open.Length == 0
                && goals.Any(value =>
                    Math.Abs(value.X - body.Position.X) <= 1
                    && Math.Abs(value.Y - body.Position.Y) <= 1);
            // A wedged march rests too: eight straight fully-blocked presses
            // on one order mean no queue is going to clear the way (the
            // blockers are often outside this body's vision cone, so the
            // open-tile read cannot know). Rest twelve ticks, probe four —
            // a successful probe step clears the counter and the march
            // resumes; carriers never wedge (they press home regardless),
            // and a body standing on its own team's bank tile never rests
            // there — it seals every allied bank, so it keeps pressing.
            bool wedged = false;
            if (!standing
                && body.Position != _ownReactor
                && _wedgedPresses.TryGetValue(
                    unitId, out (string Path, int Ticks) press)
                && press.Path == proposal.Leaf.Node.Path
                && press.Ticks >= 8)
            {
                if (press.Ticks % 16 < 12)
                {
                    wedged = true;
                    _wedgedPresses[unitId] = (press.Path, press.Ticks + 1);
                }
            }
            result.Add(unitId, new MovePlan(
                proposal.Leaf.Node.Path, goal, goals, null,
                standing || crowded || wedged));
        }
        return result;
    }

    private static Position[] DeploymentApproachGoals(
        GenericActorResolvedMatchContract contract,
        OrdersPackage package,
        MindBody body,
        ResolvedTarget target,
        OrdersPackage.UseAction use)
    {
        GenericActorRulesContract.ArcRelaySignature? signature =
            ArenaBasics.ArcRules(contract)?.Signatures.FirstOrDefault(value =>
                value.SignatureId == use.Skill || value.Kind == use.Skill);
        Position? placement = use.AtAnchor is string anchor
            ? package.AnchorPosition(anchor)
            : null;
        if (signature is null
            || !CanApproachExactDeployment(signature, use, placement)
            || CanExecuteUse(contract, body, target, use, placement))
        {
            return [];
        }
        return AdjacentStandTiles(contract.Map, placement!.Value);
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
            ?? RouteCursor.For(body.ActorId, stateId, routeId);
        if (state.Actor != body.ActorId
            || state.StateId != stateId
            || !string.Equals(state.RouteId, routeId, StringComparison.Ordinal))
        {
            state = RouteCursor.For(body.ActorId, stateId, routeId);
        }
        else if (state.Initialized && state.Lap == 0)
        {
            // A public signature or other authoritative relocation may place
            // the body farther down its same terminal route. Only an exact,
            // unique later waypoint is strong enough evidence to advance the
            // durable cursor; ambiguous/folded projections retain it.
            int[] exactForward = points
                .Select((point, index) => (point, index))
                .Where(value => value.index >= state.Index
                    && value.point == body.Position)
                .Select(value => value.index)
                .ToArray();
            if (exactForward.Length == 1 && exactForward[0] > state.Index)
            {
                state.Index = exactForward[0];
                state.BestDistance = int.MaxValue;
            }
        }
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

    private static string? EligibilityTrace(
        IReadOnlyList<OrderTreeRuntime.IneligibleRoot> roots)
    {
        if (roots.Count == 0) return null;
        return string.Join(
            "|",
            roots.Select(root => $"{root.RootId}~{root.Reason}"));
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
        string reason = $"orders:{ProgramPrefix(body.UnitId)}"
            + proposal.Leaf.Node.Path;
        if (proposal.EligibilityTrace is string eligibilityTrace)
            reason += $":eligibility[{eligibilityTrace}]";
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
            case OrdersPackage.ScanAction scan:
                ExecuteScan(
                    contract, mind, body, proposal, scan, movePlan, claims,
                    reason);
                return;
            case OrdersPackage.UseAction use:
                if (ExecuteUse(
                        contract, package, body, proposal.Target, use, reason))
                {
                    RememberOneShot(body, proposal, use.Skill);
                    if (use.Skill == "arc-toss"
                        && use.Opportunity == "transfer-forward"
                        && proposal.Target.Actor is null
                        && carried.TryGetValue(body.ActorId, out var core))
                    {
                        _pendingSoloTosses[body.UnitId] = new PendingSoloToss(
                            body.ActorId,
                            core.CoreId,
                            proposal.Target.Position,
                            mind.Tick + 40);
                    }
                }
                else if (movePlan is not null
                    && !movePlan.Arrived
                    && ArenaBasics.TryMoveToward(
                        contract,
                        mind,
                        body,
                        movePlan.Goals,
                        claims,
                        $"{reason}:approach-deployment:{use.Skill}"
                            + $":{_stepper.Trace(body.UnitId)}"))
                {
                    return;
                }
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

    private void ExecuteScan(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Proposal proposal,
        OrdersPackage.ScanAction scan,
        MovePlan? plan,
        ArenaBasics.Claims claims,
        string reason)
    {
        string scanReason = $"{reason}:scan:{scan.Zone}"
            + $":target-{proposal.Target.Key}";
        if (body.Position == proposal.Target.Position)
        {
            if (proposal.Target.Facing is Direction facing
                && ArenaBasics.TryRotate(
                    contract, body, facing,
                    $"{scanReason}:rotate-to-refresh"))
            {
                return;
            }
            body.Hold($"{scanReason}:observe-facing-{body.Facing}");
            return;
        }
        if (plan is not null
            && ArenaBasics.TryMoveToward(
                contract, mind, body, plan.Goals, claims,
                $"{scanReason}:move-to-view:{_stepper.Trace(body.UnitId)}"))
        {
            return;
        }
        body.Hold($"{scanReason}:view-path-blocked-wait");
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
        OrdersPackage.MoveAction authoredMove =
            (OrdersPackage.MoveAction)proposal.Leaf.Node.Action!;
        if (authoredMove.Kind == "to"
            && authoredMove.TargetKind == "dynamic"
            && authoredMove.Target == "current-position")
        {
            body.Hold($"{reason}:signature-commitment-hold-active");
            return;
        }
        if (plan.Arrived || plan.Goals.Contains(body.Position))
        {
            if (TryRestingYield(contract, mind, body, claims, movement))
                return;
            if (authoredMove.FaceDirection is string authoredDirection)
            {
                Direction desired = authoredDirection switch
                {
                    "n" => _mirrored ? Direction.South : Direction.North,
                    "e" => _mirrored ? Direction.West : Direction.East,
                    "s" => _mirrored ? Direction.North : Direction.South,
                    _ => _mirrored ? Direction.East : Direction.West,
                };
                string facing = $"{movement}:at-target:face-direction:"
                    + authoredDirection;
                if (desired != body.Facing
                    && ArenaBasics.TryRotate(contract, body, desired, facing))
                {
                    return;
                }
                body.Hold($"{movement}:at-target:facing-direction:"
                    + authoredDirection);
                return;
            }
            if (authoredMove.FaceAnchor is string anchorId)
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
                {
                    _wedgedPresses.Remove(body.UnitId);
                    return;
                }
            }
            if (ArenaBasics.TryMoveDirect(
                    contract, mind, body, step, claims,
                    $"{movement}:{plan.Course.Trace}",
                    carrying
                        ? TacticalMovementAuthority.Safety
                        : TacticalMovementAuthority.Tactical))
            {
                _wedgedPresses.Remove(body.UnitId);
                return;
            }
            PressWedge(body, proposal, carrying);
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
        if (moved)
        {
            _wedgedPresses.Remove(body.UnitId);
            return;
        }
        PressWedge(body, proposal, carrying);
        body.Hold($"{movement}:move-blocked:{proposal.Target.Key}");
    }

    private void PressWedge(MindBody body, Proposal proposal, bool carrying)
    {
        if (carrying)
        {
            _wedgedPresses.Remove(body.UnitId);
            return;
        }
        string path = proposal.Leaf.Node.Path;
        _wedgedPresses[body.UnitId] =
            _wedgedPresses.TryGetValue(
                body.UnitId, out (string Path, int Ticks) press)
                && press.Path == path
                ? (path, press.Ticks + 1)
                : (path, 1);
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
        bool needsWindupMargin = target is not null
            && NeedsWindupClosingMargin(
                contract,
                mind,
                body,
                target.ActorId,
                targetPosition,
                fight);
        bool outsidePreferredRange = fight.PreferredRange is int preferredRange
            && body.Position.ChebyshevDistance(targetPosition) > preferredRange;
        if (!needsWindupMargin && !outsidePreferredRange
            && ArenaBasics.TryShootAtPosition(
                contract,
                mind,
                body,
                targetPosition,
                $"{reason}:fight:{targetKey}",
                target?.ActorId,
                allowCarrierRotation:
                    ArenaBasics.CarriedCore(mind, body.ActorId) is not null))
            return;
        if (!needsWindupMargin && !outsidePreferredRange
            && ArenaBasics.CanAimAtPositionWithClearFriendlyLine(
                contract, mind, body, targetPosition))
        {
            // A Hold fighter owns its authored ground while the Fight leaf
            // remains selected. Cadence never hands its facing or tile back
            // to resting/Scan micro; ordinary floors resume next decision
            // after the fight actually ends.
            if (fight.Style != "hold"
                && TryRestingYield(contract, mind, body, claims, reason))
            {
                return;
            }
            body.Hold($"{reason}:cadence:{targetKey}");
            return;
        }
        string holdMotive = "";
        bool holdClose = fight.Style == "hold"
            && HoldMayClose(
                contract, mind, body, target?.ActorId, out holdMotive);
        if ((fight.Style != "hold" || holdClose) && inLeash)
        {
            Position[] firing = FiringContributionGoals(
                contract, mind, body, targetPosition, anchor,
                fight.ChaseLimit);
            firing = PreferredRangeGoals(fight, targetPosition, firing);
            if (needsWindupMargin)
            {
                firing = WindupBufferedFiringGoals(
                    contract, body, targetPosition, firing);
            }
            if (firing.Length > 0
                && ArenaBasics.TryMoveToward(
                    contract, mind, body, firing, claims,
                    holdClose
                        ? $"{reason}:hold-{holdMotive}-close:{targetKey}:"
                            + physical
                        : $"{reason}:close:{targetKey}:{physical}",
                    preserveImmediateEngagement: true))
            {
                return;
            }
        }
        if (fight.Style == "hold")
        {
            body.Hold($"{reason}:hold-track:{targetKey}:{physical}");
            return;
        }
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
                : signature.Kind == "arc-toss"
                    ? candidate => candidate == target.Position
                    : null;
            return ArenaBasics.TryPositionSignature(
                contract, body, signature.Kind, target.Position, trace,
                exactReceiver);
        }
        return ArenaBasics.TryParameterlessSignature(
            contract, body, signature.Kind, trace);
    }

    /// <summary>
    /// Body null means a squad-level program evaluation: validation bans
    /// body-scoped facts in program cases, and the guard below turns any
    /// slip into a loud fault instead of a wrong answer.
    /// </summary>
    private OrderTreeRuntime.ConditionEvaluation Evaluate(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        OrdersPackage package,
        MindBody? body,
        OrdersPackage.Role? role,
        OrdersPackage.ConditionExpression expression,
        int orderTicks,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        if (expression.Names.Length == 1 && expression.Names[0] == "always"
            && (expression.Groups?.Length ?? 0) == 0)
        {
            return OrderTreeRuntime.ConditionEvaluation.True;
        }
        var results = expression.Names.Select(name =>
        {
            if (name == "always")
                return (Name: name, Evaluation: OrderTreeRuntime.ConditionEvaluation.True);
            OrdersPackage.Condition condition = package.Conditions[name];
            int actual;
            string? trace = null;
            if (condition.Fact == "zone-cleared")
            {
                string[] zones = condition.ZoneSet;
                OrdersEvidenceLedger.ClearanceRead read =
                    _evidence.ReadClearance(
                        package.ZoneTilesUnion(zones, contract.Map),
                        position => package.ContainsAny(zones, position),
                        mind.Tick,
                        freshWithinTicks: 20);
                actual = read.Cleared ? 1 : 0;
                trace = $"{condition.Id}={read.Trace}";
            }
            else if (condition.Fact == "friendly-fights-in-zone")
            {
                actual = ActiveFightsInZone(
                    package, condition.ZoneSet, arc);
                trace = $"{condition.Id}={actual}-local-fights-prev-decision";
            }
            else if (condition.Fact == "formation-late-count")
            {
                actual = _evidence.FormationLateCount(
                    package, mind, condition.Roles ?? [], condition.ZoneSet,
                    orderTicks);
                trace = $"{condition.Id}={actual}-late-phase-entry-lives";
            }
            else if (condition.WindowTicks is not null || condition.Since is not null)
            {
                OrdersPackage.Condition evidenceCondition = condition;
                if (condition.Fact == "friendly-workshop-completions"
                    && condition.Workshop is "own" or "enemy")
                {
                    GenericActorContext.ArcRelayVeterancyWorkshopState?
                        workshop = ResolveWorkshops(
                            contract, arc, condition.Workshop).SingleOrDefault();
                    if (workshop is not null)
                    {
                        evidenceCondition = condition with
                        {
                            Workshop = workshop.WorkshopId,
                        };
                    }
                }
                OrdersEvidenceLedger.EvidenceRead read = _evidence.Read(
                    package, contract.Map, evidenceCondition,
                    mind.Tick, orderTicks,
                    core => CoreBankValue(contract, arc, core.SourceWellId),
                    core => condition.Well is null || string.Equals(
                        ResolveWell(contract, arc, condition.Well).WellId,
                        core.SourceWellId, StringComparison.Ordinal));
                actual = read.Actual;
                trace = read.Trace;
            }
            else
            {
                if (body is null && BodyScopedFact(condition.Fact))
                {
                    throw new InvalidOperationException(
                        $"Body-scoped fact '{condition.Fact}' has no body "
                        + "in a squad-level program evaluation.");
                }
                actual = Fact(
                    contract, mind, arc, package, body!, role!, condition,
                    orderTicks, carried);
            }
            bool matches = Compare(actual, condition.Operator, condition.Value);
            return (Name: name, Evaluation: new OrderTreeRuntime.ConditionEvaluation(
                matches,
                trace,
                $"{name}-{actual}-{condition.Operator}-{condition.Value}"));
        }).ToList();
        // Nested groups (one level by grammar) evaluate as their own
        // expression and join the combination like any other entry.
        foreach (OrdersPackage.ConditionExpression group in expression.Groups ?? [])
        {
            results.Add((Name: group.Kind, Evaluation: Evaluate(
                contract, mind, arc, package, body, role, group, orderTicks,
                carried)));
        }
        bool value = expression.Kind == "any"
            ? results.Any(result => result.Evaluation.Value)
            : results.All(result => result.Evaluation.Value);
        string? evidence = string.Join(
            "+",
            results.Where(result => result.Evaluation.Trace is not null)
                .Select(result => result.Evaluation.Trace));
        string? explanation = string.Join(
            "+",
            results.Where(result => !result.Evaluation.Value)
                .Select(result => result.Evaluation.Explanation ?? $"{result.Name}-false"));
        return new OrderTreeRuntime.ConditionEvaluation(
            value,
            string.IsNullOrEmpty(evidence) ? null : evidence,
            string.IsNullOrEmpty(explanation)
                ? value ? "condition-true" : "condition-false"
                : explanation);
    }

    private int ActiveFightsInZone(
        OrdersPackage package,
        IReadOnlyList<string> zones,
        GenericActorContext.ModeObservationState.ArcRelay arc)
    {
        return TacticalZoneFightState.Count(
            _teamId,
            position => package.ContainsAny(zones, position),
            _activeFights,
            arc.PendingStrikes);
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
        if (fact == "self-carrying-core-from-well")
        {
            if (!carried.TryGetValue(body.ActorId, out var carriedCore))
                return 0;
            return string.Equals(
                carriedCore.CoreId.SourceWellId,
                ResolveWell(contract, arc, condition.Well!).WellId,
                StringComparison.Ordinal) ? 1 : 0;
        }
        if (fact == "self-stuck-ticks")
            return _stationaryTicks.GetValueOrDefault(body.UnitId);
        if (fact is "self-order-ticks" or "elapsed-ticks")
            return orderTicks;
        if (fact == "self-ticks-since-signature-use")
        {
            return _lastSignatureUseTick.TryGetValue(
                    body.ActorId, out int usedAt)
                ? Math.Min(1201, Math.Max(0, mind.Tick - usedAt))
                : 1201;
        }
        if (fact == "self-in-zone")
            return package.Contains(condition.Zone!, body.Position) ? 1 : 0;
        if (fact == "self-nearest-visible-enemy-distance")
        {
            // Distance means work needed to JOIN the fight: the shortest
            // legal movement path from this body to a firing tile. Durable
            // known blockers count; allied and hostile bodies are traffic.
            // No reachable firing tile must fail every authored upper bound.
            return NearestFiringApproachDistance(
                contract, mind, package, body, condition.Zone) ?? 32;
        }
        if (fact == "self-nearest-visible-loose-core-distance")
        {
            VisibleCoreCandidate[] candidates = VisibleLooseCoreCandidates(
                contract,
                mind,
                arc,
                body,
                condition.CoreBankValue,
                condition.Well is null
                    ? null
                    : ResolveWell(contract, arc, condition.Well).WellId,
                maximumWalkingSteps: null);
            return candidates.Length == 0
                ? 32
                : candidates.Min(candidate => candidate.Distance);
        }
        if (fact == "self-nearest-heal-zone-distance")
        {
            int[] distances = WalkingDistanceField(contract, mind, body);
            int[] reachable = _healTiles
                .Select(tile => ArenaBasics.DistanceFromField(
                    contract.Map, distances, tile))
                .Where(distance => distance is not null)
                .Select(distance => distance!.Value)
                .ToArray();
            return reachable.Length == 0 ? 32 : reachable.Min();
        }
        if (fact == "signature-ready")
        {
            GenericActorRulesContract.ArcRelaySignature? signature =
                ArenaBasics.ArcRules(contract)?.Signatures.FirstOrDefault(value =>
                    value.ClassId == body.ClassId);
            return signature is not null
                && body.Action(signature.ActionId) is { Available: true } ? 1 : 0;
        }
        if (fact == "signature-commitment-active")
        {
            return arc.VisibleSignatures.Any(signature =>
                    signature.OwnerActorId == body.ActorId
                    && signature.Phase is GenericActorContext
                            .ArcRelaySignaturePhase.Tell
                        or GenericActorContext.ArcRelaySignaturePhase.Channel)
                ? 1
                : 0;
        }
        if (fact == "friendly-veterans-in-roles")
        {
            HashSet<string> selected = (condition.Roles ?? [])
                .ToHashSet(StringComparer.Ordinal);
            int minimumLevel = condition.Level ?? 2;
            return mind.Bodies.Count(candidate =>
                package.RolesByUnit.TryGetValue(candidate.UnitId, out var role)
                && selected.Contains(role.Id)
                && CurrentLevel(candidate) >= minimumLevel);
        }
        if (fact is "deployment-active" or "deployment-lifetime-percent"
                or "deployment-durability-percent"
                or "self-deployment-in-zone")
        {
            GenericActorContext.ArcRelaySignatureState? deployment =
                OwnedDeployment(arc, body);
            if (deployment is null)
                return 0;
            if (fact == "self-deployment-in-zone")
            {
                return deployment.Positions.Any(position =>
                    package.Contains(condition.Zone!, position)) ? 1 : 0;
            }
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
            // role-live-count may name a roles list instead of one role; the
            // members are the union of both spellings.
            MindBody[] members = mind.Bodies.Where(candidate =>
                {
                    string id = package.RolesByUnit[candidate.UnitId].Id;
                    return id == condition.Role
                        || (condition.Roles?.Contains(id) ?? false);
                })
                .ToArray();
            if (fact == "role-live-count")
                return members.Length;
            if (fact == "role-highest-level")
                return members.Length == 0 ? 0 : members.Max(CurrentLevel);
            if (fact == "role-lowest-health-percent")
                return members.Length == 0 ? 100 : members.Min(candidate =>
                    candidate.Health * 100
                    / Math.Max(1, MaxHealth(contract, candidate)));
            if (fact == "role-deployments-in-zone-count")
            {
                HashSet<int> memberSlots = package.RolesByUnit
                    .Where(pair => pair.Value.Id == condition.Role)
                    .Select(pair => pair.Key)
                    .ToHashSet();
                return arc.VisibleSignatures.Count(deployment =>
                    deployment.OwnerTeamId == _teamId
                    && memberSlots.Contains(deployment.OwnerActorId.UnitId)
                    && deployment.Phase == GenericActorContext
                        .ArcRelaySignaturePhase.Active
                    && string.Equals(
                        deployment.SignatureId,
                        condition.Signature,
                        StringComparison.Ordinal)
                    && deployment.Positions.Any(position =>
                        package.Contains(condition.Zone!, position)));
            }
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
        if (fact == "friendly-carriers-in-roles")
        {
            HashSet<string> roles = (condition.Roles ?? [])
                .ToHashSet(StringComparer.Ordinal);
            string? source = condition.Well is null ? null
                : ResolveWell(contract, arc, condition.Well).WellId;
            return carried.Count(pair => pair.Key.TeamId == _teamId
                && roles.Contains(package.RolesByUnit[pair.Key.UnitId].Id)
                && (source is null || pair.Value.CoreId.SourceWellId == source)
                && (condition.CoreBankValue is null
                    || condition.CoreBankValue.Matches(CoreBankValue(
                        contract, arc, pair.Value.CoreId.SourceWellId))));
        }
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
                package.ContainsAny(condition.ZoneSet, candidate.Position));
        if (fact == "visible-enemies-in-zone")
            return mind.Enemies.Count(enemy =>
                package.ContainsAny(condition.ZoneSet, enemy.Position));
        if (fact == "remembered-enemies-in-zone")
            return _rememberedEnemies.Values.Count(enemy =>
                mind.Tick - enemy.Tick <= condition.FreshnessTicks
                && package.ContainsAny(condition.ZoneSet, enemy.Position));
        if (fact == "known-loose-cores-in-zone")
            return _knownLooseCores.Values.Count(core =>
                package.ContainsAny(condition.ZoneSet, core.Position));
        if (fact.StartsWith("workshop-", StringComparison.Ordinal))
        {
            GenericActorContext.ArcRelayVeterancyWorkshopState[] workshops =
                ResolveWorkshops(contract, arc, condition.Workshop!);
            return fact switch
            {
                "workshop-ready" => workshops.Any(value => value.Phase
                    == GenericActorContext.ArcRelayVeterancyWorkshopPhase
                        .Ready) ? 1 : 0,
                "workshop-training" => workshops.Any(value => value.Phase
                    == GenericActorContext.ArcRelayVeterancyWorkshopPhase
                        .Training) ? 1 : 0,
                "workshop-training-self" => workshops.Any(value =>
                    value.Phase == GenericActorContext
                        .ArcRelayVeterancyWorkshopPhase.Training
                    && value.TrainingTeamId == _teamId) ? 1 : 0,
                "workshop-training-enemy" => workshops.Any(value =>
                    value.Phase == GenericActorContext
                        .ArcRelayVeterancyWorkshopPhase.Training
                    && value.TrainingTeamId != _teamId) ? 1 : 0,
                "workshop-contested" => workshops.Any(value => value.Phase
                    == GenericActorContext.ArcRelayVeterancyWorkshopPhase
                        .Contested) ? 1 : 0,
                "workshop-dormant" => workshops.Any(value => value.Phase
                    == GenericActorContext.ArcRelayVeterancyWorkshopPhase
                        .Dormant) ? 1 : 0,
                _ => 0,
            };
        }
        GenericActorContext.ArcRelayWellState well = ResolveWell(
            contract, arc, condition.Well!);
        if (fact == "well-has-outstanding-core")
            return well.OutstandingCoreId is null ? 0 : 1;
        if (fact == "well-ticks-until-birth")
            return WellTicksUntilBirth(well, mind.Tick);
        return own.FilledSocketWellIds.Contains(well.WellId,
            StringComparer.Ordinal) ? 1 : 0;
    }

    /// <summary>
    /// Programs and generated Instructions consume the one effective public
    /// Well clock. A Well with an active Core publishes no due tick, so it is
    /// deliberately farther than every authored numeric threshold.
    /// </summary>
    internal static int WellTicksUntilBirth(
        GenericActorContext.ArcRelayWellState well,
        int tick) =>
        well.NextScheduledBirthTick is int birth
            ? Math.Max(0, birth - tick)
            : int.MaxValue;

    private static bool BodyScopedFact(string fact) =>
        fact.StartsWith("self-", StringComparison.Ordinal)
        || fact is "signature-ready" or "signature-commitment-active"
            or "deployment-active" or "deployment-lifetime-percent"
            or "deployment-durability-percent";

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
            if (!_processedEvents.Add(observed.EventHandle))
                continue;
            if (_retainsVeterancy
                && observed.Payload is GenericActorContext.EventPayload.Destruction death
                && death.ActorId.TeamId == _teamId)
            {
                if (_levels.GetValueOrDefault(death.ActorId.UnitId).Actor
                    == death.ActorId)
                {
                    _levels.Remove(death.ActorId.UnitId);
                }
                if (_invested.GetValueOrDefault(death.ActorId.UnitId).Actor
                    == death.ActorId)
                {
                    _invested.Remove(death.ActorId.UnitId);
                }
                if (_pendingInvest.GetValueOrDefault(death.ActorId.UnitId).Actor
                    == death.ActorId)
                {
                    _pendingInvest.Remove(death.ActorId.UnitId);
                }
                continue;
            }
            if (observed.Payload is not GenericActorContext.EventPayload.ArcRelay mode)
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
                    ForgetSoloToss(picked.CoreId);
                    break;
                case GenericActorContext.ArcRelayEvent.CoreHandedOff handed:
                    _knownLooseCores.Remove(CoreKey(handed.CoreId));
                    ForgetSoloToss(handed.CoreId);
                    break;
                case GenericActorContext.ArcRelayEvent.CoreBanked banked:
                    _knownLooseCores.Remove(CoreKey(banked.CoreId));
                    ForgetSoloToss(banked.CoreId);
                    _lastObjectiveProgressTick = observed.SourceTick;
                    break;
                case GenericActorContext.ArcRelayEvent.LeveledUp level
                    when level.ActorId.TeamId == _teamId:
                    _levels[level.ActorId.UnitId] = (level.ActorId, level.Level);
                    _pendingInvest[level.ActorId.UnitId] = (
                        level.ActorId,
                        PendingInvestPoints(level.ActorId) + 1);
                    break;
                case GenericActorContext.ArcRelayEvent.VeterancyRetained retained
                    when retained.ActorId.TeamId == _teamId:
                    _levels[retained.ActorId.UnitId] = (
                        null,
                        retained.RetainedLevel);
                    _invested[retained.ActorId.UnitId] =
                        (null, retained.RetainedSpentPoints);
                    _pendingInvest[retained.ActorId.UnitId] = (
                        null,
                        Math.Max(
                            0,
                            retained.RetainedLevel - 1
                                - retained.RetainedSpentPoints));
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

    private void ReconcileSoloTosses(
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc)
    {
        Dictionary<int, MindBody> live = mind.Bodies.ToDictionary(
            body => body.UnitId);
        foreach ((int unitId, PendingSoloToss pending) in
                 _pendingSoloTosses.ToArray())
        {
            if (!live.TryGetValue(unitId, out MindBody? body)
                || body.ActorId != pending.Actor
                || mind.Tick > pending.ExpiresAtTick)
            {
                _pendingSoloTosses.Remove(unitId);
                continue;
            }
            GenericActorContext.ArcRelayCoreState? core = arc.VisibleCores
                .FirstOrDefault(candidate => candidate.CoreId == pending.CoreId);
            if (core is null)
                continue;
            if (core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Carried)
            {
                _pendingSoloTosses.Remove(unitId);
                continue;
            }
            _pendingSoloTosses[unitId] = pending with
            {
                Target = core.FlightTarget ?? core.Position,
            };
        }
    }

    private void ForgetSoloToss(GenericActorContext.ArcRelayCoreId coreId)
    {
        foreach (int unitId in _pendingSoloTosses
                     .Where(value => value.Value.CoreId == coreId)
                     .Select(value => value.Key)
                     .ToArray())
        {
            _pendingSoloTosses.Remove(unitId);
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

    private void ResolvePreviousOneShots(
        GenericActorResolvedMatchContract contract,
        MindContext mind)
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
                if (ArenaBasics.ArcRules(contract)?.Signatures.Any(signature =>
                        signature.ActionId == pending.ActionId) == true)
                {
                    _lastSignatureUseTick[body.ActorId] = mind.Tick - 1;
                }
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
            _wedgedPresses.Remove(stale);
            _scanCommitments.Remove(stale);
        }
        foreach ((ActorIdentity actor, string path) in _fightAnchors.Keys
                     .Where(key => !liveActors.Contains(key.Actor)).ToArray())
            _fightAnchors.Remove((actor, path));
        foreach (MindBody body in mind.Bodies)
        {
            string path = selected.GetValueOrDefault(body.UnitId)?.Leaf.Node.Path
                ?? "";
            if (_routeCursors.TryGetValue(
                    body.UnitId, out RouteCursor? routeCursor)
                && routeCursor.Actor != body.ActorId)
            {
                _routeCursors.Remove(body.UnitId);
                _routes.Reset(body.UnitId);
            }
            if (_ingressCursors.TryGetValue(
                    body.UnitId, out RouteCursor? ingressCursor)
                && ingressCursor.Actor != body.ActorId)
            {
                _ingressCursors.Remove(body.UnitId);
                _ingress.Reset(body.UnitId);
            }
            // Child use/fight/hold leaves are temporary pre-emption inside
            // the same ordinary episode. RoutePlan itself replaces a cursor
            // when a genuinely different route/state next asks to move.
            _selectedPaths[body.UnitId] = path;
            Proposal? proposal = selected.GetValueOrDefault(body.UnitId);
            if (proposal?.Leaf.Node.Action is OrdersPackage.ScanAction)
            {
                _scanCommitments[body.UnitId] = new ScanCommitment(
                    body.ActorId, path, proposal.Target.Key);
            }
            else
            {
                _scanCommitments.Remove(body.UnitId);
            }
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
        OrdersPackage package,
        MindBody body,
        string? zone)
    {
        var key = (body.ActorId, zone);
        if (_firingApproachDistances.TryGetValue(
                key, out int? cached))
            return cached;
        int? distance = ArenaBasics.NearestFiringApproachDistance(
            contract,
            mind,
            body,
            zone is null
                ? null
                : enemy => package.Contains(zone, enemy.Position));
        _firingApproachDistances[key] = distance;
        return distance;
    }

    private int[] WalkingDistanceField(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body)
    {
        if (_walkingDistanceFields.TryGetValue(
                body.ActorId, out int[]? cached))
            return cached;
        HashSet<Position> obstacles = ArenaBasics.PersistentRouteObstacles(
            contract, mind, body.ActorId.TeamId);
        obstacles.Remove(body.Position);
        int[] field = ArenaBasics.StaticRouteDistanceField(
            contract, body, body.Position, obstacles);
        _walkingDistanceFields[body.ActorId] = field;
        return field;
    }

    private VisibleCoreCandidate[] VisibleLooseCoreCandidates(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        OrdersPackage.CoreBankValueFilter? valueFilter,
        string? sourceWellId,
        int? maximumWalkingSteps)
    {
        int[] distances = WalkingDistanceField(contract, mind, body);
        return arc.VisibleCores
            .Where(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Loose
                && (sourceWellId is null
                    || core.CoreId.SourceWellId == sourceWellId))
            .Select(core =>
            {
                int bankValue = CoreBankValue(
                    contract, arc, core.CoreId.SourceWellId);
                return new
                {
                    Core = core,
                    Key = CoreKey(core.CoreId),
                    BankValue = bankValue,
                    Distance = ArenaBasics.DistanceFromField(
                        contract.Map, distances, core.Position),
                };
            })
            .Where(candidate => candidate.Distance is int distance
                && (maximumWalkingSteps is null
                    || distance <= maximumWalkingSteps)
                && (valueFilter is null
                    || valueFilter.Matches(candidate.BankValue)))
            .Select(candidate => new VisibleCoreCandidate(
                candidate.Core,
                candidate.Key,
                candidate.BankValue,
                candidate.Distance!.Value))
            .ToArray();
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
            contract, mind, body, target, reachable);
        _firingGround[key] = goals;
        return goals;
    }

    /// <summary>
    /// A Hold defender that has permission to pursue a Core carrier should not
    /// repeatedly declare a windup shot from the gun's outermost tile. A
    /// carrier can legally leave that frozen wedge before maturation, turning
    /// a faster defender into an endless sequence of cancelled shots. Reserve
    /// one tile of range per windup tick before committing; the ordinary leash
    /// and reachable-firing-ground rules remain the bounds on the close.
    /// </summary>
    private static bool NeedsWindupClosingMargin(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        ActorIdentity targetActor,
        Position target,
        OrdersPackage.FightAction fight)
    {
        if (fight.Style != "hold"
            || ArenaBasics.CarriedCore(mind, targetActor) is null)
        {
            return false;
        }
        int windup = ArenaBasics.AttackWindupTicks(contract, body.FormId);
        int reach = ArenaBasics.AttackRange(contract, body.FormId);
        if (windup <= 0 || reach <= 1)
            return false;
        int bufferedReach = Math.Max(1, reach - windup);
        return body.Position.ChebyshevDistance(target) > bufferedReach;
    }

    private static Position[] WindupBufferedFiringGoals(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Position target,
        IEnumerable<Position> firing)
    {
        int reach = ArenaBasics.AttackRange(contract, body.FormId);
        int windup = ArenaBasics.AttackWindupTicks(contract, body.FormId);
        int bufferedReach = Math.Max(1, reach - windup);
        return [.. firing.Where(position =>
            position.ChebyshevDistance(target) <= bufferedReach)];
    }

    private static Position[] PreferredRangeGoals(
        OrdersPackage.FightAction fight,
        Position target,
        IEnumerable<Position> firing) =>
        fight.PreferredRange is int range
            ? [.. firing.Where(position =>
                position.ChebyshevDistance(target) <= range)]
            : [.. firing];

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
        (ActorIdentity? Actor, int Spent) investment =
            _invested.GetValueOrDefault(body.UnitId);
        int spent = investment.Actor is null || investment.Actor == body.ActorId
            ? investment.Spent
            : 0;
        string track = role.UpgradePath[Math.Min(
            spent, role.UpgradePath.Length - 1)];
        body.Command(
            invest.ActionId,
            invest.ActionCode,
            [new GenericActorActionArgument.UpgradeTrackArgument(track)],
            $"orders:{ProgramPrefix(body.UnitId)}{role.Id}:upgrade:{track}");
        _invested[body.UnitId] = (body.ActorId, spent + 1);
        _pendingInvest[body.UnitId] = (
            body.ActorId,
            PendingInvestPoints(body.ActorId) - 1);
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
        return PendingInvestPoints(body.ActorId) > 0
            && !carried.ContainsKey(body.ActorId)
            && !mind.Enemies.Any(enemy =>
                enemy.Position.ChebyshevDistance(body.Position) <= 5)
            && body.Action("invest") is { Available: true };
    }

    private int CurrentLevel(MindBody body)
    {
        (ActorIdentity? Actor, int Level) level =
            _levels.GetValueOrDefault(body.UnitId);
        return level.Actor is null || level.Actor == body.ActorId
            ? Math.Max(1, level.Level)
            : 1;
    }

    private int PendingInvestPoints(ActorIdentity actor)
    {
        (ActorIdentity? Actor, int Points) pending =
            _pendingInvest.GetValueOrDefault(actor.UnitId);
        if (!_retainsVeterancy)
            return pending.Points;
        return pending.Actor is null || pending.Actor == actor
            ? pending.Points
            : 0;
    }

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
        int? CapacityScopeLimit,
        int Distance,
        string? SelectionTrace,
        string? EligibilityTrace = null);

    private sealed record ResolvedTarget(
        string Key,
        Position Position,
        Position[] Goals,
        ActorIdentity? Actor,
        string? CoreKey,
        string? Trace = null,
        int? WalkingDistance = null,
        Direction? Facing = null);

    private sealed record MovePlan(
        string Path,
        Position Goal,
        Position[] Goals,
        TacticalRouteGovernor.CourseStep? Course,
        bool Arrived);

    private sealed record ScanCommitment(
        ActorIdentity Actor,
        string Path,
        string TargetKey);

    private sealed class RouteCursor
    {
        private RouteCursor(
            ActorIdentity actor,
            string stateId,
            string routeId)
        {
            Actor = actor;
            StateId = stateId;
            RouteId = routeId;
        }

        public ActorIdentity Actor { get; }
        public string StateId { get; }
        public string RouteId { get; }
        public int Index { get; set; }
        public int Lap { get; set; }
        public bool Initialized { get; set; }
        public int BestDistance { get; set; } = int.MaxValue;
        public static RouteCursor For(
            ActorIdentity actor,
            string stateId,
            string routeId) => new(actor, stateId, routeId);
    }

    private sealed record PendingOneShot(
        ActorIdentity Actor,
        OrdersPackage.OrderNode Node,
        string ActionId);
    private sealed record PendingSoloToss(
        ActorIdentity Actor,
        GenericActorContext.ArcRelayCoreId CoreId,
        Position Target,
        int ExpiresAtTick);
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
    private sealed record VisibleCoreCandidate(
        GenericActorContext.ArcRelayCoreState Core,
        string Key,
        int BankValue,
        int Distance);
    private sealed record RememberedEnemy(Position Position, int Tick, bool Carrier);
}
