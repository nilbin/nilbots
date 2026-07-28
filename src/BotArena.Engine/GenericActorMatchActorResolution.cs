namespace BotArena.Engine;

/// <summary>One actor's authoritative gameplay action result.</summary>
public sealed record GenericActorMatchActorResolution(
    int ParticipantId,
    ActorIdentity ActorId,
    GenericActorRuntimeActionResolution Resolution);
