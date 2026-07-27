using System.Security.Cryptography;
using System.Text;

namespace BotArena.Engine;

/// <summary>
/// Lowercase SHA-256 fingerprints for each independent generation-3 contract
/// component and the exact resolved generation-2 match envelope.
/// </summary>
public static class ActorContractFingerprint
{
    public static string ComputeRules(ActorRulesDefinition rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return Hash(
            ActorContractManifestSerializer
                .SerializeRulesFingerprintPayload(rules));
    }

    public static string ComputeMap(ActorMapDefinition map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return Hash(
            ActorContractManifestSerializer
                .SerializeMapFingerprintPayload(map));
    }

    public static string ComputeFormat(MatchFormatDefinition format)
    {
        ArgumentNullException.ThrowIfNull(format);
        return Hash(
            ActorContractManifestSerializer
                .SerializeFormatFingerprintPayload(format));
    }

    public static string ComputeTopology(PublicMatchTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        return Hash(
            ActorContractManifestSerializer
                .SerializeTopologyFingerprintPayload(topology));
    }

    public static string ComputeMatch(ActorResolvedMatchDefinition match)
    {
        ArgumentNullException.ThrowIfNull(match);
        ValidateMatch(match);
        return Hash(
            ActorContractManifestSerializer
                .SerializeMatchFingerprintPayload(match));
    }

    internal static void ValidateMatch(ActorResolvedMatchDefinition match) =>
        ActorResolvedMatchDefinitionValidator.Validate(
            match.Rules,
            match.Map,
            match.Format,
            match.Topology,
            match.InitialDeployment,
            match.LifecycleAssignments,
            match.ParticipantRegionAssignments,
            match.ModeMapBinding);

    private static string Hash(string canonicalPayload) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload)));
}
