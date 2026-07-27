namespace BotArena.Engine;

/// <summary>
/// Deterministic in-memory recorder used by local runs, tests, and replay
/// projection. It owns no runtime callbacks.
/// </summary>
public sealed class InMemoryGenericActorMatchChronologyRecorder
    : IGenericActorMatchChronologyRecorder
{
    private readonly List<GenericActorMatchTickFrame> _ticks = [];
    private GenericActorMatchDescriptor? _descriptor;
    private GenericActorMatchInitialFrame? _initialFrame;
    private GenericActorMatchResult? _result;

    public bool HasInitialFrame => _initialFrame is not null;
    public bool IsCompleted => _result is not null;

    public GenericActorMatchChronology Snapshot
    {
        get
        {
            if (_descriptor is null || _initialFrame is null)
            {
                throw new InvalidOperationException(
                    "Chronology has not recorded its initial frame.");
            }
            return new GenericActorMatchChronology(
                _descriptor,
                _initialFrame,
                _ticks,
                _result);
        }
    }

    public void RecordInitial(
        GenericActorMatchDescriptor descriptor,
        GenericActorMatchInitialFrame initialFrame)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(initialFrame);
        if (_initialFrame is not null)
        {
            throw new InvalidOperationException(
                "Chronology initial frame may be recorded exactly once.");
        }

        _ = new GenericActorMatchChronology(
            descriptor,
            initialFrame,
            [],
            result: null);
        _descriptor = descriptor;
        _initialFrame = initialFrame;
    }

    public void RecordResolvedTick(GenericActorMatchTickFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        EnsureInitialized();
        if (_result is not null)
        {
            throw new InvalidOperationException(
                "A completed chronology cannot accept another tick.");
        }
        if (frame.Tick != _ticks.Count)
        {
            throw new InvalidOperationException(
                $"Expected chronology tick {_ticks.Count}, got {frame.Tick}.");
        }

        // Full semantic validation belongs to Snapshot/finalization. Rebuilding
        // the complete immutable chronology for every append would make a
        // T-tick match O(T²), which is unacceptable for long playback and ML
        // rollout workloads.
        _ticks.Add(frame);
    }

    public void RecordCompleted(GenericActorMatchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        EnsureInitialized();
        if (_result is not null)
        {
            throw new InvalidOperationException(
                "Chronology completion may be recorded exactly once.");
        }

        _ = new GenericActorMatchChronology(
            _descriptor!,
            _initialFrame!,
            _ticks,
            result);
        _result = result;
    }

    private void EnsureInitialized()
    {
        if (_descriptor is null || _initialFrame is null)
        {
            throw new InvalidOperationException(
                "RecordInitial must precede ticks or completion.");
        }
    }
}
