namespace BotArena.App.Notifications;

public static class NotificationChannels
{
    public const string Push = "push";
}

public static class NotificationDeliveryStates
{
    public const string Sent = "sent";

    /// <summary>Deliberately not sent — already read in-app, or the player turned it off.</summary>
    public const string Suppressed = "suppressed";

    /// <summary>The transport rejected it. A dead token is the usual cause.</summary>
    public const string Failed = "failed";
}

/// <summary>
/// What happened when one notification was pushed to one device.
/// <para>
/// A record per (notification, device) rather than per notification, because that is the
/// granularity failures happen at: one phone's token expires while the other three
/// succeed, and a single row could only record one of those outcomes.
/// </para>
/// <para>
/// Its purpose is idempotence as much as telemetry. Job retries are expected — the whole
/// point of putting APNs behind a durable job is that it may fail and run again — and
/// without a record of what already went out, every retry re-notifies every device that
/// had already succeeded.
/// </para>
/// </summary>
public class NotificationDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid NotificationId { get; set; }

    /// <summary>Null when suppressed before any device was considered.</summary>
    public Guid? DeviceRegistrationId { get; set; }

    public required string Channel { get; set; }
    public required string State { get; set; }

    /// <summary>Why it was suppressed, or how it failed. Null on success.</summary>
    public string? Detail { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
