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
    public const int Version = 28;
    public const int Tapline10Version = 27;
    public const int Tapline9Version = 26;
    public const int Tapline8Version = 25;
    public const int Tapline6Version = 24;
    public const int Tapline5Version = 23;
    public const int Tapline4Version = 22;
    public const int Tapline3Version = 21;
    public const int OrdersV2Version = 20;
    public const int TaplineV1Version = 19;
    public const int ClaimableCoreVersion = 18;
    public const int LooseCoreMemoryVersion = 17;
    public const int StableCarrierVersion = 16;
    public const int TrafficDetourVersion = 15;
    public const int CarrierClearanceVersion = 14;
    public const int StickyRouteVersion = 13;
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
        "d63ffb4829a6875573232d3cb8822beb8c77c96708bc2e9bbdd39d0aeb42ca96";
    public const string TacticalBuildInputHash =
        "1229c727f38e78599dbc228e2f8a86117d64a50fced3d155c7398dcf2a02a6f3";
    public const string OrdersTacticalArtifactHash =
        "8feed1cd9a43e9a6ad8266d8ef2e6760c39386a080a974684d634082bb5cf969";
    public const string OrdersTacticalBuildInputHash =
        "cd4615c6712c87d9830b459bafc1835c9b9004563501969df308c056b66dc8eb";
    public const string Tapline4TacticalArtifactHash =
        "4f2fbab6bce9565d936a22dd0d80a1533fda0d438835e0a5030496cf2445992d";
    public const string Tapline4TacticalBuildInputHash =
        "2b1f32c1dc1459923507d38ffe3993802c1c1dfe7c4e44c2bbf945254966e5e4";
    public const string Tapline11TacticalArtifactHash =
        "48154fbab28f48d58435f9251efe16ed992f1bcf60564603ffd6555fde665a2b";
    public const string Tapline11TacticalBuildInputHash =
        "b7b1d783612973f51f93b7c8b43e3ae8152eb70f0bc6caf4c2b42d024d2a66e2";
    public const string OrdersV2TacticalArtifactHash =
        "d9281f6abee1f5f5e3eeebbdc911dac091138199dea91feb7a8a0812d1549b88";
    public const string OrdersV2TacticalBuildInputHash =
        "76917d1516cce340d90cb29433b0163a5028b6f53d00e36c35feb9fceb856d81";
    public const string LooseCoreMemoryTacticalArtifactHash =
        "68182fd208eee987898b42069d97c300fd41aec76c90f42f7c58001c40c10348";
    public const string LooseCoreMemoryTacticalBuildInputHash =
        "5fee8282e71be3ece6f50f01c1095e363560e06d23d64a6e2ec80ddaf009c86c";
    public const string StableCarrierTacticalArtifactHash =
        "2554509e0af31b18b6ccbaf94efe5b7e6d0ce4b8ec50e4bdd4b47b1a97039f07";
    public const string StableCarrierTacticalBuildInputHash =
        "e18085e2fbcdcf6d37eb4bf44c71a2dbeb54a2c04f232345ae5069179012ce62";
    public const string TrafficDetourTacticalArtifactHash =
        "4443b18bf718114db764a440604c7987751a7ad1c28fbe092862b4d4a7fa8af5";
    public const string TrafficDetourTacticalBuildInputHash =
        "cf7edee8cf6c3239c0b3989ac20eea30bbddd8acf806ba46a032340e307d2e77";
    public const string CarrierClearanceTacticalArtifactHash =
        "08205cfdb2da34cbd894a51be89c4b638e6cc40ca16020741602d4739fb2344a";
    public const string CarrierClearanceTacticalBuildInputHash =
        "9b51321b31b38f3daab2e0f441916b5629635a5725fa31d5ee63660db25bbcb7";
    public const string StickyRouteTacticalArtifactHash =
        "0db80102a2dbc902d2bc91dae434c729a39aa0788f8b691bc0ef3d11f5507e85";
    public const string StickyRouteTacticalBuildInputHash =
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
    public const string TaplineStockPlaybookSourceHash =
        "c161502325758601b68b43c7a5e270bf2a8415b7259d26e7f66fd77641f78b48";
    public const string TaplineStockLayoutSourceHash =
        "45cbd126f22631f7a530620fd7c1edf1bd0a8e5da0dd43f98b5b13df226958b8";
    public const string OrdersStockSheetId = "wellwright-tapline-v2";
    public const string OrdersStockPlaybookSourceHash =
        "3ffa04e0ae7342ab4d961483dbb08009540ab4647b8002314ff98089bf0a2472";
    public const string OrdersV2StockPlaybookSourceHash =
        "37a85465c8b6d1f7fddf46766d1447731e32107fa6d88f0411eda6ab6543f42d";
    public const string OrdersStockMapPlanSourceHash =
        "52ffb6ba64fd0f1b93ee5011d81b28ff461a5c86e2a88493d69b647090e42867";
    public const string Tapline4StockSheetId = "tempered-standard-v1";
    public const string Tapline4StockPlaybookSourceHash =
        "74e55e520e110646407a057f493192b5c0dc4dc668c98f2452fdf96fcd269a7a";
    public const string Tapline4StockMapPlanSourceHash =
        "09f6ecf3bc0f5e3b358333ab0ec91d0801d9e68d200890bdb0bf8497b48168ab";
    public const string Tapline6StockMapPlanSourceHash =
        "ec769b71ef43be9856e8a9a0632f2b196f321aba1da84f29806df0dbdceb5809";
    public const string Tapline9StockMapPlanSourceHash =
        "07e7951b4aabb5c596badbfb48e60edacf337c0a2172c5928ac8c4f7bde278ba";

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
    public bool CanExecutePendingMatches => PlaylistVersion == Version;
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
        ArcRelayLoopProfile.Tapline11,
        source: "arc-relay-tapline-11-explicit-movement-rates",
        canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV27() =>
        Create(
            Tapline10Version,
            ArcRelayLoopProfile.Tapline10,
            source: "arc-relay-tapline-10-compact-combat-envelope",
            canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV26() =>
        Create(
            Tapline9Version,
            ArcRelayLoopProfile.Tapline9,
            source: "arc-relay-tapline-9-sealed-entrances-staggered-mid-cover",
            canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV25() => Create(
        Tapline8Version,
        ArcRelayLoopProfile.Tapline8,
        source: "arc-relay-tapline-8-source-local-supply",
        canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV24() =>
        Create(
            Tapline6Version,
            ArcRelayLoopProfile.Tapline6,
            source: "arc-relay-tapline-6-equal-return",
            canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV23() =>
        Create(
            Tapline5Version,
            ArcRelayLoopProfile.Tapline5,
            source: "arc-relay-tapline-5-delayed-centre",
            canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV22() =>
        Create(
            Tapline4Version,
            ArcRelayLoopProfile.Tapline4,
            source: "arc-relay-tapline-4-depth-game",
            canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV21() =>
        Create(
            Tapline3Version,
            ArcRelayLoopProfile.Tapline3,
            source: "arc-relay-tapline-3-coordinated-planner",
            canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV20() =>
        Create(
            OrdersV2Version,
            ArcRelayLoopProfile.Tapline1,
            source: "arc-relay-orders-v2",
            canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV19() =>
        Create(
            TaplineV1Version,
            ArcRelayLoopProfile.Tapline1,
            source: "arc-relay-tapline-sheet-editor",
            canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV18() =>
        Create(
            ClaimableCoreVersion,
            ArcRelayLoopProfile.AmbushWarren11,
            source: "arc-relay-claimable-core-recovery",
            canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV17() =>
        Create(
            LooseCoreMemoryVersion,
            ArcRelayLoopProfile.AmbushWarren11,
            source: "arc-relay-loose-core-memory",
            canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV16() =>
        Create(
            StableCarrierVersion,
            ArcRelayLoopProfile.AmbushWarren11,
            source: "arc-relay-stable-carrier-launch-clearance",
            canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV15() =>
        Create(
            TrafficDetourVersion,
            ArcRelayLoopProfile.AmbushWarren11,
            source: "arc-relay-traffic-detours-corner-strict",
            canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV14() =>
        Create(
            CarrierClearanceVersion,
            ArcRelayLoopProfile.AmbushWarren11,
            source: "arc-relay-carrier-traffic-clearance",
            canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV13() =>
        Create(
            StickyRouteVersion,
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
                    Version => Tapline11TacticalArtifactHash,
                    Tapline10Version or Tapline9Version or Tapline8Version or Tapline6Version or Tapline5Version
                        or Tapline4Version =>
                        Tapline4TacticalArtifactHash,
                    Tapline3Version => OrdersTacticalArtifactHash,
                    OrdersV2Version => OrdersV2TacticalArtifactHash,
                    TaplineV1Version or ClaimableCoreVersion =>
                        TacticalArtifactHash,
                    LooseCoreMemoryVersion =>
                        LooseCoreMemoryTacticalArtifactHash,
                    StableCarrierVersion =>
                        StableCarrierTacticalArtifactHash,
                    TrafficDetourVersion =>
                        TrafficDetourTacticalArtifactHash,
                    CarrierClearanceVersion =>
                        CarrierClearanceTacticalArtifactHash,
                    StickyRouteVersion => StickyRouteTacticalArtifactHash,
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
            writer.WriteString("sheetSchema", version is Version or Tapline10Version or Tapline9Version or Tapline8Version or Tapline6Version or Tapline5Version or Tapline4Version or Tapline3Version or OrdersV2Version
                ? "arc-relay-orders-v2"
                : version >= TacticalVersion
                    ? "arc-relay-tactical-playbook-v1"
                    : ArcRelayLegacySnapshotCodec.SchemaId);
            if (version is Version or Tapline10Version or Tapline9Version or Tapline8Version or Tapline6Version or Tapline5Version or Tapline4Version or Tapline3Version or OrdersV2Version or TaplineV1Version or ClaimableCoreVersion
                or LooseCoreMemoryVersion
                or StableCarrierVersion
                or TrafficDetourVersion
                or CarrierClearanceVersion
                or StickyRouteVersion or RelativeWellVersion
                or SemanticIntentVersion
                or DirectLadderVersion
                or WasmRepairVersion or AmbushVersion)
            {
                writer.WriteString(
                    "stockSheetId",
                    version is Version or Tapline10Version or Tapline9Version or Tapline8Version or Tapline6Version or Tapline5Version or Tapline4Version
                        ? Tapline4StockSheetId
                        : version is Tapline3Version or OrdersV2Version
                        ? OrdersStockSheetId
                        : StockSheetId);
                writer.WriteString(
                    "stockPlaybookSourceHash",
                    version is Version or Tapline10Version or Tapline9Version or Tapline8Version or Tapline6Version or Tapline5Version or Tapline4Version
                        ? Tapline4StockPlaybookSourceHash
                        : version == Tapline3Version
                            ? OrdersStockPlaybookSourceHash
                        : version == OrdersV2Version
                            ? OrdersV2StockPlaybookSourceHash
                        : version == TaplineV1Version
                            ? TaplineStockPlaybookSourceHash
                        : version is ClaimableCoreVersion or LooseCoreMemoryVersion
                        or StableCarrierVersion
                        or TrafficDetourVersion
                        or CarrierClearanceVersion
                        or StickyRouteVersion
                        or RelativeWellVersion
                        or SemanticIntentVersion
                        or DirectLadderVersion
                        ? StockPlaybookSourceHash
                        : HistoricalStockPlaybookSourceHash);
                writer.WriteString(
                    version is Version or Tapline10Version or Tapline9Version or Tapline8Version or Tapline6Version or Tapline5Version or Tapline4Version or Tapline3Version or OrdersV2Version
                        ? "stockMapPlanSourceHash"
                        : "stockLayoutSourceHash",
                    version is Version or Tapline10Version or Tapline9Version
                        ? Tapline9StockMapPlanSourceHash
                        : version is Tapline8Version or Tapline6Version
                        ? Tapline6StockMapPlanSourceHash
                        : version is Tapline5Version or Tapline4Version
                        ? Tapline4StockMapPlanSourceHash
                        : version is Tapline3Version or OrdersV2Version
                        ? OrdersStockMapPlanSourceHash
                        : version == TaplineV1Version
                            ? TaplineStockLayoutSourceHash
                        : StockLayoutSourceHash);
            }
            if (version is Version or Tapline10Version or Tapline9Version or Tapline8Version or Tapline6Version or Tapline5Version or Tapline4Version or Tapline3Version or OrdersV2Version or TaplineV1Version or ClaimableCoreVersion
                or LooseCoreMemoryVersion
                or StableCarrierVersion
                or TrafficDetourVersion
                or CarrierClearanceVersion
                or StickyRouteVersion or RelativeWellVersion
                or SemanticIntentVersion
                or DirectLadderVersion
                or WasmRepairVersion)
            {
                writer.WriteString(
                    "stockBuildInputHash",
                    version switch
                    {
                        Version => Tapline11TacticalBuildInputHash,
                        Tapline10Version or Tapline9Version or Tapline8Version or Tapline6Version or Tapline5Version
                            or Tapline4Version =>
                            Tapline4TacticalBuildInputHash,
                        Tapline3Version => OrdersTacticalBuildInputHash,
                        OrdersV2Version => OrdersV2TacticalBuildInputHash,
                        TaplineV1Version or ClaimableCoreVersion =>
                            TacticalBuildInputHash,
                        LooseCoreMemoryVersion =>
                            LooseCoreMemoryTacticalBuildInputHash,
                        StableCarrierVersion =>
                            StableCarrierTacticalBuildInputHash,
                        TrafficDetourVersion =>
                            TrafficDetourTacticalBuildInputHash,
                        CarrierClearanceVersion =>
                            CarrierClearanceTacticalBuildInputHash,
                        StickyRouteVersion =>
                            StickyRouteTacticalBuildInputHash,
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
                writer.WriteBoolean("ownerApprovedTapline11", true);
                writer.WriteNumber("baseVisionRange", 5);
                writer.WriteNumber("shortAttackRange", 3);
                writer.WriteNumber("mediumAttackRange", 4);
                writer.WriteNumber("longAttackRange", 6);
                writer.WriteBoolean("unifiedCombatStrafeMovement", true);
                writer.WriteNumber("swiftMoveOpportunitiesPerCycle", 1);
                writer.WriteNumber("swiftMoveCycleTicks", 1);
                writer.WriteNumber("standardMoveOpportunitiesPerCycle", 4);
                writer.WriteNumber("standardMoveCycleTicks", 5);
                writer.WriteNumber("deliberateMoveOpportunitiesPerCycle", 2);
                writer.WriteNumber("deliberateMoveCycleTicks", 3);
                writer.WriteBoolean("stableUnitMovementPhase", true);
                writer.WriteBoolean("movementOpportunitiesDoNotBank", true);
                writer.WriteBoolean("sealedOuterWellEntrancesStaggeredMidCover", true);
                writer.WriteBoolean("sourceLocalWellSupply", true);
                writer.WriteBoolean("bankTriggeredWellRecharge", true);
                writer.WriteBoolean("noTellArcToss", true);
                writer.WriteBoolean("equalReturnFork", true);
                writer.WriteBoolean("delayedCentreOpening", true);
                writer.WriteBoolean("taplineNativeMapPlansOnly", true);
                writer.WriteBoolean("frozenTemperedStandardStock", true);
                writer.WriteBoolean("tapline11TacticalArtifact", true);
                writer.WriteBoolean("ordersV2DirectExecutor", true);
                writer.WriteBoolean("exactRoleClasses", true);
                writer.WriteBoolean("nestedPriorityOrders", true);
                writer.WriteBoolean("sharedExactOrders", true);
                writer.WriteBoolean("namedConditions", true);
                writer.WriteBoolean("targetScopedCapacity", true);
                writer.WriteBoolean("explicitCoreActions", true);
                writer.WriteBoolean("deterministicDropReclaim", true);
                writer.WriteBoolean("authoredMovementUrgency", true);
                writer.WriteBoolean("bodyLocalFightContact", true);
                writer.WriteBoolean("atomicCoordinatedMovement", true);
                writer.WriteBoolean("vacatedTileFollowing", true);
                writer.WriteBoolean("actionMaskAwarePlanning", true);
                writer.WriteBoolean("teamRelativeCoreCharge", true);
                writer.WriteBoolean("veteranCommand", true);
                writer.WriteBoolean("publicRespawnWaves", true);
                writer.WriteBoolean("minimumSevenDistinctClasses", true);
                writer.WriteBoolean("durableDeployments", true);
                writer.WriteBoolean("combatMagnitudeScaleTen", true);
                writer.WriteBoolean("wasmFuelProfiled", true);
                writer.WriteBoolean("inProcessWasmGameplayParity", true);
                writer.WriteBoolean("priorBehaviorGuardsRetained", true);
                writer.WriteBoolean("freshRatingEra", true);
                writer.WriteBoolean("priorStockGameplayDigestChanged", true);
                writer.WriteString("linkedDataFormat", "ATL2");
            }
            else if (version == Tapline10Version)
            {
                // Preserve v27's immutable provenance bytes exactly.
                writer.WriteBoolean("ownerApprovedTapline10", true);
                writer.WriteNumber("baseVisionRange", 5);
                writer.WriteNumber("shortAttackRange", 3);
                writer.WriteNumber("mediumAttackRange", 4);
                writer.WriteNumber("longAttackRange", 6);
                writer.WriteBoolean("sealedOuterWellEntrancesStaggeredMidCover", true);
                writer.WriteBoolean("sourceLocalWellSupply", true);
                writer.WriteBoolean("bankTriggeredWellRecharge", true);
                writer.WriteBoolean("noTellArcToss", true);
                writer.WriteBoolean("equalReturnFork", true);
                writer.WriteBoolean("delayedCentreOpening", true);
                writer.WriteBoolean("taplineNativeMapPlansOnly", true);
                writer.WriteBoolean("frozenTemperedStandardStock", true);
                writer.WriteBoolean("stockArtifactReusedFromTapline4", true);
                writer.WriteBoolean("ordersV2DirectExecutor", true);
                writer.WriteBoolean("exactRoleClasses", true);
                writer.WriteBoolean("nestedPriorityOrders", true);
                writer.WriteBoolean("sharedExactOrders", true);
                writer.WriteBoolean("namedConditions", true);
                writer.WriteBoolean("targetScopedCapacity", true);
                writer.WriteBoolean("explicitCoreActions", true);
                writer.WriteBoolean("deterministicDropReclaim", true);
                writer.WriteBoolean("authoredMovementUrgency", true);
                writer.WriteBoolean("bodyLocalFightContact", true);
                writer.WriteBoolean("atomicCoordinatedMovement", true);
                writer.WriteBoolean("vacatedTileFollowing", true);
                writer.WriteBoolean("actionMaskAwarePlanning", true);
                writer.WriteBoolean("teamRelativeCoreCharge", true);
                writer.WriteBoolean("veteranCommand", true);
                writer.WriteBoolean("publicRespawnWaves", true);
                writer.WriteBoolean("minimumSevenDistinctClasses", true);
                writer.WriteBoolean("durableDeployments", true);
                writer.WriteBoolean("combatMagnitudeScaleTen", true);
                writer.WriteBoolean("wasmFuelProfiled", true);
                writer.WriteBoolean("inProcessWasmGameplayParity", true);
                writer.WriteBoolean("priorBehaviorGuardsRetained", true);
                writer.WriteBoolean("freshRatingEra", true);
                writer.WriteBoolean("priorStockGameplayDigestChanged", true);
                writer.WriteString("linkedDataFormat", "ATL2");
            }
            else if (version == Tapline9Version)
            {
                // Preserve v26's immutable provenance bytes exactly.
                writer.WriteBoolean("ownerApprovedTapline9", true);
                writer.WriteBoolean("sealedOuterWellEntrancesStaggeredMidCover", true);
                writer.WriteBoolean("newImmutableMapIdentity", true);
                writer.WriteBoolean("sourceLocalWellSupply", true);
                writer.WriteBoolean("bankTriggeredWellRecharge", true);
                writer.WriteBoolean("noTellArcToss", true);
                writer.WriteBoolean("equalReturnFork", true);
                writer.WriteBoolean("delayedCentreOpening", true);
                writer.WriteBoolean("taplineNativeMapPlansOnly", true);
                writer.WriteBoolean("frozenTemperedStandardStock", true);
                writer.WriteBoolean("stockArtifactReusedFromTapline4", true);
                writer.WriteBoolean("ordersV2DirectExecutor", true);
                writer.WriteBoolean("exactRoleClasses", true);
                writer.WriteBoolean("nestedPriorityOrders", true);
                writer.WriteBoolean("sharedExactOrders", true);
                writer.WriteBoolean("namedConditions", true);
                writer.WriteBoolean("targetScopedCapacity", true);
                writer.WriteBoolean("explicitCoreActions", true);
                writer.WriteBoolean("deterministicDropReclaim", true);
                writer.WriteBoolean("authoredMovementUrgency", true);
                writer.WriteBoolean("bodyLocalFightContact", true);
                writer.WriteBoolean("atomicCoordinatedMovement", true);
                writer.WriteBoolean("vacatedTileFollowing", true);
                writer.WriteBoolean("actionMaskAwarePlanning", true);
                writer.WriteBoolean("teamRelativeCoreCharge", true);
                writer.WriteBoolean("veteranCommand", true);
                writer.WriteBoolean("publicRespawnWaves", true);
                writer.WriteBoolean("minimumSevenDistinctClasses", true);
                writer.WriteBoolean("durableDeployments", true);
                writer.WriteBoolean("combatMagnitudeScaleTen", true);
                writer.WriteBoolean("wasmFuelProfiled", true);
                writer.WriteBoolean("inProcessWasmGameplayParity", true);
                writer.WriteBoolean("priorBehaviorGuardsRetained", true);
                writer.WriteBoolean("freshRatingEra", true);
                writer.WriteBoolean("priorStockGameplayDigestChanged", true);
                writer.WriteString("linkedDataFormat", "ATL2");
            }
            else if (version == Tapline8Version)
            {
                // Preserve v25's immutable provenance bytes exactly.
                writer.WriteBoolean("ownerApprovedTapline8", true);
                writer.WriteBoolean("sourceLocalWellSupply", true);
                writer.WriteBoolean("bankTriggeredWellRecharge", true);
                writer.WriteBoolean("noTellArcToss", true);
                writer.WriteBoolean("equalReturnFork", true);
                writer.WriteBoolean("delayedCentreOpening", true);
                writer.WriteBoolean("taplineNativeMapPlansOnly", true);
                writer.WriteBoolean("frozenTemperedStandardStock", true);
                writer.WriteBoolean("stockArtifactReusedFromTapline4", true);
                writer.WriteBoolean("ordersV2DirectExecutor", true);
                writer.WriteBoolean("exactRoleClasses", true);
                writer.WriteBoolean("nestedPriorityOrders", true);
                writer.WriteBoolean("sharedExactOrders", true);
                writer.WriteBoolean("namedConditions", true);
                writer.WriteBoolean("targetScopedCapacity", true);
                writer.WriteBoolean("explicitCoreActions", true);
                writer.WriteBoolean("deterministicDropReclaim", true);
                writer.WriteBoolean("authoredMovementUrgency", true);
                writer.WriteBoolean("bodyLocalFightContact", true);
                writer.WriteBoolean("atomicCoordinatedMovement", true);
                writer.WriteBoolean("vacatedTileFollowing", true);
                writer.WriteBoolean("actionMaskAwarePlanning", true);
                writer.WriteBoolean("teamRelativeCoreCharge", true);
                writer.WriteBoolean("veteranCommand", true);
                writer.WriteBoolean("publicRespawnWaves", true);
                writer.WriteBoolean("minimumSevenDistinctClasses", true);
                writer.WriteBoolean("durableDeployments", true);
                writer.WriteBoolean("combatMagnitudeScaleTen", true);
                writer.WriteBoolean("wasmFuelProfiled", true);
                writer.WriteBoolean("inProcessWasmGameplayParity", true);
                writer.WriteBoolean("priorBehaviorGuardsRetained", true);
                writer.WriteBoolean("freshRatingEra", true);
                writer.WriteBoolean("priorStockGameplayDigestChanged", true);
                writer.WriteString("linkedDataFormat", "ATL2");
            }
            else if (version == Tapline6Version)
            {
                // Preserve v24's immutable provenance bytes exactly.
                writer.WriteBoolean("ownerApprovedTapline6", true);
                writer.WriteBoolean("equalReturnFork", true);
                writer.WriteBoolean("newImmutableMapIdentity", true);
                writer.WriteBoolean("delayedCentreOpening", true);
                writer.WriteBoolean("taplineNativeMapPlansOnly", true);
                writer.WriteBoolean("frozenTemperedStandardStock", true);
                writer.WriteBoolean("stockArtifactReusedFromTapline4", true);
                writer.WriteBoolean("ordersV2DirectExecutor", true);
                writer.WriteBoolean("exactRoleClasses", true);
                writer.WriteBoolean("nestedPriorityOrders", true);
                writer.WriteBoolean("sharedExactOrders", true);
                writer.WriteBoolean("namedConditions", true);
                writer.WriteBoolean("targetScopedCapacity", true);
                writer.WriteBoolean("explicitCoreActions", true);
                writer.WriteBoolean("deterministicDropReclaim", true);
                writer.WriteBoolean("authoredMovementUrgency", true);
                writer.WriteBoolean("bodyLocalFightContact", true);
                writer.WriteBoolean("atomicCoordinatedMovement", true);
                writer.WriteBoolean("vacatedTileFollowing", true);
                writer.WriteBoolean("actionMaskAwarePlanning", true);
                writer.WriteBoolean("teamRelativeCoreCharge", true);
                writer.WriteBoolean("veteranCommand", true);
                writer.WriteBoolean("publicRespawnWaves", true);
                writer.WriteBoolean("minimumSevenDistinctClasses", true);
                writer.WriteBoolean("durableDeployments", true);
                writer.WriteBoolean("combatMagnitudeScaleTen", true);
                writer.WriteBoolean("wasmFuelProfiled", true);
                writer.WriteBoolean("inProcessWasmGameplayParity", true);
                writer.WriteBoolean("priorBehaviorGuardsRetained", true);
                writer.WriteBoolean("freshRatingEra", true);
                writer.WriteBoolean("priorStockGameplayDigestChanged", true);
                writer.WriteString("linkedDataFormat", "ATL2");
            }
            else if (version == Tapline5Version)
            {
                // Preserve v23's immutable provenance bytes exactly.
                writer.WriteBoolean("ownerApprovedTapline5", true);
                writer.WriteBoolean("delayedCentreOpening", true);
                writer.WriteBoolean("taplineNativeMapPlansOnly", true);
                writer.WriteBoolean("frozenTemperedStandardStock", true);
                writer.WriteBoolean("stockArtifactReusedFromTapline4", true);
                writer.WriteBoolean("ordersV2DirectExecutor", true);
                writer.WriteBoolean("exactRoleClasses", true);
                writer.WriteBoolean("nestedPriorityOrders", true);
                writer.WriteBoolean("sharedExactOrders", true);
                writer.WriteBoolean("namedConditions", true);
                writer.WriteBoolean("targetScopedCapacity", true);
                writer.WriteBoolean("explicitCoreActions", true);
                writer.WriteBoolean("deterministicDropReclaim", true);
                writer.WriteBoolean("authoredMovementUrgency", true);
                writer.WriteBoolean("bodyLocalFightContact", true);
                writer.WriteBoolean("atomicCoordinatedMovement", true);
                writer.WriteBoolean("vacatedTileFollowing", true);
                writer.WriteBoolean("actionMaskAwarePlanning", true);
                writer.WriteBoolean("teamRelativeCoreCharge", true);
                writer.WriteBoolean("veteranCommand", true);
                writer.WriteBoolean("publicRespawnWaves", true);
                writer.WriteBoolean("minimumSevenDistinctClasses", true);
                writer.WriteBoolean("durableDeployments", true);
                writer.WriteBoolean("combatMagnitudeScaleTen", true);
                writer.WriteBoolean("wasmFuelProfiled", true);
                writer.WriteBoolean("inProcessWasmGameplayParity", true);
                writer.WriteBoolean("priorBehaviorGuardsRetained", true);
                writer.WriteBoolean("freshRatingEra", true);
                writer.WriteBoolean("priorStockGameplayDigestChanged", true);
                writer.WriteString("linkedDataFormat", "ATL2");
            }
            else if (version == Tapline4Version)
            {
                // Preserve v22's immutable provenance bytes exactly.
                writer.WriteBoolean("ownerApprovedTapline4", true);
                writer.WriteBoolean("taplineNativeMapPlansOnly", true);
                writer.WriteBoolean("frozenTemperedStandardStock", true);
                writer.WriteBoolean("ordersV2DirectExecutor", true);
                writer.WriteBoolean("exactRoleClasses", true);
                writer.WriteBoolean("nestedPriorityOrders", true);
                writer.WriteBoolean("sharedExactOrders", true);
                writer.WriteBoolean("namedConditions", true);
                writer.WriteBoolean("targetScopedCapacity", true);
                writer.WriteBoolean("explicitCoreActions", true);
                writer.WriteBoolean("deterministicDropReclaim", true);
                writer.WriteBoolean("authoredMovementUrgency", true);
                writer.WriteBoolean("bodyLocalFightContact", true);
                writer.WriteBoolean("atomicCoordinatedMovement", true);
                writer.WriteBoolean("vacatedTileFollowing", true);
                writer.WriteBoolean("actionMaskAwarePlanning", true);
                writer.WriteBoolean("teamRelativeCoreCharge", true);
                writer.WriteBoolean("veteranCommand", true);
                writer.WriteBoolean("publicRespawnWaves", true);
                writer.WriteBoolean("minimumSevenDistinctClasses", true);
                writer.WriteBoolean("durableDeployments", true);
                writer.WriteBoolean("combatMagnitudeScaleTen", true);
                writer.WriteBoolean("wasmFuelProfiled", true);
                writer.WriteBoolean("inProcessWasmGameplayParity", true);
                writer.WriteBoolean("priorBehaviorGuardsRetained", true);
                writer.WriteBoolean("freshRatingEra", true);
                writer.WriteBoolean("priorStockGameplayDigestChanged", true);
                writer.WriteString("linkedDataFormat", "ATL2");
            }
            else if (version == Tapline3Version)
            {
                // Preserve v21's immutable provenance bytes exactly.
                writer.WriteBoolean("ownerApprovedTapline3", true);
                writer.WriteBoolean("taplineNativeMapPlansOnly", true);
                writer.WriteBoolean("frozenWellwrightStock", true);
                writer.WriteBoolean("ordersV2DirectExecutor", true);
                writer.WriteBoolean("exactRoleClasses", true);
                writer.WriteBoolean("nestedPriorityOrders", true);
                writer.WriteBoolean("sharedExactOrders", true);
                writer.WriteBoolean("namedConditions", true);
                writer.WriteBoolean("targetScopedCapacity", true);
                writer.WriteBoolean("explicitCoreActions", true);
                writer.WriteBoolean("deterministicDropReclaim", true);
                writer.WriteBoolean("authoredMovementUrgency", true);
                writer.WriteBoolean("bodyLocalFightContact", true);
                writer.WriteBoolean("atomicCoordinatedMovement", true);
                writer.WriteBoolean("vacatedTileFollowing", true);
                writer.WriteBoolean("actionMaskAwarePlanning", true);
                writer.WriteBoolean("wasmFuelProfiled", true);
                writer.WriteBoolean("inProcessWasmGameplayParity", true);
                writer.WriteBoolean("priorBehaviorGuardsRetained", true);
                writer.WriteBoolean("freshRatingEra", true);
                writer.WriteBoolean("priorStockGameplayDigestChanged", true);
                writer.WriteString("linkedDataFormat", "ATL2");
            }
            else if (version == OrdersV2Version)
            {
                // Preserve v20's immutable provenance bytes exactly.
                writer.WriteBoolean("ownerApprovedTapline1", true);
                writer.WriteBoolean("taplineNativeMapPlansOnly", true);
                writer.WriteBoolean("frozenWellwrightStock", true);
                writer.WriteBoolean("ordersV2DirectExecutor", true);
                writer.WriteBoolean("exactRoleClasses", true);
                writer.WriteBoolean("nestedPriorityOrders", true);
                writer.WriteBoolean("sharedExactOrders", true);
                writer.WriteBoolean("namedConditions", true);
                writer.WriteBoolean("targetScopedCapacity", true);
                writer.WriteBoolean("explicitCoreActions", true);
                writer.WriteBoolean("deterministicDropReclaim", true);
                writer.WriteBoolean("inProcessWasmGameplayParity", true);
                writer.WriteBoolean("priorBehaviorGuardsRetained", true);
                writer.WriteBoolean("freshRatingEra", true);
                writer.WriteBoolean("priorStockGameplayDigestChanged", true);
                writer.WriteString("linkedDataFormat", "ATL2");
            }
            else if (version == TaplineV1Version)
            {
                // Preserve v19's immutable provenance bytes exactly.
                writer.WriteBoolean("ownerApprovedTapline1", true);
                writer.WriteBoolean("taplineNativeLayoutsOnly", true);
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
                writer.WriteBoolean("terrainOnlyGiveUpReachability", true);
                writer.WriteBoolean("boundedCustodyFallbackRetest", true);
                writer.WriteBoolean("cascadingCarrierClearance", true);
                writer.WriteBoolean("boundedTrafficDetours", true);
                writer.WriteBoolean("stickyRouteDetourRejoin", true);
                writer.WriteBoolean("authoritativeCornerStrictMovement", true);
                writer.WriteBoolean("stableCarrierDeliveryCourses", true);
                writer.WriteBoolean("singleTickCustodyPlan", true);
                writer.WriteBoolean("deterministicCarrierPrecedence", true);
                writer.WriteBoolean("boundedFreshLifeReactorAvoidance", true);
                writer.WriteBoolean("boundedReactorExitClearance", true);
                writer.WriteBoolean("boundedLooseCoreMemory", true);
                writer.WriteBoolean("visibleAbsenceClearsLooseCoreMemory", true);
                writer.WriteBoolean("authoredPickupSelection", true);
                writer.WriteBoolean("nearestDropRecoveryDistanceFirst", true);
                writer.WriteBoolean("uncommittedDropsRemainClaimable", true);
                writer.WriteBoolean("currentCarriersCannotReservePickups", true);
                writer.WriteBoolean("freshRatingEra", true);
                writer.WriteBoolean("mapLineChanged", true);
                writer.WriteString("linkedDataFormat", "ATL1");
            }
            else if (version == ClaimableCoreVersion)
            {
                // Preserve v18's immutable provenance bytes exactly.
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
                writer.WriteBoolean("terrainOnlyGiveUpReachability", true);
                writer.WriteBoolean("boundedCustodyFallbackRetest", true);
                writer.WriteBoolean("cascadingCarrierClearance", true);
                writer.WriteBoolean("boundedTrafficDetours", true);
                writer.WriteBoolean("stickyRouteDetourRejoin", true);
                writer.WriteBoolean("authoritativeCornerStrictMovement", true);
                writer.WriteBoolean("stableCarrierDeliveryCourses", true);
                writer.WriteBoolean("singleTickCustodyPlan", true);
                writer.WriteBoolean("deterministicCarrierPrecedence", true);
                writer.WriteBoolean("boundedFreshLifeReactorAvoidance", true);
                writer.WriteBoolean("boundedReactorExitClearance", true);
                writer.WriteBoolean("boundedLooseCoreMemory", true);
                writer.WriteBoolean("visibleAbsenceClearsLooseCoreMemory", true);
                writer.WriteBoolean("authoredPickupSelection", true);
                writer.WriteBoolean("nearestDropRecoveryDistanceFirst", true);
                writer.WriteBoolean("uncommittedDropsRemainClaimable", true);
                writer.WriteBoolean("currentCarriersCannotReservePickups", true);
                writer.WriteBoolean("freshRatingEra", true);
                writer.WriteBoolean("priorStockGameplayDigestChanged", true);
                writer.WriteString("linkedDataFormat", "ATL1");
            }
            else if (version == LooseCoreMemoryVersion)
            {
                // Preserve v17's immutable provenance bytes exactly.
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
                writer.WriteBoolean("terrainOnlyGiveUpReachability", true);
                writer.WriteBoolean("boundedCustodyFallbackRetest", true);
                writer.WriteBoolean("cascadingCarrierClearance", true);
                writer.WriteBoolean("boundedTrafficDetours", true);
                writer.WriteBoolean("stickyRouteDetourRejoin", true);
                writer.WriteBoolean("authoritativeCornerStrictMovement", true);
                writer.WriteBoolean("stableCarrierDeliveryCourses", true);
                writer.WriteBoolean("singleTickCustodyPlan", true);
                writer.WriteBoolean("deterministicCarrierPrecedence", true);
                writer.WriteBoolean("boundedFreshLifeReactorAvoidance", true);
                writer.WriteBoolean("boundedReactorExitClearance", true);
                writer.WriteBoolean("boundedLooseCoreMemory", true);
                writer.WriteBoolean("visibleAbsenceClearsLooseCoreMemory", true);
                writer.WriteBoolean("authoredPickupSelection", true);
                writer.WriteBoolean("nearestDropRecoveryDistanceFirst", true);
                writer.WriteBoolean("freshRatingEra", true);
                writer.WriteBoolean("priorStockGameplayDigestChanged", true);
                writer.WriteString("linkedDataFormat", "ATL1");
            }
            else if (version == StableCarrierVersion)
            {
                // Preserve v16's immutable provenance bytes exactly.
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
                writer.WriteBoolean("terrainOnlyGiveUpReachability", true);
                writer.WriteBoolean("boundedCustodyFallbackRetest", true);
                writer.WriteBoolean("cascadingCarrierClearance", true);
                writer.WriteBoolean("boundedTrafficDetours", true);
                writer.WriteBoolean("stickyRouteDetourRejoin", true);
                writer.WriteBoolean("authoritativeCornerStrictMovement", true);
                writer.WriteBoolean("stableCarrierDeliveryCourses", true);
                writer.WriteBoolean("singleTickCustodyPlan", true);
                writer.WriteBoolean("deterministicCarrierPrecedence", true);
                writer.WriteBoolean("boundedFreshLifeReactorAvoidance", true);
                writer.WriteBoolean("boundedReactorExitClearance", true);
                writer.WriteBoolean("freshRatingEra", true);
                writer.WriteBoolean("priorStockGameplayDigestChanged", true);
                writer.WriteString("linkedDataFormat", "ATL1");
            }
            else if (version == TrafficDetourVersion)
            {
                // Preserve v15's immutable provenance bytes exactly.
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
                writer.WriteBoolean("terrainOnlyGiveUpReachability", true);
                writer.WriteBoolean("boundedCustodyFallbackRetest", true);
                writer.WriteBoolean("cascadingCarrierClearance", true);
                writer.WriteBoolean("boundedTrafficDetours", true);
                writer.WriteBoolean("stickyRouteDetourRejoin", true);
                writer.WriteBoolean("authoritativeCornerStrictMovement", true);
                writer.WriteBoolean("freshRatingEra", true);
                writer.WriteBoolean("priorStockGameplayDigestChanged", true);
                writer.WriteString("linkedDataFormat", "ATL1");
            }
            else if (version == CarrierClearanceVersion)
            {
                // Preserve v14's immutable provenance bytes exactly.
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
                writer.WriteBoolean("terrainOnlyGiveUpReachability", true);
                writer.WriteBoolean("boundedCustodyFallbackRetest", true);
                writer.WriteBoolean("cascadingCarrierClearance", true);
                writer.WriteBoolean("freshRatingEra", true);
                writer.WriteBoolean("priorStockGameplayDigestChanged", true);
                writer.WriteString("linkedDataFormat", "ATL1");
            }
            else if (version == StickyRouteVersion)
            {
                // Preserve v13's immutable provenance bytes exactly.
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
