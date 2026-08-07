using BotArena.Sdk;

/// <summary>
/// Evaluation-grade executor for the typed tactical-playbook v1 package. The
/// algorithm owns deterministic arbitration and memory; the linked playbook
/// owns roles, groups, formations, movement, combat, support, custody, and
/// phase transitions.
/// </summary>
public sealed class ArcRelayTacticalPlaybookMind : IGenericMindBot
{
    private readonly Dictionary<int, string> _stableRoles = [];
    private readonly Dictionary<int, int> _enemyUnavailableUntil = [];
    private readonly Dictionary<int, LastSeenEnemy> _lastSeenEnemies = [];
    private readonly Dictionary<ActorIdentity, int> _firstSeenEnemyLife = [];
    private readonly HashSet<ActorIdentity> _observedDestroyedEnemies = [];
    private readonly Dictionary<string, SecuredCore> _securedCores =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _processedEvents =
        new(StringComparer.Ordinal);
    private readonly Dictionary<int, RouteProgress> _routes = [];
    private readonly Dictionary<int, MotionProgress> _motion = [];
    private readonly Dictionary<string, FocusLock> _focusLocks =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, CoreReservation> _coreReservations =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, EmergencyRecovery>
        _emergencyRecoveries = new(StringComparer.Ordinal);
    /// <summary>Live bait Cores by key, owned by their custody id.</summary>
    private readonly Dictionary<string, string> _baitCores =
        new(StringComparer.Ordinal);
    /// <summary>Unspent veterancy points queued per own unit.</summary>
    private readonly Dictionary<int, int> _pendingInvest = [];
    private readonly Dictionary<int, (int LifeId, int Spent)> _investSpent = [];
    private readonly Dictionary<int, (int LifeId, int Level)> _unitLevel = [];
    private readonly Dictionary<int, int> _slotHold = [];
    /// <summary>How many consecutive ticks each unit has stood still on an
    /// own carrier's supply lane, and where.</summary>
    /// <summary>Why each own body got no focus this tick, keyed by unit —
    /// the allocation plane's only voice. Every Class-A filter in
    /// docs/EXECUTOR-SILENT-POLICY.md drops a target by RETURNING FALSE, so
    /// without this a declined fight reads as ordinary movement.</summary>
    private readonly Dictionary<int, string> _declines = [];
    /// <summary>Units that broke off to heal, by life — they do not take a
    /// new fight until whole. See <see cref="HealOutranksFighting"/>.
    /// </summary>
    private readonly Dictionary<int, int> _healBreak = [];
    /// <summary>Units fighting on because they have no exit step.</summary>
    private readonly HashSet<int> _cornered = [];

    private readonly Dictionary<int, (int LifeId, Position Tile, int Ticks)>
        _lanePlugTicks = [];
    /// <summary>The Core each unit last handed off, and when — so its own
    /// collect step cannot take it straight back.</summary>
    private readonly Dictionary<int, (int LifeId, string CoreKey, int Tick)>
        _handedOff = [];
    private int _laneReliefs;
    private readonly Dictionary<int, (int LifeId, int UntilTick)>
        _disengaging = [];
    private readonly Dictionary<int, (int LifeId, Position Rally)>
        _withdrawRallies = [];
    /// <summary>Get-behind positioning budgets (commit.approach): when the
    /// window opened per unit, keyed to the life and the target it was
    /// opened against.</summary>
    private readonly Dictionary<int, (int LifeId, ActorIdentity Target,
        int StartTick)> _positioning = [];
    private readonly Dictionary<int, (int LifeId, int Index)> _patrols = [];
    private Position[]? _trafficWaypoints;
    private readonly Dictionary<ActorIdentity, CustodyProgress>
        _custodyProgress = [];
    private readonly Dictionary<string, FriendlyDroppedCore>
        _friendlyDroppedCores = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _formationStableTicks =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, TacticalFormationPrimitives.Lifecycle>
        _formationLifecycles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _activeFormationIds =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _orderCompletion =
        new(StringComparer.Ordinal);
    private readonly Dictionary<int, ActorIdentity> _friendlyLives = [];
    private readonly HashSet<int> _joiningUnits = [];
    private readonly Dictionary<int, ActorIdentity> _returningToFormation = [];
    private GenericActorResolvedMatchContract? _contract;
    /// <summary>This mind's movement seam, chosen once at StartMatch. It is
    /// an instance field, never a static: an in-process mirror cell runs
    /// both participants inside one loaded assembly.</summary>
    private IArenaStepper _stepper = new GreedyArenaStepper();
    private TacticalPlaybookPackage? _package;
    private TacticalPlaybookMachine? _machine;
    private TacticalTaskMachine? _tasks;
    private Position _ownReactor;
    private bool _mirrored;
    private int _rearArcDamageMultiplier = 1;
    private Position _enemyReactor;
    private string? _allocationPhaseId;
    private string? _queuedFallbackPhase;
    private int _teamId;
    private int _lastObjectiveProgressTick;
    private int _lastOwnCharge;
    private Position[] _healTiles = [];
    /// <summary>How long a body may plug a loaded carrier's route home
    /// before it is moved aside.</summary>
    private const int CarrierLanePatience = 2;

    /// <summary>How long a handed-off Core stays off limits to the body that
    /// put it down — comfortably longer than an adjacent receiver needs.
    /// </summary>
    private const int HandoffGraceTicks = 30;

    /// <summary>How near a cold trail has to be before walking to it counts
    /// as pursuit rather than abandoning the post.</summary>
    private const int FlushReach = 6;

    /// <summary>How long one sighting's excursion may last.</summary>
    private const int FlushTicks = 6;

    /// <summary>Live hidden-lock excursions per engagement scope.</summary>
    private readonly Dictionary<string, HiddenFlush> _flushes =
        new(StringComparer.Ordinal);

    /// <summary>Own units the recover predicate flags this tick — see
    /// <see cref="UpdateRecovering"/>. The recover doctrine verb's task
    /// gates on its size and its assignment row selects from its members.
    /// </summary>
    private readonly HashSet<int> _recovering = [];
    /// <summary>Why each hurt body did or did not qualify, for the debug
    /// line — a gate nobody can see is a gate nobody can tune.</summary>
    private readonly List<string> _recoverTrace = [];

