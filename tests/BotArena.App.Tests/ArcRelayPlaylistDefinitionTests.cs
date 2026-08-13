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
    public void Entrant_v21_promotes_Tapline3_and_EPIBT_while_v2_through_v20_remain_executable()
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
        ArcRelayEntrantPlaylistDefinition tacticalCounterflow =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV6();
        ArcRelayEntrantPlaylistDefinition ambushV7 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV7();
        ArcRelayEntrantPlaylistDefinition wasmRepairV8 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV8();
        ArcRelayEntrantPlaylistDefinition directV9 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV9();
        ArcRelayEntrantPlaylistDefinition semanticV10 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV10();
        ArcRelayEntrantPlaylistDefinition movementV11 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV11();
        ArcRelayEntrantPlaylistDefinition relativeWellV12 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV12();
        ArcRelayEntrantPlaylistDefinition stickyRouteV13 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV13();
        ArcRelayEntrantPlaylistDefinition carrierClearanceV14 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV14();
        ArcRelayEntrantPlaylistDefinition trafficDetourV15 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV15();
        ArcRelayEntrantPlaylistDefinition stableCarrierV16 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV16();
        ArcRelayEntrantPlaylistDefinition looseCoreMemoryV17 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV17();
        ArcRelayEntrantPlaylistDefinition claimableCoreV18 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV18();
        ArcRelayEntrantPlaylistDefinition taplineV19 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV19();
        ArcRelayEntrantPlaylistDefinition ordersV20 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV20();

        Assert.Equal(21, current.PlaylistVersion);
        Assert.Equal(ArcRelayLoopProfile.Tapline3.MapId, current.Match.Map.Id);
        Assert.Equal(
            ArcRelayLoopProfile.Tapline3.RulesetId,
            current.Match.Rules.RulesetId);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.OrdersTacticalArtifactHash,
            current.CanonicalDefinition);
        Assert.Contains("arc-relay-orders-v2", current.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.OrdersStockSheetId,
            current.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.OrdersTacticalBuildInputHash,
            current.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.OrdersStockPlaybookSourceHash,
            current.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.OrdersStockMapPlanSourceHash,
            current.CanonicalDefinition);
        Assert.Contains("ownerApprovedTapline3", current.Provenance);
        Assert.Contains("taplineNativeMapPlansOnly", current.Provenance);
        Assert.Contains("ordersV2DirectExecutor", current.Provenance);
        Assert.Contains("nestedPriorityOrders", current.Provenance);
        Assert.Contains("targetScopedCapacity", current.Provenance);
        Assert.Contains("explicitCoreActions", current.Provenance);
        Assert.Contains("authoredMovementUrgency", current.Provenance);
        Assert.Contains("atomicCoordinatedMovement", current.Provenance);
        Assert.Contains("vacatedTileFollowing", current.Provenance);
        Assert.Contains("inProcessWasmGameplayParity", current.Provenance);
        Assert.Contains("\"linkedDataFormat\":\"ATL2\"", current.Provenance);
        Assert.Contains("freshRatingEra", current.Provenance);
        Assert.DoesNotContain("ratingContinuityFromVersions", current.Provenance);
        Assert.Equal(20, ordersV20.PlaylistVersion);
        Assert.Equal(ArcRelayLoopProfile.Tapline1.MapId, ordersV20.Match.Map.Id);
        Assert.Equal(
            "8a3b903fd9e6b83148b65369e8dfae2e0cfb4227dd4d688bbd17759f85486ca1",
            ordersV20.DefinitionFingerprint);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.OrdersV2TacticalArtifactHash,
            ordersV20.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.OrdersV2StockPlaybookSourceHash,
            ordersV20.CanonicalDefinition);
        Assert.Contains("ownerApprovedTapline1", ordersV20.Provenance);
        Assert.DoesNotContain("atomicCoordinatedMovement", ordersV20.Provenance);
        Assert.Equal(19, taplineV19.PlaylistVersion);
        Assert.Equal(
            "b16b7d62bd0868371edd39b49fc3b6897ef49c44a033e5b91c46bd4a511463b1",
            taplineV19.DefinitionFingerprint);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.TacticalArtifactHash,
            taplineV19.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.TaplineStockPlaybookSourceHash,
            taplineV19.CanonicalDefinition);
        Assert.Contains("arc-relay-tactical-playbook-v1", taplineV19.CanonicalDefinition);
        Assert.Contains("taplineNativeLayoutsOnly", taplineV19.Provenance);
        Assert.Contains("\"linkedDataFormat\":\"ATL1\"", taplineV19.Provenance);
        Assert.Equal(18, claimableCoreV18.PlaylistVersion);
        Assert.Equal(
            ArcRelayLoopProfile.AmbushWarren11.MapId,
            claimableCoreV18.Match.Map.Id);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.StockPlaybookSourceHash,
            claimableCoreV18.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.StockLayoutSourceHash,
            claimableCoreV18.CanonicalDefinition);
        Assert.Equal(
            "704ac64263b602a426b1b37302581c5197de16b5370d5955c21726ac40429506",
            claimableCoreV18.DefinitionFingerprint);
        Assert.Contains("uncommittedDropsRemainClaimable", claimableCoreV18.Provenance);
        Assert.DoesNotContain("ownerApprovedTapline1", claimableCoreV18.Provenance);
        Assert.Equal(17, looseCoreMemoryV17.PlaylistVersion);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition
                .LooseCoreMemoryTacticalArtifactHash,
            looseCoreMemoryV17.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition
                .LooseCoreMemoryTacticalBuildInputHash,
            looseCoreMemoryV17.CanonicalDefinition);
        Assert.Contains("boundedLooseCoreMemory", looseCoreMemoryV17.Provenance);
        Assert.DoesNotContain(
            "uncommittedDropsRemainClaimable", looseCoreMemoryV17.Provenance);
        Assert.Equal(16, stableCarrierV16.PlaylistVersion);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition
                .StableCarrierTacticalArtifactHash,
            stableCarrierV16.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition
                .StableCarrierTacticalBuildInputHash,
            stableCarrierV16.CanonicalDefinition);
        Assert.Contains("stableCarrierDeliveryCourses", stableCarrierV16.Provenance);
        Assert.DoesNotContain("boundedLooseCoreMemory", stableCarrierV16.Provenance);
        Assert.Equal(15, trafficDetourV15.PlaylistVersion);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition
                .TrafficDetourTacticalArtifactHash,
            trafficDetourV15.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition
                .TrafficDetourTacticalBuildInputHash,
            trafficDetourV15.CanonicalDefinition);
        Assert.Contains("boundedTrafficDetours", trafficDetourV15.Provenance);
        Assert.DoesNotContain(
            "stableCarrierDeliveryCourses", trafficDetourV15.Provenance);
        Assert.Equal(14, carrierClearanceV14.PlaylistVersion);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition
                .CarrierClearanceTacticalArtifactHash,
            carrierClearanceV14.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition
                .CarrierClearanceTacticalBuildInputHash,
            carrierClearanceV14.CanonicalDefinition);
        Assert.Contains(
            "cascadingCarrierClearance", carrierClearanceV14.Provenance);
        Assert.DoesNotContain(
            "boundedTrafficDetours", carrierClearanceV14.Provenance);
        Assert.Equal(13, stickyRouteV13.PlaylistVersion);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition
                .StickyRouteTacticalArtifactHash,
            stickyRouteV13.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition
                .StickyRouteTacticalBuildInputHash,
            stickyRouteV13.CanonicalDefinition);
        Assert.Contains("stickyAuthoredRouteCourses", stickyRouteV13.Provenance);
        Assert.DoesNotContain(
            "cascadingCarrierClearance", stickyRouteV13.Provenance);
        Assert.Equal(12, relativeWellV12.PlaylistVersion);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition
                .RelativeWellTacticalArtifactHash,
            relativeWellV12.CanonicalDefinition);
        Assert.Contains("relativeWellSubjects", relativeWellV12.Provenance);
        Assert.DoesNotContain(
            "stickyAuthoredRouteCourses", relativeWellV12.Provenance);
        using (JsonDocument provenance = JsonDocument.Parse(
            relativeWellV12.Provenance))
        {
            Assert.Equal(
                [8, 9, 10, 11],
                provenance.RootElement
                    .GetProperty("ratingContinuityFromVersions")
                    .EnumerateArray().Select(value => value.GetInt32()));
        }
        Assert.Equal(11, movementV11.PlaylistVersion);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition
                .BoundedMovementTacticalArtifactHash,
            movementV11.CanonicalDefinition);
        Assert.Contains("boundedOscillationYield", movementV11.Provenance);
        Assert.DoesNotContain("relativeWellSubjects", movementV11.Provenance);
        using (JsonDocument provenance = JsonDocument.Parse(
            movementV11.Provenance))
        {
            Assert.Equal(
                [8, 9, 10],
                provenance.RootElement
                    .GetProperty("ratingContinuityFromVersions")
                    .EnumerateArray().Select(value => value.GetInt32()));
        }
        Assert.Equal(10, semanticV10.PlaylistVersion);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition
                .SemanticIntentTacticalArtifactHash,
            semanticV10.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition
                .SemanticIntentTacticalBuildInputHash,
            semanticV10.CanonicalDefinition);
        Assert.Contains("semanticIntentLabels", semanticV10.Provenance);
        Assert.DoesNotContain("boundedRecoveryEpisodes", semanticV10.Provenance);
        Assert.Equal(9, directV9.PlaylistVersion);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition
                .DirectLadderTacticalArtifactHash,
            directV9.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition
                .DirectLadderTacticalBuildInputHash,
            directV9.CanonicalDefinition);
        Assert.DoesNotContain("semanticIntentLabels", directV9.Provenance);
        Assert.Equal(
            "a2024fd4234b5feb80419a97ada57245d1a8f33b319d34bd04d9aeab9eb24799",
            directV9.DefinitionFingerprint);
        Assert.Equal(8, wasmRepairV8.PlaylistVersion);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.WasmRepairTacticalArtifactHash,
            wasmRepairV8.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.WasmRepairTacticalBuildInputHash,
            wasmRepairV8.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.HistoricalStockPlaybookSourceHash,
            wasmRepairV8.CanonicalDefinition);
        Assert.Equal(7, ambushV7.PlaylistVersion);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.HistoricalTacticalArtifactHash,
            ambushV7.CanonicalDefinition);
        Assert.DoesNotContain(
            ArcRelayEntrantPlaylistDefinition.TacticalBuildInputHash,
            ambushV7.CanonicalDefinition);
        Assert.Equal(6, tacticalCounterflow.PlaylistVersion);
        Assert.Equal(
            ArcRelayLoopProfile.ForwardCombat.RulesetId,
            tacticalCounterflow.Match.Rules.RulesetId);
        Assert.Equal(5, recoveredStock.PlaylistVersion);
        Assert.Contains(
            ArcRelayPlaylistDefinition.ForwardStockArtifactHash,
            recoveredStock.CanonicalDefinition);
        Assert.Equal(4, forward.PlaylistVersion);
        Assert.Equal(ArcRelayLoopProfile.ForwardCombat.MapId, forward.Match.Map.Id);
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
                forward, recoveredStock, tacticalCounterflow, ambushV7,
                wasmRepairV8, directV9, semanticV10, movementV11,
                relativeWellV12, stickyRouteV13, carrierClearanceV14,
                trafficDetourV15, stableCarrierV16, looseCoreMemoryV17,
                claimableCoreV18, taplineV19, ordersV20, current]);
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
            ArcRelayEntrantPlaylistDefinition.RecoveryVersion));
        Assert.Same(tacticalCounterflow, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.TacticalVersion));
        Assert.Same(ambushV7, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.AmbushVersion));
        Assert.Same(wasmRepairV8, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.WasmRepairVersion));
        Assert.Same(directV9, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.DirectLadderVersion));
        Assert.Same(semanticV10, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.SemanticIntentVersion));
        Assert.Same(movementV11, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.BoundedMovementVersion));
        Assert.Same(relativeWellV12, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.RelativeWellVersion));
        Assert.Same(stickyRouteV13, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.StickyRouteVersion));
        Assert.Same(carrierClearanceV14, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.CarrierClearanceVersion));
        Assert.Same(trafficDetourV15, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.TrafficDetourVersion));
        Assert.Same(stableCarrierV16, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.StableCarrierVersion));
        Assert.Same(looseCoreMemoryV17, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.LooseCoreMemoryVersion));
        Assert.Same(claimableCoreV18, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.ClaimableCoreVersion));
        Assert.Same(taplineV19, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.TaplineV1Version));
        Assert.Same(ordersV20, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.OrdersV2Version));
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
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV5();
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

    [Fact]
    public void Frozen_stocks_pin_v7_through_v21_and_their_sources_are_pinned()
    {
        string root = RepoPaths.FindUpward("BotArena.sln") is { } solution
            ? Path.GetDirectoryName(solution)!
            : throw new InvalidOperationException("Repository root is missing.");
        string historicalArtifact = Path.Combine(
            root, "arena-bots", "arc-relay", "ambush-warren-11-stock-v1",
            "bot.wasm");
        string repairedArtifact = Path.Combine(
            root, "arena-bots", "arc-relay", "ambush-warren-11-stock-v2",
            "bot.wasm");
        string directArtifact = Path.Combine(
            root, "arena-bots", "arc-relay", "ambush-warren-11-stock-v3",
            "bot.wasm");
        string semanticArtifact = Path.Combine(
            root, "arena-bots", "arc-relay", "ambush-warren-11-stock-v4",
            "bot.wasm");
        string movementArtifact = Path.Combine(
            root, "arena-bots", "arc-relay", "ambush-warren-11-stock-v5",
            "bot.wasm");
        string relativeWellArtifact = Path.Combine(
            root, "arena-bots", "arc-relay", "ambush-warren-11-stock-v6",
            "bot.wasm");
        string stickyRouteArtifact = Path.Combine(
            root, "arena-bots", "arc-relay", "ambush-warren-11-stock-v7",
            "bot.wasm");
        string carrierClearanceArtifact = Path.Combine(
            root, "arena-bots", "arc-relay", "ambush-warren-11-stock-v8",
            "bot.wasm");
        string trafficDetourArtifact = Path.Combine(
            root, "arena-bots", "arc-relay", "ambush-warren-11-stock-v9",
            "bot.wasm");
        string stableCarrierArtifact = Path.Combine(
            root, "arena-bots", "arc-relay", "ambush-warren-11-stock-v10",
            "bot.wasm");
        string looseCoreMemoryArtifact = Path.Combine(
            root, "arena-bots", "arc-relay", "ambush-warren-11-stock-v11",
            "bot.wasm");
        string claimableDropArtifact = Path.Combine(
            root, "arena-bots", "arc-relay", "ambush-warren-11-stock-v12",
            "bot.wasm");
        string ordersV2Artifact = Path.Combine(
            root, "arena-bots", "arc-relay", "tapline-1-stock-v1",
            "bot.wasm");
        string ordersArtifact = Path.Combine(
            root, "arena-bots", "arc-relay", "tapline-3-stock-v1",
            "bot.wasm");
        string playbook = Path.Combine(
            root, "arena-bots", "arc-relay",
            "tactical-playbook-v1-2026-08-03", "playbooks",
            "wellwright-v1.json");
        string layout = Path.Combine(
            root, "arena-bots", "arc-relay",
            "tactical-playbook-v1-2026-08-03", "layouts",
            "wellwright-warren6-v1.json");
        string taplinePlaybook = Path.Combine(
            root, "arena-bots", "arc-relay",
            "tactical-playbook-v1-2026-08-03", "playbooks",
            "wellwright-tapline-v1.json");
        string taplineLayout = Path.Combine(
            root, "arena-bots", "arc-relay",
            "tactical-playbook-v1-2026-08-03", "layouts",
            "tapline-base-v1.json");
        string ordersPlaybook = Path.Combine(
            root, "arena-bots", "arc-relay",
            "tactical-playbook-v1-2026-08-03", "playbooks",
            "wellwright-tapline-v2.json");
        string ordersMapPlan = Path.Combine(
            root, "arena-bots", "arc-relay",
            "tactical-playbook-v1-2026-08-03", "map-plans",
            "wellwright-tapline-v2.json");

        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition.OrdersTacticalArtifactHash,
            BotBuilder.Sha256File(ordersArtifact));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition.OrdersV2TacticalArtifactHash,
            BotBuilder.Sha256File(ordersV2Artifact));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition.TacticalArtifactHash,
            BotBuilder.Sha256File(claimableDropArtifact));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition
                .LooseCoreMemoryTacticalArtifactHash,
            BotBuilder.Sha256File(looseCoreMemoryArtifact));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition
                .StableCarrierTacticalArtifactHash,
            BotBuilder.Sha256File(stableCarrierArtifact));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition
                .TrafficDetourTacticalArtifactHash,
            BotBuilder.Sha256File(trafficDetourArtifact));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition
                .CarrierClearanceTacticalArtifactHash,
            BotBuilder.Sha256File(carrierClearanceArtifact));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition
                .StickyRouteTacticalArtifactHash,
            BotBuilder.Sha256File(stickyRouteArtifact));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition
                .RelativeWellTacticalArtifactHash,
            BotBuilder.Sha256File(relativeWellArtifact));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition
                .BoundedMovementTacticalArtifactHash,
            BotBuilder.Sha256File(movementArtifact));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition
                .SemanticIntentTacticalArtifactHash,
            BotBuilder.Sha256File(semanticArtifact));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition
                .DirectLadderTacticalArtifactHash,
            BotBuilder.Sha256File(directArtifact));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition.WasmRepairTacticalArtifactHash,
            BotBuilder.Sha256File(repairedArtifact));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition.HistoricalTacticalArtifactHash,
            BotBuilder.Sha256File(historicalArtifact));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition.StockPlaybookSourceHash,
            BotBuilder.Sha256File(playbook));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition.StockLayoutSourceHash,
            BotBuilder.Sha256File(layout));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition.TaplineStockPlaybookSourceHash,
            BotBuilder.Sha256File(taplinePlaybook));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition.TaplineStockLayoutSourceHash,
            BotBuilder.Sha256File(taplineLayout));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition.OrdersStockPlaybookSourceHash,
            BotBuilder.Sha256File(ordersPlaybook));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition.OrdersStockMapPlanSourceHash,
            BotBuilder.Sha256File(ordersMapPlan));
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
