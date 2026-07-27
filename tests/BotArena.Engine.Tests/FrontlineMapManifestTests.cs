using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

public class FrontlineMapManifestTests
{
    [Fact]
    public void FrontlineMap_ProjectsOrderedPositionsHomesAndAnchorBoundary()
    {
        ArenaMap map = LoadFrontlineMap();

        PublicMapManifest manifest = PublicRulesManifestFactory.CreateMap(map);

        Assert.Equal(2, manifest.FormatVersion);
        Assert.Empty(manifest.ObjectiveTiles);
        PublicFrontlineMapDefinition frontline =
            Assert.IsType<PublicFrontlineMapDefinition>(manifest.Frontline);
        Assert.Equal(
            map.Frontline!.Positions.Select(position => position.PositionIndex),
            frontline.Positions.Select(position => position.PositionIndex));
        Assert.Equal(
            map.Frontline.Positions.Select(position => position.Tiles.ToArray()),
            frontline.Positions.Select(position => position.Tiles.ToArray()));
        Assert.Equal(
            map.Frontline.AnchorForbiddenTiles.ToArray(),
            frontline.AnchorForbiddenTiles.ToArray());

        Assert.Equal([0, 1], manifest.Spawns.Select(spawn => spawn.TeamId));
        Assert.Equal([0, 1], frontline.TeamHomes.Select(home => home.TeamId));
        foreach (PublicFrontlineTeamHome home in frontline.TeamHomes)
        {
            PublicMapSpawn spawn =
                manifest.Spawns.Single(candidate => candidate.TeamId == home.TeamId);
            Assert.Equal(home.PrimeSpawnPosition, spawn.Position);
            Assert.Equal(home.PrimeSpawnFacing, spawn.Facing);
            Assert.Equal(
                map.Frontline.TeamHomes
                    .Single(candidate => candidate.TeamId == home.TeamId)
                    .ProtectedSpawnPad
                    .ToArray(),
                home.ProtectedSpawnPad.ToArray());
        }
    }

    public static IEnumerable<object[]> FrontlineProfileMutations()
    {
        yield return Case(
            "Positions.Order",
            manifest => WithFrontline(
                manifest,
                frontline => frontline with
                {
                    Positions = frontline.Positions.Reverse().ToImmutableArray(),
                }));
        yield return Case(
            "Positions.PositionIndex",
            manifest => WithFrontline(
                manifest,
                frontline => frontline with
                {
                    Positions = frontline.Positions.SetItem(
                        0,
                        frontline.Positions[0] with
                        {
                            PositionIndex =
                                frontline.Positions[0].PositionIndex + 1,
                        }),
                }));
        yield return Case(
            "Positions.Tiles",
            manifest => WithFrontline(
                manifest,
                frontline => frontline with
                {
                    Positions = frontline.Positions.SetItem(
                        0,
                        frontline.Positions[0] with
                        {
                            Tiles = frontline.Positions[0].Tiles.SetItem(
                                0,
                                new Position(2, 2)),
                        }),
                }));
        yield return Case(
            "TeamHomes.TeamId",
            manifest => WithFrontline(
                manifest,
                frontline => frontline with
                {
                    TeamHomes = frontline.TeamHomes.SetItem(
                        0,
                        frontline.TeamHomes[0] with { TeamId = 2 }),
                }));
        yield return Case(
            "TeamHomes.PrimeSpawnPosition",
            manifest => WithFrontline(
                manifest,
                frontline => frontline with
                {
                    TeamHomes = frontline.TeamHomes.SetItem(
                        0,
                        frontline.TeamHomes[0] with
                        {
                            PrimeSpawnPosition =
                                frontline.TeamHomes[0].PrimeSpawnPosition.Offset(1, 0),
                        }),
                }));
        yield return Case(
            "TeamHomes.PrimeSpawnFacing",
            manifest => WithFrontline(
                manifest,
                frontline => frontline with
                {
                    TeamHomes = frontline.TeamHomes.SetItem(
                        0,
                        frontline.TeamHomes[0] with
                        {
                            PrimeSpawnFacing = Direction.North,
                        }),
                }));
        yield return Case(
            "TeamHomes.ProtectedSpawnPad",
            manifest => WithFrontline(
                manifest,
                frontline => frontline with
                {
                    TeamHomes = frontline.TeamHomes.SetItem(
                        0,
                        frontline.TeamHomes[0] with
                        {
                            ProtectedSpawnPad =
                                frontline.TeamHomes[0].ProtectedSpawnPad.SetItem(
                                    0,
                                    new Position(3, 2)),
                        }),
                }));
        yield return Case(
            "AnchorForbiddenTiles",
            manifest => WithFrontline(
                manifest,
                frontline => frontline with
                {
                    AnchorForbiddenTiles =
                        frontline.AnchorForbiddenTiles.SetItem(
                            0,
                            new Position(3, 1)),
                }));
    }

    [Theory]
    [MemberData(nameof(FrontlineProfileMutations))]
    public void EveryFrontlineProfileMutation_ChangesMapFingerprint(
        string propertyPath,
        Func<PublicMapManifest, PublicMapManifest> mutate)
    {
        PublicMapManifest baseline =
            PublicRulesManifestFactory.CreateMap(LoadFrontlineMap());
        PublicMapManifest changed = mutate(baseline) with { MapFingerprint = "" };

        Assert.NotEqual(
            baseline.MapFingerprint,
            MatchContractFingerprint.ComputeMap(changed));
        Assert.False(string.IsNullOrWhiteSpace(propertyPath));
    }

    [Fact]
    public void FrontlineTileSetsAndTeamHomes_AreCanonicalSets()
    {
        PublicMapManifest baseline =
            PublicRulesManifestFactory.CreateMap(LoadFrontlineMap());
        PublicFrontlineMapDefinition frontline = baseline.Frontline!;
        PublicMapManifest reordered = baseline with
        {
            MapFingerprint = "",
            Frontline = frontline with
            {
                Positions = frontline.Positions
                    .Select(position => position with
                    {
                        Tiles = position.Tiles.Reverse().ToImmutableArray(),
                    })
                    .ToImmutableArray(),
                TeamHomes = frontline.TeamHomes
                    .Reverse()
                    .Select(home => home with
                    {
                        ProtectedSpawnPad =
                            home.ProtectedSpawnPad.Reverse().ToImmutableArray(),
                    })
                    .ToImmutableArray(),
                AnchorForbiddenTiles =
                    frontline.AnchorForbiddenTiles.Reverse().ToImmutableArray(),
            },
        };

        Assert.Equal(
            baseline.MapFingerprint,
            MatchContractFingerprint.ComputeMap(reordered));
    }

    private static object[] Case(
        string propertyPath,
        Func<PublicMapManifest, PublicMapManifest> mutate) =>
        [propertyPath, mutate];

    private static PublicMapManifest WithFrontline(
        PublicMapManifest manifest,
        Func<PublicFrontlineMapDefinition, PublicFrontlineMapDefinition> mutate) =>
        manifest with
        {
            Frontline = mutate(manifest.Frontline!),
        };

    private static ArenaMap LoadFrontlineMap() =>
        ArenaMap.FromJson(File.ReadAllText(
            Path.Combine(
                FindRepoRoot(),
                "maps",
                "experimental",
                "frontline-01.json")));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "BotArena.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new InvalidOperationException(
                "BotArena.sln not found above the test directory.");
    }
}
