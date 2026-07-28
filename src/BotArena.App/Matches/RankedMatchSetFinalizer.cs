using System.Data;
using System.Diagnostics;
using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Cosmetics;
using BotArena.App.Shared;
using BotArena.App.Jobs;
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
    LegacyCompetitionIdentityResolver identityResolver,
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

            string[] ladders = games
                .Select(match => match.GameRulesVersion)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (ladders.Length != 1 || string.IsNullOrWhiteSpace(ladders[0]))
            {
                throw new InvalidOperationException(
                    $"Ranked set {matchSetId} did not execute entirely on one rules ladder.");
            }
            string rulesVersion = ladders[0];
            LegacyCompetitionIdentity identity =
                await identityResolver.ResolveExistingAsync(
                    rulesVersion,
                    cancellationToken);
            RepairIdentity(set, games, identity);
            // Executed games are the legacy authority for this mirror. New work
            // is pinned at admission, while this retains the historical finalizer
            // behavior for rows queued before exact identity existed.
            set.GameRulesVersion = rulesVersion;

            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            if (games.Any(match => match.Status == MatchStatus.Failed))
            {
                set.Status = MatchSetStatus.Failed;
                set.CompletedAt = now;
                await db.SaveChangesAsync(cancellationToken);
                return await FinishAsync(RankedSetFinalizationOutcome.FailedSet);
            }

            await LockBotsAsync(set.BotAId, set.BotBId, cancellationToken);

            double scoreA = ScoreForBotA(set, games);
            set.ScoreA = scoreA;
            set.ScoreB = MatchSet.Games - scoreA;

            BotRating ratingA = await GetOrCreateRatingAsync(
                set.BotAId,
                rulesVersion,
                identity,
                cancellationToken);
            BotRating ratingB = await GetOrCreateRatingAsync(
                set.BotBId,
                rulesVersion,
                identity,
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

            // Announce when the last of the six games stops broadcasting. The finalizer
            // runs as soon as every game has *executed*, which is earlier — replays are
            // still playing out — and it is the only place that can see all six broadcast
            // ends at once. A single game's worker knows only its own.
            if (games
                    .Select(BroadcastSchedule.AnnounceAt)
                    .Max() is DateTime announceAt)
            {
                db.BackgroundJobs.Add(
                    BackgroundJob.AnnounceSetResult(set.Id, announceAt));
            }

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
        LegacyCompetitionIdentity identity,
        CancellationToken cancellationToken)
    {
        BotRating? rating = await db.BotRatings.SingleOrDefaultAsync(
            candidate =>
                candidate.BotId == botId &&
                candidate.RulesVersion == rulesVersion,
            cancellationToken);
        if (rating is not null)
        {
            RepairIdentity(
                rating.LadderId,
                identity.LadderId,
                value => rating.LadderId = value,
                nameof(BotRating),
                rating.Id,
                nameof(BotRating.LadderId),
                rulesVersion);
            return rating;
        }

        rating = new BotRating
        {
            BotId = botId,
            RulesVersion = rulesVersion,
            LadderId = identity.LadderId,
        };
        db.BotRatings.Add(rating);
        return rating;
    }

    private static void RepairIdentity(
        MatchSet set,
        IReadOnlyList<Match> games,
        LegacyCompetitionIdentity identity)
    {
        RepairIdentity(
            set.PlaylistVersionId,
            identity.PlaylistVersionId,
            value => set.PlaylistVersionId = value,
            nameof(MatchSet),
            set.Id,
            nameof(MatchSet.PlaylistVersionId),
            identity.RulesVersion);
        RepairIdentity(
            set.LadderId,
            identity.LadderId,
            value => set.LadderId = value,
            nameof(MatchSet),
            set.Id,
            nameof(MatchSet.LadderId),
            identity.RulesVersion);
        foreach (Match game in games)
        {
            RepairIdentity(
                game.PlaylistVersionId,
                identity.PlaylistVersionId,
                value => game.PlaylistVersionId = value,
                nameof(Match),
                game.Id,
                nameof(Match.PlaylistVersionId),
                identity.RulesVersion);
        }
    }

    private static void RepairIdentity(
        Guid? actual,
        Guid expected,
        Action<Guid> fill,
        string rowKind,
        Guid rowId,
        string field,
        string rulesVersion)
    {
        if (actual is null)
        {
            fill(expected);
            return;
        }
        if (actual.Value != expected)
        {
            throw new InvalidOperationException(
                $"Competition identity contradiction for {rowKind} {rowId} " +
                $"on rules version '{rulesVersion}': {field} is " +
                $"{actual.Value:D}, expected {expected:D}.");
        }
    }

    private static double ScoreForBotA(MatchSet set, IReadOnlyList<Match> games)
    {
        double score = 0;
        foreach (Match game in games)
        {
            if (game.WinnerSlot is not int winner)
            {
                score += DuelMirrored6V1.DrawSeriesPoints;
                continue;
            }

            MatchParticipant participant = game.Participants.Single(
                candidate => candidate.Slot == winner);
            if (participant.BotId == set.BotAId)
                score += DuelMirrored6V1.WinSeriesPoints;
            else if (participant.BotId != set.BotBId)
            {
                throw new InvalidOperationException(
                    $"Ranked game {game.Id} winner is not a set participant.");
            }
        }
        return score;
    }
}
