using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Additive map generation for generic actor matches. It contains gameplay
/// geometry only; presentation belongs to a separate, non-gameplay contract.
/// </summary>
public sealed class ActorMapDefinition
{
    public const int CurrentFormatVersion = 3;

    private const int MaxSemanticIdLength = 64;
    private readonly bool[] _walls;

    public ActorMapDefinition(
        string id,
        int version,
        ImmutableArray<string> tileRows,
        ImmutableArray<ActorMapSpawnAnchorDefinition> spawnAnchors,
        ImmutableArray<ActorMapRegionDefinition> regions,
        ImmutableArray<ActorMapTileTagDefinition> tileTags)
    {
        var errors = new List<string>();
        if (!IsCanonicalSemanticId(id))
            errors.Add($"Map id '{id}' is not a canonical semantic ID.");
        if (version <= 0)
            errors.Add("Map version must be positive.");

        int height = tileRows.IsDefault ? 0 : tileRows.Length;
        int width = height > 0 && tileRows[0] is not null
            ? tileRows[0].Length
            : 0;
        ValidateGeometry(tileRows, width, height, errors);
        ValidateSpawnAnchors(
            spawnAnchors,
            tileRows,
            width,
            height,
            errors);
        ValidateRegions(regions, tileRows, width, height, errors);
        ValidateTileTags(tileTags, tileRows, width, height, errors);

        if (errors.Count > 0)
            throw new MapValidationException(errors);

        Id = id;
        Version = version;
        Width = width;
        Height = height;
        TileRows = tileRows;
        SpawnAnchors = spawnAnchors
            .OrderBy(anchor => anchor.Spawn.SpawnId, StringComparer.Ordinal)
            .Select(Canonicalize)
            .ToImmutableArray();
        Regions = regions
            .OrderBy(region => region.RegionId, StringComparer.Ordinal)
            .Select(Canonicalize)
            .ToImmutableArray();
        TileTags = tileTags
            .OrderBy(tag => tag.TagId, StringComparer.Ordinal)
            .Select(Canonicalize)
            .ToImmutableArray();

        _walls = new bool[checked(Width * Height)];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
                _walls[y * Width + x] = TileRows[y][x] == '#';
        }
    }

    public string Id { get; }
    public int Version { get; }
    public int FormatVersion => CurrentFormatVersion;
    public int Width { get; }
    public int Height { get; }
    public ImmutableArray<string> TileRows { get; }
    public ImmutableArray<ActorMapSpawnAnchorDefinition> SpawnAnchors { get; }
    public ImmutableArray<ActorMapRegionDefinition> Regions { get; }
    public ImmutableArray<ActorMapTileTagDefinition> TileTags { get; }

    /// <summary>Out-of-bounds counts as wall, matching historical map safety.</summary>
    public bool IsWall(int x, int y) =>
        x < 0 || y < 0 || x >= Width || y >= Height
        || _walls[y * Width + x];

    public bool IsWall(Position position) => IsWall(position.X, position.Y);

    private static void ValidateGeometry(
        ImmutableArray<string> tileRows,
        int width,
        int height,
        List<string> errors)
    {
        if (tileRows.IsDefaultOrEmpty)
        {
            errors.Add("Map tiles must be initialized and non-empty.");
            return;
        }
        if (width < 3 || height < 3)
            errors.Add("Map must be at least 3x3.");
        if (width > ArenaMap.MaxWidth || height > ArenaMap.MaxHeight)
        {
            errors.Add(
                $"Map must be at most {ArenaMap.MaxWidth}x{ArenaMap.MaxHeight}.");
        }

        for (int y = 0; y < tileRows.Length; y++)
        {
            string? row = tileRows[y];
            if (row is null)
            {
                errors.Add($"Map row {y} cannot be null.");
                continue;
            }
            if (row.Length != width)
            {
                errors.Add(
                    $"Map row {y} has length {row.Length}, expected {width}.");
                continue;
            }

            for (int x = 0; x < row.Length; x++)
            {
                if (row[x] is not ('#' or '.'))
                {
                    errors.Add(
                        $"Invalid tile symbol '{row[x]}' at ({x},{y}).");
                }
            }
        }
    }

    private static void ValidateSpawnAnchors(
        ImmutableArray<ActorMapSpawnAnchorDefinition> spawnAnchors,
        ImmutableArray<string> tileRows,
        int width,
        int height,
        List<string> errors)
    {
        if (spawnAnchors.IsDefaultOrEmpty)
        {
            errors.Add("Map spawn anchors must be initialized and non-empty.");
            return;
        }

        var spawnIds = new HashSet<string>(StringComparer.Ordinal);
        var positions = new HashSet<Position>();
        foreach (ActorMapSpawnAnchorDefinition? anchor in spawnAnchors)
        {
            if (anchor?.Spawn is null)
            {
                errors.Add("Map spawn anchors cannot contain null entries.");
                continue;
            }

            string spawnId = anchor.Spawn.SpawnId;
            if (!IsCanonicalSemanticId(spawnId))
            {
                errors.Add(
                    $"Spawn id '{spawnId}' is not a canonical semantic ID.");
            }
            else if (!spawnIds.Add(spawnId))
            {
                errors.Add($"Spawn id '{spawnId}' is declared more than once.");
            }

            if (!positions.Add(anchor.Spawn.Position))
            {
                errors.Add(
                    $"Spawn position {anchor.Spawn.Position} is declared more than once.");
            }
            if (!IsFloor(
                    anchor.Spawn.Position,
                    tileRows,
                    width,
                    height))
            {
                errors.Add(
                    $"Spawn '{spawnId}' at {anchor.Spawn.Position} must be on an " +
                    "in-bounds floor tile.");
            }
            if (!Enum.IsDefined(anchor.Spawn.Facing))
            {
                errors.Add(
                    $"Spawn '{spawnId}' has an invalid facing value.");
            }

            ValidateMovementLayers(
                spawnId,
                anchor.CompatibleMovementLayers,
                errors);
        }
    }

    private static void ValidateMovementLayers(
        string spawnId,
        ImmutableArray<ActorMovementLayer> layers,
        List<string> errors)
    {
        if (layers.IsDefaultOrEmpty)
        {
            errors.Add(
                $"Spawn '{spawnId}' must declare at least one compatible movement layer.");
            return;
        }

        var seen = new HashSet<ActorMovementLayer>();
        foreach (ActorMovementLayer layer in layers)
        {
            if (!Enum.IsDefined(layer))
            {
                errors.Add(
                    $"Spawn '{spawnId}' declares an unknown movement layer value.");
            }
            else if (!seen.Add(layer))
            {
                errors.Add(
                    $"Spawn '{spawnId}' declares movement layer '{layer}' more than once.");
            }
        }
    }

    private static void ValidateRegions(
        ImmutableArray<ActorMapRegionDefinition> regions,
        ImmutableArray<string> tileRows,
        int width,
        int height,
        List<string> errors)
    {
        if (regions.IsDefault)
        {
            errors.Add("Map regions must be initialized.");
            return;
        }

        var regionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (ActorMapRegionDefinition? region in regions)
        {
            if (region is null)
            {
                errors.Add("Map regions cannot contain null entries.");
                continue;
            }
            if (!IsCanonicalSemanticId(region.RegionId))
            {
                errors.Add(
                    $"Region id '{region.RegionId}' is not a canonical semantic ID.");
            }
            else if (!regionIds.Add(region.RegionId))
            {
                errors.Add(
                    $"Region id '{region.RegionId}' is declared more than once.");
            }
            if (!Enum.IsDefined(region.Kind))
            {
                errors.Add(
                    $"Region '{region.RegionId}' has an unknown region kind.");
            }
            ValidateFloorTileSet(
                $"Region '{region.RegionId}'",
                region.Tiles,
                tileRows,
                width,
                height,
                errors);
        }
    }

    private static void ValidateTileTags(
        ImmutableArray<ActorMapTileTagDefinition> tileTags,
        ImmutableArray<string> tileRows,
        int width,
        int height,
        List<string> errors)
    {
        if (tileTags.IsDefault)
        {
            errors.Add("Map tile tags must be initialized.");
            return;
        }

        var tagIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (ActorMapTileTagDefinition? tag in tileTags)
        {
            if (tag is null)
            {
                errors.Add("Map tile tags cannot contain null entries.");
                continue;
            }
            if (!IsCanonicalSemanticId(tag.TagId))
            {
                errors.Add(
                    $"Tile tag id '{tag.TagId}' is not a canonical semantic ID.");
            }
            else if (!tagIds.Add(tag.TagId))
            {
                errors.Add(
                    $"Tile tag id '{tag.TagId}' is declared more than once.");
            }
            if (!Enum.IsDefined(tag.Kind))
            {
                errors.Add(
                    $"Tile tag '{tag.TagId}' has an unknown tag kind.");
            }
            ValidateFloorTileSet(
                $"Tile tag '{tag.TagId}'",
                tag.Tiles,
                tileRows,
                width,
                height,
                errors);
        }
    }

    private static void ValidateFloorTileSet(
        string owner,
        ImmutableArray<Position> tiles,
        ImmutableArray<string> tileRows,
        int width,
        int height,
        List<string> errors)
    {
        if (tiles.IsDefaultOrEmpty)
        {
            errors.Add($"{owner} must contain at least one tile.");
            return;
        }

        var seen = new HashSet<Position>();
        foreach (Position tile in tiles)
        {
            if (!seen.Add(tile))
                errors.Add($"{owner} contains duplicate tile {tile}.");
            if (!IsFloor(tile, tileRows, width, height))
            {
                errors.Add(
                    $"{owner} tile {tile} must be an in-bounds floor tile.");
            }
        }
    }

    private static bool IsFloor(
        Position position,
        ImmutableArray<string> tileRows,
        int width,
        int height) =>
        position.X >= 0
        && position.Y >= 0
        && position.X < width
        && position.Y < height
        && position.Y < tileRows.Length
        && tileRows[position.Y] is string row
        && row.Length == width
        && row[position.X] == '.';

    private static ActorMapSpawnAnchorDefinition Canonicalize(
        ActorMapSpawnAnchorDefinition anchor) =>
        anchor with
        {
            CompatibleMovementLayers = anchor.CompatibleMovementLayers
                .Order()
                .ToImmutableArray(),
        };

    private static ActorMapRegionDefinition Canonicalize(
        ActorMapRegionDefinition region) =>
        region with
        {
            Tiles = CanonicalizeTiles(region.Tiles),
        };

    private static ActorMapTileTagDefinition Canonicalize(
        ActorMapTileTagDefinition tag) =>
        tag with
        {
            Tiles = CanonicalizeTiles(tag.Tiles),
        };

    private static ImmutableArray<Position> CanonicalizeTiles(
        ImmutableArray<Position> tiles) =>
        tiles
            .OrderBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .ToImmutableArray();

    private static bool IsCanonicalSemanticId(string? value)
    {
        if (string.IsNullOrEmpty(value)
            || value.Length > MaxSemanticIdLength)
        {
            return false;
        }

        bool needsSegmentStart = true;
        foreach (char character in value)
        {
            if (character == '-')
            {
                if (needsSegmentStart)
                    return false;
                needsSegmentStart = true;
                continue;
            }
            if (character is not (>= 'a' and <= 'z')
                and not (>= '0' and <= '9'))
            {
                return false;
            }
            needsSegmentStart = false;
        }
        return !needsSegmentStart;
    }
}
