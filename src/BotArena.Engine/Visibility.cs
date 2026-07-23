namespace BotArena.Engine;

/// <summary>
/// Field-of-view rules (plan §4.5): Chebyshev range limit, walls block sight, line of sight is
/// computed with a corner-strict integer supercover line so results are exact, symmetric and
/// platform-independent. See docs/DECISIONS.md for the corner rule.
/// </summary>
public static class Visibility
{
    /// <summary>A tile is visible when every supercover tile strictly between origin and target is not a wall.
    /// Wall tiles themselves are visible (you can see the wall, not through it).</summary>
    public static bool HasLineOfSight(ArenaMap map, Position origin, Position target)
    {
        foreach (var p in SupercoverLine(origin, target))
        {
            if (p == origin || p == target)
                continue;
            if (map.IsWall(p))
                return false;
        }
        return true;
    }

    public static List<Position> ComputeVisibleTiles(ArenaMap map, Position origin, int range)
        => ComputeVisibleTiles(map, origin, range, facing: null);

    /// <summary>With a facing, sight is directional (RULES-0.5-DESIGN §A): the 90°
    /// quadrant toward the facing (forward ≥ 0 and |lateral| ≤ forward, diagonal edges
    /// included) plus a Chebyshev-1 omnidirectional proximity ring. Range and
    /// corner-strict LOS apply in both modes.</summary>
    public static List<Position> ComputeVisibleTiles(ArenaMap map, Position origin, int range, Direction? facing)
    {
        var result = new List<Position>();
        for (int y = origin.Y - range; y <= origin.Y + range; y++)
        {
            for (int x = origin.X - range; x <= origin.X + range; x++)
            {
                if (x < 0 || y < 0 || x >= map.Width || y >= map.Height)
                    continue;
                var target = new Position(x, y);
                if (facing is Direction cone && !InCone(origin, target, cone))
                    continue;
                if (HasLineOfSight(map, origin, target))
                    result.Add(target);
            }
        }
        return result;
    }

    /// <summary>Cone membership: inside the facing quadrant, or within the Chebyshev-1
    /// proximity ring (point-blank is never blind).</summary>
    public static bool InCone(Position origin, Position target, Direction facing)
    {
        int dx = target.X - origin.X, dy = target.Y - origin.Y;
        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) <= 1)
            return true;
        var (fx, fy) = facing.Vector();
        int forward = dx * fx + dy * fy;
        int lateral = Math.Abs(dx * fy) + Math.Abs(dy * fx); // one term is always zero
        return forward >= 0 && lateral <= forward;
    }

    /// <summary>
    /// Integer supercover line: yields every tile the ideal segment between tile centers passes
    /// through. When the segment crosses exactly through a tile corner, both side-adjacent tiles
    /// are yielded as well ("corner-strict"), so vision cannot slip diagonally between two walls.
    /// The visited set is symmetric under swapping the endpoints.
    /// </summary>
    public static IEnumerable<Position> SupercoverLine(Position a, Position b)
    {
        int dx = b.X - a.X, dy = b.Y - a.Y;
        int nx = Math.Abs(dx), ny = Math.Abs(dy);
        int sx = Math.Sign(dx), sy = Math.Sign(dy);
        int x = a.X, y = a.Y;
        yield return a;
        int ix = 0, iy = 0;
        while (ix < nx || iy < ny)
        {
            long crossX = (long)(1 + 2 * ix) * ny;
            long crossY = (long)(1 + 2 * iy) * nx;
            if (crossX == crossY)
            {
                // Exact corner crossing: expose both side tiles, then step diagonally.
                yield return new Position(x + sx, y);
                yield return new Position(x, y + sy);
                x += sx;
                y += sy;
                ix++;
                iy++;
            }
            else if (crossX < crossY)
            {
                x += sx;
                ix++;
            }
            else
            {
                y += sy;
                iy++;
            }
            yield return new Position(x, y);
        }
    }
}
