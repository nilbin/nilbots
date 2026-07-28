namespace BotArena.App.Matches;

public sealed record RankedArenaAllowanceResponse(
    int Used,
    int Limit,
    int Remaining,
    int RollingWindowHours,
    DateTime? NextDailySlotAt,
    int InProgress,
    int ConcurrencyLimit,
    bool CanStart,
    string? RefusalCode,
    DateTime? RetryAt);