    public void StartMatch(MindStart start)
    {
        _contract = start.Contract;
        _teamId = start.TeamId;
        _healTiles = start.Contract.Map.Regions
            .Where(region => region.RegionId.StartsWith(
                "heal-", StringComparison.Ordinal))
            .SelectMany(region => region.Tiles)
            .ToArray();
        if (start.Contract.ModeMapBinding is not
            GenericActorResolvedMatchContract.ArcRelayModeMapBinding binding)
        {
            throw new InvalidOperationException(
                "TacticalPlaybookMind requires Arc Relay.");
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
        _mirrored = _ownReactor.X > (start.Contract.Map.Width - 1) / 2;
        _rearArcDamageMultiplier = start.Contract.Rules.GameMode
            is GenericActorRulesContract.ArcRelayGameMode arcMode
            ? arcMode.RearArcDamageMultiplier
            : 1;
        _package = TacticalPlaybookPackage.Load(
            start.EvaluationData, start.Contract, _ownReactor);
        _stepper = string.Equals(
            _package.StepperMode, "coordinated", StringComparison.Ordinal)
            ? new CoordinatedArenaStepper()
            : new GreedyArenaStepper();
        ValidateComposition(start.Contract, start.TeamId, _package.Source);
        AssertSignatureCoverage(start.Contract, start.TeamId);
        _machine = new TacticalPlaybookMachine(_package.Source);
        _tasks = new TacticalTaskMachine(_package.Source);
    }

    public void Think(MindContext mind)
    {
        GenericActorResolvedMatchContract contract = _contract
            ?? throw new InvalidOperationException("StartMatch was not called.");
        TacticalPlaybookPackage package = _package
            ?? throw new InvalidOperationException("Playbook was not loaded.");
        TacticalPlaybookMachine machine = _machine
            ?? throw new InvalidOperationException("Machine was not loaded.");
        TacticalTaskMachine tasks = _tasks
            ?? throw new InvalidOperationException("Task machine was not loaded.");
        if (mind.Mode is not GenericActorContext.ModeObservationState.ArcRelay arc)
        {
            foreach (MindBody body in mind.Bodies)
                body.Hold("unsupported mode");
            return;
        }

        if (_queuedFallbackPhase is string fallbackPhase)
        {
            machine.ForcePhase(fallbackPhase, mind.Tick);
            _queuedFallbackPhase = null;
        }

        UpdateMemory(mind, arc, package.Source.Memory);
        UpdateRecovering(contract, mind);
        Dictionary<int, string> roles = AllocateRoles(
            mind,
            package.Source,
            phaseBoundary: !string.Equals(
                _allocationPhaseId,
                machine.PhaseId,
                StringComparison.Ordinal));
        Dictionary<int, string> groups = GroupMembership(roles, package.Source);
        IReadOnlySet<int> priorTaskLeases = tasks.LeasedUnitIds;
        UpdateFriendlyMembership(
            mind, package, machine, roles, groups, priorTaskLeases);
        TacticalSnapshot snapshot = Snapshot(
            mind, arc, package, machine, roles, groups,
            priorTaskLeases,
            updateFormationState: false);
        foreach (TacticalPlaybookPackage.Group group in package.Source.Groups)
        {
            machine.AdvanceLocal(group, mind.Tick,
                condition => Evaluate(condition, snapshot, package));
        }
        bool phaseChanged = machine.AdvanceGlobal(mind.Tick,
            condition => Evaluate(condition, snapshot, package));
        if (phaseChanged)
        {
            roles = AllocateRoles(
                mind, package.Source, phaseBoundary: true);
            groups = GroupMembership(roles, package.Source);
        }
        _allocationPhaseId = machine.PhaseId;
        Dictionary<ActorIdentity, GenericActorContext.ArcRelayCoreState> carried =
            arc.VisibleCores
                .Where(core => core.Disposition
                    == GenericActorContext.ArcRelayCoreDisposition.Carried
                    && core.CarrierActorId is not null)
                .ToDictionary(core => core.CarrierActorId!, core => core);
        UpdateCustodyProgress(mind.Tick, mind.Bodies, carried);
        snapshot = Snapshot(
            mind, arc, package, machine, roles, groups,
            priorTaskLeases,
            updateFormationState: false);
        tasks.Update(
            mind.Tick,
            machine.PhaseId,
            mind.Bodies.Select(body => new TacticalTaskCandidate(
                    body.UnitId,
                    body.ActorId,
                    roles[body.UnitId],
                    groups[body.UnitId],
                    machine.LocalState(groups[body.UnitId]),
                    body.ClassId ?? "",
                    carried.ContainsKey(body.ActorId),
                    body.Position))
                .ToArray(),
            condition => TacticalPlaybookMachine.Matches(
                condition,
                leaf => Evaluate(leaf, snapshot, package)),
            (assignment, candidate) => TaskSelectionDistance(
                package,
                arc,
                assignment,
                candidate));
        snapshot = Snapshot(
            mind, arc, package, machine, roles, groups,
            tasks.LeasedUnitIds,
            updateFormationState: true);
        Dictionary<int, TacticalPlaybookPackage.Order> orders = ActiveOrders(
            mind, package.Source, machine, tasks, roles, groups);
        Dictionary<int, Position> authoredTargets = mind.Bodies.ToDictionary(
            body => body.UnitId,
            body => Target(
                contract, mind, package, machine, roles, groups, orders,
                carried, orders[body.UnitId], body));
        // Escorts, not formations (owner ruling 2026-08-09). A doctrine
        // order names a leader and its followers; the leader keeps the
        // target it was given and each follower's ground is a posture
        // function of where the leader IS and where it is about to step.
        EscortParty[] escorts = ResolveEscorts(
            contract, mind, roles, orders, authoredTargets);
        foreach (EscortParty party in escorts)
        foreach (EscortMember member in party.Followers)
        {
            authoredTargets[member.Body.UnitId] =
                TacticalEscortPrimitives.DesiredTile(
                    member.Policy.Posture,
                    party.Leader.Position,
                    party.LeaderStep,
                    party.Leader.Facing,
                    member.Ordinal,
                    member.Policy.Leash);
        }
        // Leader outranks follower unconditionally, and followers outrank
        // each other in the order's declared list order.
        Dictionary<int, int> escortRank = escorts
            .SelectMany(party => party.Followers
                .Select(member => (member.Body.UnitId, Rank: member.Ordinal + 1))
                .Prepend((party.Leader.UnitId, Rank: 0)))
            .ToDictionary(value => value.UnitId, value => value.Rank);
        Dictionary<int, Position> targets = ResolveFormationTargets(
            contract,
            mind,
            package,
            roles,
            orders,
            authoredTargets);
        RefreshSelfDefenseReturns(mind, orders, targets);
        UpdateOrderCompletion(mind, package, orders, targets);
        HashSet<int> carrierUnitIds = mind.Bodies
            .Where(body => carried.ContainsKey(body.ActorId))
            .Select(body => body.UnitId)
            .ToHashSet();
        HashSet<int> focusParticipants = mind.Bodies
            .Where(body => !carrierUnitIds.Contains(body.UnitId))
            .Where(body => !_returningToFormation.ContainsKey(body.UnitId))
            .Where(body => package.Source.Engagements.Single(value =>
                    value.EngagementId
                        == orders[body.UnitId].EngagementId)
                .Participants.Contains(
                    roles[body.UnitId], StringComparer.Ordinal))
            .Select(body => body.UnitId)
            .ToHashSet();
        Dictionary<int, MindBody> repairs = AllocateRepairs(
            contract, mind, package.Source, roles, orders,
            carried.Keys.ToHashSet(), focusParticipants);
        HashSet<int> unavailableAttackers = repairs.Keys.ToHashSet();
        unavailableAttackers.UnionWith(carrierUnitIds);
        Dictionary<int, FocusAssignment> focus =
            AllocateFocus(contract, mind, arc, package.Source, package,
                snapshot, roles, orders, targets, unavailableAttackers);
        GenericActorContext.ArcRelayCoreState[] loose = arc.VisibleCores
            .Where(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Loose)
            .OrderBy(core => core.CoreId.SourceWellId, StringComparer.Ordinal)
            .ThenBy(core => core.CoreId.SourceOrdinal)
            .ToArray();
        // Bait custody state: while a bait's reclaim conditions stay false,
        // its dropped Core is untouchable and unassigned for the own team.
        HashSet<string> baitingCustodies = new(StringComparer.Ordinal);
        foreach (TacticalPlaybookPackage.CustodyPolicy custodyPolicy in
                 package.Source.CustodyPolicies)
        {
            if (custodyPolicy.BaitDrop is not { } custodyBait)
                continue;
            bool reclaimed = custodyBait.ReclaimAll.Any(group =>
                TacticalPlaybookMachine.Matches(group,
                    condition => Evaluate(condition, snapshot, package)));
            if (!reclaimed)
                baitingCustodies.Add(custodyPolicy.CustodyId);
        }
        foreach (string stale in _baitCores
                     .Where(value => !baitingCustodies.Contains(value.Value)
                         || arc.VisibleCores.All(core =>
                             CoreKey(core.CoreId) != value.Key
                             || core.Disposition == GenericActorContext
                                 .ArcRelayCoreDisposition.Carried))
                     .Select(value => value.Key).ToArray())
        {
            _baitCores.Remove(stale);
        }
        HashSet<string> activeBaitKeys =
            _baitCores.Keys.ToHashSet(StringComparer.Ordinal);
        Dictionary<int, GenericActorContext.ArcRelayCoreState> pickupAssignments =
            AllocateCorePickups(
                mind, package, snapshot, roles, orders,
                loose.Where(core => !activeBaitKeys.Contains(
                        CoreKey(core.CoreId)))
                    .ToArray(),
                out HashSet<string> openSourceWells);
        var claims = ArenaBasics.Claims.ForTick(contract, mind, _stepper);
        // Rooted shooter, committed unless disengage triggers (owner ruling
        // 2026-08-07). A body winding up its OWN declared strike does not
        // move — any move abandons the declare, so a dodge, a formation
        // step or a lane clearance mid-windup spends the whole commitment
        // for nothing. Incoming declared cones are not an exception: eating
        // a cone to land yours is the trade the mechanic is for. The one
        // exception is the disengage latch, which is the body deciding the
        // fight itself is lost; the abandoning move then reads as the
        // withdraw it is. Cornered composes untouched — a latched body with
        // no exit step still has nowhere to go, so it keeps swinging.
        foreach (MindBody body in mind.Bodies)
        {
            if (arc.PendingStrikes.Any(strike =>
                    strike.Shooter == body.ActorId)
                && !DisengageLatched(mind, body))
            {
                claims.Root(body.UnitId);
            }
        }
        // A live bait is a trap, not supply: no own body may stand on it on
        // any ruleset, or tick-start pickup would spring our own trap.
        foreach (GenericActorContext.ArcRelayCoreState core in loose)
        {
            if (activeBaitKeys.Contains(CoreKey(core.CoreId)))
                claims.Reserve(core.Position);
        }
        if (ArenaBasics.ArcRules(contract) is { RipenIntervalTicks: > 0 })
        {
            // Under ripening rules a loose Core is a growing asset: stepping
            // on one custody has not released is value destruction, so only
            // this tick's assigned collectors may enter those tiles. The same
            // goes for camping a gated Well: a body waiting on the tile would
            // swallow the birth at base value, so closed Wells are no-stand
            // tiles too.
            foreach (GenericActorContext.ArcRelayCoreState core in loose)
            {
                if (!pickupAssignments.ContainsValue(core))
                    claims.Reserve(core.Position);
            }
            foreach (GenericActorContext.ArcRelayWellState well in arc.Wells)
            {
                if (!openSourceWells.Contains(well.WellId))
                    claims.Reserve(well.Position);
            }
        }
        // Escort right-of-way, said in the same machinery the carrier lane
        // already uses: the tile the leader is stepping into is reserved
        // against every other own body, its own followers first. A follower
        // caught standing in it yields this tick (escort-yield below) - the
        // leader never negotiates with its escort for the ground it wants,
        // which is the whole of "a reversal makes followers yield".
        var escortYield = new Dictionary<int, EscortParty>();
        foreach (EscortParty party in escorts)
        {
            if (party.LeaderStep is not Position leaderStep)
                continue;
            claims.ReserveLane(leaderStep, party.Leader.UnitId);
            foreach (EscortMember member in party.Followers)
            {
                if (member.Body.Position == leaderStep)
                    escortYield[member.Body.UnitId] = party;
            }
        }
        // The reserved lane must aim where the carrier will actually step,
        // routed deliveries included - a lane reserved toward the reactor
        // while the body walks a corridor is a lane nobody uses.
        Dictionary<int, Position> carrierSteps = mind.Bodies
            .Where(body => carried.ContainsKey(body.ActorId))
            .Select(body => (
                Body: body,
                Step: ArenaBasics.StaticFirstStep(
                    contract, mind, body,
                    CarrierDestination(package, orders, body))))
            .Where(value => value.Step is not null)
            .ToDictionary(value => value.Body.UnitId, value => value.Step!.Value);
        HashSet<Position> carrierClearance = carrierSteps.Values.ToHashSet();
        if (carrierSteps.Count > 0)
            carrierClearance.Add(_ownReactor);
        // Right-of-way: the carrier's next route step belongs to the
        // carrier. Reserving it as a lane keeps every other own body out
        // of it for the tick, so clearing it is durable - the measured
        // failure (w-9003, 114 ticks) was an escort that finally stepped
        // aside while a dancing teammate claimed the freed tile first.
        foreach ((int carrierUnit, Position step) in carrierSteps)
            claims.ReserveLane(step, carrierUnit);
        foreach (MindBody body in mind.Bodies
                     .OrderByDescending(body => carried.ContainsKey(body.ActorId))
                     // A body standing on a carrier's lane acts before the
                     // rest of the team: its escape tiles must be chosen
                     // before lower-priority escorts claim them (owner
                     // direction 2026-08 - cooperative movement resolves
                     // by weight, carriers heaviest, their blockers next).
                     .ThenByDescending(body =>
                         !carried.ContainsKey(body.ActorId)
                         && carrierClearance.Contains(body.Position))
                     // The same tier for an escort caught in its leader's
                     // doorway: it picks its exit before free traffic
                     // takes the tiles it could have used.
                     .ThenByDescending(body =>
                         escortYield.ContainsKey(body.UnitId))
                     // Under a cooperative stepper the tier the greedy
                     // order never needed: a body in contact settles its
                     // tile before free traffic reserves it out from
                     // under the fight. Greedy stepping leaves this key
                     // false for everyone, so its order is unchanged.
                     .ThenByDescending(body => _stepper.WantsFightPrecedence
                         && mind.Enemies.Any(enemy =>
                             enemy.Position.ChebyshevDistance(body.Position)
                                 <= 2))
                     .ThenBy(body => orders[body.UnitId].Priority)
                     // Leader before its own followers, followers in the
                     // order's declared list order. Everyone else ranks 0
                     // and keeps the historical unit-id tie-break.
                     .ThenBy(body => escortRank.GetValueOrDefault(body.UnitId))
                     .ThenBy(body => body.UnitId))
        {
            string role = roles[body.UnitId];
            string group = groups[body.UnitId];
            TacticalPlaybookPackage.Order order = orders[body.UnitId];
            TacticalPlaybookPackage.Engagement engagement = package.Source
                .Engagements.Single(value => value.EngagementId
                    == order.EngagementId);
            TacticalPlaybookPackage.Formation formation = package.ResolveFormation(
                package.Source, order.FormationId);
            Position target = targets[body.UnitId];
            TacticalPlaybookPackage.Placement facingPlacement =
                FormationPlacement(formation, roles, body.UnitId, role);
            Position? facingTarget = TacticalFormationPrimitives.FacingTarget(
                facingPlacement.Facing ?? formation.Orientation,
                body.Position,
                target,
                _ownReactor,
                _enemyReactor,
                focus.GetValueOrDefault(body.UnitId)?.Target.Position);
            body.SetRole(RoleTag(
                machine.PhaseId, group, role, order.OrderId,
                escortRank.GetValueOrDefault(body.UnitId) > 0));

            // Escort yield. Standing where the leader is going is the one
            // thing an escort may never do; the preferred exit is straight
            // on, so a reversing pair walks the corridor out escort-first
            // instead of arguing over one tile.
            if (escortYield.TryGetValue(body.UnitId, out EscortParty? yielding))
            {
                Position onward = body.Position.Offset(
                    body.Position.X - yielding.Leader.Position.X,
                    body.Position.Y - yielding.Leader.Position.Y);
                string yieldReason = Provenance(
                    machine, group, order, "escort-yield");
                if (ArenaBasics.TryMoveDirect(
                        contract, mind, body, onward, claims, yieldReason)
                    || ArenaBasics.TryMoveAside(
                        contract, mind, body, claims,
                        new HashSet<Position>
                        {
                            body.Position,
                            yielding.Leader.Position,
                        },
                        yieldReason))
                {
                    continue;
                }
            }

            if (!carried.ContainsKey(body.ActorId)
                && carrierClearance.Contains(body.Position)
                && ArenaBasics.TryMoveAside(
                    contract, mind, body, claims, carrierClearance,
                    Provenance(machine, group, order,
                        "clear-custody-return-lane")))
            {
                continue;
            }

            // Veterancy: spend a queued skill point the first quiet tick.
            if (_pendingInvest.GetValueOrDefault(body.UnitId) > 0
                && !carried.ContainsKey(body.ActorId)
                && mind.Enemies.All(enemy =>
                    enemy.Position.ChebyshevDistance(body.Position) > 5)
                && body.Action("invest") is { Available: true } investAction)
            {
                _pendingInvest[body.UnitId]--;
                // The playbook owns the build when its role declares one:
                // the Nth point of a life buys the Nth listed track,
                // repeating the last entry past the end. A death resets
                // the count with the life, matching the rules' reset.
                TacticalPlaybookPackage.Role investRole = package.Source
                    .Roles.Single(value =>
                        value.RoleId == roles[body.UnitId]);
                (int spendLife, int spent) = _investSpent.GetValueOrDefault(
                    body.UnitId,
                    (body.ActorId.LifeId, 0));
                if (spendLife != body.ActorId.LifeId) spent = 0;
                string track = investRole.Build is { Length: > 0 } build
                    ? build[Math.Min(spent, build.Length - 1)]
                    : DefaultBuildTrack(roles[body.UnitId]);
                _investSpent[body.UnitId] = (body.ActorId.LifeId, spent + 1);
                body.Command(
                    investAction.ActionId,
                    investAction.ActionCode,
                    [
                        new GenericActorActionArgument.UpgradeTrackArgument(
                            track),
                    ],
                    Provenance(machine, group, order, "veterancy-invest"));
                continue;
            }

            // The pragmatist heal path is GONE (owner ruling 2026-08-07):
            // the `recover` verb is the only road to a beacon now, so heal
            // priority is settled by mode order like every other intent. A
            // sheet with no recover mode simply never detours - the sheet
            // owns it. (The medic's beam is the separate `repair` channel
            // and is untouched.)

            // Clearing a plugged carrier lane runs BEFORE the channels: a
            // body can act every tick without displacing (facing scans,
            // in-place micro) and those paths never reach Hold at all.
            bool acted = TryCarrierLaneRelief(
                contract, mind, body, carrierUnitIds,
                repairs.Keys.ToHashSet(), targets, focus.Keys.ToHashSet(),
                claims,
                Provenance(machine, group, order, "choke-relief"));
            foreach (string channel in package.Source.Arbitration.Channels)
            {
                if (acted)
                    break;
                acted = channel switch
                {
                    "custody-emergency" => TryCustodyEmergency(
                        contract, mind, arc, package, body, role, order,
                        carried, pickupAssignments, baitingCustodies, claims),
                    "self-preservation" => TryStrikeEvacuation(
                            contract, mind, arc, body, claims)
                        || TrySelfPreservation(
                            contract, mind, package.Source, body, order,
                            claims),
                    "repair" => repairs.TryGetValue(
                            body.UnitId, out MindBody? repairTarget)
                        && ArenaBasics.TryUnitSignature(
                            contract, body, "repair-beam", repairTarget.ActorId,
                            Provenance(machine, group, order, "repair")),
                    "signature" => focus.TryGetValue(
                            body.UnitId, out FocusAssignment?
                                signatureTarget)
                        && engagement.SignatureCoordination != "damage-first"
                        && signatureTarget.UseSignature
                        && TryCombatSignatureWithReturn(
                            contract, mind, body, signatureTarget.Target, target,
                            signatureTarget, engagement,
                            Provenance(machine, group, order, "signature"))
                        || engagement.SignatureCoordination == "support-first"
                        && !focus.ContainsKey(body.UnitId)
                        && TrySupportIdleSignature(
                            contract, mind, body,
                            Provenance(machine, group, order,
                                "signature-idle"))
                        || engagement.SignatureCoordination != "none"
                        && !carried.ContainsKey(body.ActorId)
                        && TryOpportunisticHeadingSignature(
                            contract, mind, body,
                            Provenance(machine, group, order,
                                "signature-heading")),
                    "focus-fire" => focus.TryGetValue(
                            body.UnitId, out FocusAssignment?
                                shotTarget)
                        && !shotTarget.HoldDeclare
                        && WithinEngagementLeash(
                            body, target, shotTarget.Target, engagement)
                        && TryFocusChannelWithReturn(
                            contract, mind, body, shotTarget, target,
                            engagement,
                            Provenance(machine, group, order, "signature")),
                    "movement" => TryRecoverHold(
                            body, order,
                            Provenance(machine, group, order, "recover-heal"))
                        || TryWithdraw(
                            contract, mind, package, body, engagement, claims,
                            Provenance(machine, group, order, "withdraw"))
                        // Collect FIRST: the ball outranks picking a new
                        // fight. Above duel-stand and close-on-focus, below
                        // withdraw and the survival channels - a strike
                        // windup and a cone dodge are never interrupted.
                        || string.Equals(engagement.Collect, "first",
                            StringComparison.Ordinal)
                        && order.CollectZones is { Length: > 0 }
                        && !arc.VisibleCores.Any(core =>
                            core.CarrierActorId == body.ActorId)
                        && TryCollectLooseCore(
                            contract, mind, arc, package, body, order,
                            engagement, claims)
                        // An ARMED recover ranked against combat. Yield lets
                        // the fight finish; first breaks off the uncommitted
                        // part of it. One hit from dead outranks both, which
                        // is the standing survival ruling.
                        || !HealOutranksFighting(contract, body, order, engagement)
                        && TryDuelStand(
                            contract, body, focus,
                            Provenance(machine, group, order, "duel-stand"))
                        || !HealOutranksFighting(contract, body, order, engagement)
                        && TryCloseOnFocus(
                            contract, mind, body, focus, claims,
                            Provenance(machine, group, order, "close-on-focus"))
                        || !HealOutranksFighting(contract, body, order, engagement)
                        && TryFlushHidden(
                            contract, mind, body, order, engagement, focus,
                            role, target, claims,
                            Provenance(machine, group, order, "flush-hidden"))
                        || TryMovement(
                            contract, mind, arc, package, machine, snapshot,
                            body,
                            role, group, order, engagement, target, targets,
                            groups, orders,
                            pickupAssignments, focus, claims,
                            // A follower is not marching a formation; it is
                            // following someone. The trace says which.
                            escortRank.GetValueOrDefault(body.UnitId) > 0
                                ? "escort-follow"
                                : "formation-move"),
                    "facing" => TryScanSweep(
                            contract, mind, body, order, target,
                            Provenance(machine, group, order, "scan"))
                        || facingTarget is Position lookAt
                        && TryFaceTarget(
                        contract, body, lookAt,
                        Provenance(machine, group, order, "facing")),
                    "hold" => Hold(body,
                        Provenance(machine, group, order, "hold")),
                    _ => throw new InvalidDataException(
                        $"Unknown arbitration channel '{channel}'."),
                };
                if (acted)
                    break;
            }
            if (!acted)
                Hold(body, Provenance(machine, group, order, "exhausted"));
        }

        mind.Debug.Write(
            $"playbook {package.Source.PlaybookId}; phase={machine.PhaseId}; "
            + $"live={mind.Bodies.Length}; enemy-down="
            + $"{_enemyUnavailableUntil.Count}; secured={_securedCores.Count}; "
            + $"carriers={snapshot.FriendlyCarriers}; loose="
            + $"{snapshot.VisibleLooseCores}; wells="
            + $"{snapshot.WellOutstanding.Values.Sum()}; reactor="
            + $"{snapshot.ReactorIntegrity}/{snapshot.ReactorCharge}; "
            + "focus=" + string.Join(",", focus
                .OrderBy(value => value.Key)
                .Select(value => $"{value.Key}->{value.Value.Target.ActorId}"
                    + $"@{value.Value.AimPosition}")) + "; "
            + "returning=" + string.Join(",",
                _returningToFormation.Keys.Order()) + "; "
            + "repair=" + string.Join(",", repairs.Keys.Order()) + "; "
            + tasks.TraceSummary + "; "
            + "declines=" + string.Join(",", _declines
                .OrderBy(value => value.Key)
                .Select(value => $"{value.Key}:{value.Value}")) + "; "
            + "recover=" + (_recoverTrace.Count == 0
                ? "-" : string.Join(",", _recoverTrace)) + "; "
            + $"lane-reliefs={_laneReliefs}; "
            + $"sheet={package.PlaybookSha256[..8]}; layout="
            + package.LayoutSha256[..8]);
    }

    public void EndMatch(MindEnd end) => _ = end;

    private static void ValidateComposition(
        GenericActorResolvedMatchContract contract,
        int teamId,
        TacticalPlaybookPackage.Playbook playbook)
    {
        string[] actual = contract.Topology.UnitSlots
            .Where(slot => slot.TeamId == teamId)
            .OrderBy(slot => slot.UnitId)
            .Select(slot => slot.ClassId ?? "")
            .ToArray();
        if (!actual.SequenceEqual(playbook.Composition, StringComparer.Ordinal))
            throw new InvalidDataException(
                "Tactical playbook composition does not match the resolved team.");
    }

    /// <summary>
    /// Every signature in the rules contract must be known to this executor:
    /// categorized in <see cref="SignaturePlays"/>, owned by dedicated role
    /// logic, or explicitly listed as unwired. An unknown signature (a newly
    /// added class) fails here in the first screening game instead of
    /// silently never casting, and a composition that fields a class with an
    /// unwired kit is refused outright.
    /// </summary>
    private static void AssertSignatureCoverage(
        GenericActorResolvedMatchContract contract,
        int teamId)
    {
        HashSet<string> ownClasses = contract.Topology.UnitSlots
            .Where(slot => slot.TeamId == teamId)
            .Select(slot => slot.ClassId ?? "")
            .ToHashSet(StringComparer.Ordinal);
        foreach (GenericActorRulesContract.ArcRelaySignature signature in
            ArenaBasics.ArcRules(contract)?.Signatures ?? [])
        {
            // A grammar-2 signature carries its own designed-role metadata,
            // which IS the coverage: the generic dispatcher plays it.
            if (signature.Category is not null)
                continue;
            bool categorized = SignaturePlays.Any(play => string.Equals(
                play.Kind, signature.Kind, StringComparison.Ordinal));
            if (!categorized
                && !RoleHandledSignatures.Contains(signature.Kind)
                && !UnwiredSignatures.Contains(signature.Kind))
                throw new InvalidDataException(
                    $"Signature '{signature.Kind}' (class "
                    + $"'{signature.ClassId}') is unknown to this executor. "
                    + "Add it to SignaturePlays, RoleHandledSignatures, or "
                    + "UnwiredSignatures.");
            if (UnwiredSignatures.Contains(signature.Kind)
                && ownClasses.Contains(signature.ClassId))
                throw new InvalidDataException(
                    $"Composition fields class '{signature.ClassId}' but "
                    + $"this executor cannot cast '{signature.Kind}' yet. "
                    + "Wire the signature before fielding the class.");
        }
    }

    private void UpdateMemory(
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        TacticalPlaybookPackage.MemoryPolicy memory)
    {
        HashSet<ActorIdentity> visibleEnemyCarriers = arc.VisibleCores
            .Where(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Carried
                && core.CarrierActorId?.TeamId != _teamId)
            .Select(core => core.CarrierActorId!)
            .ToHashSet();
        foreach (GenericActorContext.ObservedEnemyState enemy in mind.Enemies)
        {
            _enemyUnavailableUntil.Remove(enemy.ActorId.UnitId);
            LastSeenEnemy? prior = _lastSeenEnemies.GetValueOrDefault(
                enemy.ActorId.UnitId);
            bool sameLife = prior?.ActorId == enemy.ActorId;
            _lastSeenEnemies[enemy.ActorId.UnitId] = new LastSeenEnemy(
                enemy.ActorId,
                enemy.Position,
                mind.Tick,
                sameLife ? prior!.Position : null,
                sameLife ? prior!.LastConfirmedTick : null,
                visibleEnemyCarriers.Contains(enemy.ActorId));
            _firstSeenEnemyLife.TryAdd(enemy.ActorId, mind.Tick);
        }
        foreach (GenericActorContext.ObservedEvent observed in mind.VisibleEvents)
        {
            if (!_processedEvents.Add(observed.EventHandle))
                continue;
            if (observed.Payload is GenericActorContext.EventPayload.Destruction death
                && death.ActorId.TeamId != _teamId)
            {
                _observedDestroyedEnemies.Add(death.ActorId);
                _enemyUnavailableUntil[death.ActorId.UnitId] =
                    observed.SourceTick + memory.EnemyUnavailableTicks;
                _lastSeenEnemies.Remove(death.ActorId.UnitId);
            }
            if (observed.Payload is not GenericActorContext.EventPayload.ArcRelay mode)
                continue;
            switch (mode.Fact)
            {
                case GenericActorContext.ArcRelayEvent.LeveledUp level
                    when level.ActorId.TeamId == _teamId:
                    _pendingInvest[level.ActorId.UnitId] =
                        _pendingInvest.GetValueOrDefault(
                            level.ActorId.UnitId) + 1;
                    _unitLevel[level.ActorId.UnitId] =
                        (level.ActorId.LifeId, level.Level);
                    break;
                case GenericActorContext.ArcRelayEvent.CoreDropped drop:
                    _emergencyRecoveries.Remove(CoreKey(drop.CoreId));
                    if (drop.SourceActorId.TeamId != _teamId)
                    {
                        ClearRememberedCarrier(drop.SourceActorId);
                        _securedCores[CoreKey(drop.CoreId)] = new SecuredCore(
                            drop.CoreId.SourceWellId,
                            drop.Position,
                            observed.SourceTick);
                    }
                    else
                    {
                        _friendlyDroppedCores[CoreKey(drop.CoreId)] =
                            new FriendlyDroppedCore(
                                drop.SourceActorId,
                                drop.Position,
                                observed.SourceTick);
                        _custodyProgress.Remove(drop.SourceActorId);
                    }
                    break;
                case GenericActorContext.ArcRelayEvent.CoreBanked banked:
                    _emergencyRecoveries.Remove(CoreKey(banked.CoreId));
                    ClearRememberedCarrier(banked.CarrierActorId);
                    _securedCores.Remove(CoreKey(banked.CoreId));
                    _friendlyDroppedCores.Remove(CoreKey(banked.CoreId));
                    _custodyProgress.Remove(banked.CarrierActorId);
                    if (banked.TeamId == _teamId)
                        _lastObjectiveProgressTick = observed.SourceTick;
                    break;
                case GenericActorContext.ArcRelayEvent.CorePickedUp pickup:
                    _friendlyDroppedCores.Remove(CoreKey(pickup.CoreId));
                    if (pickup.CarrierActorId.TeamId != _teamId)
                    {
                        _emergencyRecoveries.Remove(CoreKey(pickup.CoreId));
                        _securedCores.Remove(CoreKey(pickup.CoreId));
                        MarkRememberedCarrier(
                            pickup.CarrierActorId,
                            pickup.Position,
                            observed.SourceTick);
                    }
                    break;
                case GenericActorContext.ArcRelayEvent.CoreHandedOff handoff:
                    ClearRememberedCarrier(handoff.SourceActorId);
                    if (handoff.TargetActorId.TeamId != _teamId)
                    {
                        MarkRememberedCarrier(
                            handoff.TargetActorId,
                            handoff.Position,
                            observed.SourceTick);
                    }
                    if (_custodyProgress.Remove(
                            handoff.SourceActorId,
                            out CustodyProgress? transferred))
                    {
                        _custodyProgress[handoff.TargetActorId] = transferred with
                        {
                            ActorId = handoff.TargetActorId,
                            Position = handoff.Position,
                            StagnantTicks = 0,
                        };
                    }
                    break;
            }
        }
        foreach (GenericActorContext.ArcRelayCoreState core in arc.VisibleCores)
        {
            string key = CoreKey(core.CoreId);
            if (core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Carried)
            {
                _securedCores.Remove(key);
            }
            else if (_securedCores.TryGetValue(key, out SecuredCore? prior))
            {
                _securedCores[key] = prior with
                {
                    Position = core.Position,
                    LastConfirmedTick = mind.Tick,
                };
            }
        }
        HashSet<Position> visibleTiles = mind.VisibleTiles
            .Select(tile => tile.Position).ToHashSet();
        HashSet<string> visibleCoreIds = arc.VisibleCores
            .Select(core => CoreKey(core.CoreId))
            .ToHashSet(StringComparer.Ordinal);
        foreach (string stale in _securedCores
                     .Where(value => mind.Tick - value.Value.LastConfirmedTick
                             > memory.SecuredCoreTicks
                         || visibleTiles.Contains(value.Value.Position)
                         && !visibleCoreIds.Contains(value.Key))
                     .Select(value => value.Key).ToArray())
        {
            _securedCores.Remove(stale);
        }
        foreach (int stale in _enemyUnavailableUntil
                     .Where(value => mind.Tick >= value.Value)
                     .Select(value => value.Key).ToArray())
            _enemyUnavailableUntil.Remove(stale);
        foreach (int stale in _lastSeenEnemies
                     .Where(value => mind.Tick - value.Value.LastConfirmedTick
                         > memory.LastSeenEnemyTicks)
                     .Select(value => value.Key).ToArray())
            _lastSeenEnemies.Remove(stale);

        GenericActorContext.ArcRelayReactorState own = arc.Reactors.Single(
            value => value.TeamId == _teamId);
        if (own.ChargePips != _lastOwnCharge)
        {
            _lastOwnCharge = own.ChargePips;
            _lastObjectiveProgressTick = mind.Tick;
        }
    }

    private void ClearRememberedCarrier(ActorIdentity actorId)
    {
        if (_lastSeenEnemies.TryGetValue(
                actorId.UnitId, out LastSeenEnemy? remembered)
            && remembered.ActorId == actorId)
        {
            _lastSeenEnemies[actorId.UnitId] = remembered with
            {
                IsCarrier = false,
            };
        }
    }

    private void MarkRememberedCarrier(
        ActorIdentity actorId,
        Position position,
        int tick)
    {
        if (_lastSeenEnemies.TryGetValue(
                actorId.UnitId, out LastSeenEnemy? remembered)
            && remembered.ActorId == actorId)
        {
            _lastSeenEnemies[actorId.UnitId] = remembered with
            {
                Position = position,
                LastConfirmedTick = tick,
                IsCarrier = true,
            };
            return;
        }
        _lastSeenEnemies[actorId.UnitId] = new LastSeenEnemy(
            actorId, position, tick, null, null, true);
    }

    private Dictionary<int, string> AllocateRoles(
        MindContext mind,
        TacticalPlaybookPackage.Playbook playbook,
        bool phaseBoundary)
    {
        TacticalMembershipPrimitives.Candidate[] candidates = mind.Bodies
            .Select(body => new TacticalMembershipPrimitives.Candidate(
                body.UnitId,
                body.ClassId ?? "",
                body.Health,
                _friendlyLives.TryGetValue(
                    body.UnitId, out ActorIdentity? priorLife)
                && priorLife != body.ActorId))
            .ToArray();
        TacticalMembershipPrimitives.RoleRule[] rules = playbook.Roles
            .Select(role =>
            {
                TacticalPlaybookPackage.Group group = playbook.Groups.Single(
                    value => value.RoleIds.Contains(
                        role.RoleId, StringComparer.Ordinal));
                return new TacticalMembershipPrimitives.RoleRule(
                    role.RoleId,
                    group.GroupId,
                    role.CandidateClasses,
                    role.Minimum,
                    role.Preferred,
                    role.Maximum,
                    role.DeathPolicy,
                    role.RespawnPolicy,
                    role.OverflowRoleId,
                    group.Membership.Persistence,
                    group.Membership.Preemption,
                    group.Membership.Overflow);
            })
            .ToArray();
        TacticalMembershipPrimitives.GroupRule[] groupRules = playbook.Groups
            .Select(group => new TacticalMembershipPrimitives.GroupRule(
                group.GroupId,
                group.Minimum,
                group.Preferred,
                group.Maximum))
            .ToArray();
        Dictionary<int, string> result = TacticalMembershipPrimitives.Allocate(
            candidates, rules, groupRules, _stableRoles, phaseBoundary);
        foreach ((int unitId, string role) in result)
            _stableRoles[unitId] = role;
        return result;
    }

    private void UpdateCustodyProgress(
        int tick,
        IReadOnlyCollection<MindBody> bodies,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        HashSet<ActorIdentity> liveCarriers = carried.Keys
            .Where(actor => actor.TeamId == _teamId)
            .ToHashSet();
        foreach (ActorIdentity stale in _custodyProgress.Keys
                     .Where(actor => !liveCarriers.Contains(actor)).ToArray())
        {
            _custodyProgress.Remove(stale);
        }
        foreach ((ActorIdentity actorId,
                     GenericActorContext.ArcRelayCoreState core) in carried)
        {
            if (actorId.TeamId != _teamId)
                continue;
            MindBody body = bodies.Single(value => value.ActorId == actorId);
            string coreKey = CoreKey(core.CoreId);
            CustodyProgress prior = _custodyProgress.GetValueOrDefault(actorId)
                ?? new CustodyProgress(
                    actorId, coreKey, tick, body.Position, 0, body.Position);
            bool sameCustody = string.Equals(
                prior.CoreKey, coreKey, StringComparison.Ordinal);
            _custodyProgress[actorId] = new CustodyProgress(
                actorId,
                coreKey,
                sameCustody ? prior.StartedTick : tick,
                body.Position,
                sameCustody && prior.Position == body.Position
                    ? prior.StagnantTicks + 1
                    : 0,
                sameCustody ? prior.PickupPosition : body.Position,
                // The walked corridor survives the tick; a new Core is a new
                // run and starts its route from scratch.
                sameCustody ? prior.RouteWaypoint : -1,
                sameCustody ? prior.RouteForward : 0);
        }
    }

    private static Dictionary<int, string> GroupMembership(
        IReadOnlyDictionary<int, string> roles,
        TacticalPlaybookPackage.Playbook playbook) => roles.ToDictionary(
            value => value.Key,
            value => playbook.Groups.First(group =>
                group.RoleIds.Contains(value.Value, StringComparer.Ordinal))
                .GroupId);

    private void UpdateFriendlyMembership(
        MindContext mind,
        TacticalPlaybookPackage package,
        TacticalPlaybookMachine machine,
        IReadOnlyDictionary<int, string> roles,
        IReadOnlyDictionary<int, string> groups,
        IReadOnlySet<int> taskLeases)
    {
        bool initialObservation = _friendlyLives.Count == 0;
        foreach (MindBody body in mind.Bodies.OrderBy(value => value.UnitId))
        {
            if (_friendlyLives.TryGetValue(body.UnitId, out ActorIdentity? prior)
                && prior != body.ActorId)
            {
                TacticalPlaybookPackage.Role role = package.Source.Roles.Single(
                    value => value.RoleId == roles[body.UnitId]);
                if (TacticalMembershipPrimitives.JoinsCohort(
                        role.RespawnPolicy))
                    _joiningUnits.Add(body.UnitId);
                else
                    _joiningUnits.Remove(body.UnitId);
            }
            else if (!initialObservation && prior is null)
            {
                _joiningUnits.Add(body.UnitId);
            }
            _friendlyLives[body.UnitId] = body.ActorId;
        }

        foreach (TacticalPlaybookPackage.Group group in package.Source.Groups)
        {
            TacticalPlaybookPackage.Formation formation = ActiveFormation(
                package, machine, group.GroupId);
            MindBody[] members = mind.Bodies
                .Where(body => groups[body.UnitId] == group.GroupId)
                .Where(body => !taskLeases.Contains(body.UnitId))
                .OrderBy(body => body.UnitId)
                .ToArray();
            var established = members
                .Where(body => !_joiningUnits.Contains(body.UnitId))
                .Select(body => body.Position)
                .ToList();
            MindBody[] joining = members
                .Where(body => _joiningUnits.Contains(body.UnitId))
                .ToArray();

            // A replacement rejoins the tactical cohort only after physically
            // reaching it. It must never pull an established formation back to
            // the spawn simply because it inherited the same stable role.
            foreach (MindBody replacement in joining)
            {
                if (!established.Any(position => position.ChebyshevDistance(
                        replacement.Position) <= formation.Spacing.Maximum))
                {
                    continue;
                }
                _joiningUnits.Remove(replacement.UnitId);
                established.Add(replacement.Position);
            }

            // If the entire group was destroyed there is no surviving cohort
            // to meet. In that case its minimum viable replacement body may
            // establish a new cohort once the replacements are mutually
            // coherent; this prevents a permanent joining limbo after a wipe.
            if (established.Count == 0
                && joining.Length >= group.Minimum
                && CohesionPercent(
                    joining.Select(body => body.Position).ToArray(),
                    formation.Spacing.Maximum)
                    >= formation.Cohesion.ArrivalRatioPercent)
            {
                foreach (MindBody replacement in joining)
                    _joiningUnits.Remove(replacement.UnitId);
            }
        }
    }

    private TacticalSnapshot Snapshot(
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        TacticalPlaybookPackage package,
        TacticalPlaybookMachine machine,
        IReadOnlyDictionary<int, string> roles,
        IReadOnlyDictionary<int, string> groups,
        IReadOnlySet<int> taskLeases,
        bool updateFormationState)
    {
        var groupLive = package.Source.Groups.ToDictionary(
            group => group.GroupId,
            group => groups.Count(value => value.Value == group.GroupId),
            StringComparer.Ordinal);
        var groupJoining = package.Source.Groups.ToDictionary(
            group => group.GroupId,
            group => groups.Count(value => value.Value == group.GroupId
                && _joiningUnits.Contains(value.Key)),
            StringComparer.Ordinal);
        var groupCohesion = package.Source.Groups.ToDictionary(
            group => group.GroupId,
            group => CohesionPercent(
                mind.Bodies.Where(body => groups[body.UnitId] == group.GroupId
                        && !_joiningUnits.Contains(body.UnitId)
                        && !taskLeases.Contains(body.UnitId))
                    .Select(body => body.Position).ToArray(),
                ActiveFormation(package, machine, group.GroupId)
                    .Spacing.Maximum),
            StringComparer.Ordinal);
        var groupStuckTicks = package.Source.Groups.ToDictionary(
            group => group.GroupId,
            group => mind.Bodies
                .Where(body => groups[body.UnitId] == group.GroupId)
                .Where(body => !taskLeases.Contains(body.UnitId))
                .Select(body => _motion.GetValueOrDefault(body.UnitId))
                .Where(progress => progress is not null)
                .Select(progress => progress!.StuckTicks)
                .DefaultIfEmpty(0)
                .Max(),
            StringComparer.Ordinal);
        if (updateFormationState)
        {
            foreach (TacticalPlaybookPackage.Group group in package.Source.Groups)
            {
                TacticalPlaybookPackage.Formation formation = ActiveFormation(
                    package, machine, group.GroupId);
                if (!string.Equals(
                        _activeFormationIds.GetValueOrDefault(group.GroupId),
                        formation.FormationId,
                        StringComparison.Ordinal))
                {
                    _activeFormationIds[group.GroupId] = formation.FormationId;
                    _formationLifecycles[group.GroupId] = default;
                }
                int prior = _formationStableTicks.GetValueOrDefault(group.GroupId);
                _formationStableTicks[group.GroupId] =
                    groupCohesion[group.GroupId]
                        >= formation.Cohesion.ArrivalRatioPercent
                        ? Math.Min(
                            prior + 1,
                            package.Source.Memory.FormationStableTicks)
                        : 0;
                _formationLifecycles[group.GroupId] =
                    TacticalFormationPrimitives.AdvanceLifecycle(
                        _formationLifecycles.GetValueOrDefault(group.GroupId),
                        groupCohesion[group.GroupId],
                        formation.Cohesion.BreakRatioPercent,
                        formation.Cohesion.BreakTicks,
                        formation.Cohesion.ArrivalRatioPercent,
                        formation.Cohesion.ReformTicks);
            }
        }
        Dictionary<string, Dictionary<string, int>> groupZones =
            package.Source.Groups.ToDictionary(
                group => group.GroupId,
                group => package.LayoutSource.Zones.ToDictionary(
                    zone => zone.ZoneId,
                    zone => mind.Bodies.Count(body =>
                        groups[body.UnitId] == group.GroupId
                        && package.Contains(zone.ZoneId, body.Position)),
                    StringComparer.Ordinal),
                StringComparer.Ordinal);
        Dictionary<string, int> friendlyZones =
            package.LayoutSource.Zones.ToDictionary(
                zone => zone.ZoneId,
                zone => mind.Bodies.Count(body =>
                    package.Contains(zone.ZoneId, body.Position)),
                StringComparer.Ordinal);
        Dictionary<string, int> visibleEnemiesByZone =
            package.LayoutSource.Zones.ToDictionary(
                zone => zone.ZoneId,
                zone => mind.Enemies.Count(enemy =>
                    package.Contains(zone.ZoneId, enemy.Position)),
                StringComparer.Ordinal);
        Dictionary<string, int> rememberedEnemiesByZone =
            package.LayoutSource.Zones.ToDictionary(
                zone => zone.ZoneId,
                zone => _lastSeenEnemies.Count(enemy =>
                    package.Contains(zone.ZoneId, enemy.Value.Position)),
                StringComparer.Ordinal);
        Dictionary<string, int> visibleLooseCoresByZone =
            package.LayoutSource.Zones.ToDictionary(
                zone => zone.ZoneId,
                zone => arc.VisibleCores.Count(core =>
                    core.Disposition
                        == GenericActorContext.ArcRelayCoreDisposition.Loose
                    && package.Contains(zone.ZoneId, core.Position)),
                StringComparer.Ordinal);
        Dictionary<string, int> looseCoreValueByZone =
            package.LayoutSource.Zones.ToDictionary(
                zone => zone.ZoneId,
                zone => arc.VisibleCores
                    .Where(core =>
                        core.Disposition
                            == GenericActorContext.ArcRelayCoreDisposition.Loose
                        && package.Contains(zone.ZoneId, core.Position))
                    .Select(core => core.ChargeValue)
                    .DefaultIfEmpty(0)
                    .Max(),
                StringComparer.Ordinal);
        int carriers = arc.VisibleCores.Count(core =>
            core.Disposition == GenericActorContext.ArcRelayCoreDisposition.Carried
            && core.CarrierActorId?.TeamId == _teamId);
        int enemyCarriers = arc.VisibleCores.Count(core =>
            core.Disposition == GenericActorContext.ArcRelayCoreDisposition.Carried
            && core.CarrierActorId?.TeamId != _teamId);
        GenericActorContext.ArcRelayReactorState own = arc.Reactors.Single(
            reactor => reactor.TeamId == _teamId);
        GenericActorContext.ArcRelayReactorState enemyReactorState =
            arc.Reactors.Single(reactor => reactor.TeamId != _teamId);
        Dictionary<string, int> ownSocketFilled = arc.Wells.ToDictionary(
            well => well.WellId,
            well => own.FilledSocketWellIds.Contains(
                well.WellId, StringComparer.Ordinal) ? 1 : 0,
            StringComparer.Ordinal);
        Dictionary<string, int> enemySocketFilled = arc.Wells.ToDictionary(
            well => well.WellId,
            well => enemyReactorState.FilledSocketWellIds.Contains(
                well.WellId, StringComparer.Ordinal) ? 1 : 0,
            StringComparer.Ordinal);
        return new TacticalSnapshot(
            mind.Tick,
            Math.Max(0, mind.Tick - machine.PhaseEnteredTick),
            mind.Bodies.Length,
            _enemyUnavailableUntil.Count,
            _securedCores.Count,
            arc.VisibleCores.Count(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Loose),
            carriers,
            enemyCarriers,
            _lastSeenEnemies.Count(enemy => enemy.Value.IsCarrier),
            Math.Min(
                Math.Max(0, mind.Tick - _lastObjectiveProgressTick),
                package.Source.Memory.ObjectiveProgressTicks),
            own.IntegritySegments,
            own.ChargePips,
            roles.Values.GroupBy(value => value, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(),
                    StringComparer.Ordinal),
            groupLive,
            groupJoining,
            groupCohesion,
            groupStuckTicks,
            friendlyZones,
            groupZones,
            visibleEnemiesByZone,
            rememberedEnemiesByZone,
            visibleLooseCoresByZone,
            arc.VisibleCores
                .Where(core => core.Disposition
                    == GenericActorContext.ArcRelayCoreDisposition.Loose)
                .Select(core => core.ChargeValue)
                .DefaultIfEmpty(0)
                .Max(),
            looseCoreValueByZone,
            arc.Wells.ToDictionary(
                well => well.WellId,
                well => well.OutstandingCoreId is null ? 0 : 1,
                StringComparer.Ordinal),
            _formationStableTicks.ToDictionary(StringComparer.Ordinal),
            _formationLifecycles.ToDictionary(
                value => value.Key,
                value => value.Value.Broken ? 1 : 0,
                StringComparer.Ordinal),
            ownSocketFilled,
            enemySocketFilled,
            arc.Wells.ToDictionary(
                well => well.WellId,
                well => well.NextScheduledBirthTick is int birth
                    ? Math.Max(0, birth - mind.Tick)
                    : 9999,
                StringComparer.Ordinal),
            // Highest current level among a group's LIVE bodies: level is a
            // per-life fact, so a body whose recorded level belongs to an
            // earlier life reads as 1, and an empty group reads as 0 - an
            // apex phase entered on "at-least N" therefore releases when
            // the apex dies.
            package.Source.Groups.ToDictionary(
                group => group.GroupId,
                group => mind.Bodies
                    .Where(body => groups[body.UnitId] == group.GroupId)
                    .Select(body =>
                        _unitLevel.TryGetValue(
                            body.UnitId,
                            out (int LifeId, int Level) entry)
                        && entry.LifeId == body.ActorId.LifeId
                            ? entry.Level
                            : 1)
                    .DefaultIfEmpty(0)
                    .Max(),
                StringComparer.Ordinal));
    }

    /// <summary>How near a beacon counts as "already there" for recover.
    /// </summary>
    private const int RecoverReach = 8;

    /// <summary>How wide a berth a beacon (and its approach) must have from
    /// every enemy the team can see or still believes in.</summary>
    private const int RecoverClearance = 4;

    /// <summary>What counts as enemy CONTACT for the body itself: an enemy
    /// merely visible at range is not contact, and a body is not forbidden a
    /// beacon because someone is watching from across the map.</summary>
    private const int RecoverContact = 2;

    /// <summary>How recent a sighting has to be to still count against a
    /// beacon. Without this the clause is archaeology, not intelligence:
    /// the heal tiles sit on the centre line every race crosses, so the
    /// team remembers SOMEBODY near them for the rest of the match and no
    /// beacon is ever cold again (measured: recover armed zero times).
    /// </summary>
    private const int RecoverMemoryTicks = 12;

    /// <summary>
    /// The recover predicate (owner spec 2026-08-07), evaluated once per
    /// tick for every own body. A body qualifies when it is HURT, a heal
    /// beacon is COLD, and either that beacon is within
    /// <see cref="RecoverReach"/> or the body is one hit from dead - which is
    /// worth any walk. There is no separate "stop" rule: healing to full
    /// clears the first clause and an enemy arriving clears the second, so
    /// "hold until full or enemy contact" falls out of the predicate itself
    /// and the generated recover task fails the tick it empties.
    /// </summary>
    private void UpdateRecovering(
        GenericActorResolvedMatchContract contract,
        MindContext mind)
    {
        _recovering.Clear();
        _recoverTrace.Clear();
        foreach (MindBody body in mind.Bodies)
        {
            if (body.Health >= MaxHealth(contract, body))
                continue;
            if (RecoverBeacon(mind, body) is not Position beacon)
            {
                _recoverTrace.Add($"{body.UnitId}:"
                    + (mind.Enemies.Any(enemy =>
                            enemy.Position.ChebyshevDistance(body.Position)
                                <= RecoverContact)
                        ? "contact" : "watched"));
                continue;
            }
            if (body.Position.ChebyshevDistance(beacon) > RecoverReach
                && body.Health > 1)
            {
                _recoverTrace.Add($"{body.UnitId}:far");
                continue;
            }
            _recovering.Add(body.UnitId);
            // Name the beacon and its heat. A choice nobody can see is a
            // choice nobody can check, and this exact line is the evidence
            // that a desperate body still walks to the COLD one.
            _recoverTrace.Add(
                $"{body.UnitId}:ready@{beacon.X}-{beacon.Y}"
                + $"h{BeaconHeat(mind, beacon)}"
                + $"/{string.Join("|", _healTiles
                    .Select(tile => $"{tile.X}-{tile.Y}h{BeaconHeat(mind, tile)}"))}");
        }
    }

