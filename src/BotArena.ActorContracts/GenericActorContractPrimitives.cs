namespace BotArena.ActorContracts;

internal readonly record struct Position(int X, int Y);

internal enum Direction
{
    North = 0,
    East = 1,
    South = 2,
    West = 3,
}

internal sealed record PublicScoringTeam(int TeamId);

internal sealed record PublicParticipant(int ParticipantId, int TeamId);

internal sealed record PublicUnitSlot(
    int TeamId,
    int UnitId,
    int ControllerParticipantId);

internal sealed record PublicInitialLife(
    int TeamId,
    int UnitId,
    int LifeId,
    string FormId);
