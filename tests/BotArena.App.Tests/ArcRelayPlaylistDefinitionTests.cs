using BotArena.App.ArcRelay;
using BotArena.App.Competition;
using BotArena.Engine;
using BotArena.Runtime;
using System.Text.Json;

namespace BotArena.App.Tests;

public sealed class ArcRelayPlaylistDefinitionTests
{
    [Fact]
    public void Entrant_v3_advances_to_counterflow_while_v2_remains_executable()
    {
        ArcRelayEntrantPlaylistDefinition current =
            ArcRelayEntrantPlaylistDefinition.Create();
        ArcRelayEntrantPlaylistDefinition historical =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV2();

        Assert.Equal(3, current.PlaylistVersion);
        Assert.Equal(ArcRelayLoopProfile.Current.MapId, current.Match.Map.Id);
        Assert.Equal(2, historical.PlaylistVersion);
        Assert.Equal(ArcRelayLoopProfile.HomeGatesWide.MapId, historical.Match.Map.Id);
        var registry = new HostedGenericMatchDefinitionRegistry(
            [ArcRelayPlaylistDefinition.Create(), historical, current]);
        Assert.Same(historical, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.HistoricalVersion));
        Assert.Same(current, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.Version));
    }

    [Fact]
    public void Hosted_product_uses_dynamic_sheets_and_trusted_stock_runtime()
    {
        ArcRelayPlaylistDefinition definition =
            ArcRelayPlaylistDefinition.Create();
        string[] first = ArcRelayPlayerSheetCodec.NewSheetTemplate().Slots
            .OrderBy(value => value.UnitId)
            .Select(value => value.ClassId)
            .ToArray();
        string[] second = [.. first.Reverse()];

        ActorResolvedMatchDefinition resolved = definition.ResolveMatch(
        [
            new HostedGenericParticipantInput(0, 0, first),
            new HostedGenericParticipantInput(1, 1, second),
        ]);

        Assert.Equal(HostedGenericRuntimeModel.TrustedStockMind, definition.RuntimeModel);
        Assert.Equal(1.25, definition.PresentationTicksPerSecond);
        Assert.Equal(ArcRelayLoopProfile.HomeGatesWide.MapId, resolved.Map.Id);
        Assert.Equal(BotArenaVersions.GenericMindContractProfileId, definition.AdmissionPolicyId);
        Assert.Contains(ArcRelayPlaylistDefinition.StockArtifactHash, definition.CanonicalDefinition);
        Assert.Contains(ArcRelayPlayerSheetCodec.SchemaId, definition.CanonicalDefinition);
        Assert.Equal(
            second,
            resolved.Topology.UnitSlots
                .Where(value => value.TeamId == 1)
                .OrderBy(value => value.UnitId)
                .Select(value => value.ClassId));
    }

    [Fact]
    public void Trusted_projection_and_compact_recording_preserve_full_replay_behavior()
    {
        ArcRelayPlaylistDefinition hosted = ArcRelayPlaylistDefinition.Create();
        ArcRelayClassCatalog catalog = ArcRelayClassCatalog.Default;
        var codec = new ArcRelayPlayerSheetCodec(catalog);
        ArcRelaySheetCompilation first = codec.Compile(
            ArcRelayPlayerSheetCodec.NewSheetTemplate(),
            catalog.StarterIds,
            "parity-a:r1");
        ArcRelaySheetCompilation second = codec.Compile(
            ArcRelayPlayerSheetCodec.NewSheetTemplate(),
            catalog.StarterIds,
            "parity-b:r1");
        ActorResolvedMatchDefinition definition = hosted.ResolveMatch(
        [
            new HostedGenericParticipantInput(0, 0, first.Classes),
            new HostedGenericParticipantInput(1, 1, second.Classes),
        ]);

        (string normalHash, GenericActorMatchResult normalResult) =
            RunFull(definition, hosted.ReplayPresentation, first, second, optimized: false);
        (string optimizedHash, GenericActorMatchResult optimizedResult) =
            RunFull(definition, hosted.ReplayPresentation, first, second, optimized: true);
        Assert.Equal(normalHash, optimizedHash);
        Assert.Equivalent(normalResult, optimizedResult, strict: true);

        ArcRelayBroadcastDocument compact = RunCompact(
            definition,
            hosted.ReplayPresentation,
            first,
            second);
        Assert.Equivalent(normalResult, compact.Result, strict: true);

        using JsonDocument broadcast = JsonDocument.Parse(compact.CanonicalUtf8);
        JsonElement root = broadcast.RootElement;
        JsonElement worlds = root.GetProperty("worlds");
        JsonElement vision = root.GetProperty("vision");
        Assert.Equal(worlds.GetArrayLength(), vision.GetArrayLength());
        Assert.All(vision[0].EnumerateArray(), team =>
            Assert.NotEmpty(team[1].EnumerateArray()));

        using JsonDocument prefix = JsonDocument.Parse(
            ArcRelayBroadcastDocument.CreatePartialPrefix(
                System.Text.Encoding.UTF8.GetString(compact.CanonicalUtf8.Span),
                3));
        Assert.Equal(
            Math.Min(3, worlds.GetArrayLength()),
            prefix.RootElement.GetProperty("vision").GetArrayLength());
    }

    private static (string Hash, GenericActorMatchResult Result) RunFull(
        ActorResolvedMatchDefinition definition,
        GenericActorReplayPresentation presentation,
        ArcRelaySheetCompilation first,
        ArcRelaySheetCompilation second,
        bool optimized)
    {
        using var session = new GenericActorMatchSession(
            definition,
            Configurations(first, second, optimized),
            104729,
            recordChronology: true);
        GenericActorMatchResult result = session.Run();
        GenericActorReplayDocument replay = GenericActorReplayDocument.Create(
            session,
            presentation);
        return (replay.ReplayHash, result);
    }

    private static ArcRelayBroadcastDocument RunCompact(
        ActorResolvedMatchDefinition definition,
        GenericActorReplayPresentation presentation,
        ArcRelaySheetCompilation first,
        ArcRelaySheetCompilation second)
    {
        using var session = new GenericActorMatchSession(
            definition,
            Configurations(first, second, optimized: true),
            104729,
            recordChronology: false);
        return ArcRelayBroadcastDocument.CreateAndRun(session, presentation);
    }

    private static GenericActorParticipantConfiguration[] Configurations(
        ArcRelaySheetCompilation first,
        ArcRelaySheetCompilation second,
        bool optimized) =>
    [
        Configuration(0, first, optimized),
        Configuration(1, second, optimized),
    ];

    private static GenericActorParticipantConfiguration Configuration(
        int participantId,
        ArcRelaySheetCompilation sheet,
        bool optimized) => new()
    {
        ParticipantId = participantId,
        TeamId = participantId,
        Name = $"sheet-{participantId}",
        MindRuntimeFactory = new InProcessGenericMindRuntimeFactory(
            static () => new global::ArcRelayStockMind(),
            trustedArcRelayStockProjection: optimized),
        RuntimeKind = "trusted-stock-in-process-v1",
        ArtifactHash = ArcRelayPlaylistDefinition.StockArtifactHash,
        MindDataHash = sheet.ContentHash,
        MindEvaluationData = [.. sheet.LinkedData],
        Accent = participantId == 0 ? "#22d3ee" : "#fb5360",
        LookId = "arc-relay-sheet",
        ProjectileLookId = ArcRelayH0ReplayPresentation.ProjectileLookId,
    };
}
