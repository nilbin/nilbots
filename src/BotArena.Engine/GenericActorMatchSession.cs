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

    /// <summary>
    /// Route-cooldown clocks (#181): first tick each cooldown-bearing route
    /// is available again, keyed by UNIT SLOT so the clock survives the
    /// body (a respawn does not reset it). Requested queues are gated;
    /// automatic (engine-caused) returns are exempt by design — a forced
    /// return must never be trapped by its own route's clock.
    /// </summary>
    private readonly Dictionary<(int TeamId, int UnitId, string TransitionId),
        int> _routeReadyAtTick = new();
    private readonly Dictionary<string, ActorFormDefinition> _forms;
    private readonly Dictionary<string, ActorVisionProfileDefinition>
        _visionProfiles;
    private readonly Dictionary<string, ActorAttackProfileDefinition>
        _attackProfiles;
    private readonly Dictionary<string, ActorMovementProfileDefinition>
        _movementProfiles;
    private readonly Dictionary<string, ActorActionDefinition> _actions;
    private readonly Dictionary<string, ActorLifecycleProfileDefinition>
        _lifecycleProfiles;
    private readonly Dictionary<string, InitialSpawnDefinition> _spawns;
    private readonly Dictionary<int, int> _participantTeams;
    private readonly Dictionary<int, string?> _participantClassIds;
    private readonly Dictionary<(int TeamId, int UnitId), string?>
        _slotClassIds;

    /// <summary>
    /// THE ROOT FACTORY clock, per participant (DECISIONS #194). A
    /// participant whose slots are all placed by explicit fabrication cannot
    /// place one once its last body dies, so the home base seeds one for it.
    /// Empty on every ruleset that declares no bootstrap, which is every
    /// ruleset shipped before prime dissolution.
    /// </summary>
    private readonly Dictionary<int, int> _rootFactoryDueTick = [];
    private readonly Dictionary<(int TeamId, int UnitId), SlotState> _slots;
    private readonly Dictionary<ActorIdentity, LifeState> _lives = [];
    private readonly List<ProjectileState> _projectiles = [];
    private readonly List<BoundedChildFabricationProvisionalReservation>
        _fabricationReservations = [];
    private readonly List<SplitReplicationReservation> _splitReservations = [];
    private readonly Dictionary<int, int> _nextEventOrdinalByTick = [];
    private readonly Dictionary<ObservationAudienceKey, EventProjectionState>
        _eventProjectionStates = [];

    /// <summary>
    /// This tick's observable union per scoring team. Cleared at the top of
    /// every <c>PrepareTick</c>, so it can never outlive the frozen boundary
    /// it belongs to.
    /// </summary>
    private readonly Dictionary<int, GenericMindTeamProjection>
        _teamProjectionCache = [];

    /// <summary>
    /// Where every body stood in the PREVIOUS mind observation — literally
    /// "last tick's <c>Bodies</c>", which is the collection a mind would
    /// otherwise hold in its own fields. Publishing it is what makes
    /// <c>MovedLastTick</c> free rather than a favour: the fact is exactly
    /// <c>previous.Position != Position</c>, computed once by the engine
    /// instead of nine times by nine authors with a documented footgun.
    /// <para>Deliberately NOT
    /// <see cref="_positionsAtPreviousTickEnd"/>, which is the mid-step
    /// stillness reference and equals the body's tick-start position by
    /// construction.</para>
    /// </summary>
    private ImmutableDictionary<ActorIdentity, Position>
        _positionsAtPreviousMindObservation =
            ImmutableDictionary<ActorIdentity, Position>.Empty;

    /// <summary>
    /// The label each body's own mind last attached to it (§12). Sticky, so
    /// one <c>SetRole</c> keeps publishing until the mind changes it; keyed by
    /// LIFE, so a slot's next body starts unlabelled rather than inheriting its
    /// predecessor's job.
    /// <para>
    /// It is deliberately readable by the whole projection rather than by the
    /// owning mind alone, because the tag is published on VISIBLE ENEMIES too.
    /// That is the design: the engine never reads the label, so a label the
    /// enemy can read is a free deception channel and calling your channeler a
    /// screen is a real move. Empty on the per-life generation, which has no
    /// way to set one.
    /// </para>
    /// </summary>
    private readonly Dictionary<ActorIdentity, string> _roleTags = [];
    private readonly HashSet<ActorIdentity> _arcSignatureDamagedThisTick = [];
    private ImmutableArray<GenericActorAuthoritativeEvent>
        _priorResolvedEvents;
    private ImmutableDictionary<ActorIdentity, Position>
        _positionsAtPreviousTickEnd =
            ImmutableDictionary<ActorIdentity, Position>.Empty;
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
        _movementProfiles = definition.Rules.MovementProfiles.ToDictionary(
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
        _slotClassIds = definition.Topology.UnitSlots.ToDictionary(
            slot => (slot.TeamId, slot.UnitId),
            slot => slot.ClassId);
        _participantClassIds = definition.Topology.Participants.ToDictionary(
            participant => participant.ParticipantId,
            participant => participant.ClassId);
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
                    health: EffectiveMaxHealth(
                        deployment.FormId,
                        deployment.TeamId,
                        deployment.UnitId),
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
        // The deployed world is the "end of the previous tick" tick 0 reads
        // against, which is also exactly what the replay validator sees in
        // the initial frame — so the two derivations of stillness agree from
        // the first tick rather than from the second.
        RememberPositions();
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
        // The base seeds BEFORE the ordinary readiness pass, because the seed
        // consumes the very clock that pass would otherwise turn into an idle
        // Ready slot: a bootstrapped slot goes straight from pending to live.
        ApplyRootFactorySeeds(
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
        if (_mode is ArcRelayActorMatchModeDriver arcRelay)
        {
            _arcSignatureDamagedThisTick.Clear();
            GenericActorRuntimeObservation.ModeObservationState.ArcRelay
                beforeSignatures = ((GenericActorModeState.ArcRelay)
                    arcRelay.State).State;
            ArcRelaySignatureRuntime.TickResult signatureTick =
                arcRelay.Signatures.Advance(Tick, ArcRelaySignatureLives());
            EmitModeEvents(signatureTick.Events, tickStartEvents);
            ArcSignatureApplication signatureApplication =
                ApplyArcRelaySignatureEffects(
                    arcRelay,
                    signatureTick.Effects,
                    tickStartEvents);
            EmitModeEvents(
                arcRelay.ResolveForcedMovement(
                    Tick,
                    signatureApplication.RelocatedActors,
                    ModeWorldView()),
                tickStartEvents);
            if (_arcSignatureDamagedThisTick.Count > 0)
            {
                var damageInterruptEvents = ImmutableArray
                    .CreateBuilder<GenericActorModeEvent>();
                arcRelay.Signatures.NotifyDamaged(
                    Tick,
                    _arcSignatureDamagedThisTick.ToImmutableArray(),
                    damageInterruptEvents);
                EmitModeEvents(damageInterruptEvents, tickStartEvents);
                _arcSignatureDamagedThisTick.Clear();
            }
            ImmutableArray<FrontlineScrapDestruction> signatureDestructions =
                FinalizeDestroyedLives(
                    ImmutableHashSet<int>.Empty,
                    tickStartEvents);
            if (!signatureDestructions.IsEmpty)
            {
                var signatureEvents = ImmutableArray
                    .CreateBuilder<GenericActorModeEvent>();
                arcRelay.Signatures.NotifyDestroyed(
                    Tick,
                    signatureDestructions.Select(value => value.ActorId)
                        .ToImmutableArray(),
                    signatureEvents);
                EmitModeEvents(signatureEvents, tickStartEvents);
                EmitModeEvents(
                    arcRelay.HandleDestructions(Tick, signatureDestructions),
                    tickStartEvents);
            }
            GenericActorRuntimeObservation.ModeObservationState.ArcRelay
                afterSignatures = arcRelay.ProjectStateAtTick(Tick);
            if (!Equals(beforeSignatures, afterSignatures))
            {
                EmitModeChanges(
                    new GenericActorModeTickResult(
                        scoreChanges: [],
                        afterSignatures,
                        modeObjectiveReached: false),
                    tickStartEvents);
            }
            EmitModeChanges(
                arcRelay.PrepareTick(Tick, ModeWorldView()),
                tickStartEvents);
        }

        ImmutableArray<GenericActorAuthoritativeEvent> sourceEvents =
            [
                .. _priorResolvedEvents,
                .. tickStartEvents,
            ];
        // ONE union per team per tick, for BOTH profiles. Under the per-life
        // profile the specialization is now a thin wrapper — self, the ally
        // list, and the legality mask — over a union computed once instead of
        // N byte-identical times, which is the O(N^2 x mapArea) -> O(N x
        // mapArea) collapse the memo measured (§4.6). Under the mind profile
        // the same object is handed straight to the participant.
        _teamProjectionCache.Clear();
        ImmutableArray<GenericActorRuntimeObservation> observations =
            _lives.Values
                .OrderBy(life => life.ActorId)
                .Select(life => ProjectObservation(life, sourceEvents))
                .ToImmutableArray();
        ImmutableArray<GenericMindRuntimeObservation> mindObservations =
            _host.IsMindProfile
                ? ProjectMindObservations(sourceEvents)
                : [];
        _preparedTick = new GenericActorMatchPreparedTick(
            Tick,
            observations,
            tickStartEvents
                .Select(ToObservedEvent)
                .ToImmutableArray())
        {
            MindObservations = mindObservations,
        };
        _preparedChronologyTick = new GenericActorMatchTickStart(
            Tick,
            SnapshotWorld(),
            _lives.Values
                .Select(life => life.ActorId)
                .Order()
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

        // The ONE structural change the mind profile makes to a tick: one
        // runtime per participant instead of one per life. The fan-out hands
        // everything below this line exactly the shape it always received, so
        // the 16 canonical phases run unchanged.
        GenericMindRuntimeTickResult? mindTick = _host.IsMindProfile
            ? _host.CollectMindTickDecisions(
                Tick,
                tickStart.MindObservations)
            : null;
        GenericActorRuntimeTickResult runtimeTick =
            mindTick?.ToActorTickResult()
            ?? _host.CollectTickDecisions(Tick, supplied);
        var resolutions = CreateActionResolutions(runtimeTick);
        var events =
            ImmutableArray.CreateBuilder<GenericActorAuthoritativeEvent>();
        var projectileTransitions =
            ImmutableArray.CreateBuilder<GenericActorProjectileTraversal>();
        var contacts = new List<PendingDamageContact>();
        var deflections = new List<PendingDeflection>();
        int contactOrdinal = 0;
        _arcSignatureDamagedThisTick.Clear();

        ResolveRotations(resolutions, events);
        ResolveMovement(
            resolutions,
            contacts,
            ref contactOrdinal,
            deflections,
            events,
            projectileTransitions);
        if (_mode is ArcRelayActorMatchModeDriver arcRelayAfterMovement)
        {
            ImmutableArray<ActorIdentity> movedActors = resolutions.Values
                .Where(resolution =>
                    resolution.Outcome
                        == GenericActorRuntimeActionResolution.ActionOutcome
                            .Success
                    && _actions[resolution.ValidatedAction.ActionId].Kind
                        == ActorActionKind.Movement)
                .Select(resolution => resolution.ActorId)
                .Order()
                .ToImmutableArray();
            EmitModeEvents(
                arcRelayAfterMovement.ResolveMovement(
                    Tick,
                    movedActors,
                    ModeWorldView()),
                events);
            var movedSignatureEvents =
                ImmutableArray.CreateBuilder<GenericActorModeEvent>();
            arcRelayAfterMovement.Signatures.NotifyMoved(
                Tick,
                movedActors,
                movedSignatureEvents);
            EmitModeEvents(movedSignatureEvents, events);
            ArcRelaySignatureRuntime.TickResult postMovementSignatures =
                arcRelayAfterMovement.Signatures.ResolvePostMovement(
                    Tick,
                    ArcRelaySignatureLives());
            EmitModeEvents(postMovementSignatures.Events, events);
            ApplyArcRelaySignatureEffects(
                arcRelayAfterMovement,
                postMovementSignatures.Effects,
                events);
            ResolveArcRelaySignatureActions(
                resolutions,
                arcRelayAfterMovement,
                events);
            ResolveArcRelayObjectiveActions(resolutions, events);
        }
        ReserveLifecycleCreations(resolutions, events);
        StartSameLifeTransitions(resolutions, events);
        AdvanceExistingProjectiles(
            contacts,
            ref contactOrdinal,
            deflections,
            events,
            projectileTransitions);
        ResolveAttacks(
            resolutions,
            contacts,
            ref contactOrdinal,
            deflections,
            events,
            projectileTransitions);
        // The tick's one launch point for guard returns: after every advance
        // and every attack, so a returned bolt joins the world exactly like a
        // freshly fired one.
        LaunchDeflectedProjectiles(
            deflections,
            contacts,
            ref contactOrdinal,
            events,
            projectileTransitions);
        // Counters are final here — the fan has launched and every guard
        // return has been published — and damage has not landed yet, so a
        // lethal hit cancels this windup exactly as it cancels a requested one.
        StartAutomaticReturns(events);
        ImmutableArray<GenericActorModeDamageContact> scoredContacts =
            ApplyDamage(contacts, events);
        if (_mode is ArcRelayActorMatchModeDriver signatureDamageMode)
        {
            var signatureEvents =
                ImmutableArray.CreateBuilder<GenericActorModeEvent>();
            signatureDamageMode.Signatures.NotifyDamaged(
                Tick,
                contacts.Select(value => value.TargetActorId)
                    .Concat(_arcSignatureDamagedThisTick)
                    .Distinct()
                    .ToImmutableArray(),
                signatureEvents);
            EmitModeEvents(signatureEvents, events);
        }

        foreach (GenericActorRuntimeFault fault in runtimeTick.Faults)
        {
            events.Add(EmitTeamPrivate(
                Tick,
                GenericActorRuntimeObservation.EventKind.RuntimeFault,
                new GenericActorRuntimeObservation.EventPayload
                    .RuntimeFault(fault),
                fault.ActorId.TeamId));
        }

        // A mind that traps on a tick it owns NO body has no per-body event to
        // ride on, and under the shipped threshold-0 allowance that silent
        // frame is exactly the moment a participant lost the match. Publish it
        // participant-scoped instead, team-private like every other fault, so
        // the fact is visible in events and in the replay rather than only in
        // the disqualification that follows it (P3, §4.7).
        foreach (GenericMindRuntimeFault fault in
                 mindTick?.Faults ?? [])
        {
            if (fault.ActorId is not null)
                continue;
            events.Add(EmitTeamPrivate(
                Tick,
                GenericActorRuntimeObservation.EventKind.MindRuntimeFault,
                new GenericActorRuntimeObservation.EventPayload
                    .MindRuntimeFault(fault),
                fault.TeamId));
        }

        HashSet<int> newlyDisqualified =
            runtimeTick.NewlyDisqualifiedParticipantIds.ToHashSet();
        ApplyDisqualifications(
            runtimeTick.NewlyDisqualifiedParticipantIds,
            events,
            projectileTransitions);
        ImmutableArray<FrontlineScrapDestruction> destructions =
            FinalizeDestroyedLives(newlyDisqualified, events);
        // The mode store's verb settles here: after every bolt has flown, so
        // a tier bought this tick cannot lengthen this tick's shot, and
        // before the resolutions are remembered, so a blocked purchase is the
        // outcome the next observation reports.
        ResolveInvestments(resolutions);
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
                new GenericActorModeTickInput(
                    Tick,
                    scoredContacts,
                    destructions));
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
        ImmutableArray<GenericActorMatchMindTurn> mindTurns =
            ProjectMindTurns(executedTick, tickStart, mindTick);
        GenericActorWorldSnapshot postState = SnapshotWorld();
        _host.RecordResolvedTick(
            new GenericActorMatchTickFrame(
                chronologyTick,
                actorTurns,
                authoritativeEvents,
                projectileTransitions.ToImmutable(),
                postState,
                mindTurns));
        // The tags this tick's accepted commands set become NEXT tick's
        // published labels; the observation the mind just answered was frozen
        // before any of them were written, which is the same one-tick
        // telegraph grammar a claim, a windup and a purchase already use.
        foreach (GenericActorMatchMindTurn turn in mindTurns)
            turn.ApplyRoleTags(_roleTags);
        foreach (ActorIdentity dead in _roleTags.Keys
                     .Where(actor => !_lives.ContainsKey(actor))
                     .ToArray())
        {
            _roleTags.Remove(dead);
        }
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
        RememberPositions();
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
            terminalResult)
        {
            MindTurns = mindTick?.MindTurns ?? [],
        };
    }

    /// <summary>
    /// Pairs each participant's frozen mind observation with what its runtime
    /// did with it, producing the chronology's mind-era turn
    /// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §5.1). Empty on the
    /// per-life generation, where there is nothing to pair.
    /// </summary>
    private static ImmutableArray<GenericActorMatchMindTurn> ProjectMindTurns(
        int executedTick,
        GenericActorMatchPreparedTick tickStart,
        GenericMindRuntimeTickResult? mindTick)
    {
        if (mindTick is null)
            return [];

        Dictionary<int, GenericMindRuntimeObservation> byParticipant =
            tickStart.MindObservations.ToDictionary(
                observation => observation.ParticipantId);
        return
        [
            .. mindTick.MindTurns
                .OrderBy(turn => turn.ParticipantId)
                .Select(turn => new GenericActorMatchMindTurn(
                    executedTick,
                    turn.ParticipantId,
                    turn.TeamId,
                    turn.TickFuelBudget,
                    turn.LiveOwnBodyCount,
                    byParticipant[turn.ParticipantId],
                    turn.Commands,
                    turn.ResolvedBodies,
                    turn.RejectedIntents,
                    turn.RuntimeFault,
                    turn.DebugMessage)),
        ];
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
                Position arrival = ResolveAutomaticArrival(slot, spawn);
                ConsumeProjectilesAt(arrival, traversals);
                LifeState life = CreateLife(
                    slot,
                    formId,
                    slot.Assignment.InitialGeneration!.Value,
                    arrival,
                    spawn.Facing,
                    EffectiveMaxHealth(formId, slot.TeamId, slot.UnitId),
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
                Position arrival = ResolveAutomaticArrival(slot, spawn);
                ConsumeProjectilesAt(
                    arrival,
                    traversals);
                LifeState life = CreateLife(
                    slot,
                    formId,
                    slot.PendingGeneration!.Value,
                    arrival,
                    spawn.Facing,
                    EffectiveMaxHealth(formId, slot.TeamId, slot.UnitId),
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
                EffectiveMaxHealth(
                    reservation.TargetFormId,
                    reservation.TargetTeamId,
                    reservation.TargetUnitId),
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
            case ActorActionKind.Rotation:
                return;
            case ActorActionKind.Movement:
                if (_mode is ArcRelayActorMatchModeDriver movementMode
                    && !movementMode.CanCarrierRelocate(life.ActorId, Tick))
                {
                    Block(state);
                }
                return;
            case ActorActionKind.Attack:
                ActorAttackProfileDefinition? attack = AttackFor(life);
                if (attack is null
                    || _mode is ArcRelayActorMatchModeDriver arcRelay
                        && arcRelay.CarriesCore(life.ActorId)
                    || life.Cooldown > 0
                    || attack.MaxEnergy > 0
                    && life.Energy < attack.AttackEnergyCost)
                {
                    Block(state);
                }
                return;
            case ActorActionKind.Objective:
                if (_mode is not ArcRelayActorMatchModeDriver objectiveMode
                    || !ArcRelayObjectiveAvailable(
                        objectiveMode,
                        life,
                        action))
                {
                    Block(state);
                }
                return;
            case ActorActionKind.Signature:
                if (_mode is not ArcRelayActorMatchModeDriver signatureMode
                    || !ArcRelaySignatureAvailable(
                        signatureMode,
                        life,
                        action))
                    Block(state);
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
                    || RouteOnCooldown(life, sameLifeMatches[0])
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
            case ActorActionKind.ModeInvestment:
                // The mask this life was handed was computed against the bank
                // as of tick start, and this is that same instant, so a track
                // absent from it was never legal. Whether the purchase still
                // fits AFTER a teammate spent first is settled later, in
                // ResolveInvestments — that is the ordinary simultaneous
                // reservation grammar rather than a new rule.
                if (InvestedTrack(state.ValidatedAction) is not string track
                    || !_mode.InvestableTracks(life.ActorId.TeamId)
                        .Contains(track, StringComparer.Ordinal))
                {
                    Block(state);
                }
                return;
            default:
                throw new InvalidOperationException(
                    $"Action kind '{action.Kind}' has no generic resolver.");
        }
    }

    private bool RouteOnCooldown(
        LifeState life,
        ActorFormTransitionDefinition transition) =>
        transition.CooldownTicks > 0
        && _routeReadyAtTick.TryGetValue(
            (life.ActorId.TeamId, life.ActorId.UnitId,
                transition.TransitionId),
            out int readyAtTick)
        && Tick < readyAtTick;

    private ImmutableArray<GenericActorRuntimeObservation
        .ObservedRouteCooldown> LiveRouteCooldowns(int teamId, int unitId) =>
        [
            .. _routeReadyAtTick
                .Where(entry =>
                    entry.Key.TeamId == teamId
                    && entry.Key.UnitId == unitId
                    && Tick < entry.Value)
                .OrderBy(
                    entry => entry.Key.TransitionId,
                    StringComparer.Ordinal)
                .Select(entry =>
                    new GenericActorRuntimeObservation.ObservedRouteCooldown(
                        entry.Key.TransitionId,
                        entry.Value)),
        ];

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
        ICollection<PendingDeflection> deflections,
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
            ProjectileHeading heading = MovementHeading(
                resolution.ValidatedAction);
            var (dx, dy) = heading.Vector();
            Position target = life.Position.Offset(dx, dy);
            targets.Add(life.ActorId, target);
            if (_definition.Map.IsWall(target)
                || _mode is ArcRelayActorMatchModeDriver arcMovement
                    && !arcMovement.CanEnter(life.ActorId, target)
                || _mode is ArcRelayActorMatchModeDriver constructMode
                    && constructMode.Signatures.BlocksBody(target, Tick)
                || IsForeignReservedReturnTile(life, target)
                || IsReservedLifecycleTile(target)
                || occupants.ContainsKey(target)
                // Defence in depth: the legality mask already offers only the
                // facing to a FacingLocked mover, so a non-facing direction
                // never survives argument admission. If one ever did, it must
                // resolve as Blocked rather than as a free sidestep.
                || (MovementFor(life).FacingCoupling
                        == ActorMovementFacingCoupling.FacingLocked
                    && heading != life.Facing.ToProjectileHeading()))
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
                        ProjectileDamage(
                            projectile,
                            life,
                            events),
                        contactOrdinal++));
                }
                else if (contact.Deflected)
                {
                    Deflect(projectile, life, events, deflections);
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
            ProjectileHeading heading = MovementHeading(
                resolution.ValidatedAction);
            if (MovementFor(life).FacingCoupling
                    == ActorMovementFacingCoupling.FaceMovementDirection
                && heading is ProjectileHeading.North
                    or ProjectileHeading.East
                    or ProjectileHeading.South
                    or ProjectileHeading.West)
            {
                // Facing is set before the event is emitted so the Movement
                // payload carries — and therefore evidences — the new facing.
                life.Facing = (Direction)((int)heading / 2);
            }
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

    private static ProjectileHeading MovementHeading(
        GenericActorRuntimeActionResolution.ResolvedAction action) =>
        action.Arguments
            .OfType<GenericActorRuntimeActionArgument
                .ProjectileHeadingArgument>()
            .SingleOrDefault()?.Value
        ?? action.Arguments
            .OfType<GenericActorRuntimeActionArgument.DirectionArgument>()
            .Single().Value.ToProjectileHeading();

    private void ResolveArcRelayObjectiveActions(
        IReadOnlyDictionary<ActorIdentity, ActionState> resolutions,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        if (_mode is not ArcRelayActorMatchModeDriver arcRelay)
            return;
        Dictionary<ActorIdentity, GenericActorWorldSnapshot.LifeSnapshot>
            tickStartLives = _preparedChronologyTick!.State.ActiveLives
                .ToDictionary(value => value.ActorId);
        foreach (ActionState resolution in resolutions.Values
                     .OrderBy(value => value.ActorId))
        {
            ActorActionDefinition action =
                _actions[resolution.ValidatedAction.ActionId];
            if (resolution.Outcome
                    != GenericActorRuntimeActionResolution.ActionOutcome.Success
                || action.Kind != ActorActionKind.Objective)
            {
                continue;
            }
            GenericActorModeEvent? modeEvent;
            if (string.Equals(
                    action.Id,
                    ArcRelayActionIds.DropCore,
                    StringComparison.Ordinal))
            {
                if (!arcRelay.TryDrop(
                        Tick,
                        resolution.ActorId,
                        out modeEvent))
                {
                    Block(resolution);
                    continue;
                }
            }
            else if (string.Equals(
                         action.Id,
                         ArcRelayActionIds.HandoffCore,
                         StringComparison.Ordinal))
            {
                GenericActorRuntimeActionArgument.UnitTarget target =
                    resolution.ValidatedAction.Arguments
                        .OfType<GenericActorRuntimeActionArgument
                            .UnitTargetArgument>()
                        .Single().Value;
                LifeState? targetLife = _lives.Values.SingleOrDefault(value =>
                    value.ActorId.TeamId == target.TeamId
                    && value.ActorId.UnitId == target.UnitId);
                bool receiverWaited = targetLife is not null
                    && resolutions.TryGetValue(
                        targetLife.ActorId,
                        out ActionState? targetResolution)
                    && targetResolution.Outcome
                        == GenericActorRuntimeActionResolution.ActionOutcome
                            .Success
                    && _actions[targetResolution.ValidatedAction.ActionId].Kind
                        == ActorActionKind.Wait
                    && tickStartLives[targetLife.ActorId].Position
                        == targetLife.Position;
                if (!receiverWaited
                    || !arcRelay.TryHandoff(
                        Tick,
                        resolution.ActorId,
                        targetLife!.ActorId,
                        _lives[resolution.ActorId].Position,
                        targetLife.Position,
                        out modeEvent))
                {
                    Block(resolution);
                    continue;
                }
            }
            else
            {
                Block(resolution);
                continue;
            }
            EmitModeEvent(modeEvent!, events);
        }
    }

    private void ResolveArcRelaySignatureActions(
        IReadOnlyDictionary<ActorIdentity, ActionState> resolutions,
        ArcRelayActorMatchModeDriver mode,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        foreach (ActionState resolution in resolutions.Values
                     .OrderBy(value => value.ActorId))
        {
            ActorActionDefinition action =
                _actions[resolution.ValidatedAction.ActionId];
            if (resolution.Outcome
                    != GenericActorRuntimeActionResolution.ActionOutcome.Success
                || action.Kind != ActorActionKind.Signature)
            {
                continue;
            }

            ArcRelaySignatureDefinition signature =
                mode.Signatures.DefinitionForAction(action.Id);
            if (signature is ArcRelaySignatureDefinition.ArcToss
                && (!mode.CarriesCore(resolution.ActorId)
                    || !mode.CanCarrierRelocate(resolution.ActorId, Tick)))
            {
                Block(resolution);
                continue;
            }
            if (signature is ArcRelaySignatureDefinition.Exchange)
            {
                GenericActorRuntimeActionArgument.UnitTarget target =
                    resolution.ValidatedAction.Arguments
                        .OfType<GenericActorRuntimeActionArgument
                            .UnitTargetArgument>()
                        .Single().Value;
                LifeState? targetLife = _lives.Values.SingleOrDefault(value =>
                    value.ActorId.TeamId == target.TeamId
                    && value.ActorId.UnitId == target.UnitId);
                bool targetWaited = targetLife is not null
                    && resolutions.TryGetValue(
                        targetLife.ActorId,
                        out ActionState? targetResolution)
                    && targetResolution.Outcome
                        == GenericActorRuntimeActionResolution.ActionOutcome
                            .Success
                    && _actions[targetResolution.ValidatedAction.ActionId].Kind
                        == ActorActionKind.Wait;
                if (!targetWaited)
                {
                    Block(resolution);
                    continue;
                }
            }

            LifeState owner = _lives[resolution.ActorId];
            ArcRelaySignatureRuntime.TickResult started = mode.Signatures.Start(
                Tick,
                owner.ActorId,
                owner.Position,
                action.Id,
                resolution.ValidatedAction.Arguments,
                ArcRelaySignatureLives());
            EmitModeEvents(started.Events, events);
            ArcSignatureApplication application =
                ApplyArcRelaySignatureEffects(mode, started.Effects, events);
            EmitModeEvents(
                mode.ResolveForcedMovement(
                    Tick,
                    application.RelocatedActors,
                    ModeWorldView()),
                events);
        }
    }

    private ArcSignatureApplication ApplyArcRelaySignatureEffects(
        ArcRelayActorMatchModeDriver mode,
        IEnumerable<ArcRelaySignatureRuntime.Effect> effects,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        var relocated = ImmutableArray.CreateBuilder<ActorIdentity>();
        foreach (ArcRelaySignatureRuntime.Effect effect in effects)
        {
            switch (effect)
            {
                case ArcRelaySignatureRuntime.Effect.VectorDash dash:
                    ApplyVectorDash(mode, dash, relocated, events);
                    break;
                case ArcRelaySignatureRuntime.Effect.TractorHook hook:
                    ApplyTractorHook(mode, hook, relocated, events);
                    break;
                case ArcRelaySignatureRuntime.Effect.Repair repair:
                    ApplySignatureRepair(mode, repair, events);
                    break;
                case ArcRelaySignatureRuntime.Effect.FallingStar star:
                    foreach (LifeState target in _lives.Values
                                 .Where(value => IsFallingStarTile(
                                     value.Position,
                                     star.Target))
                                 .OrderBy(value => value.ActorId)
                                 .ToArray())
                    {
                        ApplySignatureDamage(
                            mode,
                            star.OperationId,
                            star.Owner,
                            target,
                            ((ArcRelaySignatureDefinition.FallingStar)
                                mode.Signatures.DefinitionFor(star.Owner)).Damage,
                            events);
                    }
                    break;
                case ArcRelaySignatureRuntime.Effect.TripNode node:
                    if (_lives.TryGetValue(
                            node.Target,
                            out LifeState? nodeTarget))
                    {
                        ApplySignatureDamage(
                            mode,
                            node.OperationId,
                            node.Owner,
                            nodeTarget,
                            node.Damage,
                            events);
                    }
                    break;
                case ArcRelaySignatureRuntime.Effect.ArcTossLaunch launch:
                    EmitModeEvents(
                        mode.LaunchArcToss(
                            Tick,
                            launch.Owner,
                            launch.Target,
                            launch.CompletesAtTick),
                        events);
                    break;
                case ArcRelaySignatureRuntime.Effect.ArcTossLand landing:
                    EmitModeEvents(
                        mode.LandArcToss(
                            Tick,
                            landing.Owner,
                            landing.Target,
                            ModeWorldView()),
                        events);
                    break;
                case ArcRelaySignatureRuntime.Effect.Exchange exchange:
                    ApplyExchange(mode, exchange, relocated, events);
                    break;
                case ArcRelaySignatureRuntime.Effect.RailLine rail:
                    ApplyRailLine(mode, rail, events);
                    break;
                case ArcRelaySignatureRuntime.Effect.KineticBurst burst:
                    ApplyKineticBurst(mode, burst, relocated, events);
                    break;
                case ArcRelaySignatureRuntime.Effect.SentinelFire sentinel:
                    ApplySentinelFire(mode, sentinel, events);
                    break;
            }
        }
        if (relocated.Count > 0)
        {
            var signatureEvents =
                ImmutableArray.CreateBuilder<GenericActorModeEvent>();
            mode.Signatures.NotifyMoved(
                Tick,
                relocated.ToImmutable(),
                signatureEvents);
            EmitModeEvents(signatureEvents, events);
        }
        return new ArcSignatureApplication(relocated.ToImmutable());
    }

    private void ApplyVectorDash(
        ArcRelayActorMatchModeDriver mode,
        ArcRelaySignatureRuntime.Effect.VectorDash effect,
        ImmutableArray<ActorIdentity>.Builder relocated,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        if (!_lives.TryGetValue(effect.Owner, out LifeState? life))
            return;
        if (mode.TrySignatureDepartureDrop(
                Tick,
                effect.Owner,
                out GenericActorModeEvent? drop))
        {
            EmitModeEvent(drop!, events);
        }
        int range = ((ArcRelaySignatureDefinition.VectorDash)
            mode.Signatures.DefinitionFor(effect.Owner)).MaxTiles;
        Position destination = FurthestLegalSignatureTile(
            life.ActorId,
            life.Position,
            effect.Heading,
            range);
        RelocateBySignature(
            mode,
            effect.OperationId,
            effect.Owner,
            life,
            destination,
            relocated,
            events);
    }

    private void ApplyTractorHook(
        ArcRelayActorMatchModeDriver mode,
        ArcRelaySignatureRuntime.Effect.TractorHook effect,
        ImmutableArray<ActorIdentity>.Builder relocated,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        if (!_lives.TryGetValue(effect.Owner, out LifeState? owner))
            return;
        ArcRelaySignatureDefinition.TractorHook rule =
            (ArcRelaySignatureDefinition.TractorHook)
                mode.Signatures.DefinitionFor(effect.Owner);
        var (dx, dy) = effect.Heading.Vector();
        LifeState? target = null;
        for (int step = 1; step <= rule.Range; step++)
        {
            Position tile = owner.Position.Offset(dx * step, dy * step);
            if (_definition.Map.IsWall(tile))
                break;
            target = _lives.Values.SingleOrDefault(value =>
                value.Position == tile);
            if (target is not null)
                break;
        }
        if (target is null)
            return;
        Position destination = target.Position;
        for (int step = 0; step < rule.MaxPullTiles; step++)
        {
            Position next = destination.Offset(-dx, -dy);
            if (next == owner.Position
                || _definition.Map.IsWall(next)
                || _lives.Values.Any(value =>
                    value.ActorId != target.ActorId
                    && value.Position == next))
            {
                break;
            }
            destination = next;
        }
        RelocateBySignature(mode, effect.OperationId, effect.Owner, target,
            destination, relocated, events);
    }

    private void ApplyExchange(
        ArcRelayActorMatchModeDriver mode,
        ArcRelaySignatureRuntime.Effect.Exchange effect,
        ImmutableArray<ActorIdentity>.Builder relocated,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        if (!_lives.TryGetValue(effect.Owner, out LifeState? owner)
            || !_lives.TryGetValue(effect.Target, out LifeState? target)
            || owner.Position != effect.SourceStart
            || target.Position != effect.TargetStart
            || !mode.CanEnter(owner.ActorId, effect.TargetStart)
            || !mode.CanEnter(target.ActorId, effect.SourceStart))
        {
            return;
        }
        if (mode.TrySignatureDepartureDrop(
                Tick,
                target.ActorId,
                out GenericActorModeEvent? drop))
        {
            EmitModeEvent(drop!, events);
        }
        Position ownerFrom = owner.Position;
        Position targetFrom = target.Position;
        owner.Position = targetFrom;
        target.Position = ownerFrom;
        relocated.Add(owner.ActorId);
        relocated.Add(target.ActorId);
        EmitSignatureRelocation(mode, effect.OperationId, effect.Owner,
            owner.ActorId, ownerFrom, owner.Position, events);
        EmitSignatureRelocation(mode, effect.OperationId, effect.Owner,
            target.ActorId, targetFrom, target.Position, events);
    }

    private void ApplyKineticBurst(
        ArcRelayActorMatchModeDriver mode,
        ArcRelaySignatureRuntime.Effect.KineticBurst effect,
        ImmutableArray<ActorIdentity>.Builder relocated,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        LifeState[] adjacent = _lives.Values.Where(value =>
                value.ActorId != effect.Owner
                && value.Position.ChebyshevDistance(effect.Origin) == 1)
            .OrderBy(value => value.ActorId).ToArray();
        Dictionary<ActorIdentity, Position> requested = adjacent.ToDictionary(
            value => value.ActorId,
            value => value.Position.Offset(
                Math.Sign(value.Position.X - effect.Origin.X),
                Math.Sign(value.Position.Y - effect.Origin.Y)));
        HashSet<Position> duplicateTargets = requested.Values
            .GroupBy(value => value)
            .Where(value => value.Count() > 1)
            .Select(value => value.Key).ToHashSet();
        foreach (LifeState target in adjacent)
        {
            Position destination = requested[target.ActorId];
            if (duplicateTargets.Contains(destination)
                || _definition.Map.IsWall(destination)
                || _lives.Values.Any(value =>
                    value.ActorId != target.ActorId
                    && value.Position == destination))
            {
                continue;
            }
            RelocateBySignature(mode, effect.OperationId, effect.Owner, target,
                destination, relocated, events);
        }
    }

    private void ApplyRailLine(
        ArcRelayActorMatchModeDriver mode,
        ArcRelaySignatureRuntime.Effect.RailLine effect,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        if (!_lives.TryGetValue(effect.Owner, out LifeState? owner))
            return;
        ArcRelaySignatureDefinition.RailLine rule =
            (ArcRelaySignatureDefinition.RailLine)
                mode.Signatures.DefinitionFor(effect.Owner);
        var (dx, dy) = effect.Heading.Vector();
        for (int step = 1; step <= rule.Range; step++)
        {
            Position tile = owner.Position.Offset(dx * step, dy * step);
            if (_definition.Map.IsWall(tile))
                break;
            foreach (LifeState target in _lives.Values
                         .Where(value => value.Position == tile)
                         .OrderBy(value => value.ActorId)
                         .ToArray())
            {
                ApplySignatureDamage(mode, effect.OperationId, effect.Owner,
                    target, rule.Damage, events);
            }
        }
    }

    private void ApplySentinelFire(
        ArcRelayActorMatchModeDriver mode,
        ArcRelaySignatureRuntime.Effect.SentinelFire effect,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        ArcRelaySignatureDefinition.SentinelSeed rule =
            (ArcRelaySignatureDefinition.SentinelSeed)
                mode.Signatures.DefinitionFor(effect.Owner);
        if (_lives.TryGetValue(effect.Target, out LifeState? target)
            && target.ActorId.TeamId != effect.Owner.TeamId
            && target.Position.ChebyshevDistance(effect.Origin) <= rule.Range)
        {
            ApplySignatureDamage(mode, effect.OperationId, effect.Owner,
                target, rule.Damage, events);
        }
    }

    private void ApplySignatureRepair(
        ArcRelayActorMatchModeDriver mode,
        ArcRelaySignatureRuntime.Effect.Repair effect,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        if (!_lives.TryGetValue(effect.Target, out LifeState? target))
            return;
        int maximum = EffectiveMaxHealth(
            target.FormId,
            target.ActorId.TeamId,
            target.ActorId.UnitId);
        int amount = Math.Min(effect.Amount, maximum - target.Health);
        if (amount <= 0)
            return;
        target.Health += amount;
        ArcRelaySignatureDefinition signature =
            mode.Signatures.DefinitionFor(effect.Owner);
        EmitModeEvent(new GenericActorModeEvent(
            new GenericActorRuntimeObservation.EventPayload.ArcRelay(
                new ArcRelayEvent.SignatureRepair(
                    effect.OperationId,
                    signature.SignatureId,
                    effect.Owner,
                    target.ActorId,
                    amount,
                    target.Health,
                    target.Position)),
            target.Position), events);
    }

    private void ApplySignatureDamage(
        ArcRelayActorMatchModeDriver mode,
        string operationId,
        ActorIdentity owner,
        LifeState target,
        int amount,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        if (target.Health <= 0)
            return;
        target.Health = Math.Max(0, target.Health - amount);
        _arcSignatureDamagedThisTick.Add(target.ActorId);
        ArcRelaySignatureDefinition signature =
            mode.Signatures.DefinitionFor(owner);
        EmitModeEvent(new GenericActorModeEvent(
            new GenericActorRuntimeObservation.EventPayload.ArcRelay(
                new ArcRelayEvent.SignatureDamage(
                    operationId,
                    signature.SignatureId,
                    owner,
                    target.ActorId,
                    amount,
                    target.Health,
                    target.Position)),
            target.Position), events);
    }

    private void RelocateBySignature(
        ArcRelayActorMatchModeDriver mode,
        string operationId,
        ActorIdentity owner,
        LifeState target,
        Position destination,
        ImmutableArray<ActorIdentity>.Builder relocated,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        if (destination == target.Position
            || _definition.Map.IsWall(destination)
            || !mode.CanEnter(target.ActorId, destination)
            || _lives.Values.Any(value =>
                value.ActorId != target.ActorId
                && value.Position == destination))
            return;
        Position from = target.Position;
        target.Position = destination;
        relocated.Add(target.ActorId);
        EmitSignatureRelocation(mode, operationId, owner, target.ActorId,
            from, destination, events);
    }

    private void EmitSignatureRelocation(
        ArcRelayActorMatchModeDriver mode,
        string operationId,
        ActorIdentity owner,
        ActorIdentity target,
        Position from,
        Position to,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        ArcRelaySignatureDefinition signature =
            mode.Signatures.DefinitionFor(owner);
        EmitModeEvent(new GenericActorModeEvent(
            new GenericActorRuntimeObservation.EventPayload.ArcRelay(
                new ArcRelayEvent.BodyRelocated(
                    operationId,
                    signature.SignatureId,
                    owner,
                    target,
                    from,
                    to)),
            to), events);
    }

    private Position FurthestLegalSignatureTile(
        ActorIdentity actorId,
        Position source,
        ProjectileHeading heading,
        int range)
    {
        var (dx, dy) = heading.Vector();
        Position current = source;
        for (int step = 0; step < range; step++)
        {
            Position next = current.Offset(dx, dy);
            if (_definition.Map.IsWall(next)
                || _mode is ArcRelayActorMatchModeDriver arcRelay
                    && !arcRelay.CanEnter(actorId, next)
                || _lives.Values.Any(value =>
                    value.ActorId != actorId && value.Position == next))
            {
                break;
            }
            current = next;
        }
        return current;
    }

    private static bool IsFallingStarTile(Position value, Position centre) =>
        value == centre
        || Math.Abs(value.X - centre.X)
            + Math.Abs(value.Y - centre.Y) == 1;

    private sealed record ArcSignatureApplication(
        ImmutableArray<ActorIdentity> RelocatedActors);

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
            life.PendingSameLifeTransitionReason =
                GenericActorRuntimeObservation.FormTransitionReason.Requested;
            events.Add(EmitSpatial(
                Tick,
                GenericActorRuntimeObservation.EventKind
                    .FormTransitionStarted,
                FormTransitionPayload(reservation),
                life.Position));
        }
    }

    /// <summary>
    /// The engine's own same-life cause: a form whose declared automatic
    /// return has reached its threshold begins that return with no action
    /// (<see cref="ActorAutomaticReturnTriggerDefinition"/>). It runs after
    /// every attack, advance, and guard return of this tick — so the counters
    /// are final — and before damage is applied, so a lethal hit cancels the
    /// windup through the ordinary destruction path exactly as it cancels a
    /// requested one. A life already leaving is left alone: an early exit
    /// below the threshold is the author's to make, and the return it is
    /// already serving cannot be started twice.
    /// </summary>
    private void StartAutomaticReturns(
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        foreach (LifeState life in _lives.Values
                     .Where(life =>
                         life.Health > 0
                         && life.PendingSameLifeTransition is null)
                     .OrderBy(life => life.ActorId)
                     .ToArray())
        {
            if (AutomaticReturnRoute(life.FormId) is not
                    { AutomaticReturn: { } trigger } route
                || AutomaticReturnCount(life, trigger) < trigger.Threshold)
            {
                continue;
            }

            var request = new ActorSameLifeTransitionRequest(
                life.ActorId,
                route.TransitionId,
                $"automatic-return:{Tick}:{life.ActorId.TeamId}:" +
                $"{life.ActorId.UnitId}:{life.ActorId.LifeId}:" +
                $"{route.TransitionId}");
            ActorSameLifeTransitionQueueOutcome outcome = _sameLife.Queue(
                Tick,
                request,
                SameLifeSnapshot(life));
            if (outcome.Reservation is not
                ActorSameLifeTransitionReservation reservation)
            {
                // A blocked queue (an illegal completion tile, say) does not
                // discharge the threshold: the counter still stands, so the
                // return is retried every tick until it takes. The budget is
                // a rule, and a rule cannot be waited out.
                continue;
            }

            life.PendingSameLifeTransition = reservation;
            life.PendingSameLifeTransitionReason =
                GenericActorRuntimeObservation.FormTransitionReason
                    .AutomaticThresholdReturn;
            events.Add(EmitSpatial(
                Tick,
                GenericActorRuntimeObservation.EventKind
                    .FormTransitionStarted,
                FormTransitionPayload(
                    reservation,
                    GenericActorRuntimeObservation.FormTransitionReason
                        .AutomaticThresholdReturn),
                life.Position));
        }
    }

    /// <summary>
    /// The one automatic-return route declared out of a form, or null. The
    /// rules validator has already refused a second one, so this is exact.
    /// </summary>
    private ActorFormTransitionDefinition? AutomaticReturnRoute(
        string formId) =>
        _definition.Rules.SameLifeTransitions
            .OfType<ActorFormTransitionDefinition>()
            .SingleOrDefault(transition =>
                transition.AutomaticReturn is not null
                && string.Equals(
                    transition.SourceFormId,
                    formId,
                    StringComparison.Ordinal));

    private static int AutomaticReturnCount(
        LifeState life,
        ActorAutomaticReturnTriggerDefinition trigger) =>
        trigger.Counter switch
        {
            ActorAutomaticReturnTriggerDefinition.AutomaticReturnCounterKind
                .AttacksIssuedSinceEnteringSourceForm =>
                life.AttacksIssuedInForm,
            ActorAutomaticReturnTriggerDefinition.AutomaticReturnCounterKind
                .ProjectilesDeflectedSinceEnteringSourceForm =>
                life.ProjectilesDeflectedInForm,
            _ => throw new NotSupportedException(
                "The automatic return counts an unsupported fact."),
        };

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
            // EnterForm, not an assignment: arriving in a form restarts its
            // automatic-return counters, so a second stance entry within one
            // life starts its budget over instead of returning on entry.
            life.EnterForm(state.FormId);
            life.Position = state.Position;
            life.Facing = state.Facing;
            life.Health = state.Health;
            life.Cooldown = state.Cooldown;
            life.Energy = state.Energy;
            GenericActorRuntimeObservation.FormTransitionReason reason =
                life.PendingSameLifeTransitionReason;
            life.PendingSameLifeTransition = null;
            life.PendingSameLifeTransitionReason =
                GenericActorRuntimeObservation.FormTransitionReason.Requested;
            life.HasPriorSameLifeTransition = true;
            ActorSameLifeTransitionDefinition completedRoute =
                SameLifeTransition(reservation);
            if (completedRoute.IrreversibleForLife)
            {
                life.IrreversibleReturnFormIds.Add(
                    reservation.SourceFormId);
            }
            if (completedRoute.CooldownTicks > 0)
            {
                _routeReadyAtTick[
                    (life.ActorId.TeamId, life.ActorId.UnitId,
                        completedRoute.TransitionId)] =
                    checked(Tick + completedRoute.CooldownTicks + 1);
            }
            events.Add(EmitSpatial(
                Tick,
                GenericActorRuntimeObservation.EventKind
                    .FormTransitionCompleted,
                FormTransitionPayload(reservation, reason),
                life.Position));
        }
    }

    private void AdvanceExistingProjectiles(
        ICollection<PendingDamageContact> contacts,
        ref int contactOrdinal,
        ICollection<PendingDeflection> deflections,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events,
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
                deflections,
                events,
                traversals,
                GenericActorProjectileTraversal.TraversalTrigger
                    .ScheduledAdvance);
        }
    }

    private void ResolveAttacks(
        IReadOnlyDictionary<ActorIdentity, ActionState> resolutions,
        ICollection<PendingDamageContact> contacts,
        ref int contactOrdinal,
        ICollection<PendingDeflection> deflections,
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
            ProjectileHeading resolvedHeading = ResolveLaunchHeading(
                shooter,
                profile,
                resolution.ValidatedAction);
            ShotProgram? program = ResolveShotProgram(
                profile,
                resolution.ValidatedAction);
            resolution.SuccessfulAttack = true;
            // The cast counter: one ACTION is one count whatever its declared
            // projectile count, so a three-bolt fan is one cast rather than
            // three (ActorAutomaticReturnTriggerDefinition).
            shooter.CountAttackIssued();

            // One successful attack action issues the profile's declared
            // projectile count. Each bolt is an ordinary projectile with its
            // own ID, Attack event, path, and traversal; the volley shape only
            // decides the headings and the launch order, and the IDs follow
            // that order contiguously (ActorAttackVolleyDefinition). The whole
            // fan's identities are reserved before any bolt flies: a bolt's
            // launch traversal can contact a projectile guard, and the
            // deflection mints the return's identity at contact — reserving
            // up front keeps the contract's contiguous-ascending-in-launch-
            // order promise true through a mid-fan deflection.
            ProjectileHeading[] headings =
                [.. VolleyHeadings(profile, resolvedHeading)];
            long firstProjectileId = _nextProjectileId;
            _nextProjectileId = checked(_nextProjectileId + headings.Length);
            for (int bolt = 0; bolt < headings.Length; bolt++)
            {
                ProjectileHeading heading = headings[bolt];
                // Effective gun reach is the profile's declared travel plus
                // whatever the mode currently adds to this body. The path and
                // the bolt's remaining distance are traced against the same
                // number, so a lengthened shot behaves exactly like a longer
                // declared gun rather than like a bolt that outlives its path.
                int extraTravel = _mode
                    .StatModifiersFor(shooter.ActorId)
                    .AttackTravelTilesDelta;
                ImmutableArray<Position> path = TraceProjectilePath(
                    shooter.Position,
                    heading,
                    profile,
                    program,
                    extraTravel);
                long projectileId = firstProjectileId + bolt;
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
                    path,
                    extraTravel);
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
                        ? checked(
                            profile.Projectile.MaxTravelTiles + extraTravel)
                        : profile.Projectile.LaunchTiles;
                TraverseProjectile(
                    projectile,
                    launchTraversal,
                    contacts,
                    ref contactOrdinal,
                    deflections,
                    events,
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
    }

    /// <summary>
    /// The exact launch headings for one attack, in launch (and therefore
    /// projectile-ID) order. A profile without a volley yields exactly the
    /// resolved heading, so every historical contract is unchanged.
    /// </summary>
    internal static IEnumerable<ProjectileHeading> VolleyHeadings(
        ActorAttackProfileDefinition profile,
        ProjectileHeading resolvedHeading)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Volley is not { } volley)
            return [resolvedHeading];
        return volley.Spread switch
        {
            ActorAttackVolleyDefinition.VolleySpreadKind
                .SharedResolvedHeading =>
                Enumerable.Repeat(
                    resolvedHeading,
                    volley.ProjectileCount),
            ActorAttackVolleyDefinition.VolleySpreadKind
                .SymmetricAdjacentHeadingFanAscendingSignedSectorOffset =>
                Enumerable
                    .Range(
                        -volley.FanHalfWidthSectors,
                        volley.ProjectileCount)
                    .Select(offset => resolvedHeading.Turned(offset)),
            _ => throw new InvalidOperationException(
                "Unknown attack volley spread."),
        };
    }

    private void TraverseProjectile(
        ProjectileState projectile,
        int maximumTiles,
        ICollection<PendingDamageContact> contacts,
        ref int contactOrdinal,
        ICollection<PendingDeflection> deflections,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events,
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
            if (_mode is ArcRelayActorMatchModeDriver arcRelay
                && arcRelay.Signatures.TryConsumeProjectile(
                    projectile.Position,
                    projectile.OwnerTeamId,
                    Tick,
                    out GenericActorModeEvent? constructContact))
            {
                EmitModeEvent(constructContact!, events);
                projectile.Consumed = true;
                _projectiles.Remove(projectile);
                terminal = new GenericActorProjectileTraversal
                    .TerminalDisposition.WallOrPathExhausted();
                break;
            }
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
                    ProjectileDamage(
                        projectile,
                        target,
                        events),
                    contactOrdinal++));
            }
            else if (contact.Deflected)
            {
                Deflect(projectile, target, events, deflections);
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

    private int ProjectileDamage(
        ProjectileState projectile,
        LifeState target,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        int damage = projectile.Profile.Projectile.DamagePerHit;
        if (_mode is not ArcRelayActorMatchModeDriver arcRelay)
            return damage;
        int bonus = arcRelay.Signatures.TargetPaintBonus(
            projectile.OwnerActorId,
            target.ActorId,
            Tick,
            out string? operationId);
        if (bonus == 0 || operationId is null)
            return damage;
        GenericActorModeEvent? modeEvent =
            arcRelay.Signatures.ConsumeTargetPaint(operationId);
        if (modeEvent is not null)
            EmitModeEvent(modeEvent, events);
        return checked(damage + bonus);
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
                    destroyed,
                    target.Position));
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

    /// <summary>
    /// Retires every body that reached zero health, and reports each one with
    /// the tile it died on. A mode that places anything at a death site — a
    /// wreck, for the scrap economy — needs the destruction rather than the
    /// contact that caused it, because the contact names where the bolt was.
    /// </summary>
    private ImmutableArray<FrontlineScrapDestruction> FinalizeDestroyedLives(
        IReadOnlySet<int> newlyDisqualified,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        var destroyed =
            ImmutableArray.CreateBuilder<FrontlineScrapDestruction>();
        foreach (LifeState life in _lives.Values
                     .Where(life => life.Health == 0)
                     .OrderBy(life => life.ActorId)
                     .ToArray())
        {
            if (newlyDisqualified.Contains(life.ParticipantId))
                continue;
            destroyed.Add(new FrontlineScrapDestruction(
                life.ActorId,
                life.Position));
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
        ScheduleRootFactorySeeds();
        return destroyed.ToImmutable();
    }

    /// <summary>
    /// Starts the root factory's clock for any participant this tick left
    /// with no live body at all.
    /// <para>It uses the ordinary destruction grammar —
    /// <c>tick + 1 + profile delay</c>, the class's own respawn delay — so a
    /// wiped participant waits exactly as long for its bootstrap body as an
    /// ordinary body waits to be re-placeable. Nothing is scheduled while any
    /// life-creating clock is still running, because a participant that is
    /// merely between bodies is not wiped.</para>
    /// </summary>
    private void ScheduleRootFactorySeeds()
    {
        foreach (int participantId in _participantTeams.Keys.Order())
        {
            if (RootFactorySlot(participantId) is not SlotState slot)
                continue;
            if (_rootFactoryDueTick.ContainsKey(participantId))
                continue;
            int dueTick = checked(
                Tick
                + 1
                + _lifecycleProfiles[slot.Assignment.LifecycleProfileId]
                    .DelayTicks);
            // The seed rides the slot's OWN availability clock rather than a
            // second one beside it: a bootstrapped slot goes from pending
            // straight to live, which is the shape an automatic activation
            // already has and the shape the chronology already validates.
            slot.Kind = SlotKind.AvailabilityPending;
            slot.DueTick = dueTick;
            slot.PendingReason = GenericActorRuntimeObservation
                .AvailabilityReason.DestructionRecovery;
            _rootFactoryDueTick[participantId] = dueTick;
        }
    }

    /// <summary>
    /// The slot the root factory would seed for one participant, or null when
    /// the participant needs no bootstrap this tick — it still holds a body,
    /// a life-creating clock is already running, or its ruleset declares no
    /// root factory at all.
    /// <para>The seeded slot is the LOWEST-numbered one that owns a home
    /// spawn, which under prime dissolution is the slot the authored
    /// PrimeSpawn pad now reserves as an ordinary home spawn.</para>
    /// </summary>
    private SlotState? RootFactorySlot(int participantId)
    {
        if (_lives.Values.Any(life => life.ParticipantId == participantId))
            return null;
        // A disqualified participant is out of the match, not merely wiped.
        // Its base seeds nothing: the bootstrap answers a total body loss, and
        // a disqualification is a loss of the PLAYER.
        if (_host.ParticipantStatuses.Any(status =>
                status.ParticipantId == participantId
                && status.Disqualified))
        {
            return null;
        }
        SlotState[] owned =
            [
                .. _slots.Values
                    .Where(slot => slot.ParticipantId == participantId)
                    .OrderBy(slot => slot.UnitId),
            ];
        // A pending automatic return or activation is a body already on its
        // way; the base does not seed against one.
        if (owned.Any(slot =>
                slot.Kind is SlotKind.AutomaticReturnPending
                || slot.Kind == SlotKind.AvailabilityPending
                    && slot.Assignment.InitialAvailability
                    == ActorUnitSlotLifecycleAssignmentDefinition
                        .InitialAvailabilityKind
                        .DormantAutomaticActivationAtTick))
        {
            return null;
        }
        if (_fabricationReservations.Any(reservation =>
                reservation.ParticipantId == participantId))
        {
            return null;
        }
        return owned.FirstOrDefault(slot =>
            slot.Kind != SlotKind.PermanentlyDormant
            && slot.Assignment.AssignedRespawnSpawnId is not null
            && _lifecycleProfiles[slot.Assignment.LifecycleProfileId]
                .RootFactorySeedFormId is not null);
    }

    /// <summary>
    /// Seeds one body at the home spawn for every participant whose root
    /// factory is due this tick. It runs in the canonical returns/readiness
    /// phase, in participant order, exactly like every other tick-start life
    /// creation, and it costs the participant nothing: no action, no scrap, no
    /// slot beyond the one it fills.
    /// </summary>
    private void ApplyRootFactorySeeds(
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events,
        ImmutableArray<GenericActorLifeStart>.Builder lifeStarts,
        ImmutableArray<GenericActorProjectileTraversal>.Builder traversals)
    {
        foreach (int participantId in _rootFactoryDueTick.Keys.Order())
        {
            if (_rootFactoryDueTick[participantId] != Tick)
                continue;
            if (RootFactorySlot(participantId) is not SlotState slot
                || slot.Kind != SlotKind.AvailabilityPending
                || slot.DueTick != Tick)
            {
                // The participant recovered by some other route before the
                // base could seed it. The bootstrap is a floor, never a bonus.
                _rootFactoryDueTick.Remove(participantId);
                continue;
            }
            string formId =
                _lifecycleProfiles[slot.Assignment.LifecycleProfileId]
                    .RootFactorySeedFormId!;
            InitialSpawnDefinition spawn = _spawns[
                slot.Assignment.AssignedRespawnSpawnId!];
            ConsumeProjectilesAt(spawn.Position, traversals);
            LifeState life = CreateLife(
                slot,
                formId,
                // A base seed starts a fresh lineage: the structure is not a
                // parent, so there is no source generation to carry.
                0,
                spawn.Position,
                spawn.Facing,
                EffectiveMaxHealth(formId, slot.TeamId, slot.UnitId),
                GenericActorRuntimeStart.SpawnReason.RootFactorySeed,
                parentActorId: null,
                sourceTransitionId: null,
                sourceOperationId: null);
            lifeStarts.Add(life.LifeStart);
            ClearPendingClock(slot);
            _rootFactoryDueTick.Remove(participantId);
            events.Add(EmitSpatial(
                Tick,
                GenericActorRuntimeObservation.EventKind.LifeSpawned,
                SpawnPayload(life),
                life.Position));
        }
        // A participant that regained a body some other way keeps no stale
        // clock: the bootstrap only ever answers a total loss.
        foreach (int participantId in _rootFactoryDueTick.Keys.ToArray())
        {
            if (_lives.Values.Any(life =>
                    life.ParticipantId == participantId))
            {
                _rootFactoryDueTick.Remove(participantId);
            }
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
                // Historically an unarmed form keeps the remaining cooldown
                // as inert state — time stops for the gun. Under the
                // advances-with-time clock (#180) the cooldown keeps
                // running, so a stance or windup no longer pauses recovery.
                if (_definition.Rules.TickResolution.CooldownClock
                    == ActorTickResolutionDefinition.CooldownClockKind
                        .AdvancesWithTime)
                {
                    life.Cooldown = Math.Max(0, life.Cooldown - 1);
                }
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
        EmitModeEvents(modeTick.ModeEvents, events);
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

    private void EmitModeEvents(
        IEnumerable<GenericActorModeEvent> modeEvents,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        foreach (GenericActorModeEvent modeEvent in modeEvents)
            EmitModeEvent(modeEvent, events);
    }

    private void EmitModeEvent(
        GenericActorModeEvent modeEvent,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events)
    {
        events.Add(modeEvent.SpatialPosition is Position position
            ? EmitSpatial(
                Tick,
                GenericActorRuntimeObservation.EventKind.ArcRelay,
                modeEvent.Payload,
                position)
            : EmitPublic(
                Tick,
                GenericActorRuntimeObservation.EventKind.ArcRelay,
                modeEvent.Payload));
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
        // Resolved up front because the mode's body-scoped facts are stamped
        // onto self, ally, and enemy states as they are built.
        GenericActorModeProjection modeProjection =
            _mode.Project(ModeWorldView());
        GenericMindTeamProjection shared = SharedProjectionFor(
            observer,
            sourceEvents,
            modeProjection);

        ImmutableArray<GenericActorRuntimeObservation.ObservedAllyState>
            allies =
            _definition.Rules.TeamPerception.Kind
                == ActorTeamPerceptionDefinition.PerceptionKind.ImmediateUnion
                ? _lives.Values
                    .Where(life =>
                        life.ActorId.TeamId == observer.ActorId.TeamId
                        && life.ActorId != observer.ActorId)
                    .OrderBy(life => life.ActorId)
                    .Select(life => ProjectAlly(life, modeProjection))
                    .ToImmutableArray()
                : [];

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
                PendingObservation(observer))
            {
                ClassId = BodyClassId(
                    observer.ActorId.TeamId,
                    observer.ActorId.UnitId,
                    observer.ParticipantId),
                RouteCooldowns = LiveRouteCooldowns(
                    observer.ActorId.TeamId,
                    observer.ActorId.UnitId),
                CarriedScrap = modeProjection.CarriedScrapByActor
                    .GetValueOrDefault(observer.ActorId),
            },
            shared.TeamUnits,
            shared.Participants,
            allies,
            shared.Enemies,
            shared.VisibleTiles,
            shared.VisibleProjectiles,
            shared.VisibleEvents,
            shared.HeardSounds,
            shared.Scoreboard,
            shared.Mode,
            ActionLegalities(observer));
    }

    /// <summary>
    /// The observable union this observer reads, computed once per team per
    /// tick and reused by reference.
    /// <para>
    /// The memoization is only correct — and is exactly correct — under
    /// immediate-union perception, because that is the case in which the
    /// sensor set, the enemy set, the redaction audience and the event
    /// projection state are all functions of the TEAM rather than of the body.
    /// Under any other perception kind the union is genuinely per-observer and
    /// nothing is shared, which is also why the mind profile requires immediate
    /// union (see <c>ValidateWorldCapabilities</c>).
    /// </para>
    /// </summary>
    private GenericMindTeamProjection SharedProjectionFor(
        LifeState observer,
        ImmutableArray<GenericActorAuthoritativeEvent> sourceEvents,
        GenericActorModeProjection modeProjection)
    {
        int teamId = observer.ActorId.TeamId;
        if (_definition.Rules.TeamPerception.Kind
            != ActorTeamPerceptionDefinition.PerceptionKind.ImmediateUnion)
        {
            return ProjectPerceptionUnion(
                teamId,
                [observer],
                EventProjectionFor(observer),
                sourceEvents,
                modeProjection);
        }
        if (!_teamProjectionCache.TryGetValue(
                teamId,
                out GenericMindTeamProjection? cached))
        {
            cached = ProjectPerceptionUnion(
                teamId,
                SensorsFor(observer),
                EventProjectionFor(observer),
                sourceEvents,
                modeProjection);
            _teamProjectionCache.Add(teamId, cached);
        }
        return cached;
    }

    /// <summary>
    /// The union for one team, independent of any observer. Used by the mind
    /// projection, which has no "self" to key on.
    /// </summary>
    private GenericMindTeamProjection SharedProjectionForTeam(
        int teamId,
        ImmutableArray<GenericActorAuthoritativeEvent> sourceEvents,
        GenericActorModeProjection modeProjection)
    {
        if (!_teamProjectionCache.TryGetValue(
                teamId,
                out GenericMindTeamProjection? cached))
        {
            cached = ProjectPerceptionUnion(
                teamId,
                [
                    .. _lives.Values
                        .Where(life => life.ActorId.TeamId == teamId)
                        .OrderBy(life => life.ActorId),
                ],
                TeamEventProjection(teamId),
                sourceEvents,
                modeProjection);
            _teamProjectionCache.Add(teamId, cached);
        }
        return cached;
    }

    /// <summary>
    /// Every body whose sensors contribute to one observer's picture. Under
    /// the shipped immediate-union perception that is the whole scoring team,
    /// which is precisely why the union is the same object for every body on
    /// it — the fact the mind profile finally exploits.
    /// </summary>
    private ImmutableArray<LifeState> SensorsFor(LifeState observer) =>
        _definition.Rules.TeamPerception.Kind
            == ActorTeamPerceptionDefinition.PerceptionKind.ImmediateUnion
            ? _lives.Values
                .Where(life =>
                    life.ActorId.TeamId == observer.ActorId.TeamId)
                .OrderBy(life => life.ActorId)
                .ToImmutableArray()
            : [observer];

    private GenericActorRuntimeObservation.ObservedAllyState ProjectAlly(
        LifeState life,
        GenericActorModeProjection modeProjection) =>
        new(
            life.ActorId,
            life.Generation,
            life.FormId,
            life.Position,
            life.Facing,
            life.Health,
            life.Cooldown,
            life.Energy,
            life.PreviousActionResolution,
            PendingObservation(life))
        {
            ClassId = BodyClassId(
                life.ActorId.TeamId,
                life.ActorId.UnitId,
                life.ParticipantId),
            RouteCooldowns = LiveRouteCooldowns(
                life.ActorId.TeamId,
                life.ActorId.UnitId),
            CarriedScrap = modeProjection.CarriedScrapByActor
                .GetValueOrDefault(life.ActorId),
            RoleTag = _roleTags.GetValueOrDefault(life.ActorId),
        };

    /// <summary>
    /// The observable union for one audience: visible tiles with provenance,
    /// visible enemies, visible projectiles, redacted events, heard sounds,
    /// the team's slot table, participant statuses, the scoreboard and the
    /// mode state.
    /// <para>
    /// This is the single most expensive computation in a tick —
    /// <c>VisibleTilesFor</c> scans the map per sensor, <c>ObserversAt</c> runs
    /// per tile per sensor, and <c>SpawnReservationAt</c> runs a
    /// <c>SingleOrDefault</c> over the reservation lists per visible tile — and
    /// under the per-life profile it is executed once per LIFE with a
    /// byte-identical result each time. The mind profile calls it once per TEAM
    /// per tick, turning <c>O(N^2 x mapArea)</c> into <c>O(N x mapArea)</c>. It
    /// is extracted rather than duplicated so the two profiles cannot drift:
    /// the null pin compares two drivers, not two implementations.
    /// </para>
    /// </summary>
    private GenericMindTeamProjection ProjectPerceptionUnion(
        int observingTeamId,
        ImmutableArray<LifeState> sensors,
        EventProjectionState eventProjection,
        ImmutableArray<GenericActorAuthoritativeEvent> sourceEvents,
        GenericActorModeProjection modeProjection)
    {
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
                        item.observedBy)
                    {
                        SpawnReservation = SpawnReservationAt(item.position),
                    })
                .ToImmutableArray();

        ImmutableArray<GenericActorRuntimeObservation.ObservedEnemyState>
            enemies = _lives.Values
                .Where(life => life.ActorId.TeamId != observingTeamId)
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
                        item.observedBy)
                    {
                        ClassId = BodyClassId(
                            item.life.ActorId.TeamId,
                            item.life.ActorId.UnitId,
                            item.life.ParticipantId),
                        CarriedScrap = modeProjection.CarriedScrapByActor
                            .GetValueOrDefault(item.life.ActorId),
                        // Public on purpose (§12.2): this game telegraphs
                        // banks, tiers, claims, holds and death sites with no
                        // visibility requirement at all, so a visible body's
                        // declared job is a smaller leak than any of those --
                        // and it is what makes the set-piece legible to a
                        // spectator watching both sides' assignments.
                        RoleTag = _roleTags.GetValueOrDefault(
                            item.life.ActorId),
                    })
                .ToImmutableArray();
        HashSet<ActorIdentity> visibleEnemyIds =
            enemies.Select(enemy => enemy.ActorId).ToHashSet();

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
                                        == observingTeamId
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
                                item.observedBy,
                                // Both were already authoritative on the
                                // firing profile and both were unreadable
                                // from an observation: the wave-2 forensics
                                // asked "should I eat this?" and could answer
                                // neither how fast the bolt closes nor what
                                // it costs. A volley bolt and a mobile bolt
                                // can differ in both, so they are per
                                // projectile rather than per contract.
                                item.projectile.Profile.Projectile
                                    .TicksPerAdvance,
                                item.projectile.Profile.Projectile
                                    .DamagePerHit))
                    .ToImmutableArray();

        var visibleEvents =
            ImmutableArray.CreateBuilder<
                GenericActorRuntimeObservation.ObservedEvent>();
        var heardSounds =
            ImmutableArray.CreateBuilder<
                GenericActorRuntimeObservation.ObservedSound>();
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
                    teamPrivate.TeamId == observingTeamId,
                GenericActorAuthoritativeEvent.Audience.Spatial spatial =>
                    ProjectSpatialEvent(
                        sourceEvent,
                        spatial.PrimaryPosition,
                        observingTeamId,
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

        return new GenericMindTeamProjection(
            observingTeamId,
            TeamUnitObservations(observingTeamId),
            _host.ParticipantStatuses,
            enemies,
            visibleTiles,
            projectiles,
            visibleEvents.ToImmutable(),
            sensors.All(sensor => VisionFor(sensor).HearingRadius == 0)
                ? null
                : heardSounds.ToImmutable(),
            modeProjection.Scoreboard,
            ProjectModeForTeam(
                modeProjection.Mode,
                observingTeamId,
                visibleTiles.Select(value => value.Position).ToHashSet()));
    }

    private GenericActorRuntimeObservation.ModeObservationState
        ProjectModeForTeam(
            GenericActorRuntimeObservation.ModeObservationState mode,
            int observingTeamId,
            IReadOnlySet<Position> visibleTiles)
    {
        if (mode is not GenericActorRuntimeObservation.ModeObservationState
                .ArcRelay arcRelay)
        {
            return mode;
        }
        int tripNodeRevealRange = ((ArcRelayGameModeDefinition)
                _definition.Rules.GameMode).Signatures
            .OfType<ArcRelaySignatureDefinition.TripNode>()
            .Single().RevealRange;
        Position[] ownBodies = _lives.Values
            .Where(value => value.ActorId.TeamId == observingTeamId)
            .Select(value => value.Position)
            .ToArray();

        return new GenericActorRuntimeObservation.ModeObservationState.ArcRelay(
            arcRelay.ModeId,
            arcRelay.Wells,
            arcRelay.Reactors,
            arcRelay.VisibleCores.Where(core =>
                    core.CarrierActorId?.TeamId == observingTeamId
                    || visibleTiles.Contains(core.Position))
                .ToImmutableArray(),
            arcRelay.VisibleSignatures.Where(signature =>
                    signature.OwnerTeamId == observingTeamId
                    // Tells are public because their only purpose is to offer
                    // deterministic counterplay before the effect resolves.
                    || signature.Phase
                        == ArcRelaySignatureState.SignaturePhase.Tell
                    || signature.Kind
                        == ArcRelaySignatureDefinition.SignatureKind.TripNode
                        && signature.Positions.Any(position =>
                            ownBodies.Any(body =>
                                body.ChebyshevDistance(position)
                                    <= tripNodeRevealRange))
                    || signature.Positions.Any(visibleTiles.Contains))
                .ToImmutableArray(),
            arcRelay.LatestPulseTeamId,
            arcRelay.LatestPulseTick);
    }

    /// <summary>
    /// One frozen observation per TICKING PARTICIPANT
    /// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §2.3, §2.7).
    /// <para>
    /// Every non-disqualified participant appears here on every tick, whether
    /// or not it owns a live body. A mind that went dark on a total body loss
    /// would lose the ability to plan the return — exactly the "real fun
    /// complexity" #190 asked for, especially under a home-walk respawn — would
    /// accumulate silent memory staleness, and would be blind during the very
    /// window its enemy-position beliefs decay fastest. It costs only the base
    /// fuel term, so it ticks.
    /// </para>
    /// </summary>
    private ImmutableArray<GenericMindRuntimeObservation>
        ProjectMindObservations(
            ImmutableArray<GenericActorAuthoritativeEvent> sourceEvents)
    {
        GenericActorModeProjection modeProjection =
            _mode.Project(ModeWorldView());
        var result =
            ImmutableArray.CreateBuilder<GenericMindRuntimeObservation>();
        foreach (int participantId in _host.TickingParticipantIds)
        {
            int teamId = _participantTeams[participantId];
            GenericMindTeamProjection shared = SharedProjectionForTeam(
                teamId,
                sourceEvents,
                modeProjection);
            result.Add(new GenericMindRuntimeObservation(
                _definition.CapabilityVersions.ObservationSchemaVersion,
                Tick,
                _host.MatchContractFingerprint,
                participantId,
                teamId,
                [
                    .. _lives.Values
                        .Where(life => life.ParticipantId == participantId)
                        .OrderBy(life => life.ActorId)
                        .Select(life => ProjectBody(life, modeProjection)),
                ],
                [
                    .. _slots.Values
                        .Where(slot => slot.ParticipantId == participantId)
                        .OrderBy(slot => slot.UnitId)
                        .Select(slot =>
                            new GenericMindRuntimeObservation.ObservedOwnSlot(
                                slot.TeamId,
                                slot.UnitId,
                                ProjectSlotState(slot),
                                SlotClassId(slot))),
                ],
                // Allied MINDS' bodies: the team's bodies this participant does
                // NOT command. Always empty in head-to-head and in FFA-N,
                // because there is one participant per scoring team; the 2v2
                // hook that makes the #190 rider structural rather than
                // documented.
                [
                    .. _lives.Values
                        .Where(life =>
                            life.ActorId.TeamId == teamId
                            && life.ParticipantId != participantId)
                        .OrderBy(life => life.ActorId)
                        .Select(life => ProjectAlly(life, modeProjection)),
                ],
                shared,
                // Reserved (§11.3): the engine writes the empty collection, so
                // the field is negotiated and the shape is fixed while nothing
                // executes.
                AlliedIntents: []));
        }

        // This tick's published positions become next tick's "previous". The
        // update happens exactly once per tick because PrepareTick memoizes.
        _positionsAtPreviousMindObservation = _lives.Values.ToImmutableDictionary(
            life => life.ActorId,
            life => life.Position);
        return result.ToImmutable();
    }

    private GenericMindRuntimeObservation.ObservedBodyState ProjectBody(
        LifeState life,
        GenericActorModeProjection modeProjection)
    {
        Position? previousPosition =
            _positionsAtPreviousMindObservation.TryGetValue(
                life.ActorId,
                out Position remembered)
                ? remembered
                : null;
        return new GenericMindRuntimeObservation.ObservedBodyState(
            life.ActorId,
            life.Generation,
            life.FormId,
            life.Position,
            life.Facing,
            life.Health,
            life.Cooldown,
            life.Energy,
            life.PreviousActionResolution,
            PendingObservation(life),
            previousPosition,
            // The wave-8 ask, published rather than reconstructed. A body with
            // no previous position is new this tick, and a new body has not
            // moved — the same rule an author had to derive and could get
            // silently wrong.
            previousPosition is Position previous
                && previous != life.Position,
            life.SpawnedAtTick,
            life.LifeStart.Origin,
            ActionLegalities(life))
        {
            ClassId = BodyClassId(
                life.ActorId.TeamId,
                life.ActorId.UnitId,
                life.ParticipantId),
            RouteCooldowns = LiveRouteCooldowns(
                life.ActorId.TeamId,
                life.ActorId.UnitId),
            CarriedScrap = modeProjection.CarriedScrapByActor
                .GetValueOrDefault(life.ActorId),
            RoleTag = _roleTags.GetValueOrDefault(life.ActorId),
            // P3's null-pin fix: the EXACT seed the per-life profile would
            // have handed this life, so a wrapped bot drawing from
            // context.Random reproduces its per-life behaviour rather than
            // merely resembling it.
            BodyRandomSeed = life.LifeStart.ActorRandomSeed,
        };
    }

    /// <summary>
    /// The chassis one BODY carries. Under a mixed composition that is the
    /// SLOT's declared chassis, not the participant's composition token — a
    /// warden's slot-3 body is a fabricator and says so, which is what makes
    /// "condition on the enemy's stats and routes" still work when an army is
    /// not one thing.
    /// <para>On every mono cell the slot declares nothing and this falls
    /// through to the participant's own ID, so nothing an existing bot reads
    /// changes.</para>
    /// </summary>
    private string? BodyClassId(int teamId, int unitId, int participantId) =>
        _slotClassIds.GetValueOrDefault((teamId, unitId))
        ?? _participantClassIds[participantId];

    private string? SlotClassId(SlotState slot) =>
        _definition.Topology.UnitSlots
            .Single(value =>
                value.TeamId == slot.TeamId
                && value.UnitId == slot.UnitId)
            .ClassId;

    private GenericActorRuntimeObservation.SpawnReservation?
        SpawnReservationAt(Position position)
    {
        BoundedChildFabricationProvisionalReservation? fabrication =
            _fabricationReservations.SingleOrDefault(reservation =>
                reservation.ReservedPosition == position);
        if (fabrication is not null)
        {
            return new GenericActorRuntimeObservation.SpawnReservation(
                fabrication.TargetTeamId,
                fabrication.TargetUnitId,
                GenericActorRuntimeObservation.SpawnReservationKind
                    .Fabrication,
                fabrication.DueTick);
        }

        SplitReplicationReservedDescendant? descendant =
            _splitReservations
                .SelectMany(reservation =>
                    reservation.Descendants.Select(value =>
                        (Reservation: reservation, Descendant: value)))
                .Where(item => item.Descendant.Position == position)
                .Select(item => item.Descendant)
                .SingleOrDefault();
        if (descendant is not null)
        {
            SplitReplicationReservation reservation = _splitReservations
                .Single(value => value.Descendants.Contains(descendant));
            return new GenericActorRuntimeObservation.SpawnReservation(
                descendant.TeamId,
                descendant.UnitId,
                GenericActorRuntimeObservation.SpawnReservationKind
                    .Replication,
                reservation.DueTick);
        }

        SlotState? returnSlot = _slots.Values
            .Where(slot =>
                _lifecycleProfiles[slot.Assignment.LifecycleProfileId]
                    .DestructionPolicy
                == ActorLifecycleProfileDefinition.DestructionPolicyKind
                    .AutomaticRespawn)
            .SingleOrDefault(slot =>
                _spawns[slot.Assignment.AssignedRespawnSpawnId!].Position
                    == position);
        return returnSlot is null
            ? null
            : new GenericActorRuntimeObservation.SpawnReservation(
                returnSlot.TeamId,
                returnSlot.UnitId,
                GenericActorRuntimeObservation.SpawnReservationKind
                    .AutomaticReturn,
                DueTick: null);
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

    private EventProjectionState EventProjectionFor(LifeState observer) =>
        EventProjectionFor(
            _definition.Rules.TeamPerception.Kind
                == ActorTeamPerceptionDefinition.PerceptionKind.ImmediateUnion
                ? new ObservationAudienceKey(
                    observer.ActorId.TeamId,
                    ActorId: null)
                : new ObservationAudienceKey(
                    observer.ActorId.TeamId,
                    observer.ActorId));

    /// <summary>
    /// The team's projected-event identity state. Under immediate union this
    /// is the same object every body on the team reads, so a mind and a
    /// per-life body see identical event handles and ordinals — which is what
    /// keeps the two profiles' event streams comparable.
    /// </summary>
    private EventProjectionState TeamEventProjection(int teamId) =>
        EventProjectionFor(new ObservationAudienceKey(teamId, ActorId: null));

    private EventProjectionState EventProjectionFor(
        ObservationAudienceKey audience)
    {
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
            ActorActionKind.Rotation => true,
            ActorActionKind.Movement =>
                _mode is not ArcRelayActorMatchModeDriver movementMode
                || movementMode.CanCarrierRelocate(life.ActorId, Tick),
            ActorActionKind.Attack =>
                AttackFor(life) is ActorAttackProfileDefinition attack
                && (_mode is not ArcRelayActorMatchModeDriver arcRelay
                    || !arcRelay.CarriesCore(life.ActorId))
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
                    .Any(transition =>
                        !RouteOnCooldown(life, transition)
                        && _sameLife.CanQueue(
                            SameLifeSnapshot(life),
                            transition.TransitionId)),
            // Available exactly when the mode's store would accept SOMETHING
            // right now. Which tracks is the constraint's job, so a bot that
            // reads its mask never prices the ladder itself.
            ActorActionKind.ModeInvestment =>
                _mode.InvestableTracks(life.ActorId.TeamId).Count > 0,
            ActorActionKind.Objective =>
                _mode is ArcRelayActorMatchModeDriver objectiveMode
                && ArcRelayObjectiveAvailable(objectiveMode, life, action),
            ActorActionKind.Signature =>
                _mode is ArcRelayActorMatchModeDriver signatureMode
                && ArcRelaySignatureAvailable(signatureMode, life, action),
            _ => false,
        };
    }

    /// <summary>
    /// The track a mode-investment names, or null when it named none.
    /// </summary>
    private static string? InvestedTrack(
        GenericActorRuntimeActionResolution.ResolvedAction action) =>
        action.Arguments
            .OfType<GenericActorRuntimeActionArgument.UpgradeTrackArgument>()
            .SingleOrDefault()
            ?.TrackId;

    private bool ArcRelayObjectiveAvailable(
        ArcRelayActorMatchModeDriver mode,
        LifeState life,
        ActorActionDefinition action) =>
        string.Equals(
            action.Id,
            ArcRelayActionIds.DropCore,
            StringComparison.Ordinal)
            ? mode.CarriesCore(life.ActorId)
            : string.Equals(
                action.Id,
                ArcRelayActionIds.HandoffCore,
                StringComparison.Ordinal)
              && mode.CarriesCore(life.ActorId)
              && mode.CanCarrierRelocate(life.ActorId, Tick)
              && ArcRelayHandoffTargets(life).Length > 0;

    private bool ArcRelaySignatureAvailable(
        ArcRelayActorMatchModeDriver mode,
        LifeState life,
        ActorActionDefinition action)
    {
        if (!mode.Signatures.CanStart(
                life.ActorId,
                action.Id,
                Tick,
                life.Position))
        {
            return false;
        }
        ArcRelaySignatureDefinition signature =
            mode.Signatures.DefinitionForAction(action.Id);
        if (signature is ArcRelaySignatureDefinition.ArcToss
            && (!mode.CarriesCore(life.ActorId)
                || !mode.CanCarrierRelocate(life.ActorId, Tick)))
        {
            return false;
        }
        return action.ParameterKinds.All(kind => kind switch
        {
            ActorActionParameterKind.PositionTarget =>
                ArcRelaySignaturePositionTargets(mode, life).Length > 0,
            ActorActionParameterKind.UnitTarget =>
                ArcRelaySignatureUnitTargets(mode, life).Length > 0,
            _ => true,
        });
    }

    private ImmutableArray<Position> ArcRelaySignaturePositionTargets(
        ArcRelayActorMatchModeDriver mode,
        LifeState life) =>
        mode.Signatures.PositionTargets(
            life.ActorId,
            life.Position,
            VisibleTilesFor(life),
            ArcRelaySignatureLives(),
            mode.CarriesCore(life.ActorId));

    private ImmutableArray<GenericActorRuntimeActionArgument.UnitTarget>
        ArcRelaySignatureUnitTargets(
            ArcRelayActorMatchModeDriver mode,
            LifeState life) =>
        mode.Signatures.UnitTargets(
            life.ActorId,
            life.Position,
            VisibleTilesFor(life),
            ArcRelaySignatureLives());

    private ImmutableArray<ArcRelaySignatureRuntime.Life>
        ArcRelaySignatureLives() =>
        _lives.Values.OrderBy(value => value.ActorId)
            .Select(value => new ArcRelaySignatureRuntime.Life(
                value.ActorId,
                value.Position,
                value.Health,
                EffectiveMaxHealth(
                    value.FormId,
                    value.ActorId.TeamId,
                    value.ActorId.UnitId)))
            .ToImmutableArray();

    private ImmutableArray<GenericActorRuntimeActionArgument.UnitTarget>
        ArcRelayHandoffTargets(LifeState source) =>
        _lives.Values
            .Where(target =>
                target.ActorId.TeamId == source.ActorId.TeamId
                && target.ActorId != source.ActorId
                && source.Position.ChebyshevDistance(target.Position) == 1
                && _mode is ArcRelayActorMatchModeDriver mode
                && !mode.CarriesCore(target.ActorId))
            .OrderBy(target => target.ActorId)
            .Select(target => new GenericActorRuntimeActionArgument.UnitTarget(
                target.ActorId.TeamId,
                target.ActorId.UnitId))
            .ToImmutableArray();

    /// <summary>
    /// Settles this tick's mode-store purchases in canonical
    /// <c>(teamId, unitId, lifeId)</c> order. Two teammates investing against
    /// a bank that covers only one leave the second Blocked, which costs it
    /// its action exactly as any other blocked verb does.
    /// </summary>
    private void ResolveInvestments(
        IReadOnlyDictionary<ActorIdentity, ActionState> resolutions)
    {
        foreach (ActionState resolution in resolutions.Values
                     .OrderBy(value => value.ActorId))
        {
            ActorActionDefinition action =
                _actions[resolution.ValidatedAction.ActionId];
            if (resolution.Outcome
                    != GenericActorRuntimeActionResolution.ActionOutcome
                        .Success
                || action.Kind != ActorActionKind.ModeInvestment)
            {
                continue;
            }
            if (InvestedTrack(resolution.ValidatedAction) is not string track
                || !_mode.TryInvest(Tick, resolution.ActorId, track))
            {
                Block(resolution);
            }
        }
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
                            AllowedDirections(life, action)),
                ActorActionParameterKind.UnitTarget =>
                    new GenericActorRuntimeActionLegality.ArgumentConstraint
                        .UnitTargetConstraint(
                            action.Kind == ActorActionKind.Fabrication
                                ? FabricationTargets(life, action)
                                : action.Kind == ActorActionKind.Signature
                                  && _mode
                                      is ArcRelayActorMatchModeDriver
                                          signatureMode
                                    ? ArcRelaySignatureUnitTargets(
                                        signatureMode,
                                        life)
                                : action.Kind == ActorActionKind.Objective
                                    ? ArcRelayHandoffTargets(life)
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
                            action.Kind == ActorActionKind.Movement
                                && MovementFor(life).FacingCoupling
                                    == ActorMovementFacingCoupling.FacingLocked
                                ? [life.Facing.ToProjectileHeading()]
                                : Enum.GetValues<ProjectileHeading>()
                                    .ToImmutableArray()),
                // Affordability lives in the mask, not in the bot: a track is
                // offered only when the team's bank covers its next tier and
                // no cap forbids it. The mask is a SET in canonical ordinal
                // order, like every other enumerated constraint; the
                // contract's DECLARED track order is the separate thing tier
                // vectors are published positionally against.
                ActorActionParameterKind.UpgradeTrack =>
                    new GenericActorRuntimeActionLegality.ArgumentConstraint
                        .UpgradeTrackConstraint(
                            [
                                .. _mode
                                    .InvestableTracks(life.ActorId.TeamId)
                                    .Order(StringComparer.Ordinal),
                            ]),
                ActorActionParameterKind.PositionTarget =>
                    new GenericActorRuntimeActionLegality.ArgumentConstraint
                        .PositionTargetConstraint(
                            action.Kind == ActorActionKind.Signature
                            && _mode is ArcRelayActorMatchModeDriver
                                signatureMode
                                ? ArcRelaySignaturePositionTargets(
                                    signatureMode,
                                    life)
                                : []),
                _ => throw new InvalidOperationException(
                    "Unknown actor action parameter kind."),
            });
        }
        return constraints.ToImmutable();
    }

    /// <summary>
    /// Publishes the Direction domain for one action. A FacingLocked mover
    /// may only step where it already looks, so the movement mask offers
    /// exactly its current facing; Rotation keeps all four cardinals under
    /// every coupling, and every other coupling keeps all four for movement
    /// too.
    /// </summary>
    private ImmutableArray<Direction> AllowedDirections(
        LifeState life,
        ActorActionDefinition action) =>
        action.Kind == ActorActionKind.Movement
        && MovementFor(life).FacingCoupling
            == ActorMovementFacingCoupling.FacingLocked
            ? [life.Facing]
            : Enum.GetValues<Direction>().ToImmutableArray();

    private HashSet<Position> VisibleTilesFor(LifeState sensor)
    {
        ActorVisionProfileDefinition vision = VisionFor(sensor);
        // Effective sight is the form's declared range plus whatever the mode
        // currently adds. The omnidirectional proximity radius is deliberately
        // NOT moved: the tier widens what a body sees at distance, it does not
        // reshape what it sees up close.
        int range = checked(
            vision.Range
            + _mode.StatModifiersFor(sensor.ActorId).VisionRangeDelta);
        var visible = new HashSet<Position>();
        foreach (Position target in AllMapPositions())
        {
            int distance = sensor.Position.ChebyshevDistance(target);
            if (distance > range)
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
            if (hasLineOfSight
                && distance > 1
                && _mode is ArcRelayActorMatchModeDriver arcRelay)
            {
                foreach (Position position in Visibility.SupercoverLine(
                             sensor.Position,
                             target))
                {
                    if (position == sensor.Position)
                        continue;
                    if (arcRelay.Signatures.IsSmokeAt(position, Tick)
                        && !arcRelay.Signatures.IsRevealedForTeam(
                            position,
                            sensor.ActorId.TeamId,
                            Tick))
                    {
                        hasLineOfSight = false;
                        break;
                    }
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

    /// <summary>
    /// The health one new life of a slot arrives with: the form's declared
    /// maximum plus whatever the mode currently adds to that slot. It is
    /// deliberately read only at SPAWN, which is what makes a purchased
    /// health tier raise the ceiling without healing anybody — a standing
    /// body keeps its exact current health, so buying mid-duel is never a
    /// rescue.
    /// </summary>
    private int EffectiveMaxHealth(string formId, int teamId, int unitId) =>
        checked(
            _forms[formId].MaxHealth
            // The modifier's scope is the stable SLOT, so the life half of the
            // identity is immaterial here and the not-yet-minted life is
            // probed as its slot.
            + _mode.StatModifiersFor(
                    ActorIdentity.FromTeamUnitLife(teamId, unitId, 0))
                .MaxHealthDelta);

    private ActorMovementProfileDefinition MovementFor(LifeState life) =>
        _movementProfiles[_forms[life.FormId].MovementProfileId];

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
        ShotProgram? program,
        int extraTravelTiles = 0) =>
        GenericActorProjectilePath.Trace(
            _definition.Map,
            origin,
            initialHeading,
            profile,
            program,
            extraTravelTiles);

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
        {
            // A hostile contact is the only one a projectile guard answers:
            // the shell turns incoming enemy fire, it does not deflect its own
            // team's bolts. Allied contacts fall through to the collision
            // contract below and never reach the guard at all.
            return DeflectsFrontalContact(target, projectile)
                ? ProjectileContact.Deflect
                : ProjectileContact.Damage;
        }
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

    /// <summary>
    /// Whether the target's form guard turns this hostile contact instead of
    /// taking damage from it. Contact puts both bodies on one tile, so the arc
    /// question is asked of the projectile's approach vector — the reverse of
    /// its travel heading — against the target's facing quadrant.
    /// </summary>
    private bool DeflectsFrontalContact(
        LifeState target,
        ProjectileState projectile)
    {
        if (_forms[target.FormId].ProjectileGuard
            != ActorFormProjectileGuardKind.FacingQuadrantContactsDeflected)
        {
            return false;
        }
        // A bolt returned on this very tick is still leaving the guard that
        // returned it and cannot be turned a second time before it has flown.
        // The rule exists so the cascade terminates by construction: two
        // shields facing each other one tile apart trade a hit instead of
        // volleying one bolt forever inside a single tick.
        if (projectile.ReturnedAtTick == Tick)
            return false;
        var (dx, dy) = projectile.Heading.Vector();
        return Visibility.InQuadrant(-dx, -dy, target.Facing);
    }

    /// <summary>
    /// Publishes one deflection and reserves the returned bolt's identity. The
    /// identity is allocated here, in contact order, so multiple deflections
    /// in one tick are ordered by the tick's deterministic contact sequence;
    /// the projectile itself is materialized later by
    /// <see cref="LaunchDeflectedProjectiles"/>.
    /// </summary>
    private void Deflect(
        ProjectileState projectile,
        LifeState target,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events,
        ICollection<PendingDeflection> deflections)
    {
        long deflectedId = checked(_nextProjectileId++);
        // The shield-break counter, counted where the deflection is published
        // so that every deflection this tick — several may land at once — is
        // already in the total when the automatic return is evaluated.
        target.CountProjectileDeflected();
        events.Add(EmitSpatial(
            Tick,
            GenericActorRuntimeObservation.EventKind.ProjectileDeflected,
            new GenericActorRuntimeObservation.EventPayload
                .ProjectileDeflected(
                    projectile.OwnerTeamId,
                    projectile.OwnerActorId,
                    target.ActorId,
                    projectile.Id,
                    deflectedId,
                    target.FormId,
                    target.Facing,
                    projectile.Heading,
                    target.Position),
            target.Position));
        deflections.Add(new PendingDeflection(
            deflectedId,
            target,
            // The exactly reversed heading: the bolt retraces its approach.
            // The deflector never aims, so the locked arc chosen on entry is
            // the whole of its control over where the return goes.
            projectile.Heading.Reversed(),
            // The shooter's own bolt comes back: identical damage, speed, and
            // range class, with a fresh travel budget from the guard's tile.
            // The stance form declares no attack profile of its own, and a
            // deflection is not an attack action.
            projectile.Profile));
    }

    /// <summary>
    /// Materializes this tick's returned bolts. It runs once, after existing
    /// projectiles advanced and after attacks launched, so a return is exactly
    /// as kinematically ordinary as a freshly fired bolt: one launch traversal
    /// of the profile's launch tiles, then the ordinary advance cadence.
    ///
    /// The work list cannot grow while it drains, because a bolt returned this
    /// tick is not eligible to be returned again this tick (see
    /// <see cref="DeflectsFrontalContact"/>) — which is what makes two shields
    /// facing each other one tile apart resolve instead of volleying forever.
    /// </summary>
    private void LaunchDeflectedProjectiles(
        ICollection<PendingDeflection> deflections,
        ICollection<PendingDamageContact> contacts,
        ref int contactOrdinal,
        ImmutableArray<GenericActorAuthoritativeEvent>.Builder events,
        ImmutableArray<GenericActorProjectileTraversal>.Builder traversals)
    {
        // A return carries no aim of its own. Where the profile publishes a
        // shot program the world contract requires one to be present, so it
        // takes that profile's own default — which is the straight, bendless
        // program — and otherwise none at all.
        foreach (PendingDeflection deflection in deflections.ToArray())
        {
            ActorProjectileDefinition kinematics =
                deflection.Profile.Projectile;
            ShotProgram? program =
                deflection.Profile.ShotProgram.Enabled
                    ? new ShotProgram(
                        deflection.Profile.ShotProgram.DefaultProgram
                            .InitialAimOffset,
                        deflection.Profile.ShotProgram.DefaultProgram
                            .BendDirection,
                        deflection.Profile.ShotProgram.DefaultProgram
                            .BendAfterTiles,
                        deflection.Profile.ShotProgram.DefaultProgram
                            .BendEveryTiles,
                        deflection.Profile.ShotProgram.DefaultProgram
                            .BendCount)
                    : null;
            // A returned bolt keeps the SHOOTER's declared kinematics with no
            // ladder on top: the edge tier buys the mobile gun's range, not
            // the parry's, and the return flies the attacker's profile — a
            // deflector's mobile-gun tier grafted onto an enemy profile is
            // incoherent, and the chronology validator rightly demands the
            // raw fresh budget (wave-8 finding: the boosted return aborted
            // every shell-plus-edge match; no completed measurement ever
            // contained one).
            const int returnExtraTravel = 0;
            var returned = new ProjectileState(
                deflection.ProjectileId,
                deflection.Deflector.ParticipantId,
                deflection.Deflector.ActorId.TeamId,
                deflection.Deflector.ActorId,
                Tick,
                deflection.Deflector.Position,
                deflection.Heading,
                program,
                deflection.Profile,
                TraceProjectilePath(
                    deflection.Deflector.Position,
                    deflection.Heading,
                    deflection.Profile,
                    program,
                    returnExtraTravel),
                returnExtraTravel)
            {
                ReturnedAtTick = Tick,
            };
            TraverseProjectile(
                returned,
                kinematics.Mode == ActorProjectileMode.InstantRay
                    ? checked(kinematics.MaxTravelTiles + returnExtraTravel)
                    : kinematics.LaunchTiles,
                contacts,
                ref contactOrdinal,
                // A return cannot produce a further return this tick, so this
                // sink stays empty and the snapshot above is the whole work.
                deflections,
                events,
                traversals,
                GenericActorProjectileTraversal.TraversalTrigger
                    .GuardDeflection);
            if (!returned.Consumed
                && returned.RemainingTiles > 0
                && kinematics.Mode == ActorProjectileMode.Discrete)
            {
                _projectiles.Add(returned);
            }
        }
    }

    /// <summary>
    /// Where a due automatic return or activation for this slot lands. The
    /// assigned spawn stays the answer for every contract except the
    /// forward-rally placement, and stays the fallback under it, so its
    /// permanent reservation remains load-bearing.
    /// </summary>
    private Position ResolveAutomaticArrival(
        SlotState slot,
        InitialSpawnDefinition spawn)
    {
        if (!FrontlineForwardRallyPlacement.MayRallyForward(_definition))
            return spawn.Position;
        if (_mode.State is not GenericActorModeState.Frontline frontline)
            return spawn.Position;

        return FrontlineForwardRallyPlacement.Resolve(
            _definition,
            slot.TeamId,
            spawn.Position,
            frontline.Control.ActivePositionIndex,
            FrontlineForwardRallyPlacement.BlockedTiles(
                _lives.Values.Select(life => life.Position),
                [
                    .. _fabricationReservations.Select(reservation =>
                        reservation.ReservedPosition),
                    .. _splitReservations.SelectMany(reservation =>
                        reservation.Descendants.Select(
                            descendant => descendant.Position)),
                ],
                ReservedReturnSpawnPositions()),
            slot.Assignment,
            // The owner as it stands when this arrival lands, which is
            // exactly the owner the boundary before it published — so the
            // chronology validator re-derives the same tile from the
            // recorded observation rather than from private state.
            frontline.Control.SecondaryControl?.OwnerTeamId);
    }

    private IEnumerable<Position> ReservedReturnSpawnPositions() =>
        _slots.Values
            .Where(slot =>
                _lifecycleProfiles[slot.Assignment.LifecycleProfileId]
                    .DestructionPolicy
                == ActorLifecycleProfileDefinition.DestructionPolicyKind
                    .AutomaticRespawn)
            .Select(slot =>
                _spawns[slot.Assignment.AssignedRespawnSpawnId!].Position);

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
        GenericActorRuntimeObservation.FormTransitionReason reason =
            life.PendingSameLifeTransitionReason;
        life.PendingSameLifeTransition = null;
        life.PendingSameLifeTransitionReason =
            GenericActorRuntimeObservation.FormTransitionReason.Requested;
        events.Add(EmitSpatial(
            Tick,
            GenericActorRuntimeObservation.EventKind.FormTransitionCancelled,
            FormTransitionPayload(reservation, reason),
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
                    life.Health,
                    _positionsAtPreviousTickEnd.TryGetValue(
                        life.ActorId,
                        out Position previous)
                        ? previous
                        : null))
                .ToImmutableArray());

    /// <summary>
    /// Remembers where every surviving life stands at the end of a resolved
    /// tick, which is what the next tick's stillness reading compares
    /// against. A life created after this snapshot — a respawn, a
    /// fabrication, a Split descendant — is simply absent, which is exactly
    /// "no previous position", and a destroyed life drops out with its slot.
    /// </summary>
    private void RememberPositions() =>
        _positionsAtPreviousTickEnd = _lives.Values.ToImmutableDictionary(
            life => life.ActorId,
            life => life.Position);

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
            ActorSameLifeTransitionReservation reservation,
            GenericActorRuntimeObservation.FormTransitionReason reason =
                GenericActorRuntimeObservation.FormTransitionReason
                    .Requested) =>
        new(
            reservation.SourceActorId,
            reservation.TransitionId,
            reservation.OperationId,
            reservation.SourceFormId,
            reservation.TargetFormId,
            reservation.StartedTick,
            reservation.DueTick,
            reason);

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
        if (definition.CapabilityVersions.IsMindProfile
            && definition.Rules.TeamPerception.Kind
                != ActorTeamPerceptionDefinition.PerceptionKind
                    .ImmediateUnion)
        {
            // The mind receives its team's observable union ONCE. That is only
            // meaningful when perception is a team union in the first place —
            // under any per-body perception the "shared" projection would be a
            // fiction. Team perception is unchanged and stays team-scoped
            // (DESIGN-MIND-ARCHITECTURE §1.3); the mind profile simply
            // requires it.
            throw new NotSupportedException(
                "The mind profile requires immediate-union team perception.");
        }
        if (definition.CapabilityVersions.IsMindProfile
            && definition.Topology.UnitSlots.Any(slot =>
                slot.ClassId is not null
                && !GenericMindContractReservations
                    .RegisteredCompositionTokens
                    .Contains(slot.ClassId, StringComparer.Ordinal)
                && (definition.Rules.GameMode
                        is not ArcRelayGameModeDefinition arcRelay
                    || !arcRelay.Signatures.Any(signature => string.Equals(
                        signature.ClassId,
                        slot.ClassId,
                        StringComparison.Ordinal)))))
        {
            // A profile ID is a pre-registration in this project, and so is a
            // composition token. An unregistered chassis on a slot faults here
            // rather than travelling unlabelled into balance evidence (§9.5).
            throw new NotSupportedException(
                "Every per-slot chassis must name a registered composition chassis.");
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

        /// <summary>
        /// Why <see cref="PendingSameLifeTransition"/> exists. The stance
        /// return route serves both the author's early exit and the engine's
        /// threshold return, so the cause has to be remembered rather than
        /// re-derived, and every event about this instance repeats it.
        /// </summary>
        public GenericActorRuntimeObservation.FormTransitionReason
            PendingSameLifeTransitionReason
        { get; set; }

        /// <summary>
        /// Automatic-return counters, scoped to the current occupancy of this
        /// life's form. They live on the life, so nothing survives a respawn,
        /// and <see cref="EnterForm"/> clears them, so nothing survives a
        /// stance cycle either.
        /// </summary>
        public int AttacksIssuedInForm { get; private set; }

        public int ProjectilesDeflectedInForm { get; private set; }

        public void CountAttackIssued() => AttacksIssuedInForm++;

        public void CountProjectileDeflected() =>
            ProjectilesDeflectedInForm++;

        /// <summary>Adopts a new form and restarts its trigger counters.</summary>
        public void EnterForm(string formId)
        {
            FormId = formId;
            AttacksIssuedInForm = 0;
            ProjectilesDeflectedInForm = 0;
        }

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
            ImmutableArray<Position> path,
            int extraTravelTiles = 0)
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
            RemainingTiles = checked(
                profile.Projectile.MaxTravelTiles + extraTravelTiles);
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

        /// <summary>
        /// Set only on a bolt a projectile guard returned, to the tick it was
        /// returned on. Runtime-only bookkeeping: the world snapshot never
        /// carries it, because the chronology reads the same fact from the
        /// deflection event that names the bolt.
        /// </summary>
        public int? ReturnedAtTick { get; init; }
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
        bool Damages,
        bool Deflected = false)
    {
        public static ProjectileContact Pass => new(false, false);
        public static ProjectileContact Block => new(true, false);
        public static ProjectileContact Damage => new(true, true);

        /// <summary>
        /// Turned by a form's projectile guard: the incoming bolt is spent, no
        /// damage is scheduled, and a replacement bolt is launched back along
        /// the reversed heading under the guard's ownership.
        /// </summary>
        public static ProjectileContact Deflect =>
            new(true, false, Deflected: true);
    }

    /// <summary>
    /// One deflection accepted during this tick's contact resolution. The
    /// returned bolt's identity is allocated at contact time — contact order
    /// is therefore identity order — while the body is materialized in the
    /// tick's single launch phase, so a deflected bolt can never advance on
    /// the tick that created it (the invariant
    /// <see cref="ActorProjectileDefinition.AdvancesOnLaunchTick"/> already
    /// states for attack launches) and a deflection chain cannot recurse
    /// inside one tick.
    /// </summary>
    private sealed record PendingDeflection(
        long ProjectileId,
        LifeState Deflector,
        ProjectileHeading Heading,
        ActorAttackProfileDefinition Profile);
}
