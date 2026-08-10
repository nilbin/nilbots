using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotArena.App.ArcRelay;
using BotArena.Engine;

namespace BotArena.App.Competition;

/// <summary>
/// Immutable Arc Relay entrant lane. Each instance pins one historical product
/// version so queued matches remain executable after the hosted map advances.
/// </summary>
public sealed class ArcRelayEntrantPlaylistDefinition : IHostedGenericMatchDefinition
{
    public const string PlaylistKey = ArcRelayPlaylistDefinition.PlaylistKey;
    public const string DisplayName = ArcRelayPlaylistDefinition.DisplayName;
    public const int Version = 13;
    public const int RelativeWellVersion = 12;
    public const int BoundedMovementVersion = 11;
    public const int SemanticIntentVersion = 10;
    public const int DirectLadderVersion = 9;
    public const int WasmRepairVersion = 8;
    public const int AmbushVersion = 7;
    public const int TacticalVersion = 6;
    public const int RecoveryVersion = 5;
    public const int ForwardVersion = 4;
    public const int CounterflowVersion = 3;
    public const int HistoricalVersion = 2;
    public const string SeriesPolicyId = "single-match-v1";
    public const string MatchmakingPolicyId = "passive-elo-proximity-v1";
    public const string Visibility = PlaylistVisibilityIds.Public;
    public const string TacticalArtifactHash =
        "0db80102a2dbc902d2bc91dae434c729a39aa0788f8b691bc0ef3d11f5507e85";
    public const string TacticalBuildInputHash =
        "f9a96665e9592b865f915c40115619c9f3068df848c0a8f5404c4034a642b129";
    public const string RelativeWellTacticalArtifactHash =
        "5e2995feb56dc480be28fdb2bde66879dc3f97d0a29ea338b11aa8a30eabaad7";
    public const string RelativeWellTacticalBuildInputHash =
        "dcfc79936f976f703d8705050f0618427f73e6bd4d770de986dc7027f13ab39e";
    public const string BoundedMovementTacticalArtifactHash =
        "50e932fe5a7e79791bfdc9959c868cb58f06925a73c170d4ce105c636dd0a060";
    public const string BoundedMovementTacticalBuildInputHash =
        "21247aa1e49b5f46c11c8b93da02245f161d6fb8d3ad4234081a915fe053a9ff";
    public const string SemanticIntentTacticalArtifactHash =
        "c2b2f0a92f34d2d006bd4e7ecf8d68d0750df89300db7e23f33ef111d793a9a0";
    public const string SemanticIntentTacticalBuildInputHash =
        "a63314ed3de6599fd96b3ccaae5a527eb96364e1950f25decec8b4241bcda4ff";
    public const string DirectLadderTacticalArtifactHash =
        "48d778f42e104e30611216c9e72620981e9994857a0052188ba08a866064de3f";
    public const string DirectLadderTacticalBuildInputHash =
        "b126edf8c768a7cca8aec88bcd851ab77f458522a88ed710e29b7399e35b390a";
    public const string WasmRepairTacticalArtifactHash =
        "7cf7e2f56fad1d80bec2250b0c309191b9b24ac2141bf36374116431c19adec3";
    public const string WasmRepairTacticalBuildInputHash =
        "b4b9646298f6aa754f662fc2dfe6838775ff32231db850f9f03b2df72f33165d";
    public const string HistoricalTacticalArtifactHash =
        "42288a911821f7893f34fb096cd8c17464f24da7a170761898f10e8b0497763b";
    public const string StockSheetId = "wellwright-v1";
    public const string StockPlaybookSourceHash =
        "2167bd34ea456e11cce271ecb822f82b03b55aa5b7341090803caed5172db017";
    public const string HistoricalStockPlaybookSourceHash =
        "684d17e9064b557441b7440570effd122363a4ea617b8484c708fb0b14c2ffc7";
    public const string StockLayoutSourceHash =
        "89141634c776ae7d98c7611577450fe7818d8dcaee8e0bb7adb9e343ce3699a5";

