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
    /// rather than placing them on the assigned spawn — unconditionally, for
    /// every team, on every automatic arrival.
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
    /// The declared MUSTER side objective, or null when this contract has
    /// none. MUSTER takes the same forward-rally derivation the keel hands
    /// both teams unconditionally and makes it a contested asset: the owner
    /// rallies forward, everyone else walks home.
    /// </summary>
    public static FrontlineSecondaryControlDefinition? Muster(
        ActorResolvedMatchDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Rules.GameMode
                is FrontlineGameModeDefinition { SecondaryControl: { } control }
            && control.Effect
                == FrontlineSecondaryControlDefinition.SecondaryEffectKind
                    .Muster
            ? control
            : null;
    }

    /// <summary>
    /// True when any automatic arrival under this contract can land anywhere
    /// other than its assigned spawn, so callers can skip the derivation
    /// entirely on the historical contracts.
    /// </summary>
    public static bool MayRallyForward(
        ActorResolvedMatchDefinition definition) =>
        IsEnabled(definition) || Muster(definition) is not null;

    /// <summary>
    /// Whether THIS slot's automatic arrival rallies forward on THIS tick.
    /// A lifecycle-declared forward rally always does. Under MUSTER the
    /// answer is owner-dependent and scoped: only a slot inside the declared
    /// rally scope, on the team that owns the site at the arrival's own tick,
    /// rallies. Deaths queued while the flag was held therefore still walk
    /// home if the flag was lost before they land — the owner at respawn
    /// time decides, which is both the simpler rule and the readable one.
    /// </summary>
    public static bool RalliesForward(
        ActorResolvedMatchDefinition definition,
        ActorUnitSlotLifecycleAssignmentDefinition? assignment,
        int teamId,
        int? secondaryOwnerTeamId)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (IsEnabled(definition))
            return true;
        return Muster(definition) is { } muster
            && secondaryOwnerTeamId == teamId
            && InRallyScope(muster, assignment);
    }

    /// <summary>
    /// The rally scope, resolved against one slot's lifecycle assignment.
    /// The Prime is the slot the contract starts the match with — the body
    /// every class fields from tick zero and the only one whose respawn runs
    /// the shared automatic-return clock.
    /// </summary>
    private static bool InRallyScope(
        FrontlineSecondaryControlDefinition muster,
        ActorUnitSlotLifecycleAssignmentDefinition? assignment) =>
        muster.RallyScope switch
        {
            FrontlineSecondaryControlDefinition.SecondaryRallyScopeKind
                .PrimeAutomaticReturnOnly =>
                assignment?.InitialAvailability
                == ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.ActiveAtTickZero,
            _ => false,
        };

    /// <summary>
    /// The tile one automatic arrival takes. Returns
    /// <paramref name="assignedSpawn"/> whenever this arrival does not rally
    /// forward, the mode is not Frontline, the own-side chain neighbour does
    /// not exist, or the derived region offers no legal tile.
    /// </summary>
    /// <param name="definition">The resolved contract.</param>
    /// <param name="teamId">The arriving slot's scoring team.</param>
    /// <param name="assignedSpawn">The slot's reserved home tile.</param>
    /// <param name="activePositionIndex">The live frontline position.</param>
    /// <param name="blocked">Tiles an arrival may not take.</param>
    /// <param name="assignment">
    /// The arriving slot's lifecycle assignment, used only to resolve a
    /// MUSTER rally scope. Null keeps the historical unconditional
    /// behaviour, which is what every non-muster caller wants.
    /// </param>
    /// <param name="secondaryOwnerTeamId">
    /// The side objective's owner at this arrival's own tick.
    /// </param>
    public static Position Resolve(
        ActorResolvedMatchDefinition definition,
        int teamId,
        Position assignedSpawn,
        int activePositionIndex,
        IReadOnlySet<Position> blocked,
        ActorUnitSlotLifecycleAssignmentDefinition? assignment = null,
        int? secondaryOwnerTeamId = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(blocked);
        if (!RalliesForward(
                definition,
                assignment,
                teamId,
                secondaryOwnerTeamId)
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
            activePositionIndex,
            TeamAdvanceOrdered(definition)))
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
    /// <summary>
    /// True when arrivals are ordered along the placing team's own advance
    /// axis rather than in canonical map order. The MUSTER effect always is:
    /// a contested rally that handed the two mirror-image regions non-mirrored
    /// tiles would bake a side sweep into the prize itself.
    /// </summary>
    private static bool TeamAdvanceOrdered(
        ActorResolvedMatchDefinition definition) =>
        definition.Rules.Lifecycle.AutomaticReturnPlacement
            == ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                .OwnSideChainAdjacentObjectiveTileInTeamAdvanceOrderThenAssignedSpawn
        || Muster(definition) is not null;

    private static IEnumerable<Position> Candidates(
        ActorResolvedMatchDefinition definition,
        FrontlineActorModeMapBindingDefinition binding,
        ActorMapRegionDefinition region,
        int activePositionIndex,
        bool teamAdvanceOrdered)
    {
        if (!teamAdvanceOrdered)
            return region.Tiles;

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
