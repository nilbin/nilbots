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

    private static readonly GameRules ExhaustiveRules = SpawnRules with
    {
        RulesVersion = "test-exhaustive-spawns",
        ShotRange = 8,
        SpawnLaneSafety = true,
        ExhaustiveSpawns = true,
    };

    [Fact]
    public void Exhaustive_IsSeedDeterministic_AndSeedsVary()
    {
        var map = TestMaps.WideRoom();
        Assert.Equal(
            SpawnVariation.Resolve(map, ExhaustiveRules, 11),
            SpawnVariation.Resolve(map, ExhaustiveRules, 11));
        int distinct = Enumerable.Range(0, 16)
            .Select(seed => SpawnVariation.Resolve(map, ExhaustiveRules, (ulong)seed))
            .Select(s => (s[0].X, s[0].Y, s[1].X, s[1].Y))
            .Distinct()
            .Count();
        Assert.True(distinct > 4, $"Expected varied exhaustive spawns across seeds, got {distinct} distinct.");
    }

    [Fact]
    public void Exhaustive_HonorsEveryConstraint_OnEverySeed()
    {
        // The whole point of §H item 3: no seed can ever bypass a constraint, because
        // there is no fallback path — so this must hold for ALL seeds, not most.
        var rules = ExhaustiveRules with { ZoneControl = true, ZoneSpawnFairness = true };
        var map = TestMaps.WideRoom();
        var zone = map.EffectiveZone();
        int minDistance = Math.Max(map.Width, map.Height) / 2;
        for (ulong seed = 0; seed < 100; seed++)
        {
            var spawns = SpawnVariation.Resolve(map, rules, seed);
            var a = new Position(spawns[0].X, spawns[0].Y);
            var b = new Position(spawns[1].X, spawns[1].Y);
            Assert.True(a.ChebyshevDistance(b) >= minDistance, $"seed {seed}: too close");
            Assert.True(map.AreConnected(a, b), $"seed {seed}: disconnected");
            // Lane safety bars a shared row/col only within firing range (an open room
            // has no walls, so range is the only out).
            bool tickZeroLane = (a.X == b.X || a.Y == b.Y)
                && Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) <= rules.ShotRange;
            Assert.False(tickZeroLane, $"seed {seed}: mutual firing lane");
            int da = zone.Min(z => WalkDistance(map, a, z));
            int db = zone.Min(z => WalkDistance(map, b, z));
            Assert.True(Math.Abs(da - db) <= SpawnVariation.ZoneDistanceTolerance,
                $"seed {seed}: unfair zone race {da} vs {db}");
        }
    }

    [Fact]
    public void Exhaustive_AMapWithNoValidPair_IsRejectedLoudly()
    {
        // A pure corridor: every distant-enough pair shares the row → lane safety
        // rejects all of them. The sampler would silently fall back to the map's
        // fixed (lane-sharing!) spawns; exhaustive enumeration must refuse instead.
        var corridor = ArenaMap.Create("test-corridor", [
            "########",
            "#......#",
            "########",
        ], [new Spawn(1, 1, Direction.East), new Spawn(6, 1, Direction.West)]);
        var ex = Assert.Throws<InvalidOperationException>(
            () => SpawnVariation.Resolve(corridor, ExhaustiveRules, 5));
        Assert.Contains("no valid spawn pair", ex.Message);
        // And the sampler fallback it replaces really would have been unfair:
        var sampled = SpawnVariation.Resolve(corridor, ExhaustiveRules with { ExhaustiveSpawns = false }, 5);
        Assert.Equal(sampled[0].Y, sampled[1].Y);
    }

    private static List<ArenaMap> ShippedMaps()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "BotArena.sln")))
            root = root.Parent;
        Assert.NotNull(root);
        var maps = Directory.EnumerateFiles(Path.Combine(root!.FullName, "maps"), "*.json").Order()
            .Select(file => ArenaMap.FromJson(File.ReadAllText(file)))
            .ToList();
        Assert.NotEmpty(maps);
        return maps;
    }

    [Fact]
    public void Exhaustive_EveryShippedMap_HasValidPairs_UnderEveryExperimentArm()
    {
        // "Map rejected loudly" is only acceptable if no shipped map trips it. Gate
        // the whole pool against every arm that uses exhaustive spawns.
        var arms = GameRules.KnownNames.Select(GameRules.Resolve).Where(r => r.ExhaustiveSpawns).ToList();
        Assert.NotEmpty(arms);
        foreach (var map in ShippedMaps())
            foreach (var arm in arms)
                for (ulong seed = 0; seed < 8; seed++)
                    _ = SpawnVariation.Resolve(map, arm, seed); // must not throw
    }

    [Fact]
    public void HardenedArms_ShareSpawnsAndBotStreams_ForTheSameMapAndSeed()
    {
        // The paired-harness prerequisite (follow-up review): the same map + seed must
        // give every arm IDENTICAL spawn geometry and IDENTICAL per-bot RNG streams,
        // so a paired A/B row differs only by the tested mechanic — a mechanics-blind
        // bot plays the bit-identical game under every arm.
        var arms = new[] { "0.5-control", "cone", "bolts", "conebolts", "conebolts1" }
            .Select(GameRules.Resolve).ToList();
        foreach (var map in ShippedMaps())
            for (ulong seed = 0; seed < 25; seed++)
            {
                var expected = SpawnVariation.Resolve(map, arms[0], seed);
                foreach (var arm in arms.Skip(1))
                    Assert.Equal(expected, SpawnVariation.Resolve(map, arm, seed));
            }
        foreach (var arm in arms)
            for (int slot = 0; slot < 2; slot++)
                Assert.Equal(
                    SeedDerivation.DeriveBotSeed(0xBEEF, slot, arms[0].SeedProfile ?? arms[0].RulesVersion),
                    SeedDerivation.DeriveBotSeed(0xBEEF, slot, arm.SeedProfile ?? arm.RulesVersion));
    }

    [Fact]
    public void RedesignArms_ShareSpawnsAndBotStreams_ForTheSameMapAndSeed()
    {
        var arms = new[]
            {
                "control", "cone-control", "cone-active",
                "cone-active-bolt1", "cone-active-bolt2", "cone-active-bolt2-overtime",
                "cone-active-bolt2-overtime-gain", "cone-active-bolt2-arcs",
            }
            .Select(GameRules.Resolve)
            .ToList();
        foreach (var map in ShippedMaps())
            for (ulong seed = 0; seed < 25; seed++)
            {
                var expected = SpawnVariation.Resolve(map, arms[0], seed);
                foreach (var arm in arms.Skip(1))
                    Assert.Equal(expected, SpawnVariation.Resolve(map, arm, seed));
            }
        foreach (var arm in arms)
            for (int slot = 0; slot < 2; slot++)
                Assert.Equal(
                    SeedDerivation.DeriveBotSeed(0xBEEF, slot, arms[0].SeedProfile ?? arms[0].RulesVersion),
                    SeedDerivation.DeriveBotSeed(0xBEEF, slot, arm.SeedProfile ?? arm.RulesVersion));
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
