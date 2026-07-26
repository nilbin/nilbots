using BotArena.App.Matches;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Notifications;

/// <summary>
/// Tells a player that someone has challenged one of their bots.
/// <para>
/// Written when the match is created, not when it starts: the value of this notification is
/// the chance to watch, and a fight queued behind a compile can be over by the time a
/// job-scheduled announcement would arrive.
/// </para>
/// <para>
/// Only the challenged is told (DECISIONS #119), and only for unranked challenges — a
/// ranked set matchmakes an opponent by rating, so nobody chose to fight anyone and there
/// is no "someone did this to you" to report.
/// </para>
/// </summary>
public sealed class MatchChallengeAnnouncer(
    AppDbContext db,
    UserNotificationWriter notifications,
    TimeProvider timeProvider)
{
    public async Task AnnounceAsync(Match match, CancellationToken cancellationToken)
    {
        if (match.MatchSetId is not null || match.InitiatedByUserId is not Guid challengerId)
            return;

        // Owners are read live rather than snapshotted: a notification is addressed to
        // whoever owns the bot now, matching how the result announcement resolves it.
        Dictionary<Guid, Guid> ownerByBot = await db.Bots
            .Where(bot => match.Participants.Select(p => p.BotId).Contains(bot.Id))
            .Select(bot => new { bot.Id, bot.OwnerUserId })
            .ToDictionaryAsync(bot => bot.Id, bot => bot.OwnerUserId, cancellationToken);

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (MatchParticipant participant in match.Participants)
        {
            if (!ownerByBot.TryGetValue(participant.BotId, out Guid ownerId))
                continue;
            if (ownerId == challengerId)
                continue;

            MatchParticipant? challenger = match.Participants
                .FirstOrDefault(other => other.Slot != participant.Slot);

            await notifications.WriteAsync(
                ownerId,
                UserNotificationKinds.MatchChallenged,
                // The subject key, so the result supersedes this row rather than sitting
                // beneath it as a "watch this" for a fight that already ended.
                UserNotificationKeys.MatchSubject(match.Id, participant.BotId),
                new MatchChallengedPayload(
                    match.Id,
                    match.MapId,
                    participant.BotId,
                    participant.NameSnapshot,
                    participant.LookIdSnapshot,
                    participant.AccentSnapshot,
                    challenger?.NameSnapshot ?? "a removed bot"),
                now,
                cancellationToken);
        }
    }
}
