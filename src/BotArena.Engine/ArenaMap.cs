using System.Text.Json;
using System.Text.Json.Serialization;

namespace BotArena.Engine;

public readonly record struct Spawn(int X, int Y, Direction Facing);

/// <summary>A map-authored wall-family override. Presentation never affects collision.</summary>
public sealed record MapWallGroup(string Family, IReadOnlyList<Position> Tiles);

/// <summary>
/// Immutable visual roles owned by the map. The theme supplies art for the named
/// families; the map decides where those families are used.
/// </summary>
public sealed record MapPresentation(
    string BoundaryWall,
    string InteriorWall,
    IReadOnlyList<MapWallGroup> WallGroups);

public sealed class MapValidationException(IReadOnlyList<string> errors)
    : Exception("Invalid map: " + string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

/// <summary>Versioned tile-grid map (plan §25). '#' is a wall, '.' is floor.</summary>
public sealed class ArenaMap
{
    public const int MaxWidth = 32;
    public const int MaxHeight = 32;

    public string Id { get; }
    public int Version { get; }
    public int Width { get; }
    public int Height { get; }
    /// <summary>Presentation theme selected by the map package. It never affects
    /// collision or simulation and is copied into the replay for immutable playback.</summary>
    public string? ThemeId { get; }
    /// <summary>Map-authored wall-family placement, copied into replays.</summary>
    public MapPresentation? Presentation { get; }
    public IReadOnlyList<string> TileRows { get; }
    public IReadOnlyList<Spawn> Spawns { get; }
    /// <summary>Declared zone-control tiles (RULES-0.3-DESIGN §C); empty when the map
    /// declares none — <see cref="EffectiveZone"/> falls back to the open center.</summary>
    public IReadOnlyList<Position> Zone { get; }

    private readonly bool[] _walls;

    private ArenaMap(
        string id,
        int version,
        string[] tileRows,
        Spawn[] spawns,
        Position[]? zone = null,
        string? themeId = null,
        MapPresentation? presentation = null)
    {
        Id = id;
        Version = version;
        ThemeId = themeId;
        Presentation = presentation;
        TileRows = tileRows;
        Spawns = spawns;
        Zone = zone ?? [];
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

    public static ArenaMap Create(
        string id,
        string[] tileRows,
        Spawn[] spawns,
        int version = 1,
        Position[]? zone = null,
        string? themeId = null,
        MapPresentation? presentation = null)
    {
        var map = new ArenaMap(id, version, tileRows, spawns, zone, themeId, presentation);
        map.Validate();
        return map;
    }

    /// <summary>Zone tiles for zone-control rules: the declared zone, else the floor
    /// tiles of the center 3x3. Deterministic per map — never seed-dependent.</summary>
    public IReadOnlyList<Position> EffectiveZone()
    {
        if (Zone.Count > 0)
            return Zone;
        int cx = Width / 2, cy = Height / 2;
        var fallback = new List<Position>();
        for (int y = cy - 1; y <= cy + 1; y++)
            for (int x = cx - 1; x <= cx + 1; x++)
                if (!IsWall(x, y))
                    fallback.Add(new Position(x, y));
        return fallback;
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
        if (dto.Theme is not null && !IsPresentationId(dto.Theme))
            errors.Add($"Invalid map theme '{dto.Theme}'.");
        if (errors.Count > 0)
            throw new MapValidationException(errors);

        if (dto.Width != dto.Tiles![0].Length || dto.Height != dto.Tiles.Length)
            errors.Add($"Declared size {dto.Width}x{dto.Height} does not match tile data " +
                       $"{dto.Tiles[0].Length}x{dto.Tiles.Length}.");
        if (dto.Width > MaxWidth || dto.Height > MaxHeight)
            errors.Add($"Map must be at most {MaxWidth}x{MaxHeight}.");
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

        var zone = ReadPositions(dto.Zone, "zone", errors);
        MapPresentation? presentation = null;
        if (dto.Presentation is not null)
        {
            var boundary = dto.Presentation.BoundaryWall;
            var interior = dto.Presentation.InteriorWall;
            if (string.IsNullOrWhiteSpace(boundary) || !IsPresentationId(boundary))
                errors.Add($"Invalid boundary wall family '{boundary}'.");
            if (string.IsNullOrWhiteSpace(interior) || !IsPresentationId(interior))
                errors.Add($"Invalid interior wall family '{interior}'.");
            var groups = new List<MapWallGroup>();
            foreach (var group in dto.Presentation.WallGroups ?? [])
            {
                if (string.IsNullOrWhiteSpace(group.Family) || !IsPresentationId(group.Family))
                {
                    errors.Add($"Invalid wall group family '{group.Family}'.");
                    continue;
                }
                groups.Add(new MapWallGroup(
                    group.Family,
                    ReadPositions(group.Tiles, $"wall group '{group.Family}'", errors)));
            }
            if (boundary is not null && interior is not null
                && IsPresentationId(boundary) && IsPresentationId(interior))
                presentation = new MapPresentation(boundary, interior, groups);
        }
        if (errors.Count > 0)
            throw new MapValidationException(errors);
        var map = new ArenaMap(
            dto.Id!, dto.Version, dto.Tiles, spawns.ToArray(), zone, dto.Theme, presentation);
        map.Validate();
        return map;
    }

    private void Validate()
    {
        var errors = new List<string>();
        if (Width < 3 || Height < 3)
            errors.Add("Map must be at least 3x3.");
        if (Width > MaxWidth || Height > MaxHeight)
            errors.Add($"Map must be at most {MaxWidth}x{MaxHeight}.");
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
        foreach (var tile in Zone)
            if (IsWall(tile))
                errors.Add($"Zone tile ({tile.X},{tile.Y}) is on a wall or outside the map.");
        if (Presentation is not null)
        {
            if (!IsPresentationId(Presentation.BoundaryWall))
                errors.Add($"Invalid boundary wall family '{Presentation.BoundaryWall}'.");
            if (!IsPresentationId(Presentation.InteriorWall))
                errors.Add($"Invalid interior wall family '{Presentation.InteriorWall}'.");
            var styledTiles = new HashSet<Position>();
            foreach (var group in Presentation.WallGroups)
            {
                if (!IsPresentationId(group.Family))
                    errors.Add($"Invalid wall group family '{group.Family}'.");
                foreach (var tile in group.Tiles)
                {
                    if (!IsWall(tile))
                        errors.Add($"Wall-family tile ({tile.X},{tile.Y}) is not a wall.");
                    if (!styledTiles.Add(tile))
                        errors.Add($"Wall-family tile ({tile.X},{tile.Y}) is assigned more than once.");
                }
            }
        }
        if (errors.Count == 0 && Zone.Count > 0 && Spawns.Count == 2
            && !AreConnected(new Position(Spawns[0].X, Spawns[0].Y), Zone[0]))
            errors.Add("Zone is not reachable from the spawns.");
        if (errors.Count > 0)
            throw new MapValidationException(errors);
    }

    private static readonly (int Dx, int Dy)[] CardinalOffsets = [(0, -1), (1, 0), (0, 1), (-1, 0)];

    /// <summary>Floor connectivity via 4-neighbor BFS (also used by seed-spawn variation).</summary>
    public bool AreConnected(Position a, Position b)
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
        [JsonPropertyName("theme")] public string? Theme { get; set; }
        [JsonPropertyName("presentation")] public MapPresentationDto? Presentation { get; set; }
        [JsonPropertyName("tiles")] public string[]? Tiles { get; set; }
        [JsonPropertyName("spawns")] public SpawnDto[]? Spawns { get; set; }
        [JsonPropertyName("zone")] public int[][]? Zone { get; set; }
    }

    private sealed class MapPresentationDto
    {
        [JsonPropertyName("boundaryWall")] public string? BoundaryWall { get; set; }
        [JsonPropertyName("interiorWall")] public string? InteriorWall { get; set; }
        [JsonPropertyName("wallGroups")] public MapWallGroupDto[]? WallGroups { get; set; }
    }

    private sealed class MapWallGroupDto
    {
        [JsonPropertyName("family")] public string? Family { get; set; }
        [JsonPropertyName("tiles")] public int[][]? Tiles { get; set; }
    }

    private sealed class SpawnDto
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("facing")] public string? Facing { get; set; }
    }

    private static bool IsPresentationId(string value) =>
        value.Length is > 0 and <= 64 &&
        value[0] is >= 'a' and <= 'z' &&
        value[^1] != '-' &&
        value.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');

    private static Position[] ReadPositions(
        int[][]? values,
        string owner,
        List<string> errors)
    {
        if (values is null)
            return [];
        var positions = new List<Position>();
        foreach (var pair in values)
        {
            if (pair.Length != 2)
            {
                errors.Add($"Invalid {owner} coordinate; expected [x,y].");
                continue;
            }
            positions.Add(new Position(pair[0], pair[1]));
        }
        return positions.ToArray();
    }
}
