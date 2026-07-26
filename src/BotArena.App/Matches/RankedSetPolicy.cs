using BotArena.App.Store;

namespace BotArena.App.Matches;

/// <summary>
/// How many ranked sets an account may start, and how many at once.
/// <para>
/// A ranked set is **six** WASM matches, so this is the most expensive thing a player can
/// ask for in a single request — more than a build, which already has a durable limit of
/// its own. Until now it had none: only the shared in-memory challenge limiter, which
/// multiplies by web replica and resets on restart.
/// </para>
/// </summary>
public sealed record RankedSetLimits(int AccountDailyLimit, int AccountConcurrentLimit)
{
    /// <summary>
    /// Ten sets — sixty matches — a day, and two in flight.
    /// <para>
    /// Lower than the 20/minute the HTTP limiter allowed, and deliberately so: that number
    /// was burst protection borrowed from unranked challenges, not a considered budget for
    /// six-match sets. Ten is enough to test a change against the ladder several times in a
    /// sitting; the concurrency limit is what stops one account filling the match worker's
    /// queue while everyone else waits.
    /// </para>
    /// </summary>
    public static RankedSetLimits FromConfiguration(IConfiguration configuration) => new(
        Read(configuration, "BOTARENA_RANKED_ACCOUNT_DAILY", 10, 1, 500),
        Read(configuration, "BOTARENA_RANKED_ACCOUNT_CONCURRENT", 2, 1, 50));

    /// <summary>These limits as they apply to one account, after anything it has bought.</summary>
    /// <remarks>
    /// Only the daily allowance moves, for the same reason it is the only build limit that
    /// does: concurrency is a claim on the shared match worker, and selling it would let a
    /// payer push everyone else's sets down the queue rather than merely play more.
    /// </remarks>
    public RankedSetLimits ForAccount(IReadOnlyCollection<string> entitlementKeys) =>
        this with
        {
            AccountDailyLimit =
                AccountDailyLimit + AccountCapacity.ExtraDailyRankedSetsFor(entitlementKeys),
        };

    private static int Read(IConfiguration configuration, string name, int fallback, int min, int max)
    {
        int value = configuration.GetValue<int?>(name) ?? fallback;
        return Math.Clamp(value, min, max);
    }
}

public sealed record RankedSetSnapshot(int AccountDailyCount, int AccountUnfinishedCount);

public static class RankedSetPolicy
{
    /// <summary>The reason a set cannot start, or null.</summary>
    public static string? Evaluate(RankedSetSnapshot snapshot, RankedSetLimits limits)
    {
        if (snapshot.AccountUnfinishedCount >= limits.AccountConcurrentLimit)
            return $"You already have {limits.AccountConcurrentLimit} ranked sets in progress. " +
                   "Wait for one to finish.";
        if (snapshot.AccountDailyCount >= limits.AccountDailyLimit)
            return $"Accounts may start at most {limits.AccountDailyLimit} ranked sets per 24 hours.";
        return null;
    }
}
