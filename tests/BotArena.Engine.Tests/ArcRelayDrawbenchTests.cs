using BotArena.Engine;
using BotArena.Sdk;

namespace BotArena.Engine.Tests;

/// <summary>
/// Drawbench keeps Crucible's tiering but turns the frame horizontal: the
/// reactors sit on the west and east edges at opposite heights. These tests
/// hold the properties that follow from that arrangement.
/// </summary>
public sealed class ArcRelayDrawbenchTests
{
    private static ActorMapDefinition Map() => ArcRelayH0Definition
        .Create(loopProfile: ArcRelayLoopProfile.Drawbench1).Map;

    [Fact]
    public void TheCanvasIsExactlyOneHundredEightyDegreeChiral()
    {
        ActorMapDefinition map = Map();
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            Assert.Equal(
                map.IsWall(x, y),
                map.IsWall(map.Width - 1 - x, map.Height - 1 - y));
        }
    }

    [Fact]
    public void OneTeamEntersHighAndTheOtherLow()
    {
        // The whole point of the horizontal frame: west and east, but not at
        // the same height. Every west anchor sits above the middle row and
        // every east anchor below it, and the two sets are exact rotations.
        ActorMapDefinition map = Map();
        var west = map.SpawnAnchors
            .Where(anchor => anchor.Spawn.SpawnId.StartsWith(
                "team-0-", StringComparison.Ordinal))
            .Select(anchor => anchor.Spawn.Position)
            .ToArray();
        var east = map.SpawnAnchors
            .Where(anchor => anchor.Spawn.SpawnId.StartsWith(
                "team-1-", StringComparison.Ordinal))
            .Select(anchor => anchor.Spawn.Position)
            .ToArray();

        Assert.Equal(8, west.Length);
        Assert.Equal(8, east.Length);
        Assert.All(west, position => Assert.True(position.Y < 11));
        Assert.All(east, position => Assert.True(position.Y > 11));
        Assert.All(west, position => Assert.True(position.X < 15));
        Assert.All(east, position => Assert.True(position.X > 15));
        for (int unitId = 0; unitId < west.Length; unitId++)
        {
            Assert.Equal(
                new Position(
                    map.Width - 1 - west[unitId].X,
                    map.Height - 1 - west[unitId].Y),
                east[unitId]);
        }
    }

    [Fact]
    public void EachOwnedWellSitsOppositeItsOwnSpawnAndOpensTowardIt()
    {
        // The point of the rework: your income is NOT beside your spawn. West
        // enters high and farms low; east enters low and farms high. The
        // pocket still opens toward its owner, so the mouths a raider must
        // reach are the ones furthest from the raider's approach.
        ActorMapDefinition map = Map();
        Position westWell = new(9, 17);
        Position eastWell = new(21, 5);

        Assert.Equal(
            eastWell,
            new Position(
                map.Width - 1 - westWell.X, map.Height - 1 - westWell.Y));
        Assert.True(westWell.Y > 11, "west farms below the middle row");
        Assert.True(eastWell.Y < 11, "east farms above the middle row");

        Position[] westMouths = [.. Neighbours(map, westWell)];
        Assert.Equal(5, westMouths.Length);
        Assert.All(westMouths, mouth =>
            Assert.True(mouth.X <= westWell.X, "west's Well opens westward"));
        Position[] eastMouths = [.. Neighbours(map, eastWell)];
        Assert.Equal(5, eastMouths.Length);
        Assert.All(eastMouths, mouth =>
            Assert.True(mouth.X >= eastWell.X, "east's Well opens eastward"));
    }

    private static IEnumerable<Position> Neighbours(
        ActorMapDefinition map,
        Position tile)
    {
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dy == 0)
                continue;
            var next = new Position(tile.X + dx, tile.Y + dy);
            if (next.X >= 0 && next.Y >= 0 && next.X < map.Width
                && next.Y < map.Height && !map.IsWall(next))
            {
                yield return next;
            }
        }
    }

    [Fact]
    public void TheCentreWellHasRoomToFightOver()
    {
        // A prize you cannot manoeuvre around is a queue, not a contest. The
        // Well keeps its whole eight-neighbourhood, and no tile within four
        // of it is a squeeze -- the horizontal lane closes three tiles out
        // instead of hard against the prize.
        ActorMapDefinition map = Map();
        Position centre = new(15, 11);

        Assert.Equal(8, Neighbours(map, centre).Count());
        int pinched = 0;
        for (int y = centre.Y - 4; y <= centre.Y + 4; y++)
        for (int x = centre.X - 4; x <= centre.X + 4; x++)
        {
            var tile = new Position(x, y);
            if (x < 1 || y < 1 || x >= map.Width - 1 || y >= map.Height - 1
                || map.IsWall(tile))
            {
                continue;
            }
            if (Neighbours(map, tile).Count() <= 4)
                pinched++;
        }
        Assert.Equal(0, pinched);
        Assert.True(map.IsWall(new Position(12, 11)));
        Assert.True(map.IsWall(new Position(18, 11)));
    }

    [Fact]
    public void NoBodyCanBeTrappedAndNoTileIsADeadEnd()
    {
        // A tile with two ways out is a tile one enemy can seal, and a tile
        // with one is a pocket with no reason to exist. Neither belongs on a
        // map where eight bodies a side share the routes.
        ActorMapDefinition map = Map();
        var trapped = new List<Position>();
        var cuttable = new List<Position>();
        var floor = new List<Position>();
        for (int y = 1; y < map.Height - 1; y++)
        for (int x = 1; x < map.Width - 1; x++)
        {
            var tile = new Position(x, y);
            if (map.IsWall(tile))
                continue;
            floor.Add(tile);
            if (Neighbours(map, tile).Count() <= 2)
                trapped.Add(tile);
        }

        Assert.Empty(trapped);

        // A diagonal escape is not an exit you can see or hold. Every floor
        // tile needs two ORTHOGONAL ways out (three on a landmark), and no
        // two tiles may be joined only corner-to-corner -- that reads as a
        // wall even though the engine lets a body slip through it.
        var nooks = new List<Position>();
        var squeezes = new List<Position>();
        var landmarks = new HashSet<Position>
        {
            new(2, 6), new(28, 16), new(21, 5), new(9, 17), new(15, 11),
            new(18, 3), new(12, 19),
        };
        foreach (Position tile in floor)
        {
            int orthogonal = 0;
            foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                if (!map.IsWall(new Position(tile.X + dx, tile.Y + dy)))
                    orthogonal++;
            }
            if (orthogonal < (landmarks.Contains(tile) ? 3 : 2))
                nooks.Add(tile);
            foreach ((int dx, int dy) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
            {
                var corner = new Position(tile.X + dx, tile.Y + dy);
                if (!map.IsWall(corner)
                    && map.IsWall(new Position(tile.X + dx, tile.Y))
                    && map.IsWall(new Position(tile.X, tile.Y + dy)))
                {
                    squeezes.Add(tile);
                }
            }
        }
        Assert.Empty(nooks);
        Assert.Empty(squeezes);

        // and no single body standing anywhere splits the map in two
        foreach (Position blocker in floor)
        {
            var blocked = new HashSet<Position> { blocker };
            Position start = floor.First(tile => tile != blocker);
            var seen = new HashSet<Position> { start };
            var queue = new Queue<Position>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                foreach (Position next in Neighbours(map, queue.Dequeue()))
                {
                    if (!blocked.Contains(next) && seen.Add(next))
                        queue.Enqueue(next);
                }
            }
            if (seen.Count != floor.Count - 1)
                cuttable.Add(blocker);
        }
        Assert.Empty(cuttable);
    }

    [Fact]
    public void TheScoringShoulderIsNotTheSpawnPad()
    {
        // Delivery sits forward of the pad and carries no spawn protection,
        // so holding the objective means standing where you can be shot.
        ActorMapDefinition map = Map();
        ActorMapRegionDefinition delivery = map.Regions.Single(region =>
            region.RegionId == "home-west");
        var protectedTiles = map.TileTags
            .Single(tag => tag.TagId == "home-pads")
            .Tiles
            .ToHashSet();

        Assert.Equal(9, delivery.Tiles.Length);
        Assert.All(delivery.Tiles, tile =>
            Assert.DoesNotContain(tile, protectedTiles));
        Assert.Contains(new Position(2, 6), protectedTiles);
    }

    [Fact]
    public void TheTwoOwnedNearWellsBirthTogetherOnEverySeed()
    {
        foreach (ulong matchSeed in new ulong[] { 0, 1, 7, 2001, 3012, 99991 })
        {
            var driver = new ArcRelayActorMatchModeDriver(
                ArcRelayH0Definition.Create(
                    loopProfile: ArcRelayLoopProfile.Drawbench1),
                matchSeed);
            var arc = Assert.IsType<GenericActorModeState.ArcRelay>(
                driver.State).State;
            Assert.Equal(
                arc.Wells.Single(well => well.WellId == "north")
                    .NextScheduledBirthTick,
                arc.Wells.Single(well => well.WellId == "south")
                    .NextScheduledBirthTick);
        }
    }

    [Fact]
    public void EveryLandmarkDistanceIsIdenticalFromEitherSide()
    {
        ActorMapDefinition map = Map();
        Position Rotate(Position value) =>
            new(map.Width - 1 - value.X, map.Height - 1 - value.Y);
        Position west = new(2, 6);
        Position east = Rotate(west);

        // Owned income sits on the OPPOSITE height from the spawn that owns
        // it, so it is a trip rather than a formality -- and still nearer to
        // its owner than to the raider by nine steps.
        (string Label, Position Target, int Steps)[] expected =
        [
            ("own delivery", new Position(7, 6), 5),
            ("own Well, low", new Position(9, 17), 11),
            ("own heal", new Position(12, 19), 13),
            ("centre Well", new Position(15, 11), 13),
            ("enemy heal", new Position(18, 3), 17),
            ("enemy Well, high", new Position(21, 5), 20),
            ("enemy reactor", east, 26),
        ];
        foreach ((string label, Position target, int steps) in expected)
        {
            Assert.Equal(steps, Distance(map, west, target));
            Assert.Equal(steps, Distance(map, east, Rotate(target)));
        }
    }

    /// <summary>Eight-way shortest path over floor, the movement the engine
    /// actually allows.</summary>
    private static int Distance(
        ActorMapDefinition map,
        Position from,
        Position to)
    {
        var seen = new HashSet<Position> { from };
        var queue = new Queue<(Position Position, int Steps)>();
        queue.Enqueue((from, 0));
        while (queue.Count > 0)
        {
            (Position position, int steps) = queue.Dequeue();
            if (position == to)
                return steps;
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                var next = new Position(position.X + dx, position.Y + dy);
                if (next.X < 0 || next.Y < 0
                    || next.X >= map.Width || next.Y >= map.Height
                    || map.IsWall(next) || !seen.Add(next))
                {
                    continue;
                }
                queue.Enqueue((next, steps + 1));
            }
        }
        return -1;
    }
}
