namespace BotArena.Engine;

/// <summary>
/// Immutable queue- or completion-time facts for one active Split source.
/// Runtime memory and decisions are deliberately outside the mechanics kernel.
/// </summary>
public sealed record SplitReplicationActorSnapshot(
    ActorIdentity ActorId,
    int ParticipantId,
    int Generation,
    string FormId,
    int Health,
    Position Position,
    Direction Facing,
    bool HasPriorSameLifeTransition,
    bool HasPendingSameLifeTransition);
