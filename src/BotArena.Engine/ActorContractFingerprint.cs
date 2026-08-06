using System.Runtime.CompilerServices;
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

    /// <summary>
    /// A resolved match definition is immutable once constructed, and one
    /// match asks for its aggregate fingerprint on every world snapshot — twice
    /// per tick — so the canonical serialization and SHA-256 are memoized
    /// against the instance rather than recomputed. The table holds the key
    /// weakly, so a finished match's entry dies with its definition.
    /// </summary>
    private static readonly ConditionalWeakTable<
        ActorResolvedMatchDefinition, string> MatchFingerprints = new();

    public static string ComputeMatch(ActorResolvedMatchDefinition match)
    {
        ArgumentNullException.ThrowIfNull(match);
        return MatchFingerprints.GetValue(match, ComputeMatchPayload);
    }

    private static string ComputeMatchPayload(
        ActorResolvedMatchDefinition match)
    {
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
