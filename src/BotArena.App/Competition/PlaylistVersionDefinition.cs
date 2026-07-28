namespace BotArena.App.Competition;

/// <summary>
/// One immutable, fully named playlist revision.
/// <para>
/// A playlist combines a game mode, ruleset, match format, map pool, series,
/// matchmaking, admission, and hosted-execution policy. A ladder may then open
/// a rating population for exactly this revision. The referenced definitions
/// are typed application catalog entries; the aggregate fingerprint pins their
/// exact resolved content.
/// </para>
/// </summary>
public sealed class PlaylistVersionDefinition
{
    public PlaylistVersionDefinition(
        PlaylistVersionId id,
        string playlistKey,
        int version,
        string displayName,
        string gameModeId,
        string rulesetId,
        string matchFormatId,
        string mapPoolId,
        string seriesPolicyId,
        string matchmakingPolicyId,
        string admissionPolicyId,
        string executionPolicyId,
        string executionEngineVersion,
        string definitionFingerprint)
    {
        if (id.IsEmpty)
            throw new ArgumentException(
                "A playlist definition requires a non-empty version id.",
                nameof(id));
        if (version <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(version),
                "A playlist version must be positive.");

        Id = id;
        PlaylistKey = Required(playlistKey, nameof(playlistKey));
        Version = version;
        DisplayName = Required(displayName, nameof(displayName));
        GameModeId = Required(gameModeId, nameof(gameModeId));
        RulesetId = Required(rulesetId, nameof(rulesetId));
        MatchFormatId = Required(matchFormatId, nameof(matchFormatId));
        MapPoolId = Required(mapPoolId, nameof(mapPoolId));
        SeriesPolicyId = Required(seriesPolicyId, nameof(seriesPolicyId));
        MatchmakingPolicyId = Required(
            matchmakingPolicyId,
            nameof(matchmakingPolicyId));
        AdmissionPolicyId = Required(
            admissionPolicyId,
            nameof(admissionPolicyId));
        ExecutionPolicyId = Required(
            executionPolicyId,
            nameof(executionPolicyId));
        ExecutionEngineVersion = Required(
            executionEngineVersion,
            nameof(executionEngineVersion));
        DefinitionFingerprint = Fingerprint(
            definitionFingerprint,
            nameof(definitionFingerprint));
    }

    public PlaylistVersionId Id { get; }
    public string PlaylistKey { get; }
    public int Version { get; }
    public string DisplayName { get; }
    public string GameModeId { get; }
    public string RulesetId { get; }
    public string MatchFormatId { get; }
    public string MapPoolId { get; }
    public string SeriesPolicyId { get; }
    public string MatchmakingPolicyId { get; }
    public string AdmissionPolicyId { get; }
    public string ExecutionPolicyId { get; }
    public string ExecutionEngineVersion { get; }
    public string DefinitionFingerprint { get; }

    private static string Required(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? "";
        return normalized.Length > 0
            ? normalized
            : throw new ArgumentException(
                "A playlist definition field cannot be blank.",
                parameterName);
    }

    private static string Fingerprint(string value, string parameterName)
    {
        string fingerprint = Required(value, parameterName);
        if (fingerprint.Length != 64 ||
            fingerprint.Any(character =>
                character is not (>= '0' and <= '9') and
                    not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "A playlist definition fingerprint must be lowercase SHA-256.",
                parameterName);
        }
        return fingerprint;
    }
}
