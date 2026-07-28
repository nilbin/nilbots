namespace BotArena.Engine;

/// <summary>Allocation-free chronology sink for callers that do not record.</summary>
public sealed class NoOpGenericActorMatchChronologyRecorder
    : IGenericActorMatchChronologyRecorder
{
    private NoOpGenericActorMatchChronologyRecorder()
    {
    }

    public static NoOpGenericActorMatchChronologyRecorder Instance { get; } =
        new();

    public void RecordInitial(
        GenericActorMatchDescriptor descriptor,
        GenericActorMatchInitialFrame initialFrame)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(initialFrame);
    }

    public void RecordResolvedTick(GenericActorMatchTickFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
    }

    public void RecordCompleted(GenericActorMatchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
    }
}
