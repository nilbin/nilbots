using BotArena.Sdk;

namespace BotArena.Engine.Tests;

/// <summary>
/// Executable geometry contract for the owner-promoted Mid-to-Well wall ribs.
/// Distances are corner-strict, terrain-only lower bounds rather than routes
/// imposed on a mind.
/// </summary>
public sealed class ArcRelayTaplineMidWellRibsTests
{
    private static readonly Position OwnReactor = new(2, 5);
    private static readonly Position OwnWell = new(11, 6);
    private static readonly Position CentreWell = new(15, 13);
    private static readonly Position EnemyWell = new(19, 20);

    private static ActorMapDefinition Current() => ArcRelayH0Definition
        .CreateMap(ArcRelayLoopProfile.Tapline9);

    [Fact]
    public void Tapline9AddsOnlyTheFourteenApprovedWallsAndKeepsTapline8()
    {
        Assert.Same(ArcRelayLoopProfile.Tapline9, ArcRelayLoopProfile.Current);
        Assert.Equal("tapline-9", ArcRelayLoopProfile.Current.Id);
        Assert.Equal("arc-relay-tapline-09", ArcRelayLoopProfile.Current.RulesetId);
        Assert.Equal("arc-relay-tapline-03", ArcRelayLoopProfile.Current.MapId);
        Assert.Equal("arc-relay-tapline-02", ArcRelayLoopProfile.Tapline8.MapId);

        ActorMapDefinition before = ArcRelayH0Definition.CreateMap(
            ArcRelayLoopProfile.Tapline8);
        ActorMapDefinition after = Current();
        Position[] expected =
        [
            new(12, 11), new(13, 11), new(14, 11), new(15, 11), new(16, 11),
            new(22, 12), new(11, 13), new(19, 13), new(8, 14),
            new(14, 15), new(15, 15), new(16, 15), new(17, 15), new(18, 15),
        ];
        Position[] delta = (
            from y in Enumerable.Range(0, after.Height)
            from x in Enumerable.Range(0, after.Width)
            where before.IsWall(x, y) != after.IsWall(x, y)
            select new Position(x, y)).ToArray();

        Assert.Equal(
            expected.OrderBy(value => value.Y).ThenBy(value => value.X),
            delta.OrderBy(value => value.Y).ThenBy(value => value.X));
        Assert.All(expected, tile =>
        {
            Assert.False(before.IsWall(tile));
            Assert.True(after.IsWall(tile));
        });
        Assert.Equal(
            "e536d04deff7e53296d10f2b93c38741a6a3e465a03e779a84336ba0ae1df519",
            ActorContractFingerprint.ComputeMap(before));
        Assert.Equal(
            "27e0d0dd62544a9f77f3273f811a5171db1c0de84488af7cad92bfca9ce1e191",
            ActorContractFingerprint.ComputeMap(after));
    }

    [Fact]
    public void LandmarksSymmetryAndTopologyStayIntact()
    {
        const int StubAllowance = 2;
        ActorMapDefinition map = Current();
        Assert.Equal((31, 27), (map.Width, map.Height));
        Position[] landmarks =
        [
            OwnReactor, new(28, 21), OwnWell, CentreWell, EnemyWell,
            new(21, 7), new(9, 19),
        ];
        Assert.All(landmarks, tile => Assert.False(map.IsWall(tile)));

        List<Position> floor = Floor(map);
        Assert.Equal(509, floor.Count);
        Assert.Equal(509, Reachable(map, floor[0], without: null));
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            Assert.Equal(
                map.IsWall(x, y),
                map.IsWall(map.Width - 1 - x, map.Height - 1 - y));
        }

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
            int reached = Reachable(
                map, floor.First(tile => tile != blocker), blocker);
            int severed = Math.Min(reached, floor.Count - 1 - reached);
            if (severed > StubAllowance)
                cuts.Add($"{blocker} severs {severed}");
        }
        Assert.Empty(cuts);

        AssertNoLeafFloor(map, 4, 12, 4, 13);
        AssertNoLeafFloor(map, 18, 26, 13, 22);
    }

    [Fact]
    public void AllSeventeenCandidateRouteMeasurementsArePinned()
    {
        ActorMapDefinition map = Current();

        Assert.Equal(12, Distance(map, OwnWell, OwnReactor));
        Assert.Equal(12, Distance(map, OwnWell, CentreWell));
        Assert.Equal(12, Distance(map, CentreWell, EnemyWell));
        Assert.Equal(24, Via(map, CentreWell, OwnReactor, OwnWell));
        Assert.Equal(24, Via(
            map, CentreWell, OwnReactor, new Position(13, 17)));
        Assert.Equal(28, Distance(map, OwnReactor, EnemyWell));
        Assert.Equal(36, Via(
            map, OwnReactor, EnemyWell, OwnWell, CentreWell));
        Assert.Equal(28, Distance(map, EnemyWell, OwnReactor));
        Assert.Equal(36, Via(
            map, EnemyWell, OwnReactor, CentreWell, OwnWell));

        Assert.Equal(31, Via(map, CentreWell, OwnReactor,
            new(15, 3), new(15, 2), new(15, 1),
            new(3, 1), new(3, 2), new(3, 3)));
        Assert.Equal(47, Via(map, CentreWell, OwnReactor,
            new(15, 23), new(15, 24), new(15, 25),
            new(3, 25), new(3, 24), new(3, 23)));
        Assert.Equal(48, Via(map, CentreWell, OwnReactor,
            new(27, 3), new(27, 2), new(27, 1),
            new(3, 1), new(3, 2), new(3, 3)));
        Assert.Equal(70, Via(map, CentreWell, OwnReactor,
            new(27, 23), new(27, 24), new(27, 25),
            new(3, 25), new(3, 24), new(3, 23)));
        Assert.Equal(43, Via(map, EnemyWell, OwnReactor,
            new(15, 3), new(15, 2), new(15, 1),
            new(3, 1), new(3, 2), new(3, 3)));
        Assert.Equal(41, Via(map, EnemyWell, OwnReactor,
            new(15, 23), new(15, 24), new(15, 25),
            new(3, 25), new(3, 24), new(3, 23)));
        Assert.Equal(56, Via(map, EnemyWell, OwnReactor,
            new(27, 3), new(27, 2), new(27, 1),
            new(3, 1), new(3, 2), new(3, 3)));
        Assert.Equal(58, Via(map, EnemyWell, OwnReactor,
            new(27, 23), new(27, 24), new(27, 25),
            new(3, 25), new(3, 24), new(3, 23)));
    }

    private static int Via(
        ActorMapDefinition map,
        Position start,
        Position end,
        params Position[] via)
    {
        Position[] points = [start, .. via, end];
        return points.Zip(points.Skip(1), (from, to) =>
            Distance(map, from, to)).Sum();
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
        return seen.Count;
    }

    private static IEnumerable<Position> Neighbours(
        ActorMapDefinition map,
        Position from)
    {
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            var next = new Position(from.X + dx, from.Y + dy);
            if ((dx != 0 || dy != 0) && CanStep(map, from, next))
                yield return next;
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
        tile.X >= 0 && tile.Y >= 0
        && tile.X < map.Width && tile.Y < map.Height;

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
