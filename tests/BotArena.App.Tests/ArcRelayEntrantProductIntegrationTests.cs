using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using BotArena.App.Accounts;
using BotArena.App.ArcRelay;
using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Jobs;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.App.Sheets;
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
        TacticalSheetCatalogResponse catalog = (await client
            .GetFromJsonAsync<TacticalSheetCatalogResponse>(
                "/api/sheets/catalog"))!;
        TacticalSheetResponse[] sheets = [];
        for (int index = 0; index < 4; index++)
        {
            sheets = [.. sheets, await SaveSheetAsync(
                client,
                $"Sheet {index + 1}",
                catalog,
                enterLadder: index switch
                {
                    0 => null,
                    < 3 => true,
                    _ => false,
                })];
        }

        Assert.All(sheets.Take(3), sheet =>
            Assert.True(sheet.Entrant.LadderOptedIn));
        Assert.False(sheets[3].Entrant.LadderOptedIn);
        TacticalSheetSummaryResponse[] listed = (await client
            .GetFromJsonAsync<TacticalSheetSummaryResponse[]>("/api/sheets"))!;
        Assert.Equal(4, listed.Length);
        TacticalSheetResponse loaded = (await client
            .GetFromJsonAsync<TacticalSheetResponse>(
                $"/api/sheets/{sheets[0].Id}"))!;
        using (JsonDocument normalizedTactics = JsonDocument.Parse(
            loaded.TacticsJson))
        {
            Assert.Equal("arc-relay-tactics-v2", normalizedTactics.RootElement
                .GetProperty("schema").GetString());
            Assert.Equal(catalog.StarterSheets[0].Id, normalizedTactics.RootElement
                .GetProperty("sheetId").GetString());
        }
        Assert.Contains("arc-relay-orders-v3", loaded.OrdersJson);
        Assert.Contains("arc-relay-map-plan-v2", loaded.MapPlanJson);

        using HttpClient otherClient = factory.CreateClient();
        await RegisterAsync(otherClient, "Other Sheet Owner");
        Assert.Equal(HttpStatusCode.NotFound,
            (await otherClient.GetAsync($"/api/sheets/{sheets[0].Id}"))
                .StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await otherClient.DeleteAsync($"/api/sheets/{sheets[3].Id}"))
                .StatusCode);

        JsonObject invalid = JsonNode.Parse(
            catalog.StarterSheets[0].TacticsJson)!.AsObject();
        invalid["schema"] = "arc-relay-workshop-v1";
        HttpResponseMessage invalidSave = await client.PostAsJsonAsync(
            "/api/sheets",
            new SaveTacticalSheetRequest(
                "Invalid legacy document",
                null,
                invalid.ToJsonString(),
                false));
        Assert.Equal(HttpStatusCode.BadRequest, invalidSave.StatusCode);
        HttpResponseMessage retiredSave = await client.PostAsJsonAsync(
            "/api/arc-relay/sheets",
            new { });
        Assert.False(retiredSave.IsSuccessStatusCode);

        HttpResponseMessage overCap = await client.PutAsJsonAsync(
            $"/api/arc-relay/entrants/{sheets[3].Id}/ladder", new SetArcRelayLadderOptInRequest(true));
        Assert.Equal(HttpStatusCode.Conflict, overCap.StatusCode);

        HttpResponseMessage revisedResponse = await client.PutAsJsonAsync(
            $"/api/sheets/{sheets[0].Id}",
            new SaveTacticalSheetRequest(
                "Sheet 1 revised",
                sheets[0].Revision,
                catalog.StarterSheets[0].TacticsJson,
                EnterLadder: true));
        revisedResponse.EnsureSuccessStatusCode();
        TacticalSheetResponse revised = (await revisedResponse.Content
            .ReadFromJsonAsync<TacticalSheetResponse>())!;
        Assert.Equal(sheets[0].Id, revised.Id);
        Assert.Equal(2, revised.Revision);
        Assert.Equal(BotRating.DefaultRating, revised.Entrant.Rating);
        Assert.Equal(sheets[0].ContentHash, revised.ContentHash);
        HttpResponseMessage staleUpdate = await client.PutAsJsonAsync(
            $"/api/sheets/{sheets[0].Id}",
            new SaveTacticalSheetRequest(
                "Stale revision",
                sheets[0].Revision,
                catalog.StarterSheets[0].TacticsJson,
                true));
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);

        // A revised Tactics document rematerializes wholesale and changes the
        // compiled identity.
        JsonObject rework = JsonNode.Parse(
            catalog.StarterSheets[0].TacticsJson)!.AsObject();
        rework["tactics"]![1]!["finish"]!["maxAttempt"] = "long";
        HttpResponseMessage reworkUpdate = await client.PutAsJsonAsync(
            $"/api/sheets/{sheets[0].Id}",
            new SaveTacticalSheetRequest(
                "Sheet 1 revised again",
                revised.Revision,
                rework.ToJsonString(),
                EnterLadder: true));
        reworkUpdate.EnsureSuccessStatusCode();
        TacticalSheetResponse reworked = (await reworkUpdate.Content
            .ReadFromJsonAsync<TacticalSheetResponse>())!;
        Assert.NotEqual(revised.ContentHash, reworked.ContentHash);

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

        JsonElement[] relayFeed = (await client.GetFromJsonAsync<JsonElement[]>(
            "/api/matches?take=25&arcRelay=true"))!;
        Assert.Contains(relayFeed, value =>
            value.GetProperty("id").GetGuid() == created.Id);
        JsonElement[] legacyFeed = (await client.GetFromJsonAsync<JsonElement[]>(
            "/api/matches?take=25&arcRelay=false"))!;
        Assert.DoesNotContain(legacyFeed, value =>
            value.GetProperty("id").GetGuid() == created.Id);

        string ladder = await client.GetStringAsync("/api/arc-relay/ladder");
        Assert.DoesNotContain("winnerSlot", ladder, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("outcome", ladder, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(created.Id.ToString(), ladder, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("playbookJson", ladder, StringComparison.Ordinal);
        Assert.DoesNotContain("layoutJson", ladder, StringComparison.Ordinal);
        string matchDetail = await client.GetStringAsync($"/api/matches/{created.Id}");
        Assert.DoesNotContain("playbookJson", matchDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("layoutJson", matchDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("mindData", matchDetail, StringComparison.OrdinalIgnoreCase);

        HttpResponseMessage deleted = await client.DeleteAsync(
            $"/api/sheets/{sheets[3].Id}");
        deleted.EnsureSuccessStatusCode();
        Assert.Equal(3, (await client
            .GetFromJsonAsync<TacticalSheetSummaryResponse[]>("/api/sheets"))!
            .Length);
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
        TacticalSheetCatalogResponse catalog = (await client
            .GetFromJsonAsync<TacticalSheetCatalogResponse>(
                "/api/sheets/catalog"))!;
        string[] classes = [.. catalog.StarterSheets[0].Composition];

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
        TacticalSheetCatalogResponse catalog = (await firstClient
            .GetFromJsonAsync<TacticalSheetCatalogResponse>(
                "/api/sheets/catalog"))!;
        TacticalSheetResponse first = await SaveSheetAsync(
            firstClient, "Alpha sheet", catalog, enterLadder: true);
        JsonObject alternate = JsonNode.Parse(
            catalog.StarterSheets[0].TacticsJson)!.AsObject();
        alternate["squad"]![0]!["build"] = "balanced";
        TacticalSheetResponse second = await SaveSheetAsync(
            secondClient,
            "Beta sheet",
            catalog,
            enterLadder: true,
            tacticsJson: alternate.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
            }));

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
        ArcRelaySeasonResponse concealedSeason = (await firstClient
            .GetFromJsonAsync<ArcRelaySeasonResponse>(
                "/api/arc-relay/seasons/current"))!;
        Assert.Empty(concealedSeason.RecentMatches);
        Assert.All(concealedSeason.Standings, standing =>
        {
            Assert.Equal((0, 0, 0),
                (standing.Wins, standing.Draws, standing.Losses));
            Assert.Single(standing.RatingHistory);
        });
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
        ArcRelaySeasonResponse revealedSeason = (await firstClient
            .GetFromJsonAsync<ArcRelaySeasonResponse>(
                "/api/arc-relay/seasons/current"))!;
        Assert.Equal(matchId, Assert.Single(revealedSeason.RecentMatches).MatchId);
        Assert.All(revealedSeason.Standings, standing =>
        {
            Assert.Equal(1,
                standing.Wins + standing.Draws + standing.Losses);
            Assert.Equal(2, standing.RatingHistory.Count);
        });

        await using (AppDbContext db = database.CreateContext())
        {
            Season season = await db.Seasons.SingleAsync(value =>
                value.Key == ArcRelayLadderPolicy.SeasonKey);
            season.EndsAt = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<
                ArcRelaySeasonLifecycleService>().AdvanceAsync();
        await using (AppDbContext db = database.CreateContext())
        {
            Season season = await db.Seasons.SingleAsync(value =>
                value.Key == ArcRelayLadderPolicy.SeasonKey);
            Assert.Equal(SeasonStatus.Closed, season.Status);
            Assert.NotNull(season.ClosedAt);
            ArcRelaySeasonEntrant[] snapshots = await db
                .ArcRelaySeasonEntrants.Where(value =>
                    value.SeasonId == season.Id).ToArrayAsync();
            Assert.Equal(2, snapshots.Length);
            Assert.All(snapshots, snapshot =>
            {
                Assert.NotNull(snapshot.ClosingRating);
                Assert.NotNull(snapshot.ClosingRank);
                Assert.NotNull(snapshot.ClosingRevision);
                Assert.Contains("OwnerDisplayName", snapshot.ClosingPresentationJson,
                    StringComparison.Ordinal);
            });
        }
    }
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task Season_one_keeps_development_history_but_opens_a_fresh_rating_book()
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
                ArcRelayEntrantPlaylistDefinition.CreateHistoricalV6();
            var version = new PlaylistVersion
            {
                PlaylistId = playlist.Id,
                Version = ArcRelayEntrantPlaylistDefinition.TacticalVersion,
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
                Key = ArcRelayLadderPolicy.LegacyLaunchSeasonKey,
                DisplayName = "Development ladder",
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
                    ArcRelayClassCatalog.Default,
                    ArcRelayClassCatalog.Default.StarterIds);
            oldLadderId = ladder.Id;
            db.PlaylistVersions.Add(version);
            db.Seasons.Add(season);
            db.Ladders.Add(ladder);
            db.ArcRelayEntrants.Add(new ArcRelayEntrant
            {
                Id = entrantId,
                OwnerUserId = owner.Id,
                Name = "Prepared mind",
                Kind = ArcRelayEntrantKind.CustomMind,
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
            ArcRelayEntrantRating seasonRating = await db
                .ArcRelayEntrantRatings.SingleAsync(value =>
                    value.EntrantId == entrantId &&
                    value.LadderId == currentLadder.Id);
            Assert.Equal(BotRating.DefaultRating, seasonRating.Rating);
            Assert.Equal(0, seasonRating.RankedMatches);
            ArcRelayEntrantRating historicalRating = await db.ArcRelayEntrantRatings
                .SingleAsync(value => value.EntrantId == entrantId &&
                    value.LadderId == oldLadderId);
            Assert.Equal(1462, historicalRating.Rating);
            Assert.Equal(17, historicalRating.RankedMatches);
            ArcRelayEntrant entrant = await db.ArcRelayEntrants.SingleAsync(
                value => value.Id == entrantId);
            Assert.Equal(ArcRelayPreflightStatus.Passed, entrant.PreflightStatus);
            Assert.Equal(3, entrant.PreflightRevision);
            Assert.True(entrant.LadderOptedIn);
            Season currentSeason = await db.Seasons.SingleAsync(value =>
                value.Key == ArcRelayLadderPolicy.SeasonKey);
            Assert.Equal(SeasonStatus.Open, currentSeason.Status);
            Assert.Equal(ArcRelayLadderPolicy.SeasonNumber, currentSeason.Number);
            Assert.Equal(TimeSpan.FromDays(ArcRelayLadderPolicy.SeasonDurationDays),
                currentSeason.EndsAt - currentSeason.StartsAt);
            ArcRelaySeasonEntrant snapshot = await db.ArcRelaySeasonEntrants
                .SingleAsync(value => value.SeasonId == currentSeason.Id &&
                    value.EntrantId == entrantId);
            Assert.True(snapshot.PresentAtOpening);
            Assert.Equal(BotRating.DefaultRating, snapshot.OpeningRating);
            Assert.Equal(1, snapshot.OpeningRank);
            Assert.Equal(1, snapshot.OpeningRevision);
            Assert.Contains("Prepared mind", snapshot.OpeningPresentationJson,
                StringComparison.Ordinal);
        }
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task Season_one_preserves_v7_eligibility_but_not_development_rating()
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
        UserResponse owner = await RegisterAsync(client, "V7 Continuity");
        Guid entrantId = Guid.NewGuid();
        Guid tiedEntrantId = Guid.NewGuid();
        Guid oldLadderId;

        await using (AppDbContext db = database.CreateContext())
        {
            Playlist playlist = await db.Playlists.SingleAsync(value =>
                value.Key == ArcRelayEntrantPlaylistDefinition.PlaylistKey);
            ArcRelayEntrantPlaylistDefinition historical =
                ArcRelayEntrantPlaylistDefinition.CreateHistoricalV7();
            var version = new PlaylistVersion
            {
                PlaylistId = playlist.Id,
                Version = ArcRelayEntrantPlaylistDefinition.AmbushVersion,
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
                Key = ArcRelayLadderPolicy.LegacyLaunchSeasonKey,
                DisplayName = "Development ladder",
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
                    ArcRelayPlaylistSeeder.TacticalPlaybookBotSlug)
                .Select(value => value.Id)
                .SingleAsync();
            Guid tiedMindBotId = await db.Bots
                .Where(value => value.Slug ==
                    ArcRelayPlaylistSeeder.ForwardStockBotSlug)
                .Select(value => value.Id)
                .SingleAsync();
            ArcRelayCompositionCompilation composition =
                ArcRelayComposition.Compile(
                    new ArcRelayCompositionDeclaration(
                        ArcRelayClassCatalog.Default.StarterIds.ToArray()),
                    ArcRelayClassCatalog.Default,
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
                Name = "V7 mind",
                MindBotId = mindBotId,
                CompositionJson = composition.CanonicalJson,
                CompositionHash = composition.ContentHash,
                PreflightStatus = ArcRelayPreflightStatus.Passed,
                PreflightRevision = 4,
                LadderOptedIn = true,
                LadderOptedInAt = DateTime.UtcNow,
            });
            db.ArcRelayEntrants.Add(new ArcRelayEntrant
            {
                Id = tiedEntrantId,
                OwnerUserId = owner.Id,
                Kind = ArcRelayEntrantKind.CustomMind,
                Name = "Second development mind",
                MindBotId = tiedMindBotId,
                CompositionJson = composition.CanonicalJson,
                CompositionHash = composition.ContentHash,
                PreflightStatus = ArcRelayPreflightStatus.Passed,
                PreflightRevision = 1,
                LadderOptedIn = true,
                LadderOptedInAt = DateTime.UtcNow,
            });
            db.ArcRelayEntrantRatings.Add(new ArcRelayEntrantRating
            {
                EntrantId = entrantId,
                LadderId = ladder.Id,
                Rating = 1517,
                RankedMatches = 23,
            });
            db.ArcRelayEntrantRatings.Add(new ArcRelayEntrantRating
            {
                EntrantId = tiedEntrantId,
                LadderId = ladder.Id,
                Rating = 905,
                RankedMatches = 2,
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
                where version.Version ==
                    ArcRelayEntrantPlaylistDefinition.Version
                select ladder).SingleAsync();
            ArcRelayEntrantRating currentRating = await db
                .ArcRelayEntrantRatings.SingleAsync(value =>
                    value.EntrantId == entrantId &&
                    value.LadderId == currentLadder.Id);
            Assert.Equal(BotRating.DefaultRating, currentRating.Rating);
            Assert.Equal(0, currentRating.RankedMatches);
            ArcRelayEntrant entrant = await db.ArcRelayEntrants.SingleAsync(
                value => value.Id == entrantId);
            Assert.Equal(
                ArcRelayPreflightStatus.Passed,
                entrant.PreflightStatus);
            Assert.Equal(4, entrant.PreflightRevision);
            Assert.True(entrant.LadderOptedIn);
            ArcRelayEntrantRating tiedRating = await db
                .ArcRelayEntrantRatings.SingleAsync(value =>
                    value.EntrantId == tiedEntrantId &&
                    value.LadderId == currentLadder.Id);
            Assert.Equal(BotRating.DefaultRating, tiedRating.Rating);
            Assert.Equal(0, tiedRating.RankedMatches);
        }

        // Opening is idempotent: process restarts cannot duplicate either the
        // rating identity or its opening snapshot.
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider
                .GetRequiredService<ArcRelayEntrantPlaylistSeeder>()
                .SeedAsync();
        await using (AppDbContext db = database.CreateContext())
        {
            Assert.Equal(1, await db.ArcRelayEntrantRatings.CountAsync(value =>
                value.EntrantId == entrantId && value.LadderId != oldLadderId));
            Assert.Equal(2, await db.ArcRelaySeasonEntrants.CountAsync());
        }

        ArcRelaySeasonResponse currentSeasonResponse = (await client
            .GetFromJsonAsync<ArcRelaySeasonResponse>(
                "/api/arc-relay/seasons/current"))!;
        ArcRelaySeasonStandingResponse[] standings =
            currentSeasonResponse.Standings.ToArray();
        Assert.Equal(2, standings.Length);
        Assert.Equal("open", currentSeasonResponse.Status);
        Assert.Equal(1, currentSeasonResponse.Number);
        Assert.All(standings, standing =>
        {
            Assert.Equal(1, standing.Rank);
            Assert.Equal(1, standing.OpeningRank);
            Assert.Equal(0, standing.RankMovement);
            Assert.Equal((0, 0, 0),
                (standing.Wins, standing.Draws, standing.Losses));
            Assert.Single(standing.RatingHistory);
        });
    }

    [SkippableTheory]
    [InlineData(ArcRelayEntrantPlaylistDefinition.OrdersV2Version)]
    [InlineData(ArcRelayEntrantPlaylistDefinition.Tapline3Version)]
    [InlineData(ArcRelayEntrantPlaylistDefinition.Tapline4Version)]
    [InlineData(ArcRelayEntrantPlaylistDefinition.Tapline5Version)]
    [InlineData(ArcRelayEntrantPlaylistDefinition.Tapline6Version)]
    [InlineData(ArcRelayEntrantPlaylistDefinition.Tapline8Version)]
    [InlineData(ArcRelayEntrantPlaylistDefinition.Tapline9Version)]
    [InlineData(ArcRelayEntrantPlaylistDefinition.Tapline10Version)]
    [InlineData(ArcRelayEntrantPlaylistDefinition.Tapline11Version)]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task Tapline12_v29_starts_a_fresh_book_from_the_open_prior_ladder(
        int predecessorVersion)
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using (AppDbContext migration =
            await database.CreateMigratedContextAsync()) { }
        using var factory = new BotArenaApplicationFactory(
            database.ConnectionString,
            legacyDuelEnabled: false);
        using HttpClient client = factory.CreateClient();
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ArcRelayPlaylistSeeder>()
                .SeedAsync();
        }
        UserResponse owner = await RegisterAsync(
            client, "V23 Tapline Rating Entrant");
        Guid entrantId = Guid.NewGuid();
        Guid predecessorLadderId;

        await using (AppDbContext db = database.CreateContext())
        {
            Playlist playlist = await db.Playlists.SingleAsync(value =>
                value.Key == ArcRelayEntrantPlaylistDefinition.PlaylistKey);
            ArcRelayEntrantPlaylistDefinition predecessor = predecessorVersion switch
            {
                ArcRelayEntrantPlaylistDefinition.OrdersV2Version =>
                    ArcRelayEntrantPlaylistDefinition.CreateHistoricalV20(),
                ArcRelayEntrantPlaylistDefinition.Tapline3Version =>
                    ArcRelayEntrantPlaylistDefinition.CreateHistoricalV21(),
                ArcRelayEntrantPlaylistDefinition.Tapline4Version =>
                    ArcRelayEntrantPlaylistDefinition.CreateHistoricalV22(),
                ArcRelayEntrantPlaylistDefinition.Tapline5Version =>
                    ArcRelayEntrantPlaylistDefinition.CreateHistoricalV23(),
                ArcRelayEntrantPlaylistDefinition.Tapline6Version =>
                    ArcRelayEntrantPlaylistDefinition.CreateHistoricalV24(),
                ArcRelayEntrantPlaylistDefinition.Tapline8Version =>
                    ArcRelayEntrantPlaylistDefinition.CreateHistoricalV25(),
                ArcRelayEntrantPlaylistDefinition.Tapline9Version =>
                    ArcRelayEntrantPlaylistDefinition.CreateHistoricalV26(),
                ArcRelayEntrantPlaylistDefinition.Tapline10Version =>
                    ArcRelayEntrantPlaylistDefinition.CreateHistoricalV27(),
                ArcRelayEntrantPlaylistDefinition.Tapline11Version =>
                    ArcRelayEntrantPlaylistDefinition.CreateHistoricalV28(),
                _ => throw new ArgumentOutOfRangeException(nameof(predecessorVersion)),
            };
            var version = new PlaylistVersion
            {
                PlaylistId = playlist.Id,
                Version = predecessorVersion,
                GameModeId = predecessor.Match.Rules.GameMode.ModeId,
                RulesetId = predecessor.Match.Rules.RulesetId,
                MatchFormatId = predecessor.Match.Format.FormatId,
                MapPoolId = predecessor.Match.Map.Id,
                SeriesPolicyId = ArcRelayEntrantPlaylistDefinition.SeriesPolicyId,
                MatchmakingPolicyId =
                    ArcRelayEntrantPlaylistDefinition.MatchmakingPolicyId,
                AdmissionPolicyId = predecessor.AdmissionPolicyId,
                ExecutionPolicyId = predecessor.ExecutionPolicyId,
                ExecutionEngineVersion = predecessor.ExecutionEngineVersion,
                CanonicalDefinition = predecessor.CanonicalDefinition,
                DefinitionFingerprint = predecessor.DefinitionFingerprint,
                Provenance = predecessor.Provenance,
                Visibility = ArcRelayEntrantPlaylistDefinition.Visibility,
            };
            DateTime startsAt = DateTime.UtcNow.AddDays(-1);
            var season = new Season
            {
                Key = ArcRelayLadderPolicy.SeasonKey,
                DisplayName = ArcRelayLadderPolicy.SeasonName,
                Number = ArcRelayLadderPolicy.SeasonNumber,
                Status = SeasonStatus.Open,
                StartsAt = startsAt,
                EndsAt = startsAt.AddDays(
                    ArcRelayLadderPolicy.SeasonDurationDays),
                OpenedAt = startsAt,
                RatingSeedPolicyId = ArcRelayLadderPolicy.RatingSeedPolicyId,
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
            predecessorLadderId = ladder.Id;
            db.PlaylistVersions.Add(version);
            db.Seasons.Add(season);
            db.Ladders.Add(ladder);
            db.ArcRelayEntrants.Add(new ArcRelayEntrant
            {
                Id = entrantId,
                OwnerUserId = owner.Id,
                Kind = ArcRelayEntrantKind.Sheet,
                Name = "V20 rated entrant",
                LadderOptedIn = true,
                LadderOptedInAt = DateTime.UtcNow,
            });
            db.ArcRelayEntrantRatings.Add(new ArcRelayEntrantRating
            {
                EntrantId = entrantId,
                LadderId = ladder.Id,
                Rating = 1473,
                RankedMatches = 19,
            });
            await db.SaveChangesAsync();
        }

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<ArcRelayEntrantPlaylistSeeder>()
                .SeedAsync();
        }

        await using (AppDbContext db = database.CreateContext())
        {
            Ladder predecessorLadder = await db.Ladders.SingleAsync(value =>
                value.Id == predecessorLadderId);
            Assert.Equal(LadderStatus.Closed, predecessorLadder.Status);
            Assert.False(predecessorLadder.IsListed);
            Ladder v29Ladder = await (
                from ladder in db.Ladders
                join version in db.PlaylistVersions
                    on ladder.PlaylistVersionId equals version.Id
                where version.Version == ArcRelayEntrantPlaylistDefinition.Version
                select ladder).SingleAsync();
            Assert.Equal(LadderStatus.Open, v29Ladder.Status);
            Assert.True(v29Ladder.IsListed);
            ArcRelayEntrantRating fresh = await db.ArcRelayEntrantRatings
                .SingleAsync(value => value.EntrantId == entrantId &&
                    value.LadderId == v29Ladder.Id);
            Assert.Equal(BotRating.DefaultRating, fresh.Rating);
            Assert.Equal(0, fresh.RankedMatches);
            ArcRelayEntrantRating historical = await db.ArcRelayEntrantRatings
                .SingleAsync(value => value.EntrantId == entrantId &&
                    value.LadderId == predecessorLadderId);
            Assert.Equal(1473, historical.Rating);
            Assert.Equal(19, historical.RankedMatches);
        }

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<ArcRelayEntrantPlaylistSeeder>()
                .SeedAsync();
        }
        await using (AppDbContext db = database.CreateContext())
        {
            Assert.Equal(2, await db.ArcRelayEntrantRatings.CountAsync(
                value => value.EntrantId == entrantId));
        }
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task Tapline12_v29_refuses_to_skip_a_registered_v28_ladder()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using (AppDbContext migration =
            await database.CreateMigratedContextAsync()) { }
        using var factory = new BotArenaApplicationFactory(
            database.ConnectionString,
            legacyDuelEnabled: false);
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ArcRelayPlaylistSeeder>()
                .SeedAsync();
        }

        await using (AppDbContext db = database.CreateContext())
        {
            Playlist playlist = await db.Playlists.SingleAsync(value =>
                value.Key == ArcRelayEntrantPlaylistDefinition.PlaylistKey);
            ArcRelayEntrantPlaylistDefinition v27Definition =
                ArcRelayEntrantPlaylistDefinition.CreateHistoricalV27();
            ArcRelayEntrantPlaylistDefinition v28Definition =
                ArcRelayEntrantPlaylistDefinition.CreateHistoricalV28();
            PlaylistVersion v27 = Version(v27Definition);
            PlaylistVersion v28 = Version(v28Definition);
            DateTime startsAt = DateTime.UtcNow.AddDays(-1);
            var season = new Season
            {
                Key = ArcRelayLadderPolicy.SeasonKey,
                DisplayName = ArcRelayLadderPolicy.SeasonName,
                Number = ArcRelayLadderPolicy.SeasonNumber,
                Status = SeasonStatus.Open,
                StartsAt = startsAt,
                EndsAt = startsAt.AddDays(
                    ArcRelayLadderPolicy.SeasonDurationDays),
                OpenedAt = startsAt,
                RatingSeedPolicyId = ArcRelayLadderPolicy.RatingSeedPolicyId,
            };
            db.PlaylistVersions.AddRange(v27, v28);
            db.Seasons.Add(season);
            db.Ladders.AddRange(
                new Ladder
                {
                    PlaylistVersionId = v27.Id,
                    SeasonId = season.Id,
                    Status = LadderStatus.Open,
                    RatingPolicyId = ArcRelayEloV1.Id,
                    IsListed = true,
                    AwardsAchievements = false,
                },
                new Ladder
                {
                    PlaylistVersionId = v28.Id,
                    SeasonId = season.Id,
                    Status = LadderStatus.Draft,
                    RatingPolicyId = ArcRelayEloV1.Id,
                    IsListed = false,
                    AwardsAchievements = false,
                });
            await db.SaveChangesAsync();

            PlaylistVersion Version(
                ArcRelayEntrantPlaylistDefinition definition) => new()
            {
                PlaylistId = playlist.Id,
                Version = definition.PlaylistVersion,
                GameModeId = definition.Match.Rules.GameMode.ModeId,
                RulesetId = definition.Match.Rules.RulesetId,
                MatchFormatId = definition.Match.Format.FormatId,
                MapPoolId = definition.Match.Map.Id,
                SeriesPolicyId = ArcRelayEntrantPlaylistDefinition.SeriesPolicyId,
                MatchmakingPolicyId =
                    ArcRelayEntrantPlaylistDefinition.MatchmakingPolicyId,
                AdmissionPolicyId = definition.AdmissionPolicyId,
                ExecutionPolicyId = definition.ExecutionPolicyId,
                ExecutionEngineVersion = definition.ExecutionEngineVersion,
                CanonicalDefinition = definition.CanonicalDefinition,
                DefinitionFingerprint = definition.DefinitionFingerprint,
                Provenance = definition.Provenance,
                Visibility = ArcRelayEntrantPlaylistDefinition.Visibility,
            };
        }

        await using AsyncServiceScope seedScope =
            factory.Services.CreateAsyncScope();
        InvalidOperationException error = await Assert.ThrowsAsync<
            InvalidOperationException>(() => seedScope.ServiceProvider
                .GetRequiredService<ArcRelayEntrantPlaylistSeeder>()
                .SeedAsync());
        Assert.Contains(
            "cannot skip a newer registered intermediate ladder",
            error.Message,
            StringComparison.Ordinal);
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

    private static async Task<TacticalSheetResponse> SaveSheetAsync(
        HttpClient client,
        string name,
        TacticalSheetCatalogResponse catalog,
        bool? enterLadder,
        string? tacticsJson = null)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/sheets",
            new SaveTacticalSheetRequest(
                name,
                ExpectedRevision: null,
                tacticsJson ?? catalog.StarterSheets[0].TacticsJson,
                enterLadder));
        Assert.True(response.IsSuccessStatusCode,
            $"Sheet save failed with {(int)response.StatusCode}: "
            + await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<TacticalSheetResponse>())!;
    }

    private static void AssertJsonEquivalent(string? expected, string? actual)
    {
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)),
            $"Expected JSON equivalent to {expected}, but received {actual}.");
    }
}
