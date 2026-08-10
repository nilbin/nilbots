using BotArena.Engine;
using BotArena.Sdk;

namespace BotArena.Engine.Tests;

/// <summary>
/// Tapline keeps the well-line warren's grammar -- the owner's verdict after
/// playing the two experiments -- and tiers the Wells the warren could not.
/// These pin the properties that follow from that.
/// </summary>
public sealed class ArcRelayTaplineTests
{
    private static ActorMapDefinition Map() => ArcRelayH0Definition
        .Create(loopProfile: ArcRelayLoopProfile.Tapline1).Map;

    [Fact]
    public void TheCanvasIsExactlyOneHundredEightyDegreeChiral()
    {
        ActorMapDefinition map = Map();
        Assert.Equal(31, map.Width);
        Assert.Equal(27, map.Height);
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            Assert.Equal(
                map.IsWall(x, y),
                map.IsWall(map.Width - 1 - x, map.Height - 1 - y));
        }
    }

    [Fact]
    public void TheThreeWellsAreCollinearAndTiered()
    {
        // The warren's three Wells all sat thirteen steps from both bases,
        // because they shared a column and the bases faced each other across
        // it. Tapline keeps them on a line and puts that line along the axis
        // of play, so yours is up-line and theirs is down-line.
        ActorMapDefinition map = Map();
        Position north = new(11, 6);
        Position centre = new(15, 13);
        Position south = new(19, 20);

        Assert.Equal(
            (centre.X - north.X, centre.Y - north.Y),
            (south.X - centre.X, south.Y - centre.Y));
        Assert.Equal(
            south,
            new Position(map.Width - 1 - north.X, map.Height - 1 - north.Y));

        Position west = new(2, 5);
        int own = Distance(map, west, north);
        int middle = Distance(map, west, centre);
        int far = Distance(map, west, south);
        Assert.True(own < middle && middle < far,
            $"expected tiering, got own {own}, centre {middle}, enemy {far}");
        Assert.Equal(10, own);
        Assert.Equal(14, middle);
        Assert.Equal(18, far);
    }

    [Fact]
    public void TheOuterShadowCorridorHasExactlyThreeWaysInPerSide()
    {
        // The warren's best idea: a full-width lane around the outside that
        // is a committed flank rather than a free highway.
        ActorMapDefinition map = Map();
        foreach (int seam in new[] { 2, map.Height - 3 })
        {
            var gaps = new List<int>();
            for (int x = 1; x < map.Width - 1; x++)
            {
                if (!map.IsWall(x, seam))
                    gaps.Add(x);
            }
            Assert.Equal(3, gaps.Count);
        }
        for (int x = 1; x < map.Width - 1; x++)
        {
            Assert.False(map.IsWall(x, 1));
            Assert.False(map.IsWall(x, map.Height - 2));
        }
    }

    [Fact]
    public void OneTeamEntersHighAndTheOtherLow()
    {
        ActorMapDefinition map = Map();
        var west = map.SpawnAnchors
            .Where(a => a.Spawn.SpawnId.StartsWith("team-0-", StringComparison.Ordinal))
            .Select(a => a.Spawn.Position).ToArray();
        var east = map.SpawnAnchors
            .Where(a => a.Spawn.SpawnId.StartsWith("team-1-", StringComparison.Ordinal))
            .Select(a => a.Spawn.Position).ToArray();
        Assert.Equal(8, west.Length);
        Assert.All(west, q => Assert.True(q.Y < 13));
        Assert.All(east, q => Assert.True(q.Y > 13));
        for (int i = 0; i < west.Length; i++)
        {
            Assert.Equal(
                new Position(map.Width - 1 - west[i].X, map.Height - 1 - west[i].Y),
                east[i]);
        }
    }

    [Fact]
    public void TheScoringShoulderIsNotTheSpawnPad()
    {
        ActorMapDefinition map = Map();
        ActorMapRegionDefinition delivery = map.Regions
            .Single(r => r.RegionId == "home-west");
        var protectedTiles = map.TileTags
            .Single(t => t.TagId == "home-pads").Tiles.ToHashSet();
        Assert.Equal(9, delivery.Tiles.Length);
        Assert.All(delivery.Tiles, t => Assert.DoesNotContain(t, protectedTiles));
        Assert.Contains(new Position(2, 5), protectedTiles);
    }

    [Fact]
    public void TheTwoOwnedWellsBirthTogetherOnEverySeed()
    {
        foreach (ulong matchSeed in new ulong[] { 0, 1, 7, 2001, 3012, 99991 })
        {
            var driver = new ArcRelayActorMatchModeDriver(
                ArcRelayH0Definition.Create(
                    loopProfile: ArcRelayLoopProfile.Tapline1),
                matchSeed);
            var arc = Assert.IsType<GenericActorModeState.ArcRelay>(
                driver.State).State;
            Assert.Equal(
                arc.Wells.Single(w => w.WellId == "north").NextScheduledBirthTick,
                arc.Wells.Single(w => w.WellId == "south").NextScheduledBirthTick);
        }
    }

    [Fact]
    public void EveryLandmarkDistanceIsIdenticalFromEitherSide()
    {
        ActorMapDefinition map = Map();
        Position Rotate(Position v) =>
            new(map.Width - 1 - v.X, map.Height - 1 - v.Y);
        Position west = new(2, 5);
        (Position Target, int Steps)[] expected =
        [
            (new Position(7, 7), 5),
            (new Position(11, 6), 10),
            (new Position(9, 19), 14),
            (new Position(15, 13), 14),
            (new Position(19, 20), 18),
            (new Position(21, 7), 19),
            (Rotate(west), 27),
        ];
        foreach ((Position target, int steps) in expected)
        {
            Assert.Equal(steps, Distance(map, west, target));
            Assert.Equal(steps, Distance(map, Rotate(west), Rotate(target)));
        }
    }

    private static int Distance(ActorMapDefinition map, Position from, Position to)
    {
        var seen = new HashSet<Position> { from };
        var queue = new Queue<(Position P, int S)>();
        queue.Enqueue((from, 0));
        while (queue.Count > 0)
        {
            (Position p, int s) = queue.Dequeue();
            if (p == to)
                return s;
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                var n = new Position(p.X + dx, p.Y + dy);
                if (n.X < 0 || n.Y < 0 || n.X >= map.Width || n.Y >= map.Height
                    || map.IsWall(n) || !seen.Add(n))
                {
                    continue;
                }
                queue.Enqueue((n, s + 1));
            }
        }
        return -1;
    }
}
