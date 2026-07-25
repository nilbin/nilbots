using BotArena.App.Notifications;

namespace BotArena.App.Tests;

/// <summary>
/// <see cref="UserNotificationResponse.Payload"/> is a concrete type, which is only sound
/// while one notification kind exists. Adding a second one compiles fine, so these pin the
/// runtime guard that stands in for the compiler.
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

        Assert.Equal("ranked-matches", response.Payload.SourceKind);
        Assert.Equal("lancer", Assert.Single(response.Payload.Items).Id);
    }

    [Fact]
    public void ToResponse_RefusesAKindItCannotRepresent()
    {
        // The failure this exists to prevent: silently deserializing another kind's
        // payload into an all-default EntitlementEarnedPayload and serving it as real.
        var unknown = Notification("season-ended", """{"season":3,"placement":"gold"}""");

        var error = Assert.Throws<NotSupportedException>(
            () => UserNotificationContracts.ToResponse(unknown));
        Assert.Contains("season-ended", error.Message);
    }
}
