using System.Collections.Immutable;

#if BOTARENA_ACTOR_CONTRACTS
namespace BotArena.ActorContracts;
#else
namespace BotArena.Sdk;
#endif

/// <summary>
/// Complete gameplay-only map generation 3 delivered to a generic actor.
/// Presentation metadata is intentionally outside this contract.
/// </summary>
#if BOTARENA_ACTOR_CONTRACTS
internal sealed class GenericActorMapContract
#else
public sealed class GenericActorMapContract
#endif
{
    internal GenericActorMapContract(
        int schemaVersion,
        string mapId,
        int mapVersion,
        string mapFingerprint,
        int formatVersion,
        int width,
        int height,
        ImmutableArray<string> tileRows,
        ImmutableArray<SpawnAnchor> spawnAnchors,
        ImmutableArray<Region> regions,
        ImmutableArray<TileTag> tileTags)
    {
        SchemaVersion = schemaVersion;
        MapId = mapId;
        MapVersion = mapVersion;
        MapFingerprint = mapFingerprint;
        FormatVersion = formatVersion;
        Width = width;
        Height = height;
        TileRows = tileRows;
        SpawnAnchors = spawnAnchors;
        Regions = regions;
        TileTags = tileTags;
    }

    /// <summary>Typed map-contract schema version.</summary>
    public int SchemaVersion { get; }
    /// <summary>Stable authored map identifier.</summary>
    public string MapId { get; }
    /// <summary>Authored revision of <see cref="MapId"/>.</summary>
    public int MapVersion { get; }
    /// <summary>Fingerprint of the canonical gameplay map definition.</summary>
    public string MapFingerprint { get; }
    /// <summary>Encoding version for <see cref="TileRows"/>.</summary>
    public int FormatVersion { get; }
    /// <summary>Map width in tiles.</summary>
    public int Width { get; }
    /// <summary>Map height in tiles.</summary>
    public int Height { get; }
    /// <summary>
    /// Gameplay tile rows indexed by Y, each containing exactly
    /// <see cref="Width"/> tile symbols indexed by X.
    /// </summary>
    public ImmutableArray<string> TileRows { get; }
    /// <summary>Named spawn anchors available to the resolved deployment.</summary>
    public ImmutableArray<SpawnAnchor> SpawnAnchors { get; }
    /// <summary>Named gameplay regions referenced by mode bindings.</summary>
    public ImmutableArray<Region> Regions { get; }
    /// <summary>Named semantic tile overlays referenced by mechanics.</summary>
    public ImmutableArray<TileTag> TileTags { get; }

    /// <summary>A named spawn position, facing, and compatible movement layers.</summary>
    /// <param name="SpawnId">Stable anchor identifier.</param>
    /// <param name="Position">Tile coordinate of the anchor.</param>
    /// <param name="Facing">Initial absolute map direction.</param>
    /// <param name="CompatibleMovementLayers">
    /// Movement layers permitted to deploy at the anchor.
    /// </param>
    public sealed record SpawnAnchor(
        string SpawnId,
        Position Position,
        Direction Facing,
        ImmutableArray<MovementLayer> CompatibleMovementLayers);

    /// <summary>A named set of tiles with a mode-facing semantic role.</summary>
    /// <param name="RegionId">Stable region identifier.</param>
    /// <param name="Kind">Region semantic kind.</param>
    /// <param name="Tiles">Canonical set of map tile coordinates.</param>
    public sealed record Region(
        string RegionId,
        RegionKind Kind,
        ImmutableArray<Position> Tiles);

    /// <summary>A named semantic overlay applied to a set of map tiles.</summary>
    /// <param name="TagId">Stable tile-tag identifier.</param>
    /// <param name="Kind">Tag semantic kind.</param>
    /// <param name="Tiles">Canonical set of map tile coordinates.</param>
    public sealed record TileTag(
        string TagId,
        TileTagKind Kind,
        ImmutableArray<Position> Tiles);

    /// <summary>Collision and traversal layer used by a movement profile.</summary>
    public enum MovementLayer
    {
        /// <summary>Ground traversal subject to ground obstacles.</summary>
        Ground = 0,
        /// <summary>Air traversal using the map's air-layer rules.</summary>
        Air = 1,
    }

    /// <summary>Semantic role of a named map region.</summary>
    public enum RegionKind
    {
        /// <summary>Region consumed by a game-mode objective binding.</summary>
        Objective = 0,
        /// <summary>Region used to validate transition placement.</summary>
        TransitionPlacement = 1,
    }

    /// <summary>Mechanics-facing semantic attached to tagged tiles.</summary>
    public enum TileTagKind
    {
        /// <summary>Same-life or lifecycle transition placement is forbidden.</summary>
        TransitionPlacementForbidden = 0,
        /// <summary>Tile receives the ruleset's spawn-protection semantics.</summary>
        SpawnProtected = 1,
        /// <summary>Placed signatures cannot occupy this required tile.</summary>
        SignaturePlacementForbidden = 2,
    }
}
