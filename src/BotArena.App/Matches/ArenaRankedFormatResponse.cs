namespace BotArena.App.Matches;

public sealed record ArenaRankedFormatResponse(
    int GamesPerSet,
    int MapSeedPairs,
    bool MirroredSlots,
    IReadOnlyList<string> MapPool,
    int MatchmakingPoolSize);