    private ArcRelayEntrantPlaylistDefinition(
        int version,
        ArcRelayLoopProfile loopProfile,
        ActorResolvedMatchDefinition representative,
        string canonicalDefinition,
        string definitionFingerprint,
        string provenance)
    {
        PlaylistVersion = version;
        LoopProfile = loopProfile;
        Match = representative;
        ReplayPresentation = ArcRelayH0ReplayPresentation.Create(representative);
        CanonicalDefinition = canonicalDefinition;
        DefinitionFingerprint = definitionFingerprint;
        Provenance = provenance;
    }

    public ActorResolvedMatchDefinition Match { get; }
    public int PlaylistVersion { get; }
    public ArcRelayLoopProfile LoopProfile { get; }
    public GenericActorReplayPresentation ReplayPresentation { get; }
    public string CanonicalDefinition { get; }
    public string DefinitionFingerprint { get; }
    public string Provenance { get; }
    public string AdmissionPolicyId => BotArenaVersions.GenericMindContractProfileId;
    public string ExecutionPolicyId => PlaylistExecutionPolicyIds.GenericActor;
    public string ExecutionEngineVersion => BotArenaVersions.GenericActorEngineVersion;
    public HostedGenericRuntimeModel RuntimeModel =>
        HostedGenericRuntimeModel.ArcRelayEntrants;
    public double? PresentationTicksPerSecond => 1.25;
    string IHostedGenericMatchDefinition.PlaylistKey => PlaylistKey;
    int IHostedGenericMatchDefinition.Version => PlaylistVersion;

