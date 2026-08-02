namespace BotArena.App.ArcRelay;

/// <summary>A saved player-authored commander sheet.</summary>
public sealed class ArcRelaySheet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }
    public required string Name { get; set; }
    public int Revision { get; set; } = 1;
    public required string CanonicalJson { get; set; }
    public required string ContentHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed record ArcRelaySheetDocument(
    int SchemaVersion,
    string MapId,
    IReadOnlyList<ArcRelaySheetSlot> Slots,
    IReadOnlyList<ArcRelaySheetZone> Zones,
    IReadOnlyList<ArcRelaySheetRallyLine> RallyLines,
    ArcRelaySheetPolicies Policies,
    IReadOnlyList<ArcRelaySheetGambit> Gambits);

public sealed record ArcRelaySheetSlot(
    int UnitId,
    string ClassId,
    string Theater,
    string Role,
    int PartnerUnitId,
    IReadOnlyList<ArcRelaySheetPoint> OutboundPath,
    IReadOnlyList<ArcRelaySheetPoint> ReturnPath);

public sealed record ArcRelaySheetPoint(int X, int Y);

public sealed record ArcRelaySheetZone(
    string Id,
    int MinX,
    int MinY,
    int MaxX,
    int MaxY);

public sealed record ArcRelaySheetRallyLine(
    string Id,
    IReadOnlyList<ArcRelaySheetPoint> Points);

public sealed record ArcRelaySheetPolicies(
    ArcRelayCarrierPolicy Carrier,
    ArcRelayEscortPolicy Escort,
    ArcRelayInterceptionPolicy Interception);

public sealed record ArcRelayCarrierPolicy(
    int HandoffHealthAtOrBelow,
    bool PreferAssignedTheater,
    int RouteFailureTicks);

public sealed record ArcRelayEscortPolicy(
    int FollowDistance,
    bool FocusEnemyCarrier);

public sealed record ArcRelayInterceptionPolicy(
    bool FocusEnemyCarrier,
    bool LooseCoreFallback);

public sealed record ArcRelaySheetGambit(
    string Id,
    string Trigger,
    int DurationTicks,
    int CooldownTicks,
    IReadOnlyList<string> ScopeRoles,
    string RoleOverride,
    string RallyLineId);

public sealed record ArcRelaySheetResponse(
    Guid Id,
    string Name,
    int Revision,
    string ContentHash,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    ArcRelaySheetDocument Document);

public sealed record SaveArcRelaySheetRequest(
    string Name,
    int? ExpectedRevision,
    ArcRelaySheetDocument Document);

public sealed record ArcRelayClassResponse(
    string Id,
    string Name,
    string SignatureName,
    string Fantasy,
    bool Starter,
    bool Unlocked);

public sealed record ArcRelayCatalogResponse(
    string PlaylistKey,
    Guid PlaylistVersionId,
    string MapId,
    IReadOnlyList<string> MapRows,
    int SlotCount,
    int MaximumCopiesPerClass,
    IReadOnlyList<string> Theaters,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> GambitTriggers,
    IReadOnlyList<ArcRelayClassResponse> Classes,
    ArcRelaySheetDocument NewSheetTemplate);

public sealed record CreateArcRelayMatchRequest(
    Guid SheetId,
    Guid OpponentSheetId,
    long? Seed);
