using System.Text.Json.Nodes;
using BotArena.Engine;

namespace BotArena.Engine.Tests;

public class FrontlineMapTests
{
    [Fact]
    public void Frontline01_PinsPurposeBuiltGeometryAndTravelEnvelope()
    {
        ArenaMap map = LoadFrontlineMap();
        FrontlineMapProfile profile = Assert.IsType<FrontlineMapProfile>(map.Frontline);

        Assert.Equal(2, map.FormatVersion);
        Assert.Equal("frontline-01", map.Id);
        Assert.Equal(23, map.Width);
        Assert.Equal(15, map.Height);
        Assert.Equal(
            [
                new Spawn(2, 7, Direction.East),
                new Spawn(20, 7, Direction.West),
            ],
            map.Spawns);
        Assert.Equal([0, 1, 2, 3, 4],
            profile.Positions.Select(position => position.PositionIndex));
        Assert.Equal([4, 4, 6, 4, 4],
            profile.Positions.Select(position => position.Tiles.Length));
        Assert.Equal([0, 1], profile.TeamHomes.Select(home => home.TeamId));
        Assert.All(profile.TeamHomes,
            home => Assert.Equal(6, home.ProtectedSpawnPad.Length));

        var anchorForbidden = profile.AnchorForbiddenTiles.ToHashSet();
        Assert.All(
            profile.Positions.SelectMany(position => position.Tiles),
            tile => Assert.Contains(tile, anchorForbidden));
        Assert.All(
            profile.TeamHomes.SelectMany(home => home.ProtectedSpawnPad),
            tile => Assert.Contains(tile, anchorForbidden));

        Assert.Equal(8, FloorDistance(
            map,
            new Position(2, 7),
            profile.Positions[2].Tiles));
        Assert.Equal(8, FloorDistance(
            map,
            new Position(20, 7),
            profile.Positions[2].Tiles));
        Assert.Equal(5, FloorDistance(
            map,
            new Position(2, 7),
            profile.Positions[1].Tiles));
        Assert.Equal(5, FloorDistance(
            map,
            new Position(20, 7),
            profile.Positions[3].Tiles));
        Assert.Equal(14, FloorDistance(
            map,
            new Position(2, 7),
            profile.Positions[3].Tiles));
        Assert.Equal(14, FloorDistance(
            map,
            new Position(20, 7),
            profile.Positions[1].Tiles));
        for (int index = 0; index < profile.Positions.Length - 1; index++)
        {
            Assert.Equal(4, FloorDistance(
                map,
                profile.Positions[index].Tiles,
                profile.Positions[index + 1].Tiles));
        }
    }

    [Fact]
    public void Frontline01_NoLegalAnchorCanFireIntoAPrimeSpawnWithinProjectileRange()
    {
        ArenaMap map = LoadFrontlineMap();
        FrontlineMapProfile profile = Assert.IsType<FrontlineMapProfile>(map.Frontline);
        GameRules rules = GameRules.V0_5 with
        {
            ZoneControl = false,
            ActiveZoneControl = false,
            SeedSpawnVariation = false,
            Frontline = new FrontlineRules(),
        };

        var threats = FrontlineMapSafety.FindAnchorSpawnThreats(
            rules,
            map,
            rules.Frontline!,
            profile);
        Assert.True(
            threats.IsEmpty,
            "Unsafe legal Anchors: " + string.Join(
                ", ",
                threats.Select(threat => threat.AnchorTile)));
    }

    [Fact]
    public void OmnidirectionalTurretSafety_IncludesDiagonalBaseHeadings()
    {
        ArenaMap map = DiagonalAimMap();
        FrontlineMapProfile profile = Assert.IsType<FrontlineMapProfile>(map.Frontline);
        var anchor = new Position(3, 3);
        FrontlineRules directional = new()
        {
            FrontlinePositionCount = 3,
            PushesToBreach = 2,
            TurretForm = new FrontlineRules().TurretForm with
            {
                OmnidirectionalShooting = false,
                AllowsProgrammedShots = false,
            },
        };
        GameRules outerRules = GameRules.V0_1 with
        {
            ShotRange = 8,
            Frontline = directional,
        };

        Assert.DoesNotContain(
            FrontlineMapSafety.FindAnchorSpawnThreats(
                outerRules,
                map,
                directional,
                profile),
            threat => threat.AnchorTile == anchor);

        FrontlineRules omnidirectional = directional with
        {
            TurretForm = directional.TurretForm with
            {
                OmnidirectionalShooting = true,
            },
        };
        FrontlineAnchorSpawnThreat threat = Assert.Single(
            FrontlineMapSafety.FindAnchorSpawnThreats(
                    outerRules with { Frontline = omnidirectional },
                    map,
                    omnidirectional,
                    profile)
                .Where(candidate => candidate.AnchorTile == anchor));
        Assert.Equal(0, threat.TeamId);
        Assert.Equal(ProjectileHeading.NorthWest, threat.LaunchHeading);
        Assert.Equal(ShotProgram.Straight, threat.Program);
    }

