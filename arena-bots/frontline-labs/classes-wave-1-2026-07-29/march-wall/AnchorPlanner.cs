using BotArena.Sdk;

/// <summary>
/// Chooses where the wall's next segment goes. A site is a tile the declared
/// anchor route would accept, that covers the live objective with unobstructed
/// turret fire, sits on our side of the contested tiles, and pinches an
/// approach rather than standing in the open. Two children of the same team
/// split deterministically onto opposite flanks so the wall widens instead of
/// stacking.
/// </summary>
internal static class AnchorPlanner
{
    public sealed record Site(Position Position, int Score, int Coverage);

    public static Site? Choose(
        ContractView view,
        GenericActorContext context,
        GenericActorRulesContract.FormTransition route,
        int activeIndex)
    {
        IReadOnlyList<Position> objective = view.ObjectiveTiles(activeIndex);
        if (objective.Count == 0)
            return null;

        GenericActorRulesContract.AttackProfile? turretGun =
            view.Attack(route.TargetFormId);
        int reach = Math.Max(2, turretGun?.Projectile.MaxTravelTiles ?? 6);
        HashSet<Position> forbidden = view.AnchorForbiddenTiles(route);
        HashSet<Position> reachable =
            Navigation.Reachable(view, context.Self.Position);
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
        bool crossIsVertical =
            Math.Abs(view.EnemyReference.X - view.HomeReference.X)
            >= Math.Abs(view.EnemyReference.Y - view.HomeReference.Y);
        int preferredFlank = view.MyUnitId % 2 == 0 ? -1 : 1;

        Site? best = null;
        for (int dy = -reach; dy <= reach; dy++)
        {
            for (int dx = -reach; dx <= reach; dx++)
            {
                var tile = new Position(centre.X + dx, centre.Y + dy);
                if (view.IsWall(tile)
                    || forbidden.Contains(tile)
                    || objectiveTiles.Contains(tile)
                    || !view.AnchorTileSatisfiesRequirements(route, tile)
                    || !reachable.Contains(tile))
                {
                    continue;
                }
                if (tile != context.Self.Position && blockedByBodies.Contains(tile))
                    continue;

                int coverage = Geometry.Coverage(
                    view.IsWall,
                    tile,
                    objective,
                    reach);
                if (coverage == 0)
                    continue;

                int distance = objective.Min(target => Geometry.Chebyshev(tile, target));
                if (distance > reach)
                    continue;

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
                    + (4 - openNeighbours) * 4
                    + side * 3
                    + (cross == preferredFlank ? 5 : cross == 0 ? 2 : 0)
                    + DistanceBand(distance, reach)
                    - crowding * 14
                    - Geometry.Chebyshev(tile, context.Self.Position);

                if (best is null
                    || score > best.Score
                    || (score == best.Score && Earlier(tile, best.Position)))
                {
                    best = new Site(tile, score, coverage);
                }
            }
        }
        return best;
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

    private static bool Earlier(Position candidate, Position current) =>
        candidate.Y != current.Y
            ? candidate.Y < current.Y
            : candidate.X < current.X;
}
