using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Resolves where an automatic arrival lands under the forward-rally
/// lifecycle placement (<see
/// cref="ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind.OwnSideChainAdjacentObjectiveTileThenAssignedSpawn"/>).
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
            == ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                .OwnSideChainAdjacentObjectiveTileThenAssignedSpawn;
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

        string regionId = binding.OrderedObjectiveRegionIds[rallyIndex];
        ActorMapRegionDefinition? region = definition.Map.Regions
            .FirstOrDefault(value => string.Equals(
                value.RegionId,
                regionId,
                StringComparison.Ordinal));
        if (region is null)
            return assignedSpawn;

        // Region tiles are canonically ordered by row then column, so "the
        // first legal tile" is a total, map-authored order rather than an
        // enumeration accident.
        foreach (Position tile in region.Tiles)
        {
            if (!definition.Map.IsWall(tile) && !blocked.Contains(tile))
                return tile;
        }
        return assignedSpawn;
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
