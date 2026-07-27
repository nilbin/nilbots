namespace BotArena.Engine.Tests;

public class PublicMapManifestTests
{
    [Fact]
    public void MapProjection_PreservesObservableSpawnAndObjectiveOrder()
    {
        ArenaMap map = CreateMap(
            id: "ordered",
            version: 7,
            spawns:
            [
                new Spawn(3, 1, Direction.West),
                new Spawn(1, 1, Direction.East),
            ],
            zone: [new Position(3, 1), new Position(1, 1), new Position(2, 1)]);

        PublicMapManifest manifest = PublicRulesManifestFactory.CreateMap(map);

        Assert.Equal("ordered", manifest.MapId);
        Assert.Equal(7, manifest.MapVersion);
        Assert.Equal(1, manifest.FormatVersion);
        Assert.Null(manifest.Frontline);
        Assert.Equal([0, 1], manifest.Spawns.Select(spawn => spawn.TeamId));
        Assert.Equal(new Position(3, 1), manifest.Spawns[0].Position);
        Assert.Equal(new Position(1, 1), manifest.Spawns[1].Position);
        Assert.Equal(
            [new Position(3, 1), new Position(1, 1), new Position(2, 1)],
            manifest.ObjectiveTiles.ToArray());
        Assert.Matches("^[0-9a-f]{64}$", manifest.MapFingerprint);
    }

    [Fact]
    public void ProvenanceAndPresentation_DoNotChangeMapFingerprint()
    {
        MapPresentation presentation = new(
            "perimeter",
            "cover",
            [new MapWallGroup("damaged", [new Position(0, 1)])]);
        ArenaMap first = CreateMap("first", version: 1, themeId: null, presentation: null);
        ArenaMap second = CreateMap(
            "second",
            version: 99,
            themeId: "control-room",
            presentation: presentation);

        Assert.Equal(
            PublicRulesManifestFactory.CreateMap(first).MapFingerprint,
            PublicRulesManifestFactory.CreateMap(second).MapFingerprint);
    }

    [Fact]
    public void SpawnOrder_IsGameplayContent()
    {
        Spawn[] spawns =
        [
            new Spawn(1, 1, Direction.East),
            new Spawn(3, 1, Direction.West),
        ];
        ArenaMap first = CreateMap("first", spawns: spawns);
        ArenaMap swapped = CreateMap("second", spawns: [spawns[1], spawns[0]]);

        Assert.NotEqual(
            PublicRulesManifestFactory.CreateMap(first).MapFingerprint,
            PublicRulesManifestFactory.CreateMap(swapped).MapFingerprint);
    }

    [Fact]
    public void ObjectiveTileDeclarationOrder_IsObservableContractContent()
    {
        Position[] zone =
        [
            new Position(1, 1),
            new Position(2, 1),
            new Position(3, 1),
        ];
        ArenaMap first = CreateMap("first", zone: zone);
        ArenaMap reordered = CreateMap("second", zone: zone.Reverse().ToArray());

        Assert.NotEqual(
            PublicRulesManifestFactory.CreateMap(first).MapFingerprint,
            PublicRulesManifestFactory.CreateMap(reordered).MapFingerprint);
    }

    [Fact]
    public void DuplicateObjectiveTiles_RemainObservableContractContent()
    {
        ArenaMap first = CreateMap(
            "first",
            zone: [new Position(1, 1), new Position(2, 1)]);
        ArenaMap duplicated = CreateMap(
            "second",
            zone:
            [
                new Position(1, 1),
                new Position(1, 1),
                new Position(2, 1),
            ]);

        Assert.NotEqual(
            PublicRulesManifestFactory.CreateMap(first).MapFingerprint,
            PublicRulesManifestFactory.CreateMap(duplicated).MapFingerprint);
        Assert.Equal(
            duplicated.EffectiveZone().ToArray(),
            PublicRulesManifestFactory.CreateMap(duplicated).ObjectiveTiles.ToArray());
    }

    [Fact]
    public void GeometryMutation_ChangesMapFingerprint()
    {
        ArenaMap first = CreateMap("first");
        ArenaMap changed = ArenaMap.Create(
            "second",
            [
                "#####",
                "#.#.#",
                "#...#",
                "#####",
            ],
            [
                new Spawn(1, 1, Direction.East),
                new Spawn(3, 1, Direction.West),
            ]);

        Assert.NotEqual(
            PublicRulesManifestFactory.CreateMap(first).MapFingerprint,
            PublicRulesManifestFactory.CreateMap(changed).MapFingerprint);
    }

    private static ArenaMap CreateMap(
        string id,
        int version = 1,
        Spawn[]? spawns = null,
        Position[]? zone = null,
        string? themeId = null,
        MapPresentation? presentation = null) =>
        ArenaMap.Create(
            id,
            [
                "#####",
                "#...#",
                "#...#",
                "#####",
            ],
            spawns ??
            [
                new Spawn(1, 1, Direction.East),
                new Spawn(3, 1, Direction.West),
            ],
            version,
            zone,
            themeId,
            presentation);
}
