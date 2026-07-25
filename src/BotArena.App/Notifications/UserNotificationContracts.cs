using System.Text.Json;

namespace BotArena.App.Notifications;

public static class UserNotificationKinds
{
    public const string EntitlementEarned = "entitlement-earned";
}

public sealed record EntitlementNotificationItem(
    string Key,
    string Kind,
    string Id,
    string Label);

public sealed record EntitlementEarnedPayload(
    string SourceKind,
    string SourceId,
    string? Reason,
    IReadOnlyList<EntitlementNotificationItem> Items);

public sealed record UserNotificationResponse(
    Guid Id,
    string Kind,
    DateTime CreatedAt,
    DateTime? ReadAt,
    JsonElement Payload);

public static class UserNotificationContracts
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T payload) =>
        JsonSerializer.Serialize(payload, JsonOptions);

    public static UserNotificationResponse ToResponse(UserNotification notification) =>
        new(
            notification.Id,
            notification.Kind,
            notification.CreatedAt,
            notification.ReadAt,
            JsonSerializer.Deserialize<JsonElement>(
                notification.PayloadJson,
                JsonOptions));
}
