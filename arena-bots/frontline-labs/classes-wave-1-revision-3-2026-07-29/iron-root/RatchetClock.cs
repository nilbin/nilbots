using BotArena.Sdk;

/// <summary>
/// Which team's progress is real right now.
///
/// <para>A capture definition may declare a <em>hold</em>: after one team
/// completes an advance, the opposing team's captures are spent for
/// <c>ratchetHoldTicks</c> — the claim resets exactly as a successful capture
/// does, and the front does not move. That single field turns "stand on the
/// objective" from one instruction into two opposite ones. Inside the hold the
/// owner's presence buys ground and the opponent's presence buys nothing; a
/// doctrine that cannot tell which side of it it is on will pay a full capture
/// window for a reset, over and over.</para>
///
/// <para><b>Neither the hold's start nor its owner is an observation field.</b>
/// Both are inferred, and this class is the whole inference:</para>
/// <list type="number">
/// <item><b>The clock.</b> <c>ControlResumesAtTick</c> is published every tick
/// and is defined as the advance tick plus the declared redeploy pause, so
/// <c>ControlResumesAtTick - RedeployPauseTicks</c> is the tick of the most
/// recent advance — available to a body on the first tick of its life, with no
/// memory at all. That is the only reason a doctrine built on this works at all
/// under a contract that hands every new life empty private memory.</item>
/// <item><b>The owner, observed.</b> A life that watches the active position
/// index move knows exactly who moved it: the sign of the change against this
/// team's own declared index delta.</item>
/// <item><b>The owner, proved.</b> A capture that completes inside a live hold
/// is visible as progress collapsing from one step below the threshold to zero
/// with the index unchanged. Declared gain and erosion are one per tick, so a
/// full-threshold drop in a single tick cannot be decay — it is a spent
/// capture, and whose it was names the hold's owner outright.</item>
/// <item><b>The owner, guessed.</b> Failing both, the signed displacement of
/// the front from the chain's centre is a prior: a team that is ahead usually
/// put itself there. It is wrong after an opponent's first regression from a
/// two-position lead, so it is reported as a guess and the doctrine treats an
/// unresolved hold as no hold.</item>
/// </list>
///
/// <para>Everything is inert on a contract that declares no hold: the field is
/// omitted from canonical bytes when it does not apply, so
/// <see cref="Phase"/> is <see cref="HoldPhase.None"/> and every caller falls
/// back to its unratcheted rule.</para>
/// </summary>
internal sealed class RatchetClock
{
    private int _lastIndex = int.MinValue;
    private int _lastProgress;
    private int? _lastClaimant;
    private int _knownAdvanceTick = int.MinValue;
    private bool _knownAdvanceMine;

    /// <summary>Whose hold, if any, is live this tick.</summary>
    public HoldPhase Phase { get; private set; } = HoldPhase.None;

    /// <summary>Ticks the live hold still has to run; zero when none.</summary>
    public int Remaining { get; private set; }

    /// <summary>
    /// True when the phase came from a watched index change or a watched spent
    /// capture rather than from the displacement prior. The doctrine only
    /// spends irreversible commitments on a certain phase.
    /// </summary>
    public bool Certain { get; private set; }

    public void Reset()
    {
        _lastIndex = int.MinValue;
        _lastProgress = 0;
        _lastClaimant = null;
        _knownAdvanceTick = int.MinValue;
        _knownAdvanceMine = false;
        Phase = HoldPhase.None;
        Remaining = 0;
        Certain = false;
    }

    public void Update(
        ContractLens lens,
        GenericActorContext context,
        GenericActorContext.ModeObservationState.Frontline? mode)
    {
        if (mode is null || lens.HoldTicks <= 0)
        {
            Phase = HoldPhase.None;
            Remaining = 0;
            Certain = true;   // "no hold" is a fact, not a guess
            return;
        }

        // (2) An index change names its owner by the sign of the move.
        if (_lastIndex != int.MinValue && mode.ActivePositionIndex != _lastIndex)
        {
            int moved = mode.ActivePositionIndex - _lastIndex;
            _knownAdvanceTick = context.Tick;
            _knownAdvanceMine = moved != 0
                && Math.Sign(moved) == Math.Sign(lens.AdvanceDelta);
        }

        // (3) A capture that vanished without moving the front was spent, which
        // means the OTHER team owns the live hold.
        bool completed = _lastIndex == mode.ActivePositionIndex
            && lens.CaptureThreshold > 0
            && _lastProgress >= lens.CaptureThreshold - 1
            && mode.CaptureProgress == 0;
        if (completed && _lastClaimant is int spender)
        {
            _knownAdvanceTick = LastAdvanceTick(lens, mode);
            _knownAdvanceMine = spender != lens.TeamId;
        }

        _lastIndex = mode.ActivePositionIndex;
        _lastProgress = mode.CaptureProgress;
        _lastClaimant = mode.ClaimingTeamId;

        // (1) The clock, from a field every life sees on its first tick.
        int advance = LastAdvanceTick(lens, mode);
        if (advance < 0)
        {
            Phase = HoldPhase.None;
            Remaining = 0;
            Certain = true;
            return;
        }
        int remaining = advance + lens.HoldTicks - context.Tick;
        if (remaining <= 0)
        {
            Phase = HoldPhase.None;
            Remaining = 0;
            Certain = true;
            return;
        }
        Remaining = remaining;

        if (_knownAdvanceTick == advance)
        {
            Phase = _knownAdvanceMine ? HoldPhase.Ours : HoldPhase.Theirs;
            Certain = true;
            return;
        }

        // (4) The prior, explicitly labelled as one.
        int centre = lens.PositionCount / 2;
        int signed = (mode.ActivePositionIndex - centre)
            * Math.Sign(lens.AdvanceDelta == 0 ? 1 : lens.AdvanceDelta);
        Certain = false;
        Phase = signed > 0
            ? HoldPhase.Ours
            : signed < 0 ? HoldPhase.Theirs : HoldPhase.Unknown;
    }

    /// <summary>
    /// The tick of the most recent advance, or a negative value when no advance
    /// has happened yet. <c>ControlResumesAtTick</c> is the advance tick plus
    /// the declared pause; at match start it is the mode's initial value and
    /// carries no advance.
    /// </summary>
    private static int LastAdvanceTick(
        ContractLens lens,
        GenericActorContext.ModeObservationState.Frontline mode) =>
        mode.ControlResumesAtTick <= 0
            ? -1
            : mode.ControlResumesAtTick - Math.Max(0, lens.RedeployPauseTicks);
}

/// <summary>Whose completed advance, if any, is currently protected.</summary>
internal enum HoldPhase
{
    /// <summary>No hold is declared, or none is live.</summary>
    None,

    /// <summary>Our advance is protected: our progress counts, theirs is spent.</summary>
    Ours,

    /// <summary>Their advance is protected: their progress counts, ours is spent.</summary>
    Theirs,

    /// <summary>A hold is live but the evidence does not name its owner.</summary>
    Unknown,
}
