using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Jobs;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.Engine;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Tests;

[Collection(ApplicationHttpCollection.Name)]
public sealed class FrontlineLabsHttpTests
{
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task DisabledCatalogIsPresentButEmpty()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        await using (var migration =
                     await database.CreateMigratedContextAsync())
        {
            await new FrontlineLabsPlaylistSeeder(migration)
                .SeedAsync();
        }

        using var factory =
            new BotArenaApplicationFactory(database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        using JsonDocument response =
            await GetJsonAsync(client, "/api/labs");

        Assert.False(response.RootElement.GetProperty("enabled").GetBoolean());
        Assert.Empty(
            response.RootElement.GetProperty("playlists").EnumerateArray());
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task CreationRequiresAuthenticationAndAnEnabledFlag()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        Guid playlistVersionId;
        await using (var migration =
                     await database.CreateMigratedContextAsync())
        {
            playlistVersionId =
                (await new FrontlineLabsPlaylistSeeder(migration)
                    .SeedAsync()).Id;
        }

        var request = new
        {
            playlistVersionId,
            entrantBotIds = new[] { Guid.NewGuid(), Guid.NewGuid() },
            seed = 1,
        };
        using (var disabledFactory =
               new BotArenaApplicationFactory(
                   database.ConnectionString))
        using (HttpClient disabledClient =
               disabledFactory.CreateClient())
        {
            HttpResponseMessage anonymous =
                await disabledClient.PostAsJsonAsync(
                    "/api/labs/matches",
                    request);
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                anonymous.StatusCode);

            _ = await RegisterAsync(
                disabledClient,
                "labs-disabled@example.test",
                "Labs Disabled");
            HttpResponseMessage disabled =
                await disabledClient.PostAsJsonAsync(
                    "/api/labs/matches",
                    request);
            Assert.Equal(HttpStatusCode.NotFound, disabled.StatusCode);
        }

