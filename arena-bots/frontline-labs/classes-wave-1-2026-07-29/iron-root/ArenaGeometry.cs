using BotArena.Sdk;

/// <summary>
/// Map arithmetic shared by the doctrine: rays, line of fire, flood-fill
/// distances, and first steps. Everything takes the map contract, so a
/// different map size, wall layout, or objective shape needs no code change.
/// </summary>
internal static class ArenaGeometry
{
    public static readonly Direction[] Cardinals =
    [
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West,
    ];

    public static readonly ProjectileHeading[] Headings =
    [
        ProjectileHeading.North,
        ProjectileHeading.NorthEast,
        ProjectileHeading.East,
        ProjectileHeading.SouthEast,
        ProjectileHeading.South,
        ProjectileHeading.SouthWest,
        ProjectileHeading.West,
        ProjectileHeading.NorthWest,
    ];

    private static readonly HashSet<Position> NoTiles = [];

    public static bool InBounds(GenericActorMapContract map, Position tile) =>
        tile.X >= 0
        && tile.Y >= 0
        && tile.X < map.Width
        && tile.Y < map.Height;

    public static bool IsOpen(GenericActorMapContract map, Position tile) =>
        InBounds(map, tile) && map.TileRows[tile.Y][tile.X] != '#';

    public static Position Step(Position tile, Direction direction)
    {
        (int dx, int dy) = direction.Vector();
        return tile.Offset(dx, dy);
    }

    /// <summary>
    /// True when <paramref name="target"/> lies on one of the eight rays from
    /// <paramref name="source"/>, reporting that heading and its tile distance.
    /// </summary>
    public static bool TryRay(
        Position source,
        Position target,
        out ProjectileHeading heading,
        out int distance)
    {
        int dx = target.X - source.X;
        int dy = target.Y - source.Y;
        distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
        heading = default;
        if (distance == 0)
            return false;
        if (dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy))
            return false;

        int stepX = Math.Sign(dx);
        int stepY = Math.Sign(dy);
        heading = (stepX, stepY) switch
        {
            (0, -1) => ProjectileHeading.North,
            (1, -1) => ProjectileHeading.NorthEast,
            (1, 0) => ProjectileHeading.East,
            (1, 1) => ProjectileHeading.SouthEast,
            (0, 1) => ProjectileHeading.South,
            (-1, 1) => ProjectileHeading.SouthWest,
            (-1, 0) => ProjectileHeading.West,
            _ => ProjectileHeading.NorthWest,
        };
        return true;
    }

    /// <summary>
    /// Whether a projectile fired from <paramref name="source"/> reaches
    /// <paramref name="target"/> without meeting a wall, honouring the declared
    /// strict-diagonal-corner rule.
    /// </summary>
    public static bool ClearRay(
        GenericActorMapContract map,
        Position source,
        Position target,
        bool strictCorners)
    {
        int stepX = Math.Sign(target.X - source.X);
        int stepY = Math.Sign(target.Y - source.Y);
        Position cursor = source;
        int guard = map.Width + map.Height;
        while (cursor != target && guard-- > 0)
        {
            if (strictCorners
                && stepX != 0
                && stepY != 0
                && (!IsOpen(map, cursor.Offset(stepX, 0))
                    || !IsOpen(map, cursor.Offset(0, stepY))))
            {
                return false;
            }
            Position next = cursor.Offset(stepX, stepY);
            if (next != target && !IsOpen(map, next))
                return false;
            cursor = next;
        }
        return cursor == target;
    }

    /// <summary>
    /// Tiles a shot from <paramref name="source"/> along <paramref name="heading"/>
    /// would sweep, stopping at the first wall or the declared range.
    /// </summary>
    public static void WalkRay(
        GenericActorMapContract map,
        Position source,
        ProjectileHeading heading,
        int range,
        bool strictCorners,
        List<Position> sink)
    {
        sink.Clear();
        (int dx, int dy) = heading.Vector();
        Position cursor = source;
        for (int step = 0; step < range; step++)
        {
            if (strictCorners
                && dx != 0
                && dy != 0
                && (!IsOpen(map, cursor.Offset(dx, 0))
                    || !IsOpen(map, cursor.Offset(0, dy))))
            {
                return;
            }
            Position next = cursor.Offset(dx, dy);
            if (!IsOpen(map, next))
                return;
            sink.Add(next);
            cursor = next;
        }
    }

    /// <summary>Breadth-first walking distance from one tile to every reachable tile.</summary>
    public static Dictionary<Position, int> Distances(
        GenericActorMapContract map,
        Position start,
        IReadOnlySet<Position>? blocked = null)
    {
        IReadOnlySet<Position> obstacles = blocked ?? NoTiles;
        var distances = new Dictionary<Position, int> { [start] = 0 };
        var queue = new Queue<Position>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            Position current = queue.Dequeue();
            int next = distances[current] + 1;
            foreach (Direction direction in Cardinals)
            {
                Position neighbour = Step(current, direction);
                if (!IsOpen(map, neighbour)
                    || obstacles.Contains(neighbour)
                    || distances.ContainsKey(neighbour))
                {
                    continue;
                }
                distances[neighbour] = next;
                queue.Enqueue(neighbour);
            }
        }
        return distances;
    }

    /// <summary>
    /// First legal cardinal step of a shortest walking route to any goal tile,
    /// restricted to the directions the current legality mask permits.
    /// </summary>
    public static Direction? FirstStep(
        GenericActorMapContract map,
        Position start,
        IReadOnlySet<Position> goals,
        IReadOnlySet<Position> blocked,
        IReadOnlySet<Direction> allowedFirstSteps)
    {
        if (goals.Count == 0)
            return null;

        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Tile, Direction First)>();
        foreach (Direction direction in Cardinals)
        {
            if (!allowedFirstSteps.Contains(direction))
                continue;
            Position next = Step(start, direction);
            if (!IsOpen(map, next) || blocked.Contains(next) || !visited.Add(next))
                continue;
            if (goals.Contains(next))
                return direction;
            queue.Enqueue((next, direction));
        }

        while (queue.Count > 0)
        {
            (Position tile, Direction first) = queue.Dequeue();
            foreach (Direction direction in Cardinals)
            {
                Position next = Step(tile, direction);
                if (!IsOpen(map, next)
                    || blocked.Contains(next)
                    || !visited.Add(next))
                {
                    continue;
                }
                if (goals.Contains(next))
                    return first;
                queue.Enqueue((next, first));
            }
        }
        return null;
    }

    public static int NearestDistance(
        Position from,
        IReadOnlyList<Position> tiles)
    {
        int best = int.MaxValue;
        foreach (Position tile in tiles)
            best = Math.Min(best, from.ChebyshevDistance(tile));
        return best == int.MaxValue ? 0 : best;
    }

    public static Position Centroid(IReadOnlyList<Position> tiles)
    {
        if (tiles.Count == 0)
            return new Position(0, 0);
        int x = 0;
        int y = 0;
        foreach (Position tile in tiles)
        {
            x += tile.X;
            y += tile.Y;
        }
        return new Position(x / tiles.Count, y / tiles.Count);
    }
}
