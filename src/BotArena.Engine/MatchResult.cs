namespace BotArena.Engine;

public enum MatchEndReason
{
    Elimination,
    Disqualification,
    MaxTicks,
    /// <summary>Zone control held for ZoneDominationTicks (rules 0.3 candidate).</summary>
    Domination,
}

public enum BotOutcome
{
    Win,
    Loss,
    Draw,
}

/// <summary>ZoneTicks is null (omitted from canonical JSON) under rules without zone
/// control, so pre-0.3 replay hashes are unaffected.</summary>
public sealed record BotMatchResult(
    int Slot, BotOutcome Outcome, int FinalHealth, int DamageDealt, int Faults, BotStatus FinalStatus,
    int? ZoneTicks = null);

public sealed record MatchResultInfo
{
    /// <summary>Null on a draw. Not `required`: canonical JSON omits nulls, so a drawn
    /// replay has no winnerSlot property and deserialization must tolerate its absence
    /// (gen-3 finding: `replay --summary`/`verify` crashed on every drawn match).</summary>
    public int? WinnerSlot { get; init; }
    public required MatchEndReason Reason { get; init; }
    public required int EndTick { get; init; }
    public required IReadOnlyList<BotMatchResult> Bots { get; init; }
}
