using BotArena.Sdk;

/// <summary>
/// Geometry the doctrine needs: reachability fields, straight rays, the
/// engine's own programmed-shot path rule, and projectile threat projection.
/// Every bound (range, tiles per advance, bend envelope, corner strictness)
/// is supplied by the caller from the contract rather than assumed here.
/// </summary>
internal static class Tactics
{
    /// <summary>Absolute cardinal directions in canonical order.</summary>
    public static readonly Direction[] Cardinals =
    [
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West,
    ];

    /// <summary>Unreachable marker used by <see cref="DistanceField"/>.</summary>
    public const int Unreachable = int.MaxValue / 4;

    /// <summary>
    /// Breadth-first tile distance to the nearest goal, ignoring transient
    /// bodies so that a temporarily crowded corridor never looks impassable.
    /// </summary>
    public static int[] DistanceField(
        ContractLens lens,
        IEnumerable<Position> goals)
    {
        int width = lens.Width;
        int[] field = new int[width * lens.Height];
        for (int index = 0; index < field.Length; index++)
            field[index] = Unreachable;

        var queue = new Queue<Position>();
        foreach (Position goal in goals)
        {
            if (lens.IsClosed(goal))
                continue;
            int index = (goal.Y * width) + goal.X;
            if (field[index] != 0)
            {
                field[index] = 0;
                queue.Enqueue(goal);
            }
        }

        while (queue.Count > 0)
        {
            Position current = queue.Dequeue();
            int next = field[(current.Y * width) + current.X] + 1;
            foreach (Direction direction in Cardinals)
            {
                (int dx, int dy) = direction.Vector();
                Position neighbour = current.Offset(dx, dy);
                if (lens.IsClosed(neighbour))
                    continue;
                int index = (neighbour.Y * width) + neighbour.X;
                if (field[index] <= next)
                    continue;
                field[index] = next;
                queue.Enqueue(neighbour);
            }
        }
        return field;
    }

    /// <summary>Reads a distance field, treating off-map tiles as unreachable.</summary>
    public static int DistanceAt(ContractLens lens, int[] field, Position tile) =>
        lens.InBounds(tile) ? field[(tile.Y * lens.Width) + tile.X] : Unreachable;

    /// <summary>Chebyshev distance to the nearest tile of a set.</summary>
    public static int NearestChebyshev(
        Position from,
        IReadOnlyList<Position> tiles)
    {
        int best = Unreachable;
        for (int index = 0; index < tiles.Count; index++)
            best = Math.Min(best, from.ChebyshevDistance(tiles[index]));
        return best;
    }

    /// <summary>
    /// Absolute heading and tile distance from source to target when the two
    /// share a straight eight-way line; false otherwise.
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

