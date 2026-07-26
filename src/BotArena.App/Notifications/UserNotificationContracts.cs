using System.Text.Json;
using System.Text.Json.Serialization;

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

/// <summary>
/// What a notification is about, as one closed set of shapes.
/// <para>
/// Polymorphic rather than a single record so a second kind cannot deserialize into the
/// first and be served as plausible empty data, and rather than a raw JsonElement so it
/// still reaches the OpenAPI document and every generated client — an untyped payload
/// costs TypeScript `unknown` and makes the C# generator emit its own `JsonElement` class
/// that collides with System.Text.Json's.
/// </para>
/// <para>
/// The discriminator repeats <see cref="UserNotificationResponse.Kind"/> on purpose: it is
/// what lets a TypeScript client narrow the union by inspecting the payload alone, which
/// the outer property cannot do.
/// </para>
/// <para>
/// This is a response contract, not a storage format. Rows hold the concrete payload with
/// no discriminator — see <see cref="UserNotificationContracts.ToResponse"/>, which reads
/// them by <see cref="UserNotification.Kind"/>. Keeping the two separate is what lets a
/// kind be added without migrating every existing row.
/// </para>
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(EntitlementEarnedPayload), UserNotificationKinds.EntitlementEarned)]
public abstract record UserNotificationPayload;

public sealed record EntitlementEarnedPayload(
    string SourceKind,
    string SourceId,
    string? Reason,
    IReadOnlyList<EntitlementNotificationItem> Items) : UserNotificationPayload;

public sealed record UserNotificationResponse(
    Guid Id,
    string Kind,
    DateTime CreatedAt,
    DateTime? ReadAt,
    UserNotificationPayload Payload);

public static class UserNotificationContracts
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Writes a payload for storage as its concrete shape, with no type discriminator.
    /// <para>
    /// Deliberately generic on the concrete type rather than taking the base: serializing
    /// through <see cref="UserNotificationPayload"/> would stamp a discriminator into the
    /// column, which no existing row has, leaving two storage formats to read forever.
    /// </para>
    /// </summary>
    public static string Serialize<T>(T payload)
        where T : UserNotificationPayload =>
        JsonSerializer.Serialize(payload, JsonOptions);

    public static UserNotificationResponse ToResponse(UserNotification notification)
    {
        // Read by Kind, because the stored JSON carries no discriminator to read it by.
        // An unmapped kind is a bug — a row written by code that skipped this switch — and
        // failing here is better than serving a default-valued payload as if it were real.
        UserNotificationPayload payload = notification.Kind switch
        {
            UserNotificationKinds.EntitlementEarned =>
                Deserialize<EntitlementEarnedPayload>(notification),
            _ => throw new NotSupportedException(
                $"Notification kind '{notification.Kind}' has no response mapping. Add a " +
                $"case here and a [JsonDerivedType] on {nameof(UserNotificationPayload)} " +
                "when emitting a new kind."),
        };

        return new(
            notification.Id,
            notification.Kind,
            notification.CreatedAt,
            notification.ReadAt,
            payload);
    }

    private static T Deserialize<T>(UserNotification notification)
        where T : UserNotificationPayload =>
        // Written by Serialize<T> with these same options, so the round-trip is total; a
        // null here means a hand-edited row.
        JsonSerializer.Deserialize<T>(notification.PayloadJson, JsonOptions)
        ?? throw new InvalidOperationException(
            $"Notification {notification.Id} has an unreadable payload.");
}
