using System.Collections.Immutable;
using System.Globalization;

namespace BotArena.Engine;

/// <summary>
/// Freezes public-only, runtime-neutral observations for every actor eligible
/// to decide the current Frontline tick. One instance belongs to one match so
/// its audience-local opaque aliases remain stable without exposing global
/// entity counters.
/// </summary>
public sealed class FrontlineObservationProjector
{
    private readonly object _gate = new();
    private readonly Dictionary<AudienceKey, AudienceAliases> _aliases = [];
    private FrontlineMatchState? _matchState;
    private string? _matchContractFingerprint;
    private int _latestTick = -1;

    public ActorObservationFrame Project(
        FrontlineMatchState state,
        FrontlineTickStart tickStart,
        IReadOnlyList<FrontlineMatchEvent> priorResolvedEvents,
        PublicMatchContractManifest contract)
    {
        lock (_gate)
        {
            return ProjectCore(
                state,
                tickStart,
                priorResolvedEvents,
                contract);
        }
    }

    private ActorObservationFrame ProjectCore(
        FrontlineMatchState state,
        FrontlineTickStart tickStart,
        IReadOnlyList<FrontlineMatchEvent> priorResolvedEvents,
        PublicMatchContractManifest contract)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(tickStart);
        ArgumentNullException.ThrowIfNull(priorResolvedEvents);
        ArgumentNullException.ThrowIfNull(contract);

        PublicFrontlineDefinition publicFrontline =
            ValidateInputs(state, tickStart, priorResolvedEvents, contract);
        ValidateProjectorScope(
            state,
            contract.MatchContractFingerprint);
        ImmutableArray<UnitSnapshot> units = SnapshotUnits(state);
        ImmutableArray<ActorSnapshot> actors = units
            .Where(unit => unit.ActiveLife is not null)
            .Select(unit => unit.ActiveLife!)
            .OrderBy(actor => actor.ActorId)
            .ToImmutableArray();
        FrontlineActorId[] projectedActorIds = actors
            .Select(actor => actor.ActorId.ToFrontline())
            .ToArray();
        if (!projectedActorIds.SequenceEqual(tickStart.ActiveActors))
        {
            throw new ArgumentException(
                "Tick-start actors must exactly match the canonical active Frontline lives.",
                nameof(tickStart));
        }

        ImmutableArray<SourcedEvent> sourceEvents = SnapshotEvents(
            priorResolvedEvents,
            tickStart.Events);
        ImmutableArray<ProjectileSnapshot> projectiles = state.Projectiles
            .OrderBy(projectile => projectile.Id)
            .Select(projectile => new ProjectileSnapshot(
                projectile.Id,
                ActorIdentity.FromFrontline(projectile.OwnerActorId),
                projectile.Position,
                projectile.Direction,
                projectile.Heading,
                projectile.Phase,
                projectile.TilesTraveled))
            .ToImmutableArray();
        ObservedFrontlineObjective objective = new(
            state.Control.ActivePositionIndex,
            state.Control.ClaimingTeamId,
            state.Control.CaptureProgress,
            state.Control.DecayTicksElapsed,
            state.Control.ControlResumesAtTick);

        var observations = ImmutableArray.CreateBuilder<ActorObservation>(
            actors.Length);
        var replayAliases =
            ImmutableArray.CreateBuilder<ActorObservationReplayAliases>(
                actors.Length);
        foreach (ActorSnapshot actor in actors)
        {
            PublicFormDefinition form = ResolveForm(
                contract.Rules,
                actor.FormId);
            ImmutableArray<ActorSnapshot> sensorActors =
                publicFrontline.TeamPerception switch
                {
                    TeamPerceptionMode.Individual => [actor],
                    TeamPerceptionMode.ImmediateUnion => actors
                        .Where(candidate =>
                            candidate.ActorId.TeamId == actor.ActorId.TeamId)
                        .ToImmutableArray(),
                    _ => throw new InvalidOperationException(
                        "Unsupported Frontline team-perception mode."),
                };
            ImmutableArray<SensorSnapshot> sensors = sensorActors
                .Select(sensorActor => BuildSensor(
                    state.Definition.Map,
                    sensorActor,
                    ResolveForm(contract.Rules, sensorActor.FormId)))
                .ToImmutableArray();
            AudienceAliases audienceAliases = ResolveAudienceAliases(
                publicFrontline.TeamPerception,
                actor.ActorId);
            var usedAliases = new UsedAliases(audienceAliases);

            ImmutableArray<ObservedUnitSlot> teamUnits = units
                .Where(unit => unit.TeamId == actor.ActorId.TeamId)
                .OrderBy(unit => unit.UnitId)
                .Select(unit => new ObservedUnitSlot(
                    unit.TeamId,
                    unit.UnitId,
                    unit.FormId,
                    unit.LifecycleStatus,
                    unit.ActiveLife?.ActorId,
                    unit.RespawnAtTick,
                    unit.UnlockAtTick,
                    unit.RebuildReadyAtTick,
                    unit.FabricationAtTick))
                .ToImmutableArray();
            ImmutableArray<ObservedAlly> allies = actors
                .Where(candidate =>
                    candidate.ActorId.TeamId == actor.ActorId.TeamId
                    && candidate.ActorId != actor.ActorId)
                .OrderBy(candidate => candidate.ActorId)
                .Select(candidate => new ObservedAlly(
                    candidate.ActorId,
                    candidate.FormId,
                    candidate.Position,
                    candidate.Facing,
                    candidate.Health,
                    candidate.Cooldown,
                    contract.Rules.Energy.Enabled
                        ? candidate.Energy
                        : null,
                    candidate.PreviousActionResult)
                {
                    PendingFormTransition =
                        ObservedTransition(
                            candidate.PendingFormTransition),
                })
                .ToImmutableArray();
            ImmutableArray<ObservedMapTile> visibleTiles = ProjectVisibleTiles(
                state.Definition.Map,
                sensors);
            ImmutableArray<ObservedEnemy> enemies = ProjectEnemies(
                actors,
                actor.ActorId.TeamId,
                sensors,
                usedAliases,
                out HashSet<ActorIdentity> visibleEnemyActorIds);
            ImmutableArray<ObservedActorProjectile>? visibleProjectiles =
                ProjectProjectiles(
                    projectiles,
                    sensors,
                    contract.Rules.Projectiles,
                    actor.ActorId.TeamId,
                    visibleEnemyActorIds,
                    usedAliases);
            (ImmutableArray<ObservedMatchEvent> visibleEvents,
                ImmutableArray<ObservedActorSound>? heardSounds) =
                ProjectEvents(
                    sourceEvents,
                    sensors,
                    contract.Rules.Vision,
                    actor.ActorId.TeamId,
                    usedAliases);

            observations.Add(new ActorObservation
            {
                SchemaVersion = BotArenaVersions.ActorObservationSchemaVersion,
                Tick = state.Tick,
                MatchContractFingerprint =
                    contract.MatchContractFingerprint,
                TeamPerception = publicFrontline.TeamPerception,
                Self = new ObservedSelf(
                    actor.ActorId,
                    actor.FormId,
                    actor.Position,
                    actor.Facing,
                    actor.Health,
                    actor.Cooldown,
                    contract.Rules.Energy.Enabled
                        ? actor.Energy
                        : null,
                    actor.PreviousActionResult)
                {
                    PendingFormTransition =
                        ObservedTransition(actor.PendingFormTransition),
                },
                TeamUnits = teamUnits,
                Allies = allies,
                Enemies = enemies,
                VisibleTiles = visibleTiles,
                VisibleProjectiles = visibleProjectiles,
                VisibleEvents = visibleEvents,
                HeardSounds = heardSounds,
                FrontlineObjective = objective,
                Actions = ProjectActions(
                    contract.Rules,
                    form,
                    actor,
                    units,
                    state.Definition.FrontlineMapProfile!),
            });
            replayAliases.Add(usedAliases.ToReplayAliases(actor.ActorId));
        }

