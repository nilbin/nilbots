using BotArena.Sdk;

/// <summary>
/// Static walkability view of the resolved map contract plus the two search
/// primitives Still Water needs: cardinal cost fields and open-tile probing.
/// Nothing here knows the map by name; every value comes from
/// <see cref="GenericActorMapContract"/>.
/// </summary>
internal sealed class Field
{
    private readonly bool[] _wall;

    public Field(GenericActorMapContract map)
    {
        Width = map.Width;
        Height = map.Height;
        _wall = new bool[Width * Height];
        for (int y = 0; y < Height; y++)
        {
            string row = map.TileRows[y];
            for (int x = 0; x < Width; x++)
                _wall[(y * Width) + x] = row[x] == '#';
        }
    }

    public int Width { get; }

    public int Height { get; }

    public static readonly Direction[] Cardinals =
    [
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West,
    ];

    public bool InBounds(Position p) =>
        p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

    public bool IsWall(Position p) => !InBounds(p) || _wall[(p.Y * Width) + p.X];

    public bool IsOpen(Position p) => !IsWall(p);

    public int Index(Position p) => (p.Y * Width) + p.X;

    /// <summary>Cardinal breadth-first cost to the nearest goal tile.</summary>
    public int[] CostField(
        IEnumerable<Position> goals,
        IReadOnlySet<Position>? blocked)
    {
        int[] cost = new int[Width * Height];
        for (int i = 0; i < cost.Length; i++)
            cost[i] = int.MaxValue;

        var queue = new Queue<Position>();
        foreach (Position goal in goals)
        {
            if (IsWall(goal))
                continue;
            int index = Index(goal);
            if (cost[index] == int.MaxValue)
            {
                cost[index] = 0;
                queue.Enqueue(goal);
            }
        }

        while (queue.Count > 0)
        {
            Position current = queue.Dequeue();
            int next = cost[Index(current)] + 1;
            foreach (Direction direction in Cardinals)
            {
                (int dx, int dy) = direction.Vector();
                Position neighbour = current.Offset(dx, dy);
                if (IsWall(neighbour))
                    continue;
                if (blocked is not null && blocked.Contains(neighbour))
                    continue;
                int index = Index(neighbour);
                if (cost[index] <= next)
                    continue;
                cost[index] = next;
                queue.Enqueue(neighbour);
            }
        }
        return cost;
    }

    public int Cost(int[] field, Position p) =>
        InBounds(p) ? field[Index(p)] : int.MaxValue;

    /// <summary>
    /// Nearest open tile to <paramref name="wanted"/>, searched outward in a
    /// bounded ring so a station never lands inside a wall.
    /// </summary>
    public Position NearestOpen(
        Position wanted,
        int maxRadius,
        Func<Position, bool>? acceptable = null)
    {
        for (int radius = 0; radius <= maxRadius; radius++)
        {
            Position best = default;
            bool found = false;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                        continue;
                    Position candidate = wanted.Offset(dx, dy);
                    if (!IsOpen(candidate))
                        continue;
                    if (acceptable is not null && !acceptable(candidate))
                        continue;
                    if (!found
                        || Better(candidate, best, wanted))
                    {
                        best = candidate;
                        found = true;
                    }
                }
            }
            if (found)
                return best;
        }
        return wanted;
    }

    private static bool Better(Position candidate, Position current, Position wanted)
    {
        int candidateCost =
            Math.Abs(candidate.X - wanted.X) + Math.Abs(candidate.Y - wanted.Y);
        int currentCost =
            Math.Abs(current.X - wanted.X) + Math.Abs(current.Y - wanted.Y);
        if (candidateCost != currentCost)
            return candidateCost < currentCost;
        return candidate.Y != current.Y
            ? candidate.Y < current.Y
            : candidate.X < current.X;
    }

    /// <summary>Unobstructed eight-way ray between two tiles, corners included.</summary>
    public bool ClearRay(Position from, Position to, bool strictCorners)
    {
        int stepX = Math.Sign(to.X - from.X);
        int stepY = Math.Sign(to.Y - from.Y);
        Position cursor = from;
        int guard = 0;
        while (cursor != to && guard++ <= 64)
        {
            Position next = cursor.Offset(stepX, stepY);
            if (next != to && IsWall(next))
                return false;
            if (strictCorners
                && stepX != 0
                && stepY != 0
                && (IsWall(cursor.Offset(stepX, 0))
                    || IsWall(cursor.Offset(0, stepY))))
            {
                return false;
            }
            cursor = next;
        }
        return cursor == to;
    }
}
