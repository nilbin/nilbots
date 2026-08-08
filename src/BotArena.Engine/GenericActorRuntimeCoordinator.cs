using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Transport-neutral owner of participant runtime factories and isolated
/// per-life runtime instances. It collects one exact active-life decision
/// batch at a time; gameplay, damage, lifecycle, and mode state remain outside
/// this boundary.
/// </summary>
public sealed class GenericActorRuntimeCoordinator : IDisposable
{
    private readonly ActorResolvedMatchDefinition _contract;
    private readonly GenericActorDecisionAdmission _admission;
    private readonly string _matchContractFingerprint;
    private readonly Dictionary<int, ParticipantState> _participants;
    private readonly Dictionary<(int TeamId, int UnitId), PublicUnitSlot>
        _slots;
    private readonly Dictionary<ActorIdentity, ActiveLifeState> _activeLives =
        [];
    private readonly HashSet<ActorIdentity> _issuedActorIds = [];
    private readonly HashSet<IGenericActorRuntime> _issuedRuntimes =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(int TeamId, int UnitId), ActorIdentity>
        _occupiedSlots = [];
    private readonly Dictionary<string, ActorFormDefinition> _formsById;
    private readonly HashSet<int> _pendingDisqualifications = [];
    private int? _lastCollectedTick;
    private bool _collectingTick;
    private bool _disposed;

    /// <summary>
    /// Creates a match-scoped coordinator and takes ownership of every
    /// participant runtime factory after successful validation.
    /// </summary>
    public GenericActorRuntimeCoordinator(
        ActorResolvedMatchDefinition contract,
        IEnumerable<GenericActorParticipantConfiguration> participants)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(participants);

        GenericActorParticipantConfiguration[] configurations =
            [.. participants];
        ValidateParticipantConfigurations(contract, configurations);

