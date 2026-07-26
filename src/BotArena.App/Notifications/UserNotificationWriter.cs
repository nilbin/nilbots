using BotArena.App.Jobs;
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

        await AnnounceAsync(notificationId, createdAt, cancellationToken);
        return true;
    }

    /// <summary>
    /// Writes a notification, replacing whatever the same subject said before.
    /// <para>
    /// This is how a challenge becomes its own result. "Pincer challenged hunter — watch"
    /// is a lie by the time the fight is over, so the row is rewritten in place rather than
    /// a second one appended: the inbox never carries a stale invitation beside its own
    /// outcome, and the dedupe key stays the natural subject id (DECISIONS #118).
    /// </para>
    /// <para>
    /// <c>ReadAt</c> is cleared, because an outcome is genuinely new information — someone
    /// who read the challenge has not read the result.
    /// </para>
    /// <para>
    /// The <c>WHERE</c> guard is what keeps retries silent. Without it every replay of a
    /// job would touch the row, clear <c>ReadAt</c> and re-announce, so a notification the
    /// player had already dismissed would come back. An identical payload updates nothing,
    /// reports nothing, and fires no <c>pg_notify</c> — the same property
    /// <see cref="WriteAsync{TPayload}"/> gets from <c>DO NOTHING</c>.
    /// </para>
    /// </summary>
    /// <returns><c>true</c> when the row was created or genuinely changed.</returns>
    public async Task<bool> SupersedeAsync<TPayload>(
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

        // RETURNING gives the surviving row's id — the pre-existing one on an update, the
        // new one on an insert — which is what the realtime channel has to carry. A guarded
        // conflict returns no row at all, which is the "nothing changed" signal.
        List<Guid> announced = await db.Database.SqlQuery<Guid>($"""
            INSERT INTO "UserNotifications"
                ("Id", "UserId", "Kind", "DedupeKey", "PayloadJson",
                 "CreatedAt", "ReadAt")
            VALUES
                ({notificationId}, {userId}, {kind},
                 {dedupeKey}, CAST({payloadJson} AS jsonb), {createdAt}, NULL)
            ON CONFLICT ("UserId", "DedupeKey")
            DO UPDATE SET
                "Kind" = EXCLUDED."Kind",
                "PayloadJson" = EXCLUDED."PayloadJson",
                "CreatedAt" = EXCLUDED."CreatedAt",
                "ReadAt" = NULL
            WHERE "UserNotifications"."Kind" IS DISTINCT FROM EXCLUDED."Kind"
               OR "UserNotifications"."PayloadJson" IS DISTINCT FROM EXCLUDED."PayloadJson"
            RETURNING "Id"
            """).ToListAsync(cancellationToken);

        if (announced.Count == 0)
            return false;

        await AnnounceAsync(announced[0], createdAt, cancellationToken);
        return true;
    }

    /// <summary>
    /// Wake every delivery channel for a row that definitely exists.
    /// <para>
    /// Realtime first, then the push job. PostgreSQL delivers a <c>pg_notify</c> when the
    /// surrounding transaction commits, so web processes only learn about rows that are
    /// visible (DECISIONS #108).
    /// </para>
    /// </summary>
    private async Task AnnounceAsync(
        Guid notificationId,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT pg_notify(
                {PostgresNotificationListener.Channel},
                {notificationId.ToString()})
            """, cancellationToken);

        // Enqueued with raw SQL rather than db.BackgroundJobs.Add, because everything else
        // in this class writes immediately and callers do not all reach a SaveChanges
        // afterwards — the challenge endpoint announces *after* saving, on purpose. A
        // tracked entity added here would silently never be inserted for that path.
        string payload = $$"""{"notificationId":"{{notificationId}}"}""";
        DateTime availableAt = createdAt.Add(PushDelay);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "BackgroundJobs"
                ("Type", "PayloadJson", "Status", "Attempts", "AvailableAt", "CreatedAt")
            VALUES
                ({BackgroundJob.DeliverPushType}, CAST({payload} AS jsonb),
                 {nameof(JobStatus.Pending)}, 0, {availableAt}, {createdAt})
            """, cancellationToken);
    }

    /// <summary>
    /// How long a push waits for the in-app channel to make it unnecessary.
    /// <para>
    /// Someone with the app open gets this over SignalR within a second and reads it; the
    /// delay is what lets that count as delivered, so their phone does not also buzz about
    /// something already on screen. Long enough to notice and read, short enough that a
    /// challenge is still worth watching when it lands.
    /// </para>
    /// </summary>
    private static readonly TimeSpan PushDelay = TimeSpan.FromSeconds(45);
}
