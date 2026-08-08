using BotArena.App.ArcRelay;

namespace BotArena.App.Sheets;

public sealed record TacticalSheetSummaryResponse(
    Guid Id,
    string Name,
    int Revision,
    string ContentHash,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    ArcRelayEntrantCardResponse Entrant);

public sealed record TacticalSheetResponse(
    Guid Id,
    string Name,
    int Revision,
    string ContentHash,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string PlaybookJson,
    string LayoutJson,
    ArcRelayEntrantCardResponse Entrant);

public sealed record SaveTacticalSheetRequest(
    string Name,
    int? ExpectedRevision,
    string PlaybookJson,
    string LayoutJson,
    bool? EnterLadder = null);

public sealed record TrialTacticalSheetRequest(
    string StockSheetId,
    long? Seed);

public sealed record TacticalSheetClassResponse(
    string Id,
    string Name,
    string SignatureName,
    string Fantasy,
    bool Starter,
    bool Unlocked);

public sealed record TacticalSheetPointResponse(int X, int Y);

public sealed record TacticalSheetMapRegionResponse(
    string Id,
    string Kind,
    IReadOnlyList<TacticalSheetPointResponse> Tiles);

public sealed record TacticalSheetMapSpawnResponse(
    string Id,
    TacticalSheetPointResponse Position,
    string Facing);

public sealed record TacticalSheetMapTagResponse(
    string Id,
    string Kind,
    IReadOnlyList<TacticalSheetPointResponse> Tiles);

public sealed record TacticalSheetMapResponse(
    string Id,
    int Version,
    int FormatVersion,
    int Width,
    int Height,
    IReadOnlyList<string> TileRows,
    IReadOnlyList<TacticalSheetMapRegionResponse> Regions,
    IReadOnlyList<TacticalSheetMapSpawnResponse> SpawnAnchors,
    IReadOnlyList<TacticalSheetMapTagResponse> TileTags);

public sealed record TacticalStockSheetResponse(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<string> Composition);

public sealed record TacticalSheetCatalogResponse(
    string PlaylistKey,
    Guid PlaylistVersionId,
    TacticalSheetMapResponse Map,
    int SlotCount,
    int MaximumCopiesPerClass,
    IReadOnlyList<TacticalSheetClassResponse> Classes,
    string TemplatePlaybookJson,
    string TemplateLayoutJson,
    IReadOnlyList<TacticalStockSheetResponse> StockOpponents);

public sealed record TacticalSheetDeletedResponse(Guid Id);
