using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Notifications;

/// <summary>
/// Writes one durable notification and wakes the realtime channel.
/// <para>
/// The insert is <c>ON CONFLICT DO NOTHING</c> against the dedupe key, which is what makes
/// a retried job silent rather than duplicating an announcement, and the
/// <c>pg_notify</c> only fires when the insert actually happened. PostgreSQL delivers that
/// notification when the surrounding transaction commits, so every web process learns
/// about a row that is definitely visible — see DECISIONS #108.
/// </para>
/// <para>
/// Raw SQL rather than EF for the same reason the entitlement path uses it: EF has no way
/// to express <c>ON CONFLICT DO NOTHING</c>, and the alternative — read, check, insert — is
/// a race between concurrent workers, which is exactly what the dedupe key exists to
/// close.
/// </para>
/// </summary>
public sealed class UserNotificationWriter(AppDbContext db)
{
    /// <returns><c>true</c> when this call created the row; <c>false</c> when it already existed.</returns>
    public async Task<bool> WriteAsync<TPayload>(
        Guid userId,
        string kind,
        string dedupeKey,
        TPayload payload,
        DateTime createdAt,
        CancellationToken cancellationToken)
        where TPayload : UserNotificationPayload
    {
        Guid notificationId = Guid.NewGuid();
        string payloadJson = UserNotificationContracts.Serialize(payload);

        int inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "UserNotifications"
                ("Id", "UserId", "Kind", "DedupeKey", "PayloadJson",
                 "CreatedAt", "ReadAt")
            VALUES
                ({notificationId}, {userId}, {kind},
                 {dedupeKey}, CAST({payloadJson} AS jsonb), {createdAt}, NULL)
            ON CONFLICT ("UserId", "DedupeKey")
            DO NOTHING
            """, cancellationToken);

        if (inserted != 1)
            return false;

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT pg_notify(
                {PostgresNotificationListener.Channel},
                {notificationId.ToString()})
            """, cancellationToken);
        return true;
    }
}
