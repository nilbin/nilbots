namespace BotArena.App.ArcRelay;

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
    IReadOnlyList<ArcRelayClassResponse> Classes);
