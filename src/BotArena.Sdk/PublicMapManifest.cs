using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>Immutable public gameplay geometry for one match map.</summary>
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
    public required ImmutableArray<Position> ObjectiveTiles { get; init; }
    public required PublicFrontlineMapDefinition? Frontline { get; init; }
}

public readonly record struct PublicMapSpawn(
    int TeamId,
    Position Position,
    Direction Facing);

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
