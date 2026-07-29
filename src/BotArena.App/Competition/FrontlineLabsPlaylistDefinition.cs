using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotArena.Engine;

namespace BotArena.App.Competition;

/// <summary>
/// Exact immutable identity for the first hosted generic Frontline arm.
/// Numeric mechanics are experimental inputs, not a balance or ship verdict.
/// </summary>
public sealed class FrontlineLabsPlaylistDefinition
    : IHostedGenericMatchDefinition
{
    public const string PlaylistKey = "frontline-labs";
    public const string DisplayName = "Frontline Labs";
    public const int LegacyVersion = 1;
    public const int Version = 2;
    public const string SeriesPolicyId = "single-match-v1";
    public const string MatchmakingPolicyId = "direct-challenge-v1";
    public const string Visibility = PlaylistVisibilityIds.Labs;

    private FrontlineLabsPlaylistDefinition(
        int version,
        ActorResolvedMatchDefinition match,
        string canonicalDefinition,
        string definitionFingerprint,
        string provenance)
    {
        HostedVersion = version;
        Match = match;
        CanonicalDefinition = canonicalDefinition;
        DefinitionFingerprint = definitionFingerprint;
        Provenance = provenance;
    }

    public ActorResolvedMatchDefinition Match { get; }
    public string CanonicalDefinition { get; }
    public string DefinitionFingerprint { get; }
    public string Provenance { get; }
    public int HostedVersion { get; }

    public string GameModeId => Match.Rules.GameMode.ModeId;
    public string RulesetId => Match.Rules.RulesetId;
    public string MatchFormatId => Match.Format.FormatId;
    public string MapPoolId => Match.Map.Id;
    public string AdmissionPolicyId =>
        Match.CapabilityVersions.ContractProfileId;
    public string ExecutionPolicyId =>
        PlaylistExecutionPolicyIds.GenericActor;
    public string ExecutionEngineVersion =>
        BotArenaVersions.GenericActorEngineVersion;
    string IHostedGenericMatchDefinition.PlaylistKey => PlaylistKey;
    int IHostedGenericMatchDefinition.Version => HostedVersion;

    public static FrontlineLabsPlaylistDefinition Create() =>
        Create(
            Version,
            FrontlineLabsDefinition.CreateProfile3());

    public static FrontlineLabsPlaylistDefinition CreateV1() =>
        Create(
            LegacyVersion,
            FrontlineLabsDefinition.Create());

    private static FrontlineLabsPlaylistDefinition Create(
        int version,
        ActorResolvedMatchDefinition match)
    {
        string resolvedMatchJson =
            ActorContractManifestSerializer.ToCanonicalJson(match);
        string resolvedMatchFingerprint =
            ActorContractFingerprint.ComputeMatch(match);
        string canonicalDefinition = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("gameModeId", match.Rules.GameMode.ModeId);
            writer.WriteString("rulesetId", match.Rules.RulesetId);
            writer.WriteString("matchFormatId", match.Format.FormatId);
            writer.WriteString("mapPoolId", match.Map.Id);
            writer.WriteString("seriesPolicyId", SeriesPolicyId);
            writer.WriteString(
                "matchmakingPolicyId",
                MatchmakingPolicyId);
            writer.WriteString(
                "admissionPolicyId",
                match.CapabilityVersions.ContractProfileId);
            writer.WriteString(
                "executionPolicyId",
                PlaylistExecutionPolicyIds.GenericActor);
            writer.WriteString(
                "executionEngineVersion",
                BotArenaVersions.GenericActorEngineVersion);
            writer.WriteString(
                "resolvedMatchFingerprint",
                resolvedMatchFingerprint);
            writer.WritePropertyName("resolvedMatch");
            using JsonDocument document =
                JsonDocument.Parse(resolvedMatchJson);
            document.RootElement.WriteTo(writer);
            writer.WriteEndObject();
        });
        string provenance = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("source", "frontline-labs");
            writer.WriteString("balanceStatus", "experimental-unvalidated");
            writer.WriteString(
                "resolvedMatchFingerprint",
                resolvedMatchFingerprint);
            writer.WriteBoolean("ranked", false);
            writer.WriteEndObject();
        });

        return new FrontlineLabsPlaylistDefinition(
            version,
            match,
            canonicalDefinition,
            Sha256(canonicalDefinition),
            provenance);
    }

    public void Validate(Playlist playlist, PlaylistVersion version)
    {
        ArgumentNullException.ThrowIfNull(playlist);
        ArgumentNullException.ThrowIfNull(version);

        Equal(nameof(playlist.Key), PlaylistKey, playlist.Key);
        Equal(nameof(version.PlaylistId), playlist.Id, version.PlaylistId);
        Equal(
            nameof(version.Version),
            HostedVersion,
            version.Version);
        Equal(nameof(version.GameModeId), GameModeId, version.GameModeId);
        Equal(nameof(version.RulesetId), RulesetId, version.RulesetId);
        Equal(
            nameof(version.MatchFormatId),
            MatchFormatId,
            version.MatchFormatId);
        Equal(nameof(version.MapPoolId), MapPoolId, version.MapPoolId);
        Equal(
            nameof(version.SeriesPolicyId),
            SeriesPolicyId,
            version.SeriesPolicyId);
        Equal(
            nameof(version.MatchmakingPolicyId),
            MatchmakingPolicyId,
            version.MatchmakingPolicyId);
        Equal(
            nameof(version.AdmissionPolicyId),
            AdmissionPolicyId,
            version.AdmissionPolicyId);
        Equal(
            nameof(version.ExecutionPolicyId),
            ExecutionPolicyId,
            version.ExecutionPolicyId);
        Equal(
            nameof(version.ExecutionEngineVersion),
            ExecutionEngineVersion,
            version.ExecutionEngineVersion);
        Equal(
            nameof(version.DefinitionFingerprint),
            DefinitionFingerprint,
            version.DefinitionFingerprint);
        Equal(
            nameof(version.Visibility),
            Visibility,
            version.Visibility);
        JsonEqual(
            nameof(version.CanonicalDefinition),
            CanonicalDefinition,
            version.CanonicalDefinition);
        JsonEqual(
            nameof(version.Provenance),
            Provenance,
            version.Provenance);
    }

    private static void Equal<T>(string field, T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"Frontline Labs playlist field {field} is '{actual}', " +
                $"expected '{expected}'. Playlist versions are immutable.");
        }
    }

    private static void JsonEqual(
        string field,
        string expected,
        string actual)
    {
        using JsonDocument expectedDocument = JsonDocument.Parse(expected);
        using JsonDocument actualDocument = JsonDocument.Parse(actual);
        if (!JsonElement.DeepEquals(
                expectedDocument.RootElement,
                actualDocument.RootElement))
        {
            throw new InvalidOperationException(
                $"Frontline Labs playlist field {field} contradicts the " +
                "frozen definition. Playlist versions are immutable.");
        }
    }

    private static string Sha256(string value) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string WriteJson(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            write(writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
