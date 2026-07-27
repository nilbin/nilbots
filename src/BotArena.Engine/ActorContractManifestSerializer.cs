namespace BotArena.Engine;

/// <summary>
/// Deliberately ordered canonical JSON for generation-3 actor contracts.
/// These writers are isolated from the frozen historical manifest serializer.
/// </summary>
public static class ActorContractManifestSerializer
{
    public const int MapManifestSchemaVersion = 1;
    public const int FormatManifestSchemaVersion = 1;
    public const int TopologyManifestSchemaVersion = 1;
    public const int MatchContractSchemaVersion =
        ActorResolvedMatchDefinition.CurrentSchemaVersion;

    public static string ToCanonicalJson(ActorRulesDefinition rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return ActorContractCanonicalJson.Write(
            writer => ActorRulesCanonicalWriter.Write(
                writer,
                rules,
                includeProvenance: true));
    }

    public static string ToCanonicalJson(ActorMapDefinition map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return ActorMatchCanonicalWriter.SerializeMap(
            map,
            includeProvenance: true);
    }

    public static string ToCanonicalJson(MatchFormatDefinition format)
    {
        ArgumentNullException.ThrowIfNull(format);
        return ActorMatchCanonicalWriter.SerializeFormat(
            format,
            includeProvenance: true);
    }

    public static string ToCanonicalJson(PublicMatchTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        return ActorMatchCanonicalWriter.SerializeTopology(
            topology,
            includeFingerprint: true);
    }

    public static string ToCanonicalJson(ActorResolvedMatchDefinition match)
    {
        ArgumentNullException.ThrowIfNull(match);
        string canonical = ActorMatchCanonicalWriter.SerializeMatch(
            match,
            includeFingerprint: true);
        ActorContractProfileAdmission.ValidateCanonicalMatch(canonical);
        return canonical;
    }

    internal static string SerializeRulesFingerprintPayload(
        ActorRulesDefinition rules) =>
        ActorContractCanonicalJson.Write(
            writer => ActorRulesCanonicalWriter.Write(
                writer,
                rules,
                includeProvenance: false));

    internal static string SerializeMapFingerprintPayload(
        ActorMapDefinition map) =>
        ActorMatchCanonicalWriter.SerializeMap(
            map,
            includeProvenance: false);

    internal static string SerializeFormatFingerprintPayload(
        MatchFormatDefinition format) =>
        ActorMatchCanonicalWriter.SerializeFormat(
            format,
            includeProvenance: false);

    internal static string SerializeTopologyFingerprintPayload(
        PublicMatchTopology topology) =>
        ActorMatchCanonicalWriter.SerializeTopology(
            topology,
            includeFingerprint: false);

    internal static string SerializeMatchFingerprintPayload(
        ActorResolvedMatchDefinition match) =>
        ActorMatchCanonicalWriter.SerializeMatch(
            match,
            includeFingerprint: false);
}
