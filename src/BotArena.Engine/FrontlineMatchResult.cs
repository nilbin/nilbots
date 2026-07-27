namespace BotArena.Engine;

/// <summary>Terminal team result for a Frontline match.</summary>
public sealed record FrontlineMatchResult(
    int? WinnerTeamId,
    FrontlineMatchEndReason Reason,
    int EndTick,
    long TerritorialScore,
    FrontlineControlState Control,
    IReadOnlyList<FrontlineTeamMatchResult> Teams);
