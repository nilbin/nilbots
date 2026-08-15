using BotArena.Engine;
using BotArena.Sdk;

namespace BotArena.Engine.Tests;

/// <summary>
/// Executable geometry contract for the owner-promoted equal-return Tapline.
/// Route lengths are terrain-only, corner-strict movement steps; they are
/// measurements of the board, never forced pathing rails.
/// </summary>
public sealed class ArcRelayTaplineEqualReturnTests
{
    private static ActorMapDefinition Current() => ArcRelayH0Definition
        .Create(loopProfile: ArcRelayLoopProfile.Tapline6).Map;

    private static ActorMapDefinition Historical() => ArcRelayH0Definition
        .Create(loopProfile: ArcRelayLoopProfile.Tapline5).Map;

    private static readonly Position OwnReactor = new(2, 5);
    private static readonly Position OwnWell = new(11, 6);
    private static readonly Position CentreWell = new(15, 13);
    private static readonly Position EnemyWell = new(19, 20);

    [Fact]
    public void Tapline6IsTheCurrentProductMapWithoutMutatingTapline5()
    {
        Assert.Same(ArcRelayLoopProfile.Tapline6, ArcRelayLoopProfile.Current);
        Assert.Equal("tapline-6", ArcRelayLoopProfile.Current.Id);
        Assert.Equal("arc-relay-tapline-06", ArcRelayLoopProfile.Current.RulesetId);
        Assert.Equal("arc-relay-tapline-02", ArcRelayLoopProfile.Current.MapId);
        Assert.Equal("arc-relay-tapline-01", ArcRelayLoopProfile.Tapline5.MapId);

        ActorMapDefinition before = Historical();
        ActorMapDefinition after = Current();
        var delta = new List<Position>();
        for (int y = 0; y < after.Height; y++)
        for (int x = 0; x < after.Width; x++)
        {
            if (before.IsWall(x, y) != after.IsWall(x, y))
                delta.Add(new Position(x, y));
        }

        Position[] expected =
        [
            new(9, 9), new(10, 9), new(11, 9), new(9, 10), new(5, 10),
            new(10, 11), new(21, 17), new(20, 17), new(19, 17),
            new(21, 16), new(25, 16), new(20, 15),
        ];
        Assert.Equal(
            expected.OrderBy(value => value.Y).ThenBy(value => value.X),
            delta.OrderBy(value => value.Y).ThenBy(value => value.X));
        Assert.All(expected, tile =>
        {
            Assert.False(before.IsWall(tile));
            Assert.True(after.IsWall(tile));
        });
    }

