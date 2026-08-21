using System.Security.Cryptography;
using System.Text;
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
            sheet, cancellationToken, currentTacticsForPractice: true);
        ParticipantMaterial stock = await StockFixtureAsync(
            templates.GetStock(stockSheetId), cancellationToken);
        return await CreateCoreAsync(
            [candidate, stock],
            ArcRelayMatchLane.Practice,
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
        CancellationToken cancellationToken,
        bool currentTacticsForPractice = false)
    {
        string owner = await db.Users.Where(value =>
                value.Id == entrant.OwnerUserId)
            .Select(value => value.DisplayName)
            .SingleAsync(cancellationToken);
        if (entrant.Kind == ArcRelayEntrantKind.Sheet)
        {
            TacticalSheet sheet = await db.TacticalSheets.SingleAsync(
                value => value.Id == entrant.Id, cancellationToken);
            // Ranked/scrimmage admission snapshots the immutable materialized
            // revision. Practice is an authoring loop: compile its player-owned
            // Tactics source with the server that is about to execute it. A
            // restarted compiler must not silently run yesterday's generated
            // Orders merely because the player made no edit worth another
            // saved revision.
            ValidatedTacticalSheet compiled = currentTacticsForPractice
                && sheet.TacticsJson is string tactics
                    ? sheetCompiler.CompileTactics(
                        tactics,
                        EveryClass(),
                        $"{sheet.Id}:practice-r{sheet.Revision}")
                    : sheetCompiler.Compile(
                        sheet.PlaybookJson,
                        sheet.LayoutJson,
                        EveryClass(),
                        $"{sheet.Id}:r{sheet.Revision}");
            if (!currentTacticsForPractice
                && !string.Equals(
                    compiled.ContentHash,
                    sheet.ContentHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Saved tactical sheet content hash failed verification.");
            }
            string playbookJson = compiled.Compilation.MaterializedOrders is { }
                ? Encoding.UTF8.GetString(
                    compiled.Compilation.MaterializedOrders)
                : sheet.PlaybookJson;
            string layoutJson = compiled.Compilation.MaterializedMapPlan is { }
                ? Encoding.UTF8.GetString(
                    compiled.Compilation.MaterializedMapPlan)
                : sheet.LayoutJson;
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
                ArcRelayEntrantPlaylistDefinition.Tapline13TacticalArtifactHash,
                compiled.Compilation.Composition,
                compiled.Composition.CanonicalJson,
                compiled.Composition.ContentHash,
                sheet.Id,
                sheet.Revision,
                compiled.ContentHash,
                playbookJson,
                layoutJson,
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
            ArcRelayEntrantPlaylistDefinition.OrdersStockSheetId => new Guid(
                "5aee9e75-6595-4fb9-a6df-c76abcf1a663"),
            "home-siege-v3" => new Guid(
                "d7ed7a58-a59e-42d4-98f8-b6216711f188"),
            "breakwater-v1" => new Guid(
                "0e8db149-77d7-47fc-8997-0b7eca82ab46"),
            _ => StockFixtureId(source.Id),
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
            ArcRelayEntrantPlaylistDefinition.Tapline13TacticalArtifactHash,
            source.Composition,
            composition.CanonicalJson,
            composition.ContentHash,
            fixtureId,
            1,
            source.ContentHash,
            source.OrdersJson,
            source.MapPlanJson,
            source.LinkedData);
    }

    private async Task<(Bot, BotVersion)> TacticalArtifactAsync(
        CancellationToken cancellationToken)
    {
        Bot bot = await db.Bots.SingleAsync(value =>
            value.Slug == ArcRelayPlaylistSeeder.TaplineTacticalPlaybookBotSlug,
            cancellationToken);
        BotVersion version = await db.BotVersions.SingleAsync(value =>
            value.BotId == bot.Id
            && value.IsActive
            && value.ArtifactHash
                == ArcRelayEntrantPlaylistDefinition.Tapline13TacticalArtifactHash,
            cancellationToken);
        return (bot, version);
    }

    private static Guid StockFixtureId(string sheetId)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"arc-relay-stock-fixture:{sheetId}"));
        return new Guid(hash.AsSpan(0, 16));
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
