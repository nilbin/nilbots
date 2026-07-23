namespace BotArena.Engine;

/// <summary>
/// Seed-derived spawn positions (GAME-DESIGN: "seeds should vary battles"). When
/// <see cref="GameRules.SeedSpawnVariation"/> is on, the two spawns become a
/// deterministic function of map + rules version + match seed: a distance-constrained,
/// connected floor pair, facing each other. Mirrored ranked games share the seed, so
/// both bots play the same geometry from both sides — fairness is preserved by the set
/// format, not by symmetric maps.
/// </summary>
public static class SpawnVariation
{
    /// <summary>Maximum allowed difference in walking distance to the zone between the
    /// two spawns under <see cref="GameRules.ZoneSpawnFairness"/>.</summary>
    public const int ZoneDistanceTolerance = 2;

    public static IReadOnlyList<Spawn> Resolve(ArenaMap map, GameRules rules, ulong matchSeed)
    {
        if (!rules.SeedSpawnVariation)
            return map.Spawns;

        var random = new DeterministicRandom(
            SeedDerivation.DeriveSpawnSeed(matchSeed, rules.RulesVersion));
        var floors = new List<Position>();
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                if (!map.IsWall(x, y))
                    floors.Add(new Position(x, y));

        int[]? zoneDistance = rules.ZoneControl && rules.ZoneSpawnFairness
            ? ZoneDistanceField(map)
            : null;

        int minDistance = Math.Max(map.Width, map.Height) / 2;
        for (int attempt = 0; attempt < rules.SpawnAttempts; attempt++)
        {
            var a = floors[random.NextInt(0, floors.Count)];
            var b = floors[random.NextInt(0, floors.Count)];
            if (a.ChebyshevDistance(b) < minDistance)
                continue;
            if (!map.AreConnected(a, b))
                continue;
            if (rules.SpawnLaneSafety && SharesClearLane(map, a, b, rules.ShotRange))
                continue; // no tick-0 firing lanes between spawns (gen-3 finding)
            if (zoneDistance is not null)
            {
                int da = zoneDistance[a.Y * map.Width + a.X];
                int db = zoneDistance[b.Y * map.Width + b.X];
                if (da == int.MaxValue || db == int.MaxValue || Math.Abs(da - db) > ZoneDistanceTolerance)
                    continue; // neither bot may start the hill race meaningfully ahead
            }
            return [new Spawn(a.X, a.Y, FacingToward(a, b)), new Spawn(b.X, b.Y, FacingToward(b, a))];
        }
        // Deterministic fallback for degenerate maps: the fixed spawns are always valid.
        return map.Spawns;
    }

    /// <summary>Walking distance (4-neighbor BFS over floor, matching orthogonal
    /// movement) from every tile to its nearest zone tile; int.MaxValue = unreachable.</summary>
    private static int[] ZoneDistanceField(ArenaMap map)
    {
        var distance = new int[map.Width * map.Height];
        Array.Fill(distance, int.MaxValue);
        var queue = new Queue<Position>();
        foreach (var tile in map.EffectiveZone())
        {
            distance[tile.Y * map.Width + tile.X] = 0;
            queue.Enqueue(tile);
        }
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            int next = distance[current.Y * map.Width + current.X] + 1;
            foreach (var (dx, dy) in Steps)
            {
                var neighbor = current.Offset(dx, dy);
                if (neighbor.X < 0 || neighbor.Y < 0 || neighbor.X >= map.Width || neighbor.Y >= map.Height)
                    continue;
                int index = neighbor.Y * map.Width + neighbor.X;
                if (map.IsWall(neighbor) || distance[index] <= next)
                    continue;
                distance[index] = next;
                queue.Enqueue(neighbor);
            }
        }
        return distance;
    }

    private static readonly (int Dx, int Dy)[] Steps = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <summary>True when the two tiles share a row/column with no wall between them,
    /// within firing range (range 0 = unlimited) — i.e. a tick-0 shot could connect.</summary>
    private static bool SharesClearLane(ArenaMap map, Position a, Position b, int shotRange)
    {
        if (a.X != b.X && a.Y != b.Y)
            return false;
        int distance = Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        if (shotRange > 0 && distance > shotRange)
            return false;
        int stepX = Math.Sign(b.X - a.X), stepY = Math.Sign(b.Y - a.Y);
        var current = a;
        while (true)
        {
            current = current.Offset(stepX, stepY);
            if (current == b)
                return true;
            if (map.IsWall(current))
                return false;
        }
    }

    /// <summary>Dominant-axis facing toward the opponent; ties (perfect diagonals)
    /// resolve horizontally so the choice is deterministic.</summary>
    private static Direction FacingToward(Position from, Position to)
    {
        int dx = to.X - from.X, dy = to.Y - from.Y;
        return Math.Abs(dx) >= Math.Abs(dy)
            ? dx >= 0 ? Direction.East : Direction.West
            : dy >= 0 ? Direction.South : Direction.North;
    }
}
