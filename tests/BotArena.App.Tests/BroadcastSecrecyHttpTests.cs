using System.Text.Json;
using BotArena.App.Matches;

namespace BotArena.App.Tests;

[Collection(ApplicationHttpCollection.Name)]
public class BroadcastSecrecyHttpTests
{
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task CompletedMatch_ConcealsAndThenRevealsOutcomeOnSharedClock()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        Guid matchId;
        await using (var db = await database.CreateMigratedContextAsync())
        {
            var match = new Match
            {
                MapId = "arena-01",
                Seed = 42,
                Status = MatchStatus.Completed,
                WinnerSlot = 0,
                EndReason = "Elimination",
                EndTick = 10,
                BroadcastStartedAt = DateTime.UtcNow.AddHours(1),
            };
            db.Matches.Add(match);
            await db.SaveChangesAsync();
            matchId = match.Id;
        }

        using var factory = new BotArenaApplicationFactory(database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        using (JsonDocument concealed = await GetMatchAsync(client, matchId))
        {
            Assert.Equal(JsonValueKind.Null,
                concealed.RootElement.GetProperty("winnerSlot").ValueKind);
            Assert.Equal(JsonValueKind.Null,
                concealed.RootElement.GetProperty("endReason").ValueKind);
            Assert.Equal(JsonValueKind.Null,
                concealed.RootElement.GetProperty("endTick").ValueKind);
        }

        await using (var db = database.CreateContext())
        {
            Match match = await db.Matches.FindAsync(matchId)
                ?? throw new InvalidOperationException("Test match disappeared.");
            match.BroadcastStartedAt = DateTime.UtcNow.AddHours(-1);
            await db.SaveChangesAsync();
        }

        using JsonDocument revealed = await GetMatchAsync(client, matchId);
        Assert.Equal(0, revealed.RootElement.GetProperty("winnerSlot").GetInt32());
        Assert.Equal("Elimination",
            revealed.RootElement.GetProperty("endReason").GetString());
        Assert.Equal(10, revealed.RootElement.GetProperty("endTick").GetInt32());
    }

    private static async Task<JsonDocument> GetMatchAsync(
        HttpClient client,
        Guid matchId)
    {
        HttpResponseMessage response = await client.GetAsync($"/api/matches/{matchId}");
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }
}
