using System.Text.Json;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Shared;
using BotArena.Engine;

namespace BotArena.App.Tests;

[Collection(ApplicationHttpCollection.Name)]
public class BotLadderPresentationHttpTests
{
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task BotPageAndLeaderboard_ShareCurrentCompetitionRankAndLook()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        Guid targetBotId;
        await using (AppDbContext db = await database.CreateMigratedContextAsync())
        {
            var owner = new User
            {
                DisplayName = "Ladder Owner",
                Email = "ladder-owner@example.test",
                PasswordHash = "not-used",
            };
            var leaderA = Bot(owner, "Leader A", "leader-a", "vanguard");
            var leaderB = Bot(owner, "Leader B", "leader-b", "glass-manta");
            var target = Bot(owner, "Target", "target", "aureate-warden");
            db.AddRange(owner, leaderA, leaderB, target);

            string rulesVersion = GameRules.Current.RulesVersion;
            db.BotRatings.AddRange(
                Rating(leaderA, rulesVersion, rating: 1400, sets: 8),
                Rating(leaderB, rulesVersion, rating: 1400, sets: 5),
                Rating(target, rulesVersion, rating: 1275, sets: 3),
                Rating(target, "historical-rules", rating: 1800, sets: 20));
            await db.SaveChangesAsync();
            targetBotId = target.Id;
        }

        using var factory = new BotArenaApplicationFactory(database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using JsonDocument detail = await GetJsonAsync(client, "/api/bots/target");
        JsonElement standing = detail.RootElement.GetProperty("currentStanding");
        Assert.Equal(GameRules.Current.RulesVersion, standing.GetProperty("rulesVersion").GetString());
        Assert.Equal(1275, standing.GetProperty("rating").GetDouble());
        Assert.Equal(3, standing.GetProperty("rankedSets").GetInt32());
        Assert.Equal(3, standing.GetProperty("rank").GetInt32());

        using JsonDocument leaderboard = await GetJsonAsync(client, "/api/leaderboard");
        JsonElement targetEntry = leaderboard.RootElement.GetProperty("entries")
            .EnumerateArray()
            .Single(entry => entry.GetProperty("id").GetGuid() == targetBotId);
        Assert.Equal("aureate-warden", targetEntry.GetProperty("lookId").GetString());
        Assert.Equal(3, targetEntry.GetProperty("rank").GetInt32());
    }

    private static Bot Bot(
        User owner,
        string name,
        string slug,
        string lookId) => new()
    {
        OwnerUserId = owner.Id,
        Name = name,
        Slug = slug,
        LookId = lookId,
    };

    private static BotRating Rating(
        Bot bot,
        string rulesVersion,
        double rating,
        int sets) => new()
    {
        BotId = bot.Id,
        RulesVersion = rulesVersion,
        Rating = rating,
        RankedSets = sets,
    };

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
