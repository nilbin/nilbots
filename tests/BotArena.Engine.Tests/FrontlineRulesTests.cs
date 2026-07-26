using BotArena.Engine;

namespace BotArena.Engine.Tests;

public class FrontlineRulesTests
{
    [Fact]
    public void Defaults_PinTheInitialFrontlineHypothesis()
    {
        var rules = new FrontlineRules();

        Assert.Equal(2, rules.TeamCount);
        Assert.Equal(1, rules.ParticipantsPerTeam);
        Assert.Equal(5, rules.FrontlinePositionCount);
        Assert.Equal(1, rules.InitialUnitsPerTeam);
        Assert.Equal(3, rules.MaxUnitsPerTeam);
        Assert.Equal(15, rules.CaptureThreshold);
        Assert.Equal(1, rules.CaptureGainPerSoleTeamTick);
        Assert.Equal(1, rules.CaptureDecayAmount);
        Assert.Equal(2, rules.CaptureDecayIntervalTicks);
        Assert.Equal(5, rules.RedeployPauseTicks);
        Assert.Equal(3, rules.PushesToBreach);
        Assert.Equal(18, rules.PrimeRespawnTicks);
        Assert.Equal(30, rules.ChildRebuildTicks);
        Assert.Equal([120, 260], rules.FabricationUnlockTicks.ToArray());

        Assert.Equal(
            new UnitFormRules(
                "prime-mobile",
                3,
                6,
                2,
                false,
                false,
                1,
                true,
                true,
                true),
            rules.PrimeForm);
        Assert.Equal(
            new UnitFormRules(
                "child-mobile",
                3,
                6,
                2,
                false,
                false,
                1,
                true,
                true,
                true),
            rules.ChildForm);
        Assert.Equal(
            new UnitFormRules(
                "turret",
                5,
                6,
                1,
                true,
                true,
                0,
                false,
                true,
                false),
            rules.TurretForm);
        Assert.Equal(1, rules.AnchorWindupTicks);
        Assert.Equal(2, rules.AnchorHealthGain);
        Assert.True(rules.AnchorIrreversibleForLife);
        Assert.False(rules.FriendlyFireEnabled);
        Assert.False(rules.AlliedProjectilesBlock);
    }

    [Fact]
    public void EveryExistingNamedRuleset_LeavesFrontlineDisabled()
    {
        foreach (string name in GameRules.KnownNames)
            Assert.Null(GameRules.Resolve(name).Frontline);
    }
}
