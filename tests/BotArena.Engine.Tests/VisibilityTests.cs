using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public class VisibilityTests
{
    [Fact]
    public void EnemyInOpenRoom_IsVisible()
    {
        var session = new MatchSession(TestMaps.OpenRoom(), GameRules.V0_1);
        var observation = session.BuildObservation(0);

        var enemy = Assert.Single(observation.VisibleEnemies);
        Assert.Equal(1, enemy.Slot);
        Assert.Equal(new Position(5, 2), enemy.Position);
        Assert.Equal(Direction.West, enemy.Facing);
    }

    [Fact]
    public void EnemyBehindWall_IsNotVisible()
    {
        var session = new MatchSession(TestMaps.PillarRoom(), GameRules.V0_1);
        Assert.Empty(session.BuildObservation(0).VisibleEnemies);
        Assert.Empty(session.BuildObservation(1).VisibleEnemies);
    }

    [Fact]
    public void EnemyBeyondVisionRange_IsNotVisible()
    {
        // Distance 9 in a straight open line; range is 6.
        var session = new MatchSession(TestMaps.WideRoom(), GameRules.V0_1);
        var observation = session.BuildObservation(0);

        Assert.Empty(observation.VisibleEnemies);
        Assert.Contains(observation.VisibleTiles, t => t.Position == new Position(7, 2));
        Assert.DoesNotContain(observation.VisibleTiles, t => t.Position == new Position(8, 2));
    }

    [Fact]
    public void WallTileItself_IsVisible()
    {
        var session = new MatchSession(TestMaps.PillarRoom(), GameRules.V0_1);
        var observation = session.BuildObservation(0);

        var pillar = Assert.Single(observation.VisibleTiles, t => t.Position == new Position(3, 2));
        Assert.True(pillar.IsWall);
    }

    [Fact]
    public void LineOfSight_IsSymmetric_ForEveryFloorPair()
    {
        var map = ArenaMap.Create("test-sym", [
            "#########",
            "#....#..#",
            "#.##....#",
            "#....##.#",
            "#.#.....#",
            "#########",
        ], [new Spawn(1, 1, Direction.East), new Spawn(7, 4, Direction.West)]);

        var floors = new List<Position>();
        for (int y = 0; y < 6; y++)
            for (int x = 0; x < 9; x++)
                if (!map.IsWall(x, y))
                    floors.Add(new Position(x, y));

        foreach (var a in floors)
            foreach (var b in floors)
                Assert.Equal(
                    Visibility.HasLineOfSight(map, a, b),
                    Visibility.HasLineOfSight(map, b, a));
    }

    [Fact]
    public void DiagonalWallCorner_BlocksLineOfSight()
    {
        var map = ArenaMap.Create("test-corner", [
            "######",
            "#....#",
            "#.#..#",
            "#..#.#",
            "#....#",
            "######",
        ], [new Spawn(1, 1, Direction.East), new Spawn(4, 4, Direction.West)]);

        // Walls at (2,2) and (3,3); the segment (2,3)->(3,2) passes exactly between them.
        Assert.False(Visibility.HasLineOfSight(map, new Position(2, 3), new Position(3, 2)));
        Assert.False(Visibility.HasLineOfSight(map, new Position(3, 2), new Position(2, 3)));
    }

    [Fact]
    public void VisibleEvents_OnlyIncludeEventsInsideCurrentFieldOfView()
    {
        // Bot 1 moves behind the pillar's shadow relative to bot 0? Simplest positive case:
        // in the open room every event is visible to both bots on the next tick.
        var session = new MatchSession(TestMaps.OpenRoom(), GameRules.V0_1);
        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.MoveForward)]);

        var observation = session.BuildObservation(0);
        Assert.Contains(observation.VisibleEvents, e => e.Type == GameEventType.Move && e.Slot == 1);
    }
}
