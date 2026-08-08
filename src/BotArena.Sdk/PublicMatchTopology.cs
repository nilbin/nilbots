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

/// <summary>
/// A stable, team-local body slot controlled by one submitted participant.
/// </summary>
/// <param name="TeamId">The scoring team the slot belongs to.</param>
/// <param name="UnitId">The team-local stable handle, which survives death.</param>
/// <param name="ControllerParticipantId">The participant that commands it.</param>
/// <param name="ClassId">
/// The chassis this slot's bodies carry, or null when the ruleset declares no
/// compositions. Under a mixed composition your army's capability set is per
/// BODY, not per team: read each body's legality mask rather than assuming
/// your class's shape. Absent on every classless contract — the additive inert
/// default, so a bot never branches on whether the mechanic exists.
/// </param>
public sealed record PublicUnitSlot(
    int TeamId,
    int UnitId,
    int ControllerParticipantId,
    string? ClassId = null);

public sealed record PublicInitialLife(
    int TeamId,
    int UnitId,
    int LifeId,
    string FormId);
