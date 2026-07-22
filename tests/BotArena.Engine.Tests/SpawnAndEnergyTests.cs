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

public class EnergyRulesTests
{
    private static readonly GameRules EnergyRules = GameRules.V0_1 with
    {
        RulesVersion = "0.2-exp-energy",
        MaxEnergy = 6,
        ShotEnergyCost = 2,
        EnergyRegenTicks = 3,
        ShootCooldownTicks = 0, // isolate energy behavior from cooldown in these tests
    };

    private static MatchSession NewSession(GameRules rules) =>
        new(TestMaps.OpenRoom(), rules);

    [Fact]
    public void Shot_SpendsEnergy()
    {
        var session = NewSession(EnergyRules);
        session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);
        Assert.Equal(4, session.State.Bots[0].Energy);
    }

    [Fact]
    public void DryGun_BecomesWaitWithOnCooldown()
    {
        var session = NewSession(EnergyRules);
        // Face a wall so the drain shots hit nobody (otherwise the opponent dies first).
        session.Step([BotDecision.Of(BotAction.TurnLeft), BotDecision.Of(BotAction.Wait)]);
        while (session.State.Bots[0].Energy >= EnergyRules.ShotEnergyCost)
            session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);
        var result = session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);
        Assert.Equal(BotAction.Shoot, result.Bots[0].ChosenAction);
        Assert.Equal(BotAction.Wait, result.Bots[0].ValidatedAction);
        Assert.Equal(ActionResult.OnCooldown, result.Bots[0].Result);
        Assert.DoesNotContain(result.Events, e => e.Type == GameEventType.Shot && e.Slot == 0);
    }

    [Fact]
    public void Energy_RegeneratesOnCadenceAndCaps()
    {
        var session = NewSession(EnergyRules);
        session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]); // t0: 6-2=4
        int after = session.State.Bots[0].Energy;
        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);  // t1
        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);  // t2 → regen tick ((2+1)%3==0)
        Assert.Equal(after + 1, session.State.Bots[0].Energy);
        // Waits forever: caps at MaxEnergy, never beyond.
        for (int i = 0; i < 20; i++)
            session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);
        Assert.Equal(EnergyRules.MaxEnergy, session.State.Bots[0].Energy);
    }

    [Fact]
    public void V0_1_HasNoEnergyInObservationsOrReplayState()
    {
        var session = NewSession(GameRules.V0_1);
        Assert.Null(session.BuildObservation(0).Energy);

        var run = new MatchEngine().Run(new MatchConfiguration
        {
            Map = TestMaps.OpenRoom(),
            Rules = GameRules.V0_1 with { MaxTicks = 3 },
            Seed = 1,
            Participants =
            [
                new MatchParticipantConfig { Name = "a", Runtime = new ScriptedRuntime() },
                new MatchParticipantConfig { Name = "b", Runtime = new ScriptedRuntime() },
            ],
        });
        Assert.All(run.Replay.Ticks.SelectMany(t => t.State), s => Assert.Null(s.Energy));
        Assert.DoesNotContain("energy", ReplaySerializer.ToCanonicalJson(run.Replay));
    }

    [Fact]
    public void EnergyRules_ObservationsCarryEnergy()
    {
        var session = NewSession(EnergyRules);
        Assert.Equal(6, session.BuildObservation(0).Energy);
    }
}
