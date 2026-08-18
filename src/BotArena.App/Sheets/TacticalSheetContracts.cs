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
    string TacticsJson,
    string OrdersJson,
    string MapPlanJson,
    ArcRelayEntrantCardResponse Entrant);

public sealed record SaveTacticalSheetRequest(
    string Name,
    int? ExpectedRevision,
    string TacticsJson,
    bool? EnterLadder = null);

public sealed record TrialTacticalSheetRequest(
    string StockSheetId,
    long? Seed);

public sealed record PracticeAuthoringSnapshotResponse(
    Guid MatchId,
    Guid SheetId,
    string SheetName,
    int Revision,
    string ContentHash,
    string OrdersJson,
    string MapPlanJson,
    string? TacticsJson,
    DateTime CreatedAt,
    bool AttributionAvailable);

public sealed record TacticalSheetSignatureOpportunityPresetResponse(
    string Id,
    string Title,
    string Description,
    string TargetKind,
    string? NamedTarget,
    bool SuggestsActivationZone);

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
    bool DurableDeployment,
    string DefaultCommitmentBehavior,
    TacticalSheetSignatureOpportunityPresetResponse Preset);

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

public sealed record TacticalStarterSheetResponse(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<string> Composition,
    string TacticsJson,
    string Classification,
    bool Recommended);

public sealed record TacticalSheetCatalogResponse(
    string PlaylistKey,
    Guid PlaylistVersionId,
    TacticalSheetMapResponse Map,
    int SlotCount,
    int MaximumCopiesPerClass,
    int MinimumDistinctClasses,
    IReadOnlyList<TacticalSheetClassResponse> Classes,
    IReadOnlyList<TacticalStarterSheetResponse> StarterSheets,
    IReadOnlyList<TacticalStockSheetResponse> StockOpponents);

public sealed record TacticalSheetDeletedResponse(Guid Id);
