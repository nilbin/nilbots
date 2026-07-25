namespace BotArena.App.Matches;

/// <summary>One bot considered as a ranked opponent, reduced to the only three facts
/// the choice depends on.</summary>
public sealed record MatchmakingCandidate(Guid BotId, double Rating, bool SameOwner);

/// <summary>
/// Picks a ranked opponent. Players do not choose who they play (DECISIONS #95): a
/// self-selected opponent is the one lever an author has over their own rating, and a
/// ladder where you pick your fights is not measuring what it claims to. Pure so the
/// selection rule can be tested without a database, bots, or a match.
/// </summary>
public static class RankedMatchmaking
{
    /// <summary>How many of the nearest-rated bots to draw from. One would be
    /// deterministic — the same two bots would play forever, and a rating would say more
    /// about who happened to be adjacent than about strength. Five keeps every set
    /// competitive while still varying the opponent.</summary>
    public const int PoolSize = 5;

    /// <summary>
    /// The nearest <see cref="PoolSize"/> candidates by rating, then uniformly at random
    /// among them. Bots belonging to the challenger are excluded whenever anyone else is
    /// available: they are legitimate practice, but ranking against yourself is the
    /// shape of every farming scheme, and on a ladder with other players there is no
    /// reason to allow it. Returns null when nobody can be played.
    /// </summary>
    /// <param name="candidates">Every bot with a built, active version except the challenger.</param>
    /// <param name="challengerRating">The challenger's rating on this ladder.</param>
    /// <param name="pickBelow">Random index source, given an exclusive upper bound.</param>
    public static Guid? Choose(
        IReadOnlyList<MatchmakingCandidate> candidates,
        double challengerRating,
        Func<int, int> pickBelow)
    {
        var pool = candidates.Where(c => !c.SameOwner).ToList();
        if (pool.Count == 0)
            pool = [.. candidates];  // a ladder of one owner: your own bots or no set at all
        if (pool.Count == 0)
            return null;

        var nearest = pool
            .OrderBy(c => Math.Abs(c.Rating - challengerRating))
            .ThenBy(c => c.BotId)  // stable, so the pool does not depend on query order
            .Take(PoolSize)
            .ToList();
        return nearest[pickBelow(nearest.Count)].BotId;
    }
}
