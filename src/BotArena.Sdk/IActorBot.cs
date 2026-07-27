namespace BotArena.Sdk;

/// <summary>
/// Entity-match programming model. Nilbots creates one independent instance
/// per active body life. Instance fields persist through form changes, but
/// destruction discards the instance and a later life starts with fresh
/// private memory.
/// </summary>
public interface IActorBot
{
    /// <summary>
    /// Called exactly once for a fresh life, before its first tick. Static
    /// rules, map, topology, identity, and deterministic seed are delivered
    /// here and are not repeated in tick observations.
    /// </summary>
    void StartLife(ActorMatchStart start) { }

    /// <summary>Choose exactly one action for this body on the current tick.</summary>
    ActorDecision Tick(ActorContext context);
}
