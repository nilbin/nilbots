using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Resolves where an automatic arrival lands under either forward-rally
/// lifecycle placement — the historical map-absolute one (<see
/// cref="ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind.OwnSideChainAdjacentObjectiveTileThenAssignedSpawn"/>)
/// and the team-advance-ordered one (<see
/// cref="ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind.OwnSideChainAdjacentObjectiveTileInTeamAdvanceOrderThenAssignedSpawn"/>).
/// It is deliberately pure and shared: the session places lives with it and
/// the chronology validator recomputes the same authoritative fact from the
/// recorded boundary, so a replay cannot claim an arrival the rules would not
/// have produced.
/// </summary>
internal static class FrontlineForwardRallyPlacement
{
    /// <summary>
    /// True when this contract derives arrivals from the objective chain
    /// rather than placing them on the assigned spawn.
    /// </summary>
    public static bool IsEnabled(
        ActorResolvedMatchDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Rules.Lifecycle.AutomaticReturnPlacement
            is ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                .OwnSideChainAdjacentObjectiveTileThenAssignedSpawn
            or ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                .OwnSideChainAdjacentObjectiveTileInTeamAdvanceOrderThenAssignedSpawn;
    }

    /// <summary>
    /// The tile one automatic arrival takes. Returns
    /// <paramref name="assignedSpawn"/> whenever the contract does not rally
    /// forward, the mode is not Frontline, the own-side chain neighbour does
    /// not exist, or the derived region offers no legal tile.
    /// </summary>
    public static Position Resolve(
        ActorResolvedMatchDefinition definition,
        int teamId,
        Position assignedSpawn,
        int activePositionIndex,
        IReadOnlySet<Position> blocked)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(blocked);
        if (!IsEnabled(definition)
            || definition.ModeMapBinding
                is not FrontlineActorModeMapBindingDefinition binding)
        {
            return assignedSpawn;
        }

        FrontlineTeamAdvanceDefinition? advance = binding.TeamAdvances
            .FirstOrDefault(value => value.TeamId == teamId);
        if (advance is null)
            return assignedSpawn;

        // Own side is one step back along this team's advance direction: the
        // objective it has already taken, not the one it is contesting.
        int rallyIndex = activePositionIndex - advance.ObjectiveIndexDelta;
        if (rallyIndex < 0
            || rallyIndex >= binding.OrderedObjectiveRegionIds.Length)
        {
            return assignedSpawn;
        }

        ActorMapRegionDefinition? region = Region(
            definition,
            binding,
            rallyIndex);
        if (region is null)
            return assignedSpawn;

        foreach (Position tile in Candidates(
            definition,
            binding,
            region,
            activePositionIndex))
        {
            if (!definition.Map.IsWall(tile) && !blocked.Contains(tile))
                return tile;
        }
        return assignedSpawn;
    }

    /// <summary>
    /// The rally region's tiles in the order this contract consumes them.
    /// The historical placement takes them in canonical map order (row, then
    /// column) — one absolute scan for both teams, which is not
    /// mirror-equivalent because their regions are reflections of each other.
    /// The team-advance placement orders them along the placing team's own
    /// advance axis instead, so the two teams take reflected tiles.
    /// </summary>
    private static IEnumerable<Position> Candidates(
        ActorResolvedMatchDefinition definition,
        FrontlineActorModeMapBindingDefinition binding,
        ActorMapRegionDefinition region,
        int activePositionIndex)
    {
        if (definition.Rules.Lifecycle.AutomaticReturnPlacement
            != ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                .OwnSideChainAdjacentObjectiveTileInTeamAdvanceOrderThenAssignedSpawn)
        {
            return region.Tiles;
        }

        (bool AlongX, int Sign) advance = AdvanceOrder(
            region,
            Region(definition, binding, activePositionIndex));
        return advance.Sign == 0
            ? region.Tiles
            : advance.AlongX
                ? region.Tiles
                    .OrderBy(tile => advance.Sign * tile.X)
                    .ThenBy(tile => tile.Y)
                : region.Tiles
                    .OrderBy(tile => advance.Sign * tile.Y)
                    .ThenBy(tile => tile.X);
    }

    /// <summary>
    /// Which map axis this team advances along, and in which direction,
    /// derived from the chain step it is about to reinforce: the vector from
    /// the rally region's centroid to the active region's centroid. Team IDs
    /// never enter it, so reflecting the world reflects the answer. Centroids
    /// are compared as exact integer ratios (both differences carry the same
    /// denominator), and the dominant component wins; an exactly diagonal
    /// step takes X, which a reflection across X preserves for both teams.
    /// A zero vector — two regions with the same centroid — is not an advance
    /// direction at all and returns sign zero, leaving canonical map order.
    /// </summary>
    private static (bool AlongX, int Sign) AdvanceOrder(
        ActorMapRegionDefinition rally,
        ActorMapRegionDefinition? active)
    {
        if (active is null || active.Tiles.Length == 0
            || rally.Tiles.Length == 0)
        {
            return (true, 0);
        }

        long rallyCount = rally.Tiles.Length;
        long activeCount = active.Tiles.Length;
        long deltaX = (Sum(active, tile => tile.X) * rallyCount)
            - (Sum(rally, tile => tile.X) * activeCount);
        long deltaY = (Sum(active, tile => tile.Y) * rallyCount)
            - (Sum(rally, tile => tile.Y) * activeCount);
        return Math.Abs(deltaX) >= Math.Abs(deltaY)
            ? (true, Math.Sign(deltaX))
            : (false, Math.Sign(deltaY));
    }

    private static long Sum(
        ActorMapRegionDefinition region,
        Func<Position, int> select) =>
        region.Tiles.Sum(tile => (long)select(tile));

    private static ActorMapRegionDefinition? Region(
        ActorResolvedMatchDefinition definition,
        FrontlineActorModeMapBindingDefinition binding,
        int positionIndex)
    {
        if (positionIndex < 0
            || positionIndex >= binding.OrderedObjectiveRegionIds.Length)
        {
            return null;
        }

        string regionId = binding.OrderedObjectiveRegionIds[positionIndex];
        return definition.Map.Regions
            .FirstOrDefault(value => string.Equals(
                value.RegionId,
                regionId,
                StringComparison.Ordinal));
    }

    /// <summary>
    /// The tiles an arrival may not take: occupied tiles, reserved lifecycle
    /// output tiles, and every permanently reserved automatic-return spawn.
    /// Projectiles never block — the lifecycle consumes them on the output
    /// tile immediately before the life is created.
    /// </summary>
    public static ImmutableHashSet<Position> BlockedTiles(
        IEnumerable<Position> occupiedTiles,
        IEnumerable<Position> reservedLifecycleTiles,
        IEnumerable<Position> reservedReturnSpawns) =>
    [
        .. occupiedTiles,
        .. reservedLifecycleTiles,
        .. reservedReturnSpawns,
    ];
}
