using BotArena.Sdk;

/// <summary>
/// The clock that decides whether a tick spent on the objective can ever become
/// an advance.
///
/// Three separate facts stack here, and only the first is delivered as an
/// observation field:
///
/// <list type="number">
/// <item>the redeploy pause - <c>ControlResumesAtTick</c> - during which
/// nobody's presence accumulates anything at all;</item>
/// <item>the ratchet hold - <c>capture.ratchetHoldTicks</c> - during which a
/// capture completed by the side that did NOT make the last advance is spent:
/// the claim resets exactly as a successful capture does and the front does not
/// move;</item>
/// <item>who owns that hold, which is not in the observation schema at all and
/// has to be inferred from the active position index changing under us.</item>
/// </list>
///
/// The inference is exact while a life is watching: an advance is the tick the
/// active index moves, its direction against this team's declared index delta
/// says whose it was, and the redeploy arithmetic the contract publishes
/// (<c>capture-tick-plus-one-plus-pause</c>) recovers the exact completion tick
/// from <c>ControlResumesAtTick</c>. It is unrecoverable across a death: private
/// memory is life-scoped, so a body that appears in the middle of somebody
/// else's hold can prove that an advance happened recently but not who made it.
/// That case is reported as <see cref="Phase.Open"/> - the baseline doctrine -
/// rather than guessed at.
/// </summary>
internal sealed class Ratchet
{
    private int _lastIndex = -1;
    private int _holdStart = int.MinValue;
    private int _holdTicks;
    private bool _holdIsOurs;
    private bool _holdKnown;

    /// <summary>What a tick of sole presence on the objective is worth now.</summary>
    public enum Phase
    {
        /// <summary>No hold is known to be running: ordinary capture value.</summary>
        Open,

        /// <summary>
        /// The redeploy pause after any advance. Control cannot resume, so
        /// presence buys strictly nothing this tick - for either side.
        /// </summary>
        Paused,

        /// <summary>
        /// Our own advance is protected. The front cannot move backwards until
        /// the hold expires, so ground committed forward cannot be lost.
        /// </summary>
        Sheltered,

        /// <summary>
        /// Their advance is protected and a capture we complete inside it would
        /// be spent. Presence still denies them a further advance, but it does
        /// not buy ground.
        /// </summary>
        Barren,
    }

    /// <summary>Whether the contract declares a hold at all.</summary>
    public bool Declared => _holdTicks > 0;

    /// <summary>Current phase, resolved by <see cref="Observe"/>.</summary>
    public Phase Current { get; private set; } = Phase.Open;

    /// <summary>Ticks until the live hold expires; zero when none is known.</summary>
    public int HoldRemaining { get; private set; }

    /// <summary>Ticks until the redeploy pause lifts; zero when control is live.</summary>
    public int PauseRemaining { get; private set; }

    /// <summary>
    /// True when the last advance is known to be ours - the window in which the
    /// contract guarantees that pushing forward cannot cost ground.
    /// </summary>
    public bool Sheltered => Current == Phase.Sheltered;

    /// <summary>
    /// True when a capture we complete right now would be discarded. Presence is
    /// then a denial instrument only, and a second body on the objective buys
    /// nothing that a first body has not already bought.
    /// </summary>
    public bool Barren => Current == Phase.Barren;

    /// <summary>Folds this tick's mode state into the clock.</summary>
    public void Observe(MatchLens lens, GenericActorContext context)
    {
        _holdTicks = lens.Capture?.HoldTicks ?? 0;
        if (context.Mode
            is not GenericActorContext.ModeObservationState.Frontline mode)
        {
            Current = Phase.Open;
            HoldRemaining = 0;
            PauseRemaining = 0;
            return;
        }

        // The pause is a published clock, so it is the one part of this that
        // survives a death intact.
        PauseRemaining = Math.Max(0, mode.ControlResumesAtTick - context.Tick);

        if (_lastIndex >= 0 && mode.ActivePositionIndex != _lastIndex)
        {
            int delta = mode.ActivePositionIndex - _lastIndex;
            _holdIsOurs = Math.Sign(delta) == Math.Sign(lens.AdvanceDelta);
            _holdStart = CompletionTick(lens, context, mode);
            _holdKnown = true;
        }
        _lastIndex = mode.ActivePositionIndex;

        HoldRemaining = _holdKnown && _holdTicks > 0
            ? Math.Max(0, _holdStart + _holdTicks - context.Tick)
            : 0;

        Current = PauseRemaining > 0
            ? Phase.Paused
            : HoldRemaining <= 0
                ? Phase.Open
                : _holdIsOurs
                    ? Phase.Sheltered
                    : Phase.Barren;
    }

    /// <summary>
    /// The tick the advance actually completed. The contract publishes its own
    /// redeploy arithmetic (<c>capture tick + 1 + pause</c>), so the completion
    /// tick is recoverable from the resume clock rather than guessed from the
    /// tick we happened to notice on; falling back to the current tick costs at
    /// most a couple of ticks on a forty-tick hold.
    /// </summary>
    private static int CompletionTick(
        MatchLens lens,
        GenericActorContext context,
        GenericActorContext.ModeObservationState.Frontline mode)
    {
        int pause = lens.Capture?.RedeployPauseTicks ?? 0;
        int derived = mode.ControlResumesAtTick - 1 - pause;
        return derived > 0 && derived <= context.Tick ? derived : context.Tick;
    }
}
