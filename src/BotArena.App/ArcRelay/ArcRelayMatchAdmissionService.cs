using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Jobs;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.App.Sheets;
using BotArena.Engine;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.ArcRelay;

/// <summary>Creates current-version matches from immutable entrant revisions.</summary>
public sealed class ArcRelayMatchAdmissionService(
    AppDbContext db,
    TacticalSheetCompiler sheetCompiler,
    TacticalSheetTemplateCatalog templates,
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
        return await CreateCoreAsync(
            materials, lane, initiatedBy, seed, cancellationToken);
    }

    public async Task<Match> CreateTrialAsync(
        ArcRelayEntrant sheet,
        string stockSheetId,
        Guid initiatedBy,
        long? seed,
        CancellationToken cancellationToken)
    {
        ParticipantMaterial candidate = await MaterializeAsync(
            sheet, cancellationToken);
        ParticipantMaterial stock = await StockFixtureAsync(
            templates.GetStock(stockSheetId), cancellationToken);
        return await CreateCoreAsync(
            [candidate, stock],
            ArcRelayMatchLane.Scrimmage,
            initiatedBy,
            seed,
            cancellationToken);
    }

    public async Task<Match> CreatePreflightAsync(
        ArcRelayEntrant customMind,
        Guid initiatedBy,
        long? seed,
        CancellationToken cancellationToken)
    {
        ParticipantMaterial candidate = await MaterializeAsync(
            customMind, cancellationToken);
        ParticipantMaterial stock = await StockFixtureAsync(
            templates.Stock[0], cancellationToken);
        return await CreateCoreAsync(
            [candidate, stock],
            ArcRelayMatchLane.Preflight,
            initiatedBy,
            seed,
            cancellationToken);
    }

    private async Task<Match> CreateCoreAsync(
        ParticipantMaterial[] materials,
        string lane,
        Guid? initiatedBy,
        long? seed,
        CancellationToken cancellationToken)
    {
        ArcRelayEntrantPlaylistDefinition definition =
            ArcRelayEntrantPlaylistDefinition.Create();
        PlaylistVersion playlistVersion = await db.PlaylistVersions.SingleAsync(
            version => version.Version
                    == ArcRelayEntrantPlaylistDefinition.Version
                && db.Playlists.Any(playlist => playlist.Id == version.PlaylistId
                    && playlist.Key
                        == ArcRelayEntrantPlaylistDefinition.PlaylistKey),
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
            RuntimeConfigurationVersion =
                resolved.CapabilityVersions.RuntimeConfigurationVersion,
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
                ProjectileLookIdSnapshot =
                    ArcRelayH0ReplayPresentation.ProjectileLookId,
                ArtifactHashSnapshot = value.ArtifactHash,
                SheetIdSnapshot = value.SheetId,
                SheetRevisionSnapshot = value.SheetRevision,
                SheetNameSnapshot = value.SheetId is null ? null : value.Name,
                SheetHashSnapshot = value.SheetHash,
                SheetCanonicalJsonSnapshot = value.PlaybookJson,
                SheetLayoutJsonSnapshot = value.LayoutJson,
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
        string owner = await db.Users.Where(value =>
                value.Id == entrant.OwnerUserId)
            .Select(value => value.DisplayName)
            .SingleAsync(cancellationToken);
        if (entrant.Kind == ArcRelayEntrantKind.Sheet)
        {
            TacticalSheet sheet = await db.TacticalSheets.SingleAsync(
                value => value.Id == entrant.Id, cancellationToken);
            ValidatedTacticalSheet compiled = sheetCompiler.Compile(
                sheet.PlaybookJson,
                sheet.LayoutJson,
                EveryClass(),
                $"{sheet.Id}:r{sheet.Revision}");
            if (!string.Equals(
                    compiled.ContentHash,
                    sheet.ContentHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Saved tactical sheet content hash failed verification.");
            }
            (Bot bot, BotVersion version) = await TacticalArtifactAsync(
                cancellationToken);
            return new ParticipantMaterial(
                entrant.Id,
                "sheet",
                sheet.Revision,
                entrant.Name,
                owner,
                ArcRelayCrestGenerator.Snapshot(
                    entrant.Id, entrant.CrestVariant),
                bot.Id,
                version.Id,
                ArcRelayEntrantPlaylistDefinition.TacticalArtifactHash,
                compiled.Compilation.Composition,
                compiled.Composition.CanonicalJson,
                compiled.Composition.ContentHash,
                sheet.Id,
                sheet.Revision,
                sheet.ContentHash,
                sheet.PlaybookJson,
                sheet.LayoutJson,
                compiled.Compilation.LinkedData);
        }

        BotVersion active = await db.BotVersions.SingleAsync(
            value => value.BotId == entrant.MindBotId
                && value.IsActive
                && value.Status == BuildStatus.Built,
            cancellationToken);
        if (!BotContractProfiles.Supports(
                active.SupportedContractProfiles,
                BotArenaVersions.GenericMindContractProfileId))
        {
            throw new InvalidOperationException(
                "Custom mind does not support the generic-mind contract.");
        }
        ArcRelayCompositionDeclaration declaration =
            ArcRelayComposition.Read(entrant.CompositionJson!);
        return new ParticipantMaterial(
            entrant.Id,
            "mind",
            active.VersionNumber,
            entrant.Name,
            owner,
            ArcRelayCrestGenerator.Snapshot(
                entrant.Id, entrant.CrestVariant),
            entrant.MindBotId!.Value,
            active.Id,
            active.ArtifactHash!,
            declaration.ClassIds,
            entrant.CompositionJson!,
            entrant.CompositionHash!,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    private async Task<ParticipantMaterial> StockFixtureAsync(
        TacticalSheetSource source,
        CancellationToken cancellationToken)
    {
        Guid fixtureId = source.Id switch
        {
            "home-siege-v3" => new Guid(
                "d7ed7a58-a59e-42d4-98f8-b6216711f188"),
            "breakwater-v1" => new Guid(
                "0e8db149-77d7-47fc-8997-0b7eca82ab46"),
            _ => new Guid("f75aca5f-10f9-40d8-b244-d333d411de83"),
        };
        (Bot bot, BotVersion version) = await TacticalArtifactAsync(
            cancellationToken);
        ArcRelayCompositionCompilation composition =
            ArcRelayComposition.Compile(
                new ArcRelayCompositionDeclaration(source.Composition),
                classCatalog,
                EveryClass());
        return new ParticipantMaterial(
            null,
            "stock-fixture",
            1,
            source.Name,
            "Nilbots",
            ArcRelayCrestGenerator.Snapshot(fixtureId, 0),
            bot.Id,
            version.Id,
            ArcRelayEntrantPlaylistDefinition.TacticalArtifactHash,
            source.Composition,
            composition.CanonicalJson,
            composition.ContentHash,
            fixtureId,
            1,
            source.ContentHash,
            source.PlaybookJson,
            source.LayoutJson,
            source.LinkedData);
    }

    private async Task<(Bot, BotVersion)> TacticalArtifactAsync(
        CancellationToken cancellationToken)
    {
        Bot bot = await db.Bots.SingleAsync(value =>
            value.Slug == ArcRelayPlaylistSeeder.TacticalPlaybookBotSlug,
            cancellationToken);
        BotVersion version = await db.BotVersions.SingleAsync(value =>
            value.BotId == bot.Id
            && value.IsActive
            && value.ArtifactHash
                == ArcRelayEntrantPlaylistDefinition.TacticalArtifactHash,
            cancellationToken);
        return (bot, version);
    }

    private IReadOnlySet<string> EveryClass() => classCatalog.All
        .Select(value => value.Id)
        .ToHashSet(StringComparer.Ordinal);

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
        string? PlaybookJson,
        string? LayoutJson,
        byte[]? MindData);
}
