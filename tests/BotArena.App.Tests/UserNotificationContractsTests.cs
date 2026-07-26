using System.Text.Json;
using BotArena.App.Notifications;

namespace BotArena.App.Tests;

/// <summary>
/// <see cref="UserNotificationResponse.Payload"/> is a closed union, so a caller has to
/// narrow before reading. What the type system still cannot check is the mapping from a
/// stored <see cref="UserNotification.Kind"/> to the shape its JSON was written as — rows
/// carry no discriminator — so these pin the runtime guard that stands in for it.
/// </summary>
public class UserNotificationContractsTests
{
    private static UserNotification Notification(string kind, string payloadJson) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Kind = kind,
            DedupeKey = $"{kind}:{Guid.NewGuid()}",
            CreatedAt = DateTime.UtcNow,
            PayloadJson = payloadJson,
        };

    [Fact]
    public void ToResponse_ReadsAnEntitlementPayload()
    {
        string json = UserNotificationContracts.Serialize(
            new EntitlementEarnedPayload(
                "ranked-matches",
                "10",
                Reason: null,
                [new EntitlementNotificationItem("bot-look:lancer", "bot-look", "lancer", "Lancer")]));

        var response = UserNotificationContracts.ToResponse(
            Notification(UserNotificationKinds.EntitlementEarned, json));

        // Narrowing is the point of the union: the payload arrives as the base type and
        // only an explicit kind check gets at the fields.
        var payload = Assert.IsType<EntitlementEarnedPayload>(response.Payload);
        Assert.Equal("ranked-matches", payload.SourceKind);
        Assert.Equal("lancer", Assert.Single(payload.Items).Id);
    }

    [Fact]
    public void ResponsePayload_CarriesItsKindDiscriminator()
    {
        // What every client narrows on. The discriminator only appears when the payload
        // is serialized *through the base type*, so a response record typed to a concrete
        // payload would drop it and leave TypeScript unable to tell the kinds apart —
        // silently, since the fields would still be there for the one kind that exists.
        var response = UserNotificationContracts.ToResponse(
            Notification(
                UserNotificationKinds.EntitlementEarned,
                UserNotificationContracts.Serialize(
                    new EntitlementEarnedPayload("ranked-matches", "10", null, []))));

        string json = JsonSerializer.Serialize(response, new JsonSerializerOptions(
            JsonSerializerDefaults.Web));

        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            UserNotificationKinds.EntitlementEarned,
            document.RootElement.GetProperty("payload").GetProperty("kind").GetString());
    }

    [Fact]
    public void StoredPayload_HasNoDiscriminator()
    {
        // Storage is the concrete shape, deliberately. Rows written before the union
        // existed have no discriminator, so writing one now would leave two formats in
        // the column to read forever.
        string stored = UserNotificationContracts.Serialize(
            new EntitlementEarnedPayload("ranked-matches", "10", null, []));

        using var document = JsonDocument.Parse(stored);
        Assert.False(document.RootElement.TryGetProperty("kind", out _));
    }

    [Fact]
    public void AChallengeAndItsResultShareOneDedupeKey()
    {
        // Supersession lives or dies on this. The result rewrites the challenge's row
        // rather than appending beside it, which only happens if both address the same
        // (user, key) pair — so a key that embedded the kind, as these once did, would
        // silently leave a stale "watch this" under its own outcome (DECISIONS #118).
        var matchId = Guid.NewGuid();
        var botId = Guid.NewGuid();

        Assert.Equal(
            UserNotificationKeys.MatchSubject(matchId, botId),
            UserNotificationKeys.MatchSubject(matchId, botId));
        Assert.DoesNotContain(UserNotificationKinds.MatchChallenged,
            UserNotificationKeys.MatchSubject(matchId, botId));
        Assert.DoesNotContain(UserNotificationKinds.MatchSettled,
            UserNotificationKeys.MatchSubject(matchId, botId));
    }

    [Fact]
    public void EachOfAPlayersBotsIsItsOwnSubject()
    {
        // Both participants can belong to one player, and each bot has its own outcome to
        // be told about. A match-only key would collapse those into one notification.
        var matchId = Guid.NewGuid();

        Assert.NotEqual(
            UserNotificationKeys.MatchSubject(matchId, Guid.NewGuid()),
            UserNotificationKeys.MatchSubject(matchId, Guid.NewGuid()));
    }

    [Fact]
    public void ToResponse_ReadsAChallengePayload()
    {
        string json = UserNotificationContracts.Serialize(
            new MatchChallengedPayload(
                Guid.NewGuid(),
                "arena-01",
                Guid.NewGuid(),
                "Pincer",
                "vanguard",
                "#22d3ee",
                "hunter"));

        var response = UserNotificationContracts.ToResponse(
            Notification(UserNotificationKinds.MatchChallenged, json));

        var payload = Assert.IsType<MatchChallengedPayload>(response.Payload);
        Assert.Equal("hunter", payload.ChallengerName);
        Assert.Equal("arena-01", payload.MapId);
    }

    [Fact]
    public void ToResponse_RefusesAKindItCannotRepresent()
    {
        // The failure this exists to prevent: silently deserializing another kind's
        // payload into an all-default EntitlementEarnedPayload and serving it as real.
        // The union alone cannot stop that — nothing about a new kind fails to compile.
        var unknown = Notification("season-ended", """{"season":3,"placement":"gold"}""");

        var error = Assert.Throws<NotSupportedException>(
            () => UserNotificationContracts.ToResponse(unknown));
        Assert.Contains("season-ended", error.Message);
    }
}
