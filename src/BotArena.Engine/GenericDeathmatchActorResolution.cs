namespace BotArena.Engine;

/// <summary>One actor's authoritative gameplay action result.</summary>
public sealed record GenericDeathmatchActorResolution(
    int ParticipantId,
    ActorIdentity ActorId,
    GenericActorRuntimeActionResolution Resolution);