    /// <summary>
    /// The safest beacon for this body, or null when none is safe.
    ///
    /// <para>Two separate questions live here and used to be one. ARMING
    /// asks whether recover may fire at all: normally no, while an enemy
    /// is within <see cref="RecoverContact"/> of the body or every beacon
    /// is hot, because channelling is stationary and a watched beacon is a
    /// trap rather than a refuge. One hit from dead, every arming gate
    /// comes off (owner ruling 2026-08-07): a body at 1 HP standing in a
    /// duel is already dead, so it should TRY.</para>
    ///
    /// <para>CHOICE is the other question, and desperation never relaxed
    /// it — it only ever picked the NEAREST beacon (owner catch
    /// 2026-08-07: a recovering ghost walked to the north beacon with five
    /// enemies around it while a clean one stood open). Choice weighs
    /// safety first, always: fewest enemies known near the beacon — seen
    /// now or remembered fresh — then the shortest WALK to it, then the
    /// mind's mirrored frame so both sides of the map choose alike. A
    /// beacon the map cannot reach is not a choice at all. Nearest-hot
    /// wins only when every beacon is equally hot.</para>
    /// </summary>
    private Position? RecoverBeacon(MindContext mind, MindBody body)
    {
        if (_healTiles.Length == 0 || _contract is not { } contract)
            return null;
        bool desperate = body.Health <= 1;
        if (!desperate
            && mind.Enemies.Any(enemy =>
                enemy.Position.ChebyshevDistance(body.Position)
                    <= RecoverContact))
        {
            return null;
        }
        return _healTiles
            .Select(tile => (
                Tile: tile,
                Heat: BeaconHeat(mind, tile),
                Walk: ArenaBasics.StaticDistance(
                    contract.Map, body.Position, tile)))
            .Where(candidate => candidate.Walk is not null
                && (desperate || candidate.Heat == 0))
            .OrderBy(candidate => candidate.Heat)
            .ThenBy(candidate => candidate.Walk)
            .ThenBy(candidate => ArenaBasics.FrameY(candidate.Tile, _mirrored))
            .ThenBy(candidate => ArenaBasics.FrameX(candidate.Tile, _mirrored))
            .Select(candidate => (Position?)candidate.Tile)
            .FirstOrDefault();
    }

    /// <summary>
    /// How many distinct enemy units the team places within
    /// <see cref="RecoverClearance"/> of a beacon — visible right now, or
    /// remembered from a sighting no older than
    /// <see cref="RecoverMemoryTicks"/>. Zero is COLD. Counting units
    /// rather than sources keeps one enemy that is both seen and
    /// remembered from reading as a crowd.
    /// </summary>
    private int BeaconHeat(MindContext mind, Position tile)
    {
        var near = mind.Enemies
            .Where(enemy => enemy.Position.ChebyshevDistance(tile)
                <= RecoverClearance)
            .Select(enemy => enemy.ActorId.UnitId)
            .ToHashSet();
        foreach ((int unit, LastSeenEnemy enemy) in _lastSeenEnemies)
        {
            if (mind.Tick - enemy.LastConfirmedTick <= RecoverMemoryTicks
                && enemy.Position.ChebyshevDistance(tile) <= RecoverClearance)
            {
                near.Add(unit);
            }
        }
        return near.Count;
    }