    [Fact]
    public void Format2_CanonicalizesTeamAndTrueSetOrder()
    {
        ArenaMap baseline = LoadFrontlineMap();
        JsonNode root = LoadFrontlineJson();
        JsonArray spawns = root["spawns"]!.AsArray();
        Swap(spawns, 0, 1);
        JsonArray homePads = root["frontline"]!["homePads"]!.AsArray();
        Swap(homePads, 0, 1);
        foreach (JsonNode? position in root["frontline"]!["positions"]!.AsArray())
            Reverse(position!["tiles"]!.AsArray());
        Reverse(root["frontline"]!["anchorForbiddenTiles"]!.AsArray());

        ArenaMap reordered = ArenaMap.FromJson(root.ToJsonString());

        Assert.Equal(baseline.Spawns, reordered.Spawns);
        Assert.Equal(
            baseline.Frontline!.TeamHomes.Select(home => home.TeamId),
            reordered.Frontline!.TeamHomes.Select(home => home.TeamId));
        for (int index = 0; index < baseline.Frontline.TeamHomes.Length; index++)
        {
            Assert.Equal(
                baseline.Frontline.TeamHomes[index].PrimeSpawn,
                reordered.Frontline.TeamHomes[index].PrimeSpawn);
            Assert.Equal(
                baseline.Frontline.TeamHomes[index].ProtectedSpawnPad.ToArray(),
                reordered.Frontline.TeamHomes[index].ProtectedSpawnPad.ToArray());
        }
        for (int index = 0; index < baseline.Frontline.Positions.Length; index++)
        {
            Assert.Equal(
                baseline.Frontline.Positions[index].PositionIndex,
                reordered.Frontline.Positions[index].PositionIndex);
            Assert.Equal(
                baseline.Frontline.Positions[index].Tiles.ToArray(),
                reordered.Frontline.Positions[index].Tiles.ToArray());
        }
        Assert.Equal(
            baseline.Frontline.AnchorForbiddenTiles.ToArray(),
            reordered.Frontline.AnchorForbiddenTiles.ToArray());
    }

    [Fact]
    public void Format2_StructurallyAcceptsAnotherOddPositionCount()
    {
        JsonNode root = LoadFrontlineJson();
        JsonArray positions = root["frontline"]!["positions"]!.AsArray();
        positions.RemoveAt(positions.Count - 1);
        positions.RemoveAt(0);

        ArenaMap map = ArenaMap.FromJson(root.ToJsonString());

        Assert.Equal(3, map.Frontline!.Positions.Length);
        Assert.Equal([0, 1, 2],
            map.Frontline.Positions.Select(position => position.PositionIndex));
    }

    [Fact]
    public void Format2_RejectsMissingAnchorCoverage()
    {
        JsonNode root = LoadFrontlineJson();
        JsonArray forbidden =
            root["frontline"]!["anchorForbiddenTiles"]!.AsArray();
        JsonNode required = forbidden.Single(node =>
            node!.AsArray()[0]!.GetValue<int>() == 6
            && node.AsArray()[1]!.GetValue<int>() == 5)!;
        forbidden.Remove(required);

        MapValidationException exception = Assert.Throws<MapValidationException>(
            () => ArenaMap.FromJson(root.ToJsonString()));

        Assert.Contains(exception.Errors,
            error => error.Contains("must include gameplay tile (6,5)"));
    }

    [Fact]
    public void Format2_RejectsOverlappingObjectiveRegions()
    {
        JsonNode root = LoadFrontlineJson();
        JsonArray positions = root["frontline"]!["positions"]!.AsArray();
        positions[1]!["tiles"]![0] = JsonNode.Parse("[3,8]");

        MapValidationException exception = Assert.Throws<MapValidationException>(
            () => ArenaMap.FromJson(root.ToJsonString()));

        Assert.Contains(exception.Errors,
            error => error.Contains("appears in more than one position"));
    }

    [Fact]
    public void Format2_RejectsImplicitOrDuplicatePrimeTeams()
    {
        JsonNode root = LoadFrontlineJson();
        root["spawns"]![0]!["teamId"] = null;

        MapValidationException exception = Assert.Throws<MapValidationException>(
            () => ArenaMap.FromJson(root.ToJsonString()));

        Assert.Contains(exception.Errors,
            error => error.Contains("requires teamId 0 or teamId 1"));
    }

