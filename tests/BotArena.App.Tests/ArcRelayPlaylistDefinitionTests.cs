using BotArena.App.ArcRelay;
using BotArena.App.Competition;
using BotArena.Engine;
using BotArena.Runtime;
using BotArena.Runtime.Wasm;
using BotArena.Toolchain;
using System.Text.Json;

namespace BotArena.App.Tests;

public sealed class ArcRelayPlaylistDefinitionTests
{
    [Fact]
    public void Entrant_v6_hosts_tactical_sheets_while_v2_through_v5_remain_executable()
    {
        ArcRelayEntrantPlaylistDefinition current =
            ArcRelayEntrantPlaylistDefinition.Create();
        ArcRelayEntrantPlaylistDefinition historicalV2 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV2();
        ArcRelayEntrantPlaylistDefinition counterflow =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV3();
        ArcRelayEntrantPlaylistDefinition forward =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV4();
        ArcRelayEntrantPlaylistDefinition recoveredStock =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV5();

        Assert.Equal(6, current.PlaylistVersion);
        Assert.Equal(ArcRelayLoopProfile.Current.MapId, current.Match.Map.Id);
        Assert.Equal(
            ArcRelayLoopProfile.ForwardCombat.RulesetId,
            current.Match.Rules.RulesetId);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.TacticalArtifactHash,
            current.CanonicalDefinition);
        Assert.Contains("arc-relay-tactical-playbook-v1", current.CanonicalDefinition);
        Assert.Equal(5, recoveredStock.PlaylistVersion);
        Assert.Contains(
            ArcRelayPlaylistDefinition.ForwardStockArtifactHash,
            recoveredStock.CanonicalDefinition);
        Assert.Equal(4, forward.PlaylistVersion);
        Assert.Equal(ArcRelayLoopProfile.Current.MapId, forward.Match.Map.Id);
        Assert.Contains(
            ArcRelayPlaylistDefinition.HistoricalForwardStockArtifactHash,
            forward.CanonicalDefinition);
        Assert.Equal(2, historicalV2.PlaylistVersion);
        Assert.Equal(
            ArcRelayLoopProfile.HomeGatesWide.MapId,
            historicalV2.Match.Map.Id);
        Assert.Equal(3, counterflow.PlaylistVersion);
        Assert.Equal(
            ArcRelayLoopProfile.DepthCounterflow.RulesetId,
            counterflow.Match.Rules.RulesetId);
        Assert.Contains(
            ArcRelayPlaylistDefinition.StockArtifactHash,
            counterflow.CanonicalDefinition);
        var registry = new HostedGenericMatchDefinitionRegistry(
            [ArcRelayPlaylistDefinition.Create(), historicalV2, counterflow,
                forward, recoveredStock, current]);
        Assert.Same(historicalV2, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.HistoricalVersion));
        Assert.Same(counterflow, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.CounterflowVersion));
        Assert.Same(forward, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.ForwardVersion));
        Assert.Same(recoveredStock, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.PreviousVersion));
        Assert.Same(current, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.Version));
    }

    [Fact]
    public void Hosted_product_uses_dynamic_sheets_and_trusted_stock_runtime()
    {
        ArcRelayPlaylistDefinition definition =
            ArcRelayPlaylistDefinition.Create();
        string[] first = ArcRelayLegacySnapshotCodec.NewSheetTemplate().Slots
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
        Assert.Contains(ArcRelayLegacySnapshotCodec.SchemaId, definition.CanonicalDefinition);
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
        var codec = new ArcRelayLegacySnapshotCodec(catalog);
        ArcRelaySheetCompilation first = codec.Compile(
            ArcRelayLegacySnapshotCodec.NewSheetTemplate(),
            catalog.StarterIds,
            "parity-a:r1");
        ArcRelaySheetCompilation second = codec.Compile(
            ArcRelayLegacySnapshotCodec.NewSheetTemplate(),
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

    [Fact]
    public void Forward_stock_wasm_and_trusted_runtime_are_independently_deterministic()
    {
        ArcRelayEntrantPlaylistDefinition hosted =
            ArcRelayEntrantPlaylistDefinition.Create();
        ArcRelayClassCatalog catalog = ArcRelayClassCatalog.Default;
        var codec = new ArcRelayLegacySnapshotCodec(catalog);
        ArcRelaySheetCompilation first = codec.Compile(
            ArcRelayLegacySnapshotCodec.NewSheetTemplate(),
            catalog.StarterIds,
            "forward-parity-a:r1");
        ArcRelaySheetCompilation second = codec.Compile(
            ArcRelayLegacySnapshotCodec.NewSheetTemplate(),
            catalog.StarterIds,
            "forward-parity-b:r1");
        ActorResolvedMatchDefinition definition = hosted.ResolveMatch(
        [
            new HostedGenericParticipantInput(0, 0, first.Classes),
            new HostedGenericParticipantInput(1, 1, second.Classes),
        ]);
        string artifact = RepoPaths.FindUpward(Path.Combine(
            "arena-bots",
            "arc-relay",
            "stock-mind-v4",
            "bot.wasm")) ?? throw new InvalidOperationException(
                "Forward-combat stock artifact is missing.");

        using var wasmFirst = new WasmGenericMindRuntimeFactory(
            new WasmMindRuntimeOptions { ModulePath = artifact });
        using var wasmSecond = new WasmGenericMindRuntimeFactory(
            new WasmMindRuntimeOptions { ModulePath = artifact });
        string wasmHash = RunForwardParity(
            definition,
            hosted.ReplayPresentation,
            first,
            second,
            wasmFirst,
            wasmSecond);
        using var wasmRepeatFirst = new WasmGenericMindRuntimeFactory(
            new WasmMindRuntimeOptions { ModulePath = artifact });
        using var wasmRepeatSecond = new WasmGenericMindRuntimeFactory(
            new WasmMindRuntimeOptions { ModulePath = artifact });
        string wasmRepeatHash = RunForwardParity(
            definition,
            hosted.ReplayPresentation,
            first,
            second,
            wasmRepeatFirst,
            wasmRepeatSecond);
        string trustedHash = RunForwardParity(
            definition,
            hosted.ReplayPresentation,
            first,
            second,
            new InProcessGenericMindRuntimeFactory(
                static () => new global::ArcRelayStrategyMind(),
                trustedArcRelayStockProjection: true),
            new InProcessGenericMindRuntimeFactory(
                static () => new global::ArcRelayStrategyMind(),
                trustedArcRelayStockProjection: true));
        string trustedRepeatHash = RunForwardParity(
            definition,
            hosted.ReplayPresentation,
            first,
            second,
            new InProcessGenericMindRuntimeFactory(
                static () => new global::ArcRelayStrategyMind(),
                trustedArcRelayStockProjection: true),
            new InProcessGenericMindRuntimeFactory(
                static () => new global::ArcRelayStrategyMind(),
                trustedArcRelayStockProjection: true));

        Assert.Equal(wasmHash, wasmRepeatHash);
        Assert.Equal(trustedHash, trustedRepeatHash);
    }

    [Fact]
    public void Frozen_forward_stock_compatibility_artifact_matches_v5_identity()
    {
        string artifact = RepoPaths.FindUpward(Path.Combine(
            "arena-bots",
            "arc-relay",
            "stock-mind-v4-frozen-999183",
            "bot.wasm")) ?? throw new InvalidOperationException(
                "Frozen forward-combat compatibility artifact is missing.");

        Assert.Equal(
            ArcRelayPlaylistDefinition.ForwardStockArtifactHash,
            BotBuilder.Sha256File(artifact));
    }

    private static string RunForwardParity(
        ActorResolvedMatchDefinition definition,
        GenericActorReplayPresentation presentation,
        ArcRelaySheetCompilation first,
        ArcRelaySheetCompilation second,
        IGenericMindRuntimeFactory firstFactory,
        IGenericMindRuntimeFactory secondFactory)
    {
        using var session = new GenericActorMatchSession(
            definition,
            [
                ForwardParityConfiguration(0, first, firstFactory),
                ForwardParityConfiguration(1, second, secondFactory),
            ],
            2026080301,
            recordChronology: true);
        session.Run();
        return GenericActorReplayDocument.Create(session, presentation).ReplayHash;
    }

    private static GenericActorParticipantConfiguration ForwardParityConfiguration(
        int participantId,
        ArcRelaySheetCompilation sheet,
        IGenericMindRuntimeFactory factory) => new()
    {
        ParticipantId = participantId,
        TeamId = participantId,
        Name = $"forward-parity-{participantId}",
        MindRuntimeFactory = factory,
        RuntimeKind = "forward-stock-parity-v1",
        ArtifactHash = ArcRelayPlaylistDefinition.ForwardStockArtifactHash,
        MindDataHash = sheet.ContentHash,
        MindEvaluationData = [.. sheet.LinkedData],
        Accent = participantId == 0 ? "#22d3ee" : "#fb5360",
        LookId = "arc-relay-sheet",
        ProjectileLookId = ArcRelayH0ReplayPresentation.ProjectileLookId,
    };

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
