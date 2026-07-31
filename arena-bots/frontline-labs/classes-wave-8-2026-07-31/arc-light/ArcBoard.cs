using BotArena.Sdk;

/// <summary>
/// Route and ray geometry. The router plans on map geometry and then spends a
/// rotation when the legality mask refuses the planned step, because under a
/// facing-locked coupling the mask offers only the current facing and a search
/// seeded from it prunes every route that needs a turn.
/// </summary>
internal static class ArcBoard
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

    public static Position Step(Position position, ProjectileHeading heading)
    {
        (int dx, int dy) = heading.Vector();
        return position.Offset(dx, dy);
    }

    public static Direction Opposite(Direction direction) =>
        direction switch
        {
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.East => Direction.West,
            _ => Direction.East,
        };

    /// <summary>
    /// Whether <paramref name="target"/> lies inside the facing quadrant of a
    /// body at <paramref name="origin"/>. This is the shape a mobile sensor
    /// sees, and it is also the arc a projectile guard protects, so the same
    /// predicate answers "can it see me" and "will it deflect this".
    /// </summary>
    public static bool InFacingQuadrant(
        Position origin,
        Direction facing,
        Position target)
    {
        int dx = target.X - origin.X;
        int dy = target.Y - origin.Y;
        return facing switch
        {
            Direction.North => dy < 0 && Math.Abs(dx) <= -dy,
            Direction.South => dy > 0 && Math.Abs(dx) <= dy,
            Direction.East => dx > 0 && Math.Abs(dy) <= dx,
            _ => dx < 0 && Math.Abs(dy) <= -dx,
        };
    }

    /// <summary>
    /// The eight-way heading from <paramref name="source"/> to
    /// <paramref name="target"/> when one exists, plus its Chebyshev length.
    /// </summary>
    public static bool TryHeading(
        Position source,
        Position target,
        out ProjectileHeading heading,
        out int distance)
    {
        int dx = target.X - source.X;
        int dy = target.Y - source.Y;
        distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
        heading = default;
        if (distance == 0
            || (dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy)))
        {
            return false;
        }
        heading = FromStep(Math.Sign(dx), Math.Sign(dy));
        return true;
    }

    public static ProjectileHeading FromStep(int stepX, int stepY) =>
        (stepX, stepY) switch
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

    /// <summary>
    /// The first step of a shortest wall-only route to <paramref name="goals"/>,
    /// with <paramref name="blockedFirstStep"/> applying to the first step only:
    /// bodies and bolts move, and treating them as permanent walls surrenders
    /// routes that will be open long before this body arrives. Walls block at
    /// every depth because walls are the only thing that does.
    /// </summary>
    public static Direction? FirstStep(
        ArcFacts facts,
        Position start,
        IReadOnlySet<Position> goals,
        IReadOnlySet<Position> blockedFirstStep,
        Direction[] order)
    {
        if (goals.Count == 0 || goals.Contains(start))
            return null;

        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Position, Direction First)>();
        foreach (Direction direction in order)
        {
            Position next = Step(start, direction);
            if (facts.Impassable(next)
                || blockedFirstStep.Contains(next)
                || !visited.Add(next))
            {
                continue;
            }
            if (goals.Contains(next))
                return direction;
            queue.Enqueue((next, direction));
        }

        while (queue.Count > 0)
        {
            (Position position, Direction first) = queue.Dequeue();
            foreach (Direction direction in order)
            {
                Position next = Step(position, direction);
                if (facts.Impassable(next) || !visited.Add(next))
                    continue;
                if (goals.Contains(next))
                    return first;
                queue.Enqueue((next, first));
            }
        }
        return null;
    }

    /// <summary>
    /// Wall-only step distance from <paramref name="start"/> to the nearest of
    /// <paramref name="goals"/>, or null when unreachable. Used to price
    /// positions rather than to walk them.
    /// </summary>
    public static int? StepDistance(
        ArcFacts facts,
        Position start,
        IReadOnlyCollection<Position> goals,
        int limit)
    {
        if (goals.Count == 0)
            return null;
        var goalSet = goals.ToHashSet();
        if (goalSet.Contains(start))
            return 0;
        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Position, int Depth)>();
        queue.Enqueue((start, 0));
        while (queue.Count > 0)
        {
            (Position position, int depth) = queue.Dequeue();
            if (depth >= limit)
                continue;
            foreach (Direction direction in Cardinals)
            {
                Position next = Step(position, direction);
                if (facts.Impassable(next) || !visited.Add(next))
                    continue;
                if (goalSet.Contains(next))
                    return depth + 1;
                queue.Enqueue((next, depth + 1));
            }
        }
        return null;
    }

    /// <summary>
    /// Whether an eight-way ray from <paramref name="source"/> to
    /// <paramref name="target"/> is clear of walls, honouring the contract's
    /// strict-diagonal-corner rule when it declares one.
    /// </summary>
    public static bool ClearRay(
        ArcFacts facts,
        Position source,
        Position target,
        bool strictCorners)
    {
        int stepX = Math.Sign(target.X - source.X);
        int stepY = Math.Sign(target.Y - source.Y);
        Position cursor = source;
        while (cursor != target)
        {
            Position next = cursor.Offset(stepX, stepY);
            if (next != target && facts.IsWall(next))
                return false;
            if (strictCorners
                && stepX != 0
                && stepY != 0
                && (facts.IsWall(cursor.Offset(stepX, 0))
                    || facts.IsWall(cursor.Offset(0, stepY))))
            {
                return false;
            }
            cursor = next;
        }
        return true;
    }
}
