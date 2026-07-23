using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public class SpawnVariationTests
{
    private static readonly GameRules SpawnRules = GameRules.V0_1 with
    {
        RulesVersion = "0.2-exp-spawns",
        SeedSpawnVariation = true,
    };

    [Fact]
    public void Disabled_ReturnsMapSpawnsIdentity()
    {
        var map = TestMaps.OpenRoom();
        Assert.Same(map.Spawns, SpawnVariation.Resolve(map, GameRules.V0_1, 42));
    }

    [Fact]
    public void SameSeed_SameSpawns()
    {
        var map = TestMaps.OpenRoom();
        Assert.Equal(SpawnVariation.Resolve(map, SpawnRules, 7), SpawnVariation.Resolve(map, SpawnRules, 7));
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentSpawnPairs()
    {
        var map = TestMaps.OpenRoom();
        var distinct = Enumerable.Range(0, 16)
            .Select(seed => SpawnVariation.Resolve(map, SpawnRules, (ulong)seed))
            .Select(s => (s[0].X, s[0].Y, s[1].X, s[1].Y))
            .Distinct()
            .Count();
        Assert.True(distinct > 4, $"Expected varied spawn pairs across seeds, got {distinct} distinct.");
    }

    [Fact]
    public void Spawns_AreFloorDistantConnectedAndFacingEachOther()
    {
        var map = TestMaps.OpenRoom();
        int minDistance = Math.Max(map.Width, map.Height) / 2;
        for (ulong seed = 0; seed < 50; seed++)
        {
            var spawns = SpawnVariation.Resolve(map, SpawnRules, seed);
            var a = new Position(spawns[0].X, spawns[0].Y);
            var b = new Position(spawns[1].X, spawns[1].Y);
            Assert.False(map.IsWall(a));
            Assert.False(map.IsWall(b));
            Assert.True(a.ChebyshevDistance(b) >= minDistance);
            Assert.True(map.AreConnected(a, b));
            // Facing points along the dominant axis toward the opponent.
            var (dx, dy) = spawns[0].Facing.Vector();
            Assert.True(Math.Sign(dx) == Math.Sign(b.X - a.X) && dx != 0
                        || Math.Sign(dy) == Math.Sign(b.Y - a.Y) && dy != 0);
        }
    }

    [Fact]
    public void LaneSafety_NeverSpawnsOnClearMutualLane()
    {
        var rules = SpawnRules with { SpawnLaneSafety = true, ShotRange = 8 };
        var map = TestMaps.OpenRoom(); // open room: any shared row/col is a clear lane
        for (ulong seed = 0; seed < 50; seed++)
        {
            var spawns = SpawnVariation.Resolve(map, rules, seed);
            bool sharedLane = spawns[0].X == spawns[1].X || spawns[0].Y == spawns[1].Y;
            Assert.False(sharedLane, $"seed {seed} spawned on a mutual lane: " +
                $"({spawns[0].X},{spawns[0].Y}) / ({spawns[1].X},{spawns[1].Y})");
        }
    }

    [Fact]
    public void ZoneSpawnFairness_KeepsHillRaceEven()
    {
        var rules = SpawnRules with
        {
            RulesVersion = "test-hill-spawns",
            ZoneControl = true,
            ZoneSpawnFairness = true,
        };
        var map = TestMaps.OpenRoom(); // no declared zone → EffectiveZone center fallback
        var zone = map.EffectiveZone();
        for (ulong seed = 0; seed < 50; seed++)
        {
            var spawns = SpawnVariation.Resolve(map, rules, seed);
            int da = zone.Min(z => WalkDistance(map, new Position(spawns[0].X, spawns[0].Y), z));
            int db = zone.Min(z => WalkDistance(map, new Position(spawns[1].X, spawns[1].Y), z));
            Assert.True(Math.Abs(da - db) <= SpawnVariation.ZoneDistanceTolerance,
                $"seed {seed}: zone distances {da} vs {db} exceed the fairness tolerance.");
        }
    }

    /// <summary>4-neighbor BFS walking distance (open room: equals Manhattan, but
    /// computed honestly so the test also holds for walled maps).</summary>
    private static int WalkDistance(ArenaMap map, Position from, Position to)
    {
        var seen = new HashSet<Position> { from };
        var queue = new Queue<(Position At, int Steps)>([(from, 0)]);
        while (queue.Count > 0)
        {
            var (at, steps) = queue.Dequeue();
            if (at == to)
                return steps;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var next = at.Offset(dx, dy);
                if (next.X < 0 || next.Y < 0 || next.X >= map.Width || next.Y >= map.Height)
                    continue;
                if (map.IsWall(next) || !seen.Add(next))
                    continue;
                queue.Enqueue((next, steps + 1));
            }
        }
        return int.MaxValue;
    }

    [Fact]
    public void LaneSafetyOff_LeavesV0_2StreamsUnchanged()
    {
        // 0.2 spawn derivation must stay bit-identical when the 0.3 flag is off.
        var map = TestMaps.OpenRoom();
        for (ulong seed = 0; seed < 20; seed++)
            Assert.Equal(
                SpawnVariation.Resolve(map, GameRules.V0_2, seed),
                SpawnVariation.Resolve(map, GameRules.V0_2 with { MaxTicks = 123 }, seed));
    }

    [Fact]
    public void FullMatch_IsDeterministicUnderSpawnVariation()
    {
        MatchRunResult Run() => new MatchEngine().Run(new MatchConfiguration
        {
            Map = TestMaps.OpenRoom(),
            Rules = SpawnRules,
            Seed = 99,
            Participants =
            [
                new MatchParticipantConfig { Name = "a", Runtime = new ScriptedRuntime(BotAction.MoveForward, BotAction.Shoot) },
                new MatchParticipantConfig { Name = "b", Runtime = new ScriptedRuntime(BotAction.TurnLeft) },
            ],
        });
        Assert.Equal(Run().ReplayHash, Run().ReplayHash);
    }
}
