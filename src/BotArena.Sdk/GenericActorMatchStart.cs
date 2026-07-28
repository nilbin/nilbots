namespace BotArena.Sdk;

/// <summary>
/// Immutable initialization delivered once to one generic actor life. Static
/// match data is parsed from the exact canonical schema-2 contract carried by
/// the wire and is not repeated in observations.
/// </summary>
public sealed record GenericActorMatchStart
{
    /// <summary>MatchStart payload schema selected during negotiation.</summary>
    public required int SchemaVersion { get; init; }
    /// <summary>Generic runtime-contract version selected during negotiation.</summary>
    public required int RuntimeContractVersion { get; init; }
    /// <summary>Exact identity of this independently executing body life.</summary>
    public required ActorIdentity ActorId { get; init; }
    /// <summary>Participant whose submitted program controls this life.</summary>
    public required int ParticipantId { get; init; }
    /// <summary>
    /// Deterministic private random seed scoped to this exact body life.
    /// </summary>
    public required ulong ActorRandomSeed { get; init; }
    /// <summary>Immutable creation reason and lineage for this life.</summary>
    public required LifeOrigin Origin { get; init; }
    /// <summary>
    /// Frozen rules, map, format, topology, deployment, and mode binding.
    /// </summary>
    public required GenericActorResolvedMatchContract Contract { get; init; }

    /// <summary>
    /// Immutable lineage for this life. Transition-created siblings share
    /// <paramref name="SourceOperationId"/>, while
    /// <paramref name="SourceTransitionId"/> identifies the static catalog
    /// rule that created them.
    /// </summary>
    /// <param name="Reason">Why this independently executing life was created.</param>
    /// <param name="Generation">Replication/return generation of the new life.</param>
    /// <param name="ParentActorId">
    /// Prior same-slot life for automatic return, or source life for
    /// fabrication/replication; <see langword="null"/> for an initial life or
    /// declared first automatic activation.
    /// </param>
    /// <param name="SourceTransitionId">
    /// Static catalog transition ID for transition-created lives; otherwise
    /// <see langword="null"/>.
    /// </param>
    /// <param name="SourceOperationId">
    /// Unique occurrence handle shared by sibling outputs; otherwise
    /// <see langword="null"/>.
    /// </param>
    public sealed record LifeOrigin(
        SpawnReason Reason,
        int Generation,
        ActorIdentity? ParentActorId,
        string? SourceTransitionId,
        string? SourceOperationId);

    /// <summary>Reason a new independently executing body life was created.</summary>
    public enum SpawnReason
    {
        /// <summary>Life exists in the match's initial deployment.</summary>
        Initial = 0,
        /// <summary>Life returned through its lifecycle profile after destruction.</summary>
        AutomaticReturn = 1,
        /// <summary>Life was created by a fabrication transition.</summary>
        Fabrication = 2,
        /// <summary>Life was created by a replication transition.</summary>
        Replication = 3,
        /// <summary>
        /// The slot's declared delayed activation created its first life.
        /// </summary>
        AutomaticActivation = 4,
    }
}
