namespace BotArena.Engine;

/// <summary>
/// A stable, team-local body slot controlled by one submitted participant.
/// A participant may control more than one slot.
/// </summary>
public sealed record PublicUnitSlot(
    int TeamId,
    int UnitId,
    int ControllerParticipantId);
