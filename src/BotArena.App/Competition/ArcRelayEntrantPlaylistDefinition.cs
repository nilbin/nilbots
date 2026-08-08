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
    public const int Version = 6;
    public const int PreviousVersion = 5;
    public const int ForwardVersion = 4;
    public const int CounterflowVersion = 3;
    public const int HistoricalVersion = 2;
    public const string SeriesPolicyId = "single-match-v1";
    public const string MatchmakingPolicyId = "passive-elo-proximity-v1";
    public const string Visibility = PlaylistVisibilityIds.Public;
    public const string TacticalArtifactHash =
        "42288a911821f7893f34fb096cd8c17464f24da7a170761898f10e8b0497763b";

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
        ArcRelayLoopProfile.Current,
        source: "arc-relay-tactical-sheet-editor-pass",
        canonicalGameplayUnchangedFromVersion1: false);

    public static ArcRelayEntrantPlaylistDefinition CreateHistoricalV5() => Create(
        PreviousVersion,
        ArcRelayLoopProfile.Current,
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
                    PreviousVersion => ArcRelayPlaylistDefinition.ForwardStockArtifactHash,
                    ForwardVersion => ArcRelayPlaylistDefinition
                        .HistoricalForwardStockArtifactHash,
                    _ => ArcRelayPlaylistDefinition.StockArtifactHash,
                });
            writer.WriteString("sheetSchema", version == Version
                ? "arc-relay-tactical-playbook-v1"
                : ArcRelayLegacySnapshotCodec.SchemaId);
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
                writer.WriteBoolean("ownerApprovedForwardCombat", true);
                writer.WriteBoolean("tacticalPlaybookCompilerShared", true);
                writer.WriteBoolean("portableTwoFileSheets", true);
                writer.WriteNumber("ratingContinuityFromVersion", PreviousVersion);
            }
            else if (version == PreviousVersion)
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
