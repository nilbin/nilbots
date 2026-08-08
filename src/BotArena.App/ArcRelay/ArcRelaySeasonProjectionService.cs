using BotArena.App.Competition;
using BotArena.App.Matches;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.ArcRelay;

/// <summary>
/// Public, reveal-safe read model for the current season. Rating settlement is
/// the publication boundary, so no pending or merely simulated match enters a
/// trajectory, record, recent result, or derived rank.
/// </summary>
public sealed class ArcRelaySeasonProjectionService(
    AppDbContext db,
    ArcRelayEntrantProjector entrants,
    ArcRelaySeasonLifecycleService lifecycle)
{
    public async Task<ArcRelaySeasonResponse> CurrentAsync(
        Guid? viewerId,
        CancellationToken cancellationToken)
    {
        (Season season, Ladder ladder) = await lifecycle.CurrentAsync(
            cancellationToken);
        ArcRelayEntrantRating[] ratings = await db.ArcRelayEntrantRatings
            .AsNoTracking()
            .Where(value => value.LadderId == ladder.Id)
            .OrderByDescending(value => value.Rating)
            .ThenBy(value => value.EntrantId)
            .ToArrayAsync(cancellationToken);
        Dictionary<Guid, ArcRelaySeasonEntrant> opening = await db
            .ArcRelaySeasonEntrants.AsNoTracking()
            .Where(value => value.SeasonId == season.Id)
            .ToDictionaryAsync(value => value.EntrantId, cancellationToken);
        ArcRelayRankedMatch[] settled = await db.ArcRelayRankedMatches
            .AsNoTracking()
            .Where(value => value.LadderId == ladder.Id &&
                value.SettledAt != null)
            .OrderBy(value => value.SettledAt)
            .ThenBy(value => value.MatchId)
            .ToArrayAsync(cancellationToken);
        Guid[] matchIds = settled.Select(value => value.MatchId).ToArray();
        MatchTeamResult[] results = await db.MatchTeamResults.AsNoTracking()
            .Where(value => matchIds.Contains(value.MatchId))
            .ToArrayAsync(cancellationToken);
        Dictionary<(Guid MatchId, int TeamId), MatchTeamResult> resultByTeam =
            results.ToDictionary(value => (value.MatchId, value.TeamId));
        Dictionary<Guid, ArcRelayEntrant> entrantById = await db
            .ArcRelayEntrants.AsNoTracking()
            .Where(value => ratings.Select(rating => rating.EntrantId)
                .Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        IReadOnlyList<ArcRelayEntrantCardResponse> cards = await entrants
            .ProjectManyAsync(
                ratings.Select(value => entrantById[value.EntrantId]).ToArray(),
                viewerId, ladder.Id, cancellationToken);
        Dictionary<Guid, ArcRelayEntrantCardResponse> cardById = cards
            .ToDictionary(value => value.Id);

        var standings = new List<ArcRelaySeasonStandingResponse>(ratings.Length);
        int row = 0;
        int rank = 0;
        double? previousRating = null;
        foreach (ArcRelayEntrantRating rating in ratings)
        {
            row++;
            if (previousRating is null || rating.Rating != previousRating.Value)
                rank = row;
            previousRating = rating.Rating;
            ArcRelayEntrant entrant = entrantById[rating.EntrantId];
            opening.TryGetValue(entrant.Id, out ArcRelaySeasonEntrant? snapshot);
            ArcRelayRankedMatch[] entrantMatches = settled.Where(value =>
                value.EntrantAId == entrant.Id ||
                value.EntrantBId == entrant.Id).ToArray();
            int wins = 0;
            int draws = 0;
            int losses = 0;
            var history = new List<ArcRelaySeasonRatingPointResponse>();
            if (snapshot is not null)
            {
                history.Add(new ArcRelaySeasonRatingPointResponse(
                    null, snapshot.EnteredAt,
                    Math.Round(snapshot.OpeningRating)));
            }
            foreach (ArcRelayRankedMatch match in entrantMatches)
            {
                bool isA = match.EntrantAId == entrant.Id;
                MatchTeamResult mine = resultByTeam[(match.MatchId, isA ? 0 : 1)];
                MatchTeamResult theirs = resultByTeam[(match.MatchId, isA ? 1 : 0)];
                if (mine.Placement == theirs.Placement) draws++;
                else if (mine.Placement < theirs.Placement) wins++;
                else losses++;
                double before = isA ? match.RatingABefore : match.RatingBBefore;
                double change = isA
                    ? match.RatingChangeA!.Value
                    : match.RatingChangeB!.Value;
                history.Add(new ArcRelaySeasonRatingPointResponse(
                    match.MatchId,
                    match.SettledAt!.Value,
                    Math.Round(before + change)));
            }
            int? movement = snapshot?.OpeningRank is int openingRank
                ? openingRank - rank
                : null;
            standings.Add(new ArcRelaySeasonStandingResponse(
                rank,
                snapshot?.OpeningRank,
                movement,
                wins,
                draws,
                losses,
                history,
                cardById[entrant.Id]));
        }

        Dictionary<Guid, string> names = entrantById.ToDictionary(
            value => value.Key, value => value.Value.Name);
        ArcRelaySeasonMatchResponse[] recent = settled
            .OrderByDescending(value => value.SettledAt)
            .ThenByDescending(value => value.MatchId)
            .Take(8)
            .Select(value => new ArcRelaySeasonMatchResponse(
                value.MatchId,
                value.SettledAt!.Value,
                value.EntrantAId,
                names[value.EntrantAId],
                resultByTeam[(value.MatchId, 0)].Placement,
                value.EntrantBId,
                names[value.EntrantBId],
                resultByTeam[(value.MatchId, 1)].Placement))
            .ToArray();

        return new ArcRelaySeasonResponse(
            season.Id,
            season.Key,
            season.Number ?? ArcRelayLadderPolicy.SeasonNumber,
            season.DisplayName,
            season.Status.ToString().ToLowerInvariant(),
            season.StartsAt!.Value,
            season.EndsAt!.Value,
            season.OpenedAt,
            season.ClosedAt,
            ladder.Id,
            ArcRelayLadderPolicy.LadderName,
            ArcRelayLadderPolicy.MaximumOptedInPerAccount,
            standings,
            recent);
    }
}