    public static ArcRelayEntrantPlaylistDefinition Create() => Create(
        Version,
        ArcRelayLoopProfile.AmbushWarren11,
        source: "arc-relay-progress-aware-routes",
        canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV12() =>
        Create(
            RelativeWellVersion,
            ArcRelayLoopProfile.AmbushWarren11,
            source: "arc-relay-relative-well-subjects",
            canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV11() =>
        Create(
            BoundedMovementVersion,
            ArcRelayLoopProfile.AmbushWarren11,
            source: "arc-relay-bounded-tactical-movement",
            canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV10() =>
        Create(
            SemanticIntentVersion,
            ArcRelayLoopProfile.AmbushWarren11,
            source: "arc-relay-semantic-intent-labels",
            canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV9() =>
        Create(
            DirectLadderVersion,
            ArcRelayLoopProfile.AmbushWarren11,
            source: "arc-relay-direct-sheet-dsl",
            canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV8() =>
        Create(
            WasmRepairVersion,
            ArcRelayLoopProfile.AmbushWarren11,
            source: "arc-relay-tactical-wasm-repair",
            canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV7() =>
        Create(
            AmbushVersion,
            ArcRelayLoopProfile.AmbushWarren11,
            source: "arc-relay-ambush-warren-11-hosted-promotion",
            canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV6() => Create(
        TacticalVersion,
        ArcRelayLoopProfile.ForwardCombat,
        source: "arc-relay-tactical-sheet-editor-pass",
        canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV5() => Create(
        RecoveryVersion,
        ArcRelayLoopProfile.ForwardCombat,
        source: "arc-relay-stock-recovery-pass",
        canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV4() => Create(
        ForwardVersion,
        ArcRelayLoopProfile.ForwardCombat,
        source: "arc-relay-forward-combat-owner-ruling",
        canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV3() => Create(
        CounterflowVersion,
        ArcRelayLoopProfile.DepthCounterflow,
        source: "arc-relay-counterflow-owner-ruling",
        canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV2() => Create(
        HistoricalVersion,
        ArcRelayLoopProfile.HomeGatesWide,
        source: "arc-relay-entrant-ladder-pass",
        canonicalGameplayUnchangedFromVersion1: true);

    private static ArcRelayEntrantPlaylistDefinition Create(
        int version,
        ArcRelayLoopProfile loopProfile,
        string source,
        bool canonicalGameplayUnchangedFromVersion1)
    {
        string[] classes = ArcRelayLegacySnapshotCodec.NewSheetTemplate().Slots
            .OrderBy(slot => slot.UnitId)
            .Select(slot => slot.ClassId)
            .ToArray();
        ActorResolvedMatchDefinition representative = ArcRelayH0Definition.Create(
            classes,
            classes,
            loopProfile: loopProfile);
        string canonicalDefinition = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("gameModeId", representative.Rules.GameMode.ModeId);
            writer.WriteString("rulesetId", representative.Rules.RulesetId);
            writer.WriteString("matchFormatId", representative.Format.FormatId);
            writer.WriteString("mapPoolId", representative.Map.Id);
            writer.WriteString("loopProfileId", loopProfile.Id);
            writer.WriteString("seriesPolicyId", SeriesPolicyId);
            writer.WriteString("matchmakingPolicyId", MatchmakingPolicyId);
            writer.WriteString("admissionPolicyId", BotArenaVersions.GenericMindContractProfileId);
            writer.WriteString("executionPolicyId", PlaylistExecutionPolicyIds.GenericActor);
            writer.WriteString("executionEngineVersion", BotArenaVersions.GenericActorEngineVersion);
            writer.WriteString("runtimeModel", "arc-relay-entrants-v1");
            writer.WriteString(
                "stockArtifactHash",
                version switch
                {
                    Version => TacticalArtifactHash,
                    RelativeWellVersion =>
                        RelativeWellTacticalArtifactHash,
                    BoundedMovementVersion =>
                        BoundedMovementTacticalArtifactHash,
                    SemanticIntentVersion =>
                        SemanticIntentTacticalArtifactHash,
                    DirectLadderVersion =>
                        DirectLadderTacticalArtifactHash,
                    WasmRepairVersion => WasmRepairTacticalArtifactHash,
                    AmbushVersion or TacticalVersion =>
                        HistoricalTacticalArtifactHash,
                    RecoveryVersion => ArcRelayPlaylistDefinition.ForwardStockArtifactHash,
                    ForwardVersion => ArcRelayPlaylistDefinition
                        .HistoricalForwardStockArtifactHash,
                    _ => ArcRelayPlaylistDefinition.StockArtifactHash,
                });
            writer.WriteString("sheetSchema", version >= TacticalVersion
                ? "arc-relay-tactical-playbook-v1"
                : ArcRelayLegacySnapshotCodec.SchemaId);
            if (version is Version or RelativeWellVersion
                or SemanticIntentVersion
                or DirectLadderVersion
                or WasmRepairVersion or AmbushVersion)
            {
                writer.WriteString("stockSheetId", StockSheetId);
                writer.WriteString(
                    "stockPlaybookSourceHash",
                    version is Version or RelativeWellVersion
                        or SemanticIntentVersion
                        or DirectLadderVersion
                        ? StockPlaybookSourceHash
                        : HistoricalStockPlaybookSourceHash);
                writer.WriteString(
                    "stockLayoutSourceHash", StockLayoutSourceHash);
            }
            if (version is Version or RelativeWellVersion
                or SemanticIntentVersion
                or DirectLadderVersion
                or WasmRepairVersion)
            {
                writer.WriteString(
                    "stockBuildInputHash",
                    version switch
                    {
                        Version => TacticalBuildInputHash,
                        RelativeWellVersion =>
                            RelativeWellTacticalBuildInputHash,
                        SemanticIntentVersion =>
                            SemanticIntentTacticalBuildInputHash,
                        DirectLadderVersion =>
                            DirectLadderTacticalBuildInputHash,
                        _ => WasmRepairTacticalBuildInputHash,
                    });
            }
            writer.WriteString("compositionSchema", "arc-relay-composition-v1");
            writer.WriteNumber("compositionSlots", ArcRelayComposition.SlotCount);
            writer.WriteNumber("maximumCopiesPerClass", ArcRelayComposition.MaximumCopiesPerClass);
            writer.WriteNumber("maximumOptedInPerAccount", ArcRelayLadderPolicy.MaximumOptedInPerAccount);
            writer.WriteNumber("maximumMatchesPerEntrantPerDay", ArcRelayLadderPolicy.MaximumMatchesPerEntrantPerDay);
            writer.WriteNumber("presentationTicksPerSecond", 1.25);
            writer.WriteString("rulesFingerprint", ActorContractFingerprint.ComputeRules(representative.Rules));
            writer.WriteString("mapFingerprint", ActorContractFingerprint.ComputeMap(representative.Map));
            writer.WriteEndObject();
        });
        string provenance = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("source", source);
            writer.WriteBoolean("ranked", true);
            writer.WriteBoolean("sheetTrustedStockOnly", true);
            writer.WriteBoolean("customMindSandboxOnly", true);
            writer.WriteBoolean(
                "canonicalGameplayUnchangedFromVersion1",
                canonicalGameplayUnchangedFromVersion1);
            if (version == Version)
            {
                writer.WriteBoolean("ownerApprovedAmbushWarren11", true);
                writer.WriteBoolean("warrenNativeLayoutsOnly", true);
                writer.WriteBoolean("frozenWellwrightStock", true);
                writer.WriteBoolean("tacticalPlaybookCompilerShared", true);
                writer.WriteBoolean("portableTwoFileSheets", true);
                writer.WriteBoolean("staleWasmArtifactRepaired", true);
                writer.WriteBoolean("pendingStrikeWireParity", true);
                writer.WriteBoolean("consolidatedDirectSheetDsl", true);
                writer.WriteBoolean("semanticIntentLabels", true);
                writer.WriteBoolean("boundedRecoveryEpisodes", true);
                writer.WriteBoolean("deterministicTrafficTickets", true);
                writer.WriteBoolean("boundedOscillationYield", true);
                writer.WriteBoolean("relativeWellSubjects", true);
                writer.WriteBoolean("stickyAuthoredRouteCourses", true);
                writer.WriteBoolean("progressAwareRouteHints", true);
                writer.WriteBoolean("freshRatingEra", true);
                writer.WriteBoolean("priorStockGameplayDigestChanged", true);
                writer.WriteString("linkedDataFormat", "ATL1");
            }
            else if (version == RelativeWellVersion)
            {
                // Preserve v12's immutable provenance bytes exactly.
                writer.WriteBoolean("ownerApprovedAmbushWarren11", true);
                writer.WriteBoolean("warrenNativeLayoutsOnly", true);
                writer.WriteBoolean("frozenWellwrightStock", true);
                writer.WriteBoolean("tacticalPlaybookCompilerShared", true);
                writer.WriteBoolean("portableTwoFileSheets", true);
                writer.WriteBoolean("staleWasmArtifactRepaired", true);
                writer.WriteBoolean("pendingStrikeWireParity", true);
                writer.WriteBoolean("consolidatedDirectSheetDsl", true);
                writer.WriteBoolean("semanticIntentLabels", true);
                writer.WriteBoolean("boundedRecoveryEpisodes", true);
                writer.WriteBoolean("deterministicTrafficTickets", true);
                writer.WriteBoolean("boundedOscillationYield", true);
                writer.WriteBoolean("relativeWellSubjects", true);
                writer.WriteString("linkedDataFormat", "ATL1");
                writer.WriteStartArray("ratingContinuityFromVersions");
                writer.WriteNumberValue(WasmRepairVersion);
                writer.WriteNumberValue(DirectLadderVersion);
                writer.WriteNumberValue(SemanticIntentVersion);
                writer.WriteNumberValue(BoundedMovementVersion);
                writer.WriteEndArray();
            }
            else if (version == BoundedMovementVersion)
            {
                // Preserve v11's immutable provenance bytes exactly.
                writer.WriteBoolean("ownerApprovedAmbushWarren11", true);
                writer.WriteBoolean("warrenNativeLayoutsOnly", true);
                writer.WriteBoolean("frozenWellwrightStock", true);
                writer.WriteBoolean("tacticalPlaybookCompilerShared", true);
                writer.WriteBoolean("portableTwoFileSheets", true);
                writer.WriteBoolean("staleWasmArtifactRepaired", true);
                writer.WriteBoolean("pendingStrikeWireParity", true);
                writer.WriteBoolean("consolidatedDirectSheetDsl", true);
                writer.WriteBoolean("semanticIntentLabels", true);
                writer.WriteBoolean("boundedRecoveryEpisodes", true);
                writer.WriteBoolean("deterministicTrafficTickets", true);
                writer.WriteBoolean("boundedOscillationYield", true);
                writer.WriteString("linkedDataFormat", "ATL1");
                writer.WriteStartArray("ratingContinuityFromVersions");
                writer.WriteNumberValue(WasmRepairVersion);
                writer.WriteNumberValue(DirectLadderVersion);
                writer.WriteNumberValue(SemanticIntentVersion);
                writer.WriteEndArray();
            }
            else if (version == SemanticIntentVersion)
            {
                // Preserve v10's immutable provenance bytes exactly.
                writer.WriteBoolean("ownerApprovedAmbushWarren11", true);
                writer.WriteBoolean("warrenNativeLayoutsOnly", true);
                writer.WriteBoolean("frozenWellwrightStock", true);
                writer.WriteBoolean("tacticalPlaybookCompilerShared", true);
                writer.WriteBoolean("portableTwoFileSheets", true);
                writer.WriteBoolean("staleWasmArtifactRepaired", true);
                writer.WriteBoolean("pendingStrikeWireParity", true);
                writer.WriteBoolean("consolidatedDirectSheetDsl", true);
                writer.WriteBoolean("semanticIntentLabels", true);
                writer.WriteString("linkedDataFormat", "ATL1");
                writer.WriteStartArray("ratingContinuityFromVersions");
                writer.WriteNumberValue(WasmRepairVersion);
                writer.WriteNumberValue(DirectLadderVersion);
                writer.WriteEndArray();
            }
            else if (version == DirectLadderVersion)
            {
                // Preserve v9's immutable provenance bytes exactly.
                writer.WriteBoolean("ownerApprovedAmbushWarren11", true);
                writer.WriteBoolean("warrenNativeLayoutsOnly", true);
                writer.WriteBoolean("frozenWellwrightStock", true);
                writer.WriteBoolean("tacticalPlaybookCompilerShared", true);
                writer.WriteBoolean("portableTwoFileSheets", true);
                writer.WriteBoolean("staleWasmArtifactRepaired", true);
                writer.WriteBoolean("pendingStrikeWireParity", true);
                writer.WriteBoolean("consolidatedDirectSheetDsl", true);
                writer.WriteString("linkedDataFormat", "ATL1");
                writer.WriteNumber(
                    "ratingContinuityFromVersion", WasmRepairVersion);
            }
            else if (version == WasmRepairVersion)
            {
                // Preserve v8's immutable provenance bytes exactly.
                writer.WriteBoolean("ownerApprovedAmbushWarren11", true);
                writer.WriteBoolean("warrenNativeLayoutsOnly", true);
                writer.WriteBoolean("frozenWellwrightStock", true);
                writer.WriteBoolean("tacticalPlaybookCompilerShared", true);
                writer.WriteBoolean("portableTwoFileSheets", true);
                writer.WriteBoolean("staleWasmArtifactRepaired", true);
                writer.WriteBoolean("pendingStrikeWireParity", true);
                writer.WriteNumber(
                    "ratingContinuityFromVersion", AmbushVersion);
            }
            else if (version == AmbushVersion)
            {
                // Preserve v7's immutable provenance bytes exactly.
                writer.WriteBoolean("ownerApprovedAmbushWarren11", true);
                writer.WriteBoolean("freshRatingEra", true);
                writer.WriteBoolean("warrenNativeLayoutsOnly", true);
                writer.WriteBoolean("frozenWellwrightStock", true);
                writer.WriteBoolean("tacticalPlaybookCompilerShared", true);
                writer.WriteBoolean("portableTwoFileSheets", true);
            }
            else if (version == TacticalVersion)
            {
                // Preserve v6's immutable provenance bytes exactly.
                writer.WriteBoolean("ownerApprovedForwardCombat", true);
                writer.WriteBoolean("tacticalPlaybookCompilerShared", true);
                writer.WriteBoolean("portableTwoFileSheets", true);
                writer.WriteNumber("ratingContinuityFromVersion", RecoveryVersion);
            }
            else if (version == RecoveryVersion)
            {
                // Preserve v5's immutable provenance bytes exactly.
                writer.WriteBoolean("ownerApprovedForwardCombat", true);
                writer.WriteBoolean("stockRecoveryPass", true);
                writer.WriteNumber("ratingContinuityFromVersion", ForwardVersion);
            }
            else if (version == ForwardVersion)
            {
                // Preserve v4's immutable provenance bytes exactly.
                writer.WriteBoolean("ownerApprovedForwardCombat", true);
                writer.WriteNumber(
                    "ratingContinuityFromVersion", CounterflowVersion);
            }
            else if (version == CounterflowVersion)
            {
                // Preserve v3's immutable provenance bytes exactly.
                writer.WriteBoolean("ownerAcceptedFailedDepthGates", true);
                writer.WriteNumber("ratingContinuityFromVersion", HistoricalVersion);
            }
            writer.WriteEndObject();
        });
        return new ArcRelayEntrantPlaylistDefinition(
            version,
            loopProfile,
            representative,
            canonicalDefinition,
            Sha256(canonicalDefinition),
            provenance);
    }

    public ActorResolvedMatchDefinition ResolveMatch(
        IReadOnlyList<HostedGenericParticipantInput> participants)
    {
        if (participants.Count != 2)
            throw new InvalidOperationException("Arc Relay requires two entrants.");
        HostedGenericParticipantInput first = participants.Single(value =>
            value.ParticipantId == 0 && value.TeamId == 0);
        HostedGenericParticipantInput second = participants.Single(value =>
            value.ParticipantId == 1 && value.TeamId == 1);
        return ArcRelayH0Definition.Create(
            first.ClassIds,
            second.ClassIds,
            loopProfile: LoopProfile);
    }

    public void Validate(Playlist playlist, PlaylistVersion version)
    {
        Equal(nameof(playlist.Key), PlaylistKey, playlist.Key);
        Equal(nameof(version.PlaylistId), playlist.Id, version.PlaylistId);
        Equal(nameof(version.Version), PlaylistVersion, version.Version);
        Equal(nameof(version.GameModeId), Match.Rules.GameMode.ModeId, version.GameModeId);
        Equal(nameof(version.RulesetId), Match.Rules.RulesetId, version.RulesetId);
        Equal(nameof(version.MatchFormatId), Match.Format.FormatId, version.MatchFormatId);
        Equal(nameof(version.MapPoolId), Match.Map.Id, version.MapPoolId);
        Equal(nameof(version.SeriesPolicyId), SeriesPolicyId, version.SeriesPolicyId);
        Equal(nameof(version.MatchmakingPolicyId), MatchmakingPolicyId, version.MatchmakingPolicyId);
        Equal(nameof(version.AdmissionPolicyId), AdmissionPolicyId, version.AdmissionPolicyId);
        Equal(nameof(version.ExecutionPolicyId), ExecutionPolicyId, version.ExecutionPolicyId);
        Equal(nameof(version.ExecutionEngineVersion), ExecutionEngineVersion, version.ExecutionEngineVersion);
        Equal(nameof(version.DefinitionFingerprint), DefinitionFingerprint, version.DefinitionFingerprint);
        Equal(nameof(version.Visibility), Visibility, version.Visibility);
        JsonEqual(nameof(version.CanonicalDefinition), CanonicalDefinition, version.CanonicalDefinition);
        JsonEqual(nameof(version.Provenance), Provenance, version.Provenance);
    }

    private static void Equal<T>(string field, T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Arc Relay entrant playlist {field} contradicts its immutable definition.");
    }

    private static void JsonEqual(string field, string expected, string actual)
    {
        using JsonDocument a = JsonDocument.Parse(expected);
        using JsonDocument b = JsonDocument.Parse(actual);
        if (!JsonElement.DeepEquals(a.RootElement, b.RootElement))
            throw new InvalidOperationException($"Arc Relay entrant playlist {field} contradicts its immutable definition.");
    }

    private static string Sha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string WriteJson(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            write(writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
