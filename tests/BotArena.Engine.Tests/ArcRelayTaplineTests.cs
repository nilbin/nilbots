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

    /// <summary>
    /// Movement is corner-strict -- a diagonal step needs both orthogonal
    /// companions open, the same rule LOS and the strike cone already apply.
    /// Two consequences have to be authored in, because neither shows up as a
    /// failure: a wall corner that reads like an opening is a lie to the
    /// player, and a tile whose occupant splits the map is a congestion
    /// generator on a map built around contested traffic. The first draft had
    /// fourteen of the former and sixteen of the latter.
    /// </summary>
    private static bool CanStep(ActorMapDefinition map, Position from, Position to)
    {
        if (to.X < 0 || to.Y < 0 || to.X >= map.Width || to.Y >= map.Height
            || map.IsWall(to))
        {
            return false;
        }
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        return dx == 0 || dy == 0
            || (!map.IsWall(new Position(from.X + dx, from.Y))
                && !map.IsWall(new Position(from.X, from.Y + dy)));
    }

    private static List<Position> Floor(ActorMapDefinition map)
    {
        var tiles = new List<Position>();
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var p = new Position(x, y);
                if (!map.IsWall(p))
                {
                    tiles.Add(p);
                }
            }
        }
        return tiles;
    }

    private static int ReachableCount(
        ActorMapDefinition map, Position start, Position? without)
    {
        var seen = new HashSet<Position> { start };
        var queue = new Queue<Position>();
        queue.Enqueue(start);
        int count = 0;
        while (queue.Count > 0)
        {
            Position p = queue.Dequeue();
            count++;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }
                    var n = new Position(p.X + dx, p.Y + dy);
                    if (n == without || !CanStep(map, p, n) || !seen.Add(n))
                    {
                        continue;
                    }
                    queue.Enqueue(n);
                }
            }
        }
        return count;
    }

    [Fact]
    public void NoWallCornerPretendsToBeAPassage()
    {
        ActorMapDefinition map = Map();
        var lies = new List<string>();
        foreach (Position p in Floor(map))
        {
            foreach ((int dx, int dy) in new[] { (1, 1), (1, -1) })
            {
                var d = new Position(p.X + dx, p.Y + dy);
                if (d.X < 0 || d.Y < 0 || d.X >= map.Width || d.Y >= map.Height
                    || map.IsWall(d))
                {
                    continue;
                }
                if (map.IsWall(new Position(p.X + dx, p.Y))
                    && map.IsWall(new Position(p.X, p.Y + dy)))
                {
                    lies.Add($"({p.X},{p.Y})-({d.X},{d.Y})");
                }
            }
        }
        Assert.True(lies.Count == 0,
            "corner-touching tiles read as open but cannot be crossed: "
                + string.Join(", ", lies));
    }

    [Fact]
    public void NoSingleBodyStandingStillCanSeverMoreThanACornerNook()
    {
        // Severity is the measure, not mere existence. The shadow corridor's
        // four ends are two-tile stubs past the outermost seal gap, so a body
        // there cuts off a nook -- that follows from the authored flank (row 1
        // full width, exactly three gaps in row 2) and costs nothing. A body
        // that severs more than a nook is a different animal: it is a traffic
        // jam that no amount of executor re-routing can dissolve.
        const int Nook = 2;
        ActorMapDefinition map = Map();
        List<Position> floor = Floor(map);
        Assert.Equal(floor.Count, ReachableCount(map, floor[0], without: null));

        var severe = new List<string>();
        foreach (Position blocker in floor)
        {
            Position start = floor.First(t => t != blocker);
            int reached = ReachableCount(map, start, without: blocker);
            // `start` may itself sit in the cut-off piece, so the severed side
            // is the smaller of the two -- not whatever this walk happened to
            // reach from the top-left tile.
            int severed = Math.Min(reached, floor.Count - 1 - reached);
            if (severed > Nook)
            {
                severe.Add($"({blocker.X},{blocker.Y}) severs {severed}");
            }
        }
        Assert.True(severe.Count == 0,
            "one body standing on these tiles severs more than a nook: "
                + string.Join(", ", severe));
    }

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
        // Corner-strict distances. The earlier 10 / 14 / 18 were measured with
        // a permissive walk that cut wall corners no body may cut; the map has
        // since been opened so the honest numbers land back near them.
        Assert.Equal(12, own);
        Assert.Equal(14, middle);
        Assert.Equal(21, far);
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

        // No anchor may be caged by its own siblings: every one needs at
        // least two ways out that another spawn tile does not already own.
        var cluster = west.ToHashSet();
        foreach (Position anchor in west)
        {
            int ways = 0;
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;
                var next = new Position(anchor.X + dx, anchor.Y + dy);
                if (!map.IsWall(next) && !cluster.Contains(next))
                    ways++;
            }
            Assert.True(ways >= 2, $"anchor {anchor} has {ways} free exits");
        }
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
        // Corner-strict steps: what a body actually walks. The mirror is the
        // property under test; the absolute values are pinned so a density
        // pass cannot quietly stretch or shorten the map.
        (Position Target, int Steps)[] expected =
        [
            (new Position(7, 7), 5),     // own delivery
            (new Position(11, 6), 12),   // own Well, up-line
            (new Position(9, 19), 16),   // own heal
            (new Position(15, 13), 14),  // centre Well
            (new Position(19, 20), 21),  // enemy Well, down-line
            (new Position(21, 7), 21),   // enemy heal
            (Rotate(west), 28),          // enemy reactor
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
                // Corner-strict, because that is how a body actually travels.
                // Measuring with a permissive walk reports distances no body
                // can achieve and hides geometry that only looks passable.
                var n = new Position(p.X + dx, p.Y + dy);
                if (!CanStep(map, p, n) || !seen.Add(n))
                {
                    continue;
                }
                queue.Enqueue((n, s + 1));
            }
        }
        return -1;
    }
}
