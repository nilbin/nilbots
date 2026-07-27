namespace BotArena.Runtime.Wasm;

/// <summary>A structured fault reported by the framework-owned WASM guest.</summary>
public sealed class ActorWasmGuestException(string message) : Exception(message);
