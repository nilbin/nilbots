namespace BotArena.App.Notifications;

/// <summary>
/// A durable, user-owned product notification. Realtime channels are delivery
/// accelerators; this row is what guarantees the user sees it after returning.
/// </summary>
public sealed class UserNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string Kind { get; set; }
    public required string DedupeKey { get; set; }
    public required string PayloadJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
