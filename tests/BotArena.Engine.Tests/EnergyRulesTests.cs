using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

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
