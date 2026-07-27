namespace BotArena.Engine;

/// <summary>Immutable state owned exclusively by the pure Frontline objective kernel.</summary>
public sealed record FrontlineControlState(
    int NextTick,
    int ActivePositionIndex,
    int? ClaimingTeamId,
    int CaptureProgress,
    int DecayTicksElapsed,
    int ControlResumesAtTick,
    int? WinnerTeamId);
