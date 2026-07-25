namespace BotArena.App.Matches;

/// <summary>
/// One elo ladder. <see cref="ActiveRulesVersion"/> tells a reader which ladder still
/// accepts sets; every other entry in <see cref="Ladders"/> is a historical record
/// (DECISIONS #97).
/// </summary>
public sealed record LeaderboardResponse(
    string RulesVersion,
    string ActiveRulesVersion,
    IReadOnlyList<string> Ladders,
    IReadOnlyList<LeaderboardEntryResponse> Entries);

/// <summary>
/// A bot's standing on one ladder. <see cref="Rank"/> is competition rank, so equal
/// ratings share a rank and the next rank skips accordingly.
/// </summary>
public sealed record LeaderboardEntryResponse(
    Guid Id,
    string Slug,
    string Name,
    string Accent,
    string LookId,
    string Owner,
    double Rating,
    int RankedSets,
    int Rank);
