namespace BotArena.App.Matches;

public sealed record CreateLabsMatchRequest(
    Guid PlaylistVersionId,
    IReadOnlyList<Guid> EntrantBotIds,
    long? Seed);
