using BotArena.Sdk;

/// <summary>
/// Where the bodies stand relative to each other, which is the part of "mass on
/// the objective" that revision 3 did not price.
///
/// Under a control policy where surplus objective weight scales capture
/// pressure, a second body on the objective is worth real progress - and
/// revision 3 correctly started sending it. What it did not read is that weight
/// stacked into <b>one lane</b> is not the same asset as weight spread around
/// the region:
///
/// <list type="bullet">
/// <item>a bolt is a ray, and it stops on the first enemy body it meets, so two
/// of ours on one ray are two chances for the same bolt and only one target for
/// the other side's aim;</item>
/// <item>a fan launches along a facing lane and its neighbours at once, so a
/// column of bodies in a lane is the shape the fan was built to punish;</item>
/// <item>a guard's arc is a fixed quadrant that never tracks, so pressure from
/// two bearings always has a side the shield is not facing;</item>
/// <item>bodies block bodies. Five arrivals converging on a four-tile region by
/// the shortest route spend their ticks refusing each other's steps.</item>
/// </list>
///
/// So this is the exchange rate on a tile: how many allies already share its
/// firing lane, whether it sits in a lane something is already aimed down, and
/// whether its bearing onto the contested ground is one nobody else is
/// covering. Every input is observed or read - allied bodies from team
/// perception, reach from the opposing forms' own attack profiles, fan lanes
/// from a declared <c>volley</c>. Nothing here is tuned to an arm, and on a
/// three-slot contract with one companion the terms are all but inert because
/// there is nobody to be crowded by.
/// </summary>
internal static class Bearings
{
    /// <summary>
    /// Allied bodies that share a clear straight eight-way ray with
    /// <paramref name="tile"/> inside <paramref name="reach"/> tiles - the
    /// bodies one enemy bolt line can threaten together. Self is excluded by the
    /// caller passing only allies.
    /// </summary>
    public static int LaneShare(
        MatchLens lens,
        Position tile,
        IReadOnlyList<Position> allies,
        int reach)
    {
        int shared = 0;
        foreach (Position ally in allies)
        {
            if (ally == tile)
                continue;
            if (tile.ChebyshevDistance(ally) > reach)
                continue;
            if (Field.RayReaches(lens, tile, ally))
                shared++;
        }
        return shared;
    }

    /// <summary>
    /// Eight-way bearing sector from <paramref name="focus"/> out to
    /// <paramref name="tile"/>, or -1 when the two coincide. Sectors are the
    /// coarse "which side of the objective is this" question, so two bodies in
    /// different sectors are pressure from two bearings even when neither is on
    /// a clean ray.
    /// </summary>
    public static int Sector(Position focus, Position tile)
    {
        int dx = tile.X - focus.X;
        int dy = tile.Y - focus.Y;
        if (dx == 0 && dy == 0)
            return -1;
        // Eight octants, split on the 2:1 boundary so a cardinal keeps a full
        // octant rather than only its exact ray.
        int ax = Math.Abs(dx);
        int ay = Math.Abs(dy);
        bool diagonal = ax * 2 > ay && ay * 2 > ax;
        if (!diagonal)
        {
            return ax >= ay
                ? dx > 0 ? 2 : 6
                : dy > 0 ? 4 : 0;
        }
        return dx > 0
            ? dy > 0 ? 3 : 1
            : dy > 0 ? 5 : 7;
    }

    /// <summary>
    /// Bearing sectors around <paramref name="focus"/> that live allied bodies
    /// already cover. A body arriving into an uncovered sector adds a bearing;
    /// one arriving into a covered sector adds only weight.
    /// </summary>
    public static HashSet<int> CoveredSectors(
        Position focus,
        IReadOnlyList<Position> allies)
    {
        var sectors = new HashSet<int>();
        foreach (Position ally in allies)
        {
            int sector = Sector(focus, ally);
            if (sector >= 0)
                sectors.Add(sector);
        }
        return sectors;
    }

    /// <summary>
    /// What standing on <paramref name="tile"/> costs in shape rather than in
    /// ground: lanes shared with allies, a lane something is already aimed down,
    /// and a bearing that adds nothing to the envelope. Positive is worse, and
    /// every term is scaled so that a tile of actual objective presence still
    /// outranks any of them - this decides WHICH contested tile to hold, never
    /// whether to hold one.
    /// </summary>
    public static int Crowding(
        MatchLens lens,
        Stances stances,
        Position tile,
        Position focus,
        IReadOnlyList<Position> allies,
        IReadOnlySet<int> coveredSectors)
    {
        int score = LaneShare(lens, tile, allies, lens.LongestEnemyReach) * 3;
        if (stances.FanLanes.Contains(tile))
            score += 6;
        int sector = Sector(focus, tile);
        if (sector >= 0 && coveredSectors.Contains(sector))
            score += 2;
        return score;
    }

    /// <summary>
    /// Live allied body tiles, which is the only honest input for "am I
    /// crowding somebody": a slot roster says what the team owns, not where it
    /// is standing.
    /// </summary>
    public static List<Position> AllyTiles(GenericActorContext context)
    {
        var tiles = new List<Position>(context.Allies.Length);
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
            tiles.Add(ally.Position);
        return tiles;
    }
}
