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

        int minDistance = Math.Max(map.Width, map.Height) / 2;
        for (int attempt = 0; attempt < 64; attempt++)
        {
            var a = floors[random.NextInt(0, floors.Count)];
            var b = floors[random.NextInt(0, floors.Count)];
            if (a.ChebyshevDistance(b) < minDistance)
                continue;
            if (!map.AreConnected(a, b))
                continue;
            if (rules.SpawnLaneSafety && SharesClearLane(map, a, b, rules.ShotRange))
                continue; // no tick-0 firing lanes between spawns (gen-3 finding)
            return [new Spawn(a.X, a.Y, FacingToward(a, b)), new Spawn(b.X, b.Y, FacingToward(b, a))];
        }
        // Deterministic fallback for degenerate maps: the fixed spawns are always valid.
        return map.Spawns;
    }

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