        return new ActorObservationFrame(
            state.Tick,
            observations.MoveToImmutable())
        {
            ReplayAliases = replayAliases.MoveToImmutable(),
        };
    }

    private void ValidateProjectorScope(
        FrontlineMatchState state,
        string matchContractFingerprint)
    {
        if (_matchState is null)
        {
            _matchState = state;
            _matchContractFingerprint = matchContractFingerprint;
        }
        else if (!ReferenceEquals(_matchState, state)
                 || !string.Equals(
                     _matchContractFingerprint,
                     matchContractFingerprint,
                     StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A Frontline observation projector cannot be reused across matches.");
        }

        if (state.Tick < _latestTick)
        {
            throw new InvalidOperationException(
                "Frontline observations cannot be projected backwards in time.");
        }
        _latestTick = state.Tick;
    }

    private AudienceAliases ResolveAudienceAliases(
        TeamPerceptionMode mode,
        ActorIdentity actorId)
    {
        AudienceKey key = mode switch
        {
            TeamPerceptionMode.ImmediateUnion => new AudienceKey(
                mode,
                actorId.TeamId,
                UnitId: null,
                LifeId: null),
            TeamPerceptionMode.Individual => new AudienceKey(
                mode,
                actorId.TeamId,
                actorId.UnitId,
                actorId.LifeId),
            _ => throw new InvalidOperationException(
                "Unsupported Frontline team-perception mode."),
        };
        if (!_aliases.TryGetValue(key, out AudienceAliases? aliases))
        {
            aliases = new AudienceAliases();
            _aliases.Add(key, aliases);
        }
        return aliases;
    }

    private static PublicFrontlineDefinition ValidateInputs(
        FrontlineMatchState state,
        FrontlineTickStart tickStart,
        IReadOnlyList<FrontlineMatchEvent> priorResolvedEvents,
        PublicMatchContractManifest contract)
    {
        if (!state.Definition.IsFrontline)
        {
            throw new ArgumentException(
                "Frontline observations require a Frontline match state.",
                nameof(state));
        }
        ArgumentNullException.ThrowIfNull(tickStart.ActiveActors);
        ArgumentNullException.ThrowIfNull(tickStart.Events);
        ArgumentNullException.ThrowIfNull(contract.Rules);
        ArgumentNullException.ThrowIfNull(contract.Map);
        ArgumentNullException.ThrowIfNull(contract.Topology);

        if (tickStart.Tick != state.Tick)
        {
            throw new ArgumentException(
                "Tick-start data must belong to the state's current tick.",
                nameof(tickStart));
        }
        if (tickStart.ActiveActors
            .Zip(tickStart.ActiveActors.Skip(1))
            .Any(pair => pair.First.CompareTo(pair.Second) >= 0))
        {
            throw new ArgumentException(
                "Tick-start actors must be uniquely and canonically ordered.",
                nameof(tickStart));
        }
        if (state.Tick < 0)
        {
            throw new ArgumentException(
                "Frontline state tick cannot be negative.",
                nameof(state));
        }
        int priorTick = state.Tick - 1;
        if ((state.Tick == 0 && priorResolvedEvents.Count > 0)
            || priorResolvedEvents.Any(matchEvent =>
                matchEvent is null || matchEvent.Tick != priorTick))
        {
            throw new ArgumentException(
                "Prior resolved events must belong exactly to the previous tick.",
                nameof(priorResolvedEvents));
        }
        if (tickStart.Events.Any(matchEvent =>
                matchEvent is null || matchEvent.Tick != state.Tick))
        {
            throw new ArgumentException(
                "Tick-start events must belong to the projected tick.",
                nameof(tickStart));
        }

        PublicFrontlineDefinition publicFrontline =
            contract.Rules.Frontline
            ?? throw new ArgumentException(
                "The public match contract must describe Frontline rules.",
                nameof(contract));
        if (contract.Map.Frontline is null)
        {
            throw new ArgumentException(
                "The public match contract must describe a Frontline map.",
                nameof(contract));
        }
        if (!Enum.IsDefined(publicFrontline.TeamPerception))
        {
            throw new ArgumentException(
                "The public match contract has an unknown team-perception mode.",
                nameof(contract));
        }
        string computedFingerprint =
            MatchContractFingerprint.ComputeMatch(contract);
        if (!string.Equals(
                computedFingerprint,
                contract.MatchContractFingerprint,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The public match-contract fingerprint is invalid.",
                nameof(contract));
        }
        string computedRulesFingerprint =
            MatchContractFingerprint.ComputeRules(
                contract.Rules,
                state.Definition.Rules);
        string computedMapFingerprint =
            MatchContractFingerprint.ComputeMap(contract.Map);
        PublicMatchContractManifest expectedContract =
            PublicRulesManifestFactory.CreateMatchContract(
                state.Definition.Rules,
                state.Definition.Map,
                state.Definition.Topology);
        if (!string.Equals(
                computedRulesFingerprint,
                contract.Rules.RulesFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                computedMapFingerprint,
                contract.Map.MapFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                contract.Rules.RulesFingerprint,
                expectedContract.Rules.RulesFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                contract.Map.MapFingerprint,
                expectedContract.Map.MapFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                contract.MatchContractFingerprint,
                expectedContract.MatchContractFingerprint,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The public match contract does not exactly match the supplied state definition.",
                nameof(contract));
        }
        if (!string.Equals(
                contract.Rules.RulesetId,
                state.Definition.Rules.RulesVersion,
                StringComparison.Ordinal)
            || !string.Equals(
                contract.Map.MapId,
                state.Definition.Map.Id,
                StringComparison.Ordinal)
            || contract.Map.MapVersion != state.Definition.Map.Version
            || contract.Map.Width != state.Definition.Map.Width
            || contract.Map.Height != state.Definition.Map.Height)
        {
            throw new ArgumentException(
                "The public match contract does not describe the supplied state.",
                nameof(contract));
        }

        int[] stateTeamIds = state.Teams
            .Select(team => team.TeamId)
            .Order()
            .ToArray();
        int[] contractTeamIds = contract.Topology.Teams
            .Select(team => team.TeamId)
            .Order()
            .ToArray();
        if (!stateTeamIds.SequenceEqual(contractTeamIds))
        {
            throw new ArgumentException(
                "The public topology teams do not match the supplied state.",
                nameof(contract));
        }

        ValidateFormsAndActions(contract.Rules);
        ValidateVision(contract.Rules.Vision);
        return publicFrontline;
    }

    private static void ValidateFormsAndActions(PublicRulesManifest rules)
    {
        if (rules.Forms.IsDefaultOrEmpty
            || rules.Forms.Any(form => form is null)
            || rules.Forms.Select(form => form.Id)
                .Distinct(StringComparer.Ordinal)
                .Count() != rules.Forms.Length)
        {
            throw new ArgumentException(
                "Public form definitions must be initialized with unique IDs.",
                nameof(rules));
        }
        if (rules.Actions.IsDefault
            || rules.Actions.Any(action => action is null)
            || rules.Actions.Select(action => action.Id)
                .Distinct(StringComparer.Ordinal)
                .Count() != rules.Actions.Length
            || rules.Actions.Select(action => action.Code)
                .Distinct()
                .Count() != rules.Actions.Length)
        {
            throw new ArgumentException(
                "Public action definitions must have unique IDs and codes.",
                nameof(rules));
        }
    }

    private static void ValidateVision(PublicVisionRules vision)
    {
        if (vision.HearingRadius <= 0)
            return;
        if (vision.HearingBearingSectors != 8)
        {
            throw new ArgumentException(
                "The Frontline hearing projector currently requires eight bearing sectors.",
                nameof(vision));
        }
        if (vision.HearingDistanceBandUpperBounds.IsDefault
            || vision.HearingDistanceBandUpperBounds.Any(bound => bound < 0)
            || vision.HearingDistanceBandUpperBounds
                .Zip(vision.HearingDistanceBandUpperBounds.Skip(1))
                .Any(pair => pair.First >= pair.Second))
        {
            throw new ArgumentException(
                "Hearing distance-band bounds must be initialized and increasing.",
                nameof(vision));
        }
        if (vision.LoudEventTypes.IsDefault)
        {
            throw new ArgumentException(
                "Loud hearing event types must be initialized.",
                nameof(vision));
        }
    }

    private static ImmutableArray<UnitSnapshot> SnapshotUnits(
        FrontlineMatchState state)
    {
        var units = ImmutableArray.CreateBuilder<UnitSnapshot>();
        foreach (FrontlineTeamState team in state.Teams
                     .OrderBy(team => team.TeamId))
        {
            foreach (FrontlineUnitState unit in team.Units
                         .OrderBy(unit => unit.UnitId))
            {
                bool shouldHaveLife =
                    unit.LifecycleStatus == FrontlineLifecycleStatus.Active;
                if (shouldHaveLife != (unit.ActiveLife is not null))
                {
                    throw new InvalidOperationException(
                        $"Frontline unit {unit.TeamId}:{unit.UnitId} has inconsistent lifecycle state.");
                }

                ActorSnapshot? activeLife = unit.ActiveLife is null
                    ? null
                    : new ActorSnapshot(
                        ActorIdentity.FromFrontline(unit.ActiveLife.ActorId),
                        unit.ActiveLife.FormId,
                        unit.ActiveLife.Position,
                        unit.ActiveLife.Facing,
                        unit.ActiveLife.Health,
                        unit.ActiveLife.Cooldown,
                        unit.ActiveLife.Energy,
                        unit.ActiveLife.LastActionResult,
                        unit.ActiveLife.PendingFormTransition);
                units.Add(new UnitSnapshot(
                    unit.TeamId,
                    unit.UnitId,
                    unit.FormId,
                    unit.LifecycleStatus,
                    activeLife,
                    unit.RespawnAtTick,
                    unit.UnlockAtTick,
                    unit.RebuildReadyAtTick,
                    unit.FabricationAtTick,
                    unit.ReservedSpawn));
            }
        }
        return units.ToImmutable();
    }

    private static SensorSnapshot BuildSensor(
        ArenaMap map,
        ActorSnapshot actor,
        PublicFormDefinition form)
    {
        Direction? facing = form.OmnidirectionalVision
            ? null
            : actor.Facing;
        HashSet<Position> visibleTiles = Visibility.ComputeVisibleTiles(
                map,
                actor.Position,
                form.VisionRange,
                facing)
            .ToHashSet();
        return new SensorSnapshot(actor, visibleTiles);
    }

    private static PublicFormDefinition ResolveForm(
        PublicRulesManifest rules,
        string formId)
    {
        PublicFormDefinition[] matches = rules.Forms
            .Where(form => string.Equals(
                form.Id,
                formId,
                StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException(
                $"Frontline form '{formId}' is absent or ambiguous in the public contract.");
    }

    private static ImmutableArray<ObservedMapTile> ProjectVisibleTiles(
        ArenaMap map,
        ImmutableArray<SensorSnapshot> sensors)
    {
        var tiles = ImmutableArray.CreateBuilder<ObservedMapTile>();
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var position = new Position(x, y);
                ImmutableArray<ActorIdentity> observedBy =
                    ObserversAt(sensors, position);
                if (!observedBy.IsEmpty)
                {
                    tiles.Add(new ObservedMapTile(
                        position,
                        map.IsWall(position),
                        observedBy));
                }
            }
        }
        return tiles.ToImmutable();
    }

    private static ImmutableArray<ObservedEnemy> ProjectEnemies(
        ImmutableArray<ActorSnapshot> actors,
        int observingTeamId,
        ImmutableArray<SensorSnapshot> sensors,
        UsedAliases usedAliases,
        out HashSet<ActorIdentity> visibleEnemyActorIds)
    {
        visibleEnemyActorIds = [];
        var enemies = ImmutableArray.CreateBuilder<ObservedEnemy>();
        foreach (ActorSnapshot enemy in actors
                     .Where(actor => actor.ActorId.TeamId != observingTeamId)
                     .OrderBy(actor => actor.ActorId))
        {
            ImmutableArray<ActorIdentity> observedBy =
                ObserversAt(sensors, enemy.Position);
            if (observedBy.IsEmpty)
                continue;
            visibleEnemyActorIds.Add(enemy.ActorId);
            enemies.Add(new ObservedEnemy(
                usedAliases.Enemy(enemy.ActorId),
                enemy.FormId,
                enemy.Position,
                enemy.Facing,
                enemy.Health,
                observedBy)
            {
                PendingFormTransition =
                    ObservedTransition(enemy.PendingFormTransition),
            });
        }
        return enemies.ToImmutable();
    }

    private static ImmutableArray<ObservedActorProjectile>? ProjectProjectiles(
        ImmutableArray<ProjectileSnapshot> projectiles,
        ImmutableArray<SensorSnapshot> sensors,
        PublicProjectileRules rules,
        int observingTeamId,
        IReadOnlySet<ActorIdentity> visibleEnemyActorIds,
        UsedAliases usedAliases)
    {
        if (rules.Mode == PublicProjectileMode.InstantRay)
            return null;
        if (rules.Mode != PublicProjectileMode.Discrete
            || rules.TicksPerAdvance <= 0
            || rules.TilesPerAdvance <= 0)
        {
            throw new ArgumentException(
                "Discrete projectile observations require positive cadence.",
                nameof(rules));
        }

        var observed =
            ImmutableArray.CreateBuilder<ObservedActorProjectile>();
        foreach (ProjectileSnapshot projectile in projectiles
                     .OrderBy(projectile => projectile.ProjectileId))
        {
            ImmutableArray<ActorIdentity> observedBy =
                ObserversAt(sensors, projectile.Position);
            if (observedBy.IsEmpty)
                continue;
            int ticksUntilAdvance = rules.TicksPerAdvance - projectile.Phase;
            if (ticksUntilAdvance <= 0
                || ticksUntilAdvance > rules.TicksPerAdvance)
            {
                throw new InvalidOperationException(
                    $"Projectile {projectile.ProjectileId} has an invalid cadence phase.");
            }
            int remainingTiles = rules.MaxTravelTiles > 0
                ? Math.Max(
                    0,
                    rules.MaxTravelTiles - projectile.TilesTraveled)
                : -1;
            bool ownerIdentityVisible =
                projectile.OwnerActorId.TeamId == observingTeamId
                || visibleEnemyActorIds.Contains(projectile.OwnerActorId);
            observed.Add(new ObservedActorProjectile(
                usedAliases.Projectile(projectile.ProjectileId),
                projectile.OwnerActorId.TeamId,
                projectile.OwnerActorId.TeamId == observingTeamId
                    ? projectile.OwnerActorId
                    : null,
                projectile.OwnerActorId.TeamId != observingTeamId
                    && ownerIdentityVisible
                    ? usedAliases.Enemy(projectile.OwnerActorId)
                    : null,
                projectile.Position,
                projectile.Heading
                    ?? projectile.LaunchDirection.ToProjectileHeading(),
                rules.TilesPerAdvance,
                ticksUntilAdvance,
                remainingTiles,
                observedBy));
        }
        return observed.ToImmutable();
    }

    private static (
        ImmutableArray<ObservedMatchEvent> VisibleEvents,
        ImmutableArray<ObservedActorSound>? HeardSounds)
        ProjectEvents(
            ImmutableArray<SourcedEvent> sourceEvents,
            ImmutableArray<SensorSnapshot> sensors,
            PublicVisionRules vision,
            int observingTeamId,
            UsedAliases usedAliases)
    {
        var visibleCandidates =
            ImmutableArray.CreateBuilder<VisibleEventCandidate>();
        ImmutableArray<SoundCandidate>.Builder? heardCandidates =
            vision.HearingRadius > 0
                ? ImmutableArray.CreateBuilder<SoundCandidate>()
                : null;

        foreach (SourcedEvent sourced in sourceEvents
                     .OrderBy(item => item.Event.Tick)
                     .ThenBy(item => item.SourceOrdinal)
                     .ThenBy(item => item.StreamOrdinal))
        {
            FrontlineMatchEvent matchEvent = sourced.Event;
            ObservedMatchEventType observedType = ToObservedType(
                matchEvent.Type);
            Position? primaryPosition = PrimaryPosition(matchEvent);
            if (IsGlobalObjectiveEvent(matchEvent.Type))
            {
                visibleCandidates.Add(new VisibleEventCandidate(
                    sourced,
                    observedType,
                    PrimaryPosition: null,
                    ObservedBy: []));
                continue;
            }
            if (primaryPosition is not Position source)
                continue;

            ImmutableArray<ActorIdentity> observedBy =
                ObserversAt(sensors, source);
            if (!observedBy.IsEmpty)
            {
                visibleCandidates.Add(new VisibleEventCandidate(
                    sourced,
                    observedType,
                    source,
                    observedBy));
                continue;
            }
            if (heardCandidates is null
                || !IsLoud(matchEvent.Type, vision))
            {
                continue;
            }

            foreach (SensorSnapshot sensor in sensors
                         .OrderBy(sensor => sensor.Actor.ActorId))
            {
                int distance =
                    sensor.Actor.Position.ChebyshevDistance(source);
                if (distance > vision.HearingRadius)
                    continue;
                heardCandidates.Add(new SoundCandidate(
                    sourced,
                    sensor.Actor.ActorId,
                    observedType,
                    Hearing.BearingOctant(
                        sensor.Actor.Position,
                        source),
                    DistanceBand(
                        distance,
                        vision.HearingDistanceBandUpperBounds)));
            }
        }

        // Allocate visible-event handles after redaction and before the
        // separate sound stream. Consequently, hidden authoritative events
        // cannot create visible ordinals or gaps.
        ImmutableArray<ObservedMatchEvent> visible = visibleCandidates
            .Select(candidate => BuildObservedEvent(
                candidate.Sourced,
                candidate.Type,
                candidate.PrimaryPosition,
                candidate.ObservedBy,
                observingTeamId,
                usedAliases))
            .ToImmutableArray();
        ImmutableArray<ObservedActorSound>? heard = heardCandidates is null
            ? null
            : heardCandidates
                .Select(candidate => new ObservedActorSound(
                    usedAliases.Event(
                        candidate.Sourced.AuthoritativeEventId),
                    candidate.Sourced.Event.Tick,
                    candidate.ObserverActorId,
                    candidate.Type,
                    candidate.Bearing,
                    candidate.Distance))
                .ToImmutableArray();
        return (
            visible,
            heard);
    }

    private static ObservedMatchEvent BuildObservedEvent(
        SourcedEvent sourced,
        ObservedMatchEventType type,
        Position? primaryPosition,
        ImmutableArray<ActorIdentity> observedBy,
        int observingTeamId,
        UsedAliases usedAliases)
    {
        FrontlineMatchEvent matchEvent = sourced.Event;
        bool spatial = !IsGlobalObjectiveEvent(matchEvent.Type);
        ActorIdentity? exactActor =
            spatial && matchEvent.ActorId is FrontlineActorId actorId
                ? ActorIdentity.FromFrontline(actorId)
                : null;
        return new ObservedMatchEvent(
            usedAliases.Event(sourced.AuthoritativeEventId),
            matchEvent.Tick,
            type,
            matchEvent.TeamId,
            exactActor is not null
                && exactActor.TeamId == observingTeamId
                ? exactActor
                : null,
            exactActor is not null
                && exactActor.TeamId != observingTeamId
                ? usedAliases.Enemy(exactActor)
                : null,
            matchEvent.Type is
                FrontlineMatchEventType.Shot or
                FrontlineMatchEventType.Damage or
                FrontlineMatchEventType.Destroyed
                ? matchEvent.ProjectileId is long projectileId
                    ? usedAliases.Projectile(projectileId)
                    : null
                : null,
            primaryPosition,
            matchEvent.Type is
                FrontlineMatchEventType.Turn or
                FrontlineMatchEventType.Shot or
                FrontlineMatchEventType.Respawned or
                FrontlineMatchEventType.FormTransitionStarted or
                FrontlineMatchEventType.FormChanged or
                FrontlineMatchEventType.FormTransitionCancelled
                ? matchEvent.ToFacing ?? matchEvent.FromFacing
                : null,
            matchEvent.Type == FrontlineMatchEventType.Damage
                ? matchEvent.Amount
                : null,
            matchEvent.Type is
                FrontlineMatchEventType.Damage or
                FrontlineMatchEventType.Destroyed or
                FrontlineMatchEventType.Respawned or
                FrontlineMatchEventType.FormTransitionStarted or
                FrontlineMatchEventType.FormChanged or
                FrontlineMatchEventType.FormTransitionCancelled
                ? matchEvent.NewHealth
                : null,
            observedBy)
        {
            ProjectileHeading = matchEvent.Type
                    == FrontlineMatchEventType.Shot
                ? matchEvent.ProjectileHeading
                : null,
            FromFormId = matchEvent.FromFormId,
            ToFormId = matchEvent.ToFormId,
            FormTransitionStartedAtTick =
                matchEvent.FormTransitionStartedAtTick,
            FormTransitionCompletesAtTick =
                matchEvent.FormTransitionCompletesAtTick,
            ActionId = matchEvent.Type is
                    FrontlineMatchEventType.Shot or
                    FrontlineMatchEventType.FormTransitionStarted or
                    FrontlineMatchEventType.FormChanged or
                    FrontlineMatchEventType.FormTransitionCancelled
                ? matchEvent.ActionId
                : null,
            ActionCode = matchEvent.Type is
                    FrontlineMatchEventType.Shot or
                    FrontlineMatchEventType.FormTransitionStarted or
                    FrontlineMatchEventType.FormChanged or
                    FrontlineMatchEventType.FormTransitionCancelled
                ? matchEvent.ActionCode
                : null,
            FormTargetId = matchEvent.Type is
                    FrontlineMatchEventType.FormTransitionStarted or
                    FrontlineMatchEventType.FormChanged or
                    FrontlineMatchEventType.FormTransitionCancelled
                ? matchEvent.ActionPayload?.FormTargetId
                : null,
            ActionResult = matchEvent.Type is
                    FrontlineMatchEventType.Shot or
                    FrontlineMatchEventType.FormTransitionStarted or
                    FrontlineMatchEventType.FormChanged or
                    FrontlineMatchEventType.FormTransitionCancelled
                ? matchEvent.ActionResult
                : null,
        };
    }

    private static ImmutableArray<ObservedActionAvailability> ProjectActions(
        PublicRulesManifest rules,
        PublicFormDefinition form,
        ActorSnapshot actor,
        ImmutableArray<UnitSnapshot> units,
        FrontlineMapProfile profile)
    {
        HashSet<string> allowedActionIds = form.AllowedActionIds
            .ToHashSet(StringComparer.Ordinal);
        var actions =
            ImmutableArray.CreateBuilder<ObservedActionAvailability>(
                rules.Actions.Length);
        foreach (PublicActionDefinition action in rules.Actions
                     .OrderBy(action => action.Code)
                     .ThenBy(action => action.Id, StringComparer.Ordinal))
        {
            bool available =
                action.Enabled && allowedActionIds.Contains(action.Id);
            if (actor.PendingFormTransition is not null
                && !string.Equals(
                    action.Id,
                    PublicActionIds.Wait,
                    StringComparison.Ordinal))
            {
                available = false;
            }
            if (string.Equals(
                    action.Id,
                    PublicActionIds.Shoot,
                    StringComparison.Ordinal)
                || string.Equals(
                    action.Id,
                    PublicActionIds.ShootDirection,
                    StringComparison.Ordinal))
            {
                available = available
                    && form.CanShoot
                    && actor.Cooldown == 0
                    && (!rules.Energy.Enabled
                        || actor.Energy >= rules.Energy.ShotEnergyCost);
            }

            bool? shotProgramAvailable =
                action.ParameterKinds.Contains(
                    PublicActionParameterKind.ShotProgram)
                    ? available
                        && form.AllowsProgrammedShots
                        && rules.ShotPrograms.Enabled
                    : null;
            FrontlineTeamHome home = profile.TeamHomes.Single(
                value => value.TeamId == actor.ActorId.TeamId);
            PublicFrontlineFabricationDefinition? fabrication =
                rules.Frontline?.Fabrication;
            bool fabricatorOnHomePad =
                fabrication is not null
                && actor.ActorId.UnitId == fabrication.FabricatorUnitId
                && home.ProtectedSpawnPad.Contains(actor.Position);
            ImmutableArray<ObservedUnitTarget>? allowedUnitTargets =
                action.ParameterKinds.Contains(
                    PublicActionParameterKind.UnitTarget)
                    ? units
                        .Where(unit =>
                            fabricatorOnHomePad
                            && unit.TeamId == actor.ActorId.TeamId
                            && unit.UnitId
                                != fabrication!.FabricatorUnitId
                            && unit.LifecycleStatus
                                == FrontlineLifecycleStatus.Ready)
                        .OrderBy(unit => unit.TeamId)
                        .ThenBy(unit => unit.UnitId)
                        .Select(unit => new ObservedUnitTarget(
                            unit.TeamId,
                            unit.UnitId))
                        .ToImmutableArray()
                    : null;
            if (string.Equals(
                    action.Id,
                    PublicActionIds.Fabricate,
                    StringComparison.Ordinal))
            {
                available = available
                    && allowedUnitTargets is { IsEmpty: false };
            }
            PublicFrontlineAnchorDefinition? anchor =
                rules.Frontline?.Anchor;
            ImmutableArray<string>? allowedFormTargets =
                action.ParameterKinds.Contains(
                    PublicActionParameterKind.FormTarget)
                    ? available
                        && anchor is not null
                        && string.Equals(
                            actor.FormId,
                            anchor.SourceFormId,
                            StringComparison.Ordinal)
                        && !profile.AnchorForbiddenTiles.Contains(
                            actor.Position)
                        ? [anchor.TargetFormId]
                        : []
                    : null;
            if (string.Equals(
                    action.Id,
                    PublicActionIds.Transform,
                    StringComparison.Ordinal))
            {
                available = available
                    && allowedFormTargets is { IsEmpty: false };
            }
            ImmutableArray<ProjectileHeading>? allowedProjectileHeadings =
                action.ParameterKinds.Contains(
                    PublicActionParameterKind.ProjectileHeading)
                    ? available
                        ? rules.Frontline!.TurretFire
                            .AllowedProjectileHeadings
                        : []
                    : null;
            actions.Add(new ObservedActionAvailability(
                action.Id,
                action.Code,
                action.ParameterKinds,
                action.Enabled,
                available,
                shotProgramAvailable,
                AllowedDirections: null,
                AllowedUnitTargets: allowedUnitTargets,
                AllowedFormTargets: allowedFormTargets)
            {
                AllowedProjectileHeadings =
                    allowedProjectileHeadings,
            });
        }
        return actions.MoveToImmutable();
    }

    private static ImmutableArray<ActorIdentity> ObserversAt(
        ImmutableArray<SensorSnapshot> sensors,
        Position position) =>
        sensors
            .Where(sensor => sensor.VisibleTiles.Contains(position))
            .Select(sensor => sensor.Actor.ActorId)
            .Order()
            .ToImmutableArray();

    private static ObservedFormTransition? ObservedTransition(
        FrontlinePendingFormTransition? transition) =>
        transition is null
            ? null
            : new ObservedFormTransition(
                transition.FromFormId,
                transition.ToFormId,
                transition.StartedAtTick,
                transition.CompletesAtTick);

    private static ImmutableArray<SourcedEvent> SnapshotEvents(
        IReadOnlyList<FrontlineMatchEvent> priorResolvedEvents,
        IReadOnlyList<FrontlineMatchEvent> tickStartEvents)
    {
        var events = ImmutableArray.CreateBuilder<SourcedEvent>(
            priorResolvedEvents.Count + tickStartEvents.Count);
        int streamOrdinal = 0;
        for (int index = 0; index < priorResolvedEvents.Count; index++)
        {
            events.Add(new SourcedEvent(
                priorResolvedEvents[index],
                index,
                streamOrdinal++,
                ReplayV2Identifiers.ResolutionEventId(
                    priorResolvedEvents[index].Tick,
                    index)));
        }
        for (int index = 0; index < tickStartEvents.Count; index++)
        {
            events.Add(new SourcedEvent(
                tickStartEvents[index],
                index,
                streamOrdinal++,
                ReplayV2Identifiers.LifecycleEventId(
                    tickStartEvents[index].Tick,
                    index)));
        }
        return events.MoveToImmutable();
    }

    private static Position? PrimaryPosition(
        FrontlineMatchEvent matchEvent) =>
        matchEvent.Type switch
        {
            FrontlineMatchEventType.Respawned => matchEvent.To,
            FrontlineMatchEventType.FabricationQueued
                or FrontlineMatchEventType.Fabricated => matchEvent.To,
            FrontlineMatchEventType.FabricationUnlocked
                or FrontlineMatchEventType.RebuildReady => null,
            FrontlineMatchEventType.Turn
                or FrontlineMatchEventType.Move
                or FrontlineMatchEventType.MoveBlocked
                or FrontlineMatchEventType.Shot
                or FrontlineMatchEventType.Damage
                or FrontlineMatchEventType.Destroyed
                or FrontlineMatchEventType.FormTransitionStarted
                or FrontlineMatchEventType.FormChanged
                or FrontlineMatchEventType.FormTransitionCancelled =>
                matchEvent.From,
            FrontlineMatchEventType.FrontlineProgressChanged
                or FrontlineMatchEventType.FrontlinePositionAdvanced
                or FrontlineMatchEventType.BaseBreached => null,
            _ => throw new ArgumentOutOfRangeException(
                nameof(matchEvent),
                $"Unknown Frontline event type {matchEvent.Type}."),
        };

    private static ObservedMatchEventType ToObservedType(
        FrontlineMatchEventType type) =>
        type switch
        {
            FrontlineMatchEventType.Respawned =>
                ObservedMatchEventType.Respawned,
            FrontlineMatchEventType.FabricationUnlocked =>
                ObservedMatchEventType.FabricationUnlocked,
            FrontlineMatchEventType.FabricationQueued =>
                ObservedMatchEventType.FabricationQueued,
            FrontlineMatchEventType.Fabricated =>
                ObservedMatchEventType.Fabricated,
            FrontlineMatchEventType.RebuildReady =>
                ObservedMatchEventType.RebuildReady,
            FrontlineMatchEventType.FormTransitionStarted =>
                ObservedMatchEventType.FormTransitionStarted,
            FrontlineMatchEventType.FormChanged =>
                ObservedMatchEventType.FormChanged,
            FrontlineMatchEventType.FormTransitionCancelled =>
                ObservedMatchEventType.FormTransitionCancelled,
            FrontlineMatchEventType.Turn =>
                ObservedMatchEventType.Turn,
            FrontlineMatchEventType.Move =>
                ObservedMatchEventType.Move,
            FrontlineMatchEventType.MoveBlocked =>
                ObservedMatchEventType.MoveBlocked,
            FrontlineMatchEventType.Shot =>
                ObservedMatchEventType.Shot,
            FrontlineMatchEventType.Damage =>
                ObservedMatchEventType.Damage,
            FrontlineMatchEventType.Destroyed =>
                ObservedMatchEventType.Destroyed,
            FrontlineMatchEventType.FrontlineProgressChanged =>
                ObservedMatchEventType.FrontlineProgressChanged,
            FrontlineMatchEventType.FrontlinePositionAdvanced =>
                ObservedMatchEventType.FrontlinePositionAdvanced,
            FrontlineMatchEventType.BaseBreached =>
                ObservedMatchEventType.BaseBreached,
            _ => throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "Unknown Frontline event type."),
        };

    private static bool IsGlobalObjectiveEvent(
        FrontlineMatchEventType type) =>
        type is
            FrontlineMatchEventType.FrontlineProgressChanged or
            FrontlineMatchEventType.FrontlinePositionAdvanced or
            FrontlineMatchEventType.BaseBreached;

    private static bool IsLoud(
        FrontlineMatchEventType type,
        PublicVisionRules vision)
    {
        GameEventType? legacyType = type switch
        {
            FrontlineMatchEventType.Shot => GameEventType.Shot,
            FrontlineMatchEventType.Damage => GameEventType.Damage,
            FrontlineMatchEventType.Destroyed => GameEventType.Destroyed,
            _ => null,
        };
        return legacyType is GameEventType mapped
            && vision.LoudEventTypes.Contains(mapped);
    }

    private static int DistanceBand(
        int distance,
        ImmutableArray<int> upperBounds)
    {
        for (int index = 0; index < upperBounds.Length; index++)
        {
            if (distance <= upperBounds[index])
                return index;
        }
        return upperBounds.Length;
    }

    private sealed record UnitSnapshot(
        int TeamId,
        int UnitId,
        string FormId,
        FrontlineLifecycleStatus LifecycleStatus,
        ActorSnapshot? ActiveLife,
        int? RespawnAtTick,
        int? UnlockAtTick,
        int? RebuildReadyAtTick,
        int? FabricationAtTick,
        Position? ReservedSpawn);

    private sealed record ActorSnapshot(
        ActorIdentity ActorId,
        string FormId,
        Position Position,
        Direction Facing,
        int Health,
        int Cooldown,
        int Energy,
        ActionResult PreviousActionResult,
        FrontlinePendingFormTransition? PendingFormTransition);

    private sealed record SensorSnapshot(
        ActorSnapshot Actor,
        HashSet<Position> VisibleTiles);

    private sealed record ProjectileSnapshot(
        long ProjectileId,
        ActorIdentity OwnerActorId,
        Position Position,
        Direction LaunchDirection,
        ProjectileHeading? Heading,
        int Phase,
        int TilesTraveled);

    private sealed record SourcedEvent(
        FrontlineMatchEvent Event,
        int SourceOrdinal,
        int StreamOrdinal,
        string AuthoritativeEventId);

    private sealed record VisibleEventCandidate(
        SourcedEvent Sourced,
        ObservedMatchEventType Type,
        Position? PrimaryPosition,
        ImmutableArray<ActorIdentity> ObservedBy);

    private sealed record SoundCandidate(
        SourcedEvent Sourced,
        ActorIdentity ObserverActorId,
        ObservedMatchEventType Type,
        int Bearing,
        int Distance);

    private readonly record struct AudienceKey(
        TeamPerceptionMode Mode,
        int TeamId,
        int? UnitId,
        int? LifeId);

    private sealed class AudienceAliases
    {
        private readonly Dictionary<ActorIdentity, string> _enemyLives = [];
        private readonly Dictionary<long, string> _projectiles = [];
        private readonly Dictionary<string, string> _events =
            new(StringComparer.Ordinal);
        private int _nextEnemyLife;
        private int _nextProjectile;
        private int _nextEvent;

        public string EnemyLife(ActorIdentity actorId) =>
            Resolve(
                _enemyLives,
                actorId,
                "enemy-life",
                ref _nextEnemyLife);

        public string Projectile(long projectileId) =>
            Resolve(
                _projectiles,
                projectileId,
                "projectile",
                ref _nextProjectile);

        public string Event(string eventId) =>
            Resolve(
                _events,
                eventId,
                "event",
                ref _nextEvent);

        private static string Resolve<TKey>(
            IDictionary<TKey, string> aliases,
            TKey authoritativeId,
            string prefix,
            ref int next)
            where TKey : notnull
        {
            if (aliases.TryGetValue(
                    authoritativeId,
                    out string? existing))
            {
                return existing;
            }

            string handle = string.Create(
                CultureInfo.InvariantCulture,
                $"{prefix}-{next}");
            next = checked(next + 1);
            aliases.Add(authoritativeId, handle);
            return handle;
        }
    }

    private sealed class UsedAliases(AudienceAliases audience)
    {
        private readonly Dictionary<string, ActorIdentity> _enemyLives =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _projectiles =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _events =
            new(StringComparer.Ordinal);

        public ObservedEnemyActorRef Enemy(ActorIdentity actorId)
        {
            string handle = audience.EnemyLife(actorId);
            _enemyLives.TryAdd(handle, actorId);
            return new ObservedEnemyActorRef(
                actorId.TeamId,
                actorId.UnitId,
                handle);
        }

        public string Projectile(long projectileId)
        {
            string handle = audience.Projectile(projectileId);
            _projectiles.TryAdd(handle, projectileId);
            return handle;
        }

        public string Event(string eventId)
        {
            string handle = audience.Event(eventId);
            _events.TryAdd(handle, eventId);
            return handle;
        }

        public ActorObservationReplayAliases ToReplayAliases(
            ActorIdentity actorId) =>
            new(
                actorId,
                _enemyLives
                    .OrderBy(pair => HandleOrdinal(
                        pair.Key,
                        "enemy-life"))
                    .Select(pair =>
                        new ActorObservationEnemyLifeAlias(
                            pair.Key,
                            pair.Value))
                    .ToImmutableArray(),
                _projectiles
                    .OrderBy(pair => HandleOrdinal(
                        pair.Key,
                        "projectile"))
                    .Select(pair =>
                        new ActorObservationProjectileAlias(
                            pair.Key,
                            pair.Value))
                    .ToImmutableArray(),
                _events
                    .OrderBy(pair => HandleOrdinal(pair.Key, "event"))
                    .Select(pair =>
                        new ActorObservationEventAlias(
                            pair.Key,
                            pair.Value))
                    .ToImmutableArray());

        private static int HandleOrdinal(string handle, string prefix)
        {
            ReadOnlySpan<char> suffix =
                handle.AsSpan(prefix.Length + 1);
            return int.Parse(
                suffix,
                NumberStyles.None,
                CultureInfo.InvariantCulture);
        }
    }
}
