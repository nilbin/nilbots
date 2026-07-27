using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// A Frontline projectile in flight. Ownership remains bound to the exact
/// firing life even when that life has been destroyed and respawned.
/// </summary>
public sealed class FrontlineProjectileState
{
    private readonly ImmutableArray<Position>? _programmedPath;

    internal FrontlineProjectileState(
        long id,
        FrontlineActorId ownerActorId,
        Position position,
        Direction direction,
        ProjectileHeading? heading = null,
        ShotProgram? shotProgram = null,
        IReadOnlyList<Position>? programmedPath = null)
    {
        Id = id;
        OwnerActorId = ownerActorId;
        Position = position;
        Direction = direction;
        Heading = heading;
        ShotProgram = shotProgram;
        _programmedPath = programmedPath?.ToImmutableArray();
    }

    public long Id { get; }
    public FrontlineActorId OwnerActorId { get; }
    public int OwnerTeamId => OwnerActorId.TeamId;
    public Position Position { get; internal set; }
    public Direction Direction { get; }
    public ProjectileHeading? Heading { get; internal set; }
    public ShotProgram? ShotProgram { get; }
    public IReadOnlyList<Position>? ProgrammedPath => _programmedPath;
    public int NextProgrammedPathIndex { get; internal set; }
    public int TilesTraveled { get; internal set; }
    public int Phase { get; internal set; }
}
