namespace BotArena.App.Store;

/// <summary>
/// Entitlements that change what an account may *do*, rather than how its bots look.
/// <para>
/// The catalog knows these as items with the <c>capacity</c> kind; this is the only place
/// that says what holding one actually means. Keeping the effect here rather than in the
/// catalog JSON is deliberate: a limit is enforced by code that has to be read alongside
/// the rule it bends, and a number in a data file that silently multiplies a rate limit is
/// how a store ends up selling something nobody can reason about.
/// </para>
/// <para>
/// **Only the daily build cap is sold.** The ten-minute and queued caps exist to protect
/// the compiler from bursts, and the ladder from someone iterating faster than everyone
/// else during a tuning session — selling those would be selling tempo in a competitive
/// game. The daily cap is the one that reads as "I ran out of turns today", and lifting it
/// removes waiting rather than granting an edge in the moment.
/// </para>
/// </summary>
public static class AccountCapacity
{
    /// <summary>Catalog kind for an item that raises a limit.</summary>
    public const string Kind = "capacity";

    /// <summary>Raises the daily build allowance. Stacks: two held means twice the extra.</summary>
    public const string ExtraDailyBuildsId = "extra-daily-builds";

    public const string ExtraDailyBuildsKey = $"{Kind}:{ExtraDailyBuildsId}";

    /// <summary>
    /// How many builds one grant adds to the daily cap.
    /// <para>
    /// Additive rather than a multiplier so the effect is legible on the account page —
    /// "30 + 30" is a sentence a player can check, and a multiplier compounds in ways that
    /// need a calculator to predict once a second tier exists.
    /// </para>
    /// </summary>
    public const int ExtraDailyBuilds = 30;

    /// <summary>
    /// Ceiling on how far the daily cap can be bought up, regardless of grants held.
    /// <para>
    /// The compiler is a real machine and the daily cap is partly a capacity plan, not only
    /// a fairness rule. Without a ceiling, enough purchases could commit the build farm to
    /// work it cannot do — and the person who paid would be the one who found out.
    /// </para>
    /// </summary>
    public const int MaxPurchasedDailyBuilds = 120;

    /// <summary>Raises the daily ranked-set allowance. Stacks, like the build grant.</summary>
    public const string ExtraDailyRankedSetsId = "extra-daily-ranked-sets";

    public const string ExtraDailyRankedSetsKey = $"{Kind}:{ExtraDailyRankedSetsId}";

    /// <summary>
    /// How many ranked sets one grant adds to the daily cap.
    /// <para>
    /// Five, against a free ten — a smaller step than the build grant's thirty, because a
    /// set is six matches and the match worker is the scarcer resource of the two.
    /// </para>
    /// </summary>
    public const int ExtraDailyRankedSets = 5;

    /// <summary>Ceiling on bought ranked sets, for the same reason builds have one.</summary>
    public const int MaxPurchasedDailyRankedSets = 30;

    /// <summary>How many extra daily builds these entitlement keys are worth.</summary>
    public static int ExtraDailyBuildsFor(IReadOnlyCollection<string> entitlementKeys) =>
        Stacked(entitlementKeys, ExtraDailyBuildsKey, ExtraDailyBuilds, MaxPurchasedDailyBuilds);

    /// <summary>How many extra daily ranked sets these entitlement keys are worth.</summary>
    public static int ExtraDailyRankedSetsFor(IReadOnlyCollection<string> entitlementKeys) =>
        Stacked(
            entitlementKeys,
            ExtraDailyRankedSetsKey,
            ExtraDailyRankedSets,
            MaxPurchasedDailyRankedSets);

    private static int Stacked(
        IReadOnlyCollection<string> entitlementKeys,
        string key,
        int perGrant,
        int ceiling) =>
        Math.Min(entitlementKeys.Count(held => held == key) * perGrant, ceiling);
}
