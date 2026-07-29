using BotArena.App.Competition;

namespace BotArena.App.Matches;

public enum MatchSetStatus
{
    Running,
    Completed,
    Failed,
}

/// <summary>
/// A ranked match set (plan §36): six games — three map/seed pairs, each played twice
/// with mirrored starting slots so positional advantage cancels out. Ratings change
/// only when a whole set completes.
/// </summary>
public class MatchSet
{
    public const int Games = DuelMirrored6V1.GameCount;
    public const double EloK = DuelEloV1.KFactor;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BotAId { get; set; }
    public Guid BotBId { get; set; }
    public Guid BotAVersionId { get; set; }
    public Guid BotBVersionId { get; set; }
    /// <summary>The legacy rules NAME the challenger supplied. Identity-bearing
    /// sets execute the exact playlist/rules version pinned below; only rows
    /// written by an old image with no playlist identity retain the historical
    /// null-means-server-default execution behavior.</summary>
    public string? RulesName { get; set; }
    public string GameRulesVersion { get; set; } = Engine.BotArenaVersions.GameRulesVersion;
    public string RuntimeConfigurationVersion { get; set; } = Engine.BotArenaVersions.RuntimeConfigurationVersion;
    /// <summary>
    /// Exact immutable playlist revision selected when this series was queued.
    /// Null only for legacy or not-yet-backfilled rows during migration.
    /// </summary>
    public Guid? PlaylistVersionId { get; set; }
    /// <summary>
    /// Opaque rating population selected when this ranked series was queued.
    /// Unranked and legacy/not-yet-backfilled series may leave it null.
    /// </summary>
    public Guid? LadderId { get; set; }
    public MatchSetStatus Status { get; set; } = MatchSetStatus.Running;
    /// <summary>Set points: 1 per game win, 0.5 per draw.</summary>
    public double ScoreA { get; set; }
    public double ScoreB { get; set; }
    public double RatingABefore { get; set; }
    public double RatingBBefore { get; set; }
    public double RatingChangeA { get; set; }
    public double RatingChangeB { get; set; }
    public Guid? WinnerBotId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
