using BotArena.Sdk;

/// <summary>
/// The ratchet clock. A mean-reverting frontline makes ground a rental: every
/// tile is worth the same as every other tile because all of it comes back. A
/// declared <c>ratchetHoldTicks</c> ends that — for the length of the hold the
/// team that advanced cannot be pushed back, and its opponent's completed
/// capture is SPENT: the claim resets and the objective does not move.
///
/// That single fact re-prices presence in both directions, so the doctrine has
/// to know whose hold is live and how much of it is left. Neither is an
/// observation field. Two derivations give it:
///
/// 1. An advance is a change in the observed active position index, and its
///    sign against our declared advance delta names the team that made it.
///    A body alive across the change knows both facts exactly.
/// 2. On a life's first tick there is no history — but the redeploy pause
///    dates the advance anyway. <c>ControlResumesAtTick</c> is the capture tick
///    plus one plus the declared pause, so a live pause is a timestamp. It does
///    not name the team, and a life that appears after the pause lapses cannot
///    see the hold at all. That is the honest limit of the inference; the
///    doctrine treats an unnamed hold as the opponent's, which is the reading
///    that never spends a body on a claim it might not be able to bank.
/// </summary>
internal sealed class Pendulum
{
    internal enum Hold
    {
        /// <summary>No hold is declared, or none is live.</summary>
        None,

        /// <summary>Our own advance is protected; their capture is spent.</summary>
        Ours,

        /// <summary>Their advance is protected; our capture is spent.</summary>
        Theirs,

        /// <summary>A hold is live but this life never saw who made it.</summary>
        Unknown,
    }

    private readonly ContractView _view;
    private int _seenIndex = -1;
    private int _holdStartTick = int.MinValue;
    private Hold _holdOwner = Hold.None;

    public Pendulum(ContractView view) => _view = view;

    public void Observe(GenericActorContext context)
    {
        if (_view.HoldTicks is null)
            return;
        if (context.Mode is not GenericActorContext.ModeObservationState.Frontline
            mode)
        {
            return;
        }

        if (_seenIndex < 0)
        {
            _seenIndex = mode.ActivePositionIndex;
            if (mode.ControlResumesAtTick > context.Tick)
            {
                _holdStartTick =
                    mode.ControlResumesAtTick - 1 - _view.RedeployPauseTicks;
                _holdOwner = Hold.Unknown;
            }
            return;
        }

        if (mode.ActivePositionIndex == _seenIndex)
            return;

        int moved = mode.ActivePositionIndex - _seenIndex;
        _holdOwner = Math.Sign(moved) == Math.Sign(_view.AdvanceDelta)
            ? Hold.Ours
            : Hold.Theirs;
        _holdStartTick = context.Tick - 1;
        _seenIndex = mode.ActivePositionIndex;
    }

    public Hold State(int tick) =>
        _view.HoldTicks is not int hold
        || _holdStartTick == int.MinValue
        || tick - _holdStartTick >= hold
            ? Hold.None
            : _holdOwner;

    /// <summary>Ticks of protection left on the live hold, zero when none is.</summary>
    public int Remaining(int tick) =>
        _view.HoldTicks is not int hold || _holdStartTick == int.MinValue
            ? 0
            : Math.Max(0, _holdStartTick + hold - tick);

    /// <summary>
    /// Ticks of control one team still needs to finish the claim in front of
    /// it, from the observed progress and the declared threshold and gain. A
    /// team that is not the current claimant starts from zero.
    /// </summary>
    public int TicksToComplete(GenericActorContext context, int teamId)
    {
        if (context.Mode
            is not GenericActorContext.ModeObservationState.Frontline mode)
        {
            return int.MaxValue;
        }
        int progress = mode.ClaimingTeamId == teamId ? mode.CaptureProgress : 0;
        int remaining = Math.Max(0, _view.CaptureThreshold - progress);
        return (remaining + _view.CaptureGain - 1) / _view.CaptureGain;
    }

    /// <summary>
    /// Whether a claim built from here is worth building. Inside an opposing
    /// hold it is not — the completion is discarded and the front does not
    /// move — unless the hold lapses before we could finish, in which case the
    /// capture we start now lands on the far side of it and banks normally.
    /// This is the whole timing question the hold creates, and it is why a
    /// ratcheted front rewards arriving with a claim ready rather than early.
    /// </summary>
    public bool ClaimIsBankable(GenericActorContext context)
    {
        Hold hold = State(context.Tick);
        if (hold is Hold.None or Hold.Ours)
            return true;
        return Remaining(context.Tick)
            <= TicksToComplete(context, _view.MyTeamId);
    }

    /// <summary>
    /// True while our own advance is protected: the front cannot come back, so
    /// every tick of the hold is free to spend on the next position.
    /// </summary>
    public bool OurGroundIsSafe(int tick) => State(tick) == Hold.Ours;
}
