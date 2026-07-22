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
    public string? ArtifactPath { get; set; }
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
