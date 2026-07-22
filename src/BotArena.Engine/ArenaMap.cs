using System.Text.Json;
using System.Text.Json.Serialization;

namespace BotArena.Engine;

public readonly record struct Spawn(int X, int Y, Direction Facing);

public sealed class MapValidationException(IReadOnlyList<string> errors)
    : Exception("Invalid map: " + string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

/// <summary>Versioned tile-grid map (plan §25). '#' is a wall, '.' is floor.</summary>
public sealed class ArenaMap
{
    public string Id { get; }
    public int Version { get; }
    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<string> TileRows { get; }
    public IReadOnlyList<Spawn> Spawns { get; }

    private readonly bool[] _walls;

    private ArenaMap(string id, int version, string[] tileRows, Spawn[] spawns)
    {
        Id = id;
        Version = version;
        TileRows = tileRows;
        Spawns = spawns;
        Height = tileRows.Length;
        Width = tileRows.Length > 0 ? tileRows[0].Length : 0;
        _walls = new bool[Width * Height];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                _walls[y * Width + x] = tileRows[y][x] == '#';
    }

    /// <summary>Out-of-bounds counts as wall so bots can never leave the grid.</summary>
    public bool IsWall(int x, int y) =>
        x < 0 || y < 0 || x >= Width || y >= Height || _walls[y * Width + x];

    public bool IsWall(Position position) => IsWall(position.X, position.Y);

    public static ArenaMap Create(string id, string[] tileRows, Spawn[] spawns, int version = 1)
    {
        var map = new ArenaMap(id, version, tileRows, spawns);
        map.Validate();
        return map;
    }

    public static ArenaMap FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<MapDto>(json, JsonOptions)
                  ?? throw new MapValidationException(["Empty map document."]);
        var errors = new List<string>();
        if (dto.FormatVersion != 1)
            errors.Add($"Unsupported map formatVersion {dto.FormatVersion}.");
        if (string.IsNullOrWhiteSpace(dto.Id))
            errors.Add("Map id is required.");
        if (dto.Tiles is null || dto.Tiles.Length == 0)
            errors.Add("Map tiles are required.");
        if (dto.Spawns is null || dto.Spawns.Length == 0)
            errors.Add("Map spawns are required.");
        if (errors.Count > 0)
            throw new MapValidationException(errors);

        if (dto.Width != dto.Tiles![0].Length || dto.Height != dto.Tiles.Length)
            errors.Add($"Declared size {dto.Width}x{dto.Height} does not match tile data " +
                       $"{dto.Tiles[0].Length}x{dto.Tiles.Length}.");
        var spawns = new List<Spawn>();
        foreach (var s in dto.Spawns!)
        {
            if (!Enum.TryParse<Direction>(s.Facing, ignoreCase: false, out var facing))
            {
                errors.Add($"Invalid spawn facing '{s.Facing}'.");
                continue;
            }
            spawns.Add(new Spawn(s.X, s.Y, facing));
        }
        if (errors.Count > 0)
            throw new MapValidationException(errors);

        var map = new ArenaMap(dto.Id!, dto.Version, dto.Tiles, spawns.ToArray());
        map.Validate();
        return map;
    }

    private void Validate()
    {
        var errors = new List<string>();
        if (Width < 3 || Height < 3)
            errors.Add("Map must be at least 3x3.");
        for (int y = 0; y < Height; y++)
        {
            if (TileRows[y].Length != Width)
                errors.Add($"Row {y} has length {TileRows[y].Length}, expected {Width}.");
            else
                for (int x = 0; x < Width; x++)
                    if (TileRows[y][x] is not ('#' or '.'))
                        errors.Add($"Invalid tile symbol '{TileRows[y][x]}' at ({x},{y}).");
        }
        if (errors.Count > 0)
            throw new MapValidationException(errors);

        if (Spawns.Count != 2)
            errors.Add($"Exactly 2 spawns are required, found {Spawns.Count}.");
        foreach (var spawn in Spawns)
            if (IsWall(spawn.X, spawn.Y))
                errors.Add($"Spawn at ({spawn.X},{spawn.Y}) is on a wall or outside the map.");
        if (Spawns.Count == 2 && Spawns[0].X == Spawns[1].X && Spawns[0].Y == Spawns[1].Y)
            errors.Add("Spawns must be on distinct tiles.");
        if (errors.Count == 0 && Spawns.Count == 2 && !AreConnected(
                new Position(Spawns[0].X, Spawns[0].Y), new Position(Spawns[1].X, Spawns[1].Y)))
            errors.Add("Spawns are not connected by floor tiles.");
        if (errors.Count > 0)
            throw new MapValidationException(errors);
    }

    private static readonly (int Dx, int Dy)[] CardinalOffsets = [(0, -1), (1, 0), (0, 1), (-1, 0)];

    private bool AreConnected(Position a, Position b)
    {
        var seen = new bool[Width * Height];
        var queue = new Queue<Position>();
        queue.Enqueue(a);
        seen[a.Y * Width + a.X] = true;
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            if (p == b)
                return true;
            foreach (var (dx, dy) in CardinalOffsets)
            {
                var n = p.Offset(dx, dy);
                if (IsWall(n) || seen[n.Y * Width + n.X])
                    continue;
                seen[n.Y * Width + n.X] = true;
                queue.Enqueue(n);
            }
        }
        return false;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private sealed class MapDto
    {
        [JsonPropertyName("formatVersion")] public int FormatVersion { get; set; }
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("version")] public int Version { get; set; } = 1;
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
        [JsonPropertyName("tiles")] public string[]? Tiles { get; set; }
        [JsonPropertyName("spawns")] public SpawnDto[]? Spawns { get; set; }
    }

    private sealed class SpawnDto
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("facing")] public string? Facing { get; set; }
    }
}
