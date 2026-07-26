using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Immutable public gameplay geometry for one map. Presentation metadata is
/// intentionally absent because it cannot affect simulation or the map fingerprint.
/// </summary>
public sealed record PublicMapManifest
{
    public required int SchemaVersion { get; init; }
    public required string MapId { get; init; }
    public required int MapVersion { get; init; }
    public required string MapFingerprint { get; init; }
    public required int FormatVersion { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required ImmutableArray<string> TileRows { get; init; }
    public required ImmutableArray<PublicMapSpawn> Spawns { get; init; }
    /// <summary>
    /// Legacy zone-objective tiles in bot-observable order. Order and duplicates
    /// are preserved because format-v1 observations expose both. Empty for
    /// Frontline maps, whose ordered position groups live in <see cref="Frontline"/>.
    /// </summary>
    public required ImmutableArray<Position> ObjectiveTiles { get; init; }
    /// <summary>
    /// Ordered format-v2 Frontline gameplay geometry. Null for format-v1 maps.
    /// Position array order is semantic; every tile set is canonical Y/X order.
    /// </summary>
    public required PublicFrontlineMapDefinition? Frontline { get; init; }
}

public readonly record struct PublicMapSpawn(int TeamId, Position Position, Direction Facing);

public sealed record PublicFrontlineMapDefinition(
    ImmutableArray<PublicFrontlinePosition> Positions,
    ImmutableArray<PublicFrontlineTeamHome> TeamHomes,
    ImmutableArray<Position> AnchorForbiddenTiles);

public sealed record PublicFrontlinePosition(
    int PositionIndex,
    ImmutableArray<Position> Tiles);

public sealed record PublicFrontlineTeamHome(
    int TeamId,
    Position PrimeSpawnPosition,
    Direction PrimeSpawnFacing,
    ImmutableArray<Position> ProtectedSpawnPad);
