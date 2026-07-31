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

    /// <summary>
    /// Root seed of the scoring team's shared stream. Identical for every life
    /// on the team — including lives created later in the match — so a pure
    /// function of it is common knowledge inside the team.
    /// </summary>
    public required ulong TeamRandomSeed { get; init; }
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
        AutomaticActivation = 4,

        /// <summary>
        /// Additive append (DECISIONS #194), exactly the discipline the
        /// observed-event enum follows: the participant's HOME BASE acted as
        /// the root factory and seeded this body because the participant had
        /// none left. It happens only on a ruleset whose lifecycle profile
        /// declares <c>rootFactorySeedFormId</c>, so no existing replay or
        /// observation changes.
        /// </summary>
        RootFactorySeed = 5,
    }
}
