using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Exact match-local ownership and initial-life topology. Counts come from
/// these collections rather than from a runtime's currently visible allies.
/// </summary>
public sealed record PublicMatchTopology
{
    public required ImmutableArray<PublicScoringTeam> Teams { get; init; }
    public required ImmutableArray<PublicParticipant> Participants { get; init; }
    public required ImmutableArray<PublicUnitSlot> UnitSlots { get; init; }
    public required ImmutableArray<PublicInitialLife> InitialLives { get; init; }
}

/// <summary>A side with its own objective score and win/loss result.</summary>
public sealed record PublicScoringTeam(int TeamId);

/// <summary>A submitted policy/artifact assigned to one scoring team.</summary>
public sealed record PublicParticipant(int ParticipantId, int TeamId);

/// <summary>
/// A stable, team-local body slot controlled by one submitted participant.
/// A participant may control more than one slot.
/// </summary>
public sealed record PublicUnitSlot(
    int TeamId,
    int UnitId,
    int ControllerParticipantId);

/// <summary>
/// A runtime life occupying a stable unit slot at tick zero. Later lives are
/// dynamic match state and receive a new life ID without changing the slot.
/// </summary>
public sealed record PublicInitialLife(
    int TeamId,
    int UnitId,
    int LifeId,
    string FormId);
