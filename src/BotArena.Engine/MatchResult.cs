namespace BotArena.Engine;

public enum MatchEndReason
{
    Elimination,
    Disqualification,
    MaxTicks,
}

public enum BotOutcome
{
    Win,
    Loss,
    Draw,
}

public sealed record BotMatchResult(
    int Slot, BotOutcome Outcome, int FinalHealth, int DamageDealt, int Faults, BotStatus FinalStatus);

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
