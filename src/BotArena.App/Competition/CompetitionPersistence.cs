namespace BotArena.App.Competition;

/// <summary>
/// Stable public identity for a family of immutable playlist revisions.
/// Display metadata belongs here; executable match identity belongs to a
/// specific <see cref="PlaylistVersion"/>.
/// </summary>
public sealed class Playlist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Key { get; set; }
    public required string DisplayName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<PlaylistVersion> Versions { get; set; } = [];
}

/// <summary>
/// Immutable persisted definition of an exact game-mode, ruleset, match-format,
/// map-pool, and series-policy combination.
/// </summary>
public sealed class PlaylistVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlaylistId { get; set; }
    public int Version { get; set; }
    public required string GameModeId { get; set; }
    public required string RulesetId { get; set; }
    public required string MatchFormatId { get; set; }
    public required string MapPoolId { get; set; }
    public required string SeriesPolicyId { get; set; }
    public required string MatchmakingPolicyId { get; set; }
    public required string AdmissionPolicyId { get; set; }
    public required string CanonicalDefinition { get; set; }
    public required string DefinitionFingerprint { get; set; }
    public required string Provenance { get; set; }
    public required string Visibility { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A named publishing and competition window shared by any number of
/// playlist-specific ladders.
/// </summary>
public sealed class Season
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Key { get; set; }
    public required string DisplayName { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// One opaque rating population. The identity is deliberately independent of
/// ruleset names so seasons and formats can share mechanics without sharing
/// ratings.
/// </summary>
public sealed class Ladder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlaylistVersionId { get; set; }
    public Guid SeasonId { get; set; }
    public LadderStatus Status { get; set; } = LadderStatus.Draft;
    public required string RatingPolicyId { get; set; }
    public string? LegacyRulesVersion { get; set; }
    public bool IsListed { get; set; }
    public bool AwardsAchievements { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
