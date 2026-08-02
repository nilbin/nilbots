namespace BotArena.App.ArcRelay;

public enum ArcRelayEntrantKind
{
    Sheet,
    CustomMind,
}

public enum ArcRelayPreflightStatus
{
    NotRequired,
    Required,
    Pending,
    Passed,
    Failed,
}

/// <summary>
/// Persistent Arc Relay competitive identity. Sheet contents and mind artifacts
/// may be revised; the entrant, crest and rating population do not move.
/// </summary>
public sealed class ArcRelayEntrant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }
    public ArcRelayEntrantKind Kind { get; set; }
    public required string Name { get; set; }
    public int CrestVariant { get; set; }

    /// <summary>
    /// Internal controlled-build identity for a custom mind. Null for sheets,
    /// whose shared primary key resolves their stock-mind data.
    /// </summary>
    public Guid? MindBotId { get; set; }

    /// <summary>Canonical ordered eight-class declaration for a custom mind.</summary>
    public string? CompositionJson { get; set; }
    public string? CompositionHash { get; set; }

    public ArcRelayPreflightStatus PreflightStatus { get; set; } =
        ArcRelayPreflightStatus.NotRequired;
    public Guid? PreflightMatchId { get; set; }
    public int? PreflightRevision { get; set; }
    public string? PreflightFailure { get; set; }

    public bool LadderOptedIn { get; set; }
    public DateTime? LadderOptedInAt { get; set; }
    public string? SuspensionReason { get; set; }
    public Guid? SuspensionMatchId { get; set; }
    public DateTime? SuspendedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
