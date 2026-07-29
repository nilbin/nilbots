namespace BotArena.App.Matches;

public sealed record ArenaAllowanceResponse(
    int Used,
    int Limit,
    int Remaining,
    int RollingWindowHours,
    DateTime? NextDailySlotAt,
    bool CanStart,
    string? RefusalCode,
    DateTime? RetryAt);
