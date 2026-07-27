namespace BotArena.Engine;

/// <summary>Ordered tiles traversed by one projectile during a Frontline tick.</summary>
public sealed record FrontlineProjectileTraversal(
    long Id,
    FrontlineActorId OwnerActorId,
    Direction Direction,
    Position From,
    IReadOnlyList<Position> Path,
    ProjectileHeading? Heading = null,
    ShotProgram? ShotProgram = null,
    IReadOnlyList<Position>? ProgrammedPath = null)
{
    public int OwnerTeamId => OwnerActorId.TeamId;
}
