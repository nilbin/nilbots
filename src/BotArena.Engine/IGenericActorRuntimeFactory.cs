namespace BotArena.Engine;

/// <summary>
/// Match-scoped artifact owner that creates isolated generic life runtimes.
/// </summary>
public interface IGenericActorRuntimeFactory : IDisposable
{
    IGenericActorRuntime CreateRuntime();

    void IDisposable.Dispose() { }
}
