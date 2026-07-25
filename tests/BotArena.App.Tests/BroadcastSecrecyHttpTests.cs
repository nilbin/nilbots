using System.Text.Json;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Matches;
using Microsoft.EntityFrameworkCore;

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
        Guid matchSetId;
        Guid winnerBotId;
        List<Guid> matchIds;
        await using (var db = await database.CreateMigratedContextAsync())
        {
            var owner = new User
            {
                DisplayName = "Broadcast Owner",
                Email = "broadcast-owner@example.test",
                PasswordHash = "not-used",
            };
            var winner = new Bot
            {
                OwnerUserId = owner.Id,
                Name = "Concealed Winner",
                Slug = "concealed-winner",
            };
            var loser = new Bot
            {
                OwnerUserId = owner.Id,
                Name = "Concealed Loser",
                Slug = "concealed-loser",
            };
            var set = new MatchSet
            {
                BotAId = winner.Id,
                BotBId = loser.Id,
                BotAVersionId = Guid.NewGuid(),
                BotBVersionId = Guid.NewGuid(),
                Status = MatchSetStatus.Completed,
                ScoreA = 4,
                ScoreB = 2,
                RatingChangeA = 12.5,
                RatingChangeB = -12.5,
                WinnerBotId = winner.Id,
            };
            var match = new Match
            {
                MapId = "arena-01",
                Seed = 42,
                Status = MatchStatus.Completed,
                MatchSetId = set.Id,
                SetGame = 1,
                WinnerSlot = 0,
                EndReason = "Elimination",
                EndTick = 10,
                ReplayHash = "secret-replay-hash",
                BroadcastStartedAt = DateTime.UtcNow.AddHours(1),
            };
            AddParticipants(match, set, winner, loser, owner.DisplayName);
            var matches = new List<Match> { match };
            for (int game = 2; game <= MatchSet.Games; game++)
            {
                var additional = new Match
                {
                    MapId = "arena-01",
                    Seed = 41 + game,
                    Status = MatchStatus.Completed,
                    MatchSetId = set.Id,
                    SetGame = game,
                    WinnerSlot = 0,
                    EndReason = "Elimination",
                    EndTick = 10,
                    ReplayHash = $"secret-replay-hash-{game}",
                    BroadcastStartedAt = DateTime.UtcNow.AddHours(1),
                };
                AddParticipants(
                    additional,
                    set,
                    winner,
                    loser,
                    owner.DisplayName);
                matches.Add(additional);
            }
            db.Users.Add(owner);
            db.Bots.AddRange(winner, loser);
            db.MatchSets.Add(set);
            db.Matches.AddRange(matches);
            await db.SaveChangesAsync();
            matchId = match.Id;
            matchSetId = set.Id;
            winnerBotId = winner.Id;
            matchIds = matches.Select(candidate => candidate.Id).ToList();
        }

        using var factory = new BotArenaApplicationFactory(database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        using (JsonDocument concealed = await GetJsonAsync(
            client,
            $"/api/matches/{matchId}"))
        {
            AssertMatchConcealed(concealed.RootElement);
            JsonElement participant =
                concealed.RootElement.GetProperty("participants")[0];
            AssertNull(participant, "outcome");
            AssertNull(participant, "finalHealth");
            AssertNull(participant, "damageDealt");
            AssertNull(participant, "faults");
        }
        using (JsonDocument feed = await GetJsonAsync(
            client,
            "/api/matches?take=25"))
        {
            JsonElement concealed = feed.RootElement
                .EnumerateArray()
                .Single(candidate =>
                    candidate.GetProperty("id").GetGuid() == matchId);
            AssertMatchConcealed(concealed);
            AssertNull(concealed.GetProperty("participants")[0], "outcome");
            AssertNull(concealed.GetProperty("participants")[0], "finalHealth");
        }
        using (JsonDocument set = await GetJsonAsync(
            client,
            $"/api/matchsets/{matchSetId}"))
        {
            Assert.False(set.RootElement.GetProperty("revealed").GetBoolean());
            AssertNull(set.RootElement, "scoreA");
            AssertNull(set.RootElement, "scoreB");
            AssertNull(set.RootElement, "ratingChangeA");
            AssertNull(set.RootElement, "ratingChangeB");
            AssertNull(set.RootElement, "winnerBotId");
            AssertNull(set.RootElement.GetProperty("games")[0], "winnerBotId");
            Assert.False(
                set.RootElement.GetProperty("games")[0]
                    .GetProperty("draw")
                    .GetBoolean());
        }
        using (JsonDocument history = await GetJsonAsync(
            client,
            $"/api/bots/{winnerBotId}/matches"))
        {
            Assert.Equal(0, history.RootElement.GetProperty("wins").GetInt32());
            AssertNull(
                history.RootElement.GetProperty("matches")[0],
                "outcome");
        }
        using (JsonDocument live = await GetJsonAsync(
            client,
            $"/api/matches/{matchId}/live"))
        {
            AssertNull(live.RootElement, "totalTicks");
            Assert.False(
                live.RootElement.GetProperty("broadcastComplete").GetBoolean());
        }

        await using (var db = database.CreateContext())
        {
            List<Match> matches = await db.Matches
                .Where(match => matchIds.Contains(match.Id))
                .ToListAsync();
            Assert.Equal(MatchSet.Games, matches.Count);
            foreach (Match match in matches)
                match.BroadcastStartedAt = DateTime.UtcNow.AddHours(-1);
            await db.SaveChangesAsync();
        }

        using JsonDocument revealed = await GetJsonAsync(
            client,
            $"/api/matches/{matchId}");
        Assert.Equal(0, revealed.RootElement.GetProperty("winnerSlot").GetInt32());
        Assert.Equal("Elimination",
            revealed.RootElement.GetProperty("endReason").GetString());
        Assert.Equal(10, revealed.RootElement.GetProperty("endTick").GetInt32());
        Assert.Equal(
            "secret-replay-hash",
            revealed.RootElement.GetProperty("replayHash").GetString());
        JsonElement revealedParticipant =
            revealed.RootElement.GetProperty("participants")[0];
        Assert.Equal("Win", revealedParticipant.GetProperty("outcome").GetString());
        Assert.Equal(2, revealedParticipant.GetProperty("finalHealth").GetInt32());
        Assert.Equal(3, revealedParticipant.GetProperty("damageDealt").GetInt32());
        Assert.Equal(0, revealedParticipant.GetProperty("faults").GetInt32());

        using JsonDocument revealedLive = await GetJsonAsync(
            client,
            $"/api/matches/{matchId}/live");
        Assert.Equal(
            11,
            revealedLive.RootElement.GetProperty("totalTicks").GetInt32());
        Assert.True(
            revealedLive.RootElement.GetProperty("broadcastComplete").GetBoolean());

        using JsonDocument revealedFeed = await GetJsonAsync(
            client,
            "/api/matches?take=25");
        JsonElement revealedSummary = revealedFeed.RootElement
            .EnumerateArray()
            .Single(candidate =>
                candidate.GetProperty("id").GetGuid() == matchId);
        Assert.Equal(
            0,
            revealedSummary.GetProperty("winnerSlot").GetInt32());
        Assert.Equal(
            "Win",
            revealedSummary.GetProperty("participants")[0]
                .GetProperty("outcome")
                .GetString());

        using JsonDocument revealedSet = await GetJsonAsync(
            client,
            $"/api/matchsets/{matchSetId}");
        Assert.True(revealedSet.RootElement.GetProperty("revealed").GetBoolean());
        Assert.Equal(4, revealedSet.RootElement.GetProperty("scoreA").GetDouble());
        Assert.Equal(
            winnerBotId,
            revealedSet.RootElement.GetProperty("winnerBotId").GetGuid());
        Assert.Equal(
            12.5,
            revealedSet.RootElement.GetProperty("ratingChangeA").GetDouble());
        Assert.Equal(
            winnerBotId,
            revealedSet.RootElement.GetProperty("games")[0]
                .GetProperty("winnerBotId")
                .GetGuid());

        using JsonDocument revealedHistory = await GetJsonAsync(
            client,
            $"/api/bots/{winnerBotId}/matches");
        Assert.Equal(
            MatchSet.Games,
            revealedHistory.RootElement.GetProperty("wins").GetInt32());
        Assert.Equal(
            "Win",
            revealedHistory.RootElement.GetProperty("matches")[0]
                .GetProperty("outcome")
                .GetString());
    }

    private static void AddParticipants(
        Match match,
        MatchSet set,
        Bot winner,
        Bot loser,
        string ownerDisplayName)
    {
        match.Participants.Add(new MatchParticipant
        {
            MatchId = match.Id,
            Slot = 0,
            BotId = winner.Id,
            BotVersionId = set.BotAVersionId,
            NameSnapshot = winner.Name,
            OwnerDisplayNameSnapshot = ownerDisplayName,
            AccentSnapshot = winner.Accent,
            Outcome = "Win",
            FinalHealth = 2,
            DamageDealt = 3,
            Faults = 0,
        });
        match.Participants.Add(new MatchParticipant
        {
            MatchId = match.Id,
            Slot = 1,
            BotId = loser.Id,
            BotVersionId = set.BotBVersionId,
            NameSnapshot = loser.Name,
            OwnerDisplayNameSnapshot = ownerDisplayName,
            AccentSnapshot = loser.Accent,
            Outcome = "Loss",
            FinalHealth = 0,
            DamageDealt = 1,
            Faults = 1,
        });
    }

    private static void AssertMatchConcealed(JsonElement match)
    {
        AssertNull(match, "winnerSlot");
        AssertNull(match, "endReason");
        AssertNull(match, "endTick");
        if (match.TryGetProperty("replayHash", out JsonElement replayHash))
            Assert.Equal(JsonValueKind.Null, replayHash.ValueKind);
    }

    private static void AssertNull(JsonElement element, string property) =>
        Assert.Equal(
            JsonValueKind.Null,
            element.GetProperty(property).ValueKind);

    private static async Task<JsonDocument> GetJsonAsync(
        HttpClient client,
        string path)
    {
        HttpResponseMessage response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }
}
