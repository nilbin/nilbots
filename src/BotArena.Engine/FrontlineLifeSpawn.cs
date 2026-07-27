namespace BotArena.Engine;

/// <summary>One exact actor life created by a tick-start lifecycle transition.</summary>
public sealed record FrontlineLifeSpawn(
    FrontlineActorId ActorId,
    ActorSpawnReason Reason);
