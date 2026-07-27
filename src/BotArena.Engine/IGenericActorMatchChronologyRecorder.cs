namespace BotArena.Engine;

/// <summary>
/// Synchronous, callback-free sink at the generic match chronology boundary.
/// </summary>
public interface IGenericActorMatchChronologyRecorder
{
    void RecordInitial(
        GenericActorMatchDescriptor descriptor,
        GenericActorMatchInitialFrame initialFrame);

    void RecordResolvedTick(GenericActorMatchTickFrame frame);

    void RecordCompleted(GenericActorMatchResult result);
}
