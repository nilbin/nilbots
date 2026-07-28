using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Mode-neutral schema-3 actor world. It joins catalog-driven mechanics,
/// participant-scoped runtimes, stable-slot lifecycle, Split, and a closed
/// mode driver without routing through either frozen historical session.
/// </summary>
public sealed class GenericActorMatchSession : IDisposable
{
    private readonly ActorResolvedMatchDefinition _definition;
    private readonly GenericActorMatchHost _host;
    private readonly IGenericActorMatchModeDriver _mode;
    private readonly BoundedChildFabricationKernel _fabrication;
    private readonly SplitReplicationKernel _split;
    private readonly ActorSameLifeTransitionKernel _sameLife;
    private readonly Dictionary<string, ActorFormDefinition> _forms;
    private readonly Dictionary<string, ActorVisionProfileDefinition>
        _visionProfiles;
    private readonly Dictionary<string, ActorAttackProfileDefinition>
        _attackProfiles;
    private readonly Dictionary<string, ActorActionDefinition> _actions;
    private readonly Dictionary<string, ActorLifecycleProfileDefinition>
        _lifecycleProfiles;
    private readonly Dictionary<string, InitialSpawnDefinition> _spawns;
    private readonly Dictionary<int, int> _participantTeams;
    private readonly Dictionary<(int TeamId, int UnitId), SlotState> _slots;
    private readonly Dictionary<ActorIdentity, LifeState> _lives = [];
    private readonly List<ProjectileState> _projectiles = [];
    private readonly List<BoundedChildFabricationProvisionalReservation>
        _fabricationReservations = [];
    private readonly List<SplitReplicationReservation> _splitReservations = [];
    private readonly Dictionary<int, int> _nextEventOrdinalByTick = [];
    private readonly Dictionary<ObservationAudienceKey, EventProjectionState>
        _eventProjectionStates = [];
    private ImmutableArray<GenericActorAuthoritativeEvent>
        _priorResolvedEvents;
    private GenericActorMatchPreparedTick? _preparedTick;
    private GenericActorMatchTickStart? _preparedChronologyTick;
    private long _nextAuthoritativeFactOrdinal;
    private long _nextProjectileId;

    /// <summary>
    /// Creates a match-scoped session and takes ownership of participant
    /// runtime factories through the common runtime coordinator.
    /// </summary>
    public GenericActorMatchSession(
        ActorResolvedMatchDefinition definition,
        IEnumerable<GenericActorParticipantConfiguration> participants,
        ulong matchSeed)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(participants);
        IGenericActorMatchModeDriver mode =
            GenericActorMatchModeDriverFactory.Create(definition);
        ValidateWorldCapabilities(definition);

        _definition = definition;
        _forms = definition.Rules.Forms.ToDictionary(
            form => form.Id,
            StringComparer.Ordinal);
        _visionProfiles = definition.Rules.VisionProfiles.ToDictionary(
            profile => profile.Id,
            StringComparer.Ordinal);
        _attackProfiles = definition.Rules.AttackProfiles.ToDictionary(
            profile => profile.Id,
            StringComparer.Ordinal);
        _actions = definition.Rules.Actions.ToDictionary(
            action => action.Id,
            StringComparer.Ordinal);
        _lifecycleProfiles = definition.Rules.Lifecycle.Profiles.ToDictionary(
            profile => profile.ProfileId,
            StringComparer.Ordinal);
        _spawns = definition.Map.SpawnAnchors.ToDictionary(
            anchor => anchor.Spawn.SpawnId,
            anchor => anchor.Spawn,
            StringComparer.Ordinal);
        _participantTeams = definition.Topology.Participants.ToDictionary(
            participant => participant.ParticipantId,
            participant => participant.TeamId);
        _slots = CreateSlots(definition);
        _mode = mode;
        _fabrication = new BoundedChildFabricationKernel(definition);
        _split = new SplitReplicationKernel(definition);
        _sameLife = new ActorSameLifeTransitionKernel(definition);
        _host = new GenericActorMatchHost(
            definition,
            participants,
            matchSeed);

        var initialEvents =
            ImmutableArray.CreateBuilder<GenericActorAuthoritativeEvent>();
        var initialStarts =
            ImmutableArray.CreateBuilder<GenericActorLifeStart>();
        try
        {
            foreach (InitialLifeDeployment deployment in
                     definition.InitialDeployment.Lives
                         .OrderBy(life => life.TeamId)
                         .ThenBy(life => life.UnitId)
                         .ThenBy(life => life.LifeId))
            {
                SlotState slot = _slots[
                    (deployment.TeamId, deployment.UnitId)];
                InitialSpawnDefinition spawn = _spawns[deployment.SpawnId];
                LifeState life = CreateLife(
                    slot,
                    deployment.FormId,
                    slot.Assignment.InitialGeneration!.Value,
                    spawn.Position,
                    spawn.Facing,
                    health: _forms[deployment.FormId].MaxHealth,
                    GenericActorRuntimeStart.SpawnReason.Initial,
                    parentActorId: null,
                    sourceTransitionId: null,
                    sourceOperationId: null,
                    exactLifeId: deployment.LifeId);
                initialStarts.Add(life.LifeStart);
                initialEvents.Add(EmitSpatial(
                    tick: 0,
                    GenericActorRuntimeObservation.EventKind.LifeSpawned,
                    SpawnPayload(life),
                    life.Position));
            }
        }
        catch
        {
            _host.Dispose();
            throw;
        }