    private static int CohesionPercent(Position[] positions, int maximumSpacing)
    {
        if (positions.Length <= 1)
            return positions.Length * 100;
        int largest = 0;
        foreach (Position start in positions)
        {
            var reached = new HashSet<Position> { start };
            var queue = new Queue<Position>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                Position current = queue.Dequeue();
                foreach (Position candidate in positions.Where(candidate =>
                             !reached.Contains(candidate)
                             && current.ChebyshevDistance(candidate)
                                 <= maximumSpacing))
                {
                    reached.Add(candidate);
                    queue.Enqueue(candidate);
                }
            }
            largest = Math.Max(largest, reached.Count);
        }
        return largest * 100 / positions.Length;
    }

    private static TacticalPlaybookPackage.Formation ActiveFormation(
        TacticalPlaybookPackage package,
        TacticalPlaybookMachine machine,
        string groupId)
    {
        string local = machine.LocalState(groupId);
        TacticalPlaybookPackage.Order order = machine.Phase.OrderIds
            .Select(id => package.Source.Orders.Single(
                value => value.OrderId == id))
            .Where(value => value.GroupId == groupId)
            .OrderBy(value => value.LocalState == local ? 0 : 1)
            .ThenBy(value => value.Priority)
            .First();
        return package.ResolveFormation(package.Source, order.FormationId);
    }

    private bool Evaluate(
        TacticalPlaybookPackage.Condition condition,
        TacticalSnapshot snapshot,
        TacticalPlaybookPackage package)
    {
        int actual = condition.Fact switch
        {
            "always" => 1,
            // The recover verb's gate: how many own bodies the recover
            // predicate flags right now (UpdateRecovering). Body-scoped
            // facts - this body's health, its distance to a beacon - cannot
            // be said in the team-level condition grammar, so the mind
            // answers the whole question and the sheet only counts. A
            // subject narrows the count to one role, which is what a
            // doctrine wants: arming the ghost's recover on a wounded
            // HAULER would trigger a task no row could ever fill.
            "recover-ready-bodies" => condition.Subject.Length == 0
                ? _recovering.Count
                : _recovering.Count(unit => string.Equals(
                    _stableRoles.GetValueOrDefault(unit),
                    condition.Subject,
                    StringComparison.Ordinal)),
            "tick" => snapshot.Tick,
            "phase-state-ticks" => snapshot.PhaseStateTicks,
            "live-friendlies" => snapshot.LiveFriendlies,
            "friendlies-in-zone-count" => snapshot.FriendlyZones
                .GetValueOrDefault(condition.Zone),
            "group-live-count" => snapshot.GroupLive
                .GetValueOrDefault(condition.Subject),
            "group-joining-count" => snapshot.GroupJoining
                .GetValueOrDefault(condition.Subject),
            "group-in-zone-count" => snapshot.GroupZones
                .GetValueOrDefault(condition.Subject)?
                .GetValueOrDefault(condition.Zone) ?? 0,
            "group-cohesion" => snapshot.GroupCohesion
                .GetValueOrDefault(condition.Subject),
            "group-stuck-ticks" => snapshot.GroupStuckTicks
                .GetValueOrDefault(condition.Subject),
            "known-enemies-unavailable" => snapshot.KnownEnemiesUnavailable,
            "visible-enemies-in-zone" => snapshot.VisibleEnemiesByZone
                .GetValueOrDefault(condition.Zone),
            "remembered-enemies-in-zone" => condition.FreshnessTicks == 0
                ? snapshot.RememberedEnemiesByZone
                    .GetValueOrDefault(condition.Zone)
                : _lastSeenEnemies.Count(enemy =>
                    snapshot.Tick - enemy.Value.LastConfirmedTick
                        <= condition.FreshnessTicks
                    && package.Contains(
                        condition.Zone, enemy.Value.Position)),
            "visible-enemy-carriers" => snapshot.VisibleEnemyCarriers,
            "known-enemy-carriers" => snapshot.KnownEnemyCarriers,
            "friendly-carriers" => snapshot.FriendlyCarriers,
            "secured-cores" => condition.FreshnessTicks == 0
                ? snapshot.SecuredCores
                : _securedCores.Count(core =>
                    snapshot.Tick - core.Value.LastConfirmedTick
                        <= condition.FreshnessTicks),
            "visible-loose-cores" => snapshot.VisibleLooseCores,
            "visible-loose-cores-in-zone" => snapshot.VisibleLooseCoresByZone
                .GetValueOrDefault(condition.Zone),
            "visible-loose-core-value" => snapshot.LooseCoreValueMax,
            "visible-loose-core-value-in-zone" => snapshot.LooseCoreValueByZone
                .GetValueOrDefault(condition.Zone),
            "well-has-outstanding" => snapshot.WellOutstanding
                .GetValueOrDefault(condition.Subject),
            "outstanding-well-count" => snapshot.WellOutstanding.Values.Sum(),
            "ticks-without-objective-progress" =>
                snapshot.TicksWithoutObjectiveProgress,
            "reactor-integrity" => snapshot.ReactorIntegrity,
            "reactor-charge" => snapshot.ReactorCharge,
            "formation-established-ticks" => snapshot.FormationStableTicks
                .GetValueOrDefault(condition.Subject),
            "group-formation-broken" => snapshot.FormationBroken
                .GetValueOrDefault(condition.Subject),
            "movement-complete" => _orderCompletion
                .GetValueOrDefault(condition.Subject),
            "custody-state-ticks" => _custodyProgress.Count == 0
                ? 0
                : _custodyProgress.Values.Max(value =>
                    snapshot.Tick - value.StartedTick),
            "role-live-count" => snapshot.RoleLive
                .GetValueOrDefault(condition.Subject),
            "group-max-level" => snapshot.GroupMaxLevel
                .GetValueOrDefault(condition.Subject),
            // Threefold sockets: subjects are the contract's absolute well
            // ids, exactly like custody sourceWells.
            "own-socket-filled" => snapshot.OwnSocketFilled
                .GetValueOrDefault(condition.Subject),
            "enemy-socket-filled" => snapshot.EnemySocketFilled
                .GetValueOrDefault(condition.Subject),
            "own-filled-sockets" => snapshot.OwnSocketFilled.Values.Sum(),
            "enemy-filled-sockets" => snapshot.EnemySocketFilled.Values.Sum(),
            // Ticks until the subject Well's next scheduled birth (9999 when
            // exhausted). The jittered schedule is public, so leading it is
            // legal strategy, not an oracle.
            "well-ticks-until-birth" => snapshot.WellBirthIn
                .GetValueOrDefault(condition.Subject, 9999),
            _ => throw new InvalidDataException(
                $"Unknown tactical fact '{condition.Fact}'."),
        };
        _ = package;
        return condition.Operator switch
        {
            "at-least" => actual >= condition.Value,
            "at-most" => actual <= condition.Value,
            "equals" => actual == condition.Value,
            "less-than" => actual < condition.Value,
            "greater-than" => actual > condition.Value,
            _ => throw new InvalidDataException(
                $"Unknown tactical operator '{condition.Operator}'."),
        };
    }

    private Dictionary<int, TacticalPlaybookPackage.Order> ActiveOrders(
        MindContext mind,
        TacticalPlaybookPackage.Playbook playbook,
        TacticalPlaybookMachine machine,
        TacticalTaskMachine tasks,
        IReadOnlyDictionary<int, string> roles,
        IReadOnlyDictionary<int, string> groups)
    {
        TacticalPlaybookPackage.Order[] phaseOrders = machine.Phase.OrderIds
            .Select(id => playbook.Orders.Single(value => value.OrderId == id))
            .ToArray();
        Dictionary<string, TacticalPlaybookPackage.Order> ordersById =
            playbook.Orders.ToDictionary(
                value => value.OrderId,
                StringComparer.Ordinal);
        var result = new Dictionary<int, TacticalPlaybookPackage.Order>();
        foreach (IGrouping<string, MindBody> group in mind.Bodies
                     .OrderBy(body => body.UnitId)
                     .GroupBy(body => groups[body.UnitId],
                         StringComparer.Ordinal))
        {
            bool hasJoiningOrder = phaseOrders.Any(order => string.Equals(
                    order.GroupId, group.Key, StringComparison.Ordinal)
                && string.Equals(
                    order.LocalState, "joining", StringComparison.Ordinal));
            foreach (IGrouping<string, MindBody> cohort in group.GroupBy(body =>
                         hasJoiningOrder && _joiningUnits.Contains(body.UnitId)
                             ? "joining"
                             : machine.LocalState(group.Key),
                         StringComparer.Ordinal))
            {
                TacticalPlaybookPackage.Order[] candidates = phaseOrders
                    .Where(order => string.Equals(
                            order.GroupId, group.Key, StringComparison.Ordinal)
                        && string.Equals(
                            order.LocalState,
                            cohort.Key,
                            StringComparison.Ordinal))
                    .ToArray();
                IReadOnlyDictionary<int, string> assignments =
                    TacticalDetachmentPrimitives.Assign(
                        cohort.Select(body =>
                            new TacticalDetachmentPrimitives.Member(
                                body.UnitId,
                                roles[body.UnitId],
                                body.ClassId ?? "")),
                        candidates.Select(order =>
                            new TacticalDetachmentPrimitives.Selection(
                                order.OrderId,
                                order.Priority,
                                order.Members.Kind,
                                order.Members.Roles ?? [],
                                order.Members.Classes ?? [],
                                order.Members.Count ?? 0)));
                foreach ((int unitId, string orderId) in assignments)
                    result.Add(unitId, ordersById[orderId]);
            }
        }
        foreach (MindBody body in mind.Bodies)
        {
            TacticalTaskDirective? directive = tasks.DirectiveFor(body.UnitId);
            if (directive is not null)
                result[body.UnitId] = ordersById[directive.OrderId];
        }
        return result;
    }

    private int TaskSelectionDistance(
        TacticalPlaybookPackage package,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        TacticalPlaybookPackage.TaskAssignment assignment,
        TacticalTaskCandidate candidate) => assignment.Distance.Kind switch
    {
        "none" => 0,
        // Merit promotion (owner doctrine 2026-08-07): the assassin is any
        // body at the promotion level or above, whatever class it started
        // as. Encoded as 10 - level so the assignment's distance maximum is
        // the gate (maximum 7 admits level 3+) and ordering still prefers
        // the most-leveled candidate.
        "veterancy-rank" => 10 - (_unitLevel.TryGetValue(
                candidate.UnitId, out (int LifeId, int Level) level)
            && level.LifeId == candidate.ActorId.LifeId
                ? level.Level
                : 1),
        // The recover row admits only a body the recover predicate already
        // flagged this tick; among those it takes the one nearest a beacon.
        // A body that must not recover is unreachably far - the same idiom
        // the loose-core selector below uses for "no such thing".
        "heal-beacon" => _recovering.Contains(candidate.UnitId)
            ? _healTiles
                .Select(tile => candidate.Position.ChebyshevDistance(tile))
                .DefaultIfEmpty(int.MaxValue)
                .Min()
            : int.MaxValue,
        "anchor" => candidate.Position.ChebyshevDistance(
            package.AnchorPosition(assignment.Distance.Target)),
        "own-reactor" => candidate.Position.ChebyshevDistance(_ownReactor),
        "enemy-reactor" => candidate.Position.ChebyshevDistance(_enemyReactor),
        "visible-loose-core-in-zone" => arc.VisibleCores
            .Where(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Loose
                && package.Contains(assignment.Distance.Target, core.Position))
            .Select(core => candidate.Position.ChebyshevDistance(core.Position))
            .DefaultIfEmpty(int.MaxValue)
            .Min(),
        _ => throw new InvalidDataException(
            $"Unknown task selection distance '{assignment.Distance.Kind}'."),
    };

    private Position Target(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        TacticalPlaybookPackage package,
        TacticalPlaybookMachine machine,
        IReadOnlyDictionary<int, string> roles,
        IReadOnlyDictionary<int, string> groups,
        IReadOnlyDictionary<int, TacticalPlaybookPackage.Order> orders,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried,
        TacticalPlaybookPackage.Order order,
        MindBody body)
    {
        Position anchor = order.Movement.Kind switch
        {
            "route" => RouteTarget(contract, package, order, body),
            "zone" => package.ZoneCenter(order.Movement.Target),
            "anchor" => package.AnchorPosition(order.Movement.Target),
            "reactor" => order.Movement.Target == "own"
                ? _ownReactor : _enemyReactor,
            "carrier" => CarrierTarget(
                mind, package, groups, orders, carried, order, body),
            "enemy-carrier" => EnemyCarrierTarget(
                package, carried, order),
            "enemy-carrier-cutoff" => EnemyCarrierCutoffTarget(
                contract, package, carried, order, body),
            "shadow-traffic" => ShadowTrafficTarget(
                contract, mind, package, order, body),
            "incoming-cutoff" => IncomingCutoffTarget(
                contract, mind, package, order, body),
            "secured-core" => SecuredCoreTarget(
                package, order),
            "heal-beacon" => RecoverBeacon(mind, body) ?? body.Position,
            "hold" => body.Position,
            _ => throw new InvalidDataException(
                $"Unknown movement kind '{order.Movement.Kind}'."),
        };
        if (order.Movement.Kind == "route"
            && _routes.GetValueOrDefault(body.UnitId) is RouteProgress progress
            && progress.Index < package.RoutePoints(order.Movement.Target).Length - 1)
        {
            // A route is the shared marching spine. Relative formation slots
            // engage only on its final approach; applying rear offsets to the
            // first waypoint can legitimately point a rear body back through
            // its own spawn instead of keeping the column together.
            return anchor;
        }
        if (order.Movement.Kind == "heal-beacon")
        {
            // The beacon is a single tile that heals; a formation offset
            // would park the body politely NEXT to the thing it came for.
            return anchor;
        }
        if (order.Movement.Kind == "enemy-carrier-cutoff"
            && anchor != EnemyCarrierTarget(package, carried, order))
        {
            // The interceptor can reach this predicted lane tile no later
            // than the carrier. It is already a complete point target; a
            // formation offset would move the body past the interception.
            return anchor;
        }
        TacticalPlaybookPackage.Formation formation = package.ResolveFormation(
            package.Source, order.FormationId);
        string role = roles[body.UnitId];
        TacticalPlaybookPackage.Placement placement = FormationPlacement(
            formation, roles, body.UnitId, role);
        _ = contract;
        _ = machine;
        return package.FormationPosition(anchor, placement.Offset);
    }

    /// <summary>The slot this body stands on. An order with no formation
    /// plane has no slots at all: the body stands on its own target, which
    /// is offset zero.</summary>
    private TacticalPlaybookPackage.Placement FormationPlacement(
        TacticalPlaybookPackage.Formation formation,
        IReadOnlyDictionary<int, string> roles,
        int unitId,
        string role)
    {
        TacticalPlaybookPackage.Placement[] placements = formation.Placements
            .Where(value => value.RoleId == role)
            .OrderBy(value => value.Order).ToArray();
        if (placements.Length == 0)
            return new TacticalPlaybookPackage.Placement(role, "centre", 0, [0, 0]);
        int ordinal = TacticalFormationPrimitives.FormationOrdinal(
            unitId,
            role,
            roles,
            _stableRoles,
            formation.Reflow.Vacancy,
            placements.Length);
        return placements[Math.Min(ordinal, placements.Length - 1)];
    }

    private Position EnemyCarrierTarget(
        TacticalPlaybookPackage package,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried,
        TacticalPlaybookPackage.Order order)
    {
        Position fallback = package.AnchorPosition(order.Movement.Target);
        TacticalCoordinationPrimitives.EnemyCarrierCandidate? selected =
            TacticalCoordinationPrimitives.SelectEnemyCarrier(
                carried
                    .Where(value => value.Key.TeamId != _teamId)
                    .Select(value =>
                        new TacticalCoordinationPrimitives
                            .EnemyCarrierCandidate(
                                value.Key, value.Value.Position))
                    .Concat(_lastSeenEnemies.Values
                        .Where(value => value.IsCarrier)
                        .Select(value => new TacticalCoordinationPrimitives
                            .EnemyCarrierCandidate(
                                value.ActorId, value.Position)))
                    .DistinctBy(value => value.ActorId),
                fallback,
                _enemyReactor,
                order.Movement.ChaseLeash);
        return selected?.Position ?? fallback;
    }

    private Position EnemyCarrierCutoffTarget(
        GenericActorResolvedMatchContract contract,
        TacticalPlaybookPackage package,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried,
        TacticalPlaybookPackage.Order order,
        MindBody interceptor)
    {
        Position fallback = package.AnchorPosition(order.Movement.Target);
        TacticalCoordinationPrimitives.EnemyCarrierCandidate? selected =
            TacticalCoordinationPrimitives.SelectEnemyCarrier(
                EnemyCarrierCandidates(carried),
                fallback,
                _enemyReactor,
                order.Movement.ChaseLeash);
        if (selected is not { } carrier)
        {
            // Stalk (owner doctrine 2026-08-07: "it needs to actually hunt
            // and look for ambushes"): no known carrier does not mean no
            // prey. Take the nearest remembered enemy that has no ally
            // within support range - the same isolation discipline the
            // strike obeys - and go set up on it; the perch is only for a
            // truly empty memory.
            Position? prey = _lastSeenEnemies.Values
                .Where(enemy => !_lastSeenEnemies.Values.Any(ally =>
                    ally.ActorId != enemy.ActorId
                    && ally.Position.ChebyshevDistance(enemy.Position) <= 4))
                .OrderBy(enemy => interceptor.Position
                    .ChebyshevDistance(enemy.Position))
                .Select(enemy => (Position?)enemy.Position)
                .FirstOrDefault();
            return prey ?? fallback;
        }
        Position cutoff = TacticalCoordinationPrimitives
            .PredictReturnLaneCutoff(
                _mirrored,
            carrier.Position,
            carrier.PreviousPosition,
            order.Movement.LeadTiles,
            position => ArenaBasics.StaticDistance(
                contract.Map, position, _enemyReactor),
            (from, to) => to.ChebyshevDistance(fallback)
                    <= order.Movement.ChaseLeash
                && ArenaBasics.IsLegalTerrainStep(contract.Map, from, to));
        int? cutoffDistance = ArenaBasics.StaticDistance(
            contract.Map, interceptor.Position, cutoff);
        return cutoff != carrier.Position
            && cutoffDistance is not null
            && cutoffDistance <= order.Movement.LeadTiles
                ? cutoff
                : carrier.Position;
    }

    /// <summary>
    /// Traffic shadowing (ghost doctrine v2, owner design): patrol the
    /// corridors the enemy actually walks - for each Well, the point a
    /// third of the way along its static route to the ENEMY reactor, which
    /// is where their collectors and escorts stream from and back to. The
    /// waypoints are computed once from the contract (map, wells, reactor)
    /// and toured as a loop; the movement target names the fallback anchor
    /// for a map with no wells.
    /// </summary>
    private Position ShadowTrafficTarget(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        TacticalPlaybookPackage package,
        TacticalPlaybookPackage.Order order,
        MindBody body)
    {
        // Patrol and interception are one job (owner design): the body
        // walks the enemy's traffic corridors, and the moment its memory
        // shows someone inbound toward our half it moves on THEM instead
        // of the next waypoint.
        if (FindInboundCutoff(contract, mind, order, body)
            is Position inbound)
        {
            return inbound;
        }
        _trafficWaypoints ??= ComputeTrafficWaypoints(contract, mind);
        if (_trafficWaypoints.Length == 0)
            return package.AnchorPosition(order.Movement.Target);
        (int LifeId, int Index) patrol =
            _patrols.GetValueOrDefault(body.UnitId);
        if (patrol.LifeId != body.ActorId.LifeId)
            patrol = (body.ActorId.LifeId, 0);
        Position waypoint =
            _trafficWaypoints[patrol.Index % _trafficWaypoints.Length];
        if (body.Position.ChebyshevDistance(waypoint)
            <= order.Movement.ArrivalRadius)
        {
            patrol = (patrol.LifeId,
                (patrol.Index + 1) % _trafficWaypoints.Length);
            waypoint = _trafficWaypoints[patrol.Index];
        }
        _patrols[body.UnitId] = patrol;
        return waypoint;
    }

    private Position[] ComputeTrafficWaypoints(
        GenericActorResolvedMatchContract contract,
        MindContext mind)
    {
        GenericActorContext.ModeObservationState.ArcRelay? arc =
            ArenaBasics.ArcState(mind);
        if (arc is null)
            return [];
        var waypoints = new List<Position>();
        foreach (GenericActorContext.ArcRelayWellState well in arc.Wells
                     .OrderBy(value => ArenaBasics.FrameY(
                         value.Position, _mirrored))
                     .ThenBy(value => ArenaBasics.FrameX(
                         value.Position, _mirrored)))
        {
            int? total = ArenaBasics.StaticDistance(
                contract.Map, well.Position, _enemyReactor);
            if (total is not int distance || distance <= 2)
                continue;
            int steps = Math.Max(1, distance / 3);
            Position cursor = well.Position;
            for (int step = 0; step < steps; step++)
            {
                Position best = cursor;
                (int Distance, int FrameY, int FrameX) bestKey = (
                    ArenaBasics.StaticDistance(
                        contract.Map, cursor, _enemyReactor)
                        ?? int.MaxValue,
                    int.MaxValue,
                    int.MaxValue);
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0)
                            continue;
                        Position next = cursor.Offset(dx, dy);
                        if (!ArenaBasics.IsLegalTerrainStep(
                                contract.Map, cursor, next))
                        {
                            continue;
                        }
                        if (ArenaBasics.StaticDistance(
                                contract.Map, next, _enemyReactor)
                            is not int candidate)
                        {
                            continue;
                        }
                        (int, int, int) key = (
                            candidate,
                            ArenaBasics.FrameY(next, _mirrored),
                            ArenaBasics.FrameX(next, _mirrored));
                        if (key.CompareTo(bestKey) < 0)
                        {
                            bestKey = key;
                            best = next;
                        }
                    }
                }
                if (best == cursor)
                    break;
                cursor = best;
            }
            waypoints.Add(cursor);
        }
        return [.. waypoints.Distinct()];
    }

    /// <summary>
    /// Interception (ghost doctrine v2, owner design: "act on an incoming
    /// enemy that it saw start walking from the mid"): take the freshest
    /// remembered enemy whose last observed motion carried it CLOSER to
    /// our reactor, and move to cut its lane - the same predicted-lane
    /// machinery the carrier cutoff uses, pointed at our own bank. The
    /// movement target names the fallback anchor when nothing is inbound.
    /// </summary>
    private Position IncomingCutoffTarget(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        TacticalPlaybookPackage package,
        TacticalPlaybookPackage.Order order,
        MindBody body) =>
        FindInboundCutoff(contract, mind, order, body)
            ?? package.AnchorPosition(order.Movement.Target);

    /// <summary>
    /// The freshest remembered enemy whose last observed motion carried it
    /// CLOSER to our reactor, resolved to a lane cutoff - the same
    /// predicted-lane machinery the carrier cutoff uses, pointed at our
    /// own bank. Null when nothing is inbound.
    /// </summary>
    private Position? FindInboundCutoff(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        TacticalPlaybookPackage.Order order,
        MindBody body)
    {
        LastSeenEnemy? inbound = _lastSeenEnemies.Values
            .Where(enemy => mind.Tick - enemy.LastConfirmedTick <= 24
                && enemy.PreviousPosition is Position prior
                && ArenaBasics.StaticDistance(
                        contract.Map, enemy.Position, _ownReactor)
                    is int now
                && ArenaBasics.StaticDistance(
                        contract.Map, prior, _ownReactor)
                    is int before
                && now < before)
            .OrderBy(enemy => ArenaBasics.StaticDistance(
                    contract.Map, enemy.Position, _ownReactor)
                ?? int.MaxValue)
            .ThenBy(enemy => enemy.ActorId.UnitId)
            .FirstOrDefault();
        if (inbound is null)
            return null;
        Position cutoff = TacticalCoordinationPrimitives
            .PredictReturnLaneCutoff(
                _mirrored,
                inbound.Position,
                inbound.PreviousPosition,
                Math.Max(1, order.Movement.LeadTiles),
                position => ArenaBasics.StaticDistance(
                    contract.Map, position, _ownReactor),
                (from, to) =>
                    ArenaBasics.IsLegalTerrainStep(contract.Map, from, to));
        int? reach = ArenaBasics.StaticDistance(
            contract.Map, body.Position, cutoff);
        return cutoff != inbound.Position
            && reach is int steps
            && steps <= Math.Max(1, order.Movement.LeadTiles)
                ? cutoff
                : inbound.Position;
    }

    private IEnumerable<TacticalCoordinationPrimitives.EnemyCarrierCandidate>
        EnemyCarrierCandidates(
            IReadOnlyDictionary<ActorIdentity,
                GenericActorContext.ArcRelayCoreState> carried) => carried
        .Where(value => value.Key.TeamId != _teamId)
        .Select(value =>
        {
            LastSeenEnemy? remembered = _lastSeenEnemies.GetValueOrDefault(
                value.Key.UnitId);
            return new TacticalCoordinationPrimitives.EnemyCarrierCandidate(
                value.Key,
                value.Value.Position,
                remembered?.ActorId == value.Key
                    ? remembered.PreviousPosition
                    : null);
        })
        .Concat(_lastSeenEnemies.Values
            .Where(value => value.IsCarrier)
            .Select(value => new TacticalCoordinationPrimitives
                .EnemyCarrierCandidate(
                    value.ActorId,
                    value.Position,
                    value.PreviousPosition)))
        .DistinctBy(value => value.ActorId);

    /// <summary>
    /// The escorted orders that are actually live this tick: for each, the
    /// leader body, the tile the leader is about to step into, and its
    /// followers in the order's declared precedence. Nothing here counts
    /// followers or knows what a posture does — an order listing three
    /// escorts under three postures resolves the same way this one does.
    /// An order whose leader is dead has no party at all, and its would-be
    /// followers simply walk the order's own target, which is exactly what
    /// a leaderless escort should do.
    /// </summary>
    private static EscortParty[] ResolveEscorts(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        IReadOnlyDictionary<int, string> roles,
        IReadOnlyDictionary<int, TacticalPlaybookPackage.Order> orders,
        IReadOnlyDictionary<int, Position> authoredTargets)
    {
        var parties = new List<EscortParty>();
        foreach (IGrouping<string, MindBody> party in mind.Bodies
                     .Where(body => orders[body.UnitId].Escort is not null)
                     .OrderBy(body => body.UnitId)
                     .GroupBy(body => orders[body.UnitId].OrderId,
                         StringComparer.Ordinal))
        {
            TacticalPlaybookPackage.Escort escort =
                orders[party.First().UnitId].Escort!;
            MindBody? leader = party.FirstOrDefault(body => string.Equals(
                roles[body.UnitId],
                escort.LeaderRole,
                StringComparison.Ordinal));
            if (leader is null)
                continue;
            var followers = new List<EscortMember>();
            foreach (TacticalPlaybookPackage.EscortFollower policy in
                     escort.Followers)
            {
                foreach (MindBody body in party.Where(body => string.Equals(
                             roles[body.UnitId],
                             policy.RoleId,
                             StringComparison.Ordinal)))
                {
                    followers.Add(new EscortMember(
                        body, policy, followers.Count));
                }
            }
            if (followers.Count == 0)
                continue;
            parties.Add(new EscortParty(
                leader,
                ArenaBasics.StaticFirstStep(
                    contract, mind, leader, authoredTargets[leader.UnitId]),
                followers.ToArray()));
        }
        return parties.ToArray();
    }

    private Dictionary<int, Position> ResolveFormationTargets(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        TacticalPlaybookPackage package,
        IReadOnlyDictionary<int, string> roles,
        IReadOnlyDictionary<int, TacticalPlaybookPackage.Order> orders,
        IReadOnlyDictionary<int, Position> authoredTargets)
    {
        var result = new Dictionary<int, Position>();
        foreach (IGrouping<string, MindBody> group in mind.Bodies
                     .OrderBy(body => body.UnitId)
                     .GroupBy(body => orders[body.UnitId].FormationId,
                         StringComparer.Ordinal))
        {
            var assigned = new List<
                TacticalFormationPrimitives.AssignedTarget>();
            foreach (MindBody body in group)
            {
                TacticalPlaybookPackage.Formation formation = package.ResolveFormation(
                    package.Source, orders[body.UnitId].FormationId);
                TacticalPlaybookPackage.Order order = orders[body.UnitId];
                int searchRadius = Math.Min(
                    formation.Reflow.SearchRadius,
                    order.Movement.ChaseLeash);
                if (string.Equals(
                        order.Movement.Kind, "route", StringComparison.Ordinal)
                    && _routes.GetValueOrDefault(body.UnitId) is
                        RouteProgress progress
                    && progress.Index
                        < package.RoutePoints(order.Movement.Target).Length - 1)
                {
                    searchRadius = Math.Min(
                        searchRadius,
                        package.RouteCorridorWidth(order.Movement.Target));
                }
                Position authored = authoredTargets[body.UnitId];
                string role = roles[body.UnitId];
                // A swarm has no slots (DECISIONS #212): everyone heads for
                // the authored target and per-tick tile reservations do the
                // spacing. Slot contention was the dance factory; there is
                // nothing here to contend.
                if (string.Equals(
                        formation.Shape, "swarm", StringComparison.Ordinal))
                {
                    result[body.UnitId] = authored;
                    assigned.Add(
                        new TacticalFormationPrimitives.AssignedTarget(
                            role, authored));
                    continue;
                }
                Position selected = TacticalFormationPrimitives
                    .SelectFormationTarget(
                        _mirrored,
                        contract.Map.Width,
                        contract.Map.Height,
                        contract.Map.TileRows,
                        authored,
                        role,
                        formation.Spacing.Minimum,
                        formation.Spacing.Preferred,
                        formation.Spacing.Maximum,
                        searchRadius,
                        formation.Reflow.BlockedSlot,
                        formation.Reflow.MedicSeparation,
                        assigned);
                result[body.UnitId] = selected;
                assigned.Add(new TacticalFormationPrimitives.AssignedTarget(
                    role, selected));
            }
        }
        return result;
    }

    private Position SecuredCoreTarget(
        TacticalPlaybookPackage package,
        TacticalPlaybookPackage.Order order)
    {
        Position fallback = package.AnchorPosition(order.Movement.Target);
        TacticalPlaybookPackage.CustodyPolicy policy = package.Source
            .CustodyPolicies.Single(value => value.CustodyId
                == order.CustodyId);
        TacticalCoordinationPrimitives.SecuredCoreCandidate? selected =
            TacticalCoordinationPrimitives.SelectSecuredCore(
                _securedCores.Select(value =>
                    new TacticalCoordinationPrimitives.SecuredCoreCandidate(
                        value.Key,
                        value.Value.SourceWellId,
                        value.Value.Position)),
                policy.SourceWells.ToHashSet(StringComparer.Ordinal),
                fallback,
                order.Movement.ChaseLeash);
        return selected?.Position ?? fallback;
    }

    private Position CarrierTarget(
        MindContext mind,
        TacticalPlaybookPackage package,
        IReadOnlyDictionary<int, string> groups,
        IReadOnlyDictionary<int, TacticalPlaybookPackage.Order> orders,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried,
        TacticalPlaybookPackage.Order order,
        MindBody body)
    {
        TacticalPlaybookPackage.CustodyPolicy? policy =
            string.IsNullOrEmpty(order.CustodyId)
                ? null
                : package.Source.CustodyPolicies.Single(value =>
                    value.CustodyId == order.CustodyId);
        if (policy is not null
            && !policy.EscortGroups.Contains(
                groups[body.UnitId], StringComparer.Ordinal))
        {
            return body.Position;
        }

        Position fallback = package.AnchorPosition(order.Movement.Target);
        (ActorIdentity ActorId, Position Position)[] candidates = mind.Bodies
            .Where(candidate => carried.ContainsKey(candidate.ActorId)
                && candidate.UnitId != body.UnitId
                && (policy is null || string.Equals(
                        orders[candidate.UnitId].CustodyId,
                        order.CustodyId,
                        StringComparison.Ordinal))
                && candidate.Position.ChebyshevDistance(fallback)
                    <= order.Movement.ChaseLeash)
            .Select(candidate => (candidate.ActorId, candidate.Position))
            .ToArray();
        if (candidates.Length == 0)
            return fallback;
        Array.Sort(candidates, (left, right) =>
            TacticalCustodyPrimitives.CompareEscortCandidate(
                body.Position, left, right));
        return candidates[0].Position;
    }

    private Position RouteTarget(
        GenericActorResolvedMatchContract contract,
        TacticalPlaybookPackage package,
        TacticalPlaybookPackage.Order order,
        MindBody body)
    {
        Position[] route = package.RoutePoints(order.Movement.Target);
        // A reset rejoins at the nearest waypoint by WALKED distance, so a
        // checkpoint behind a wall no longer looks close, and ties go
        // forward so a body finishing its circuit is never snapped back to
        // the start.
        RouteProgress state = _routes.GetValueOrDefault(body.UnitId)
            ?? new RouteProgress(
                body.ActorId, order.OrderId,
                RejoinWaypoint(contract, route, body));
        if (state.ActorId != body.ActorId
            || !string.Equals(state.OrderId, order.OrderId,
                StringComparison.Ordinal))
        {
            state = new RouteProgress(
                body.ActorId, order.OrderId,
                RejoinWaypoint(contract, route, body));
        }
        int index = Math.Min(state.Index, route.Length - 1);
        // A route is a corridor, not a sequence of single-tile queues. A body
        // reflowed to the edge of the declared corridor has completed that
        // waypoint just as surely as one standing on its centre tile. Using a
        // fixed radius of one stranded rear members around crowded waypoints.
        int arrival = Math.Max(
            Math.Max(1, order.Movement.ArrivalRadius),
            package.RouteCorridorWidth(order.Movement.Target));
        // ONE waypoint per tick. Advancing greedily let a body "complete"
        // waypoints it never approached: on a loop whose legs sit closer
        // together than the arrival ring, a tile between two legs satisfies
        // BOTH, so the index raced past the near corner and re-targeted the
        // leg behind the body — which then walked back, satisfied that
        // corner, and raced forward again. The measured shape was a
        // six-tile pendulum on the west base's stage-loop, period 12, all
        // match (owner catch 2026-08-09, w-9001 u7 at t297-t316). Catching
        // up one waypoint per tick costs a displaced body a few ticks and
        // cannot oscillate, because the index only ever moves forward.
        // ...and NEARER to this waypoint than to the one after it. The `if`
        // above stopped the index consuming several corners in a tick; it did
        // not stop it consuming one every tick forever, which on this ring is
        // the same disease one gear down. `stage-loop`'s legs are four tiles
        // long and the arrival ring is two, so the MIDDLE of every leg sits
        // inside both ends: the index advanced on each tick from a tile that
        // had approached nothing, lapped the body, and handed it a different
        // corner every few ticks. The body walked each one dutifully and the
        // result was a closed eleven-tick circuit beside its own base — owner
        // catch 2026-08-09 on the engagement-lens gallery, w-9001 team 0 u1,
        // t150-t250, boxed in x 8..9, y 10..15, never once reaching a corner.
        //
        // The lens is what settled it: the STEPS were right every tick and the
        // DESTINATION was wrong, so none of the blockade suspects (carrier lane
        // reservations, step ties, ally-occupied tiles) were involved at all —
        // the body was never blocked and moved every single tick.
        //
        // Comparing against the next waypoint rather than clamping the radius
        // keeps the corridor generous where it was meant to be and still lets a
        // body pass an unreachable waypoint: standing one tile off a walled
        // corner is nearer to it than to the following one, so the index moves
        // on exactly as it did before.
        if (index < route.Length - 1
            && body.Position.ChebyshevDistance(route[index]) <= arrival
            && body.Position.ChebyshevDistance(route[index])
                < body.Position.ChebyshevDistance(route[index + 1]))
            index++;
        // A route whose last waypoint is its first is a closed patrol loop:
        // reaching the end re-arms the start, so an authored post is a beat
        // to walk rather than a spot to stand (owner doctrine 2026-08-09 -
        // standing posts near base read as bugs even when intended).
        // The same nearness test on the seam. `route[^1]` IS `route[0]`, so the
        // waypoint after it is `route[1]`; comparing against the duplicate
        // would compare a tile with itself and re-arm the loop from anywhere.
        if (index == route.Length - 1
            && route.Length > 2
            && route[0] == route[^1]
            && body.Position.ChebyshevDistance(route[index]) <= arrival
            && body.Position.ChebyshevDistance(route[index])
                < body.Position.ChebyshevDistance(route[1]))
        {
            index = 0;
        }
        _routes[body.UnitId] = state with { Index = index };
        return route[index];
    }

    /// <summary>
    /// The approach half of the attack verb (DECISIONS #212): a body that
    /// HOLDS a focus target it did not just fire at must be closing on it -
    /// focus-locked statues standing at satisfied formation slots while
    /// their gates veto the shot were the last measured stuck-carrier wall.
    /// Runs after the skirmish step (kite cadence keeps its say) and before
    /// formation movement (a hunt outranks a slot).
    /// </summary>
    /// <summary>
    /// A body in a live duel stands its ground while the gun cycles.
    /// Without this, cooldown ticks fell through to formation movement and
    /// the body drifted toward its slot mid-fight - the owner watched a
    /// "fully committed" ghost walk away from an adjacent enemy between
    /// strikes (exhibition trace 2026-08-08: 60-70% of all turn-away
    /// moments were formation-move on cooldown). Declared-cone evacuation
    /// and self-preservation still outrank this: stepping out of a
    /// telegraphed strike is dodging, not disengagement.
    /// </summary>
    /// <summary>
    /// The standing half of the recover verb: a body under a heal-beacon
    /// order that has REACHED its beacon channels there. Commands
    /// <see cref="MindBody.Hold"/> directly rather than the idle-breaking
    /// <see cref="Hold(MindBody, string)"/> — channelling is stationary by
    /// nature, and the no-idle invariant exempts it explicitly.
    /// </summary>
    /// <summary>
    /// True when this body is under an armed recover and the sheet ranks
    /// healing above picking a new fight - or when it is one hit from dead,
    /// which outranks the knob.
    /// </summary>
    private bool HealOutranksFighting(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        TacticalPlaybookPackage.Order order,
        TacticalPlaybookPackage.Engagement engagement)
    {
        if (!string.Equals(
                order.Movement.Kind, "heal-beacon", StringComparison.Ordinal))
        {
            _healBreak.Remove(body.UnitId);
            return false;
        }
        // Re-engagement hysteresis, the same shape as the handed-off Core's
        // grace window: a body that broke off to heal does not take a NEW
        // fight until it is whole. Without it the boundary flaps - one hit
        // healed, an enemy still in view, back into the duel, hurt again -
        // which is the ball drop-pickup loop wearing different clothes.
        // Full health is the honest threshold because it is the same edge
        // the recover predicate itself disarms on.
        bool broken = _healBreak.TryGetValue(
                body.UnitId, out int brokenLife)
            && brokenLife == body.ActorId.LifeId;
        bool outranks = body.Health <= 1
            || string.Equals(
                engagement.Heal, "first", StringComparison.Ordinal)
            || broken && body.Health < MaxHealth(contract, body);
        if (outranks)
            _healBreak[body.UnitId] = body.ActorId.LifeId;
        else
            _healBreak.Remove(body.UnitId);
        return outranks;
    }

    /// <summary>
    /// Whether any adjacent tile is a legal, unoccupied step. Walls, the map
    /// edge and every live body (ours and theirs) block; a body with none of
    /// these free is cornered.
    /// </summary>
    private static bool HasExitStep(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body)
    {
        GenericActorMapContract map = contract.Map;
        foreach ((int dx, int dy) in EightWay)
        {
            var tile = new Position(body.Position.X + dx, body.Position.Y + dy);
            if (tile.X < 0 || tile.Y < 0
                || tile.X >= map.Width || tile.Y >= map.Height
                || map.TileRows[tile.Y][tile.X] == '#')
            {
                continue;
            }
            if (mind.Bodies.Any(other => other.Position == tile)
                || mind.Enemies.Any(enemy => enemy.Position == tile))
            {
                continue;
            }
            return true;
        }
        return false;
    }

    private static readonly (int Dx, int Dy)[] EightWay =
    [
        (0, -1), (1, -1), (1, 0), (1, 1),
        (0, 1), (-1, 1), (-1, 0), (-1, -1),
    ];

    private bool TryRecoverHold(
        MindBody body,
        TacticalPlaybookPackage.Order order,
        string reason)
    {
        if (!string.Equals(
                order.Movement.Kind, "heal-beacon", StringComparison.Ordinal)
            || !_healTiles.Contains(body.Position))
        {
            return false;
        }
        body.Hold(reason);
        return true;
    }

    private bool TryDuelStand(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        IReadOnlyDictionary<int, FocusAssignment> focus,
        string reason)
    {
        if (body.Cooldown <= 0
            || !focus.TryGetValue(body.UnitId, out FocusAssignment? duel)
            || duel.HoldDeclare
            || body.Position.ChebyshevDistance(duel.Target.Position)
                > AttackRange(contract, body))
        {
            return false;
        }
        return Hold(body, reason);
    }

    private static bool TryCloseOnFocus(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        IReadOnlyDictionary<int, FocusAssignment> focus,
        ArenaBasics.Claims claims,
        string reason)
    {
        // No cooldown exclusion: a cycling gun is not a reason to let prey
        // open the gap (owner trace 2026-08-08 - "the enemy ran off" was
        // this: strike, four free ticks of formation drift, then the
        // hidden-2-ticks release ended the fight). Between strikes a body
        // in range stands (TryDuelStand, ordered just before this) and a
        // body out of range closes.
        if (!focus.TryGetValue(body.UnitId, out FocusAssignment? assignment)
            || assignment.HoldDeclare
            || body.Position.ChebyshevDistance(
                assignment.Target.Position) <= 2)
        {
            return false;
        }
        return ArenaBasics.StaticFirstStepAvoidingReservations(
                    contract, mind, body, assignment.Target.Position)
                is Position step
            && ArenaBasics.TryMoveDirect(
                contract, mind, body, step, claims, reason);
    }

    /// <summary>
    /// Hidden-lock pursuit — what makes chase.persistTicks honest (owner
    /// catch 2026-08-07). Fog here is a facing CONE, so prey that rounds a
    /// corner leaves the visible set while its engagement scope still holds
    /// the lock for policy.Release.HiddenTicks. Focus allocation only ever
    /// assigns VISIBLE enemies, so every one of those retained ticks used to
    /// be spent walking back to a formation slot: the sheet said "chase for
    /// 30" and the body said "he's gone". Now the body walks to the prey's
    /// last-seen tile - inside the order's movement chase leash, respecting
    /// claims and reservations exactly as TryCloseOnFocus does - and once
    /// there sweeps its own cone, because the cone IS the sensor and turning
    /// is the only way to re-acquire from a cold trail.
    ///
    /// A flush is an EXCURSION, not a career (owner catch 2026-08-07:
    /// "patrol seems broken"). Unbounded, it ate 25-45% of the ghost's ticks
    /// - 30 to 48 separate interruptions per match, because cone vision
    /// drops and regains a target every few ticks in a warren and each drop
    /// started a fresh hunt - and the authored circuit never ran again.
    /// Three bounds keep the beat: the trail must be CLOSE (a corner turned,
    /// not a rumour across the map), one sighting buys one short walk, and
    /// arriving on cold ground spends it. Re-acquiring the prey for real
    /// refreshes the lock and buys the next excursion honestly.
    /// </summary>
    private bool TryFlushHidden(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        TacticalPlaybookPackage.Order order,
        TacticalPlaybookPackage.Engagement engagement,
        IReadOnlyDictionary<int, FocusAssignment> focus,
        string role,
        Position target,
        ArenaBasics.Claims claims,
        string reason)
    {
        // A live assignment always outranks a cold trail: this step exists
        // only for the ticks where the lock is retained but unassignable.
        if (focus.ContainsKey(body.UnitId)
            || !engagement.Participants.Contains(
                role, StringComparer.Ordinal))
        {
            return false;
        }
        string scopeId = string.Equals(
            engagement.CoordinationScope, "shared-policy",
            StringComparison.Ordinal)
                ? engagement.EngagementId
                : $"{engagement.EngagementId}:{order.GroupId}";
        if (!_focusLocks.TryGetValue(scopeId, out FocusLock? held)
            || _observedDestroyedEnemies.Contains(held.ActorId)
            || mind.Tick - held.LastVisibleTick >= engagement.Release.HiddenTicks
            // Visible again: allocation owns it from here.
            || mind.Enemies.Any(enemy => enemy.ActorId == held.ActorId)
            || !_lastSeenEnemies.TryGetValue(
                held.ActorId.UnitId, out LastSeenEnemy? trail)
            || trail.ActorId != held.ActorId)
        {
            return false;
        }
        // The leash is the order's, measured from the authored post - the
        // same reading every other movement-plane chase uses - and the trail
        // must be within arm's reach of the body, or this is desertion
        // dressed as pursuit.
        if (trail.Position.ChebyshevDistance(target)
                > Math.Max(order.Movement.ChaseLeash, 1)
            || body.Position.ChebyshevDistance(trail.Position) > FlushReach)
        {
            return false;
        }
        // One sighting, one excursion. A lock whose LastVisibleTick has
        // advanced is a fresh sighting and starts a fresh budget.
        HiddenFlush? excursion = _flushes.GetValueOrDefault(scopeId);
        if (excursion is null
            || excursion.Target != held.ActorId
            || excursion.LastVisibleTick != held.LastVisibleTick)
        {
            excursion = new HiddenFlush(
                held.ActorId, held.LastVisibleTick, mind.Tick, Spent: false);
            _flushes[scopeId] = excursion;
        }
        if (excursion.Spent || mind.Tick - excursion.StartTick >= FlushTicks)
            return false;
        if (body.Position.ChebyshevDistance(trail.Position) > 1)
        {
            return ArenaBasics.StaticFirstStepAvoidingReservations(
                        contract, mind, body, trail.Position) is Position step
                && ArenaBasics.TryMoveDirect(
                    contract, mind, body, step, claims, reason);
        }
        // Standing on the trail with nothing on it: one sweep of the cone,
        // then this scent is spent and the body goes back to work.
        _flushes[scopeId] = excursion with { Spent = true };
        return TryRotate(
            contract,
            body,
            (Direction)(((mind.Tick / 2) + body.UnitId) % 4),
            reason);
    }

    /// <summary>
    /// Where to rejoin a route: the waypoint whose WALK is shortest, walls
    /// counted - a real static path, not a straight line, so a checkpoint
    /// behind a wall stops looking close. Ties go to the LATER waypoint,
    /// which is forward progress: routes double back on themselves, and
    /// taking the first minimum snapped a body finishing its circuit back to
    /// the start.
    ///
    /// It deliberately does NOT weigh "route still ahead of this waypoint"
    /// (owner suggestion 2026-08-07). That objective is written for an open
    /// route and degenerates on a closed loop: shadow-north-long ends where
    /// it starts, so its last waypoint has ZERO route ahead of it and wins
    /// from every position on the map - and that waypoint is (8,1), the
    /// north-west checkpoint whose up-left detour prompted the request. The
    /// measured table is in the commit message. A loop has no "remaining",
    /// so the honest fixes are route-shaped, not executor-shaped.
    /// </summary>
    private static int RejoinWaypoint(
        GenericActorResolvedMatchContract contract,
        Position[] route,
        MindBody body)
    {
        int best = 0;
        int bestWalk = int.MaxValue;
        for (int index = 0; index < route.Length; index++)
        {
            int walk = ArenaBasics.StaticDistance(
                    contract.Map, body.Position, route[index])
                ?? body.Position.ChebyshevDistance(route[index]);
            if (walk <= bestWalk)
            {
                bestWalk = walk;
                best = index;
            }
        }
        return best;
    }

    /// <summary>
    /// Which filter dropped this body's best candidate. Predicates are asked
    /// in the same order the allocator asks them, and the answer is for the
    /// NEAREST enemy in the scope's target order - the one a spectator would
    /// have expected it to fight.
    /// </summary>
    private string DeclineReason(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        GenericActorContext.ObservedEnemyState[] targetOrder,
        Position post,
        TacticalPlaybookPackage.Engagement policy,
        IReadOnlyDictionary<ActorIdentity, int> attackerCounts,
        IReadOnlyDictionary<ActorIdentity, int> committedDamage)
    {
        GenericActorContext.ObservedEnemyState? nearest = targetOrder
            .OrderBy(enemy =>
                enemy.Position.ChebyshevDistance(body.Position))
            .ThenBy(enemy => enemy.ActorId.UnitId)
            .FirstOrDefault();
        if (nearest is null)
            return "no-target";
        int gap = nearest.Position.ChebyshevDistance(body.Position);
        if (attackerCounts.GetValueOrDefault(nearest.ActorId)
            >= policy.MaximumAttackersPerTarget)
            return $"cap@{gap}";
        if (!NeedsFocusAssignment(
                policy, nearest,
                committedDamage.GetValueOrDefault(nearest.ActorId)))
            return $"overkill@{gap}";
        if (!WithinEngagementLeash(body, post, nearest, policy))
            return $"leash@{gap}";
        if (!CommitAllowsTarget(contract, body, nearest, policy))
            return $"commit-target@{gap}";
        if (!CanContributeToTarget(
                contract, body, nearest, requireFireReady: false))
            return $"unreachable@{gap}";
        return $"other@{gap}";
    }

    private Dictionary<int, FocusAssignment> AllocateFocus(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        TacticalPlaybookPackage.Playbook playbook,
        TacticalPlaybookPackage package,
        TacticalSnapshot snapshot,
        IReadOnlyDictionary<int, string> roles,
        IReadOnlyDictionary<int, TacticalPlaybookPackage.Order> orders,
        IReadOnlyDictionary<int, Position> targets,
        IReadOnlySet<int> unavailableParticipants)
    {
        var allocations = new Dictionary<int, FocusAssignment>();
        _declines.Clear();
        foreach (MindBody idle in mind.Bodies)
        {
            _declines[idle.UnitId] = unavailableParticipants.Contains(
                idle.UnitId) ? "busy" : "no-scope";
        }
        HashSet<ActorIdentity> carriers = arc.VisibleCores
            .Where(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Carried
                && core.CarrierActorId?.TeamId != _teamId)
            .Select(core => core.CarrierActorId!).ToHashSet();
        foreach (TacticalPlaybookPackage.Engagement policy in
                 playbook.Engagements)
        {
            KeyValuePair<int, TacticalPlaybookPackage.Order>[] matching = orders
                .Where(value => value.Value.EngagementId == policy.EngagementId)
                .ToArray();
            IEnumerable<(string ScopeId,
                    KeyValuePair<int, TacticalPlaybookPackage.Order>[] Orders)>
                scopes = policy.CoordinationScope == "shared-policy"
                    ? [(policy.EngagementId, matching)]
                    : matching.GroupBy(value => value.Value.GroupId,
                            StringComparer.Ordinal)
                        .Select(group => (
                            $"{policy.EngagementId}:{group.Key}",
                            group.ToArray()));
            foreach ((string scopeId,
                         KeyValuePair<int, TacticalPlaybookPackage.Order>[]
                         policyOrders) in scopes)
            {
                MindBody[] participants = policyOrders
                    .Select(value => mind.Bodies.Single(body =>
                        body.UnitId == value.Key))
                    .Where(body => !unavailableParticipants.Contains(body.UnitId)
                        && policy.Participants.Contains(
                            roles[body.UnitId], StringComparer.Ordinal))
                    .OrderBy(body => body.UnitId).ToArray();
                foreach (MindBody inScope in participants)
                    _declines[inScope.UnitId] = "no-target";
                if (participants.Length == 0)
                    continue;
                if (mind.Enemies.Length == 0)
                {
                    foreach (MindBody blind in participants)
                        _declines[blind.UnitId] = "none-visible";
                    continue;
                }
                GenericActorContext.ObservedEnemyState[] enemies = mind.Enemies
                    // The isolation gate keeps a fragile predator from diving
                    // escorted prey - but a target whose BACK is to one of our
                    // shooters is a free backstab under the rear-arc rules,
                    // escorts or not, and refusing it read as "he doesn't
                    // like attacking even when clearly behind an enemy"
                    // (owner review 2026-08-10). Rear exposure overrides
                    // isolation; nothing else does.
                    .Where(enemy => policy.Isolation is not
                            TacticalPlaybookPackage.Isolation isolation
                        || ArenaBasics.RearExposedRank(participants, enemy) == 1
                        || !mind.Enemies.Any(ally =>
                            ally.ActorId != enemy.ActorId
                            && ally.Position.ChebyshevDistance(enemy.Position)
                                <= isolation.SupportRange))
                    .OrderBy(enemy => enemy,
                        Comparer<GenericActorContext.ObservedEnemyState>.Create(
                            (left, right) => CompareTargets(
                                policy, left, right, carriers, participants,
                                mind.Tick)))
                    .ToArray();
                if (policy.HoldFire is { } holdFire
                    && !(holdFire.ReleaseAll is { Length: > 0 } releaseGroups
                        && releaseGroups.Any(group =>
                            TacticalPlaybookMachine.Matches(group,
                                condition => Evaluate(
                                    condition, snapshot, package)))))
                {
                    // Ambush discipline: acquire nothing outside the gate, so
                    // held bodies neither rotate to track nor chase early.
                    enemies = enemies.Where(enemy => participants.Any(
                            participant => participant.Position
                                .ChebyshevDistance(enemy.Position)
                                    <= holdFire.WithinDistance))
                        .ToArray();
                    if (enemies.Length == 0)
                    {
                        foreach (MindBody held in participants)
                            _declines[held.UnitId] = "hold-fire";
                        _focusLocks.Remove(scopeId);
                        continue;
                    }
                }
                GenericActorContext.ObservedEnemyState primary = enemies[0];
                bool retainHiddenLock = false;
                bool selectedExistingLock = false;
                if (_focusLocks.TryGetValue(scopeId, out FocusLock? prior))
                {
                    GenericActorContext.ObservedEnemyState? locked = enemies
                        .FirstOrDefault(enemy =>
                            enemy.ActorId == prior.ActorId);
                    if (locked is null)
                    {
                        bool destroyed = _observedDestroyedEnemies.Contains(
                            prior.ActorId);
                        retainHiddenLock = !destroyed
                            && mind.Tick - prior.LastVisibleTick
                                < policy.Release.HiddenTicks;
                        if (!retainHiddenLock)
                            _focusLocks.Remove(scopeId);
                    }
                    else
                    {
                        bool withinLeash = participants.Any(body =>
                            WithinEngagementLeash(
                                body, targets[body.UnitId], locked, policy));
                        bool reachable = participants.Any(body =>
                            CanContributeToTarget(
                                contract, body, locked,
                                requireFireReady: false));
                        int unreachableTicks = reachable
                            ? 0
                            : prior.UnreachableTicks + 1;
                        bool release = TacticalCoordinationPrimitives
                            .ShouldReleaseFocus(
                                destroyed: _observedDestroyedEnemies.Contains(
                                    prior.ActorId),
                                releaseOnDestroyed: policy.Release.Destroyed,
                                outsideLeash: !withinLeash,
                                releaseOutsideLeash:
                                    policy.Release.OutsideLeash,
                                reachable,
                                unreachableTicks,
                                policy.Release.UnreachableTicks);
                        if (release)
                        {
                            _focusLocks.Remove(scopeId);
                        }
                        else
                        {
                            bool preempt = TacticalCoordinationPrimitives
                                .ShouldPreemptFocus(
                                    policy.LockPreemption,
                                    PriorityRank(
                                        policy.TargetPriorities,
                                        primary,
                                        carriers,
                                        mind.Tick),
                                    PriorityRank(
                                        policy.TargetPriorities,
                                        locked,
                                        carriers,
                                        mind.Tick),
                                    carriers.Contains(primary.ActorId),
                                    carriers.Contains(locked.ActorId),
                                    primary.Position.ChebyshevDistance(
                                        _enemyReactor),
                                    locked.Position.ChebyshevDistance(
                                        _enemyReactor));
                            _focusLocks[scopeId] = prior with
                            {
                                LastVisibleTick = mind.Tick,
                                UnreachableTicks = unreachableTicks,
                            };
                            if (!preempt
                                && (mind.Tick - prior.LockedTick
                                    < policy.LockTicks
                                || primary.ActorId == locked.ActorId)
                               )
                            {
                                primary = locked;
                                selectedExistingLock = true;
                            }
                        }
                    }
                }
                if (!retainHiddenLock && !selectedExistingLock
                    && (!_focusLocks.TryGetValue(
                            scopeId, out FocusLock? current)
                        || current.ActorId != primary.ActorId))
                {
                    _focusLocks[scopeId] = new FocusLock(
                        primary.ActorId,
                        mind.Tick,
                        mind.Tick,
                        UnreachableTicks: 0);
                }
                GenericActorContext.ObservedEnemyState[] targetOrder =
                    [primary, .. enemies.Where(enemy =>
                        enemy.ActorId != primary.ActorId)];
                if (!carriers.Contains(primary.ActorId))
                {
                    foreach (MindBody going in participants.Where(body =>
                                 _returningToFormation.ContainsKey(
                                     body.UnitId)))
                    {
                        _declines[going.UnitId] = "returning";
                    }
                    participants = participants.Where(body =>
                            !_returningToFormation.ContainsKey(body.UnitId))
                        .ToArray();
                }
                var committedDamage = new Dictionary<ActorIdentity, int>();
                var attackerCounts = new Dictionary<ActorIdentity, int>();
                foreach (MindBody body in participants
                             .OrderBy(body =>
                                 ArenaBasics.CanFireAtPosition(
                                     contract, body, primary.Position)
                                     ? 0 : 1)
                             .ThenBy(body => body.UnitId))
                {
                    // Sheet-owned commit discipline (ghost doctrine v2):
                    // the threat picture decides whether this body fights
                    // at all, before any target is weighed. Role-scoped:
                    // a lone hunter's discretion, not the pack's rules.
                    if (policy.Commit is { } commitPolicy
                        && CommitAppliesTo(commitPolicy, body)
                        && !CommitAllowsEngaging(contract, mind, body, commitPolicy))
                    {
                        _declines[body.UnitId] = "commit-engage";
                        continue;
                    }
                    GenericActorContext.ObservedEnemyState? selected =
                        targetOrder.FirstOrDefault(enemy =>
                            attackerCounts.GetValueOrDefault(enemy.ActorId)
                                < policy.MaximumAttackersPerTarget
                            && NeedsFocusAssignment(
                                policy,
                                enemy,
                                committedDamage.GetValueOrDefault(
                                    enemy.ActorId))
                            && WithinEngagementLeash(
                                body, targets[body.UnitId], enemy, policy)
                            && CommitAllowsTarget(
                                contract, body, enemy, policy)
                            && CanContributeToTarget(
                                contract, body, enemy,
                                requireFireReady: true));
                    selected ??= targetOrder.FirstOrDefault(enemy =>
                        attackerCounts.GetValueOrDefault(enemy.ActorId)
                            < policy.MaximumAttackersPerTarget
                        && NeedsFocusAssignment(
                            policy,
                            enemy,
                            committedDamage.GetValueOrDefault(enemy.ActorId))
                        && WithinEngagementLeash(
                            body, targets[body.UnitId], enemy, policy)
                            && CommitAllowsTarget(
                                contract, body, enemy, policy)
                            && CanContributeToTarget(
                                contract, body, enemy,
                                requireFireReady: false));
                    if (selected is null)
                    {
                        _declines[body.UnitId] = DeclineReason(
                            contract, body, targetOrder, targets[body.UnitId],
                            policy, attackerCounts, committedDamage);
                        continue;
                    }
                    _declines.Remove(body.UnitId);
                    // Approach discipline (ghost doctrine v3): a body told
                    // to strike from behind holds its declare and maneuvers
                    // toward the target's blind rear while the position
                    // budget lasts; on expiry it either strikes frontally
                    // anyway or trips the timed break-off latch. The window
                    // resets whenever the target changes or the life does.
                    bool holdDeclare = false;
                    if (policy.Commit is { Approach: { } approach }
                            approachCommit
                        && CommitAppliesTo(approachCommit, body)
                        && ArenaBasics.RearExposedRank([body], selected) == 1)
                    {
                        (int LifeId, ActorIdentity Target, int StartTick)
                            window = _positioning.GetValueOrDefault(
                                body.UnitId);
                        if (window.LifeId != body.ActorId.LifeId
                            || window.Target != selected.ActorId)
                        {
                            window = (
                                body.ActorId.LifeId,
                                selected.ActorId,
                                mind.Tick);
                            _positioning[body.UnitId] = window;
                        }
                        if (mind.Tick - window.StartTick
                            < approach.PositionTicks)
                        {
                            holdDeclare = true;
                        }
                        else if (string.Equals(
                                     approach.Else,
                                     "breakOff",
                                     StringComparison.Ordinal))
                        {
                            _disengaging[body.UnitId] = (
                                body.ActorId.LifeId,
                                mind.Tick + (approachCommit.DisengageWhen
                                    ?.RecoverTicks ?? 24));
                            _positioning.Remove(body.UnitId);
                            continue;
                        }
                        // else "strike": the budget is spent - declare
                        // frontally and keep the fight.
                    }
                    else
                    {
                        _positioning.Remove(body.UnitId);
                    }
                    // Positional combat (DECISIONS #212/#213): the aim IS the
                    // target's tile. The declared wedge and nearest-body
                    // resolution own everything the escape-lane coverage
                    // arithmetic used to compute here.
                    Position aim = selected.Position;
                    allocations[body.UnitId] = new FocusAssignment(
                        selected,
                        aim,
                        UseSignature: false,
                        SelfDefenseExcursion: TacticalCoordinationPrimitives
                            .IsSelfDefenseExcursion(
                                body.Position,
                                targets[body.UnitId],
                                selected.Position,
                                policy.ChaseLeash,
                                policy.SelfDefense.Enabled,
                                policy.SelfDefense.ThreatDistance),
                        HoldDeclare: holdDeclare);
                    attackerCounts[selected.ActorId] = attackerCounts
                        .GetValueOrDefault(selected.ActorId) + 1;
                    if (ArenaBasics.CanFireAtPosition(
                            contract, body, selected.Position))
                    {
                        committedDamage[selected.ActorId] = committedDamage
                            .GetValueOrDefault(selected.ActorId)
                            + ExpectedDamage(contract, body);
                    }
                }

                if (policy.SignatureCoordination is
                    "control-first" or "support-first")
                {
                    TacticalCoordinationPrimitives.SignatureCandidate[]
                        candidates = allocations
                            .Where(value => participants.Any(body =>
                                body.UnitId == value.Key))
                            .Select(value => (
                                Assignment: value,
                                Body: participants.Single(body =>
                                    body.UnitId == value.Key)))
                            .Select(value => new
                                TacticalCoordinationPrimitives
                                    .SignatureCandidate(
                                    value.Body.UnitId,
                                    value.Assignment.Value.Target.ActorId,
                                    CombatSignatureKey(contract, value.Body)
                                        ?? ""))
                            .Where(value => value.SignatureId.Length > 0)
                            .ToArray();
                    foreach (int controller in TacticalCoordinationPrimitives
                                 .SelectSignatureControllers(candidates))
                    {
                        allocations[controller] = allocations[controller]
                            with { UseSignature = true };
                    }
                }
            }
        }
        return allocations;
    }

    private static bool NeedsFocusAssignment(
        TacticalPlaybookPackage.Engagement policy,
        GenericActorContext.ObservedEnemyState target,
        int committedDamage) => TacticalCoordinationPrimitives
        .NeedsFocusAssignment(
            target.Health,
            committedDamage,
            policy.OverkillDamage);

    private static string? CombatSignatureKey(
        GenericActorResolvedMatchContract contract,
        MindBody body) => contract.Rules.Actions
        .Where(action => action.Kind
            == GenericActorRulesContract.ActionKind.Signature)
        .OrderBy(action => action.Code)
        .Select(action => body.Action(action.Id))
        .FirstOrDefault(action => action is { Available: true })
        ?.ActionId;

    /// <summary>
    /// Whether this body can contribute to killing that target. The strict
    /// pass asks whether it could fire RIGHT NOW; the lenient pass is meant
    /// to ask whether it could contribute at all - and used to answer with
    /// CanAimAtPosition, which is still ray-exact. An enemy two tiles away
    /// but off the eight rays therefore read as unreachable, so allocation
    /// refused to assign it, so the body never got close-on-focus,
    /// duel-stand or the flush machinery and simply walked past (owner
    /// catch: "it doesn't chase or engage nearby fights"; measured on
    /// commitx16-w-9001, `unreachable@1` and `unreachable@2` were the modal
    /// decline). A body that can WALK to its prey can contribute; the
    /// engagement leash is what bounds how far, and it is asked first.
    /// </summary>
    private static bool CanContributeToTarget(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        GenericActorContext.ObservedEnemyState target,
        bool requireFireReady) => requireFireReady
        ? ArenaBasics.CanFireAtPosition(contract, body, target.Position)
        : ArenaBasics.CanAimAtPosition(contract, body, target.Position)
            || ArenaBasics.StaticDistance(
                contract.Map, body.Position, target.Position) is not null;

    private int CompareTargets(
        TacticalPlaybookPackage.Engagement policy,
        GenericActorContext.ObservedEnemyState left,
        GenericActorContext.ObservedEnemyState right,
        IReadOnlySet<ActorIdentity> carriers,
        IReadOnlyCollection<MindBody> participants,
        int tick)
    {
        int comparison = PriorityRank(
            policy.TargetPriorities, left, carriers, tick).CompareTo(
            PriorityRank(policy.TargetPriorities, right, carriers, tick));
        if (comparison != 0)
            return comparison;
        if (_rearArcDamageMultiplier > 1)
        {
            // Backstab rulesets: at equal priority, prefer the target our
            // nearest shooter would hit in its blind rear arc — the shot is
            // worth double there.
            comparison = ArenaBasics.RearExposedRank(participants, left)
                .CompareTo(ArenaBasics.RearExposedRank(participants, right));
            if (comparison != 0)
                return comparison;
        }
        foreach (string tieBreaker in policy.TieBreakers)
        {
            comparison = tieBreaker switch
            {
                "health" => left.Health.CompareTo(right.Health),
                "distance" => participants.Min(body =>
                        body.Position.ChebyshevDistance(left.Position))
                    .CompareTo(participants.Min(body =>
                        body.Position.ChebyshevDistance(right.Position))),
                "enemy-reactor-distance" => left.Position
                    .ChebyshevDistance(_enemyReactor)
                    .CompareTo(right.Position.ChebyshevDistance(
                        _enemyReactor)),
                "own-reactor-distance" => left.Position
                    .ChebyshevDistance(_ownReactor)
                    .CompareTo(right.Position.ChebyshevDistance(
                        _ownReactor)),
                "unit-id" => left.ActorId.UnitId.CompareTo(
                    right.ActorId.UnitId),
                "life-id" => left.ActorId.LifeId.CompareTo(
                    right.ActorId.LifeId),
                "position" => left.Position.Y != right.Position.Y
                    ? left.Position.Y.CompareTo(right.Position.Y)
                    : left.Position.X.CompareTo(right.Position.X),
                _ => 0,
            };
            if (comparison != 0)
                return comparison;
        }
        comparison = left.ActorId.UnitId.CompareTo(right.ActorId.UnitId);
        return comparison != 0
            ? comparison
            : left.ActorId.LifeId.CompareTo(right.ActorId.LifeId);
    }

    private int PriorityRank(
        IReadOnlyList<string> priorities,
        GenericActorContext.ObservedEnemyState enemy,
        IReadOnlySet<ActorIdentity> carriers,
        int tick)
    {
        for (int index = 0; index < priorities.Count; index++)
        {
            bool matches = priorities[index] switch
            {
                // "class:<classId>" prefers targets of one chassis — the
                // kill-the-medics verb and its siblings, expressible in data.
                var term when term.StartsWith("class:", StringComparison.Ordinal)
                    => string.Equals(enemy.ClassId,
                        term["class:".Length..], StringComparison.Ordinal),
                "enemy-carrier" => carriers.Contains(enemy.ActorId),
                "lowest-health" => true,
                "closest-to-anchor" => enemy.Position.ChebyshevDistance(
                    _enemyReactor) <= 5,
                "closest-to-reactor" => true,
                "highest-threat" => enemy.Position.ChebyshevDistance(
                    _ownReactor) <= 6,
                "fresh-respawn" => tick - _firstSeenEnemyLife
                    .GetValueOrDefault(enemy.ActorId, tick) <= 5,
                _ => false,
            };
            if (matches)
                return index;
        }
        return priorities.Count;
    }

    private static int ExpectedDamage(
        GenericActorResolvedMatchContract contract,
        MindBody body)
    {
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(value => value.Id == body.FormId);
        GenericActorRulesContract.AttackProfile? attack =
            form?.AttackProfileId is string id
                ? contract.Rules.AttackProfiles.FirstOrDefault(value =>
                    value.Id == id)
                : null;
        return attack is null
            ? 1
            : Math.Max(1, attack.Projectile.DamagePerHit
                * attack.ProjectilesPerAttack);
    }

    private static bool WithinEngagementLeash(
        MindBody body,
        Position assignment,
        GenericActorContext.ObservedEnemyState enemy,
        TacticalPlaybookPackage.Engagement policy)
    {
        // Commit chase discipline: an authored chase leash overrides the
        // engagement's, and a target at or below the execute threshold
        // suspends the leash entirely - the kill is worth the ground.
        int leash = policy.ChaseLeash;
        if (policy.Commit?.Chase is { } chase)
        {
            if (chase.ExecuteBelowHealth > 0
                && enemy.Health <= chase.ExecuteBelowHealth)
            {
                return true;
            }
            if (chase.Leash > 0)
                leash = chase.Leash;
        }
        return TacticalCoordinationPrimitives.IsWithinEngagementLeash(
            assignment,
            enemy.Position,
            body.Position,
            leash,
            policy.SelfDefense.Enabled,
            policy.SelfDefense.ThreatDistance);
    }

    /// <summary>
    /// The commit threat picture: visible enemies within the awareness
    /// radius plus remembered positions no staler than the memory window,
    /// deduplicated by unit. Defaults: radius 8, memory 24.
    /// </summary>
    private int AwarenessThreats(
        MindContext mind,
        MindBody body,
        TacticalPlaybookPackage.Commit commit)
    {
        int radius = commit.Awareness?.Radius ?? 8;
        int memory = commit.Awareness?.MemoryTicks ?? 24;
        var units = new HashSet<int>();
        foreach (GenericActorContext.ObservedEnemyState enemy in mind.Enemies)
        {
            if (enemy.Position.ChebyshevDistance(body.Position) <= radius)
                units.Add(enemy.ActorId.UnitId);
        }
        foreach (LastSeenEnemy remembered in _lastSeenEnemies.Values)
        {
            if (mind.Tick - remembered.LastConfirmedTick <= memory
                && remembered.Position.ChebyshevDistance(body.Position)
                    <= radius)
            {
                units.Add(remembered.ActorId.UnitId);
            }
        }
        return units.Count;
    }

    /// <summary>
    /// Whether the commit discipline lets this body pick a fight at all,
    /// updating its disengage latch: at or past the disengage threshold the
    /// body breaks off (and withdraws, when the sheet names where) until
    /// the picture thins back to the engage gate.
    /// </summary>
    /// <summary>
    /// Whether this body's break-off latch is running right now. Read by
    /// the engage gate that trips it, and by the windup root: a body that
    /// has decided the fight is lost may still walk away from its own
    /// declare, and nothing else may.
    /// </summary>
    private bool DisengageLatched(MindContext mind, MindBody body)
    {
        (int LifeId, int UntilTick) latch =
            _disengaging.GetValueOrDefault(body.UnitId);
        return latch.LifeId == body.ActorId.LifeId
            && mind.Tick < latch.UntilTick;
    }

    private bool CommitAllowsEngaging(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        TacticalPlaybookPackage.Commit commit)
    {
        int threats = AwarenessThreats(mind, body, commit);
        (int LifeId, int UntilTick) latch =
            _disengaging.GetValueOrDefault(body.UnitId);
        bool active = DisengageLatched(mind, body);
        // Disengagement is a BREAK, never a retirement (the ab70 lesson:
        // a while-threats-persist latch parked the ghost for 250 ticks at
        // a hot home front, because a losing side's home always has
        // threats). The latch runs a fixed recovery window and expires
        // unconditionally; it can re-trip only after expiry.
        if (!active
            && commit.DisengageWhen is { } disengage
            && (disengage.Threats > 0 && threats >= disengage.Threats
                || disengage.Health > 0 && body.Health <= disengage.Health)
            && latch.UntilTick != mind.Tick)
        {
            active = true;
            _disengaging[body.UnitId] = (
                body.ActorId.LifeId,
                mind.Tick + disengage.RecoverTicks);
        }
        // Cornered (owner design 2026-08-07): "fighting until there's an
        // exit path is the way." A latched body with nowhere to step is not
        // breaking off, it is dying politely - the t256-263 death-pin, eight
        // ticks refusing every fight at 1 HP with an enemy adjacent and every
        // neighbour blocked. While no exit step exists the break is
        // SUSPENDED and the body fights; the tick the wall opens it resumes.
        // The latch itself keeps running, so this buys swings, not a reset.
        if (active && !HasExitStep(contract, mind, body))
        {
            _cornered.Add(body.UnitId);
            return true;
        }
        _cornered.Remove(body.UnitId);
        if (!active)
            _withdrawRallies.Remove(body.UnitId);
        if (active)
            return false;
        return commit.EngageWhen.MaxThreats == 0
            || threats <= commit.EngageWhen.MaxThreats;
    }

    /// <summary>
    /// Whether the commit discipline lets this body take THIS target: it
    /// must die fast enough (ceil(health / own damage) x own cadence within
    /// the authored window) and be catchable - movement is uniform-speed in
    /// this contract, so catchable means standing between the target and
    /// its own bank; equal-speed pursuit from behind never closes.
    /// </summary>
    private bool CommitAllowsTarget(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        GenericActorContext.ObservedEnemyState enemy,
        TacticalPlaybookPackage.Engagement policy)
    {
        if (policy.Commit is not { } commit
            || !CommitAppliesTo(commit, body))
        {
            return true;
        }
        if (commit.EngageWhen.KillWithinTicks > 0)
        {
            int damage = ExpectedDamage(contract, body);
            int cadence = AttackCadence(contract, body);
            int hits = (enemy.Health + damage - 1) / damage;
            if (hits * cadence > commit.EngageWhen.KillWithinTicks)
                return false;
        }
        // Catchability gates CHASES, never fights already in reach - and
        // "in reach" is DISTANCE, not aim-readiness (owner catch: a ghost
        // circling a lone carrier is never aim-ready under facing-locked
        // combat, so an aim-based bypass let it orbit forever without
        // striking). Anything inside the gun's range is killable now;
        // geometry only decides whether to CHASE what is beyond it.
        if (commit.Chase is { OnlyCatchable: true }
            && enemy.Health > (commit.Chase?.ExecuteBelowHealth ?? 0)
            && body.Position.ChebyshevDistance(enemy.Position)
                > AttackRange(contract, body))
        {
            Position enemyHome = enemy.ActorId.TeamId == _teamId
                ? _ownReactor
                : _enemyReactor;
            int? own = ArenaBasics.StaticDistance(
                contract.Map, body.Position, enemyHome);
            int? theirs = ArenaBasics.StaticDistance(
                contract.Map, enemy.Position, enemyHome);
            if (own is int ours && theirs is int retreat && ours > retreat)
                return false;
        }
        return true;
    }

    private bool CommitAppliesTo(
        TacticalPlaybookPackage.Commit commit,
        MindBody body) =>
        commit.Roles is not { Length: > 0 } roles
        || roles.Contains(
            _stableRoles.GetValueOrDefault(body.UnitId),
            StringComparer.Ordinal);

    private static int AttackRange(
        GenericActorResolvedMatchContract contract,
        MindBody body)
    {
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(value => value.Id == body.FormId);
        GenericActorRulesContract.AttackProfile? attack =
            form?.AttackProfileId is string id
                ? contract.Rules.AttackProfiles.FirstOrDefault(value =>
                    value.Id == id)
                : null;
        return attack?.Projectile.MaxTravelTiles ?? 1;
    }

    private static int AttackCadence(
        GenericActorResolvedMatchContract contract,
        MindBody body)
    {
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(value => value.Id == body.FormId);
        GenericActorRulesContract.AttackProfile? attack =
            form?.AttackProfileId is string id
                ? contract.Rules.AttackProfiles.FirstOrDefault(value =>
                    value.Id == id)
                : null;
        return attack is null ? 1 : Math.Max(1, attack.CooldownTicks + 1);
    }

    private void RefreshSelfDefenseReturns(
        MindContext mind,
        IReadOnlyDictionary<int, TacticalPlaybookPackage.Order> orders,
        IReadOnlyDictionary<int, Position> targets)
    {
        foreach (int unitId in _returningToFormation.Keys.ToArray())
        {
            MindBody? body = mind.Bodies.FirstOrDefault(value =>
                value.UnitId == unitId);
            if (body is null
                || body.ActorId != _returningToFormation[unitId]
                || TacticalCoordinationPrimitives.HasReturnedToFormation(
                    body.Position,
                    targets[unitId],
                    Math.Max(1, orders[unitId].Movement.ArrivalRadius)))
            {
                _returningToFormation.Remove(unitId);
            }
        }
    }

    private static Dictionary<int, MindBody> AllocateRepairs(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        TacticalPlaybookPackage.Playbook playbook,
        IReadOnlyDictionary<int, string> roles,
        IReadOnlyDictionary<int, TacticalPlaybookPackage.Order> orders,
        IReadOnlySet<ActorIdentity> carriers,
        IReadOnlySet<int> focusParticipants)
    {
        var result = new Dictionary<int, MindBody>();
        foreach (TacticalPlaybookPackage.SupportPolicy policy in
                 playbook.SupportPolicies)
        {
            MindBody[] providers = mind.Bodies
                .Where(body => policy.Providers.Contains(
                        roles[body.UnitId], StringComparer.Ordinal)
                    && orders[body.UnitId].SupportId == policy.SupportId)
                .OrderBy(body => body.UnitId).ToArray();
            var counts = new Dictionary<int, int>();
            var assignedProviderPositions = new Dictionary<int, List<Position>>();
            foreach (MindBody provider in providers)
            {
                MindBody? selected = mind.Bodies
                    .Where(body => body.UnitId != provider.UnitId
                        && body.Health < MaxHealth(contract, body)
                        && ArenaBasics.CanUseUnitSignature(
                            contract, provider, "repair-beam", body.ActorId)
                        && counts.GetValueOrDefault(body.UnitId)
                            < policy.MaximumProvidersPerTarget
                        && TacticalCoordinationPrimitives
                            .HonorsProviderSeparation(
                                provider.Position,
                                assignedProviderPositions.GetValueOrDefault(
                                    body.UnitId) ?? [],
                                policy.MinimumProviderSeparation))
                    .OrderBy(body => SupportRank(
                        policy, body, roles, carriers, focusParticipants))
                    .ThenBy(body => body.Health)
                    .ThenBy(body => provider.Position.ChebyshevDistance(
                        body.Position))
                    .ThenBy(body => body.UnitId)
                    .FirstOrDefault();
                if (selected is null)
                    continue;
                result[provider.UnitId] = selected;
                counts[selected.UnitId] = counts.GetValueOrDefault(
                    selected.UnitId) + 1;
                if (!assignedProviderPositions.TryGetValue(
                        selected.UnitId,
                        out List<Position>? positions))
                {
                    positions = [];
                    assignedProviderPositions[selected.UnitId] = positions;
                }
                positions.Add(provider.Position);
            }
        }
        return result;
    }

    private static int SupportRank(
        TacticalPlaybookPackage.SupportPolicy policy,
        MindBody body,
        IReadOnlyDictionary<int, string> roles,
        IReadOnlySet<ActorIdentity> carriers,
        IReadOnlySet<int> focusParticipants)
    {
        for (int index = 0; index < policy.TargetPriorities.Length; index++)
        {
            bool matches = policy.TargetPriorities[index] switch
            {
                "carrier" => carriers.Contains(body.ActorId),
                "medic" => roles[body.UnitId] == "medic",
                "lowest-health" => true,
                "focus-participant" => focusParticipants.Contains(body.UnitId),
                "formation-anchor" => roles[body.UnitId] == "line",
                "any" => true,
                _ => false,
            };
            if (matches)
                return index;
        }
        return policy.TargetPriorities.Length;
    }

    private static int MaxHealth(
        GenericActorResolvedMatchContract contract,
        MindBody body) => contract.Rules.Forms.Single(value =>
        value.Id == body.FormId).MaxHealth;

    private bool TryCustodyEmergency(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        TacticalPlaybookPackage package,
        MindBody body,
        string role,
        TacticalPlaybookPackage.Order order,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried,
        IReadOnlyDictionary<int,
            GenericActorContext.ArcRelayCoreState> pickupAssignments,
        IReadOnlySet<string> baitingCustodies,
        ArenaBasics.Claims claims)
    {
        if (!carried.ContainsKey(body.ActorId))
        {
            if (pickupAssignments.TryGetValue(
                    body.UnitId,
                    out GenericActorContext.ArcRelayCoreState? assignedCore)
                && _emergencyRecoveries.TryGetValue(
                    CoreKey(assignedCore.CoreId),
                    out EmergencyRecovery? visibleRecovery)
                && visibleRecovery.ActorId == body.ActorId)
            {
                // A visible emergency may still sit inside a protected home
                // pad or behind current traffic. Do not turn an impossible
                // pickup into a standing lease: fall through so the body can
                // keep fighting/holding the authored seal until a legal path
                // exists.
                return TryCollectEmergencyCore(
                    contract, mind, body, assignedCore, claims);
            }
            return false;
        }
        TacticalPlaybookPackage.CustodyPolicy custody = package.Source
            .CustodyPolicies.Single(value =>
                value.CustodyId == order.CustodyId);
        CustodyProgress progress = _custodyProgress.GetValueOrDefault(
                body.ActorId)
            ?? new CustodyProgress(
                body.ActorId,
                CoreKey(carried[body.ActorId].CoreId),
                mind.Tick,
                body.Position,
                0);
        string carriedCoreKey = CoreKey(carried[body.ActorId].CoreId);
        if (_emergencyRecoveries.TryGetValue(
                carriedCoreKey, out EmergencyRecovery? emergency)
            && emergency.ActorId == body.ActorId
            && string.Equals(
                emergency.CustodyId,
                custody.CustodyId,
                StringComparison.Ordinal)
            && string.Equals(
                custody.EmergencyRecoveryDisposition,
                "displace",
                StringComparison.Ordinal))
        {
            bool acted = ActEmergencyDisplacement(
                contract,
                mind,
                package,
                body,
                custody,
                carriedCoreKey,
                claims);
            return acted || Hold(body, "custody:emergency-displacement-wait");
        }
        if (custody.AuthorizedCarrierRoles.Contains(role,
                StringComparer.Ordinal))
        {
            if (custody.BaitDrop is { } bait
                && baitingCustodies.Contains(custody.CustodyId))
            {
                bool baitActed = ActBaitCarrier(
                    contract, mind, package, body, custody, bait, carried,
                    claims);
                return baitActed || Hold(body, "custody:bait-carry-wait");
            }
            bool acted = TacticalCustodyPrimitives.DeliveryTimedOut(
                    progress.StagnantTicks,
                    custody.DeliveryTimeoutTicks)
                ? ActUnreachableCustodyFallback(
                    contract, mind, package, body, custody, claims)
                : ActCarrier(contract, mind, package, body, custody, claims);
            // Carrying is an exclusive tactical state. On movement-cooldown
            // ticks ActCarrier may have no legal action; falling through
            // would let the prior formation rotate or steer the carrier away
            // from its homeward route between delivery steps.
            return acted || Hold(body, "custody:committed-delivery-wait");
        }

        if (string.Equals(custody.AccidentalPickup, "deliver",
                StringComparison.Ordinal))
        {
            bool acted = TacticalCustodyPrimitives.DeliveryTimedOut(
                    progress.StagnantTicks,
                    custody.DeliveryTimeoutTicks)
                ? ActUnreachableCustodyFallback(
                    contract, mind, package, body, custody, claims)
                : ActCarrier(contract, mind, package, body, custody, claims);
            return acted || Hold(body, "custody:committed-delivery-wait");
        }

        if (string.Equals(custody.AccidentalPickup, "drop-safe",
                StringComparison.Ordinal))
        {
            if (IsSafeToDrop(mind, body) && TryDropCore(body))
                return true;
            return TacticalCustodyPrimitives.TransferWindowOpen(
                    mind.Tick - progress.StartedTick,
                    custody.TransferTimeoutTicks)
                ? ArenaBasics.TryEvade(contract, mind, body, claims)
                    || Hold(body, "custody:await-safe-drop")
                : ActCarrier(contract, mind, package, body, custody, claims);
        }

        // Transfer, per the owner's spec: give the Core to an ally in the
        // OWN-BASE direction - any own body strictly nearer our reactor than
        // this one - by walking to it and dropping. WHICH ally is the
        // cheapest for the BALL, not the nearest to the passer (see
        // TransferReceiver). The engine puts a voluntary drop on the
        // dropper's homeward neighbour, which is the receiver's side, so
        // they collect it at the next tick start and carry on home. With no
        // such ally there is nobody to hand to and the honest answer is to
        // courier it home yourself. The old handoff / await / drop dance is
        // gone: it was the transfer loop.
        MindBody? receiver = TransferReceiver(contract, mind, body, carried);
        if (receiver is null)
        {
            return ActCarrier(contract, mind, package, body, custody, claims)
                || Hold(body, "custody:transfer-none-courier");
        }
        if (body.Position.ChebyshevDistance(receiver.Position) <= 1)
        {
            if (!TryDropCore(body, "custody:transfer-drop"))
                return Hold(body, "custody:transfer-drop-wait");
            // A Core you just put down is not yours to pick up. Without this
            // the collect knob walks straight back onto the tile the drop
            // landed on and lifts it again - the ghost re-collected its own
            // hand-off four times in eight ticks and the receiver only got it
            // when it finally out-raced him (owner catch on commitx12-w-9001,
            // t437-448). The hand-off needs a moment to be somebody else's.
            _handedOff[body.UnitId] = (
                body.ActorId.LifeId,
                CoreKey(carried[body.ActorId].CoreId),
                mind.Tick);
            return true;
        }
        return ArenaBasics.StaticFirstStepAvoidingReservations(
                    contract, mind, body, receiver.Position)
                is Position toReceiver
            && ArenaBasics.TryMoveDirect(
                contract, mind, body, toReceiver, claims,
                "custody:transfer-approach")
            || Hold(body, "custody:transfer-approach-wait");
    }

    /// <summary>
    /// Who to hand the ball to: among own bodies strictly nearer home than
    /// the passer, walkable-from-here and not already carrying, the one that
    /// minimises the BALL'S remaining journey — the walk out to the receiver
    /// plus the receiver's walk home.
    ///
    /// <para>The old rule picked the eligible body nearest the PASSER, and
    /// "nearest to me" is not the same question as "cheapest for the ball":
    /// it sent a passer trekking a long way to a receiver that was barely
    /// ahead of it, past nearer bodies that were much further along (owner
    /// catch 2026-08-09). Summing the two legs balances the two by
    /// construction — a receiver one tile away that is one tile ahead is
    /// worth no more than the trip it saves.</para>
    ///
    /// <para>Distances are MAP distances, not Chebyshev: "nearer home" and
    /// "close by" both have to mean walking, or a body one tile away across
    /// a wall reads as the obvious receiver. An unreachable candidate has no
    /// cost and is not a candidate. The fields are goal-keyed and cached, so
    /// both legs are lookups against two floods.</para>
    /// </summary>
    private MindBody? TransferReceiver(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        int? carrierHome = ArenaBasics.StaticDistance(
            contract.Map, body.Position, _ownReactor);
        if (carrierHome is null)
            return null;
        return mind.Bodies
            .Where(candidate => candidate.UnitId != body.UnitId
                && !carried.ContainsKey(candidate.ActorId))
            .Select(candidate => (
                Body: candidate,
                ToReceiver: ArenaBasics.StaticDistance(
                    contract.Map, candidate.Position, body.Position),
                Home: ArenaBasics.StaticDistance(
                    contract.Map, candidate.Position, _ownReactor)))
            .Where(candidate => candidate.ToReceiver is not null
                && candidate.Home is not null
                && candidate.Home < carrierHome)
            .OrderBy(candidate =>
                candidate.ToReceiver!.Value + candidate.Home!.Value)
            // Same total journey: the passer walks the shorter half.
            .ThenBy(candidate => candidate.ToReceiver!.Value)
            .ThenBy(candidate => candidate.Body.UnitId)
            .Select(candidate => candidate.Body)
            .FirstOrDefault();
    }

    private bool ActUnreachableCustodyFallback(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        TacticalPlaybookPackage package,
        MindBody body,
        TacticalPlaybookPackage.CustodyPolicy custody,
        ArenaBasics.Claims claims) => custody.UnreachableFallback switch
    {
        "hold" => Hold(body, "custody:delivery-timeout-hold"),
        "guard" => ArenaBasics.TryEvade(contract, mind, body, claims)
            || Hold(body, "custody:delivery-timeout-guard"),
        "alternate-core" => IsSafeToDrop(mind, body) && TryDropCore(body)
            || Hold(body, "custody:delivery-timeout-alternate-core"),
        "regroup" => ActCarrier(contract, mind, package, body, custody, claims),
        _ => throw new InvalidDataException(
            $"Unknown custody fallback '{custody.UnreachableFallback}'."),
    };

    private static bool IsSafeToDrop(MindContext mind, MindBody carrier) =>
        mind.Enemies.All(enemy => enemy.Position.ChebyshevDistance(
            carrier.Position) > 3)
        && mind.Bodies.Any(ally => ally.UnitId != carrier.UnitId
            && ally.Position.ChebyshevDistance(carrier.Position) <= 2);

    private static bool TryDropCore(MindBody carrier)
        => TryDropCore(carrier, "custody:accidental-pickup-safe-drop");

    private static bool TryDropCore(MindBody carrier, string reason)
    {
        GenericActorActionLegality? drop = carrier.Action("drop-core");
        if (drop is not { Available: true })
            return false;
        carrier.Command(drop.ActionId, drop.ActionCode, [], reason);
        return true;
    }

    private bool ActEmergencyDisplacement(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        TacticalPlaybookPackage package,
        MindBody body,
        TacticalPlaybookPackage.CustodyPolicy custody,
        string coreKey,
        ArenaBasics.Claims claims)
    {
        string targetId = custody.EmergencyDisplacementTarget
            ?? throw new InvalidDataException(
                $"Custody policy '{custody.CustodyId}' has no emergency "
                + "displacement target.");
        Position target = package.AnchorPosition(targetId);
        string[] recoveryZones = custody.EmergencyRecoveryZones ?? [];
        if (ArenaBasics.TryPositionSignature(
                contract,
                body,
                "arc-toss",
                target,
                "custody:emergency-displacement-arc-toss",
                position => recoveryZones.All(zone =>
                    !package.Contains(zone, position))))
        {
            _emergencyRecoveries.Remove(coreKey);
            return true;
        }
        if (body.Position.ChebyshevDistance(target)
                <= custody.EmergencyDisplacementReleaseRadius
            && recoveryZones.All(zone =>
                !package.Contains(zone, body.Position))
            && TryDropCore(body, "custody:emergency-displacement-drop"))
        {
            _emergencyRecoveries.Remove(coreKey);
            return true;
        }
        if (TryAdvanceSignature(contract, body, target))
            return true;
        return ArenaBasics.TryMoveToward(
            contract,
            mind,
            body,
            [target],
            claims,
            "custody:emergency-displacement-move");
    }

    /// <summary>
    /// Carries toward the bait pocket and tosses the Core in from range. The
    /// toss (not a drop) is load-bearing: an uncaught arc-toss landing is a
    /// loose Core with no body standing on it, while a voluntary drop would
    /// be re-collected from the dropper's own tile at the next tick start.
    /// Non-toss carriers walk into the pocket and lure in person.
    /// </summary>
    private bool ActBaitCarrier(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        TacticalPlaybookPackage package,
        MindBody body,
        TacticalPlaybookPackage.CustodyPolicy custody,
        TacticalPlaybookPackage.BaitDrop bait,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried,
        ArenaBasics.Claims claims)
    {
        Position pocket = package.ZoneCenter(bait.Zone);
        bool Occupied(Position tile) =>
            mind.Bodies.Any(ally => ally.Position == tile)
            || mind.Enemies.Any(enemy => enemy.Position == tile);
        Position? landing = new (int Dx, int Dy)[]
            {
                (0, 0), (0, -1), (1, 0), (0, 1), (-1, 0),
                (1, -1), (1, 1), (-1, 1), (-1, -1),
            }
            .Select(offset => new Position(
                pocket.X + offset.Dx, pocket.Y + offset.Dy))
            .Where(tile => !Occupied(tile))
            .Cast<Position?>()
            .FirstOrDefault();
        if (landing is Position target
            && ArenaBasics.TryPositionSignature(
                contract, body, "arc-toss", target, "custody:bait-toss"))
        {
            _baitCores[CoreKey(carried[body.ActorId].CoreId)] =
                custody.CustodyId;
            return true;
        }
        Position? step = ArenaBasics.StaticFirstStepAvoidingReservations(
            contract, mind, body, pocket);
        return step is Position committed
            && ArenaBasics.TryMoveDirect(
                contract, mind, body, committed, claims,
                "custody:bait-approach");
    }

    private bool ActCarrier(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        TacticalPlaybookPackage package,
        MindBody body,
        TacticalPlaybookPackage.CustodyPolicy custody,
        ArenaBasics.Claims claims)
    {
        // A Relay carrier with a teammate meaningfully closer to the bank
        // throws instead of walking: the pass is the designed answer to a
        // contested return, and the catcher inherits ordinary custody.
        // Sheet-gated: an unconditional pass changed frozen champions'
        // games, so only sheets that opt in ever volunteer it.
        if (string.Equals(custody.ForwardPass, "relay-catcher",
                StringComparison.Ordinal)
            && TryTossToForwardCatcher(contract, mind, body))
            return true;
        // Where a delivery started decides how it comes home. A Core lifted
        // inside a zone the custody names walks that zone's route corridor
        // instead of the shortest line - the ghost's ball found deep in enemy
        // ground comes back along the top corridor rather than straight
        // through the middle of their team.
        Position destination = DeliveryDestination(package, custody, body)
            ?? _ownReactor;
        string reason = destination == _ownReactor
            ? "custody:committed-delivery"
            : "custody:delivery-route";
        Position? step = ArenaBasics.StaticFirstStepAvoidingReservations(
            contract, mind, body, destination);
        if (step is Position committed
            && ArenaBasics.TryMoveDirect(
                contract, mind, body, committed, claims, reason))
            return true;
        if (TryAdvanceSignature(contract, body, destination))
            return true;
        if (ArenaBasics.TryMoveHomeward(
                contract, mind, body, destination, claims,
                "custody:delivery"))
            return true;
        return false;
    }

    /// <summary>Where this carrier is heading right now: its custody's
    /// delivery route when one applies, otherwise the reactor.</summary>
    private Position CarrierDestination(
        TacticalPlaybookPackage package,
        IReadOnlyDictionary<int, TacticalPlaybookPackage.Order> orders,
        MindBody body)
    {
        if (!orders.TryGetValue(
                body.UnitId, out TacticalPlaybookPackage.Order? order)
            || string.IsNullOrEmpty(order.CustodyId))
        {
            return _ownReactor;
        }
        TacticalPlaybookPackage.CustodyPolicy custody = package.Source
            .CustodyPolicies.Single(value =>
                value.CustodyId == order.CustodyId);
        return DeliveryDestination(package, custody, body) ?? _ownReactor;
    }

    /// <summary>How close a routed carrier has to get before a corridor
    /// waypoint counts as reached and the walk moves on to the next.</summary>
    private const int DeliveryWaypointArrivalRadius = 2;

    /// <summary>
    /// The corridor waypoint a routed delivery is walking to, or null when
    /// this custody names no route for where the Core was lifted - or when
    /// the corridor no longer helps, which is when the body is already closer
    /// to home than the waypoint would take it. Direction along the route is
    /// whichever way leads home, so one route serves both orientations of the
    /// map and both ends of a loop.
    /// <para>
    /// A corridor is WALKED, so the choice is sticky: the committed waypoint
    /// and direction are remembered on the custody and only ever advance -
    /// once the body has been within the arrival radius of a waypoint, that
    /// waypoint is spent for the rest of the run. Re-deriving the nearest
    /// waypoint every tick is what produced the w-9002 dance: at a wall pinch
    /// two consecutive waypoints' shortest paths leave in OPPOSITE
    /// directions, so a body straddling the arrival radius chose the far
    /// waypoint, stepped away, un-reached the near one, chose it again, and
    /// traded the same two tiles for forty ticks with a Core in its hands.
    /// </para>
    /// </summary>
    private Position? DeliveryDestination(
        TacticalPlaybookPackage package,
        TacticalPlaybookPackage.CustodyPolicy custody,
        MindBody body)
    {
        if (custody.DeliveryRoutes is not { Length: > 0 } rules
            || !_custodyProgress.TryGetValue(
                body.ActorId, out CustodyProgress? progress))
        {
            return null;
        }
        TacticalPlaybookPackage.DeliveryRoute? rule = rules.FirstOrDefault(
            value => package.Contains(value.Zone, progress.PickupPosition));
        if (rule is null)
            return null;
        Position[] route = package.RoutePoints(rule.Route);
        if (route.Length < 2)
            return null;
        int from;
        int forward;
        if (progress.RouteForward != 0
            && progress.RouteWaypoint >= 0
            && progress.RouteWaypoint < route.Length)
        {
            from = progress.RouteWaypoint;
            forward = progress.RouteForward;
        }
        else
        {
            int nearest = 0;
            for (int index = 1; index < route.Length; index++)
            {
                if (body.Position.ChebyshevDistance(route[index])
                    < body.Position.ChebyshevDistance(route[nearest]))
                    nearest = index;
            }
            from = nearest;
            forward = route[(nearest + 1) % route.Length]
                    .ChebyshevDistance(_ownReactor)
                <= route[(nearest - 1 + route.Length) % route.Length]
                    .ChebyshevDistance(_ownReactor)
                    ? 1
                    : -1;
        }
        int homeDistance = body.Position.ChebyshevDistance(_ownReactor);
        for (int step = 0; step < route.Length; step++)
        {
            int index = ((from + (forward * step)) % route.Length
                + route.Length) % route.Length;
            Position waypoint = route[index];
            if (waypoint.ChebyshevDistance(_ownReactor) >= homeDistance)
                continue;
            if (body.Position.ChebyshevDistance(waypoint)
                <= DeliveryWaypointArrivalRadius)
            {
                continue;
            }
            _custodyProgress[body.ActorId] = progress with
            {
                RouteWaypoint = index,
                RouteForward = forward,
            };
            return waypoint;
        }
        _custodyProgress[body.ActorId] = progress with
        {
            RouteWaypoint = -1,
            RouteForward = 0,
        };
        return null;
    }

    /// <summary>
    /// Throws the carried Core to a live teammate standing on a legal toss
    /// landing at least three tiles closer to the own reactor. Purely an
    /// executor competence: legality (range, walls, carrier state) is
    /// enforced by the action mask, and a teammate on the landing tile
    /// catches by rule, so a failed prediction degrades to a loose Core
    /// exactly as the rules intend.
    /// </summary>
    private bool TryTossToForwardCatcher(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body)
    {
        int carrierDistance = body.Position.ChebyshevDistance(_ownReactor);
        MindBody? catcher = mind.Bodies
            .Where(candidate => candidate.UnitId != body.UnitId
                && candidate.Position.ChebyshevDistance(_ownReactor)
                    <= carrierDistance - 3
                && candidate.Position.ChebyshevDistance(body.Position) <= 5)
            .OrderBy(candidate => candidate.Position.ChebyshevDistance(
                _ownReactor))
            .ThenBy(candidate => candidate.UnitId)
            .FirstOrDefault();
        return catcher is not null
            && ArenaBasics.TryPositionSignature(
                contract, body, "arc-toss", catcher.Position,
                "custody:forward-pass");
    }

    /// <summary>
    /// Support casting for a body holding formation with no combat focus:
    /// a wall projector angles toward the nearest known threat, and a
    /// flare-bearer illuminates the stalest remembered enemy nearby.
    /// Everything remains causal team knowledge; legality masks decide
    /// whether the cast actually happens.
    /// </summary>
    private bool TrySupportIdleSignature(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        string reason)
    {
        Position? nearestThreat = mind.Enemies
            .Select(enemy => (Position?)enemy.Position)
            .Concat(_lastSeenEnemies.Values
                .Select(seen => (Position?)seen.Position))
            .Where(position => position is Position candidate
                && body.Position.ChebyshevDistance(candidate) <= 6)
            .OrderBy(position => body.Position.ChebyshevDistance(
                position!.Value))
            .FirstOrDefault();
        // Idle time converts into persistent value: a wall angled at the
        // threat, or a deployable (sentinel, mine) laid on its approach.
        // Bodies without these signatures fall straight through.
        if (nearestThreat is Position threat
            && (ArenaBasics.TryDirectionSignature(
                    contract, body, "prism-wall", threat, reason,
                    mirrored: _mirrored)
                || ArenaBasics.TryPositionSignature(
                    contract, body, "sentinel-seed", threat, reason,
                    mirrored: _mirrored)
                || ArenaBasics.TryPositionSignature(
                    contract, body, "trip-node", threat, reason,
                    mirrored: _mirrored)))
            return true;
        LastSeenEnemy? stalest = _lastSeenEnemies.Values
            .Where(seen => seen.LastConfirmedTick < mind.Tick - 4
                && body.Position.ChebyshevDistance(seen.Position) <= 8)
            .OrderBy(seen => seen.LastConfirmedTick)
            .ThenBy(seen => seen.ActorId.UnitId)
            .FirstOrDefault();
        return stalest is not null
            && ArenaBasics.TryPositionSignature(
                contract, body, "survey-flare", stalest.Position, reason,
                mirrored: _mirrored);
    }

    /// <summary>
    /// The hook/rail channel: a heading signature needs exact ray alignment,
    /// which the focus-gated combat path almost never supplies (measured 0-2
    /// casts against the stock mind's ~40 per game). Any non-carrying body
    /// with a ready heading signature scans the visible hostiles for an
    /// aligned one in range and fires, independent of its gun state. Enemy
    /// carriers come first: a landed hook on a courier is the class's whole
    /// purpose.
    /// </summary>
    private bool TryOpportunisticHeadingSignature(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        string reason)
    {
        if (!mind.Enemies.Any())
            return false;
        HashSet<ActorIdentity> carriers = mind.Mode is
            GenericActorContext.ModeObservationState.ArcRelay arcState
            ? arcState.VisibleCores
                .Where(core => core.CarrierActorId is not null)
                .Select(core => core.CarrierActorId!)
                .ToHashSet()
            : [];
        GenericActorContext.ObservedEnemyState[] ordered = [.. mind.Enemies
            .OrderBy(enemy => carriers.Contains(enemy.ActorId) ? 0 : 1)
            .ThenBy(enemy => body.Position.ChebyshevDistance(enemy.Position))
            .ThenBy(enemy => ArenaBasics.FrameY(enemy.Position, _mirrored))
            .ThenBy(enemy => ArenaBasics.FrameX(enemy.Position, _mirrored))];
        foreach (string kind in HeadingSignatureKinds(contract))
        {
            foreach (GenericActorContext.ObservedEnemyState enemy in ordered)
            {
                if (ArenaBasics.TryHeadingSignature(
                        contract, body, kind, enemy.Position, reason))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The heading-argument combat signatures this contract carries, from
    /// metadata when annotated (grammar 2) and from the known grammar-1
    /// kinds otherwise. Movement dashes stay with their dedicated logic.
    /// </summary>
    private static string[] HeadingSignatureKinds(
        GenericActorResolvedMatchContract contract)
    {
        GenericActorRulesContract.ArcRelaySignature[] annotated =
            (ArenaBasics.ArcRules(contract)?.Signatures
                ?? Enumerable.Empty<
                    GenericActorRulesContract.ArcRelaySignature>())
            .Where(signature => signature.Category is not null)
            .ToArray();
        if (annotated.Length > 0)
        {
            return [.. annotated
                .Where(signature => string.Equals(
                        signature.ArgumentKind, "heading",
                        StringComparison.Ordinal)
                    && !RoleHandledSignatures.Contains(signature.Kind))
                .Select(signature => signature.Kind)];
        }
        return ["tractor-hook", "rail-line"];
    }

    /// <summary>
    /// Declared-strike evacuation (DECISIONS #212): a body standing on a lit
    /// tile leaves NOW, whatever its health — the public announcement is the
    /// whole counterplay, and it outranks every other movement concern. The
    /// shooter is exempt on its own ray (it starts underfoot), and the step
    /// prefers un-lit tiles in the canonical frame so both sides evacuate
    /// mirror-fairly.
    /// <para>
    /// A declared LINE ATTACK lights tiles the same way (owner ruling
    /// 2026-08-08): a rail, hook or sentinel winding up publishes the tiles
    /// it will resolve on, and stepping off them is the dodge. One channel
    /// answers both, because to a body underfoot they are the same fact —
    /// this tile is announced. A telegraph the caster owns is exempt, and a
    /// signature that is not telegraphing lights nothing.
    /// </para>
    /// </summary>
    private static bool TryStrikeEvacuation(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        ArenaBasics.Claims claims)
    {
        var lit = arc.PendingStrikes.IsDefaultOrEmpty
            ? []
            : arc.PendingStrikes
                .Where(strike => strike.Shooter != body.ActorId)
                .SelectMany(strike => strike.Tiles)
                .ToHashSet();
        if (!arc.VisibleSignatures.IsDefaultOrEmpty)
        {
            lit.UnionWith(arc.VisibleSignatures
                .Where(signature => signature.Phase
                        == GenericActorContext.ArcRelaySignaturePhase.Tell
                    && signature.OwnerActorId != body.ActorId)
                .SelectMany(signature => signature.Positions));
        }
        if (!lit.Contains(body.Position))
            return false;
        bool mirrored = ArenaBasics.MirroredFrame(contract, mind);
        foreach (Position candidate in new (int Dx, int Dy)[]
                 {
                     (0, -1), (1, 0), (0, 1), (-1, 0),
                     (1, -1), (1, 1), (-1, 1), (-1, -1),
                 }
                 .Select(offset => new Position(
                     body.Position.X + offset.Dx,
                     body.Position.Y + offset.Dy))
                 .Where(tile => !lit.Contains(tile))
                 .OrderBy(tile => ArenaBasics.FrameY(tile, mirrored))
                 .ThenBy(tile => ArenaBasics.FrameX(tile, mirrored)))
        {
            if (ArenaBasics.TryMoveDirect(
                    contract, mind, body, candidate, claims,
                    "strike-evacuation"))
            {
                return true;
            }
        }
        return false;
    }

    private bool TrySelfPreservation(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        TacticalPlaybookPackage.Playbook playbook,
        MindBody body,
        TacticalPlaybookPackage.Order order,
        ArenaBasics.Claims claims)
    {
        TacticalPlaybookPackage.SupportPolicy? support =
            string.IsNullOrEmpty(order.SupportId)
                ? null
                : playbook.SupportPolicies.Single(value =>
                    value.SupportId == order.SupportId);
        int reserve = support?.ReserveHealthPercent ?? 20;
        if (body.Health * 100 > MaxHealth(contract, body) * reserve)
            return false;
        string directive = support is null
            ? "evade"
            : TacticalCoordinationPrimitives.SurvivalDirective(
                support.SurvivalFallback);
        return directive switch
        {
            "evade" => ArenaBasics.TryEvade(
                contract, mind, body, claims),
            "regroup" => ArenaBasics.TryMoveToward(
                contract,
                mind,
                body,
                [_ownReactor],
                claims,
                "support:survival-regroup"),
            "hold" => Hold(body, "support:survival-hold"),
            "self-defense" => false,
            _ => throw new InvalidDataException(
                $"Unknown support directive '{directive}'."),
        };
    }

    private bool TryMovement(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        TacticalPlaybookPackage package,
        TacticalPlaybookMachine machine,
        TacticalSnapshot snapshot,
        MindBody body,
        string role,
        string group,
        TacticalPlaybookPackage.Order order,
        TacticalPlaybookPackage.Engagement engagement,
        Position target,
        IReadOnlyDictionary<int, Position> targets,
        IReadOnlyDictionary<int, string> groups,
        IReadOnlyDictionary<int, TacticalPlaybookPackage.Order> orders,
        IReadOnlyDictionary<int,
            GenericActorContext.ArcRelayCoreState> pickupAssignments,
        IReadOnlyDictionary<int, FocusAssignment> focus,
        ArenaBasics.Claims claims,
        string moveChannel)
    {
        TacticalPlaybookPackage.Group groupPolicy = package.Source.Groups
            .Single(value => value.GroupId == group);
        int liveGroupMembers = groups.Count(value => value.Value == group);
        if (liveGroupMembers < groupPolicy.Minimum)
        {
            switch (order.Fallback.OnUnderstrength)
            {
                case "continue":
                    break;
                case "regroup":
                case "fallback-phase":
                    QueueFallbackPhase(order);
                    return Hold(body, Provenance(
                        machine, group, order, "understrength-fallback"));
                default:
                    throw new InvalidDataException(
                        $"Unknown understrength fallback "
                        + $"'{order.Fallback.OnUnderstrength}'.");
            }
        }

        // Flank doctrine under predation rules: a focused shooter that
        // cannot hit its target's blind rear from here steps toward the
        // nearest rear firing tile instead of holding a frontal slot,
        // staying inside its engagement leash. Ambushers keep concealment
        // instead - unless a held declare (commit.approach) says the whole
        // point of this fight is to get behind first; everything else
        // about the order stays authored.
        if (_rearArcDamageMultiplier > 1
            && focus.TryGetValue(body.UnitId, out FocusAssignment? hunt)
            && (hunt.HoldDeclare
                || !string.Equals(
                    order.Stance, "ambush", StringComparison.Ordinal))
            && ArenaBasics.RearExposedRank([body], hunt.Target) == 1
            && body.Position.ChebyshevDistance(hunt.Target.Position) <= 7)
        {
            if (ArenaBasics.NearestRearFiringTile(contract, body, hunt.Target)
                    is Position firing
                && firing != body.Position
                && firing.ChebyshevDistance(target)
                    <= Math.Max(order.Movement.ChaseLeash, 1)
                        + order.Movement.ArrivalRadius + 2
                && ArenaBasics.StaticFirstStepAvoidingReservations(
                    contract, mind, body, firing) is Position flankStep
                && ArenaBasics.TryMoveDirect(
                    contract, mind, body, flankStep, claims,
                    Provenance(machine, group, order, "flank-approach")))
            {
                return true;
            }
        }

        // Ambush stance: a body settled near its post that is standing
        // inside a visible enemy's facing cone slips one tile into cover
        // instead of following the formation. Everything else about the
        // order (engagement gate, custody, facing) stays authored.
        if (string.Equals(order.Stance, "ambush", StringComparison.Ordinal)
            && body.Position.ChebyshevDistance(target)
                <= order.Movement.ArrivalRadius + 2
            && ArenaBasics.SeenByVisibleEnemy(mind, body.Position))
        {
            foreach (Position candidate in new (int Dx, int Dy)[]
                     {
                         (0, -1), (1, 0), (0, 1), (-1, 0),
                         (1, -1), (1, 1), (-1, 1), (-1, -1),
                     }
                     .Select(offset => new Position(
                         body.Position.X + offset.Dx,
                         body.Position.Y + offset.Dy))
                     .Where(tile => !ArenaBasics.SeenByVisibleEnemy(mind, tile)
                         && tile.ChebyshevDistance(target)
                             <= order.Movement.ArrivalRadius + 2)
                     .OrderBy(tile => tile.ChebyshevDistance(target)))
            {
                if (ArenaBasics.TryMoveDirect(
                        contract, mind, body, candidate, claims,
                        Provenance(machine, group, order, "ambush-conceal")))
                    return true;
            }
        }

        if (!DynamicTargetAvailable(
                mind, arc, package, order, body))
        {
            switch (order.Fallback.OnInvalidTarget)
            {
                case "alternate":
                    break;
                case "hold":
                    return Hold(body, Provenance(
                        machine, group, order, "invalid-target-hold"));
                case "fallback-phase":
                    QueueFallbackPhase(order);
                    return Hold(body, Provenance(
                        machine, group, order, "invalid-target-fallback"));
                default:
                    throw new InvalidDataException(
                        $"Unknown invalid-target fallback "
                        + $"'{order.Fallback.OnInvalidTarget}'.");
            }
        }

        if (!string.IsNullOrEmpty(order.CustodyId))
        {
            TacticalPlaybookPackage.CustodyPolicy policy =
                package.Source.CustodyPolicies.Single(value =>
                    value.CustodyId == order.CustodyId);
            if (policy.AuthorizedCarrierRoles.Contains(role,
                    StringComparer.Ordinal)
                && string.Equals(
                    policy.AccidentalPickup,
                    "transfer",
                    StringComparison.Ordinal)
                && TryReachAccidentalCarrier(
                    contract, mind, arc, policy, body, claims))
            {
                return true;
            }
            if (policy.AuthorizedCarrierRoles.Contains(role,
                    StringComparer.Ordinal)
                && pickupAssignments.TryGetValue(
                    body.UnitId,
                    out GenericActorContext.ArcRelayCoreState? assignedCore)
                && TryCollectCore(
                    contract, mind, body, assignedCore, claims))
                return true;
        }

        // Opportunistic collection (owner direction 2026-08-07): a ball lying
        // loose in the named zone is worth breaking the order for, whatever
        // the custody roster says about who is a courier. The ghost is not an
        // authorized carrier - its role forbids it, so the normal pickup
        // allocation never offers it a Core - but a Core it can SEE on the
        // enemy half is free tempo, and standing on it collects it by rule.
        // What happens next is ordinary: an unauthorized carrier follows its
        // custody's accidental-pickup policy, which for a transfer custody
        // means handing off homeward or couriering it home by the route.
        if (order.CollectZones is { Length: > 0 }
            && !arc.VisibleCores.Any(core =>
                core.CarrierActorId == body.ActorId)
            && TryCollectLooseCore(
                contract, mind, arc, package, body, order, engagement,
                claims))
        {
            return true;
        }

        MotionProgress motion = _motion.GetValueOrDefault(body.UnitId)
            ?? new MotionProgress(
                body.ActorId, order.OrderId, body.Position, body.Position, 0);
        // Ground is made by reaching a tile neither of the last two ticks put
        // this body on. Standing still and pacing between two tiles are the
        // same failure and now count the same; see MotionProgress.
        int stuck = motion.ActorId == body.ActorId
            && motion.OrderId == order.OrderId
            && (motion.Position == body.Position
                || motion.Previous == body.Position)
                ? motion.StuckTicks + 1
                : 0;
        _motion[body.UnitId] = new MotionProgress(
            body.ActorId, order.OrderId, body.Position, motion.Position, stuck);
        if (stuck >= order.Movement.StuckTicks)
        {
            switch (order.Movement.StuckRecovery)
            {
                case "yield":
                    _motion[body.UnitId] = new MotionProgress(
                        body.ActorId, order.OrderId,
                        body.Position, body.Position, 0);
                    return Hold(body, MovementProvenance(
                        machine, group, order, "stuck-yield", target));
                case "hold":
                    return Hold(body, MovementProvenance(
                        machine, group, order, "stuck-hold", target));
                case "regroup":
                    QueueFallbackPhase(order);
                    return Hold(body, MovementProvenance(
                        machine, group, order, "stuck-fallback", target));
                case "repath":
                    // Keep the body's monotonic route progress. Repath widens
                    // the local goal search below; resetting the route index
                    // here sent a body that was blocked on the final approach
                    // all the way back toward waypoint zero.
                    break;
                case "reflow":
                    break;
                default:
                    throw new InvalidDataException(
                        $"Unknown stuck recovery "
                        + $"'{order.Movement.StuckRecovery}'.");
            }
        }
        // Statue-livelock breaker (owner replay observation 2026-08-07,
        // seed 9002: both teams frozen for hundreds of ticks). A body
        // several recovery windows deep is wedged WITH its own formation -
        // the pace gate and mutual tile claims can hold a group in place
        // forever while every member busily repaths. Any legal displacing
        // step beats standing still: stepping away from one's own tile
        // accepts any open neighbour, bypasses the pace gate below, and
        // resets the counter so the normal machinery re-plans from the new
        // tile.
        if (stuck >= order.Movement.StuckTicks * 3
            && ArenaBasics.TryStepAway(
                contract, mind, body, [body.Position, _ownReactor], claims,
                MovementProvenance(
                    machine, group, order, "wedge-shake", target)))
        {
            _motion[body.UnitId] = new MotionProgress(
                body.ActorId, order.OrderId,
                body.Position, body.Position, 0);
            // The shake alone made dancers: the body stepped out and walked
            // straight back into the same contended slot. Blacklist the
            // approach for a while so the reflow spreads to an alternative.
            _slotHold[body.UnitId] = mind.Tick + 24;
            return true;
        }
        TacticalPlaybookPackage.Formation formation = package.ResolveFormation(
            package.Source, order.FormationId);
        MindBody[] paceMembers = mind.Bodies
            .Where(candidate => groups[candidate.UnitId] == group)
            .Where(candidate => string.Equals(
                orders[candidate.UnitId].FormationId,
                order.FormationId,
                StringComparison.Ordinal))
            .OrderBy(candidate => candidate.UnitId)
            .ToArray();
        int leaderUnitId = paceMembers[0].UnitId;
        int leaderDistance = paceMembers[0].Position.ChebyshevDistance(
            targets[leaderUnitId]);
        int furthestDistance = paceMembers.Max(candidate =>
            candidate.Position.ChebyshevDistance(targets[candidate.UnitId]));
        if (snapshot.FormationBroken.GetValueOrDefault(group) != 0
            && !TacticalFormationPrimitives.CanAdvanceAtPace(
                order.Movement.Pace,
                body.UnitId,
                leaderUnitId,
                body.Position.ChebyshevDistance(target),
                leaderDistance,
                furthestDistance))
        {
            return Hold(body, MovementProvenance(
                machine, group, order, "formation-pace", target));
        }
        bool targetBlocked = !TacticalFormationPrimitives.IsEnterable(
            contract.Map.Width,
            contract.Map.Height,
            contract.Map.TileRows,
            target);
        Position[] goals = TacticalFormationPrimitives.ReflowGoals(
            _mirrored,
            contract.Map.Width,
            contract.Map.Height,
            contract.Map.TileRows,
            target,
            stuck >= order.Movement.StuckTicks || targetBlocked
                || _slotHold.GetValueOrDefault(body.UnitId) > mind.Tick
                ? formation.Reflow.SearchRadius
                : 0,
            formation.Reflow.BlockedSlot);
        if (TryAdvanceSignature(contract, body, target))
            return true;
        if (ArenaBasics.TryMoveToward(
            contract, mind, body, goals, claims,
            MovementProvenance(machine, group, order,
                stuck >= order.Movement.StuckTicks
                    ? $"{order.Movement.StuckRecovery}-reflow"
                    : moveChannel,
                target)))
        {
            return true;
        }
        return order.Fallback.OnNoPath switch
        {
            "reflow" => false,
            "hold" => Hold(body, MovementProvenance(
                machine, group, order, "no-path-hold", target)),
            "regroup" => QueueFallbackPhaseAndHold(
                body, machine, group, order, "no-path-fallback"),
            _ => throw new InvalidDataException(
                $"Unknown no-path fallback '{order.Fallback.OnNoPath}'."),
        };
    }

    private bool DynamicTargetAvailable(
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        TacticalPlaybookPackage package,
        TacticalPlaybookPackage.Order order,
        MindBody body) => order.Movement.Kind switch
    {
        "carrier" => arc.VisibleCores.Any(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Carried
            && core.CarrierActorId is { } carrier
            && carrier.TeamId == _teamId
            && carrier != body.ActorId),
        "enemy-carrier" or "enemy-carrier-cutoff" =>
            arc.VisibleCores.Any(core => core.Disposition
                    == GenericActorContext.ArcRelayCoreDisposition.Carried
                && core.CarrierActorId is { } carrier
                && carrier.TeamId != _teamId
                && core.Position.ChebyshevDistance(
                        package.AnchorPosition(order.Movement.Target))
                    <= order.Movement.ChaseLeash)
            || _lastSeenEnemies.Values.Any(enemy => enemy.IsCarrier
                && enemy.Position.ChebyshevDistance(
                        package.AnchorPosition(order.Movement.Target))
                    <= order.Movement.ChaseLeash),
        "secured-core" => SecuredCoreAvailable(package, order),
        "heal-beacon" => RecoverBeacon(mind, body) is not null,
        _ => true,
    };

    private bool SecuredCoreAvailable(
        TacticalPlaybookPackage package,
        TacticalPlaybookPackage.Order order)
    {
        TacticalPlaybookPackage.CustodyPolicy policy = package.Source
            .CustodyPolicies.Single(value => value.CustodyId
                == order.CustodyId);
        Position fallback = package.AnchorPosition(order.Movement.Target);
        return _securedCores.Any(value => policy.SourceWells.Contains(
                value.Value.SourceWellId, StringComparer.Ordinal)
            && value.Value.Position.ChebyshevDistance(fallback)
                <= order.Movement.ChaseLeash);
    }

    private void QueueFallbackPhase(
        TacticalPlaybookPackage.Order order)
    {
        if (string.IsNullOrEmpty(order.Fallback.PhaseId))
        {
            throw new InvalidDataException(
                $"Order '{order.OrderId}' has no fallback phase.");
        }
        _queuedFallbackPhase ??= order.Fallback.PhaseId;
    }

    private bool QueueFallbackPhaseAndHold(
        MindBody body,
        TacticalPlaybookMachine machine,
        string group,
        TacticalPlaybookPackage.Order order,
        string channel)
    {
        QueueFallbackPhase(order);
        return Hold(body, Provenance(machine, group, order, channel));
    }

    private void UpdateOrderCompletion(
        MindContext mind,
        TacticalPlaybookPackage package,
        IReadOnlyDictionary<int, TacticalPlaybookPackage.Order> orders,
        IReadOnlyDictionary<int, Position> targets)
    {
        _orderCompletion.Clear();
        foreach (IGrouping<string, MindBody> members in mind.Bodies
                     .GroupBy(body => orders[body.UnitId].OrderId,
                         StringComparer.Ordinal))
        {
            MindBody[] ordered = members.OrderBy(body => body.UnitId).ToArray();
            TacticalPlaybookPackage.Order order = orders[ordered[0].UnitId];
            TacticalPlaybookPackage.Formation formation = package.ResolveFormation(
                package.Source, order.FormationId);
            int arrived = ordered.Count(body => body.Position
                .ChebyshevDistance(targets[body.UnitId])
                <= order.Movement.ArrivalRadius);
            bool complete = ordered.Any(body =>
                TacticalFormationPrimitives.OrderComplete(
                    order.Movement.Completion,
                    body.UnitId,
                    ordered[0].UnitId,
                    body.Position.ChebyshevDistance(targets[body.UnitId])
                        <= order.Movement.ArrivalRadius,
                    arrived,
                    ordered.Length,
                    formation.Cohesion.ArrivalRatioPercent));
            _orderCompletion[order.OrderId] = complete ? 1 : 0;
        }
    }

    private bool TryReachAccidentalCarrier(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        TacticalPlaybookPackage.CustodyPolicy policy,
        MindBody authorizedCarrier,
        ArenaBasics.Claims claims)
    {
        HashSet<ActorIdentity> carrierIds = arc.VisibleCores
            .Where(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Carried
                && core.CarrierActorId?.TeamId == _teamId)
            .Select(core => core.CarrierActorId!)
            .ToHashSet();
        MindBody? accidental = mind.Bodies
            .Where(candidate => carrierIds.Contains(candidate.ActorId)
                && !policy.AuthorizedCarrierRoles.Contains(
                    _stableRoles.GetValueOrDefault(candidate.UnitId),
                    StringComparer.Ordinal)
                && _custodyProgress.TryGetValue(
                    candidate.ActorId,
                    out CustodyProgress? progress)
                && TacticalCustodyPrimitives.TransferWindowOpen(
                    mind.Tick - progress.StartedTick,
                    policy.TransferTimeoutTicks))
            .OrderBy(candidate => candidate.Position.ChebyshevDistance(
                authorizedCarrier.Position))
            .ThenBy(candidate => candidate.UnitId)
            .FirstOrDefault();
        if (accidental is null)
            return false;
        return TryAdvanceSignature(
                contract, authorizedCarrier, accidental.Position)
            || ArenaBasics.TryMoveToward(
                contract,
                mind,
                authorizedCarrier,
                [accidental.Position],
                claims,
                "custody:reach-accidental-carrier");
    }

    /// <summary>
    /// Walk to the nearest visible loose Core inside any of the order's
    /// collect zones, within the order's chase leash of the body. Reserved and bait
    /// Cores are already excluded from the claim set by the caller's tick
    /// setup, so this only ever diverts for a Core nobody else is owed.
    /// </summary>
    private bool TryCollectLooseCore(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        TacticalPlaybookPackage package,
        MindBody body,
        TacticalPlaybookPackage.Order order,
        TacticalPlaybookPackage.Engagement engagement,
        ArenaBasics.Claims claims)
    {
        // PRECEDENCE, hard rule not a knob: an ARMED recover outranks the
        // ball. collect and heal each rank against COMBAT but said nothing
        // about each other, so a hurt body with a Core in sight tugged
        // between the beacon and the ball exactly the way collect tugged
        // against the cone dodge (owner catch on commitx19-w-9001: t202 heal
        // -> t204 collect -> t206 heal, and again at t255). A hurt body
        // hauling a Core is a liability twice over - it cannot fight, and it
        // loses the ball when it dies - so healing wins, and the 1-hp
        // override already outranks everything. Not authorable: there is no
        // sheet for which "fetch the ball while bleeding out" is the answer.
        //
        // The mask is sticky until WHOLE, mirroring the heal re-engage
        // hysteresis: without it the boundary flaps the moment one hit heals.
        if (_recovering.Contains(body.UnitId)
            || string.Equals(
                order.Movement.Kind, "heal-beacon", StringComparison.Ordinal)
            || (_healBreak.TryGetValue(body.UnitId, out int healingLife)
                && healingLife == body.ActorId.LifeId))
        {
            return false;
        }
        // Never go shopping at knife range - unless the sheet says the ball
        // comes FIRST, in which case breaking off an uncommitted fight for it
        // is the whole point. Collect sits in the movement
        // channel, so on a tick where the declared-cone dodge does not fire
        // it can win against a body that is toe to toe with an enemy - and
        // then the next tick evacuation pulls the other way. The owner
        // watched that thrash read as standing still (commitx17-w-9001
        // t139-142). A body in contact fights or dodges; the ball keeps.
        if (!string.Equals(
                engagement.Collect, "first", StringComparison.Ordinal)
            && mind.Enemies.Any(enemy =>
                enemy.Position.ChebyshevDistance(body.Position)
                    <= RecoverContact))
        {
            return false;
        }
        (int LifeId, string CoreKey, int Tick) handed =
            _handedOff.GetValueOrDefault(body.UnitId);
        bool JustHandedOff(GenericActorContext.ArcRelayCoreState core) =>
            handed.LifeId == body.ActorId.LifeId
            && mind.Tick - handed.Tick < HandoffGraceTicks
            && string.Equals(
                handed.CoreKey, CoreKey(core.CoreId), StringComparison.Ordinal);
        GenericActorContext.ArcRelayCoreState? prize = arc.VisibleCores
            .Where(core => core.Disposition
                    == GenericActorContext.ArcRelayCoreDisposition.Loose
                && !JustHandedOff(core)
                && order.CollectZones!.Any(zone =>
                    package.Contains(zone, core.Position))
                && core.Position.ChebyshevDistance(body.Position)
                    <= Math.Max(order.Movement.ChaseLeash, 1))
            .OrderBy(core => core.Position.ChebyshevDistance(body.Position))
            .ThenBy(core => core.CoreId.SourceWellId, StringComparer.Ordinal)
            .ThenBy(core => core.CoreId.SourceOrdinal)
            .FirstOrDefault();
        if (prize is null)
            return false;
        if (TryAdvanceSignature(contract, body, prize.Position))
            return true;
        return ArenaBasics.TryMoveToward(
            contract, mind, body, [prize.Position], claims, "collect-core");
    }

    private bool TryCollectCore(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        GenericActorContext.ArcRelayCoreState core,
        ArenaBasics.Claims claims)
    {
        Position destination = core.Position;
        if (TryAdvanceSignature(contract, body, destination))
            return true;
        return ArenaBasics.TryMoveToward(
            contract, mind, body, [destination], claims,
            "custody:authorized-pickup");
    }

    private bool TryCollectEmergencyCore(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        GenericActorContext.ArcRelayCoreState core,
        ArenaBasics.Claims claims)
    {
        Position destination = core.Position;
        if (TryAdvanceSignature(contract, body, destination))
            return true;
        Position? step = ArenaBasics.StaticFirstStepAvoidingReservations(
            contract, mind, body, destination);
        return step is Position committed
            && ArenaBasics.TryMoveDirect(
                contract,
                mind,
                body,
                committed,
                claims,
                "custody:emergency-pickup")
            || ArenaBasics.TryMoveHomeward(
                contract,
                mind,
                body,
                destination,
                claims,
                "custody:emergency-pickup-fallback");
    }

    private Dictionary<int, GenericActorContext.ArcRelayCoreState>
        AllocateCorePickups(
            MindContext mind,
            TacticalPlaybookPackage package,
            TacticalSnapshot snapshot,
            IReadOnlyDictionary<int, string> roles,
            IReadOnlyDictionary<int, TacticalPlaybookPackage.Order> orders,
            IReadOnlyCollection<GenericActorContext.ArcRelayCoreState> loose,
            out HashSet<string> openSourceWells)
    {
        GenericActorContext.ArcRelayCoreState[] targets = loose
            .OrderBy(core => core.CoreId.SourceWellId, StringComparer.Ordinal)
            .ThenBy(core => core.CoreId.SourceOrdinal)
            .ToArray();
        HashSet<string> targetIds = targets
            .Select(core => CoreKey(core.CoreId))
            .ToHashSet(StringComparer.Ordinal);
        foreach (string stale in _coreReservations
                     .Where(value => !targetIds.Contains(value.Key)
                         || mind.Tick >= value.Value.ExpiresTick)
                     .Select(value => value.Key).ToArray())
        {
            _coreReservations.Remove(stale);
            if (targetIds.Contains(stale))
                _emergencyRecoveries.Remove(stale);
        }

        var eligible = new Dictionary<int,
            (MindBody Body, TacticalPlaybookPackage.CustodyPolicy Policy,
                int CarrierRank, bool SafeConversion,
                bool EmergencyRecovery)>();
        foreach (MindBody body in mind.Bodies.OrderBy(body => body.UnitId))
        {
            TacticalPlaybookPackage.Order order = orders[body.UnitId];
            if (string.IsNullOrEmpty(order.CustodyId))
                continue;
            TacticalPlaybookPackage.Role role = package.Source.Roles.Single(
                value => value.RoleId == roles[body.UnitId]);
            int carrierRank = TacticalCustodyPrimitives
                .CarrierPreferenceRank(role.CarrierPreference);
            if (carrierRank == int.MaxValue)
                continue;
            TacticalPlaybookPackage.CustodyPolicy policy = package.Source
                .CustodyPolicies.Single(value => value.CustodyId
                    == order.CustodyId);
            bool safeConversion = policy.SafeConversionAll.Any(group =>
                TacticalPlaybookMachine.Matches(group,
                    condition => Evaluate(condition, snapshot, package)));
            bool emergencyRecovery = policy.EmergencyRecoveryAll is
                    { Length: > 0 } emergencyConditions
                && policy.EmergencyRecoveryRoles is
                    { Length: > 0 } emergencyRoles
                && emergencyRoles.Contains(
                    roles[body.UnitId], StringComparer.Ordinal)
                && emergencyConditions.Any(group =>
                    TacticalPlaybookMachine.Matches(group,
                        condition => Evaluate(condition, snapshot, package)));
            if (!policy.AuthorizedCarrierRoles.Contains(
                    roles[body.UnitId], StringComparer.Ordinal)
                || !safeConversion && !emergencyRecovery)
            {
                continue;
            }
            eligible[body.UnitId] = (
                body, policy, carrierRank, safeConversion, emergencyRecovery);
        }
        openSourceWells = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in eligible.Values)
        {
            if (value.SafeConversion)
                openSourceWells.UnionWith(value.Policy.SourceWells);
            if (value.EmergencyRecovery
                && value.Policy.EmergencyRecoverySourceWells is
                    { Length: > 0 } emergencyWells)
            {
                openSourceWells.UnionWith(emergencyWells);
            }
        }

        var allocations = new Dictionary<int,
            GenericActorContext.ArcRelayCoreState>();
        foreach (GenericActorContext.ArcRelayCoreState core in targets)
        {
            string key = CoreKey(core.CoreId);
            if (!_coreReservations.TryGetValue(
                    key, out CoreReservation? reservation))
                continue;
            KeyValuePair<int,
                (MindBody Body, TacticalPlaybookPackage.CustodyPolicy Policy,
                    int CarrierRank, bool SafeConversion,
                    bool EmergencyRecovery)>
                retained = eligible.FirstOrDefault(value =>
                    value.Value.Body.ActorId == reservation.ActorId
                    && string.Equals(value.Value.Policy.CustodyId,
                        reservation.CustodyId, StringComparison.Ordinal)
                    && MayUseSourceWell(
                        core,
                        value.Value.Policy,
                        value.Value.SafeConversion,
                        value.Value.EmergencyRecovery)
                    && MayAssignCore(
                        package,
                        core,
                        value.Value.Body,
                        value.Value.Policy,
                        value.Value.SafeConversion,
                        value.Value.EmergencyRecovery)
                    && MayRecoverCore(
                        key,
                        value.Value.Policy,
                        value.Value.Body.ActorId,
                        value.Value.SafeConversion
                            || value.Value.EmergencyRecovery));
            if (retained.Value.Body is null
                || allocations.ContainsKey(retained.Key))
            {
                _coreReservations.Remove(key);
                continue;
            }
            allocations[retained.Key] = core;
        }

        foreach (GenericActorContext.ArcRelayCoreState core in targets
                     .Where(core => !_coreReservations.ContainsKey(
                         CoreKey(core.CoreId))))
        {
            KeyValuePair<int,
                (MindBody Body, TacticalPlaybookPackage.CustodyPolicy Policy,
                    int CarrierRank, bool SafeConversion,
                    bool EmergencyRecovery)>
                selected = eligible
                    .Where(value => !allocations.ContainsKey(value.Key)
                        && MayUseSourceWell(
                            core,
                            value.Value.Policy,
                            value.Value.SafeConversion,
                            value.Value.EmergencyRecovery)
                        && MayAssignCore(
                            package,
                            core,
                            value.Value.Body,
                            value.Value.Policy,
                            value.Value.SafeConversion,
                            value.Value.EmergencyRecovery)
                        && MayRecoverCore(
                            CoreKey(core.CoreId),
                            value.Value.Policy,
                            value.Value.Body.ActorId,
                            value.Value.SafeConversion
                                || value.Value.EmergencyRecovery))
                    .OrderBy(value => value.Value.CarrierRank)
                    .ThenBy(value => value.Value.Body.Position
                        .ChebyshevDistance(core.Position))
                    .ThenBy(value => value.Key)
                    .FirstOrDefault();
            if (selected.Value.Body is null)
                continue;
            allocations[selected.Key] = core;
            _coreReservations[CoreKey(core.CoreId)] = new CoreReservation(
                selected.Value.Body.ActorId,
                mind.Tick + selected.Value.Policy.PickupReservationTicks,
                selected.Value.Policy.CustodyId);
            if (IsEmergencyCore(
                    package,
                    core,
                    selected.Value.Body,
                    selected.Value.Policy,
                    selected.Value.EmergencyRecovery))
            {
                _emergencyRecoveries[CoreKey(core.CoreId)] =
                    new EmergencyRecovery(
                        selected.Value.Body.ActorId,
                        selected.Value.Policy.CustodyId);
            }
        }
        return allocations;
    }

    private static bool MayAssignCore(
        TacticalPlaybookPackage package,
        GenericActorContext.ArcRelayCoreState core,
        MindBody body,
        TacticalPlaybookPackage.CustodyPolicy policy,
        bool safeConversion,
        bool emergencyRecovery) => safeConversion
        || IsEmergencyCore(
            package, core, body, policy, emergencyRecovery);

    private static bool IsEmergencyCore(
        TacticalPlaybookPackage package,
        GenericActorContext.ArcRelayCoreState core,
        MindBody body,
        TacticalPlaybookPackage.CustodyPolicy policy,
        bool emergencyRecovery) => emergencyRecovery
        && policy.EmergencyRecoveryZones is { Length: > 0 } zones
        && zones.Any(zone => package.Contains(zone, core.Position))
        && body.Position.ChebyshevDistance(core.Position)
            <= policy.EmergencyPickupRadius;

    private static bool MayUseSourceWell(
        GenericActorContext.ArcRelayCoreState core,
        TacticalPlaybookPackage.CustodyPolicy policy,
        bool safeConversion,
        bool emergencyRecovery) => safeConversion
        && policy.SourceWells.Contains(
            core.CoreId.SourceWellId, StringComparer.Ordinal)
        || emergencyRecovery
        && policy.EmergencyRecoverySourceWells is
            { Length: > 0 } emergencySourceWells
        && emergencySourceWells.Contains(
            core.CoreId.SourceWellId, StringComparer.Ordinal);

    private bool MayRecoverCore(
        string coreKey,
        TacticalPlaybookPackage.CustodyPolicy policy,
        ActorIdentity candidate,
        bool safeConversion) => !_friendlyDroppedCores.TryGetValue(
            coreKey, out FriendlyDroppedCore? dropped)
        || TacticalCustodyPrimitives.MayRecoverDrop(
            policy.DropRecovery,
            candidate,
            dropped.SourceCarrier,
            safeConversion);

    private bool TryCombatSignatureWithReturn(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        GenericActorContext.ObservedEnemyState target,
        Position assignment,
        FocusAssignment focus,
        TacticalPlaybookPackage.Engagement policy,
        string reason)
    {
        bool acted = TryCombatSignature(
            contract, mind, body, target, assignment, policy, reason);
        if (acted)
            TrackSelfDefenseReturn(body, focus, policy);
        return acted;
    }

    private static bool TryCombatSignature(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        GenericActorContext.ObservedEnemyState target,
        Position assignment,
        TacticalPlaybookPackage.Engagement policy,
        string reason) => policy.SignatureCoordination != "none"
        && (TrySignatureCategory(
                contract, mind, body, target, assignment, "damage", reason)
            || TrySignatureCategory(
                contract, mind, body, target, assignment, "control", reason)
            || TrySignatureCategory(
                contract, mind, body, target, assignment, "support", reason));

    /// <summary>
    /// One combat-category entry per signature: its category and how to cast
    /// it at a focus target. Categories follow each signature's designed
    /// role, not its owner's current job: damage resolves hits, control
    /// shapes space and actions, support protects and reveals. Array order
    /// is the deterministic attempt order within a category.
    /// This table, <see cref="RoleHandledSignatures"/>, and
    /// <see cref="UnwiredSignatures"/> must jointly cover every signature in
    /// the rules contract — <see cref="AssertSignatureCoverage"/> enforces
    /// that at match start, so a newly added class fails loudly here instead
    /// of silently never casting.
    /// </summary>
    private sealed record SignaturePlay(
        string Kind,
        string Category,
        Func<GenericActorResolvedMatchContract, MindContext, MindBody,
            GenericActorContext.ObservedEnemyState, Position, string, string,
            bool> Cast);

    private static readonly SignaturePlay[] SignaturePlays =
    [
        new("rail-line", "damage", static (c, _, b, t, _, kind, r) =>
            ArenaBasics.TryHeadingSignature(c, b, kind, t.Position, r)),
        new("falling-star", "damage", static (c, _, b, t, _, kind, r) =>
            ArenaBasics.TryPositionSignature(c, b, kind, t.Position, r)),
        // A Sentinel is a sustained gun: worth deploying while the fight is
        // near enough (turret range 4 plus approach) to spend its duration.
        new("sentinel-seed", "damage", static (c, _, b, t, _, kind, r) =>
            b.Position.ChebyshevDistance(t.Position) <= 6
            && ArenaBasics.TryPositionSignature(c, b, kind, t.Position, r)),
        new("kinetic-burst", "damage", static (c, _, b, t, _, kind, r) =>
            b.Position.ChebyshevDistance(t.Position) <= 1
            && ArenaBasics.TryParameterlessSignature(c, b, kind, r)),
        new("target-paint", "control", static (c, _, b, t, _, kind, r) =>
            ArenaBasics.TryUnitSignature(c, b, kind, t.ActorId, r)),
        new("tractor-hook", "control", static (c, _, b, t, _, kind, r) =>
            ArenaBasics.TryHeadingSignature(c, b, kind, t.Position, r)),
        // A mine on the approach side only pays off when the enemy is close
        // enough to walk it; placement legality clamps to adjacent tiles.
        new("trip-node", "control", static (c, _, b, t, _, kind, r) =>
            b.Position.ChebyshevDistance(t.Position) <= 4
            && ArenaBasics.TryPositionSignature(c, b, kind, t.Position, r)),
        new("null-field", "control", static (c, _, b, t, _, kind, r) =>
            b.Position.ChebyshevDistance(t.Position) <= 3
            && ArenaBasics.TryParameterlessSignature(c, b, kind, r)),
        new("hardlight-block", "control",
            static (c, _, b, _, assignment, kind, r) =>
                ArenaBasics.TryPositionSignature(c, b, kind, assignment, r)),
        new("prism-wall", "support", static (c, _, b, t, _, kind, r) =>
            ArenaBasics.TryDirectionSignature(c, b, kind, t.Position, r)),
        // Smoke on the threat: denies its sightline through the cloud while
        // the rest of the team repositions.
        new("smoke-canister", "support", static (c, _, b, t, _, kind, r) =>
            b.Position.ChebyshevDistance(t.Position) <= 6
            && ArenaBasics.TryPositionSignature(c, b, kind, t.Position, r)),
        new("survey-flare", "support", static (c, _, b, t, _, kind, r) =>
            ArenaBasics.TryPositionSignature(c, b, kind, t.Position, r)),
        new("exchange", "support", TryExchangeOut),
    ];

    /// <summary>
    /// Signatures deliberately owned by dedicated logic instead of the
    /// combat-category table: movement (vector-dash), custody (arc-toss),
    /// and the medic channel (repair-beam).
    /// </summary>
    private static readonly HashSet<string> RoleHandledSignatures = new(
        StringComparer.Ordinal)
        { "vector-dash", "arc-toss", "repair-beam" };

    /// <summary>
    /// Signatures this executor cannot cast yet. Fielding a class whose kit
    /// lives here is refused at match start rather than silently played
    /// without its signature. Currently empty; the set stays so a future
    /// class can be parked here deliberately instead of forgotten.
    /// </summary>
    private static readonly HashSet<string> UnwiredSignatures = new(
        StringComparer.Ordinal);

    /// <summary>
    /// The Switchback escape swap: a hurt caster trades places with a
    /// healthier visible teammate standing meaningfully farther from the
    /// focus target, so a fresh body holds the line while the caster
    /// recovers. Legality (ally, range 6, visibility) comes from the
    /// action's own constraints.
    /// </summary>
    private static bool TryExchangeOut(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        GenericActorContext.ObservedEnemyState target,
        Position assignment,
        string kind,
        string reason)
    {
        if (body.Health * 2 >= MaxHealth(contract, body))
            return false;
        GenericActorRulesContract.ArcRelaySignature? signature =
            ArenaBasics.Signature(contract, kind);
        GenericActorActionLegality? action = signature is null
            ? null : body.Action(signature.ActionId);
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            allowed = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint>()
                .SingleOrDefault();
        if (action is not { Available: true } || allowed is null)
            return false;
        int ownDistance = body.Position.ChebyshevDistance(target.Position);
        MindBody? relief = mind.Bodies
            .Where(candidate => allowed.AllowedValues.Contains(
                new GenericActorActionArgument.UnitTarget(
                    candidate.ActorId.TeamId, candidate.UnitId)))
            .Where(candidate => candidate.Health > body.Health
                && candidate.Position.ChebyshevDistance(target.Position)
                    >= ownDistance + 2)
            .OrderByDescending(candidate =>
                candidate.Position.ChebyshevDistance(target.Position))
            .ThenBy(candidate => candidate.UnitId)
            .FirstOrDefault();
        return relief is not null
            && ArenaBasics.TryUnitSignature(
                contract, body, kind, relief.ActorId, reason);
    }

    private static bool TrySignatureCategory(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        GenericActorContext.ObservedEnemyState target,
        Position assignment,
        string category,
        string reason)
    {
        // Grammar-2 contracts carry designed-role metadata: dispatch from
        // the contract itself, so a class this executor has never heard of
        // plays correctly. The hand table remains for grammar-1 contracts
        // and for judgment plays metadata cannot express (the Switchback
        // escape swap).
        GenericActorRulesContract.ArcRelaySignature[] annotated =
            (ArenaBasics.ArcRules(contract)?.Signatures
                ?? Enumerable.Empty<
                    GenericActorRulesContract.ArcRelaySignature>())
            .Where(signature => signature.Category is not null)
            .ToArray();
        if (annotated.Length > 0)
        {
            foreach (GenericActorRulesContract.ArcRelaySignature signature in
                annotated)
            {
                if (!string.Equals(
                        signature.Category, category, StringComparison.Ordinal))
                    continue;
                if (TryMetadataCast(
                        contract, mind, body, target, assignment, signature,
                        reason))
                    return true;
            }
            return false;
        }
        foreach (SignaturePlay play in SignaturePlays)
        {
            if (string.Equals(play.Category, category, StringComparison.Ordinal)
                && play.Cast(contract, mind, body, target, assignment,
                    play.Kind, reason))
                return true;
        }
        return false;
    }

    private static bool TryMetadataCast(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        GenericActorContext.ObservedEnemyState target,
        Position assignment,
        GenericActorRulesContract.ArcRelaySignature signature,
        string reason)
    {
        if (string.Equals(signature.Kind, "exchange", StringComparison.Ordinal))
            return TryExchangeOut(
                contract, mind, body, target, assignment, signature.Kind,
                reason);
        int range = signature.EngagementRange ?? int.MaxValue;
        if (body.Position.ChebyshevDistance(target.Position) > range)
            return false;
        return signature.ArgumentKind switch
        {
            // A heading cast needs exact ray alignment; when the focus
            // target is off-ray, sweep the other visible hostiles so a hook
            // or rail fires at whoever IS aligned instead of idling (the
            // stock mind casts ~40 hooks a game to this executor's 0-2).
            "heading" => ArenaBasics.TryHeadingSignature(
                contract, body, signature.Kind, target.Position, reason)
                || mind.Enemies
                    .Where(enemy => enemy.ActorId != target.ActorId
                        && body.Position.ChebyshevDistance(enemy.Position)
                            <= range)
                    .OrderBy(enemy =>
                        body.Position.ChebyshevDistance(enemy.Position))
                    .ThenBy(enemy => ArenaBasics.FrameY(
                        enemy.Position,
                        ArenaBasics.MirroredFrame(contract, mind)))
                    .ThenBy(enemy => ArenaBasics.FrameX(
                        enemy.Position,
                        ArenaBasics.MirroredFrame(contract, mind)))
                    .Any(enemy => ArenaBasics.TryHeadingSignature(
                        contract, body, signature.Kind, enemy.Position,
                        reason)),
            "position" => ArenaBasics.TryPositionSignature(
                contract, body, signature.Kind,
                string.Equals(
                    signature.Kind, "hardlight-block", StringComparison.Ordinal)
                    ? assignment
                    : target.Position,
                reason,
                mirrored: ArenaBasics.MirroredFrame(contract, mind)),
            "unit" => ArenaBasics.TryUnitSignature(
                contract, body, signature.Kind, target.ActorId, reason),
            "direction" => ArenaBasics.TryDirectionSignature(
                contract, body, signature.Kind, target.Position, reason,
                mirrored: ArenaBasics.MirroredFrame(contract, mind)),
            "parameterless" => ArenaBasics.TryParameterlessSignature(
                contract, body, signature.Kind, reason),
            _ => false,
        };
    }

    private static bool TryLeadSignature(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        GenericActorContext.ObservedEnemyState target,
        Position assignment,
        TacticalPlaybookPackage.Engagement policy,
        string reason) => policy.SignatureCoordination switch
    {
        // The lead category fires BEFORE the basic gun, so a wall goes up
        // or a hook lands even when a shot is available.
        "control-first" => TrySignatureCategory(
            contract, mind, body, target, assignment, "control", reason),
        "support-first" => TrySignatureCategory(
            contract, mind, body, target, assignment, "support", reason),
        _ => false,
    };

    private bool TryFocusChannelWithReturn(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        FocusAssignment focus,
        Position assignment,
        TacticalPlaybookPackage.Engagement policy,
        string signatureReason)
    {
        bool mayPrepareAim = string.Equals(
            policy.AimPreparation,
            "rotate-to-engage",
            StringComparison.Ordinal);
        bool mayFireNow = ArenaBasics.CanFireAtPosition(
            contract, body, focus.AimPosition);
        bool acted = (mayPrepareAim || mayFireNow)
            && TryLeadSignature(
                contract, mind, body, focus.Target, assignment, policy,
                signatureReason)
            || (mayPrepareAim || mayFireNow)
            && ArenaBasics.TryShootAtPosition(
                contract, mind, body, focus.AimPosition,
                $"focus {focus.Target.ActorId}",
                declaredTarget: focus.Target.ActorId)
            || policy.SignatureCoordination != "none"
            && (mayPrepareAim || mayFireNow)
            && TryCombatSignature(
                contract, mind, body, focus.Target, assignment, policy,
                signatureReason);
        if (acted)
            TrackSelfDefenseReturn(body, focus, policy);
        return acted;
    }

    private void TrackSelfDefenseReturn(
        MindBody body,
        FocusAssignment focus,
        TacticalPlaybookPackage.Engagement policy)
    {
        if (focus.SelfDefenseExcursion
            && policy.SelfDefense.ReturnToFormation)
        {
            _returningToFormation[body.UnitId] = body.ActorId;
        }
    }

    private static bool TryAdvanceSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Position target) => body.Position.ChebyshevDistance(target) >= 3
        && ArenaBasics.TryHeadingSignature(
            contract, body, "vector-dash", target, "movement:vector-dash");

    /// <summary>
    /// Role-keyed default build under veterancy rules: sharpshooters buy
    /// damage, eyes buy vision, everyone else buys vitality (which heal
    /// zones make recoverable).
    /// </summary>
    private static string DefaultBuildTrack(string roleId) => roleId switch
    {
        "sharp" or "hook" => "damage",
        "eyes" => "vision",
        _ => "vitality",
    };

    /// <summary>
    /// The skirmish posture's range band: inside the self-defense threat
    /// distance the body steps out rather than trading, at range it stands
    /// and fires. The step preempts focus-fire (the channel checks this
    /// predicate), because a kiter that finishes its aim first is just a
    /// fragile fighter - against an opponent that halts to shoot, the band
    /// yields the fire-while-withdrawing rhythm: they stop, we gain ground
    /// and shoot; they close, we step.
    /// </summary>
    /// <summary>
    /// Withdraw discipline (ghost doctrine v2): a body whose commit latch
    /// tripped breaks toward the sheet's named withdraw destination until
    /// the threat picture thins back to the engage gate. Falls through to
    /// ordinary movement when the sheet names nowhere (or the id resolves
    /// to nothing on this layout).
    /// </summary>
    private bool TryWithdraw(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        TacticalPlaybookPackage package,
        MindBody body,
        TacticalPlaybookPackage.Engagement engagement,
        ArenaBasics.Claims claims,
        string reason)
    {
        if (engagement.Commit?.DisengageWhen?.WithdrawTo is not string named)
            return false;
        (int LifeId, int UntilTick) latch =
            _disengaging.GetValueOrDefault(body.UnitId);
        if (latch.LifeId != body.ActorId.LifeId
            || mind.Tick >= latch.UntilTick)
        {
            return false;
        }
        Position[] points = package.WithdrawPoints(named);
        // A withdraw destination at the body's own feet is a no-op that
        // suppresses fighting while going nowhere (the camping-ghost
        // scene, owner catch 2026-08). Withdrawal means AWAY: skip points
        // already underfoot and take the one farthest from the nearest
        // threat.
        // The rally is STICKY: chosen once when the latch trips, kept
        // until release or arrival. Re-picking per tick made the
        // farthest-from-threat choice flip as enemies moved, and the ghost
        // paced two tiles forever - the dance the owner caught (ab69
        // trace: 'withdraw via North / via South' alternating every tick).
        (int LifeId, Position Rally) rally =
            _withdrawRallies.GetValueOrDefault(body.UnitId);
        if (rally.LifeId != body.ActorId.LifeId)
        {
            Position[] away = [.. points
                .Where(point => body.Position.ChebyshevDistance(point) > 2)];
            if (away.Length == 0)
                return false;
            rally = (body.ActorId.LifeId, away
                .OrderByDescending(point => mind.Enemies
                    .Select(enemy =>
                        enemy.Position.ChebyshevDistance(point))
                    .DefaultIfEmpty(int.MaxValue)
                    .Min())
                .ThenBy(point => body.Position.ChebyshevDistance(point))
                .ThenBy(point => ArenaBasics.FrameY(point, _mirrored))
                .ThenBy(point => ArenaBasics.FrameX(point, _mirrored))
                .First());
            _withdrawRallies[body.UnitId] = rally;
        }
        if (body.Position.ChebyshevDistance(rally.Rally) <= 1)
        {
            // Arrived and still latched: hold the rally rather than resume
            // the route into the same threats.
            return Hold(body, reason);
        }
        return ArenaBasics.TryMoveHomeward(
            contract, mind, body, rally.Rally, claims, reason);
    }

    /// <summary>
    /// Patrol scan (ghost doctrine v2): a body on post under a movement
    /// with scan: sweep, with no enemy in sight nearby, rotates a quadrant
    /// every few ticks so its vision cone covers the approaches instead of
    /// staring down one corridor. Staggered by unit so two scouts never
    /// stare the same way.
    /// </summary>
    private static bool TryScanSweep(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        TacticalPlaybookPackage.Order order,
        Position target,
        string reason)
    {
        if (!string.Equals(
                order.Movement.Scan, "sweep", StringComparison.Ordinal))
        {
            return false;
        }
        if (body.Position.ChebyshevDistance(target)
            > order.Movement.ArrivalRadius + 1)
        {
            return false;
        }
        if (mind.Enemies.Any(enemy =>
                enemy.Position.ChebyshevDistance(body.Position) <= 8))
        {
            return false;
        }
        return TryRotate(
            contract,
            body,
            (Direction)(((mind.Tick / 8) + body.UnitId) % 4),
            reason);
    }

    private static bool TryFaceTarget(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Position target,
        string reason)
    {
        int dx = target.X - body.Position.X;
        int dy = target.Y - body.Position.Y;
        if (dx == 0 && dy == 0)
            return false;
        return TryRotate(
            contract,
            body,
            Math.Abs(dx) >= Math.Abs(dy)
                ? dx >= 0 ? Direction.East : Direction.West
                : dy >= 0 ? Direction.South : Direction.North,
            reason);
    }

    /// <summary>Turn to face <paramref name="desired"/>, if the current form
    /// may and is not already facing it.</summary>
    private static bool TryRotate(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Direction desired,
        string reason)
    {
        if (body.Facing == desired)
            return false;
        GenericActorRulesContract.ActionDefinition? definition =
            contract.Rules.Actions.FirstOrDefault(value =>
                value.Kind == GenericActorRulesContract.ActionKind.Rotation);
        GenericActorActionLegality? action = definition is null
            ? null : body.Action(definition.Id);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = action?.Constraints.OfType<
                GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>().SingleOrDefault();
        if (action is not { Available: true } || directions is null
            || !directions.AllowedValues.Contains(desired))
            return false;
        body.Command(action.ActionId, action.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(desired)], reason);
        return true;
    }

    private bool Hold(MindBody body, string reason)
    {
        body.Hold(reason);
        return true;
    }

    /// <summary>
    /// Carrier-lane relief - all that remains of the no-idle watchdog (owner
    /// ruling 2026-08-07: the general invariant is removed, standing is sheet
    /// policy again). This is not about idling. It is about BLOCKING: the
    /// ab51 pocket family (owner replay finding, "it's friendly bodies
    /// blocking it") was an escort whose post resolved onto a loaded
    /// carrier's only bankward corridor tile - on post forever, corridor
    /// plugged 114 ticks with no enemy in sight. Whole-map path existence was
    /// tried and measured wrong (ab53): winding detours exist, but the
    /// carrier's movement policy refuses distance-increasing steps, so it
    /// never takes them. The plug test asks the policy's own question - is
    /// this body standing on an admissible homeward step while every such
    /// step is taken - and after two ticks of that the plug displaces away
    /// from the own reactor, which is exactly off the carrier's route.
    /// Fires ONLY beside an own carrier whose route this body plugs; a body
    /// standing anywhere else is the sheet's business now.
    /// </summary>
    private bool TryCarrierLaneRelief(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        IReadOnlySet<int> carrierUnitIds,
        IReadOnlySet<int> repairerUnitIds,
        IReadOnlyDictionary<int, Position> targets,
        IReadOnlySet<int> focusUnitIds,
        ArenaBasics.Claims claims,
        string reason)
    {
        if (repairerUnitIds.Contains(body.UnitId) || body.Cooldown > 0)
            return false;
        // Who yields to whom: a carrier outranks everyone, a body in a fight
        // outranks an idler, and equal ranks yield to the higher unit id so
        // exactly one of a mutual pair moves. Carriers never yield.
        int Rank(int unit) => carrierUnitIds.Contains(unit) ? 0
            : focusUnitIds.Contains(unit) ? 1
            : 2;
        if (Rank(body.UnitId) == 0)
            return false;
        // Whose way is it standing in: an adjacent own body that WANTS this
        // tile - its own next step toward its own target lands here - and
        // that outranks this one. That is the whole trigger. It is not the
        // no-idle watchdog reborn: a body standing anywhere nobody needs is
        // left alone forever, which is the sheet's business.
        MindBody[] blocked = mind.Bodies
            .Where(other => other.UnitId != body.UnitId
                && other.Position.ChebyshevDistance(body.Position) <= 1
                && (Rank(other.UnitId) < Rank(body.UnitId)
                    || (Rank(other.UnitId) == Rank(body.UnitId)
                        && other.UnitId < body.UnitId))
                && targets.TryGetValue(other.UnitId, out Position goal)
                && ArenaBasics.StaticFirstStep(contract, mind, other, goal)
                    == body.Position)
            .ToArray();
        (int LifeId, Position Tile, int Ticks) plug =
            _lanePlugTicks.GetValueOrDefault(body.UnitId);
        if (blocked.Length == 0)
        {
            _lanePlugTicks.Remove(body.UnitId);
            return false;
        }
        int ticks = plug.LifeId == body.ActorId.LifeId
            && plug.Tile == body.Position
                ? plug.Ticks + 1
                : 1;
        _lanePlugTicks[body.UnitId] =
            (body.ActorId.LifeId, body.Position, ticks);
        // A loaded carrier's lane clears a tick sooner - that was the
        // measured ab51 harm and it keeps its priority.
        bool carrierWaiting = blocked.Any(other =>
            carrierUnitIds.Contains(other.UnitId));
        if (ticks < (carrierWaiting ? 1 : CarrierLanePatience))
            return false;
        if (!ArenaBasics.TryStepAway(
                contract, mind, body, [body.Position, _ownReactor],
                claims, $"lane-relief:{reason}"))
        {
            return false;
        }
        _lanePlugTicks.Remove(body.UnitId);
        _laneReliefs++;
        return true;
    }

    private static string Provenance(
        TacticalPlaybookMachine machine,
        string group,
        TacticalPlaybookPackage.Order order,
        string channel) =>
        $"tp:{machine.PhaseId}:{group}:{order.OrderId}:{channel}";

    /// <summary>
    /// A movement diagnostic, with the RESOLVED destination attached.
    /// </summary>
    /// <remarks>
    /// Owner review 2026-08-09: "I still see a lot of pathing mistakes but
    /// can't put a finger on it." A reason that says <c>formation-move via
    /// West</c> answers which way the body stepped and never answers where it
    /// believed it was going — so a body walking confidently at the wrong tile
    /// and a body oscillating between two tiles read identically. Naming the
    /// destination turns both into something a spectator can point at.
    ///
    /// The <c>@x,y</c> tail is machine-readable ON PURPOSE and is the only
    /// structured suffix the vocabulary has besides <c>via</c>: the broadcast
    /// projection lifts it into its own delta column
    /// (<c>scripts/arc-relay-broadcast.py</c>) and the viewer draws it for the
    /// SELECTED body only. It goes on the channel rather than after
    /// <c>via</c> because both reason parsers cut the string at <c>' via '</c>
    /// and would drop anything behind it.
    ///
    /// Only the route/formation movement plane carries it. A body closing on a
    /// focus or slipping into ambush cover is walking at something this
    /// destination is not, and publishing the order's target there would be a
    /// confident lie.
    /// </remarks>
    private static string MovementProvenance(
        TacticalPlaybookMachine machine,
        string group,
        TacticalPlaybookPackage.Order order,
        string channel,
        Position destination) =>
        $"{Provenance(machine, group, order, channel)}"
        + $"@{destination.X},{destination.Y}";

    // The tag is the spectator's answer to "what does this unit think it is
    // doing" (owner review 2026-08-09: a guard reading as a bug because
    // intent was illegible). Order ids are already job-shaped - race-north,
    // guard-home, ghost-stalk - so the tag IS the current order, with the
    // role only as a prefix when it adds information.
    private static string RoleTag(
        string phase,
        string group,
        string role,
        string order,
        bool escorting)
    {
        _ = phase;
        _ = group;
        _ = role;
        // The arena badge truncates past 14 characters, and every order id
        // is shorter than that: the order alone is the whole caption. A
        // body riding someone else's order says so — "ghost-assault" on two
        // units reads as two ghosts. The suffix survives the 24-byte tag
        // cap; the ORDER gives way, because "-escort" is the new fact.
        // (Tags are lowercase-kebab semantic ids, so the separator is a
        // dash rather than a space.)
        string value = order.ToLowerInvariant();
        const string suffix = "-escort";
        if (escorting)
        {
            if (value.Length + suffix.Length > 24)
                value = value[..(24 - suffix.Length)];
            value = value.TrimEnd('-') + suffix;
        }
        return (value.Length <= 24 ? value : value[..24]).TrimEnd('-');
    }

    private static string CoreKey(GenericActorContext.ArcRelayCoreId id) =>
        $"{id.SourceWellId}:{id.SourceOrdinal}";

    /// <summary>One escorted order's live cast for one tick.</summary>
    private sealed record EscortParty(
        MindBody Leader,
        Position? LeaderStep,
        EscortMember[] Followers);

    /// <param name="Ordinal">Position in the order's follower list, which is
    /// both this follower's precedence and what its posture reads to space
    /// a file of several.</param>
    private sealed record EscortMember(
        MindBody Body,
        TacticalPlaybookPackage.EscortFollower Policy,
        int Ordinal);

    private sealed record LastSeenEnemy(
        ActorIdentity ActorId,
        Position Position,
        int LastConfirmedTick,
        Position? PreviousPosition,
        int? PreviousConfirmedTick,
        bool IsCarrier);

    private sealed record SecuredCore(
        string SourceWellId,
        Position Position,
        int LastConfirmedTick);

    private sealed record RouteProgress(
        ActorIdentity ActorId,
        string OrderId,
        int Index);

    /// <summary>
    /// A body's recent ground, and how long it has failed to make any.
    /// </summary>
    /// <remarks>
    /// TWO tiles of history, not one. "Stuck" used to mean "standing on the
    /// tile I stood on last tick", which cannot see the failure mode that
    /// actually produces the dance a spectator complains about: a body stepping
    /// A -> B -> A -> B forever. Its tile changes every tick, so every recovery
    /// downstream of this counter — repath, reflow, the wedge shake — was blind
    /// to it, and a body could burn a hundred ticks four tiles from its own
    /// reactor while the machinery reported healthy progress (owner catch
    /// 2026-08-09, e-9004 u3 at t214-t224: eleven ticks alternating between
    /// (28,14) and (28,15) with an unchanging destination at (23,17)).
    ///
    /// Remembering the tile before last closes the two-cycle exactly, and it
    /// cannot fire on real movement: a body walking A -> B -> C never returns
    /// to A. Longer cycles are deliberately NOT chased here — a three-tile loop
    /// is a route or a patrol as often as it is a bug, and this counter is not
    /// the place to decide which.
    /// </remarks>
    private sealed record MotionProgress(
        ActorIdentity ActorId,
        string OrderId,
        Position Position,
        Position Previous,
        int StuckTicks);

    /// <summary>One walk to one cold trail, ended by its budget or by
    /// arriving on ground the prey has already left.</summary>
    private sealed record HiddenFlush(
        ActorIdentity Target,
        int LastVisibleTick,
        int StartTick,
        bool Spent);

    private sealed record FocusLock(
        ActorIdentity ActorId,
        int LockedTick,
        int LastVisibleTick,
        int UnreachableTicks);

    private sealed record FocusAssignment(
        GenericActorContext.ObservedEnemyState Target,
        Position AimPosition,
        bool UseSignature,
        bool SelfDefenseExcursion,
        bool HoldDeclare = false);

    private sealed record CoreReservation(
        ActorIdentity ActorId,
        int ExpiresTick,
        string CustodyId);

    private sealed record EmergencyRecovery(
        ActorIdentity ActorId,
        string CustodyId);

    private sealed record CustodyProgress(
        ActorIdentity ActorId,
        string CoreKey,
        int StartedTick,
        Position Position,
        int StagnantTicks,
        /// <summary>Where this custody BEGAN — the tile the Core was lifted
        /// from. The delivery-route rules key off it, so a Core taken deep on
        /// the enemy half keeps its safe way home for the whole run.</summary>
        Position PickupPosition = default,
        /// <summary>The corridor waypoint this routed delivery is currently
        /// walking to, as an index into the resolved route, and the direction
        /// it is walking the loop. Sticky: a corridor is walked forward, so
        /// this only ever advances past waypoints the body has reached.
        /// <c>-1</c>/<c>0</c> mean nothing is committed yet.</summary>
        int RouteWaypoint = -1,
        int RouteForward = 0);

    private sealed record FriendlyDroppedCore(
        ActorIdentity SourceCarrier,
        Position Position,
        int DroppedTick);

    private sealed record TacticalSnapshot(
        int Tick,
        int PhaseStateTicks,
        int LiveFriendlies,
        int KnownEnemiesUnavailable,
        int SecuredCores,
        int VisibleLooseCores,
        int FriendlyCarriers,
        int VisibleEnemyCarriers,
        int KnownEnemyCarriers,
        int TicksWithoutObjectiveProgress,
        int ReactorIntegrity,
        int ReactorCharge,
        IReadOnlyDictionary<string, int> RoleLive,
        IReadOnlyDictionary<string, int> GroupLive,
        IReadOnlyDictionary<string, int> GroupJoining,
        IReadOnlyDictionary<string, int> GroupCohesion,
        IReadOnlyDictionary<string, int> GroupStuckTicks,
        IReadOnlyDictionary<string, int> FriendlyZones,
        IReadOnlyDictionary<string, Dictionary<string, int>> GroupZones,
        IReadOnlyDictionary<string, int> VisibleEnemiesByZone,
        IReadOnlyDictionary<string, int> RememberedEnemiesByZone,
        IReadOnlyDictionary<string, int> VisibleLooseCoresByZone,
        int LooseCoreValueMax,
        IReadOnlyDictionary<string, int> LooseCoreValueByZone,
        IReadOnlyDictionary<string, int> WellOutstanding,
        IReadOnlyDictionary<string, int> FormationStableTicks,
        IReadOnlyDictionary<string, int> FormationBroken,
        IReadOnlyDictionary<string, int> OwnSocketFilled,
        IReadOnlyDictionary<string, int> EnemySocketFilled,
        IReadOnlyDictionary<string, int> WellBirthIn,
        IReadOnlyDictionary<string, int> GroupMaxLevel);
}
