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
    ///
    /// <para>WAVE 5: THE POINT ITSELF IS A POST when the route allows it. Revision
    /// 4 excluded the scoring surface unconditionally, for two reasons that were
    /// both true then — the anchor route forbade the tags the objective carries,
    /// and a root was a one-way sale so a turret standing on the ground it had
    /// stopped being able to hold was the worst tile on the map. Under an open
    /// placement arm the first reason is simply gone (ask the route), and under a
    /// reversible cycle the second inverts: a turret ON the point physically
    /// denies the tile (actors block actors), sweeps the rest of the surface at
    /// the turret's own cadence, and is one windup from being objective weight
    /// standing exactly where the weight is needed. Zero walking. So the surface
    /// is offered, ranked BELOW an equal-coverage tile beside it — because while
    /// the turret stands there the tile scores nothing, and that price is real.
    /// </para>
    /// </summary>
    public static List<Position> RankSites(
        ContractLens lens,
        Position[] objectiveTiles,
        int range,
        bool strictCorners,
        Position? home,
        GenericActorRulesContract.FormTransition? route = null)
    {
        var ranked =
            new List<(Position Tile, int Cover, int OffPoint, int Near, int Home)>();
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
                    || lens.SpawnProtected.Contains(tile))
                {
                    continue;
                }
                bool onPoint = surface.Contains(tile);
                if (route is null)
                {
                    // No declared route to price: fall back to the map's own
                    // vocabulary and keep off the surface, which is what every
                    // stricter arm resolves to anyway.
                    if (onPoint || lens.TransitionForbidden.Contains(tile))
                        continue;
                }
                else if (!lens.PlacementAllows(route, tile))
                {
                    continue;
                }
                else if (onPoint && !lens.Reversible(route))
                {
                    continue;   // a one-way root must not eat the ground it holds
                }
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
                ranked.Add((tile, cover, onPoint ? 1 : 0, near, homeDistance));
            }
        }

        ranked.Sort(static (left, right) =>
        {
            int cover = right.Cover.CompareTo(left.Cover);
            if (cover != 0)
                return cover;
            int offPoint = left.OffPoint.CompareTo(right.OffPoint);
            if (offPoint != 0)
                return offPoint;
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
            // WAVE 5, THE DEFENSIVE HALF OF THE AIM ENVELOPE. A muzzle that
            // declares an initial aim offset sweeps its facing lane AND both
            // adjacent diagonals with no rotation at all, so a tile one octant
            // off an enemy's nose is hot rather than safe. The width is that
            // enemy's own declared envelope; on a zero-offset arm it collapses to
            // exactly the one lane revision 4 swept.
            int width = Gunnery.LaunchWidth(attack);
            for (int offset = -width; offset <= width; offset++)
            {
                var heading = (ProjectileHeading)(
                    ((int)enemy.Facing.ToProjectileHeading() + offset + 8) % 8);
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
        }
        return hot;
    }
}
