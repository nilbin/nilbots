using BotArena.App.Competition;
using BotArena.App.Jobs;
using BotArena.App.Matches;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.ArcRelay;

public sealed class ArcRelayRatingSettlementJobHandler(
    AppDbContext db,
    TimeProvider timeProvider)
{
    public async Task<JobExecutionResult> HandleAsync(Guid matchId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        ArcRelayRankedMatch? ranked = await db.ArcRelayRankedMatches.SingleOrDefaultAsync(
            value => value.MatchId == matchId, cancellationToken);
        if (ranked is null) return new JobExecutionResult("not_ranked");
        if (ranked.SettledAt is not null) return new JobExecutionResult("already_settled");
        Match match = await db.Matches.Include(value => value.TeamResults)
            .SingleAsync(value => value.Id == matchId, cancellationToken);
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        if (!match.BroadcastComplete(now))
            throw new InvalidOperationException("Arc Relay rating cannot settle before the causal broadcast completes.");
        ArcRelayEntrantRating[] ratings = await db.ArcRelayEntrantRatings.Where(value =>
            value.LadderId == ranked.LadderId &&
            (value.EntrantId == ranked.EntrantAId || value.EntrantId == ranked.EntrantBId))
            .ToArrayAsync(cancellationToken);
        if (ratings.Length != 2) throw new InvalidOperationException("Ranked match has no complete rating population.");
        ArcRelayEntrantRating a = ratings.Single(value => value.EntrantId == ranked.EntrantAId);
        ArcRelayEntrantRating b = ratings.Single(value => value.EntrantId == ranked.EntrantBId);
        MatchTeamResult resultA = match.TeamResults.Single(value => value.TeamId == 0);
        MatchTeamResult resultB = match.TeamResults.Single(value => value.TeamId == 1);
        double pointsA = resultA.Placement == resultB.Placement ? .5 : resultA.Placement < resultB.Placement ? 1 : 0;
        var policy = new ArcRelayEloV1();
        IReadOnlyList<RatingUpdate> updates = policy.Calculate(new RatingPolicyInput(
            LadderId.From(ranked.LadderId),
            [new RatingEntrant(a.EntrantId, 0, a.Rating), new RatingEntrant(b.EntrantId, 1, b.Rating)],
            [new TeamSeriesResult(0, resultA.Placement, pointsA), new TeamSeriesResult(1, resultB.Placement, 1 - pointsA)]));
        RatingUpdate updateA = updates.Single(value => value.EntrantId == a.EntrantId);
        RatingUpdate updateB = updates.Single(value => value.EntrantId == b.EntrantId);
        a.Rating = updateA.RatingAfter; a.RankedMatches++;
        b.Rating = updateB.RatingAfter; b.RankedMatches++;
        ranked.RatingChangeA = updateA.RatingChange;
        ranked.RatingChangeB = updateB.RatingChange;
        ranked.SettledAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new JobExecutionResult("settled");
    }
}
