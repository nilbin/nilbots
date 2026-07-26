using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// One team's Prime spawn and map-declared protected pad. In Package 2,
/// protection means every pad tile is Anchor-forbidden; it does not yet imply
/// occupancy exclusion, immunity, or a respawn-protection timer. Those lifecycle
/// semantics must be resolved and tested by the playable Frontline session.
/// PrimeSpawn is the only spawnable tile declared by this package; the remaining
/// pad tiles are not implicit respawn or fabrication candidates.
/// </summary>
public sealed record FrontlineTeamHome(
    int TeamId,
    Spawn PrimeSpawn,
    ImmutableArray<Position> ProtectedSpawnPad);
