using BotArena.App.Matches;

namespace BotArena.App.Bots;

/// <summary>
/// A bot as it appears in the public roster. Projected directly from EF, so every
/// member has to stay translatable — keep computation in the query, not the record.
/// <para>
/// <see cref="CurrentStanding"/> is attached after the projection, not by it: rank is a
/// property of the whole ladder, so it cannot be computed per row in SQL. It carries the
/// same shape as the bot detail endpoint's, so a client renders standings identically
/// whether it came from the roster or a bot page — and never has to join the leaderboard
/// itself to find a rank.
/// </para>
/// </summary>
public sealed record BotSummaryResponse(
    Guid Id,
    string Name,
    string Slug,
    string Accent,
    string LookId,
    string ProjectileLookId,
    DateTime CreatedAt,
    IReadOnlyList<BotLadderRatingResponse> Ratings,
    string Owner,
    BotActiveVersionResponse? ActiveVersion,
    int VersionCount,
    LadderStanding? CurrentStanding = null);

/// <summary>One rules-version ladder a bot has fought on (DECISIONS #54), newest first.</summary>
public sealed record BotLadderRatingResponse(string RulesVersion, double Rating, int RankedSets);

/// <summary>The built version currently fighting for a bot, absent until one builds.</summary>
public sealed record BotActiveVersionResponse(
    Guid Id,
    int VersionNumber,
    string? ArtifactHash,
    IReadOnlyList<string>? SupportedContractProfiles);

/// <summary>
/// A bot owned by the caller. Deliberately narrower than <see cref="BotSummaryResponse"/>:
/// the garage lists bots and their build state, and nothing else.
/// </summary>
public sealed record MyBotResponse(
    Guid Id,
    string Name,
    string Slug,
    string Accent,
    string LookId,
    string ProjectileLookId,
    MyBotVersionResponse? LatestVersion);

/// <summary>The newest version of an owned bot, whatever its build state.</summary>
public sealed record MyBotVersionResponse(int VersionNumber, string Status, bool IsActive);

/// <summary>
/// Build state for one version. Deliberately excludes sources and logs so polling this
/// endpoint stays cheap — <c>GET /api/bots/{key}</c> carries those for the owner.
/// </summary>
public sealed record BotBuildStatusResponse(
    Guid Id,
    int VersionNumber,
    string Status,
    string? ArtifactHash,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? BuiltAt);
