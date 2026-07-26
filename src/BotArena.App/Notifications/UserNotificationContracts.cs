using System.Text.Json;
using System.Text.Json.Serialization;

namespace BotArena.App.Notifications;

public static class UserNotificationKinds
{
    public const string EntitlementEarned = "entitlement-earned";

    /// <summary>
    /// Someone has thrown a bot of theirs at a bot of yours, and it is about to fight.
    /// <para>
    /// Only the challenged is told. The challenger pressed the button and is almost
    /// certainly looking at the screen; telling them is an echo, and an app that echoes
    /// your own actions is one you learn to ignore (DECISIONS #119).
    /// </para>
    /// <para>
    /// This kind is transient by design: the same row becomes <see cref="MatchSettled"/>
    /// when the fight finishes broadcasting, rather than a second row appearing beside it.
    /// See <see cref="UserNotificationKeys.MatchSubject"/>.
    /// </para>
    /// </summary>
    public const string MatchChallenged = "match-challenged";

    /// <summary>
    /// An unranked match a player's bot fought has finished broadcasting.
    /// <para>
    /// Games inside a ranked set never use this: a set announces once, as
    /// <see cref="SetSettled"/>. Six rows per set would both bury the inbox and leak the
    /// set's shape game by game (DECISIONS #118).
    /// </para>
    /// </summary>
    public const string MatchSettled = "match-settled";

    /// <summary>A ranked set has been revealed, with its score and rating change.</summary>
    public const string SetSettled = "set-settled";
}

/// <summary>
/// Dedupe keys, which decide what counts as "the same notification".
/// <para>
/// A key is per *subject*, not per kind, and that is what makes supersession work: the
/// challenge and its result are one row that changes, rather than a stale "watch this"
/// accumulating beside its own outcome (DECISIONS #118).
/// </para>
/// </summary>
public static class UserNotificationKeys
{
    /// <summary>
    /// One row per (player's bot, match) — written as a challenge, rewritten as a result.
    /// <para>
    /// Scoped by bot as well as match because both participants can belong to one player,
    /// and each of their bots has its own outcome to be told about.
    /// </para>
    /// </summary>
    public static string MatchSubject(Guid matchId, Guid botId) => $"match:{matchId}:{botId}";

    /// <summary>One row per (player's bot, ranked set).</summary>
    public static string SetSubject(Guid matchSetId, Guid botId) => $"set:{matchSetId}:{botId}";
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
[JsonDerivedType(typeof(MatchChallengedPayload), UserNotificationKinds.MatchChallenged)]
[JsonDerivedType(typeof(MatchSettledPayload), UserNotificationKinds.MatchSettled)]
[JsonDerivedType(typeof(SetSettledPayload), UserNotificationKinds.SetSettled)]
public abstract record UserNotificationPayload;

public sealed record EntitlementEarnedPayload(
    string SourceKind,
    string SourceId,
    string? Reason,
    IReadOnlyList<EntitlementNotificationItem> Items) : UserNotificationPayload;

/// <summary>
/// A match that has been created against one of the recipient's bots.
/// <para>
/// Carries the same bot identity fields as <see cref="MatchSettledPayload"/> so a client
/// renders the challenge and its eventual result with one component and one set of assets
/// — the row is going to turn into that result in place.
/// </para>
/// <para>
/// No outcome, obviously, and none can be inferred: this is written when the match is
/// queued, long before broadcast secrecy would allow a result to exist.
/// </para>
/// </summary>
public sealed record MatchChallengedPayload(
    Guid MatchId,
    string MapId,
    Guid BotId,
    string BotName,
    string BotLookId,
    string BotAccent,
    string ChallengerName) : UserNotificationPayload;

/// <summary>
/// A finished match, phrased from the recipient's side.
/// <para>
/// Per-recipient rather than a neutral description of the match: the whole value of this
/// notification is "my bot won", so the outcome is already resolved for whoever is being
/// told rather than leaving each client to work out which participant is theirs.
/// </para>
/// </summary>
public sealed record MatchSettledPayload(
    Guid MatchId,
    string MapId,
    Guid BotId,
    string BotName,
    /// <summary>Stable catalog id and accent, so a client renders the bot from its own assets (#108).</summary>
    string BotLookId,
    string BotAccent,
    /// <summary>Win, Loss or Draw, from this recipient's point of view.</summary>
    string Outcome,
    string OpponentName) : UserNotificationPayload;

/// <summary>
/// A revealed ranked set, phrased from the recipient's side.
/// <para>
/// <see cref="RatingChange"/> is this bot's own signed delta, not the set's — it is the
/// number the notification exists to deliver ("+25"), and leaving each client to work out
/// its sign from which side it is on is how one of them eventually gets it backwards.
/// </para>
/// </summary>
public sealed record SetSettledPayload(
    Guid MatchSetId,
    Guid BotId,
    string BotName,
    string BotLookId,
    string BotAccent,
    string Outcome,
    double Score,
    double OpponentScore,
    double RatingChange,
    string OpponentName) : UserNotificationPayload;

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
            UserNotificationKinds.MatchChallenged =>
                Deserialize<MatchChallengedPayload>(notification),
            UserNotificationKinds.MatchSettled =>
                Deserialize<MatchSettledPayload>(notification),
            UserNotificationKinds.SetSettled =>
                Deserialize<SetSettledPayload>(notification),
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
