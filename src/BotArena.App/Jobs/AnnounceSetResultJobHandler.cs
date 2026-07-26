using BotArena.App.Matches;
using BotArena.App.Notifications;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Jobs;

/// <summary>
/// Tells both players how a ranked set went, once every game in it has finished
/// broadcasting.
/// <para>
/// Both players, unlike an unranked match: rating moved for each of them, so each has
/// news (DECISIONS #119). And once for the set rather than once per game — six rows would
/// bury the inbox and reveal the set's shape as it played out.
/// </para>
/// </summary>
public sealed class AnnounceSetResultJobHandler(
    AppDbContext db,
    UserNotificationWriter notifications,
    TimeProvider timeProvider)
{
    public async Task<JobExecutionResult> HandleAsync(
        Guid matchSetId,
        CancellationToken cancellationToken)
    {
        MatchSet? set = await db.MatchSets
            .SingleOrDefaultAsync(candidate => candidate.Id == matchSetId, cancellationToken);

        // A failed set has no score to report, and an unfinished one has nothing yet.
        if (set is null || set.Status != MatchSetStatus.Completed)
            return new JobExecutionResult("nothing_to_announce");

        List<Match> games = await db.Matches
            .Where(match => match.MatchSetId == matchSetId)
            .ToListAsync(cancellationToken);

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        // The set's boundary is its *last* game's. Announcing before every game is public
        // would reveal the score while viewers are still watching one of them decide it.
        if (games.Any(game => !game.BroadcastComplete(now)))
        {
            throw new InvalidOperationException(
                $"Ranked set {matchSetId} still has a game broadcasting; its score cannot " +
                "be announced yet.");
        }

        var bots = await db.Bots
            .Where(bot => bot.Id == set.BotAId || bot.Id == set.BotBId)
            .Select(bot => new { bot.Id, bot.Name, bot.LookId, bot.Accent, bot.OwnerUserId })
            .ToListAsync(cancellationToken);

        foreach (var bot in bots)
        {
            bool isA = bot.Id == set.BotAId;
            var opponent = bots.FirstOrDefault(other => other.Id != bot.Id);
            double score = isA ? set.ScoreA : set.ScoreB;
            double opponentScore = isA ? set.ScoreB : set.ScoreA;

            // A set has no challenge to supersede — its opponent is matchmade, so nobody
            // was told it was coming — but it takes the same subject-keyed form so both
            // paths dedupe the same way.
            await notifications.WriteAsync(
                bot.OwnerUserId,
                UserNotificationKinds.SetSettled,
                UserNotificationKeys.SetSubject(set.Id, bot.Id),
                new SetSettledPayload(
                    set.Id,
                    bot.Id,
                    bot.Name,
                    bot.LookId,
                    bot.Accent,
                    set.WinnerBotId is null
                        ? "Draw"
                        : set.WinnerBotId == bot.Id
                            ? "Win"
                            : "Loss",
                    score,
                    opponentScore,
                    // This bot's own delta, already signed for whoever is being told.
                    isA ? set.RatingChangeA : set.RatingChangeB,
                    opponent?.Name ?? "a removed bot"),
                now,
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new JobExecutionResult("announced");
    }
}
