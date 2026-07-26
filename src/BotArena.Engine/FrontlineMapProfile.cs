using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Typed format-v2 geometry for a two-team Frontline match. Position order is
/// gameplay-significant: team 0 pushes toward higher indices and team 1 toward
/// lower indices. Tile order inside each set is canonicalized by Y then X.
/// </summary>
public sealed record FrontlineMapProfile(
    ImmutableArray<FrontlineRegion> Positions,
    ImmutableArray<FrontlineTeamHome> TeamHomes,
    ImmutableArray<Position> AnchorForbiddenTiles);
