using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public class MatchFlowTests
{
    [Fact]
    public void DestroyingTheOpponent_WinsByElimination()
    {
        // Shoot every 3rd tick: 3 hits kill at default rules.
        var shooter = new DelegateRuntime(o =>
            BotDecision.Of(o.Cooldown == 0 ? BotAction.Shoot : BotAction.Wait));
        var idle = new DelegateRuntime(_ => BotDecision.Of(BotAction.Wait));
        var run = new MatchEngine().Run(TestMaps.Config(TestMaps.OpenRoom(), shooter, idle));

        Assert.Equal(0, run.Result.WinnerSlot);
        Assert.Equal(MatchEndReason.Elimination, run.Result.Reason);
        Assert.Equal(BotOutcome.Win, run.Result.Bots[0].Outcome);
        Assert.Equal(BotOutcome.Loss, run.Result.Bots[1].Outcome);
        Assert.Equal(0, run.Result.Bots[1].FinalHealth);
        Assert.Equal(3, run.Result.Bots[0].DamageDealt);
    }

    [Fact]
    public void MaxTicksReached_HigherHealthWins()
    {
        var rules = GameRules.V0_1 with { MaxTicks = 5 };
        var oneShot = new ScriptedRuntime(BotAction.Shoot);
        var idle = new DelegateRuntime(_ => BotDecision.Of(BotAction.Wait));
        var run = new MatchEngine().Run(TestMaps.Config(TestMaps.OpenRoom(), oneShot, idle, rules: rules));

        Assert.Equal(MatchEndReason.MaxTicks, run.Result.Reason);
        Assert.Equal(0, run.Result.WinnerSlot);
        Assert.Equal(4, run.Result.EndTick);
        Assert.Equal(3, run.Result.Bots[0].FinalHealth);
        Assert.Equal(2, run.Result.Bots[1].FinalHealth);
    }

    [Fact]
    public void MaxTicksWithEqualHealthAndDamage_IsDraw()
    {
        var rules = GameRules.V0_1 with { MaxTicks = 3 };
        var idle0 = new DelegateRuntime(_ => BotDecision.Of(BotAction.Wait));
        var idle1 = new DelegateRuntime(_ => BotDecision.Of(BotAction.Wait));
        var run = new MatchEngine().Run(TestMaps.Config(TestMaps.OpenRoom(), idle0, idle1, rules: rules));

        Assert.Null(run.Result.WinnerSlot);
        Assert.Equal(MatchEndReason.MaxTicks, run.Result.Reason);
        Assert.All(run.Result.Bots, b => Assert.Equal(BotOutcome.Draw, b.Outcome));
    }

    [Fact]
    public void FaultingBot_WaitsAndIsDisqualifiedAfterThreeFaults()
    {
        var idle = new DelegateRuntime(_ => BotDecision.Of(BotAction.Wait));
        var faulty = new ThrowingRuntime();
        var run = new MatchEngine().Run(TestMaps.Config(TestMaps.OpenRoom(), idle, faulty));

        Assert.Equal(0, run.Result.WinnerSlot);
        Assert.Equal(MatchEndReason.Disqualification, run.Result.Reason);
        Assert.Equal(2, run.Result.EndTick);
        Assert.Equal(3, run.Result.Bots[1].Faults);
        Assert.Equal(BotStatus.Disqualified, run.Result.Bots[1].FinalStatus);

        var lastTick = run.Replay.Ticks[^1];
        Assert.Contains(lastTick.Events, e => e.Type == GameEventType.Disqualified && e.Slot == 1);
        Assert.Equal(3, run.Replay.Ticks.SelectMany(t => t.Events)
            .Count(e => e.Type == GameEventType.Fault && e.Slot == 1));
    }

    [Fact]
    public void FaultedTick_IsRecordedAsWaitWithFaultedResult()
    {
        var session = new MatchSession(TestMaps.OpenRoom(), GameRules.V0_1);
        var result = session.Step([BotDecision.Fault("boom"), BotDecision.Of(BotAction.Wait)]);

        Assert.Equal(BotAction.Wait, result.Bots[0].ChosenAction);
        Assert.Equal(BotAction.Wait, result.Bots[0].ValidatedAction);
        Assert.Equal(ActionResult.Faulted, result.Bots[0].Result);
        Assert.True(result.Bots[0].Faulted);
        Assert.Contains(result.Events, e => e.Type == GameEventType.Fault && e.Message == "boom");
    }

    [Fact]
    public void Observation_CarriesPreviousActionResult()
    {
        var session = new MatchSession(TestMaps.OpenRoom(facing0: Direction.West), GameRules.V0_1);
        Assert.Equal(ActionResult.None, session.BuildObservation(0).PreviousActionResult);

        session.Step([BotDecision.Of(BotAction.MoveForward), BotDecision.Of(BotAction.Wait)]);
        Assert.Equal(ActionResult.Blocked, session.BuildObservation(0).PreviousActionResult);
    }

    [Fact]
    public void RuntimeExceptionInsideEngineRun_BecomesFaultNotCrash()
    {
        var idle = new DelegateRuntime(_ => BotDecision.Of(BotAction.Wait));
        var throwing = new ThrowingRuntime(goodTicks: 1);
        var run = new MatchEngine().Run(TestMaps.Config(TestMaps.OpenRoom(), idle, throwing));

        Assert.Equal(MatchEndReason.Disqualification, run.Result.Reason);
        Assert.Equal(0, run.Result.WinnerSlot);
    }
}
