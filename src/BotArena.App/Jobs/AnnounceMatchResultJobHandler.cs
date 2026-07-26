using BotArena.App.Matches;
using BotArena.App.Notifications;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Jobs;

/// <summary>
/// Tells the players of an unranked match how it went, once it has finished broadcasting.
/// <para>
/// Scheduled rather than swept: the broadcast's end is known exactly when the match
/// completes, so <see cref="BackgroundJob.AvailableAt"/> carries the announcement to that
/// instant. Nothing polls for elapsed broadcasts.
/// </para>
/// <para>
/// It still re-checks the boundary on arrival. A job can be claimed early — a clock skew
/// between the worker and PostgreSQL, or an operator replaying a job — and announcing a
/// result while the replay is still playing out is the exact failure broadcast secrecy
/// exists to prevent (DECISIONS #118). Too early is rescheduled, not published.
/// </para>
/// </summary>
public sealed class AnnounceMatchResultJobHandler(
    AppDbContext db,
    UserNotificationWriter notifications,
    TimeProvider timeProvider)
{
    public async Task<JobExecutionResult> HandleAsync(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        Match? match = await db.Matches
            .Include(candidate => candidate.Participants)
            .SingleOrDefaultAsync(candidate => candidate.Id == matchId, cancellationToken);

        // A match that no longer exists, or never ran, has nothing to announce. Neither is
        // an error worth retrying.
        if (match is null || match.Status != MatchStatus.Completed)
            return new JobExecutionResult("nothing_to_announce");

        // A set announces once as a whole; its games stay silent.
        if (match.MatchSetId is not null)
            return new JobExecutionResult("set_game_skipped");

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        if (!match.BroadcastComplete(now))
        {
            // Throwing is how a handler asks for a retry — the worker owns the backoff.
            // Returning success here would drop the announcement entirely, which is worse
            // than a late one.
            throw new InvalidOperationException(
                $"Match {matchId} is still broadcasting; its result cannot be announced " +
                $"until {BroadcastSchedule.CompletesAt(match):O}.");
        }

        // Owner ids are read now rather than snapshotted at match time: a notification is
        // addressed to whoever owns the bot when it is delivered, and a transferred bot's
        // old owner has no claim on the result.
        Dictionary<Guid, Guid> ownerByBot = await db.Bots
            .Where(bot => match.Participants.Select(p => p.BotId).Contains(bot.Id))
            .Select(bot => new { bot.Id, bot.OwnerUserId })
            .ToDictionaryAsync(bot => bot.Id, bot => bot.OwnerUserId, cancellationToken);

        foreach (MatchParticipant participant in match.Participants)
        {
            if (!ownerByBot.TryGetValue(participant.BotId, out Guid ownerId))
                continue;

            // Only the challenged is told (DECISIONS #119). Whoever asked for the match
            // already knows it happened, and an app that echoes your own actions is one
            // you learn to ignore.
            if (match.InitiatedByUserId == ownerId)
                continue;

            MatchParticipant? opponent = match.Participants
                .FirstOrDefault(other => other.Slot != participant.Slot);

            // Supersede, not write: this bot may already have a "challenged — watch" row
            // for the same match, and by now that invitation is a lie. The subject key
            // makes it the same row, so the outcome replaces the invitation in place and
            // an identical retry still changes nothing (DECISIONS #118).
            await notifications.SupersedeAsync(
                ownerId,
                UserNotificationKinds.MatchSettled,
                UserNotificationKeys.MatchSubject(match.Id, participant.BotId),
                new MatchSettledPayload(
                    match.Id,
                    match.MapId,
                    participant.BotId,
                    participant.NameSnapshot,
                    // Snapshots, so a bot restyled since still appears as it fought.
                    participant.LookIdSnapshot,
                    participant.AccentSnapshot,
                    participant.Outcome ?? "Draw",
                    opponent?.NameSnapshot ?? "a removed bot"),
                now,
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new JobExecutionResult("announced");
    }
}
