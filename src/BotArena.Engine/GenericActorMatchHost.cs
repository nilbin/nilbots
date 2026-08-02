using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Mode-neutral match infrastructure shared by generic actor sessions. It
/// owns runtime resources, match identity, seed derivation, chronology, and
/// the operation boundary that isolates in-process runtime callbacks from
/// authoritative session state.
/// </summary>
internal sealed class GenericActorMatchHost : IDisposable
{
    private readonly GenericActorMatchDescriptor _descriptor;
    private readonly InMemoryGenericActorMatchChronologyRecorder? _chronology;
    private readonly GenericActorRuntimeCoordinator? _runtimes;
    private readonly GenericMindRuntimeCoordinator? _minds;
    private int _operationGate;
    private bool _disposed;

    public GenericActorMatchHost(
        ActorResolvedMatchDefinition definition,
        IEnumerable<GenericActorParticipantConfiguration> participants,
        ulong matchSeed,
        bool recordChronology = true)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(participants);

        GenericActorParticipantConfiguration[] participantSnapshot =
            [.. participants];
        _descriptor = GenericActorMatchDescriptor.Create(
            definition,
            matchSeed,
            participantSnapshot);
        _chronology = recordChronology
            ? new InMemoryGenericActorMatchChronologyRecorder()
            : null;
        // ONE profile ID decides the execution shape: one runtime per life, or
        // one runtime per participant. Everything downstream of decision
        // collection is identical, which is the whole design (DECISIONS #191).
        if (definition.CapabilityVersions.IsMindProfile)
        {
            _minds = new GenericMindRuntimeCoordinator(
                definition,
                participantSnapshot,
                matchSeed);
        }
        else
        {
            _runtimes = new GenericActorRuntimeCoordinator(
                definition,
                participantSnapshot);
        }
    }

    /// <summary>True when this match runs the participant-scoped mind profile.</summary>
    public bool IsMindProfile => _minds is not null;

    /// <summary>
    /// Whether this host retains the audit-grade observation chronology.
    /// Trusted product playback may instead consume each authoritative tick
    /// immediately through the session result and persist a compact broadcast.
    /// </summary>
    public bool RecordsChronology => _chronology is not null;

    /// <inheritdoc cref="GenericMindRuntimeCoordinator.TickingParticipantIds"/>
    public ImmutableArray<int> TickingParticipantIds =>
        Minds.TickingParticipantIds;

    private GenericActorRuntimeCoordinator Runtimes =>
        _runtimes
        ?? throw new InvalidOperationException(
            "This match resolved the mind profile and has no per-life runtime coordinator.");

    private GenericMindRuntimeCoordinator Minds =>
        _minds
        ?? throw new InvalidOperationException(
            "This match resolved the per-life profile and has no mind coordinator.");

    public GenericActorMatchDescriptor Descriptor
    {
        get
        {
            ThrowIfOperationInProgress();
            return _descriptor;
        }
    }

    public GenericActorMatchChronology Chronology
    {
        get
        {
            ThrowIfOperationInProgress();
            return _chronology?.Snapshot
                ?? throw new InvalidOperationException(
                    "This match was configured for compact playback and has no audit chronology.");
        }
    }

    public string MatchContractFingerprint =>
        _minds?.MatchContractFingerprint
        ?? Runtimes.MatchContractFingerprint;

    public ImmutableArray<
        GenericActorRuntimeObservation.ObservedParticipantStatus>
        ParticipantStatuses =>
        _minds?.ParticipantStatuses ?? Runtimes.ParticipantStatuses;

    public bool IsDisposed => _disposed;

    public GenericActorLifeStart StartLife(
        ActorIdentity actorId,
        int participantId,
        GenericActorRuntimeStart.LifeOrigin origin)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(actorId);
        ArgumentNullException.ThrowIfNull(origin);

        var runtimeStart = new GenericActorRuntimeStart
        {
            SchemaVersion =
                _descriptor.Definition.CapabilityVersions
                    .MatchStartSchemaVersion,
            RuntimeContractVersion =
                _descriptor.Definition.CapabilityVersions
                    .RuntimeContractVersion,
            ActorId = actorId,
            ParticipantId = participantId,
            ActorRandomSeed = SeedDerivation.DeriveActorSeed(
                _descriptor.MatchSeed,
                actorId,
                _descriptor.Definition.Rules.SeedMechanics.SeedProfileId),
            TeamRandomSeed = SeedDerivation.DeriveTeamSeed(
                _descriptor.MatchSeed,
                actorId.TeamId,
                _descriptor.Definition.Rules.SeedMechanics.SeedProfileId),
            Origin = origin,
            Contract = _descriptor.Definition,
        };
        GenericActorLifeStart lifeStart =
            GenericActorLifeStart.FromRuntimeStart(runtimeStart);
        if (_minds is not null)
            _minds.StartLife(runtimeStart);
        else
            Runtimes.StartLife(runtimeStart);
        return lifeStart;
    }

    public void RetireLife(ActorIdentity actorId)
    {
        if (_minds is not null)
            _minds.RetireLife(actorId);
        else
            Runtimes.RetireLife(actorId);
    }

    public GenericActorRuntimeTickResult CollectTickDecisions(
        int tick,
        IEnumerable<GenericActorRuntimeObservation> observations) =>
        Runtimes.CollectTickDecisions(tick, observations);

    /// <summary>
    /// Collects one decision MAP per participant and fans it out across that
    /// participant's bodies. This is the ONE structural change the mind
    /// profile makes to the host: <c>PrepareTick()</c> -&gt; <c>Step()</c>, the
    /// 16 tick phases, the re-entrancy guard and the invocation ordering are
    /// all preserved.
    /// </summary>
    public GenericMindRuntimeTickResult CollectMindTickDecisions(
        int tick,
        IEnumerable<GenericMindRuntimeObservation> observations) =>
        Minds.CollectTickDecisions(tick, observations);

    public ImmutableArray<ActorIdentity> ApplyDisqualification(
        int participantId) =>
        _minds is not null
            ? _minds.ApplyDisqualification(participantId)
            : Runtimes.ApplyDisqualification(participantId);

    public bool TryProjectSubmittedAction(
        GenericActorRuntimeDecision? decision,
        out GenericActorRuntimeActionResolution.ResolvedAction?
            submittedAction) =>
        _minds is not null
            ? _minds.TryProjectSubmittedAction(decision, out submittedAction)
            : Runtimes.TryProjectSubmittedAction(decision, out submittedAction);

    public void RecordInitial(GenericActorMatchInitialFrame initialFrame) =>
        _chronology?.RecordInitial(_descriptor, initialFrame);

    public void RecordResolvedTick(GenericActorMatchTickFrame frame) =>
        _chronology?.RecordResolvedTick(frame);

    public void RecordCompleted(GenericActorMatchResult result) =>
        _chronology?.RecordCompleted(result);

    public void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    public void ThrowIfOperationInProgress()
    {
        if (Volatile.Read(ref _operationGate) != 0)
        {
            throw new InvalidOperationException(
                "Authoritative session state cannot be inspected from inside a runtime callback.");
        }
    }

    public void Dispose()
    {
        using HostOperation scope = EnterOperation(nameof(Dispose));
        DisposeWithinOperation();
    }

    public void DisposeWithinOperation()
    {
        if (Volatile.Read(ref _operationGate) == 0)
        {
            throw new InvalidOperationException(
                "Match resources may only be disposed inside a host operation.");
        }
        if (_disposed)
            return;

        _disposed = true;
        _runtimes?.Dispose();
        _minds?.Dispose();
    }

    public HostOperation EnterOperation(string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        if (Interlocked.CompareExchange(
                ref _operationGate,
                value: 1,
                comparand: 0) != 0)
        {
            throw new InvalidOperationException(
                $"Generic actor match host cannot enter '{operationName}' while another match operation is in progress.");
        }
        return new HostOperation(this);
    }

    private void ExitOperation() => Volatile.Write(ref _operationGate, 0);

    public readonly struct HostOperation : IDisposable
    {
        private readonly GenericActorMatchHost _owner;

        public HostOperation(GenericActorMatchHost owner) => _owner = owner;

        public void Dispose() => _owner.ExitOperation();
    }
}