        _contract = contract;
        _admission = new GenericActorDecisionAdmission(contract);
        _matchContractFingerprint =
            ActorContractFingerprint.ComputeMatch(contract);
        _slots = contract.Topology.UnitSlots.ToDictionary(
            slot => (slot.TeamId, slot.UnitId));
        _formsById = contract.Rules.Forms.ToDictionary(
            form => form.Id,
            StringComparer.Ordinal);
        _participants = configurations.ToDictionary(
            configuration => configuration.ParticipantId,
            configuration => new ParticipantState(configuration));
    }

    /// <summary>The immutable resolved match definition used by all lives.</summary>
    public ActorResolvedMatchDefinition Contract => _contract;

    /// <summary>Fingerprint joining every life and observation to the match.</summary>
    public string MatchContractFingerprint => _matchContractFingerprint;

    /// <summary>Active runtime-layer actor identities in canonical order.</summary>
    public ImmutableArray<ActorIdentity> ActiveActorIds
    {
        get
        {
            ThrowIfDisposed();
            return _activeLives.Keys.Order().ToImmutableArray();
        }
    }

    /// <summary>
    /// Canonical participant status snapshot. A participant whose threshold
    /// was reached remains eligible until the caller applies the reported
    /// disqualification after joint damage.
    /// </summary>
    public ImmutableArray<
        GenericActorRuntimeObservation.ObservedParticipantStatus>
        ParticipantStatuses
    {
        get
        {
            ThrowIfDisposed();
            return _participants.Values
                .OrderBy(participant => participant.ParticipantId)
                .Select(participant =>
                    new GenericActorRuntimeObservation
                        .ObservedParticipantStatus(
                            participant.ParticipantId,
                            participant.TeamId,
                            participant.FaultCount,
                            participant.Disqualified)
                        {
                            ClassId = _contract.Topology.Participants
                                .Single(value =>
                                    value.ParticipantId
                                        == participant.ParticipantId)
                                .ClassId,
                        })
                .ToImmutableArray();
        }
    }

    /// <summary>
    /// Projects a raw runtime reply into the public typed action model only
    /// when its selectors, argument shape, and catalog-static values are
    /// representable. Dynamic per-tick illegality and debug-message faults do
    /// not make an otherwise well-formed submitted action unrepresentable.
    /// </summary>
    internal bool TryProjectSubmittedAction(
        GenericActorRuntimeDecision? decision,
        out GenericActorRuntimeActionResolution.ResolvedAction?
            submittedAction) =>
        _admission.TryProjectSubmittedAction(decision, out submittedAction);

    /// <summary>
    /// Registers one authoritative active life. Runtime creation and
    /// <see cref="IGenericActorRuntime.StartLife"/> occur immediately before
    /// its first canonical decision opportunity, so any failure joins that
    /// tick's complete fault batch.
    /// </summary>
    public void StartLife(GenericActorRuntimeStart start)
    {
        ThrowIfDisposed();
        ThrowIfCollectingTick();
        ArgumentNullException.ThrowIfNull(start);
        if (_pendingDisqualifications.Count != 0)
        {
            throw new InvalidOperationException(
                "Reported participant disqualifications must be applied before starting another life.");
        }

        ValidateLifeStart(start);
        if (_issuedActorIds.Contains(start.ActorId))
        {
            throw new ArgumentException(
                $"Actor life identity '{start.ActorId}' was already issued in this match.",
                nameof(start));
        }

        var slotKey = (start.ActorId.TeamId, start.ActorId.UnitId);
        if (_occupiedSlots.TryGetValue(
                slotKey,
                out ActorIdentity? occupyingActor))
        {
            throw new ArgumentException(
                $"Unit slot {slotKey.TeamId}:{slotKey.UnitId} is already " +
                $"occupied by actor life '{occupyingActor}'.",
                nameof(start));
        }

        _activeLives.Add(
            start.ActorId,
            new ActiveLifeState(start));
        _issuedActorIds.Add(start.ActorId);
        _occupiedSlots.Add(slotKey, start.ActorId);
    }

    /// <summary>
    /// Retires one runtime life without changing its participant fault state.
    /// Destruction, replication, and other gameplay lifecycle owners call this
    /// explicitly when their authoritative life ceases to be active.
    /// </summary>
    public void RetireLife(ActorIdentity actorId)
    {
        ThrowIfDisposed();
        ThrowIfCollectingTick();
        ArgumentNullException.ThrowIfNull(actorId);
        if (!_activeLives.Remove(
                actorId,
                out ActiveLifeState? life))
        {
            throw new ArgumentException(
                $"Actor life '{actorId}' is not active.",
                nameof(actorId));
        }

        _occupiedSlots.Remove((actorId.TeamId, actorId.UnitId));
        DiscardRuntime(life);
    }

    /// <summary>
    /// Invokes every and only active life once, in participant then actor
    /// identity order. The complete input is validated before the first
    /// runtime is touched.
    /// </summary>
    public GenericActorRuntimeTickResult CollectTickDecisions(
        int tick,
        IEnumerable<GenericActorRuntimeObservation> observations)
    {
        ThrowIfDisposed();
        ThrowIfCollectingTick();
        ArgumentNullException.ThrowIfNull(observations);
        if (tick < 0)
            throw new ArgumentOutOfRangeException(nameof(tick));
        if (_lastCollectedTick is int previousTick
            && tick <= previousTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tick),
                "Decision ticks must increase strictly.");
        }
        if (_pendingDisqualifications.Count != 0)
        {
            throw new InvalidOperationException(
                "Reported participant disqualifications must be applied before collecting another tick.");
        }

        Dictionary<ActorIdentity, GenericActorRuntimeObservation>
            observationsByActor = ValidateExactObservationBatch(
                tick,
                observations);
        ActiveLifeState[] invocationOrder = _activeLives.Values
            .OrderBy(life => life.Start.ParticipantId)
            .ThenBy(life => life.Start.ActorId)
            .ToArray();

        _collectingTick = true;
        _lastCollectedTick = tick;
        try
        {
            var turns = ImmutableArray.CreateBuilder<
                GenericActorRuntimeTurn>(invocationOrder.Length);
            var faults =
                ImmutableArray.CreateBuilder<GenericActorRuntimeFault>();
            foreach (ActiveLifeState life in invocationOrder)
            {
                GenericActorRuntimeObservation observation =
                    observationsByActor[life.Start.ActorId];
                GenericActorRuntimeTurn turn = Invoke(life, observation);
                turns.Add(turn);
                if (turn.RuntimeFault is not null)
                    faults.Add(turn.RuntimeFault);
            }

            ImmutableArray<int> newlyDisqualified =
                _pendingDisqualifications
                    .Order()
                    .ToImmutableArray();
            return new GenericActorRuntimeTickResult(
                tick,
                turns.MoveToImmutable(),
                faults.ToImmutable(),
                newlyDisqualified);
        }
        finally
        {
            _collectingTick = false;
        }
    }

    /// <summary>
    /// Applies one threshold crossing previously reported by a complete tick
    /// batch. All runtime-layer lives owned by the participant are removed and
    /// disposed in actor identity order. The returned identities let the
    /// gameplay session perform its own retirement and slot cleanup.
    /// </summary>
    public ImmutableArray<ActorIdentity> ApplyDisqualification(
        int participantId)
    {
        ThrowIfDisposed();
        ThrowIfCollectingTick();
        if (!_participants.TryGetValue(
                participantId,
                out ParticipantState? participant))
        {
            throw new ArgumentOutOfRangeException(nameof(participantId));
        }
        if (!_pendingDisqualifications.Remove(participantId))
        {
            throw new InvalidOperationException(
                $"Participant {participantId} has no reported disqualification pending.");
        }

        participant.Disqualified = true;
        ActorIdentity[] actorIds = _activeLives.Values
            .Where(life => life.Start.ParticipantId == participantId)
            .Select(life => life.Start.ActorId)
            .Order()
            .ToArray();
        ActiveLifeState[] retiredLives = actorIds
            .Select(actorId => _activeLives[actorId])
            .ToArray();
        foreach (ActorIdentity actorId in actorIds)
        {
            _activeLives.Remove(actorId);
            _occupiedSlots.Remove((actorId.TeamId, actorId.UnitId));
        }
        foreach (ActiveLifeState life in retiredLives)
        {
            DiscardRuntime(life);
        }

        return actorIds.ToImmutableArray();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        ThrowIfCollectingTick();
        _disposed = true;

        var errors = new List<Exception>();
        foreach (ActiveLifeState life in _activeLives.Values
                     .OrderBy(life => life.Start.ParticipantId)
                     .ThenBy(life => life.Start.ActorId))
        {
            TryDisposeRuntime(life, errors);
        }
        _activeLives.Clear();
        _occupiedSlots.Clear();

        foreach (ParticipantState participant in _participants.Values
                     .OrderBy(participant => participant.ParticipantId))
        {
            try
            {
                participant.RuntimeFactory.Dispose();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        ThrowDisposalErrors(errors);
    }

    private GenericActorRuntimeTurn Invoke(
        ActiveLifeState life,
        GenericActorRuntimeObservation observation)
    {
        ParticipantState participant =
            _participants[life.Start.ParticipantId];
        GenericActorRuntimeFault? fault;
        if (life.Runtime is null)
        {
            IGenericActorRuntime created;
            try
            {
                created = participant.RuntimeFactory.CreateRuntime()
                    ?? throw new InvalidOperationException(
                        "Runtime factory returned null.");
            }
            catch (Exception)
            {
                life.Runtime = null;
                fault = RecordFault(
                    participant,
                    life.Start.ActorId,
                    GenericActorRuntimeFault.FaultStage.RuntimeCreate,
                    GenericActorRuntimeFaultCodes.RuntimeCreateFailed);
                return FaultedTurn(life, observation, fault, null);
            }

            if (!_issuedRuntimes.Add(created))
            {
                fault = RecordFault(
                    participant,
                    life.Start.ActorId,
                    GenericActorRuntimeFault.FaultStage.RuntimeCreate,
                    GenericActorRuntimeFaultCodes.RuntimeInstanceReused);
                return FaultedTurn(life, observation, fault, null);
            }
            life.Runtime = created;

            try
            {
                life.Runtime.StartLife(life.Start);
            }
            catch (Exception)
            {
                DiscardRuntime(life);
                fault = RecordFault(
                    participant,
                    life.Start.ActorId,
                    GenericActorRuntimeFault.FaultStage.LifeStart,
                    GenericActorRuntimeFaultCodes.LifeStartFailed);
                return FaultedTurn(life, observation, fault, null);
            }
        }

        GenericActorRuntimeDecision? submitted;
        try
        {
            submitted = life.Runtime.ExecuteTick(observation);
        }
        catch (Exception)
        {
            DiscardRuntime(life);
            fault = RecordFault(
                participant,
                life.Start.ActorId,
                GenericActorRuntimeFault.FaultStage.TickExecution,
                GenericActorRuntimeFaultCodes.TickExecutionFailed);
            return FaultedTurn(life, observation, fault, null);
        }

        if (!_admission.TryAdmitDecision(
                observation.Self.FormId,
                observation.ActionLegalities,
                submitted,
                out GenericActorRuntimeDecision? admitted,
                out string? faultCode))
        {
            fault = RecordFault(
                participant,
                life.Start.ActorId,
                GenericActorRuntimeFault.FaultStage.DecisionValidation,
                faultCode!);
            return FaultedTurn(
                life,
                observation,
                fault,
                submitted);
        }

        return new GenericActorRuntimeTurn(
            participant.ParticipantId,
            life.Start.ActorId,
            submitted,
            admitted!,
            GenericActorRuntimeActionResolution.ActionOutcome.Success,
            RuntimeFault: null);
    }

    private GenericActorRuntimeTurn FaultedTurn(
        ActiveLifeState life,
        GenericActorRuntimeObservation observation,
        GenericActorRuntimeFault fault,
        GenericActorRuntimeDecision? submitted) =>
        new(
            life.Start.ParticipantId,
            life.Start.ActorId,
            submitted,
            SyntheticWait(observation.Self.FormId),
            GenericActorRuntimeActionResolution.ActionOutcome.Faulted,
            fault);

    private GenericActorRuntimeFault RecordFault(
        ParticipantState participant,
        ActorIdentity actorId,
        GenericActorRuntimeFault.FaultStage stage,
        string faultCode)
    {
        long threshold = _contract.Rules.Limits.RuntimeFaults
            .DisqualificationFaultCount;
        long previous = participant.FaultCount;
        if (previous < threshold)
            participant.FaultCount = previous + 1;
        bool triggered =
            previous < threshold && participant.FaultCount >= threshold;
        if (triggered)
            _pendingDisqualifications.Add(participant.ParticipantId);

        return new GenericActorRuntimeFault(
            participant.ParticipantId,
            actorId,
            stage,
            faultCode,
            participant.FaultCount,
            triggered);
    }

    private Dictionary<ActorIdentity, GenericActorRuntimeObservation>
        ValidateExactObservationBatch(
            int tick,
            IEnumerable<GenericActorRuntimeObservation> observations)
    {
        var byActor =
            new Dictionary<ActorIdentity, GenericActorRuntimeObservation>();
        foreach (GenericActorRuntimeObservation? observation in observations)
        {
            if (observation is null)
            {
                throw new ArgumentException(
                    "Observation batches cannot contain null entries.",
                    nameof(observations));
            }
            ActorIdentity? actorId = observation.Self?.ActorId;
            if (actorId is null)
            {
                throw new ArgumentException(
                    "Every observation must identify its actor life.",
                    nameof(observations));
            }
            if (!byActor.TryAdd(actorId, observation))
            {
                throw new ArgumentException(
                    $"Observation batch contains actor '{actorId}' more than once.",
                    nameof(observations));
            }
        }

        if (byActor.Count != _activeLives.Count
            || byActor.Keys.Any(actorId => !_activeLives.ContainsKey(actorId))
            || _activeLives.Keys.Any(actorId => !byActor.ContainsKey(actorId)))
        {
            throw new ArgumentException(
                "Observation batch must contain every and only active actor life exactly once.",
                nameof(observations));
        }

        foreach ((ActorIdentity actorId,
                  GenericActorRuntimeObservation observation) in byActor)
        {
            ValidateObservation(
                tick,
                _activeLives[actorId],
                observation);
        }
        return byActor;
    }

    private void ValidateObservation(
        int tick,
        ActiveLifeState life,
        GenericActorRuntimeObservation observation)
    {
        if (observation.SchemaVersion
            != _contract.CapabilityVersions.ObservationSchemaVersion)
        {
            throw InvalidObservation(
                life.Start.ActorId,
                "schema version does not match the resolved capability profile");
        }
        if (observation.Tick != tick)
        {
            throw InvalidObservation(
                life.Start.ActorId,
                $"tick {observation.Tick} does not match batch tick {tick}");
        }
        if (!string.Equals(
                observation.MatchContractFingerprint,
                _matchContractFingerprint,
                StringComparison.Ordinal))
        {
            throw InvalidObservation(
                life.Start.ActorId,
                "match-contract fingerprint does not match");
        }
        if (observation.Self.ActorId != life.Start.ActorId)
        {
            throw InvalidObservation(
                life.Start.ActorId,
                "self identity does not match the registered life");
        }
        if (observation.Self.Generation != life.Start.Origin.Generation)
        {
            throw InvalidObservation(
                life.Start.ActorId,
                "generation does not match the registered life");
        }
        if (!_formsById.TryGetValue(
                observation.Self.FormId,
                out ActorFormDefinition? form))
        {
            throw InvalidObservation(
                life.Start.ActorId,
                $"self form '{observation.Self.FormId}' is not in the rules catalog");
        }

        _admission.ValidateActionLegalities(
            life.Start.ActorId,
            form,
            observation.ActionLegalities);
    }

    private GenericActorRuntimeDecision SyntheticWait(string formId) =>
        _admission.SyntheticWait(formId);

    private void ValidateLifeStart(GenericActorRuntimeStart start)
    {
        if (start.ActorId is null
            || start.Origin is null
            || start.Contract is null)
        {
            throw new ArgumentException(
                "Life start must contain actor identity, origin, and contract.",
                nameof(start));
        }
        if (!_participants.TryGetValue(
                start.ParticipantId,
                out ParticipantState? participant))
        {
            throw new ArgumentException(
                $"Life start references unknown participant {start.ParticipantId}.",
                nameof(start));
        }
        if (participant.Disqualified
            || _pendingDisqualifications.Contains(start.ParticipantId))
        {
            throw new InvalidOperationException(
                $"Disqualified participant {start.ParticipantId} cannot start another life.");
        }
        if (!_slots.TryGetValue(
                (start.ActorId.TeamId, start.ActorId.UnitId),
                out PublicUnitSlot? slot)
            || slot.ControllerParticipantId != start.ParticipantId
            || participant.TeamId != start.ActorId.TeamId)
        {
            throw new ArgumentException(
                $"Actor '{start.ActorId}' is not owned by participant {start.ParticipantId}.",
                nameof(start));
        }
        if (start.SchemaVersion
                != _contract.CapabilityVersions.MatchStartSchemaVersion
            || start.RuntimeContractVersion
                != _contract.CapabilityVersions.RuntimeContractVersion)
        {
            throw new ArgumentException(
                "Life start schema versions do not match the resolved capability profile.",
                nameof(start));
        }
        if (start.Origin.Generation < 0
            || !Enum.IsDefined(start.Origin.Reason))
        {
            throw new ArgumentException(
                "Life start origin is malformed.",
                nameof(start));
        }
        if (!string.Equals(
                ActorContractFingerprint.ComputeMatch(start.Contract),
                _matchContractFingerprint,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Life start contract does not match the coordinator contract.",
                nameof(start));
        }
    }

    private static void ValidateParticipantConfigurations(
        ActorResolvedMatchDefinition contract,
        IReadOnlyList<GenericActorParticipantConfiguration> configurations)
    {
        var expected = contract.Topology.Participants.ToDictionary(
            participant => participant.ParticipantId);
        var actual =
            new Dictionary<int, GenericActorParticipantConfiguration>();
        var factories = new HashSet<IGenericActorRuntimeFactory>(
            ReferenceEqualityComparer.Instance);
        foreach (GenericActorParticipantConfiguration? configuration in
                 configurations)
        {
            if (configuration is null
                || configuration.RuntimeFactory is null)
            {
                throw new ArgumentException(
                    "Participant configurations and runtime factories cannot be null.",
                    nameof(configurations));
            }
            if (!actual.TryAdd(
                    configuration.ParticipantId,
                    configuration))
            {
                throw new ArgumentException(
                    $"Participant {configuration.ParticipantId} is configured more than once.",
                    nameof(configurations));
            }
            if (!expected.TryGetValue(
                    configuration.ParticipantId,
                    out PublicParticipant? participant)
                || participant.TeamId != configuration.TeamId)
            {
                throw new ArgumentException(
                    $"Participant {configuration.ParticipantId} does not match the resolved topology.",
                    nameof(configurations));
            }
            if (!factories.Add(configuration.RuntimeFactory))
            {
                throw new ArgumentException(
                    "Each participant must own a distinct runtime factory instance.",
                    nameof(configurations));
            }
        }
        if (actual.Count != expected.Count
            || expected.Keys.Any(participantId =>
                !actual.ContainsKey(participantId)))
        {
            throw new ArgumentException(
                "Participant configurations must exactly match the resolved topology.",
                nameof(configurations));
        }
    }

    private static ArgumentException InvalidObservation(
        ActorIdentity actorId,
        string reason) =>
        GenericActorDecisionAdmission.InvalidObservation(actorId, reason);

    private static void DiscardRuntime(ActiveLifeState life)
    {
        IGenericActorRuntime? runtime = life.Runtime;
        life.Runtime = null;
        if (runtime is null)
            return;
        try
        {
            runtime.Dispose();
        }
        catch (Exception)
        {
            // Adapter cleanup is best-effort after authoritative runtime and
            // lifecycle state has already committed.
        }
    }

    private static void TryDisposeRuntime(
        ActiveLifeState life,
        ICollection<Exception> errors)
    {
        IGenericActorRuntime? runtime = life.Runtime;
        life.Runtime = null;
        if (runtime is null)
            return;
        try
        {
            runtime.Dispose();
        }
        catch (Exception exception)
        {
            errors.Add(exception);
        }
    }

    private static void ThrowDisposalErrors(IReadOnlyCollection<Exception> errors)
    {
        if (errors.Count == 1)
            throw errors.Single();
        if (errors.Count > 1)
            throw new AggregateException(errors);
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    private void ThrowIfCollectingTick()
    {
        if (_collectingTick)
        {
            throw new InvalidOperationException(
                "The runtime coordinator cannot be mutated or invoked recursively while collecting a joint tick.");
        }
    }

    private sealed class ParticipantState
    {
        public ParticipantState(
            GenericActorParticipantConfiguration configuration)
        {
            ParticipantId = configuration.ParticipantId;
            TeamId = configuration.TeamId;
            RuntimeFactory = configuration.RuntimeFactory!;
        }

        public int ParticipantId { get; }
        public int TeamId { get; }
        public IGenericActorRuntimeFactory RuntimeFactory { get; }
        public long FaultCount { get; set; }
        public bool Disqualified { get; set; }
    }

    private sealed class ActiveLifeState
    {
        public ActiveLifeState(GenericActorRuntimeStart start)
        {
            Start = start;
        }

        public GenericActorRuntimeStart Start { get; }
        public IGenericActorRuntime? Runtime { get; set; }
    }
}
