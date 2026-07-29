namespace BotArena.Engine;

/// <summary>Immutable state owned exclusively by the pure Frontline objective kernel.</summary>
public sealed record FrontlineControlState(
    int NextTick,
    int ActivePositionIndex,
    int? ClaimingTeamId,
    int CaptureProgress,
    int DecayTicksElapsed,
    int ControlResumesAtTick,
    int? WinnerTeamId)
{
    /// <summary>
    /// The live territory-ratchet hold, or null when no advance is currently
    /// protected. Only the high-water-mark redeploy policy ever sets it; it is
    /// kernel-internal and is not part of the public observed control state.
    /// </summary>
    public FrontlineRatchetHold? RatchetHold { get; init; }
}

/// <summary>
/// One team's protected advance: the objective position it reached and the
/// last tick through which the frontline may not be pushed back past it.
/// </summary>
public sealed record FrontlineRatchetHold(
    int TeamId,
    int PositionIndex,
    int HoldsThroughTick);
