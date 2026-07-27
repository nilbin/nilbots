using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

public class ActorMapDefinitionTests
{
    [Fact]
    public void FourSpawnMapSupportsFfaAndCanonicalizesContractSets()
    {
        var map = new ActorMapDefinition(
            "crossroads-ffa",
            version: 1,
            [
                "#########",
                "#.......#",
                "#.......#",
                "#.......#",
                "#.......#",
                "#.......#",
                "#########",
            ],
            [
                Spawn("west", 1, 3, Direction.East),
                Spawn("south", 4, 5, Direction.North),
                Spawn(
                    "north",
                    4,
                    1,
                    Direction.South,
                    ActorMovementLayer.Air,
                    ActorMovementLayer.Ground),
                Spawn("east", 7, 3, Direction.West),
            ],
            [
                new(
                    "transition-ring",
                    ActorMapRegionDefinition.RegionKind.TransitionPlacement,
                    [new(5, 3), new(3, 3)]),
                new(
                    "objective-centre",
                    ActorMapRegionDefinition.RegionKind.Objective,
                    [new(4, 4), new(4, 2), new(4, 3)]),
            ],
            [
                new(
                    "protected-spawns",
                    ActorMapTileTagDefinition.TileTagKind.SpawnProtected,
                    [new(7, 3), new(1, 3)]),
                new(
                    "no-transition",
                    ActorMapTileTagDefinition.TileTagKind
                        .TransitionPlacementForbidden,
                    [new(4, 3), new(4, 2)]),
            ]);
        var format = new FreeForAllMatchFormatDefinition(participantCount: 4);

        Assert.Equal(ActorMapDefinition.CurrentFormatVersion, map.FormatVersion);
        Assert.Equal(format.ParticipantCount, map.SpawnAnchors.Count(
            anchor => anchor.CompatibleMovementLayers.Contains(
                ActorMovementLayer.Ground)));
        Assert.Equal(
            ["east", "north", "south", "west"],
            map.SpawnAnchors
                .Select(anchor => anchor.Spawn.SpawnId)
                .ToArray());
        Assert.Equal(
            [ActorMovementLayer.Ground, ActorMovementLayer.Air],
            map.SpawnAnchors[1].CompatibleMovementLayers.ToArray());
        Assert.Equal(
            ["objective-centre", "transition-ring"],
            map.Regions.Select(region => region.RegionId).ToArray());
        Assert.Equal(
            [new Position(4, 2), new Position(4, 3), new Position(4, 4)],
            map.Regions[0].Tiles.ToArray());
        Assert.Equal(
            ["no-transition", "protected-spawns"],
            map.TileTags.Select(tag => tag.TagId).ToArray());
        Assert.True(map.IsWall(0, 0));
        Assert.True(map.IsWall(-1, 3));
        Assert.True(map.IsWall(9, 3));
        Assert.False(map.IsWall(new Position(4, 3)));
    }

