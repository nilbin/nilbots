namespace BotArena.Engine;

/// <summary>Per-match initialization data handed to a runtime before the first tick.</summary>
public sealed record BotMatchStart
{
    public required int Slot { get; init; }
    public required ulong BotRandomSeed { get; init; }
    public required string GameRulesVersion { get; init; }
    public required string RuntimeProtocolVersion { get; init; }
}

/// <summary>
/// Runtime abstraction (plan §17). The engine drives any bot through this interface and must
/// behave identically regardless of the implementation (in-process, WASM, scripted test double).
/// </summary>
public interface IBotRuntime : IDisposable
{
    /// <summary>Creates the per-match bot instance. A runtime must never reuse an instance across matches.</summary>
    void StartMatch(BotMatchStart start);

    /// <summary>Executes one tick. Implementations must catch bot failures and return a faulted decision
    /// rather than throwing; the engine treats an exception here as a fault as a last resort.</summary>
    BotDecision ExecuteTick(BotObservation observation);

    void IDisposable.Dispose() { }
}
