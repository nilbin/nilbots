using BotArena.Sdk;

/// <summary>
/// Chooses where the wall's next segment goes. A site is a tile the declared
/// anchor route would accept, that covers the live objective with unobstructed
/// turret fire, sits on our side of the contested tiles, and pinches an approach
/// rather than standing in the open. Two children of the same team split
/// deterministically onto opposite flanks so the wall widens instead of
/// stacking.
///
/// <para>WHERE that is legal is a contract read, and it moved this wave. Every
/// previous revision could rely on the route's own
/// <c>placement.forbiddenTileTags</c> to keep a segment off the objective and out
/// of the central corridor, and revision 4's notes complained at length that the
/// tags deleted the interesting placements. An open-ground arm declares that list
/// EMPTY — the tag still exists on the map, and the route no longer consults it —
/// so the exclusions that used to be free now have to be judged, and two of them
/// are judged differently:</para>
///
/// <list type="bullet">
/// <item>a ONE-TILE CORRIDOR on the enemy's side of the point is the best tile on
/// the board for a segment, because bodies block bodies: a turret standing in a
/// pinch is a physical gate as well as a gun, and it turns a four-tile approach
/// into a fifteen-tile one. It is taken only when our own side can still reach
/// the objective without it.</item>
/// <item>the OBJECTIVE ITSELF is legal and usually wrong. A turret there scores
/// nothing and occupies a tile our own scorer wants, so it carries a penalty
/// rather than an exclusion — it wins only when the coverage it buys pays for the
/// tile it costs.</item>
/// </list>
/// </summary>
internal static class AnchorPlanner
{
    /// <summary>
    /// A gate is worth more than any coverage an open tile can buy, because it
    /// changes the enemy's route instead of taxing it. The bonus is set above the
    /// whole coverage range a six-tile objective can produce.
    /// </summary>
    private const int SealBonus = 26;

    /// <summary>
    /// Standing on the point costs a tile our own scorer wants and buys the
    /// shortest sightlines onto it. Priced, not forbidden — that is exactly the
    /// difference the open-ground arm made.
    /// </summary>
    private const int OnObjectivePenalty = 10;

    public sealed record Site(
        Position Position,
        int Score,
        int Coverage,
        bool Seals,
        bool OnObjective);

    public static Site? Choose(
        ContractView view,
        GenericActorContext context,
        GenericActorRulesContract.FormTransition route,
        int activeIndex,
        Column column)
    {
        IReadOnlyList<Position> objective = view.ObjectiveTiles(activeIndex);
        if (objective.Count == 0)
            return null;

        GenericActorRulesContract.AttackProfile? turretGun =
            view.Attack(route.TargetFormId);
        int reach = Math.Max(2, turretGun?.Projectile.MaxTravelTiles ?? 6);
        HashSet<Position> forbidden = view.AnchorForbiddenTiles(route);
        HashSet<Position> reachable =
            Navigation.Reachable(view, context.Self.Position, blocked: null);
        HashSet<Position> objectiveTiles = objective.ToHashSet();
        HashSet<Position> blockedByBodies = context.Allies
            .Select(ally => ally.Position)
            .Concat(context.Enemies.Select(enemy => enemy.Position))
            .ToHashSet();
        HashSet<Position> alliedForts = context.Allies
            .Where(ally => view.IsFortified(ally.FormId))
            .Select(ally => ally.Position)
            .ToHashSet();

        Position centre = Centre(objective);
        HashSet<Position> gates = Gates(view, centre, objectiveTiles, reach);
        bool crossIsVertical =
            Math.Abs(view.EnemyReference.X - view.HomeReference.X)
            >= Math.Abs(view.EnemyReference.Y - view.HomeReference.Y);
        int preferredFlank = view.MyUnitId % 2 == 0 ? -1 : 1;
        Direction[] order = Navigation.Order(view, context);

        Site? best = null;
        for (int dy = -reach; dy <= reach; dy++)
        {
            for (int dx = -reach; dx <= reach; dx++)
            {
                var tile = new Position(centre.X + dx, centre.Y + dy);
                if (view.IsWall(tile)
                    || forbidden.Contains(tile)
                    || !view.AnchorTileSatisfiesRequirements(route, tile)
                    || !reachable.Contains(tile))
                {
                    continue;
                }
                if (tile != context.Self.Position && blockedByBodies.Contains(tile))
                    continue;

                // Revision 6. Two tiles are excluded here for the first time and
                // neither is excluded by the map: one a sibling's route needs, and
                // one whose corridor run a sibling still has to walk. A segment is
                // a wall we build ourselves, and the wave-5 gate rule tested only
                // whether the MAP still connected our side to the point — which a
                // detour satisfies while costing the sibling behind us six ticks it
                // did not have. The route is the question; the map was the proxy.
                if (column.RefusesEmplacement(view, tile))
                    continue;

                int coverage = Geometry.Coverage(
                    view.IsWall,
                    tile,
                    objective,
                    reach);
                bool seals = gates.Contains(tile);
                if (coverage == 0 && !seals)
                    continue;

                int distance =
                    objective.Min(target => Geometry.Chebyshev(tile, target));
                if (distance > reach)
                    continue;

                bool onObjective = objectiveTiles.Contains(tile);
                int openNeighbours = Geometry.Cardinals
                    .Count(direction => view.IsOpen(Geometry.Step(tile, direction)));
                int side = Math.Clamp(
                    Geometry.Chebyshev(tile, view.EnemyReference)
                        - Geometry.Chebyshev(tile, view.HomeReference),
                    -3,
                    3);
                int cross = crossIsVertical
                    ? Math.Sign(tile.Y - centre.Y)
                    : Math.Sign(tile.X - centre.X);
                int crowding = alliedForts
                    .Count(fort => Geometry.Chebyshev(fort, tile) <= 1);

                int score =
                    coverage * 12
                    - (column.Stacked(tile) ? 14 : 0)
                    + (4 - openNeighbours) * 4
                    + side * 3
                    + (cross == preferredFlank ? 5 : cross == 0 ? 2 : 0)
                    + DistanceBand(distance, reach)
                    + (seals ? SealBonus : 0)
                    - (onObjective ? OnObjectivePenalty : 0)
                    - crowding * 14
                    - Geometry.Chebyshev(tile, context.Self.Position);

                if (best is null
                    || score > best.Score
                    || (score == best.Score
                        && Preferred(tile, best.Position, centre, order)))
                {
                    best = new Site(tile, score, coverage, seals, onObjective);
                }
            }
        }
        return best;
    }

