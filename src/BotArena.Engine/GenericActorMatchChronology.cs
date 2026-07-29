using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Immutable mode-neutral match chronology. Partial is derived solely from
/// the absence of terminal facts.
/// </summary>
public sealed record GenericActorMatchChronology
{
    public GenericActorMatchChronology(
        GenericActorMatchDescriptor descriptor,
        GenericActorMatchInitialFrame initialFrame,
        IReadOnlyCollection<GenericActorMatchTickFrame> ticks,
        GenericActorMatchResult? result)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(initialFrame);
        ArgumentNullException.ThrowIfNull(ticks);

        GenericActorMatchTickFrame[] tickSnapshot = [.. ticks];
        if (tickSnapshot.Any(frame => frame is null))
        {
            throw new ArgumentException(
                "Chronology ticks cannot contain null frames.",
                nameof(ticks));
        }
        for (int index = 0; index < tickSnapshot.Length; index++)
        {
            if (tickSnapshot[index].Tick != index)
            {
                throw new ArgumentException(
                    "Chronology ticks must be contiguous from tick zero.",
                    nameof(ticks));
            }
        }

        ValidateRecordedEvidence(
            descriptor,
            initialFrame,
            tickSnapshot);
        ValidateLifeContinuity(
            descriptor,
            initialFrame,
            tickSnapshot);
        ValidateCausalBoundaries(
            descriptor,
            initialFrame,
            tickSnapshot);
        ValidateSkippedTerminalSameLifeWork(
            descriptor,
            initialFrame,
            tickSnapshot,
            result);
        if (descriptor.Definition.Rules.GameMode
            is FrontlineGameModeDefinition)
        {
            GenericFrontlineChronologyEvidence.Validate(
                descriptor.Definition,
                initialFrame,
                tickSnapshot,
                result);
        }
        ValidateFactChronology(initialFrame, tickSnapshot);
        if (result is not null)
        {
            ValidateResult(
                descriptor,
                initialFrame,
                tickSnapshot,
                result);
        }

