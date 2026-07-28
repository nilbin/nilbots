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
