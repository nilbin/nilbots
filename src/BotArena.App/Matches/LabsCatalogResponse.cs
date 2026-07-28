namespace BotArena.App.Matches;

public sealed record LabsCatalogResponse(
    bool Enabled,
    IReadOnlyList<LabsPlaylistResponse> Playlists);
