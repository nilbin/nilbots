using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using BotArena.App.Accounts;
using BotArena.App.ArcRelay;
using BotArena.App.Bots;
using BotArena.App.Competition;
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
public sealed class ArcRelayEntrantProductIntegrationTests
{
    private const string MindSource = """
        using BotArena.Sdk;

        public sealed class ProductMind : IGenericMindBot
        {
            public void StartMatch(MindStart start) { }
            public void Think(MindContext mind)
            {
                foreach (MindBody body in mind.Bodies)
                    body.Hold("hosted product integration");
            }
            public void EndMatch(MindEnd end) { }
        }
        """;

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task Sheet_revision_keeps_rating_cap_is_combined_and_scrimmage_is_unrated()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using (AppDbContext migration = await database.CreateMigratedContextAsync()) { }
        using var factory = new BotArenaApplicationFactory(database.ConnectionString, legacyDuelEnabled: false);
        using HttpClient client = factory.CreateClient();
        await SeedAsync(factory);
        await RegisterAsync(client, "Sheet Owner");
        ArcRelayCatalogResponse catalog = (await client.GetFromJsonAsync<ArcRelayCatalogResponse>("/api/arc-relay/catalog"))!;
        ArcRelaySheetResponse[] sheets = [];
        for (int index = 0; index < 4; index++)
            sheets = [.. sheets, await SaveSheetAsync(client, $"Sheet {index + 1}", catalog.NewSheetTemplate)];

        for (int index = 0; index < 3; index++)
            (await client.PutAsJsonAsync($"/api/arc-relay/entrants/{sheets[index].Id}/ladder",
                new SetArcRelayLadderOptInRequest(true))).EnsureSuccessStatusCode();
        HttpResponseMessage overCap = await client.PutAsJsonAsync(
            $"/api/arc-relay/entrants/{sheets[3].Id}/ladder", new SetArcRelayLadderOptInRequest(true));
        Assert.Equal(HttpStatusCode.Conflict, overCap.StatusCode);

        HttpResponseMessage revisedResponse = await client.PutAsJsonAsync(
            $"/api/arc-relay/sheets/{sheets[0].Id}",
            new SaveArcRelaySheetRequest("Sheet 1 revised", sheets[0].Revision, catalog.NewSheetTemplate));
        revisedResponse.EnsureSuccessStatusCode();
        ArcRelaySheetResponse revised = (await revisedResponse.Content.ReadFromJsonAsync<ArcRelaySheetResponse>())!;
        Assert.Equal(sheets[0].Id, revised.Id);
        Assert.Equal(2, revised.Revision);
        Assert.Equal(BotRating.DefaultRating, revised.Entrant.Rating);

        HttpResponseMessage scrimmage = await client.PostAsJsonAsync("/api/arc-relay/scrimmages",
            new CreateArcRelayScrimmageRequest(sheets[0].Id, sheets[1].Id, 104729));
        scrimmage.EnsureSuccessStatusCode();
        CreatedMatchResponse created = (await scrimmage.Content.ReadFromJsonAsync<CreatedMatchResponse>())!;
        await using (AppDbContext db = database.CreateContext())
        {
            Match match = await db.Matches.SingleAsync(value => value.Id == created.Id);
            Assert.Equal(ArcRelayMatchLane.Scrimmage, match.ArcRelayLane);
            Assert.False(await db.ArcRelayRankedMatches.AnyAsync(value => value.MatchId == created.Id));
            ArcRelayEntrantRating rating = await db.ArcRelayEntrantRatings.SingleAsync(
                value => value.EntrantId == sheets[0].Id);
            Assert.Equal(BotRating.DefaultRating, rating.Rating);
            Assert.Equal(0, rating.RankedMatches);
        }

