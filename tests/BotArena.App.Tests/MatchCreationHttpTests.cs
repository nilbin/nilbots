using System.Net.Http.Json;
using System.Text.Json;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Matches;
using BotArena.Engine;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Tests;

[Collection(ApplicationHttpCollection.Name)]
public class MatchCreationHttpTests
{
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task RankedAndUnrankedCreation_ShareImmutableCompleteSnapshots()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using (var migration = await database.CreateMigratedContextAsync())
        {
            Assert.Empty(migration.ChangeTracker.Entries());
        }

        using var factory = new BotArenaApplicationFactory(database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        Guid challengerOwnerId = await RegisterAsync(
            client,
            "snapshot-challenger@example.test",
            "Snapshot Challenger");

        Guid challengerId;
        Guid opponentId;
        await using (var db = database.CreateContext())
        {
            var opponentOwner = new User
            {
                DisplayName = "Snapshot Opponent",
                Email = "snapshot-opponent@example.test",
                PasswordHash = "not-used",
            };
            var challenger = new Bot
            {
                OwnerUserId = challengerOwnerId,
                Name = "Snapshot Alpha",
                Slug = "snapshot-alpha",
                Accent = "#112233",
                LookId = "vanguard",
                ProjectileLookId = "pulse-bolt",
            };
            var opponent = new Bot
            {
                OwnerUserId = opponentOwner.Id,
                Name = "Snapshot Beta",
                Slug = "snapshot-beta",
                Accent = "#445566",
                LookId = "vanguard",
                ProjectileLookId = "pulse-bolt",
            };
            db.Users.Add(opponentOwner);
            db.Bots.AddRange(challenger, opponent);
            db.BotVersions.AddRange(
                BuiltVersion(challenger.Id, "alpha-artifact"),
                BuiltVersion(opponent.Id, "beta-artifact"));
            await db.SaveChangesAsync();
            challengerId = challenger.Id;
            opponentId = opponent.Id;
        }

        HttpResponseMessage unrankedResponse = await client.PostAsJsonAsync(
            "/api/matches/challenge",
            new
            {
                botId = challengerId,
                opponentBotId = opponentId,
                mapId = "arena-01",
                seed = 17,
            });
        unrankedResponse.EnsureSuccessStatusCode();
        Guid unrankedId = await ReadIdAsync(unrankedResponse);

        HttpResponseMessage rankedResponse = await client.PostAsJsonAsync(
            "/api/matches/ranked",
            new { botId = challengerId });
        rankedResponse.EnsureSuccessStatusCode();
        Guid setId = await ReadIdAsync(rankedResponse);

        await using (var db = database.CreateContext())
        {
            Match unranked = await db.Matches
                .Include(match => match.Participants)
                .SingleAsync(match => match.Id == unrankedId);
            Assert.Equal(GameRules.Current.RulesVersion, unranked.GameRulesVersion);
            AssertSnapshot(
                unranked.Participants.Single(participant => participant.BotId == challengerId),
                "Snapshot Alpha",
                "Snapshot Challenger",
                "alpha-artifact");
            AssertSnapshot(
                unranked.Participants.Single(participant => participant.BotId == opponentId),
                "Snapshot Beta",
                "Snapshot Opponent",
                "beta-artifact");

            List<Match> ranked = await db.Matches
                .Include(match => match.Participants)
                .Where(match => match.MatchSetId == setId)
                .OrderBy(match => match.SetGame)
                .ToListAsync();
            Assert.Equal(MatchSet.Games, ranked.Count);
            Assert.All(
                ranked,
                match =>
                {
                    Assert.Equal(GameRules.Current.RulesVersion, match.GameRulesVersion);
                    Assert.Equal(2, match.Participants.Count);
                    Assert.Contains(
                        match.Participants,
                        participant =>
                            participant.BotId == challengerId &&
                            participant.OwnerDisplayNameSnapshot == "Snapshot Challenger" &&
                            participant.ArtifactHashSnapshot == "alpha-artifact");
                    Assert.Contains(
                        match.Participants,
                        participant =>
                            participant.BotId == opponentId &&
                            participant.OwnerDisplayNameSnapshot == "Snapshot Opponent" &&
                            participant.ArtifactHashSnapshot == "beta-artifact");
                });

            Bot challenger = await db.Bots.FindAsync(challengerId)
                ?? throw new InvalidOperationException("Challenger disappeared.");
            User owner = await db.Users.FindAsync(challengerOwnerId)
                ?? throw new InvalidOperationException("Owner disappeared.");
            challenger.Name = "Renamed After Challenge";
            owner.DisplayName = "Renamed Owner";
            await db.SaveChangesAsync();
        }

        using JsonDocument historical = await GetJsonAsync(
            client,
            $"/api/matches/{unrankedId}");
        JsonElement challengerSnapshot = historical.RootElement
            .GetProperty("participants")
            .EnumerateArray()
            .Single(participant =>
                participant.GetProperty("botId").GetGuid() == challengerId);
        Assert.Equal(
            "Snapshot Alpha",
            challengerSnapshot.GetProperty("nameSnapshot").GetString());
        Assert.Equal(
            "Snapshot Challenger",
            challengerSnapshot.GetProperty("ownerDisplayNameSnapshot").GetString());
    }

    private static void AssertSnapshot(
        MatchParticipant participant,
        string name,
        string owner,
        string artifact)
    {
        Assert.Equal(name, participant.NameSnapshot);
        Assert.Equal(owner, participant.OwnerDisplayNameSnapshot);
        Assert.Equal(artifact, participant.ArtifactHashSnapshot);
        Assert.Equal("vanguard", participant.LookIdSnapshot);
        Assert.Equal("pulse-bolt", participant.ProjectileLookIdSnapshot);
    }

    private static BotVersion BuiltVersion(Guid botId, string artifactHash) =>
        new()
        {
            BotId = botId,
            VersionNumber = 1,
            EntryType = "Bot",
            SourcesJson = "[]",
            SourceHash = "source",
            Status = BuildStatus.Built,
            ArtifactHash = artifactHash,
            IsActive = true,
        };

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
        return await ReadIdAsync(response);
    }

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
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
