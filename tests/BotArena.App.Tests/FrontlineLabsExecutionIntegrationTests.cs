using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Cosmetics;
using BotArena.App.Jobs;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.App.Storage;
using BotArena.Engine;
using BotArena.Toolchain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BotArena.App.Tests;

[Collection(ApplicationHttpCollection.Name)]
public sealed class FrontlineLabsExecutionIntegrationTests
{
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task HostedWasmMatchPersistsV3AndWithholdsTerminalFacts()
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
        Guid ownerId = await RegisterAsync(client);

        string artifactPath = RepoPaths.FindUpward(
            Path.Combine("artifacts", "wasm", "builtin-bots.wasm"))
            ?? throw new InvalidOperationException(
                "The controlled built-in WASM artifact is required.");
        string artifactHash;
        await using (var artifact = File.OpenRead(artifactPath))
        {
            artifactHash = Convert.ToHexStringLower(
                await SHA256.HashDataAsync(artifact));
        }
        string artifactKey = ObjectKeys.Artifact(artifactHash);
        var objectStore =
            factory.Services.GetRequiredService<IObjectStore>();
        await using (var artifact = File.OpenRead(artifactPath))
        {
            await objectStore.PutAsync(
                artifactKey,
                artifact,
                artifactHash);
        }

        Guid challengerId;
        Guid opponentId;
        await using (var db = database.CreateContext())
        {
            var opponentOwner = new User
            {
                DisplayName = "Hosted Labs Opponent",
                Email = "hosted-labs-opponent@example.test",
                PasswordHash = "not-used",
            };
            var challenger = new Bot
            {
                OwnerUserId = ownerId,
                Name = "Hosted Rusher",
                Slug = "hosted-labs-rusher",
            };
            var opponent = new Bot
            {
                OwnerUserId = opponentOwner.Id,
                Name = "Hosted Counterpunch",
                Slug = "hosted-labs-counterpunch",
            };
            db.AddRange(opponentOwner, challenger, opponent);
            db.BotVersions.AddRange(
                GenericVersion(
                    challenger.Id,
                    artifactKey,
                    artifactHash,
                    "frontline-rusher"),
                GenericVersion(
                    opponent.Id,
                    artifactKey,
                    artifactHash,
                    "frontline-counterpunch"));
            await db.SaveChangesAsync();
            challengerId = challenger.Id;
            opponentId = opponent.Id;
        }

        HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/labs/matches",
            new
            {
                playlistVersionId,
                entrantBotIds = new[] { challengerId, opponentId },
                seed = 1729,
            });
        created.EnsureSuccessStatusCode();
        Guid matchId = await ReadIdAsync(created);

        await using (AsyncServiceScope scope =
                     factory.Services.CreateAsyncScope())
        {
            var legacyHandler = scope.ServiceProvider
                .GetRequiredService<MatchExecutionJobHandler>();
            InvalidOperationException wrongLane =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => legacyHandler.HandleAsync(
                        matchId,
                        CancellationToken.None));
            Assert.Contains(
                PlaylistExecutionPolicyIds.GenericActor,
                wrongLane.Message,
                StringComparison.Ordinal);
            await using (AppDbContext verifyPending =
                         database.CreateContext())
            {
                Assert.Equal(
                    MatchStatus.Pending,
                    (await verifyPending.Matches.SingleAsync(
                        candidate => candidate.Id == matchId)).Status);
            }

            var handler = scope.ServiceProvider
                .GetRequiredService<
                    GenericActorMatchExecutionJobHandler>();
            string executionJobType =
                GenericActorMatchJobType.ForPlaylist(
                    FrontlineLabsPlaylistDefinition.PlaylistKey,
                    FrontlineLabsPlaylistDefinition.Version);
            JobExecutionResult executed =
                await handler.HandleAsync(
                    matchId,
                    executionJobType,
                    CancellationToken.None);
            Assert.Equal("completed", executed.Outcome);

            JobExecutionResult retried =
                await handler.HandleAsync(
                    matchId,
                    executionJobType,
                    CancellationToken.None);
            Assert.Equal("already_completed", retried.Outcome);
        }

        string replayHash;
        await using (var db = database.CreateContext())
        {
            Match match = await db.Matches
                .Include(candidate => candidate.TeamResults)
                    .ThenInclude(result => result.Scores)
                .SingleAsync(candidate => candidate.Id == matchId);
            Assert.Equal(MatchStatus.Completed, match.Status);
            Assert.Equal(
                BotArenaVersions.GenericActorReplayFormatVersion,
                match.ReplayFormatVersion);
            Assert.Equal(2, match.TeamResults.Count);
            Assert.All(
                match.TeamResults,
                result =>
                    Assert.Contains(
                        result.Scores,
                        score =>
                            score.ScoreChannelId ==
                            "territorial-progress"));
            List<BackgroundJob> announcementJobs =
                await db.BackgroundJobs
                    .Where(job =>
                        job.Type ==
                        BackgroundJob.AnnounceMatchResultType)
                    .ToListAsync();
            Assert.DoesNotContain(
                announcementJobs,
                job => job.PayloadId("matchId") == matchId);
            Assert.False(await db.EntitlementGrants.AnyAsync(grant =>
                grant.UserId == ownerId &&
                grant.SourceKind == CosmeticUnlockEvents.Challenge &&
                grant.SourceId == CosmeticUnlockEvents.FirstUnrankedMatch));
            replayHash = Assert.IsType<string>(match.ReplayHash);
        }

        using (JsonDocument hiddenDetail =
               await GetJsonAsync(client, $"/api/matches/{matchId}"))
        {
            Assert.Equal(
                JsonValueKind.Null,
                hiddenDetail.RootElement
                    .GetProperty("replayFormatVersion")
                    .ValueKind);
            Assert.Empty(
                hiddenDetail.RootElement
                    .GetProperty("teamResults")
                    .EnumerateArray());
        }
        using (JsonDocument partial =
               await GetJsonAsync(client, $"/api/matches/{matchId}/replay"))
        {
            Assert.True(
                partial.RootElement.GetProperty("partial").GetBoolean());
            Assert.Equal(
                JsonValueKind.Null,
                partial.RootElement.GetProperty("result").ValueKind);
            Assert.Equal(
                JsonValueKind.Null,
                partial.RootElement.GetProperty("replayHash").ValueKind);
            Assert.Equal(
                JsonValueKind.Object,
                partial.RootElement.GetProperty("initialFrame").ValueKind);
        }

        await using (var db = database.CreateContext())
        {
            Match match = await db.Matches.SingleAsync(
                candidate => candidate.Id == matchId);
            match.BroadcastStartedAt = DateTime.UtcNow.AddMinutes(-10);
            await db.SaveChangesAsync();
        }

        using (JsonDocument revealedDetail =
               await GetJsonAsync(client, $"/api/matches/{matchId}"))
        {
            Assert.Equal(
                BotArenaVersions.GenericActorReplayFormatVersion,
                revealedDetail.RootElement
                    .GetProperty("replayFormatVersion")
                    .GetInt32());
            Assert.Equal(
                2,
                revealedDetail.RootElement
                    .GetProperty("teamResults")
                    .GetArrayLength());
        }
        using (JsonDocument complete =
               await GetJsonAsync(client, $"/api/matches/{matchId}/replay"))
        {
            Assert.False(
                complete.RootElement.GetProperty("partial").GetBoolean());
            Assert.Equal(
                replayHash,
                complete.RootElement.GetProperty("replayHash").GetString());
            Assert.Equal(
                JsonValueKind.Object,
                complete.RootElement.GetProperty("result").ValueKind);
        }
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task ExecutionCapabilityMustMatchPinnedPlaylistIdentity()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        Guid matchId;
        await using (AppDbContext db =
                     await database.CreateMigratedContextAsync())
        {
            PlaylistVersion playlistVersion =
                await new FrontlineLabsPlaylistSeeder(db).SeedAsync();
            Playlist playlist = await db.Playlists.SingleAsync(
                candidate =>
                    candidate.Id == playlistVersion.PlaylistId);
            playlist.Key = "tampered-frontline-labs";
            FrontlineLabsPlaylistDefinition definition =
                FrontlineLabsPlaylistDefinition.Create();
            var match = new Match
            {
                MapId = definition.Match.Map.Id,
                MapVersion = definition.Match.Map.Version,
                GameRulesVersion = definition.Match.Rules.RulesetId,
                RuntimeConfigurationVersion =
                    definition.Match.CapabilityVersions
                        .RuntimeConfigurationVersion,
                PlaylistVersionId = playlistVersion.Id,
                Seed = 81,
            };
            db.Matches.Add(match);
            await db.SaveChangesAsync();
            matchId = match.Id;
        }

        using var factory =
            new BotArenaApplicationFactory(database.ConnectionString);
        await using AsyncServiceScope scope =
            factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<
            GenericActorMatchExecutionJobHandler>();
        InvalidOperationException mismatch =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => handler.HandleAsync(
                    matchId,
                    GenericActorMatchJobType.ForPlaylist(
                        FrontlineLabsPlaylistDefinition.PlaylistKey,
                        FrontlineLabsPlaylistDefinition.Version),
                    CancellationToken.None));
        Assert.Contains(
            "does not match playlist",
            mismatch.Message,
            StringComparison.Ordinal);

        await using AppDbContext verify = database.CreateContext();
        Assert.Equal(
            MatchStatus.Pending,
            (await verify.Matches.SingleAsync(
                candidate => candidate.Id == matchId)).Status);
    }

    private static BotVersion GenericVersion(
        Guid botId,
        string artifactKey,
        string artifactHash,
        string guestBotName) =>
        new()
        {
            BotId = botId,
            VersionNumber = 1,
            EntryType = "BuiltInActorBot",
            SourcesJson = "[]",
            SourceHash = $"source-{guestBotName}",
            Status = BuildStatus.Built,
            ArtifactKey = artifactKey,
            ArtifactHash = artifactHash,
            SupportedContractProfiles =
                [BotArenaVersions.GenericActorContractProfileId],
            GuestBotName = guestBotName,
            IsActive = true,
        };

    private static async Task<Guid> RegisterAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/accounts/register",
            new
            {
                displayName = "Hosted Labs Owner",
                email = "hosted-labs-owner@example.test",
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
