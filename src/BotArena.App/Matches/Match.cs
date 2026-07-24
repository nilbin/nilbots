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
    public string? ReplayKey { get; set; }
    public string? ReplayHash { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    /// <summary>Presentation timeline start (plan §28.1): the simulation completes first,
    /// then events are presented from this instant at PresentationTicksPerSecond.</summary>
    public DateTime? BroadcastStartedAt { get; set; }
    public int PresentationTicksPerSecond { get; set; } = 5;
    public Guid? MatchSetId { get; set; }
    /// <summary>1-based game number inside a ranked set.</summary>
    public int? SetGame { get; set; }
    public List<MatchParticipant> Participants { get; set; } = [];

    /// <summary>The tick the shared presentation clock has reached (-1 during countdown).</summary>
    public int PresentationTick(DateTime utcNow)
    {
        if (BroadcastStartedAt is not DateTime start)
            return int.MaxValue; // Legacy matches: fully visible.
        double elapsed = (utcNow - start).TotalSeconds;
        if (elapsed < 0)
            return -1;
        // Old matches: elapsed * tps overflows int after weeks; clamp or they un-reveal.
        double tick = elapsed * PresentationTicksPerSecond;
        return tick >= int.MaxValue ? int.MaxValue : (int)tick;
    }

    public bool BroadcastComplete(DateTime utcNow) =>
        Status == MatchStatus.Completed &&
        (BroadcastStartedAt is null || EndTick is null || PresentationTick(utcNow) > EndTick.Value);
}

/// <summary>Snapshot of who fought (plan §33.4/§39): names, presentation, and
/// artifact hashes are copied at challenge time so history never changes when bots evolve.</summary>
public class MatchParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MatchId { get; set; }
    public int Slot { get; set; }
    public Guid BotId { get; set; }
    public Guid BotVersionId { get; set; }
    public required string NameSnapshot { get; set; }
    public required string AccentSnapshot { get; set; }
    public string LookIdSnapshot { get; set; } = "vanguard";
    public string ArtifactHashSnapshot { get; set; } = "";
    public string? Outcome { get; set; }
    public int? FinalHealth { get; set; }
    public int? DamageDealt { get; set; }
    public int? Faults { get; set; }
}