        heading = HeadingOf(Math.Sign(dx), Math.Sign(dy));
        return true;
    }

    /// <summary>Absolute heading for a unit step, defaulting to north.</summary>
    public static ProjectileHeading HeadingOf(int stepX, int stepY) =>
        (stepX, stepY) switch
        {
            (0, -1) => ProjectileHeading.North,
            (1, -1) => ProjectileHeading.NorthEast,
            (1, 0) => ProjectileHeading.East,
            (1, 1) => ProjectileHeading.SouthEast,
            (0, 1) => ProjectileHeading.South,
            (-1, 1) => ProjectileHeading.SouthWest,
            (-1, 0) => ProjectileHeading.West,
            (-1, -1) => ProjectileHeading.NorthWest,
            _ => ProjectileHeading.North,
        };

    /// <summary>
    /// True when a straight eight-way line from source to target is walkable
    /// by a projectile: no wall on the way and, when the profile requires it,
    /// no blocked diagonal corner. Allocation-free, so it is safe to call from
    /// inside loops that are already using the shared path buffer.
    /// </summary>
    public static bool ClearRay(
        ContractLens lens,
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
            if (lens.IsWall(next))
                return false;
            if (strictCorners
                && stepX != 0
                && stepY != 0
                && (lens.IsWall(cursor.Offset(stepX, 0))
                    || lens.IsWall(cursor.Offset(0, stepY))))
            {
                return false;
            }
            cursor = next;
        }
        return true;
    }

    /// <summary>
    /// True when a hostile bolt's own declared remaining travel budget can
    /// still put it on the tile. This is the difference between a shot that
    /// merely points at you and one that can actually arrive: apparent heading
    /// says yes long after the declared remainder has run out.
    /// </summary>
    public static bool WithinDeclaredRemaining(
        ContractLens lens,
        GenericActorContext.ObservedProjectile projectile,
        Position target,
        bool strictCorners)
    {
        if (projectile.Position == target)
            return true;
        (int dx, int dy) = projectile.Heading.Vector();
        Position cursor = projectile.Position;
        int remaining = projectile.RemainingTiles;
        while (remaining > 0)
        {
            Position next = cursor.Offset(dx, dy);
            if (lens.IsWall(next))
                return false;
            if (strictCorners
                && dx != 0
                && dy != 0
                && (lens.IsWall(cursor.Offset(dx, 0))
                    || lens.IsWall(cursor.Offset(0, dy))))
            {
                return false;
            }
            cursor = next;
            remaining--;
            if (cursor == target)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Walks a straight absolute heading from an origin and writes the entered
    /// tiles into <paramref name="path"/>. Traversal terminates strictly on
    /// walls and, when required, on blocked diagonal corners.
    /// </summary>
    public static int WalkHeading(
        ContractLens lens,
        Position origin,
        ProjectileHeading heading,
        int maxTiles,
        bool strictCorners,
        Position[] path)
    {
        (int dx, int dy) = heading.Vector();
        Position cursor = origin;
        int count = 0;
        int limit = Math.Min(maxTiles, path.Length);
        while (count < limit)
        {
            Position next = cursor.Offset(dx, dy);
            if (lens.IsWall(next))
                break;
            if (strictCorners
                && dx != 0
                && dy != 0
                && (lens.IsWall(cursor.Offset(dx, 0))
                    || lens.IsWall(cursor.Offset(0, dy))))
            {
                break;
            }
            cursor = next;
            path[count++] = cursor;
        }
        return count;
    }

    /// <summary>
    /// Replays the host's programmed-shot path rule locally so the bot only
    /// commits to trajectories it has already validated against wall geometry.
    /// </summary>
    public static int WalkProgram(
        ContractLens lens,
        Position origin,
        Direction facing,
        ShotProgram program,
        int maxTiles,
        bool strictCorners,
        Position[] path)
    {
        ProjectileHeading heading = facing
            .ToProjectileHeading()
            .Turned(program.InitialAimOffset);
        int bendEvery = Math.Max(1, program.BendEveryTiles);
        Position cursor = origin;
        int turns = 0;
        int count = 0;
        int limit = Math.Min(maxTiles, path.Length);
        for (int travelled = 0; count < limit; travelled++)
        {
            bool shouldBend = turns < program.BendCount
                && travelled >= program.BendAfterTiles
                && (travelled - program.BendAfterTiles) % bendEvery == 0;
            if (shouldBend)
            {
                heading = heading.Turned(program.BendDirection);
                turns++;
            }

            (int dx, int dy) = heading.Vector();
            Position next = cursor.Offset(dx, dy);
            if (lens.IsWall(next))
                break;
            if (strictCorners
                && dx != 0
                && dy != 0
                && (lens.IsWall(cursor.Offset(dx, 0))
                    || lens.IsWall(cursor.Offset(0, dy))))
            {
                break;
            }
            cursor = next;
            path[count++] = cursor;
        }
        return count;
    }

    /// <summary>
    /// Ticks after launch at which a projectile occupies the given zero-based
    /// path tile, using the contract's launch and advance cadence.
    /// </summary>
    public static int ArrivalTick(
        GenericActorRulesContract.Projectile projectile,
        int pathIndex)
    {
        int perAdvance = Math.Max(1, projectile.TilesPerAdvance);
        int travelled = pathIndex + 1 - Math.Max(0, projectile.LaunchTiles);
        if (travelled <= 0)
            return 0;
        int advances = (travelled + perAdvance - 1) / perAdvance;
        return advances * Math.Max(1, projectile.TicksPerAdvance);
    }

    /// <summary>
    /// True when a visible projectile can reach the tile within the given
    /// number of its own advances along its currently revealed heading.
    /// </summary>
    public static bool Threatens(
        ContractLens lens,
        GenericActorContext.ObservedProjectile projectile,
        Position target,
        int advances,
        bool strictCorners)
    {
        if (projectile.Position == target)
            return true;
        (int dx, int dy) = projectile.Heading.Vector();
        Position cursor = projectile.Position;
        int remaining = projectile.RemainingTiles;
        for (int advance = 0; advance < advances; advance++)
        {
            for (int step = 0; step < projectile.TilesPerAdvance; step++)
            {
                if (remaining <= 0)
                    return false;
                Position next = cursor.Offset(dx, dy);
                if (lens.IsWall(next))
                    return false;
                if (strictCorners
                    && dx != 0
                    && dy != 0
                    && (lens.IsWall(cursor.Offset(dx, 0))
                        || lens.IsWall(cursor.Offset(0, dy))))
                {
                    return false;
                }
                cursor = next;
                remaining--;
                if (cursor == target)
                    return true;
            }
        }
        return false;
    }
}
