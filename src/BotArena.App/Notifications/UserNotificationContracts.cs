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

/// <summary>
/// <para>
/// <see cref="Payload"/> is typed rather than a raw JsonElement so it reaches the OpenAPI
/// document and every generated client. An untyped payload costs more than it looks:
/// TypeScript gets `unknown`, and the C# generator emits its own `JsonElement` class that
/// collides with System.Text.Json's.
/// </para>
/// <para>
/// This assumes one payload shape, which holds while <see cref="UserNotificationKinds"/>
/// has a single member. Adding a second kind means making this a discriminated union
/// (<c>[JsonPolymorphic]</c> on a base type, keyed by <see cref="Kind"/>) — not widening
/// it back to JsonElement. Nothing about a second kind fails to compile, so
/// <see cref="UserNotificationContracts.ToResponse"/> rejects unknown kinds at runtime
/// instead: without that guard a new kind would deserialize into an empty payload of this
/// type and ship silently wrong data to every client.
/// </para>
/// </summary>
public sealed record UserNotificationResponse(
    Guid Id,
    string Kind,
    DateTime CreatedAt,
    DateTime? ReadAt,
    EntitlementEarnedPayload Payload);

public static class UserNotificationContracts
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T payload) =>
        JsonSerializer.Serialize(payload, JsonOptions);

    public static UserNotificationResponse ToResponse(UserNotification notification)
    {
        // Deserializing by Kind, not blindly: every kind currently shares one payload
        // record, so an unrecognized kind would otherwise parse into an all-default
        // EntitlementEarnedPayload and be served as if it were real. Fail here instead —
        // this throw is what a second notification kind runs into, since adding one
        // compiles perfectly well.
        if (notification.Kind != UserNotificationKinds.EntitlementEarned)
        {
            throw new NotSupportedException(
                $"Notification kind '{notification.Kind}' has no response mapping. Give " +
                $"{nameof(UserNotificationResponse)}.Payload a discriminated union keyed " +
                "by Kind before emitting a new kind.");
        }

        // Written by Serialize<EntitlementEarnedPayload> with these same options, so the
        // round-trip is total; a null here means a hand-edited row.
        var payload = JsonSerializer.Deserialize<EntitlementEarnedPayload>(
            notification.PayloadJson,
            JsonOptions)
            ?? throw new InvalidOperationException(
                $"Notification {notification.Id} has an unreadable payload.");

        return new(
            notification.Id,
            notification.Kind,
            notification.CreatedAt,
            notification.ReadAt,
            payload);
    }
}
