namespace BotArena.App.Bots;

public class Bot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string Accent { get; set; } = "#22d3ee";
    public string LookId { get; set; } = "vanguard";
    public string ProjectileLookId { get; set; } = "pulse-bolt";
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
    /// <summary>
    /// Opaque ladder identity for migrated ratings. Null during the additive
    /// compatibility window so older application images can continue writing
    /// rules-version keyed rows.
    /// </summary>
    public Guid? LadderId { get; set; }
    /// <summary>The resolved rules version string a set was played under ("0.5", "0.4",
    /// "0.4-exp-hill3"…) — experiments get their own ladders instead of polluting
    /// official elo.</summary>
    public required string RulesVersion { get; set; }
    /// <summary>Where an unrated bot enters a ladder. Named because matchmaking has to
    /// assume it for bots that have never played (DECISIONS #95).</summary>
    public const double DefaultRating = 1200;
    public double Rating { get; set; } = DefaultRating;
    public int RankedSets { get; set; }
    /// <summary>
    /// Competition rank captured in the ladder's season-opening snapshot. Null
    /// covers legacy ratings, ladders without that snapshot, and bots entering
    /// after the season opened; it is not a mid-season entry rank.
    /// </summary>
    public int? SeasonOpeningRank { get; set; }
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
    /// <summary>Server-keyed /24 IPv4 or /64 IPv6 pseudonym used only for abuse limits.</summary>
    public string? SubmissionNetworkHash { get; set; }
    public BuildStatus Status { get; set; } = BuildStatus.Pending;
    public string? BuildLog { get; set; }
    public string? ArtifactKey { get; set; }
    public string? ArtifactHash { get; set; }
    /// <summary>Public provenance receipt for a successful server build.</summary>
    public string? BuildReceiptJson { get; set; }
    /// <summary>For built-in bots hosted in the shared catalog artifact: the guest-side bot name.</summary>
    public string? GuestBotName { get; set; }
    public string GameRulesVersion { get; set; } = Engine.BotArenaVersions.GameRulesVersion;
    public string RuntimeProtocolVersion { get; set; } = Engine.BotArenaVersions.RuntimeProtocolVersion;
    public string RuntimeConfigurationVersion { get; set; } = Engine.BotArenaVersions.RuntimeConfigurationVersion;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? BuiltAt { get; set; }
    public bool IsActive { get; set; }
}
