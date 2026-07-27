namespace BotArena.Engine;

/// <summary>
/// Tick-start lifecycle transitions and the canonical actor set eligible to
/// submit the joint decision for this tick.
/// </summary>
public sealed record FrontlineTickStart(
    int Tick,
    IReadOnlyList<FrontlineActorId> ActiveActors,
    IReadOnlyList<FrontlineActorId> RespawnedActors,
    IReadOnlyList<FrontlineMatchEvent> Events);
