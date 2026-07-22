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
    public required int? WinnerSlot { get; init; }
    public required MatchEndReason Reason { get; init; }
    public required int EndTick { get; init; }
    public required IReadOnlyList<BotMatchResult> Bots { get; init; }
}