        Descriptor = descriptor;
        InitialFrame = initialFrame;
        Ticks = tickSnapshot.ToImmutableArray();
        Result = result;
    }

    private static void ValidateLifeContinuity(
        GenericActorMatchDescriptor descriptor,
        GenericActorMatchInitialFrame initialFrame,
        IReadOnlyList<GenericActorMatchTickFrame> ticks)
    {
        Dictionary<(int TeamId, int UnitId), int> expectedNextLifeIds =
            descriptor.Definition.Topology.UnitSlots.ToDictionary(
                slot => (slot.TeamId, slot.UnitId),
                _ => 0);
        var recordedActors =
            new Dictionary<ActorIdentity, GenericActorLifeStart>();
        ActorIdentity[] expectedInitialActors = descriptor.Definition
            .Topology.InitialLives
            .Select(life => new ActorIdentity(
                life.TeamId,
                life.UnitId,
                life.LifeId))
            .Order()
            .ToArray();
        if (!initialFrame.LifeStarts
            .Select(start => start.ActorId)
            .Order()
            .SequenceEqual(expectedInitialActors))
        {
            throw new ArgumentException(
                "Initial life starts must exactly match the resolved topology.",
                nameof(initialFrame));
        }
        ApplyLifeStarts(
            initialFrame.LifeStarts,
            expectedNextLifeIds,
            recordedActors,
            nameof(initialFrame));
        ValidateNextLifeIds(
            initialFrame.State,
            expectedNextLifeIds,
            nameof(initialFrame));

        GenericActorWorldSnapshot previousState = initialFrame.State;
        foreach (GenericActorMatchTickFrame frame in ticks)
        {
            HashSet<ActorIdentity> previousActors = previousState.ActiveLives
                .Select(life => life.ActorId)
                .ToHashSet();
            ActorIdentity[] expected = frame.TickStart.State.ActiveLives
                .Where(life => !previousActors.Contains(life.ActorId))
                .Select(life => life.ActorId)
                .Order()
                .ToArray();
            ActorIdentity[] actual = frame.TickStart.LifeStarts
                .Select(start => start.ActorId)
                .Order()
                .ToArray();
            if (!actual.SequenceEqual(expected))
            {
                throw new ArgumentException(
                    "Tick-start life starts must cover exactly the lives created at that boundary.",
                    nameof(ticks));
            }
            ApplyLifeStarts(
                frame.TickStart.LifeStarts,
                expectedNextLifeIds,
                recordedActors,
                nameof(ticks));
            ValidateNextLifeIds(
                frame.TickStart.State,
                expectedNextLifeIds,
                nameof(ticks));
            ValidateNextLifeIds(
                frame.PostState,
                expectedNextLifeIds,
                nameof(ticks));

            previousState = frame.PostState;
        }
    }

    public GenericActorMatchDescriptor Descriptor { get; }
    public GenericActorMatchInitialFrame InitialFrame { get; }
    public ImmutableArray<GenericActorMatchTickFrame> Ticks { get; }
    public GenericActorMatchResult? Result { get; }
    public bool Partial => Result is null;

    private static void ValidateRecordedEvidence(
        GenericActorMatchDescriptor descriptor,
        GenericActorMatchInitialFrame initialFrame,
        IReadOnlyCollection<GenericActorMatchTickFrame> ticks)
    {
        initialFrame.ValidateAgainst(descriptor);
        IEnumerable<GenericActorWorldSnapshot> states =
        [
            initialFrame.State,
            .. ticks.Select(frame => frame.TickStart.State),
            .. ticks.Select(frame => frame.PostState),
        ];
        if (states.Any(state => !string.Equals(
                state.MatchContractFingerprint,
                descriptor.MatchContractFingerprint,
                StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Every world snapshot must reference the chronology descriptor's exact match contract.",
                nameof(ticks));
        }

        IEnumerable<GenericActorLifeStart> starts =
            initialFrame.LifeStarts.Concat(
                ticks.SelectMany(frame => frame.TickStart.LifeStarts));
        foreach (GenericActorLifeStart start in starts)
        {
            start.ValidateAgainst(descriptor);
        }

        foreach (GenericActorMatchActorTurn turn in
                 ticks.SelectMany(frame => frame.ActorTurns))
        {
            if (!string.Equals(
                    turn.Observation.MatchContractFingerprint,
                    descriptor.MatchContractFingerprint,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Every recorded observation must reference the chronology descriptor's exact contract.",
                    nameof(ticks));
            }
            turn.ValidateAgainst(descriptor);
        }
    }

    private static void ValidateFactChronology(
        GenericActorMatchInitialFrame initialFrame,
        IReadOnlyList<GenericActorMatchTickFrame> ticks)
    {
        GenericActorAuthoritativeEvent[] events =
        [
            .. initialFrame.Events,
            .. ticks.SelectMany(frame => frame.TickStart.Events),
            .. ticks.SelectMany(frame => frame.Events),
        ];
        if (events.Select(item => item.EventHandle)
                .Distinct(StringComparer.Ordinal).Count() != events.Length)
        {
            throw new ArgumentException(
                "Authoritative event handles must be globally unique.",
                nameof(ticks));
        }

        var chronologicalOrdinals = new List<long>();
        var chronologicalEvents =
            new List<GenericActorAuthoritativeEvent>();
        AppendPhase(
            initialFrame.Events,
            [],
            chronologicalOrdinals,
            chronologicalEvents);
        foreach (GenericActorMatchTickFrame frame in ticks)
        {
            AppendPhase(
                frame.TickStart.Events,
                frame.TickStart.Traversals,
                chronologicalOrdinals,
                chronologicalEvents);
            AppendPhase(
                frame.Events,
                frame.Traversals,
                chronologicalOrdinals,
                chronologicalEvents);
        }
        for (int index = 0; index < chronologicalOrdinals.Count; index++)
        {
            if (chronologicalOrdinals[index] != index)
            {
                throw new ArgumentException(
                    "Authoritative fact ordinals must be globally contiguous from zero in chronology order.",
                    nameof(ticks));
            }
        }

        foreach (IGrouping<int, GenericActorAuthoritativeEvent> tickEvents in
                 chronologicalEvents.GroupBy(item => item.Tick))
        {
            int expectedSourceOrdinal = 0;
            foreach (GenericActorAuthoritativeEvent item in tickEvents)
            {
                if (item.SourceOrdinal != expectedSourceOrdinal)
                {
                    throw new ArgumentException(
                        "Authoritative event source ordinals must be contiguous from zero within each tick.",
                        nameof(ticks));
                }
                expectedSourceOrdinal = checked(expectedSourceOrdinal + 1);
            }
        }
    }

    private static void ValidateCausalBoundaries(
        GenericActorMatchDescriptor descriptor,
        GenericActorMatchInitialFrame initialFrame,
        IReadOnlyList<GenericActorMatchTickFrame> ticks)
    {
        ActorResolvedMatchDefinition definition = descriptor.Definition;
        ValidateDerivedActiveHealth(
            definition,
            initialFrame.State,
            nameof(initialFrame));
        ValidateLifeSpawnEvidence(
            initialFrame.LifeStarts,
            initialFrame.State,
            initialFrame.Events,
            nameof(initialFrame));
        ValidateLifecycleEventCatalog(
            definition,
            initialFrame.Events,
            nameof(initialFrame));
        if (initialFrame.Events.Any(IsLifeRemovalEvent))
        {
            throw new ArgumentException(
                "Initial events cannot retire or destroy a life.",
                nameof(initialFrame));
        }
        if (initialFrame.Events.Any(IsLifecycleEvent))
        {
            throw new ArgumentException(
                "Initial events cannot queue, cancel, or complete lifecycle work.",
                nameof(initialFrame));
        }

        GenericActorWorldSnapshot previousState = initialFrame.State;
        var irreversibleReturnForms =
            new Dictionary<ActorIdentity, HashSet<string>>();
        var actorsWithPriorSameLifeTransition =
            new HashSet<ActorIdentity>();
        foreach (GenericActorMatchTickFrame frame in ticks)
        {
            ValidateConfiguredSameLifeCompletionBoundaries(
                definition,
                frame,
                nameof(ticks));
            ValidateDerivedActiveHealth(
                definition,
                frame.TickStart.State,
                nameof(ticks));
            ValidateDerivedActiveHealth(
                definition,
                frame.PostState,
                nameof(ticks));
            ValidateLifecycleBoundary(
                definition,
                previousState,
                frame.TickStart,
                actorsWithPriorSameLifeTransition,
                nameof(ticks));
            ValidateAndAdvanceIrreversibleSameLifeHistory(
                definition,
                frame.TickStart.Events,
                irreversibleReturnForms,
                nameof(ticks));
            RecordCompletedSameLifeActors(
                frame.TickStart.Events,
                actorsWithPriorSameLifeTransition);
            ValidateResolutionLifeEvidence(
                definition,
                frame.TickStart.State,
                frame.PostState,
                frame.ActorTurns,
                frame.Events,
                frame.Traversals,
                actorsWithPriorSameLifeTransition,
                nameof(ticks));
            ValidateAndAdvanceIrreversibleSameLifeHistory(
                definition,
                frame.Events,
                irreversibleReturnForms,
                nameof(ticks));
            RecordCompletedSameLifeActors(
                frame.Events,
                actorsWithPriorSameLifeTransition);
            previousState = frame.PostState;
        }
    }

    private static void RecordCompletedSameLifeActors(
        IEnumerable<GenericActorAuthoritativeEvent> events,
        ISet<ActorIdentity> actors)
    {
        foreach (GenericActorAuthoritativeEvent item in events.Where(item =>
                     item.Kind
                        == GenericActorRuntimeObservation.EventKind
                            .FormTransitionCompleted))
        {
            var payload =
                (GenericActorRuntimeObservation.EventPayload.FormTransition)
                    item.Payload;
            actors.Add(payload.ActorId);
        }
    }

    private static void ValidateConfiguredSameLifeCompletionBoundaries(
        ActorResolvedMatchDefinition definition,
        GenericActorMatchTickFrame frame,
        string parameterName)
    {
        ValidateConfiguredSameLifeCompletionBoundary(
            definition,
            frame.TickStart.Events,
            ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                .TickStartAfterDuration,
            parameterName);
        ValidateConfiguredSameLifeCompletionBoundary(
            definition,
            frame.Events,
            ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                .EndOfStartedTickPlusDurationMinusOneAfterModeUpdate,
            parameterName);
    }

    private static void ValidateConfiguredSameLifeCompletionBoundary(
        ActorResolvedMatchDefinition definition,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        ActorTransitionWindupDefinition.ActorTransitionCompletionKind
            expectedCompletion,
        string parameterName)
    {
        foreach (GenericActorAuthoritativeEvent item in events.Where(item =>
                     item.Kind
                         == GenericActorRuntimeObservation.EventKind
                             .FormTransitionCompleted))
        {
            var payload =
                (GenericActorRuntimeObservation.EventPayload.FormTransition)
                    item.Payload;
            ActorFormTransitionDefinition? transition = definition.Rules
                .SameLifeTransitions
                .OfType<ActorFormTransitionDefinition>()
                .SingleOrDefault(value => string.Equals(
                    value.TransitionId,
                    payload.TransitionId,
                    StringComparison.Ordinal));
            if (transition is not null
                && transition.Windup.Completion != expectedCompletion)
            {
                throw new ArgumentException(
                    "A same-life completion must be recorded at its configured completion boundary.",
                    parameterName);
            }
        }
    }

    internal static void ValidateAndAdvanceIrreversibleSameLifeHistory(
        ActorResolvedMatchDefinition definition,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        IDictionary<ActorIdentity, HashSet<string>> blockedReturnForms,
        string parameterName)
    {
        foreach (GenericActorAuthoritativeEvent item in events
                     .Where(item =>
                         item.Kind
                            == GenericActorRuntimeObservation.EventKind
                                .FormTransitionCompleted)
                     .OrderBy(item => item.Ordinal))
        {
            if (item.Payload is not
                GenericActorRuntimeObservation.EventPayload.FormTransition
                    payload)
            {
                throw new ArgumentException(
                    "A form-transition completion must carry typed transition evidence.",
                    parameterName);
            }
            ActorSameLifeTransitionDefinition? transition =
                definition.Rules.SameLifeTransitions.SingleOrDefault(
                    value => string.Equals(
                        value.TransitionId,
                        payload.TransitionId,
                        StringComparison.Ordinal));
            if (transition is null)
            {
                throw new ArgumentException(
                    "A form-transition completion references an unknown transition.",
                    parameterName);
            }
            if (!blockedReturnForms.TryGetValue(
                    payload.ActorId,
                    out HashSet<string>? blocked))
            {
                blocked = new HashSet<string>(StringComparer.Ordinal);
                blockedReturnForms.Add(payload.ActorId, blocked);
            }
            if (blocked.Contains(transition.TargetFormId))
            {
                throw new ArgumentException(
                    "A same-life completion cannot reverse an earlier irreversible transition for that life.",
                    parameterName);
            }
            if (transition.IrreversibleForLife)
                blocked.Add(transition.SourceFormId);
        }
    }

    private static void ValidateSkippedTerminalSameLifeWork(
        GenericActorMatchDescriptor descriptor,
        GenericActorMatchInitialFrame initialFrame,
        IReadOnlyList<GenericActorMatchTickFrame> ticks,
        GenericActorMatchResult? result)
    {
        GenericActorWorldSnapshot[] states =
        [
            initialFrame.State,
            .. ticks.Select(frame => frame.TickStart.State),
            .. ticks.Select(frame => frame.PostState),
        ];
        GenericActorWorldSnapshot[] overdueStates = states
            .Where(state => state.ActiveLives.Any(life =>
                life.PendingSameLifeTransition is { } pending
                && pending.DueTick < state.NextTick))
            .ToArray();
        if (overdueStates.Length == 0)
            return;

        bool isFaultTerminal = result?.Mode switch
        {
            GenericActorMatchModeResult.Deathmatch deathmatch =>
                deathmatch.Reason
                    == GenericDeathmatchEndReason.FaultEligibility,
            GenericActorMatchModeResult.Frontline frontline =>
                frontline.Reason
                    == GenericFrontlineEndReason.FaultEligibility,
            _ => false,
        };
        GenericActorWorldSnapshot? finalState =
            ticks.Count == 0
                ? null
                : ticks[^1].PostState;
        if (!isFaultTerminal
            || finalState is null
            || overdueStates.Length != 1
            || !ReferenceEquals(overdueStates[0], finalState))
        {
            throw new ArgumentException(
                "Due same-life work may remain pending only in the final state of a fault-eligibility terminal tick that skipped later phases.",
                nameof(ticks));
        }

        foreach (GenericActorWorldSnapshot.LifeSnapshot life in
                 finalState.ActiveLives.Where(life =>
                     life.PendingSameLifeTransition is { } pending
                     && pending.DueTick < finalState.NextTick))
        {
            GenericActorRuntimeObservation.PendingSameLifeTransition pending =
                life.PendingSameLifeTransition!;
            ActorSameLifeTransitionDefinition transition =
                descriptor.Definition.Rules.SameLifeTransitions.Single(
                    value => string.Equals(
                        value.TransitionId,
                        pending.TransitionId,
                        StringComparison.Ordinal));
            if (pending.DueTick != finalState.NextTick - 1
                || transition.Windup.Completion
                    != ActorTransitionWindupDefinition
                        .ActorTransitionCompletionKind
                        .EndOfStartedTickPlusDurationMinusOneAfterModeUpdate)
            {
                throw new ArgumentException(
                    "Fault-terminal pending same-life work must be exact-due work from the skipped end-clock phase.",
                    nameof(ticks));
            }
        }
    }

    private static void ValidateLifecycleBoundary(
        ActorResolvedMatchDefinition definition,
        GenericActorWorldSnapshot before,
        GenericActorMatchTickStart tickStart,
        IReadOnlySet<ActorIdentity> actorsWithPriorSameLifeTransition,
        string parameterName)
    {
        GenericActorWorldSnapshot after = tickStart.State;
        ValidateLifecycleEventCatalog(
            definition,
            tickStart.Events,
            parameterName);
        if (before.NextTick != tickStart.Tick
            || before.NextProjectileId != after.NextProjectileId
            || !before.Participants.SequenceEqual(after.Participants)
            || !Equals(before.Mode, after.Mode)
            || !ScoreboardsStableAcrossLifecycleBoundary(
                before.Scoreboard,
                after.Scoreboard))
        {
            throw new ArgumentException(
                "Tick-start lifecycle processing cannot change participants, mode, projectile issuance, eligibility, or non-derived scores.",
                parameterName);
        }

        ValidateSlotClockLifecycleBoundary(
            definition,
            before,
            tickStart,
            parameterName);
        ValidateFabricationLifecycleBoundary(
            definition,
            before,
            tickStart,
            parameterName);
        ValidateSplitLifecycleBoundary(
            definition,
            before,
            tickStart,
            actorsWithPriorSameLifeTransition,
            parameterName);
        ValidateTickStartLifecyclePhaseOrdering(
            definition,
            tickStart,
            parameterName);
        ValidateLifeSpawnEvidence(
            tickStart.LifeStarts,
            after,
            tickStart.Events,
            parameterName);
        ValidateLifeRemovalEvidence(
            before,
            after,
            tickStart.Events,
            requireUnchangedPosition: true,
            parameterName);
        ValidateSurvivingLivesAcrossLifecycleBoundary(
            definition,
            before,
            after,
            tickStart.Events,
            parameterName);
        ValidateProjectileLifecycleBoundary(
            before,
            after,
            tickStart.Events,
            tickStart.Traversals,
            parameterName);
    }

    private static void ValidateSlotClockLifecycleBoundary(
        ActorResolvedMatchDefinition definition,
        GenericActorWorldSnapshot before,
        GenericActorMatchTickStart tickStart,
        string parameterName)
    {
        Dictionary<(int TeamId, int UnitId),
            GenericActorWorldSnapshot.SlotSnapshot> afterSlots =
            tickStart.State.Slots.ToDictionary(slot =>
                (slot.TeamId, slot.UnitId));
        Dictionary<string, InitialSpawnDefinition> spawns =
            definition.Map.SpawnAnchors.ToDictionary(
                anchor => anchor.Spawn.SpawnId,
                anchor => anchor.Spawn,
                StringComparer.Ordinal);
        Dictionary<(int TeamId, int UnitId),
            ActorUnitSlotLifecycleAssignmentDefinition> assignments =
            definition.LifecycleAssignments.ToDictionary(assignment =>
                (assignment.TeamId, assignment.UnitId));
        Dictionary<string, ActorFormDefinition> forms =
            definition.Rules.Forms.ToDictionary(
                form => form.Id,
                StringComparer.Ordinal);
        Dictionary<string, ActorLifecycleProfileDefinition> profiles =
            definition.Rules.Lifecycle.Profiles.ToDictionary(
                profile => profile.ProfileId,
                StringComparer.Ordinal);
        var expectedAutomaticActors = new List<ActorIdentity>();
        var expectedActivationActors = new List<ActorIdentity>();

        foreach (GenericActorWorldSnapshot.SlotSnapshot beforeSlot in
                 before.Slots)
        {
            GenericActorWorldSnapshot.SlotSnapshot afterSlot =
                afterSlots[(beforeSlot.TeamId, beforeSlot.UnitId)];
            switch (beforeSlot.State)
            {
                case GenericActorRuntimeObservation.UnitSlotState
                    .AvailabilityPending pending:
                    if (pending.DueTick > tickStart.Tick)
                    {
                        if (!SlotSnapshotsSemanticallyEqual(
                                beforeSlot,
                                afterSlot))
                        {
                            throw new ArgumentException(
                                "A not-yet-due availability clock must remain exact across tick start.",
                                parameterName);
                        }
                        break;
                    }
                    ActorUnitSlotLifecycleAssignmentDefinition
                        availabilityAssignment = assignments[
                            (beforeSlot.TeamId, beforeSlot.UnitId)];
                    if (availabilityAssignment.InitialAvailability
                        == ActorUnitSlotLifecycleAssignmentDefinition
                            .InitialAvailabilityKind
                            .DormantAutomaticActivationAtTick)
                    {
                        if (pending.DueTick != tickStart.Tick
                            || pending.Reason
                                != GenericActorRuntimeObservation
                                    .AvailabilityReason.InitialUnlock)
                        {
                            throw new ArgumentException(
                                "An automatic-activation clock must be an exact-due initial unlock.",
                                parameterName);
                        }
                        InitialSpawnDefinition activationSpawn = spawns[
                            availabilityAssignment
                                .AssignedRespawnSpawnId!];
                        ActorLifecycleProfileDefinition activationProfile =
                            profiles[
                                availabilityAssignment.LifecycleProfileId];
                        string activationFormId =
                            activationProfile.AutomaticReturnFormId!;
                        var activationActorId = new ActorIdentity(
                            beforeSlot.TeamId,
                            beforeSlot.UnitId,
                            beforeSlot.NextLifeId);
                        expectedActivationActors.Add(activationActorId);
                        GenericActorWorldSnapshot.LifeSnapshot[]
                            activationLives = tickStart.State.ActiveLives
                                .Where(life =>
                                    life.ActorId == activationActorId)
                                .ToArray();
                        GenericActorLifeStart[] activationStarts =
                            tickStart.LifeStarts
                                .Where(start =>
                                    start.ActorId == activationActorId)
                                .ToArray();
                        GenericActorAuthoritativeEvent[]
                            activationSpawnEvents = tickStart.Events
                                .Where(item =>
                                    item.Payload is
                                        GenericActorRuntimeObservation
                                            .EventPayload.LifeSpawned spawned
                                    && spawned.ActorId
                                        == activationActorId)
                                .ToArray();
                        if (activationLives.Length != 1
                            || activationStarts.Length != 1
                            || activationSpawnEvents.Length != 1)
                        {
                            throw new ArgumentException(
                                "An exact-due automatic activation needs one active life, life start, and spawn event.",
                                parameterName);
                        }
                        GenericActorWorldSnapshot.LifeSnapshot
                            activationLife = activationLives[0];
                        GenericActorLifeStart activationStart =
                            activationStarts[0];
                        var activationSpawned =
                            (GenericActorRuntimeObservation.EventPayload
                                .LifeSpawned)activationSpawnEvents[0].Payload;
                        ActorFormDefinition activationForm =
                            forms[activationFormId];
                        int activationGeneration =
                            availabilityAssignment.InitialGeneration!.Value;
                        int? activationEnergy = InitialFormEnergy(
                            definition,
                            activationFormId);
                        if (afterSlot.ParticipantId
                                != beforeSlot.ParticipantId
                            || afterSlot.NextLifeId
                                != checked(beforeSlot.NextLifeId + 1)
                            || afterSlot.State is not
                                GenericActorRuntimeObservation.UnitSlotState
                                    .Active activationActive
                            || activationActive.ActorId
                                != activationActorId
                            || activationActive.Generation
                                != activationGeneration
                            || !string.Equals(
                                activationActive.FormId,
                                activationFormId,
                                StringComparison.Ordinal)
                            || afterSlot.PendingParentActorId is not null
                            || afterSlot.SplitReservation is not null
                            || activationLife.ParticipantId
                                != beforeSlot.ParticipantId
                            || activationLife.Generation
                                != activationGeneration
                            || !string.Equals(
                                activationLife.FormId,
                                activationFormId,
                                StringComparison.Ordinal)
                            || activationLife.Position
                                != activationSpawn.Position
                            || activationLife.Facing
                                != activationSpawn.Facing
                            || activationLife.Health
                                != activationForm.MaxHealth
                            || activationLife.Cooldown != 0
                            || activationLife.Energy != activationEnergy
                            || activationLife.SpawnedAtTick
                                != tickStart.Tick
                            || activationLife.SpawnReason
                                != GenericActorRuntimeStart.SpawnReason
                                    .AutomaticActivation
                            || activationLife.ParentActorId is not null
                            || activationLife.SourceTransitionId is not null
                            || activationLife.SourceOperationId is not null
                            || activationLife.PreviousActionResolution
                                is not null
                            || activationLife.PendingSameLifeTransition
                                is not null
                            || activationStart.ParticipantId
                                != beforeSlot.ParticipantId
                            || activationStart.Origin.Reason
                                != GenericActorRuntimeStart.SpawnReason
                                    .AutomaticActivation
                            || activationStart.Origin.Generation
                                != activationGeneration
                            || activationStart.Origin.ParentActorId
                                is not null
                            || activationStart.Origin.SourceTransitionId
                                is not null
                            || activationStart.Origin.SourceOperationId
                                is not null
                            || activationSpawned.ParticipantId
                                != beforeSlot.ParticipantId
                            || activationSpawned.Reason
                                != GenericActorRuntimeStart.SpawnReason
                                    .AutomaticActivation
                            || activationSpawned.ParentActorId is not null
                            || activationSpawned.Generation
                                != activationGeneration
                            || !string.Equals(
                                activationSpawned.FormId,
                                activationFormId,
                                StringComparison.Ordinal)
                            || activationSpawned.Health
                                != activationForm.MaxHealth
                            || activationSpawned.Position
                                != activationSpawn.Position
                            || activationSpawned.SourceTransitionId
                                is not null
                            || activationSpawned.SourceOperationId
                                is not null)
                        {
                            throw new ArgumentException(
                                "An automatic activation must consume its exact contract clock into the declared first assigned-spawn life.",
                                parameterName);
                        }
                        break;
                    }
                    if (pending.DueTick != tickStart.Tick
                        || afterSlot.TeamId != beforeSlot.TeamId
                        || afterSlot.UnitId != beforeSlot.UnitId
                        || afterSlot.ParticipantId
                            != beforeSlot.ParticipantId
                        || afterSlot.NextLifeId != beforeSlot.NextLifeId
                        || afterSlot.State is not
                            GenericActorRuntimeObservation.UnitSlotState.Ready
                        || afterSlot.PendingParentActorId is not null
                        || afterSlot.SplitReservation is not null)
                    {
                        throw new ArgumentException(
                            "An exact-due availability clock must become one exact Ready slot.",
                            parameterName);
                    }
                    break;

                case GenericActorRuntimeObservation.UnitSlotState
                    .AutomaticReturnPending pending:
                    if (pending.DueTick > tickStart.Tick)
                    {
                        if (!SlotSnapshotsSemanticallyEqual(
                                beforeSlot,
                                afterSlot))
                        {
                            throw new ArgumentException(
                                "A not-yet-due automatic-return clock must remain exact across tick start.",
                                parameterName);
                        }
                        break;
                    }
                    if (pending.DueTick != tickStart.Tick)
                    {
                        throw new ArgumentException(
                            "A slot clock cannot remain overdue.",
                            parameterName);
                    }

                    ActorUnitSlotLifecycleAssignmentDefinition assignment =
                        assignments[
                            (beforeSlot.TeamId, beforeSlot.UnitId)];
                    InitialSpawnDefinition spawn =
                        spawns[assignment.AssignedRespawnSpawnId!];
                    var actorId = new ActorIdentity(
                        beforeSlot.TeamId,
                        beforeSlot.UnitId,
                        beforeSlot.NextLifeId);
                    expectedAutomaticActors.Add(actorId);
                    GenericActorWorldSnapshot.LifeSnapshot[] lives =
                        tickStart.State.ActiveLives
                            .Where(life => life.ActorId == actorId)
                            .ToArray();
                    GenericActorLifeStart[] starts =
                        tickStart.LifeStarts
                            .Where(start => start.ActorId == actorId)
                            .ToArray();
                    GenericActorAuthoritativeEvent[] spawnEvents =
                        tickStart.Events
                            .Where(item =>
                                item.Payload is
                                    GenericActorRuntimeObservation
                                        .EventPayload.LifeSpawned spawned
                                && spawned.ActorId == actorId)
                            .ToArray();
                    if (lives.Length != 1
                        || starts.Length != 1
                        || spawnEvents.Length != 1)
                    {
                        throw new ArgumentException(
                            "An exact-due automatic-return clock needs one active life, life start, and spawn event.",
                            parameterName);
                    }
                    GenericActorWorldSnapshot.LifeSnapshot life = lives[0];
                    GenericActorLifeStart start = starts[0];
                    var spawned =
                        (GenericActorRuntimeObservation.EventPayload
                            .LifeSpawned)spawnEvents[0].Payload;
                    ActorFormDefinition form = forms[pending.TargetFormId];
                    int? expectedEnergy = InitialFormEnergy(
                        definition,
                        pending.TargetFormId);
                    if (afterSlot.ParticipantId
                            != beforeSlot.ParticipantId
                        || afterSlot.NextLifeId
                            != checked(beforeSlot.NextLifeId + 1)
                        || afterSlot.State is not
                            GenericActorRuntimeObservation.UnitSlotState.Active
                            active
                        || active.ActorId != actorId
                        || active.Generation != pending.Generation
                        || !string.Equals(
                            active.FormId,
                            pending.TargetFormId,
                            StringComparison.Ordinal)
                        || afterSlot.PendingParentActorId is not null
                        || afterSlot.SplitReservation is not null
                        || life.ParticipantId != beforeSlot.ParticipantId
                        || life.Generation != pending.Generation
                        || !string.Equals(
                            life.FormId,
                            pending.TargetFormId,
                            StringComparison.Ordinal)
                        || life.Position != spawn.Position
                        || life.Facing != spawn.Facing
                        || life.Health != form.MaxHealth
                        || life.Cooldown != 0
                        || life.Energy != expectedEnergy
                        || life.SpawnedAtTick != tickStart.Tick
                        || life.SpawnReason
                            != GenericActorRuntimeStart.SpawnReason
                                .AutomaticReturn
                        || life.ParentActorId
                            != beforeSlot.PendingParentActorId
                        || life.SourceTransitionId is not null
                        || life.SourceOperationId is not null
                        || life.PreviousActionResolution is not null
                        || life.PendingSameLifeTransition is not null
                        || start.ParticipantId != beforeSlot.ParticipantId
                        || start.Origin.Reason
                            != GenericActorRuntimeStart.SpawnReason
                                .AutomaticReturn
                        || start.Origin.Generation != pending.Generation
                        || start.Origin.ParentActorId
                            != beforeSlot.PendingParentActorId
                        || start.Origin.SourceTransitionId is not null
                        || start.Origin.SourceOperationId is not null
                        || spawned.ParticipantId
                            != beforeSlot.ParticipantId
                        || spawned.Reason
                            != GenericActorRuntimeStart.SpawnReason
                                .AutomaticReturn
                        || spawned.ParentActorId
                            != beforeSlot.PendingParentActorId
                        || spawned.Generation != pending.Generation
                        || !string.Equals(
                            spawned.FormId,
                            pending.TargetFormId,
                            StringComparison.Ordinal)
                        || spawned.Health != form.MaxHealth
                        || spawned.Position != spawn.Position
                        || spawned.SourceTransitionId is not null
                        || spawned.SourceOperationId is not null)
                    {
                        throw new ArgumentException(
                            "An automatic return must consume its exact clock into a fresh assigned-spawn life.",
                            parameterName);
                    }
                    break;
            }
        }

        ActorIdentity[] expected = expectedAutomaticActors
            .Order()
            .ToArray();
        ActorIdentity[] recordedStarts = tickStart.LifeStarts
            .Where(start =>
                start.Origin.Reason
                    == GenericActorRuntimeStart.SpawnReason.AutomaticReturn)
            .Select(start => start.ActorId)
            .Order()
            .ToArray();
        ActorIdentity[] recordedEvents = tickStart.Events
            .Where(item =>
                item.Payload is
                    GenericActorRuntimeObservation.EventPayload.LifeSpawned
                        spawned
                && spawned.Reason
                    == GenericActorRuntimeStart.SpawnReason.AutomaticReturn)
            .Select(item =>
                ((GenericActorRuntimeObservation.EventPayload.LifeSpawned)
                    item.Payload).ActorId)
            .Order()
            .ToArray();
        ActorIdentity[] expectedActivations = expectedActivationActors
            .Order()
            .ToArray();
        ActorIdentity[] recordedActivationStarts = tickStart.LifeStarts
            .Where(start =>
                start.Origin.Reason
                    == GenericActorRuntimeStart.SpawnReason
                        .AutomaticActivation)
            .Select(start => start.ActorId)
            .Order()
            .ToArray();
        ActorIdentity[] recordedActivationEvents = tickStart.Events
            .Where(item =>
                item.Payload is
                    GenericActorRuntimeObservation.EventPayload.LifeSpawned
                        spawned
                && spawned.Reason
                    == GenericActorRuntimeStart.SpawnReason
                        .AutomaticActivation)
            .Select(item =>
                ((GenericActorRuntimeObservation.EventPayload.LifeSpawned)
                    item.Payload).ActorId)
            .Order()
            .ToArray();
        if (!expected.SequenceEqual(recordedStarts)
            || !expected.SequenceEqual(recordedEvents)
            || !expectedActivations.SequenceEqual(
                recordedActivationStarts)
            || !expectedActivations.SequenceEqual(
                recordedActivationEvents)
            || tickStart.Events.Any(item => item.Kind
                == GenericActorRuntimeObservation.EventKind
                    .LifecycleClockCancelled))
        {
            throw new ArgumentException(
                "Automatic lifecycle evidence must originate from every and only exact-due slot clock, and tick-start clocks cannot be cancelled.",
                parameterName);
        }
    }

    private static void ValidateTickStartLifecyclePhaseOrdering(
        ActorResolvedMatchDefinition definition,
        GenericActorMatchTickStart tickStart,
        string parameterName)
    {
        HashSet<string> fabricationTransitionIds = definition.Rules
            .FabricationTransitions
            .Select(transition => transition.TransitionId)
            .ToHashSet(StringComparer.Ordinal);
        GenericActorAuthoritativeEvent[] activationSpawns =
            tickStart.Events
                .Where(item =>
                    item.Payload is
                        GenericActorRuntimeObservation.EventPayload.LifeSpawned
                            spawned
                    && spawned.Reason
                        == GenericActorRuntimeStart.SpawnReason
                            .AutomaticActivation)
                .OrderBy(item => item.Ordinal)
                .ToArray();
        GenericActorAuthoritativeEvent[] automaticSpawns =
            tickStart.Events
                .Where(item =>
                    item.Payload is
                        GenericActorRuntimeObservation.EventPayload.LifeSpawned
                            spawned
                    && spawned.Reason
                        == GenericActorRuntimeStart.SpawnReason.AutomaticReturn)
                .OrderBy(item => item.Ordinal)
                .ToArray();
        if (!activationSpawns.SequenceEqual(
                activationSpawns.OrderBy(item =>
                    ((GenericActorRuntimeObservation.EventPayload.LifeSpawned)
                        item.Payload).ActorId))
            || !automaticSpawns.SequenceEqual(
                automaticSpawns.OrderBy(item =>
                    ((GenericActorRuntimeObservation.EventPayload.LifeSpawned)
                        item.Payload).ActorId)))
        {
            throw new ArgumentException(
                "Automatic activations and returns must each resolve in stable-slot order.",
                parameterName);
        }
        GenericActorAuthoritativeEvent[] automaticLifecycleSpawns =
        [
            .. activationSpawns,
            .. automaticSpawns,
        ];
        long priorAutomaticBundleEnd = -1;
        foreach (GenericActorAuthoritativeEvent spawnEvent in
                 automaticLifecycleSpawns)
        {
            Position position =
                ((GenericActorRuntimeObservation.EventPayload.LifeSpawned)
                    spawnEvent.Payload).Position;
            GenericActorProjectileTraversal[] purges =
                tickStart.Traversals
                    .Where(traversal =>
                        traversal.Terminal is
                            GenericActorProjectileTraversal
                                .TerminalDisposition
                                .LifecyclePlacementPurge purge
                        && purge.Position == position)
                    .OrderBy(traversal => traversal.Ordinal)
                    .ToArray();
            long bundleStart = purges
                .Select(traversal => traversal.Ordinal)
                .Append(spawnEvent.Ordinal)
                .Min();
            if (bundleStart <= priorAutomaticBundleEnd
                || purges.Any(traversal =>
                    traversal.Ordinal >= spawnEvent.Ordinal)
                || !purges.Select(traversal => traversal.ProjectileId)
                    .SequenceEqual(purges
                        .Select(traversal => traversal.ProjectileId)
                        .Order()))
            {
                throw new ArgumentException(
                    "Each automatic lifecycle bundle must purge projectiles by ID and spawn atomically in declared phase order.",
                    parameterName);
            }
            priorAutomaticBundleEnd = spawnEvent.Ordinal;
        }

        Position[] automaticPositions = automaticLifecycleSpawns
            .Select(item =>
                ((GenericActorRuntimeObservation.EventPayload.LifeSpawned)
                    item.Payload).Position)
            .ToArray();
        Position[] fabricationPositions = tickStart.Events
            .Where(item =>
                item.Payload is
                    GenericActorRuntimeObservation.EventPayload.LifeSpawned
                        spawned
                && spawned.Reason
                    == GenericActorRuntimeStart.SpawnReason.Fabrication)
            .Select(item =>
                ((GenericActorRuntimeObservation.EventPayload.LifeSpawned)
                    item.Payload).Position)
            .ToArray();
        Position[] replicationPositions = tickStart.Events
            .Where(item =>
                item.Payload is
                    GenericActorRuntimeObservation.EventPayload.LifeSpawned
                        spawned
                && spawned.Reason
                    == GenericActorRuntimeStart.SpawnReason.Replication)
            .Select(item =>
                ((GenericActorRuntimeObservation.EventPayload.LifeSpawned)
                    item.Payload).Position)
            .ToArray();

        var automaticOrdinals = automaticLifecycleSpawns
            .Select(item => item.Ordinal)
            .Concat(tickStart.Traversals
                .Where(traversal =>
                    traversal.Terminal is
                        GenericActorProjectileTraversal.TerminalDisposition
                            .LifecyclePlacementPurge purge
                    && automaticPositions.Contains(purge.Position))
                .Select(traversal => traversal.Ordinal))
            .ToArray();
        var fabricationOrdinals = tickStart.Events
            .Where(item =>
                item.Payload is
                    GenericActorRuntimeObservation.EventPayload.LifeSpawned
                        spawned
                && spawned.Reason
                    == GenericActorRuntimeStart.SpawnReason.Fabrication
                || item.Payload is
                    GenericActorRuntimeObservation.EventPayload.Lifecycle
                        lifecycle
                && fabricationTransitionIds.Contains(
                    lifecycle.TransitionId))
            .Select(item => item.Ordinal)
            .Concat(tickStart.Traversals
                .Where(traversal =>
                    traversal.Terminal is
                        GenericActorProjectileTraversal.TerminalDisposition
                            .LifecyclePlacementPurge purge
                    && fabricationPositions.Contains(purge.Position))
                .Select(traversal => traversal.Ordinal))
            .ToArray();
        var replicationOrdinals = tickStart.Events
            .Where(item =>
                item.Payload switch
                {
                    GenericActorRuntimeObservation.EventPayload.LifeSpawned
                        spawned =>
                        spawned.Reason
                        == GenericActorRuntimeStart.SpawnReason.Replication,
                    GenericActorRuntimeObservation.EventPayload.LifeRetired
                        retired =>
                        retired.SourceTransitionId is string transitionId
                        && IsReplicationTransition(
                            definition,
                            transitionId),
                    GenericActorRuntimeObservation.EventPayload.Lifecycle
                        lifecycle =>
                        IsReplicationTransition(
                            definition,
                            lifecycle.TransitionId),
                    _ => false,
                })
            .Select(item => item.Ordinal)
            .Concat(tickStart.Traversals
                .Where(traversal =>
                    traversal.Terminal is
                        GenericActorProjectileTraversal.TerminalDisposition
                            .LifecyclePlacementPurge purge
                    && replicationPositions.Contains(purge.Position))
                .Select(traversal => traversal.Ordinal))
            .ToArray();
        var sameLifeOrdinals = tickStart.Events
            .Where(IsFormTransitionEvent)
            .Select(item => item.Ordinal)
            .ToArray();

        long previousPhaseEnd = -1;
        foreach (long[] phase in new[]
                 {
                     automaticOrdinals,
                     fabricationOrdinals,
                     replicationOrdinals,
                     sameLifeOrdinals,
                 })
        {
            if (phase.Length == 0)
                continue;
            if (phase.Min() <= previousPhaseEnd)
            {
                throw new ArgumentException(
                    "Tick-start lifecycle facts must resolve as automatic activation/return, fabrication, Split, then same-life work without phase interleaving.",
                    parameterName);
            }
            previousPhaseEnd = phase.Max();
        }
    }

    private static void ValidateFabricationLifecycleBoundary(
        ActorResolvedMatchDefinition definition,
        GenericActorWorldSnapshot before,
        GenericActorMatchTickStart tickStart,
        string parameterName)
    {
        ValidateFabricationTickStartOrdering(
            definition,
            tickStart.Events,
            tickStart.Traversals,
            parameterName);
        Dictionary<string, FabricationPendingFact> beforeFacts =
            FabricationPendingFacts(before);
        Dictionary<string, FabricationPendingFact> afterFacts =
            FabricationPendingFacts(tickStart.State);
        ILookup<string, GenericActorAuthoritativeEvent> eventsByOperation =
            FabricationLifecycleEvents(
                    definition,
                    tickStart.Events)
                .ToLookup(
                    item => LifecyclePayload(item).OperationId,
                    StringComparer.Ordinal);

        if (eventsByOperation.Any(group =>
                group.Any(item => item.Kind
                    is GenericActorRuntimeObservation.EventKind
                        .LifecycleQueued
                    or GenericActorRuntimeObservation.EventKind
                        .LifecycleCancelled)))
        {
            throw new ArgumentException(
                "A fabrication may neither queue nor cancel during tick-start lifecycle processing.",
                parameterName);
        }

        foreach ((string operationId, FabricationPendingFact beforeFact)
                 in beforeFacts)
        {
            GenericActorAuthoritativeEvent[] operationEvents =
                eventsByOperation[operationId]
                    .OrderBy(item => item.Ordinal)
                    .ToArray();
            if (beforeFact.Pending.DueTick > tickStart.Tick)
            {
                if (!afterFacts.TryGetValue(
                        operationId,
                        out FabricationPendingFact? afterFact)
                    || !FabricationPendingFactsEqual(
                        beforeFact,
                        afterFact)
                    || operationEvents.Length != 0)
                {
                    throw new ArgumentException(
                        "A not-yet-due fabrication must remain exact across the tick-start boundary without lifecycle evidence.",
                        parameterName);
                }
                continue;
            }

            if (beforeFact.Pending.DueTick != tickStart.Tick
                || afterFacts.ContainsKey(operationId)
                || operationEvents.Length != 1
                || operationEvents[0].Kind
                    != GenericActorRuntimeObservation.EventKind
                        .LifecycleCompleted
                || !LifecyclePayloadMatchesPending(
                    LifecyclePayload(operationEvents[0]),
                    beforeFact,
                    cancellationReason: null))
            {
                throw new ArgumentException(
                    "An exact-due fabrication must consume its pending claim with one matching completion event.",
                    parameterName);
            }
            ValidateFabricationCompletion(
                definition,
                beforeFact,
                operationEvents[0],
                tickStart,
                parameterName);
        }

        if (afterFacts.Keys.Any(operationId =>
                !beforeFacts.ContainsKey(operationId))
            || eventsByOperation.Any(group =>
                !beforeFacts.ContainsKey(group.Key)))
        {
            throw new ArgumentException(
                "Tick-start fabrication state and completion evidence must originate from an exact prior pending claim.",
                parameterName);
        }
    }

    private static void ValidateSplitLifecycleBoundary(
        ActorResolvedMatchDefinition definition,
        GenericActorWorldSnapshot before,
        GenericActorMatchTickStart tickStart,
        IReadOnlySet<ActorIdentity> actorsWithPriorSameLifeTransition,
        string parameterName)
    {
        Dictionary<string, SplitReplicationReservation> beforeReservations =
            before.PendingReplications.ToDictionary(
                reservation => reservation.OperationId,
                StringComparer.Ordinal);
        Dictionary<string, SplitReplicationReservation> afterReservations =
            tickStart.State.PendingReplications.ToDictionary(
                reservation => reservation.OperationId,
                StringComparer.Ordinal);
        HashSet<string> transitionIds = definition.Rules
            .ReplicationTransitions
            .OfType<SplitReplicationTransitionDefinition>()
            .Select(transition => transition.TransitionId)
            .ToHashSet(StringComparer.Ordinal);
        ILookup<string, GenericActorAuthoritativeEvent> eventsByOperation =
            tickStart.Events
                .Where(item =>
                    IsLifecycleEvent(item)
                    && item.Payload is
                        GenericActorRuntimeObservation.EventPayload.Lifecycle
                            lifecycle
                    && transitionIds.Contains(lifecycle.TransitionId))
                .ToLookup(
                    item => LifecyclePayload(item).OperationId,
                    StringComparer.Ordinal);
        if (eventsByOperation.Any(group => group.Any(item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .LifecycleQueued)))
        {
            throw new ArgumentException(
                "A Split cannot queue during tick-start lifecycle processing.",
                parameterName);
        }

        var kernel = new SplitReplicationKernel(definition);
        var dueBundles = new List<SplitBoundaryBundle>();
        foreach (SplitReplicationReservation reservation in
                 before.PendingReplications)
        {
            GenericActorAuthoritativeEvent[] operationEvents =
                eventsByOperation[reservation.OperationId]
                    .OrderBy(item => item.Ordinal)
                    .ToArray();
            if (reservation.DueTick > tickStart.Tick)
            {
                if (!afterReservations.TryGetValue(
                        reservation.OperationId,
                        out SplitReplicationReservation? retained)
                    || !ReplicationReservationsSemanticallyEqual(
                        reservation,
                        retained)
                    || operationEvents.Length != 0)
                {
                    throw new ArgumentException(
                        "A not-yet-due Split must remain exact across tick start without lifecycle evidence.",
                        parameterName);
                }
                continue;
            }
            if (reservation.DueTick != tickStart.Tick
                || afterReservations.ContainsKey(reservation.OperationId))
            {
                throw new ArgumentException(
                    "A due Split must consume its exact prior reservation.",
                    parameterName);
            }

            GenericActorWorldSnapshot.LifeSnapshot? source =
                before.ActiveLives.SingleOrDefault(life =>
                    life.ActorId == reservation.SourceActorId);
            SplitReplicationActorSnapshot? sourceSnapshot = source is null
                ? null
                : new SplitReplicationActorSnapshot(
                    source.ActorId,
                    source.ParticipantId,
                    source.Generation,
                    source.FormId,
                    source.Health,
                    source.Position,
                    source.Facing,
                    actorsWithPriorSameLifeTransition.Contains(
                        source.ActorId),
                    source.PendingSameLifeTransition is not null);
            SplitReplicationCompletion completion = kernel.Complete(
                tickStart.Tick,
                reservation,
                sourceSnapshot);
            GenericActorRuntimeObservation.EventKind expectedKind =
                completion.Outcome
                    == SplitReplicationCompletion
                        .SplitCompletionOutcomeKind.Completed
                    ? GenericActorRuntimeObservation.EventKind
                        .LifecycleCompleted
                    : GenericActorRuntimeObservation.EventKind
                        .LifecycleCancelled;
            string? cancellationReason = completion.Reason is null
                ? null
                : SplitCompletionCancellationReason(
                    completion.Reason.Value);
            if (operationEvents.Length != 1
                || operationEvents[0].Kind != expectedKind
                || !LifecyclePayloadMatchesSplitWork(
                    LifecyclePayload(operationEvents[0]),
                    reservation,
                    cancellationReason))
            {
                throw new ArgumentException(
                    "A due Split must record the exact reconstructed completion or cancellation outcome.",
                    parameterName);
            }

            long bundleStart = completion.Outcome
                    == SplitReplicationCompletion
                        .SplitCompletionOutcomeKind.Completed
                ? ValidateCompletedSplitBoundary(
                    definition,
                    before,
                    tickStart,
                    completion,
                    operationEvents[0],
                    parameterName)
                : ValidateCancelledSplitBoundary(
                    tickStart,
                    reservation,
                    operationEvents[0],
                    parameterName);
            dueBundles.Add(
                new SplitBoundaryBundle(
                    reservation,
                    operationEvents[0],
                    bundleStart));
        }

        if (afterReservations.Keys.Any(operationId =>
                !beforeReservations.ContainsKey(operationId))
            || eventsByOperation.Any(group =>
                !beforeReservations.ContainsKey(group.Key)))
        {
            throw new ArgumentException(
                "Tick-start Split state and evidence must originate from an exact prior reservation.",
                parameterName);
        }

        SplitBoundaryBundle[] canonicalBundles = dueBundles
            .OrderBy(bundle => bundle.Reservation.SourceActorId)
            .ThenBy(
                bundle => bundle.Reservation.TransitionId,
                StringComparer.Ordinal)
            .ThenBy(
                bundle => bundle.Reservation.OperationId,
                StringComparer.Ordinal)
            .ToArray();
        SplitBoundaryBundle[] recordedBundles = dueBundles
            .OrderBy(bundle => bundle.Event.Ordinal)
            .ToArray();
        long priorEnd = -1;
        foreach (SplitBoundaryBundle bundle in recordedBundles)
        {
            if (bundle.BundleStartOrdinal <= priorEnd)
            {
                throw new ArgumentException(
                    "Due Split bundles cannot interleave.",
                    parameterName);
            }
            priorEnd = bundle.Event.Ordinal;
        }
        if (!recordedBundles.Select(bundle =>
                bundle.Reservation.OperationId)
            .SequenceEqual(canonicalBundles.Select(bundle =>
                bundle.Reservation.OperationId))
            || recordedBundles.Length > 0
            && tickStart.Events.Any(item =>
                IsFormTransitionEvent(item)
                && item.Ordinal < recordedBundles[^1].Event.Ordinal))
        {
            throw new ArgumentException(
                "Due Splits must finish in canonical order before same-life tick-start work.",
                parameterName);
        }
    }

    private static long ValidateCompletedSplitBoundary(
        ActorResolvedMatchDefinition definition,
        GenericActorWorldSnapshot before,
        GenericActorMatchTickStart tickStart,
        SplitReplicationCompletion completion,
        GenericActorAuthoritativeEvent completionEvent,
        string parameterName)
    {
        SplitReplicationReservation reservation = completion.Reservation;
        GenericActorAuthoritativeEvent[] retirements = tickStart.Events
            .Where(item =>
                item.Payload is
                    GenericActorRuntimeObservation.EventPayload.LifeRetired
                        retired
                && retired.ActorId == reservation.SourceActorId
                && string.Equals(
                    retired.SourceOperationId,
                    reservation.OperationId,
                    StringComparison.Ordinal))
            .ToArray();
        if (retirements.Length != 1)
        {
            throw new ArgumentException(
                "A completed Split must have one exact source-retirement event.",
                parameterName);
        }
        GenericActorAuthoritativeEvent retirement = retirements[0];
        GenericActorRuntimeObservation.EventPayload.LifeRetired
            retiredPayload =
            (GenericActorRuntimeObservation.EventPayload.LifeRetired)
                retirement.Payload;
        if (!string.Equals(
                retiredPayload.Reason,
                "replication",
                StringComparison.Ordinal)
            || !string.Equals(
                retiredPayload.SourceTransitionId,
                reservation.TransitionId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A completed Split must retire its exact source as replication.",
                parameterName);
        }

        Dictionary<(int TeamId, int UnitId),
            GenericActorWorldSnapshot.SlotSnapshot> beforeSlots =
            before.Slots.ToDictionary(slot =>
                (slot.TeamId, slot.UnitId));
        long priorSpawnOrdinal = retirement.Ordinal;
        long firstFactOrdinal = retirement.Ordinal;
        long priorPurgeOrdinal = -1;
        foreach (SplitReplicationSpawn spawn in completion.Descendants
                     .OrderBy(descendant => descendant.TeamId)
                     .ThenBy(descendant => descendant.UnitId))
        {
            GenericActorWorldSnapshot.SlotSnapshot priorSlot =
                beforeSlots[(spawn.TeamId, spawn.UnitId)];
            var actorId = new ActorIdentity(
                spawn.TeamId,
                spawn.UnitId,
                priorSlot.NextLifeId);
            GenericActorLifeStart[] starts = tickStart.LifeStarts
                .Where(value => value.ActorId == actorId)
                .ToArray();
            GenericActorWorldSnapshot.LifeSnapshot[] lives =
                tickStart.State.ActiveLives
                    .Where(value => value.ActorId == actorId)
                    .ToArray();
            GenericActorAuthoritativeEvent[] spawnEvents =
                tickStart.Events.Where(item =>
                    item.Payload is
                        GenericActorRuntimeObservation.EventPayload
                            .LifeSpawned spawned
                    && spawned.ActorId == actorId)
                .ToArray();
            if (starts.Length != 1
                || lives.Length != 1
                || spawnEvents.Length != 1)
            {
                throw new ArgumentException(
                    "Every completed Split descendant needs one exact life start, active life, and spawn event.",
                    parameterName);
            }
            GenericActorLifeStart start = starts[0];
            GenericActorWorldSnapshot.LifeSnapshot life = lives[0];
            GenericActorAuthoritativeEvent spawnEvent = spawnEvents[0];
            int? expectedEnergy = InitialFormEnergy(
                definition,
                spawn.FormId);
            if (spawnEvent.Ordinal <= priorSpawnOrdinal
                || start.ParticipantId != reservation.ParticipantId
                || start.Origin.Reason
                    != GenericActorRuntimeStart.SpawnReason.Replication
                || start.Origin.ParentActorId
                    != reservation.SourceActorId
                || !string.Equals(
                    start.Origin.SourceTransitionId,
                    reservation.TransitionId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    start.Origin.SourceOperationId,
                    reservation.OperationId,
                    StringComparison.Ordinal)
                || life.ParticipantId != reservation.ParticipantId
                || life.Generation != spawn.Generation
                || !string.Equals(
                    life.FormId,
                    spawn.FormId,
                    StringComparison.Ordinal)
                || life.Position != spawn.Position
                || life.Facing != reservation.SourceFacing
                || life.Health != spawn.Health
                || life.Cooldown != 0
                || life.Energy != expectedEnergy
                || life.SpawnedAtTick != tickStart.Tick
                || life.SpawnReason
                    != GenericActorRuntimeStart.SpawnReason.Replication
                || life.ParentActorId != reservation.SourceActorId
                || !string.Equals(
                    life.SourceTransitionId,
                    reservation.TransitionId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    life.SourceOperationId,
                    reservation.OperationId,
                    StringComparison.Ordinal)
                || life.PreviousActionResolution is not null
                || life.PendingSameLifeTransition is not null)
            {
                throw new ArgumentException(
                    "A completed Split must create each canonical descendant with fresh runtime defaults and captured lineage.",
                    parameterName);
            }
            priorSpawnOrdinal = spawnEvent.Ordinal;

            GenericActorProjectileTraversal[] purges =
                tickStart.Traversals.Where(traversal =>
                    traversal.Trigger
                        == GenericActorProjectileTraversal.TraversalTrigger
                            .LifecyclePlacement
                    && traversal.Terminal is
                        GenericActorProjectileTraversal.TerminalDisposition
                            .LifecyclePlacementPurge purge
                    && purge.Position == spawn.Position)
                .OrderBy(traversal => traversal.Ordinal)
                .ToArray();
            if (purges.Any(traversal =>
                    traversal.Ordinal >= retirement.Ordinal)
                || purges.Length > 0
                && purges[0].Ordinal <= priorPurgeOrdinal
                || !purges.Select(traversal => traversal.ProjectileId)
                    .SequenceEqual(purges
                        .Select(traversal => traversal.ProjectileId)
                        .Order()))
            {
                throw new ArgumentException(
                    "Split output purges must follow canonical descendant and projectile order before source retirement.",
                    parameterName);
            }
            if (purges.Length > 0)
            {
                priorPurgeOrdinal = purges[^1].Ordinal;
                firstFactOrdinal = Math.Min(
                    firstFactOrdinal,
                    purges.Min(traversal => traversal.Ordinal));
            }
        }
        if (priorSpawnOrdinal >= completionEvent.Ordinal)
        {
            throw new ArgumentException(
                "Split completion must follow every canonical descendant spawn.",
                parameterName);
        }
        return firstFactOrdinal;
    }

    private static long ValidateCancelledSplitBoundary(
        GenericActorMatchTickStart tickStart,
        SplitReplicationReservation reservation,
        GenericActorAuthoritativeEvent cancellationEvent,
        string parameterName)
    {
        if (tickStart.LifeStarts.Any(start => string.Equals(
                start.Origin.SourceOperationId,
                reservation.OperationId,
                StringComparison.Ordinal))
            || tickStart.Events.Any(item =>
                item.Payload is
                    GenericActorRuntimeObservation.EventPayload.LifeRetired
                        retired
                && string.Equals(
                    retired.SourceOperationId,
                    reservation.OperationId,
                    StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "A cancelled Split cannot retire its source or start descendants.",
                parameterName);
        }
        return cancellationEvent.Ordinal;
    }

    private static string SplitCompletionCancellationReason(
        SplitReplicationCompletion.SplitCancellationReason reason) =>
        reason switch
        {
            SplitReplicationCompletion.SplitCancellationReason
                    .SourceUnavailable =>
                "source-unavailable",
            SplitReplicationCompletion.SplitCancellationReason
                    .SourceIdentityChanged =>
                "source-identity-changed",
            SplitReplicationCompletion.SplitCancellationReason
                    .SourceStateChanged =>
                "source-state-changed",
            SplitReplicationCompletion.SplitCancellationReason
                    .InsufficientHealth =>
                "insufficient-health",
            _ => throw new ArgumentOutOfRangeException(nameof(reason)),
        };

    private static int? InitialFormEnergy(
        ActorResolvedMatchDefinition definition,
        string formId)
    {
        ActorFormDefinition form = definition.Rules.Forms.Single(value =>
            string.Equals(
                value.Id,
                formId,
                StringComparison.Ordinal));
        if (form.AttackProfileId is not string attackProfileId)
            return null;
        ActorAttackProfileDefinition attack =
            definition.Rules.AttackProfiles.Single(value =>
                string.Equals(
                    value.Id,
                    attackProfileId,
                    StringComparison.Ordinal));
        return attack.MaxEnergy > 0 ? attack.MaxEnergy : null;
    }

    private sealed record SplitBoundaryBundle(
        SplitReplicationReservation Reservation,
        GenericActorAuthoritativeEvent Event,
        long BundleStartOrdinal);

    private static void ValidateFabricationTickStartOrdering(
        ActorResolvedMatchDefinition definition,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        IReadOnlyCollection<GenericActorProjectileTraversal> traversals,
        string parameterName)
    {
        GenericActorAuthoritativeEvent[] completions =
            FabricationLifecycleEvents(definition, events)
                .Where(item => item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .LifecycleCompleted)
                .OrderBy(item => item.Ordinal)
                .ToArray();
        GenericActorAuthoritativeEvent[] canonical = completions
            .OrderBy(item => LifecyclePayload(item).SourceActorId)
            .ThenBy(
                item => LifecyclePayload(item).TransitionId,
                StringComparer.Ordinal)
            .ThenBy(item =>
                LifecyclePayload(item).TargetTeamId ?? int.MaxValue)
            .ThenBy(item =>
                LifecyclePayload(item).TargetUnitId ?? int.MaxValue)
            .ThenBy(
                item => LifecyclePayload(item).OperationId,
                StringComparer.Ordinal)
            .ToArray();
        if (!completions.SequenceEqual(canonical))
        {
            throw new ArgumentException(
                "Due fabrications must complete in canonical source, transition, target, and operation order.",
                parameterName);
        }
        if (completions.Length == 0)
            return;

        long priorBundleEnd = -1;
        foreach (GenericActorAuthoritativeEvent completion in completions)
        {
            GenericActorRuntimeObservation.EventPayload.Lifecycle payload =
                LifecyclePayload(completion);
            GenericActorAuthoritativeEvent spawn = events.Single(item =>
                item.Payload is
                    GenericActorRuntimeObservation.EventPayload.LifeSpawned
                        spawned
                && spawned.Reason
                    == GenericActorRuntimeStart.SpawnReason.Fabrication
                && string.Equals(
                    spawned.SourceOperationId,
                    payload.OperationId,
                    StringComparison.Ordinal));
            Position position =
                ((GenericActorRuntimeObservation.EventPayload.LifeSpawned)
                    spawn.Payload).Position;
            GenericActorProjectileTraversal[] purges = traversals
                .Where(traversal =>
                    traversal.Trigger
                        == GenericActorProjectileTraversal.TraversalTrigger
                            .LifecyclePlacement
                    && traversal.Terminal is
                        GenericActorProjectileTraversal.TerminalDisposition
                            .LifecyclePlacementPurge purge
                    && purge.Position == position)
                .OrderBy(traversal => traversal.Ordinal)
                .ToArray();
            long bundleStart = purges
                .Select(traversal => traversal.Ordinal)
                .Append(spawn.Ordinal)
                .Min();
            if (bundleStart <= priorBundleEnd
                || purges.Any(traversal =>
                    traversal.Ordinal >= spawn.Ordinal)
                || !purges.Select(traversal => traversal.ProjectileId)
                    .SequenceEqual(purges
                        .Select(traversal => traversal.ProjectileId)
                        .Order())
                || spawn.Ordinal >= completion.Ordinal)
            {
                throw new ArgumentException(
                    "Each due fabrication bundle must atomically purge its output, spawn its child, and complete before the next canonical bundle.",
                    parameterName);
            }
            priorBundleEnd = completion.Ordinal;
        }

        GenericActorAuthoritativeEvent[] automaticReturns = events
            .Where(item =>
                item.Payload is
                    GenericActorRuntimeObservation.EventPayload.LifeSpawned
                        spawned
                && spawned.Reason
                    == GenericActorRuntimeStart.SpawnReason.AutomaticReturn)
            .OrderBy(item => item.Ordinal)
            .ToArray();
        GenericActorAuthoritativeEvent[] canonicalReturns =
            automaticReturns
                .OrderBy(item =>
                    ((GenericActorRuntimeObservation.EventPayload
                        .LifeSpawned)item.Payload).ActorId)
                .ToArray();
        long firstFabricationOrdinal = events
            .Where(item =>
                completions.Contains(item)
                || item.Payload is
                    GenericActorRuntimeObservation.EventPayload.LifeSpawned
                        spawned
                && spawned.Reason
                    == GenericActorRuntimeStart.SpawnReason.Fabrication)
            .Min(item => item.Ordinal);
        if (!automaticReturns.SequenceEqual(canonicalReturns)
            || automaticReturns.Any(item =>
                item.Ordinal > firstFabricationOrdinal))
        {
            throw new ArgumentException(
                "Automatic-return clock work must complete in stable-slot order before fabrication.",
                parameterName);
        }

        long finalFabricationOrdinal = completions[^1].Ordinal;
        bool laterPhaseAppearsEarly = events.Any(item =>
            item.Ordinal < finalFabricationOrdinal
            && (item.Kind is
                    GenericActorRuntimeObservation.EventKind
                        .FormTransitionStarted
                    or GenericActorRuntimeObservation.EventKind
                        .FormTransitionCompleted
                    or GenericActorRuntimeObservation.EventKind
                        .FormTransitionCancelled
                || item.Payload is
                    GenericActorRuntimeObservation.EventPayload.LifeSpawned
                        spawned
                && spawned.Reason
                    == GenericActorRuntimeStart.SpawnReason.Replication
                || item.Payload is
                    GenericActorRuntimeObservation.EventPayload.LifeRetired
                        retired
                && retired.SourceTransitionId is string transitionId
                && IsReplicationTransition(
                    definition,
                    transitionId)
                || item.Payload is
                    GenericActorRuntimeObservation.EventPayload.Lifecycle
                        lifecycle
                && IsReplicationTransition(
                    definition,
                    lifecycle.TransitionId)));
        if (laterPhaseAppearsEarly)
        {
            throw new ArgumentException(
                "Fabrication completion must precede Split and same-life tick-start work.",
                parameterName);
        }
    }

    private static void ValidateFabricationCompletion(
        ActorResolvedMatchDefinition definition,
        FabricationPendingFact pendingFact,
        GenericActorAuthoritativeEvent completionEvent,
        GenericActorMatchTickStart tickStart,
        string parameterName)
    {
        GenericActorWorldSnapshot.SlotSnapshot targetSlot =
            tickStart.State.Slots.Single(slot =>
                slot.TeamId == pendingFact.TargetTeamId
                && slot.UnitId == pendingFact.TargetUnitId);
        if (targetSlot.State is not
                GenericActorRuntimeObservation.UnitSlotState.Active active
            || targetSlot.ParticipantId != pendingFact.ParticipantId
            || targetSlot.NextLifeId
                != checked(pendingFact.NextLifeId + 1)
            || active.ActorId
                != new ActorIdentity(
                    pendingFact.TargetTeamId,
                    pendingFact.TargetUnitId,
                    pendingFact.NextLifeId)
            || !string.Equals(
                active.FormId,
                pendingFact.Pending.TargetFormId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A completed fabrication must activate the exact reserved target slot with its next life ID and output form.",
                parameterName);
        }

        BoundedChildFabricationDefinition transition =
            FabricationTransition(
                definition,
                pendingFact.Pending.TransitionId,
                parameterName);
        GenericActorWorldSnapshot.LifeSnapshot child =
            tickStart.State.ActiveLives.Single(life =>
                life.ActorId == active.ActorId);
        GenericActorLifeStart start = tickStart.LifeStarts.SingleOrDefault(
                value => value.ActorId == child.ActorId)
            ?? throw new ArgumentException(
                "A completed fabrication must record one exact child life start.",
                parameterName);
        ActorFormDefinition outputForm = definition.Rules.Forms.Single(
            form => string.Equals(
                form.Id,
                transition.OutputFormId,
                StringComparison.Ordinal));
        Direction outputFacing = definition.ParticipantRegionAssignments
            .Single(assignment =>
                assignment.ParticipantId == pendingFact.ParticipantId
                && string.Equals(
                    assignment.RegionRoleId,
                    transition.OutputRegionRoleId,
                    StringComparison.Ordinal))
            .Facing;
        int? outputEnergy = null;
        if (outputForm.AttackProfileId is string attackProfileId)
        {
            ActorAttackProfileDefinition attack =
                definition.Rules.AttackProfiles.Single(profile =>
                    string.Equals(
                        profile.Id,
                        attackProfileId,
                        StringComparison.Ordinal));
            if (attack.MaxEnergy > 0)
                outputEnergy = attack.MaxEnergy;
        }

        if (child.ParticipantId != pendingFact.ParticipantId
            || child.Generation != start.Origin.Generation
            || !string.Equals(
                child.FormId,
                transition.OutputFormId,
                StringComparison.Ordinal)
            || child.Position != pendingFact.Pending.ReservedPosition
            || child.Facing != outputFacing
            || child.Health != outputForm.MaxHealth
            || child.Cooldown != 0
            || child.Energy != outputEnergy
            || child.SpawnedAtTick != tickStart.Tick
            || child.SpawnReason
                != GenericActorRuntimeStart.SpawnReason.Fabrication
            || child.ParentActorId
                != pendingFact.Pending.SourceActorId
            || !string.Equals(
                child.SourceTransitionId,
                pendingFact.Pending.TransitionId,
                StringComparison.Ordinal)
            || !string.Equals(
                child.SourceOperationId,
                pendingFact.Pending.OperationId,
                StringComparison.Ordinal)
            || child.PreviousActionResolution is not null
            || child.PendingSameLifeTransition is not null
            || start.ParticipantId != pendingFact.ParticipantId
            || start.Origin.Reason
                != GenericActorRuntimeStart.SpawnReason.Fabrication
            || start.Origin.ParentActorId
                != pendingFact.Pending.SourceActorId
            || !string.Equals(
                start.Origin.SourceTransitionId,
                pendingFact.Pending.TransitionId,
                StringComparison.Ordinal)
            || !string.Equals(
                start.Origin.SourceOperationId,
                pendingFact.Pending.OperationId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A fabricated life must use the reserved placement, captured lineage, target defaults, and a fresh runtime state.",
                parameterName);
        }

        GenericActorAuthoritativeEvent spawnEvent =
            tickStart.Events.Single(item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind.LifeSpawned
                && item.Payload is
                    GenericActorRuntimeObservation.EventPayload.LifeSpawned
                        spawned
                && spawned.ActorId == child.ActorId);
        if (spawnEvent.Ordinal >= completionEvent.Ordinal)
        {
            throw new ArgumentException(
                "Fabrication completion evidence must follow its child spawn evidence.",
                parameterName);
        }
    }

    private static void ValidateLifecycleReservationArbitration(
        ActorResolvedMatchDefinition definition,
        GenericActorWorldSnapshot before,
        GenericActorWorldSnapshot after,
        IReadOnlyCollection<GenericActorMatchActorTurn> turns,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        IReadOnlyCollection<GenericActorProjectileTraversal> traversals,
        IReadOnlySet<ActorIdentity> actorsWithPriorSameLifeTransition,
        string parameterName)
    {
        Dictionary<ActorIdentity, LifecycleQueuePose> poses =
            LifecycleQueuePoses(
                definition,
                before,
                turns,
                events,
                traversals,
                parameterName);
        BoundedChildFabricationActorSnapshot[] fabricationActors =
            before.ActiveLives
                .OrderBy(life => life.ActorId)
                .Select(life =>
                {
                    LifecycleQueuePose pose = poses[life.ActorId];
                    return new BoundedChildFabricationActorSnapshot(
                        life.ActorId,
                        life.ParticipantId,
                        life.Generation,
                        life.FormId,
                        pose.Position,
                        pose.Facing);
                })
                .ToArray();
        SplitReplicationActorSnapshot[] splitActors = before.ActiveLives
            .OrderBy(life => life.ActorId)
            .Select(life =>
            {
                LifecycleQueuePose pose = poses[life.ActorId];
                return new SplitReplicationActorSnapshot(
                    life.ActorId,
                    life.ParticipantId,
                    life.Generation,
                    life.FormId,
                    life.Health,
                    pose.Position,
                    pose.Facing,
                    actorsWithPriorSameLifeTransition.Contains(
                        life.ActorId),
                    life.PendingSameLifeTransition is not null);
            })
            .ToArray();
        BoundedChildFabricationSlotSnapshot[] fabricationSlots =
            before.Slots
                .OrderBy(slot => slot.TeamId)
                .ThenBy(slot => slot.UnitId)
                .Select(FabricationQueueSlot)
                .ToArray();
        SplitReplicationSlotSnapshot[] splitSlots = before.Slots
            .OrderBy(slot => slot.TeamId)
            .ThenBy(slot => slot.UnitId)
            .Select(SplitQueueSlot)
            .ToArray();

        var fabricationRequests =
            new List<BoundedChildFabricationRequest>();
        var splitRequests = new List<SplitReplicationRequest>();
        var turnsByOperation =
            new Dictionary<string, GenericActorMatchActorTurn>(
                StringComparer.Ordinal);
        Dictionary<ActorIdentity, GenericActorWorldSnapshot.LifeSnapshot>
            livesByActor = before.ActiveLives.ToDictionary(
                life => life.ActorId);
        Dictionary<string, ActorActionDefinition> actions =
            definition.Rules.Actions.ToDictionary(
                action => action.Id,
                StringComparer.Ordinal);
        Dictionary<string, ActorFormDefinition> forms =
            definition.Rules.Forms.ToDictionary(
                form => form.Id,
                StringComparer.Ordinal);
        HashSet<ActorIdentity> pendingSplitSources =
            before.PendingReplications
                .Select(reservation => reservation.SourceActorId)
                .ToHashSet();
        foreach (GenericActorMatchActorTurn turn in turns
                     .OrderBy(turn => turn.ActorId))
        {
            GenericActorRuntimeActionResolution resolution =
                turn.ActionResolution;
            ActorActionDefinition action =
                actions[resolution.ValidatedAction.ActionId];
            if (action.Kind is not ActorActionKind.Fabrication
                and not ActorActionKind.Replication
                || resolution.RuntimeFault is not null)
            {
                continue;
            }

            GenericActorWorldSnapshot.LifeSnapshot life =
                livesByActor[turn.ActorId];
            ActorFormDefinition form = forms[life.FormId];
            if (!form.AllowedActionIds.Contains(
                    action.Id,
                    StringComparer.Ordinal))
            {
                RequireLifecycleActionOutcome(
                    turn,
                    GenericActorRuntimeActionResolution.ActionOutcome
                        .Rejected,
                    parameterName);
                continue;
            }
            if (pendingSplitSources.Contains(turn.ActorId)
                || life.PendingSameLifeTransition is not null)
            {
                RequireLifecycleActionOutcome(
                    turn,
                    GenericActorRuntimeActionResolution.ActionOutcome
                        .Blocked,
                    parameterName);
                continue;
            }

            if (action.Kind == ActorActionKind.Fabrication)
            {
                BoundedChildFabricationDefinition[] matches = definition
                    .Rules.FabricationTransitions
                    .OfType<BoundedChildFabricationDefinition>()
                    .Where(transition =>
                        string.Equals(
                            transition.ActionId,
                            action.Id,
                            StringComparison.Ordinal)
                        && transition.SourceFormIds.Contains(
                            life.FormId,
                            StringComparer.Ordinal))
                    .ToArray();
                if (matches.Length != 1)
                {
                    RequireLifecycleActionOutcome(
                        turn,
                        GenericActorRuntimeActionResolution.ActionOutcome
                            .Blocked,
                        parameterName);
                    continue;
                }
                GenericActorRuntimeActionArgument.UnitTarget target =
                    resolution.ValidatedAction.Arguments
                        .OfType<GenericActorRuntimeActionArgument
                            .UnitTargetArgument>()
                        .Single()
                        .Value;
                string operationId =
                    $"fabrication:{turn.Tick}:{turn.ActorId.TeamId}:" +
                    $"{turn.ActorId.UnitId}:{turn.ActorId.LifeId}:" +
                    $"{matches[0].TransitionId}:{target.TeamId}:" +
                    $"{target.UnitId}";
                fabricationRequests.Add(
                    new BoundedChildFabricationRequest(
                        turn.ActorId,
                        matches[0].TransitionId,
                        operationId,
                        target.TeamId,
                        target.UnitId));
                turnsByOperation.Add(operationId, turn);
                continue;
            }

            SplitReplicationTransitionDefinition[] splitMatches =
                definition.Rules.ReplicationTransitions
                    .OfType<SplitReplicationTransitionDefinition>()
                    .Where(transition =>
                        string.Equals(
                            transition.ActionId,
                            action.Id,
                            StringComparison.Ordinal)
                        && transition.SourceFormIds.Contains(
                            life.FormId,
                            StringComparer.Ordinal))
                    .ToArray();
            if (splitMatches.Length != 1)
            {
                RequireLifecycleActionOutcome(
                    turn,
                    GenericActorRuntimeActionResolution.ActionOutcome
                        .Blocked,
                    parameterName);
                continue;
            }
            string splitOperationId =
                $"split:{turn.Tick}:{turn.ActorId.TeamId}:" +
                $"{turn.ActorId.UnitId}:{turn.ActorId.LifeId}";
            splitRequests.Add(
                new SplitReplicationRequest(
                    turn.ActorId,
                    splitMatches[0].TransitionId,
                    splitOperationId));
            turnsByOperation.Add(splitOperationId, turn);
        }

        Position[] existingTileClaims =
        [
            .. FabricationPendingFacts(before).Values
                .Select(fact => fact.Pending.ReservedPosition),
            .. before.PendingReplications
                .SelectMany(reservation => reservation.Descendants)
                .Select(descendant => descendant.Position),
        ];
        var fabricationKernel =
            new BoundedChildFabricationKernel(definition);
        var splitKernel = new SplitReplicationKernel(definition);
        ImmutableArray<BoundedChildFabricationReservationOutcome>
            provisionalFabrications =
            fabricationKernel.BuildProvisionalBatch(
                before.NextTick,
                fabricationRequests,
                fabricationActors,
                fabricationSlots,
                existingTileClaims);
        ImmutableArray<SplitReplicationReservationOutcome>
            provisionalSplits = splitKernel.BuildProvisionalBatch(
                before.NextTick,
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
        Dictionary<string, FabricationPendingFact> afterFabrications =
            FabricationPendingFacts(after);

        foreach (BoundedChildFabricationReservationOutcome outcome
                 in fabrications)
        {
            GenericActorMatchActorTurn turn =
                turnsByOperation[outcome.Request.OperationId];
            GenericActorRuntimeActionResolution.ActionOutcome expected =
                outcome.Outcome switch
                {
                    BoundedChildFabricationReservationOutcome
                            .FabricationReservationOutcomeKind.Reserved =>
                        GenericActorRuntimeActionResolution.ActionOutcome
                            .Success,
                    BoundedChildFabricationReservationOutcome
                            .FabricationReservationOutcomeKind.Blocked =>
                        GenericActorRuntimeActionResolution.ActionOutcome
                            .Blocked,
                    BoundedChildFabricationReservationOutcome
                            .FabricationReservationOutcomeKind.Rejected =>
                        GenericActorRuntimeActionResolution.ActionOutcome
                            .Rejected,
                    BoundedChildFabricationReservationOutcome
                            .FabricationReservationOutcomeKind.Faulted =>
                        GenericActorRuntimeActionResolution.ActionOutcome
                            .Faulted,
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(outcome)),
                };
            RequireLifecycleActionOutcome(
                turn,
                expected,
                parameterName);
            GenericActorAuthoritativeEvent[] queueEvents =
                LifecycleQueueEvents(
                    events,
                    outcome.Request.OperationId);
            if (outcome.Reservation is not
                BoundedChildFabricationProvisionalReservation reservation)
            {
                if (queueEvents.Length != 0
                    || afterFabrications.ContainsKey(
                        outcome.Request.OperationId))
                {
                    throw new ArgumentException(
                        "A blocked or rejected fabrication cannot retain a claim or queue evidence.",
                        parameterName);
                }
                continue;
            }

            if (queueEvents.Length != 1
                || !LifecyclePayloadMatchesReservation(
                    LifecyclePayload(queueEvents[0]),
                    reservation))
            {
                throw new ArgumentException(
                    "A successful fabrication must queue the exact canonical kernel reservation.",
                    parameterName);
            }
            if (afterFabrications.TryGetValue(
                    reservation.OperationId,
                    out FabricationPendingFact? pending)
                && !FabricationPendingMatchesReservation(
                    pending,
                    reservation))
            {
                throw new ArgumentException(
                    "A retained fabrication claim must preserve the kernel-selected slot, tile, form, clock, and lineage.",
                    parameterName);
            }
        }

        foreach (SplitReplicationReservationOutcome outcome in splits)
        {
            GenericActorMatchActorTurn turn =
                turnsByOperation[outcome.Request.OperationId];
            GenericActorRuntimeActionResolution.ActionOutcome expected =
                outcome.Reservation is null
                    ? GenericActorRuntimeActionResolution.ActionOutcome
                        .Blocked
                    : GenericActorRuntimeActionResolution.ActionOutcome
                        .Success;
            RequireLifecycleActionOutcome(
                turn,
                expected,
                parameterName);
            GenericActorAuthoritativeEvent[] queueEvents =
                LifecycleQueueEvents(
                    events,
                    outcome.Request.OperationId);
            if (outcome.Reservation is not
                SplitReplicationReservation reservation)
            {
                if (queueEvents.Length != 0
                    || after.PendingReplications.Any(candidate =>
                        string.Equals(
                            candidate.OperationId,
                            outcome.Request.OperationId,
                            StringComparison.Ordinal)))
                {
                    throw new ArgumentException(
                        "A blocked Split cannot retain a claim or queue evidence.",
                        parameterName);
                }
                continue;
            }

            if (queueEvents.Length != 1
                || !LifecyclePayloadMatchesReservation(
                    LifecyclePayload(queueEvents[0]),
                    reservation))
            {
                throw new ArgumentException(
                    "A successful Split must queue the exact jointly arbitrated kernel reservation.",
                    parameterName);
            }
            SplitReplicationReservation? retained =
                after.PendingReplications.SingleOrDefault(candidate =>
                    string.Equals(
                        candidate.OperationId,
                        reservation.OperationId,
                        StringComparison.Ordinal));
            if (retained is not null)
            {
                if (!ReplicationReservationsSemanticallyEqual(
                        retained,
                        reservation)
                    || SplitCancellationEvents(
                        events,
                        reservation.OperationId).Length != 0)
                {
                    throw new ArgumentException(
                        "A retained Split claim must preserve its exact canonical reservation without cancellation evidence.",
                        parameterName);
                }
            }
            else
            {
                GenericActorAuthoritativeEvent[] cancellations =
                    SplitCancellationEvents(
                        events,
                        reservation.OperationId);
                if (cancellations.Length != 1
                    || !ValidateSplitCancellation(
                        reservation,
                        cancellations[0],
                        before,
                        after,
                        events))
                {
                    throw new ArgumentException(
                        "A newly queued Split may disappear only through exact same-resolution source destruction or participant disqualification.",
                        parameterName);
                }
            }
        }

        ValidatePriorSplitResolutionContinuity(
            before,
            after,
            events,
            parameterName);
        ValidateLifecycleQueueOrdering(
            definition,
            before,
            events,
            parameterName);
    }

    private static Dictionary<ActorIdentity, LifecycleQueuePose>
        LifecycleQueuePoses(
            ActorResolvedMatchDefinition definition,
            GenericActorWorldSnapshot before,
            IReadOnlyCollection<GenericActorMatchActorTurn> turns,
            IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
            IReadOnlyCollection<GenericActorProjectileTraversal> traversals,
            string parameterName)
    {
        Dictionary<ActorIdentity, LifecycleQueuePose> poses =
            before.ActiveLives.ToDictionary(
                life => life.ActorId,
                life => new LifecycleQueuePose(
                    life.Position,
                    life.Facing));
        Dictionary<ActorIdentity, GenericActorMatchActorTurn> turnsByActor =
            turns.ToDictionary(turn => turn.ActorId);
        Dictionary<string, ActorActionDefinition> actions =
            definition.Rules.Actions.ToDictionary(
                action => action.Id,
                StringComparer.Ordinal);
        Dictionary<string, ActorFormDefinition> forms =
            definition.Rules.Forms.ToDictionary(
                form => form.Id,
                StringComparer.Ordinal);
        Dictionary<string, ActorMovementProfileDefinition> movementProfiles =
            definition.Rules.MovementProfiles.ToDictionary(
                profile => profile.Id,
                StringComparer.Ordinal);
        var rotatedActors = new HashSet<ActorIdentity>();
        foreach (GenericActorMatchActorTurn turn in turns)
        {
            ActorActionDefinition action =
                actions[turn.ActionResolution.ValidatedAction.ActionId];
            if (action.Kind != ActorActionKind.Rotation)
                continue;
            GenericActorWorldSnapshot.LifeSnapshot life =
                before.ActiveLives.Single(value =>
                    value.ActorId == turn.ActorId);
            GenericActorRuntimeActionResolution.ActionOutcome expected =
                !forms[life.FormId].AllowedActionIds.Contains(
                    action.Id,
                    StringComparer.Ordinal)
                    ? GenericActorRuntimeActionResolution.ActionOutcome.Rejected
                    : before.PendingReplications.Any(reservation =>
                        reservation.SourceActorId == turn.ActorId)
                    || life.PendingSameLifeTransition is not null
                        ? GenericActorRuntimeActionResolution.ActionOutcome
                            .Blocked
                        : GenericActorRuntimeActionResolution.ActionOutcome
                            .Success;
            if (turn.ActionResolution.Outcome != expected)
            {
                throw new ArgumentException(
                    "Rotation outcome does not match the reconstructed pre-movement state.",
                    parameterName);
            }
        }

        foreach (GenericActorAuthoritativeEvent item in events
                     .Where(item => item.Payload is
                         GenericActorRuntimeObservation.EventPayload.Rotation)
                     .OrderBy(item => item.Ordinal))
        {
            var rotation =
                (GenericActorRuntimeObservation.EventPayload.Rotation)
                    item.Payload;
            if (!poses.TryGetValue(
                    rotation.ActorId,
                    out LifecycleQueuePose pose)
                || !turnsByActor.TryGetValue(
                    rotation.ActorId,
                    out GenericActorMatchActorTurn? turn))
            {
                throw new ArgumentException(
                    "Rotation evidence references an actor outside the resolution snapshot.",
                    parameterName);
            }
            ActorActionDefinition action =
                actions[turn.ActionResolution.ValidatedAction.ActionId];
            Direction expectedFacing =
                turn.ActionResolution.ValidatedAction.Arguments
                    .OfType<GenericActorRuntimeActionArgument
                        .DirectionArgument>()
                    .Single()
                    .Value;
            if (rotation.Position != pose.Position
                || rotation.FromFacing != pose.Facing
                || rotation.ToFacing != expectedFacing
                || action.Kind != ActorActionKind.Rotation
                || turn.ActionResolution.Outcome
                    != GenericActorRuntimeActionResolution.ActionOutcome
                        .Success
                || !ResolvedActionsSemanticallyEqual(
                    turn.ActionResolution.ValidatedAction,
                    rotation.Action)
                || !rotatedActors.Add(rotation.ActorId))
            {
                throw new ArgumentException(
                    "Rotation evidence cannot forge the lifecycle queue-time pose.",
                    parameterName);
            }
            poses[rotation.ActorId] =
                pose with { Facing = rotation.ToFacing };
        }

        ActorIdentity[] expectedRotations = turns
            .Where(turn =>
                actions[turn.ActionResolution.ValidatedAction.ActionId].Kind
                    == ActorActionKind.Rotation
                && turn.ActionResolution.Outcome
                    == GenericActorRuntimeActionResolution.ActionOutcome
                        .Success)
            .Select(turn => turn.ActorId)
            .Order()
            .ToArray();
        if (!rotatedActors.Order().SequenceEqual(expectedRotations))
        {
            throw new ArgumentException(
                "Every successful rotation must have exactly one canonical pose-changing event.",
                parameterName);
        }

        var occupiedPositions = before.ActiveLives
            .Select(life => life.Position)
            .ToHashSet();
        var reservedLifecyclePositions = before.Slots
            .Select(slot => slot.State)
            .OfType<GenericActorRuntimeObservation.UnitSlotState
                .LifecyclePending>()
            .Select(pending => pending.ReservedPosition)
            .Concat(before.PendingReplications.SelectMany(reservation =>
                reservation.Descendants.Select(descendant =>
                    descendant.Position)))
            .ToHashSet();
        Dictionary<string, ActorLifecycleProfileDefinition> lifecycleProfiles =
            definition.Rules.Lifecycle.Profiles.ToDictionary(
                profile => profile.ProfileId,
                StringComparer.Ordinal);
        Dictionary<string, Position> spawnPositions =
            definition.Map.SpawnAnchors.ToDictionary(
                anchor => anchor.Spawn.SpawnId,
                anchor => anchor.Spawn.Position,
                StringComparer.Ordinal);
        var automaticReturnReservations =
            definition.LifecycleAssignments
                .Where(assignment =>
                    lifecycleProfiles[assignment.LifecycleProfileId]
                        .DestructionPolicy
                    == ActorLifecycleProfileDefinition
                        .DestructionPolicyKind.AutomaticRespawn)
                .Select(assignment => (
                    assignment.TeamId,
                    assignment.UnitId,
                    Position: spawnPositions[
                        assignment.AssignedRespawnSpawnId!]))
                .ToArray();

        var candidates =
            new Dictionary<ActorIdentity, MovementQueueCandidate>();
        foreach (GenericActorMatchActorTurn turn in turns)
        {
            ActorActionDefinition action =
                actions[turn.ActionResolution.ValidatedAction.ActionId];
            if (action.Kind != ActorActionKind.Movement)
                continue;
            GenericActorWorldSnapshot.LifeSnapshot life =
                before.ActiveLives.Single(value =>
                    value.ActorId == turn.ActorId);
            bool allowed = forms[life.FormId].AllowedActionIds.Contains(
                action.Id,
                StringComparer.Ordinal);
            bool preblocked = before.PendingReplications.Any(reservation =>
                    reservation.SourceActorId == turn.ActorId)
                || life.PendingSameLifeTransition is not null;
            if (!allowed || preblocked)
            {
                GenericActorRuntimeActionResolution.ActionOutcome expected =
                    allowed
                        ? GenericActorRuntimeActionResolution.ActionOutcome
                            .Blocked
                        : GenericActorRuntimeActionResolution.ActionOutcome
                            .Rejected;
                if (turn.ActionResolution.Outcome != expected)
                {
                    throw new ArgumentException(
                        "Movement outcome does not match the reconstructed pre-movement state.",
                        parameterName);
                }
                continue;
            }

            Direction direction =
                turn.ActionResolution.ValidatedAction.Arguments
                    .OfType<GenericActorRuntimeActionArgument
                        .DirectionArgument>()
                    .Single()
                    .Value;
            var (dx, dy) = direction.Vector();
            Position target = life.Position.Offset(dx, dy);
            bool foreignReturnReservation =
                automaticReturnReservations.Any(reservation =>
                    reservation.Position == target
                    && (reservation.TeamId != life.ActorId.TeamId
                        || reservation.UnitId != life.ActorId.UnitId));
            ActorMovementFacingCoupling coupling =
                movementProfiles[forms[life.FormId].MovementProfileId]
                    .FacingCoupling;
            Direction queueFacing = poses[turn.ActorId].Facing;
            bool blocked = definition.Map.IsWall(target)
                || occupiedPositions.Contains(target)
                || reservedLifecyclePositions.Contains(target)
                || foreignReturnReservation
                // Mirrors the session's defensive block: a FacingLocked mover
                // that somehow validated an off-facing direction is Blocked,
                // never displaced.
                || (coupling == ActorMovementFacingCoupling.FacingLocked
                    && direction != queueFacing);
            candidates.Add(
                turn.ActorId,
                new MovementQueueCandidate(
                    life.Position,
                    target,
                    queueFacing,
                    coupling
                        == ActorMovementFacingCoupling.FaceMovementDirection
                        ? direction
                        : queueFacing,
                    blocked));
        }

        foreach (IGrouping<Position,
                     KeyValuePair<ActorIdentity, MovementQueueCandidate>>
                 claims in candidates.GroupBy(pair => pair.Value.Target))
        {
            if (claims.Count() <= 1)
                continue;
            foreach (KeyValuePair<ActorIdentity, MovementQueueCandidate> claim
                     in claims)
            {
                candidates[claim.Key] =
                    claim.Value with { Blocked = true };
            }
        }

        Dictionary<long, GenericActorWorldSnapshot.ProjectileSnapshot>
            availableProjectiles = before.Projectiles.ToDictionary(
                projectile => projectile.ProjectileId);
        var expectedMovementContacts =
            new Dictionary<long, MovementContactExpectation>();
        foreach ((ActorIdentity actorId, MovementQueueCandidate candidate)
                 in candidates.OrderBy(pair => pair.Key))
        {
            GenericActorWorldSnapshot.LifeSnapshot life =
                before.ActiveLives.Single(value =>
                    value.ActorId == actorId);
            foreach (GenericActorWorldSnapshot.ProjectileSnapshot projectile
                     in availableProjectiles.Values
                         .Where(projectile =>
                             projectile.Position == candidate.Target)
                         .OrderBy(projectile => projectile.ProjectileId)
                         .ToArray())
            {
                bool ignoresFiringLife =
                    definition.Rules.Collisions.ProjectilesIgnoreFiringLife
                    && actorId == projectile.OwnerActorId;
                bool allied = actorId.TeamId == projectile.OwnerTeamId;
                bool consumes = !ignoresFiringLife
                    && (!allied
                        || definition.Rules.Collisions
                            .AlliedProjectileContact
                        != ActorCollisionDefinition
                            .AlliedProjectileContactKind.PassThrough);
                if (!consumes)
                    continue;
                bool damages = !allied
                    || definition.Rules.Collisions.AlliedProjectileContact
                    == ActorCollisionDefinition.AlliedProjectileContactKind
                        .DamageAndBlock;
                candidates[actorId] =
                    candidate with { Blocked = true };
                expectedMovementContacts.Add(
                    projectile.ProjectileId,
                    new MovementContactExpectation(
                        actorId,
                        damages,
                        projectile));
                availableProjectiles.Remove(projectile.ProjectileId);
            }
        }

        GenericActorProjectileTraversal[] movementContacts = traversals
            .Where(traversal =>
                traversal.Trigger
                    == GenericActorProjectileTraversal.TraversalTrigger
                        .MovementContact)
            .OrderBy(traversal => traversal.Ordinal)
            .ToArray();
        long[] canonicalMovementContactIds = expectedMovementContacts
            .OrderBy(pair => pair.Value.TargetActorId)
            .ThenBy(pair => pair.Key)
            .Select(pair => pair.Key)
            .ToArray();
        if (movementContacts.Length != expectedMovementContacts.Count
            || movementContacts.Select(traversal => traversal.ProjectileId)
                .Distinct()
                .Count() != movementContacts.Length
            || !movementContacts
                .Select(traversal => traversal.ProjectileId)
                .SequenceEqual(canonicalMovementContactIds))
        {
            throw new ArgumentException(
                "Movement-contact traversals must exactly match consuming destination projectiles in canonical actor and projectile order.",
                parameterName);
        }
        foreach (GenericActorProjectileTraversal traversal in movementContacts)
        {
            if (!expectedMovementContacts.TryGetValue(
                    traversal.ProjectileId,
                    out MovementContactExpectation expected)
                || traversal.OwnerParticipantId
                    != expected.Projectile.OwnerParticipantId
                || traversal.OwnerTeamId
                    != expected.Projectile.OwnerTeamId
                || traversal.OwnerActorId
                    != expected.Projectile.OwnerActorId
                || !string.Equals(
                    traversal.AttackProfileId,
                    expected.Projectile.AttackProfileId,
                    StringComparison.Ordinal)
                || traversal.From != expected.Projectile.Position
                || !traversal.Path.IsEmpty
                || traversal.LaunchHeading
                    != expected.Projectile.LaunchHeading
                || traversal.FinalHeading != expected.Projectile.Heading
                || traversal.ShotProgram != expected.Projectile.ShotProgram
                || traversal.Terminal is not
                    GenericActorProjectileTraversal.TerminalDisposition
                        .MovementContact contact
                || contact.TargetActorId != expected.TargetActorId
                || contact.AppliedDamage != expected.AppliedDamage)
            {
                throw new ArgumentException(
                    "Movement-contact traversal evidence does not match the reconstructed collision.",
                    parameterName);
            }
        }

        Dictionary<ActorIdentity, GenericActorAuthoritativeEvent[]>
            movementEventsByActor = events
                .Where(item => item.Payload is
                    GenericActorRuntimeObservation.EventPayload.Movement
                    or GenericActorRuntimeObservation.EventPayload
                        .MovementBlocked)
                .GroupBy(item => item.Payload switch
                {
                    GenericActorRuntimeObservation.EventPayload.Movement
                        movement => movement.ActorId,
                    GenericActorRuntimeObservation.EventPayload
                        .MovementBlocked blocked => blocked.ActorId,
                    _ => throw new InvalidOperationException(
                        "Unknown movement evidence."),
                })
                .ToDictionary(
                    group => group.Key,
                    group => group.ToArray());
        if (movementEventsByActor.Any(pair =>
                !candidates.ContainsKey(pair.Key)
                || pair.Value.Length != 1))
        {
            throw new ArgumentException(
                "Only a reconstructed movement candidate may emit one movement result.",
                parameterName);
        }
        foreach ((ActorIdentity actorId, MovementQueueCandidate candidate)
                 in candidates.OrderBy(pair => pair.Key))
        {
            GenericActorMatchActorTurn turn = turnsByActor[actorId];
            GenericActorRuntimeActionResolution.ActionOutcome expectedOutcome =
                candidate.Blocked
                    ? GenericActorRuntimeActionResolution.ActionOutcome.Blocked
                    : GenericActorRuntimeActionResolution.ActionOutcome.Success;
            if (turn.ActionResolution.Outcome != expectedOutcome
                || !movementEventsByActor.TryGetValue(
                    actorId,
                    out GenericActorAuthoritativeEvent[]? actorEvents)
                || actorEvents.Length != 1)
            {
                throw new ArgumentException(
                    "Movement outcome and evidence must equal the reconstructed joint-grid result.",
                    parameterName);
            }
            GenericActorAuthoritativeEvent actorEvent = actorEvents[0];
            bool exact = actorEvent.Payload switch
            {
                GenericActorRuntimeObservation.EventPayload.Movement movement
                    when !candidate.Blocked =>
                    movement.From == candidate.Source
                    && movement.To == candidate.Target
                    // Under a facing-coupled movement profile the successful
                    // Movement event is itself the facing-change evidence, so
                    // it carries the post-step facing rather than the
                    // queue-time one.
                    && movement.Facing == candidate.SuccessFacing
                    && ResolvedActionsSemanticallyEqual(
                        turn.ActionResolution.ValidatedAction,
                        movement.Action),
                GenericActorRuntimeObservation.EventPayload.MovementBlocked
                    blocked when candidate.Blocked =>
                    blocked.From == candidate.Source
                    && blocked.AttemptedTo == candidate.Target
                    && blocked.Facing == candidate.Facing
                    && ResolvedActionsSemanticallyEqual(
                        turn.ActionResolution.ValidatedAction,
                        blocked.Action),
                _ => false,
            };
            if (!exact)
            {
                throw new ArgumentException(
                    "Movement evidence cannot forge the lifecycle queue-time occupancy snapshot.",
                    parameterName);
            }
            if (!candidate.Blocked)
            {
                poses[actorId] = poses[actorId] with
                {
                    Position = candidate.Target,
                    Facing = candidate.SuccessFacing,
                };
            }
        }

        GenericActorAuthoritativeEvent[] rotationEvents = events
            .Where(item => item.Kind
                == GenericActorRuntimeObservation.EventKind.Rotation)
            .OrderBy(item => item.Ordinal)
            .ToArray();
        GenericActorAuthoritativeEvent[] movementEvents = events
            .Where(item => item.Kind
                is GenericActorRuntimeObservation.EventKind.Movement
                or GenericActorRuntimeObservation.EventKind.MovementBlocked)
            .OrderBy(item => item.Ordinal)
            .ToArray();
        GenericActorAuthoritativeEvent[] queueEvents = events
            .Where(item => item.Kind
                == GenericActorRuntimeObservation.EventKind.LifecycleQueued)
            .OrderBy(item => item.Ordinal)
            .ToArray();
        if (!rotationEvents.SequenceEqual(
                rotationEvents.OrderBy(item =>
                    ((GenericActorRuntimeObservation.EventPayload.Rotation)
                        item.Payload).ActorId))
            || !movementEvents.SequenceEqual(
                movementEvents.OrderBy(item => item.Payload switch
                {
                    GenericActorRuntimeObservation.EventPayload.Movement
                        movement => movement.ActorId,
                    GenericActorRuntimeObservation.EventPayload
                        .MovementBlocked blocked => blocked.ActorId,
                    _ => throw new InvalidOperationException(
                        "Unknown movement evidence."),
                }))
            || !LifecycleFactPhasesAreOrdered(
                rotationEvents.Select(item => item.Ordinal).ToArray(),
                movementContacts.Select(item => item.Ordinal).ToArray(),
                movementEvents.Select(item => item.Ordinal).ToArray(),
                queueEvents.Select(item => item.Ordinal).ToArray()))
        {
            throw new ArgumentException(
                "Canonical rotation, movement-contact, and movement phases must finish before lifecycle reservation evidence.",
                parameterName);
        }
        return poses;
    }

    private static BoundedChildFabricationSlotSnapshot
        FabricationQueueSlot(
            GenericActorWorldSnapshot.SlotSnapshot slot) =>
        new(
            slot.TeamId,
            slot.UnitId,
            slot.State switch
            {
                GenericActorRuntimeObservation.UnitSlotState.Active =>
                    BoundedChildFabricationSlotSnapshot
                        .FabricationSlotState.Active,
                GenericActorRuntimeObservation.UnitSlotState.Ready =>
                    BoundedChildFabricationSlotSnapshot
                        .FabricationSlotState.Ready,
                GenericActorRuntimeObservation.UnitSlotState
                        .FabricationPending
                    or GenericActorRuntimeObservation.UnitSlotState
                        .ReplicationPending =>
                    BoundedChildFabricationSlotSnapshot
                        .FabricationSlotState.Reserved,
                GenericActorRuntimeObservation.UnitSlotState
                        .PermanentlyDormant =>
                    BoundedChildFabricationSlotSnapshot
                        .FabricationSlotState.PermanentlyDormant,
                _ => BoundedChildFabricationSlotSnapshot
                    .FabricationSlotState.Unavailable,
            },
            slot.State is
                GenericActorRuntimeObservation.UnitSlotState.Active active
                ? active.ActorId
                : null);

    private static SplitReplicationSlotSnapshot SplitQueueSlot(
        GenericActorWorldSnapshot.SlotSnapshot slot) =>
        new(
            slot.TeamId,
            slot.UnitId,
            slot.State switch
            {
                GenericActorRuntimeObservation.UnitSlotState.Active =>
                    SplitReplicationSlotSnapshot.SplitSlotState.Active,
                GenericActorRuntimeObservation.UnitSlotState.Ready =>
                    SplitReplicationSlotSnapshot.SplitSlotState.Ready,
                GenericActorRuntimeObservation.UnitSlotState
                        .FabricationPending
                    or GenericActorRuntimeObservation.UnitSlotState
                        .ReplicationPending =>
                    SplitReplicationSlotSnapshot.SplitSlotState.Reserved,
                GenericActorRuntimeObservation.UnitSlotState
                        .PermanentlyDormant =>
                    SplitReplicationSlotSnapshot.SplitSlotState
                        .PermanentlyDormant,
                _ => SplitReplicationSlotSnapshot.SplitSlotState
                    .Unavailable,
            },
            slot.State is
                GenericActorRuntimeObservation.UnitSlotState.Active active
                ? active.ActorId
                : null);

    private static void RequireLifecycleActionOutcome(
        GenericActorMatchActorTurn turn,
        GenericActorRuntimeActionResolution.ActionOutcome expected,
        string parameterName)
    {
        if (turn.ActionResolution.Outcome != expected)
        {
            throw new ArgumentException(
                "A lifecycle action outcome must equal its reconstructed joint kernel result.",
                parameterName);
        }
    }

    private static GenericActorAuthoritativeEvent[] LifecycleQueueEvents(
        IEnumerable<GenericActorAuthoritativeEvent> events,
        string operationId) =>
        events.Where(item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .LifecycleQueued
                && item.Payload is
                    GenericActorRuntimeObservation.EventPayload.Lifecycle
                        lifecycle
                && string.Equals(
                    lifecycle.OperationId,
                    operationId,
                    StringComparison.Ordinal))
            .ToArray();

    private static bool LifecyclePayloadMatchesReservation(
        GenericActorRuntimeObservation.EventPayload.Lifecycle payload,
        BoundedChildFabricationProvisionalReservation reservation) =>
        string.Equals(
            payload.TransitionId,
            reservation.TransitionId,
            StringComparison.Ordinal)
        && string.Equals(
            payload.OperationId,
            reservation.OperationId,
            StringComparison.Ordinal)
        && payload.SourceActorId == reservation.SourceActorId
        && payload.TargetTeamId == reservation.TargetTeamId
        && payload.TargetUnitId == reservation.TargetUnitId
        && payload.DueTick == reservation.DueTick
        && payload.CancellationReason is null;

    private static bool LifecyclePayloadMatchesReservation(
        GenericActorRuntimeObservation.EventPayload.Lifecycle payload,
        SplitReplicationReservation reservation) =>
        LifecyclePayloadMatchesSplitWork(
            payload,
            reservation,
            cancellationReason: null);

    private static bool LifecyclePayloadMatchesSplitWork(
        GenericActorRuntimeObservation.EventPayload.Lifecycle payload,
        SplitReplicationReservation reservation,
        string? cancellationReason)
    {
        SplitReplicationReservedDescendant? target =
            reservation.Descendants
                .Where(descendant =>
                    descendant.TeamId
                        != reservation.SourceActorId.TeamId
                    || descendant.UnitId
                        != reservation.SourceActorId.UnitId)
                .OrderBy(descendant => descendant.TeamId)
                .ThenBy(descendant => descendant.UnitId)
                .FirstOrDefault();
        return string.Equals(
                payload.TransitionId,
                reservation.TransitionId,
                StringComparison.Ordinal)
            && string.Equals(
                payload.OperationId,
                reservation.OperationId,
                StringComparison.Ordinal)
            && payload.SourceActorId == reservation.SourceActorId
            && payload.TargetTeamId == target?.TeamId
            && payload.TargetUnitId == target?.UnitId
            && payload.DueTick == reservation.DueTick
            && string.Equals(
                payload.CancellationReason,
                cancellationReason,
                StringComparison.Ordinal);
    }

    private static void ValidatePriorSplitResolutionContinuity(
        GenericActorWorldSnapshot before,
        GenericActorWorldSnapshot after,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        string parameterName)
    {
        foreach (SplitReplicationReservation reservation
                 in before.PendingReplications)
        {
            SplitReplicationReservation? retained =
                after.PendingReplications.SingleOrDefault(candidate =>
                    string.Equals(
                        candidate.OperationId,
                        reservation.OperationId,
                        StringComparison.Ordinal));
            GenericActorAuthoritativeEvent[] cancellations =
                SplitCancellationEvents(
                    events,
                    reservation.OperationId);
            bool hasOtherLifecycleEvidence = events.Any(item =>
                item.Payload is
                    GenericActorRuntimeObservation.EventPayload.Lifecycle
                        lifecycle
                && string.Equals(
                    lifecycle.OperationId,
                    reservation.OperationId,
                    StringComparison.Ordinal)
                && item.Kind
                    != GenericActorRuntimeObservation.EventKind
                        .LifecycleCancelled);
            if (retained is not null)
            {
                if (!ReplicationReservationsSemanticallyEqual(
                        retained,
                        reservation)
                    || cancellations.Length != 0
                    || hasOtherLifecycleEvidence)
                {
                    throw new ArgumentException(
                        "A not-yet-due Split must remain exact through resolution without lifecycle evidence.",
                        parameterName);
                }
                continue;
            }

            if (cancellations.Length != 1
                || hasOtherLifecycleEvidence
                || !ValidateSplitCancellation(
                    reservation,
                    cancellations[0],
                    before,
                    after,
                    events))
            {
                throw new ArgumentException(
                    "A pending Split may disappear during resolution only through exact source destruction or participant-disqualification cancellation.",
                    parameterName);
            }
        }
    }

    private static GenericActorAuthoritativeEvent[] SplitCancellationEvents(
        IEnumerable<GenericActorAuthoritativeEvent> events,
        string operationId) =>
        events.Where(item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .LifecycleCancelled
                && item.Payload is
                    GenericActorRuntimeObservation.EventPayload.Lifecycle
                        lifecycle
                && string.Equals(
                    lifecycle.OperationId,
                    operationId,
                    StringComparison.Ordinal))
            .ToArray();

    private static bool ValidateSplitCancellation(
        SplitReplicationReservation reservation,
        GenericActorAuthoritativeEvent cancellation,
        GenericActorWorldSnapshot before,
        GenericActorWorldSnapshot after,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events)
    {
        GenericActorRuntimeObservation.EventPayload.Lifecycle payload =
            LifecyclePayload(cancellation);
        if (string.Equals(
                payload.CancellationReason,
                "participant-disqualified",
                StringComparison.Ordinal))
        {
            GenericActorAuthoritativeEvent? disqualification = events
                .SingleOrDefault(item =>
                    item.Kind
                        == GenericActorRuntimeObservation.EventKind
                            .ParticipantDisqualified
                    && item.Payload is
                        GenericActorRuntimeObservation.EventPayload
                            .Participant participant
                    && participant.ParticipantId
                        == reservation.ParticipantId);
            return LifecyclePayloadMatchesSplitWork(
                    payload,
                    reservation,
                    "participant-disqualified")
                && disqualification is not null
                && cancellation.Ordinal < disqualification.Ordinal
                && !before.Participants.Single(participant =>
                        participant.ParticipantId
                            == reservation.ParticipantId)
                    .Disqualified
                && after.Participants.Single(participant =>
                        participant.ParticipantId
                            == reservation.ParticipantId)
                    .Disqualified;
        }
        if (!string.Equals(
                payload.CancellationReason,
                "source-destroyed",
                StringComparison.Ordinal))
        {
            return false;
        }
        GenericActorAuthoritativeEvent? destruction = events
            .SingleOrDefault(item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind.Destruction
                && item.Payload is
                    GenericActorRuntimeObservation.EventPayload.Destruction
                        destroyed
                && destroyed.ActorId == reservation.SourceActorId);
        return LifecyclePayloadMatchesSplitWork(
                payload,
                reservation,
                "source-destroyed")
            && destruction is not null
            && cancellation.Ordinal < destruction.Ordinal
            && after.ActiveLives.All(life =>
                life.ActorId != reservation.SourceActorId);
    }

    private static bool FabricationPendingMatchesReservation(
        FabricationPendingFact fact,
        BoundedChildFabricationProvisionalReservation reservation) =>
        fact.TargetTeamId == reservation.TargetTeamId
        && fact.TargetUnitId == reservation.TargetUnitId
        && fact.ParticipantId == reservation.ParticipantId
        && fact.Pending.SourceActorId == reservation.SourceActorId
        && string.Equals(
            fact.Pending.TransitionId,
            reservation.TransitionId,
            StringComparison.Ordinal)
        && string.Equals(
            fact.Pending.OperationId,
            reservation.OperationId,
            StringComparison.Ordinal)
        && string.Equals(
            fact.Pending.TargetFormId,
            reservation.TargetFormId,
            StringComparison.Ordinal)
        && fact.Pending.DueTick == reservation.DueTick
        && fact.Pending.ReservedPosition
            == reservation.ReservedPosition;

    private static void ValidateLifecycleQueueOrdering(
        ActorResolvedMatchDefinition definition,
        GenericActorWorldSnapshot before,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        string parameterName)
    {
        GenericActorAuthoritativeEvent[] fabricationQueues =
            FabricationLifecycleEvents(definition, events)
                .Where(item => item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .LifecycleQueued)
                .OrderBy(item => item.Ordinal)
                .ToArray();
        GenericActorAuthoritativeEvent[] canonical = fabricationQueues
            .OrderBy(item => LifecyclePayload(item).SourceActorId)
            .ThenBy(
                item => LifecyclePayload(item).TransitionId,
                StringComparer.Ordinal)
            .ThenBy(item =>
                LifecyclePayload(item).TargetTeamId ?? int.MaxValue)
            .ThenBy(item =>
                LifecyclePayload(item).TargetUnitId ?? int.MaxValue)
            .ThenBy(
                item => LifecyclePayload(item).OperationId,
                StringComparer.Ordinal)
            .ToArray();
        GenericActorAuthoritativeEvent[] splitQueues = events
            .Where(item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .LifecycleQueued
                && item.Payload is
                    GenericActorRuntimeObservation.EventPayload.Lifecycle
                        lifecycle
                && IsReplicationTransition(
                    definition,
                    lifecycle.TransitionId))
            .OrderBy(item => item.Ordinal)
            .ToArray();
        GenericActorAuthoritativeEvent[] canonicalSplitQueues =
            splitQueues
                .OrderBy(item => LifecyclePayload(item).SourceActorId)
                .ThenBy(
                    item => LifecyclePayload(item).TransitionId,
                    StringComparer.Ordinal)
                .ThenBy(
                    item => LifecyclePayload(item).OperationId,
                    StringComparer.Ordinal)
                .ToArray();
        if (!fabricationQueues.SequenceEqual(canonical)
            || !splitQueues.SequenceEqual(canonicalSplitQueues)
            || splitQueues.Any(split =>
                fabricationQueues.Any(fabrication =>
                    split.Ordinal < fabrication.Ordinal)))
        {
            throw new ArgumentException(
                "Fabrication and Split queues must use canonical family order, with every fabrication before every Split.",
                parameterName);
        }

        GenericActorAuthoritativeEvent[] fabricationCancellations =
            FabricationLifecycleEvents(definition, events)
                .Where(item => item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .LifecycleCancelled)
                .OrderBy(item => item.Ordinal)
                .ToArray();
        GenericActorAuthoritativeEvent[] canonicalCancellations =
            fabricationCancellations
                .OrderBy(item => LifecyclePayload(item).SourceActorId)
                .ThenBy(
                    item => LifecyclePayload(item).TransitionId,
                    StringComparer.Ordinal)
                .ThenBy(item =>
                    LifecyclePayload(item).TargetTeamId ?? int.MaxValue)
                .ThenBy(item =>
                    LifecyclePayload(item).TargetUnitId ?? int.MaxValue)
                .ThenBy(
                    item => LifecyclePayload(item).OperationId,
                    StringComparer.Ordinal)
                .ToArray();
        HashSet<int> disqualifiedParticipantIds = events
            .Where(item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .ParticipantDisqualified)
            .Select(item =>
                ((GenericActorRuntimeObservation.EventPayload.Participant)
                    item.Payload).ParticipantId)
            .ToHashSet();
        Dictionary<ActorIdentity, int> participantByActor =
            before.ActiveLives.ToDictionary(
                life => life.ActorId,
                life => life.ParticipantId);
        Dictionary<(int TeamId, int UnitId), int> participantBySlot =
            definition.Topology.UnitSlots.ToDictionary(
                slot => (slot.TeamId, slot.UnitId),
                slot => slot.ControllerParticipantId);
        GenericActorAuthoritativeEvent[] clockCancellations = events
            .Where(item => item.Kind
                == GenericActorRuntimeObservation.EventKind
                    .LifecycleClockCancelled)
            .Where(item =>
            {
                var clock =
                    (GenericActorRuntimeObservation.EventPayload
                        .LifecycleClockCancelled)item.Payload;
                return disqualifiedParticipantIds.Contains(
                    participantBySlot[
                        (clock.TargetTeamId, clock.TargetUnitId)]);
            })
            .OrderBy(item => item.Ordinal)
            .ToArray();
        GenericActorAuthoritativeEvent[] canonicalClocks =
            clockCancellations
                .OrderBy(item =>
                    ((GenericActorRuntimeObservation.EventPayload
                        .LifecycleClockCancelled)item.Payload).TargetTeamId)
                .ThenBy(item =>
                    ((GenericActorRuntimeObservation.EventPayload
                        .LifecycleClockCancelled)item.Payload).TargetUnitId)
                .ToArray();
        GenericActorAuthoritativeEvent[] splitCancellations = events
            .Where(item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .LifecycleCancelled
                && item.Payload is
                    GenericActorRuntimeObservation.EventPayload.Lifecycle
                        lifecycle
                && IsReplicationTransition(
                    definition,
                    lifecycle.TransitionId)
                && string.Equals(
                    lifecycle.CancellationReason,
                    "participant-disqualified",
                    StringComparison.Ordinal))
            .OrderBy(item => item.Ordinal)
            .ToArray();
        GenericActorAuthoritativeEvent[] canonicalSplitCancellations =
            splitCancellations
                .OrderBy(item => LifecyclePayload(item).SourceActorId)
                .ThenBy(
                    item => LifecyclePayload(item).TransitionId,
                    StringComparer.Ordinal)
                .ThenBy(item =>
                    LifecyclePayload(item).TargetTeamId ?? int.MaxValue)
                .ThenBy(item =>
                    LifecyclePayload(item).TargetUnitId ?? int.MaxValue)
                .ThenBy(
                    item => LifecyclePayload(item).OperationId,
                    StringComparer.Ordinal)
                .ToArray();
        HashSet<ActorIdentity> lethallyRemovedActors = events
            .Where(item => item.Kind
                == GenericActorRuntimeObservation.EventKind.Destruction)
            .Select(item =>
                ((GenericActorRuntimeObservation.EventPayload.Destruction)
                    item.Payload).ActorId)
            .ToHashSet();
        GenericActorAuthoritativeEvent[] sameLifeCancellations = events
            .Where(item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .FormTransitionCancelled
                && item.Payload is
                    GenericActorRuntimeObservation.EventPayload
                        .FormTransition transition
                && participantByActor.TryGetValue(
                    transition.ActorId,
                    out int participantId)
                && disqualifiedParticipantIds.Contains(participantId)
                && !lethallyRemovedActors.Contains(transition.ActorId))
            .OrderBy(item => item.Ordinal)
            .ToArray();
        GenericActorAuthoritativeEvent[] disqualifiedLifeRemovals = events
            .Where(IsLifeRemovalEvent)
            .Where(item =>
                participantByActor.TryGetValue(
                    RemovedActorId(item),
                    out int participantId)
                && disqualifiedParticipantIds.Contains(participantId))
            .OrderBy(item => item.Ordinal)
            .ToArray();
        GenericActorAuthoritativeEvent[] disqualificationEvents = events
            .Where(item => item.Kind
                == GenericActorRuntimeObservation.EventKind
                    .ParticipantDisqualified)
            .OrderBy(item => item.Ordinal)
            .ToArray();
        if (!fabricationCancellations.SequenceEqual(
                canonicalCancellations)
            || !clockCancellations.SequenceEqual(canonicalClocks)
            || !splitCancellations.SequenceEqual(
                canonicalSplitCancellations)
            || !sameLifeCancellations.SequenceEqual(
                sameLifeCancellations.OrderBy(item =>
                    ((GenericActorRuntimeObservation.EventPayload
                        .FormTransition)item.Payload).ActorId))
            || !disqualifiedLifeRemovals.SequenceEqual(
                disqualifiedLifeRemovals.OrderBy(RemovedActorId))
            || !disqualificationEvents.SequenceEqual(
                disqualificationEvents.OrderBy(item =>
                    ((GenericActorRuntimeObservation.EventPayload
                        .Participant)item.Payload).ParticipantId))
            || !LifecyclePhasesAreOrdered(
                clockCancellations,
                fabricationCancellations,
                splitCancellations,
                sameLifeCancellations,
                disqualifiedLifeRemovals,
                disqualificationEvents))
        {
            throw new ArgumentException(
                "A joint disqualification batch must use global clock, fabrication, Split, same-life, life-removal, and participant-event phase barriers with canonical family order.",
                parameterName);
        }
    }

    private static bool LifecyclePhasesAreOrdered(
        params IReadOnlyCollection<GenericActorAuthoritativeEvent>[]
            phases)
    {
        long lastOrdinal = -1;
        foreach (IReadOnlyCollection<GenericActorAuthoritativeEvent> phase
                 in phases)
        {
            if (phase.Count == 0)
                continue;
            long first = phase.Min(item => item.Ordinal);
            if (first <= lastOrdinal)
                return false;
            lastOrdinal = phase.Max(item => item.Ordinal);
        }
        return true;
    }

    private static bool LifecycleFactPhasesAreOrdered(
        params IReadOnlyCollection<long>[] phases)
    {
        long lastOrdinal = -1;
        foreach (IReadOnlyCollection<long> phase in phases)
        {
            if (phase.Count == 0)
                continue;
            long first = phase.Min();
            if (first <= lastOrdinal)
                return false;
            lastOrdinal = phase.Max();
        }
        return true;
    }

    private readonly record struct LifecycleQueuePose(
        Position Position,
        Direction Facing);

    /// <param name="Facing">
    /// The mover's facing entering the movement phase — the facing a Blocked
    /// attempt must still evidence, because a blocked move changes nothing.
    /// </param>
    /// <param name="SuccessFacing">
    /// The facing a successful move must evidence. It equals
    /// <paramref name="Facing"/> unless the mover's movement profile couples
    /// facing to movement, in which case a successful step turns the body to
    /// the movement direction (DECISIONS #156).
    /// </param>
    private readonly record struct MovementQueueCandidate(
        Position Source,
        Position Target,
        Direction Facing,
        Direction SuccessFacing,
        bool Blocked);

    private readonly record struct MovementContactExpectation(
        ActorIdentity TargetActorId,
        bool AppliedDamage,
        GenericActorWorldSnapshot.ProjectileSnapshot Projectile);

    private static void ValidateFabricationResolutionBoundary(
        ActorResolvedMatchDefinition definition,
        GenericActorWorldSnapshot before,
        GenericActorWorldSnapshot after,
        IReadOnlyCollection<GenericActorMatchActorTurn> turns,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        IReadOnlyCollection<GenericActorProjectileTraversal> traversals,
        IReadOnlySet<ActorIdentity> actorsWithPriorSameLifeTransition,
        string parameterName)
    {
        ValidateLifecycleReservationArbitration(
            definition,
            before,
            after,
            turns,
            events,
            traversals,
            actorsWithPriorSameLifeTransition,
            parameterName);
        Dictionary<string, FabricationPendingFact> beforeFacts =
            FabricationPendingFacts(before);
        Dictionary<string, FabricationPendingFact> afterFacts =
            FabricationPendingFacts(after);
        ILookup<string, GenericActorAuthoritativeEvent> eventsByOperation =
            FabricationLifecycleEvents(definition, events)
                .ToLookup(
                    item => LifecyclePayload(item).OperationId,
                    StringComparer.Ordinal);
        if (eventsByOperation.Any(group => group.Any(item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .LifecycleCompleted)))
        {
            throw new ArgumentException(
                "Fabrication completion may occur only at its exact tick-start boundary.",
                parameterName);
        }

        foreach ((string operationId, FabricationPendingFact beforeFact)
                 in beforeFacts)
        {
            GenericActorAuthoritativeEvent[] operationEvents =
                eventsByOperation[operationId]
                    .OrderBy(item => item.Ordinal)
                    .ToArray();
            if (afterFacts.TryGetValue(
                    operationId,
                    out FabricationPendingFact? afterFact))
            {
                if (!FabricationPendingFactsEqual(
                        beforeFact,
                        afterFact)
                    || operationEvents.Length != 0)
                {
                    throw new ArgumentException(
                        "A retained fabrication must remain exact through resolution without duplicate lifecycle evidence.",
                        parameterName);
                }
                continue;
            }

            if (operationEvents.Length != 1
                || operationEvents[0].Kind
                    != GenericActorRuntimeObservation.EventKind
                        .LifecycleCancelled
                || !LifecyclePayloadMatchesPending(
                    LifecyclePayload(operationEvents[0]),
                    beforeFact,
                    "participant-disqualified"))
            {
                throw new ArgumentException(
                    "A pending fabrication may disappear during resolution only through exact participant-disqualification cancellation.",
                    parameterName);
            }
            ValidateFabricationDisqualificationCancellation(
                definition,
                operationEvents[0],
                before,
                after,
                events,
                parameterName);
        }

        foreach ((string operationId, FabricationPendingFact afterFact)
                 in afterFacts.Where(pair =>
                     !beforeFacts.ContainsKey(pair.Key)))
        {
            GenericActorAuthoritativeEvent[] operationEvents =
                eventsByOperation[operationId]
                    .OrderBy(item => item.Ordinal)
                    .ToArray();
            if (operationEvents.Length != 1
                || operationEvents[0].Kind
                    != GenericActorRuntimeObservation.EventKind
                        .LifecycleQueued
                || !LifecyclePayloadMatchesPending(
                    LifecyclePayload(operationEvents[0]),
                    afterFact,
                    cancellationReason: null))
            {
                throw new ArgumentException(
                    "Every newly pending fabrication must have one exact queue event.",
                    parameterName);
            }
            ValidateFabricationQueueAction(
                definition,
                afterFact.Pending,
                afterFact.TargetTeamId,
                afterFact.TargetUnitId,
                operationEvents[0],
                before,
                turns,
                parameterName);
        }

        HashSet<string> stateOperationIds =
        [
            .. beforeFacts.Keys,
            .. afterFacts.Keys,
        ];
        foreach (IGrouping<string, GenericActorAuthoritativeEvent> group
                 in eventsByOperation.Where(group =>
                     !stateOperationIds.Contains(group.Key)))
        {
            GenericActorAuthoritativeEvent[] operationEvents = group
                .OrderBy(item => item.Ordinal)
                .ToArray();
            if (operationEvents.Length != 2
                || operationEvents[0].Kind
                    != GenericActorRuntimeObservation.EventKind
                        .LifecycleQueued
                || operationEvents[1].Kind
                    != GenericActorRuntimeObservation.EventKind
                        .LifecycleCancelled)
            {
                throw new ArgumentException(
                    "A fabrication with no boundary state must be an exact same-resolution queue followed by disqualification cancellation.",
                    parameterName);
            }
            GenericActorRuntimeObservation.EventPayload.Lifecycle queued =
                LifecyclePayload(operationEvents[0]);
            GenericActorRuntimeObservation.EventPayload.Lifecycle cancelled =
                LifecyclePayload(operationEvents[1]);
            if (!LifecyclePayloadsDescribeSameWork(queued, cancelled)
                || !string.Equals(
                    cancelled.CancellationReason,
                    "participant-disqualified",
                    StringComparison.Ordinal)
                || queued.TargetTeamId is not int targetTeamId
                || queued.TargetUnitId is not int targetUnitId)
            {
                throw new ArgumentException(
                    "An ephemeral fabrication queue and cancellation must describe the same participant-owned work.",
                    parameterName);
            }
            ValidateFabricationQueueAction(
                definition,
                queued,
                targetTeamId,
                targetUnitId,
                operationEvents[0],
                before,
                turns,
                parameterName);
            ValidateFabricationDisqualificationCancellation(
                definition,
                operationEvents[1],
                before,
                after,
                events,
                parameterName);
        }
    }

    private static void ValidateFabricationQueueAction(
        ActorResolvedMatchDefinition definition,
        GenericActorRuntimeObservation.UnitSlotState.FabricationPending
            pending,
        int targetTeamId,
        int targetUnitId,
        GenericActorAuthoritativeEvent queuedEvent,
        GenericActorWorldSnapshot before,
        IReadOnlyCollection<GenericActorMatchActorTurn> turns,
        string parameterName) =>
        ValidateFabricationQueueAction(
            definition,
            new GenericActorRuntimeObservation.EventPayload.Lifecycle(
                pending.TransitionId,
                pending.OperationId,
                pending.SourceActorId,
                targetTeamId,
                targetUnitId,
                pending.DueTick,
                CancellationReason: null),
            targetTeamId,
            targetUnitId,
            queuedEvent,
            before,
            turns,
            parameterName);

    private static void ValidateFabricationQueueAction(
        ActorResolvedMatchDefinition definition,
        GenericActorRuntimeObservation.EventPayload.Lifecycle payload,
        int targetTeamId,
        int targetUnitId,
        GenericActorAuthoritativeEvent queuedEvent,
        GenericActorWorldSnapshot before,
        IReadOnlyCollection<GenericActorMatchActorTurn> turns,
        string parameterName)
    {
        BoundedChildFabricationDefinition transition =
            FabricationTransition(
                definition,
                payload.TransitionId,
                parameterName);
        GenericActorMatchActorTurn? turn = turns.SingleOrDefault(value =>
            value.ActorId == payload.SourceActorId);
        GenericActorRuntimeActionArgument.UnitTargetArgument?
            targetArgument = turn?.ActionResolution.ValidatedAction
                .Arguments
                .OfType<GenericActorRuntimeActionArgument
                    .UnitTargetArgument>()
                .SingleOrDefault();
        GenericActorWorldSnapshot.SlotSnapshot? targetSlot =
            before.Slots.SingleOrDefault(slot =>
                slot.TeamId == targetTeamId
                && slot.UnitId == targetUnitId);
        long expectedDueTick =
            (long)queuedEvent.Tick + transition.Delay.DurationTicks;
        if (payload.TargetTeamId != targetTeamId
            || payload.TargetUnitId != targetUnitId
            || payload.DueTick != expectedDueTick
            || turn is null
            || turn.ActionResolution.Outcome
                != GenericActorRuntimeActionResolution.ActionOutcome.Success
            || !string.Equals(
                turn.ActionResolution.ValidatedAction.ActionId,
                transition.ActionId,
                StringComparison.Ordinal)
            || !transition.SourceFormIds.Contains(
                turn.Observation.Self.FormId,
                StringComparer.Ordinal)
            || targetArgument?.Value
                != new GenericActorRuntimeActionArgument.UnitTarget(
                    targetTeamId,
                    targetUnitId)
            || targetSlot is null
            || targetSlot.State is not
                GenericActorRuntimeObservation.UnitSlotState.Ready
            || targetSlot.TeamId != payload.SourceActorId.TeamId
            || targetSlot.ParticipantId != turn.ParticipantId
            || (targetSlot.TeamId == payload.SourceActorId.TeamId
                && targetSlot.UnitId == payload.SourceActorId.UnitId))
        {
            throw new ArgumentException(
                "A fabrication queue must be caused by one successful matching source action against an explicit ready same-controller target.",
                parameterName);
        }
    }

    private static void ValidateFabricationDisqualificationCancellation(
        ActorResolvedMatchDefinition definition,
        GenericActorAuthoritativeEvent cancellationEvent,
        GenericActorWorldSnapshot before,
        GenericActorWorldSnapshot after,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        string parameterName)
    {
        GenericActorRuntimeObservation.EventPayload.Lifecycle payload =
            LifecyclePayload(cancellationEvent);
        if (payload.TargetTeamId is not int targetTeamId
            || payload.TargetUnitId is not int targetUnitId)
        {
            throw new ArgumentException(
                "A bounded-child cancellation must identify its exact target slot.",
                parameterName);
        }
        PublicUnitSlot targetSlot = definition.Topology.UnitSlots.Single(
            slot => slot.TeamId == targetTeamId
                && slot.UnitId == targetUnitId);
        int participantId = targetSlot.ControllerParticipantId;
        GenericActorAuthoritativeEvent[] disqualifications = events
            .Where(item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .ParticipantDisqualified
                && item.Payload is
                    GenericActorRuntimeObservation.EventPayload.Participant
                        participant
                && participant.ParticipantId == participantId)
            .ToArray();
        bool cancelledBeforeClocks = events
            .Where(item => item.Kind
                == GenericActorRuntimeObservation.EventKind
                    .LifecycleClockCancelled)
            .Where(item =>
            {
                var clock =
                    (GenericActorRuntimeObservation.EventPayload
                        .LifecycleClockCancelled)item.Payload;
                PublicUnitSlot slot = definition.Topology.UnitSlots.Single(
                    candidate =>
                        candidate.TeamId == clock.TargetTeamId
                        && candidate.UnitId == clock.TargetUnitId);
                return slot.ControllerParticipantId == participantId;
            })
            .Any(item => item.Ordinal >= cancellationEvent.Ordinal);
        GenericActorAuthoritativeEvent[] replicationCancellations = events
            .Where(item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .LifecycleCancelled
                && item.Payload is
                    GenericActorRuntimeObservation.EventPayload.Lifecycle
                        lifecycle
                && IsReplicationTransition(
                    definition,
                    lifecycle.TransitionId)
                && LifecycleParticipantId(
                    definition,
                    lifecycle) == participantId)
            .ToArray();
        HashSet<ActorIdentity> participantActors = before.ActiveLives
            .Where(life => life.ParticipantId == participantId)
            .Select(life => life.ActorId)
            .ToHashSet();
        GenericActorAuthoritativeEvent[] removals = events
            .Where(IsLifeRemovalEvent)
            .Where(item =>
                participantActors.Contains(RemovedActorId(item)))
            .ToArray();
        if (!string.Equals(
                payload.CancellationReason,
                "participant-disqualified",
                StringComparison.Ordinal)
            || disqualifications.Length != 1
            || cancellationEvent.Ordinal >= disqualifications[0].Ordinal
            || cancelledBeforeClocks
            || replicationCancellations.Any(item =>
                item.Ordinal <= cancellationEvent.Ordinal)
            || removals.Any(item =>
                item.Ordinal <= cancellationEvent.Ordinal)
            || before.Participants.Single(participant =>
                    participant.ParticipantId == participantId)
                .Disqualified
            || !after.Participants.Single(participant =>
                    participant.ParticipantId == participantId)
                .Disqualified)
        {
            throw new ArgumentException(
                "Fabrication cancellation must occur in deterministic disqualification order after clocks and before replications, life retirement, and the participant event.",
                parameterName);
        }
    }

    private static void ValidateLifecycleEventCatalog(
        ActorResolvedMatchDefinition definition,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        string parameterName)
    {
        HashSet<string> transitionIds =
        [
            .. definition.Rules.FabricationTransitions
                .Select(transition => transition.TransitionId),
            .. definition.Rules.ReplicationTransitions
                .Select(transition => transition.TransitionId),
        ];
        foreach (GenericActorAuthoritativeEvent item in events.Where(
                     IsLifecycleEvent))
        {
            GenericActorRuntimeObservation.EventPayload.Lifecycle payload =
                LifecyclePayload(item);
            bool cancellationShape = item.Kind switch
            {
                GenericActorRuntimeObservation.EventKind.LifecycleQueued
                    or GenericActorRuntimeObservation.EventKind
                        .LifecycleCompleted =>
                    payload.CancellationReason is null,
                GenericActorRuntimeObservation.EventKind
                        .LifecycleCancelled =>
                    !string.IsNullOrWhiteSpace(
                        payload.CancellationReason),
                _ => false,
            };
            if (!transitionIds.Contains(payload.TransitionId)
                || string.IsNullOrWhiteSpace(payload.OperationId)
                || !cancellationShape)
            {
                throw new ArgumentException(
                    "Lifecycle evidence must reference one declared transition and use event-kind-consistent cancellation data.",
                    parameterName);
            }
        }
    }

    private static Dictionary<string, FabricationPendingFact>
        FabricationPendingFacts(GenericActorWorldSnapshot state) =>
        state.Slots
            .Where(slot => slot.State is
                GenericActorRuntimeObservation.UnitSlotState
                    .FabricationPending)
            .Select(slot => new FabricationPendingFact(
                slot.TeamId,
                slot.UnitId,
                slot.ParticipantId,
                slot.NextLifeId,
                (GenericActorRuntimeObservation.UnitSlotState
                    .FabricationPending)slot.State))
            .ToDictionary(
                fact => fact.Pending.OperationId,
                StringComparer.Ordinal);

    private static IEnumerable<GenericActorAuthoritativeEvent>
        FabricationLifecycleEvents(
            ActorResolvedMatchDefinition definition,
            IEnumerable<GenericActorAuthoritativeEvent> events)
    {
        HashSet<string> transitionIds = definition.Rules
            .FabricationTransitions
            .OfType<BoundedChildFabricationDefinition>()
            .Select(transition => transition.TransitionId)
            .ToHashSet(StringComparer.Ordinal);
        return events.Where(item =>
            IsLifecycleEvent(item)
            && item.Payload is
                GenericActorRuntimeObservation.EventPayload.Lifecycle
                    lifecycle
            && transitionIds.Contains(lifecycle.TransitionId));
    }

    private static BoundedChildFabricationDefinition FabricationTransition(
        ActorResolvedMatchDefinition definition,
        string transitionId,
        string parameterName) =>
        definition.Rules.FabricationTransitions
            .OfType<BoundedChildFabricationDefinition>()
            .SingleOrDefault(transition => string.Equals(
                transition.TransitionId,
                transitionId,
                StringComparison.Ordinal))
        ?? throw new ArgumentException(
            "Fabrication lifecycle evidence references an unsupported transition.",
            parameterName);

    private static bool FabricationPendingFactsEqual(
        FabricationPendingFact left,
        FabricationPendingFact right) =>
        left.TargetTeamId == right.TargetTeamId
        && left.TargetUnitId == right.TargetUnitId
        && left.ParticipantId == right.ParticipantId
        && left.NextLifeId == right.NextLifeId
        && left.Pending.DueTick == right.Pending.DueTick
        && left.Pending.SourceActorId == right.Pending.SourceActorId
        && string.Equals(
            left.Pending.TransitionId,
            right.Pending.TransitionId,
            StringComparison.Ordinal)
        && string.Equals(
            left.Pending.OperationId,
            right.Pending.OperationId,
            StringComparison.Ordinal)
        && string.Equals(
            left.Pending.TargetFormId,
            right.Pending.TargetFormId,
            StringComparison.Ordinal)
        && left.Pending.ReservedPosition
            == right.Pending.ReservedPosition;

    private static bool LifecyclePayloadMatchesPending(
        GenericActorRuntimeObservation.EventPayload.Lifecycle payload,
        FabricationPendingFact pendingFact,
        string? cancellationReason) =>
        string.Equals(
            payload.TransitionId,
            pendingFact.Pending.TransitionId,
            StringComparison.Ordinal)
        && string.Equals(
            payload.OperationId,
            pendingFact.Pending.OperationId,
            StringComparison.Ordinal)
        && payload.SourceActorId
            == pendingFact.Pending.SourceActorId
        && payload.TargetTeamId == pendingFact.TargetTeamId
        && payload.TargetUnitId == pendingFact.TargetUnitId
        && payload.DueTick == pendingFact.Pending.DueTick
        && string.Equals(
            payload.CancellationReason,
            cancellationReason,
            StringComparison.Ordinal);

    private static bool LifecyclePayloadsDescribeSameWork(
        GenericActorRuntimeObservation.EventPayload.Lifecycle left,
        GenericActorRuntimeObservation.EventPayload.Lifecycle right) =>
        string.Equals(
            left.TransitionId,
            right.TransitionId,
            StringComparison.Ordinal)
        && string.Equals(
            left.OperationId,
            right.OperationId,
            StringComparison.Ordinal)
        && left.SourceActorId == right.SourceActorId
        && left.TargetTeamId == right.TargetTeamId
        && left.TargetUnitId == right.TargetUnitId
        && left.DueTick == right.DueTick;

    private static GenericActorRuntimeObservation.EventPayload.Lifecycle
        LifecyclePayload(GenericActorAuthoritativeEvent item) =>
        item.Payload as
            GenericActorRuntimeObservation.EventPayload.Lifecycle
        ?? throw new ArgumentException(
            "Lifecycle event evidence must carry a lifecycle payload.");

    private static bool IsReplicationTransition(
        ActorResolvedMatchDefinition definition,
        string transitionId) =>
        definition.Rules.ReplicationTransitions.Any(transition =>
            string.Equals(
                transition.TransitionId,
                transitionId,
                StringComparison.Ordinal));

    private static int? LifecycleParticipantId(
        ActorResolvedMatchDefinition definition,
        GenericActorRuntimeObservation.EventPayload.Lifecycle payload)
    {
        if (payload.TargetTeamId is not int teamId
            || payload.TargetUnitId is not int unitId)
        {
            return null;
        }
        return definition.Topology.UnitSlots.SingleOrDefault(slot =>
            slot.TeamId == teamId
            && slot.UnitId == unitId)?.ControllerParticipantId;
    }

    private sealed record FabricationPendingFact(
        int TargetTeamId,
        int TargetUnitId,
        int ParticipantId,
        int NextLifeId,
        GenericActorRuntimeObservation.UnitSlotState.FabricationPending
            Pending);

    private static void ValidateLifeSpawnEvidence(
        IReadOnlyCollection<GenericActorLifeStart> starts,
        GenericActorWorldSnapshot state,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        string parameterName)
    {
        GenericActorAuthoritativeEvent[] spawnEvents = events
            .Where(item => item.Kind
                == GenericActorRuntimeObservation.EventKind.LifeSpawned)
            .ToArray();
        if (spawnEvents.Length != starts.Count
            || spawnEvents
                .Select(item =>
                    ((GenericActorRuntimeObservation.EventPayload
                        .LifeSpawned)item.Payload).ActorId)
                .Distinct()
                .Count() != spawnEvents.Length)
        {
            throw new ArgumentException(
                "Every life start must have exactly one actor-unique LifeSpawned event.",
                parameterName);
        }

        Dictionary<ActorIdentity, GenericActorAuthoritativeEvent>
            eventsByActor = spawnEvents.ToDictionary(
                item =>
                    ((GenericActorRuntimeObservation.EventPayload
                        .LifeSpawned)item.Payload).ActorId);
        Dictionary<ActorIdentity, GenericActorWorldSnapshot.LifeSnapshot>
            livesByActor = state.ActiveLives.ToDictionary(
                life => life.ActorId);
        foreach (GenericActorLifeStart start in starts)
        {
            if (!eventsByActor.TryGetValue(
                    start.ActorId,
                    out GenericActorAuthoritativeEvent? item)
                || !livesByActor.TryGetValue(
                    start.ActorId,
                    out GenericActorWorldSnapshot.LifeSnapshot? life)
                || item.Payload is not
                    GenericActorRuntimeObservation.EventPayload.LifeSpawned
                        spawned
                || spawned.ActorId != start.ActorId
                || spawned.ParticipantId != start.ParticipantId
                || spawned.ParentActorId != start.Origin.ParentActorId
                || spawned.Generation != start.Origin.Generation
                || !string.Equals(
                    spawned.FormId,
                    life.FormId,
                    StringComparison.Ordinal)
                || spawned.Health != life.Health
                || spawned.Position != life.Position
                || spawned.Reason != start.Origin.Reason
                || !string.Equals(
                    spawned.SourceTransitionId,
                    start.Origin.SourceTransitionId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    spawned.SourceOperationId,
                    start.Origin.SourceOperationId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "LifeSpawned event payloads must exactly describe their life start and active life.",
                    parameterName);
            }
        }
    }

    private static void ValidateLifeRemovalEvidence(
        GenericActorWorldSnapshot before,
        GenericActorWorldSnapshot after,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        bool requireUnchangedPosition,
        string parameterName)
    {
        Dictionary<ActorIdentity, GenericActorWorldSnapshot.LifeSnapshot>
            beforeLives = before.ActiveLives.ToDictionary(
                life => life.ActorId);
        HashSet<ActorIdentity> afterActors = after.ActiveLives
            .Select(life => life.ActorId)
            .ToHashSet();
        ActorIdentity[] removedActors = beforeLives.Keys
            .Where(actorId => !afterActors.Contains(actorId))
            .Order()
            .ToArray();
        GenericActorAuthoritativeEvent[] removalEvents = events
            .Where(IsLifeRemovalEvent)
            .ToArray();
        ActorIdentity[] evidencedActors = removalEvents
            .Select(RemovedActorId)
            .Order()
            .ToArray();
        if (!removedActors.SequenceEqual(evidencedActors)
            || evidencedActors.Distinct().Count()
                != evidencedActors.Length)
        {
            throw new ArgumentException(
                "Every removed life must have exactly one Destruction or LifeRetired event, with no orphan removal events.",
                parameterName);
        }

        foreach (GenericActorAuthoritativeEvent item in removalEvents)
        {
            ActorIdentity actorId = RemovedActorId(item);
            GenericActorWorldSnapshot.LifeSnapshot life =
                beforeLives[actorId];
            bool exact = item.Payload switch
            {
                GenericActorRuntimeObservation.EventPayload.Destruction
                    destruction =>
                    destruction.Generation == life.Generation
                    && string.Equals(
                        destruction.FormId,
                        life.FormId,
                        StringComparison.Ordinal)
                    && (!requireUnchangedPosition
                        || destruction.Position == life.Position),
                GenericActorRuntimeObservation.EventPayload.LifeRetired
                    retired =>
                    retired.Generation == life.Generation
                    && string.Equals(
                        retired.FormId,
                        life.FormId,
                        StringComparison.Ordinal)
                    && (!requireUnchangedPosition
                        || retired.Position == life.Position),
                _ => false,
            };
            if (!exact)
            {
                throw new ArgumentException(
                    "Life removal evidence must identify the removed life's exact generation and form, plus its unchanged position at a tick-start boundary.",
                    parameterName);
            }
        }
    }

    private static void ValidateResolutionLifeEvidence(
        ActorResolvedMatchDefinition definition,
        GenericActorWorldSnapshot before,
        GenericActorWorldSnapshot after,
        IReadOnlyCollection<GenericActorMatchActorTurn> turns,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        IReadOnlyCollection<GenericActorProjectileTraversal> traversals,
        IReadOnlySet<ActorIdentity> actorsWithPriorSameLifeTransition,
        string parameterName)
    {
        ValidateLifecycleEventCatalog(
            definition,
            events,
            parameterName);
        HashSet<ActorIdentity> beforeActors = before.ActiveLives
            .Select(life => life.ActorId)
            .ToHashSet();
        if (after.ActiveLives.Any(life =>
                !beforeActors.Contains(life.ActorId))
            || events.Any(item => item.Kind
                == GenericActorRuntimeObservation.EventKind.LifeSpawned))
        {
            throw new ArgumentException(
                "New runtime lives may only enter chronology through a recorded boundary life start.",
                parameterName);
        }
        ValidateFabricationResolutionBoundary(
            definition,
            before,
            after,
            turns,
            events,
            traversals,
            actorsWithPriorSameLifeTransition,
            parameterName);
        ValidateLifeRemovalEvidence(
            before,
            after,
            events,
            requireUnchangedPosition: false,
            parameterName);
        ValidateResolutionSameLifeTransitions(
            definition,
            before,
            after,
            turns,
            events,
            parameterName);
    }

    private static void ValidateResolutionSameLifeTransitions(
        ActorResolvedMatchDefinition definition,
        GenericActorWorldSnapshot before,
        GenericActorWorldSnapshot after,
        IReadOnlyCollection<GenericActorMatchActorTurn> turns,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        string parameterName)
    {
        Dictionary<ActorIdentity, GenericActorWorldSnapshot.LifeSnapshot>
            afterLives = after.ActiveLives.ToDictionary(
                life => life.ActorId);
        Dictionary<ActorIdentity, GenericActorMatchActorTurn> turnsByActor =
            turns.ToDictionary(turn => turn.ActorId);
        GenericActorAuthoritativeEvent[] orderedEvents = events
            .OrderBy(item => item.Ordinal)
            .ToArray();
        ILookup<ActorIdentity, GenericActorAuthoritativeEvent>
            transitionsByActor = orderedEvents
                .Where(IsFormTransitionEvent)
                .ToLookup(item =>
                    ((GenericActorRuntimeObservation.EventPayload
                        .FormTransition)item.Payload).ActorId);
        HashSet<ActorIdentity> beforeActors = before.ActiveLives
            .Select(life => life.ActorId)
            .ToHashSet();
        if (transitionsByActor.Any(group =>
                !beforeActors.Contains(group.Key)))
        {
            throw new ArgumentException(
                "Resolution form-transition evidence must identify an actor active at the frozen tick boundary.",
                parameterName);
        }

        foreach (GenericActorWorldSnapshot.LifeSnapshot beforeLife in
                 before.ActiveLives)
        {
            string expectedFormId = beforeLife.FormId;
            GenericActorRuntimeObservation.PendingSameLifeTransition?
                expectedPending = beforeLife.PendingSameLifeTransition;
            ActorFormTransitionDefinition? completedTransition = null;
            GenericActorAuthoritativeEvent? completionEvent = null;
            foreach (GenericActorAuthoritativeEvent item in
                     transitionsByActor[beforeLife.ActorId]
                         .OrderBy(value => value.Ordinal))
            {
                var payload =
                    (GenericActorRuntimeObservation.EventPayload
                        .FormTransition)item.Payload;
                ActorFormTransitionDefinition transition =
                    ResolutionTransition(
                        definition,
                        payload,
                        item,
                        parameterName);
                switch (item.Kind)
                {
                    case GenericActorRuntimeObservation.EventKind
                        .FormTransitionStarted:
                        if (expectedPending is not null
                            || payload.StartedTick != item.Tick
                            || !string.Equals(
                                expectedFormId,
                                payload.FromFormId,
                                StringComparison.Ordinal))
                        {
                            throw new ArgumentException(
                                "A resolution transition start must queue one route from the actor's current source form.",
                                parameterName);
                        }
                        expectedPending =
                            PendingTransitionFrom(payload);
                        break;
                    case GenericActorRuntimeObservation.EventKind
                        .FormTransitionCancelled:
                        if (expectedPending is null
                            || !PendingTransitionMatches(
                                expectedPending,
                                payload)
                            || !string.Equals(
                                expectedFormId,
                                payload.FromFormId,
                                StringComparison.Ordinal)
                            || item.Tick < payload.StartedTick
                            || item.Tick > payload.DueTick)
                        {
                            throw new ArgumentException(
                                "A resolution transition cancellation must clear the actor's exact pending route before its due boundary passes.",
                                parameterName);
                        }
                        expectedPending = null;
                        break;
                    case GenericActorRuntimeObservation.EventKind
                        .FormTransitionCompleted:
                        if (expectedPending is null
                            || !PendingTransitionMatches(
                                expectedPending,
                                payload)
                            || !string.Equals(
                                expectedFormId,
                                payload.FromFormId,
                                StringComparison.Ordinal)
                            || payload.DueTick != item.Tick
                            || completedTransition is not null)
                        {
                            throw new ArgumentException(
                                "A resolution transition completion must consume one exact due pending route.",
                                parameterName);
                        }
                        expectedFormId = payload.ToFormId;
                        expectedPending = null;
                        completedTransition = transition;
                        completionEvent = item;
                        break;
                    default:
                        throw new InvalidOperationException(
                            "Unknown same-life transition event.");
                }
            }

            if (!afterLives.TryGetValue(
                    beforeLife.ActorId,
                    out GenericActorWorldSnapshot.LifeSnapshot? afterLife))
            {
                if (completedTransition is not null
                    || expectedPending is not null)
                {
                    throw new ArgumentException(
                        "A removed life cannot complete or retain pending same-life work.",
                        parameterName);
                }
                ValidateDestructionCancellationOrder(
                    beforeLife,
                    orderedEvents,
                    parameterName);
                continue;
            }
            if (!string.Equals(
                    expectedFormId,
                    afterLife.FormId,
                    StringComparison.Ordinal)
                || expectedPending != afterLife.PendingSameLifeTransition)
            {
                throw new ArgumentException(
                    "Resolution form-transition evidence must exactly explain the surviving actor's post-state form and pending route.",
                    parameterName);
            }
            if (completedTransition is not null
                && !CompletedResolutionTransitionStateMatches(
                    definition,
                    completedTransition,
                    beforeLife,
                    afterLife,
                    after,
                    turnsByActor[beforeLife.ActorId],
                    orderedEvents,
                    completionEvent!))
            {
                throw new ArgumentException(
                    "A resolution transition completion must preserve identity and evaluate exact completion-time health and combat continuity.",
                    parameterName);
            }
        }
    }

    private static ActorFormTransitionDefinition ResolutionTransition(
        ActorResolvedMatchDefinition definition,
        GenericActorRuntimeObservation.EventPayload.FormTransition payload,
        GenericActorAuthoritativeEvent item,
        string parameterName)
    {
        ActorFormTransitionDefinition? transition =
            definition.Rules.SameLifeTransitions
                .OfType<ActorFormTransitionDefinition>()
                .SingleOrDefault(value => string.Equals(
                    value.TransitionId,
                    payload.TransitionId,
                    StringComparison.Ordinal));
        long dueOffset = transition?.Windup.Completion switch
        {
            ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                    .TickStartAfterDuration =>
                transition.Windup.DurationTicks,
            ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                    .EndOfStartedTickPlusDurationMinusOneAfterModeUpdate =>
                transition.Windup.DurationTicks - 1L,
            _ => -1,
        };
        if (transition is null
            || string.IsNullOrWhiteSpace(payload.OperationId)
            || !string.Equals(
                transition.SourceFormId,
                payload.FromFormId,
                StringComparison.Ordinal)
            || !string.Equals(
                transition.TargetFormId,
                payload.ToFormId,
                StringComparison.Ordinal)
            || payload.StartedTick < 0
            || dueOffset < 0
            || (long)payload.StartedTick + dueOffset != payload.DueTick
            || item.Tick < payload.StartedTick)
        {
            throw new ArgumentException(
                "Resolution form-transition evidence must reference one exact declared route and configured clock.",
                parameterName);
        }
        return transition;
    }

    private static GenericActorRuntimeObservation.PendingSameLifeTransition
        PendingTransitionFrom(
            GenericActorRuntimeObservation.EventPayload.FormTransition
                payload) =>
        new(
            payload.TransitionId,
            payload.OperationId,
            payload.ToFormId,
            payload.StartedTick,
            payload.DueTick);

    private static void ValidateDestructionCancellationOrder(
        GenericActorWorldSnapshot.LifeSnapshot before,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        string parameterName)
    {
        GenericActorAuthoritativeEvent? destruction = events
            .SingleOrDefault(item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind.Destruction
                && item.Payload is
                    GenericActorRuntimeObservation.EventPayload.Destruction
                        payload
                && payload.ActorId == before.ActorId);
        if (destruction is null)
            return;
        GenericActorAuthoritativeEvent? cancellation = events
            .SingleOrDefault(item =>
                item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .FormTransitionCancelled
                && item.Payload is
                    GenericActorRuntimeObservation.EventPayload
                        .FormTransition payload
                && payload.ActorId == before.ActorId);
        if (cancellation is not null
            && cancellation.Ordinal <= destruction.Ordinal)
        {
            throw new ArgumentException(
                "Lethal same-life cancellation evidence must follow the actor's destruction evidence.",
                parameterName);
        }
    }

    private static bool CompletedResolutionTransitionStateMatches(
        ActorResolvedMatchDefinition definition,
        ActorFormTransitionDefinition transition,
        GenericActorWorldSnapshot.LifeSnapshot before,
        GenericActorWorldSnapshot.LifeSnapshot after,
        GenericActorWorldSnapshot postState,
        GenericActorMatchActorTurn turn,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        GenericActorAuthoritativeEvent completionEvent)
    {
        ActorFormDefinition source = definition.Rules.Forms.Single(form =>
            string.Equals(
                form.Id,
                transition.SourceFormId,
                StringComparison.Ordinal));
        ActorFormDefinition target = definition.Rules.Forms.Single(form =>
            string.Equals(
                form.Id,
                transition.TargetFormId,
                StringComparison.Ordinal));
        int completionHealth = events
            .Where(item => item.Ordinal < completionEvent.Ordinal
                && item.Payload is
                    GenericActorRuntimeObservation.EventPayload.Damage
                        damage
                && damage.TargetActorId == before.ActorId)
            .Select(item =>
                ((GenericActorRuntimeObservation.EventPayload.Damage)
                    item.Payload).NewHealth)
            .LastOrDefault(before.Health);
        (int cooldown, int? energy) = ResolutionResourceState(
            definition,
            source,
            before,
            events,
            completionEvent.Tick,
            FaultEligibilitySkipsResourceUpdate(
                definition,
                postState));
        int expectedHealth = TransitionHealth(
            transition.Health,
            completionHealth,
            source.MaxHealth,
            target.MaxHealth);
        int? expectedEnergy = TransitionEnergy(
            definition,
            target,
            energy);
        return before.ActorId == after.ActorId
            && before.ParticipantId == after.ParticipantId
            && before.Generation == after.Generation
            && before.Position == after.Position
            && before.Facing == after.Facing
            && after.Health == expectedHealth
            && after.Cooldown == cooldown
            && after.Energy == expectedEnergy
            && before.SpawnedAtTick == after.SpawnedAtTick
            && before.SpawnReason == after.SpawnReason
            && before.ParentActorId == after.ParentActorId
            && string.Equals(
                before.SourceTransitionId,
                after.SourceTransitionId,
                StringComparison.Ordinal)
            && string.Equals(
                before.SourceOperationId,
                after.SourceOperationId,
                StringComparison.Ordinal)
            && ActionResolutionsSemanticallyEqual(
                turn.ActionResolution,
                after.PreviousActionResolution);
    }

    private static (int Cooldown, int? Energy) ResolutionResourceState(
        ActorResolvedMatchDefinition definition,
        ActorFormDefinition source,
        GenericActorWorldSnapshot.LifeSnapshot before,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        int tick,
        bool skipUpdate)
    {
        if (skipUpdate)
            return (before.Cooldown, before.Energy);
        if (source.AttackProfileId is not string attackProfileId)
            return (before.Cooldown, null);
        ActorAttackProfileDefinition attack =
            definition.Rules.AttackProfiles.Single(profile =>
                string.Equals(
                    profile.Id,
                    attackProfileId,
                    StringComparison.Ordinal));
        bool attacked = events.Any(item =>
            item.Payload is
                GenericActorRuntimeObservation.EventPayload.Attack attackEvent
            && attackEvent.ActorId == before.ActorId);
        int cooldown = attacked
            ? attack.CooldownTicks
            : Math.Max(0, before.Cooldown - 1);
        if (attack.MaxEnergy == 0)
            return (cooldown, null);
        int energy = before.Energy
            ?? throw new ArgumentException(
                "An energy-bearing chronology life has no energy state.");
        if (attacked)
            energy = checked(energy - attack.AttackEnergyCost);
        if (attack.EnergyRegenerationIntervalTicks > 0
            && (tick + 1)
                % attack.EnergyRegenerationIntervalTicks == 0)
        {
            energy = checked((int)Math.Min(
                attack.MaxEnergy,
                checked((long)energy
                    + attack.EnergyRegenerationAmount)));
        }
        return (cooldown, energy);
    }

    private static bool FaultEligibilitySkipsResourceUpdate(
        ActorResolvedMatchDefinition definition,
        GenericActorWorldSnapshot postState)
    {
        HashSet<int> disqualified = postState.Participants
            .Where(participant => participant.Disqualified)
            .Select(participant => participant.ParticipantId)
            .ToHashSet();
        int eligibleTeamCount = definition.Topology.Teams.Count(team =>
            definition.Topology.Participants.Any(participant =>
                participant.TeamId == team.TeamId
                && !disqualified.Contains(participant.ParticipantId)));
        return eligibleTeamCount <= 1;
    }

    private static void ValidateSurvivingLivesAcrossLifecycleBoundary(
        ActorResolvedMatchDefinition definition,
        GenericActorWorldSnapshot before,
        GenericActorWorldSnapshot after,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        string parameterName)
    {
        Dictionary<ActorIdentity, GenericActorWorldSnapshot.LifeSnapshot>
            afterLives = after.ActiveLives.ToDictionary(
                life => life.ActorId);
        ILookup<ActorIdentity, GenericActorAuthoritativeEvent>
            transitionsByActor = events
                .Where(IsFormTransitionEvent)
                .ToLookup(item =>
                    ((GenericActorRuntimeObservation.EventPayload
                        .FormTransition)item.Payload).ActorId);
        foreach (GenericActorWorldSnapshot.LifeSnapshot beforeLife in
                 before.ActiveLives)
        {
            if (!afterLives.TryGetValue(
                    beforeLife.ActorId,
                    out GenericActorWorldSnapshot.LifeSnapshot? afterLife))
            {
                continue;
            }

            GenericActorAuthoritativeEvent[] transitionEvents =
                transitionsByActor[beforeLife.ActorId].ToArray();
            if (LifeSnapshotsSemanticallyEqual(beforeLife, afterLife))
            {
                if (transitionEvents.Length != 0)
                {
                    throw new ArgumentException(
                        "A tick-start form-transition event must cause its declared same-life state transition.",
                        parameterName);
                }
                continue;
            }
            if (transitionEvents.Length != 1
                || !SameLifeTransitionExplainsChange(
                    definition,
                    beforeLife,
                    afterLife,
                    transitionEvents[0]))
            {
                throw new ArgumentException(
                    "A surviving life must remain exact across tick-start unless one declared same-life transition event fully explains the change.",
                    parameterName);
            }
        }

        HashSet<ActorIdentity> survivingActors = before.ActiveLives
            .Select(life => life.ActorId)
            .Intersect(afterLives.Keys)
            .ToHashSet();
        if (events.Where(IsFormTransitionEvent).Any(item =>
                !survivingActors.Contains(
                    ((GenericActorRuntimeObservation.EventPayload
                        .FormTransition)item.Payload).ActorId)))
        {
            throw new ArgumentException(
                "Tick-start form-transition evidence must identify one surviving life.",
                parameterName);
        }
    }

    private static bool SameLifeTransitionExplainsChange(
        ActorResolvedMatchDefinition definition,
        GenericActorWorldSnapshot.LifeSnapshot before,
        GenericActorWorldSnapshot.LifeSnapshot after,
        GenericActorAuthoritativeEvent item)
    {
        if (item.Payload is not
                GenericActorRuntimeObservation.EventPayload.FormTransition
                    payload
            || payload.ActorId != before.ActorId)
        {
            return false;
        }
        ActorSameLifeTransitionDefinition? transition =
            definition.Rules.SameLifeTransitions.SingleOrDefault(value =>
                string.Equals(
                    value.TransitionId,
                    payload.TransitionId,
                    StringComparison.Ordinal));
        if (transition is null
            || !string.Equals(
                transition.SourceFormId,
                payload.FromFormId,
                StringComparison.Ordinal)
            || !string.Equals(
                transition.TargetFormId,
                payload.ToFormId,
                StringComparison.Ordinal))
        {
            return false;
        }

        return item.Kind switch
        {
            GenericActorRuntimeObservation.EventKind
                    .FormTransitionStarted =>
                before.PendingSameLifeTransition is null
                && after.PendingSameLifeTransition is { } started
                && PendingTransitionMatches(started, payload)
                && payload.StartedTick == item.Tick
                && LifeSnapshotsEqualExceptPending(before, after),
            GenericActorRuntimeObservation.EventKind
                    .FormTransitionCancelled =>
                before.PendingSameLifeTransition is { } cancelled
                && PendingTransitionMatches(cancelled, payload)
                && after.PendingSameLifeTransition is null
                && LifeSnapshotsEqualExceptPending(before, after),
            GenericActorRuntimeObservation.EventKind
                    .FormTransitionCompleted =>
                before.PendingSameLifeTransition is { } completed
                && PendingTransitionMatches(completed, payload)
                && payload.DueTick == item.Tick
                && after.PendingSameLifeTransition is null
                && CompletedTransitionStateMatches(
                    definition,
                    transition,
                    before,
                    after),
            _ => false,
        };
    }

    private static bool PendingTransitionMatches(
        GenericActorRuntimeObservation.PendingSameLifeTransition pending,
        GenericActorRuntimeObservation.EventPayload.FormTransition payload) =>
        string.Equals(
            pending.TransitionId,
            payload.TransitionId,
            StringComparison.Ordinal)
        && string.Equals(
            pending.OperationId,
            payload.OperationId,
            StringComparison.Ordinal)
        && string.Equals(
            pending.TargetFormId,
            payload.ToFormId,
            StringComparison.Ordinal)
        && pending.StartedTick == payload.StartedTick
        && pending.DueTick == payload.DueTick;

    private static bool LifeSnapshotsEqualExceptPending(
        GenericActorWorldSnapshot.LifeSnapshot before,
        GenericActorWorldSnapshot.LifeSnapshot after) =>
        before.ActorId == after.ActorId
        && before.ParticipantId == after.ParticipantId
        && before.Generation == after.Generation
        && string.Equals(
            before.FormId,
            after.FormId,
            StringComparison.Ordinal)
        && before.Position == after.Position
        && before.Facing == after.Facing
        && before.Health == after.Health
        && before.Cooldown == after.Cooldown
        && before.Energy == after.Energy
        && before.SpawnedAtTick == after.SpawnedAtTick
        && before.SpawnReason == after.SpawnReason
        && before.ParentActorId == after.ParentActorId
        && string.Equals(
            before.SourceTransitionId,
            after.SourceTransitionId,
            StringComparison.Ordinal)
        && string.Equals(
            before.SourceOperationId,
            after.SourceOperationId,
            StringComparison.Ordinal)
        && ActionResolutionsSemanticallyEqual(
            before.PreviousActionResolution,
            after.PreviousActionResolution);

    private static bool CompletedTransitionStateMatches(
        ActorResolvedMatchDefinition definition,
        ActorSameLifeTransitionDefinition transition,
        GenericActorWorldSnapshot.LifeSnapshot before,
        GenericActorWorldSnapshot.LifeSnapshot after)
    {
        ActorFormDefinition source = definition.Rules.Forms.Single(form =>
            string.Equals(
                form.Id,
                transition.SourceFormId,
                StringComparison.Ordinal));
        ActorFormDefinition target = definition.Rules.Forms.Single(form =>
            string.Equals(
                form.Id,
                transition.TargetFormId,
                StringComparison.Ordinal));
        int expectedHealth = TransitionHealth(
            transition.Health,
            before.Health,
            source.MaxHealth,
            target.MaxHealth);
        int? expectedEnergy = TransitionEnergy(
            definition,
            target,
            before.Energy);
        return before.ActorId == after.ActorId
            && before.ParticipantId == after.ParticipantId
            && before.Generation == after.Generation
            && string.Equals(
                before.FormId,
                transition.SourceFormId,
                StringComparison.Ordinal)
            && string.Equals(
                after.FormId,
                transition.TargetFormId,
                StringComparison.Ordinal)
            && before.Position == after.Position
            && before.Facing == after.Facing
            && after.Health == expectedHealth
            && before.Cooldown == after.Cooldown
            && after.Energy == expectedEnergy
            && before.SpawnedAtTick == after.SpawnedAtTick
            && before.SpawnReason == after.SpawnReason
            && before.ParentActorId == after.ParentActorId
            && string.Equals(
                before.SourceTransitionId,
                after.SourceTransitionId,
                StringComparison.Ordinal)
            && string.Equals(
                before.SourceOperationId,
                after.SourceOperationId,
                StringComparison.Ordinal)
            && ActionResolutionsSemanticallyEqual(
                before.PreviousActionResolution,
                after.PreviousActionResolution);
    }

    private static int TransitionHealth(
        ActorSameLifeHealthDefinition definition,
        int currentHealth,
        int sourceMaximum,
        int targetMaximum) =>
        definition.Policy switch
        {
            ActorSameLifeHealthDefinition.HealthPolicyKind
                    .PreserveCurrentCappedToTargetMaximum =>
                Math.Min(currentHealth, targetMaximum),
            ActorSameLifeHealthDefinition.HealthPolicyKind
                    .AddFlatCappedToTargetMaximum =>
                checked((int)Math.Min(
                    (long)currentHealth + definition.FlatHealthGain,
                    targetMaximum)),
            ActorSameLifeHealthDefinition.HealthPolicyKind
                    .SetToTargetMaximum =>
                targetMaximum,
            ActorSameLifeHealthDefinition.HealthPolicyKind
                    .PreserveRatioFloorMinimumOne =>
                checked((int)Math.Clamp(
                    checked((long)currentHealth * targetMaximum)
                        / sourceMaximum,
                    1L,
                    targetMaximum)),
            _ => throw new ArgumentOutOfRangeException(nameof(definition)),
        };

    private static int? TransitionEnergy(
        ActorResolvedMatchDefinition definition,
        ActorFormDefinition target,
        int? currentEnergy)
    {
        if (target.AttackProfileId is not string attackProfileId)
            return null;
        ActorAttackProfileDefinition attack =
            definition.Rules.AttackProfiles.Single(profile =>
                string.Equals(
                    profile.Id,
                    attackProfileId,
                    StringComparison.Ordinal));
        return attack.MaxEnergy == 0
            ? null
            : Math.Min(currentEnergy ?? 0, attack.MaxEnergy);
    }

    private static void ValidateProjectileLifecycleBoundary(
        GenericActorWorldSnapshot before,
        GenericActorWorldSnapshot after,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        IReadOnlyCollection<GenericActorProjectileTraversal> traversals,
        string parameterName)
    {
        Dictionary<long, GenericActorWorldSnapshot.ProjectileSnapshot>
            beforeProjectiles = before.Projectiles.ToDictionary(
                projectile => projectile.ProjectileId);
        Dictionary<long, GenericActorWorldSnapshot.ProjectileSnapshot>
            afterProjectiles = after.Projectiles.ToDictionary(
                projectile => projectile.ProjectileId);
        if (afterProjectiles.Keys.Any(id =>
                !beforeProjectiles.ContainsKey(id))
            || afterProjectiles.Any(pair =>
                !ProjectileSnapshotsSemanticallyEqual(
                    beforeProjectiles[pair.Key],
                    pair.Value)))
        {
            throw new ArgumentException(
                "Tick-start lifecycle processing cannot create or mutate a surviving projectile.",
                parameterName);
        }

        long[] removedIds = beforeProjectiles.Keys
            .Where(id => !afterProjectiles.ContainsKey(id))
            .Order()
            .ToArray();
        HashSet<ActorIdentity> priorActors = before.ActiveLives
            .Select(life => life.ActorId)
            .ToHashSet();
        Position[] placementPositions = after.ActiveLives
            .Where(life => !priorActors.Contains(life.ActorId))
            .Select(life => life.Position)
            .ToArray();
        long[] expectedRemovedIds = beforeProjectiles.Values
            .Where(projectile =>
                placementPositions.Contains(projectile.Position))
            .Select(projectile => projectile.ProjectileId)
            .Order()
            .ToArray();
        ILookup<long, GenericActorProjectileTraversal> traversalsById =
            traversals.ToLookup(item => item.ProjectileId);
        if (!removedIds.SequenceEqual(expectedRemovedIds)
            || removedIds.Any(id => traversalsById[id].Count() != 1)
            || traversals.Any(item =>
                beforeProjectiles.ContainsKey(item.ProjectileId)
                && afterProjectiles.ContainsKey(item.ProjectileId)))
        {
            throw new ArgumentException(
                "Every tick-start projectile removal must have exactly one lifecycle-placement traversal.",
                parameterName);
        }

        foreach (long removedId in removedIds)
        {
            GenericActorProjectileTraversal traversal =
                traversalsById[removedId].Single();
            GenericActorWorldSnapshot.ProjectileSnapshot projectile =
                beforeProjectiles[traversal.ProjectileId];
            GenericActorAuthoritativeEvent spawnEvent = events.Single(
                item =>
                    item.Payload is
                        GenericActorRuntimeObservation.EventPayload
                            .LifeSpawned spawned
                    && spawned.Position == projectile.Position);
            if (traversal.OwnerParticipantId
                    != projectile.OwnerParticipantId
                || traversal.OwnerTeamId != projectile.OwnerTeamId
                || traversal.OwnerActorId != projectile.OwnerActorId
                || !string.Equals(
                    traversal.AttackProfileId,
                    projectile.AttackProfileId,
                    StringComparison.Ordinal)
                || traversal.From != projectile.Position
                || !traversal.Path.IsEmpty
                || traversal.LaunchHeading != projectile.LaunchHeading
                || traversal.FinalHeading != projectile.Heading
                || traversal.ShotProgram != projectile.ShotProgram
                || traversal.Terminal is not
                    GenericActorProjectileTraversal.TerminalDisposition
                        .LifecyclePlacementPurge purge
                || purge.Position != projectile.Position
                || traversal.Ordinal >= spawnEvent.Ordinal)
            {
                throw new ArgumentException(
                    "A lifecycle-placement traversal must purge exactly a newly occupied output tile before its spawn evidence.",
                    parameterName);
            }
        }
    }

    private static void ValidateDerivedActiveHealth(
        ActorResolvedMatchDefinition definition,
        GenericActorWorldSnapshot state,
        string parameterName)
    {
        bool hasActiveHealth = definition.Rules.GameMode.ScoreCatalog.Any(
            channel => channel.Channel
                == ScoreChannelDefinition.ChannelKind.ActiveHealth);
        if (!hasActiveHealth)
            return;

        string channelId = ActorContractCanonicalIds.Id(
            ScoreChannelDefinition.ChannelKind.ActiveHealth);
        Dictionary<int, long> expected = definition.Topology.Teams
            .ToDictionary(team => team.TeamId, _ => 0L);
        foreach (GenericActorWorldSnapshot.LifeSnapshot life in
                 state.ActiveLives)
        {
            expected[life.ActorId.TeamId] = checked(
                expected[life.ActorId.TeamId] + life.Health);
        }
        if (state.Scoreboard.Teams.Any(team =>
                team.Scores.Single(score => string.Equals(
                    score.Channel,
                    channelId,
                    StringComparison.Ordinal)).Value
                != expected[team.TeamId]))
        {
            throw new ArgumentException(
                "The active-health score must equal the exact sum of active life health for each team.",
                parameterName);
        }
    }

    private static bool ScoreboardsStableAcrossLifecycleBoundary(
        GenericActorRuntimeObservation.ScoreboardState before,
        GenericActorRuntimeObservation.ScoreboardState after)
    {
        if (before.Teams.Length != after.Teams.Length)
            return false;
        string activeHealthId = ActorContractCanonicalIds.Id(
            ScoreChannelDefinition.ChannelKind.ActiveHealth);
        for (int index = 0; index < before.Teams.Length; index++)
        {
            GenericActorRuntimeObservation.TeamScoreState beforeTeam =
                before.Teams[index];
            GenericActorRuntimeObservation.TeamScoreState afterTeam =
                after.Teams[index];
            if (beforeTeam.TeamId != afterTeam.TeamId
                || beforeTeam.Eligible != afterTeam.Eligible
                || beforeTeam.Scores.Length != afterTeam.Scores.Length)
            {
                return false;
            }
            for (int scoreIndex = 0;
                 scoreIndex < beforeTeam.Scores.Length;
                 scoreIndex++)
            {
                GenericActorRuntimeObservation.ScoreValue beforeScore =
                    beforeTeam.Scores[scoreIndex];
                GenericActorRuntimeObservation.ScoreValue afterScore =
                    afterTeam.Scores[scoreIndex];
                if (!string.Equals(
                        beforeScore.Channel,
                        afterScore.Channel,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        beforeScore.Channel,
                        activeHealthId,
                        StringComparison.Ordinal)
                    && beforeScore.Value != afterScore.Value)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static bool IsLifeRemovalEvent(
        GenericActorAuthoritativeEvent item) =>
        item.Kind is GenericActorRuntimeObservation.EventKind.Destruction
            or GenericActorRuntimeObservation.EventKind.LifeRetired;

    private static bool IsLifecycleEvent(
        GenericActorAuthoritativeEvent item) =>
        item.Kind is
            GenericActorRuntimeObservation.EventKind.LifecycleQueued
            or GenericActorRuntimeObservation.EventKind.LifecycleCancelled
            or GenericActorRuntimeObservation.EventKind.LifecycleCompleted;

    private static ActorIdentity RemovedActorId(
        GenericActorAuthoritativeEvent item) =>
        item.Payload switch
        {
            GenericActorRuntimeObservation.EventPayload.Destruction value =>
                value.ActorId,
            GenericActorRuntimeObservation.EventPayload.LifeRetired value =>
                value.ActorId,
            _ => throw new ArgumentException(
                "Event is not life-removal evidence.",
                nameof(item)),
        };

    private static bool IsFormTransitionEvent(
        GenericActorAuthoritativeEvent item) =>
        item.Kind is
            GenericActorRuntimeObservation.EventKind.FormTransitionStarted
            or GenericActorRuntimeObservation.EventKind
                .FormTransitionCompleted
            or GenericActorRuntimeObservation.EventKind
                .FormTransitionCancelled;

    private static void ValidateResult(
        GenericActorMatchDescriptor descriptor,
        GenericActorMatchInitialFrame initialFrame,
        IReadOnlyList<GenericActorMatchTickFrame> ticks,
        GenericActorMatchResult result)
    {
        int? expectedEndTick = ticks.Count == 0
            ? null
            : ticks[^1].Tick;
        if (result.EndTick != expectedEndTick)
        {
            throw new ArgumentException(
                "Terminal EndTick must identify the final executed frame, or be null when no tick executed.",
                nameof(result));
        }

        bool modeMatches =
            (descriptor.Definition.Rules.GameMode, result.Mode) switch
            {
                (
                    DeathmatchGameModeDefinition,
                    GenericActorMatchModeResult.Deathmatch
                ) => true,
                (
                    FrontlineGameModeDefinition,
                    GenericActorMatchModeResult.Frontline
                ) => true,
                _ => false,
            };
        if (!modeMatches)
        {
            throw new ArgumentException(
                "Terminal mode facts must match the resolved game mode.",
                nameof(result));
        }

        GenericActorWorldSnapshot finalState = ticks.Count == 0
            ? initialFrame.State
            : ticks[^1].PostState;
        int[] expectedEligibleTeams = finalState.Scoreboard.Teams
            .Where(team => team.Eligible)
            .Select(team => team.TeamId)
            .Order()
            .ToArray();
        if (!result.EligibleTeamIds.SequenceEqual(expectedEligibleTeams))
        {
            throw new ArgumentException(
                "Terminal eligible teams must match the final authoritative scoreboard.",
                nameof(result));
        }
        Dictionary<(int TeamId, int UnitId),
            GenericActorWorldSnapshot.SlotSnapshot> stateSlots =
            finalState.Slots.ToDictionary(
                slot => (slot.TeamId, slot.UnitId));
        Dictionary<ActorIdentity, GenericActorWorldSnapshot.LifeSnapshot>
            stateLives = finalState.ActiveLives.ToDictionary(
                life => life.ActorId);
        if (result.Units.Length != stateSlots.Count
            || result.Units.Any(unit =>
                !stateSlots.TryGetValue(
                    (unit.TeamId, unit.UnitId),
                    out GenericActorWorldSnapshot.SlotSnapshot? slot)
                || !SlotSnapshotsSemanticallyEqual(slot, unit.Slot)
                || (unit.ActiveLife is null
                    ? slot.State is
                        GenericActorRuntimeObservation.UnitSlotState.Active
                    : !stateLives.TryGetValue(
                        unit.ActiveLife.ActorId,
                        out GenericActorWorldSnapshot.LifeSnapshot? life)
                        || !LifeSnapshotsSemanticallyEqual(
                            life,
                            unit.ActiveLife))))
        {
            throw new ArgumentException(
                "Terminal unit facts must exactly snapshot the final world.",
                nameof(result));
        }

        ValidateTerminalScores(finalState, result);
        if (descriptor.Definition.Rules.GameMode
            is DeathmatchGameModeDefinition)
        {
            GenericDeathmatchResultEvidence.Validate(
                descriptor.Definition,
                finalState,
                result);
        }
    }

    private static void ApplyLifeStarts(
        IEnumerable<GenericActorLifeStart> starts,
        IDictionary<(int TeamId, int UnitId), int> expectedNextLifeIds,
        IDictionary<ActorIdentity, GenericActorLifeStart> recordedActors,
        string parameterName)
    {
        GenericActorLifeStart[] orderedStarts = starts
            .OrderBy(value => value.ActorId)
            .ToArray();
        var batchActors = new HashSet<ActorIdentity>();
        foreach (GenericActorLifeStart start in orderedStarts)
        {
            var slotId = (start.ActorId.TeamId, start.ActorId.UnitId);
            GenericActorLifeStart? parent = null;
            if (start.Origin.ParentActorId is ActorIdentity parentActorId)
            {
                recordedActors.TryGetValue(parentActorId, out parent);
            }
            if (!expectedNextLifeIds.TryGetValue(
                    slotId,
                    out int expectedLifeId)
                || start.ActorId.LifeId != expectedLifeId
                || recordedActors.ContainsKey(start.ActorId)
                || !batchActors.Add(start.ActorId)
                || expectedLifeId == int.MaxValue)
            {
                throw new ArgumentException(
                    "Life starts must issue each stable slot's life IDs exactly once and in sequence, with an already-issued parent when lineage is present.",
                    parameterName);
            }
            start.ValidateDynamicLineage(parent);
            expectedNextLifeIds[slotId] = expectedLifeId + 1;
        }
        foreach (GenericActorLifeStart start in orderedStarts)
            recordedActors.Add(start.ActorId, start);
    }

    private static void ValidateNextLifeIds(
        GenericActorWorldSnapshot state,
        IReadOnlyDictionary<(int TeamId, int UnitId), int>
            expectedNextLifeIds,
        string parameterName)
    {
        if (state.Slots.Any(slot =>
                !expectedNextLifeIds.TryGetValue(
                    (slot.TeamId, slot.UnitId),
                    out int expectedNextLifeId)
                || slot.NextLifeId != expectedNextLifeId))
        {
            throw new ArgumentException(
                "World slot NextLifeId values must follow recorded life-start issuance exactly.",
                parameterName);
        }
    }

    private static void AppendPhase(
        IEnumerable<GenericActorAuthoritativeEvent> events,
        IEnumerable<GenericActorProjectileTraversal> traversals,
        ICollection<long> chronologicalOrdinals,
        ICollection<GenericActorAuthoritativeEvent> chronologicalEvents)
    {
        foreach ((long Ordinal, GenericActorAuthoritativeEvent? Event) fact in
                 events
                     .Select(item =>
                         (item.Ordinal,
                             (GenericActorAuthoritativeEvent?)item))
                     .Concat(traversals.Select(item =>
                         (item.Ordinal,
                             (GenericActorAuthoritativeEvent?)null)))
                     .OrderBy(item => item.Ordinal))
        {
            chronologicalOrdinals.Add(fact.Ordinal);
            if (fact.Event is not null)
                chronologicalEvents.Add(fact.Event);
        }
    }

    private static void ValidateTerminalScores(
        GenericActorWorldSnapshot finalState,
        GenericActorMatchResult result)
    {
        Dictionary<int,
            GenericActorRuntimeObservation.TeamScoreState> scoreboard =
            finalState.Scoreboard.Teams.ToDictionary(team => team.TeamId);
        foreach (TeamStanding standing in result.Standings.Standings)
        {
            GenericActorRuntimeObservation.TeamScoreState state =
                scoreboard[standing.TeamId];
            if (standing.Scores.Length != state.Scores.Length
                || standing.Scores.Any(score =>
                    !state.Scores.Any(value =>
                        string.Equals(
                            value.Channel,
                            ActorContractCanonicalIds.Id(score.Channel),
                            StringComparison.Ordinal)
                        && value.Value == score.Value)))
            {
                throw new ArgumentException(
                    "Terminal standings scores must exactly match the final authoritative scoreboard.",
                    nameof(result));
            }
        }

        if (result.Mode
            is GenericActorMatchModeResult.Frontline frontline)
        {
            string frontlineCompletionReason = frontline.Reason switch
            {
                GenericFrontlineEndReason.FaultEligibility =>
                    "fault-eligibility",
                GenericFrontlineEndReason.BaseBreach => "base-breach",
                GenericFrontlineEndReason.MaxTicks => "max-ticks",
                _ => throw new ArgumentOutOfRangeException(nameof(result)),
            };
            if (!string.Equals(
                    result.CompletionReason,
                    frontlineCompletionReason,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Frontline completion reason must match its typed terminal reason.",
                    nameof(result));
            }
            foreach (FrontlineTeamScore score in frontline.Scores.Teams)
            {
                if (!ScoreMatches(
                        scoreboard[score.TeamId],
                        ScoreChannelDefinition.ChannelKind
                            .TerritorialProgress,
                        score.TerritorialProgress))
                {
                    throw new ArgumentException(
                        "Frontline terminal scores must match every corresponding final scoreboard channel.",
                        nameof(result));
                }
            }
            return;
        }

        if (result.Mode is not
            GenericActorMatchModeResult.Deathmatch deathmatch)
        {
            return;
        }
        string expectedCompletionReason = deathmatch.Reason switch
        {
            GenericDeathmatchEndReason.FaultEligibility =>
                "fault-eligibility",
            GenericDeathmatchEndReason.KillLimit => "kill-limit",
            GenericDeathmatchEndReason.MaxTicks => "max-ticks",
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };
        if (!string.Equals(
                result.CompletionReason,
                expectedCompletionReason,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Deathmatch completion reason must match its typed terminal reason.",
                nameof(result));
        }

        foreach (DeathmatchTeamScore score in deathmatch.Scores.Teams)
        {
            GenericActorRuntimeObservation.TeamScoreState state =
                scoreboard[score.TeamId];
            if (!ScoreMatches(
                    state,
                    ScoreChannelDefinition.ChannelKind.Kills,
                    score.Kills)
                || !ScoreMatches(
                    state,
                    ScoreChannelDefinition.ChannelKind.Deaths,
                    score.Deaths)
                || !ScoreMatches(
                    state,
                    ScoreChannelDefinition.ChannelKind.DamageDealt,
                    score.DamageDealt))
            {
                throw new ArgumentException(
                    "Deathmatch terminal counters must match every corresponding final scoreboard channel.",
                    nameof(result));
            }
        }
    }

    private static bool ScoreMatches(
        GenericActorRuntimeObservation.TeamScoreState state,
        ScoreChannelDefinition.ChannelKind channel,
        long expected)
    {
        string channelId = ActorContractCanonicalIds.Id(channel);
        GenericActorRuntimeObservation.ScoreValue? score = state.Scores
            .SingleOrDefault(value => string.Equals(
                value.Channel,
                channelId,
                StringComparison.Ordinal));
        return score is null || score.Value == expected;
    }

    private static bool SlotSnapshotsSemanticallyEqual(
        GenericActorWorldSnapshot.SlotSnapshot left,
        GenericActorWorldSnapshot.SlotSnapshot right) =>
        left.TeamId == right.TeamId
        && left.UnitId == right.UnitId
        && left.ParticipantId == right.ParticipantId
        && left.NextLifeId == right.NextLifeId
        && left.State == right.State
        && left.PendingParentActorId == right.PendingParentActorId
        && ReplicationReservationsSemanticallyEqual(
            left.SplitReservation,
            right.SplitReservation);

    private static bool LifeSnapshotsSemanticallyEqual(
        GenericActorWorldSnapshot.LifeSnapshot left,
        GenericActorWorldSnapshot.LifeSnapshot right) =>
        left.ActorId == right.ActorId
        && left.ParticipantId == right.ParticipantId
        && left.Generation == right.Generation
        && string.Equals(left.FormId, right.FormId, StringComparison.Ordinal)
        && left.Position == right.Position
        && left.Facing == right.Facing
        && left.Health == right.Health
        && left.Cooldown == right.Cooldown
        && left.Energy == right.Energy
        && left.SpawnedAtTick == right.SpawnedAtTick
        && left.SpawnReason == right.SpawnReason
        && left.ParentActorId == right.ParentActorId
        && string.Equals(
            left.SourceTransitionId,
            right.SourceTransitionId,
            StringComparison.Ordinal)
        && string.Equals(
            left.SourceOperationId,
            right.SourceOperationId,
            StringComparison.Ordinal)
        && ActionResolutionsSemanticallyEqual(
            left.PreviousActionResolution,
            right.PreviousActionResolution)
        && left.PendingSameLifeTransition
            == right.PendingSameLifeTransition;

    private static bool ReplicationReservationsSemanticallyEqual(
        SplitReplicationReservation? left,
        SplitReplicationReservation? right)
    {
        if (left is null || right is null)
            return left is null && right is null;
        return left.ParticipantId == right.ParticipantId
            && left.SourceActorId == right.SourceActorId
            && left.SourceGeneration == right.SourceGeneration
            && string.Equals(
                left.SourceFormId,
                right.SourceFormId,
                StringComparison.Ordinal)
            && left.SourcePosition == right.SourcePosition
            && left.SourceFacing == right.SourceFacing
            && left.QueuedTick == right.QueuedTick
            && left.DueTick == right.DueTick
            && string.Equals(
                left.TransitionId,
                right.TransitionId,
                StringComparison.Ordinal)
            && string.Equals(
                left.OperationId,
                right.OperationId,
                StringComparison.Ordinal)
            && left.Descendants.SequenceEqual(right.Descendants);
    }

    private static bool ProjectileSnapshotsSemanticallyEqual(
        GenericActorWorldSnapshot.ProjectileSnapshot left,
        GenericActorWorldSnapshot.ProjectileSnapshot right) =>
        left.ProjectileId == right.ProjectileId
        && left.OwnerParticipantId == right.OwnerParticipantId
        && left.OwnerTeamId == right.OwnerTeamId
        && left.OwnerActorId == right.OwnerActorId
        && string.Equals(
            left.AttackProfileId,
            right.AttackProfileId,
            StringComparison.Ordinal)
        && left.SpawnedAtTick == right.SpawnedAtTick
        && left.Origin == right.Origin
        && left.Position == right.Position
        && left.LaunchHeading == right.LaunchHeading
        && left.Heading == right.Heading
        && left.ShotProgram == right.ShotProgram
        && left.CommittedPath.SequenceEqual(right.CommittedPath)
        && left.NextPathIndex == right.NextPathIndex
        && left.RemainingTiles == right.RemainingTiles
        && left.TicksUntilAdvance == right.TicksUntilAdvance;

    private static bool ActionResolutionsSemanticallyEqual(
        GenericActorRuntimeActionResolution? left,
        GenericActorRuntimeActionResolution? right)
    {
        if (left is null || right is null)
            return left is null && right is null;
        return ResolvedActionsSemanticallyEqual(
                left.SubmittedAction,
                right.SubmittedAction)
            && ResolvedActionsSemanticallyEqual(
                left.AcceptedAction,
                right.AcceptedAction)
            && ResolvedActionsSemanticallyEqual(
                left.ValidatedAction,
                right.ValidatedAction)
            && left.Outcome == right.Outcome
            && left.RuntimeFault == right.RuntimeFault;
    }

    private static bool ResolvedActionsSemanticallyEqual(
        GenericActorRuntimeActionResolution.ResolvedAction? left,
        GenericActorRuntimeActionResolution.ResolvedAction? right)
    {
        if (left is null || right is null)
            return left is null && right is null;
        return string.Equals(
                left.ActionId,
                right.ActionId,
                StringComparison.Ordinal)
            && left.ActionCode == right.ActionCode
            && !left.Arguments.IsDefault
            && !right.Arguments.IsDefault
            && left.Arguments.Length == right.Arguments.Length
            && left.Arguments
                .OrderBy(argument => argument.Kind)
                .Zip(right.Arguments.OrderBy(argument => argument.Kind))
                .All(pair => pair.First == pair.Second);
    }
}
