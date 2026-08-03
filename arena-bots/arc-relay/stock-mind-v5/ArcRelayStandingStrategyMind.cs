using BotArena.Sdk;

/// <summary>
/// Evaluation-grade standing-strategy executor. The algorithm is tactic
/// agnostic: phase graphs, thresholds, lane, formations, class pools and
/// behavior assignments all arrive in the separately hashed v3 sheet.
/// </summary>
public sealed class ArcRelayStandingStrategyMind : IGenericMindBot
{
    private static readonly IReadOnlySet<Position> EmptyPositions =
        new HashSet<Position>();
    private readonly Dictionary<int, int> _enemyUnavailableUntil = [];
    private readonly Dictionary<int, LastSeenEnemyMemory> _lastSeenEnemies = [];
    private readonly Dictionary<string, SecuredCoreMemory> _securedCores =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, StableZoneMemory> _stableZones =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _processedEvents =
        new(StringComparer.Ordinal);
    private readonly Dictionary<int, RouteProgress> _routes = [];
    private GenericActorResolvedMatchContract? _contract;
    private StandingStrategySheet? _sheet;
    private StandingStrategyMachine? _machine;
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
                "StandingStrategyMind requires Arc Relay.");
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
            .Where(value => value.RegionId.Contains("reactor",
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(value => value.RegionId, StringComparer.Ordinal)
            .Select(value => value.Tiles[0])
            .First();
        bool mirror = _ownReactor.X > (start.Contract.Map.Width - 1) / 2;
        _sheet = StandingStrategySheet.Load(
            start.EvaluationData, start.Contract, mirror);
        _machine = new StandingStrategyMachine(_sheet.Strategy);
    }

    public void Think(MindContext mind)
    {
        GenericActorResolvedMatchContract contract = _contract
            ?? throw new InvalidOperationException("StartMatch was not called.");
        StandingStrategySheet sheet = _sheet
            ?? throw new InvalidOperationException("Sheet was not loaded.");
        StandingStrategyMachine machine = _machine
            ?? throw new InvalidOperationException("Machine was not loaded.");
        if (mind.Mode is not GenericActorContext.ModeObservationState.ArcRelay arc)
        {
            foreach (MindBody body in mind.Bodies)
                body.Hold("unsupported mode");
            return;
        }

        UpdateMemory(mind, arc, sheet.Strategy.Memory);
        StandingSnapshot snapshot = Snapshot(mind, arc, sheet, machine);
        machine.Advance(snapshot, (condition, value) =>
            Evaluate(condition, value, sheet));
        snapshot = Snapshot(mind, arc, sheet, machine);
        Dictionary<int, StandingUnitAssignment> assignments = Assign(
            mind, arc, _teamId, snapshot, machine.Phase, sheet);
        GenericActorContext.ObservedEnemyState? enemyCarrier =
            ArenaBasics.VisibleEnemyCarrier(mind, _teamId);
        GenericActorContext.ObservedEnemyState? focus = FocusTarget(
            mind, sheet.Strategy.FocusPolicy, enemyCarrier);
        var carried = arc.VisibleCores
            .Where(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Carried
                && core.CarrierActorId is not null)
            .ToDictionary(core => core.CarrierActorId!, core => core);
        HashSet<Position> looseCoreTiles = arc.VisibleCores
            .Where(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Loose)
            .Select(core => core.Position).ToHashSet();
        Dictionary<int, MindBody> repairs = AllocateRepairs(
            contract, mind, assignments, carried.Keys.ToHashSet());
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
                     .OrderByDescending(candidate =>
                         carried.ContainsKey(candidate.ActorId))
                     .ThenBy(candidate => assignments[candidate.UnitId]
                         .Plan.Priority)
                     .ThenBy(candidate => candidate.UnitId))
        {
            StandingUnitAssignment assignment = assignments[body.UnitId];
            StandingAssignmentPlan task = assignment.Plan;
            body.SetRole(Role(machine.PhaseId, task.Id));
            bool carrying = carried.ContainsKey(body.ActorId);

            if (task.CorePolicy is "avoid" or "drop" or "guard")
            {
                foreach (GenericActorContext.ArcRelayCoreState loose in
                         arc.VisibleCores.Where(core => core.Disposition
                             == GenericActorContext.ArcRelayCoreDisposition.Loose))
                {
                    claims.Reserve(loose.Position);
                }
            }

            if (carrying)
            {
                if (string.Equals(task.CorePolicy, "deliver",
                        StringComparison.Ordinal))
                {
                    ActCarrier(contract, mind, body, sheet, task, claims);
                }
                else if (task.CorePolicy is "drop" or "avoid" or "guard")
                {
                    if (!TryDropCore(body, "release unintended secured Core"))
                    {
                        ActTransferCarrier(
                            contract, mind, body, assignments, carried, task,
                            claims);
                    }
                }
                else
                {
                    ActTransferCarrier(
                        contract, mind, body, assignments, carried, task,
                        claims);
                }
                continue;
            }
            if (!carrying
                && carrierClearance.Contains(body.Position)
                && ArenaBasics.TryMoveAside(
                    contract, mind, body, claims, carrierClearance,
                    "clearing standing-strategy return lane"))
            {
                continue;
            }
            if ((string.Equals(task.Behavior, "score", StringComparison.Ordinal)
                    || string.Equals(task.CorePolicy, "collect",
                        StringComparison.Ordinal))
                && TryCollectCore(
                    contract, mind, arc, body, sheet, task, claims))
            {
                continue;
            }
            if (repairs.TryGetValue(body.UnitId, out MindBody? repairTarget)
                && ArenaBasics.TryUnitSignature(
                    contract, body, "repair-beam", repairTarget.ActorId,
                    "standing support allocation"))
            {
                continue;
            }

            Position target = task.Behavior switch
            {
                "escort" => EscortTarget(contract, mind, body, carried),
                "guard" => GuardTarget(
                    contract, body, arc.VisibleCores, _securedCores),
                _ => Target(
                    sheet, task, assignment.FormationIndex, body, mind.Tick,
                    task.CorePolicy is "avoid" or "drop"
                        ? looseCoreTiles
                        : EmptyPositions),
            };
            int focusDistance = focus is null
                ? int.MaxValue
                : body.Position.ChebyshevDistance(focus.Position);
            bool atAssignment = AtAssignment(sheet, task, body.Position);
            bool mayEngage = MayEngage(task.Engagement, atAssignment,
                focusDistance);
            if (focus is not null
                && mayEngage
                && TryCombatSignature(
                    contract, mind, body, focus, target, task.Signature))
            {
                continue;
            }
            if (focus is not null
                && mayEngage
                && ArenaBasics.TryShoot(
                    contract, mind, body, focus, preferredOnly: true))
            {
                continue;
            }
            if (TryAdvanceSignature(contract, body, target))
                continue;
            if (ArenaBasics.TryMoveToward(
                    contract, mind, body, [target], claims,
                    $"{machine.PhaseId}:{task.Id}"))
            {
                continue;
            }
            if (TryAuthoredFacing(contract, body, task.Facing, target))
                continue;
            if (focus is not null
                && ArenaBasics.TryShoot(
                    contract, mind, body, focus, preferredOnly: true))
            {
                continue;
            }
            if (ArenaBasics.TryEvade(contract, mind, body, claims))
                continue;
            body.Hold($"maintaining {machine.PhaseId}:{task.Id}");
        }

        mind.Debug.Write(
            $"standing {machine.PhaseId}; live={snapshot.LiveFriendlies}; "
            + $"enemy-down={snapshot.KnownEnemiesUnavailable}; "
            + $"secured={snapshot.SecuredCores}; "
            + $"stalled={snapshot.TicksWithoutObjectiveProgress}");
    }

    public void EndMatch(MindEnd end) => _ = end;

    private void UpdateMemory(
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        StandingMemoryPolicy policy)
    {
        foreach (GenericActorContext.ObservedEnemyState enemy in mind.Enemies)
        {
            _enemyUnavailableUntil.Remove(enemy.ActorId.UnitId);
            _lastSeenEnemies[enemy.ActorId.UnitId] = new LastSeenEnemyMemory(
                enemy.Position, mind.Tick);
        }
        foreach (GenericActorContext.ObservedEvent observed in mind.VisibleEvents)
        {
            if (!_processedEvents.Add(observed.EventHandle))
                continue;
            if (observed.Payload is GenericActorContext.EventPayload.Destruction death
                && death.ActorId.TeamId != _teamId)
            {
                _enemyUnavailableUntil[death.ActorId.UnitId] =
                    observed.SourceTick + policy.EnemyUnavailableTicks;
                _lastSeenEnemies.Remove(death.ActorId.UnitId);
            }
            if (observed.Payload is not GenericActorContext.EventPayload.ArcRelay mode)
                continue;
            switch (mode.Fact)
            {
                case GenericActorContext.ArcRelayEvent.CoreDropped drop
                    when drop.SourceActorId.TeamId != _teamId:
                    _securedCores[CoreKey(drop.CoreId)] = new SecuredCoreMemory(
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
            else if (_securedCores.TryGetValue(key, out SecuredCoreMemory? prior))
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
        foreach (string contradicted in _securedCores
                     .Where(value => visibleTiles.Contains(value.Value.Position)
                         && !visibleCoreIds.Contains(value.Key))
                     .Select(value => value.Key).ToArray())
        {
            _securedCores.Remove(contradicted);
        }
        foreach (string stale in _securedCores
                     .Where(value => mind.Tick - value.Value.LastConfirmedTick
                         > policy.SecuredCoreMemoryTicks)
                     .Select(value => value.Key).ToArray())
        {
            _securedCores.Remove(stale);
        }
        foreach (int stale in _enemyUnavailableUntil
                     .Where(value => mind.Tick >= value.Value)
                     .Select(value => value.Key).ToArray())
        {
            _enemyUnavailableUntil.Remove(stale);
        }
        foreach (int stale in _lastSeenEnemies
                     .Where(value => mind.Tick - value.Value.LastConfirmedTick
                         > policy.LastSeenEnemyTicks)
                     .Select(value => value.Key).ToArray())
        {
            _lastSeenEnemies.Remove(stale);
        }
        GenericActorContext.ArcRelayReactorState own = arc.Reactors.Single(
            value => value.TeamId == _teamId);
        if (own.ChargePips != _lastOwnCharge)
        {
            _lastObjectiveProgressTick = mind.Tick;
            _lastOwnCharge = own.ChargePips;
        }
    }

    private StandingSnapshot Snapshot(
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        StandingStrategySheet sheet,
        StandingStrategyMachine machine)
    {
        string[] zones = sheet.Strategy.Phases
            .SelectMany(phase => phase.Entry)
            .Concat(sheet.Strategy.Phases
            .SelectMany(phase => phase.Transitions)
            .SelectMany(transition => transition.When)
            )
            .SelectMany(group => group.All.Concat(group.Any))
            .Select(condition => condition.Zone)
            .Concat(sheet.Strategy.Phases
                .SelectMany(phase => phase.Assignments)
                .SelectMany(task => task.When)
                .SelectMany(group => group.All.Concat(group.Any))
                .Select(condition => condition.Zone))
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.Ordinal).ToArray();
        Dictionary<string, int> friendly = zones.ToDictionary(
            zone => zone,
            zone => mind.Bodies.Count(body => sheet.Contains(zone, body.Position)),
            StringComparer.Ordinal);
        Dictionary<string, int> enemy = zones.ToDictionary(
            zone => zone,
            zone => mind.Enemies.Count(body => sheet.Contains(zone, body.Position)),
            StringComparer.Ordinal);
        Dictionary<string, int> remembered = zones.ToDictionary(
            zone => zone,
            zone => _lastSeenEnemies.Count(value =>
                sheet.Contains(zone, value.Value.Position)),
            StringComparer.Ordinal);
        var stable = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach ((string zone, int count) in friendly)
        {
            StableZoneMemory prior = _stableZones.GetValueOrDefault(zone)
                ?? new StableZoneMemory(count, 0, -1);
            int stableTicks = prior.LastTick == mind.Tick
                ? prior.StableTicks
                : prior.Count == count
                    ? prior.StableTicks + 1
                    : 1;
            var current = new StableZoneMemory(count, stableTicks, mind.Tick);
            _stableZones[zone] = current;
            stable[zone] = stableTicks >= sheet.Strategy.Memory.StableControlTicks
                ? count
                : 0;
        }
        int carriers = arc.VisibleCores.Count(core =>
            core.Disposition == GenericActorContext.ArcRelayCoreDisposition.Carried
            && core.CarrierActorId?.TeamId == _teamId);
        int loose = arc.VisibleCores.Count(core =>
            core.Disposition == GenericActorContext.ArcRelayCoreDisposition.Loose);
        return new StandingSnapshot(
            mind.Tick,
            mind.Bodies.Length,
            _enemyUnavailableUntil.Count,
            _securedCores.Count,
            loose,
            carriers,
            Math.Max(0, mind.Tick - _lastObjectiveProgressTick),
            arc.Wells.ToDictionary(
                well => well.WellId,
                well => well.OutstandingCoreId is null ? 0 : 1,
                StringComparer.Ordinal),
            friendly,
            stable,
            enemy,
            remembered);
    }

    private static bool Evaluate(
        StandingCondition condition,
        StandingSnapshot snapshot,
        StandingStrategySheet sheet)
    {
        int actual = condition.Fact switch
        {
            "always" => 1,
            "live-friendlies" => snapshot.LiveFriendlies,
            "known-enemies-unavailable" => snapshot.KnownEnemiesUnavailable,
            "secured-cores" => snapshot.SecuredCores,
            "visible-loose-cores" => snapshot.VisibleLooseCores,
            "friendly-carriers" => snapshot.FriendlyCarriers,
            "ticks-without-objective-progress" =>
                snapshot.TicksWithoutObjectiveProgress,
            "well-has-outstanding" => snapshot.OutstandingCoresByWell
                .GetValueOrDefault(ResolveSubject(condition.Subject, sheet)),
            "friendlies-in-zone" => snapshot.FriendliesByZone
                .GetValueOrDefault(condition.Zone),
            "stable-friendlies-in-zone" => snapshot.StableFriendliesByZone
                .GetValueOrDefault(condition.Zone),
            "visible-enemies-in-zone" => snapshot.EnemiesByZone
                .GetValueOrDefault(condition.Zone),
            "remembered-enemies-in-zone" => snapshot.RememberedEnemiesByZone
                .GetValueOrDefault(condition.Zone),
            _ => throw new InvalidDataException(
                $"Unknown standing fact '{condition.Fact}'."),
        };
        return condition.Operator switch
        {
            "at-least" => actual >= condition.Value,
            "at-most" => actual <= condition.Value,
            "equals" => actual == condition.Value,
            "less-than" => actual < condition.Value,
            "greater-than" => actual > condition.Value,
            _ => throw new InvalidDataException(
                $"Unknown standing operator '{condition.Operator}'."),
        };
    }

    private static string ResolveSubject(
        string subject,
        StandingStrategySheet sheet) => subject.StartsWith('$')
        ? sheet.Parameter(subject[1..])
        : subject;

    private static Dictionary<int, StandingUnitAssignment> Assign(
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        int ownTeamId,
        StandingSnapshot snapshot,
        StandingPhasePlan phase,
        StandingStrategySheet sheet)
    {
        var result = new Dictionary<int, StandingUnitAssignment>();
        HashSet<int> carrierUnits = arc.VisibleCores
            .Where(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Carried
                && core.CarrierActorId?.TeamId == ownTeamId)
            .Select(core => core.CarrierActorId!.UnitId)
            .ToHashSet();
        foreach (StandingAssignmentPlan task in phase.Assignments
                     .Where(task => task.When.Length == 0
                         || task.When.Any(group => StandingStrategyMachine.Matches(
                             group, snapshot,
                             (condition, value) => Evaluate(
                                 condition, value, sheet))))
                     .OrderBy(task => task.Priority)
                     .ThenBy(task => task.Id, StringComparer.Ordinal))
        {
            MindBody[] candidates = mind.Bodies
                .Where(body => !result.ContainsKey(body.UnitId))
                .Where(body => !task.CarrierOnly
                    || carrierUnits.Contains(body.UnitId))
                .Where(body => task.CandidateClasses.Length == 0
                    || task.CandidateClasses.Contains(
                        body.ClassId, StringComparer.Ordinal))
                .OrderBy(body => (string.Equals(task.CorePolicy, "deliver",
                        StringComparison.Ordinal) || task.PreferCarrier)
                    && carrierUnits.Contains(body.UnitId) ? 0 : 1)
                .ThenBy(body => ClassRank(task, body.ClassId))
                .ThenByDescending(body => body.Health)
                .ThenBy(body => body.UnitId)
                .Take(task.Count < 0 ? 8 : task.Count)
                .ToArray();
            foreach (MindBody body in candidates)
                result[body.UnitId] = new StandingUnitAssignment(
                    body.UnitId, task, body.UnitId);
        }
        StandingAssignmentPlan fallback = phase.Assignments
            .OrderByDescending(task => task.Count < 0)
            .ThenByDescending(task => task.Priority)
            .ThenBy(task => task.Id, StringComparer.Ordinal)
            .First();
        foreach (MindBody body in mind.Bodies.Where(body =>
                     !result.ContainsKey(body.UnitId)))
        {
            result[body.UnitId] = new StandingUnitAssignment(
                body.UnitId, fallback, body.UnitId);
        }
        return result;
    }

    private static int ClassRank(StandingAssignmentPlan task, string? classId)
    {
        int index = Array.IndexOf(task.CandidateClasses, classId);
        return index < 0 ? int.MaxValue : index;
    }

    private Position Target(
        StandingStrategySheet sheet,
        StandingAssignmentPlan task,
        int formationIndex,
        MindBody body,
        int tick,
        IReadOnlySet<Position> forbidden)
    {
        if (string.Equals(task.Position.Kind, "path", StringComparison.Ordinal))
        {
            Position[] path = sheet.Path(task.Position.Target);
            RouteProgress state = _routes.GetValueOrDefault(body.UnitId)
                ?? new RouteProgress(body.ActorId, task.Id, 0);
            if (state.ActorId != body.ActorId
                || !string.Equals(state.TaskId, task.Id,
                    StringComparison.Ordinal))
            {
                state = new RouteProgress(body.ActorId, task.Id, 0);
            }
            int index = Math.Min(state.Index, path.Length - 1);
            while (index < path.Length - 1
                && body.Position.ChebyshevDistance(path[index]) <= 1)
            {
                index++;
            }
            _routes[body.UnitId] = state with { Index = index };
            if (index == path.Length - 1
                && body.Position.ChebyshevDistance(path[index]) <= 1
                && !string.IsNullOrEmpty(task.Formation))
            {
                Position[] arrival = sheet.Formation(task.Formation);
                return FormationPosition(arrival, formationIndex, forbidden);
            }
            return path[index];
        }
        if (!string.IsNullOrEmpty(task.Formation))
        {
            Position[] formation = sheet.Formation(task.Formation);
            return FormationPosition(formation, formationIndex, forbidden);
        }
        if (string.Equals(task.Position.Kind, "zone", StringComparison.Ordinal))
            return sheet.ZoneCenter(task.Position.Target);
        if (string.Equals(task.Position.Kind, "own-reactor", StringComparison.Ordinal))
            return _ownReactor;
        if (string.Equals(task.Position.Kind, "enemy-reactor", StringComparison.Ordinal))
            return _enemyReactor;
        throw new InvalidDataException(
            $"Unknown position kind '{task.Position.Kind}'.");
    }

    private static Position FormationPosition(
        IReadOnlyCollection<Position> formation,
        int formationIndex,
        IReadOnlySet<Position> forbidden)
    {
        Position[] available = formation
            .Where(position => !forbidden.Contains(position))
            .ToArray();
        if (available.Length == 0)
            available = formation.ToArray();
        return available[Math.Abs(formationIndex) % available.Length];
    }

    private static Position EscortTarget(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        MindBody? carrier = mind.Bodies
            .Where(candidate => candidate.UnitId != body.UnitId
                && carried.ContainsKey(candidate.ActorId))
            .OrderBy(candidate => candidate.Position.ChebyshevDistance(
                body.Position))
            .ThenBy(candidate => candidate.UnitId)
            .FirstOrDefault();
        if (carrier is null)
            return body.Position;
        return ArenaBasics.ApproachTiles(contract.Map, carrier.Position)
            .OrderBy(position => position.ChebyshevDistance(body.Position))
            .ThenBy(position => position.Y)
            .ThenBy(position => position.X)
            .FirstOrDefault(carrier.Position);
    }

    private static Position GuardTarget(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        IReadOnlyCollection<GenericActorContext.ArcRelayCoreState> visibleCores,
        IReadOnlyDictionary<string, SecuredCoreMemory> securedCores)
    {
        Position? core = visibleCores
            .Where(value => value.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Loose)
            .OrderBy(value => body.Position.ChebyshevDistance(value.Position))
            .ThenBy(value => value.CoreId.SourceWellId, StringComparer.Ordinal)
            .ThenBy(value => value.CoreId.SourceOrdinal)
            .Select(value => (Position?)value.Position)
            .FirstOrDefault()
            ?? securedCores.Values
                .OrderBy(value => body.Position.ChebyshevDistance(
                    value.Position))
                .ThenByDescending(value => value.LastConfirmedTick)
                .Select(value => (Position?)value.Position)
                .FirstOrDefault();
        if (core is not Position secured)
            return body.Position;
        return ArenaBasics.ApproachTiles(contract.Map, secured)
            .OrderBy(position => position.ChebyshevDistance(body.Position))
            .ThenBy(position => position.Y)
            .ThenBy(position => position.X)
            .FirstOrDefault(body.Position);
    }

    private static bool MayEngage(
        string engagement,
        bool atAssignment,
        int focusDistance) => engagement switch
    {
        "avoid" => false,
        "evade" => atAssignment || focusDistance <= 4,
        "hold" => atAssignment,
        "advance-under-fire" => atAssignment || focusDistance <= 4,
        _ => atAssignment || focusDistance <= 4,
    };

    private bool TryAuthoredFacing(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string policy,
        Position target)
    {
        Direction? desired = policy switch
        {
            "north" => Direction.North,
            "east" => Direction.East,
            "south" => Direction.South,
            "west" => Direction.West,
            "toward-target" => DirectionToward(body.Position, target),
            "toward-own-reactor" => DirectionToward(
                body.Position, _ownReactor),
            "toward-enemy-reactor" => DirectionToward(
                body.Position, _enemyReactor),
            _ => null,
        };
        if (desired is not Direction direction || body.Facing == direction)
            return false;
        GenericActorRulesContract.ActionDefinition? definition =
            contract.Rules.Actions.FirstOrDefault(value =>
                value.Kind == GenericActorRulesContract.ActionKind.Rotation);
        GenericActorActionLegality? action = definition is null
            ? null
            : body.Action(definition.Id);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = action?.Constraints.OfType<
                GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>().SingleOrDefault();
        if (action is not { Available: true }
            || directions is null
            || !directions.AllowedValues.Contains(direction))
        {
            return false;
        }
        body.Command(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(direction)],
            $"hold authored facing {policy}");
        return true;
    }

    private static Direction? DirectionToward(Position from, Position to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        if (dx == 0 && dy == 0)
            return null;
        if (Math.Abs(dx) >= Math.Abs(dy))
            return dx >= 0 ? Direction.East : Direction.West;
        return dy >= 0 ? Direction.South : Direction.North;
    }

    private static bool AtAssignment(
        StandingStrategySheet sheet,
        StandingAssignmentPlan task,
        Position position) => task.Position.Kind switch
    {
        "zone" => sheet.Contains(task.Position.Target, position),
        _ => false,
    };

    private GenericActorContext.ObservedEnemyState? FocusTarget(
        MindContext mind,
        string policy,
        GenericActorContext.ObservedEnemyState? enemyCarrier)
    {
        IEnumerable<GenericActorContext.ObservedEnemyState> ordered = policy switch
        {
            "weakest" => mind.Enemies
                .OrderBy(enemy => enemy.Health)
                .ThenBy(enemy => enemy.ActorId),
            "home-threat" => mind.Enemies
                .OrderBy(enemy => enemy.Position.ChebyshevDistance(_ownReactor))
                .ThenBy(enemy => enemy.Health)
                .ThenBy(enemy => enemy.ActorId),
            _ => mind.Enemies
                .OrderBy(enemy => enemy.Position.ChebyshevDistance(_enemyReactor))
                .ThenBy(enemy => enemy.Health)
                .ThenBy(enemy => enemy.ActorId),
        };
        return string.Equals(policy, "carrier-first", StringComparison.Ordinal)
            && enemyCarrier is not null
                ? enemyCarrier
                : ordered.FirstOrDefault();
    }

    private static Dictionary<int, MindBody> AllocateRepairs(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        IReadOnlyDictionary<int, StandingUnitAssignment> assignments,
        IReadOnlySet<ActorIdentity> carriers)
    {
        MindBody[] injured = mind.Bodies
            .Where(body => body.Health < contract.Rules.Forms.Single(form =>
                string.Equals(form.Id, body.FormId,
                    StringComparison.Ordinal)).MaxHealth)
            .OrderBy(body => body.Health)
            .ThenBy(body => body.UnitId).ToArray();
        var claimed = new HashSet<int>();
        var result = new Dictionary<int, MindBody>();
        foreach (MindBody medic in mind.Bodies
                     .Where(body => assignments[body.UnitId].Plan.Behavior
                         is "support" or "escort")
                     .OrderBy(body => body.UnitId))
        {
            MindBody? target = injured
                .Where(value => value.UnitId != medic.UnitId
                    && !claimed.Contains(value.UnitId))
                .OrderBy(value => string.Equals(
                        assignments[medic.UnitId].Plan.Behavior,
                        "escort", StringComparison.Ordinal)
                    && carriers.Contains(value.ActorId)
                        ? 0 : 1)
                .ThenBy(value => value.Position.ChebyshevDistance(
                    medic.Position))
                .ThenBy(value => value.Health)
                .ThenBy(value => value.UnitId)
                .FirstOrDefault();
            if (target is null)
                continue;
            claimed.Add(target.UnitId);
            result[medic.UnitId] = target;
        }
        return result;
    }

    private bool TryCollectCore(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        StandingStrategySheet sheet,
        StandingAssignmentPlan task,
        ArenaBasics.Claims claims)
    {
        string source = string.IsNullOrEmpty(task.CoreSource)
            ? ""
            : task.CoreSource.StartsWith('$')
                ? sheet.Parameter(task.CoreSource[1..])
                : task.CoreSource;
        GenericActorContext.ArcRelayCoreState? core = arc.VisibleCores
            .Where(value => value.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Loose)
            .OrderBy(value => _securedCores.ContainsKey(CoreKey(value.CoreId))
                ? 0 : 1)
            .ThenBy(value => body.Position.ChebyshevDistance(value.Position))
            .ThenBy(value => value.CoreId.SourceWellId, StringComparer.Ordinal)
            .ThenBy(value => value.CoreId.SourceOrdinal)
            .FirstOrDefault();
        if (core is null)
        {
            GenericActorContext.ArcRelayWellState? well = arc.Wells
                .Where(value => value.OutstandingCoreId is not null)
                .Where(value => string.IsNullOrEmpty(source)
                    || string.Equals(value.WellId, source,
                        StringComparison.Ordinal))
                .OrderBy(value => body.Position.ChebyshevDistance(
                    value.Position))
                .ThenBy(value => value.WellId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (well is null)
                return false;
            if (TryAdvanceSignature(contract, body, well.Position))
                return true;
            return ArenaBasics.TryMoveToward(
                contract, mind, body, [well.Position], claims,
                $"scouting authored {well.WellId} Core source");
        }
        if (TryAdvanceSignature(contract, body, core.Position))
            return true;
        return ArenaBasics.TryMoveToward(
            contract, mind, body, [core.Position], claims,
            "standing scorer collects declared Core");
    }

    private void ActCarrier(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        StandingStrategySheet sheet,
        StandingAssignmentPlan task,
        ArenaBasics.Claims claims)
    {
        Position extraction = ReturnTarget(sheet, task, body);
        Position? committed = ArenaBasics.StaticFirstStepAvoidingReservations(
            contract, mind, body, extraction);
        if (committed is Position step
            && ArenaBasics.TryMoveDirect(
                contract, mind, body, step, claims,
                "committed standing-strategy return step"))
        {
            return;
        }
        if (TryAdvanceSignature(contract, body, extraction))
            return;
        if (ArenaBasics.TryMoveToward(
                contract, mind, body, [extraction], claims,
                "standing scorer follows authored extraction"))
            return;
        GenericActorContext.ObservedEnemyState? threat = mind.Enemies
            .OrderBy(enemy => enemy.Position.ChebyshevDistance(body.Position))
            .ThenBy(enemy => enemy.Health).ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault();
        if (ArenaBasics.TryShoot(contract, mind, body, threat))
            return;
        if (ArenaBasics.TryEvade(contract, mind, body, claims))
            return;
        body.Hold("standing scorer waits for return lane");
    }

    private Position ReturnTarget(
        StandingStrategySheet sheet,
        StandingAssignmentPlan task,
        MindBody body)
    {
        string returnPath = sheet.Strategy.Parameters
            .GetValueOrDefault("returnPath", "");
        if (string.IsNullOrEmpty(returnPath))
            return _ownReactor;
        Position[] path = sheet.Path(returnPath);
        string taskId = $"return-{task.Id}";
        RouteProgress state = _routes.GetValueOrDefault(body.UnitId)
            ?? new RouteProgress(body.ActorId, taskId, 0);
        if (state.ActorId != body.ActorId
            || !string.Equals(state.TaskId, taskId, StringComparison.Ordinal))
        {
            state = new RouteProgress(body.ActorId, taskId, 0);
        }
        int index = Math.Min(state.Index, path.Length - 1);
        while (index < path.Length - 1
            && body.Position.ChebyshevDistance(path[index]) <= 1)
        {
            index++;
        }
        _routes[body.UnitId] = state with { Index = index };
        return path[index];
    }

    private void ActTransferCarrier(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        IReadOnlyDictionary<int, StandingUnitAssignment> assignments,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried,
        StandingAssignmentPlan task,
        ArenaBasics.Claims claims)
    {
        GenericActorActionLegality? handoff = body.Action("handoff-core");
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = handoff?.Constraints.OfType<
                GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint>().SingleOrDefault();
        MindBody? scorer = mind.Bodies
            .Where(candidate => !carried.ContainsKey(candidate.ActorId)
                && string.Equals(assignments[candidate.UnitId].Plan.CorePolicy,
                    "deliver", StringComparison.Ordinal))
            .OrderBy(candidate => candidate.Position.ChebyshevDistance(
                body.Position))
            .ThenBy(candidate => candidate.UnitId)
            .FirstOrDefault(candidate => targets?.AllowedValues.Contains(
                new GenericActorActionArgument.UnitTarget(
                    candidate.ActorId.TeamId, candidate.UnitId)) == true);
        if (handoff is { Available: true } && targets is not null
            && scorer is not null)
        {
            var target = new GenericActorActionArgument.UnitTarget(
                scorer.ActorId.TeamId, scorer.UnitId);
            body.Command(handoff.ActionId, handoff.ActionCode,
                [new GenericActorActionArgument.UnitTargetArgument(target)],
                "transfer accidental pickup to declared scorer");
            return;
        }
        if (string.Equals(task.CoreFallback, "deliver",
                StringComparison.Ordinal))
        {
            ActCarrier(
                contract, mind, body, _sheet!, task, claims);
            return;
        }
        if (string.Equals(task.CoreFallback, "drop", StringComparison.Ordinal)
            && TryDropCore(body, "fallback cache for declared scorer"))
        {
            return;
        }
        GenericActorContext.ObservedEnemyState? threat = mind.Enemies
            .OrderBy(enemy => enemy.Position.ChebyshevDistance(body.Position))
            .ThenBy(enemy => enemy.Health).ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault();
        if (ArenaBasics.TryShoot(contract, mind, body, threat))
            return;
        body.Hold("holding secured Core for a legal declared handoff");
    }

    private static bool TryDropCore(MindBody body, string reason)
    {
        GenericActorActionLegality? drop = body.Action("drop-core");
        if (drop is not { Available: true })
            return false;
        body.Command(drop.ActionId, drop.ActionCode, [], reason);
        return true;
    }

    private static bool TryCombatSignature(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        GenericActorContext.ObservedEnemyState target,
        Position assignment,
        string policy)
    {
        if (policy is "none" or "conserve")
            return false;
        return ArenaBasics.TryUnitSignature(contract, body, "target-paint",
            target.ActorId, "standing focus paint")
        || ArenaBasics.TryHeadingSignature(contract, body, "tractor-hook",
            target.Position, "standing focus displacement")
        || ArenaBasics.TryHeadingSignature(contract, body, "rail-line",
            target.Position, "standing focus rail")
        || ArenaBasics.TryPositionSignature(contract, body, "falling-star",
            target.Position, "standing focus artillery")
        || (body.Position.ChebyshevDistance(target.Position) <= 1
            && ArenaBasics.TryParameterlessSignature(contract, body,
                "kinetic-burst", "standing perimeter burst"))
        || (body.Position.ChebyshevDistance(target.Position) <= 3
            && ArenaBasics.TryParameterlessSignature(contract, body,
                "null-field", "standing perimeter suppression"))
        || ArenaBasics.TryDirectionSignature(contract, body, "prism-wall",
            target.Position, "standing perimeter screen")
        || ArenaBasics.TryPositionSignature(contract, body, "hardlight-block",
            assignment, "standing perimeter block");
    }

    private static bool TryAdvanceSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Position target) => body.Position.ChebyshevDistance(target) >= 3
        && ArenaBasics.TryHeadingSignature(
            contract, body, "vector-dash", target,
            "standing route vector dash");

    private static string CoreKey(GenericActorContext.ArcRelayCoreId id) =>
        $"{id.SourceWellId}:{id.SourceOrdinal}";

    private static string Role(string phase, string task)
    {
        string value = $"{phase}-{task}".ToLowerInvariant();
        return value.Length <= 24 ? value : value[..24];
    }

    private sealed record SecuredCoreMemory(
        Position Position,
        int LastConfirmedTick);

    private sealed record LastSeenEnemyMemory(
        Position Position,
        int LastConfirmedTick);

    private sealed record StableZoneMemory(
        int Count,
        int StableTicks,
        int LastTick);

    private sealed record RouteProgress(
        ActorIdentity ActorId,
        string TaskId,
        int Index);
}
