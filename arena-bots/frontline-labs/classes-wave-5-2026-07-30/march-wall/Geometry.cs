using BotArena.Sdk;

/// <summary>
/// Pure tile geometry shared by the doctrine. Nothing here reads a rules value;
/// every range, corner rule and travel budget is passed in by the caller from
/// the resolved contract.
/// </summary>
internal static class Geometry
{
    public static readonly Direction[] Cardinals =
    [
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West,
    ];

    public static Position Step(Position position, Direction direction)
    {
        (int dx, int dy) = direction.Vector();
        return position.Offset(dx, dy);
    }

    public static int Chebyshev(Position a, Position b) =>
        a.ChebyshevDistance(b);

    /// <summary>Cardinal steps between two tiles, ignoring walls.</summary>
    public static int Manhattan(Position a, Position b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    /// <summary>
    /// Resolves the eight-way heading from <paramref name="source"/> to
    /// <paramref name="target"/>, or reports false when no straight ray exists.
    /// </summary>
    public static bool TryRay(
        Position source,
        Position target,
        out ProjectileHeading heading,
        out int distance)
    {
        heading = default;
        int dx = target.X - source.X;
        int dy = target.Y - source.Y;
        distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (distance == 0)
            return false;
        if (dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy))
            return false;

        heading = (Math.Sign(dx), Math.Sign(dy)) switch
        {
            (0, -1) => ProjectileHeading.North,
            (1, -1) => ProjectileHeading.NorthEast,
            (1, 0) => ProjectileHeading.East,
            (1, 1) => ProjectileHeading.SouthEast,
            (0, 1) => ProjectileHeading.South,
            (-1, 1) => ProjectileHeading.SouthWest,
            (-1, 0) => ProjectileHeading.West,
            (-1, -1) => ProjectileHeading.NorthWest,
            _ => default,
        };
        return true;
    }

    /// <summary>
    /// True when a straight ray from source to target crosses only open tiles.
    /// Diagonal steps are checked against both orthogonal corners, matching the
    /// strictest declared projectile rule.
    /// </summary>
    public static bool ClearRay(
        Func<Position, bool> isWall,
        Position source,
        Position target)
    {
        int stepX = Math.Sign(target.X - source.X);
        int stepY = Math.Sign(target.Y - source.Y);
        Position cursor = source;
        int guard = 0;
        while (cursor != target && guard++ < 512)
        {
            Position next = cursor.Offset(stepX, stepY);
            if (next != target && isWall(next))
                return false;
            if (stepX != 0
                && stepY != 0
                && (isWall(cursor.Offset(stepX, 0))
                    || isWall(cursor.Offset(0, stepY))))
            {
                return false;
            }
            cursor = next;
        }
        return true;
    }

    /// <summary>
    /// True when this tile is a one-tile corridor: exactly two of its four
    /// cardinal neighbours are open and they lie on opposite sides, so a body
    /// standing here is the only way through. Bodies block bodies, so a
    /// stationary body on such a tile is a physical gate rather than merely a
    /// gun covering one.
    ///
    /// <para>It is a shape test and nothing else — no rules value, no tile tag,
    /// no coordinate. On a map with no pinch points it simply never answers
    /// true, and the doctrine that asks falls through to covering ground with
    /// fire.</para>
    /// </summary>
    public static bool IsCorridor(Func<Position, bool> isWall, Position tile)
    {
        if (isWall(tile))
            return false;
        bool north = !isWall(Step(tile, Direction.North));
        bool south = !isWall(Step(tile, Direction.South));
        bool east = !isWall(Step(tile, Direction.East));
        bool west = !isWall(Step(tile, Direction.West));
        int open = (north ? 1 : 0) + (south ? 1 : 0) + (east ? 1 : 0)
            + (west ? 1 : 0);
        if (open != 2)
            return false;
        return (north && south) || (east && west);
    }

    /// <summary>
    /// Counts how many of <paramref name="tiles"/> a stationary shooter at
    /// <paramref name="from"/> could reach with an unobstructed straight ray
    /// inside <paramref name="maxTravelTiles"/>.
    /// </summary>
    public static int Coverage(
        Func<Position, bool> isWall,
        Position from,
        IEnumerable<Position> tiles,
        int maxTravelTiles)
    {
        int covered = 0;
        foreach (Position tile in tiles)
        {
            if (!TryRay(from, tile, out _, out int distance))
                continue;
            if (distance > maxTravelTiles)
                continue;
            if (ClearRay(isWall, from, tile))
                covered++;
        }
        return covered;
    }
}
