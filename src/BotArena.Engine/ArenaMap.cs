using System.Collections.Immutable;
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
    public int FormatVersion { get; }
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
    /// <summary>Format-v2 Frontline geometry; null for every format-v1 map.</summary>
    public FrontlineMapProfile? Frontline { get; }

    private readonly bool[] _walls;

    private ArenaMap(
        string id,
        int version,
        int formatVersion,
        string[] tileRows,
        Spawn[] spawns,
        Position[]? zone = null,
        string? themeId = null,
        MapPresentation? presentation = null,
        FrontlineMapProfile? frontline = null)
    {
        Id = id;
        Version = version;
        FormatVersion = formatVersion;
        ThemeId = themeId;
        Presentation = presentation;
        TileRows = tileRows;
        Spawns = spawns;
        Zone = zone ?? [];
        Frontline = frontline;
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
        var map = new ArenaMap(
            id,
            version,
            1,
            tileRows,
            spawns,
            zone,
            themeId,
            presentation);
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
        if (dto.FormatVersion is not (1 or 2))
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
        var parsedSpawns = new List<ParsedSpawn>();
        foreach (var s in dto.Spawns!)
        {
            if (!Enum.TryParse<Direction>(s.Facing, ignoreCase: false, out var facing))
            {
                errors.Add($"Invalid spawn facing '{s.Facing}'.");
                continue;
            }
            parsedSpawns.Add(new ParsedSpawn(
                s.TeamId,
                new Spawn(s.X, s.Y, facing)));
        }

        if (dto.FormatVersion == 2)
            ValidateAndOrderFrontlineSpawns(parsedSpawns, errors);

        if (errors.Count > 0)
            throw new MapValidationException(errors);

        var zone = ReadPositions(dto.Zone, "zone", errors);
        if (dto.FormatVersion == 2 && zone.Length > 0)
            errors.Add("Format-v2 Frontline maps cannot declare a legacy zone.");

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

        FrontlineMapProfile? frontline = null;
        if (dto.FormatVersion == 2)
        {
            if (dto.Frontline is null)
            {
                errors.Add("Format-v2 maps require a frontline profile.");
            }
            else
            {
                frontline = ReadFrontlineProfile(
                    dto.Frontline,
                    parsedSpawns,
                    errors);
            }
        }

        if (errors.Count > 0)
            throw new MapValidationException(errors);

        Spawn[] spawns = parsedSpawns.Select(parsed => parsed.Spawn).ToArray();
        var map = new ArenaMap(
            dto.Id!,
            dto.Version,
            dto.FormatVersion,
            dto.Tiles,
            spawns,
            zone,
            dto.Theme,
            presentation,
            frontline);
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
        if (FormatVersion == 1)
        {
            if (Frontline is not null)
                errors.Add("Format-v1 maps cannot contain a Frontline profile.");
            if (errors.Count == 0 && Zone.Count > 0 && Spawns.Count == 2
                && !AreConnected(new Position(Spawns[0].X, Spawns[0].Y), Zone[0]))
            {
                errors.Add("Zone is not reachable from the spawns.");
            }
        }
        else if (FormatVersion == 2)
        {
            if (Version < 1)
                errors.Add("Format-v2 map version must be positive.");
            if (Zone.Count > 0)
                errors.Add("Format-v2 Frontline maps cannot declare a legacy zone.");
            ValidateFrontlineProfile(errors);
        }
        else
        {
            errors.Add($"Unsupported map formatVersion {FormatVersion}.");
        }

        if (errors.Count > 0)
            throw new MapValidationException(errors);
    }

    private void ValidateFrontlineProfile(List<string> errors)
    {
        if (Frontline is null)
        {
            errors.Add("Format-v2 maps require a Frontline profile.");
            return;
        }

        if (Frontline.Positions.Length < 3
            || Frontline.Positions.Length % 2 == 0)
        {
            errors.Add(
                "Frontline maps require an odd ordered position count of at least 3.");
        }

        var objectiveTiles = new HashSet<Position>();
        for (int index = 0; index < Frontline.Positions.Length; index++)
        {
            FrontlineRegion region = Frontline.Positions[index];
            if (region.PositionIndex != index)
            {
                errors.Add(
                    $"Frontline position at sequence index {index} declares index " +
                    $"{region.PositionIndex}.");
            }
            if (region.Tiles.IsDefaultOrEmpty)
            {
                errors.Add($"Frontline position {index} must contain at least one tile.");
                continue;
            }

            ValidateCanonicalTileSet(
                region.Tiles,
                $"Frontline position {index}",
                errors);
            if (!IsCardinallyConnected(region.Tiles))
                errors.Add($"Frontline position {index} must be cardinally connected.");
            foreach (Position tile in region.Tiles)
            {
                if (!objectiveTiles.Add(tile))
                {
                    errors.Add(
                        $"Frontline objective tile ({tile.X},{tile.Y}) appears in more " +
                        "than one position.");
                }
            }
        }

        int[] teamIds = Frontline.TeamHomes
            .Select(home => home.TeamId)
            .Order()
            .ToArray();
        if (!teamIds.SequenceEqual([0, 1]))
        {
            errors.Add(
                "Frontline maps require exactly one team home for team 0 and team 1.");
        }

        var protectedTiles = new HashSet<Position>();
        foreach (FrontlineTeamHome home in Frontline.TeamHomes.OrderBy(home => home.TeamId))
        {
            if (home.TeamId is not (0 or 1))
                continue;

            if (home.ProtectedSpawnPad.IsDefaultOrEmpty)
            {
                errors.Add(
                    $"Team {home.TeamId} protected spawn pad must contain at least one tile.");
                continue;
            }

            ValidateCanonicalTileSet(
                home.ProtectedSpawnPad,
                $"Team {home.TeamId} protected spawn pad",
                errors);
            if (!IsCardinallyConnected(home.ProtectedSpawnPad))
            {
                errors.Add(
                    $"Team {home.TeamId} protected spawn pad must be cardinally connected.");
            }

            Position primePosition = new(home.PrimeSpawn.X, home.PrimeSpawn.Y);
            if (!home.ProtectedSpawnPad.Contains(primePosition))
            {
                errors.Add(
                    $"Team {home.TeamId} Prime spawn {primePosition} must be inside its " +
                    "protected spawn pad.");
            }

            if (home.TeamId < Spawns.Count && home.PrimeSpawn != Spawns[home.TeamId])
            {
                errors.Add(
                    $"Team {home.TeamId} home Prime spawn does not match the map spawn.");
            }

            foreach (Position tile in home.ProtectedSpawnPad)
            {
                if (!protectedTiles.Add(tile))
                {
                    errors.Add(
                        $"Protected spawn tile ({tile.X},{tile.Y}) belongs to more than " +
                        "one team.");
                }
                if (objectiveTiles.Contains(tile))
                {
                    errors.Add(
                        $"Protected spawn tile ({tile.X},{tile.Y}) overlaps a Frontline " +
                        "position.");
                }
            }
        }

        ValidateCanonicalTileSet(
            Frontline.AnchorForbiddenTiles,
            "Anchor-forbidden tiles",
            errors);
        var anchorForbidden = Frontline.AnchorForbiddenTiles.ToHashSet();
        foreach (Position requiredTile in objectiveTiles
                     .Concat(protectedTiles)
                     .OrderBy(tile => tile.Y)
                     .ThenBy(tile => tile.X))
        {
            if (!anchorForbidden.Contains(requiredTile))
            {
                errors.Add(
                    $"Anchor-forbidden tiles must include gameplay tile " +
                    $"({requiredTile.X},{requiredTile.Y}).");
            }
        }

        if (Spawns.Count == 2 && Spawns.All(spawn => !IsWall(spawn.X, spawn.Y)))
        {
            Position origin = new(Spawns[0].X, Spawns[0].Y);
            IEnumerable<Position> gameplayTiles = objectiveTiles
                .Concat(protectedTiles)
                .Where(tile => !IsWall(tile))
                .OrderBy(tile => tile.Y)
                .ThenBy(tile => tile.X);
            foreach (Position tile in gameplayTiles)
            {
                if (AreConnected(origin, tile))
                    continue;
                errors.Add(
                    $"Frontline gameplay tile ({tile.X},{tile.Y}) is not reachable " +
                    "from both team homes.");
                break;
            }
        }
    }

    private void ValidateCanonicalTileSet(
        ImmutableArray<Position> tiles,
        string owner,
        List<string> errors)
    {
        Position? previous = null;
        foreach (Position tile in tiles)
        {
            if (IsWall(tile))
                errors.Add($"{owner} tile ({tile.X},{tile.Y}) is on a wall or outside the map.");
            if (previous is Position prior
                && (tile.Y < prior.Y || tile.Y == prior.Y && tile.X <= prior.X))
            {
                errors.Add($"{owner} tiles must be unique and ordered by Y then X.");
            }
            previous = tile;
        }
    }

    private static bool IsCardinallyConnected(ImmutableArray<Position> tiles)
    {
        if (tiles.IsDefaultOrEmpty)
            return false;

        var remaining = tiles.ToHashSet();
        var queue = new Queue<Position>();
        queue.Enqueue(tiles[0]);
        remaining.Remove(tiles[0]);
        while (queue.TryDequeue(out Position tile))
        {
            foreach (var (dx, dy) in CardinalOffsets)
            {
                Position adjacent = tile.Offset(dx, dy);
                if (remaining.Remove(adjacent))
                    queue.Enqueue(adjacent);
            }
        }
        return remaining.Count == 0;
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

    private static void ValidateAndOrderFrontlineSpawns(
        List<ParsedSpawn> spawns,
        List<string> errors)
    {
        if (spawns.Count != 2)
            errors.Add($"Format-v2 maps require exactly 2 Prime spawns, found {spawns.Count}.");

        var teamIds = new HashSet<int>();
        foreach (ParsedSpawn spawn in spawns)
        {
            if (spawn.TeamId is not (0 or 1))
            {
                errors.Add(
                    "Every format-v2 Prime spawn requires teamId 0 or teamId 1.");
                continue;
            }
            if (!teamIds.Add(spawn.TeamId.Value))
                errors.Add($"Team {spawn.TeamId.Value} declares more than one Prime spawn.");
        }
        if (!teamIds.SetEquals([0, 1]))
            errors.Add("Format-v2 maps require one Prime spawn for team 0 and team 1.");

        spawns.Sort((left, right) =>
            Nullable.Compare(left.TeamId, right.TeamId));
    }

    private static FrontlineMapProfile ReadFrontlineProfile(
        FrontlineDto dto,
        IReadOnlyList<ParsedSpawn> parsedSpawns,
        List<string> errors)
    {
        var positions = ImmutableArray.CreateBuilder<FrontlineRegion>();
        FrontlinePositionDto?[] authoredPositions = dto.Positions ?? [];
        for (int index = 0; index < authoredPositions.Length; index++)
        {
            FrontlinePositionDto? authoredPosition = authoredPositions[index];
            if (authoredPosition is null)
            {
                errors.Add($"Frontline position {index} cannot be null.");
                positions.Add(new FrontlineRegion(index, []));
                continue;
            }
            positions.Add(new FrontlineRegion(
                index,
                ReadCanonicalPositionSet(
                    authoredPosition.Tiles,
                    $"Frontline position {index}",
                    errors)));
        }

        Dictionary<int, Spawn> spawnsByTeam = parsedSpawns
            .Where(spawn => spawn.TeamId is 0 or 1)
            .ToDictionary(spawn => spawn.TeamId!.Value, spawn => spawn.Spawn);
        var homes = ImmutableArray.CreateBuilder<FrontlineTeamHome>();
        var seenHomeTeams = new HashSet<int>();
        foreach (FrontlineHomePadDto? home in dto.HomePads ?? [])
        {
            if (home is null)
            {
                errors.Add("Frontline home pads cannot contain null entries.");
                continue;
            }
            if (home.TeamId is not (0 or 1))
            {
                errors.Add(
                    "Every Frontline home pad requires teamId 0 or teamId 1.");
                continue;
            }
            int teamId = home.TeamId.Value;
            if (!seenHomeTeams.Add(teamId))
            {
                errors.Add($"Team {teamId} declares more than one protected spawn pad.");
                continue;
            }
            if (!spawnsByTeam.TryGetValue(teamId, out Spawn primeSpawn))
            {
                errors.Add($"Team {teamId} home has no matching Prime spawn.");
                continue;
            }
            homes.Add(new FrontlineTeamHome(
                teamId,
                primeSpawn,
                ReadCanonicalPositionSet(
                    home.Tiles,
                    $"Team {teamId} protected spawn pad",
                    errors)));
        }

        return new FrontlineMapProfile(
            positions.ToImmutable(),
            homes
                .OrderBy(home => home.TeamId)
                .ToImmutableArray(),
            ReadCanonicalPositionSet(
                dto.AnchorForbiddenTiles,
                "Anchor-forbidden tiles",
                errors));
    }

    private static ImmutableArray<Position> ReadCanonicalPositionSet(
        int[][]? values,
        string owner,
        List<string> errors)
    {
        Position[] positions = ReadPositions(values, owner, errors);
        var unique = new HashSet<Position>();
        foreach (Position tile in positions)
        {
            if (!unique.Add(tile))
                errors.Add($"{owner} contains duplicate tile ({tile.X},{tile.Y}).");
        }
        return unique
            .OrderBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .ToImmutableArray();
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
        [JsonPropertyName("frontline")] public FrontlineDto? Frontline { get; set; }
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
        [JsonPropertyName("teamId")] public int? TeamId { get; set; }
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("facing")] public string? Facing { get; set; }
    }

    private sealed class FrontlineDto
    {
        [JsonPropertyName("positions")] public FrontlinePositionDto?[]? Positions { get; set; }
        [JsonPropertyName("homePads")] public FrontlineHomePadDto?[]? HomePads { get; set; }
        [JsonPropertyName("anchorForbiddenTiles")] public int[][]? AnchorForbiddenTiles { get; set; }
    }

    private sealed class FrontlinePositionDto
    {
        [JsonPropertyName("tiles")] public int[][]? Tiles { get; set; }
    }

    private sealed class FrontlineHomePadDto
    {
        [JsonPropertyName("teamId")] public int? TeamId { get; set; }
        [JsonPropertyName("tiles")] public int[][]? Tiles { get; set; }
    }

    private readonly record struct ParsedSpawn(int? TeamId, Spawn Spawn);

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
