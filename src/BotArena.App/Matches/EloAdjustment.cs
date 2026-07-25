namespace BotArena.App.Matches;

/// <summary>
/// The rating move for one completed ranked set, as a pure function so it can be
/// reasoned about and tested without a database or a match.
/// </summary>
public static class EloAdjustment
{
    /// <summary>No rating may fall below this. Bots start at 1200, so the floor is far
    /// enough down that no honest bot reaches it by losing, and it exists to bound the
    /// farming case: ratings are zero-sum, so inflating one bot means draining another,
    /// and a drained bot eventually has nothing left to give (DECISIONS #94).</summary>
    public const double Floor = 100;

    /// <summary>
    /// How much bot A's rating changes; bot B changes by exactly the negative of it.
    ///
    /// Elo's expectation term is what actually makes farming pointless: sweeping a
    /// sacrificial bot pays about 7.5 rating on the tenth set, 0.9 on the hundredth and
    /// 0.18 on the five-hundredth, so the grind decays faster than anyone can grind. The
    /// floor is the hard bound underneath — ratings cannot run to absurd or negative
    /// values, and a bot at the bottom is worth exactly nothing to beat.
    ///
    /// It must be applied to the PAIR, not to each side separately. Clamping only the
    /// loser would let the winner take points the loser never lost, minting rating out of
    /// nothing and turning a floored bot into an infinite supply — the opposite of the
    /// intent. Capping the transfer at what the loser can afford keeps every set zero-sum.
    /// </summary>
    /// <param name="ratingA">Bot A's current rating on this ladder.</param>
    /// <param name="ratingB">Bot B's current rating on this ladder.</param>
    /// <param name="scoreA">Games A won, counting a draw as a half.</param>
    /// <param name="games">Games in the set.</param>
    /// <param name="k">Elo K-factor.</param>
    public static double ForBotA(double ratingA, double ratingB, double scoreA, int games, double k)
    {
        double expectedA = 1.0 / (1.0 + Math.Pow(10, (ratingB - ratingA) / 400.0));
        double desired = k * (scoreA / games - expectedA);
        return desired >= 0
            ? Math.Min(desired, Math.Max(0, ratingB - Floor))   // A can only win what B can lose
            : Math.Max(desired, Math.Min(0, Floor - ratingA));  // A can only lose down to the floor
    }
}
