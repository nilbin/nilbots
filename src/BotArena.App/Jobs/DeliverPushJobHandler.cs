using BotArena.App.Notifications;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Jobs;

/// <summary>
/// Pushes one durable notification to an account's devices.
/// <para>
/// A job rather than an inline send, because APNs and FCM are network calls that fail,
/// retry and rate-limit. Sending inside the transaction that finalizes a ranked set would
/// make match settlement depend on Apple being reachable; here a failure is a retry of the
/// push alone, and the durable record — the thing that actually matters — was committed
/// long before.
/// </para>
/// <para>
/// It is scheduled a short way into the future on purpose. A player watching the app gets
/// the notification over SignalR immediately and reads it; the delay is what lets that
/// count, so the phone does not buzz about something already on screen.
/// </para>
/// </summary>
public sealed class DeliverPushJobHandler(
    AppDbContext db,
    IPushTransport transport,
    TimeProvider timeProvider,
    ILogger<DeliverPushJobHandler> logger)
{
    public async Task<JobExecutionResult> HandleAsync(
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        UserNotification? notification = await db.UserNotifications
            .SingleOrDefaultAsync(row => row.Id == notificationId, cancellationToken);
        if (notification is null)
            return new JobExecutionResult("notification_gone");

        // Read in-app before the push went out. This is the case the delay exists for.
        if (notification.ReadAt is not null)
            return await SuppressAsync(notification, "read_in_app", cancellationToken);

        UserNotificationPayload payload =
            UserNotificationContracts.ToResponse(notification).Payload;
        if (PushCopy.For(payload) is not var (title, body, data))
            return await SuppressAsync(notification, "kind_not_pushed", cancellationToken);

        // Absent means on: preferences are stored as exceptions, so a new kind reaches
        // everyone without a backfill and only opt-outs carry a row.
        bool optedOut = await db.NotificationPreferences.AnyAsync(
            preference => preference.UserId == notification.UserId
                && preference.Kind == notification.Kind
                && !preference.PushEnabled,
            cancellationToken);
        if (optedOut)
            return await SuppressAsync(notification, "opted_out", cancellationToken);

        List<DeviceRegistration> devices = await db.DeviceRegistrations
            .Where(device => device.UserId == notification.UserId)
            .ToListAsync(cancellationToken);
        if (devices.Count == 0)
            return await SuppressAsync(notification, "no_devices", cancellationToken);

        // What already went out on a previous attempt. Without this a retry re-notifies
        // every device that had already succeeded, which is precisely the failure mode a
        // retrying job invites.
        HashSet<Guid> alreadySent = await db.NotificationDeliveries
            .Where(delivery => delivery.NotificationId == notification.Id
                && delivery.State == NotificationDeliveryStates.Sent
                && delivery.DeviceRegistrationId != null)
            .Select(delivery => delivery.DeviceRegistrationId!.Value)
            .ToHashSetAsync(cancellationToken);

        DeviceRegistration[] pending = devices
            .Where(device => !alreadySent.Contains(device.Id))
            .ToArray();
        if (pending.Length == 0)
            return new JobExecutionResult("already_delivered");

        IReadOnlyList<PushResult> results = await transport.SendAsync(
            pending.Select(device => new PushMessage(device.PushToken, title, body, data)).ToArray(),
            cancellationToken);

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        var byToken = pending.ToDictionary(device => device.PushToken);
        foreach (PushResult result in results)
        {
            if (!byToken.TryGetValue(result.PushToken, out DeviceRegistration? device))
                continue;

            db.NotificationDeliveries.Add(new NotificationDelivery
            {
                NotificationId = notification.Id,
                DeviceRegistrationId = device.Id,
                Channel = NotificationChannels.Push,
                State = result.Ok
                    ? NotificationDeliveryStates.Sent
                    : NotificationDeliveryStates.Failed,
                Detail = result.Error,
                CreatedAt = now,
            });

            // A dead token never comes back. Keeping it means every future send fans out
            // to a device that uninstalled the app months ago.
            if (result.TokenIsDead)
            {
                db.DeviceRegistrations.Remove(device);
                logger.LogInformation(
                    "Dropped unregistered push token for user {UserId}", device.UserId);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new JobExecutionResult(
            results.Any(result => result.Ok) ? "pushed" : "push_failed");
    }

    private async Task<JobExecutionResult> SuppressAsync(
        UserNotification notification,
        string reason,
        CancellationToken cancellationToken)
    {
        db.NotificationDeliveries.Add(new NotificationDelivery
        {
            NotificationId = notification.Id,
            Channel = NotificationChannels.Push,
            State = NotificationDeliveryStates.Suppressed,
            Detail = reason,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        });
        await db.SaveChangesAsync(cancellationToken);
        return new JobExecutionResult($"suppressed_{reason}");
    }
}
