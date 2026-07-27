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
    private readonly InMemoryGenericActorMatchChronologyRecorder _chronology =
        new();
    private readonly GenericActorRuntimeCoordinator _runtimes;
    private int _operationGate;
    private bool _disposed;

    public GenericActorMatchHost(
        ActorResolvedMatchDefinition definition,
        IEnumerable<GenericActorParticipantConfiguration> participants,
        ulong matchSeed)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(participants);

        GenericActorParticipantConfiguration[] participantSnapshot =
            [.. participants];
        _descriptor = GenericActorMatchDescriptor.Create(
            definition,
            matchSeed,
            participantSnapshot);
        _runtimes = new GenericActorRuntimeCoordinator(
            definition,
            participantSnapshot);
    }

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
            return _chronology.Snapshot;
        }
    }

    public string MatchContractFingerprint =>
        _runtimes.MatchContractFingerprint;

    public ImmutableArray<
        GenericActorRuntimeObservation.ObservedParticipantStatus>
        ParticipantStatuses => _runtimes.ParticipantStatuses;

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
            Origin = origin,
            Contract = _descriptor.Definition,
        };
        GenericActorLifeStart lifeStart =
            GenericActorLifeStart.FromRuntimeStart(runtimeStart);
        _runtimes.StartLife(runtimeStart);
        return lifeStart;
    }

    public void RetireLife(ActorIdentity actorId) =>
        _runtimes.RetireLife(actorId);

    public GenericActorRuntimeTickResult CollectTickDecisions(
        int tick,
        IEnumerable<GenericActorRuntimeObservation> observations) =>
        _runtimes.CollectTickDecisions(tick, observations);

    public ImmutableArray<ActorIdentity> ApplyDisqualification(
        int participantId) =>
        _runtimes.ApplyDisqualification(participantId);

    public bool TryProjectSubmittedAction(
        GenericActorRuntimeDecision? decision,
        out GenericActorRuntimeActionResolution.ResolvedAction?
            submittedAction) =>
        _runtimes.TryProjectSubmittedAction(decision, out submittedAction);

    public void RecordInitial(GenericActorMatchInitialFrame initialFrame) =>
        _chronology.RecordInitial(_descriptor, initialFrame);

    public void RecordResolvedTick(GenericActorMatchTickFrame frame) =>
        _chronology.RecordResolvedTick(frame);

    public void RecordCompleted(GenericActorMatchResult result) =>
        _chronology.RecordCompleted(result);

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
        _runtimes.Dispose();
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
