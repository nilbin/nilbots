namespace BotArena.Engine;

/// <summary>
/// Classifies how each <see cref="GameRules"/> property participates in the public
/// match contract. The catalog is exhaustive and deliberately maintained by hand.
/// </summary>
public enum GameRuleDisclosure
{
    /// <summary>Projected into the bot-facing rules manifest and its fingerprint.</summary>
    PublicGameplay,

    /// <summary>Host/runtime enforcement that is not part of the bot-facing game model.</summary>
    RuntimeOnly,

    /// <summary>Controls replay/debug representation without changing simulated play.</summary>
    ReplayOnly,

    /// <summary>
    /// Deterministic seed or spawn machinery. It is omitted from the bot-facing shape
    /// but its effective behavior participates in the rules fingerprint.
    /// </summary>
    InternalSeedMechanics,
}
