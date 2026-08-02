using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Jobs;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.Engine;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.ArcRelay;

/// <summary>Creates one v2 match from immutable entrant revision snapshots.</summary>
public sealed class ArcRelayMatchAdmissionService(
    AppDbContext db,
    ArcRelayPlayerSheetCodec sheetCodec,
    ArcRelayClassCatalog classCatalog)
{
    public async Task<Match> CreateAsync(
        ArcRelayEntrant first,
        ArcRelayEntrant second,
        string lane,
        Guid? initiatedBy,
        long? seed,
        CancellationToken cancellationToken)
    {
        ParticipantMaterial[] materials =
        [
            await MaterializeAsync(first, cancellationToken),
            await MaterializeAsync(second, cancellationToken),
        ];
        return await CreateCoreAsync(materials, lane, initiatedBy, seed, cancellationToken);
    }

    public async Task<Match> CreatePreflightAsync(
        ArcRelayEntrant customMind,
        Guid initiatedBy,
        long? seed,
        CancellationToken cancellationToken)
    {
        ParticipantMaterial candidate = await MaterializeAsync(customMind, cancellationToken);
        ParticipantMaterial stock = await StockFixtureAsync(cancellationToken);
        return await CreateCoreAsync([candidate, stock], ArcRelayMatchLane.Preflight, initiatedBy, seed, cancellationToken);
    }

    private async Task<Match> CreateCoreAsync(
        ParticipantMaterial[] materials,
        string lane,
        Guid? initiatedBy,
        long? seed,
        CancellationToken cancellationToken)
    {
        ArcRelayEntrantPlaylistDefinition definition = ArcRelayEntrantPlaylistDefinition.Create();
        PlaylistVersion playlistVersion = await db.PlaylistVersions.SingleAsync(
            version => version.Version == ArcRelayEntrantPlaylistDefinition.Version &&
                db.Playlists.Any(playlist => playlist.Id == version.PlaylistId && playlist.Key == ArcRelayEntrantPlaylistDefinition.PlaylistKey),
            cancellationToken);
        ActorResolvedMatchDefinition resolved = definition.ResolveMatch(
        [
            new HostedGenericParticipantInput(0, 0, materials[0].Classes),
            new HostedGenericParticipantInput(1, 1, materials[1].Classes),
        ]);
        var match = new Match
        {
            MapId = resolved.Map.Id,
            MapVersion = resolved.Map.Version,
            Seed = seed ?? Random.Shared.NextInt64(),
            InitiatedByUserId = initiatedBy,
            GameRulesVersion = resolved.Rules.RulesetId,
            RuntimeConfigurationVersion = resolved.CapabilityVersions.RuntimeConfigurationVersion,
            PlaylistVersionId = playlistVersion.Id,
            ArcRelayLane = lane,
        };
        for (int index = 0; index < 2; index++)
        {
            ParticipantMaterial value = materials[index];
            match.Participants.Add(new MatchParticipant
            {
                MatchId = match.Id,
                Slot = index,
                TeamId = index,
                BotId = value.BotId,
                BotVersionId = value.BotVersionId,
                NameSnapshot = value.Name,
                OwnerDisplayNameSnapshot = value.OwnerName,
                AccentSnapshot = index == 0 ? "#22d3ee" : "#fb5360",
                LookIdSnapshot = "arc-relay-entrant",
                ProjectileLookIdSnapshot = ArcRelayH0ReplayPresentation.ProjectileLookId,
                ArtifactHashSnapshot = value.ArtifactHash,
                SheetIdSnapshot = value.SheetId,
                SheetRevisionSnapshot = value.SheetRevision,
                SheetNameSnapshot = value.SheetId is null ? null : value.Name,
                SheetHashSnapshot = value.SheetHash,
                SheetCanonicalJsonSnapshot = value.SheetJson,
                MindDataSnapshot = value.MindData,
                EntrantIdSnapshot = value.EntrantId,
                EntrantKindSnapshot = value.Kind,
                EntrantRevisionSnapshot = value.Revision,
                CrestSnapshot = value.Crest,
                CompositionSnapshot = value.CompositionJson,
                CompositionHashSnapshot = value.CompositionHash,
            });
        }
        db.Matches.Add(match);
        db.BackgroundJobs.Add(BackgroundJob.ExecuteGenericActorMatch(
            match.Id,
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.Version));
        return match;
    }

    private async Task<ParticipantMaterial> MaterializeAsync(
        ArcRelayEntrant entrant,
        CancellationToken cancellationToken)
    {
        string owner = await db.Users.Where(value => value.Id == entrant.OwnerUserId)
            .Select(value => value.DisplayName).SingleAsync(cancellationToken);
        if (entrant.Kind == ArcRelayEntrantKind.Sheet)
        {
            ArcRelaySheet sheet = await db.ArcRelaySheets.SingleAsync(value => value.Id == entrant.Id, cancellationToken);
            IReadOnlySet<string> all = classCatalog.All.Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
            ArcRelaySheetCompilation compiled = sheetCodec.Compile(sheetCodec.Read(sheet.CanonicalJson), all, $"{sheet.Id}:r{sheet.Revision}");
            if (!string.Equals(compiled.ContentHash, sheet.ContentHash, StringComparison.Ordinal))
                throw new InvalidDataException("Saved sheet content hash failed verification.");
            (Bot bot, BotVersion version) = await StockArtifactAsync(cancellationToken);
            string composition = ArcRelayComposition.Compile(
                new ArcRelayCompositionDeclaration(compiled.Classes), sheetCodec, all).CanonicalJson;
            string compositionHash = ArcRelayComposition.Compile(
                new ArcRelayCompositionDeclaration(compiled.Classes), sheetCodec, all).ContentHash;
            return new ParticipantMaterial(
                entrant.Id, "sheet", sheet.Revision, entrant.Name, owner,
                ArcRelayCrestGenerator.Snapshot(entrant.Id, entrant.CrestVariant),
                bot.Id, version.Id, ArcRelayPlaylistDefinition.StockArtifactHash,
                compiled.Classes, composition, compositionHash,
                sheet.Id, sheet.Revision, sheet.ContentHash, sheet.CanonicalJson, compiled.LinkedData);
        }

        BotVersion active = await db.BotVersions.SingleAsync(
            value => value.BotId == entrant.MindBotId && value.IsActive && value.Status == BuildStatus.Built,
            cancellationToken);
        if (!BotContractProfiles.Supports(active.SupportedContractProfiles, BotArenaVersions.GenericMindContractProfileId))
            throw new InvalidOperationException("Custom mind does not support the generic-mind contract.");
        ArcRelayCompositionDeclaration declaration = ArcRelayComposition.Read(entrant.CompositionJson!);
        return new ParticipantMaterial(
            entrant.Id, "mind", active.VersionNumber, entrant.Name, owner,
            ArcRelayCrestGenerator.Snapshot(entrant.Id, entrant.CrestVariant),
            entrant.MindBotId!.Value, active.Id, active.ArtifactHash!, declaration.ClassIds,
            entrant.CompositionJson!, entrant.CompositionHash!, null, null, null, null, null);
    }

    private async Task<ParticipantMaterial> StockFixtureAsync(CancellationToken cancellationToken)
    {
        Guid fixtureId = new("d7ed7a58-a59e-42d4-98f8-b6216711f188");
        (Bot bot, BotVersion version) = await StockArtifactAsync(cancellationToken);
        ArcRelaySheetDocument document = ArcRelayPlayerSheetCodec.NewSheetTemplate();
        IReadOnlySet<string> all = classCatalog.All.Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
        ArcRelaySheetCompilation compiled = sheetCodec.Compile(document, all, $"{fixtureId}:r1");
        ArcRelayCompositionCompilation composition = ArcRelayComposition.Compile(
            new ArcRelayCompositionDeclaration(compiled.Classes), sheetCodec, all);
        return new ParticipantMaterial(
            null, "stock-fixture", 1, "Stock validation", "Nilbots",
            ArcRelayCrestGenerator.Snapshot(fixtureId, 0),
            bot.Id, version.Id, ArcRelayPlaylistDefinition.StockArtifactHash,
            compiled.Classes, composition.CanonicalJson, composition.ContentHash,
            fixtureId, 1, compiled.ContentHash, compiled.CanonicalJson, compiled.LinkedData);
    }

    private async Task<(Bot, BotVersion)> StockArtifactAsync(CancellationToken cancellationToken)
    {
        Bot bot = await db.Bots.SingleAsync(value => value.Slug == ArcRelayPlaylistSeeder.StockBotSlug, cancellationToken);
        BotVersion version = await db.BotVersions.SingleAsync(value => value.BotId == bot.Id && value.IsActive && value.ArtifactHash == ArcRelayPlaylistDefinition.StockArtifactHash, cancellationToken);
        return (bot, version);
    }

    private sealed record ParticipantMaterial(
        Guid? EntrantId,
        string Kind,
        int Revision,
        string Name,
        string OwnerName,
        string Crest,
        Guid BotId,
        Guid BotVersionId,
        string ArtifactHash,
        IReadOnlyList<string> Classes,
        string CompositionJson,
        string CompositionHash,
        Guid? SheetId,
        int? SheetRevision,
        string? SheetHash,
        string? SheetJson,
        byte[]? MindData);
}
