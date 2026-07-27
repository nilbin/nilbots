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
        if (initialFrame.Events.Any(IsLifeRemovalEvent))
        {
            throw new ArgumentException(
                "Initial events cannot retire or destroy a life.",
                nameof(initialFrame));
        }

        GenericActorWorldSnapshot previousState = initialFrame.State;
        foreach (GenericActorMatchTickFrame frame in ticks)
        {
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
                nameof(ticks));
            ValidateResolutionLifeEvidence(
                frame.TickStart.State,
                frame.PostState,
                frame.Events,
                nameof(ticks));
            previousState = frame.PostState;
        }
    }

    private static void ValidateLifecycleBoundary(
        ActorResolvedMatchDefinition definition,
        GenericActorWorldSnapshot before,
        GenericActorMatchTickStart tickStart,
        string parameterName)
    {
        GenericActorWorldSnapshot after = tickStart.State;
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
            tickStart.Traversals,
            parameterName);
    }

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
        GenericActorWorldSnapshot before,
        GenericActorWorldSnapshot after,
        IReadOnlyCollection<GenericActorAuthoritativeEvent> events,
        string parameterName)
    {
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
        ValidateLifeRemovalEvidence(
            before,
            after,
            events,
            requireUnchangedPosition: false,
            parameterName);
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
        ILookup<long, GenericActorProjectileTraversal> traversalsById =
            traversals.ToLookup(item => item.ProjectileId);
        if (removedIds.Any(id => traversalsById[id].Count() != 1)
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
                || purge.Position != projectile.Position)
            {
                throw new ArgumentException(
                    "A lifecycle-placement traversal must exactly snapshot the purged projectile.",
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

        bool isDeathmatch = descriptor.Definition.Rules.GameMode
            is DeathmatchGameModeDefinition;
        if (isDeathmatch
            != (result.Mode is GenericActorMatchModeResult.Deathmatch))
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
        if (isDeathmatch)
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
