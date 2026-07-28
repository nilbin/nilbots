using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BotArena.App.Competition;

/// <summary>
/// Deterministic compatibility definition for one rules-version keyed Duel
/// population. Historical details that were never persisted are deliberately
/// labelled as legacy imports rather than inferred from current configuration.
/// </summary>
public sealed class LegacyCompetitionDefinition
{
    public const string SeasonKey = "legacy-import";
    public const string SeasonDisplayName = "Legacy Import";
    public const int PlaylistVersion = 1;
    public const string GameModeId = "deathmatch";
    public const string MatchFormatId = "head-to-head";
    public const string UnknownDefinitionId = "legacy-import";
    public const string Visibility = PlaylistVisibilityIds.Legacy;

    private LegacyCompetitionDefinition(
        string rulesVersion,
        string playlistKey,
        string playlistDisplayName,
        string canonicalDefinition,
        string definitionFingerprint,
        string provenance)
    {
        RulesVersion = rulesVersion;
        PlaylistKey = playlistKey;
        PlaylistDisplayName = playlistDisplayName;
        CanonicalDefinition = canonicalDefinition;
        DefinitionFingerprint = definitionFingerprint;
        Provenance = provenance;
    }

    public string RulesVersion { get; }
    public string PlaylistKey { get; }
    public string PlaylistDisplayName { get; }
    public string CanonicalDefinition { get; }
    public string DefinitionFingerprint { get; }
    public string Provenance { get; }

    public static LegacyCompetitionDefinition Create(string rulesVersion)
    {
        ValidateRulesVersion(rulesVersion, nameof(rulesVersion));

        string rulesHash = Sha256(rulesVersion);
        string canonicalDefinition = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("gameModeId", GameModeId);
            writer.WriteString("rulesetId", rulesVersion);
            writer.WriteString("matchFormatId", MatchFormatId);
            writer.WriteString("mapPoolId", UnknownDefinitionId);
            writer.WriteString("seriesPolicyId", UnknownDefinitionId);
            writer.WriteString("matchmakingPolicyId", UnknownDefinitionId);
            writer.WriteString("admissionPolicyId", UnknownDefinitionId);
            writer.WriteEndObject();
        });
        string provenance = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("source", UnknownDefinitionId);
            writer.WriteString("legacyRulesVersion", rulesVersion);
            writer.WritePropertyName("unknownHistoricalMetadata");
            writer.WriteStartArray();
            writer.WriteStringValue("mapPool");
            writer.WriteStringValue("seriesPolicy");
            writer.WriteStringValue("matchmakingPolicy");
            writer.WriteStringValue("admissionPolicy");
            writer.WriteStringValue("season");
            writer.WriteEndArray();
            writer.WriteEndObject();
        });

        return new LegacyCompetitionDefinition(
            rulesVersion,
            $"legacy-duel-{rulesHash}",
            $"Legacy Duel {rulesVersion}",
            canonicalDefinition,
            Sha256(canonicalDefinition),
            provenance);
    }

    public static void ValidateRulesVersion(
        string rulesVersion,
        string? parameterName = null)
    {
        if (string.IsNullOrWhiteSpace(rulesVersion))
        {
            throw new ArgumentException(
                "A legacy competition rules version cannot be blank.",
                parameterName ?? nameof(rulesVersion));
        }
        if (rulesVersion.Length > 100)
        {
            throw new ArgumentException(
                "A legacy competition rules version cannot exceed 100 characters.",
                parameterName ?? nameof(rulesVersion));
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
