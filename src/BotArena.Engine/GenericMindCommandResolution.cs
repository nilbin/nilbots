namespace BotArena.Engine;

/// <summary>
/// One submitted command and what the host did with it. The distinction to
/// keep sharp is engine-refused-at-runtime versus document-malformed
/// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §5.3): a
/// <see cref="GenericMindCommandOutcome.Rejected"/> command naming a dead body
/// is legitimate, recorded, and replayable.
/// </summary>
public sealed record GenericMindCommandResolution(
    GenericMindCommand Command,
    GenericMindCommandOutcome Outcome);
