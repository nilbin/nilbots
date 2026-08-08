namespace BotArena.Engine;

/// <summary>
/// Match-scoped artifact owner that creates the ONE mind runtime a submitted
/// participant owns for the whole match. Contrast
/// <see cref="IGenericActorRuntimeFactory"/>, which creates one runtime per
/// life: at nine bodies that is 9 Stores, 9 guest threads and 576 MiB per
/// participant against 1, 1 and 128 MiB here
/// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §4.1).
/// </summary>
public interface IGenericMindRuntimeFactory : IDisposable
{
    IGenericMindRuntime CreateRuntime();

    void IDisposable.Dispose() { }
}
