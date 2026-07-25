using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BotArena.App.Cosmetics;
using BotArena.App.Notifications;

namespace BotArena.App.Tests;

[Collection(ApplicationHttpCollection.Name)]
public class UserNotificationsHttpTests
{
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task InboxAndHub_AreAuthenticatedScopedAndAcknowledgeIdempotently()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using (var migration = await database.CreateMigratedContextAsync())
        {
            Assert.Empty(migration.ChangeTracker.Entries());
        }

        using var factory = new BotArenaApplicationFactory(database.ConnectionString);
        using HttpClient firstClient = factory.CreateClient();
        using HttpClient secondClient = factory.CreateClient();
        using HttpClient anonymousClient = factory.CreateClient();
        Guid firstUserId = await RegisterAsync(
            firstClient,
            "notifications-one@example.test",
            "Notifications One");
        Guid secondUserId = await RegisterAsync(
            secondClient,
            "notifications-two@example.test",
            "Notifications Two");

        await using (var db = database.CreateContext())
        {
            var service = new CosmeticEntitlementService(
                db,
                CosmeticCatalog.LoadDefault());
            await service.GrantForEventAsync(
                firstUserId,
                CosmeticUnlockEvents.Achievement,
                CosmeticUnlockEvents.FirstSuccessfulBuild);
            await service.GrantForEventAsync(
                secondUserId,
                CosmeticUnlockEvents.Challenge,
                CosmeticUnlockEvents.FirstUnrankedMatch);
        }

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymousClient.GetAsync("/api/notifications")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymousClient.PostAsync(
                "/hubs/notifications/negotiate?negotiateVersion=1",
                content: null)).StatusCode);
        (await firstClient.PostAsync(
            "/hubs/notifications/negotiate?negotiateVersion=1",
            content: null)).EnsureSuccessStatusCode();

        using JsonDocument firstInbox = await GetJsonAsync(
            firstClient,
            "/api/notifications");
        JsonElement firstNotification =
            Assert.Single(firstInbox.RootElement.EnumerateArray());
        Guid notificationId = firstNotification.GetProperty("id").GetGuid();
        Assert.Equal(
            "lancer",
            firstNotification.GetProperty("payload")
                .GetProperty("items")[0]
                .GetProperty("id")
                .GetString());

        // A guessed ID cannot acknowledge another account's notification.
        (await secondClient.PostAsync(
            $"/api/notifications/{notificationId}/read",
            content: null)).EnsureSuccessStatusCode();
        Assert.Single((await GetJsonAsync(firstClient, "/api/notifications"))
            .RootElement.EnumerateArray());

        (await firstClient.PostAsync(
            $"/api/notifications/{notificationId}/read",
            content: null)).EnsureSuccessStatusCode();
        (await firstClient.PostAsync(
            $"/api/notifications/{notificationId}/read",
            content: null)).EnsureSuccessStatusCode();
        Assert.Empty((await GetJsonAsync(firstClient, "/api/notifications"))
            .RootElement.EnumerateArray());
    }

    private static async Task<Guid> RegisterAsync(
        HttpClient client,
        string email,
        string displayName)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/accounts/register",
            new
            {
                displayName,
                email,
                password = "correct-horse-battery-staple",
            });
        response.EnsureSuccessStatusCode();
        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> GetJsonAsync(
        HttpClient client,
        string path)
    {
        HttpResponseMessage response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
    }
}