    [Fact]
    public void LandmarksStayOpenAndTheFloorIsOneSymmetric523TileComponent()
    {
        ActorMapDefinition map = Current();
        Assert.Equal((31, 27), (map.Width, map.Height));
        Position[] landmarks =
        [
            OwnReactor, new(28, 21), OwnWell, CentreWell, EnemyWell,
            new(21, 7), new(9, 19),
        ];
        Assert.All(landmarks, tile => Assert.False(map.IsWall(tile)));

        List<Position> floor = Floor(map);
        Assert.Equal(523, floor.Count);
        Assert.Equal(523, Reachable(map, floor[0], without: null));
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            Assert.Equal(
                map.IsWall(x, y),
                map.IsWall(map.Width - 1 - x, map.Height - 1 - y));
        }
    }

    [Fact]
    public void CornersCutVerticesAndOwnWellDeadEndsStayOutOfTheMap()
    {
        const int StubAllowance = 2;
        ActorMapDefinition map = Current();
        List<Position> floor = Floor(map);
        var falseCorners = new List<string>();
        foreach (Position tile in floor)
        foreach ((int dx, int dy) in new[] { (1, 1), (1, -1) })
        {
            var diagonal = new Position(tile.X + dx, tile.Y + dy);
            if (!Inside(map, diagonal) || map.IsWall(diagonal))
                continue;
            if (map.IsWall(new Position(tile.X + dx, tile.Y))
                && map.IsWall(new Position(tile.X, tile.Y + dy)))
            {
                falseCorners.Add($"{tile}-{diagonal}");
            }
        }
        Assert.Empty(falseCorners);

        var cuts = new List<string>();
        foreach (Position blocker in floor)
        {
            int reached = Reachable(map, floor.First(tile => tile != blocker), blocker);
            int severed = Math.Min(reached, floor.Count - 1 - reached);
            if (severed > StubAllowance)
                cuts.Add($"{blocker} severs {severed}");
        }
        Assert.Empty(cuts);

        // The complete shortcut seal deliberately fills the otherwise-useless
        // branch beside each owned Well instead of merely moving its dead end
        // inward by one tile.
        AssertNoLeafFloor(map, minX: 4, maxX: 12, minY: 4, maxY: 13);
        AssertNoLeafFloor(map, minX: 18, maxX: 26, minY: 13, maxY: 22);
    }

    [Fact]
    public void AllFourteenReturnMeasurementsArePinned()
    {
        ActorMapDefinition map = Current();
        Assert.Equal(12, Distance(map, OwnWell, OwnReactor));

        Assert.Equal(19, Via(map, CentreWell, OwnReactor, OwnWell));
        Assert.Equal(19, Via(map, CentreWell, OwnReactor, new Position(9, 11)));
        Assert.Equal(28, Via(map, CentreWell, OwnReactor,
            new(15, 3), new(15, 2), new(15, 1), new(3, 1), new(3, 2), new(3, 3)));
        Assert.Equal(44, Via(map, CentreWell, OwnReactor,
            new(15, 23), new(15, 24), new(15, 25), new(3, 25), new(3, 24), new(3, 23)));
        Assert.Equal(47, Via(map, CentreWell, OwnReactor,
            new(27, 3), new(27, 2), new(27, 1), new(3, 1), new(3, 2), new(3, 3)));
        Assert.Equal(65, Via(map, CentreWell, OwnReactor,
            new(27, 23), new(27, 24), new(27, 25), new(3, 25), new(3, 24), new(3, 23)));

        Assert.Equal(25, Distance(map, EnemyWell, OwnReactor));
        Assert.Equal(26, Via(map, EnemyWell, OwnReactor, CentreWell, OwnWell));
        Assert.Equal(26, Via(map, EnemyWell, OwnReactor, CentreWell, new Position(9, 11)));
        Assert.Equal(35, Via(map, EnemyWell, OwnReactor,
            new(15, 3), new(15, 2), new(15, 1), new(3, 1), new(3, 2), new(3, 3)));
        Assert.Equal(41, Via(map, EnemyWell, OwnReactor,
            new(15, 23), new(15, 24), new(15, 25), new(3, 25), new(3, 24), new(3, 23)));
        Assert.Equal(52, Via(map, EnemyWell, OwnReactor,
            new(27, 3), new(27, 2), new(27, 1), new(3, 1), new(3, 2), new(3, 3)));
        Assert.Equal(58, Via(map, EnemyWell, OwnReactor,
            new(27, 23), new(27, 24), new(27, 25), new(3, 25), new(3, 24), new(3, 23)));
    }

    private static int Via(
        ActorMapDefinition map,
        Position start,
        Position end,
        params Position[] via)
    {
        Position[] points = [start, .. via, end];
        return points.Zip(points.Skip(1), (from, to) => Distance(map, from, to)).Sum();
    }

    private static int Distance(
        ActorMapDefinition map,
        Position start,
        Position end,
        Position? without = null)
    {
        var seen = new HashSet<Position> { start };
        var queue = new Queue<(Position Tile, int Steps)>();
        queue.Enqueue((start, 0));
        while (queue.Count > 0)
        {
            (Position tile, int steps) = queue.Dequeue();
            if (tile == end)
                return steps;
            foreach (Position next in Neighbours(map, tile))
            {
                if (next != without && seen.Add(next))
                    queue.Enqueue((next, steps + 1));
            }
        }
        return -1;
    }

    private static int Reachable(
        ActorMapDefinition map,
        Position start,
        Position? without) => DistanceLedger(map, start, without).Count;

    private static HashSet<Position> DistanceLedger(
        ActorMapDefinition map,
        Position start,
        Position? without)
    {
        var seen = new HashSet<Position> { start };
        var queue = new Queue<Position>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            Position tile = queue.Dequeue();
            foreach (Position next in Neighbours(map, tile))
            {
                if (next != without && seen.Add(next))
                    queue.Enqueue(next);
            }
        }
        return seen;
    }

    private static IEnumerable<Position> Neighbours(
        ActorMapDefinition map,
        Position from)
    {
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            if ((dx == 0 && dy == 0)
                || !CanStep(map, from, new Position(from.X + dx, from.Y + dy)))
                continue;
            yield return new Position(from.X + dx, from.Y + dy);
        }
    }

    private static bool CanStep(
        ActorMapDefinition map,
        Position from,
        Position to)
    {
        if (!Inside(map, to) || map.IsWall(to))
            return false;
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        return dx == 0 || dy == 0
            || (!map.IsWall(new Position(from.X + dx, from.Y))
                && !map.IsWall(new Position(from.X, from.Y + dy)));
    }

    private static bool Inside(ActorMapDefinition map, Position tile) =>
        tile.X >= 0 && tile.Y >= 0 && tile.X < map.Width && tile.Y < map.Height;

    private static List<Position> Floor(ActorMapDefinition map) =>
        (from y in Enumerable.Range(0, map.Height)
         from x in Enumerable.Range(0, map.Width)
         let tile = new Position(x, y)
         where !map.IsWall(tile)
         select tile).ToList();

    private static void AssertNoLeafFloor(
        ActorMapDefinition map,
        int minX,
        int maxX,
        int minY,
        int maxY)
    {
        var leaves = new List<Position>();
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            var tile = new Position(x, y);
            if (!map.IsWall(tile) && Neighbours(map, tile).Take(2).Count() < 2)
                leaves.Add(tile);
        }
        Assert.Empty(leaves);
    }
}
