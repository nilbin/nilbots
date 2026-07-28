namespace BotArena.Engine;

/// <summary>
/// Immutable initialization for one generic actor life. The Engine retains its
/// authoritative resolved definition; runtime adapters project it into their
/// own public contract representation.
/// </summary>
public sealed record GenericActorRuntimeStart
{
    public required int SchemaVersion { get; init; }
    public required int RuntimeContractVersion { get; init; }
    public required ActorIdentity ActorId { get; init; }
    public required int ParticipantId { get; init; }
    public required ulong ActorRandomSeed { get; init; }
    public required LifeOrigin Origin { get; init; }
    public required ActorResolvedMatchDefinition Contract { get; init; }

    public sealed record LifeOrigin(
        SpawnReason Reason,
        int Generation,
        ActorIdentity? ParentActorId,
        string? SourceTransitionId,
        string? SourceOperationId);

    public enum SpawnReason
    {
        Initial = 0,
        AutomaticReturn = 1,
        Fabrication = 2,
        Replication = 3,
    }
}
