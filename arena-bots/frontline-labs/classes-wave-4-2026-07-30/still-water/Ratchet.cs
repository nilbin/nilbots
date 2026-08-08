using BotArena.Sdk;

/// <summary>
/// What a push is actually worth on this contract, and when.
///
/// A mean-reverting frontline and a ratcheted one reward opposite tempo. When
/// <see cref="ArenaBasics.CaptureRules.HoldTicks"/> is declared, a completed
/// advance is protected: a capture finished inside the holder's window is
/// <em>spent</em> — the claim resets exactly as a successful capture does and
/// the objective does not move. So the same fifteen ticks of presence buy a
/// position, or buy nothing at all, depending on a clock that is not in the
/// observation schema.
///
/// <para>
/// That clock is now PUBLISHED. <c>holdOwnerTeamId</c> and
/// <c>holdEndsAtTick</c> arrive on the Frontline mode observation, travelling
/// together, in the same grammar as <c>controlResumesAtTick</c>, and
/// <see cref="ArenaBasics.LiveHold"/> is the one-line read. Revision 3 had to
/// reconstruct both, and the owner had no sound derivation at all — only a
/// guess from front displacement, which is wrong the first time an opponent
/// regresses from a lead and is unavailable to a life born inside the hold,
/// because private memory is life-scoped. Revision 4 asks.
/// </para>
///
/// <para>
/// The reconstruction is retained below as a FALLBACK for the one shape the
/// observation can take that is not an answer: a contract that declares
/// <c>ratchetHoldTicks</c> while publishing a half-populated pair. It never runs
/// when the fields are present, and a published null pair means "no hold binds
/// this tick" rather than "no information". It reconstructs the clock four
/// independent ways so that a life born at any point in the match has an
/// answer:
/// </para>
///
/// <list type="number">
/// <item>a change in <c>ActivePositionIndex</c> between two ticks this life
/// saw — exact, including who advanced, from the sign against this team's
/// declared index delta;</item>
/// <item><c>ControlResumesAtTick</c> minus the declared redeploy pause — the
/// advance tick to the tick, available to a life that opens its eyes inside
/// the pause;</item>
/// <item>a claim at or near the threshold that resets to zero while the index
/// stays put — the signature of a capture being spent, which proves the
/// <em>other</em> side owns a live hold;</item>
/// <item>displacement of the front from the centre of the chain, which names
/// the side that made the last advance whenever the front is not level.</item>
/// </list>
///
/// Every branch defaults to "unknown", and unknown behaves exactly like a
/// contract with no hold at all. A doctrine that guesses wrong about a hold
/// either throws away a capture or defends ground that cannot be taken; both
/// are worse than playing the baseline.
/// </summary>
internal sealed class Ratchet
{
    private readonly Doctrine _doctrine;

    private int _lastIndex = -1;
    private int _lastProgress;
    private int? _lastClaimer;
    private int _knownAdvanceTick = int.MinValue;
    private int _knownAdvanceOwner = Unknown;

    private const int Unknown = -1;
    private const int Mine = 0;
    private const int Theirs = 1;

    public Ratchet(Doctrine doctrine)
    {
        _doctrine = doctrine;
    }

    /// <summary>Whether this contract locks a completed advance at all.</summary>
    public bool Sticky => _doctrine.HoldTicks is > 0;

    /// <summary>Whether returns and activations land at the front rather than at home.</summary>
    public bool RallyForward => _doctrine.RallyForward;

    /// <summary>Whether surplus objective weight scales capture pressure.</summary>
    public bool WeightScales => _doctrine.WeightScales;

    /// <summary>
    /// Whether only an enemy standing alone on the point erodes a claim, so that
    /// empty and contested ticks preserve it. Under that clock an unopposed enemy
    /// body is the only thing that costs ground, which makes answering one the
    /// single errand that outranks everything a gun could be doing.
    /// </summary>
    public bool SoleDecayOnly => _doctrine.SoleDecayOnly;

