using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>
/// Exact match-local teams, submitted participants, stable unit slots, and
/// initial lives. Never infer player count from currently visible allies.
/// </summary>
public sealed record PublicMatchTopology
{
    public required ImmutableArray<PublicScoringTeam> Teams { get; init; }
    public required ImmutableArray<PublicParticipant> Participants { get; init; }
    public required ImmutableArray<PublicUnitSlot> UnitSlots { get; init; }
    public required ImmutableArray<PublicInitialLife> InitialLives { get; init; }
}

public sealed record PublicScoringTeam(
    int TeamId,
    string? ClassId = null);

public sealed record PublicParticipant(
    int ParticipantId,
    int TeamId,
    string? ClassId = null);

public sealed record PublicUnitSlot(
    int TeamId,
    int UnitId,
    int ControllerParticipantId);

public sealed record PublicInitialLife(
    int TeamId,
    int UnitId,
    int LifeId,
    string FormId);
