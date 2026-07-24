namespace BotArena.Engine.Tests;

public class MapTests
{
    [Fact]
    public void ValidMapJson_Loads()
    {
        const string json = """
        {
          "formatVersion": 1,
          "id": "basic-01",
          "width": 12,
          "height": 8,
          "theme": "control-room",
          "tiles": [
            "############",
            "#..........#",
            "#..##......#",
            "#..........#",
            "#......##..#",
            "#..........#",
            "#..........#",
            "############"
          ],
          "spawns": [
            { "x": 1, "y": 1, "facing": "East" },
            { "x": 10, "y": 6, "facing": "West" }
          ]
        }
        """;
        var map = ArenaMap.FromJson(json);

        Assert.Equal("basic-01", map.Id);
        Assert.Equal(12, map.Width);
        Assert.Equal(8, map.Height);
        Assert.Equal("control-room", map.ThemeId);
        Assert.True(map.IsWall(3, 2));
        Assert.False(map.IsWall(1, 1));
        Assert.Equal(2, map.Spawns.Count);
    }

    [Fact]
    public void InvalidThemeId_Throws()
    {
        const string json = """
        {
          "formatVersion": 1,
          "id": "bad-theme",
          "width": 5,
          "height": 3,
          "theme": "../surprise",
          "tiles": ["#####", "#...#", "#####"],
          "spawns": [
            { "x": 1, "y": 1, "facing": "East" },
            { "x": 3, "y": 1, "facing": "West" }
          ]
        }
        """;

        var ex = Assert.Throws<MapValidationException>(() => ArenaMap.FromJson(json));

        Assert.Contains(ex.Errors, error => error.Contains("Invalid map theme"));
    }

    [Fact]
    public void OutOfBounds_CountsAsWall()
    {
        var map = ArenaMap.Create("t", ["#####", "#...#", "#####"],
            [new Spawn(1, 1, Direction.East), new Spawn(3, 1, Direction.West)]);
        Assert.True(map.IsWall(-1, 0));
        Assert.True(map.IsWall(0, -1));
        Assert.True(map.IsWall(5, 1));
        Assert.True(map.IsWall(1, 3));
    }

    [Fact]
    public void InvalidTileSymbol_Throws()
    {
        var ex = Assert.Throws<MapValidationException>(() => ArenaMap.Create("t", [
            "#####",
            "#.X.#",
            "#####",
        ], [new Spawn(1, 1, Direction.East), new Spawn(3, 1, Direction.West)]));
        Assert.Contains(ex.Errors, e => e.Contains("Invalid tile symbol"));
    }

    [Fact]
    public void SpawnOnWall_Throws()
    {
        var ex = Assert.Throws<MapValidationException>(() => ArenaMap.Create("t", [
            "#####",
            "#...#",
            "#####",
        ], [new Spawn(0, 0, Direction.East), new Spawn(3, 1, Direction.West)]));
        Assert.Contains(ex.Errors, e => e.Contains("wall"));
    }

    [Fact]
    public void DisconnectedSpawns_Throw()
    {
        var ex = Assert.Throws<MapValidationException>(() => ArenaMap.Create("t", [
            "#####",
            "#.#.#",
            "#####",
        ], [new Spawn(1, 1, Direction.East), new Spawn(3, 1, Direction.West)]));
        Assert.Contains(ex.Errors, e => e.Contains("not connected"));
    }

    [Fact]
    public void DeclaredSizeMismatch_Throws()
    {
        const string json = """
        {
          "formatVersion": 1,
          "id": "bad",
          "width": 10,
          "height": 3,
          "tiles": ["#####", "#...#", "#####"],
          "spawns": [
            { "x": 1, "y": 1, "facing": "East" },
            { "x": 3, "y": 1, "facing": "West" }
          ]
        }
        """;
        var ex = Assert.Throws<MapValidationException>(() => ArenaMap.FromJson(json));
        Assert.Contains(ex.Errors, e => e.Contains("does not match"));
    }

    [Fact]
    public void RankedZones_AreConnectedRegionsWithLocalMovementSpace()
    {
        string root = FindRepoRoot();
        string[] ranked = ["basic-01", "arena-01", "crossfire-01", "bastion-01", "gallery-01"];
        (int Dx, int Dy)[] offsets = [(0, -1), (1, 0), (0, 1), (-1, 0)];

        foreach (string id in ranked)
        {
            var map = ArenaMap.FromJson(File.ReadAllText(Path.Combine(root, "maps", id + ".json")));
            var zone = map.Zone.ToHashSet();
            Assert.True(zone.Count >= 4, $"{id}: ranked zone needs at least four tiles.");
            Assert.True(
                zone.Max(p => p.X) > zone.Min(p => p.X)
                && zone.Max(p => p.Y) > zone.Min(p => p.Y),
                $"{id}: ranked zone must have both horizontal and vertical dodge space.");

            var reached = new HashSet<Position>();
            var queue = new Queue<Position>();
            queue.Enqueue(zone.First());
            reached.Add(zone.First());
            while (queue.TryDequeue(out var tile))
                foreach (var (dx, dy) in offsets)
                {
                    var next = tile.Offset(dx, dy);
                    if (zone.Contains(next) && reached.Add(next))
                        queue.Enqueue(next);
                }
            Assert.Equal(zone.Count, reached.Count);

            var approaches = new HashSet<(int Dx, int Dy)>();
            var surroundingFloor = new HashSet<Position>();
            foreach (var tile in zone)
                foreach (var offset in offsets)
                {
                    var next = tile.Offset(offset.Dx, offset.Dy);
                    if (zone.Contains(next) || map.IsWall(next))
                        continue;
                    approaches.Add(offset);
                    surroundingFloor.Add(next);
                }
            Assert.True(approaches.Count >= 2, $"{id}: zone needs at least two approach directions.");
            Assert.True(surroundingFloor.Count >= 4, $"{id}: zone needs room to attack and circle.");
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BotArena.sln")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("BotArena.sln not found above the test directory.");
    }
}
