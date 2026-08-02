using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotArena.App.ArcRelay;
using BotArena.Engine;

namespace BotArena.App.Competition;

/// <summary>
/// Immutable Arc Relay entrant lane. It carries the same rules and map as v1,
/// while admission selects a trusted stock mind for sheets and sandboxed WASM
/// for custom minds on a per-participant basis.
/// </summary>
public sealed class ArcRelayEntrantPlaylistDefinition : IHostedGenericMatchDefinition
{
    public const string PlaylistKey = ArcRelayPlaylistDefinition.PlaylistKey;
    public const string DisplayName = ArcRelayPlaylistDefinition.DisplayName;
    public const int Version = 2;
    public const string SeriesPolicyId = "single-match-v1";
    public const string MatchmakingPolicyId = "passive-elo-proximity-v1";
    public const string Visibility = PlaylistVisibilityIds.Public;

    private ArcRelayEntrantPlaylistDefinition(
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
    public HostedGenericRuntimeModel RuntimeModel =>
        HostedGenericRuntimeModel.ArcRelayEntrants;
    public double? PresentationTicksPerSecond => 1.25;
    string IHostedGenericMatchDefinition.PlaylistKey => PlaylistKey;
    int IHostedGenericMatchDefinition.Version => Version;

    public static ArcRelayEntrantPlaylistDefinition Create()
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
            writer.WriteString("runtimeModel", "arc-relay-entrants-v1");
            writer.WriteString("stockArtifactHash", ArcRelayPlaylistDefinition.StockArtifactHash);
            writer.WriteString("sheetSchema", ArcRelayPlayerSheetCodec.SchemaId);
            writer.WriteString("compositionSchema", "arc-relay-composition-v1");
            writer.WriteNumber("compositionSlots", ArcRelayPlayerSheetCodec.SlotCount);
            writer.WriteNumber("maximumCopiesPerClass", ArcRelayPlayerSheetCodec.MaximumCopiesPerClass);
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
            writer.WriteString("source", "arc-relay-entrant-ladder-pass");
            writer.WriteBoolean("ranked", true);
            writer.WriteBoolean("sheetTrustedStockOnly", true);
            writer.WriteBoolean("customMindSandboxOnly", true);
            writer.WriteBoolean("canonicalGameplayUnchangedFromVersion1", true);
            writer.WriteEndObject();
        });
        return new ArcRelayEntrantPlaylistDefinition(
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
