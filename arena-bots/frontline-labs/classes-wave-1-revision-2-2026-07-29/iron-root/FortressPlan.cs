using BotArena.Sdk;

/// <summary>
/// Where a fortress belongs. A covering tile is one that is legal to transform
/// on, off the scoring surface, and whose eight firing lanes actually cross the
/// active objective — computed from the map contract and the target form's own
/// declared reach, never from remembered coordinates.
/// </summary>
internal static class FortressPlan
{
    /// <summary>How many active-objective tiles this tile can shoot.</summary>
    public static int Coverage(
        GenericActorMapContract map,
        Position tile,
        Position[] objectiveTiles,
        int range,
        bool strictCorners)
    {
        if (objectiveTiles.Length == 0)
            return 0;
        var goals = new HashSet<Position>(objectiveTiles);
        var swept = new List<Position>();
        int covered = 0;
        foreach (ProjectileHeading heading in ArenaGeometry.Headings)
        {
            ArenaGeometry.WalkRay(
                map,
                tile,
                heading,
                range,
                strictCorners,
                swept);
            foreach (Position position in swept)
            {
                if (goals.Contains(position))
                    covered++;
            }
        }
        return covered;
    }

    /// <summary>
    /// Which active-objective tiles this tile's eight lanes actually reach.
    /// A fortress has objective weight zero, so its suppression only becomes
    /// territory when an allied body is standing on one of these tiles — this
    /// is the set a screen is stationed on while the fortress stands.
    /// </summary>
    public static HashSet<Position> CoveredTiles(
        GenericActorMapContract map,
        Position tile,
        Position[] objectiveTiles,
        int range,
        bool strictCorners)
    {
        var covered = new HashSet<Position>();
        if (objectiveTiles.Length == 0)
            return covered;
        var goals = new HashSet<Position>(objectiveTiles);
        var swept = new List<Position>();
        foreach (ProjectileHeading heading in ArenaGeometry.Headings)
        {
            ArenaGeometry.WalkRay(
                map,
                tile,
                heading,
                range,
                strictCorners,
                swept);
            foreach (Position position in swept)
            {
                if (goals.Contains(position))
                    covered.Add(position);
            }
        }
        return covered;
    }

    /// <summary>
    /// Candidate fortress tiles for one objective, best first. Coverage rules;
    /// ties prefer the tile nearer the objective and then the tile nearer our
    /// own side of the line, because a fortress that has to be walked past is
    /// worth more than one that has to be walked through.
    /// </summary>
    public static List<Position> RankSites(
        ContractLens lens,
        Position[] objectiveTiles,
        int range,
        bool strictCorners,
        Position? home,
        bool transformableOnly = true)
    {
        var ranked = new List<(Position Tile, int Cover, int Near, int Home)>();
        if (objectiveTiles.Length == 0)
            return [];

        var surface = new HashSet<Position>(objectiveTiles);
        GenericActorMapContract map = lens.Map;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var tile = new Position(x, y);
                if (!ArenaGeometry.IsOpen(map, tile)
                    || surface.Contains(tile)
                    || lens.SpawnProtected.Contains(tile))
                {
                    continue;
                }
                if (transformableOnly && lens.TransitionForbidden.Contains(tile))
                    continue;
                int near = ArenaGeometry.NearestDistance(tile, objectiveTiles);
                if (near > range)
                    continue;
                int cover = Coverage(
                    map,
                    tile,
                    objectiveTiles,
                    range,
                    strictCorners);
                if (cover <= 0)
                    continue;
                int homeDistance = home is Position anchor
                    ? tile.ChebyshevDistance(anchor)
                    : 0;
                ranked.Add((tile, cover, near, homeDistance));
            }
        }

        ranked.Sort(static (left, right) =>
        {
            int cover = right.Cover.CompareTo(left.Cover);
            if (cover != 0)
                return cover;
            int near = left.Near.CompareTo(right.Near);
            if (near != 0)
                return near;
            int home = left.Home.CompareTo(right.Home);
            if (home != 0)
                return home;
            int x = left.Tile.X.CompareTo(right.Tile.X);
            return x != 0 ? x : left.Tile.Y.CompareTo(right.Tile.Y);
        });

        var sites = new List<Position>();
        for (int index = 0; index < ranked.Count && index < 12; index++)
            sites.Add(ranked[index].Tile);
        return sites;
    }

    /// <summary>
    /// Tiles from which <paramref name="tile"/> can be shot, given the longest
    /// reach worth worrying about. Clear rays are symmetric, so sweeping our own
    /// eight lanes outward enumerates every muzzle that can answer.
    /// </summary>
    public static HashSet<Position> FiringTilesOn(
        GenericActorMapContract map,
        Position tile,
        int reach,
        bool strictCorners)
    {
        var lanes = new HashSet<Position>();
        var swept = new List<Position>();
        foreach (ProjectileHeading heading in ArenaGeometry.Headings)
        {
            ArenaGeometry.WalkRay(
                map,
                tile,
                heading,
                reach,
                strictCorners,
                swept);
            foreach (Position position in swept)
                lanes.Add(position);
        }
        return lanes;
    }

    /// <summary>
    /// Tiles currently swept by an enemy gun: every lane of a visible enemy
    /// fortress, plus the muzzle lane of a facing-locked mobile enemy. Crossing
    /// one is sometimes correct; doing it by accident never is.
    /// </summary>
    public static HashSet<Position> HotTiles(
        ContractLens lens,
        GenericActorContext context)
    {
        var hot = new HashSet<Position>();
        var swept = new List<Position>();
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            GenericActorRulesContract.Form? form = lens.Form(enemy.FormId);
            GenericActorRulesContract.AttackProfile? attack = lens.Attack(form);
            if (attack is null)
                continue;
            int range = attack.Projectile.MaxTravelTiles;
            bool strict = attack.Projectile.DiagonalCornersMustBeClear;
            if (attack.OmnidirectionalAim || lens.IsStatic(enemy.FormId))
            {
                foreach (ProjectileHeading heading in ArenaGeometry.Headings)
                {
                    ArenaGeometry.WalkRay(
                        lens.Map,
                        enemy.Position,
                        heading,
                        range,
                        strict,
                        swept);
                    foreach (Position tile in swept)
                        hot.Add(tile);
                }
                continue;
            }
            ArenaGeometry.WalkRay(
                lens.Map,
                enemy.Position,
                enemy.Facing.ToProjectileHeading(),
                range,
                strict,
                swept);
            foreach (Position tile in swept)
                hot.Add(tile);
        }
        return hot;
    }
}
