using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BotArena.App.Bots;
using BotArena.App.Matches;
using BotArena.App.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace BotArena.App.Tests;

[Collection(ApplicationHttpCollection.Name)]
public class BotAppearanceHttpTests
{
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task CreateAndUpdate_ReturnNamedPayloadsAndStableProblemCodes()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using (var db = await database.CreateMigratedContextAsync())
        {
            Assert.Empty(db.ChangeTracker.Entries());
        }
        using var factory = new BotArenaApplicationFactory(database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        await RegisterAsync(client, "http-create@example.test", "HTTP Create");

        HttpResponseMessage locked = await client.PostAsJsonAsync(
            "/api/bots/",
            new
            {
                name = "Locked HTTP Bot",
                accent = "#ABCDEF",
                lookId = "lancer",
                projectileLookId = "pulse-bolt",
            });
        Assert.Equal(HttpStatusCode.Forbidden, locked.StatusCode);
        using (JsonDocument problem = await JsonDocument.ParseAsync(
            await locked.Content.ReadAsStreamAsync()))
        {
            Assert.Equal(
                "appearance.bot_look_locked",
                problem.RootElement.GetProperty("code").GetString());
            Assert.False(string.IsNullOrWhiteSpace(
                problem.RootElement.GetProperty("traceId").GetString()));
        }

        HttpResponseMessage createdResponse = await client.PostAsJsonAsync(
            "/api/bots/",
            new
            {
                name = "HTTP Bot",
                accent = "#ABCDEF",
                lookId = "VANGUARD",
                projectileLookId = "PULSE-BOLT",
            });
        createdResponse.EnsureSuccessStatusCode();
        using JsonDocument created = await JsonDocument.ParseAsync(
            await createdResponse.Content.ReadAsStreamAsync());
        Guid botId = created.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("#abcdef", created.RootElement.GetProperty("accent").GetString());
        Assert.Equal("vanguard", created.RootElement.GetProperty("lookId").GetString());

        HttpResponseMessage invalidUpdate = await client.PutAsJsonAsync(
            $"/api/bots/{botId}/appearance",
            new
            {
                accent = "blue",
                lookId = "vanguard",
                projectileLookId = "pulse-bolt",
            });
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdate.StatusCode);
        using JsonDocument updateProblem = await JsonDocument.ParseAsync(
            await invalidUpdate.Content.ReadAsStreamAsync());
        Assert.Equal(
            "appearance.accent_invalid",
            updateProblem.RootElement.GetProperty("code").GetString());
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task MatchAdmission_RechecksEntitlement_ButReplayReadDoesNot()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using (var migration = await database.CreateMigratedContextAsync())
        {
            Assert.Empty(migration.ChangeTracker.Entries());
        }
        using var factory = new BotArenaApplicationFactory(database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        Guid userId = await RegisterAsync(
            client,
            "http-match@example.test",
            "HTTP Match");

        Guid lockedBotId;
        Guid opponentId;
        Guid historicalMatchId;
        string replayKey;
        await using (var db = database.CreateContext())
        {
            var lockedBot = new Bot
            {
                OwnerUserId = userId,
                Name = "Revoked Lancer",
                Slug = "revoked-lancer",
                LookId = "lancer",
            };
            var opponent = new Bot
            {
                OwnerUserId = userId,
                Name = "Starter Opponent",
                Slug = "starter-opponent",
            };
            BotVersion lockedVersion = BuiltVersion(lockedBot.Id);
            BotVersion opponentVersion = BuiltVersion(opponent.Id);
            db.Bots.AddRange(lockedBot, opponent);
            db.BotVersions.AddRange(lockedVersion, opponentVersion);

            var historicalMatch = new Match
            {
                MapId = "arena-01",
                Seed = 7,
                Status = MatchStatus.Completed,
                EndTick = 1,
                ReplayHash = "historical",
            };
            replayKey = ObjectKeys.Replay(historicalMatch.Id);
            historicalMatch.ReplayKey = replayKey;
            historicalMatch.Participants.Add(new MatchParticipant
            {
                MatchId = historicalMatch.Id,
                Slot = 0,
                BotId = lockedBot.Id,
                BotVersionId = lockedVersion.Id,
                NameSnapshot = lockedBot.Name,
                AccentSnapshot = lockedBot.Accent,
                LookIdSnapshot = lockedBot.LookId,
                ProjectileLookIdSnapshot = lockedBot.ProjectileLookId,
            });
            db.Matches.Add(historicalMatch);
            await db.SaveChangesAsync();
            lockedBotId = lockedBot.Id;
            opponentId = opponent.Id;
            historicalMatchId = historicalMatch.Id;
        }

        IObjectStore objects = factory.Services.GetRequiredService<IObjectStore>();
        await using (var replay = new MemoryStream(Encoding.UTF8.GetBytes("{}")))
            await objects.PutAsync(replayKey, replay, expectedSha256: null);

        HttpResponseMessage denied = await client.PostAsJsonAsync(
            "/api/matches/challenge",
            new
            {
                botId = lockedBotId,
                opponentBotId = opponentId,
                mapId = "arena-01",
                seed = 9,
            });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (JsonDocument problem = await JsonDocument.ParseAsync(
            await denied.Content.ReadAsStreamAsync()))
        {
            Assert.Equal(
                "appearance.bot_look_locked",
                problem.RootElement.GetProperty("code").GetString());
        }

        HttpResponseMessage replayResponse =
            await client.GetAsync($"/api/matches/{historicalMatchId}/replay");
        replayResponse.EnsureSuccessStatusCode();
        Assert.Equal("{}", await replayResponse.Content.ReadAsStringAsync());
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

    private static BotVersion BuiltVersion(Guid botId)
    {
        var version = new BotVersion
        {
            BotId = botId,
            VersionNumber = 1,
            EntryType = "Bot",
            SourcesJson = "[]",
            SourceHash = "test",
            Status = BuildStatus.Built,
            ArtifactHash = "test",
            IsActive = true,
        };
        return version;
    }
}
