namespace BotArena.Cli;

/// <summary>
/// What <c>--print-candidate-contract</c> emits.
/// <para>
/// The flag shipped printing IDENTITY — ruleset/map/format IDs and their
/// fingerprints — which answers "which contract is this cell?" but not "what
/// are its numbers?". Three authoring waves reported the same friction (#184,
/// #188): the declared values that decide doctrine (cooldown ticks, capture
/// arithmetic, the economy schedule, every transition's windup) were reachable
/// only by running a throwaway match and mining
/// <c>replay.json → header.contract</c>. <see cref="Full"/> prints exactly
/// those bytes instead.
/// </para>
/// </summary>
public enum FrontlineLabsContractPrintMode
{
    /// <summary>IDs and fingerprints only — the historical bare-flag output.</summary>
    Identity,

    /// <summary>
    /// The complete resolved canonical match contract: the same document the
    /// runtime receives at MatchStart and the same one a replay-v3 header
    /// carries, byte for byte.
    /// </summary>
    Full,
}
