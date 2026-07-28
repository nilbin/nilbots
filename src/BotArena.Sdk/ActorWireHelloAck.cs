namespace BotArena.Sdk;

/// <summary>
/// Parsed HelloAck including the optional exact contract generation selected
/// by a generation-aware guest.
/// </summary>
internal readonly record struct ActorWireHelloAck(
    int SelectedMajor,
    ActorContractProfile? SelectedProfile);
