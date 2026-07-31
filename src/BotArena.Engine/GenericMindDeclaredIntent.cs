namespace BotArena.Engine;

/// <summary>
/// RESERVED inter-mind declaration
/// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §11.1). Ships nothing:
/// a non-empty submission is <c>Rejected</c> until a format with allied minds
/// is admitted, and the engine never delivers one.
/// <para>
/// The reservation exists because #188's honest finding — "TeamRandom's first
/// doctrine verdict is null-to-negative" — has an explanation the mind
/// supplies. Intra-team the scarce thing was AGREEMENT, not unpredictability,
/// and solving agreement also solved the coin. Between allied minds agreement
/// is genuinely unavailable, so a shared stream the enemy cannot derive is the
/// only channel that exists at tick 0. This is where that value actually is.
/// </para>
/// </summary>
/// <param name="TagId">
/// Lowercase kebab semantic ID, at most 32 UTF-8 bytes.
/// </param>
public sealed record GenericMindDeclaredIntent(string TagId, long Value);
