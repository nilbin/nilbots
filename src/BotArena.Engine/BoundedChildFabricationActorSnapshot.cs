namespace BotArena.Engine;

/// <summary>
/// Immutable queue-time facts for one active fabrication-capable life.
/// Runtime and private-memory state are deliberately absent.
/// </summary>
public sealed record BoundedChildFabricationActorSnapshot(
    ActorIdentity ActorId,
    int ParticipantId,
    int Generation,
    string FormId,
    Position Position,
    Direction Facing);
