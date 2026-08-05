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
    private TacticalPlaybookPackage? _package;
    private TacticalPlaybookMachine? _machine;
    private TacticalTaskMachine? _tasks;
    private Position _ownReactor;
    private Position _enemyReactor;
    private string? _allocationPhaseId;
    private string? _queuedFallbackPhase;
    private int _teamId;
    private int _lastObjectiveProgressTick;
    private int _lastOwnCharge;

    public void StartMatch(MindStart start)
    {
        _contract = start.Contract;
        _teamId = start.TeamId;
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
        _package = TacticalPlaybookPackage.Load(
            start.EvaluationData, start.Contract, _ownReactor);
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
            AllocateFocus(contract, mind, arc, package.Source, roles, orders,
                targets, unavailableAttackers);
        GenericActorContext.ArcRelayCoreState[] loose = arc.VisibleCores
            .Where(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Loose)
            .OrderBy(core => core.CoreId.SourceWellId, StringComparer.Ordinal)
            .ThenBy(core => core.CoreId.SourceOrdinal)
            .ToArray();
        Dictionary<int, GenericActorContext.ArcRelayCoreState> pickupAssignments =
            AllocateCorePickups(
                mind, package, snapshot, roles, orders, loose);
        var claims = ArenaBasics.Claims.ForTick(mind);
        Dictionary<int, Position> carrierSteps = mind.Bodies
            .Where(body => carried.ContainsKey(body.ActorId))
            .Select(body => (
                Body: body,
                Step: ArenaBasics.StaticFirstStep(
                    contract, body, _ownReactor)))
            .Where(value => value.Step is not null)
            .ToDictionary(value => value.Body.UnitId, value => value.Step!.Value);
        HashSet<Position> carrierClearance = carrierSteps.Values.ToHashSet();
        if (carrierSteps.Count > 0)
            carrierClearance.Add(_ownReactor);

        foreach (MindBody body in mind.Bodies
                     .OrderByDescending(body => carried.ContainsKey(body.ActorId))
                     .ThenBy(body => orders[body.UnitId].Priority)
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
            body.SetRole(RoleTag(machine.PhaseId, group, role, order.OrderId));

            if (!carried.ContainsKey(body.ActorId)
                && carrierClearance.Contains(body.Position)
                && ArenaBasics.TryMoveAside(
                    contract, mind, body, claims, carrierClearance,
                    Provenance(machine, group, order,
                        "clear-custody-return-lane")))
            {
                continue;
            }

            bool acted = false;
            foreach (string channel in package.Source.Arbitration.Channels)
            {
                acted = channel switch
                {
                    "custody-emergency" => TryCustodyEmergency(
                        contract, mind, arc, package, body, role, order,
                        carried, pickupAssignments, claims),
                    "self-preservation" => TrySelfPreservation(
                        contract, mind, package.Source, body, order, claims),
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
                                "signature-idle")),
                    "focus-fire" => focus.TryGetValue(
                            body.UnitId, out FocusAssignment?
                                shotTarget)
                        && WithinEngagementLeash(
                            body, target, shotTarget.Target, engagement)
                        && TryFocusChannelWithReturn(
                            contract, mind, body, shotTarget, target,
                            engagement,
                            Provenance(machine, group, order, "signature")),
                    "movement" => TryMovement(
                        contract, mind, arc, package, machine, snapshot, body,
                        role, group, order, target, targets, groups,
                        orders,
                        pickupAssignments, claims),
                    "facing" => facingTarget is Position lookAt
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
                body.Hold(Provenance(machine, group, order, "exhausted"));
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
                    actorId, coreKey, tick, body.Position, 0);
            bool sameCustody = string.Equals(
                prior.CoreKey, coreKey, StringComparison.Ordinal);
            _custodyProgress[actorId] = new CustodyProgress(
                actorId,
                coreKey,
                sameCustody ? prior.StartedTick : tick,
                body.Position,
                sameCustody && prior.Position == body.Position
                    ? prior.StagnantTicks + 1
                    : 0);
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
        int carriers = arc.VisibleCores.Count(core =>
            core.Disposition == GenericActorContext.ArcRelayCoreDisposition.Carried
            && core.CarrierActorId?.TeamId == _teamId);
        int enemyCarriers = arc.VisibleCores.Count(core =>
            core.Disposition == GenericActorContext.ArcRelayCoreDisposition.Carried
            && core.CarrierActorId?.TeamId != _teamId);
        GenericActorContext.ArcRelayReactorState own = arc.Reactors.Single(
            reactor => reactor.TeamId == _teamId);
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
            arc.Wells.ToDictionary(
                well => well.WellId,
                well => well.OutstandingCoreId is null ? 0 : 1,
                StringComparer.Ordinal),
            _formationStableTicks.ToDictionary(StringComparer.Ordinal),
            _formationLifecycles.ToDictionary(
                value => value.Key,
                value => value.Value.Broken ? 1 : 0,
                StringComparer.Ordinal));
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
            "route" => RouteTarget(package, order, body),
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
            "secured-core" => SecuredCoreTarget(
                package, order),
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

    private TacticalPlaybookPackage.Placement FormationPlacement(
        TacticalPlaybookPackage.Formation formation,
        IReadOnlyDictionary<int, string> roles,
        int unitId,
        string role)
    {
        TacticalPlaybookPackage.Placement[] placements = formation.Placements
            .Where(value => value.RoleId == role)
            .OrderBy(value => value.Order).ToArray();
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
            return fallback;
        Position cutoff = TacticalCoordinationPrimitives
            .PredictReturnLaneCutoff(
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
                Position selected = TacticalFormationPrimitives
                    .SelectFormationTarget(
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
        TacticalPlaybookPackage package,
        TacticalPlaybookPackage.Order order,
        MindBody body)
    {
        Position[] route = package.RoutePoints(order.Movement.Target);
        RouteProgress state = _routes.GetValueOrDefault(body.UnitId)
            ?? new RouteProgress(body.ActorId, order.OrderId, 0);
        if (state.ActorId != body.ActorId
            || !string.Equals(state.OrderId, order.OrderId,
                StringComparison.Ordinal))
            state = new RouteProgress(body.ActorId, order.OrderId, 0);
        int index = Math.Min(state.Index, route.Length - 1);
        // A route is a corridor, not a sequence of single-tile queues. A body
        // reflowed to the edge of the declared corridor has completed that
        // waypoint just as surely as one standing on its centre tile. Using a
        // fixed radius of one stranded rear members around crowded waypoints.
        int arrival = Math.Max(
            Math.Max(1, order.Movement.ArrivalRadius),
            package.RouteCorridorWidth(order.Movement.Target));
        while (index < route.Length - 1
            && body.Position.ChebyshevDistance(route[index]) <= arrival)
            index++;
        _routes[body.UnitId] = state with { Index = index };
        return route[index];
    }

    private Dictionary<int, FocusAssignment> AllocateFocus(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        TacticalPlaybookPackage.Playbook playbook,
        IReadOnlyDictionary<int, string> roles,
        IReadOnlyDictionary<int, TacticalPlaybookPackage.Order> orders,
        IReadOnlyDictionary<int, Position> targets,
        IReadOnlySet<int> unavailableParticipants)
    {
        var allocations = new Dictionary<int, FocusAssignment>();
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
                if (participants.Length == 0 || mind.Enemies.Length == 0)
                    continue;
                GenericActorContext.ObservedEnemyState[] enemies = mind.Enemies
                    .OrderBy(enemy => enemy,
                        Comparer<GenericActorContext.ObservedEnemyState>.Create(
                            (left, right) => CompareTargets(
                                policy, left, right, carriers, participants,
                                mind.Tick)))
                    .ToArray();
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
                                contract, body, locked, policy,
                                carriers.Contains(locked.ActorId),
                                committedDamage: 0,
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
                    participants = participants.Where(body =>
                            !_returningToFormation.ContainsKey(body.UnitId))
                        .ToArray();
                }
                var committedDamage = new Dictionary<ActorIdentity, int>();
                var attackerCounts = new Dictionary<ActorIdentity, int>();
                var coveredOptions = new Dictionary<ActorIdentity,
                    HashSet<Position>>();
                foreach (MindBody body in participants
                             .OrderBy(body =>
                                 ArenaBasics.CanFireAtPosition(
                                     contract, body, primary.Position)
                                     ? 0 : 1)
                             .ThenBy(body => body.UnitId))
                {
                    GenericActorContext.ObservedEnemyState? selected =
                        targetOrder.FirstOrDefault(enemy =>
                            attackerCounts.GetValueOrDefault(enemy.ActorId)
                                < policy.MaximumAttackersPerTarget
                            && NeedsFocusAssignment(
                                policy,
                                enemy,
                                committedDamage.GetValueOrDefault(
                                    enemy.ActorId),
                                coveredOptions.GetValueOrDefault(enemy.ActorId)
                                    ?.Count ?? 0)
                            && WithinEngagementLeash(
                                body, targets[body.UnitId], enemy, policy)
                            && CanContributeToTarget(
                                contract, body, enemy, policy,
                                carriers.Contains(enemy.ActorId),
                                committedDamage.GetValueOrDefault(
                                    enemy.ActorId),
                                requireFireReady: true));
                    selected ??= targetOrder.FirstOrDefault(enemy =>
                        attackerCounts.GetValueOrDefault(enemy.ActorId)
                            < policy.MaximumAttackersPerTarget
                        && NeedsFocusAssignment(
                            policy,
                            enemy,
                            committedDamage.GetValueOrDefault(enemy.ActorId),
                            coveredOptions.GetValueOrDefault(enemy.ActorId)
                                ?.Count ?? 0)
                        && WithinEngagementLeash(
                            body, targets[body.UnitId], enemy, policy)
                            && CanContributeToTarget(
                                contract, body, enemy, policy,
                                carriers.Contains(enemy.ActorId),
                                committedDamage.GetValueOrDefault(enemy.ActorId),
                                requireFireReady: false));
                    if (selected is null)
                        continue;
                    Position aim = SelectCoverageAim(
                        contract, body, selected, policy,
                        carriers.Contains(selected.ActorId),
                        coveredOptions.GetValueOrDefault(selected.ActorId)
                            ?? [],
                        directDamageNeeded: committedDamage.GetValueOrDefault(
                            selected.ActorId) < (carriers.Contains(
                                selected.ActorId)
                                    ? Math.Min(
                                        selected.Health,
                                        policy.DodgeCoverage.MinimumDirectShots)
                                    : selected.Health));
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
                                policy.SelfDefense.ThreatDistance));
                    attackerCounts[selected.ActorId] = attackerCounts
                        .GetValueOrDefault(selected.ActorId) + 1;
                    HashSet<Position> coverage = coveredOptions
                        .GetValueOrDefault(selected.ActorId) ?? [];
                    foreach (Position option in EscapeOptions(
                                 contract, selected, policy))
                    {
                        if (SameShotLane(body.Position, aim, option))
                            coverage.Add(option);
                    }
                    coveredOptions[selected.ActorId] = coverage;
                    if (SameShotLane(
                            body.Position, aim, selected.Position)
                        && ArenaBasics.CanFireAtPosition(
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
        int committedDamage,
        int coveredOptions) => TacticalCoordinationPrimitives
        .NeedsFocusAssignment(
            target.Health,
            committedDamage,
            policy.OverkillDamage,
            string.Equals(policy.DodgeCoverage.Mode, "escape-lanes",
                StringComparison.Ordinal),
            coveredOptions,
            policy.DodgeCoverage.MinimumCoveredOptions);

    private static string? CombatSignatureKey(
        GenericActorResolvedMatchContract contract,
        MindBody body) => contract.Rules.Actions
        .Where(action => action.Kind
            == GenericActorRulesContract.ActionKind.Signature)
        .OrderBy(action => action.Code)
        .Select(action => body.Action(action.Id))
        .FirstOrDefault(action => action is { Available: true })
        ?.ActionId;

    private static bool CanContributeToTarget(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        GenericActorContext.ObservedEnemyState target,
        TacticalPlaybookPackage.Engagement policy,
        bool targetIsCarrier,
        int committedDamage,
        bool requireFireReady) => EscapeOptions(contract, target, policy)
        .Where(position => targetIsCarrier
            && string.Equals(policy.DodgeCoverage.Mode, "escape-lanes",
                StringComparison.Ordinal)
            || committedDamage >= target.Health
            || position == target.Position)
        .Any(position => requireFireReady
            ? ArenaBasics.CanFireAtPosition(contract, body, position)
            : ArenaBasics.CanAimAtPosition(contract, body, position));

    private Position SelectCoverageAim(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        GenericActorContext.ObservedEnemyState target,
        TacticalPlaybookPackage.Engagement policy,
        bool targetIsCarrier,
        IReadOnlySet<Position> alreadyCovered,
        bool directDamageNeeded)
    {
        Position[] options = EscapeOptions(contract, target, policy);
        bool predictiveCarrier = targetIsCarrier
            && string.Equals(policy.DodgeCoverage.Mode, "escape-lanes",
                StringComparison.Ordinal)
            && policy.DodgeCoverage.HorizonTicks > 0;
        if (directDamageNeeded
            && ArenaBasics.CanFireAtPosition(
                contract, body, target.Position))
        {
            return target.Position;
        }
        if (predictiveCarrier)
        {
            LastSeenEnemy? memory = _lastSeenEnemies.GetValueOrDefault(
                target.ActorId.UnitId);
            Position? previous = memory?.ActorId == target.ActorId
                && memory.PreviousConfirmedTick == memory.LastConfirmedTick - 1
                    ? memory.PreviousPosition
                    : null;
            Position[] oneStep = TacticalCoordinationPrimitives
                .OrderCarrierAimOptions(
                    target.Position,
                    previous,
                    options,
                    position => ArenaBasics.StaticDistance(
                        contract.Map, position, _enemyReactor));
            Position[] course = ProjectCarrierCourse(
                contract,
                target,
                previous,
                policy.DodgeCoverage.HorizonTicks);
            Position[] predicted = course
                .Select((position, index) => new
                {
                    Position = position,
                    Step = index + 1,
                    ContactStep = CarrierContactStep(
                        contract, body, position),
                    NewlyCovered = options.Count(option =>
                        !alreadyCovered.Contains(option)
                        && SameShotLane(body.Position, position, option)),
                })
                .Where(candidate => candidate.ContactStep is not null
                    && ArenaBasics.CanAimAtPosition(
                        contract, body, candidate.Position))
                .OrderBy(candidate => Math.Abs(
                    candidate.ContactStep!.Value - candidate.Step))
                .ThenByDescending(candidate => candidate.NewlyCovered)
                .ThenBy(candidate => candidate.Step)
                .ThenBy(candidate => candidate.Position.Y)
                .ThenBy(candidate => candidate.Position.X)
                .Select(candidate => candidate.Position)
                .Concat(oneStep)
                .Distinct()
                .ToArray();
            Position? uncovered = predicted.Where(position =>
                    ArenaBasics.CanAimAtPosition(contract, body, position)
                    && options.Any(option => !alreadyCovered.Contains(option)
                        && SameShotLane(body.Position, position, option)))
                .Select(position => (Position?)position)
                .FirstOrDefault();
            if (uncovered is Position selected)
                return selected;
            Position? fallback = predicted.Where(position =>
                    ArenaBasics.CanAimAtPosition(contract, body, position))
                .Select(position => (Position?)position)
                .FirstOrDefault();
            if (fallback is Position available)
                return available;
        }
        if (directDamageNeeded
            || string.Equals(policy.DodgeCoverage.Mode, "current-position",
                StringComparison.Ordinal)
            || alreadyCovered.Count
                >= policy.DodgeCoverage.MinimumCoveredOptions)
        {
            return ArenaBasics.CanAimAtPosition(
                    contract, body, target.Position)
                ? target.Position
                : options.First(position => ArenaBasics.CanAimAtPosition(
                    contract, body, position));
        }
        Position[] candidates = options
            .Where(position => ArenaBasics.CanAimAtPosition(
                contract, body, position))
            .OrderByDescending(position => options.Count(option =>
                !alreadyCovered.Contains(option)
                && SameShotLane(body.Position, position, option)))
            .ThenByDescending(position => options.Count(option =>
                SameShotLane(body.Position, position, option)))
            .ThenBy(position => position == target.Position ? 0 : 1)
            .ThenBy(position => position.Y)
            .ThenBy(position => position.X)
            .ToArray();
        int index = TacticalCoordinationPrimitives.CoverageFallbackIndex(
            policy.DodgeCoverage.Fallback,
            candidates.Select(position => options.Count(option =>
                    !alreadyCovered.Contains(option)
                    && SameShotLane(body.Position, position, option)))
                .ToArray());
        if (index >= 0)
            return candidates[index];
        return target.Position;
    }

    private Position[] ProjectCarrierCourse(
        GenericActorResolvedMatchContract contract,
        GenericActorContext.ObservedEnemyState target,
        Position? previous,
        int horizonTicks)
    {
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(value => value.Id == target.FormId);
        GenericActorRulesContract.MovementProfile? movement =
            form?.MovementProfileId is string movementId
                ? contract.Rules.MovementProfiles.FirstOrDefault(value =>
                    value.Id == movementId)
                : null;
        ProjectileHeading[] headings = movement?.FacingCoupling
                == GenericActorRulesContract.MovementFacingCoupling.FacingLocked
            ? [(ProjectileHeading)((int)target.Facing * 2)]
            : Enum.GetValues<ProjectileHeading>();
        var result = new List<Position>();
        Position cursor = target.Position;
        (int X, int Y)? continuation = previous is Position prior
            ? (Math.Sign(cursor.X - prior.X), Math.Sign(cursor.Y - prior.Y))
            : null;
        for (int step = 0; step < horizonTicks; step++)
        {
            Position[] legal = headings.Select(heading =>
                {
                    (int dx, int dy) = heading.Vector();
                    return cursor.Offset(dx, dy);
                })
                .Where(position => ArenaBasics.IsLegalTerrainStep(
                    contract.Map, cursor, position))
                .Distinct()
                .ToArray();
            if (legal.Length == 0)
            {
                result.Add(cursor);
                continue;
            }

            int currentDistance = ArenaBasics.StaticDistance(
                contract.Map, cursor, _enemyReactor) ?? int.MaxValue;
            Position? continuing = continuation is { } delta
                ? legal.Where(position =>
                        position.X - cursor.X == delta.X
                        && position.Y - cursor.Y == delta.Y
                        && (ArenaBasics.StaticDistance(
                                contract.Map, position, _enemyReactor)
                            ?? int.MaxValue) <= currentDistance)
                    .Select(position => (Position?)position)
                    .FirstOrDefault()
                : null;
            Position selected = continuing ?? legal
                .OrderBy(position => ArenaBasics.StaticDistance(
                        contract.Map, position, _enemyReactor)
                    ?? int.MaxValue)
                .ThenBy(position => position.Y)
                .ThenBy(position => position.X)
                .First();
            result.Add(selected);
            continuation = (
                selected.X - cursor.X,
                selected.Y - cursor.Y);
            cursor = selected;
        }
        return result.ToArray();
    }

    private static int? CarrierContactStep(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Position target)
    {
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(value => value.Id == body.FormId);
        GenericActorRulesContract.AttackProfile? attack =
            form?.AttackProfileId is string attackId
                ? contract.Rules.AttackProfiles.FirstOrDefault(value =>
                    value.Id == attackId)
                : null;
        int distance = body.Position.ChebyshevDistance(target);
        if (attack is null || distance < 1
            || distance > attack.Projectile.MaxTravelTiles)
        {
            return null;
        }
        return TacticalCoordinationPrimitives
            .CarrierMovementStepsBeforeProjectileContact(
                distance,
                attack.Projectile.Mode
                    == GenericActorRulesContract.ProjectileMode.InstantRay,
                attack.Projectile.LaunchTiles,
                attack.Projectile.TilesPerAdvance,
                attack.Projectile.TicksPerAdvance,
                attack.Projectile.AdvancesOnLaunchTick);
    }

    private static Position[] EscapeOptions(
        GenericActorResolvedMatchContract contract,
        GenericActorContext.ObservedEnemyState target,
        TacticalPlaybookPackage.Engagement policy)
    {
        if (string.Equals(policy.DodgeCoverage.Mode, "current-position",
                StringComparison.Ordinal)
            || policy.DodgeCoverage.HorizonTicks == 0)
        {
            return [target.Position];
        }
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(value => value.Id == target.FormId);
        GenericActorRulesContract.MovementProfile? movement =
            form?.MovementProfileId is string movementId
                ? contract.Rules.MovementProfiles.FirstOrDefault(value =>
                    value.Id == movementId)
                : null;
        IEnumerable<ProjectileHeading> headings = movement?.FacingCoupling
                == GenericActorRulesContract.MovementFacingCoupling.FacingLocked
            ? [(ProjectileHeading)((int)target.Facing * 2)]
            : Enum.GetValues<ProjectileHeading>();
        return [target.Position, .. headings
            .Select(heading =>
            {
                (int dx, int dy) = heading.Vector();
                return target.Position.Offset(dx, dy);
            })
            .Where(position => ArenaBasics.IsLegalTerrainStep(
                contract.Map, target.Position, position))
            .Distinct()
            .OrderBy(position => position.Y)
            .ThenBy(position => position.X)];
    }

    private static bool SameShotLane(
        Position origin,
        Position first,
        Position second)
    {
        (int X, int Y)? left = ShotRay(origin, first);
        (int X, int Y)? right = ShotRay(origin, second);
        return left.HasValue && right.HasValue && left.Value == right.Value;
    }

    private static (int X, int Y)? ShotRay(Position origin, Position target)
    {
        int dx = target.X - origin.X;
        int dy = target.Y - origin.Y;
        if (dx == 0 && dy == 0)
            return null;
        if (dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy))
            return null;
        return (Math.Sign(dx), Math.Sign(dy));
    }

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
        TacticalPlaybookPackage.Engagement policy) =>
        TacticalCoordinationPrimitives.IsWithinEngagementLeash(
            assignment,
            enemy.Position,
            body.Position,
            policy.ChaseLeash,
            policy.SelfDefense.Enabled,
            policy.SelfDefense.ThreatDistance);

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
            bool acted = TacticalCustodyPrimitives.DeliveryTimedOut(
                    progress.StagnantTicks,
                    custody.DeliveryTimeoutTicks)
                ? ActUnreachableCustodyFallback(
                    contract, mind, body, custody, claims)
                : ActCarrier(contract, mind, body, custody, claims);
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
                    contract, mind, body, custody, claims)
                : ActCarrier(contract, mind, body, custody, claims);
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
                : ActCarrier(contract, mind, body, custody, claims);
        }

        GenericActorActionLegality? handoff = body.Action("handoff-core");
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = handoff?.Constraints.OfType<
                GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint>().SingleOrDefault();
        MindBody? runner = mind.Bodies
            .Where(candidate => !carried.ContainsKey(candidate.ActorId)
                && custody.AuthorizedCarrierRoles.Contains(
                    _stableRoles.GetValueOrDefault(candidate.UnitId),
                    StringComparer.Ordinal))
            .OrderBy(candidate => candidate.Position.ChebyshevDistance(
                body.Position))
            .ThenBy(candidate => candidate.UnitId)
            .FirstOrDefault();
        GenericActorActionArgument.UnitTarget? target = runner is null
            ? null
            : new GenericActorActionArgument.UnitTarget(
                runner.ActorId.TeamId, runner.UnitId);
        if (handoff is { Available: true }
            && target is not null
            && targets?.AllowedValues.Contains(target.Value) == true)
        {
            body.Command(handoff.ActionId, handoff.ActionCode,
                [new GenericActorActionArgument.UnitTargetArgument(target.Value)],
                "custody:accidental-pickup-transfer");
            return true;
        }
        if (runner is not null
            && TacticalCustodyPrimitives.TransferWindowOpen(
                mind.Tick - progress.StartedTick,
                custody.TransferTimeoutTicks))
        {
            return Hold(body, "custody:await-authorized-transfer");
        }
        // Expiry deliberately becomes delivery, not a voluntary drop/re-pickup
        // loop. A temporarily unavailable runner must not strand the Core.
        return ActCarrier(contract, mind, body, custody, claims)
            || Hold(body, "custody:committed-delivery-wait");
    }

    private bool ActUnreachableCustodyFallback(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        TacticalPlaybookPackage.CustodyPolicy custody,
        ArenaBasics.Claims claims) => custody.UnreachableFallback switch
    {
        "hold" => Hold(body, "custody:delivery-timeout-hold"),
        "guard" => ArenaBasics.TryEvade(contract, mind, body, claims)
            || Hold(body, "custody:delivery-timeout-guard"),
        "alternate-core" => IsSafeToDrop(mind, body) && TryDropCore(body)
            || Hold(body, "custody:delivery-timeout-alternate-core"),
        "regroup" => ActCarrier(contract, mind, body, custody, claims),
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

    private bool ActCarrier(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
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
        Position? step = ArenaBasics.StaticFirstStepAvoidingReservations(
            contract, mind, body, _ownReactor);
        if (step is Position committed
            && ArenaBasics.TryMoveDirect(
                contract, mind, body, committed, claims,
                "custody:committed-delivery"))
            return true;
        if (TryAdvanceSignature(contract, body, _ownReactor))
            return true;
        if (ArenaBasics.TryMoveHomeward(
                contract, mind, body, _ownReactor, claims,
                "custody:delivery"))
            return true;
        return false;
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
                    contract, body, "prism-wall", threat, reason)
                || ArenaBasics.TryPositionSignature(
                    contract, body, "sentinel-seed", threat, reason)
                || ArenaBasics.TryPositionSignature(
                    contract, body, "trip-node", threat, reason)))
            return true;
        LastSeenEnemy? stalest = _lastSeenEnemies.Values
            .Where(seen => seen.LastConfirmedTick < mind.Tick - 4
                && body.Position.ChebyshevDistance(seen.Position) <= 8)
            .OrderBy(seen => seen.LastConfirmedTick)
            .ThenBy(seen => seen.ActorId.UnitId)
            .FirstOrDefault();
        return stalest is not null
            && ArenaBasics.TryPositionSignature(
                contract, body, "survey-flare", stalest.Position, reason);
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
        Position target,
        IReadOnlyDictionary<int, Position> targets,
        IReadOnlyDictionary<int, string> groups,
        IReadOnlyDictionary<int, TacticalPlaybookPackage.Order> orders,
        IReadOnlyDictionary<int,
            GenericActorContext.ArcRelayCoreState> pickupAssignments,
        ArenaBasics.Claims claims)
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

        MotionProgress motion = _motion.GetValueOrDefault(body.UnitId)
            ?? new MotionProgress(body.ActorId, order.OrderId, body.Position, 0);
        int stuck = motion.ActorId == body.ActorId
            && motion.OrderId == order.OrderId
            && motion.Position == body.Position
                ? motion.StuckTicks + 1
                : 0;
        _motion[body.UnitId] = new MotionProgress(
            body.ActorId, order.OrderId, body.Position, stuck);
        if (stuck >= order.Movement.StuckTicks)
        {
            switch (order.Movement.StuckRecovery)
            {
                case "yield":
                    _motion[body.UnitId] = new MotionProgress(
                        body.ActorId, order.OrderId, body.Position, 0);
                    return Hold(body, Provenance(
                        machine, group, order, "stuck-yield"));
                case "hold":
                    return Hold(body, Provenance(
                        machine, group, order, "stuck-hold"));
                case "regroup":
                    QueueFallbackPhase(order);
                    return Hold(body, Provenance(
                        machine, group, order, "stuck-fallback"));
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
            return Hold(body, Provenance(
                machine, group, order, "formation-pace"));
        }
        bool targetBlocked = !TacticalFormationPrimitives.IsEnterable(
            contract.Map.Width,
            contract.Map.Height,
            contract.Map.TileRows,
            target);
        Position[] goals = TacticalFormationPrimitives.ReflowGoals(
            contract.Map.Width,
            contract.Map.Height,
            contract.Map.TileRows,
            target,
            stuck >= order.Movement.StuckTicks || targetBlocked
                ? formation.Reflow.SearchRadius
                : 0,
            formation.Reflow.BlockedSlot);
        if (TryAdvanceSignature(contract, body, target))
            return true;
        if (ArenaBasics.TryMoveToward(
            contract, mind, body, goals, claims,
            Provenance(machine, group, order,
                stuck >= order.Movement.StuckTicks
                    ? $"{order.Movement.StuckRecovery}-reflow"
                    : "formation-move")))
        {
            return true;
        }
        return order.Fallback.OnNoPath switch
        {
            "reflow" => false,
            "hold" => Hold(body, Provenance(
                machine, group, order, "no-path-hold")),
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
            IReadOnlyCollection<GenericActorContext.ArcRelayCoreState> loose)
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
            "heading" => ArenaBasics.TryHeadingSignature(
                contract, body, signature.Kind, target.Position, reason),
            "position" => ArenaBasics.TryPositionSignature(
                contract, body, signature.Kind,
                string.Equals(
                    signature.Kind, "hardlight-block", StringComparison.Ordinal)
                    ? assignment
                    : target.Position,
                reason),
            "unit" => ArenaBasics.TryUnitSignature(
                contract, body, signature.Kind, target.ActorId, reason),
            "direction" => ArenaBasics.TryDirectionSignature(
                contract, body, signature.Kind, target.Position, reason),
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
                $"focus {focus.Target.ActorId}")
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
        Direction desired = Math.Abs(dx) >= Math.Abs(dy)
            ? dx >= 0 ? Direction.East : Direction.West
            : dy >= 0 ? Direction.South : Direction.North;
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
            || body.Facing == desired
            || !directions.AllowedValues.Contains(desired))
            return false;
        body.Command(action.ActionId, action.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(desired)], reason);
        return true;
    }

    private static bool Hold(MindBody body, string reason)
    {
        body.Hold(reason);
        return true;
    }

    private static string Provenance(
        TacticalPlaybookMachine machine,
        string group,
        TacticalPlaybookPackage.Order order,
        string channel) =>
        $"tp:{machine.PhaseId}:{group}:{order.OrderId}:{channel}";

    private static string RoleTag(
        string phase,
        string group,
        string role,
        string order)
    {
        string value = $"{phase}-{group}-{role}-{order}".ToLowerInvariant();
        return (value.Length <= 24 ? value : value[..24]).TrimEnd('-');
    }

    private static string CoreKey(GenericActorContext.ArcRelayCoreId id) =>
        $"{id.SourceWellId}:{id.SourceOrdinal}";

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

    private sealed record MotionProgress(
        ActorIdentity ActorId,
        string OrderId,
        Position Position,
        int StuckTicks);

    private sealed record FocusLock(
        ActorIdentity ActorId,
        int LockedTick,
        int LastVisibleTick,
        int UnreachableTicks);

    private sealed record FocusAssignment(
        GenericActorContext.ObservedEnemyState Target,
        Position AimPosition,
        bool UseSignature,
        bool SelfDefenseExcursion);

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
        int StagnantTicks);

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
        IReadOnlyDictionary<string, int> WellOutstanding,
        IReadOnlyDictionary<string, int> FormationStableTicks,
        IReadOnlyDictionary<string, int> FormationBroken);
}