    [Fact]
    public void Format2_RejectsNullProfileElementsWithoutANullReference()
    {
        JsonNode nullPosition = LoadFrontlineJson();
        nullPosition["frontline"]!["positions"]![0] = null;
        MapValidationException positionException =
            Assert.Throws<MapValidationException>(() =>
                ArenaMap.FromJson(nullPosition.ToJsonString()));

        JsonNode nullHome = LoadFrontlineJson();
        nullHome["frontline"]!["homePads"]![0] = null;
        MapValidationException homeException =
            Assert.Throws<MapValidationException>(() =>
                ArenaMap.FromJson(nullHome.ToJsonString()));

        Assert.Contains(positionException.Errors,
            error => error.Contains("position 0 cannot be null"));
        Assert.Contains(homeException.Errors,
            error => error.Contains("home pads cannot contain null entries"));
    }

    [Fact]
    public void Format1_RemainsLegacyAndHasNoFrontlineProfile()
    {
        const string json = """
        {
          "formatVersion": 1,
          "id": "legacy",
          "width": 5,
          "height": 3,
          "tiles": ["#####", "#...#", "#####"],
          "spawns": [
            { "x": 1, "y": 1, "facing": "East" },
            { "x": 3, "y": 1, "facing": "West" }
          ]
        }
        """;

        ArenaMap map = ArenaMap.FromJson(json);

        Assert.Equal(1, map.FormatVersion);
        Assert.Null(map.Frontline);
        Assert.Equal(
            [
                new Spawn(1, 1, Direction.East),
                new Spawn(3, 1, Direction.West),
            ],
            map.Spawns);
    }

    private static int FloorDistance(
        ArenaMap map,
        Position origin,
        IReadOnlyCollection<Position> targets) =>
        FloorDistance(map, [origin], targets);

    private static int FloorDistance(
        ArenaMap map,
        IReadOnlyCollection<Position> origins,
        IReadOnlyCollection<Position> targets)
    {
        var targetSet = targets.ToHashSet();
        var seen = origins.ToHashSet();
        var queue = new Queue<(Position Position, int Distance)>(
            origins.Select(origin => (origin, 0)));
        while (queue.TryDequeue(out var item))
        {
            if (targetSet.Contains(item.Position))
                return item.Distance;
            foreach (var (dx, dy) in new[]
                     {
                         (0, -1),
                         (1, 0),
                         (0, 1),
                         (-1, 0),
                     })
            {
                Position next = item.Position.Offset(dx, dy);
                if (!map.IsWall(next) && seen.Add(next))
                    queue.Enqueue((next, item.Distance + 1));
            }
        }
        throw new InvalidOperationException("No floor path between test positions.");
    }

    private static ArenaMap LoadFrontlineMap() =>
        ArenaMap.FromJson(File.ReadAllText(FrontlineMapPath()));

    private static ArenaMap DiagonalAimMap() =>
        ArenaMap.FromJson("""
        {
          "formatVersion": 2,
          "id": "diagonal-aim",
          "version": 1,
          "width": 7,
          "height": 7,
          "tiles": [
            "#######",
            "#.....#",
            "#.....#",
            "#.....#",
            "#.....#",
            "#.....#",
            "#######"
          ],
          "spawns": [
            { "teamId": 0, "x": 1, "y": 1, "facing": "East" },
            { "teamId": 1, "x": 5, "y": 1, "facing": "West" }
          ],
          "frontline": {
            "positions": [
              { "tiles": [[1,5]] },
              { "tiles": [[3,5]] },
              { "tiles": [[5,5]] }
            ],
            "homePads": [
              { "teamId": 0, "tiles": [[1,1], [1,2]] },
              { "teamId": 1, "tiles": [[5,1], [5,2]] }
            ],
            "anchorForbiddenTiles": [
              [1,1], [5,1], [1,2], [5,2], [1,5], [3,5], [5,5]
            ]
          }
        }
        """);

    private static JsonNode LoadFrontlineJson() =>
        JsonNode.Parse(File.ReadAllText(FrontlineMapPath()))
        ?? throw new InvalidOperationException("Frontline map JSON was empty.");

    private static string FrontlineMapPath() =>
        Path.Combine(
            FindRepoRoot(),
            "maps",
            "experimental",
            "frontline-01.json");

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

    private static void Reverse(JsonArray values)
    {
        JsonNode?[] reversed = values
            .Reverse()
            .Select(value => value?.DeepClone())
            .ToArray();
        values.Clear();
        foreach (JsonNode? value in reversed)
            values.Add(value);
    }

    private static void Swap(JsonArray values, int left, int right)
    {
        JsonNode? leftValue = values[left]?.DeepClone();
        JsonNode? rightValue = values[right]?.DeepClone();
        values[left] = rightValue;
        values[right] = leftValue;
    }
}
