namespace BotArena.Sdk;

/// <summary>
/// Generic entity-match programming model. Nilbots creates one independent
/// instance for each active body life. The same interface applies across
/// match formats, game modes, team sizes, and host-declared transform
/// catalogs.
/// </summary>
public interface IGenericActorBot
{
    /// <summary>
    /// Called exactly once for a fresh life before its first tick. The exact
    /// canonical rules, map, topology, identity, lineage, and deterministic
    /// seed are delivered here and are not repeated in observations.
    /// </summary>
    /// <param name="start">The immutable initialization for this body life.</param>
    void StartLife(GenericActorMatchStart start) { }

    /// <summary>Chooses exactly one catalog action for this body this tick.</summary>
    /// <param name="context">The observing life's public pre-tick state.</param>
    /// <returns>One action selected from the negotiated catalog.</returns>
    GenericActorDecision Tick(GenericActorContext context);
}
