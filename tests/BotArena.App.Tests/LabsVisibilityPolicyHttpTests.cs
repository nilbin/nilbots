using System.Text.Json;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.Engine;

namespace BotArena.App.Tests;

[Collection(ApplicationHttpCollection.Name)]
public sealed class LabsVisibilityPolicyHttpTests
{
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task LegacySurfacesHideLabsButKeepNonLabsGenericMatches()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        Guid botId;
        Guid labsMatchId;
        Guid publicGenericMatchId;
        await using (AppDbContext db =
                     await database.CreateMigratedContextAsync())
        {
            PlaylistVersion labs =
                await new FrontlineLabsPlaylistSeeder(db).SeedAsync();
            PlaylistVersion publicGeneric =
                AddPublicGenericPlaylist(db);
            var owner = new User
            {
                DisplayName = "Visibility Owner",
                Email = "visibility-owner@example.test",
                PasswordHash = "not-used",
            };
            var opponentOwner = new User
            {
                DisplayName = "Visibility Opponent",
                Email = "visibility-opponent@example.test",
                PasswordHash = "not-used",
            };
            var bot = new Bot
            {
                OwnerUserId = owner.Id,
                Name = "Visible Generic",
                Slug = "visible-generic",
            };
            var opponent = new Bot
            {
                OwnerUserId = opponentOwner.Id,
                Name = "Visibility Counter",
                Slug = "visibility-counter",
            };
            db.AddRange(owner, opponentOwner, bot, opponent);

            Match labsMatch = AddCompletedGenericMatch(
                db,
                labs.Id,
                bot,
                opponent,
                owner.Id,
                "frontline-labs-policy",
                damageDealt: 91);
            Match publicMatch = AddCompletedGenericMatch(
                db,
                publicGeneric.Id,
                bot,
                opponent,
                owner.Id,
                "public-generic-policy",
                damageDealt: 7);
            await db.SaveChangesAsync();

            botId = bot.Id;
            labsMatchId = labsMatch.Id;
            publicGenericMatchId = publicMatch.Id;
        }

        using var factory =
            new BotArenaApplicationFactory(database.ConnectionString);
        using HttpClient client = factory.CreateClient();

        using (JsonDocument feed =
               await GetJsonAsync(client, "/api/matches?take=25"))
        {
            Guid[] ids = feed.RootElement
                .EnumerateArray()
                .Select(item => item.GetProperty("id").GetGuid())
                .ToArray();
            Assert.Contains(publicGenericMatchId, ids);
            Assert.DoesNotContain(labsMatchId, ids);
        }

        using (JsonDocument history = await GetJsonAsync(
                   client,
                   $"/api/bots/{botId}/matches"))
        {
            JsonElement row = Assert.Single(
                history.RootElement
                    .GetProperty("matches")
                    .EnumerateArray());
            Assert.Equal(
                publicGenericMatchId,
                row.GetProperty("id").GetGuid());
            Assert.Equal(
                1,
                history.RootElement.GetProperty("wins").GetInt32());
        }

        using (JsonDocument statistics = await GetJsonAsync(
                   client,
                   $"/api/bots/{botId}/stats"))
        {
            Assert.Equal(
                1,
                statistics.RootElement
                    .GetProperty("unranked")
                    .GetProperty("played")
                    .GetInt32());
            Assert.Equal(
                1,
                statistics.RootElement
                    .GetProperty("unranked")
                    .GetProperty("wins")
                    .GetInt32());
            Assert.Equal(
                1,
                statistics.RootElement
                    .GetProperty("combat")
                    .GetProperty("games")
                    .GetInt32());
            Assert.Equal(
                7,
                statistics.RootElement
                    .GetProperty("combat")
                    .GetProperty("damageDealt")
                    .GetInt32());
        }
    }

    private static PlaylistVersion AddPublicGenericPlaylist(
        AppDbContext db)
    {
        FrontlineLabsPlaylistDefinition definition =
            FrontlineLabsPlaylistDefinition.Create();
        var playlist = new Playlist
        {
            Key = "public-generic-policy-test",
            DisplayName = "Public Generic Policy Test",
        };
        var version = new PlaylistVersion
        {
            PlaylistId = playlist.Id,
            Version = 1,
            GameModeId = definition.GameModeId,
            RulesetId = definition.RulesetId,
            MatchFormatId = definition.MatchFormatId,
            MapPoolId = definition.MapPoolId,
            SeriesPolicyId =
                FrontlineLabsPlaylistDefinition.SeriesPolicyId,
            MatchmakingPolicyId =
                FrontlineLabsPlaylistDefinition.MatchmakingPolicyId,
            AdmissionPolicyId =
                BotArenaVersions.GenericActorContractProfileId,
            ExecutionPolicyId =
                PlaylistExecutionPolicyIds.GenericActor,
            ExecutionEngineVersion =
                BotArenaVersions.GenericActorEngineVersion,
            CanonicalDefinition = definition.CanonicalDefinition,
            DefinitionFingerprint = definition.DefinitionFingerprint,
            Provenance = definition.Provenance,
            Visibility = "public",
        };
        db.Playlists.Add(playlist);
        db.PlaylistVersions.Add(version);
        return version;
    }

    private static Match AddCompletedGenericMatch(
        AppDbContext db,
        Guid playlistVersionId,
        Bot bot,
        Bot opponent,
        Guid initiatedByUserId,
        string mapId,
        int damageDealt)
    {
        var match = new Match
        {
            MapId = mapId,
            Seed = 1,
            Status = MatchStatus.Completed,
            WinnerSlot = 0,
            EndReason = "test-complete",
            EndTick = 1,
            ReplayFormatVersion =
                BotArenaVersions.GenericActorReplayFormatVersion,
            CompletedAt = DateTime.UtcNow.AddHours(-1),
            BroadcastStartedAt = DateTime.UtcNow.AddHours(-1),
            PlaylistVersionId = playlistVersionId,
            InitiatedByUserId = initiatedByUserId,
        };
        match.Participants.Add(new MatchParticipant
        {
            MatchId = match.Id,
            Slot = 0,
            TeamId = 0,
            BotId = bot.Id,
            BotVersionId = Guid.NewGuid(),
            NameSnapshot = bot.Name,
            AccentSnapshot = bot.Accent,
            Outcome = "Win",
            FinalHealth = 3,
            DamageDealt = damageDealt,
            Faults = 0,
        });
        match.Participants.Add(new MatchParticipant
        {
            MatchId = match.Id,
            Slot = 1,
            TeamId = 1,
            BotId = opponent.Id,
            BotVersionId = Guid.NewGuid(),
            NameSnapshot = opponent.Name,
            AccentSnapshot = opponent.Accent,
            Outcome = "Loss",
            FinalHealth = 0,
            DamageDealt = 1,
            Faults = 0,
        });
        db.Matches.Add(match);
        return match;
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
