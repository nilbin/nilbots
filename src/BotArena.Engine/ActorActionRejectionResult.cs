namespace BotArena.Engine;

/// <summary>Typed outcomes for structurally unsupported action payloads.</summary>
public enum ActorActionRejectionResult
{
    Blocked = 0,
    Faulted = 1,
    Rejected = 2,
}
