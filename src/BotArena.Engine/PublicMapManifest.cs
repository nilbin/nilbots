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
    /// Effective objective tiles in bot-observable order. Order and duplicates
    /// are preserved because legacy observations expose both.
    /// </summary>
    public required ImmutableArray<Position> ObjectiveTiles { get; init; }
}

public readonly record struct PublicMapSpawn(int TeamId, Position Position, Direction Facing);
