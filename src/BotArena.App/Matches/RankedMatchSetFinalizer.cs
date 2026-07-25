using System.Data;
using System.Diagnostics;
using BotArena.App.Bots;
using BotArena.App.Cosmetics;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BotArena.App.Matches;

/// <summary>
/// Completes one ranked set as a single transactional unit. The set lock makes
/// retries and concurrent last-game workers observe one completion; the bot
/// locks serialize different sets that would otherwise update the same ladder
/// rating concurrently.
/// </summary>
public sealed class RankedMatchSetFinalizer(
    AppDbContext db,
    CosmeticAchievementService achievements,
    TimeProvider timeProvider)
{
    public async Task<RankedSetFinalizationOutcome> TryFinalizeAsync(
        Guid matchSetId,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity =
            ApplicationTelemetry.ActivitySource.StartActivity("matches.finalize_ranked_set");
        activity?.SetTag("match_set.id", matchSetId);

        IDbContextTransaction? ownedTransaction = null;
        try
        {
            if (db.Database.CurrentTransaction is null)
            {
                ownedTransaction = await db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);
            }

            MatchSet? set = (await db.MatchSets
                    .FromSqlInterpolated($"""
                        SELECT *
                        FROM "MatchSets"
                        WHERE "Id" = {matchSetId}
                        FOR UPDATE
                        """)
                    .AsTracking()
                    .ToListAsync(cancellationToken))
                .SingleOrDefault();
            if (set is null)
                return await FinishAsync(RankedSetFinalizationOutcome.NotFound);
            if (set.Status != MatchSetStatus.Running)
                return await FinishAsync(RankedSetFinalizationOutcome.AlreadyTerminal);

            List<Match> games = await db.Matches
                .Include(match => match.Participants)
                .Where(match => match.MatchSetId == matchSetId)
                .ToListAsync(cancellationToken);
            if (games.Count < MatchSet.Games ||
                games.Any(match =>
                    match.Status is MatchStatus.Pending or MatchStatus.Running))
            {
                return await FinishAsync(RankedSetFinalizationOutcome.NotReady);
            }
            if (games.Count != MatchSet.Games)
            {
                throw new InvalidOperationException(
                    $"Ranked set {matchSetId} contains {games.Count} games; expected {MatchSet.Games}.");
            }

            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            if (games.Any(match => match.Status == MatchStatus.Failed))
            {
                set.Status = MatchSetStatus.Failed;
                set.CompletedAt = now;
                await db.SaveChangesAsync(cancellationToken);
                return await FinishAsync(RankedSetFinalizationOutcome.FailedSet);
            }

            string[] ladders = games
                .Select(match => match.GameRulesVersion)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (ladders.Length != 1 || string.IsNullOrWhiteSpace(ladders[0]))
            {
                throw new InvalidOperationException(
                    $"Ranked set {matchSetId} did not execute entirely on one rules ladder.");
            }

            await LockBotsAsync(set.BotAId, set.BotBId, cancellationToken);

            double scoreA = ScoreForBotA(set, games);
            set.ScoreA = scoreA;
            set.ScoreB = MatchSet.Games - scoreA;
            set.GameRulesVersion = ladders[0];

            BotRating ratingA = await GetOrCreateRatingAsync(
                set.BotAId,
                ladders[0],
                cancellationToken);
            BotRating ratingB = await GetOrCreateRatingAsync(
                set.BotBId,
                ladders[0],
                cancellationToken);
            set.RatingABefore = ratingA.Rating;
            set.RatingBBefore = ratingB.Rating;
            double change = EloAdjustment.ForBotA(
                ratingA.Rating,
                ratingB.Rating,
                scoreA,
                MatchSet.Games,
                MatchSet.EloK);
            set.RatingChangeA = change;
            set.RatingChangeB = -change;
            ratingA.Rating += change;
            ratingB.Rating -= change;
            ratingA.RankedSets++;
            ratingB.RankedSets++;
            set.WinnerBotId =
                scoreA > set.ScoreB
                    ? set.BotAId
                    : scoreA < set.ScoreB
                        ? set.BotBId
                        : null;
            set.Status = MatchSetStatus.Completed;
            set.CompletedAt = now;

            // Flush the rating and set transition before evaluating milestones so
            // their queries see this set and its new rating. Both writes, ledger
            // grants, notification, and pg_notify remain inside this transaction.
            await db.SaveChangesAsync(cancellationToken);
            await achievements.AwardForCompletedRankedSetAsync(
                matchSetId,
                cancellationToken);
            return await FinishAsync(RankedSetFinalizationOutcome.Finalized);
        }
        catch
        {
            RankedFinalizationTelemetry.RecordException();
            if (ownedTransaction is not null)
                await ownedTransaction.RollbackAsync(CancellationToken.None);
            activity?.SetTag("application.outcome", "exception");
            throw;
        }
        finally
        {
            if (ownedTransaction is not null)
                await ownedTransaction.DisposeAsync();
        }

        async Task<RankedSetFinalizationOutcome> FinishAsync(
            RankedSetFinalizationOutcome outcome)
        {
            if (ownedTransaction is not null)
                await ownedTransaction.CommitAsync(cancellationToken);
            RankedFinalizationTelemetry.Record(outcome);
            activity?.SetTag(
                "application.outcome",
                outcome.ToString().ToLowerInvariant());
            return outcome;
        }
    }

    private async Task LockBotsAsync(
        Guid botAId,
        Guid botBId,
        CancellationToken cancellationToken)
    {
        if (botAId == botBId)
            throw new InvalidOperationException("A ranked set cannot contain the same bot twice.");
        Guid first = botAId.CompareTo(botBId) < 0 ? botAId : botBId;
        Guid second = first == botAId ? botBId : botAId;
        int locked = (await db.Bots
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM "Bots"
                    WHERE "Id" = {first} OR "Id" = {second}
                    ORDER BY "Id"
                    FOR UPDATE
                    """)
                .AsTracking()
                .ToListAsync(cancellationToken))
            .Count;
        if (locked != 2)
        {
            throw new InvalidOperationException(
                $"Ranked set references {locked} existing bots; expected 2.");
        }
    }

    private async Task<BotRating> GetOrCreateRatingAsync(
        Guid botId,
        string rulesVersion,
        CancellationToken cancellationToken)
    {
        BotRating? rating = await db.BotRatings.SingleOrDefaultAsync(
            candidate =>
                candidate.BotId == botId &&
                candidate.RulesVersion == rulesVersion,
            cancellationToken);
        if (rating is not null)
            return rating;

        rating = new BotRating
        {
            BotId = botId,
            RulesVersion = rulesVersion,
        };
        db.BotRatings.Add(rating);
        return rating;
    }

    private static double ScoreForBotA(MatchSet set, IReadOnlyList<Match> games)
    {
        double score = 0;
        foreach (Match game in games)
        {
            if (game.WinnerSlot is not int winner)
            {
                score += 0.5;
                continue;
            }

            MatchParticipant participant = game.Participants.Single(
                candidate => candidate.Slot == winner);
            if (participant.BotId == set.BotAId)
                score++;
            else if (participant.BotId != set.BotBId)
            {
                throw new InvalidOperationException(
                    $"Ranked game {game.Id} winner is not a set participant.");
            }
        }
        return score;
    }
}
