namespace BotArena.Engine;

/// <summary>Immutable authoritative state for one persistent projectile.</summary>
public sealed record GenericDeathmatchProjectileSnapshot(
    long ProjectileId,
    int OwnerTeamId,
    ActorIdentity OwnerActorId,
    Position Position,
    ProjectileHeading Heading,
    int TilesPerAdvance,
    int TicksUntilAdvance,
    int RemainingTiles);
