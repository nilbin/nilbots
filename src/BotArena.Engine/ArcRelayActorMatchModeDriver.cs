using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Arc Relay's match-scoped objective authority. It owns Core identities,
/// Well capacity/rearm clocks, reactor state, and the Core relocation clock;
/// the common session calls its methods at the matching generic phases.
/// </summary>
internal sealed class ArcRelayActorMatchModeDriver
    : IGenericActorMatchModeDriver
{
    private const string PulseChannel = "pulses";
    private const string ChargeChannel = "reactor-charge";

    private readonly ActorResolvedMatchDefinition _definition;
    private readonly ArcRelayGameModeDefinition _gameMode;
    private readonly PublicMatchTopology _topology;
    private readonly ImmutableArray<WellRuntime> _wells;
    private readonly Dictionary<string, WellRuntime> _wellsById;
    private readonly Dictionary<ArcRelayCoreId, CoreRuntime> _cores = [];
    private readonly Dictionary<int, ReactorRuntime> _reactors;
    private readonly ImmutableDictionary<int, ImmutableHashSet<Position>>
        _homePads;
    private readonly ArcRelaySignatureRuntime _signatures;
    private readonly ulong _matchSeed;
    private int _currentTick;
    private int? _latestPulseTeamId;
    private int? _latestPulseTick;

    public ArcRelayActorMatchModeDriver(
        ActorResolvedMatchDefinition definition,
        ulong matchSeed)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _matchSeed = matchSeed;
        _definition = definition;
        _gameMode = definition.Rules.GameMode as ArcRelayGameModeDefinition
            ?? throw new ArgumentException(
                "Arc Relay driver requires Arc Relay rules.",
                nameof(definition));
        var binding = definition.ModeMapBinding
            as ArcRelayActorModeMapBindingDefinition
            ?? throw new ArgumentException(
                "Arc Relay driver requires an Arc Relay map binding.",
                nameof(definition));
        _topology = definition.Topology;
        _signatures = new ArcRelaySignatureRuntime(definition, _gameMode);
        Dictionary<string, ActorMapRegionDefinition> regions = definition.Map
            .Regions.ToDictionary(value => value.RegionId, StringComparer.Ordinal);
        _wells = _gameMode.Wells.Select((schedule, index) =>
                new WellRuntime(
                    schedule,
                    regions[binding.OrderedWellRegionIds[index]].Tiles.Single()))
            .ToImmutableArray();
        _wellsById = _wells.ToDictionary(
            value => value.Schedule.WellId,
            StringComparer.Ordinal);
        Dictionary<int, int> participantTeam = _topology.Participants
            .ToDictionary(value => value.ParticipantId, value => value.TeamId);
        _reactors = definition.ParticipantRegionAssignments
            .Where(value => string.Equals(
                value.RegionRoleId,
                binding.ReactorRegionRoleId,
                StringComparison.Ordinal))
            .ToDictionary(
                value => participantTeam[value.ParticipantId],
                value => new ReactorRuntime(
                    participantTeam[value.ParticipantId],
                    regions[value.MapRegionId].Tiles.Single(),
                    _gameMode.ArcRelayVictory.PulsesToDestroyReactor));
        _homePads = definition.ParticipantRegionAssignments
            .Where(value => string.Equals(
                value.RegionRoleId,
                binding.HomePadRegionRoleId,
                StringComparison.Ordinal))
            .ToImmutableDictionary(
                value => participantTeam[value.ParticipantId],
                value => regions[value.MapRegionId].Tiles.ToImmutableHashSet());
    }

    public GenericActorModeState State =>
        new GenericActorModeState.ArcRelay(ProjectState());

    internal ArcRelaySignatureRuntime Signatures => _signatures;

    internal GenericActorRuntimeObservation.ModeObservationState.ArcRelay
        ProjectStateAtTick(int tick)
    {
        _currentTick = tick;
        return ProjectState();
    }

    public bool CarriesCore(ActorIdentity actorId) =>
        _cores.Values.Any(value => value.CarrierActorId == actorId);

    public bool CanCarrierRelocate(ActorIdentity actorId, int tick) =>
        _cores.Values.SingleOrDefault(value => value.CarrierActorId == actorId)
            is not { } core
        || tick >= core.NextRelocationTick;

    public bool CanEnter(ActorIdentity actorId, Position position) =>
        !_homePads.Any(value =>
            value.Key != actorId.TeamId && value.Value.Contains(position));

    public ImmutableArray<GenericActorModeEvent> LaunchArcToss(
        int tick,
        ActorIdentity actorId,
        Position target,
        int completesAtTick)
    {
        CoreRuntime? core = _cores.Values.SingleOrDefault(value =>
            value.CarrierActorId == actorId);
        if (core is null || tick < core.NextRelocationTick)
            return [];
        core.CarrierActorId = null;
        core.FlightTarget = target;
        core.FlightCompletesAtTick = completesAtTick;
        return
        [
            Spatial(
                new ArcRelayEvent.CoreDropped(
                    core.CoreId,
                    actorId,
                    core.Position,
                    core.NextRelocationTick,
                    ArcRelayEvent.CoreDropKind.SignatureDeparture),
                core.Position),
        ];
    }

    public ImmutableArray<GenericActorModeEvent> LandArcToss(
        int tick,
        ActorIdentity ownerActorId,
        Position requestedTarget,
        GenericActorModeWorldView world)
    {
        CoreRuntime? core = _cores.Values.SingleOrDefault(value =>
            value.FlightCompletesAtTick == tick
            && value.FlightTarget == requestedTarget);
        if (core is null)
            return [];
        Position from = core.Position;
        Position landing = ArcTossLanding(from, requestedTarget);
        core.Position = landing;
        core.FlightTarget = null;
        core.FlightCompletesAtTick = null;
        core.NextRelocationTick = checked(
            tick + _gameMode.CoreRelocationIntervalTicks);
        var events = ImmutableArray.CreateBuilder<GenericActorModeEvent>();
        events.Add(Spatial(
            new ArcRelayEvent.CoreRelocated(
                core.CoreId,
                CarrierActorId: null,
                from,
                landing,
                core.NextRelocationTick,
                ArcRelayEvent.CoreRelocationKind.ArcTossLanding),
            landing));
        GenericActorModeActiveLife? catcher = world.ActiveLives
            .Where(value => value.ActorId.TeamId == ownerActorId.TeamId
                && value.Position == landing
                && !CarriesCore(value.ActorId))
            .OrderBy(value => value.ActorId)
            .FirstOrDefault();
        if (catcher is not null)
        {
            core.CarrierActorId = catcher.ActorId;
            events.Add(Spatial(
                new ArcRelayEvent.CorePickedUp(
                    core.CoreId,
                    catcher.ActorId,
                    landing,
                    core.NextRelocationTick),
                landing));
        }
        else
        {
            events.Add(Spatial(
                new ArcRelayEvent.CoreDropped(
                    core.CoreId,
                    ownerActorId,
                    landing,
                    core.NextRelocationTick,
                    ArcRelayEvent.CoreDropKind.ArcTossLanding),
                landing));
        }
        return events.ToImmutable();
    }

    public GenericActorModeTickResult PrepareTick(
        int tick,
        GenericActorModeWorldView world)
    {
        _currentTick = tick;
        GenericActorRuntimeObservation.ModeObservationState.ArcRelay before =
            ProjectState();
        var events = ImmutableArray.CreateBuilder<GenericActorModeEvent>();

        foreach (WellRuntime well in _wells)
        {
            if (well.RearmCompletesAtTick == tick)
            {
                well.RearmCompletesAtTick = null;
                well.PendingCharge = false;
                Birth(well, tick, world, events);
            }
        }
        foreach (WellRuntime well in _wells)
        {
            ArcRelayWellScheduleDefinition schedule = well.Schedule;
            if (!ScheduledBirthAt(schedule, tick))
                continue;
            if (well.OutstandingCoreId is null
                && well.RearmCompletesAtTick is null)
            {
                Birth(well, tick, world, events);
            }
            else if (!well.PendingCharge)
            {
                well.PendingCharge = true;
                events.Add(Public(new ArcRelayEvent.WellChanged(
                    well.Schedule.WellId,
                    true,
                    well.RearmCompletesAtTick,
                    well.OutstandingCoreId)));
            }
        }
        // A loose Core may have been left under a surviving body by a death
        // drop or other end-of-tick effect. Tick-start pickup is therefore a
        // general phase, not merely a side effect of birth. This is also what
        // makes the frozen observation honest: possession is settled before
        // minds see the new tick.
        PickUpLooseCores(tick, world, events);

        GenericActorRuntimeObservation.ModeObservationState.ArcRelay after =
            ProjectState();
        return new GenericActorModeTickResult(
            scoreChanges: [],
            Equals(before, after) ? null : after,
            modeObjectiveReached: false,
            events);
    }

    public ImmutableArray<GenericActorModeEvent> ResolveMovement(
        int tick,
        IReadOnlyCollection<ActorIdentity> movedActors,
        GenericActorModeWorldView world)
    {
        _currentTick = tick;
        var events = ImmutableArray.CreateBuilder<GenericActorModeEvent>();
        Dictionary<ActorIdentity, GenericActorModeActiveLife> lives = world
            .ActiveLives.ToDictionary(value => value.ActorId);
        foreach (ActorIdentity actorId in movedActors.Order())
        {
            if (!_cores.Values.Any(value => value.CarrierActorId == actorId))
                continue;
            CoreRuntime core = _cores.Values.Single(
                value => value.CarrierActorId == actorId);
            Position from = core.Position;
            Position to = lives[actorId].Position;
            if (from == to)
                continue;
            core.Position = to;
            core.NextRelocationTick = checked(
                tick + _gameMode.CoreRelocationIntervalTicks);
            events.Add(Spatial(
                new ArcRelayEvent.CoreRelocated(
                    core.CoreId,
                    actorId,
                    from,
                    to,
                    core.NextRelocationTick,
                    ArcRelayEvent.CoreRelocationKind.CarriedMovement),
                to));
        }
        PickUpLooseCores(tick, world, events);
        return events.ToImmutable();
    }

    public ImmutableArray<GenericActorModeEvent> ResolveForcedMovement(
        int tick,
        IReadOnlyCollection<ActorIdentity> movedActors,
        GenericActorModeWorldView world)
    {
        _currentTick = tick;
        var events = ImmutableArray.CreateBuilder<GenericActorModeEvent>();
        Dictionary<ActorIdentity, GenericActorModeActiveLife> lives = world
            .ActiveLives.ToDictionary(value => value.ActorId);
        foreach (ActorIdentity actorId in movedActors.Order())
        {
            CoreRuntime? core = _cores.Values.SingleOrDefault(value =>
                value.CarrierActorId == actorId);
            if (core is null || !lives.TryGetValue(actorId, out var life)
                || core.Position == life.Position)
            {
                continue;
            }
            Position from = core.Position;
            core.Position = life.Position;
            core.NextRelocationTick = checked(
                tick + _gameMode.CoreRelocationIntervalTicks);
            events.Add(Spatial(
                new ArcRelayEvent.CoreRelocated(
                    core.CoreId,
                    actorId,
                    from,
                    core.Position,
                    core.NextRelocationTick,
                    ArcRelayEvent.CoreRelocationKind.ForcedDisplacement),
                core.Position));
        }
        PickUpLooseCores(tick, world, events);
        return events.ToImmutable();
    }

    public bool TrySignatureDepartureDrop(
        int tick,
        ActorIdentity sourceActorId,
        out GenericActorModeEvent? modeEvent)
    {
        CoreRuntime? core = _cores.Values.SingleOrDefault(value =>
            value.CarrierActorId == sourceActorId);
        if (core is null)
        {
            modeEvent = null;
            return false;
        }
        core.CarrierActorId = null;
        modeEvent = Spatial(
            new ArcRelayEvent.CoreDropped(
                core.CoreId,
                sourceActorId,
                core.Position,
                core.NextRelocationTick,
                ArcRelayEvent.CoreDropKind.SignatureDeparture),
            core.Position);
        return true;
    }

    public ImmutableArray<GenericActorModeEvent> HandleDestructions(
        int tick,
        IReadOnlyCollection<FrontlineScrapDestruction> destructions)
    {
        _currentTick = tick;
        var events = ImmutableArray.CreateBuilder<GenericActorModeEvent>();
        foreach (FrontlineScrapDestruction destruction in destructions)
        {
            CoreRuntime? core = _cores.Values.SingleOrDefault(value =>
                value.CarrierActorId == destruction.ActorId);
            if (core is null)
                continue;
            core.CarrierActorId = null;
            core.Position = destruction.Position;
            events.Add(Spatial(
                new ArcRelayEvent.CoreDropped(
                    core.CoreId,
                    destruction.ActorId,
                    destruction.Position,
                    core.NextRelocationTick,
                    ArcRelayEvent.CoreDropKind.Destruction),
                destruction.Position));
        }
        return events.ToImmutable();
    }

    public bool TryDrop(
        int tick,
        ActorIdentity sourceActorId,
        out GenericActorModeEvent? modeEvent)
    {
        _currentTick = tick;
        CoreRuntime? core = _cores.Values.SingleOrDefault(
            value => value.CarrierActorId == sourceActorId);
        if (core is null)
        {
            modeEvent = null;
            return false;
        }
        core.CarrierActorId = null;
        modeEvent = Spatial(
            new ArcRelayEvent.CoreDropped(
                core.CoreId,
                sourceActorId,
                core.Position,
                core.NextRelocationTick,
                ArcRelayEvent.CoreDropKind.Voluntary),
            core.Position);
        return true;
    }

    public bool TryHandoff(
        int tick,
        ActorIdentity sourceActorId,
        ActorIdentity targetActorId,
        Position sourcePosition,
        Position targetPosition,
        out GenericActorModeEvent? modeEvent)
    {
        _currentTick = tick;
        CoreRuntime? core = _cores.Values.SingleOrDefault(
            value => value.CarrierActorId == sourceActorId);
        if (core is null
            || tick < core.NextRelocationTick
            || sourceActorId.TeamId != targetActorId.TeamId
            || sourcePosition.ChebyshevDistance(targetPosition) != 1
            || CarriesCore(targetActorId))
        {
            modeEvent = null;
            return false;
        }
        core.CarrierActorId = targetActorId;
        core.Position = targetPosition;
        core.NextRelocationTick = checked(
            tick + _gameMode.CoreRelocationIntervalTicks);
        modeEvent = Spatial(
            new ArcRelayEvent.CoreHandedOff(
                core.CoreId,
                sourceActorId,
                targetActorId,
                targetPosition,
                core.NextRelocationTick),
            targetPosition);
        return true;
    }

    public GenericActorModeTickResult ApplyJointTick(
        GenericActorModeWorldView world,
        GenericActorModeTickInput input)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(input);
        _currentTick = input.Tick;
        GenericActorRuntimeObservation.ModeObservationState.ArcRelay before =
            ProjectState();
        Dictionary<int, (int Pulses, int Charge)> oldScores = _reactors
            .ToDictionary(
                value => value.Key,
                value => (value.Value.Pulses, value.Value.Charge));
        var events = ImmutableArray.CreateBuilder<GenericActorModeEvent>();

        events.AddRange(HandleDestructions(input.Tick, input.Destructions));

        foreach (GenericActorModeActiveLife life in world.ActiveLives
                     .OrderBy(value => value.ActorId))
        {
            CoreRuntime? core = _cores.Values.SingleOrDefault(
                value => value.CarrierActorId == life.ActorId);
            if (core is null
                || !_reactors.TryGetValue(life.ActorId.TeamId, out var reactor)
                || reactor.Position != life.Position)
            {
                continue;
            }
            // Threefold: a Core whose origin socket is already filled cannot
            // be consumed. It stays carried, physical, and contestable.
            if (_gameMode.ThreefoldSockets
                && reactor.FilledSockets.Contains(core.CoreId.SourceWellId))
            {
                continue;
            }
            Bank(input.Tick, core, life.ActorId, reactor, events);
        }

        GenericActorRuntimeObservation.ModeObservationState.ArcRelay after =
            ProjectState();
        var changes = ImmutableArray.CreateBuilder<GenericActorModeScoreChange>();
        foreach ((int teamId, ReactorRuntime reactor) in _reactors.OrderBy(
                     value => value.Key))
        {
            (int pulses, int charge) = oldScores[teamId];
            if (pulses != reactor.Pulses)
                changes.Add(new GenericActorModeScoreChange(
                    teamId,
                    PulseChannel,
                    reactor.Pulses));
            if (charge != reactor.Charge)
                changes.Add(new GenericActorModeScoreChange(
                    teamId,
                    ChargeChannel,
                    reactor.Charge));
        }
        return new GenericActorModeTickResult(
            changes.ToImmutable(),
            Equals(before, after) ? null : after,
            _reactors.Values.Any(value => value.IntegritySegments == 0),
            events);
    }

    public GenericActorModeCompletion ResolveCompletion(
        GenericActorModeCompletionKind kind,
        int endTick,
        GenericActorModeWorldView world)
    {
        GenericArcRelayEndReason reason = kind switch
        {
            GenericActorModeCompletionKind.FaultEligibility =>
                GenericArcRelayEndReason.FaultEligibility,
            GenericActorModeCompletionKind.ModeObjective =>
                GenericArcRelayEndReason.ReactorDestroyed,
            GenericActorModeCompletionKind.MaxTicks =>
                GenericArcRelayEndReason.MaxTicks,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        string completionReason = reason switch
        {
            GenericArcRelayEndReason.FaultEligibility => "fault-eligibility",
            GenericArcRelayEndReason.ReactorDestroyed => "reactor-destroyed",
            GenericArcRelayEndReason.MaxTicks => "max-ticks",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        return new GenericActorModeCompletion.ArcRelay(
            completionReason,
            endTick,
            ResolveStandings(world, kind),
            reason,
            ProjectState());
    }

    public GenericActorModeProjection Project(
        GenericActorModeWorldView world)
    {
        HashSet<int> eligible = world.EligibleTeamIds.ToHashSet();
        return new GenericActorModeProjection(
            new GenericActorRuntimeObservation.ScoreboardState(
                _topology.Teams.OrderBy(value => value.TeamId)
                    .Select(team => new GenericActorRuntimeObservation
                        .TeamScoreState(
                            team.TeamId,
                            eligible.Contains(team.TeamId),
                            Scores(_reactors[team.TeamId])))
                    .ToImmutableArray()),
            ProjectState());
    }

    public IReadOnlyList<string> InvestableTracks(int teamId) => [];
    public bool TryInvest(int tick, ActorIdentity actor, string trackId) => false;
    public GenericActorModeStatModifiers StatModifiersFor(ActorIdentity actor) =>
        GenericActorModeStatModifiers.None;

    private void Birth(
        WellRuntime well,
        int tick,
        GenericActorModeWorldView world,
        ImmutableArray<GenericActorModeEvent>.Builder events)
    {
        var coreId = new ArcRelayCoreId(
            well.Schedule.WellId,
            well.NextSourceOrdinal++);
        var core = new CoreRuntime(coreId, well.Position);
        _cores.Add(coreId, core);
        well.OutstandingCoreId = coreId;
        events.Add(Spatial(
            new ArcRelayEvent.CoreBorn(coreId, well.Position),
            well.Position));
        events.Add(Public(new ArcRelayEvent.WellChanged(
            well.Schedule.WellId,
            well.PendingCharge,
            well.RearmCompletesAtTick,
            coreId)));
        PickUpCore(tick, core, world, events);
    }

    private void PickUpLooseCores(
        int tick,
        GenericActorModeWorldView world,
        ImmutableArray<GenericActorModeEvent>.Builder events)
    {
        foreach (CoreRuntime core in _cores.Values
                     .Where(value => value.CarrierActorId is null
                         && value.FlightTarget is null)
                     .OrderBy(value => value.CoreId.SourceWellId,
                         StringComparer.Ordinal)
                     .ThenBy(value => value.CoreId.SourceOrdinal))
        {
            PickUpCore(tick, core, world, events);
        }
    }

    private void PickUpCore(
        int tick,
        CoreRuntime core,
        GenericActorModeWorldView world,
        ImmutableArray<GenericActorModeEvent>.Builder events)
    {
        GenericActorModeActiveLife? picker = world.ActiveLives
            .Where(value => value.Position == core.Position
                && !CarriesCore(value.ActorId))
            .OrderBy(value => value.ActorId)
            .FirstOrDefault();
        if (picker is null)
            return;
        core.CarrierActorId = picker.ActorId;
        core.NextRelocationTick = checked(
            tick + _gameMode.CoreRelocationIntervalTicks);
        events.Add(Spatial(
            new ArcRelayEvent.CorePickedUp(
                core.CoreId,
                picker.ActorId,
                core.Position,
                core.NextRelocationTick),
            core.Position));
    }

    private void Bank(
        int tick,
        CoreRuntime core,
        ActorIdentity carrier,
        ReactorRuntime reactor,
        ImmutableArray<GenericActorModeEvent>.Builder events)
    {
        _cores.Remove(core.CoreId);
        WellRuntime well = _wellsById[core.CoreId.SourceWellId];
        well.OutstandingCoreId = null;
        if (_gameMode.ThreefoldSockets)
        {
            reactor.FilledSockets.Add(core.CoreId.SourceWellId);
            reactor.Charge = reactor.FilledSockets.Count;
        }
        else
        {
            reactor.Charge++;
        }
        events.Add(Public(new ArcRelayEvent.CoreBanked(
            core.CoreId,
            carrier,
            reactor.TeamId,
            reactor.Position,
            reactor.Charge)));
        if (well.PendingCharge)
        {
            well.RearmCompletesAtTick = checked(
                tick + 1 + _gameMode.PendingRearmTicks);
        }
        events.Add(Public(new ArcRelayEvent.WellChanged(
            well.Schedule.WellId,
            well.PendingCharge,
            well.RearmCompletesAtTick,
            null)));
        if (_gameMode.ThreefoldSockets
                ? reactor.FilledSockets.Count < _wells.Length
                : reactor.Charge < _gameMode.CoresPerPulse)
        {
            return;
        }
        reactor.FilledSockets.Clear();
        reactor.Charge = 0;
        reactor.Pulses++;
        ReactorRuntime opposing = _reactors.Values.Single(
            value => value.TeamId != reactor.TeamId);
        opposing.IntegritySegments--;
        _latestPulseTeamId = reactor.TeamId;
        _latestPulseTick = tick;
        events.Add(Public(new ArcRelayEvent.Pulse(
            reactor.TeamId,
            reactor.Pulses,
            opposing.IntegritySegments)));
    }

    private GenericActorRuntimeObservation.ModeObservationState.ArcRelay
        ProjectState() =>
        new(
            _gameMode.ModeId,
            _wells.Select(well => new ArcRelayWellState(
                    well.Schedule.WellId,
                    well.Position,
                    NextScheduledBirth(well.Schedule, _currentTick),
                    well.OutstandingCoreId,
                    well.PendingCharge,
                    well.RearmCompletesAtTick))
                .ToImmutableArray(),
            _reactors.Values.OrderBy(value => value.TeamId)
                .Select(value => new ArcRelayReactorState(
                    value.TeamId,
                    value.Position,
                    value.Charge,
                    value.IntegritySegments)
                {
                    FilledSocketWellIds = _gameMode.ThreefoldSockets
                        ? _wells
                            .Select(well => well.Schedule.WellId)
                            .Where(value.FilledSockets.Contains)
                            .ToImmutableArray()
                        : [],
                })
                .ToImmutableArray(),
            _cores.Values.OrderBy(value => value.CoreId.SourceWellId,
                    StringComparer.Ordinal)
                .ThenBy(value => value.CoreId.SourceOrdinal)
                .Select(value => new ArcRelayCoreState(
                    value.CoreId,
                    value.Position,
                    value.FlightTarget is not null
                        ? ArcRelayCoreState.CoreDisposition.InFlight
                        : value.CarrierActorId is null
                            ? ArcRelayCoreState.CoreDisposition.Loose
                            : ArcRelayCoreState.CoreDisposition.Carried,
                    value.CarrierActorId,
                    value.NextRelocationTick,
                    value.FlightTarget,
                    value.FlightCompletesAtTick))
                .ToImmutableArray(),
            _signatures.Project(_currentTick),
            _latestPulseTeamId,
            _latestPulseTick);

    private Position ArcTossLanding(Position from, Position requested)
    {
        ProjectileHeading heading = ProjectileHeadingExtensions.Between(
            from,
            requested);
        var (dx, dy) = heading.Vector();
        Position current = from;
        int distance = from.ChebyshevDistance(requested);
        for (int step = 0; step < distance; step++)
        {
            Position next = current.Offset(dx, dy);
            if (_definition.Map.IsWall(next))
                break;
            current = next;
        }
        return current;
    }

    private int? NextScheduledBirth(
        ArcRelayWellScheduleDefinition schedule,
        int tick)
    {
        if (_gameMode.WellBirthJitterTicks == 0)
        {
            if (tick >= schedule.FinalBirthTick)
                return null;
            if (tick < schedule.FirstBirthTick)
                return schedule.FirstBirthTick;
            int elapsed = tick - schedule.FirstBirthTick;
            int steps = checked(
                elapsed / schedule.CadenceTicks + 1);
            int next = checked(
                schedule.FirstBirthTick + steps * schedule.CadenceTicks);
            return next <= schedule.FinalBirthTick ? next : null;
        }
        int round = tick < schedule.FirstBirthTick
            ? 0
            : (tick - schedule.FirstBirthTick) / schedule.CadenceTicks;
        while (true)
        {
            int nominal = checked(
                schedule.FirstBirthTick + round * schedule.CadenceTicks);
            if (nominal > schedule.FinalBirthTick)
                return null;
            int actual = nominal + BirthJitter(schedule.WellId, round);
            if (actual > tick)
                return actual;
            round++;
        }
    }

    /// <summary>
    /// Whether this exact tick is a scheduled birth for the well. With
    /// jitter, round k fires at nominal + draw(seed, well, k); the definition
    /// guarantees 2*J &lt; cadence, so every tick maps to at most one
    /// candidate round and windows never overlap.
    /// </summary>
    private bool ScheduledBirthAt(
        ArcRelayWellScheduleDefinition schedule,
        int tick)
    {
        int jitter = _gameMode.WellBirthJitterTicks;
        if (jitter == 0)
        {
            return tick >= schedule.FirstBirthTick
                && tick <= schedule.FinalBirthTick
                && (tick - schedule.FirstBirthTick) % schedule.CadenceTicks
                    == 0;
        }
        if (tick < schedule.FirstBirthTick - jitter)
            return false;
        int round = tick < schedule.FirstBirthTick
            ? 0
            : (tick - schedule.FirstBirthTick + schedule.CadenceTicks / 2)
                / schedule.CadenceTicks;
        int nominal = checked(
            schedule.FirstBirthTick + round * schedule.CadenceTicks);
        return nominal <= schedule.FinalBirthTick
            && tick == nominal + BirthJitter(schedule.WellId, round);
    }

    private int BirthJitter(string wellId, int round)
    {
        int jitter = _gameMode.WellBirthJitterTicks;
        if (jitter == 0)
            return 0;
        ulong draw = SeedDerivation.DeriveWellBirthDraw(
            _matchSeed, wellId, round);
        return (int)(draw % (ulong)(2 * jitter + 1)) - jitter;
    }

    private TeamStandings ResolveStandings(
        GenericActorModeWorldView world,
        GenericActorModeCompletionKind kind)
    {
        HashSet<int> eligible = world.EligibleTeamIds.ToHashSet();
        int[] eligibleOrder = _reactors.Values
            .Where(value => eligible.Contains(value.TeamId))
            .OrderByDescending(value => value.Pulses)
            .ThenByDescending(value => value.Charge)
            .Select(value => value.TeamId)
            .ToArray();
        Dictionary<int, int> ranks = [];
        for (int index = 0; index < eligibleOrder.Length; index++)
        {
            ReactorRuntime current = _reactors[eligibleOrder[index]];
            int rank = index == 0
                ? 1
                : _reactors[eligibleOrder[index - 1]].Pulses == current.Pulses
                    && _reactors[eligibleOrder[index - 1]].Charge
                        == current.Charge
                    ? ranks[eligibleOrder[index - 1]]
                    : index + 1;
            ranks[current.TeamId] = rank;
        }
        int bottomRank = eligibleOrder.Length + 1;
        foreach (int teamId in _reactors.Keys.Where(value => !eligible.Contains(value)))
            ranks[teamId] = bottomRank;
        int topCount = ranks.Count(value => value.Value == 1);
        return new TeamStandings(
            _topology,
            _gameMode,
            _reactors.Values.Select(reactor => new TeamStanding(
                reactor.TeamId,
                ranks[reactor.TeamId],
                ranks[reactor.TeamId] == 1
                    ? topCount == 1
                        ? TeamStandingOutcome.Win
                        : TeamStandingOutcome.Draw
                    : TeamStandingOutcome.Loss,
                _gameMode.ScoreCatalog.Select(channel => new TeamScoreValue(
                    channel.Channel,
                    channel.Channel switch
                    {
                        ScoreChannelDefinition.ChannelKind.Pulses =>
                            reactor.Pulses,
                        ScoreChannelDefinition.ChannelKind.ReactorCharge =>
                            reactor.Charge,
                        _ => throw new InvalidOperationException(
                            "Arc Relay declared an unsupported score channel."),
                    })).ToArray())).ToArray());
    }

    private ImmutableArray<GenericActorRuntimeObservation.ScoreValue> Scores(
        ReactorRuntime reactor) =>
        _gameMode.ScoreCatalog.Select(channel =>
            new GenericActorRuntimeObservation.ScoreValue(
                ActorContractCanonicalIds.Id(channel.Channel),
                channel.Channel switch
                {
                    ScoreChannelDefinition.ChannelKind.Pulses => reactor.Pulses,
                    ScoreChannelDefinition.ChannelKind.ReactorCharge =>
                        reactor.Charge,
                    _ => throw new InvalidOperationException(
                        "Arc Relay declared an unsupported score channel."),
                })).ToImmutableArray();

    private static GenericActorModeEvent Public(ArcRelayEvent fact) =>
        new(
            new GenericActorRuntimeObservation.EventPayload.ArcRelay(fact),
            SpatialPosition: null);

    private static GenericActorModeEvent Spatial(
        ArcRelayEvent fact,
        Position position) =>
        new(
            new GenericActorRuntimeObservation.EventPayload.ArcRelay(fact),
            position);

    private sealed class WellRuntime
    {
        public WellRuntime(
            ArcRelayWellScheduleDefinition schedule,
            Position position)
        {
            Schedule = schedule;
            Position = position;
        }
        public ArcRelayWellScheduleDefinition Schedule { get; }
        public Position Position { get; }
        public int NextSourceOrdinal { get; set; }
        public ArcRelayCoreId? OutstandingCoreId { get; set; }
        public bool PendingCharge { get; set; }
        public int? RearmCompletesAtTick { get; set; }
    }

    private sealed class CoreRuntime
    {
        public CoreRuntime(ArcRelayCoreId coreId, Position position)
        {
            CoreId = coreId;
            Position = position;
        }
        public ArcRelayCoreId CoreId { get; }
        public Position Position { get; set; }
        public ActorIdentity? CarrierActorId { get; set; }
        public int NextRelocationTick { get; set; }
        public Position? FlightTarget { get; set; }
        public int? FlightCompletesAtTick { get; set; }
    }

    private sealed class ReactorRuntime
    {
        public ReactorRuntime(
            int teamId,
            Position position,
            int integritySegments)
        {
            TeamId = teamId;
            Position = position;
            IntegritySegments = integritySegments;
        }
        public int TeamId { get; }
        public Position Position { get; }
        public int Charge { get; set; }
        public int Pulses { get; set; }
        public int IntegritySegments { get; set; }

        /// <summary>Threefold origins banked this cycle, empty otherwise.</summary>
        public HashSet<string> FilledSockets { get; } =
            new(StringComparer.Ordinal);
    }
}
