namespace BotArena.Runtime.Wasm;

/// <summary>
/// The module is a valid nilbots artifact but did not negotiate actor protocol
/// 1.0. Admission can distinguish this explicit Frontline ineligibility from
/// a trap or malformed actor reply.
/// </summary>
public sealed class ActorProtocolNotSupportedException(string message)
    : Exception(message);
