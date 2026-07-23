using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public class StrafeTests
{
    private static readonly GameRules StrafeRules = GameRules.V0_1 with
    {
        RulesVersion = "test-strafe",
        AllowStrafe = true,
    };

    [Fact]
    public void Strafe_MovesPerpendicular_WithoutRotating()
    {
        var session = new MatchSession(TestMaps.OpenRoom(), StrafeRules);
        var start = session.State.Bots[0].Position;
        var facing = session.State.Bots[0].Facing; // East
        var result = session.Step([BotDecision.Of(BotAction.StrafeLeft), BotDecision.Of(BotAction.Wait)]);

        Assert.Equal(facing, session.State.Bots[0].Facing);
        Assert.Equal(start.Offset(0, -1), session.State.Bots[0].Position); // left of East = North
        Assert.Equal(ActionResult.Success, result.Bots[0].Result);
        Assert.Contains(result.Events, e => e.Type == GameEventType.Move && e.Slot == 0);
    }

    [Fact]
    public void StrafeIntoWall_IsBlocked()
    {
        // OpenRoom spawn 0 is at (1,2) facing East; strafe right = South... use a bot
        // against the top wall: strafe left of East = North into the wall at y=0.
        var map = ArenaMap.Create("test-wallhug", [
            "#######",
            "#.....#",
            "#.....#",
            "#######",
        ], [new Spawn(1, 1, Direction.East), new Spawn(5, 2, Direction.West)]);
        var session = new MatchSession(map, StrafeRules);
        var result = session.Step([BotDecision.Of(BotAction.StrafeLeft), BotDecision.Of(BotAction.Wait)]);
        Assert.Equal(new Position(1, 1), session.State.Bots[0].Position);
        Assert.Equal(ActionResult.Blocked, result.Bots[0].Result);
    }

    [Fact]
    public void StrafeConflicts_ResolveLikeMoves()
    {
        // Both strafe into the same tile: both fail (same rule as MoveForward).
        var map = ArenaMap.Create("test-collide", [
            "#####",
            "#...#",
            "#...#",
            "#...#",
            "#####",
        ], [new Spawn(1, 1, Direction.East), new Spawn(1, 3, Direction.West)]);
        // Bot 0 faces East (strafe right = South → (1,2)); bot 1 faces West (strafe right = North → (1,2)).
        var session = new MatchSession(map, StrafeRules);
        var result = session.Step([BotDecision.Of(BotAction.StrafeRight), BotDecision.Of(BotAction.StrafeRight)]);
        Assert.Equal(new Position(1, 1), session.State.Bots[0].Position);
        Assert.Equal(new Position(1, 3), session.State.Bots[1].Position);
        Assert.Equal(ActionResult.Blocked, result.Bots[0].Result);
        Assert.Equal(ActionResult.Blocked, result.Bots[1].Result);
    }

    [Fact]
    public void Strafe_UnderStrafelessRules_BecomesWaitBlocked_NotFault()
    {
        var session = new MatchSession(TestMaps.OpenRoom(), GameRules.V0_1);
        var start = session.State.Bots[0].Position;
        var result = session.Step([BotDecision.Of(BotAction.StrafeLeft), BotDecision.Of(BotAction.Wait)]);
        Assert.Equal(start, session.State.Bots[0].Position);
        Assert.Equal(BotAction.StrafeLeft, result.Bots[0].ChosenAction);
        Assert.Equal(BotAction.Wait, result.Bots[0].ValidatedAction);
        Assert.Equal(ActionResult.Blocked, result.Bots[0].Result);
        Assert.False(result.Bots[0].Faulted);
        Assert.Equal(0, session.State.Bots[0].Faults);
    }
}