    /// <summary>
    /// Ticks left on a hold this team owns, or zero when no such hold is known
    /// to be live. While it runs, an opposing capture on the active objective
    /// is spent: their claim resets and the front does not move. Their claim
    /// still counts toward the tick-cap territorial formula, so this buys
    /// freedom from the <em>advance</em>, not freedom from the scoreboard.
    /// </summary>
    public int OwnHoldRemaining { get; private set; }

    /// <summary>
    /// Ticks left on a hold the opposition owns. While it runs, a capture this
    /// team completes is spent — the fifteen ticks of presence buy nothing —
    /// so the push is worth starting only late enough to land outside it.
    /// </summary>
    public int EnemyHoldRemaining { get; private set; }

    /// <summary>
    /// Whether a push started now would complete inside an opposing hold and
    /// therefore be thrown away. The comparison uses the declared capture
    /// arithmetic at this tick, not a constant; it adds the walk still owed and
    /// credits progress this team has already banked.
    /// </summary>
    public bool PushWouldBeSpent(int tick, int ticksToReach, int ownProgress) =>
        EnemyHoldRemaining > 0
        && EnemyHoldRemaining > ticksToReach + TicksToComplete(ownProgress, tick);

    /// <summary>
    /// Whether an opposing claim standing at <paramref name="theirProgress"/>
    /// would complete inside a hold this team owns, and so be spent — their
    /// claim resets, the front does not move, and the presence they spent
    /// buying it is simply gone.
    /// <para>
    /// This is the distinction that makes a hold usable rather than dangerous.
    /// A hold protects the <em>advance</em>; it does not delete progress. A
    /// claim still standing when the window drops converts the instant it does,
    /// so treating every opposing claim as harmless until the last tick turns a
    /// protected window into a handover: the ground comes back the moment the
    /// hold ends, with the enemy's capture already paid for. Measured on this
    /// lineage's own replays, that single confusion cost position 0 twice in
    /// one match.
    /// </para>
    /// </summary>
    public bool EnemyPushWouldBeSpent(int tick, int theirProgress) =>
        OwnHoldRemaining > 0
        && OwnHoldRemaining > TicksToComplete(theirProgress, tick);

    /// <summary>
    /// Ticks of sole presence still owed on a claim already standing at
    /// <paramref name="progress"/>, under the gain phase declared for this tick
    /// rather than a remembered constant.
    /// </summary>
    private int TicksToComplete(int progress, int tick)
    {
        int gain = Gain(tick);
        int owed = Math.Max(0, Threshold() - Math.Max(0, progress));
        return (owed + gain - 1) / gain;
    }

    /// <summary>
    /// Whether this tick's hold came from the observation rather than from the
    /// reconstruction below. It should always be true on a contract that
    /// declares a hold; it is recorded so a replay can prove the bot stopped
    /// guessing.
    /// </summary>
    public bool HoldWasPublished { get; private set; }

