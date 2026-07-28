namespace BotArena.Guest;

internal sealed class ActorCapabilityNotSupportedException(
    string capability,
    string message)
    : NotSupportedException(message)
{
    public string Capability { get; } = capability;
}
