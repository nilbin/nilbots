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
        Assert.True(map.IsWall(3, 2));
        Assert.False(map.IsWall(1, 1));
        Assert.Equal(2, map.Spawns.Count);
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
}