    [Fact]
    public void RejectsNonCanonicalAndDuplicateSemanticIds()
    {
        MapValidationException error = Assert.Throws<MapValidationException>(() =>
            new ActorMapDefinition(
                "Bad Map",
                version: 1,
                OpenGrid(),
                [
                    Spawn("same", 1, 1, Direction.East),
                    Spawn("same", 3, 3, Direction.West),
                ],
                [
                    new(
                        "Bad Region",
                        ActorMapRegionDefinition.RegionKind.Objective,
                        [new(2, 2)]),
                ],
                [
                    new(
                        "bad--tag",
                        ActorMapTileTagDefinition.TileTagKind
                            .TransitionPlacementForbidden,
                        [new(2, 3)]),
                ]));

        Assert.Contains(error.Errors, message =>
            message.Contains("Map id", StringComparison.Ordinal));
        Assert.Contains(error.Errors, message =>
            message.Contains("declared more than once", StringComparison.Ordinal));
        Assert.Contains(error.Errors, message =>
            message.Contains("Region id", StringComparison.Ordinal));
        Assert.Contains(error.Errors, message =>
            message.Contains("Tile tag id", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsRaggedOversizedAndInvalidTileGeometry()
    {
        MapValidationException ragged = Assert.Throws<MapValidationException>(() =>
            new ActorMapDefinition(
                "ragged",
                version: 1,
                [".....", "....", "..x.."],
                [Spawn("one", 1, 1, Direction.East)],
                [],
                []));
        ImmutableArray<string> oversized = Enumerable
            .Repeat(
                new string('.', ArenaMap.MaxWidth + 1),
                3)
            .ToImmutableArray();
        MapValidationException tooLarge = Assert.Throws<MapValidationException>(() =>
            new ActorMapDefinition(
                "too-large",
                version: 1,
                oversized,
                [Spawn("one", 1, 1, Direction.East)],
                [],
                []));

        Assert.Contains(ragged.Errors, message =>
            message.Contains("length", StringComparison.Ordinal));
        Assert.Contains(ragged.Errors, message =>
            message.Contains("Invalid tile symbol", StringComparison.Ordinal));
        Assert.Contains(tooLarge.Errors, message =>
            message.Contains("at most", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsIllegalOrAmbiguousSpawnAnchors()
    {
        MapValidationException error = Assert.Throws<MapValidationException>(() =>
            new ActorMapDefinition(
                "bad-spawns",
                version: 1,
                [
                    "#####",
                    "#...#",
                    "#...#",
                    "#...#",
                    "#####",
                ],
                [
                    Spawn("wall", 0, 0, Direction.East),
                    Spawn("duplicate-position", 0, 0, Direction.West),
                    Spawn(
                        "duplicate-layer",
                        2,
                        2,
                        Direction.North,
                        ActorMovementLayer.Ground,
                        ActorMovementLayer.Ground),
                    new(
                        new InitialSpawnDefinition(
                            "unknown-layer",
                            new Position(3, 3),
                            Direction.North),
                        [(ActorMovementLayer)99]),
                ],
                [],
                []));

        Assert.Contains(error.Errors, message =>
            message.Contains("in-bounds floor", StringComparison.Ordinal));
        Assert.Contains(error.Errors, message =>
            message.Contains("position", StringComparison.Ordinal)
            && message.Contains("more than once", StringComparison.Ordinal));
        Assert.Contains(error.Errors, message =>
            message.Contains("movement layer 'Ground'", StringComparison.Ordinal));
        Assert.Contains(error.Errors, message =>
            message.Contains("unknown movement layer", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsDuplicateOrNonFloorRegionAndTagTiles()
    {
        MapValidationException error = Assert.Throws<MapValidationException>(() =>
            new ActorMapDefinition(
                "bad-sets",
                version: 1,
                [
                    "#####",
                    "#...#",
                    "#...#",
                    "#...#",
                    "#####",
                ],
                [Spawn("one", 1, 1, Direction.East)],
                [
                    new(
                        "objective",
                        ActorMapRegionDefinition.RegionKind.Objective,
                        [new(2, 2), new(2, 2), new(0, 0)]),
                ],
                [
                    new(
                        "no-transition",
                        ActorMapTileTagDefinition.TileTagKind
                            .TransitionPlacementForbidden,
                        [new(3, 3), new(3, 3), new(9, 9)]),
                ]));

        Assert.Contains(error.Errors, message =>
            message.Contains("Region 'objective' contains duplicate", StringComparison.Ordinal));
        Assert.Contains(error.Errors, message =>
            message.Contains("Region 'objective' tile", StringComparison.Ordinal)
            && message.Contains("in-bounds floor", StringComparison.Ordinal));
        Assert.Contains(error.Errors, message =>
            message.Contains("Tile tag 'no-transition' contains duplicate", StringComparison.Ordinal));
        Assert.Contains(error.Errors, message =>
            message.Contains("Tile tag 'no-transition' tile", StringComparison.Ordinal)
            && message.Contains("in-bounds floor", StringComparison.Ordinal));
    }

    private static ImmutableArray<string> OpenGrid() =>
    [
        "#####",
        "#...#",
        "#...#",
        "#...#",
        "#####",
    ];

    private static ActorMapSpawnAnchorDefinition Spawn(
        string id,
        int x,
        int y,
        Direction facing,
        params ActorMovementLayer[] layers) =>
        new(
            new InitialSpawnDefinition(id, new Position(x, y), facing),
            layers.Length == 0
                ? [ActorMovementLayer.Ground]
                : layers.ToImmutableArray());
}
