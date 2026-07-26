namespace BotArena.App.Notifications;

/// <summary>
/// One account's opt-out for one notification kind's push channel.
/// <para>
/// Stored as exceptions rather than a row per (account, kind): push is on by default, and
/// a table of defaults would need backfilling every time a kind is added. An absent row
/// means "on", so a new kind reaches everyone without a migration and only people who
/// actually turned something off carry a row.
/// </para>
/// <para>
/// This governs the *push* channel only. Turning result pushes off must not stop the
/// durable record being written or delivered in-app — the inbox is the record of what
/// happened, and letting a preference erase history would make the ladder and the inbox
/// disagree.
/// </para>
/// </summary>
public class NotificationPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid UserId { get; set; }

    /// <summary>A value from <see cref="UserNotificationKinds"/>.</summary>
    public required string Kind { get; set; }

    public bool PushEnabled { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