        _priorResolvedEvents = initialEvents.ToImmutable();
        _host.RecordInitial(
            new GenericActorMatchInitialFrame(
                SnapshotWorld(),
                initialStarts.ToImmutable(),
                _priorResolvedEvents));
    }

    public ActorResolvedMatchDefinition Definition => _definition;
    public int Tick { get; private set; }
    public bool IsCompleted => Result is not null;
    public GenericActorMatchResult? Result { get; private set; }
    internal GenericActorModeState ModeState
    {
        get
        {
            ThrowIfOperationInProgress();
            return _mode.State;
        }
    }
    public GenericActorMatchDescriptor MatchDescriptor
    {
        get
        {
            ThrowIfOperationInProgress();
            return _host.Descriptor;
        }
    }
    public GenericActorMatchChronology Chronology
    {
        get
        {
            ThrowIfOperationInProgress();
            return _host.Chronology;
        }
    }

    public ImmutableArray<GenericActorWorldSnapshot.LifeSnapshot> ActiveLives
    {
        get
        {
            ThrowIfOperationInProgress();
            ThrowIfDisposed();
            return _lives.Values
                .OrderBy(life => life.ActorId)
                .Select(SnapshotLife)
                .ToImmutableArray();
        }
    }

    public ImmutableArray<GenericActorWorldSnapshot.ProjectileSnapshot>
        Projectiles
    {
        get
        {
            ThrowIfOperationInProgress();
            ThrowIfDisposed();
            return _projectiles
                .OrderBy(projectile => projectile.Id)
                .Select(SnapshotProjectile)
                .ToImmutableArray();
        }
    }

    public ImmutableArray<GenericActorWorldSnapshot.SlotSnapshot> Slots
    {
        get
        {
            ThrowIfOperationInProgress();
            ThrowIfDisposed();
            return _slots.Values
                .OrderBy(slot => slot.TeamId)
                .ThenBy(slot => slot.UnitId)
                .Select(SnapshotSlot)
                .ToImmutableArray();
        }
    }

    /// <summary>
    /// Applies due lifecycle exactly once, freezes public pre-tick
    /// observations, and returns the exact active-life input batch.
    /// </summary>
    public GenericActorMatchPreparedTick PrepareTick()
    {
        using SessionOperation operation =
            EnterOperation(nameof(PrepareTick));
        return PrepareTickCore();
    }

    private GenericActorMatchPreparedTick PrepareTickCore()
    {
        ThrowIfDisposed();
        if (IsCompleted)
            throw new InvalidOperationException("Generic actor match already completed.");
        if (_preparedTick is not null)
            return _preparedTick;

        var tickStartEvents =
            ImmutableArray.CreateBuilder<GenericActorAuthoritativeEvent>();
        var lifeStarts =
            ImmutableArray.CreateBuilder<GenericActorLifeStart>();
        var projectileTransitions =
            ImmutableArray.CreateBuilder<GenericActorProjectileTraversal>();
        ApplyInitialUnlocks(
            tickStartEvents,
            lifeStarts,
            projectileTransitions);
        ApplyAutomaticReturns(
            tickStartEvents,
            lifeStarts,
            projectileTransitions);
        CompleteDueFabrications(
            tickStartEvents,
            lifeStarts,
            projectileTransitions);
        CompleteDueSplits(
            tickStartEvents,
            lifeStarts,
            projectileTransitions);
        CompleteDueSameLifeTransitions(
            ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                .TickStartAfterDuration,
            tickStartEvents);

        ImmutableArray<GenericActorAuthoritativeEvent> sourceEvents =
            [
                .. _priorResolvedEvents,
                .. tickStartEvents,
            ];
        ImmutableArray<GenericActorRuntimeObservation> observations =
            _lives.Values
                .OrderBy(life => life.ActorId)
                .Select(life => ProjectObservation(life, sourceEvents))
                .ToImmutableArray();
        _preparedTick = new GenericActorMatchPreparedTick(
            Tick,
            observations,
            tickStartEvents
                .Select(ToObservedEvent)
                .ToImmutableArray());
        _preparedChronologyTick = new GenericActorMatchTickStart(
            Tick,
            SnapshotWorld(),
            observations
                .Select(observation => observation.Self.ActorId)
                .ToImmutableArray(),
            lifeStarts.ToImmutable(),
            tickStartEvents.ToImmutable(),
            projectileTransitions.ToImmutable());
        return _preparedTick;
    }

    /// <summary>Resolves the prepared observation batch in canonical order.</summary>
    public GenericActorMatchStepResult Step()
    {
        using SessionOperation operation = EnterOperation(nameof(Step));
        return StepCore(PrepareTickCore().Observations);
    }

    /// <summary>
    /// Resolves the prepared batch after accepting an arbitrary enumeration
    /// order of the exact frozen observation objects. This seam makes
    /// collection-order determinism directly testable without allowing callers
    /// to author world state.
    /// </summary>
    public GenericActorMatchStepResult Step(
        IEnumerable<GenericActorRuntimeObservation> observations)
    {
        using SessionOperation operation = EnterOperation(nameof(Step));
        return StepCore(observations);
    }

    private GenericActorMatchStepResult StepCore(
        IEnumerable<GenericActorRuntimeObservation> observations)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(observations);
        if (IsCompleted)
            throw new InvalidOperationException(
                "Generic actor match already completed.");
        GenericActorMatchPreparedTick tickStart = _preparedTick
            ?? throw new InvalidOperationException(
                "PrepareTick must be called before Step.");
        GenericActorRuntimeObservation[] supplied = [.. observations];
        ValidateFrozenObservationBatch(tickStart, supplied);

        GenericActorRuntimeTickResult runtimeTick =
            _host.CollectTickDecisions(Tick, supplied);
        var resolutions = CreateActionResolutions(runtimeTick);
        var events =
            ImmutableArray.CreateBuilder<GenericActorAuthoritativeEvent>();
        var projectileTransitions =
            ImmutableArray.CreateBuilder<GenericActorProjectileTraversal>();
        var contacts = new List<PendingDamageContact>();
        int contactOrdinal = 0;

        ResolveRotations(resolutions, events);
        ResolveMovement(
            resolutions,
            contacts,
            ref contactOrdinal,
            events,
            projectileTransitions);
        ReserveLifecycleCreations(resolutions, events);
        StartSameLifeTransitions(resolutions, events);
        AdvanceExistingProjectiles(
            contacts,
            ref contactOrdinal,
            projectileTransitions);
        ResolveAttacks(
            resolutions,
            contacts,
            ref contactOrdinal,
            events,
            projectileTransitions);
        ImmutableArray<GenericActorModeDamageContact> scoredContacts =
            ApplyDamage(contacts, events);

        foreach (GenericActorRuntimeFault fault in runtimeTick.Faults)
        {
            events.Add(EmitTeamPrivate(
                Tick,
                GenericActorRuntimeObservation.EventKind.RuntimeFault,
                new GenericActorRuntimeObservation.EventPayload
                    .RuntimeFault(fault),
                fault.ActorId.TeamId));
        }

        HashSet<int> newlyDisqualified =
            runtimeTick.NewlyDisqualifiedParticipantIds.ToHashSet();
        ApplyDisqualifications(
            runtimeTick.NewlyDisqualifiedParticipantIds,
            events,
            projectileTransitions);
        FinalizeDestroyedLives(newlyDisqualified, events);
        RememberActionResolutions(resolutions);

        ImmutableArray<int> eligibleTeams = EligibleTeamIds();
        GenericActorModeCompletion? terminal = null;
        bool faultEligibilityCompletion = eligibleTeams.Length <= 1;
        bool modeObjectiveCompletion = false;
        if (!faultEligibilityCompletion)
        {
            // Resource clocks belong to action resolution, and therefore
            // settle before the later mode update (including a kill-limit
            // completion on this same tick).
            UpdateCooldownsAndEnergy(resolutions);
            GenericActorModeTickResult modeTick = _mode.ApplyJointTick(
                ModeWorldView(),
                new GenericActorModeTickInput(Tick, scoredContacts));
            EmitModeChanges(modeTick, events);
            modeObjectiveCompletion = modeTick.ModeObjectiveReached;
            CompleteDueSameLifeTransitions(
                ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                    .EndOfStartedTickPlusDurationMinusOneAfterModeUpdate,
                events);
        }
        if (faultEligibilityCompletion)
        {
            // Fault eligibility is an earlier terminal phase and therefore
            // skips resources, mode update, and end-clock same-life work.
            terminal = Complete(
                GenericActorModeCompletionKind.FaultEligibility);
        }
        else if (modeObjectiveCompletion)
        {
            terminal = Complete(
                GenericActorModeCompletionKind.ModeObjective);
        }

        int executedTick = Tick;
        int nextTick = checked(Tick + 1);
        if (terminal is null
            && nextTick >= _definition.Rules.Limits.MaxTicks)
        {
            terminal = Complete(
                GenericActorModeCompletionKind.MaxTicks);
        }
        Tick = nextTick;

        ImmutableArray<GenericActorMatchActorResolution>
            publicResolutions = resolutions.Values
                .OrderBy(resolution => resolution.ActorId)
                .Select(resolution =>
                    new GenericActorMatchActorResolution(
                        resolution.ParticipantId,
                        resolution.ActorId,
                        resolution.ToPublic()))
                .ToImmutableArray();
        ImmutableArray<GenericActorAuthoritativeEvent> authoritativeEvents =
            events.ToImmutable();
        ImmutableArray<GenericActorRuntimeObservation.ObservedEvent>
            resolvedEvents = authoritativeEvents
                .Select(ToObservedEvent)
                .ToImmutableArray();

        GenericActorMatchTickStart chronologyTick =
            _preparedChronologyTick
            ?? throw new InvalidOperationException(
                "The prepared tick has no authoritative chronology.");
        Dictionary<ActorIdentity, GenericActorRuntimeObservation>
            observationsByActor = tickStart.Observations.ToDictionary(
                observation => observation.Self.ActorId);
        ImmutableArray<GenericActorMatchActorTurn> actorTurns =
            runtimeTick.Turns
                .OrderBy(turn => turn.ActorId)
                .Select(turn =>
                    new GenericActorMatchActorTurn(
                        executedTick,
                        turn.ParticipantId,
                        turn.ActorId,
                        observationsByActor[turn.ActorId],
                        turn.SubmittedDecision,
                        resolutions[turn.ActorId].ToPublic()))
                .ToImmutableArray();
        GenericActorWorldSnapshot postState = SnapshotWorld();
        _host.RecordResolvedTick(
            new GenericActorMatchTickFrame(
                chronologyTick,
                actorTurns,
                authoritativeEvents,
                projectileTransitions.ToImmutable(),
                postState));
        GenericActorMatchResult? terminalResult = null;
        if (terminal is not null)
        {
            terminalResult = ToGenericResult(
                terminal,
                eligibleTeams,
                postState);
            Result = terminalResult;
            _host.RecordCompleted(terminalResult);
        }

        _priorResolvedEvents = authoritativeEvents;
        _preparedTick = null;
        _preparedChronologyTick = null;
        return new GenericActorMatchStepResult(
            executedTick,
            tickStart,
            runtimeTick,
            publicResolutions,
            resolvedEvents,
            postState,
            IsCompleted,
            terminalResult);
    }

    /// <summary>Runs until one terminal rule completes and returns its result.</summary>
    public GenericActorMatchResult Run()
    {
        using SessionOperation operation = EnterOperation(nameof(Run));
        ThrowIfDisposed();
        while (!IsCompleted)
        {
            PrepareTickCore();
            StepCore(_preparedTick!.Observations);
        }
        return Result!;
    }

    public void Dispose()
    {
        using SessionOperation operation = EnterOperation(nameof(Dispose));
        if (_host.IsDisposed)
            return;
        try
        {
            _host.DisposeWithinOperation();
        }
        finally
        {
            _lives.Clear();
            _projectiles.Clear();
            _fabricationReservations.Clear();
            _splitReservations.Clear();
            _eventProjectionStates.Clear();
            _preparedTick = null;
            _preparedChronologyTick = null;
        }
    }

    private void ApplyInitialUnlocks(
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events,
        ImmutableArray<GenericActorLifeStart>.Builder lifeStarts,
        ImmutableArray<GenericActorProjectileTraversal>.Builder traversals)
    {
        foreach (SlotState slot in _slots.Values
                     .OrderBy(slot => slot.TeamId)
                     .ThenBy(slot => slot.UnitId))
        {
            if (slot.Kind != SlotKind.AvailabilityPending
                || slot.PendingReason
                    != GenericActorRuntimeObservation.AvailabilityReason
                        .InitialUnlock
                || slot.DueTick != Tick)
            {
                continue;
            }
            if (slot.Assignment.InitialAvailability
                == ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind
                    .DormantAutomaticActivationAtTick)
            {
                ActorLifecycleProfileDefinition profile =
                    _lifecycleProfiles[slot.Assignment.LifecycleProfileId];
                string formId = profile.AutomaticReturnFormId
                    ?? throw new InvalidOperationException(
                        "Automatic activation has no target form.");
                InitialSpawnDefinition spawn = _spawns[
                    slot.Assignment.AssignedRespawnSpawnId!];
                ConsumeProjectilesAt(spawn.Position, traversals);
                LifeState life = CreateLife(
                    slot,
                    formId,
                    slot.Assignment.InitialGeneration!.Value,
                    spawn.Position,
                    spawn.Facing,
                    _forms[formId].MaxHealth,
                    GenericActorRuntimeStart.SpawnReason
                        .AutomaticActivation,
                    parentActorId: null,
                    sourceTransitionId: null,
                    sourceOperationId: null);
                lifeStarts.Add(life.LifeStart);
                ClearPendingClock(slot);
                events.Add(EmitSpatial(
                    Tick,
                    GenericActorRuntimeObservation.EventKind.LifeSpawned,
                    SpawnPayload(life),
                    life.Position));
                continue;
            }
            slot.Kind = SlotKind.Ready;
            slot.DueTick = null;
            slot.PendingReason = null;
        }
    }

    private void ApplyAutomaticReturns(
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events,
        ImmutableArray<GenericActorLifeStart>.Builder lifeStarts,
        ImmutableArray<GenericActorProjectileTraversal>.Builder traversals)
    {
        foreach (SlotState slot in _slots.Values
                     .OrderBy(slot => slot.TeamId)
                     .ThenBy(slot => slot.UnitId))
        {
            if (slot.DueTick != Tick)
                continue;
            if (slot.Kind == SlotKind.AutomaticReturnPending)
            {
                string formId = slot.PendingFormId
                    ?? throw new InvalidOperationException(
                        "Automatic return has no target form.");
                InitialSpawnDefinition spawn = _spawns[
                    slot.Assignment.AssignedRespawnSpawnId!];
                ConsumeProjectilesAt(
                    spawn.Position,
                    traversals);
                LifeState life = CreateLife(
                    slot,
                    formId,
                    slot.PendingGeneration!.Value,
                    spawn.Position,
                    spawn.Facing,
                    _forms[formId].MaxHealth,
                    GenericActorRuntimeStart.SpawnReason.AutomaticReturn,
                    slot.PendingParentActorId,
                    sourceTransitionId: null,
                    sourceOperationId: null);
                lifeStarts.Add(life.LifeStart);
                ClearPendingClock(slot);
                events.Add(EmitSpatial(
                    Tick,
                    GenericActorRuntimeObservation.EventKind.LifeSpawned,
                    SpawnPayload(life),
                    life.Position));
            }
            else if (slot.Kind == SlotKind.AvailabilityPending
                     && slot.PendingReason
                         == GenericActorRuntimeObservation.AvailabilityReason
                             .DestructionRecovery)
            {
                slot.Kind = SlotKind.Ready;
                ClearPendingClock(slot);
            }
        }
    }

    private void CompleteDueFabrications(
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events,
        ImmutableArray<GenericActorLifeStart>.Builder lifeStarts,
        ImmutableArray<GenericActorProjectileTraversal>.Builder traversals)
    {
        BoundedChildFabricationProvisionalReservation[] due =
            _fabricationReservations
                .Where(reservation => reservation.DueTick == Tick)
                .OrderBy(reservation => reservation.SourceActorId)
                .ThenBy(
                    reservation => reservation.TransitionId,
                    StringComparer.Ordinal)
                .ThenBy(reservation => reservation.TargetTeamId)
                .ThenBy(reservation => reservation.TargetUnitId)
                .ThenBy(
                    reservation => reservation.OperationId,
                    StringComparer.Ordinal)
                .ToArray();
        foreach (BoundedChildFabricationProvisionalReservation reservation
                 in due)
        {
            _fabrication.ValidateReservationEvidence(reservation);
            _fabricationReservations.Remove(reservation);
            ReleaseFabricationTarget(reservation);
            ConsumeProjectilesAt(
                reservation.ReservedPosition,
                traversals);

            SlotState slot = _slots[
                (reservation.TargetTeamId, reservation.TargetUnitId)];
            LifeState child = CreateLife(
                slot,
                reservation.TargetFormId,
                reservation.TargetGeneration,
                reservation.ReservedPosition,
                reservation.OutputFacing,
                _forms[reservation.TargetFormId].MaxHealth,
                GenericActorRuntimeStart.SpawnReason.Fabrication,
                reservation.SourceActorId,
                reservation.TransitionId,
                reservation.OperationId);
            lifeStarts.Add(child.LifeStart);
            events.Add(EmitSpatial(
                Tick,
                GenericActorRuntimeObservation.EventKind.LifeSpawned,
                SpawnPayload(child),
                child.Position));
            events.Add(EmitSpatial(
                Tick,
                GenericActorRuntimeObservation.EventKind.LifecycleCompleted,
                LifecyclePayload(
                    reservation,
                    cancellationReason: null),
                reservation.SourcePosition));
        }
    }

    private void CompleteDueSplits(
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events,
        ImmutableArray<GenericActorLifeStart>.Builder lifeStarts,
        ImmutableArray<GenericActorProjectileTraversal>.Builder traversals)
    {
        SplitReplicationReservation[] due = _splitReservations
            .Where(reservation => reservation.DueTick == Tick)
            .OrderBy(reservation => reservation.SourceActorId)
            .ThenBy(
                reservation => reservation.TransitionId,
                StringComparer.Ordinal)
            .ThenBy(
                reservation => reservation.OperationId,
                StringComparer.Ordinal)
            .ToArray();
        foreach (SplitReplicationReservation reservation in due)
        {
            _lives.TryGetValue(
                reservation.SourceActorId,
                out LifeState? source);
            SplitReplicationCompletion completion = _split.Complete(
                Tick,
                reservation,
                source is null ? null : SplitSnapshot(source));
            _splitReservations.Remove(reservation);
            ReleaseSplitTargets(reservation);

            if (completion.Outcome
                == SplitReplicationCompletion
                    .SplitCompletionOutcomeKind.Cancelled)
            {
                events.Add(EmitSpatial(
                    Tick,
                    GenericActorRuntimeObservation.EventKind
                        .LifecycleCancelled,
                    LifecyclePayload(
                        reservation,
                        SplitCancellationId(
                            completion.Reason!.Value)),
                    reservation.SourcePosition));
                continue;
            }

            if (source is null)
            {
                throw new InvalidOperationException(
                    "A completed Split has no source life.");
            }
            foreach (SplitReplicationSpawn spawn in completion.Descendants
                         .OrderBy(item => item.TeamId)
                         .ThenBy(item => item.UnitId))
            {
                // A lifecycle placement wins the tick-start tile. Purge every
                // projectile already occupying any output tile before the
                // first descendant is created, so descendant enumeration
                // order cannot decide which output survives.
                ConsumeProjectilesAt(
                    spawn.Position,
                    traversals);
            }
            _host.RetireLife(source.ActorId);
            _lives.Remove(source.ActorId);
            _slots[(source.ActorId.TeamId, source.ActorId.UnitId)]
                .ActiveLife = null;
            events.Add(EmitSpatial(
                Tick,
                GenericActorRuntimeObservation.EventKind.LifeRetired,
                new GenericActorRuntimeObservation.EventPayload.LifeRetired(
                    source.ActorId,
                    source.Generation,
                    source.FormId,
                    source.Position,
                    "replication",
                    reservation.TransitionId,
                    reservation.OperationId),
                source.Position));

            foreach (SplitReplicationSpawn spawn in completion.Descendants
                         .OrderBy(item => item.TeamId)
                         .ThenBy(item => item.UnitId))
            {
                SlotState slot = _slots[(spawn.TeamId, spawn.UnitId)];
                LifeState descendant = CreateLife(
                    slot,
                    spawn.FormId,
                    spawn.Generation,
                    spawn.Position,
                    reservation.SourceFacing,
                    spawn.Health,
                    GenericActorRuntimeStart.SpawnReason.Replication,
                    source.ActorId,
                    reservation.TransitionId,
                    reservation.OperationId);
                lifeStarts.Add(descendant.LifeStart);
                events.Add(EmitSpatial(
                    Tick,
                    GenericActorRuntimeObservation.EventKind.LifeSpawned,
                    SpawnPayload(descendant),
                    descendant.Position));
            }
            events.Add(EmitSpatial(
                Tick,
                GenericActorRuntimeObservation.EventKind.LifecycleCompleted,
                LifecyclePayload(reservation, cancellationReason: null),
                reservation.SourcePosition));
        }
    }

    private Dictionary<ActorIdentity, ActionState> CreateActionResolutions(
        GenericActorRuntimeTickResult runtimeTick)
    {
        var result = new Dictionary<ActorIdentity, ActionState>();
        foreach (GenericActorRuntimeTurn turn in runtimeTick.Turns)
        {
            LifeState life = _lives[turn.ActorId];
            GenericActorRuntimeActionResolution.ResolvedAction accepted =
                ToResolved(turn.AcceptedDecision);
            _host.TryProjectSubmittedAction(
                turn.SubmittedDecision,
                out GenericActorRuntimeActionResolution.ResolvedAction?
                    submitted);
            var state = new ActionState(
                turn.ParticipantId,
                turn.ActorId,
                submitted,
                accepted,
                accepted,
                turn.AdmissionOutcome,
                turn.RuntimeFault);
            if (turn.RuntimeFault is not null)
            {
                result.Add(turn.ActorId, state);
                continue;
            }

            ActorActionDefinition action = _actions[accepted.ActionId];
            ActorFormDefinition form = _forms[life.FormId];
            if (!form.AllowedActionIds.Contains(
                    action.Id,
                    StringComparer.Ordinal))
            {
                state.Outcome =
                    GenericActorRuntimeActionResolution.ActionOutcome.Rejected;
            }
            else if (_splitReservations.Any(reservation =>
                         reservation.SourceActorId == life.ActorId)
                     && action.Kind != ActorActionKind.Wait)
            {
                state.Outcome =
                    GenericActorRuntimeActionResolution.ActionOutcome.Blocked;
            }
            else if (life.PendingSameLifeTransition is not null
                     && action.Kind != ActorActionKind.Wait)
            {
                state.Outcome =
                    GenericActorRuntimeActionResolution.ActionOutcome.Blocked;
            }
            else
            {
                ValidateGameplayAvailability(life, action, state);
            }
            result.Add(turn.ActorId, state);
        }
        return result;
    }

    private void ValidateGameplayAvailability(
        LifeState life,
        ActorActionDefinition action,
        ActionState state)
    {
        switch (action.Kind)
        {
            case ActorActionKind.Wait:
            case ActorActionKind.Movement:
            case ActorActionKind.Rotation:
                return;
            case ActorActionKind.Attack:
                ActorAttackProfileDefinition? attack = AttackFor(life);
                if (attack is null
                    || life.Cooldown > 0
                    || attack.MaxEnergy > 0
                    && life.Energy < attack.AttackEnergyCost)
                {
                    Block(state);
                }
                return;
            case ActorActionKind.Replication:
                SplitReplicationTransitionDefinition[] matches =
                    MatchingSplitTransitions(life, action.Id);
                if (matches.Length != 1)
                    Block(state);
                return;
            case ActorActionKind.SameLifeTransition:
                ActorFormTransitionDefinition[] sameLifeMatches =
                    MatchingSameLifeTransitions(
                        life,
                        state.ValidatedAction)
                    .ToArray();
                if (sameLifeMatches.Length != 1
                    || !_sameLife.CanQueue(
                        SameLifeSnapshot(life),
                        sameLifeMatches[0].TransitionId))
                {
                    Block(state);
                }
                return;
            case ActorActionKind.Fabrication:
                BoundedChildFabricationDefinition[] fabricationMatches =
                    MatchingFabricationTransitions(life, action.Id);
                if (fabricationMatches.Length != 1)
                    Block(state);
                return;
            default:
                throw new InvalidOperationException(
                    $"Action kind '{action.Kind}' has no generic resolver.");
        }
    }

    private static void Block(ActionState state)
    {
        state.Outcome =
            GenericActorRuntimeActionResolution.ActionOutcome.Blocked;
    }

    private void ResolveRotations(
        IReadOnlyDictionary<ActorIdentity, ActionState> resolutions,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        foreach (ActionState resolution in resolutions.Values
                     .OrderBy(value => value.ActorId))
        {
            ActorActionDefinition action =
                _actions[resolution.ValidatedAction.ActionId];
            if (resolution.Outcome
                    != GenericActorRuntimeActionResolution.ActionOutcome.Success
                || action.Kind != ActorActionKind.Rotation)
            {
                continue;
            }

            LifeState life = _lives[resolution.ActorId];
            Direction to = resolution.ValidatedAction.Arguments
                .OfType<
                    GenericActorRuntimeActionArgument.DirectionArgument>()
                .Single()
                .Value;
            Direction from = life.Facing;
            life.Facing = to;
            events.Add(EmitSpatial(
                Tick,
                GenericActorRuntimeObservation.EventKind.Rotation,
                new GenericActorRuntimeObservation.EventPayload.Rotation(
                    life.ActorId,
                    resolution.ValidatedAction,
                    life.Position,
                    from,
                    to),
                life.Position));
        }
    }

    private void ResolveMovement(
        IReadOnlyDictionary<ActorIdentity, ActionState> resolutions,
        ICollection<PendingDamageContact> contacts,
        ref int contactOrdinal,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events,
        ImmutableArray<GenericActorProjectileTraversal>.Builder traversals)
    {
        var targets = new Dictionary<ActorIdentity, Position>();
        var blocked = new HashSet<ActorIdentity>();
        Dictionary<Position, LifeState> occupants = _lives.Values.ToDictionary(
            life => life.Position);
        foreach (ActionState resolution in resolutions.Values
                     .OrderBy(value => value.ActorId))
        {
            ActorActionDefinition action =
                _actions[resolution.ValidatedAction.ActionId];
            if (resolution.Outcome
                    != GenericActorRuntimeActionResolution.ActionOutcome.Success
                || action.Kind != ActorActionKind.Movement)
            {
                continue;
            }

            LifeState life = _lives[resolution.ActorId];
            Direction direction = resolution.ValidatedAction.Arguments
                .OfType<
                    GenericActorRuntimeActionArgument.DirectionArgument>()
                .Single()
                .Value;
            var (dx, dy) = direction.Vector();
            Position target = life.Position.Offset(dx, dy);
            targets.Add(life.ActorId, target);
            if (_definition.Map.IsWall(target)
                || IsForeignReservedReturnTile(life, target)
                || IsReservedLifecycleTile(target)
                || occupants.ContainsKey(target))
            {
                blocked.Add(life.ActorId);
            }

            foreach (ProjectileState projectile in _projectiles
                         .Where(projectile =>
                             projectile.Position == target)
                         .OrderBy(projectile => projectile.Id)
                         .ToArray())
            {
                ProjectileContact contact = Contact(
                    projectile,
                    life);
                if (!contact.Consumes)
                    continue;
                blocked.Add(life.ActorId);
                _projectiles.Remove(projectile);
                traversals.Add(CreateProjectileTraversal(
                    projectile,
                    GenericActorProjectileTraversal.TraversalPhase.Resolution,
                    GenericActorProjectileTraversal.TraversalTrigger
                        .MovementContact,
                    projectile.Position,
                    [],
                    new GenericActorProjectileTraversal.TerminalDisposition
                        .MovementContact(
                            life.ActorId,
                            contact.Damages)));
                if (contact.Damages)
                {
                    contacts.Add(new PendingDamageContact(
                        life.ActorId,
                        projectile.OwnerTeamId,
                        projectile.OwnerActorId,
                        projectile.Id,
                        projectile.Profile.Projectile.DamagePerHit,
                        contactOrdinal++));
                }
            }
        }

        foreach (IGrouping<Position, KeyValuePair<ActorIdentity, Position>>
                 claims in targets.GroupBy(pair => pair.Value))
        {
            if (claims.Count() <= 1)
                continue;
            foreach (KeyValuePair<ActorIdentity, Position> claim in claims)
                blocked.Add(claim.Key);
        }

        foreach ((ActorIdentity actorId, Position target) in targets
                     .OrderBy(pair => pair.Key))
        {
            LifeState life = _lives[actorId];
            Position from = life.Position;
            ActionState resolution = resolutions[actorId];
            if (blocked.Contains(actorId))
            {
                resolution.Outcome =
                    GenericActorRuntimeActionResolution.ActionOutcome.Blocked;
                events.Add(EmitSpatial(
                    Tick,
                    GenericActorRuntimeObservation.EventKind.MovementBlocked,
                    new GenericActorRuntimeObservation.EventPayload
                        .MovementBlocked(
                            actorId,
                            resolution.ValidatedAction,
                            from,
                            target,
                            life.Facing),
                    // A blocked mover never leaves its source tile.
                    from));
                continue;
            }

            life.Position = target;
            events.Add(EmitSpatial(
                Tick,
                GenericActorRuntimeObservation.EventKind.Movement,
                new GenericActorRuntimeObservation.EventPayload.Movement(
                    actorId,
                    resolution.ValidatedAction,
                    from,
                    target,
                    life.Facing),
                // Movement is sight-gated at the destination: an entry into
                // sight is observable, while a completed departure into
                // hidden space reveals no hidden destination.
                target));
        }
    }

    private void ReserveLifecycleCreations(
        IReadOnlyDictionary<ActorIdentity, ActionState> resolutions,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        BoundedChildFabricationRequest[] fabricationRequests =
            resolutions.Values
                .Where(resolution =>
                    resolution.Outcome
                        == GenericActorRuntimeActionResolution
                            .ActionOutcome.Success
                    && _actions[resolution.ValidatedAction.ActionId].Kind
                        == ActorActionKind.Fabrication)
                .OrderBy(resolution => resolution.ActorId)
                .Select(resolution =>
                {
                    LifeState life = _lives[resolution.ActorId];
                    BoundedChildFabricationDefinition transition =
                        MatchingFabricationTransitions(
                            life,
                            resolution.ValidatedAction.ActionId)
                        .Single();
                    GenericActorRuntimeActionArgument.UnitTarget target =
                        resolution.ValidatedAction.Arguments
                            .OfType<GenericActorRuntimeActionArgument
                                .UnitTargetArgument>()
                            .Single()
                            .Value;
                    return new BoundedChildFabricationRequest(
                        life.ActorId,
                        transition.TransitionId,
                        $"fabrication:{Tick}:{life.ActorId.TeamId}:" +
                        $"{life.ActorId.UnitId}:{life.ActorId.LifeId}:" +
                        $"{transition.TransitionId}:{target.TeamId}:" +
                        $"{target.UnitId}",
                        target.TeamId,
                        target.UnitId);
                })
                .ToArray();
        SplitReplicationRequest[] splitRequests = resolutions.Values
            .Where(resolution =>
                resolution.Outcome
                    == GenericActorRuntimeActionResolution.ActionOutcome.Success
                && _actions[resolution.ValidatedAction.ActionId].Kind
                    == ActorActionKind.Replication)
            .OrderBy(resolution => resolution.ActorId)
            .Select(resolution =>
            {
                LifeState life = _lives[resolution.ActorId];
                SplitReplicationTransitionDefinition transition =
                    MatchingSplitTransitions(
                        life,
                        resolution.ValidatedAction.ActionId)
                    .Single();
                return new SplitReplicationRequest(
                    life.ActorId,
                    transition.TransitionId,
                    $"split:{Tick}:{life.ActorId.TeamId}:" +
                    $"{life.ActorId.UnitId}:{life.ActorId.LifeId}");
            })
            .ToArray();
        if (fabricationRequests.Length == 0
            && splitRequests.Length == 0)
        {
            return;
        }

        BoundedChildFabricationActorSnapshot[] fabricationActors =
            _lives.Values
                .OrderBy(life => life.ActorId)
                .Select(FabricationSnapshot)
                .ToArray();
        BoundedChildFabricationSlotSnapshot[] fabricationSlots =
            _slots.Values
                .OrderBy(slot => slot.TeamId)
                .ThenBy(slot => slot.UnitId)
                .Select(FabricationSlotSnapshot)
                .ToArray();
        SplitReplicationActorSnapshot[] splitActors = _lives.Values
            .OrderBy(life => life.ActorId)
            .Select(SplitSnapshot)
            .ToArray();
        SplitReplicationSlotSnapshot[] splitSlots = _slots.Values
            .OrderBy(slot => slot.TeamId)
            .ThenBy(slot => slot.UnitId)
            .Select(SplitSlotSnapshot)
            .ToArray();
        Position[] existingTileClaims =
        [
            .. _fabricationReservations
                .Select(reservation => reservation.ReservedPosition),
            .. _splitReservations
                .SelectMany(reservation => reservation.Descendants)
                .Select(descendant => descendant.Position),
        ];
        ImmutableArray<BoundedChildFabricationReservationOutcome>
            provisionalFabrications =
            _fabrication.BuildProvisionalBatch(
                Tick,
                fabricationRequests,
                fabricationActors,
                fabricationSlots,
                existingTileClaims);
        ImmutableArray<SplitReplicationReservationOutcome>
            provisionalSplits = _split.BuildProvisionalBatch(
            Tick,
            splitRequests,
            splitActors,
            splitSlots,
            existingTileClaims);
        ImmutableHashSet<string> blockedOperationIds =
            ActorLifecycleReservationArbiter.BlockedOperationIds(
            [
                .. provisionalFabrications
                    .Where(outcome => outcome.Reservation is not null)
                    .Select(outcome =>
                        BoundedChildFabricationKernel.LifecycleClaim(
                            outcome.Reservation!)),
                .. provisionalSplits
                    .Where(outcome => outcome.Reservation is not null)
                    .Select(outcome =>
                        SplitReplicationKernel.LifecycleClaim(
                            outcome.Reservation!)),
            ]);
        ImmutableArray<BoundedChildFabricationReservationOutcome>
            fabrications = BoundedChildFabricationKernel.FinalizeBatch(
                provisionalFabrications,
                blockedOperationIds);
        ImmutableArray<SplitReplicationReservationOutcome> splits =
            SplitReplicationKernel.FinalizeBatch(
                provisionalSplits,
                blockedOperationIds);

        foreach (BoundedChildFabricationReservationOutcome outcome in
                 fabrications)
        {
            ActionState resolution =
                resolutions[outcome.Request.SourceActorId];
            if (outcome.Reservation is not
                BoundedChildFabricationProvisionalReservation reservation)
            {
                resolution.Outcome = outcome.Outcome switch
                {
                    BoundedChildFabricationReservationOutcome
                            .FabricationReservationOutcomeKind.Rejected =>
                        GenericActorRuntimeActionResolution.ActionOutcome
                            .Rejected,
                    BoundedChildFabricationReservationOutcome
                            .FabricationReservationOutcomeKind.Faulted =>
                        GenericActorRuntimeActionResolution.ActionOutcome
                            .Faulted,
                    _ =>
                        GenericActorRuntimeActionResolution.ActionOutcome
                            .Blocked,
                };
                continue;
            }

            _fabricationReservations.Add(reservation);
            SlotState slot = _slots[
                (reservation.TargetTeamId, reservation.TargetUnitId)];
            slot.Kind = SlotKind.FabricationPending;
            slot.FabricationReservation = reservation;
            events.Add(EmitSpatial(
                Tick,
                GenericActorRuntimeObservation.EventKind.LifecycleQueued,
                LifecyclePayload(
                    reservation,
                    cancellationReason: null),
                reservation.SourcePosition));
        }

        foreach (SplitReplicationReservationOutcome outcome in splits)
        {
            ActionState resolution = resolutions[
                outcome.Request.SourceActorId];
            if (outcome.Reservation is null)
            {
                resolution.Outcome =
                    GenericActorRuntimeActionResolution.ActionOutcome.Blocked;
                continue;
            }

            SplitReplicationReservation reservation = outcome.Reservation;
            _splitReservations.Add(reservation);
            foreach (SplitReplicationReservedDescendant descendant in
                     reservation.Descendants)
            {
                if (descendant.TeamId == reservation.SourceActorId.TeamId
                    && descendant.UnitId
                        == reservation.SourceActorId.UnitId)
                {
                    continue;
                }
                SlotState slot = _slots[
                    (descendant.TeamId, descendant.UnitId)];
                slot.Kind = SlotKind.ReplicationPending;
                slot.SplitReservation = reservation;
            }
            events.Add(EmitSpatial(
                Tick,
                GenericActorRuntimeObservation.EventKind.LifecycleQueued,
                LifecyclePayload(reservation, cancellationReason: null),
                reservation.SourcePosition));
        }
    }

    private void StartSameLifeTransitions(
        IReadOnlyDictionary<ActorIdentity, ActionState> resolutions,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        foreach (ActionState resolution in resolutions.Values
                     .Where(resolution =>
                         resolution.Outcome
                            == GenericActorRuntimeActionResolution
                                .ActionOutcome.Success
                         && _actions[resolution.ValidatedAction.ActionId].Kind
                            == ActorActionKind.SameLifeTransition)
                     .OrderBy(resolution => resolution.ActorId))
        {
            LifeState life = _lives[resolution.ActorId];
            ActorFormTransitionDefinition transition =
                MatchingSameLifeTransitions(
                    life,
                    resolution.ValidatedAction)
                .Single();
            var request = new ActorSameLifeTransitionRequest(
                life.ActorId,
                transition.TransitionId,
                $"same-life:{Tick}:{life.ActorId.TeamId}:" +
                $"{life.ActorId.UnitId}:{life.ActorId.LifeId}:" +
                $"{transition.TransitionId}");
            ActorSameLifeTransitionQueueOutcome outcome = _sameLife.Queue(
                Tick,
                request,
                SameLifeSnapshot(life));
            if (outcome.Reservation is not
                ActorSameLifeTransitionReservation reservation)
            {
                resolution.Outcome =
                    GenericActorRuntimeActionResolution.ActionOutcome.Blocked;
                continue;
            }

            life.PendingSameLifeTransition = reservation;
            events.Add(EmitSpatial(
                Tick,
                GenericActorRuntimeObservation.EventKind
                    .FormTransitionStarted,
                FormTransitionPayload(reservation),
                life.Position));
        }
    }

    private void CompleteDueSameLifeTransitions(
        ActorTransitionWindupDefinition.ActorTransitionCompletionKind
            completionKind,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        foreach (LifeState life in _lives.Values
                     .Where(life =>
                         life.PendingSameLifeTransition is
                             ActorSameLifeTransitionReservation pending
                         && pending.DueTick == Tick
                         && SameLifeTransition(pending).Windup.Completion
                            == completionKind)
                     .OrderBy(life => life.ActorId)
                     .ToArray())
        {
            ActorSameLifeTransitionReservation reservation =
                life.PendingSameLifeTransition!;
            ActorSameLifeTransitionCompletion completion =
                _sameLife.Complete(
                    Tick,
                    reservation,
                    SameLifeSnapshot(life));
            if (completion.Outcome
                == ActorSameLifeTransitionCompletion
                    .CompletionOutcomeKind.Cancelled)
            {
                CancelSameLifeTransition(life, events);
                continue;
            }

            ActorSameLifeTransitionCompletion.CompletedState state =
                completion.State
                ?? throw new InvalidOperationException(
                    "A completed same-life transition has no state.");
            life.FormId = state.FormId;
            life.Position = state.Position;
            life.Facing = state.Facing;
            life.Health = state.Health;
            life.Cooldown = state.Cooldown;
            life.Energy = state.Energy;
            life.PendingSameLifeTransition = null;
            life.HasPriorSameLifeTransition = true;
            if (SameLifeTransition(reservation).IrreversibleForLife)
            {
                life.IrreversibleReturnFormIds.Add(
                    reservation.SourceFormId);
            }
            events.Add(EmitSpatial(
                Tick,
                GenericActorRuntimeObservation.EventKind
                    .FormTransitionCompleted,
                FormTransitionPayload(reservation),
                life.Position));
        }
    }

    private void AdvanceExistingProjectiles(
        ICollection<PendingDamageContact> contacts,
        ref int contactOrdinal,
        ImmutableArray<GenericActorProjectileTraversal>.Builder traversals)
    {
        foreach (ProjectileState projectile in _projectiles
                     .OrderBy(projectile => projectile.Id)
                     .ToArray())
        {
            projectile.TicksUntilAdvance--;
            if (projectile.TicksUntilAdvance > 0)
                continue;
            projectile.TicksUntilAdvance =
                projectile.Profile.Projectile.TicksPerAdvance;
            TraverseProjectile(
                projectile,
                projectile.Profile.Projectile.TilesPerAdvance,
                contacts,
                ref contactOrdinal,
                traversals,
                GenericActorProjectileTraversal.TraversalTrigger
                    .ScheduledAdvance);
        }
    }

    private void ResolveAttacks(
        IReadOnlyDictionary<ActorIdentity, ActionState> resolutions,
        ICollection<PendingDamageContact> contacts,
        ref int contactOrdinal,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events,
        ImmutableArray<GenericActorProjectileTraversal>.Builder traversals)
    {
        foreach (ActionState resolution in resolutions.Values
                     .OrderBy(value => value.ActorId))
        {
            ActorActionDefinition action =
                _actions[resolution.ValidatedAction.ActionId];
            if (resolution.Outcome
                    != GenericActorRuntimeActionResolution.ActionOutcome.Success
                || action.Kind != ActorActionKind.Attack)
            {
                continue;
            }
            LifeState shooter = _lives[resolution.ActorId];
            ActorAttackProfileDefinition profile = AttackFor(shooter)
                ?? throw new InvalidOperationException(
                    "A successful attack has no form attack profile.");
            ProjectileHeading heading = ResolveLaunchHeading(
                shooter,
                profile,
                resolution.ValidatedAction);
            ShotProgram? program = ResolveShotProgram(
                profile,
                resolution.ValidatedAction);
            ImmutableArray<Position> path = TraceProjectilePath(
                shooter.Position,
                heading,
                profile,
                program);
            long projectileId = checked(_nextProjectileId++);
            var projectile = new ProjectileState(
                projectileId,
                shooter.ParticipantId,
                shooter.ActorId.TeamId,
                shooter.ActorId,
                Tick,
                shooter.Position,
                heading,
                program,
                profile,
                path);
            resolution.SuccessfulAttack = true;
            events.Add(EmitSpatial(
                Tick,
                GenericActorRuntimeObservation.EventKind.Attack,
                new GenericActorRuntimeObservation.EventPayload.Attack(
                    shooter.ActorId,
                    resolution.ValidatedAction,
                    projectileId,
                    shooter.Position,
                    heading),
                shooter.Position));

            int launchTraversal =
                profile.Projectile.Mode == ActorProjectileMode.InstantRay
                    ? profile.Projectile.MaxTravelTiles
                    : profile.Projectile.LaunchTiles;
            TraverseProjectile(
                projectile,
                launchTraversal,
                contacts,
                ref contactOrdinal,
                traversals,
                GenericActorProjectileTraversal.TraversalTrigger
                    .AttackLaunch);
            if (!projectile.Consumed
                && projectile.RemainingTiles > 0
                && profile.Projectile.Mode == ActorProjectileMode.Discrete)
            {
                _projectiles.Add(projectile);
            }
        }
    }

    private void TraverseProjectile(
        ProjectileState projectile,
        int maximumTiles,
        ICollection<PendingDamageContact> contacts,
        ref int contactOrdinal,
        ImmutableArray<GenericActorProjectileTraversal>.Builder traversals,
        GenericActorProjectileTraversal.TraversalTrigger trigger)
    {
        Position from = projectile.Position;
        var entered = ImmutableArray.CreateBuilder<Position>();
        GenericActorProjectileTraversal.TerminalDisposition? terminal = null;
        for (int step = 0;
             step < maximumTiles
             && projectile.NextPathIndex < projectile.Path.Length
             && projectile.RemainingTiles > 0;
             step++)
        {
            Position next =
                projectile.Path[projectile.NextPathIndex++];
            projectile.Heading = ProjectileHeadingExtensions.Between(
                projectile.Position,
                next);
            projectile.Position = next;
            entered.Add(next);
            projectile.RemainingTiles--;
            LifeState? target = _lives.Values
                .Where(life => life.Position == projectile.Position)
                .OrderBy(life => life.ActorId)
                .FirstOrDefault();
            if (target is null)
                continue;

            ProjectileContact contact = Contact(projectile, target);
            if (!contact.Consumes)
                continue;
            projectile.Consumed = true;
            _projectiles.Remove(projectile);
            terminal =
                new GenericActorProjectileTraversal.TerminalDisposition
                    .ActorContact(
                        target.ActorId,
                        contact.Damages);
            if (contact.Damages)
            {
                contacts.Add(new PendingDamageContact(
                    target.ActorId,
                    projectile.OwnerTeamId,
                    projectile.OwnerActorId,
                    projectile.Id,
                    projectile.Profile.Projectile.DamagePerHit,
                    contactOrdinal++));
            }
            break;
        }

        if (terminal is null
            && (projectile.NextPathIndex >= projectile.Path.Length
                || projectile.RemainingTiles == 0))
        {
            projectile.Consumed = true;
            _projectiles.Remove(projectile);
            terminal = projectile.RemainingTiles == 0
                ? new GenericActorProjectileTraversal.TerminalDisposition
                    .RangeExhausted()
                : new GenericActorProjectileTraversal.TerminalDisposition
                    .WallOrPathExhausted();
        }
        terminal ??=
            new GenericActorProjectileTraversal.TerminalDisposition.Retained();
        traversals.Add(CreateProjectileTraversal(
            projectile,
            GenericActorProjectileTraversal.TraversalPhase.Resolution,
            trigger,
            from,
            entered.ToImmutable(),
            terminal));
    }

    private ImmutableArray<GenericActorModeDamageContact> ApplyDamage(
        IEnumerable<PendingDamageContact> contacts,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        var scored =
            ImmutableArray.CreateBuilder<GenericActorModeDamageContact>();
        foreach (IGrouping<ActorIdentity, PendingDamageContact> targetGroup in
                 contacts
                     .OrderBy(contact => contact.TargetActorId)
                     .ThenBy(contact =>
                         contact.SourceTeamId)
                     .ThenBy(contact =>
                         contact.SourceActorId.UnitId)
                     .ThenBy(contact =>
                         contact.SourceActorId.LifeId)
                     .ThenBy(contact => contact.ProjectileId)
                     .ThenBy(contact => contact.ContactOrdinal)
                     .GroupBy(contact => contact.TargetActorId))
        {
            if (!_lives.TryGetValue(
                    targetGroup.Key,
                    out LifeState? target))
            {
                continue;
            }
            foreach (PendingDamageContact contact in targetGroup)
            {
                int actual = Math.Min(contact.Damage, target.Health);
                if (actual <= 0)
                    continue;
                target.Health -= actual;
                bool destroyed = target.Health == 0;
                if (destroyed && target.DestructionCause is null)
                    target.DestructionCause = contact;
                scored.Add(new GenericActorModeDamageContact(
                    contact.SourceTeamId,
                    target.ActorId.TeamId,
                    actual,
                    destroyed));
                events.Add(EmitSpatial(
                    Tick,
                    GenericActorRuntimeObservation.EventKind.Damage,
                    new GenericActorRuntimeObservation.EventPayload.Damage(
                        contact.SourceTeamId,
                        contact.SourceActorId,
                        target.ActorId,
                        contact.ProjectileId,
                        actual,
                        target.Health,
                        target.Position),
                    target.Position));
            }
        }
        return scored.ToImmutable();
    }

    private void ApplyDisqualifications(
        IEnumerable<int> participantIds,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events,
        ImmutableArray<GenericActorProjectileTraversal>.Builder traversals)
    {
        int[] participantBatch = participantIds
            .Distinct()
            .Order()
            .ToArray();
        if (participantBatch.Length == 0)
            return;
        HashSet<int> participantSet = participantBatch.ToHashSet();

        // Cancellation ordering is one complete joint-fault batch, never one
        // participant transaction at a time: every target-slot clock, then
        // every fabrication bundle, every replication bundle, same-life work,
        // then every active-life retirement.
        CancelParticipantClocks(participantSet, events);
        CancelParticipantFabrications(participantSet, events);
        CancelParticipantSplits(participantSet, events);
        CancelParticipantSameLifeTransitions(
            participantSet,
            includeDestroyed: false,
            events);

        ActorIdentity[] retiredActors = participantBatch
            .SelectMany(participantId =>
                _host.ApplyDisqualification(participantId))
            .Order()
            .ToArray();
        foreach (ActorIdentity actorId in retiredActors)
        {
            if (!_lives.Remove(actorId, out LifeState? life))
                continue;
            SlotState slot = _slots[(actorId.TeamId, actorId.UnitId)];
            slot.ActiveLife = null;
            if (life.Health == 0)
            {
                events.Add(EmitSpatial(
                    Tick,
                    GenericActorRuntimeObservation.EventKind.Destruction,
                    DestructionPayload(life),
                    life.Position));
                CancelSameLifeTransition(life, events);
            }
            else
            {
                events.Add(EmitSpatial(
                    Tick,
                    GenericActorRuntimeObservation.EventKind.LifeRetired,
                    new GenericActorRuntimeObservation.EventPayload
                        .LifeRetired(
                            actorId,
                            life.Generation,
                            life.FormId,
                            life.Position,
                            "participant-disqualified",
                            SourceTransitionId: null,
                            SourceOperationId: null),
                    life.Position));
            }
        }

        foreach (SlotState slot in _slots.Values
                     .Where(slot =>
                         participantSet.Contains(slot.ParticipantId))
                     .OrderBy(slot => slot.TeamId)
                     .ThenBy(slot => slot.UnitId))
        {
            slot.Kind = SlotKind.PermanentlyDormant;
            slot.ActiveLife = null;
            ClearPendingClock(slot);
            slot.FabricationReservation = null;
            slot.SplitReservation = null;
        }
        foreach (ProjectileState projectile in _projectiles
                     .Where(projectile =>
                         participantSet.Contains(
                             projectile.OwnerParticipantId))
                     .OrderBy(projectile => projectile.Id)
                     .ToArray())
        {
            _projectiles.Remove(projectile);
            traversals.Add(CreateProjectileTraversal(
                projectile,
                GenericActorProjectileTraversal.TraversalPhase.Resolution,
                GenericActorProjectileTraversal.TraversalTrigger
                    .ParticipantDisqualification,
                projectile.Position,
                [],
                new GenericActorProjectileTraversal.TerminalDisposition
                    .ParticipantDisqualification(
                        projectile.OwnerParticipantId)));
        }
        foreach (int participantId in participantBatch)
        {
            events.Add(EmitPublic(
                Tick,
                GenericActorRuntimeObservation.EventKind
                    .ParticipantDisqualified,
                new GenericActorRuntimeObservation.EventPayload.Participant(
                    participantId,
                    _participantTeams[participantId])));
        }
    }

    private void FinalizeDestroyedLives(
        IReadOnlySet<int> newlyDisqualified,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        foreach (LifeState life in _lives.Values
                     .Where(life => life.Health == 0)
                     .OrderBy(life => life.ActorId)
                     .ToArray())
        {
            if (newlyDisqualified.Contains(life.ParticipantId))
                continue;
            CancelSourceSplit(life.ActorId, "source-destroyed", events);
            _host.RetireLife(life.ActorId);
            _lives.Remove(life.ActorId);
            SlotState slot = _slots[
                (life.ActorId.TeamId, life.ActorId.UnitId)];
            slot.ActiveLife = null;
            events.Add(EmitSpatial(
                Tick,
                GenericActorRuntimeObservation.EventKind.Destruction,
                DestructionPayload(life),
                life.Position));
            CancelSameLifeTransition(life, events);
            ScheduleAfterDestruction(slot, life);
        }
    }

    private void CancelParticipantClocks(
        IReadOnlySet<int> participantIds,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        foreach (SlotState slot in _slots.Values
                     .Where(slot =>
                         participantIds.Contains(slot.ParticipantId)
                         && slot.Kind is SlotKind.AvailabilityPending
                             or SlotKind.AutomaticReturnPending)
                     .OrderBy(slot => slot.TeamId)
                     .ThenBy(slot => slot.UnitId))
        {
            GenericActorRuntimeObservation.UnitSlotState cancelledState =
                ProjectSlotState(slot);
            events.Add(EmitTeamPrivate(
                Tick,
                GenericActorRuntimeObservation.EventKind
                    .LifecycleClockCancelled,
                new GenericActorRuntimeObservation.EventPayload
                    .LifecycleClockCancelled(
                        slot.TeamId,
                        slot.UnitId,
                        cancelledState,
                        "participant-disqualified"),
                slot.TeamId));
            ClearPendingClock(slot);
            slot.Kind = SlotKind.Ready;
        }
    }

    private void ScheduleAfterDestruction(
        SlotState slot,
        LifeState life)
    {
        ActorLifecycleProfileDefinition profile =
            _lifecycleProfiles[slot.Assignment.LifecycleProfileId];
        switch (profile.DestructionPolicy)
        {
            case ActorLifecycleProfileDefinition.DestructionPolicyKind
                .AutomaticRespawn:
                slot.Kind = SlotKind.AutomaticReturnPending;
                slot.DueTick = checked(Tick + 1 + profile.DelayTicks);
                slot.PendingFormId = profile.AutomaticReturnFormId;
                slot.PendingGeneration = life.Generation;
                slot.PendingParentActorId = life.ActorId;
                break;
            case ActorLifecycleProfileDefinition.DestructionPolicyKind
                .ReadyForExplicitFabrication:
                slot.Kind = SlotKind.AvailabilityPending;
                slot.DueTick = checked(Tick + 1 + profile.DelayTicks);
                slot.PendingReason =
                    GenericActorRuntimeObservation.AvailabilityReason
                        .DestructionRecovery;
                break;
            case ActorLifecycleProfileDefinition.DestructionPolicyKind
                .PermanentlyDormant:
                slot.Kind = SlotKind.PermanentlyDormant;
                break;
            default:
                throw new InvalidOperationException(
                    "Unknown destruction policy.");
        }
    }

    private void RememberActionResolutions(
        IReadOnlyDictionary<ActorIdentity, ActionState> resolutions)
    {
        foreach (ActionState state in resolutions.Values)
        {
            if (_lives.TryGetValue(
                    state.ActorId,
                    out LifeState? life))
            {
                life.PreviousActionResolution = state.ToPublic();
            }
        }
    }

    private void UpdateCooldownsAndEnergy(
        IReadOnlyDictionary<ActorIdentity, ActionState> resolutions)
    {
        foreach (LifeState life in _lives.Values
                     .OrderBy(life => life.ActorId))
        {
            ActorAttackProfileDefinition? attack = AttackFor(life);
            if (attack is null)
            {
                // A same-life transition into an unarmed form keeps the
                // remaining cooldown as inert state.
                life.Energy = null;
                continue;
            }

            bool attacked = resolutions.TryGetValue(
                    life.ActorId,
                    out ActionState? resolution)
                && resolution.SuccessfulAttack;
            life.Cooldown = attacked
                ? attack.CooldownTicks
                : Math.Max(0, life.Cooldown - 1);
            if (attack.MaxEnergy == 0)
            {
                life.Energy = null;
                continue;
            }

            int energy = life.Energy
                ?? throw new InvalidOperationException(
                    "An energy-bearing life has no energy state.");
            if (attacked)
                energy = checked(energy - attack.AttackEnergyCost);
            if (attack.EnergyRegenerationIntervalTicks > 0
                && (Tick + 1)
                    % attack.EnergyRegenerationIntervalTicks == 0)
            {
                energy = (int)Math.Min(
                    attack.MaxEnergy,
                    checked(
                        (long)energy
                        + attack.EnergyRegenerationAmount));
            }
            life.Energy = energy;
        }
    }

    private void EmitModeChanges(
        GenericActorModeTickResult modeTick,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        foreach (GenericActorModeScoreChange change
                 in modeTick.ScoreChanges)
        {
            events.Add(EmitPublic(
                Tick,
                GenericActorRuntimeObservation.EventKind.ScoreChanged,
                new GenericActorRuntimeObservation.EventPayload.ScoreChanged(
                    change.TeamId,
                    change.Channel,
                    change.NewValue)));
        }
        if (modeTick.ModeChange is null)
        {
            return;
        }
        events.Add(EmitPublic(
            Tick,
            GenericActorRuntimeObservation.EventKind.ModeChanged,
            new GenericActorRuntimeObservation.EventPayload.ModeChanged(
                modeTick.ModeChange)));
    }

    private GenericActorModeCompletion Complete(
        GenericActorModeCompletionKind kind)
        => _mode.ResolveCompletion(
            kind,
            Tick,
            ModeWorldView());

    private GenericActorRuntimeObservation ProjectObservation(
        LifeState observer,
        ImmutableArray<GenericActorAuthoritativeEvent> sourceEvents)
    {
        ImmutableArray<LifeState> sensors =
            _definition.Rules.TeamPerception.Kind
                == ActorTeamPerceptionDefinition.PerceptionKind.ImmediateUnion
                ? _lives.Values
                    .Where(life =>
                        life.ActorId.TeamId == observer.ActorId.TeamId)
                    .OrderBy(life => life.ActorId)
                    .ToImmutableArray()
                : [observer];
        Dictionary<ActorIdentity, HashSet<Position>> visibleBySensor =
            sensors.ToDictionary(
                sensor => sensor.ActorId,
                VisibleTilesFor);

        ImmutableArray<GenericActorRuntimeObservation.ObservedTile>
            visibleTiles = AllMapPositions()
                .Select(position =>
                {
                    ImmutableArray<ActorIdentity> observedBy =
                        ObserversAt(position, visibleBySensor);
                    return (position, observedBy);
                })
                .Where(item => !item.observedBy.IsEmpty)
                .Select(item =>
                    new GenericActorRuntimeObservation.ObservedTile(
                        item.position,
                        _definition.Map.IsWall(item.position),
                        item.observedBy))
                .ToImmutableArray();

        ImmutableArray<GenericActorRuntimeObservation.ObservedEnemyState>
            enemies = _lives.Values
                .Where(life =>
                    life.ActorId.TeamId != observer.ActorId.TeamId)
                .OrderBy(life => life.ActorId)
                .Select(life =>
                    (life, observedBy:
                        ObserversAt(life.Position, visibleBySensor)))
                .Where(item => !item.observedBy.IsEmpty)
                .Select(item =>
                    new GenericActorRuntimeObservation.ObservedEnemyState(
                        item.life.ActorId,
                        item.life.FormId,
                        item.life.Position,
                        item.life.Facing,
                        item.life.Health,
                        PendingObservation(item.life),
                        item.observedBy))
                .ToImmutableArray();
        HashSet<ActorIdentity> visibleEnemyIds =
            enemies.Select(enemy => enemy.ActorId).ToHashSet();

        ImmutableArray<GenericActorRuntimeObservation.ObservedAllyState>
            allies =
            _definition.Rules.TeamPerception.Kind
                == ActorTeamPerceptionDefinition.PerceptionKind.ImmediateUnion
                ? _lives.Values
                    .Where(life =>
                        life.ActorId.TeamId == observer.ActorId.TeamId
                        && life.ActorId != observer.ActorId)
                    .OrderBy(life => life.ActorId)
                    .Select(life =>
                        new GenericActorRuntimeObservation.ObservedAllyState(
                            life.ActorId,
                            life.Generation,
                            life.FormId,
                            life.Position,
                            life.Facing,
                            life.Health,
                            life.Cooldown,
                            life.Energy,
                            life.PreviousActionResolution,
                            PendingObservation(life)))
                    .ToImmutableArray()
                : [];

        ImmutableArray<GenericActorRuntimeObservation.ObservedProjectile>?
            projectiles = _definition.Rules.AttackProfiles.All(profile =>
                profile.Projectile.Mode == ActorProjectileMode.InstantRay)
                ? null
                : _projectiles
                    .OrderBy(projectile => projectile.Id)
                    .Select(projectile =>
                        (projectile, observedBy:
                            ObserversAt(
                                projectile.Position,
                                visibleBySensor)))
                    .Where(item => !item.observedBy.IsEmpty)
                    .Select(item =>
                        new GenericActorRuntimeObservation
                            .ObservedProjectile(
                                item.projectile.Id,
                                item.projectile.OwnerTeamId,
                                item.projectile.OwnerTeamId
                                        == observer.ActorId.TeamId
                                    || visibleEnemyIds.Contains(
                                        item.projectile.OwnerActorId)
                                    ? item.projectile.OwnerActorId
                                    : null,
                                item.projectile.Position,
                                item.projectile.Heading,
                                item.projectile.Profile.Projectile
                                    .TilesPerAdvance,
                                item.projectile.TicksUntilAdvance,
                                item.projectile.RemainingTiles,
                                item.observedBy))
                    .ToImmutableArray();

        var visibleEvents =
            ImmutableArray.CreateBuilder<
                GenericActorRuntimeObservation.ObservedEvent>();
        var heardSounds =
            ImmutableArray.CreateBuilder<
                GenericActorRuntimeObservation.ObservedSound>();
        EventProjectionState eventProjection =
            EventProjectionFor(observer);
        foreach (GenericActorAuthoritativeEvent source in
                 sourceEvents
                     .OrderBy(item => item.Tick)
                     .ThenBy(item => item.GlobalOrdinal))
        {
            GenericActorRuntimeObservation.ObservedEvent sourceEvent =
                ToObservedEvent(source);
            GenericActorRuntimeObservation.EventPayload projectedPayload =
                source.UnredactedPayload;
            ImmutableArray<ActorIdentity> observedBy = [];
            var sounds = new List<ProjectedSound>();
            bool includeVisible = source.EventAudience switch
            {
                GenericActorAuthoritativeEvent.Audience.Public => true,
                GenericActorAuthoritativeEvent.Audience.TeamPrivate
                    teamPrivate =>
                    teamPrivate.TeamId == observer.ActorId.TeamId,
                GenericActorAuthoritativeEvent.Audience.Spatial spatial =>
                    ProjectSpatialEvent(
                        sourceEvent,
                        spatial.PrimaryPosition,
                        observer.ActorId.TeamId,
                        sensors,
                        visibleBySensor,
                        visibleEnemyIds,
                        sounds,
                        ref projectedPayload,
                        ref observedBy),
                _ => throw new InvalidOperationException(
                    "Unknown authoritative event audience."),
            };
            if (!includeVisible && sounds.Count == 0)
                continue;

            ProjectedEventIdentity identity = eventProjection.Resolve(
                source.EventHandle,
                source.Tick);
            if (includeVisible)
            {
                visibleEvents.Add(new
                    GenericActorRuntimeObservation.ObservedEvent(
                        identity.Handle,
                        source.Tick,
                        identity.SourceOrdinal,
                        source.Kind,
                        projectedPayload,
                        observedBy));
            }
            foreach (ProjectedSound sound in sounds)
            {
                heardSounds.Add(
                    new GenericActorRuntimeObservation.ObservedSound(
                        identity.Handle,
                        source.Tick,
                        identity.SourceOrdinal,
                        sound.ObserverActorId,
                        source.Kind,
                        sound.Bearing,
                        sound.DistanceBand));
            }
        }

        GenericActorModeProjection modeProjection =
            _mode.Project(ModeWorldView());
        return new GenericActorRuntimeObservation(
            _definition.CapabilityVersions.ObservationSchemaVersion,
            Tick,
            _host.MatchContractFingerprint,
            new GenericActorRuntimeObservation.ObservedSelfState(
                observer.ActorId,
                observer.Generation,
                observer.FormId,
                observer.Position,
                observer.Facing,
                observer.Health,
                observer.Cooldown,
                observer.Energy,
                observer.PreviousActionResolution,
                PendingObservation(observer)),
            TeamUnitObservations(observer.ActorId.TeamId),
            _host.ParticipantStatuses,
            allies,
            enemies,
            visibleTiles,
            projectiles,
            visibleEvents.ToImmutable(),
            sensors.All(sensor => VisionFor(sensor).HearingRadius == 0)
                ? null
                : heardSounds.ToImmutable(),
            modeProjection.Scoreboard,
            modeProjection.Mode,
            ActionLegalities(observer));
    }

    private bool ProjectSpatialEvent(
        GenericActorRuntimeObservation.ObservedEvent source,
        Position primaryPosition,
        int observingTeamId,
        ImmutableArray<LifeState> sensors,
        IReadOnlyDictionary<ActorIdentity, HashSet<Position>>
            visibleBySensor,
        IReadOnlySet<ActorIdentity> visibleEnemyIds,
        ICollection<ProjectedSound> sounds,
        ref GenericActorRuntimeObservation.EventPayload projectedPayload,
        ref ImmutableArray<ActorIdentity> observedBy)
    {
        observedBy = ObserversAt(primaryPosition, visibleBySensor);
        if (!observedBy.IsEmpty)
        {
            projectedPayload = RedactEventPayload(
                source.Payload,
                observingTeamId,
                visibleEnemyIds);
            return true;
        }

        ActorAudibleEventKind? audibleKind = AudibleKind(source.Kind);
        if (audibleKind is null)
            return false;
        foreach (LifeState sensor in sensors)
        {
            ActorVisionProfileDefinition vision = VisionFor(sensor);
            int distance =
                sensor.Position.ChebyshevDistance(primaryPosition);
            if (vision.HearingRadius == 0
                || distance == 0
                || distance > vision.HearingRadius
                || !vision.LoudEventKinds.Contains(audibleKind.Value))
            {
                continue;
            }
            sounds.Add(new ProjectedSound(
                sensor.ActorId,
                Hearing.BearingOctant(
                    sensor.Position,
                    primaryPosition),
                HearingDistanceBand(
                    distance,
                    vision.HearingDistanceBandUpperBounds)));
        }
        return false;
    }

    private EventProjectionState EventProjectionFor(LifeState observer)
    {
        ObservationAudienceKey audience =
            _definition.Rules.TeamPerception.Kind
                == ActorTeamPerceptionDefinition.PerceptionKind.ImmediateUnion
                ? new ObservationAudienceKey(
                    observer.ActorId.TeamId,
                    ActorId: null)
                : new ObservationAudienceKey(
                    observer.ActorId.TeamId,
                    observer.ActorId);
        if (!_eventProjectionStates.TryGetValue(
                audience,
                out EventProjectionState? projection))
        {
            projection = new EventProjectionState();
            _eventProjectionStates.Add(audience, projection);
        }
        return projection;
    }

    private ImmutableArray<GenericActorRuntimeObservation.ObservedUnitSlot>
        TeamUnitObservations(int teamId) =>
        _slots.Values
            .Where(slot => slot.TeamId == teamId)
            .OrderBy(slot => slot.UnitId)
            .Select(slot =>
                new GenericActorRuntimeObservation.ObservedUnitSlot(
                    slot.TeamId,
                    slot.UnitId,
                    ProjectSlotState(slot)))
            .ToImmutableArray();

    private GenericActorRuntimeObservation.UnitSlotState ProjectSlotState(
        SlotState slot) =>
        slot.Kind switch
        {
            SlotKind.Active =>
                new GenericActorRuntimeObservation.UnitSlotState.Active(
                    slot.ActiveLife!.ActorId,
                    slot.ActiveLife.Generation,
                    slot.ActiveLife.FormId),
            SlotKind.AvailabilityPending =>
                new GenericActorRuntimeObservation.UnitSlotState
                    .AvailabilityPending(
                        slot.PendingReason!.Value,
                        slot.DueTick!.Value),
            SlotKind.AutomaticReturnPending =>
                new GenericActorRuntimeObservation.UnitSlotState
                    .AutomaticReturnPending(
                        slot.DueTick!.Value,
                        slot.PendingFormId!,
                        slot.PendingGeneration!.Value),
            SlotKind.Ready =>
                new GenericActorRuntimeObservation.UnitSlotState.Ready(),
            SlotKind.FabricationPending =>
                FabricationPendingState(slot),
            SlotKind.ReplicationPending =>
                ReplicationPendingState(slot),
            SlotKind.PermanentlyDormant =>
                new GenericActorRuntimeObservation.UnitSlotState
                    .PermanentlyDormant(),
            _ => throw new InvalidOperationException(
                "Unknown stable-slot state."),
        };

    private static GenericActorRuntimeObservation.UnitSlotState
        FabricationPendingState(SlotState slot)
    {
        BoundedChildFabricationProvisionalReservation reservation =
            slot.FabricationReservation
            ?? throw new InvalidOperationException(
                "Pending fabrication slot has no reservation.");
        return new GenericActorRuntimeObservation.UnitSlotState
            .FabricationPending(
                reservation.DueTick,
                reservation.SourceActorId,
                reservation.TransitionId,
                reservation.OperationId,
                reservation.TargetFormId,
                reservation.ReservedPosition);
    }

    private static GenericActorRuntimeObservation.UnitSlotState
        ReplicationPendingState(SlotState slot)
    {
        SplitReplicationReservation reservation = slot.SplitReservation
            ?? throw new InvalidOperationException(
                "Pending Split slot has no reservation.");
        SplitReplicationReservedDescendant descendant =
            reservation.Descendants.Single(item =>
                item.TeamId == slot.TeamId
                && item.UnitId == slot.UnitId);
        return new GenericActorRuntimeObservation.UnitSlotState
            .ReplicationPending(
                reservation.DueTick,
                reservation.SourceActorId,
                reservation.TransitionId,
                reservation.OperationId,
                descendant.FormId,
                descendant.Position);
    }

    private ImmutableArray<GenericActorRuntimeActionLegality>
        ActionLegalities(LifeState life)
    {
        ActorFormDefinition form = _forms[life.FormId];
        return _definition.Rules.Actions
            .OrderBy(action => action.Code)
            .Select(action =>
            {
                bool allowed = form.AllowedActionIds.Contains(
                    action.Id,
                    StringComparer.Ordinal);
                return new GenericActorRuntimeActionLegality(
                    action.Id,
                    action.Code,
                    allowed,
                    allowed && IsAvailable(life, action),
                    ActionConstraints(life, action));
            })
            .ToImmutableArray();
    }

    private bool IsAvailable(
        LifeState life,
        ActorActionDefinition action)
    {
        if (_splitReservations.Any(reservation =>
                reservation.SourceActorId == life.ActorId))
        {
            return action.Kind == ActorActionKind.Wait;
        }
        if (life.PendingSameLifeTransition is not null)
            return action.Kind == ActorActionKind.Wait;
        return action.Kind switch
        {
            ActorActionKind.Wait => true,
            ActorActionKind.Movement or ActorActionKind.Rotation => true,
            ActorActionKind.Attack =>
                AttackFor(life) is ActorAttackProfileDefinition attack
                && life.Cooldown == 0
                && (attack.MaxEnergy == 0
                    || life.Energy >= attack.AttackEnergyCost),
            ActorActionKind.Replication =>
                MatchingSplitTransitions(life, action.Id) is
                    [SplitReplicationTransitionDefinition split]
                && IsSplitAvailable(life, split),
            ActorActionKind.Fabrication =>
                MatchingFabricationTransitions(life, action.Id) is
                    [BoundedChildFabricationDefinition fabrication]
                && _fabrication.IsSourceEligibleForRequest(
                    FabricationSnapshot(life),
                    fabrication)
                && FabricationTargets(life, action).Length > 0,
            ActorActionKind.SameLifeTransition =>
                _sameLife.MatchRoutes(life.FormId, action.Id)
                    .Any(transition => _sameLife.CanQueue(
                        SameLifeSnapshot(life),
                        transition.TransitionId)),
            _ => false,
        };
    }

    private bool IsSplitAvailable(
        LifeState life,
        SplitReplicationTransitionDefinition transition)
    {
        if (!SplitReplicationKernel.IsEligibleSource(
                SplitSnapshot(life),
                transition))
        {
            return false;
        }

        SlotState sourceSlot =
            _slots[(life.ActorId.TeamId, life.ActorId.UnitId)];
        if (!sourceSlot.Assignment.AllowedFormIds.Contains(
                transition.OutputFormId,
                StringComparer.Ordinal))
        {
            return false;
        }

        int additionalSlotsRequired = transition.DescendantCount - 1;
        int compatibleReadySlots = _slots.Values.Count(slot =>
            slot.TeamId == life.ActorId.TeamId
            && slot.ParticipantId == life.ParticipantId
            && slot.UnitId != life.ActorId.UnitId
            && slot.Kind == SlotKind.Ready
            && slot.Assignment.AllowedFormIds.Contains(
                transition.OutputFormId,
                StringComparer.Ordinal));
        return compatibleReadySlots >= additionalSlotsRequired;
    }

    private ImmutableArray<
        GenericActorRuntimeActionLegality.ArgumentConstraint>
        ActionConstraints(
            LifeState life,
            ActorActionDefinition action)
    {
        var constraints = ImmutableArray.CreateBuilder<
            GenericActorRuntimeActionLegality.ArgumentConstraint>();
        foreach (ActorActionParameterKind kind in action.ParameterKinds)
        {
            constraints.Add(kind switch
            {
                ActorActionParameterKind.ShotProgram =>
                    new GenericActorRuntimeActionLegality.ArgumentConstraint
                        .ShotProgramConstraint(
                            AttackFor(life)?.ShotProgram.Enabled == true),
                ActorActionParameterKind.Direction =>
                    new GenericActorRuntimeActionLegality.ArgumentConstraint
                        .DirectionConstraint(
                            Enum.GetValues<Direction>().ToImmutableArray()),
                ActorActionParameterKind.UnitTarget =>
                    new GenericActorRuntimeActionLegality.ArgumentConstraint
                        .UnitTargetConstraint(
                            action.Kind == ActorActionKind.Fabrication
                                ? FabricationTargets(life, action)
                                : _definition.Topology.UnitSlots
                                    .OrderBy(slot => slot.TeamId)
                                    .ThenBy(slot => slot.UnitId)
                                    .Select(slot =>
                                        new GenericActorRuntimeActionArgument
                                            .UnitTarget(
                                                slot.TeamId,
                                                slot.UnitId))
                                    .ToImmutableArray()),
                ActorActionParameterKind.FormTarget =>
                    new GenericActorRuntimeActionLegality.ArgumentConstraint
                        .FormTargetConstraint(
                            _sameLife.MatchRoutes(
                                    life.FormId,
                                    action.Id)
                                .Select(transition =>
                                    transition.TargetFormId)
                                .Distinct(StringComparer.Ordinal)
                                .Order(StringComparer.Ordinal)
                                .ToImmutableArray()),
                ActorActionParameterKind.ProjectileHeading =>
                    new GenericActorRuntimeActionLegality.ArgumentConstraint
                        .ProjectileHeadingConstraint(
                            Enum.GetValues<ProjectileHeading>()
                                .ToImmutableArray()),
                _ => throw new InvalidOperationException(
                    "Unknown actor action parameter kind."),
            });
        }
        return constraints.ToImmutable();
    }

    private HashSet<Position> VisibleTilesFor(LifeState sensor)
    {
        ActorVisionProfileDefinition vision = VisionFor(sensor);
        var visible = new HashSet<Position>();
        foreach (Position target in AllMapPositions())
        {
            int distance = sensor.Position.ChebyshevDistance(target);
            if (distance > vision.Range)
                continue;
            if (vision.Shape == ActorVisionShape.FacingQuadrant
                && distance > vision.OmnidirectionalProximityRange
                && !Visibility.InCone(
                    sensor.Position,
                    target,
                    sensor.Facing))
            {
                continue;
            }
            bool hasLineOfSight = true;
            foreach (Position position in Visibility.SupercoverLine(
                         sensor.Position,
                         target))
            {
                if (position == sensor.Position || position == target)
                    continue;
                if (_definition.Map.IsWall(position))
                {
                    hasLineOfSight = false;
                    break;
                }
            }
            if (hasLineOfSight)
                visible.Add(target);
        }
        return visible;
    }

    private IEnumerable<Position> AllMapPositions()
    {
        for (int y = 0; y < _definition.Map.Height; y++)
        {
            for (int x = 0; x < _definition.Map.Width; x++)
                yield return new Position(x, y);
        }
    }

    private static ImmutableArray<ActorIdentity> ObserversAt(
        Position position,
        IReadOnlyDictionary<ActorIdentity, HashSet<Position>>
            visibleBySensor) =>
        visibleBySensor
            .Where(pair => pair.Value.Contains(position))
            .Select(pair => pair.Key)
            .Order()
            .ToImmutableArray();

    private static ActorAudibleEventKind? AudibleKind(
        GenericActorRuntimeObservation.EventKind kind) =>
        kind switch
        {
            GenericActorRuntimeObservation.EventKind.Attack =>
                ActorAudibleEventKind.Attack,
            GenericActorRuntimeObservation.EventKind.Damage =>
                ActorAudibleEventKind.Damage,
            GenericActorRuntimeObservation.EventKind.Destruction =>
                ActorAudibleEventKind.Destruction,
            _ => null,
        };

    private static int HearingDistanceBand(
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

    private static string SplitCancellationId(
        SplitReplicationCompletion.SplitCancellationReason reason) =>
        reason switch
        {
            SplitReplicationCompletion.SplitCancellationReason
                .SourceUnavailable => "source-unavailable",
            SplitReplicationCompletion.SplitCancellationReason
                .SourceIdentityChanged => "source-identity-changed",
            SplitReplicationCompletion.SplitCancellationReason
                .SourceStateChanged => "source-state-changed",
            SplitReplicationCompletion.SplitCancellationReason
                .InsufficientHealth => "insufficient-health",
            _ => throw new ArgumentOutOfRangeException(nameof(reason)),
        };

    private static GenericActorRuntimeObservation.EventPayload
        RedactEventPayload(
            GenericActorRuntimeObservation.EventPayload payload,
            int observingTeamId,
            IReadOnlySet<ActorIdentity> visibleEnemyIds) =>
        payload switch
        {
            GenericActorRuntimeObservation.EventPayload.Attack value
                when value.ActorId.TeamId != observingTeamId =>
                value with
                {
                    // The launch heading is observable, but accepted
                    // arguments may encode future bends that have not yet
                    // manifested on the board.
                    Action = value.Action with
                    {
                        Arguments = [],
                    },
                },
            GenericActorRuntimeObservation.EventPayload.Damage value =>
                value with
                {
                    SourceActorId =
                        value.SourceActorId is ActorIdentity source
                        && (source.TeamId == observingTeamId
                            || visibleEnemyIds.Contains(source))
                            ? source
                            : null,
                },
            GenericActorRuntimeObservation.EventPayload.Destruction value =>
                value with
                {
                    SourceActorId =
                        value.SourceActorId is ActorIdentity source
                        && (source.TeamId == observingTeamId
                            || visibleEnemyIds.Contains(source))
                            ? source
                            : null,
                },
            GenericActorRuntimeObservation.EventPayload.LifeSpawned value =>
                RedactLifeSpawned(
                    value,
                    observingTeamId,
                    visibleEnemyIds),
            _ => payload,
        };

    internal static GenericActorRuntimeObservation.EventPayload.LifeSpawned
        RedactLifeSpawned(
            GenericActorRuntimeObservation.EventPayload.LifeSpawned value,
            int observingTeamId,
            IReadOnlySet<ActorIdentity> visibleEnemyIds)
    {
        bool parentDisclosed =
            value.ParentActorId is ActorIdentity parent
            && (parent.TeamId == observingTeamId
                || visibleEnemyIds.Contains(parent));
        return value with
        {
            ParentActorId = parentDisclosed
                ? value.ParentActorId
                : null,
            // Operation handles correlate every descendant with its source
            // bundle. Some handles also predate this projector and embed the
            // source identity, so they must follow the same disclosure rule
            // as ParentActorId.
            SourceOperationId = parentDisclosed
                ? value.SourceOperationId
                : null,
        };
    }

    private ActorVisionProfileDefinition VisionFor(LifeState life) =>
        _visionProfiles[_forms[life.FormId].VisionProfileId];

    private ActorAttackProfileDefinition? AttackFor(LifeState life)
    {
        string? attackId = _forms[life.FormId].AttackProfileId;
        return attackId is null ? null : _attackProfiles[attackId];
    }

    private BoundedChildFabricationDefinition[]
        MatchingFabricationTransitions(
            LifeState life,
            string actionId) =>
        _definition.Rules.FabricationTransitions
            .OfType<BoundedChildFabricationDefinition>()
            .Where(transition =>
                string.Equals(
                    transition.ActionId,
                    actionId,
                    StringComparison.Ordinal)
                && transition.SourceFormIds.Contains(
                    life.FormId,
                    StringComparer.Ordinal))
            .OrderBy(
                transition => transition.TransitionId,
                StringComparer.Ordinal)
            .ToArray();

    private ImmutableArray<GenericActorRuntimeActionArgument.UnitTarget>
        FabricationTargets(
            LifeState life,
            ActorActionDefinition action)
    {
        BoundedChildFabricationDefinition? transition =
            MatchingFabricationTransitions(life, action.Id)
                .SingleOrDefault();
        if (transition is null)
            return [];
        return _slots.Values
            .Where(slot =>
                slot.Kind == SlotKind.Ready
                && slot.TeamId == life.ActorId.TeamId
                && slot.ParticipantId == life.ParticipantId
                && (slot.TeamId, slot.UnitId)
                    != (life.ActorId.TeamId, life.ActorId.UnitId)
                && slot.Assignment.AllowedFormIds.Contains(
                    transition.OutputFormId,
                    StringComparer.Ordinal))
            .OrderBy(slot => slot.TeamId)
            .ThenBy(slot => slot.UnitId)
            .Select(slot =>
                new GenericActorRuntimeActionArgument.UnitTarget(
                    slot.TeamId,
                    slot.UnitId))
            .ToImmutableArray();
    }

    private SplitReplicationTransitionDefinition[]
        MatchingSplitTransitions(
            LifeState life,
            string actionId) =>
        _definition.Rules.ReplicationTransitions
            .OfType<SplitReplicationTransitionDefinition>()
            .Where(transition =>
                string.Equals(
                    transition.ActionId,
                    actionId,
                    StringComparison.Ordinal)
                && transition.SourceFormIds.Contains(
                    life.FormId,
                    StringComparer.Ordinal))
            .OrderBy(
                transition => transition.TransitionId,
                StringComparer.Ordinal)
            .ToArray();

    private ImmutableArray<ActorFormTransitionDefinition>
        MatchingSameLifeTransitions(
            LifeState life,
            GenericActorRuntimeActionResolution.ResolvedAction action)
    {
        string? targetFormId = action.Arguments
            .OfType<
                GenericActorRuntimeActionArgument.FormTargetArgument>()
            .SingleOrDefault()
            ?.FormId;
        return _sameLife.MatchRoutes(
            life.FormId,
            action.ActionId,
            targetFormId);
    }

    private ActorFormTransitionDefinition SameLifeTransition(
        ActorSameLifeTransitionReservation reservation) =>
        _definition.Rules.SameLifeTransitions
            .OfType<ActorFormTransitionDefinition>()
            .Where(transition => string.Equals(
                transition.TransitionId,
                reservation.TransitionId,
                StringComparison.Ordinal))
            .Single();

    private static ActorSameLifeTransitionActorSnapshot SameLifeSnapshot(
        LifeState life) =>
        new(
            life.ActorId,
            life.ParticipantId,
            life.Generation,
            life.FormId,
            life.Position,
            life.Facing,
            life.Health,
            life.Cooldown,
            life.Energy,
            life.HasPriorSameLifeTransition,
            life.IrreversibleReturnFormIds
                .Order(StringComparer.Ordinal)
                .ToImmutableArray(),
            life.PendingSameLifeTransition);

    private static GenericActorRuntimeActionResolution.ResolvedAction
        ToResolved(GenericActorRuntimeDecision decision) =>
        new(
            decision.ActionId,
            decision.ActionCode,
            decision.Arguments);

    private static ProjectileHeading ResolveLaunchHeading(
        LifeState shooter,
        ActorAttackProfileDefinition profile,
        GenericActorRuntimeActionResolution.ResolvedAction action)
    {
        if (profile.OmnidirectionalAim)
        {
            return action.Arguments
                .OfType<
                    GenericActorRuntimeActionArgument
                        .ProjectileHeadingArgument>()
                .Single()
                .Value;
        }
        ShotProgram? program = ResolveShotProgram(profile, action);
        return shooter.Facing
            .ToProjectileHeading()
            .Turned(program?.InitialAimOffset ?? 0);
    }

    private static ShotProgram? ResolveShotProgram(
        ActorAttackProfileDefinition profile,
        GenericActorRuntimeActionResolution.ResolvedAction action)
    {
        if (!profile.ShotProgram.Enabled)
            return null;
        return action.Arguments
                .OfType<
                    GenericActorRuntimeActionArgument.ShotProgramArgument>()
                .Select(argument => (ShotProgram?)argument.Value)
                .SingleOrDefault()
            ?? new ShotProgram(
                profile.ShotProgram.DefaultProgram.InitialAimOffset,
                profile.ShotProgram.DefaultProgram.BendDirection,
                profile.ShotProgram.DefaultProgram.BendAfterTiles,
                profile.ShotProgram.DefaultProgram.BendEveryTiles,
                profile.ShotProgram.DefaultProgram.BendCount);
    }

    private ImmutableArray<Position> TraceProjectilePath(
        Position origin,
        ProjectileHeading initialHeading,
        ActorAttackProfileDefinition profile,
        ShotProgram? program) =>
        GenericActorProjectilePath.Trace(
            _definition.Map,
            origin,
            initialHeading,
            profile,
            program);

    private ProjectileContact Contact(
        ProjectileState projectile,
        LifeState target)
    {
        if (_definition.Rules.Collisions.ProjectilesIgnoreFiringLife
            && target.ActorId == projectile.OwnerActorId)
        {
            return ProjectileContact.Pass;
        }
        if (target.ActorId.TeamId != projectile.OwnerTeamId)
            return ProjectileContact.Damage;
        return _definition.Rules.Collisions.AlliedProjectileContact switch
        {
            ActorCollisionDefinition.AlliedProjectileContactKind.PassThrough =>
                ProjectileContact.Pass,
            ActorCollisionDefinition.AlliedProjectileContactKind
                .BlockWithoutDamage => ProjectileContact.Block,
            ActorCollisionDefinition.AlliedProjectileContactKind
                .DamageAndBlock => ProjectileContact.Damage,
            _ => throw new InvalidOperationException(
                "Unknown allied projectile policy."),
        };
    }

    private bool IsForeignReservedReturnTile(
        LifeState mover,
        Position position)
    {
        foreach (SlotState slot in _slots.Values)
        {
            ActorLifecycleProfileDefinition profile =
                _lifecycleProfiles[slot.Assignment.LifecycleProfileId];
            if (profile.DestructionPolicy
                    != ActorLifecycleProfileDefinition.DestructionPolicyKind
                        .AutomaticRespawn)
            {
                continue;
            }
            Position reserved = _spawns[
                slot.Assignment.AssignedRespawnSpawnId!].Position;
            if (reserved == position
                && (slot.TeamId != mover.ActorId.TeamId
                    || slot.UnitId != mover.ActorId.UnitId))
            {
                return true;
            }
        }
        return false;
    }

    private bool IsReservedLifecycleTile(Position position) =>
        _fabricationReservations.Any(reservation =>
            reservation.ReservedPosition == position)
        || _splitReservations.Any(reservation =>
            reservation.Descendants.Any(descendant =>
                descendant.Position == position));

    private void ConsumeProjectilesAt(
        Position position,
        ImmutableArray<GenericActorProjectileTraversal>.Builder traversals)
    {
        foreach (ProjectileState projectile in _projectiles
                     .Where(projectile => projectile.Position == position)
                     .OrderBy(projectile => projectile.Id)
                     .ToArray())
        {
            _projectiles.Remove(projectile);
            traversals.Add(CreateProjectileTraversal(
                projectile,
                GenericActorProjectileTraversal.TraversalPhase.TickStart,
                GenericActorProjectileTraversal.TraversalTrigger
                    .LifecyclePlacement,
                projectile.Position,
                [],
                new GenericActorProjectileTraversal.TerminalDisposition
                    .LifecyclePlacementPurge(position)));
        }
    }

    private GenericActorProjectileTraversal CreateProjectileTraversal(
        ProjectileState projectile,
        GenericActorProjectileTraversal.TraversalPhase phase,
        GenericActorProjectileTraversal.TraversalTrigger trigger,
        Position from,
        IReadOnlyList<Position> path,
        GenericActorProjectileTraversal.TerminalDisposition terminal)
    {
        return new GenericActorProjectileTraversal(
            Tick,
            NextAuthoritativeFactOrdinal(),
            phase,
            trigger,
            projectile.Id,
            projectile.OwnerParticipantId,
            projectile.OwnerTeamId,
            projectile.OwnerActorId,
            projectile.Profile.Id,
            from,
            path,
            projectile.LaunchHeading,
            projectile.Heading,
            projectile.ShotProgram,
            terminal);
    }

    private LifeState CreateLife(
        SlotState slot,
        string formId,
        int generation,
        Position position,
        Direction facing,
        int health,
        GenericActorRuntimeStart.SpawnReason reason,
        ActorIdentity? parentActorId,
        string? sourceTransitionId,
        string? sourceOperationId,
        int? exactLifeId = null)
    {
        if (slot.ActiveLife is not null
            || _lives.Values.Any(life => life.Position == position))
        {
            throw new InvalidOperationException(
                $"Cannot create a life in occupied slot/tile " +
                $"{slot.TeamId}:{slot.UnitId} at {position}.");
        }
        if (!slot.Assignment.AllowedFormIds.Contains(
                formId,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Slot {slot.TeamId}:{slot.UnitId} does not allow form " +
                $"'{formId}'.");
        }

        int lifeId = exactLifeId ?? slot.NextLifeId;
        if (lifeId != slot.NextLifeId)
        {
            throw new InvalidOperationException(
                $"Slot {slot.TeamId}:{slot.UnitId} expected life ID " +
                $"{slot.NextLifeId}, got {lifeId}.");
        }
        int nextLifeId = checked(lifeId + 1);
        var actorId = new ActorIdentity(
            slot.TeamId,
            slot.UnitId,
            lifeId);
        ActorAttackProfileDefinition? attack =
            _forms[formId].AttackProfileId is string attackId
                ? _attackProfiles[attackId]
                : null;
        int? energy = attack is { MaxEnergy: > 0 }
            ? attack.MaxEnergy
            : null;
        GenericActorLifeStart lifeStart = _host.StartLife(
            actorId,
            slot.ParticipantId,
            new GenericActorRuntimeStart.LifeOrigin(
                reason,
                generation,
                parentActorId,
                sourceTransitionId,
                sourceOperationId));
        var life = new LifeState(
            actorId,
            slot.ParticipantId,
            generation,
            formId,
            position,
            facing,
            health,
            energy,
            Tick,
            lifeStart,
            reason,
            parentActorId,
            sourceTransitionId,
            sourceOperationId);
        slot.NextLifeId = nextLifeId;
        slot.ActiveLife = life;
        slot.Kind = SlotKind.Active;
        _lives.Add(actorId, life);
        return life;
    }

    private void ClearPendingClock(SlotState slot)
    {
        slot.DueTick = null;
        slot.PendingReason = null;
        slot.PendingFormId = null;
        slot.PendingGeneration = null;
        slot.PendingParentActorId = null;
    }

    private void ReleaseFabricationTarget(
        BoundedChildFabricationProvisionalReservation reservation)
    {
        SlotState slot = _slots[
            (reservation.TargetTeamId, reservation.TargetUnitId)];
        if (slot.Kind == SlotKind.FabricationPending
            && ReferenceEquals(
                slot.FabricationReservation,
                reservation))
        {
            slot.Kind = SlotKind.Ready;
            slot.FabricationReservation = null;
        }
    }

    private void ReleaseSplitTargets(
        SplitReplicationReservation reservation)
    {
        foreach (SplitReplicationReservedDescendant descendant in
                 reservation.Descendants)
        {
            if (descendant.TeamId == reservation.SourceActorId.TeamId
                && descendant.UnitId == reservation.SourceActorId.UnitId)
            {
                continue;
            }
            SlotState slot = _slots[
                (descendant.TeamId, descendant.UnitId)];
            if (slot.Kind == SlotKind.ReplicationPending
                && ReferenceEquals(
                    slot.SplitReservation,
                    reservation))
            {
                slot.Kind = SlotKind.Ready;
                slot.SplitReservation = null;
            }
        }
    }

    private void CancelSourceSplit(
        ActorIdentity sourceActorId,
        string reason,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        foreach (SplitReplicationReservation reservation in
                 _splitReservations
                     .Where(item =>
                         item.SourceActorId == sourceActorId)
                     .OrderBy(item => item.SourceActorId)
                     .ThenBy(
                         item => item.TransitionId,
                         StringComparer.Ordinal)
                     .ToArray())
        {
            _splitReservations.Remove(reservation);
            ReleaseSplitTargets(reservation);
            events.Add(EmitSpatial(
                Tick,
                GenericActorRuntimeObservation.EventKind.LifecycleCancelled,
                LifecyclePayload(reservation, reason),
                reservation.SourcePosition));
        }
    }

    private void CancelSameLifeTransition(
        LifeState life,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        if (life.PendingSameLifeTransition is not
            ActorSameLifeTransitionReservation reservation)
        {
            return;
        }
        life.PendingSameLifeTransition = null;
        events.Add(EmitSpatial(
            Tick,
            GenericActorRuntimeObservation.EventKind.FormTransitionCancelled,
            FormTransitionPayload(reservation),
            life.Position));
    }

    private void CancelParticipantSameLifeTransitions(
        IReadOnlySet<int> participantIds,
        bool includeDestroyed,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        foreach (LifeState life in _lives.Values
                     .Where(life =>
                         participantIds.Contains(life.ParticipantId)
                         && life.PendingSameLifeTransition is not null
                         && (includeDestroyed || life.Health > 0))
                     .OrderBy(life => life.ActorId)
                     .ToArray())
        {
            CancelSameLifeTransition(life, events);
        }
    }

    private void CancelParticipantFabrications(
        IReadOnlySet<int> participantIds,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        foreach (BoundedChildFabricationProvisionalReservation reservation
                 in _fabricationReservations
                     .Where(item =>
                         participantIds.Contains(item.ParticipantId))
                     .OrderBy(item => item.SourceActorId)
                     .ThenBy(
                         item => item.TransitionId,
                         StringComparer.Ordinal)
                     .ThenBy(item => item.TargetTeamId)
                     .ThenBy(item => item.TargetUnitId)
                     .ThenBy(
                         item => item.OperationId,
                         StringComparer.Ordinal)
                     .ToArray())
        {
            _fabricationReservations.Remove(reservation);
            ReleaseFabricationTarget(reservation);
            events.Add(EmitSpatial(
                Tick,
                GenericActorRuntimeObservation.EventKind.LifecycleCancelled,
                LifecyclePayload(
                    reservation,
                    "participant-disqualified"),
                reservation.SourcePosition));
        }
    }

    private void CancelParticipantSplits(
        IReadOnlySet<int> participantIds,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        foreach (SplitReplicationReservation reservation in
                 _splitReservations
                     .Where(item =>
                         participantIds.Contains(item.ParticipantId))
                     .OrderBy(item => item.SourceActorId)
                     .ThenBy(
                         item => item.TransitionId,
                         StringComparer.Ordinal)
                     .ThenBy(item =>
                         SplitTarget(item)?.TeamId ?? int.MaxValue)
                     .ThenBy(item =>
                         SplitTarget(item)?.UnitId ?? int.MaxValue)
                     .ThenBy(
                         item => item.OperationId,
                         StringComparer.Ordinal)
                     .ToArray())
        {
            _splitReservations.Remove(reservation);
            ReleaseSplitTargets(reservation);
            events.Add(EmitSpatial(
                Tick,
                GenericActorRuntimeObservation.EventKind.LifecycleCancelled,
                LifecyclePayload(
                    reservation,
                    "participant-disqualified"),
                reservation.SourcePosition));
        }
    }

    private SplitReplicationActorSnapshot SplitSnapshot(
        LifeState life) =>
        new(
            life.ActorId,
            life.ParticipantId,
            life.Generation,
            life.FormId,
            life.Health,
            life.Position,
            life.Facing,
            life.HasPriorSameLifeTransition,
            life.PendingSameLifeTransition is not null);

    private static BoundedChildFabricationActorSnapshot
        FabricationSnapshot(LifeState life) =>
        new(
            life.ActorId,
            life.ParticipantId,
            life.Generation,
            life.FormId,
            life.Position,
            life.Facing);

    private static BoundedChildFabricationSlotSnapshot
        FabricationSlotSnapshot(SlotState slot) =>
        new(
            slot.TeamId,
            slot.UnitId,
            slot.Kind switch
            {
                SlotKind.Active =>
                    BoundedChildFabricationSlotSnapshot
                        .FabricationSlotState.Active,
                SlotKind.Ready =>
                    BoundedChildFabricationSlotSnapshot
                        .FabricationSlotState.Ready,
                SlotKind.FabricationPending
                    or SlotKind.ReplicationPending =>
                    BoundedChildFabricationSlotSnapshot
                        .FabricationSlotState.Reserved,
                SlotKind.PermanentlyDormant =>
                    BoundedChildFabricationSlotSnapshot
                        .FabricationSlotState.PermanentlyDormant,
                _ => BoundedChildFabricationSlotSnapshot
                    .FabricationSlotState.Unavailable,
            },
            slot.ActiveLife?.ActorId);

    private SplitReplicationSlotSnapshot SplitSlotSnapshot(
        SlotState slot) =>
        new(
            slot.TeamId,
            slot.UnitId,
            slot.Kind switch
            {
                SlotKind.Active =>
                    SplitReplicationSlotSnapshot.SplitSlotState.Active,
                SlotKind.Ready =>
                    SplitReplicationSlotSnapshot.SplitSlotState.Ready,
                SlotKind.FabricationPending
                    or SlotKind.ReplicationPending =>
                    SplitReplicationSlotSnapshot.SplitSlotState.Reserved,
                SlotKind.PermanentlyDormant =>
                    SplitReplicationSlotSnapshot.SplitSlotState
                        .PermanentlyDormant,
                _ => SplitReplicationSlotSnapshot.SplitSlotState.Unavailable,
            },
            slot.ActiveLife?.ActorId);

    private void ValidateFrozenObservationBatch(
        GenericActorMatchPreparedTick tickStart,
        IReadOnlyCollection<GenericActorRuntimeObservation> supplied)
    {
        Dictionary<ActorIdentity, GenericActorRuntimeObservation> expected =
            tickStart.Observations.ToDictionary(
                observation => observation.Self.ActorId);
        var actual =
            new Dictionary<ActorIdentity, GenericActorRuntimeObservation>();
        foreach (GenericActorRuntimeObservation? observation in supplied)
        {
            if (observation?.Self?.ActorId is not ActorIdentity actorId
                || !actual.TryAdd(actorId, observation))
            {
                throw new ArgumentException(
                    "Step observations must be unique non-null frozen objects.",
                    nameof(supplied));
            }
        }
        if (actual.Count != expected.Count
            || expected.Any(pair =>
                !actual.TryGetValue(
                    pair.Key,
                    out GenericActorRuntimeObservation? observation)
                || !ReferenceEquals(pair.Value, observation)))
        {
            throw new ArgumentException(
                "Step requires every and only the exact observations returned by PrepareTick.",
                nameof(supplied));
        }
    }

    private ImmutableArray<int> EligibleTeamIds()
    {
        HashSet<int> disqualified = _host.ParticipantStatuses
            .Where(status => status.Disqualified)
            .Select(status => status.ParticipantId)
            .ToHashSet();
        return _definition.Topology.Teams
            .Where(team => _definition.Topology.Participants.Any(
                participant =>
                    participant.TeamId == team.TeamId
                    && !disqualified.Contains(participant.ParticipantId)))
            .Select(team => team.TeamId)
            .Order()
            .ToImmutableArray();
    }

    private Dictionary<int, long> ActiveHealthByTeam() =>
        _definition.Topology.Teams.ToDictionary(
            team => team.TeamId,
            team => _lives.Values
                .Where(life => life.ActorId.TeamId == team.TeamId)
                .Sum(life => (long)life.Health));

    private GenericActorModeWorldView ModeWorldView() =>
        new(
            ActiveHealthByTeam(),
            EligibleTeamIds(),
            _lives.Values
                .OrderBy(life => life.ActorId)
                .Select(life => new GenericActorModeActiveLife(
                    life.ActorId,
                    life.FormId,
                    life.Position,
                    life.Health))
                .ToImmutableArray());

    private GenericActorRuntimeObservation.EventPayload.LifeSpawned
        SpawnPayload(LifeState life)
    {
        return new GenericActorRuntimeObservation.EventPayload.LifeSpawned(
            life.ActorId,
            life.ParticipantId,
            life.ParentActorId,
            life.Generation,
            life.FormId,
            life.Health,
            life.Position,
            life.SpawnReason,
            life.SourceTransitionId,
            life.SourceOperationId);
    }

    private static GenericActorRuntimeObservation.EventPayload.Lifecycle
        LifecyclePayload(
            BoundedChildFabricationProvisionalReservation reservation,
            string? cancellationReason) =>
        new(
            reservation.TransitionId,
            reservation.OperationId,
            reservation.SourceActorId,
            reservation.TargetTeamId,
            reservation.TargetUnitId,
            reservation.DueTick,
            cancellationReason);

    private static GenericActorRuntimeObservation.EventPayload.Lifecycle
        LifecyclePayload(
            SplitReplicationReservation reservation,
            string? cancellationReason)
    {
        SplitReplicationReservedDescendant? target =
            SplitTarget(reservation);
        return new GenericActorRuntimeObservation.EventPayload.Lifecycle(
            reservation.TransitionId,
            reservation.OperationId,
            reservation.SourceActorId,
            target?.TeamId,
            target?.UnitId,
            reservation.DueTick,
            cancellationReason);
    }

    private static GenericActorRuntimeObservation.PendingSameLifeTransition?
        PendingObservation(LifeState life) =>
        life.PendingSameLifeTransition is
            ActorSameLifeTransitionReservation pending
            ? new GenericActorRuntimeObservation.PendingSameLifeTransition(
                pending.TransitionId,
                pending.OperationId,
                pending.TargetFormId,
                pending.StartedTick,
                pending.DueTick)
            : null;

    private static GenericActorRuntimeObservation.EventPayload.FormTransition
        FormTransitionPayload(
            ActorSameLifeTransitionReservation reservation) =>
        new(
            reservation.SourceActorId,
            reservation.TransitionId,
            reservation.OperationId,
            reservation.SourceFormId,
            reservation.TargetFormId,
            reservation.StartedTick,
            reservation.DueTick);

    private static SplitReplicationReservedDescendant? SplitTarget(
        SplitReplicationReservation reservation) =>
        reservation.Descendants
            .Where(descendant =>
                descendant.TeamId != reservation.SourceActorId.TeamId
                || descendant.UnitId != reservation.SourceActorId.UnitId)
            .OrderBy(descendant => descendant.TeamId)
            .ThenBy(descendant => descendant.UnitId)
            .FirstOrDefault();

    private static GenericActorRuntimeObservation.EventPayload.Destruction
        DestructionPayload(LifeState life)
    {
        PendingDamageContact? cause = life.DestructionCause;
        return new GenericActorRuntimeObservation.EventPayload.Destruction(
            life.ActorId,
            cause?.SourceTeamId,
            cause?.SourceActorId,
            cause?.ProjectileId,
            life.Generation,
            life.FormId,
            life.Position);
    }

    private GenericActorAuthoritativeEvent EmitSpatial(
        int tick,
        GenericActorRuntimeObservation.EventKind kind,
        GenericActorRuntimeObservation.EventPayload payload,
        Position primaryPosition) =>
        Emit(
            tick,
            kind,
            payload,
            new GenericActorAuthoritativeEvent.Audience.Spatial(
                primaryPosition));

    private GenericActorAuthoritativeEvent EmitTeamPrivate(
        int tick,
        GenericActorRuntimeObservation.EventKind kind,
        GenericActorRuntimeObservation.EventPayload payload,
        int teamId) =>
        Emit(
            tick,
            kind,
            payload,
            new GenericActorAuthoritativeEvent.Audience.TeamPrivate(teamId));

    private GenericActorAuthoritativeEvent EmitPublic(
        int tick,
        GenericActorRuntimeObservation.EventKind kind,
        GenericActorRuntimeObservation.EventPayload payload) =>
        Emit(
            tick,
            kind,
            payload,
            new GenericActorAuthoritativeEvent.Audience.Public());

    private GenericActorAuthoritativeEvent Emit(
        int tick,
        GenericActorRuntimeObservation.EventKind kind,
        GenericActorRuntimeObservation.EventPayload payload,
        GenericActorAuthoritativeEvent.Audience audience)
    {
        long globalOrdinal = NextAuthoritativeFactOrdinal();
        int sourceOrdinal = _nextEventOrdinalByTick.GetValueOrDefault(tick);
        _nextEventOrdinalByTick[tick] = checked(sourceOrdinal + 1);
        return new GenericActorAuthoritativeEvent(
            FormattableString.Invariant(
                $"authoritative-event:{globalOrdinal}"),
            tick,
            globalOrdinal,
            sourceOrdinal,
            kind,
            payload,
            audience);
    }

    private long NextAuthoritativeFactOrdinal()
    {
        long ordinal = _nextAuthoritativeFactOrdinal;
        _nextAuthoritativeFactOrdinal = checked(ordinal + 1);
        return ordinal;
    }

    private static GenericActorRuntimeObservation.ObservedEvent
        ToObservedEvent(GenericActorAuthoritativeEvent source) =>
        new(
            source.EventHandle,
            source.Tick,
            source.SourceOrdinal,
            source.Kind,
            source.UnredactedPayload,
            []);

    private GenericActorWorldSnapshot.SlotSnapshot SnapshotSlot(
        SlotState slot) =>
        new(
            slot.TeamId,
            slot.UnitId,
            slot.ParticipantId,
            slot.NextLifeId,
            ProjectSlotState(slot),
            slot.PendingParentActorId,
            slot.SplitReservation);

    private GenericActorWorldSnapshot.LifeSnapshot SnapshotLife(
        LifeState life) =>
        new(
            life.ActorId,
            life.ParticipantId,
            life.Generation,
            life.FormId,
            life.Position,
            life.Facing,
            life.Health,
            life.Cooldown,
            life.Energy,
            life.SpawnedAtTick,
            life.SpawnReason,
            life.ParentActorId,
            life.SourceTransitionId,
            life.SourceOperationId,
            life.PreviousActionResolution,
            PendingObservation(life));

    private static GenericActorWorldSnapshot.ProjectileSnapshot
        SnapshotProjectile(ProjectileState projectile) =>
        new(
            projectile.Id,
            projectile.OwnerParticipantId,
            projectile.OwnerTeamId,
            projectile.OwnerActorId,
            projectile.Profile.Id,
            projectile.SpawnedAtTick,
            projectile.Origin,
            projectile.Position,
            projectile.LaunchHeading,
            projectile.Heading,
            projectile.ShotProgram,
            projectile.Path,
            projectile.NextPathIndex,
            projectile.RemainingTiles,
            projectile.TicksUntilAdvance);

    private GenericActorWorldSnapshot SnapshotWorld()
    {
        ImmutableArray<GenericActorWorldSnapshot.SlotSnapshot> slots =
            _slots.Values
                .OrderBy(slot => slot.TeamId)
                .ThenBy(slot => slot.UnitId)
                .Select(SnapshotSlot)
                .ToImmutableArray();
        ImmutableArray<GenericActorWorldSnapshot.LifeSnapshot> lives =
            _lives.Values
                .OrderBy(life => life.ActorId)
                .Select(SnapshotLife)
                .ToImmutableArray();
        ImmutableArray<GenericActorWorldSnapshot.ProjectileSnapshot>
            projectiles = _projectiles
                .OrderBy(projectile => projectile.Id)
                .Select(SnapshotProjectile)
                .ToImmutableArray();
        GenericActorModeProjection modeProjection =
            _mode.Project(ModeWorldView());
        return new GenericActorWorldSnapshot(
            _definition,
            Tick,
            _nextProjectileId,
            _host.ParticipantStatuses,
            slots,
            lives,
            _splitReservations,
            projectiles,
            modeProjection.Scoreboard,
            modeProjection.Mode);
    }

    private static GenericActorMatchResult ToGenericResult(
        GenericActorModeCompletion result,
        IReadOnlyCollection<int> eligibleTeamIds,
        GenericActorWorldSnapshot finalState)
    {
        Dictionary<(int TeamId, int UnitId),
            GenericActorWorldSnapshot.LifeSnapshot> livesBySlot =
            finalState.ActiveLives.ToDictionary(
                life => (life.ActorId.TeamId, life.ActorId.UnitId));
        ImmutableArray<GenericActorMatchResult.UnitTerminalFact> units =
            finalState.Slots
                .Select(slot =>
                    new GenericActorMatchResult.UnitTerminalFact(
                        slot,
                        livesBySlot.GetValueOrDefault(
                            (slot.TeamId, slot.UnitId))))
                .ToImmutableArray();
        return new GenericActorMatchResult(
            result.CompletionReason,
            result.EndTick,
            result.Standings,
            eligibleTeamIds,
            units,
            result.ModeResult);
    }

    private static Dictionary<(int TeamId, int UnitId), SlotState>
        CreateSlots(ActorResolvedMatchDefinition definition)
    {
        Dictionary<
            (int TeamId, int UnitId),
            ActorUnitSlotLifecycleAssignmentDefinition> assignments =
            definition.LifecycleAssignments.ToDictionary(
                assignment => (assignment.TeamId, assignment.UnitId));
        return definition.Topology.UnitSlots.ToDictionary(
            slot => (slot.TeamId, slot.UnitId),
            slot =>
            {
                ActorUnitSlotLifecycleAssignmentDefinition assignment =
                    assignments[(slot.TeamId, slot.UnitId)];
                return new SlotState(
                    slot.TeamId,
                    slot.UnitId,
                    slot.ControllerParticipantId,
                    assignment)
                {
                    Kind = assignment.InitialAvailability
                        == ActorUnitSlotLifecycleAssignmentDefinition
                            .InitialAvailabilityKind.ActiveAtTickZero
                            ? SlotKind.Active
                            : SlotKind.AvailabilityPending,
                    DueTick = assignment.UnlockTick,
                    PendingReason = assignment.InitialAvailability
                        != ActorUnitSlotLifecycleAssignmentDefinition
                            .InitialAvailabilityKind.ActiveAtTickZero
                            ? GenericActorRuntimeObservation
                                .AvailabilityReason.InitialUnlock
                            : null,
                };
            });
    }

    private static void ValidateWorldCapabilities(
        ActorResolvedMatchDefinition definition)
    {
        if (definition.Rules.FabricationTransitions
            .OfType<BoundedChildFabricationDefinition>()
            .Any(transition =>
                transition.UnavailablePlacementResult
                    == ActorActionRejectionResult.Faulted))
        {
            throw new NotSupportedException(
                "The generic actor match session does not support fabrication placement outcomes that fault a participant.");
        }
        if (definition.Topology.InitialLives.Any(life =>
                life.LifeId != 0)
            || definition.LifecycleAssignments.Any(assignment =>
                assignment.InitialGeneration is int generation
                && generation != 0))
        {
            throw new NotSupportedException(
                "The first generic session requires initial life IDs and generations to start at zero.");
        }

        Dictionary<string, ActorMovementProfileDefinition> movement =
            definition.Rules.MovementProfiles.ToDictionary(
                profile => profile.Id,
                StringComparer.Ordinal);
        foreach (ActorFormDefinition form in definition.Rules.Forms)
        {
            if (movement[form.MovementProfileId].MovementLayer
                != ActorMovementLayer.Ground)
            {
                throw new NotSupportedException(
                    $"Form '{form.Id}' uses an unsupported non-ground movement layer.");
            }
        }
    }

    private void ThrowIfDisposed() => _host.ThrowIfDisposed();

    private void ThrowIfOperationInProgress() =>
        _host.ThrowIfOperationInProgress();

    private SessionOperation EnterOperation(string operationName) =>
        new(_host.EnterOperation(operationName));

    private readonly record struct ObservationAudienceKey(
        int TeamId,
        ActorIdentity? ActorId);

    private sealed class EventProjectionState
    {
        private readonly Dictionary<string, ProjectedEventIdentity>
            _projectedByAuthoritativeHandle = new(StringComparer.Ordinal);
        private readonly Dictionary<int, int> _nextSourceOrdinalByTick = [];
        private int _nextHandleOrdinal;

        public ProjectedEventIdentity Resolve(
            string authoritativeHandle,
            int sourceTick)
        {
            if (_projectedByAuthoritativeHandle.TryGetValue(
                    authoritativeHandle,
                    out ProjectedEventIdentity existing))
            {
                return existing;
            }

            int sourceOrdinal =
                _nextSourceOrdinalByTick.GetValueOrDefault(sourceTick);
            _nextSourceOrdinalByTick[sourceTick] =
                checked(sourceOrdinal + 1);
            int handleOrdinal = _nextHandleOrdinal;
            _nextHandleOrdinal = checked(handleOrdinal + 1);
            var projected = new ProjectedEventIdentity(
                FormattableString.Invariant($"event-{handleOrdinal}"),
                sourceOrdinal);
            _projectedByAuthoritativeHandle.Add(
                authoritativeHandle,
                projected);
            return projected;
        }
    }

    private readonly record struct ProjectedEventIdentity(
        string Handle,
        int SourceOrdinal);

    private readonly record struct ProjectedSound(
        ActorIdentity ObserverActorId,
        int Bearing,
        int DistanceBand);

    private readonly struct SessionOperation : IDisposable
    {
        private readonly GenericActorMatchHost.HostOperation _operation;

        public SessionOperation(
            GenericActorMatchHost.HostOperation operation)
        {
            _operation = operation;
        }

        public void Dispose()
        {
            _operation.Dispose();
        }
    }

    private enum SlotKind
    {
        Active = 0,
        AvailabilityPending = 1,
        AutomaticReturnPending = 2,
        Ready = 3,
        FabricationPending = 4,
        ReplicationPending = 5,
        PermanentlyDormant = 6,
    }

    private sealed class SlotState
    {
        public SlotState(
            int teamId,
            int unitId,
            int participantId,
            ActorUnitSlotLifecycleAssignmentDefinition assignment)
        {
            TeamId = teamId;
            UnitId = unitId;
            ParticipantId = participantId;
            Assignment = assignment;
        }

        public int TeamId { get; }
        public int UnitId { get; }
        public int ParticipantId { get; }
        public ActorUnitSlotLifecycleAssignmentDefinition Assignment { get; }
        public SlotKind Kind { get; set; }
        public LifeState? ActiveLife { get; set; }
        public int NextLifeId { get; set; }
        public int? DueTick { get; set; }
        public GenericActorRuntimeObservation.AvailabilityReason?
            PendingReason
        { get; set; }
        public string? PendingFormId { get; set; }
        public int? PendingGeneration { get; set; }
        public ActorIdentity? PendingParentActorId { get; set; }
        public BoundedChildFabricationProvisionalReservation?
            FabricationReservation
        { get; set; }
        public SplitReplicationReservation? SplitReservation { get; set; }
    }

    private sealed class LifeState
    {
        public LifeState(
            ActorIdentity actorId,
            int participantId,
            int generation,
            string formId,
            Position position,
            Direction facing,
            int health,
            int? energy,
            int spawnedAtTick,
            GenericActorLifeStart lifeStart,
            GenericActorRuntimeStart.SpawnReason spawnReason,
            ActorIdentity? parentActorId,
            string? sourceTransitionId,
            string? sourceOperationId)
        {
            ActorId = actorId;
            ParticipantId = participantId;
            Generation = generation;
            FormId = formId;
            Position = position;
            Facing = facing;
            Health = health;
            Energy = energy;
            SpawnedAtTick = spawnedAtTick;
            LifeStart = lifeStart;
            SpawnReason = spawnReason;
            ParentActorId = parentActorId;
            SourceTransitionId = sourceTransitionId;
            SourceOperationId = sourceOperationId;
        }

        public ActorIdentity ActorId { get; }
        public int ParticipantId { get; }
        public int Generation { get; }
        public string FormId { get; set; }
        public Position Position { get; set; }
        public Direction Facing { get; set; }
        public int Health { get; set; }
        public int Cooldown { get; set; }
        public int? Energy { get; set; }
        public int SpawnedAtTick { get; }
        public GenericActorLifeStart LifeStart { get; }
        public GenericActorRuntimeStart.SpawnReason SpawnReason { get; }
        public ActorIdentity? ParentActorId { get; }
        public string? SourceTransitionId { get; }
        public string? SourceOperationId { get; }
        public GenericActorRuntimeActionResolution? PreviousActionResolution
        {
            get;
            set;
        }
        public PendingDamageContact? DestructionCause { get; set; }
        public ActorSameLifeTransitionReservation?
            PendingSameLifeTransition
        { get; set; }
        public bool HasPriorSameLifeTransition { get; set; }
        public HashSet<string> IrreversibleReturnFormIds { get; } =
            new(StringComparer.Ordinal);
    }

    private sealed class ActionState
    {
        public ActionState(
            int participantId,
            ActorIdentity actorId,
            GenericActorRuntimeActionResolution.ResolvedAction?
                submittedAction,
            GenericActorRuntimeActionResolution.ResolvedAction acceptedAction,
            GenericActorRuntimeActionResolution.ResolvedAction validatedAction,
            GenericActorRuntimeActionResolution.ActionOutcome outcome,
            GenericActorRuntimeFault? runtimeFault)
        {
            ParticipantId = participantId;
            ActorId = actorId;
            SubmittedAction = submittedAction;
            AcceptedAction = acceptedAction;
            ValidatedAction = validatedAction;
            Outcome = outcome;
            RuntimeFault = runtimeFault;
        }

        public int ParticipantId { get; }
        public ActorIdentity ActorId { get; }
        public GenericActorRuntimeActionResolution.ResolvedAction?
            SubmittedAction
        { get; }
        public GenericActorRuntimeActionResolution.ResolvedAction
            AcceptedAction
        { get; }
        public GenericActorRuntimeActionResolution.ResolvedAction
            ValidatedAction
        { get; set; }
        public GenericActorRuntimeActionResolution.ActionOutcome Outcome
        {
            get;
            set;
        }
        public GenericActorRuntimeFault? RuntimeFault { get; }
        public bool SuccessfulAttack { get; set; }

        public GenericActorRuntimeActionResolution ToPublic() =>
            new(
                SubmittedAction,
                AcceptedAction,
                ValidatedAction,
                Outcome,
                RuntimeFault);
    }

    private sealed class ProjectileState
    {
        public ProjectileState(
            long id,
            int ownerParticipantId,
            int ownerTeamId,
            ActorIdentity ownerActorId,
            int spawnedAtTick,
            Position origin,
            ProjectileHeading launchHeading,
            ShotProgram? shotProgram,
            ActorAttackProfileDefinition profile,
            ImmutableArray<Position> path)
        {
            Id = id;
            OwnerParticipantId = ownerParticipantId;
            OwnerTeamId = ownerTeamId;
            OwnerActorId = ownerActorId;
            SpawnedAtTick = spawnedAtTick;
            Origin = origin;
            Position = origin;
            LaunchHeading = launchHeading;
            Heading = launchHeading;
            ShotProgram = shotProgram;
            Profile = profile;
            Path = path;
            RemainingTiles = profile.Projectile.MaxTravelTiles;
            TicksUntilAdvance = profile.Projectile.TicksPerAdvance;
        }

        public long Id { get; }
        public int OwnerParticipantId { get; }
        public int OwnerTeamId { get; }
        public ActorIdentity OwnerActorId { get; }
        public int SpawnedAtTick { get; }
        public Position Origin { get; }
        public Position Position { get; set; }
        public ProjectileHeading LaunchHeading { get; }
        public ProjectileHeading Heading { get; set; }
        public ShotProgram? ShotProgram { get; }
        public ActorAttackProfileDefinition Profile { get; }
        public ImmutableArray<Position> Path { get; }
        public int NextPathIndex { get; set; }
        public int RemainingTiles { get; set; }
        public int TicksUntilAdvance { get; set; }
        public bool Consumed { get; set; }
    }

    private sealed record PendingDamageContact(
        ActorIdentity TargetActorId,
        int SourceTeamId,
        ActorIdentity SourceActorId,
        long ProjectileId,
        int Damage,
        int ContactOrdinal);

    private readonly record struct ProjectileContact(
        bool Consumes,
        bool Damages)
    {
        public static ProjectileContact Pass => new(false, false);
        public static ProjectileContact Block => new(true, false);
        public static ProjectileContact Damage => new(true, true);
    }
}
