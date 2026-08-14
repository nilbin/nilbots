using System.Text.Json;
using BotArena.App.Bots;
using BotArena.App.Shared;
using BotArena.App.Storage;
using BotArena.Engine;
using BotArena.Toolchain;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Competition;

/// <summary>Seeds the immutable Arc Relay playlist and frozen stock identity.</summary>
public sealed class ArcRelayPlaylistSeeder(
    AppDbContext db,
    IObjectStore objectStore)
{
    public const string StockBotSlug = "arc-relay-stock-mind";
    public const string ForwardStockBotSlug = "arc-relay-forward-stock-mind";
    public const string TacticalPlaybookBotSlug =
        "arc-relay-tactical-playbook-mind";
    public const string AmbushTacticalPlaybookBotSlug =
        "arc-relay-ambush-11-tactical-playbook-mind";
    public const string TaplineTacticalPlaybookBotSlug =
        "arc-relay-tapline-tactical-playbook-mind";

    public async Task<ArcRelaySeedResult> SeedAsync(
        CancellationToken cancellationToken = default)
    {
        ArcRelayPlaylistDefinition expected = ArcRelayPlaylistDefinition.Create();
        Playlist? playlist = await db.Playlists.SingleOrDefaultAsync(
            value => value.Key == ArcRelayPlaylistDefinition.PlaylistKey,
            cancellationToken);
        if (playlist is null)
        {
            playlist = new Playlist
            {
                Key = ArcRelayPlaylistDefinition.PlaylistKey,
                DisplayName = ArcRelayPlaylistDefinition.DisplayName,
            };
            db.Playlists.Add(playlist);
            await db.SaveChangesAsync(cancellationToken);
        }

        PlaylistVersion? version = await db.PlaylistVersions.SingleOrDefaultAsync(
            value => value.PlaylistId == playlist.Id
                && value.Version == ArcRelayPlaylistDefinition.Version,
            cancellationToken);
        if (version is null)
        {
            version = new PlaylistVersion
            {
                PlaylistId = playlist.Id,
                Version = ArcRelayPlaylistDefinition.Version,
                GameModeId = expected.Match.Rules.GameMode.ModeId,
                RulesetId = expected.Match.Rules.RulesetId,
                MatchFormatId = expected.Match.Format.FormatId,
                MapPoolId = expected.Match.Map.Id,
                SeriesPolicyId = ArcRelayPlaylistDefinition.SeriesPolicyId,
                MatchmakingPolicyId = ArcRelayPlaylistDefinition.MatchmakingPolicyId,
                AdmissionPolicyId = expected.AdmissionPolicyId,
                ExecutionPolicyId = expected.ExecutionPolicyId,
                ExecutionEngineVersion = expected.ExecutionEngineVersion,
                CanonicalDefinition = expected.CanonicalDefinition,
                DefinitionFingerprint = expected.DefinitionFingerprint,
                Provenance = expected.Provenance,
                Visibility = ArcRelayPlaylistDefinition.Visibility,
            };
            db.PlaylistVersions.Add(version);
            await db.SaveChangesAsync(cancellationToken);
        }
        expected.Validate(playlist, version);

        BotVersion stockVersion = await SeedStockMindAsync(cancellationToken);
        await SeedForwardStockMindAsync(cancellationToken);
        await SeedTacticalPlaybookMindAsync(cancellationToken);
        await SeedAmbushTacticalPlaybookMindAsync(cancellationToken);
        await SeedTaplineTacticalPlaybookMindAsync(cancellationToken);
        if (await db.Ladders.AnyAsync(
                ladder => ladder.PlaylistVersionId == version.Id,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "Arc Relay v1 is an unranked sheet-scrimmage playlist and must not have a ladder.");
        }
        return new ArcRelaySeedResult(version, stockVersion);
    }

    private async Task<BotVersion> SeedStockMindAsync(
        CancellationToken cancellationToken)
    {
        string? wasmPath = RepoPaths.FindUpward(
            Path.Combine("arena-bots", "arc-relay", "stock-mind-v0", "bot.wasm"));
        if (wasmPath is null)
            throw new InvalidOperationException("Frozen Arc Relay stock mind artifact is missing.");
        string artifactHash = BotBuilder.Sha256File(wasmPath);
        if (!string.Equals(
                artifactHash,
                ArcRelayPlaylistDefinition.StockArtifactHash,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Frozen Arc Relay stock mind hash moved.");
        }
        string artifactKey = ObjectKeys.Artifact(artifactHash);
        await using (var stream = File.OpenRead(wasmPath))
            await objectStore.PutAsync(artifactKey, stream, artifactHash, cancellationToken);

        var system = await BuiltInBotSeeder.GetOrCreateSystemUser(db, cancellationToken);
        Bot? bot = await db.Bots.Include(value => value.Versions)
            .SingleOrDefaultAsync(value => value.Slug == StockBotSlug, cancellationToken);
        if (bot is null)
        {
            bot = new Bot
            {
                OwnerUserId = system.Id,
                Name = "Arc Relay stock mind",
                Slug = StockBotSlug,
                Accent = "#22d3ee",
                LookId = "arc-relay",
                ProjectileLookId = ArcRelayH0ReplayPresentation.ProjectileLookId,
            };
            db.Bots.Add(bot);
        }
        BotVersion? version = bot.Versions.SingleOrDefault(value =>
            string.Equals(value.ArtifactHash, artifactHash, StringComparison.Ordinal));
        if (version is null)
        {
            foreach (BotVersion old in bot.Versions)
                old.IsActive = false;
            version = new BotVersion
            {
                BotId = bot.Id,
                VersionNumber = bot.Versions.Count + 1,
                EntryType = "ArcRelayStockMind",
                SourcesJson = "[]",
                SourceHash = "c8182e133a202733ef7c6b43367097eb118d2295a91dcdbf592e6fe13ff48f79",
                Status = BuildStatus.Built,
                ArtifactKey = artifactKey,
                ArtifactHash = artifactHash,
                BuildReceiptJson = JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    kind = "frozen-first-party-stock-mind",
                    sheetSchema = ArcRelay.ArcRelayLegacySnapshotCodec.SchemaId,
                }),
                SupportedContractProfiles = [BotArenaVersions.GenericMindContractProfileId],
                GuestBotName = "Arc Relay stock mind",
                GameRulesVersion = ArcRelayH0Definition.RulesetId,
                RuntimeProtocolVersion = BotArenaVersions.RuntimeProtocolVersion,
                RuntimeConfigurationVersion = BotArenaVersions.GenericMindRuntimeConfigurationVersion,
                BuiltAt = DateTime.UtcNow,
                IsActive = true,
            };
            bot.Versions.Add(version);
            db.BotVersions.Add(version);
        }
        await db.SaveChangesAsync(cancellationToken);
        return version;
    }

    private async Task<BotVersion> SeedForwardStockMindAsync(
        CancellationToken cancellationToken)
    {
        string? wasmPath = RepoPaths.FindUpward(
            Path.Combine(
                "arena-bots",
                "arc-relay",
                "stock-mind-v4-frozen-999183",
                "bot.wasm"));
        if (wasmPath is null)
        {
            throw new InvalidOperationException(
                "Forward-combat Arc Relay stock mind artifact is missing.");
        }
        string artifactHash = BotBuilder.Sha256File(wasmPath);
        if (!string.Equals(
                artifactHash,
                ArcRelayPlaylistDefinition.ForwardStockArtifactHash,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Forward-combat Arc Relay stock mind hash moved.");
        }
        string artifactKey = ObjectKeys.Artifact(artifactHash);
        await using (var stream = File.OpenRead(wasmPath))
        {
            await objectStore.PutAsync(
                artifactKey,
                stream,
                artifactHash,
                cancellationToken);
        }

        var system = await BuiltInBotSeeder.GetOrCreateSystemUser(
            db,
            cancellationToken);
        Bot? bot = await db.Bots.Include(value => value.Versions)
            .SingleOrDefaultAsync(
                value => value.Slug == ForwardStockBotSlug,
                cancellationToken);
        if (bot is null)
        {
            bot = new Bot
            {
                OwnerUserId = system.Id,
                Name = "Arc Relay forward-combat stock mind",
                Slug = ForwardStockBotSlug,
                Accent = "#22d3ee",
                LookId = "arc-relay",
                ProjectileLookId = ArcRelayH0ReplayPresentation.ProjectileLookId,
            };
            db.Bots.Add(bot);
        }
        BotVersion? version = bot.Versions.SingleOrDefault(value =>
            string.Equals(
                value.ArtifactHash,
                artifactHash,
                StringComparison.Ordinal));
        if (version is null)
        {
            foreach (BotVersion old in bot.Versions)
                old.IsActive = false;
            version = new BotVersion
            {
                BotId = bot.Id,
                VersionNumber = bot.Versions.Count + 1,
                EntryType = "ArcRelayStrategyMind",
                SourcesJson = "[]",
                SourceHash =
                    "637822b12b9b0643410ddaa42919d009f0a4727768e210b792ae3179e5a8b23b",
                Status = BuildStatus.Built,
                ArtifactKey = artifactKey,
                ArtifactHash = artifactHash,
                BuildReceiptJson = JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    kind = "versioned-first-party-stock-mind",
                    combatProfile = ArcRelayLoopProfile.ForwardCombat.Id,
                    sheetSchema = ArcRelay.ArcRelayLegacySnapshotCodec.SchemaId,
                    sourceSha256 =
                        "637822b12b9b0643410ddaa42919d009f0a4727768e210b792ae3179e5a8b23b",
                    arenaBasicsSha256 =
                        "35ff4bcd0c634e01f3f6f89d9741fe9ed02a57b9a32cc04fd3d1d20fd807b8c1",
                    artifactSha256 = artifactHash,
                }),
                SupportedContractProfiles =
                    [BotArenaVersions.GenericMindContractProfileId],
                GuestBotName = "Arc Relay forward-combat stock mind",
                GameRulesVersion = ArcRelayLoopProfile.ForwardCombat.RulesetId,
                RuntimeProtocolVersion = BotArenaVersions.RuntimeProtocolVersion,
                RuntimeConfigurationVersion =
                    BotArenaVersions.GenericMindRuntimeConfigurationVersion,
                BuiltAt = DateTime.UtcNow,
                IsActive = true,
            };
            bot.Versions.Add(version);
            db.BotVersions.Add(version);
        }
        await db.SaveChangesAsync(cancellationToken);
        return version;
    }

    private async Task<BotVersion> SeedTacticalPlaybookMindAsync(
        CancellationToken cancellationToken)
    {
        string? wasmPath = RepoPaths.FindUpward(Path.Combine(
            "arena-bots",
            "arc-relay",
            "tactical-playbook-mind-v1",
            "bot.wasm"));
        if (wasmPath is null)
        {
            throw new InvalidOperationException(
                "Tactical-playbook Arc Relay mind artifact is missing.");
        }
        string artifactHash = BotBuilder.Sha256File(wasmPath);
        if (!string.Equals(
                artifactHash,
                ArcRelayEntrantPlaylistDefinition.Tapline4TacticalArtifactHash,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Tactical-playbook Arc Relay mind hash moved.");
        }
        string artifactKey = ObjectKeys.Artifact(artifactHash);
        await using (var stream = File.OpenRead(wasmPath))
        {
            await objectStore.PutAsync(
                artifactKey, stream, artifactHash, cancellationToken);
        }

        var system = await BuiltInBotSeeder.GetOrCreateSystemUser(
            db, cancellationToken);
        Bot? bot = await db.Bots.Include(value => value.Versions)
            .SingleOrDefaultAsync(value =>
                value.Slug == TacticalPlaybookBotSlug, cancellationToken);
        if (bot is null)
        {
            bot = new Bot
            {
                OwnerUserId = system.Id,
                Name = "Arc Relay tactical-playbook mind",
                Slug = TacticalPlaybookBotSlug,
                Accent = "#22d3ee",
                LookId = "arc-relay",
                ProjectileLookId =
                    ArcRelayH0ReplayPresentation.ProjectileLookId,
            };
            db.Bots.Add(bot);
        }
        BotVersion? version = bot.Versions.SingleOrDefault(value =>
            string.Equals(
                value.ArtifactHash, artifactHash, StringComparison.Ordinal));
        if (version is null)
        {
            foreach (BotVersion old in bot.Versions)
                old.IsActive = false;
            version = new BotVersion
            {
                BotId = bot.Id,
                VersionNumber = bot.Versions.Count + 1,
                EntryType = "ArcRelayTacticalPlaybookMind",
                SourcesJson = "[]",
                SourceHash = artifactHash,
                Status = BuildStatus.Built,
                ArtifactKey = artifactKey,
                ArtifactHash = artifactHash,
                BuildReceiptJson = JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    kind = "versioned-first-party-tactical-playbook-mind",
                    playbookSchema = "arc-relay-orders-v2",
                    buildInputSha256 = ArcRelayEntrantPlaylistDefinition
                        .Tapline4TacticalBuildInputHash,
                    artifactSha256 = artifactHash,
                }),
                SupportedContractProfiles =
                    [BotArenaVersions.GenericMindContractProfileId],
                GuestBotName = "Arc Relay tactical-playbook mind",
                GameRulesVersion = ArcRelayLoopProfile.Tapline4.RulesetId,
                RuntimeProtocolVersion =
                    BotArenaVersions.RuntimeProtocolVersion,
                RuntimeConfigurationVersion =
                    BotArenaVersions.GenericMindRuntimeConfigurationVersion,
                BuiltAt = DateTime.UtcNow,
                IsActive = true,
            };
            bot.Versions.Add(version);
            db.BotVersions.Add(version);
        }
        await db.SaveChangesAsync(cancellationToken);
        return version;
    }

    private async Task SeedAmbushTacticalPlaybookMindAsync(
        CancellationToken cancellationToken)
    {
        await SeedAmbushTacticalPlaybookMindVersionAsync(
            "ambush-warren-11-stock-v1",
            ArcRelayEntrantPlaylistDefinition.HistoricalTacticalArtifactHash,
            ArcRelayEntrantPlaylistDefinition.HistoricalStockPlaybookSourceHash,
            buildInputHash: null,
            active: false,
            cancellationToken);
        await SeedAmbushTacticalPlaybookMindVersionAsync(
            "ambush-warren-11-stock-v2",
            ArcRelayEntrantPlaylistDefinition.WasmRepairTacticalArtifactHash,
            ArcRelayEntrantPlaylistDefinition.HistoricalStockPlaybookSourceHash,
            ArcRelayEntrantPlaylistDefinition.WasmRepairTacticalBuildInputHash,
            active: false,
            cancellationToken);
        await SeedAmbushTacticalPlaybookMindVersionAsync(
            "ambush-warren-11-stock-v3",
            ArcRelayEntrantPlaylistDefinition
                .DirectLadderTacticalArtifactHash,
            ArcRelayEntrantPlaylistDefinition.StockPlaybookSourceHash,
            ArcRelayEntrantPlaylistDefinition
                .DirectLadderTacticalBuildInputHash,
            active: false,
            cancellationToken);
        await SeedAmbushTacticalPlaybookMindVersionAsync(
            "ambush-warren-11-stock-v4",
            ArcRelayEntrantPlaylistDefinition
                .SemanticIntentTacticalArtifactHash,
            ArcRelayEntrantPlaylistDefinition.StockPlaybookSourceHash,
            ArcRelayEntrantPlaylistDefinition
                .SemanticIntentTacticalBuildInputHash,
            active: false,
            cancellationToken);
        await SeedAmbushTacticalPlaybookMindVersionAsync(
            "ambush-warren-11-stock-v5",
            ArcRelayEntrantPlaylistDefinition
                .BoundedMovementTacticalArtifactHash,
            ArcRelayEntrantPlaylistDefinition.StockPlaybookSourceHash,
            ArcRelayEntrantPlaylistDefinition
                .BoundedMovementTacticalBuildInputHash,
            active: false,
            cancellationToken);
        await SeedAmbushTacticalPlaybookMindVersionAsync(
            "ambush-warren-11-stock-v6",
            ArcRelayEntrantPlaylistDefinition
                .RelativeWellTacticalArtifactHash,
            ArcRelayEntrantPlaylistDefinition.StockPlaybookSourceHash,
            ArcRelayEntrantPlaylistDefinition
                .RelativeWellTacticalBuildInputHash,
            active: false,
            cancellationToken);
        await SeedAmbushTacticalPlaybookMindVersionAsync(
            "ambush-warren-11-stock-v7",
            ArcRelayEntrantPlaylistDefinition
                .StickyRouteTacticalArtifactHash,
            ArcRelayEntrantPlaylistDefinition.StockPlaybookSourceHash,
            ArcRelayEntrantPlaylistDefinition
                .StickyRouteTacticalBuildInputHash,
            active: false,
            cancellationToken);
        await SeedAmbushTacticalPlaybookMindVersionAsync(
            "ambush-warren-11-stock-v8",
            ArcRelayEntrantPlaylistDefinition
                .CarrierClearanceTacticalArtifactHash,
            ArcRelayEntrantPlaylistDefinition.StockPlaybookSourceHash,
            ArcRelayEntrantPlaylistDefinition
                .CarrierClearanceTacticalBuildInputHash,
            active: false,
            cancellationToken);
        await SeedAmbushTacticalPlaybookMindVersionAsync(
            "ambush-warren-11-stock-v9",
            ArcRelayEntrantPlaylistDefinition
                .TrafficDetourTacticalArtifactHash,
            ArcRelayEntrantPlaylistDefinition.StockPlaybookSourceHash,
            ArcRelayEntrantPlaylistDefinition
                .TrafficDetourTacticalBuildInputHash,
            active: false,
            cancellationToken);
        await SeedAmbushTacticalPlaybookMindVersionAsync(
            "ambush-warren-11-stock-v10",
            ArcRelayEntrantPlaylistDefinition
                .StableCarrierTacticalArtifactHash,
            ArcRelayEntrantPlaylistDefinition.StockPlaybookSourceHash,
            ArcRelayEntrantPlaylistDefinition
                .StableCarrierTacticalBuildInputHash,
            active: false,
            cancellationToken);
        await SeedAmbushTacticalPlaybookMindVersionAsync(
            "ambush-warren-11-stock-v11",
            ArcRelayEntrantPlaylistDefinition
                .LooseCoreMemoryTacticalArtifactHash,
            ArcRelayEntrantPlaylistDefinition.StockPlaybookSourceHash,
            ArcRelayEntrantPlaylistDefinition
                .LooseCoreMemoryTacticalBuildInputHash,
            active: false,
            cancellationToken);
        await SeedAmbushTacticalPlaybookMindVersionAsync(
            "ambush-warren-11-stock-v12",
            ArcRelayEntrantPlaylistDefinition.TacticalArtifactHash,
            ArcRelayEntrantPlaylistDefinition.StockPlaybookSourceHash,
            ArcRelayEntrantPlaylistDefinition.TacticalBuildInputHash,
            active: true,
            cancellationToken);
    }

    private async Task<BotVersion> SeedAmbushTacticalPlaybookMindVersionAsync(
        string artifactDirectory,
        string expectedArtifactHash,
        string stockPlaybookSourceHash,
        string? buildInputHash,
        bool active,
        CancellationToken cancellationToken)
    {
        string? wasmPath = RepoPaths.FindUpward(Path.Combine(
            "arena-bots",
            "arc-relay",
            artifactDirectory,
            "bot.wasm"));
        if (wasmPath is null)
        {
            throw new InvalidOperationException(
                "Frozen ambush-warren-11 tactical mind artifact is missing.");
        }
        string artifactHash = BotBuilder.Sha256File(wasmPath);
        if (!string.Equals(
                artifactHash,
                expectedArtifactHash,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Frozen ambush-warren-11 tactical mind hash moved.");
        }
        string artifactKey = ObjectKeys.Artifact(artifactHash);
        await using (var stream = File.OpenRead(wasmPath))
        {
            await objectStore.PutAsync(
                artifactKey, stream, artifactHash, cancellationToken);
        }

        var system = await BuiltInBotSeeder.GetOrCreateSystemUser(
            db, cancellationToken);
        Bot? bot = await db.Bots.Include(value => value.Versions)
            .SingleOrDefaultAsync(value =>
                value.Slug == AmbushTacticalPlaybookBotSlug,
                cancellationToken);
        if (bot is null)
        {
            bot = new Bot
            {
                OwnerUserId = system.Id,
                Name = "Arc Relay ambush-11 frozen Wellwright",
                Slug = AmbushTacticalPlaybookBotSlug,
                Accent = "#22d3ee",
                LookId = "arc-relay",
                ProjectileLookId =
                    ArcRelayH0ReplayPresentation.ProjectileLookId,
            };
            db.Bots.Add(bot);
        }
        BotVersion? version = bot.Versions.SingleOrDefault(value =>
            string.Equals(
                value.ArtifactHash, artifactHash, StringComparison.Ordinal));
        if (version is null)
        {
            if (active)
            {
                foreach (BotVersion old in bot.Versions)
                    old.IsActive = false;
            }
            version = new BotVersion
            {
                BotId = bot.Id,
                VersionNumber = bot.Versions.Count + 1,
                EntryType = "ArcRelayTacticalPlaybookMind",
                SourcesJson = "[]",
                SourceHash = artifactHash,
                Status = BuildStatus.Built,
                ArtifactKey = artifactKey,
                ArtifactHash = artifactHash,
                BuildReceiptJson = JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    kind = "frozen-first-party-ambush-warren-11-stock",
                    loopProfile = ArcRelayLoopProfile.AmbushWarren11.Id,
                    playbookSchema = "arc-relay-tactical-playbook-v1",
                    stockSheetId =
                        ArcRelayEntrantPlaylistDefinition.StockSheetId,
                    stockPlaybookSourceHash,
                    stockLayoutSourceHash =
                        ArcRelayEntrantPlaylistDefinition.StockLayoutSourceHash,
                    buildInputSha256 = buildInputHash,
                    artifactSha256 = artifactHash,
                }),
                SupportedContractProfiles =
                    [BotArenaVersions.GenericMindContractProfileId],
                GuestBotName = "Arc Relay ambush-11 Wellwright",
                GameRulesVersion = ArcRelayLoopProfile.AmbushWarren11.RulesetId,
                RuntimeProtocolVersion =
                    BotArenaVersions.RuntimeProtocolVersion,
                RuntimeConfigurationVersion =
                    BotArenaVersions.GenericMindRuntimeConfigurationVersion,
                BuiltAt = DateTime.UtcNow,
                IsActive = active,
            };
            bot.Versions.Add(version);
            db.BotVersions.Add(version);
        }
        if (active)
        {
            foreach (BotVersion old in bot.Versions)
                old.IsActive = old.Id == version.Id;
            version.IsActive = true;
        }
        await db.SaveChangesAsync(cancellationToken);
        return version;
    }

    private async Task<BotVersion> SeedTaplineTacticalPlaybookMindAsync(
        CancellationToken cancellationToken)
    {
        await SeedTaplineTacticalPlaybookMindVersionAsync(
            "ambush-warren-11-stock-v12",
            ArcRelayEntrantPlaylistDefinition.TacticalArtifactHash,
            "arc-relay-tactical-playbook-v1",
            ArcRelayEntrantPlaylistDefinition.StockSheetId,
            ArcRelayEntrantPlaylistDefinition.TaplineStockPlaybookSourceHash,
            ArcRelayEntrantPlaylistDefinition.TaplineStockLayoutSourceHash,
            ArcRelayEntrantPlaylistDefinition.TacticalBuildInputHash,
            ArcRelayLoopProfile.Tapline1,
            active: false,
            cancellationToken);
        await SeedTaplineTacticalPlaybookMindVersionAsync(
            "tapline-1-stock-v1",
            ArcRelayEntrantPlaylistDefinition.OrdersV2TacticalArtifactHash,
            "arc-relay-orders-v2",
            ArcRelayEntrantPlaylistDefinition.OrdersStockSheetId,
            ArcRelayEntrantPlaylistDefinition.OrdersV2StockPlaybookSourceHash,
            ArcRelayEntrantPlaylistDefinition.OrdersStockMapPlanSourceHash,
            ArcRelayEntrantPlaylistDefinition.OrdersV2TacticalBuildInputHash,
            ArcRelayLoopProfile.Tapline1,
            active: false,
            cancellationToken);
        await SeedTaplineTacticalPlaybookMindVersionAsync(
            "tapline-3-stock-v1",
            ArcRelayEntrantPlaylistDefinition.OrdersTacticalArtifactHash,
            "arc-relay-orders-v2",
            ArcRelayEntrantPlaylistDefinition.OrdersStockSheetId,
            ArcRelayEntrantPlaylistDefinition.OrdersStockPlaybookSourceHash,
            ArcRelayEntrantPlaylistDefinition.OrdersStockMapPlanSourceHash,
            ArcRelayEntrantPlaylistDefinition.OrdersTacticalBuildInputHash,
            ArcRelayLoopProfile.Tapline3,
            active: false,
            cancellationToken);
        return await SeedTaplineTacticalPlaybookMindVersionAsync(
            "tapline-4-stock-v1",
            ArcRelayEntrantPlaylistDefinition.Tapline4TacticalArtifactHash,
            "arc-relay-orders-v2",
            ArcRelayEntrantPlaylistDefinition.Tapline4StockSheetId,
            ArcRelayEntrantPlaylistDefinition.Tapline4StockPlaybookSourceHash,
            ArcRelayEntrantPlaylistDefinition.Tapline4StockMapPlanSourceHash,
            ArcRelayEntrantPlaylistDefinition.Tapline4TacticalBuildInputHash,
            ArcRelayLoopProfile.Tapline4,
            active: true,
            cancellationToken);
    }

    private async Task<BotVersion> SeedTaplineTacticalPlaybookMindVersionAsync(
        string artifactDirectory,
        string expectedArtifactHash,
        string playbookSchema,
        string stockSheetId,
        string stockPlaybookSourceHash,
        string stockMapPlanSourceHash,
        string buildInputHash,
        ArcRelayLoopProfile loopProfile,
        bool active,
        CancellationToken cancellationToken)
    {
        string? wasmPath = RepoPaths.FindUpward(Path.Combine(
            "arena-bots",
            "arc-relay",
            artifactDirectory,
            "bot.wasm"));
        if (wasmPath is null)
        {
            throw new InvalidOperationException(
                "Frozen Tapline tactical mind artifact is missing.");
        }
        string artifactHash = BotBuilder.Sha256File(wasmPath);
        if (!string.Equals(
                artifactHash,
                expectedArtifactHash,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Frozen Tapline tactical mind hash moved.");
        }
        string artifactKey = ObjectKeys.Artifact(artifactHash);
        await using (var stream = File.OpenRead(wasmPath))
        {
            await objectStore.PutAsync(
                artifactKey, stream, artifactHash, cancellationToken);
        }

        var system = await BuiltInBotSeeder.GetOrCreateSystemUser(
            db, cancellationToken);
        Bot? bot = await db.Bots.Include(value => value.Versions)
            .SingleOrDefaultAsync(value =>
                value.Slug == TaplineTacticalPlaybookBotSlug,
                cancellationToken);
        if (bot is null)
        {
            bot = new Bot
            {
                OwnerUserId = system.Id,
                Name = "Arc Relay Tapline frozen Wellwright",
                Slug = TaplineTacticalPlaybookBotSlug,
                Accent = "#22d3ee",
                LookId = "arc-relay",
                ProjectileLookId =
                    ArcRelayH0ReplayPresentation.ProjectileLookId,
            };
            db.Bots.Add(bot);
        }
        BotVersion? version = bot.Versions.SingleOrDefault(value =>
            string.Equals(
                value.ArtifactHash, artifactHash, StringComparison.Ordinal));
        if (version is null)
        {
            if (active)
            {
                foreach (BotVersion old in bot.Versions)
                    old.IsActive = false;
            }
            string buildReceiptJson = string.Equals(
                playbookSchema, "arc-relay-orders-v2", StringComparison.Ordinal)
                ? JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    kind = "frozen-first-party-tapline-stock",
                    loopProfile = loopProfile.Id,
                    playbookSchema,
                    stockSheetId,
                    stockPlaybookSourceHash,
                    stockMapPlanSourceHash,
                    buildInputSha256 = buildInputHash,
                    artifactSha256 = artifactHash,
                })
                : JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    kind = "frozen-first-party-tapline-stock",
                    loopProfile = loopProfile.Id,
                    playbookSchema,
                    stockSheetId,
                    stockPlaybookSourceHash,
                    stockLayoutSourceHash = stockMapPlanSourceHash,
                    buildInputSha256 = buildInputHash,
                    artifactSha256 = artifactHash,
                });
            version = new BotVersion
            {
                BotId = bot.Id,
                VersionNumber = bot.Versions.Count + 1,
                EntryType = "ArcRelayTacticalPlaybookMind",
                SourcesJson = "[]",
                SourceHash = artifactHash,
                Status = BuildStatus.Built,
                ArtifactKey = artifactKey,
                ArtifactHash = artifactHash,
                BuildReceiptJson = buildReceiptJson,
                SupportedContractProfiles =
                    [BotArenaVersions.GenericMindContractProfileId],
                GuestBotName = "Arc Relay Tapline Wellwright",
                GameRulesVersion = loopProfile.RulesetId,
                RuntimeProtocolVersion =
                    BotArenaVersions.RuntimeProtocolVersion,
                RuntimeConfigurationVersion =
                    BotArenaVersions.GenericMindRuntimeConfigurationVersion,
                BuiltAt = DateTime.UtcNow,
                IsActive = active,
            };
            bot.Versions.Add(version);
            db.BotVersions.Add(version);
        }
        if (active)
        {
            foreach (BotVersion candidate in bot.Versions)
                candidate.IsActive = candidate.Id == version.Id;
            version.IsActive = true;
        }
        await db.SaveChangesAsync(cancellationToken);
        return version;
    }
}

public sealed record ArcRelaySeedResult(
    PlaylistVersion PlaylistVersion,
    BotVersion StockBotVersion);
