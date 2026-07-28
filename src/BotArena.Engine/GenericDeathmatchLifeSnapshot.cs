namespace BotArena.Engine;

/// <summary>Immutable authoritative state for one active generic actor life.</summary>
public sealed record GenericDeathmatchLifeSnapshot(
    ActorIdentity ActorId,
    int ParticipantId,
    int Generation,
    string FormId,
    Position Position,
    Direction Facing,
    int Health,
    int Cooldown,
    int? Energy,
    GenericActorRuntimeActionResolution? PreviousActionResolution);
