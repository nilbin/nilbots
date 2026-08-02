using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using BotArena.App.Accounts;
using BotArena.App.ArcRelay;
using BotArena.App.Competition;
using BotArena.App.Jobs;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.App.Storage;
using BotArena.Engine;
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
        ArcRelayCatalogResponse catalog = Assert.IsType<ArcRelayCatalogResponse>(
            await client.GetFromJsonAsync<ArcRelayCatalogResponse>(
                "/api/arc-relay/catalog"));
        Assert.Equal(ArcRelayPlayerSheetCodec.SlotCount, catalog.NewSheetTemplate.Slots.Count);

        ArcRelaySheetResponse first = await SaveAsync(
            client,
            "First line",
            catalog.NewSheetTemplate);
        ArcRelaySheetDocument counterPlan = catalog.NewSheetTemplate with
        {
            Policies = catalog.NewSheetTemplate.Policies with
            {
                Carrier = catalog.NewSheetTemplate.Policies.Carrier with
                {
                    HandoffHealthAtOrBelow =
                        catalog.NewSheetTemplate.Policies.Carrier.HandoffHealthAtOrBelow + 1,
                },
            },
        };
        ArcRelaySheetResponse second = await SaveAsync(
            client,
            "Counter line",
            counterPlan);
        Assert.NotEqual(first.ContentHash, second.ContentHash);

        HttpResponseMessage queued = await client.PostAsJsonAsync(
            "/api/arc-relay/matches",
            new CreateArcRelayMatchRequest(first.Id, second.Id, Seed: 104729));
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
            Assert.Equal(ArcRelayBroadcastDocument.FormatVersion, match.ReplayFormatVersion);
            Assert.Equal(1.25, match.PresentationTicksPerSecond);
            Assert.Equal(2, match.TeamResults.Count);
            Assert.Equal(2, match.Participants.Count);
            output.WriteLine("end-tick={0}", match.EndTick);
            Assert.All(match.Participants, participant =>
            {
                Assert.Equal(owner.DisplayName, participant.OwnerDisplayNameSnapshot);
                Assert.Equal(ArcRelayPlaylistDefinition.StockArtifactHash, participant.ArtifactHashSnapshot);
                Assert.NotNull(participant.SheetIdSnapshot);
                Assert.NotNull(participant.EntrantIdSnapshot);
                Assert.Equal("sheet", participant.EntrantKindSnapshot);
                Assert.Equal(1, participant.SheetRevisionSnapshot);
                Assert.NotNull(participant.SheetHashSnapshot);
                Assert.NotNull(participant.SheetCanonicalJsonSnapshot);
                Assert.NotEmpty(Assert.IsType<byte[]>(participant.MindDataSnapshot));
            });
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
            ArcRelayPlaylistDefinition.StockArtifactHash,
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
            "/api/arc-relay/matches",
            new CreateArcRelayMatchRequest(first.Id, second.Id, Seed: 104729));
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
            "/api/arc-relay/matches",
            new CreateArcRelayMatchRequest(first.Id, second.Id, Seed: 104729));
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

        // This is deliberately generous and only catches a regression back to
        // per-sheet WASM/build execution. The stricter warm-host performance
        // target is measured by the repeatable benchmark, not by a shared CI VM.
        Assert.True(
            elapsed.Elapsed < TimeSpan.FromSeconds(5),
            $"Trusted hosted execution took {elapsed.Elapsed.TotalSeconds:F3}s.");
        // The entrant lane performs the same deterministic simulation plus immutable
        // identity/composition/crest snapshot checks. Keep a hard ceiling that catches a
        // return to per-sheet builds or WASM while leaving shared CI enough scheduling
        // headroom; production Release measurements remain reported separately.
        Assert.True(
            steadyElapsed.Elapsed < TimeSpan.FromSeconds(2),
            $"Steady hosted entrant execution took {steadyElapsed.Elapsed.TotalMilliseconds:F1}ms.");
    }

    private static async Task<UserResponse> RegisterAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/accounts/register",
            new
            {
                displayName = "Arc Relay Owner",
                email = $"arc-relay-{Guid.NewGuid():N}@example.test",
                password = "correct-horse-battery-staple",
            });
        response.EnsureSuccessStatusCode();
        return Assert.IsType<UserResponse>(
            await response.Content.ReadFromJsonAsync<UserResponse>());
    }

    private static async Task<ArcRelaySheetResponse> SaveAsync(
        HttpClient client,
        string name,
        ArcRelaySheetDocument document)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/arc-relay/sheets",
            new SaveArcRelaySheetRequest(name, ExpectedRevision: null, document));
        response.EnsureSuccessStatusCode();
        return Assert.IsType<ArcRelaySheetResponse>(
            await response.Content.ReadFromJsonAsync<ArcRelaySheetResponse>());
    }
}