    /// <summary>
    /// One-tile corridors between the enemy and the point that we can seal
    /// without sealing ourselves out. Two conditions, both necessary: the tile
    /// has to be a genuine pinch (a shape test, so a map without one simply
    /// yields nothing), and it has to sit upstream of the objective measured from
    /// THEIR deployment rather than from the map's compass, so the two sides'
    /// answers are exact reflections.
    ///
    /// <para>The reachability check is the one that matters. A gate that also
    /// cuts our own side off from the ground we are trying to take is a wall
    /// across our own advance, so each candidate is tested once with itself
    /// removed. A map has a handful of pinches, so this costs a handful of walks
    /// rather than one per candidate tile.</para>
    /// </summary>
    private static HashSet<Position> Gates(
        ContractView view,
        Position centre,
        HashSet<Position> objectiveTiles,
        int reach)
    {
        HashSet<Position> gates = [];
        int centreDistance = Geometry.Chebyshev(centre, view.EnemyReference);
        for (int dy = -reach; dy <= reach; dy++)
        {
            for (int dx = -reach; dx <= reach; dx++)
            {
                var tile = new Position(centre.X + dx, centre.Y + dy);
                if (objectiveTiles.Contains(tile)
                    || !Geometry.IsCorridor(view.IsWall, tile))
                {
                    continue;
                }
                if (Geometry.Chebyshev(tile, view.EnemyReference) >= centreDistance)
                    continue;
                HashSet<Position> closed = [tile];
                HashSet<Position> without =
                    Navigation.Reachable(view, view.HomeReference, closed);
                if (objectiveTiles.Any(without.Contains))
                    gates.Add(tile);
            }
        }
        return gates;
    }

    /// <summary>
    /// A turret one tile from the objective plugs its own team's approach lane;
    /// one at the edge of its reach is easy to walk around. The middle band is
    /// where a wall segment actually holds ground.
    /// </summary>
    private static int DistanceBand(int distance, int reach)
    {
        if (distance <= 1)
            return -6;
        if (distance <= Math.Max(2, reach / 2))
            return 8;
        return 2;
    }

    public static Position Centre(IReadOnlyList<Position> tiles)
    {
        int x = 0;
        int y = 0;
        foreach (Position tile in tiles)
        {
            x += tile.X;
            y += tile.Y;
        }
        return new Position(x / tiles.Count, y / tiles.Count);
    }

    /// <summary>
    /// Tie-break between equally good sites, projected onto this life's
    /// mirror-fair direction order rather than onto the map's absolute compass.
    /// Preferring the lower row and column is the same systematic side bias as
    /// an absolute movement preference: both teams share it, so on a
    /// mirror-symmetric map one of them is always right.
    /// </summary>
    private static bool Preferred(
        Position candidate,
        Position current,
        Position centre,
        Direction[] order) =>
        Projection(candidate, centre, order) > Projection(current, centre, order);

    private static int Projection(
        Position tile,
        Position centre,
        Direction[] order)
    {
        int score = 0;
        for (int index = 0; index < order.Length; index++)
        {
            (int dx, int dy) = order[index].Vector();
            score += ((tile.X - centre.X) * dx + (tile.Y - centre.Y) * dy)
                * (order.Length - index);
        }
        return score;
    }
}
