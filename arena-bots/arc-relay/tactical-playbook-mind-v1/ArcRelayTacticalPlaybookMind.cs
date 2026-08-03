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
    private readonly Dictionary<int, int> _firstSeenEnemyLife = [];
    private readonly Dictionary<string, SecuredCore> _securedCores =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _processedEvents =
        new(StringComparer.Ordinal);
    private readonly Dictionary<int, RouteProgress> _routes = [];
    private readonly Dictionary<int, MotionProgress> _motion = [];
    private readonly Dictionary<string, FocusLock> _focusLocks =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _formationStableTicks =
        new(StringComparer.Ordinal);
    private GenericActorResolvedMatchContract? _contract;
    private TacticalPlaybookPackage? _package;
    private TacticalPlaybookMachine? _machine;
    private Position _ownReactor;
    private Position _enemyReactor;
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
        _machine = new TacticalPlaybookMachine(_package.Source);
    }

    public void Think(MindContext mind)
    {
        GenericActorResolvedMatchContract contract = _contract
            ?? throw new InvalidOperationException("StartMatch was not called.");
        TacticalPlaybookPackage package = _package
            ?? throw new InvalidOperationException("Playbook was not loaded.");
        TacticalPlaybookMachine machine = _machine
            ?? throw new InvalidOperationException("Machine was not loaded.");
        if (mind.Mode is not GenericActorContext.ModeObservationState.ArcRelay arc)
        {
            foreach (MindBody body in mind.Bodies)
                body.Hold("unsupported mode");
            return;
        }

        UpdateMemory(mind, arc, package.Source.Memory);
        Dictionary<int, string> roles = AllocateRoles(mind, package.Source);
        Dictionary<int, string> groups = GroupMembership(roles, package.Source);
        TacticalSnapshot snapshot = Snapshot(
            mind, arc, package, machine, roles, groups,
            updateFormationState: false);
        foreach (TacticalPlaybookPackage.Group group in package.Source.Groups)
        {
            machine.AdvanceLocal(group, mind.Tick,
                condition => Evaluate(condition, snapshot, package));
        }
        machine.AdvanceGlobal(mind.Tick,
            condition => Evaluate(condition, snapshot, package));
        snapshot = Snapshot(mind, arc, package, machine, roles, groups,
            updateFormationState: true);

        Dictionary<int, TacticalPlaybookPackage.Order> orders = ActiveOrders(
            mind, package.Source, machine, groups);
        Dictionary<int, Position> targets = mind.Bodies.ToDictionary(
            body => body.UnitId,
            body => Target(
                contract, mind, package, machine, roles, groups,
                orders[body.UnitId], body));
        Dictionary<int, MindBody> repairs = AllocateRepairs(
            contract, mind, package.Source, roles, orders,
            new HashSet<int>());
        Dictionary<int, GenericActorContext.ObservedEnemyState> focus =
            AllocateFocus(contract, mind, arc, package.Source, roles, orders,
                targets, repairs.Keys.ToHashSet());
        Dictionary<ActorIdentity, GenericActorContext.ArcRelayCoreState> carried =
            arc.VisibleCores
                .Where(core => core.Disposition
                    == GenericActorContext.ArcRelayCoreDisposition.Carried
                    && core.CarrierActorId is not null)
                .ToDictionary(core => core.CarrierActorId!, core => core);
        GenericActorContext.ArcRelayCoreState[] loose = arc.VisibleCores
            .Where(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Loose)
            .OrderBy(core => core.CoreId.SourceWellId, StringComparer.Ordinal)
            .ThenBy(core => core.CoreId.SourceOrdinal)
            .ToArray();
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
            Position target = targets[body.UnitId];
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
                        carried, claims),
                    "self-preservation" => TrySelfPreservation(
                        contract, mind, package.Source, body, order, claims),
                    "repair" => repairs.TryGetValue(
                            body.UnitId, out MindBody? repairTarget)
                        && ArenaBasics.TryUnitSignature(
                            contract, body, "repair-beam", repairTarget.ActorId,
                            Provenance(machine, group, order, "repair")),
                    "signature" => focus.TryGetValue(
                            body.UnitId, out GenericActorContext.ObservedEnemyState?
                                signatureTarget)
                        && TryCombatSignature(
                            contract, mind, body, signatureTarget, target,
                            package.Source.Engagements.Single(value =>
                                string.Equals(value.EngagementId,
                                    order.EngagementId,
                                    StringComparison.Ordinal)),
                            Provenance(machine, group, order, "signature")),
                    "focus-fire" => focus.TryGetValue(
                            body.UnitId, out GenericActorContext.ObservedEnemyState?
                                shotTarget)
                        && WithinEngagementLeash(body, target, shotTarget,
                            package.Source.Engagements.Single(value =>
                                string.Equals(value.EngagementId,
                                    order.EngagementId,
                                    StringComparison.Ordinal)))
                        && ArenaBasics.TryShoot(
                            contract, mind, body, shotTarget,
                            preferredOnly: true),
                    "movement" => TryMovement(
                        contract, mind, arc, package, machine, snapshot, body,
                        role, group, order, target, loose, claims),
                    "facing" => TryFaceTarget(
                        contract, body, target,
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

    private void UpdateMemory(
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        TacticalPlaybookPackage.MemoryPolicy memory)
    {
        foreach (GenericActorContext.ObservedEnemyState enemy in mind.Enemies)
        {
            _enemyUnavailableUntil.Remove(enemy.ActorId.UnitId);
            _lastSeenEnemies[enemy.ActorId.UnitId] = new LastSeenEnemy(
                enemy.ActorId, enemy.Position, mind.Tick);
            _firstSeenEnemyLife.TryAdd(enemy.ActorId.GetHashCode(), mind.Tick);
        }
        foreach (GenericActorContext.ObservedEvent observed in mind.VisibleEvents)
        {
            if (!_processedEvents.Add(observed.EventHandle))
                continue;
            if (observed.Payload is GenericActorContext.EventPayload.Destruction death
                && death.ActorId.TeamId != _teamId)
            {
                _enemyUnavailableUntil[death.ActorId.UnitId] =
                    observed.SourceTick + memory.EnemyUnavailableTicks;
                _lastSeenEnemies.Remove(death.ActorId.UnitId);
            }
            if (observed.Payload is not GenericActorContext.EventPayload.ArcRelay mode)
                continue;
            switch (mode.Fact)
            {
                case GenericActorContext.ArcRelayEvent.CoreDropped drop
                    when drop.SourceActorId.TeamId != _teamId:
                    _securedCores[CoreKey(drop.CoreId)] = new SecuredCore(
                        drop.Position, observed.SourceTick);
                    break;
                case GenericActorContext.ArcRelayEvent.CoreBanked banked:
                    _securedCores.Remove(CoreKey(banked.CoreId));
                    if (banked.TeamId == _teamId)
                        _lastObjectiveProgressTick = observed.SourceTick;
                    break;
                case GenericActorContext.ArcRelayEvent.CorePickedUp pickup
                    when pickup.CarrierActorId.TeamId != _teamId:
                    _securedCores.Remove(CoreKey(pickup.CoreId));
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

    private Dictionary<int, string> AllocateRoles(
        MindContext mind,
        TacticalPlaybookPackage.Playbook playbook)
    {
        var result = new Dictionary<int, string>();
        var available = mind.Bodies.ToDictionary(body => body.UnitId);
        foreach (TacticalPlaybookPackage.Role role in playbook.Roles)
        {
            MindBody[] retained = available.Values
                .Where(body => _stableRoles.GetValueOrDefault(body.UnitId)
                        == role.RoleId
                    && role.CandidateClasses.Contains(
                        body.ClassId, StringComparer.Ordinal))
                .OrderBy(body => body.UnitId)
                .Take(role.Maximum)
                .ToArray();
            foreach (MindBody body in retained)
            {
                result[body.UnitId] = role.RoleId;
                available.Remove(body.UnitId);
            }
            int needed = Math.Max(0, role.Preferred - retained.Length);
            MindBody[] promoted = available.Values
                .Where(body => role.CandidateClasses.Contains(
                    body.ClassId, StringComparer.Ordinal))
                .OrderBy(body => Array.IndexOf(
                    role.CandidateClasses, body.ClassId))
                .ThenByDescending(body => body.Health)
                .ThenBy(body => body.UnitId)
                .Take(needed)
                .ToArray();
            foreach (MindBody body in promoted)
            {
                result[body.UnitId] = role.RoleId;
                available.Remove(body.UnitId);
            }
        }
        TacticalPlaybookPackage.Role fallback = playbook.Roles.Last();
        foreach (MindBody body in available.Values.OrderBy(body => body.UnitId))
            result[body.UnitId] = fallback.RoleId;
        foreach ((int unitId, string role) in result)
            _stableRoles[unitId] = role;
        return result;
    }

    private static Dictionary<int, string> GroupMembership(
        IReadOnlyDictionary<int, string> roles,
        TacticalPlaybookPackage.Playbook playbook) => roles.ToDictionary(
            value => value.Key,
            value => playbook.Groups.First(group =>
                group.RoleIds.Contains(value.Value, StringComparer.Ordinal))
                .GroupId);

    private TacticalSnapshot Snapshot(
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        TacticalPlaybookPackage package,
        TacticalPlaybookMachine machine,
        IReadOnlyDictionary<int, string> roles,
        IReadOnlyDictionary<int, string> groups,
        bool updateFormationState)
    {
        var groupLive = package.Source.Groups.ToDictionary(
            group => group.GroupId,
            group => groups.Count(value => value.Value == group.GroupId),
            StringComparer.Ordinal);
        var groupCohesion = package.Source.Groups.ToDictionary(
            group => group.GroupId,
            group => CohesionPercent(
                mind.Bodies.Where(body => groups[body.UnitId] == group.GroupId)
                    .Select(body => body.Position).ToArray(),
                ActiveFormation(package.Source, machine, group.GroupId)
                    .Spacing.Maximum),
            StringComparer.Ordinal);
        if (updateFormationState)
        {
            foreach (TacticalPlaybookPackage.Group group in package.Source.Groups)
            {
                TacticalPlaybookPackage.Formation formation = ActiveFormation(
                    package.Source, machine, group.GroupId);
                int prior = _formationStableTicks.GetValueOrDefault(group.GroupId);
                _formationStableTicks[group.GroupId] =
                    groupCohesion[group.GroupId]
                        >= formation.Cohesion.ArrivalRatioPercent
                        ? prior + 1
                        : 0;
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
            mind.Bodies.Length,
            _enemyUnavailableUntil.Count,
            _securedCores.Count,
            arc.VisibleCores.Count(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Loose),
            carriers,
            enemyCarriers,
            Math.Max(0, mind.Tick - _lastObjectiveProgressTick),
            own.IntegritySegments,
            own.ChargePips,
            roles.Values.GroupBy(value => value, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(),
                    StringComparer.Ordinal),
            groupLive,
            groupCohesion,
            friendlyZones,
            groupZones,
            visibleEnemiesByZone,
            rememberedEnemiesByZone,
            arc.Wells.ToDictionary(
                well => well.WellId,
                well => well.OutstandingCoreId is null ? 0 : 1,
                StringComparer.Ordinal),
            _formationStableTicks.ToDictionary(StringComparer.Ordinal));
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
        TacticalPlaybookPackage.Playbook playbook,
        TacticalPlaybookMachine machine,
        string groupId)
    {
        string local = machine.LocalState(groupId);
        TacticalPlaybookPackage.Order order = machine.Phase.OrderIds
            .Select(id => playbook.Orders.Single(value => value.OrderId == id))
            .Where(value => value.GroupId == groupId)
            .OrderBy(value => value.LocalState == local ? 0 : 1)
            .ThenBy(value => value.Priority)
            .First();
        return playbook.Formations.Single(value =>
            value.FormationId == order.FormationId);
    }

    private static bool Evaluate(
        TacticalPlaybookPackage.Condition condition,
        TacticalSnapshot snapshot,
        TacticalPlaybookPackage package)
    {
        int actual = condition.Fact switch
        {
            "always" => 1,
            "tick" => snapshot.Tick,
            "live-friendlies" => snapshot.LiveFriendlies,
            "friendlies-in-zone-count" => snapshot.FriendlyZones
                .GetValueOrDefault(condition.Zone),
            "group-live-count" => snapshot.GroupLive
                .GetValueOrDefault(condition.Subject),
            "group-in-zone-count" => snapshot.GroupZones
                .GetValueOrDefault(condition.Subject)?
                .GetValueOrDefault(condition.Zone) ?? 0,
            "group-cohesion" => snapshot.GroupCohesion
                .GetValueOrDefault(condition.Subject),
            "group-stuck-ticks" => 0,
            "known-enemies-unavailable" => snapshot.KnownEnemiesUnavailable,
            "visible-enemies-in-zone" => snapshot.VisibleEnemiesByZone
                .GetValueOrDefault(condition.Zone),
            "remembered-enemies-in-zone" => snapshot.RememberedEnemiesByZone
                .GetValueOrDefault(condition.Zone),
            "visible-enemy-carriers" => snapshot.VisibleEnemyCarriers,
            "friendly-carriers" => snapshot.FriendlyCarriers,
            "secured-cores" => snapshot.SecuredCores,
            "visible-loose-cores" => snapshot.VisibleLooseCores,
            "well-has-outstanding" => snapshot.WellOutstanding
                .GetValueOrDefault(condition.Subject),
            "ticks-without-objective-progress" =>
                snapshot.TicksWithoutObjectiveProgress,
            "reactor-integrity" => snapshot.ReactorIntegrity,
            "reactor-charge" => snapshot.ReactorCharge,
            "formation-established-ticks" => snapshot.FormationStableTicks
                .GetValueOrDefault(condition.Subject),
            "custody-state-ticks" => snapshot.TicksWithoutObjectiveProgress,
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

    private static Dictionary<int, TacticalPlaybookPackage.Order> ActiveOrders(
        MindContext mind,
        TacticalPlaybookPackage.Playbook playbook,
        TacticalPlaybookMachine machine,
        IReadOnlyDictionary<int, string> groups)
    {
        TacticalPlaybookPackage.Order[] phaseOrders = machine.Phase.OrderIds
            .Select(id => playbook.Orders.Single(value => value.OrderId == id))
            .ToArray();
        return mind.Bodies.ToDictionary(
            body => body.UnitId,
            body => phaseOrders
                .Where(order => order.GroupId == groups[body.UnitId])
                .OrderBy(order => order.LocalState
                    == machine.LocalState(groups[body.UnitId]) ? 0 : 1)
                .ThenBy(order => order.Priority)
                .ThenBy(order => order.OrderId, StringComparer.Ordinal)
                .First());
    }

    private Position Target(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        TacticalPlaybookPackage package,
        TacticalPlaybookMachine machine,
        IReadOnlyDictionary<int, string> roles,
        IReadOnlyDictionary<int, string> groups,
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
            "carrier" => mind.Bodies
                .Where(candidate => candidate.UnitId != body.UnitId)
                .OrderBy(candidate => candidate.Position.ChebyshevDistance(
                    body.Position))
                .Select(candidate => candidate.Position)
                .FirstOrDefault(body.Position),
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
        TacticalPlaybookPackage.Formation formation =
            package.Source.Formations.Single(value =>
                value.FormationId == order.FormationId);
        string role = roles[body.UnitId];
        int ordinal = roles
            .Where(value => value.Value == role)
            .OrderBy(value => value.Key)
            .Select((value, index) => (value.Key, index))
            .Single(value => value.Key == body.UnitId).index;
        TacticalPlaybookPackage.Placement[] placements = formation.Placements
            .Where(value => value.RoleId == role)
            .OrderBy(value => value.Order).ToArray();
        TacticalPlaybookPackage.Placement placement = placements[
            Math.Min(ordinal, placements.Length - 1)];
        _ = contract;
        _ = machine;
        _ = groups;
        return package.FormationPosition(anchor, placement.Offset);
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
        int arrival = Math.Max(1, order.Movement.ArrivalRadius);
        while (index < route.Length - 1
            && body.Position.ChebyshevDistance(route[index]) <= arrival)
            index++;
        _routes[body.UnitId] = state with { Index = index };
        return route[index];
    }

    private Dictionary<int, GenericActorContext.ObservedEnemyState> AllocateFocus(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        TacticalPlaybookPackage.Playbook playbook,
        IReadOnlyDictionary<int, string> roles,
        IReadOnlyDictionary<int, TacticalPlaybookPackage.Order> orders,
        IReadOnlyDictionary<int, Position> targets,
        IReadOnlySet<int> unavailableParticipants)
    {
        var allocations = new Dictionary<int,
            GenericActorContext.ObservedEnemyState>();
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
                    .OrderBy(enemy => PriorityRank(
                        policy.TargetPriorities, enemy, carriers, mind.Tick))
                    .ThenBy(enemy => enemy.Health)
                    .ThenBy(enemy => enemy.Position.ChebyshevDistance(
                        _enemyReactor))
                    .ThenBy(enemy => enemy.ActorId)
                    .ToArray();
                GenericActorContext.ObservedEnemyState selected = enemies[0];
                if (_focusLocks.TryGetValue(scopeId, out FocusLock? prior)
                    && mind.Tick - prior.LockedTick <= policy.LockTicks)
                {
                    selected = enemies.FirstOrDefault(enemy =>
                        enemy.ActorId == prior.ActorId) ?? selected;
                }
                _focusLocks[scopeId] = new FocusLock(
                    selected.ActorId, mind.Tick);
                int committedDamage = 0;
                int attackers = 0;
                foreach (MindBody body in participants
                             .OrderBy(body => body.Position.ChebyshevDistance(
                                 selected.Position))
                             .ThenBy(body => body.UnitId))
                {
                    int damage = ExpectedDamage(contract, body);
                    if (attackers >= policy.MaximumAttackersPerTarget
                        || committedDamage
                            >= selected.Health + policy.OverkillDamage)
                        break;
                    if (!WithinEngagementLeash(
                            body, targets[body.UnitId], selected, policy))
                        continue;
                    allocations[body.UnitId] = selected;
                    committedDamage += damage;
                    attackers++;
                }
            }
        }
        return allocations;
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
                "enemy-carrier" => carriers.Contains(enemy.ActorId),
                "lowest-health" => true,
                "closest-to-anchor" => enemy.Position.ChebyshevDistance(
                    _enemyReactor) <= 5,
                "closest-to-reactor" => true,
                "highest-threat" => enemy.Position.ChebyshevDistance(
                    _ownReactor) <= 6,
                "fresh-respawn" => tick - _firstSeenEnemyLife
                    .GetValueOrDefault(enemy.ActorId.GetHashCode(), tick) <= 5,
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
        body.Position.ChebyshevDistance(assignment) <= policy.ChaseLeash
        || policy.SelfDefense.Enabled
        && body.Position.ChebyshevDistance(enemy.Position)
            <= policy.SelfDefense.ThreatDistance;

    private static Dictionary<int, MindBody> AllocateRepairs(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        TacticalPlaybookPackage.Playbook playbook,
        IReadOnlyDictionary<int, string> roles,
        IReadOnlyDictionary<int, TacticalPlaybookPackage.Order> orders,
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
            foreach (MindBody provider in providers)
            {
                MindBody? selected = mind.Bodies
                    .Where(body => body.UnitId != provider.UnitId
                        && body.Health < MaxHealth(contract, body)
                        && counts.GetValueOrDefault(body.UnitId)
                            < policy.MaximumProvidersPerTarget)
                    .OrderBy(body => SupportRank(
                        policy, body, roles, focusParticipants))
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
            }
        }
        return result;
    }

    private static int SupportRank(
        TacticalPlaybookPackage.SupportPolicy policy,
        MindBody body,
        IReadOnlyDictionary<int, string> roles,
        IReadOnlySet<int> focusParticipants)
    {
        for (int index = 0; index < policy.TargetPriorities.Length; index++)
        {
            bool matches = policy.TargetPriorities[index] switch
            {
                "carrier" => roles[body.UnitId] == "runner",
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
        ArenaBasics.Claims claims)
    {
        if (!carried.ContainsKey(body.ActorId))
            return false;
        TacticalPlaybookPackage.CustodyPolicy? custody =
            string.IsNullOrEmpty(order.CustodyId)
                ? package.Source.CustodyPolicies.FirstOrDefault()
                : package.Source.CustodyPolicies.Single(value =>
                    value.CustodyId == order.CustodyId);
        if (custody is null)
            return false;
        if (custody.AuthorizedCarrierRoles.Contains(role,
                StringComparer.Ordinal))
            return ActCarrier(contract, mind, body, claims);

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
            .FirstOrDefault(candidate => targets?.AllowedValues.Contains(
                new GenericActorActionArgument.UnitTarget(
                    candidate.ActorId.TeamId, candidate.UnitId)) == true);
        if (handoff is { Available: true } && runner is not null)
        {
            var target = new GenericActorActionArgument.UnitTarget(
                runner.ActorId.TeamId, runner.UnitId);
            body.Command(handoff.ActionId, handoff.ActionCode,
                [new GenericActorActionArgument.UnitTargetArgument(target)],
                "custody:accidental-pickup-transfer");
            return true;
        }
        // Do not create a drop/re-pickup cycle when a handoff is unavailable.
        return ActCarrier(contract, mind, body, claims);
    }

    private bool ActCarrier(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        ArenaBasics.Claims claims)
    {
        Position? step = ArenaBasics.StaticFirstStepAvoidingReservations(
            contract, mind, body, _ownReactor);
        if (step is Position committed
            && ArenaBasics.TryMoveDirect(
                contract, mind, body, committed, claims,
                "custody:committed-delivery"))
            return true;
        if (TryAdvanceSignature(contract, body, _ownReactor))
            return true;
        if (ArenaBasics.TryMoveToward(
                contract, mind, body, [_ownReactor], claims,
                "custody:delivery"))
            return true;
        return ArenaBasics.TryEvade(contract, mind, body, claims);
    }

    private static bool TrySelfPreservation(
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
        return body.Health * 100 <= MaxHealth(contract, body) * reserve
            && ArenaBasics.TryEvade(contract, mind, body, claims);
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
        IReadOnlyCollection<GenericActorContext.ArcRelayCoreState> loose,
        ArenaBasics.Claims claims)
    {
        if (!string.IsNullOrEmpty(order.CustodyId))
        {
            TacticalPlaybookPackage.CustodyPolicy policy =
                package.Source.CustodyPolicies.Single(value =>
                    value.CustodyId == order.CustodyId);
            bool safe = policy.SafeConversionAll.Any(conditionGroup =>
                TacticalPlaybookMachine.Matches(conditionGroup,
                    condition => Evaluate(condition, snapshot, package)));
            if (policy.AuthorizedCarrierRoles.Contains(role,
                    StringComparer.Ordinal)
                && safe
                && TryCollectCore(
                    contract, mind, arc, body, policy, loose, claims))
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
        TacticalPlaybookPackage.Formation formation =
            package.Source.Formations.Single(value =>
                value.FormationId == order.FormationId);
        Position[] goals = ReflowGoals(
            contract.Map, target,
            stuck >= order.Movement.StuckTicks
                ? formation.Reflow.SearchRadius
                : 0);
        if (TryAdvanceSignature(contract, body, target))
            return true;
        return ArenaBasics.TryMoveToward(
            contract, mind, body, goals, claims,
            Provenance(machine, group, order,
                stuck >= order.Movement.StuckTicks
                    ? $"{order.Movement.StuckRecovery}-reflow"
                    : "formation-move"));
    }

    private bool TryCollectCore(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        TacticalPlaybookPackage.CustodyPolicy policy,
        IReadOnlyCollection<GenericActorContext.ArcRelayCoreState> loose,
        ArenaBasics.Claims claims)
    {
        GenericActorContext.ArcRelayCoreState? core = loose
            .OrderBy(value => _securedCores.ContainsKey(CoreKey(value.CoreId))
                ? 0 : 1)
            .ThenBy(value => body.Position.ChebyshevDistance(value.Position))
            .ThenBy(value => value.CoreId.SourceWellId, StringComparer.Ordinal)
            .ThenBy(value => value.CoreId.SourceOrdinal)
            .FirstOrDefault();
        Position? target = core?.Position;
        if (target is null)
        {
            target = arc.Wells
                .Where(well => well.OutstandingCoreId is not null
                    && string.Equals(well.WellId, policy.SourceWell,
                        StringComparison.Ordinal))
                .Select(well => (Position?)well.Position)
                .FirstOrDefault();
        }
        if (target is not Position destination)
            return false;
        if (TryAdvanceSignature(contract, body, destination))
            return true;
        return ArenaBasics.TryMoveToward(
            contract, mind, body, [destination], claims,
            "custody:authorized-pickup");
    }

    private static Position[] ReflowGoals(
        GenericActorMapContract map,
        Position target,
        int radius)
    {
        var result = new List<Position> { target };
        for (int distance = 1; distance <= radius; distance++)
        {
            for (int dy = -distance; dy <= distance; dy++)
            for (int dx = -distance; dx <= distance; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != distance)
                    continue;
                Position candidate = target.Offset(dx, dy);
                if (candidate.X >= 0 && candidate.Y >= 0
                    && candidate.X < map.Width && candidate.Y < map.Height)
                    result.Add(candidate);
            }
        }
        return result.Distinct().ToArray();
    }

    private static bool TryCombatSignature(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        GenericActorContext.ObservedEnemyState target,
        Position assignment,
        TacticalPlaybookPackage.Engagement policy,
        string reason)
    {
        if (policy.SignatureCoordination == "none")
            return false;
        return ArenaBasics.TryUnitSignature(contract, body, "target-paint",
            target.ActorId, reason)
        || ArenaBasics.TryHeadingSignature(contract, body, "tractor-hook",
            target.Position, reason)
        || ArenaBasics.TryHeadingSignature(contract, body, "rail-line",
            target.Position, reason)
        || ArenaBasics.TryPositionSignature(contract, body, "falling-star",
            target.Position, reason)
        || (body.Position.ChebyshevDistance(target.Position) <= 1
            && ArenaBasics.TryParameterlessSignature(
                contract, body, "kinetic-burst", reason))
        || (body.Position.ChebyshevDistance(target.Position) <= 3
            && ArenaBasics.TryParameterlessSignature(
                contract, body, "null-field", reason))
        || ArenaBasics.TryDirectionSignature(contract, body, "prism-wall",
            target.Position, reason)
        || ArenaBasics.TryPositionSignature(
            contract, body, "hardlight-block", assignment, reason);
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
        int LastConfirmedTick);

    private sealed record SecuredCore(Position Position, int LastConfirmedTick);

    private sealed record RouteProgress(
        ActorIdentity ActorId,
        string OrderId,
        int Index);

    private sealed record MotionProgress(
        ActorIdentity ActorId,
        string OrderId,
        Position Position,
        int StuckTicks);

    private sealed record FocusLock(ActorIdentity ActorId, int LockedTick);

    private sealed record TacticalSnapshot(
        int Tick,
        int LiveFriendlies,
        int KnownEnemiesUnavailable,
        int SecuredCores,
        int VisibleLooseCores,
        int FriendlyCarriers,
        int VisibleEnemyCarriers,
        int TicksWithoutObjectiveProgress,
        int ReactorIntegrity,
        int ReactorCharge,
        IReadOnlyDictionary<string, int> RoleLive,
        IReadOnlyDictionary<string, int> GroupLive,
        IReadOnlyDictionary<string, int> GroupCohesion,
        IReadOnlyDictionary<string, int> FriendlyZones,
        IReadOnlyDictionary<string, Dictionary<string, int>> GroupZones,
        IReadOnlyDictionary<string, int> VisibleEnemiesByZone,
        IReadOnlyDictionary<string, int> RememberedEnemiesByZone,
        IReadOnlyDictionary<string, int> WellOutstanding,
        IReadOnlyDictionary<string, int> FormationStableTicks);
}
