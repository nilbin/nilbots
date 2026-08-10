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

        (string Label, Position Target, int Steps)[] expected =
        [
            ("own delivery", new Position(7, 6), 5),
            ("own near Well", new Position(9, 3), 7),
            ("near heal", new Position(10, 18), 12),
            ("centre Well", new Position(15, 11), 13),
            ("far heal", new Position(20, 4), 18),
            ("enemy near Well", new Position(21, 19), 19),
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
