namespace BotArena.Sdk;

/// <summary>
/// Immutable initialization delivered once to a fresh body life. It includes
/// the complete public match contract rather than resending static rules on
/// every tick.
/// </summary>
public sealed record ActorMatchStart
{
    public required int SchemaVersion { get; init; }
    public required int RuntimeContractVersion { get; init; }
    public required ActorIdentity ActorId { get; init; }
    public required int ParticipantId { get; init; }
    public required ulong ActorRandomSeed { get; init; }
    public required ActorSpawnReason SpawnReason { get; init; }
    public required PublicMatchContractManifest Contract { get; init; }
}
