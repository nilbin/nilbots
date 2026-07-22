using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public class MovementTests
{
    private static TickResult Step(MatchSession session, BotAction a0, BotAction a1) =>
        session.Step([BotDecision.Of(a0), BotDecision.Of(a1)]);

    [Fact]
    public void MoveForward_MovesOneTileInFacingDirection()
    {
        var session = new MatchSession(TestMaps.OpenRoom(), GameRules.V0_1);
        var result = Step(session, BotAction.MoveForward, BotAction.Wait);

        Assert.Equal(new Position(2, 2), session.State.Bots[0].Position);
        Assert.Equal(ActionResult.Success, result.Bots[0].Result);
        Assert.Contains(result.Events, e => e.Type == GameEventType.Move && e.Slot == 0);
    }

    [Fact]
    public void MoveIntoWall_IsBlocked()
    {
        var session = new MatchSession(TestMaps.OpenRoom(facing0: Direction.West), GameRules.V0_1);
        var result = Step(session, BotAction.MoveForward, BotAction.Wait);

        Assert.Equal(new Position(1, 2), session.State.Bots[0].Position);
        Assert.Equal(ActionResult.Blocked, result.Bots[0].Result);
        Assert.Contains(result.Events, e => e.Type == GameEventType.MoveBlocked && e.Slot == 0);
    }

    [Fact]
    public void TurnRight_CyclesThroughAllDirections()
    {
        var session = new MatchSession(TestMaps.OpenRoom(), GameRules.V0_1);
        var expected = new[] { Direction.South, Direction.West, Direction.North, Direction.East };
        foreach (var direction in expected)
        {
            Step(session, BotAction.TurnRight, BotAction.Wait);
            Assert.Equal(direction, session.State.Bots[0].Facing);
        }
    }

    [Fact]
    public void TurnLeft_IsInverseOfTurnRight()
    {
        var session = new MatchSession(TestMaps.OpenRoom(), GameRules.V0_1);
        Step(session, BotAction.TurnRight, BotAction.Wait);
        Step(session, BotAction.TurnLeft, BotAction.Wait);
        Assert.Equal(Direction.East, session.State.Bots[0].Facing);
    }

    [Fact]
    public void BothMovingToSameTile_NeitherMoves()
    {
        var map = ArenaMap.Create("test-contested", [
            "#######",
            "#.....#",
            "#.....#",
            "#.....#",
            "#######",
        ], [new Spawn(2, 2, Direction.East), new Spawn(4, 2, Direction.West)]);
        var session = new MatchSession(map, GameRules.V0_1);

        var result = Step(session, BotAction.MoveForward, BotAction.MoveForward);

        Assert.Equal(new Position(2, 2), session.State.Bots[0].Position);
        Assert.Equal(new Position(4, 2), session.State.Bots[1].Position);
        Assert.Equal(ActionResult.Blocked, result.Bots[0].Result);
        Assert.Equal(ActionResult.Blocked, result.Bots[1].Result);
    }

    [Fact]
    public void SwappingPositions_BothMovesFail()
    {
        var map = ArenaMap.Create("test-swap", [
            "#######",
            "#.....#",
            "#.....#",
            "#.....#",
            "#######",
        ], [new Spawn(2, 2, Direction.East), new Spawn(3, 2, Direction.West)]);
        var session = new MatchSession(map, GameRules.V0_1);

        var result = Step(session, BotAction.MoveForward, BotAction.MoveForward);

        Assert.Equal(new Position(2, 2), session.State.Bots[0].Position);
        Assert.Equal(new Position(3, 2), session.State.Bots[1].Position);
        Assert.All(result.Bots, b => Assert.Equal(ActionResult.Blocked, b.Result));
    }

    [Fact]
    public void MovingIntoTileBeingVacated_Succeeds()
    {
        var map = ArenaMap.Create("test-follow", [
            "#######",
            "#.....#",
            "#.....#",
            "#.....#",
            "#######",
        ], [new Spawn(2, 2, Direction.East), new Spawn(3, 2, Direction.East)]);
        var session = new MatchSession(map, GameRules.V0_1);

        var result = Step(session, BotAction.MoveForward, BotAction.MoveForward);

        Assert.Equal(new Position(3, 2), session.State.Bots[0].Position);
        Assert.Equal(new Position(4, 2), session.State.Bots[1].Position);
        Assert.All(result.Bots, b => Assert.Equal(ActionResult.Success, b.Result));
    }

    [Fact]
    public void MovingIntoStationaryBot_IsBlocked()
    {
        var map = ArenaMap.Create("test-bump", [
            "#######",
            "#.....#",
            "#.....#",
            "#.....#",
            "#######",
        ], [new Spawn(2, 2, Direction.East), new Spawn(3, 2, Direction.West)]);
        var session = new MatchSession(map, GameRules.V0_1);

        var result = Step(session, BotAction.MoveForward, BotAction.Wait);

        Assert.Equal(new Position(2, 2), session.State.Bots[0].Position);
        Assert.Equal(ActionResult.Blocked, result.Bots[0].Result);
    }

    [Fact]
    public void FollowerIsBlocked_WhenLeaderHitsWall()
    {
        var map = ArenaMap.Create("test-chain", [
            "#######",
            "#.....#",
            "#.....#",
            "#.....#",
            "#######",
        ], [new Spawn(4, 2, Direction.East), new Spawn(5, 2, Direction.East)]);
        var session = new MatchSession(map, GameRules.V0_1);

        var result = Step(session, BotAction.MoveForward, BotAction.MoveForward);

        Assert.Equal(new Position(4, 2), session.State.Bots[0].Position);
        Assert.Equal(new Position(5, 2), session.State.Bots[1].Position);
        Assert.All(result.Bots, b => Assert.Equal(ActionResult.Blocked, b.Result));
    }
}