    public void Observe(
        GenericActorContext context,
        GenericActorContext.ModeObservationState.Frontline? mode)
    {
        OwnHoldRemaining = 0;
        EnemyHoldRemaining = 0;
        HoldWasPublished = false;
        if (mode is null || _doctrine.HoldTicks is not int hold || hold <= 0)
        {
            Remember(mode);
            return;
        }

        // The hold is PUBLISHED. Owner and expiry travel together on the mode
        // observation, they read exactly like ControlResumesAtTick, and a
        // published hold is a binding one — so ask, and stop reconstructing.
        // The four-signal derivation below is retained only for a contract that
        // declares ratchetHoldTicks but publishes no live clock; a life born
        // inside a hold cannot derive its owner at all, which is precisely why
        // asking replaced guessing.
        if (ArenaBasics.LiveHold(context) is { } live)
        {
            HoldWasPublished = true;
            if (live.Mine)
                OwnHoldRemaining = Math.Max(0, live.RemainingTicks);
            else
                EnemyHoldRemaining = Math.Max(0, live.RemainingTicks);
            Remember(mode);
            return;
        }
        if (mode.HoldOwnerTeamId is null && mode.HoldEndsAtTick is null)
        {
            // A null pair is a real answer: no hold binds on this tick. The
            // reconstruction must not invent one on top of it.
            HoldWasPublished = true;
            Remember(mode);
            return;
        }

        int tick = context.Tick;
        int index = mode.ActivePositionIndex;

        // (1) The advance this life actually watched happen. The sign of the
        // index change against this team's declared delta names the owner
        // without any compass or spawn assumption.
        if (_lastIndex >= 0 && index != _lastIndex)
        {
            int moved = index - _lastIndex;
            _knownAdvanceOwner =
                Math.Sign(moved) == Math.Sign(_doctrine.IndexDelta)
                    ? Mine
                    : Theirs;
            _knownAdvanceTick = AdvanceTick(mode, tick);
        }
        else if (_lastIndex >= 0
            && index == _lastIndex
            && _lastClaimer is int claimer
            && claimer != _doctrine.TeamId
            && mode.CaptureProgress == 0
            && _lastProgress > 0
            && _lastProgress + Gain(tick) >= Threshold())
        {
            // (3) Their claim was one tick from completion, the claim is gone,
            // and the front did not move. Only a spent capture looks like that,
            // and only a live hold of ours can spend one.
            _knownAdvanceOwner = Mine;
            _knownAdvanceTick = Math.Max(_knownAdvanceTick, tick - hold + 1);
        }

        int advanceTick = _knownAdvanceTick;
        int owner = _knownAdvanceOwner;

        // (2) A live redeploy pause dates the advance exactly, which repairs
        // the tick for a life created after it. Only the owner is missing.
        if (mode.ControlResumesAtTick > tick)
        {
            int derived = mode.ControlResumesAtTick - RedeployPause();
            if (derived > advanceTick)
            {
                advanceTick = derived;
                owner = Unknown;
            }
        }

        if (owner == Unknown)
            owner = InferOwnerFromDisplacement(index);

        // A life born long after the advance cannot date it, and inventing a
        // clock is worse than not having one: the doctrine simply plays the
        // baseline until it watches the next position change for itself.
        int remaining = advanceTick == int.MinValue
            ? 0
            : advanceTick + hold - tick;
        if (remaining > 0 && owner != Unknown)
        {
            if (owner == Mine)
                OwnHoldRemaining = remaining;
            else
                EnemyHoldRemaining = remaining;
        }

        Remember(mode);
    }

    private void Remember(GenericActorContext.ModeObservationState.Frontline? mode)
    {
        if (mode is null)
            return;
        _lastIndex = mode.ActivePositionIndex;
        _lastProgress = mode.CaptureProgress;
        _lastClaimer = mode.ClaimingTeamId;
    }

    private int Threshold() => _doctrine.Capture?.Threshold ?? 15;

    private int Gain(int tick) =>
        _doctrine.Capture is { } capture
            ? Math.Max(1, capture.GainPhaseAtTick(tick).GainPerSoleTeamTick)
            : 1;

    private int RedeployPause() => _doctrine.Capture?.RedeployPauseTicks ?? 0;

    private int AdvanceTick(
        GenericActorContext.ModeObservationState.Frontline mode,
        int tick) =>
        mode.ControlResumesAtTick > tick
            ? mode.ControlResumesAtTick - RedeployPause()
            : tick;

    /// <summary>
    /// The side that made the last advance, read off the front's displacement
    /// from the centre of the chain. A front standing in this team's half was
    /// last pushed there by the opposition and vice versa; a level front is
    /// genuinely ambiguous and stays unknown.
    /// </summary>
    private int InferOwnerFromDisplacement(int index)
    {
        int count = _doctrine.ObjectiveTiles.Length;
        if (count <= 1)
            return Unknown;
        int signed = (index - (count / 2)) * Math.Sign(_doctrine.IndexDelta);
        return signed switch
        {
            > 0 => Mine,
            < 0 => Theirs,
            _ => Unknown,
        };
    }
}
