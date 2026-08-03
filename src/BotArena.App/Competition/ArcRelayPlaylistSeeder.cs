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
                    sheetSchema = ArcRelay.ArcRelayPlayerSheetCodec.SchemaId,
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
                "stock-mind-v4",
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
                    "cc85c8da7f8e9c2dfd87e20ec1985dce77c2b2f2da7f01bc2f0683a224d2cb96",
                Status = BuildStatus.Built,
                ArtifactKey = artifactKey,
                ArtifactHash = artifactHash,
                BuildReceiptJson = JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    kind = "versioned-first-party-stock-mind",
                    combatProfile = ArcRelayLoopProfile.ForwardCombat.Id,
                    sheetSchema = ArcRelay.ArcRelayPlayerSheetCodec.SchemaId,
                    sourceSha256 =
                        "cc85c8da7f8e9c2dfd87e20ec1985dce77c2b2f2da7f01bc2f0683a224d2cb96",
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
}

public sealed record ArcRelaySeedResult(
    PlaylistVersion PlaylistVersion,
    BotVersion StockBotVersion);
