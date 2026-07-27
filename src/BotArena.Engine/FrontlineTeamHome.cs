using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// One team's Prime spawn and map-declared protected pad. Enemy ground movement
/// cannot enter this pad. It grants no damage immunity and does not block
/// projectiles. PrimeSpawn is the only Prime respawn tile; the remaining pad
/// tiles are not implicit respawn or fabrication candidates.
/// </summary>
public sealed record FrontlineTeamHome(
    int TeamId,
    Spawn PrimeSpawn,
    ImmutableArray<Position> ProtectedSpawnPad);
