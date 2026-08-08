using BotArena.Sdk;

/// <summary>
/// The clock that decides whether a tick spent on the objective can ever become
/// an advance.
///
/// Revision 3 derived the hold. Revision 4 <b>asks</b>: the mode observation now
/// publishes <c>holdOwnerTeamId</c> and <c>holdEndsAtTick</c> together, so the
/// two facts the old code could not get right are delivered.
///
/// <list type="number">
/// <item>the redeploy pause - <c>ControlResumesAtTick</c> - during which
/// nobody's presence accumulates anything at all;</item>
/// <item>the ratchet hold - published as an owner and an expiry tick that read
/// exactly like the resume clock, so the hold binds while the observed tick is
/// strictly below it - during which a capture completed by the side that does
/// NOT own the hold is spent: the claim resets exactly as a successful capture
/// does and the front does not move;</item>
/// <item>null means no hold binds this tick, including every ruleset whose
/// capture definition declares no ratchet at all.</item>
/// </list>
///
/// The old derivation is retained as a <b>fallback only</b>, for a contract that
/// declares a hold duration but publishes no live clock. It was expensive and
/// partly wrong: the owner had no derivation except the sign of the front's
/// displacement, which is wrong the first time an opponent regresses from a lead
/// and is unavailable to a life born inside the hold, because private memory is
/// life-scoped. That blind spot - the single friction I ranked first at revision
/// 3 - is closed, and <see cref="OwnerRead"/> reports which of the two answered.
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

    /// <summary>How this tick's hold ownership was established.</summary>
    public enum Source
    {
        /// <summary>No hold binds this tick.</summary>
        None,

        /// <summary>The observation published the owner and the expiry.</summary>
        Published,

        /// <summary>The contract declared a duration but published no clock.</summary>
        Inferred,
    }

    /// <summary>Which channel answered "whose hold is this" this tick.</summary>
    public Source OwnerRead { get; private set; } = Source.None;

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
            OwnerRead = Source.None;
            return;
        }

        // The pause is a published clock, so it is the one part of this that
        // survives a death intact.
        PauseRemaining = Math.Max(0, mode.ControlResumesAtTick - context.Tick);

        // Ask first. The published hold travels as owner + expiry or not at all,
        // and it never appears in the past, so a delivered hold is a binding one
        // and its remaining ticks are the difference. A life born inside the
        // hold reads it exactly as well as a life that watched the advance.
        if (ArenaBasics.LiveHold(context) is ArenaBasics.Hold hold)
        {
            _holdIsOurs = hold.Mine;
            HoldRemaining = Math.Max(0, hold.RemainingTicks);
            OwnerRead = Source.Published;
            _holdKnown = true;
        }
        else
        {
            // Fallback, and only that: a ruleset that declares ratchetHoldTicks
            // yet publishes no live clock. Watch the front move and read the
            // sign of the move against our own declared index delta.
            if (_lastIndex >= 0 && mode.ActivePositionIndex != _lastIndex)
            {
                int delta = mode.ActivePositionIndex - _lastIndex;
                _holdIsOurs = Math.Sign(delta) == Math.Sign(lens.AdvanceDelta);
                _holdStart = CompletionTick(lens, context, mode);
                _holdKnown = true;
            }
            HoldRemaining = _holdKnown && _holdTicks > 0
                ? Math.Max(0, _holdStart + _holdTicks - context.Tick)
                : 0;
            OwnerRead = HoldRemaining > 0 ? Source.Inferred : Source.None;
        }
        _lastIndex = mode.ActivePositionIndex;

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
