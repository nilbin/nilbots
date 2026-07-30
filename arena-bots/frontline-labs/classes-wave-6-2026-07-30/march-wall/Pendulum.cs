using BotArena.Sdk;

/// <summary>
/// The ratchet clock. A mean-reverting frontline makes ground a rental: every
/// tile is worth the same as every other because all of it comes back. A
/// declared <c>ratchetHoldTicks</c> ends that — for the length of the hold the
/// team that advanced cannot be pushed back, and its opponent's completed
/// capture is SPENT: the claim resets and the objective does not move.
///
/// <para>Revision 3 INFERRED whose hold was live, and wrote three paragraphs of
/// DX about where the inference was blind. The observation now publishes both
/// facts — <c>holdOwnerTeamId</c> and <c>holdEndsAtTick</c> — so this class
/// asks. The blind spot it documented goes with it: a life born after the
/// redeploy pause lapsed could not tell that a hold was running at all, which
/// under a forward rally is the COMMON case, because that arm manufactures fresh
/// lives beside a fight they know nothing about.</para>
///
/// <para>The old derivation survives in one narrow role: a fallback for a
/// contract that declares a hold duration but whose observation never publishes
/// a hold. As soon as one published hold has been seen, the derivation is
/// retired for the rest of this life — a schema that publishes the fact is
/// authoritative about its absence too, and null then means "no hold binds"
/// rather than "not told".</para>
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

    // Published — read from the observation.
    private bool _everPublished;
    private Hold _publishedOwner = Hold.None;
    private int _publishedEndsAtTick = int.MinValue;

    // Derived — fallback only; see the class remark.
    private int _seenIndex = -1;
    private int _derivedStartTick = int.MinValue;
    private Hold _derivedOwner = Hold.None;

    public Pendulum(ContractView view) => _view = view;

    /// <summary>
    /// True while this life is still relying on the retired derivation, which
    /// means the ruleset declares a hold and the observation has not published
    /// one yet.
    /// </summary>
    public bool UsingFallback => !_everPublished && _view.HoldTicks is not null;

    public void Observe(GenericActorContext context)
    {
        if (ArenaBasics.LiveHold(context) is ArenaBasics.Hold live)
        {
            _everPublished = true;
            _publishedOwner = live.Mine ? Hold.Ours : Hold.Theirs;
            _publishedEndsAtTick = live.EndsAtTick;
        }
        else
        {
            _publishedOwner = Hold.None;
            _publishedEndsAtTick = int.MinValue;
        }

        if (_everPublished || _view.HoldTicks is null)
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
                _derivedStartTick =
                    mode.ControlResumesAtTick - 1 - _view.RedeployPauseTicks;
                _derivedOwner = Hold.Unknown;
            }
            return;
        }

        if (mode.ActivePositionIndex == _seenIndex)
            return;

        int moved = mode.ActivePositionIndex - _seenIndex;
        _derivedOwner = Math.Sign(moved) == Math.Sign(_view.AdvanceDelta)
            ? Hold.Ours
            : Hold.Theirs;
        _derivedStartTick = context.Tick - 1;
        _seenIndex = mode.ActivePositionIndex;
    }

    public Hold State(int tick)
    {
        if (_everPublished)
            return tick < _publishedEndsAtTick ? _publishedOwner : Hold.None;
        return _view.HoldTicks is not int hold
            || _derivedStartTick == int.MinValue
            || tick - _derivedStartTick >= hold
                ? Hold.None
                : _derivedOwner;
    }

    /// <summary>Ticks of protection left on the live hold, zero when none is.</summary>
    public int Remaining(int tick)
    {
        if (_everPublished)
            return Math.Max(0, _publishedEndsAtTick - tick);
        return _view.HoldTicks is not int hold
            || _derivedStartTick == int.MinValue
                ? 0
                : Math.Max(0, _derivedStartTick + hold - tick);
    }

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
