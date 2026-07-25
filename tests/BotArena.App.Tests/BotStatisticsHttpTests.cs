using System.Net;
using System.Text.Json;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Matches;
using BotArena.App.Shared;

namespace BotArena.App.Tests;

[Collection(ApplicationHttpCollection.Name)]
public class BotStatisticsHttpTests
{
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task PublicStats_SeparateSetsFromChallengesAndConcealBroadcasts()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        Guid botId;
        await using (AppDbContext db = await database.CreateMigratedContextAsync())
        {
            var owner = new User
            {
                DisplayName = "Stats Owner",
                Email = "stats-owner@example.test",
                PasswordHash = "not-used",
            };
            var opponentOwner = new User
            {
                DisplayName = "Opponent Owner",
                Email = "stats-opponent@example.test",
                PasswordHash = "not-used",
            };
            var bot = new Bot
            {
                OwnerUserId = owner.Id,
                Name = "Record Keeper",
                Slug = "record-keeper",
            };
            var opponent = new Bot
            {
                OwnerUserId = opponentOwner.Id,
                Name = "Counter",
                Slug = "counter",
            };
            db.AddRange(owner, opponentOwner, bot, opponent);

            MatchSet revealedSet = AddRankedSet(
                db,
                bot,
                opponent,
                winnerBotId: bot.Id,
                broadcastStartedAt: DateTime.UtcNow.AddHours(-1),
                damagePerGame: 2,
                faultsPerGame: 1);
            AddRankedSet(
                db,
                bot,
                opponent,
                winnerBotId: opponent.Id,
                broadcastStartedAt: DateTime.UtcNow.AddHours(-1),
                damagePerGame: 100,
                faultsPerGame: 100,
                finalGameBroadcastStartedAt: DateTime.UtcNow.AddHours(1));
            AddUnranked(
                db,
                bot,
                opponent,
                owner.Id,
                winnerBotId: opponent.Id,
                broadcastStartedAt: DateTime.UtcNow.AddHours(-1),
                damage: 3,
                faults: 2);
            AddUnranked(
                db,
                bot,
                opponent,
                owner.Id,
                winnerBotId: bot.Id,
                broadcastStartedAt: DateTime.UtcNow.AddHours(1),
                damage: 100,
                faults: 100);
            AddUnranked(
                db,
                bot,
                opponent,
                initiatedByUserId: null,
                winnerBotId: bot.Id,
                broadcastStartedAt: DateTime.UtcNow.AddHours(-1),
                damage: 100,
                faults: 100);

            await db.SaveChangesAsync();
            Assert.Equal(MatchSetStatus.Completed, revealedSet.Status);
            botId = bot.Id;
        }

        using var factory = new BotArenaApplicationFactory(database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        using JsonDocument document = await GetStatsAsync(client, botId);
        JsonElement root = document.RootElement;

        AssertRecord(root.GetProperty("ranked"), played: 1, wins: 1, losses: 0, draws: 0);
        AssertRecord(root.GetProperty("unranked"), played: 1, wins: 0, losses: 1, draws: 0);
        AssertRecord(root.GetProperty("overall"), played: 2, wins: 1, losses: 1, draws: 0);

        JsonElement combat = root.GetProperty("combat");
        Assert.Equal(7, combat.GetProperty("games").GetInt32());
        Assert.Equal(15, combat.GetProperty("damageDealt").GetInt32());
        Assert.Equal(8, combat.GetProperty("faults").GetInt32());

        HttpResponseMessage missing = await client.GetAsync(
            $"/api/bots/{Guid.NewGuid()}/stats");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private static MatchSet AddRankedSet(
        AppDbContext db,
        Bot bot,
        Bot opponent,
        Guid? winnerBotId,
        DateTime broadcastStartedAt,
        int damagePerGame,
        int faultsPerGame,
        DateTime? finalGameBroadcastStartedAt = null)
    {
        var set = new MatchSet
        {
            BotAId = bot.Id,
            BotBId = opponent.Id,
            BotAVersionId = Guid.NewGuid(),
            BotBVersionId = Guid.NewGuid(),
            Status = MatchSetStatus.Completed,
            ScoreA = winnerBotId == bot.Id ? 4 : 2,
            ScoreB = winnerBotId == bot.Id ? 2 : 4,
            WinnerBotId = winnerBotId,
            CompletedAt = DateTime.UtcNow,
        };
        db.MatchSets.Add(set);
        for (int game = 1; game <= MatchSet.Games; game++)
        {
            AddGame(
                db,
                bot,
                opponent,
                set.Id,
                initiatedByUserId: null,
                winnerBotId,
                game == MatchSet.Games && finalGameBroadcastStartedAt is DateTime finalBroadcast
                    ? finalBroadcast
                    : broadcastStartedAt,
                damagePerGame,
                faultsPerGame,
                game);
        }
        return set;
    }

    private static void AddUnranked(
        AppDbContext db,
        Bot bot,
        Bot opponent,
        Guid? initiatedByUserId,
        Guid? winnerBotId,
        DateTime broadcastStartedAt,
        int damage,
        int faults)
    {
        AddGame(
            db,
            bot,
            opponent,
            matchSetId: null,
            initiatedByUserId,
            winnerBotId,
            broadcastStartedAt,
            damage,
            faults,
            setGame: null);
    }

    private static void AddGame(
        AppDbContext db,
        Bot bot,
        Bot opponent,
        Guid? matchSetId,
        Guid? initiatedByUserId,
        Guid? winnerBotId,
        DateTime broadcastStartedAt,
        int damage,
        int faults,
        int? setGame)
    {
        var match = new Match
        {
            MapId = "arena-01",
            Seed = setGame ?? 1,
            Status = MatchStatus.Completed,
            WinnerSlot = winnerBotId is null ? null : winnerBotId == bot.Id ? 0 : 1,
            EndTick = 10,
            CompletedAt = DateTime.UtcNow,
            BroadcastStartedAt = broadcastStartedAt,
            MatchSetId = matchSetId,
            InitiatedByUserId = initiatedByUserId,
            SetGame = setGame,
        };
        match.Participants.Add(new MatchParticipant
        {
            MatchId = match.Id,
            Slot = 0,
            BotId = bot.Id,
            BotVersionId = Guid.NewGuid(),
            NameSnapshot = bot.Name,
            AccentSnapshot = bot.Accent,
            DamageDealt = damage,
            Faults = faults,
        });
        match.Participants.Add(new MatchParticipant
        {
            MatchId = match.Id,
            Slot = 1,
            BotId = opponent.Id,
            BotVersionId = Guid.NewGuid(),
            NameSnapshot = opponent.Name,
            AccentSnapshot = opponent.Accent,
            DamageDealt = 1,
            Faults = 0,
        });
        db.Matches.Add(match);
    }

    private static async Task<JsonDocument> GetStatsAsync(
        HttpClient client,
        Guid botId)
    {
        HttpResponseMessage response = await client.GetAsync(
            $"/api/bots/{botId}/stats");
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
    }

    private static void AssertRecord(
        JsonElement record,
        int played,
        int wins,
        int losses,
        int draws)
    {
        Assert.Equal(played, record.GetProperty("played").GetInt32());
        Assert.Equal(wins, record.GetProperty("wins").GetInt32());
        Assert.Equal(losses, record.GetProperty("losses").GetInt32());
        Assert.Equal(draws, record.GetProperty("draws").GetInt32());
    }
}
