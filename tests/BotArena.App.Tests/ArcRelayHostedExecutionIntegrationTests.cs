using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using BotArena.App.Accounts;
using BotArena.App.ArcRelay;
using BotArena.App.Competition;
using BotArena.App.Jobs;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.App.Sheets;
using BotArena.App.Storage;
using BotArena.Engine;
using BotArena.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace BotArena.App.Tests;

[Collection(ApplicationHttpCollection.Name)]
public sealed class ArcRelayHostedExecutionIntegrationTests
{
    private readonly ITestOutputHelper output;

    public ArcRelayHostedExecutionIntegrationTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void TacticalHostedLaneRetainsTheCompleteObservationContract()
    {
        using InProcessGenericMindRuntimeFactory factory =
            GenericActorMatchExecutor.CreateTacticalMindRuntimeFactory();

        Assert.False(factory.UsesTrustedArcRelayStockProjection);
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task SeederCorrectsOnlyTheUnpublishedMistakenV26Definition()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using (AppDbContext migration =
                     await database.CreateMigratedContextAsync())
        {
        }

        using var factory = new BotArenaApplicationFactory(database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<ArcRelayPlaylistSeeder>()
                .SeedAsync();
            await scope.ServiceProvider
                .GetRequiredService<ArcRelayEntrantPlaylistSeeder>()
                .SeedAsync();
        }

        Guid versionId;
        await using (AppDbContext db = database.CreateContext())
        {
            PlaylistVersion version = await db.PlaylistVersions.SingleAsync(
                value => value.Version
                    == ArcRelayEntrantPlaylistDefinition.Version);
            versionId = version.Id;
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"PlaylistVersions\" DISABLE TRIGGER "
                + "\"TR_PlaylistVersions_Immutable\";");
            try
            {
                const string mistakenCanonical =
                    "{\"mistakenLocalV26\":true}";
                const string mistakenProvenance =
                    "{\"midWellRibs\":true}";
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "PlaylistVersions"
                    SET "Version" = {
                            ArcRelayEntrantPlaylistDefinition.Tapline9Version},
                        "DefinitionFingerprint" = {
                        ArcRelayEntrantPlaylistSeeder
                            .UnpublishedMistakenTapline9DefinitionFingerprint},
                        "CanonicalDefinition" = CAST({mistakenCanonical} AS jsonb),
                        "Provenance" = CAST({mistakenProvenance} AS jsonb)
                    WHERE "Id" = {versionId};
                    """);
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE \"PlaylistVersions\" ENABLE TRIGGER "
                    + "\"TR_PlaylistVersions_Immutable\";");
            }
        }

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ArcRelayEntrantSeedResult repaired = await scope.ServiceProvider
                .GetRequiredService<ArcRelayEntrantPlaylistSeeder>()
                .SeedAsync();
            Assert.Equal(
                ArcRelayEntrantPlaylistDefinition.Version,
                repaired.PlaylistVersion.Version);
            Assert.NotEqual(versionId, repaired.PlaylistVersion.Id);
        }

        await using (AppDbContext db = database.CreateContext())
        {
            Playlist playlist = await db.Playlists.SingleAsync(value =>
                value.Key == ArcRelayEntrantPlaylistDefinition.PlaylistKey);
            PlaylistVersion repaired = await db.PlaylistVersions.SingleAsync(
                value => value.Id == versionId);
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV26()
                .Validate(playlist, repaired);
        }
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task PlayerSheetsQueueAndExecuteThroughTrustedHostedLane()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using (AppDbContext migration =
                     await database.CreateMigratedContextAsync())
        {
            // The application test host runs the production `web` role, whose
            // normal startup deliberately never migrates or seeds.
        }

        using var factory = new BotArenaApplicationFactory(database.ConnectionString);
        using HttpClient client = factory.CreateClient();
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ArcRelaySeedResult seeded = await scope.ServiceProvider
                .GetRequiredService<ArcRelayPlaylistSeeder>()
                .SeedAsync();
            await scope.ServiceProvider.GetRequiredService<ArcRelayEntrantPlaylistSeeder>()
                .SeedAsync();
            Assert.Equal(
                ArcRelayPlaylistDefinition.StockArtifactHash,
                seeded.StockBotVersion.ArtifactHash);
        }

        UserResponse owner = await RegisterAsync(client);
        TacticalSheetCatalogResponse catalog =
            Assert.IsType<TacticalSheetCatalogResponse>(
                await client.GetFromJsonAsync<TacticalSheetCatalogResponse>(
                    "/api/sheets/catalog"));
        Assert.Equal(ArcRelayComposition.SlotCount, catalog.SlotCount);
        Assert.Equal(ArcRelayLoopProfile.Tapline15.MapId, catalog.Map.Id);
        Assert.Equal(31, catalog.Map.Width);
        Assert.Equal(27, catalog.Map.Height);
        Assert.Equal(2, catalog.StockOpponents.Count);
        Assert.Equal(
            ["first-stand-v1", "mid-pincer-v1"],
            catalog.StarterSheets.Select(value => value.Id));
        Assert.All(catalog.StarterSheets, starter =>
        {
            Assert.Equal(8, starter.Composition.Count);
            Assert.Contains("arc-relay-tactics-v1", starter.TacticsJson);
        });
        Assert.Equal(16, catalog.Classes.Count);
        Assert.All(catalog.Classes, entry =>
        {
            Assert.NotEmpty(entry.SignatureAuthoring.AllowedSelectors);
            Assert.True(entry.SignatureAuthoring.CooldownTicks > 0);
            Assert.False(string.IsNullOrWhiteSpace(
                entry.SignatureAuthoring.ArgumentKind));
            Assert.False(string.IsNullOrWhiteSpace(
                entry.SignatureAuthoring.Preset.Title));
            Assert.True(entry.VisionRange > 0);
            Assert.InRange(entry.VisionProximityRange, 0, entry.VisionRange);
            Assert.True(entry.AttackRange >= 0);
        });
        Assert.Equal(16, catalog.Classes.Select(entry =>
            entry.SignatureAuthoring.Preset.Id).Distinct().Count());
        Assert.All(catalog.Classes, entry => Assert.Equal(5, entry.VisionRange));
        Assert.Equal(
            [3, 4, 6],
            catalog.Classes.Select(entry => entry.AttackRange)
                .Distinct().Order().ToArray());
        TacticalSheetClassResponse kestrel = catalog.Classes.Single(value =>
            value.Id == "kestrel");
        Assert.True(kestrel.SignatureAuthoring.AllowsRouteForward);
        Assert.Equal("heading", kestrel.SignatureAuthoring.ArgumentKind);
        TacticalSheetClassResponse relay = catalog.Classes.Single(value =>
            value.Id == "relay");
        Assert.True(relay.SignatureAuthoring.AllowsRouteAheadRole);
        Assert.True(relay.SignatureAuthoring.AllowsFriendlyRole);
        TacticalStockSheetResponse stock = catalog.StockOpponents[0];
        Assert.Equal("demolition-march-v1", stock.Id);
        Assert.Equal("idle-field-v1", catalog.StockOpponents[1].Id);

        TacticalSheetResponse first = await SaveAsync(
            client,
            "First line",
            catalog.StarterSheets[0].TacticsJson);
        JsonObject counterPlan = JsonNode.Parse(
            catalog.StarterSheets[0].TacticsJson)!.AsObject();
        // Change a real executable value so distinct drafts get distinct
        // materializations and therefore distinct hashes.
        counterPlan["tactics"]![1]!["finish"]!["maxAttempt"] = "long";
        TacticalSheetResponse second = await SaveAsync(
            client,
            "Counter line",
            counterPlan.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
            }));
        Assert.NotEqual(first.ContentHash, second.ContentHash);

        HttpResponseMessage queued = await client.PostAsJsonAsync(
            "/api/arc-relay/scrimmages",
            new CreateArcRelayScrimmageRequest(
                first.Id, second.Id, Seed: 104729));
        queued.EnsureSuccessStatusCode();
        CreatedMatchResponse created = Assert.IsType<CreatedMatchResponse>(
            await queued.Content.ReadFromJsonAsync<CreatedMatchResponse>());

        Stopwatch elapsed = Stopwatch.StartNew();
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<
                GenericActorMatchExecutionJobHandler>();
            JobExecutionResult result = await handler.HandleAsync(
                created.Id,
                GenericActorMatchJobType.ForPlaylist(
                    ArcRelayEntrantPlaylistDefinition.PlaylistKey,
                    ArcRelayEntrantPlaylistDefinition.Version),
                CancellationToken.None);
            Assert.Equal("completed", result.Outcome);
        }
        elapsed.Stop();
        output.WriteLine(
            "trusted-hosted-worker-ms={0:F3}",
            elapsed.Elapsed.TotalMilliseconds);

        string replayKey;
        string replayHash;
        await using (AppDbContext db = database.CreateContext())
        {
            Match match = await db.Matches
                .Include(value => value.Participants)
                .Include(value => value.TeamResults)
                .SingleAsync(value => value.Id == created.Id);
            Assert.Equal(MatchStatus.Completed, match.Status);
            Assert.Equal(ArcRelayLoopProfile.Tapline15.MapId, match.MapId);
            Assert.Equal(
                ArcRelayLoopProfile.Tapline15.RulesetId,
                match.GameRulesVersion);
            Assert.Equal(ArcRelayBroadcastDocument.FormatVersion, match.ReplayFormatVersion);
            Assert.Equal(1.25, match.PresentationTicksPerSecond);
            Assert.Equal(2, match.TeamResults.Count);
            Assert.Equal(2, match.Participants.Count);
            output.WriteLine("end-tick={0}", match.EndTick);
            Assert.All(match.Participants, participant =>
            {
                Assert.Equal(owner.DisplayName, participant.OwnerDisplayNameSnapshot);
                Assert.Equal(
                    ArcRelayEntrantPlaylistDefinition
                        .Tapline15TacticalArtifactHash,
                    participant.ArtifactHashSnapshot);
                Assert.NotNull(participant.SheetIdSnapshot);
                Assert.NotNull(participant.EntrantIdSnapshot);
                Assert.Equal("sheet", participant.EntrantKindSnapshot);
                Assert.Equal(1, participant.SheetRevisionSnapshot);
                Assert.NotNull(participant.SheetHashSnapshot);
                Assert.NotNull(participant.SheetCanonicalJsonSnapshot);
                Assert.NotNull(participant.SheetLayoutJsonSnapshot);
                Assert.NotEmpty(Assert.IsType<byte[]>(participant.MindDataSnapshot));
            });
            Guid[] tacticalVersionIds = match.Participants
                .Select(value => value.BotVersionId)
                .Distinct()
                .ToArray();
            var tacticalVersions = await db.BotVersions
                .Where(value => tacticalVersionIds.Contains(value.Id))
                .Select(value => new
                {
                    value.BotId,
                    value.GameRulesVersion,
                })
                .ToListAsync();
            Assert.All(tacticalVersions, value => Assert.Equal(
                ArcRelayLoopProfile.Tapline15.RulesetId,
                value.GameRulesVersion));
            Guid[] tacticalBotIds = tacticalVersions
                .Select(value => value.BotId)
                .Distinct()
                .ToArray();
            string tacticalSlug = Assert.Single(await db.Bots
                .Where(value => tacticalBotIds.Contains(value.Id))
                .Select(value => value.Slug)
                .ToListAsync());
            Assert.Equal(
                ArcRelayPlaylistSeeder.TaplineTacticalPlaybookBotSlug,
                tacticalSlug);
            Assert.Equal(first.ContentHash, match.Participants.Single(value => value.Slot == 0).SheetHashSnapshot);
            Assert.Equal(second.ContentHash, match.Participants.Single(value => value.Slot == 1).SheetHashSnapshot);
            replayKey = Assert.IsType<string>(match.ReplayKey);
            replayHash = Assert.IsType<string>(match.ReplayHash);
        }

        IObjectStore objectStore = factory.Services.GetRequiredService<IObjectStore>();
        await using Stream replayStream = Assert.IsAssignableFrom<Stream>(
            await objectStore.OpenReadAsync(replayKey));
        output.WriteLine("stored-replay-bytes={0}", replayStream.Length);
        Assert.InRange(replayStream.Length, 1, 300 * 1024);
        await using var decompressed = new System.IO.Compression.GZipStream(
            replayStream,
            System.IO.Compression.CompressionMode.Decompress);
        using JsonDocument replay = await JsonDocument.ParseAsync(decompressed);
        Assert.Equal(ArcRelayBroadcastDocument.BroadcastVersion,
            replay.RootElement.GetProperty("broadcastVersion").GetInt32());
        JsonElement[] provenance = replay.RootElement
            .GetProperty("header")
            .GetProperty("provenance")
            .GetProperty("participants")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(2, provenance.Length);
        Assert.Collection(
            provenance,
            value => Assert.Equal(first.ContentHash, value.GetProperty("mindDataHash").GetString()),
            value => Assert.Equal(second.ContentHash, value.GetProperty("mindDataHash").GetString()));
        Assert.All(provenance, value => Assert.Equal(
            ArcRelayEntrantPlaylistDefinition
                .Tapline15TacticalArtifactHash,
            value.GetProperty("artifactHash").GetString()));
        Assert.Equal(replayHash, replay.RootElement.GetProperty("replayHash").GetString());

        // A completed match remains causal until its broadcast clock catches up.
        string partialJson = await client.GetStringAsync($"/api/matches/{created.Id}/replay");
        using (JsonDocument partial = JsonDocument.Parse(partialJson))
        {
            Assert.True(partial.RootElement.GetProperty("partial").GetBoolean());
            Assert.Equal(JsonValueKind.Null, partial.RootElement.GetProperty("result").ValueKind);
            Assert.Equal(JsonValueKind.Null, partial.RootElement.GetProperty("replayHash").ValueKind);
        }

        await using (AppDbContext db = database.CreateContext())
        {
            Match match = await db.Matches.SingleAsync(value => value.Id == created.Id);
            match.BroadcastStartedAt = DateTime.UtcNow.AddHours(-1);
            await db.SaveChangesAsync();
        }
        HttpResponseMessage completeResponse = await client.GetAsync(
            $"/api/matches/{created.Id}/replay");
        completeResponse.EnsureSuccessStatusCode();
        Assert.Contains("gzip", completeResponse.Content.Headers.ContentEncoding);
        await using Stream completeBody = await completeResponse.Content.ReadAsStreamAsync();
        await using var completeGzip = new System.IO.Compression.GZipStream(
            completeBody,
            System.IO.Compression.CompressionMode.Decompress);
        using var completeReader = new StreamReader(completeGzip);
        string completeJson = await completeReader.ReadToEndAsync();
        using (JsonDocument complete = JsonDocument.Parse(completeJson))
        {
            Assert.False(complete.RootElement.GetProperty("partial").GetBoolean());
            Assert.Equal(replayHash, complete.RootElement.GetProperty("replayHash").GetString());
            Assert.Equal(JsonValueKind.Object, complete.RootElement.GetProperty("result").ValueKind);
        }

        // Execute the same immutable inputs again after JIT warm-up. This is both the
        // product latency sample and the compact-format determinism proof.
        HttpResponseMessage warmQueued = await client.PostAsJsonAsync(
            "/api/arc-relay/scrimmages",
            new CreateArcRelayScrimmageRequest(
                first.Id, second.Id, Seed: 104729));
        warmQueued.EnsureSuccessStatusCode();
        CreatedMatchResponse warmMatch = Assert.IsType<CreatedMatchResponse>(
            await warmQueued.Content.ReadFromJsonAsync<CreatedMatchResponse>());
        Stopwatch warmElapsed = Stopwatch.StartNew();
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<
                GenericActorMatchExecutionJobHandler>();
            JobExecutionResult result = await handler.HandleAsync(
                warmMatch.Id,
                GenericActorMatchJobType.ForPlaylist(
                    ArcRelayEntrantPlaylistDefinition.PlaylistKey,
                    ArcRelayEntrantPlaylistDefinition.Version),
                CancellationToken.None);
            Assert.Equal("completed", result.Outcome);
        }
        warmElapsed.Stop();
        output.WriteLine("trusted-hosted-warm-worker-ms={0:F3}", warmElapsed.Elapsed.TotalMilliseconds);
        await using (AppDbContext db = database.CreateContext())
        {
            Match repeated = await db.Matches.SingleAsync(value => value.Id == warmMatch.Id);
            Assert.Equal(replayHash, repeated.ReplayHash);
        }

        HttpResponseMessage steadyQueued = await client.PostAsJsonAsync(
            "/api/arc-relay/scrimmages",
            new CreateArcRelayScrimmageRequest(
                first.Id, second.Id, Seed: 104729));
        steadyQueued.EnsureSuccessStatusCode();
        CreatedMatchResponse steadyMatch = Assert.IsType<CreatedMatchResponse>(
            await steadyQueued.Content.ReadFromJsonAsync<CreatedMatchResponse>());
        Stopwatch steadyElapsed = Stopwatch.StartNew();
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<
                GenericActorMatchExecutionJobHandler>();
            JobExecutionResult result = await handler.HandleAsync(
                steadyMatch.Id,
                GenericActorMatchJobType.ForPlaylist(
                    ArcRelayEntrantPlaylistDefinition.PlaylistKey,
                    ArcRelayEntrantPlaylistDefinition.Version),
                CancellationToken.None);
            Assert.Equal("completed", result.Outcome);
        }
        steadyElapsed.Stop();
        output.WriteLine("trusted-hosted-steady-worker-ms={0:F3}", steadyElapsed.Elapsed.TotalMilliseconds);
        await using (AppDbContext db = database.CreateContext())
        {
            Match repeated = await db.Matches.SingleAsync(value => value.Id == steadyMatch.Id);
            Assert.Equal(replayHash, repeated.ReplayHash);
        }

        // Practice compiles the player-owned Tactics source with the running
        // server. Simulate a sheet saved by an older materializer: its sibling
        // Orders/map/hash are internally consistent but belong to another
        // source. Scrimmage/ranked continue pinning those saved bytes; Practice
        // must not execute them after a compiler restart.
        await using (AppDbContext db = database.CreateContext())
        {
            TacticalSheet stale = await db.TacticalSheets.SingleAsync(
                value => value.Id == first.Id);
            stale.PlaybookJson = second.OrdersJson;
            stale.LayoutJson = second.MapPlanJson;
            stale.ContentHash = second.ContentHash;
            await db.SaveChangesAsync();
        }
        HttpResponseMessage trialQueued = await client.PostAsJsonAsync(
            $"/api/sheets/{first.Id}/trial",
            new TrialTacticalSheetRequest(
                catalog.StockOpponents[0].Id,
                Seed: 65537));
        trialQueued.EnsureSuccessStatusCode();
        CreatedMatchResponse trialMatch = Assert.IsType<CreatedMatchResponse>(
            await trialQueued.Content.ReadFromJsonAsync<CreatedMatchResponse>());
        PracticeAuthoringSnapshotResponse authoring = Assert.IsType<PracticeAuthoringSnapshotResponse>(
            await client.GetFromJsonAsync<PracticeAuthoringSnapshotResponse>(
                $"/api/sheets/trials/{trialMatch.Id}/authoring"));
        Assert.True(authoring.AttributionAvailable);
        Assert.Equal(first.Id, authoring.SheetId);
        Assert.Equal(first.Revision, authoring.Revision);
        Assert.Equal(first.ContentHash, authoring.ContentHash);
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(first.OrdersJson),
            JsonNode.Parse(authoring.OrdersJson)));
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(first.MapPlanJson),
            JsonNode.Parse(authoring.MapPlanJson)));
        Assert.NotEqual(second.ContentHash, authoring.ContentHash);
        Assert.Contains(
            "arc-relay-tactics-v2", authoring.TacticsJson,
            StringComparison.Ordinal);

        JsonObject edited = JsonNode.Parse(first.TacticsJson)!.AsObject();
        edited["tactics"]![1]!["maxDuration"] = "brief";
        HttpResponseMessage editedResponse = await client.PutAsJsonAsync(
            $"/api/sheets/{first.Id}",
            new SaveTacticalSheetRequest(
                first.Name,
                first.Revision,
                edited.ToJsonString(),
                EnterLadder: true));
        editedResponse.EnsureSuccessStatusCode();
        PracticeAuthoringSnapshotResponse unchanged = Assert.IsType<PracticeAuthoringSnapshotResponse>(
            await client.GetFromJsonAsync<PracticeAuthoringSnapshotResponse>(
                $"/api/sheets/trials/{trialMatch.Id}/authoring"));
        Assert.Equal(authoring, unchanged);
        using HttpClient stranger = factory.CreateClient();
        await RegisterAsync(stranger);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.GetAsync(
                $"/api/sheets/trials/{trialMatch.Id}/authoring")).StatusCode);
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            JobExecutionResult result = await scope.ServiceProvider
                .GetRequiredService<GenericActorMatchExecutionJobHandler>()
                .HandleAsync(
                    trialMatch.Id,
                    GenericActorMatchJobType.ForPlaylist(
                        ArcRelayEntrantPlaylistDefinition.PlaylistKey,
                        ArcRelayEntrantPlaylistDefinition.Version),
                    CancellationToken.None);
            Assert.Equal("completed", result.Outcome);
        }
        await using (AppDbContext db = database.CreateContext())
        {
            Match trialResult = await db.Matches
                .Include(value => value.Participants)
                .SingleAsync(value => value.Id == trialMatch.Id);
            Assert.Equal(MatchStatus.Completed, trialResult.Status);
            Assert.Equal(ArcRelayMatchLane.Practice, trialResult.ArcRelayLane);
            Assert.Null(trialResult.BroadcastStartedAt);
            Assert.True(trialResult.BroadcastComplete(DateTime.UtcNow));
            Assert.Collection(
                trialResult.Participants.OrderBy(value => value.Slot),
                participant => Assert.Equal("sheet",
                    participant.EntrantKindSnapshot),
                participant => Assert.Equal("stock-fixture",
                    participant.EntrantKindSnapshot));
        }
        HttpResponseMessage practiceReplayResponse = await client.GetAsync(
            $"/api/matches/{trialMatch.Id}/replay");
        practiceReplayResponse.EnsureSuccessStatusCode();
        Assert.Contains(
            "gzip",
            practiceReplayResponse.Content.Headers.ContentEncoding);
        await using (Stream practiceBody =
                     await practiceReplayResponse.Content.ReadAsStreamAsync())
        await using (var practiceGzip = new System.IO.Compression.GZipStream(
                         practiceBody,
                         System.IO.Compression.CompressionMode.Decompress))
        using (JsonDocument practiceReplay =
               await JsonDocument.ParseAsync(practiceGzip))
        {
            Assert.False(
                practiceReplay.RootElement.GetProperty("partial").GetBoolean());
            Assert.Equal(
                JsonValueKind.Object,
                practiceReplay.RootElement.GetProperty("result").ValueKind);
            Assert.Equal(
                JsonValueKind.String,
                practiceReplay.RootElement.GetProperty("replayHash").ValueKind);
        }

        // The first executed match pays one-time application/runtime JIT and
        // cache warm-up and is intentionally reported, not asserted. The two
        // subsequent identical jobs are the actual hosted latency samples;
        // using a second coarse wall-clock gate here made a 5.045s cold start
        // fail while the steady production-shaped job passed at 1.450s.
        // CI and the PostgreSQL harness run this project in Release. That is
        // the production-shaped warm-worker gate: the reference Tapline-3
        // cell measured 1.241s end to end (1.136s in simulation). Debug is a
        // correctness lane with a coarse wrong-path guard, not a latency
        // benchmark; its unoptimized planner measured about 2.5s on the
        // hand-authored stock sheet and about 3.4s on the materialized
        // Tactics starter, whose per-bot post and fight leaves evaluate a
        // larger order tree. Tapline-8's bank-gated supply makes matches run
        // deeper into the 600-tick horizon (the reference cell ends ~541),
        // which measured about 5.0s in Debug.
#if DEBUG
        Assert.True(
            steadyElapsed.Elapsed < TimeSpan.FromSeconds(8),
            $"Debug hosted entrant execution took {steadyElapsed.Elapsed.TotalMilliseconds:F1}ms.");
#else
        // The Tapline-8 reference cell measured 2.220s steady in Release —
        // the bank-gated economy runs matches deeper into the horizon than
        // the 1.241s Tapline-3 reference this gate was first cut for.
        Assert.True(
            steadyElapsed.Elapsed < TimeSpan.FromSeconds(3),
            $"Steady hosted entrant execution took {steadyElapsed.Elapsed.TotalMilliseconds:F1}ms.");
#endif
    }

    private static async Task<UserResponse> RegisterAsync(HttpClient client)
    {
        string identity = Guid.NewGuid().ToString("N");
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/accounts/register",
            new
            {
                displayName = $"Arc Relay {identity[..8]}",
                email = $"arc-relay-{identity}@example.test",
                password = "correct-horse-battery-staple",
            });
        response.EnsureSuccessStatusCode();
        return Assert.IsType<UserResponse>(
            await response.Content.ReadFromJsonAsync<UserResponse>());
    }

    private static async Task<TacticalSheetResponse> SaveAsync(
        HttpClient client,
        string name,
        string tacticsJson)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/sheets",
            new SaveTacticalSheetRequest(
                name,
                ExpectedRevision: null,
                tacticsJson,
                EnterLadder: true));
        Assert.True(response.IsSuccessStatusCode,
            $"Sheet save failed with {(int)response.StatusCode}: "
            + await response.Content.ReadAsStringAsync());
        return Assert.IsType<TacticalSheetResponse>(
            await response.Content.ReadFromJsonAsync<TacticalSheetResponse>());
    }
}
