namespace BotArena.App.Bots;

public sealed record CompilerSubmissionSnapshot(
    bool BotHasActiveBuild,
    int AccountTenMinuteCount,
    int AccountDailyCount,
    int NetworkTenMinuteCount,
    int NetworkDailyCount,
    int AccountQueuedCount,
    int GlobalQueuedCount);

public sealed record CompilerSubmissionDenial(string Message, TimeSpan RetryAfter);

public static class CompilerSubmissionPolicy
{
    public static CompilerSubmissionDenial? Evaluate(
        CompilerSubmissionSnapshot snapshot,
        CompilerSubmissionLimits limits)
    {
        if (snapshot.BotHasActiveBuild)
            return Wait("This bot already has a build queued or running.");
        if (snapshot.AccountQueuedCount >= limits.AccountQueuedLimit)
            return Wait($"Your account already has {limits.AccountQueuedLimit} builds queued.");
        if (snapshot.GlobalQueuedCount >= limits.GlobalQueuedLimit)
            return Wait("The compiler queue is currently full.");
        if (snapshot.AccountTenMinuteCount >= limits.AccountTenMinuteLimit)
            return new(
                $"Accounts may submit at most {limits.AccountTenMinuteLimit} builds per ten minutes.",
                TimeSpan.FromMinutes(10));
        if (snapshot.AccountDailyCount >= limits.AccountDailyLimit)
            return new(
                $"Accounts may submit at most {limits.AccountDailyLimit} builds per 24 hours.",
                TimeSpan.FromHours(24));
        if (snapshot.NetworkTenMinuteCount >= limits.NetworkTenMinuteLimit)
            return new(
                "This network has submitted too many builds in the last ten minutes.",
                TimeSpan.FromMinutes(10));
        if (snapshot.NetworkDailyCount >= limits.NetworkDailyLimit)
            return new(
                "This network has submitted too many builds in the last 24 hours.",
                TimeSpan.FromHours(24));
        return null;
    }

    private static CompilerSubmissionDenial Wait(string message) =>
        new(message, TimeSpan.FromMinutes(1));
}