        string ladder = await client.GetStringAsync("/api/arc-relay/ladder");
        Assert.DoesNotContain("winnerSlot", ladder, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("outcome", ladder, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(created.Id.ToString(), ladder, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("outboundPath", ladder, StringComparison.Ordinal);
        Assert.DoesNotContain("rallyLines", ladder, StringComparison.Ordinal);
        string matchDetail = await client.GetStringAsync($"/api/matches/{created.Id}");
        Assert.DoesNotContain("outboundPath", matchDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("rallyLines", matchDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("mindData", matchDetail, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task Custom_mind_uses_real_build_and_sandboxed_preflight_before_ladder()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using (AppDbContext migration = await database.CreateMigratedContextAsync()) { }
        using var factory = new BotArenaApplicationFactory(database.ConnectionString, legacyDuelEnabled: false);
        using HttpClient client = factory.CreateClient();
        await SeedAsync(factory);
        await RegisterAsync(client, "Mind Owner");
        ArcRelayCatalogResponse catalog = (await client.GetFromJsonAsync<ArcRelayCatalogResponse>("/api/arc-relay/catalog"))!;
        string[] classes = catalog.NewSheetTemplate.Slots.OrderBy(value => value.UnitId)
            .Select(value => value.ClassId).ToArray();

        string[] invalidClasses = [.. classes];
        invalidClasses[0] = invalidClasses[1] = invalidClasses[2] = "kestrel";
        HttpResponseMessage invalid = await client.PostAsJsonAsync("/api/arc-relay/minds",
            MindRequest("Invalid declaration", invalidClasses));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        HttpResponseMessage submitted = await client.PostAsJsonAsync("/api/arc-relay/minds",
            MindRequest("Eightfold", classes));
        submitted.EnsureSuccessStatusCode();
        ArcRelayMindResponse mind = (await submitted.Content.ReadFromJsonAsync<ArcRelayMindResponse>())!;
        Guid versionId;
        await using (AppDbContext db = database.CreateContext())
        {
            ArcRelayEntrant entrant = await db.ArcRelayEntrants.SingleAsync(value => value.Id == mind.Entrant.Id);
            versionId = await db.BotVersions.Where(value => value.BotId == entrant.MindBotId)
                .Select(value => value.Id).SingleAsync();
        }
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            JobExecutionResult build = await scope.ServiceProvider.GetRequiredService<CompileSubmissionJobHandler>()
                .HandleAsync(versionId, CancellationToken.None);
            Assert.Equal("built", build.Outcome);
        }

        HttpResponseMessage beforePreflight = await client.PutAsJsonAsync(
            $"/api/arc-relay/entrants/{mind.Entrant.Id}/ladder", new SetArcRelayLadderOptInRequest(true));
        Assert.Equal(HttpStatusCode.Conflict, beforePreflight.StatusCode);
        HttpResponseMessage queued = await client.PostAsJsonAsync(
            $"/api/arc-relay/entrants/{mind.Entrant.Id}/preflight", new { });
        Assert.Equal(HttpStatusCode.Accepted, queued.StatusCode);
        ArcRelayPreflightResponse preflight = (await queued.Content.ReadFromJsonAsync<ArcRelayPreflightResponse>())!;
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            JobExecutionResult execution = await scope.ServiceProvider
                .GetRequiredService<GenericActorMatchExecutionJobHandler>().HandleAsync(
                    preflight.MatchId,
                    GenericActorMatchJobType.ForPlaylist(
                        ArcRelayEntrantPlaylistDefinition.PlaylistKey,
                        ArcRelayEntrantPlaylistDefinition.Version),
                    CancellationToken.None);
            Assert.Equal("completed", execution.Outcome);
        }

        string replayKey;
        await using (AppDbContext db = database.CreateContext())
        {
            ArcRelayEntrant entrant = await db.ArcRelayEntrants.SingleAsync(value => value.Id == mind.Entrant.Id);
            Assert.Equal(ArcRelayPreflightStatus.Passed, entrant.PreflightStatus);
            Match match = await db.Matches.Include(value => value.Participants)
                .SingleAsync(value => value.Id == preflight.MatchId);
            MatchParticipant candidate = match.Participants.Single(value => value.EntrantIdSnapshot == entrant.Id);
            Assert.Equal("mind", candidate.EntrantKindSnapshot);
            Assert.Null(candidate.MindDataSnapshot);
            Assert.Equal(0, candidate.Faults.GetValueOrDefault());
            replayKey = match.ReplayKey!;
        }
        IObjectStore store = factory.Services.GetRequiredService<IObjectStore>();
        await using Stream stored = (await store.OpenReadAsync(replayKey))!;
        await using var gzip = new GZipStream(stored, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        string replay = await reader.ReadToEndAsync();
        Assert.Contains("sandboxed-wasm-mind-v1", replay, StringComparison.Ordinal);

        HttpResponseMessage admitted = await client.PutAsJsonAsync(
            $"/api/arc-relay/entrants/{mind.Entrant.Id}/ladder", new SetArcRelayLadderOptInRequest(true));
        admitted.EnsureSuccessStatusCode();

        HttpResponseMessage revised = await client.PutAsJsonAsync($"/api/arc-relay/minds/{mind.Entrant.Id}",
            new ReviseArcRelayMindRequest(
                "Eightfold revised", 1, "ProductMind",
                [new SourceFileDto("ProductMind.cs", MindSource)],
                new ArcRelayCompositionDeclaration(classes)));
        Assert.Equal(HttpStatusCode.Accepted, revised.StatusCode);
        await using (AppDbContext db = database.CreateContext())
        {
            ArcRelayEntrant entrant = await db.ArcRelayEntrants.SingleAsync(value => value.Id == mind.Entrant.Id);
            ArcRelayEntrantRating rating = await db.ArcRelayEntrantRatings.SingleAsync(value => value.EntrantId == entrant.Id);
            Assert.Equal(BotRating.DefaultRating, rating.Rating);
            Assert.False(entrant.LadderOptedIn);
            Assert.Equal(ArcRelayPreflightStatus.Required, entrant.PreflightStatus);
            Assert.Equal(2, await db.BotVersions.CountAsync(value => value.BotId == entrant.MindBotId));
        }
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task Passive_pairing_is_cross_account_and_rating_waits_for_the_broadcast()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using (AppDbContext migration = await database.CreateMigratedContextAsync()) { }
        using var factory = new BotArenaApplicationFactory(database.ConnectionString, legacyDuelEnabled: false);
        using HttpClient firstClient = factory.CreateClient();
        using HttpClient secondClient = factory.CreateClient();
        await SeedAsync(factory);
        UserResponse firstOwner = await RegisterAsync(firstClient, "Ladder Alpha");
        UserResponse secondOwner = await RegisterAsync(secondClient, "Ladder Beta");
        ArcRelayCatalogResponse catalog = (await firstClient.GetFromJsonAsync<ArcRelayCatalogResponse>("/api/arc-relay/catalog"))!;
        ArcRelaySheetResponse first = await SaveSheetAsync(firstClient, "Alpha sheet", catalog.NewSheetTemplate);
        ArcRelaySheetDocument alternate = catalog.NewSheetTemplate with
        {
            Policies = catalog.NewSheetTemplate.Policies with
            {
                Escort = catalog.NewSheetTemplate.Policies.Escort with
                {
                    FollowDistance = Math.Min(4, catalog.NewSheetTemplate.Policies.Escort.FollowDistance + 1),
                },
            },
        };
        ArcRelaySheetResponse second = await SaveSheetAsync(secondClient, "Beta sheet", alternate);
        (await firstClient.PutAsJsonAsync($"/api/arc-relay/entrants/{first.Id}/ladder",
            new SetArcRelayLadderOptInRequest(true))).EnsureSuccessStatusCode();
        (await secondClient.PutAsJsonAsync($"/api/arc-relay/entrants/{second.Id}/ladder",
            new SetArcRelayLadderOptInRequest(true))).EnsureSuccessStatusCode();

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ArcRelayLadderPairingService>()
                .PairAsync(CancellationToken.None);
        Guid matchId;
        await using (AppDbContext db = database.CreateContext())
        {
            Match match = await db.Matches.Include(value => value.Participants)
                .SingleAsync(value => value.ArcRelayLane == ArcRelayMatchLane.Ranked);
            Assert.True(match.Participants
                .Select(value => db.ArcRelayEntrants.Single(entry => entry.Id == value.EntrantIdSnapshot).OwnerUserId)
                .ToHashSet().SetEquals([firstOwner.Id, secondOwner.Id]));
            matchId = match.Id;
        }
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            JobExecutionResult execution = await scope.ServiceProvider
                .GetRequiredService<GenericActorMatchExecutionJobHandler>().HandleAsync(
                    matchId,
                    GenericActorMatchJobType.ForPlaylist(
                        ArcRelayEntrantPlaylistDefinition.PlaylistKey,
                        ArcRelayEntrantPlaylistDefinition.Version),
                    CancellationToken.None);
            Assert.Equal("completed", execution.Outcome);
        }

        await using (AppDbContext db = database.CreateContext())
        {
            ArcRelayEntrant suspended = await db.ArcRelayEntrants.SingleAsync(value => value.Id == first.Id);
            ArcRelayEntrantSuspension.Apply(suspended, matchId, ["handoff ping-pong"], DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        ArcRelayLadderResponse concealed = (await firstClient.GetFromJsonAsync<ArcRelayLadderResponse>("/api/arc-relay/ladder"))!;
        Assert.All(concealed.Entrants, entry =>
        {
            Assert.Equal(BotRating.DefaultRating, entry.Rating);
            Assert.Equal(0, entry.RankedMatches);
        });
        ArcRelayEntrantCardResponse concealedSuspension = concealed.Entrants.Single(value => value.Id == first.Id);
        Assert.True(concealedSuspension.LadderOptedIn);
        Assert.Equal("ready", concealedSuspension.Status);
        Assert.Null(concealedSuspension.SuspensionReason);
        Assert.Null(concealedSuspension.SuspensionMatchId);
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider
                .GetRequiredService<ArcRelayRatingSettlementJobHandler>()
                .HandleAsync(matchId, CancellationToken.None));
        }
        await using (AppDbContext db = database.CreateContext())
        {
            Match match = await db.Matches.SingleAsync(value => value.Id == matchId);
            match.BroadcastStartedAt = DateTime.UtcNow.AddHours(-1);
            await db.SaveChangesAsync();
        }
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            JobExecutionResult settled = await scope.ServiceProvider
                .GetRequiredService<ArcRelayRatingSettlementJobHandler>()
                .HandleAsync(matchId, CancellationToken.None);
            Assert.Equal("settled", settled.Outcome);
        }
        ArcRelayLadderResponse revealed = (await firstClient.GetFromJsonAsync<ArcRelayLadderResponse>("/api/arc-relay/ladder"))!;
        Assert.All(revealed.Entrants, entry => Assert.Equal(1, entry.RankedMatches));
        Assert.Equal(BotRating.DefaultRating * 2, revealed.Entrants.Sum(value => value.Rating));
        ArcRelayEntrantCardResponse revealedSuspension = revealed.Entrants.Single(value => value.Id == first.Id);
        Assert.False(revealedSuspension.LadderOptedIn);
        Assert.Equal("suspended", revealedSuspension.Status);
        Assert.Equal("handoff ping-pong", revealedSuspension.SuspensionReason);
        Assert.Equal(matchId, revealedSuspension.SuspensionMatchId);
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task Current_cutover_migrates_legacy_sheets_and_carries_ratings_to_v5()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using (AppDbContext migration = await database.CreateMigratedContextAsync()) { }
        using var factory = new BotArenaApplicationFactory(database.ConnectionString, legacyDuelEnabled: false);
        using HttpClient client = factory.CreateClient();
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ArcRelayPlaylistSeeder>().SeedAsync();
        UserResponse owner = await RegisterAsync(client, "Map Migrant");
        Guid entrantId = Guid.NewGuid();
        Guid oldLadderId;

        await using (AppDbContext db = database.CreateContext())
        {
            Playlist playlist = await db.Playlists.SingleAsync(value =>
                value.Key == ArcRelayEntrantPlaylistDefinition.PlaylistKey);
            ArcRelayEntrantPlaylistDefinition historical =
                ArcRelayEntrantPlaylistDefinition.CreateHistoricalV2();
            var version = new PlaylistVersion
            {
                PlaylistId = playlist.Id,
                Version = ArcRelayEntrantPlaylistDefinition.HistoricalVersion,
                GameModeId = historical.Match.Rules.GameMode.ModeId,
                RulesetId = historical.Match.Rules.RulesetId,
                MatchFormatId = historical.Match.Format.FormatId,
                MapPoolId = historical.Match.Map.Id,
                SeriesPolicyId = ArcRelayEntrantPlaylistDefinition.SeriesPolicyId,
                MatchmakingPolicyId = ArcRelayEntrantPlaylistDefinition.MatchmakingPolicyId,
                AdmissionPolicyId = historical.AdmissionPolicyId,
                ExecutionPolicyId = historical.ExecutionPolicyId,
                ExecutionEngineVersion = historical.ExecutionEngineVersion,
                CanonicalDefinition = historical.CanonicalDefinition,
                DefinitionFingerprint = historical.DefinitionFingerprint,
                Provenance = historical.Provenance,
                Visibility = ArcRelayEntrantPlaylistDefinition.Visibility,
            };
            var season = new Season
            {
                Key = ArcRelayLadderPolicy.SeasonKey,
                DisplayName = ArcRelayLadderPolicy.SeasonName,
            };
            var ladder = new Ladder
            {
                PlaylistVersionId = version.Id,
                SeasonId = season.Id,
                Status = LadderStatus.Open,
                RatingPolicyId = ArcRelayEloV1.Id,
                IsListed = true,
                AwardsAchievements = false,
            };
            oldLadderId = ladder.Id;
            var codec = new ArcRelayPlayerSheetCodec(ArcRelayClassCatalog.Default);
            ArcRelaySheetCompilation current = codec.Compile(
                ArcRelayPlayerSheetCodec.NewSheetTemplate(),
                ArcRelayClassCatalog.Default.StarterIds,
                $"{entrantId}:r1");
            string legacyJson = current.CanonicalJson.Replace(
                ArcRelayLoopProfile.Current.MapId,
                ArcRelayLoopProfile.HomeGatesWide.MapId,
                StringComparison.Ordinal);
            string legacyHash = Convert.ToHexStringLower(SHA256.HashData(
                Encoding.UTF8.GetBytes(legacyJson)));
            db.PlaylistVersions.Add(version);
            db.Seasons.Add(season);
            db.Ladders.Add(ladder);
            db.ArcRelayEntrants.Add(new ArcRelayEntrant
            {
                Id = entrantId,
                OwnerUserId = owner.Id,
                Kind = ArcRelayEntrantKind.Sheet,
                Name = "Legacy line",
                LadderOptedIn = true,
                LadderOptedInAt = DateTime.UtcNow,
            });
            db.ArcRelaySheets.Add(new ArcRelaySheet
            {
                Id = entrantId,
                OwnerUserId = owner.Id,
                Name = "Legacy line",
                CanonicalJson = legacyJson,
                ContentHash = legacyHash,
            });
            db.ArcRelayEntrantRatings.Add(new ArcRelayEntrantRating
            {
                EntrantId = entrantId,
                LadderId = ladder.Id,
                Rating = 1337,
                RankedMatches = 9,
            });
            await db.SaveChangesAsync();
        }

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ArcRelayEntrantPlaylistSeeder>().SeedAsync();

        await using (AppDbContext db = database.CreateContext())
        {
            Ladder oldLadder = await db.Ladders.SingleAsync(value => value.Id == oldLadderId);
            Assert.Equal(LadderStatus.Closed, oldLadder.Status);
            Assert.False(oldLadder.IsListed);
            Ladder currentLadder = await (
                from ladder in db.Ladders
                join version in db.PlaylistVersions on ladder.PlaylistVersionId equals version.Id
                where version.Version == ArcRelayEntrantPlaylistDefinition.Version
                select ladder).SingleAsync();
            ArcRelayEntrantRating rating = await db.ArcRelayEntrantRatings.SingleAsync(value =>
                value.EntrantId == entrantId && value.LadderId == currentLadder.Id);
            Assert.Equal(1337, rating.Rating);
            Assert.Equal(9, rating.RankedMatches);
            ArcRelaySheet sheet = await db.ArcRelaySheets.SingleAsync(value => value.Id == entrantId);
            Assert.Equal(2, sheet.Revision);
            Assert.Equal(
                ArcRelayLoopProfile.Current.MapId,
                new ArcRelayPlayerSheetCodec(ArcRelayClassCatalog.Default)
                    .Read(sheet.CanonicalJson).MapId);
            Assert.True((await db.ArcRelayEntrants.SingleAsync(value => value.Id == entrantId)).LadderOptedIn);
        }
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task Stock_recovery_cutover_preserves_v4_minds_preflight_and_rating()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using (AppDbContext migration =
            await database.CreateMigratedContextAsync()) { }
        using var factory = new BotArenaApplicationFactory(
            database.ConnectionString,
            legacyDuelEnabled: false);
        using HttpClient client = factory.CreateClient();
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ArcRelayPlaylistSeeder>()
                .SeedAsync();
        UserResponse owner = await RegisterAsync(client, "Recovery Migrant");
        Guid entrantId = Guid.NewGuid();
        Guid oldLadderId;

        await using (AppDbContext db = database.CreateContext())
        {
            Playlist playlist = await db.Playlists.SingleAsync(value =>
                value.Key == ArcRelayEntrantPlaylistDefinition.PlaylistKey);
            ArcRelayEntrantPlaylistDefinition historical =
                ArcRelayEntrantPlaylistDefinition.CreateHistoricalV4();
            var version = new PlaylistVersion
            {
                PlaylistId = playlist.Id,
                Version = ArcRelayEntrantPlaylistDefinition.PreviousVersion,
                GameModeId = historical.Match.Rules.GameMode.ModeId,
                RulesetId = historical.Match.Rules.RulesetId,
                MatchFormatId = historical.Match.Format.FormatId,
                MapPoolId = historical.Match.Map.Id,
                SeriesPolicyId = ArcRelayEntrantPlaylistDefinition.SeriesPolicyId,
                MatchmakingPolicyId =
                    ArcRelayEntrantPlaylistDefinition.MatchmakingPolicyId,
                AdmissionPolicyId = historical.AdmissionPolicyId,
                ExecutionPolicyId = historical.ExecutionPolicyId,
                ExecutionEngineVersion = historical.ExecutionEngineVersion,
                CanonicalDefinition = historical.CanonicalDefinition,
                DefinitionFingerprint = historical.DefinitionFingerprint,
                Provenance = historical.Provenance,
                Visibility = ArcRelayEntrantPlaylistDefinition.Visibility,
            };
            var season = new Season
            {
                Key = ArcRelayLadderPolicy.SeasonKey,
                DisplayName = ArcRelayLadderPolicy.SeasonName,
            };
            var ladder = new Ladder
            {
                PlaylistVersionId = version.Id,
                SeasonId = season.Id,
                Status = LadderStatus.Open,
                RatingPolicyId = ArcRelayEloV1.Id,
                IsListed = true,
                AwardsAchievements = false,
            };
            Guid mindBotId = await db.Bots
                .Where(value => value.Slug ==
                    ArcRelayPlaylistSeeder.ForwardStockBotSlug)
                .Select(value => value.Id)
                .SingleAsync();
            ArcRelayCompositionCompilation composition =
                ArcRelayComposition.Compile(
                    new ArcRelayCompositionDeclaration(
                        ArcRelayClassCatalog.Default.StarterIds.ToArray()),
                    new ArcRelayPlayerSheetCodec(ArcRelayClassCatalog.Default),
                    ArcRelayClassCatalog.Default.StarterIds);
            oldLadderId = ladder.Id;
            db.PlaylistVersions.Add(version);
            db.Seasons.Add(season);
            db.Ladders.Add(ladder);
            db.ArcRelayEntrants.Add(new ArcRelayEntrant
            {
                Id = entrantId,
                OwnerUserId = owner.Id,
                Kind = ArcRelayEntrantKind.CustomMind,
                Name = "Prepared mind",
                MindBotId = mindBotId,
                CompositionJson = composition.CanonicalJson,
                CompositionHash = composition.ContentHash,
                PreflightStatus = ArcRelayPreflightStatus.Passed,
                PreflightRevision = 3,
                LadderOptedIn = true,
                LadderOptedInAt = DateTime.UtcNow,
            });
            db.ArcRelayEntrantRatings.Add(new ArcRelayEntrantRating
            {
                EntrantId = entrantId,
                LadderId = ladder.Id,
                Rating = 1462,
                RankedMatches = 17,
            });
            await db.SaveChangesAsync();
        }

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider
                .GetRequiredService<ArcRelayEntrantPlaylistSeeder>()
                .SeedAsync();

        await using (AppDbContext db = database.CreateContext())
        {
            Ladder oldLadder = await db.Ladders.SingleAsync(value =>
                value.Id == oldLadderId);
            Assert.Equal(LadderStatus.Closed, oldLadder.Status);
            Assert.False(oldLadder.IsListed);
            Ladder currentLadder = await (
                from ladder in db.Ladders
                join version in db.PlaylistVersions
                    on ladder.PlaylistVersionId equals version.Id
                where version.Version == ArcRelayEntrantPlaylistDefinition.Version
                select ladder).SingleAsync();
            ArcRelayEntrantRating rating = await db.ArcRelayEntrantRatings
                .SingleAsync(value => value.EntrantId == entrantId &&
                    value.LadderId == currentLadder.Id);
            Assert.Equal(1462, rating.Rating);
            Assert.Equal(17, rating.RankedMatches);
            ArcRelayEntrant entrant = await db.ArcRelayEntrants.SingleAsync(
                value => value.Id == entrantId);
            Assert.Equal(ArcRelayPreflightStatus.Passed, entrant.PreflightStatus);
            Assert.Equal(3, entrant.PreflightRevision);
            Assert.True(entrant.LadderOptedIn);
        }
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task Production_gate_retires_legacy_duel_admission_without_deleting_reads()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using (AppDbContext migration = await database.CreateMigratedContextAsync()) { }
        using var factory = new BotArenaApplicationFactory(database.ConnectionString, legacyDuelEnabled: false);
        using HttpClient client = factory.CreateClient();
        await RegisterAsync(client, "Archive Reader");

        HttpResponseMessage challenge = await client.PostAsJsonAsync("/api/matches/challenge", new
        {
            botId = Guid.NewGuid(), opponentBotId = Guid.NewGuid(), mapId = "arena-01",
        });
        HttpResponseMessage ranked = await client.PostAsJsonAsync("/api/matches/ranked", new
        {
            botId = Guid.NewGuid(),
        });

        Assert.Equal(HttpStatusCode.Gone, challenge.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, ranked.StatusCode);
        Assert.Contains("retired", await challenge.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/bots")).StatusCode);
    }

    private static CreateArcRelayMindRequest MindRequest(string name, string[] classes) => new(
        name,
        "ProductMind",
        [new SourceFileDto("ProductMind.cs", MindSource)],
        new ArcRelayCompositionDeclaration(classes));

    private static async Task SeedAsync(BotArenaApplicationFactory factory)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ArcRelayPlaylistSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<ArcRelayEntrantPlaylistSeeder>().SeedAsync();
    }

    private static async Task<UserResponse> RegisterAsync(HttpClient client, string displayName)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/accounts/register", new RegisterRequest(
            displayName, $"{displayName.Replace(' ', '-').ToLowerInvariant()}-{Guid.NewGuid():N}@example.test",
            "correct-horse-battery-staple"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserResponse>())!;
    }

    private static async Task<ArcRelaySheetResponse> SaveSheetAsync(
        HttpClient client, string name, ArcRelaySheetDocument document)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/arc-relay/sheets",
            new SaveArcRelaySheetRequest(name, null, document));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ArcRelaySheetResponse>())!;
    }
}
