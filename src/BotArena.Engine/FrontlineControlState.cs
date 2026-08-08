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

    /// <summary>
    /// The channel run currently in progress, or null when nobody is
    /// channeling. Only the channel control policy ever sets it. It exists so
    /// the interrupt can revert WORK rather than the raw claim: the run
    /// remembers where the controller found the number, and no amount of
    /// damage can push the number past that point in the opponent's favour.
    /// <para>It is authoritative kernel state and is deliberately NOT
    /// published: <c>captureProgress</c> and <c>claimingTeamId</c> keep their
    /// exact published shape and meaning, and every channel rule moves those
    /// same two facts.</para>
    /// </summary>
    public FrontlineChannelRun? ChannelRun { get; init; }
}

/// <summary>
/// One controller's continuous stretch of control, and the signed claim it
/// found when the stretch began. The sign is read from the controller's own
/// side: positive is the controller's own standing claim, negative is the
/// opponent's, and zero is a neutral objective. Work done on the run is
/// exactly the distance the signed number has travelled since
/// <see cref="StartSignedProgress"/>, so reverting work is one clamp.
/// </summary>
/// <param name="TeamId">The team currently controlling.</param>
/// <param name="StartSignedProgress">
/// The signed claim, from that team's side, on the tick control began.
/// </param>
public sealed record FrontlineChannelRun(
    int TeamId,
    int StartSignedProgress);

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
