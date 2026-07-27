namespace BotArena.Engine;

/// <summary>
/// One isolated generic runtime instance for exactly one actor life.
/// </summary>
public interface IGenericActorRuntime : IDisposable
{
    void StartLife(GenericActorRuntimeStart start);

    GenericActorRuntimeDecision ExecuteTick(GenericActorRuntimeObservation observation);

    void IDisposable.Dispose() { }
}
