using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotArena.App.ArcRelay;
using BotArena.Engine;

namespace BotArena.App.Competition;

/// <summary>
/// Immutable hosted Arc Relay product lane: frozen stock algorithm plus two
/// separately hashed player sheets, on the provisionally recommended loop
/// geometry from Gate 3.2.
/// </summary>
public sealed class ArcRelayPlaylistDefinition : IHostedGenericMatchDefinition
{
    public const string PlaylistKey = "arc-relay";
    public const string DisplayName = "Arc Relay";
    public const int Version = 1;
    public const string SeriesPolicyId = "single-match-v1";
    public const string MatchmakingPolicyId = "sheet-scrimmage-v1";
    public const string Visibility = PlaylistVisibilityIds.Public;
    public const string StockArtifactHash =
        "c574c09a832d0a28cd1be8fd645a02685ad9c24a02543bce5c9819d5e1fd65f9";

    private ArcRelayPlaylistDefinition(
        ActorResolvedMatchDefinition representative,
        string canonicalDefinition,
        string definitionFingerprint,
        string provenance)
    {
        Match = representative;
        ReplayPresentation = ArcRelayH0ReplayPresentation.Create(representative);
        CanonicalDefinition = canonicalDefinition;
        DefinitionFingerprint = definitionFingerprint;
        Provenance = provenance;
    }

    public ActorResolvedMatchDefinition Match { get; }
    public GenericActorReplayPresentation ReplayPresentation { get; }
    public string CanonicalDefinition { get; }
    public string DefinitionFingerprint { get; }
    public string Provenance { get; }
    public string AdmissionPolicyId => BotArenaVersions.GenericMindContractProfileId;
    public string ExecutionPolicyId => PlaylistExecutionPolicyIds.GenericActor;
    public string ExecutionEngineVersion => BotArenaVersions.GenericActorEngineVersion;
    public HostedGenericRuntimeModel RuntimeModel => HostedGenericRuntimeModel.TrustedStockMind;
    public double? PresentationTicksPerSecond => 1.25;
    string IHostedGenericMatchDefinition.PlaylistKey => PlaylistKey;
    int IHostedGenericMatchDefinition.Version => Version;

    public static ArcRelayPlaylistDefinition Create()
    {
        string[] classes = ArcRelayPlayerSheetCodec.NewSheetTemplate().Slots
            .OrderBy(slot => slot.UnitId)
            .Select(slot => slot.ClassId)
            .ToArray();
        ActorResolvedMatchDefinition representative = ArcRelayH0Definition.Create(
            classes,
            classes,
            loopProfile: ArcRelayLoopProfile.HomeGatesWide);
        string canonicalDefinition = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("gameModeId", representative.Rules.GameMode.ModeId);
            writer.WriteString("rulesetId", representative.Rules.RulesetId);
            writer.WriteString("matchFormatId", representative.Format.FormatId);
            writer.WriteString("mapPoolId", representative.Map.Id);
            writer.WriteString("loopProfileId", ArcRelayLoopProfile.HomeGatesWide.Id);
            writer.WriteString("seriesPolicyId", SeriesPolicyId);
            writer.WriteString("matchmakingPolicyId", MatchmakingPolicyId);
            writer.WriteString("admissionPolicyId", BotArenaVersions.GenericMindContractProfileId);
            writer.WriteString("executionPolicyId", PlaylistExecutionPolicyIds.GenericActor);
            writer.WriteString("executionEngineVersion", BotArenaVersions.GenericActorEngineVersion);
            writer.WriteString("runtimeModel", "trusted-stock-mind-v1");
            writer.WriteString("stockArtifactHash", StockArtifactHash);
            writer.WriteString("sheetSchema", ArcRelayPlayerSheetCodec.SchemaId);
            writer.WriteNumber("sheetSlots", ArcRelayPlayerSheetCodec.SlotCount);
            writer.WriteNumber("maximumCopiesPerClass", ArcRelayPlayerSheetCodec.MaximumCopiesPerClass);
            // Viewer base cadence (2.5) × Arc Relay's approved first-watch 0.5x.
            writer.WriteNumber("presentationTicksPerSecond", 1.25);
            writer.WriteString("rulesFingerprint", ActorContractFingerprint.ComputeRules(representative.Rules));
            writer.WriteString("mapFingerprint", ActorContractFingerprint.ComputeMap(representative.Map));
            writer.WriteEndObject();
        });
        string provenance = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("source", "arc-relay-gate-3-product-pass");
            writer.WriteString("balanceStatus", "provisional-loop-recommended");
            writer.WriteBoolean("ranked", false);
            writer.WriteBoolean("stockAlgorithmBuildOnce", true);
            writer.WriteBoolean("playerSheetsSeparatelyHashed", true);
            writer.WriteEndObject();
        });
        return new ArcRelayPlaylistDefinition(
            representative,
            canonicalDefinition,
            Sha256(canonicalDefinition),
            provenance);
    }

    public ActorResolvedMatchDefinition ResolveMatch(
        IReadOnlyList<HostedGenericParticipantInput> participants)
    {
        if (participants.Count != 2)
            throw new InvalidOperationException("Arc Relay requires two sheets.");
        HostedGenericParticipantInput first = participants.Single(value => value.ParticipantId == 0 && value.TeamId == 0);
        HostedGenericParticipantInput second = participants.Single(value => value.ParticipantId == 1 && value.TeamId == 1);
        return ArcRelayH0Definition.Create(
            first.ClassIds,
            second.ClassIds,
            loopProfile: ArcRelayLoopProfile.HomeGatesWide);
    }

    public void Validate(Playlist playlist, PlaylistVersion version)
    {
        Equal(nameof(playlist.Key), PlaylistKey, playlist.Key);
        Equal(nameof(version.PlaylistId), playlist.Id, version.PlaylistId);
        Equal(nameof(version.Version), Version, version.Version);
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
            throw new InvalidOperationException($"Arc Relay playlist {field} contradicts its immutable definition.");
    }

    private static void JsonEqual(string field, string expected, string actual)
    {
        using JsonDocument a = JsonDocument.Parse(expected);
        using JsonDocument b = JsonDocument.Parse(actual);
        if (!JsonElement.DeepEquals(a.RootElement, b.RootElement))
            throw new InvalidOperationException($"Arc Relay playlist {field} contradicts its immutable definition.");
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
