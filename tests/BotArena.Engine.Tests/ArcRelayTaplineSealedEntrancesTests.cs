using BotArena.Sdk;

namespace BotArena.Engine.Tests;

/// <summary>
/// Executable geometry contract for the owner-promoted sealed outer-Well
/// entrances with staggered Mid cover.
/// Distances are corner-strict, terrain-only lower bounds rather than routes
/// imposed on a mind.
/// </summary>
public sealed class ArcRelayTaplineSealedEntrancesTests
{
    private static readonly Position OwnReactor = new(2, 5);
    private static readonly Position OwnWell = new(11, 6);
    private static readonly Position CentreWell = new(15, 13);
    private static readonly Position EnemyWell = new(19, 20);

    private static ActorMapDefinition Current() => ArcRelayH0Definition
        .CreateMap(ArcRelayLoopProfile.Tapline9);

    [Fact]
    public void Tapline9AddsOnlyTheSixApprovedWallsAndKeepsTapline8()
    {
        Assert.Same(ArcRelayLoopProfile.Tapline13, ArcRelayLoopProfile.Current);
        Assert.Equal("tapline-13", ArcRelayLoopProfile.Current.Id);
        Assert.Equal("arc-relay-tapline-13", ArcRelayLoopProfile.Current.RulesetId);
        Assert.Equal("arc-relay-tapline-03", ArcRelayLoopProfile.Current.MapId);
        Assert.Equal("arc-relay-tapline-02", ArcRelayLoopProfile.Tapline8.MapId);

        ActorMapDefinition before = ArcRelayH0Definition.CreateMap(
            ArcRelayLoopProfile.Tapline8);
        ActorMapDefinition after = Current();
        Position[] expected =
        [
            new(12, 9), new(15, 9), new(13, 12),
            new(17, 14), new(15, 17), new(18, 17),
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
            "935cdb4225498db195145c3cd462a6907f0be8ec8bff723b537189024b4c98be",
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
        Assert.Equal(517, floor.Count);
        Assert.Equal(517, Reachable(map, floor[0], without: null));
        Assert.DoesNotContain(map.TileRows, row => row.Contains('A'));
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
    public void AllElevenApprovedRouteMeasurementsArePinned()
    {
        ActorMapDefinition map = Current();

        Assert.Equal(12, Distance(map, OwnWell, OwnReactor));
        Assert.Equal(22, Distance(map, OwnWell, CentreWell));
        Assert.Equal(22, Via(
            map, OwnWell, CentreWell, new Position(18, 7)));
        Assert.Equal(22, Distance(map, CentreWell, EnemyWell));
        Assert.Equal(22, Via(
            map, CentreWell, EnemyWell, new Position(12, 19)));
        Assert.Equal(19, Via(
            map, CentreWell, OwnReactor, new Position(9, 11)));
        Assert.Equal(29, Via(
            map, CentreWell, OwnReactor, new Position(18, 7)));
        Assert.Equal(31, Distance(map, OwnReactor, EnemyWell));
        Assert.Equal(41, Via(
            map, OwnReactor, EnemyWell, CentreWell));
        Assert.Equal(31, Distance(map, EnemyWell, OwnReactor));
        Assert.Equal(41, Via(
            map, EnemyWell, OwnReactor, CentreWell, new Position(9, 11)));
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
