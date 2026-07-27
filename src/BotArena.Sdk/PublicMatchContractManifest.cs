namespace BotArena.Sdk;

/// <summary>The complete immutable public contract resolved before a match.</summary>
public sealed record PublicMatchContractManifest
{
    public required int SchemaVersion { get; init; }
    public required string MatchContractFingerprint { get; init; }
    public required PublicRulesManifest Rules { get; init; }
    public required PublicMapManifest Map { get; init; }
    public required PublicMatchTopology Topology { get; init; }
}
