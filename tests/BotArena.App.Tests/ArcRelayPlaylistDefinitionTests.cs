using BotArena.App.ArcRelay;
using BotArena.App.Competition;
using BotArena.App.Jobs;
using BotArena.Engine;
using BotArena.Runtime;
using BotArena.Runtime.Wasm;
using BotArena.Toolchain;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BotArena.App.Tests;

public sealed class ArcRelayPlaylistDefinitionTests
{
    [Fact]
    [Trait("Category", "ReleaseWasm")]
    public void ReleaseGate_CurrentPlaylistKeepsItsFrozenTacticalArtifact()
    {
        if (!ReleaseWasmRequired())
            return;

        string root = FindRepoRoot();
        string receiptPath = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-mind-v1",
            "bot.build-receipt.json");
        using JsonDocument receipt = JsonDocument.Parse(
            File.ReadAllText(receiptPath));
        JsonElement document = receipt.RootElement;

        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition.Broadloom7TacticalArtifactHash,
            document.GetProperty("artifactSha256").GetString());
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition.Broadloom7TacticalBuildInputHash,
            document.GetProperty("buildInputSha256").GetString());
    }

    [Fact]
    public void Entrant_v37_declares_signature_lines_while_v2_through_v36_remain_resolvable_history()
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
        ArcRelayEntrantPlaylistDefinition tapline3V21 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV21();
        ArcRelayEntrantPlaylistDefinition tapline4V22 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV22();
        ArcRelayEntrantPlaylistDefinition tapline5V23 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV23();
        ArcRelayEntrantPlaylistDefinition tapline6V24 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV24();
        ArcRelayEntrantPlaylistDefinition tapline8V25 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV25();
        ArcRelayEntrantPlaylistDefinition tapline9V26 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV26();
        ArcRelayEntrantPlaylistDefinition tapline10V27 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV27();
        ArcRelayEntrantPlaylistDefinition tapline11V28 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV28();
        ArcRelayEntrantPlaylistDefinition tapline12V29 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV29();
        ArcRelayEntrantPlaylistDefinition tapline13V30 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV30();
        ArcRelayEntrantPlaylistDefinition tapline14V31 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV31();
        ArcRelayEntrantPlaylistDefinition completeV32 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV32();
        ArcRelayEntrantPlaylistDefinition broadloomV33 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV33();
        ArcRelayEntrantPlaylistDefinition broadloomV34 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV34();
        ArcRelayEntrantPlaylistDefinition broadloomV35 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV35();
        ArcRelayEntrantPlaylistDefinition broadloomV36 =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV36();

        Assert.Equal(37, current.PlaylistVersion);
        Assert.Equal(ArcRelayLoopProfile.Broadloom7.MapId, current.Match.Map.Id);
        Assert.Equal("arc-relay-broadloom-01", current.Match.Map.Id);
        Assert.Equal(
            ArcRelayLoopProfile.Broadloom7.RulesetId,
            current.Match.Rules.RulesetId);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.Broadloom7TacticalArtifactHash,
            current.CanonicalDefinition);
        Assert.Contains("arc-relay-orders-v2", current.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.BroadloomStockSheetId,
            current.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.Broadloom7TacticalBuildInputHash,
            current.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.BroadloomStockPlaybookSourceHash,
            current.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.BroadloomStockMapPlanSourceHash,
            current.CanonicalDefinition);
        Assert.Contains("ownerApprovedBroadloom7", current.Provenance);
        Assert.Contains("\"newImmutableMapIdentity\":false", current.Provenance);
        Assert.Contains("\"mapWidth\":37", current.Provenance);
        Assert.Contains("\"mapHeight\":27", current.Provenance);
        Assert.Contains(
            "\"neutralVeterancyWorkshops\":true", current.Provenance);
        Assert.Contains("\"workshopUnlockTick\":25", current.Provenance);
        Assert.Contains("\"workshopChannelTicks\":15", current.Provenance);
        Assert.Contains("\"workshopXpAward\":1", current.Provenance);
        Assert.Contains("\"workshopDormancyTicks\":0", current.Provenance);
        Assert.Contains(
            "\"workshopPublicNeutralSignal\":true", current.Provenance);
        Assert.Contains("\"trainAtWorkshopTactics\":true", current.Provenance);
        Assert.Contains("\"reactiveImpactFacing\":true", current.Provenance);
        Assert.Contains("\"movingPrismScreen\":true", current.Provenance);
        Assert.Contains("\"baseVisionRange\":5", current.Provenance);
        Assert.Contains("\"shortAttackRange\":3", current.Provenance);
        Assert.Contains("\"mediumAttackRange\":4", current.Provenance);
        Assert.Contains("\"longAttackRange\":6", current.Provenance);
        Assert.Contains("sourceLocalWellSupply", current.Provenance);
        Assert.Contains("bankTriggeredWellRecharge", current.Provenance);
        Assert.Contains("equalReturnFork", current.Provenance);
        Assert.Contains("delayedCentreOpening", current.Provenance);
        Assert.Contains("broadloomNativeMapPlansOnly", current.Provenance);
        Assert.Contains("temperedBroadloomTacticsStock", current.Provenance);
        Assert.Contains("broadloom7TacticalArtifact", current.Provenance);
        Assert.Contains("custodySafeVectorDash", current.Provenance);
        Assert.Contains("damagingFrontPullTractorHook", current.Provenance);
        Assert.Contains("authoredHookWallBreachTiles", current.Provenance);
        Assert.Contains("hostileOnlyDamagingKineticBurst", current.Provenance);
        Assert.Contains("surveyFlareTeamVisionRadius", current.Provenance);
        Assert.Contains("custodySafeCarrierExchange", current.Provenance);
        Assert.Contains("priorityAwareSmartSignatures", current.Provenance);
        Assert.Contains("aggressiveRepulsorClosesForBurst", current.Provenance);
        Assert.Contains(
            "completeTacticalObservationProjection", current.Provenance);
        Assert.Contains("teamRelativeCoreCharge", current.Provenance);
        Assert.Contains("publicRespawnWaves", current.Provenance);
        Assert.Contains("minimumSevenDistinctClasses", current.Provenance);
        Assert.Contains("durableDeployments", current.Provenance);
        Assert.Contains("ordersV2DirectExecutor", current.Provenance);
        Assert.Contains("freshRatingEra", current.Provenance);
        Assert.Contains("\"veterancyDeathRetentionPercent\":50", current.Provenance);
        Assert.Contains("\"alliedProjectilesBlock\":true", current.Provenance);
        Assert.Contains("friendlyLaneAwareTacticalMovement", current.Provenance);
        Assert.Contains("veterancyPhaseConditions", current.Provenance);
        Assert.Contains("playerPlottedBlindFallingStar", current.Provenance);
        Assert.Contains("\"hiddenTripNodeMaximumActive\":2", current.Provenance);
        Assert.Contains("fullNullFieldSuppression", current.Provenance);
        Assert.Contains("declaredFirstContactHookRay", current.Provenance);
        Assert.Contains("declaredFirstContactSentryRay", current.Provenance);
        Assert.Contains("piercingRailLine", current.Provenance);
        Assert.Contains("fightOnlyDeclaredStrikeEvasion", current.Provenance);
        Assert.Contains("priorStockGameplayDigestChanged", current.Provenance);
        Assert.Contains("\"linkedDataFormat\":\"ATL2\"", current.Provenance);
        Assert.DoesNotContain("ownerApprovedTapline14", current.Provenance);
        Assert.DoesNotContain("frozenTemperedStandardStock", current.Provenance);
        Assert.DoesNotContain("ratingContinuityFromVersions", current.Provenance);
        Assert.Equal(36, broadloomV36.PlaylistVersion);
        Assert.Equal(
            ArcRelayLoopProfile.Broadloom4.RulesetId,
            broadloomV36.Match.Rules.RulesetId);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.Broadloom4TacticalArtifactHash,
            broadloomV36.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.Broadloom4TacticalBuildInputHash,
            broadloomV36.CanonicalDefinition);
        Assert.Contains("ownerApprovedBroadloom4", broadloomV36.Provenance);
        Assert.Contains("broadloom4TacticalArtifact", broadloomV36.Provenance);
        Assert.Contains("custodySafeVectorDash", broadloomV36.Provenance);
        Assert.Contains("priorityAwareSmartSignatures", broadloomV36.Provenance);
        Assert.DoesNotContain("playerPlottedBlindFallingStar", broadloomV36.Provenance);
        Assert.DoesNotContain("declaredFirstContactHookRay", broadloomV36.Provenance);
        Assert.Equal(35, broadloomV35.PlaylistVersion);
        Assert.Equal(
            ArcRelayLoopProfile.Broadloom3.RulesetId,
            broadloomV35.Match.Rules.RulesetId);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.Broadloom3TacticalArtifactHash,
            broadloomV35.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.Broadloom3TacticalBuildInputHash,
            broadloomV35.CanonicalDefinition);
        Assert.Contains("ownerApprovedBroadloom3", broadloomV35.Provenance);
        Assert.Contains("\"newImmutableMapIdentity\":false", broadloomV35.Provenance);
        Assert.Contains("broadloom3TacticalArtifact", broadloomV35.Provenance);
        Assert.DoesNotContain("custodySafeVectorDash", broadloomV35.Provenance);
        Assert.Equal(34, broadloomV34.PlaylistVersion);
        Assert.Equal(
            ArcRelayLoopProfile.Broadloom2.RulesetId,
            broadloomV34.Match.Rules.RulesetId);
        Assert.Contains("ownerApprovedBroadloom2", broadloomV34.Provenance);
        Assert.Contains(
            "\"workshopDormancyTicks\":45",
            broadloomV34.Provenance);
        Assert.Equal(
            "f7a1416289596dbc25555f78f4f13c208f880238e61ab8dd32ccce4fd28550ce",
            broadloomV34.DefinitionFingerprint);
        Assert.Equal(33, broadloomV33.PlaylistVersion);
        Assert.Equal(
            ArcRelayLoopProfile.Broadloom1.MapId,
            broadloomV33.Match.Map.Id);
        Assert.Equal(
            ArcRelayLoopProfile.Broadloom1.RulesetId,
            broadloomV33.Match.Rules.RulesetId);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition
                .Broadloom1TacticalArtifactHash,
            broadloomV33.CanonicalDefinition);
        Assert.Equal(
            "5ed88ecdb0a05d024ca47058661e622a1682215388965e6dcc6abc6d1ff1ee4a",
            broadloomV33.DefinitionFingerprint);
        Assert.Contains("neutralVeterancyWorkshops", broadloomV33.Provenance);
        Assert.DoesNotContain(
            "veterancyDeathRetentionPercent", broadloomV33.Provenance);
        Assert.DoesNotContain("alliedProjectilesBlock", broadloomV33.Provenance);
        Assert.Equal(32, completeV32.PlaylistVersion);
        Assert.Equal(
            ArcRelayLoopProfile.Tapline14.MapId, completeV32.Match.Map.Id);
        Assert.Equal(
            ArcRelayLoopProfile.Tapline14.RulesetId,
            completeV32.Match.Rules.RulesetId);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition
                .CompleteObservationTacticalArtifactHash,
            completeV32.CanonicalDefinition);
        Assert.DoesNotContain("veterancyDeathRetentionPercent", completeV32.Provenance);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.Tapline4StockSheetId,
            completeV32.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.Tapline14StockPlaybookSourceHash,
            completeV32.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.Tapline9StockMapPlanSourceHash,
            completeV32.CanonicalDefinition);
        Assert.Contains("ownerApprovedTapline14", completeV32.Provenance);
        Assert.Contains(
            "completeTacticalObservationProjection", completeV32.Provenance);
        Assert.Contains(
            "approachExactDurableDeployments", completeV32.Provenance);
        Assert.Contains("freshRatingEra", completeV32.Provenance);
        Assert.DoesNotContain(
            "neutralVeterancyWorkshops", completeV32.Provenance);
        Assert.Equal(31, tapline14V31.PlaylistVersion);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.Tapline14TacticalArtifactHash,
            tapline14V31.CanonicalDefinition);
        Assert.Contains("freshRatingEra", tapline14V31.Provenance);
        Assert.DoesNotContain(
            "completeTacticalObservationProjection", tapline14V31.Provenance);
        Assert.Equal(30, tapline13V30.PlaylistVersion);
        Assert.Equal(
            ArcRelayLoopProfile.Tapline13.RulesetId,
            tapline13V30.Match.Rules.RulesetId);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.Tapline13TacticalArtifactHash,
            tapline13V30.CanonicalDefinition);
        Assert.Contains("ownerApprovedTapline13", tapline13V30.Provenance);
        Assert.DoesNotContain(
            "reactiveImpactFacing",
            tapline13V30.Provenance);
        Assert.Equal(29, tapline12V29.PlaylistVersion);
        Assert.Equal(
            "898b2120e75d0ac3ab2a9d7ac91eff1213ebfd9ee8ed5fb02131cdda930441f4",
            tapline12V29.DefinitionFingerprint);
        Assert.Equal(
            ArcRelayLoopProfile.Tapline12.RulesetId,
            tapline12V29.Match.Rules.RulesetId);
        Assert.Contains("ownerApprovedTapline12", tapline12V29.Provenance);
        Assert.Contains("movingPrismScreen", tapline12V29.Provenance);
        Assert.DoesNotContain("sentryTeamVision", tapline12V29.Provenance);
        Assert.Equal(28, tapline11V28.PlaylistVersion);
        Assert.Equal(
            ArcRelayLoopProfile.Tapline11.RulesetId,
            tapline11V28.Match.Rules.RulesetId);
        Assert.Contains("ownerApprovedTapline11", tapline11V28.Provenance);
        Assert.DoesNotContain("movingPrismScreen", tapline11V28.Provenance);
        Assert.Equal(27, tapline10V27.PlaylistVersion);
        Assert.Equal(
            ArcRelayLoopProfile.Tapline10.RulesetId,
            tapline10V27.Match.Rules.RulesetId);
        Assert.Contains("ownerApprovedTapline10", tapline10V27.Provenance);
        Assert.DoesNotContain("unifiedCombatStrafeMovement", tapline10V27.Provenance);
        Assert.Equal(26, tapline9V26.PlaylistVersion);
        Assert.Equal(
            ArcRelayLoopProfile.Tapline9.RulesetId,
            tapline9V26.Match.Rules.RulesetId);
        Assert.Equal("arc-relay-tapline-03", tapline9V26.Match.Map.Id);
        Assert.Contains("ownerApprovedTapline9", tapline9V26.Provenance);
        Assert.Contains("newImmutableMapIdentity", tapline9V26.Provenance);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.Tapline9StockMapPlanSourceHash,
            tapline9V26.CanonicalDefinition);
        Assert.Equal(25, tapline8V25.PlaylistVersion);
        Assert.Equal(
            ArcRelayLoopProfile.Tapline8.RulesetId,
            tapline8V25.Match.Rules.RulesetId);
        Assert.Equal("arc-relay-tapline-02", tapline8V25.Match.Map.Id);
        Assert.Contains("ownerApprovedTapline8", tapline8V25.Provenance);
        Assert.DoesNotContain("sealedOuterWellEntrancesStaggeredMidCover", tapline8V25.Provenance);
        Assert.DoesNotContain("newImmutableMapIdentity", tapline8V25.Provenance);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.Tapline6StockMapPlanSourceHash,
            tapline8V25.CanonicalDefinition);
        Assert.Equal(24, tapline6V24.PlaylistVersion);
        Assert.Equal(
            ArcRelayLoopProfile.Tapline6.RulesetId,
            tapline6V24.Match.Rules.RulesetId);
        Assert.Equal("arc-relay-tapline-02", tapline6V24.Match.Map.Id);
        Assert.Contains("ownerApprovedTapline6", tapline6V24.Provenance);
        Assert.Contains("newImmutableMapIdentity", tapline6V24.Provenance);
        Assert.Contains("equalReturnFork", tapline6V24.Provenance);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.Tapline6StockMapPlanSourceHash,
            tapline6V24.CanonicalDefinition);
        Assert.Equal(23, tapline5V23.PlaylistVersion);
        Assert.Equal(
            ArcRelayLoopProfile.Tapline5.RulesetId,
            tapline5V23.Match.Rules.RulesetId);
        Assert.Equal("arc-relay-tapline-01", tapline5V23.Match.Map.Id);
        Assert.Contains("ownerApprovedTapline5", tapline5V23.Provenance);
        Assert.Contains("delayedCentreOpening", tapline5V23.Provenance);
        Assert.DoesNotContain("equalReturnFork", tapline5V23.Provenance);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.Tapline4StockMapPlanSourceHash,
            tapline5V23.CanonicalDefinition);
        Assert.Equal(22, tapline4V22.PlaylistVersion);
        Assert.Equal(
            ArcRelayLoopProfile.Tapline4.RulesetId,
            tapline4V22.Match.Rules.RulesetId);
        Assert.Contains("ownerApprovedTapline4", tapline4V22.Provenance);
        Assert.DoesNotContain("delayedCentreOpening", tapline4V22.Provenance);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.Tapline4TacticalArtifactHash,
            tapline4V22.CanonicalDefinition);
        Assert.Equal(21, tapline3V21.PlaylistVersion);
        Assert.Equal(
            ArcRelayLoopProfile.Tapline3.RulesetId,
            tapline3V21.Match.Rules.RulesetId);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.OrdersTacticalArtifactHash,
            tapline3V21.CanonicalDefinition);
        Assert.Contains(
            ArcRelayEntrantPlaylistDefinition.OrdersStockSheetId,
            tapline3V21.CanonicalDefinition);
        Assert.Contains("ownerApprovedTapline3", tapline3V21.Provenance);
        Assert.DoesNotContain("teamRelativeCoreCharge", tapline3V21.Provenance);
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
                claimableCoreV18, taplineV19, ordersV20, tapline3V21,
                tapline4V22, tapline5V23, tapline6V24, tapline8V25,
                tapline9V26, tapline10V27, tapline11V28, tapline12V29,
                tapline13V30, tapline14V31, completeV32, broadloomV33,
                broadloomV34, broadloomV35, broadloomV36, current]);
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
        Assert.Same(tapline3V21, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.Tapline3Version));
        Assert.Same(tapline4V22, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.Tapline4Version));
        Assert.Same(tapline5V23, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.Tapline5Version));
        Assert.Same(tapline6V24, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.Tapline6Version));
        Assert.Same(tapline8V25, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.Tapline8Version));
        Assert.Same(tapline9V26, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.Tapline9Version));
        Assert.Same(tapline10V27, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.Tapline10Version));
        Assert.Same(tapline11V28, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.Tapline11Version));
        Assert.Same(tapline12V29, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.Tapline12Version));
        Assert.Same(tapline13V30, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.Tapline13Version));
        Assert.Same(tapline14V31, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.Tapline14Version));
        Assert.Same(completeV32, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.CompleteObservationVersion));
        Assert.Same(broadloomV33, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.BroadloomInitialVersion));
        Assert.Same(broadloomV34, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.Broadloom2Version));
        Assert.Same(broadloomV35, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.Broadloom3Version));
        Assert.Same(broadloomV36, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.Broadloom4Version));
        Assert.Same(current, registry.Resolve(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.Version));
        string currentJobType = GenericActorMatchJobType.ForPlaylist(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.Version);
        string historicalJobType = GenericActorMatchJobType.ForPlaylist(
            ArcRelayEntrantPlaylistDefinition.PlaylistKey,
            ArcRelayEntrantPlaylistDefinition.OrdersV2Version);
        Assert.True(registry.SupportsJobType(currentJobType));
        Assert.False(registry.SupportsJobType(historicalJobType));
        Assert.False(ArcRelayPlaylistDefinition.Create()
            .CanExecutePendingMatches);
        Assert.Throws<InvalidOperationException>(
            () => registry.ResolveJobType(historicalJobType));
    }

    [Fact]
    public void Historical_v27_definition_and_provenance_remain_byte_exact()
    {
        ArcRelayEntrantPlaylistDefinition historical =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV27();

        const string definition =
            "2c73b3d8233c314aa23f5f722a183bde720543c3179baba6eaca930a5ae81652";
        Assert.Equal(definition, historical.DefinitionFingerprint);
        Assert.Equal(definition, Sha256Text(historical.CanonicalDefinition));
        Assert.Equal(
            "70afce1f2ed1430bd7b862647d842673f36e9062152c577a04e5617a9a139447",
            Sha256Text(historical.Provenance));
    }

    [Fact]
    public void Historical_v28_definition_and_provenance_remain_byte_exact()
    {
        ArcRelayEntrantPlaylistDefinition historical =
            ArcRelayEntrantPlaylistDefinition.CreateHistoricalV28();

        const string definition =
            "d36f71ad174a48b478508a0f8fb87b519f211993fec0f81b82599b3d726ecd33";
        Assert.Equal(definition, historical.DefinitionFingerprint);
        Assert.Equal(definition, Sha256Text(historical.CanonicalDefinition));
        Assert.Equal(
            "9b4c8d1ad3153e82f838a3c441bc8cabf76cb3807146c69b0dad12c6730e981f",
            Sha256Text(historical.Provenance));
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

    private static bool ReleaseWasmRequired() =>
        string.Equals(
            Environment.GetEnvironmentVariable(
                "BOTARENA_REQUIRE_CURRENT_TACTICAL_WASM"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    private static string FindRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BotArena.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root is missing.");
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
        JsonElement orders = root.GetProperty("orders");
        JsonElement destinations = root.GetProperty("destinations");
        JsonElement facings = root.GetProperty("facings");
        Assert.Equal(worlds.GetArrayLength(), vision.GetArrayLength());
        Assert.Equal(worlds.GetArrayLength(), orders.GetArrayLength());
        Assert.Equal(worlds.GetArrayLength(), destinations.GetArrayLength());
        Assert.Equal(worlds.GetArrayLength(), facings.GetArrayLength());
        Assert.All(vision[0].EnumerateArray(), team =>
            Assert.NotEmpty(team[1].EnumerateArray()));

        using JsonDocument prefix = JsonDocument.Parse(
            ArcRelayBroadcastDocument.CreatePartialPrefix(
                System.Text.Encoding.UTF8.GetString(compact.CanonicalUtf8.Span),
                3));
        Assert.Equal(
            Math.Min(3, worlds.GetArrayLength()),
            prefix.RootElement.GetProperty("vision").GetArrayLength());
        Assert.Equal(
            Math.Min(3, worlds.GetArrayLength()),
            prefix.RootElement.GetProperty("facings").GetArrayLength());
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
    public void Frozen_stocks_pin_v7_through_v24_and_their_sources_are_pinned()
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
        string tapline4Artifact = Path.Combine(
            root, "arena-bots", "arc-relay", "tapline-4-stock-v1",
            "bot.wasm");
        string tapline11Artifact = Path.Combine(
            root, "arena-bots", "arc-relay", "tapline-11-stock-v1",
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
        string tapline4Playbook = Path.Combine(
            root, "arena-bots", "arc-relay",
            "tactical-playbook-v1-2026-08-03", "playbooks",
            "tempered-standard-v1.json");
        string tapline4MapPlan = Path.Combine(
            root, "arena-bots", "arc-relay",
            "tactical-playbook-v1-2026-08-03", "map-plans",
            "tempered-standard-v1.json");
        string tapline6MapPlan = Path.Combine(
            root, "arena-bots", "arc-relay",
            "tactical-playbook-v1-2026-08-03", "map-plans", "tapline-6",
            "tempered-standard-v1.json");
        string tapline9MapPlan = Path.Combine(
            root, "arena-bots", "arc-relay",
            "tactical-playbook-v1-2026-08-03", "map-plans", "tapline-9",
            "tempered-standard-v1.json");

        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition.Tapline11TacticalArtifactHash,
            BotBuilder.Sha256File(tapline11Artifact));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition.Tapline4TacticalArtifactHash,
            BotBuilder.Sha256File(tapline4Artifact));
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
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition.Tapline14StockPlaybookSourceHash,
            BotBuilder.Sha256File(tapline4Playbook));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition.Tapline4StockMapPlanSourceHash,
            BotBuilder.Sha256File(tapline4MapPlan));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition.Tapline6StockMapPlanSourceHash,
            BotBuilder.Sha256File(tapline6MapPlan));
        Assert.Equal(
            ArcRelayEntrantPlaylistDefinition.Tapline9StockMapPlanSourceHash,
            BotBuilder.Sha256File(tapline9MapPlan));
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

    private static string Sha256Text(string value) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
