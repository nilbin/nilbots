using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// One team's Prime spawn and map-declared protected pad. Enemy ground movement
/// cannot enter this pad. It grants no damage immunity and does not block
/// projectiles. PrimeSpawn is the only Prime respawn tile and stays reserved
/// for the Prime; child fabrication explicitly selects from the remaining pad
/// tiles.
/// </summary>
public sealed record FrontlineTeamHome(
    int TeamId,
    Spawn PrimeSpawn,
    ImmutableArray<Position> ProtectedSpawnPad);
