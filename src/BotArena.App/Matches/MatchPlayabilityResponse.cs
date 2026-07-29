namespace BotArena.App.Matches;

public sealed record MatchPlayabilityResponse(
    Guid BotId,
    bool IsOwned,
    bool Playable,
    string? RefusalCode,
    string? RefusalDetail);
