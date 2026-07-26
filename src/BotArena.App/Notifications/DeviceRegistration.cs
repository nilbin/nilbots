namespace BotArena.App.Notifications;

/// <summary>
/// A device that has asked to receive push notifications for an account.
/// <para>
/// Keyed on the push token rather than the device, because the token is what the transport
/// addresses and what uniqueness has to hold for. <see cref="DeviceId"/> is carried
/// alongside so a reinstall that mints a new token can retire the old one: without it, an
/// account accumulates dead tokens forever and every send fans out to them.
/// </para>
/// <para>
/// Registrations are per account *and* per device. Two people sharing a phone must not
/// inherit each other's notifications, so signing in registers and signing out removes —
/// the row belongs to the pair, not to the hardware.
/// </para>
/// </summary>
public class DeviceRegistration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid UserId { get; set; }

    /// <summary>The Expo push token. Rotates, so it is refreshed rather than assumed stable.</summary>
    public required string PushToken { get; set; }

    /// <summary>
    /// A stable per-installation id from the client.
    /// <para>
    /// Not the hardware id — those are unavailable or restricted on both platforms, and
    /// this only needs to be stable enough to recognise "the same install, new token".
    /// </para>
    /// </summary>
    public required string DeviceId { get; set; }

    /// <summary>`ios` or `android`. Recorded for diagnosis, not routing — Expo handles both.</summary>
    public required string Platform { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last time the client confirmed this token.
    /// <para>
    /// Expo reports a token as unregistered only when a send fails, so this is the only
    /// signal available before then — a registration that has not been refreshed in months
    /// belongs to an app that was deleted.
    /// </para>
    /// </summary>
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}
