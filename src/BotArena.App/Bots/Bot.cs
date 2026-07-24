namespace BotArena.App.Bots;

public class Bot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string Accent { get; set; } = "#22d3ee";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<BotVersion> Versions { get; set; } = [];
    public List<BotRating> Ratings { get; set; } = [];
}

/// <summary>One elo ladder entry per (bot, rules version) — DECISIONS #54: a rules era
/// change must not vaporize a bot's standing. The ladder a set moves is the ladder of
/// the rules it was played under; legacy bots keep their old-era ratings and rank
/// fresh (1200) on new eras. Pilot deviation from plan §36 (version-level ratings)
/// carries over from the bot-level design: bots, not versions, hold ladder identity.</summary>
public class BotRating
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BotId { get; set; }
    /// <summary>The resolved rules version string a set was played under ("0.5", "0.4",
    /// "0.4-exp-hill3"…) — experiments get their own ladders instead of polluting
    /// official elo.</summary>
    public required string RulesVersion { get; set; }
    public double Rating { get; set; } = 1200;
    public int RankedSets { get; set; }
}

public enum BuildStatus
{
    Pending,
    Building,
    Built,
    Failed,
}

/// <summary>Immutable once built (plan §35): sources, entry type, artifact hash and the
/// toolchain versions are snapshotted per version. The server-built artifact is canonical.</summary>
public class BotVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BotId { get; set; }
    public int VersionNumber { get; set; }
    public required string EntryType { get; set; }
    /// <summary>Submitted sources as JSON: [{"relativePath":..., "content":...}].</summary>
    public required string SourcesJson { get; set; }
    public required string SourceHash { get; set; }
    public BuildStatus Status { get; set; } = BuildStatus.Pending;
    public string? BuildLog { get; set; }
    public string? ArtifactKey { get; set; }
    public string? ArtifactHash { get; set; }
    /// <summary>For built-in bots hosted in the shared catalog artifact: the guest-side bot name.</summary>
    public string? GuestBotName { get; set; }
    public string GameRulesVersion { get; set; } = Engine.BotArenaVersions.GameRulesVersion;
    public string RuntimeProtocolVersion { get; set; } = Engine.BotArenaVersions.RuntimeProtocolVersion;
    public string RuntimeConfigurationVersion { get; set; } = Engine.BotArenaVersions.RuntimeConfigurationVersion;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? BuiltAt { get; set; }
    public bool IsActive { get; set; }
}
