namespace BotArena.App.Matches;

public enum MatchStatus
{
    Pending,
    Running,
    Completed,
    Failed,
}

public class Match
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string MapId { get; set; }
    public int MapVersion { get; set; } = 1;
    public string GameRulesVersion { get; set; } = Engine.BotArenaVersions.GameRulesVersion;
    public string RuntimeConfigurationVersion { get; set; } = Engine.BotArenaVersions.RuntimeConfigurationVersion;
    /// <summary>Stored as the two's-complement bigint of the unsigned seed.</summary>
    public long Seed { get; set; }
    public MatchStatus Status { get; set; } = MatchStatus.Pending;
    public int? WinnerSlot { get; set; }
    public string? EndReason { get; set; }
    public int? EndTick { get; set; }
    public string? ReplayPath { get; set; }
    public string? ReplayHash { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public List<MatchParticipant> Participants { get; set; } = [];
}

/// <summary>Snapshot of who fought (plan §33.4/§39): names, accents and artifact hashes are
/// copied at challenge time so history never changes when bots evolve.</summary>
public class MatchParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MatchId { get; set; }
    public int Slot { get; set; }
    public Guid BotId { get; set; }
    public Guid BotVersionId { get; set; }
    public required string NameSnapshot { get; set; }
    public required string AccentSnapshot { get; set; }
    public string ArtifactHashSnapshot { get; set; } = "";
    public string? Outcome { get; set; }
    public int? FinalHealth { get; set; }
    public int? DamageDealt { get; set; }
    public int? Faults { get; set; }
}
