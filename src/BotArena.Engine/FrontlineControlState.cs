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
    /// protected. Only the high-water-mark redeploy policy ever sets it. The
    /// observation projection exposes its owner and remaining duration while
    /// retaining this richer authoritative position/tick representation.
    /// </summary>
    public FrontlineRatchetHold? RatchetHold { get; init; }

    /// <summary>
    /// The side objective's latch, or null when the mode declares no
    /// secondary control at all. Kept beside the front's own claim because
    /// both are contested control clocks resolved from the same objective
    /// weight; the observation projection publishes the owner and a signed
    /// claim, and this record keeps the authoritative claimant/tick pair.
    /// </summary>
    public FrontlineSecondaryControlState? SecondaryControl { get; init; }
}

/// <summary>
/// The side objective's authoritative latch: who owns it, who is currently
/// claiming it, and how many consecutive sole-presence ticks that claim has
/// accumulated. Ownership survives an empty or contested site; a claim does
/// not.
/// </summary>
/// <param name="OwnerTeamId">Latched owner, or null while neutral.</param>
/// <param name="ClaimingTeamId">
/// The team accumulating a claim, or null when no claim stands.
/// </param>
/// <param name="ClaimTicks">
/// Consecutive sole-presence ticks the standing claim has accumulated,
/// strictly below the declared threshold (reaching it completes a capture).
/// </param>
public sealed record FrontlineSecondaryControlState(
    int? OwnerTeamId,
    int? ClaimingTeamId,
    int ClaimTicks);

/// <summary>
/// One team's protected advance: the objective position it reached and the
/// last tick through which the frontline may not be pushed back past it.
/// </summary>
public sealed record FrontlineRatchetHold(
    int TeamId,
    int PositionIndex,
    int HoldsThroughTick);
