using BotArena.App.Bots;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Matches;

public sealed record LadderStanding(
    Guid BotId,
    string RulesVersion,
    double Rating,
    int RankedSets,
    int Rank);

/// <summary>
/// The authoritative ranked-ladder rules. A bot enters the public ladder after
/// completing a set, and rank uses competition ranking: equal ratings share a rank.
/// </summary>
public static class LadderStandings
{
    public static IQueryable<BotRating> RankedForRules(
        this IQueryable<BotRating> ratings,
        string rulesVersion) => ratings.Where(rating =>
            rating.RulesVersion == rulesVersion &&
            rating.RankedSets > 0);

    public static async Task<LadderStanding?> ForBotAsync(
        this IQueryable<BotRating> ratings,
        string rulesVersion,
        Guid botId,
        CancellationToken cancellationToken = default)
    {
        IQueryable<BotRating> ranked = ratings.RankedForRules(rulesVersion);
        var own = await ranked
            .Where(rating => rating.BotId == botId)
            .Select(rating => new
            {
                rating.BotId,
                rating.RulesVersion,
                rating.Rating,
                rating.RankedSets,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (own is null)
            return null;

        int rank = 1 + await ranked.CountAsync(
            other => other.Rating > own.Rating,
            cancellationToken);
        return new LadderStanding(
            own.BotId,
            own.RulesVersion,
            Math.Round(own.Rating),
            own.RankedSets,
            rank);
    }

    public static int CompetitionRank(
        IEnumerable<double> ladderRatings,
        double rating) => 1 + ladderRatings.Count(other => other > rating);
}
