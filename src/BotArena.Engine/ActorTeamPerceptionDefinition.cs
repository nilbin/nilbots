namespace BotArena.Engine;

/// <summary>
/// Declares which authoritative observations are shared between active lives
/// controlled by participants on the same scoring team.
/// </summary>
public sealed record ActorTeamPerceptionDefinition
{
    public ActorTeamPerceptionDefinition(PerceptionKind kind)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        Kind = kind;
    }

    public PerceptionKind Kind { get; }
    public SnapshotKind Snapshot => SnapshotKind.FrozenPreTickState;
    public SameTickDecisionSharingKind SameTickDecisionSharing =>
        SameTickDecisionSharingKind.None;
    public ObservationProvenanceKind ObservationProvenance =>
        ObservationProvenanceKind.ExactObservedByActorIdentities;

    public enum PerceptionKind
    {
        Individual = 0,
        ImmediateUnion = 1,
    }

    public enum SnapshotKind
    {
        /// <summary>
        /// Individual and union observations are computed from the same
        /// authoritative pre-tick state before any decision is collected.
        /// ImmediateUnion gives each active teammate the union of all active
        /// allied sensors plus its own private self state.
        /// </summary>
        FrozenPreTickState = 0,
    }

    public enum SameTickDecisionSharingKind
    {
        None = 0,
    }

    public enum ObservationProvenanceKind
    {
        ExactObservedByActorIdentities = 0,
    }
}