        using var enabledFactory = new BotArenaApplicationFactory(
            database.ConnectionString,
            frontlineLabsEnabled: true);
        using HttpClient enabledClient = enabledFactory.CreateClient();
        HttpResponseMessage enabledAnonymous =
            await enabledClient.PostAsJsonAsync(
                "/api/labs/matches",
                request);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            enabledAnonymous.StatusCode);
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task CreationRejectsForgedPlaylistAndWrongOwnerFirstEntrant()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        Guid playlistVersionId;
        await using (var migration =
                     await database.CreateMigratedContextAsync())
        {
            playlistVersionId =
                (await new FrontlineLabsPlaylistSeeder(migration)
                    .SeedAsync()).Id;
        }

        using var factory = new BotArenaApplicationFactory(
            database.ConnectionString,
            frontlineLabsEnabled: true);
        using HttpClient client = factory.CreateClient();
        Guid callerId = await RegisterAsync(
            client,
            "labs-guard@example.test",
            "Labs Guard");

        Guid callerBotId;
        Guid otherBotId;
        await using (var db = database.CreateContext())
        {
            var otherOwner = new User
            {
                DisplayName = "Other Labs Owner",
                Email = "other-labs-guard@example.test",
                PasswordHash = "not-used",
            };
            var callerBot = new Bot
            {
                OwnerUserId = callerId,
                Name = "Owned Labs Bot",
                Slug = "owned-labs-guard",
            };
            var otherBot = new Bot
            {
                OwnerUserId = otherOwner.Id,
                Name = "Other Labs Bot",
                Slug = "other-labs-guard",
            };
            db.AddRange(otherOwner, callerBot, otherBot);
            db.BotVersions.AddRange(
                GenericVersion(callerBot.Id, "owned-guard-artifact"),
                GenericVersion(otherBot.Id, "other-guard-artifact"));
            await db.SaveChangesAsync();
            callerBotId = callerBot.Id;
            otherBotId = otherBot.Id;
        }

        HttpResponseMessage wrongOwner = await client.PostAsJsonAsync(
            "/api/labs/matches",
            new
            {
                playlistVersionId,
                entrantBotIds = new[] { otherBotId, callerBotId },
                seed = 2,
            });
        Assert.Equal(HttpStatusCode.Forbidden, wrongOwner.StatusCode);

        HttpResponseMessage forged = await client.PostAsJsonAsync(
            "/api/labs/matches",
            new
            {
                playlistVersionId = Guid.NewGuid(),
                entrantBotIds = new[] { callerBotId, otherBotId },
                seed = 3,
            });
        Assert.Equal(HttpStatusCode.NotFound, forged.StatusCode);
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task EnabledCreationPinsExactUnrankedGenericIdentity()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        Guid playlistVersionId;
        await using (var migration =
                     await database.CreateMigratedContextAsync())
        {
            PlaylistVersion version =
                await new FrontlineLabsPlaylistSeeder(migration)
                    .SeedAsync();
            playlistVersionId = version.Id;
        }

        using var factory = new BotArenaApplicationFactory(
            database.ConnectionString,
            frontlineLabsEnabled: true);
        using HttpClient client = factory.CreateClient();
        Guid ownerId = await RegisterAsync(
            client,
            "labs-owner@example.test",
            "Labs Owner");

        Guid challengerId;
        Guid opponentId;
        await using (var db = database.CreateContext())
        {
            var opponentOwner = new User
            {
                DisplayName = "Labs Opponent Owner",
                Email = "labs-opponent@example.test",
                PasswordHash = "not-used",
            };
            var challenger = new Bot
            {
                OwnerUserId = ownerId,
                Name = "Labs Challenger",
                Slug = "labs-challenger",
            };
            var opponent = new Bot
            {
                OwnerUserId = opponentOwner.Id,
                Name = "Labs Opponent",
                Slug = "labs-opponent",
            };
            db.AddRange(opponentOwner, challenger, opponent);
            db.BotVersions.AddRange(
                GenericVersion(challenger.Id, "labs-alpha-artifact"),
                GenericVersion(opponent.Id, "labs-beta-artifact"));
            await db.SaveChangesAsync();
            challengerId = challenger.Id;
            opponentId = opponent.Id;
        }

        using JsonDocument catalog =
            await GetJsonAsync(client, "/api/labs");
        Assert.True(catalog.RootElement.GetProperty("enabled").GetBoolean());
        JsonElement playlist = Assert.Single(
            catalog.RootElement
                .GetProperty("playlists")
                .EnumerateArray());
        Assert.Equal(
            playlistVersionId,
            playlist.GetProperty("playlistVersionId").GetGuid());
        Assert.Equal(
            BotArenaVersions.GenericActorContractProfileId,
            playlist.GetProperty("requiredContractProfileId").GetString());
        Assert.Equal(2, playlist.GetProperty("participantCount").GetInt32());

        HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/labs/matches",
            new
            {
                playlistVersionId,
                entrantBotIds = new[] { challengerId, opponentId },
                seed = 42,
            });
        created.EnsureSuccessStatusCode();
        Guid matchId = await ReadIdAsync(created);

        await using (var db = database.CreateContext())
        {
            Match match = await db.Matches
                .Include(candidate => candidate.Participants)
                .SingleAsync(candidate => candidate.Id == matchId);
            FrontlineLabsPlaylistDefinition expected =
                FrontlineLabsPlaylistDefinition.Create();

            Assert.Null(match.MatchSetId);
            Assert.Equal(ownerId, match.InitiatedByUserId);
            Assert.Equal(playlistVersionId, match.PlaylistVersionId);
            Assert.Equal(expected.RulesetId, match.GameRulesVersion);
            Assert.Equal(expected.MapPoolId, match.MapId);
            Assert.Equal(42, match.Seed);
            Assert.Collection(
                match.Participants.OrderBy(value => value.Slot),
                participant =>
                {
                    Assert.Equal(0, participant.Slot);
                    Assert.Equal(0, participant.TeamId);
                    Assert.Equal(challengerId, participant.BotId);
                },
                participant =>
                {
                    Assert.Equal(1, participant.Slot);
                    Assert.Equal(1, participant.TeamId);
                    Assert.Equal(opponentId, participant.BotId);
                });
            Assert.False(await db.Ladders.AnyAsync(
                ladder =>
                    ladder.PlaylistVersionId == playlistVersionId));
            List<BackgroundJob> jobs = await db.BackgroundJobs
                .Where(job =>
                    job.Type ==
                    GenericActorMatchJobType.ForPlaylist(
                        FrontlineLabsPlaylistDefinition.PlaylistKey,
                        FrontlineLabsPlaylistDefinition.Version))
                .ToListAsync();
            Assert.Contains(
                jobs,
                job => job.PayloadId("matchId") == matchId);
            List<BackgroundJob> legacyJobs =
                await db.BackgroundJobs
                    .Where(job =>
                        job.Type == BackgroundJob.ExecuteMatchType)
                    .ToListAsync();
            Assert.DoesNotContain(
                legacyJobs,
                job => job.PayloadId("matchId") == matchId);
        }

        using JsonDocument feed =
            await GetJsonAsync(client, "/api/matches?take=25");
        Assert.DoesNotContain(
            feed.RootElement.EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == matchId);

        HttpResponseMessage second = await client.PostAsJsonAsync(
            "/api/labs/matches",
            new
            {
                playlistVersionId,
                entrantBotIds = new[] { challengerId, opponentId },
                seed = 43,
            });
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            second.StatusCode);
        Assert.Contains(
            "active Labs matches",
            await second.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task GenericAdmissionRejectsLegacyOnlyOpponent()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        Guid playlistVersionId;
        await using (var migration =
                     await database.CreateMigratedContextAsync())
        {
            playlistVersionId =
                (await new FrontlineLabsPlaylistSeeder(migration)
                    .SeedAsync()).Id;
        }

        using var factory = new BotArenaApplicationFactory(
            database.ConnectionString,
            frontlineLabsEnabled: true);
        using HttpClient client = factory.CreateClient();
        Guid ownerId = await RegisterAsync(
            client,
            "labs-profile@example.test",
            "Labs Profile");

        Guid genericBotId;
        Guid legacyBotId;
        await using (var db = database.CreateContext())
        {
            var other = new User
            {
                DisplayName = "Legacy Owner",
                Email = "legacy-labs@example.test",
                PasswordHash = "not-used",
            };
            var generic = new Bot
            {
                OwnerUserId = ownerId,
                Name = "Generic",
                Slug = "generic-labs-profile",
            };
            var legacy = new Bot
            {
                OwnerUserId = other.Id,
                Name = "Legacy",
                Slug = "legacy-labs-profile",
            };
            db.AddRange(other, generic, legacy);
            db.BotVersions.Add(
                GenericVersion(generic.Id, "generic-profile-artifact"));
            db.BotVersions.Add(new BotVersion
            {
                BotId = legacy.Id,
                VersionNumber = 1,
                EntryType = "Legacy",
                SourcesJson = "[]",
                SourceHash = "legacy-source",
                Status = BuildStatus.Built,
                ArtifactHash = "legacy-profile-artifact",
                SupportedContractProfiles =
                    [BotContractProfiles.LegacyDuel],
                IsActive = true,
            });
            await db.SaveChangesAsync();
            genericBotId = generic.Id;
            legacyBotId = legacy.Id;
        }

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/labs/matches",
            new
            {
                playlistVersionId,
                entrantBotIds = new[] { genericBotId, legacyBotId },
                seed = 7,
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains(
            ApplicationErrorCodes.MatchContractProfileRequired,
            body,
            StringComparison.Ordinal);
    }

    private static BotVersion GenericVersion(
        Guid botId,
        string artifactHash) =>
        new()
        {
            BotId = botId,
            VersionNumber = 1,
            EntryType = "GenericBot",
            SourcesJson = "[]",
            SourceHash = $"source-{artifactHash}",
            Status = BuildStatus.Built,
            ArtifactHash = artifactHash,
            SupportedContractProfiles =
                [BotArenaVersions.GenericActorContractProfileId],
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

    private static async Task<Guid> ReadIdAsync(
        HttpResponseMessage response)
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
