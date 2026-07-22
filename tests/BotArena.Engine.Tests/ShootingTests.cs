using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public class ShootingTests
{
    private static TickResult Step(MatchSession session, BotAction a0, BotAction a1) =>
        session.Step([BotDecision.Of(a0), BotDecision.Of(a1)]);

    [Fact]
    public void Shot_HitsFirstBotInLine()
    {
        var session = new MatchSession(TestMaps.OpenRoom(), GameRules.V0_1);
        var result = Step(session, BotAction.Shoot, BotAction.Wait);

        var shot = Assert.Single(result.Events, e => e.Type == GameEventType.Shot);
        Assert.Equal(1, shot.HitSlot);
        Assert.Equal(5, shot.ToX);
        var damage = Assert.Single(result.Events, e => e.Type == GameEventType.Damage);
        Assert.Equal(1, damage.TargetSlot);
        Assert.Equal(2, session.State.Bots[1].Health);
        Assert.Equal(GameRules.V0_1.ShootCooldownTicks, session.State.Bots[0].Cooldown);
    }

    [Fact]
    public void Shot_StopsAtWall()
    {
        var session = new MatchSession(TestMaps.PillarRoom(), GameRules.V0_1);
        var result = Step(session, BotAction.Shoot, BotAction.Wait);

        var shot = Assert.Single(result.Events, e => e.Type == GameEventType.Shot);
        Assert.Null(shot.HitSlot);
        Assert.Equal(3, shot.ToX);
        Assert.Equal(2, shot.ToY);
        Assert.Equal(3, session.State.Bots[1].Health);
        Assert.DoesNotContain(result.Events, e => e.Type == GameEventType.Damage);
    }

    [Fact]
    public void ShootingWhileOnCooldown_BecomesWait()
    {
        var session = new MatchSession(TestMaps.OpenRoom(), GameRules.V0_1);
        Step(session, BotAction.Shoot, BotAction.Wait);
        var result = Step(session, BotAction.Shoot, BotAction.Wait);

        Assert.Equal(ActionResult.OnCooldown, result.Bots[0].Result);
        Assert.Equal(BotAction.Shoot, result.Bots[0].ChosenAction);
        Assert.Equal(BotAction.Wait, result.Bots[0].ValidatedAction);
        Assert.DoesNotContain(result.Events, e => e.Type == GameEventType.Shot);
        Assert.Equal(2, session.State.Bots[1].Health);
    }

    [Fact]
    public void Cooldown_AllowsShootingEveryThirdTick()
    {
        var session = new MatchSession(TestMaps.OpenRoom(), GameRules.V0_1);
        Assert.Equal(ActionResult.Success, Step(session, BotAction.Shoot, BotAction.Wait).Bots[0].Result);
        Assert.Equal(ActionResult.OnCooldown, Step(session, BotAction.Shoot, BotAction.Wait).Bots[0].Result);
        Assert.Equal(ActionResult.OnCooldown, Step(session, BotAction.Shoot, BotAction.Wait).Bots[0].Result);
        Assert.Equal(ActionResult.Success, Step(session, BotAction.Shoot, BotAction.Wait).Bots[0].Result);
    }

    [Fact]
    public void ShootingResolvesAfterMovement_TargetEscapesTheLine()
    {
        var session = new MatchSession(TestMaps.OpenRoom(facing1: Direction.North), GameRules.V0_1);
        var result = Step(session, BotAction.Shoot, BotAction.MoveForward);

        Assert.Equal(new Position(5, 1), session.State.Bots[1].Position);
        var shot = Assert.Single(result.Events, e => e.Type == GameEventType.Shot);
        Assert.Null(shot.HitSlot);
        Assert.Equal(6, shot.ToX);
        Assert.Equal(3, session.State.Bots[1].Health);
    }

    [Fact]
    public void SimultaneousFatalShots_DestroyBothAndDraw()
    {
        var rules = GameRules.V0_1 with { MaxHealth = 1 };
        var session = new MatchSession(TestMaps.OpenRoom(), rules);
        var result = Step(session, BotAction.Shoot, BotAction.Shoot);

        Assert.True(result.MatchCompleted);
        Assert.Equal(BotStatus.Destroyed, session.State.Bots[0].Status);
        Assert.Equal(BotStatus.Destroyed, session.State.Bots[1].Status);
        Assert.Null(session.Result!.WinnerSlot);
        Assert.Equal(MatchEndReason.Elimination, session.Result.Reason);
        Assert.All(session.Result.Bots, b => Assert.Equal(BotOutcome.Draw, b.Outcome));
    }
}
