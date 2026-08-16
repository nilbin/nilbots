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
    string OrdersJson,
    string MapPlanJson,
    string? WorkshopJson,
    ArcRelayEntrantCardResponse Entrant);

public sealed record SaveTacticalSheetRequest(
    string Name,
    int? ExpectedRevision,
    string OrdersJson,
    string MapPlanJson,
    bool? EnterLadder = null,
    string? WorkshopJson = null,
    bool? ReplaceWorkshop = null);

public sealed record TrialTacticalSheetRequest(
    string StockSheetId,
    long? Seed);

public sealed record TacticalSheetSignatureAuthoringResponse(
    string Category,
    string ArgumentKind,
    int EngagementRange,
    int CooldownTicks,
    string ActivationTiming,
    string InitialPhase,
    string MaturityPhase,
    string CooldownAnchor,
    IReadOnlyDictionary<string, int> Parameters,
    IReadOnlyList<string> AllowedSelectors,
    bool AllowsFriendlyRole,
    bool AllowsMostExperiencedAlly,
    bool AllowsExactAnchor,
    bool AllowsRouteAheadRole,
    bool AllowsRouteForward,
    string DefaultCommitmentBehavior);

public sealed record TacticalSheetClassResponse(
    string Id,
    string Name,
    string SignatureId,
    string ActionId,
    string SignatureName,
    string Fantasy,
    bool Starter,
    bool Unlocked,
    TacticalSheetSignatureAuthoringResponse SignatureAuthoring);

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
    int MinimumDistinctClasses,
    IReadOnlyList<TacticalSheetClassResponse> Classes,
    string TemplateOrdersJson,
    string TemplateMapPlanJson,
    IReadOnlyList<TacticalStockSheetResponse> StockOpponents);

public sealed record TacticalSheetDeletedResponse(Guid Id);
