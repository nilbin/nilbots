using BotArena.Sdk;

/// <summary>
/// What the active objective is actually worth to this body, this tick, under
/// <em>this</em> contract's declared capture policy.
///
/// Revisions 1 and 2 priced ground from three assumptions the rule card taught:
/// one body captures, any enemy body nulls it, and a completed capture always
/// moves the front. A contract may declare all three differently — surplus
/// objective weight can scale capture pressure, and a completed advance can be
/// protected by a hold that makes the loser's next capture reset its own claim
/// and move nothing. None of that changes the observation schema, so a bot that
/// never reads the policy simply believes the wrong thing about what its ticks
/// are buying.
///
/// This type is the reading. Every field below comes from
/// <see cref="ArenaBasics.Capture"/>, <see cref="ArenaBasics.ObjectivePresence"/>
/// and the frozen Frontline observation; nothing is named, assumed, or tuned.
/// On a contract that declares no hold and binary control it reproduces exactly
/// what revision 2 believed, which is the point: the doctrine did not change,
/// its inputs stopped being guesses.
/// </summary>
internal sealed class Advance
{
    private Advance(
        ArenaBasics.CaptureRules? rules,
        int ownWeight,
        int enemyWeight,
        bool selfPresent,
        int gainIfPresent,
        bool controlPaused,
        bool claimIsOurs,
        int progress,
        int? holdEndsAtTick,
        bool holdIsOurs,
        int tick,
        int enemyStepsToObjective)
    {
        Rules = rules;
        OwnWeight = ownWeight;
        EnemyWeight = enemyWeight;
        SelfPresent = selfPresent;
        GainIfPresent = gainIfPresent;
        ControlPaused = controlPaused;
        ClaimIsOurs = claimIsOurs;
        Progress = progress;
        HoldEndsAtTick = holdEndsAtTick;
        HoldIsOurs = holdIsOurs;
        HoldRemaining = holdEndsAtTick is int end ? Math.Max(0, end - tick) : 0;
        EnemyStepsToObjective = enemyStepsToObjective;
    }

    /// <summary>The declared capture policy, or null when the mode has none.</summary>
    public ArenaBasics.CaptureRules? Rules { get; }
    /// <summary>Allied objective weight standing on the active objective.</summary>
    public int OwnWeight { get; }
    /// <summary>Observed enemy objective weight there — a lower bound.</summary>
    public int EnemyWeight { get; }
    /// <summary>True when this life is one of the bodies counted.</summary>
    public bool SelfPresent { get; }
    /// <summary>
    /// Progress per tick this team would earn with this body on the objective,
    /// under the declared control policy. Zero means the tick buys no ground.
    /// </summary>
    public int GainIfPresent { get; }
    /// <summary>True while the post-advance pause suppresses control.</summary>
    public bool ControlPaused { get; }
    /// <summary>True when the current claim belongs to this team.</summary>
    public bool ClaimIsOurs { get; }
    /// <summary>Current capture progress on the active objective.</summary>
    public int Progress { get; }
    /// <summary>Published tick a live advance hold expires, or null when none.</summary>
    public int? HoldEndsAtTick { get; }
    /// <summary>True when the live hold protects this team's own advance.</summary>
    public bool HoldIsOurs { get; }
    /// <summary>Ticks left on the live hold; zero when none is live.</summary>
    public int HoldRemaining { get; }
    /// <summary>
    /// Steps the nearest observed enemy body of positive objective weight
    /// needs to stand on the active objective, or <see cref="int.MaxValue"/>
    /// when none is visible. A lower bound on how long the ground can be left
    /// unattended — and, like every enemy count here, blind to bodies this
    /// team does not currently see.
    /// </summary>
    public int EnemyStepsToObjective { get; }

    /// <summary>
    /// True when a capture completed now would be spent: the opposing team's
    /// advance is still held, so the claim resets and the front does not move.
    /// Ticks poured into progress during this window buy a bank, not ground.
    /// </summary>
    public bool CaptureIsSpent => HoldRemaining > 0 && !HoldIsOurs;

    /// <summary>
    /// True when a second allied body on the objective is worth more than the
    /// first body's presence — the contract's answer to "does stacking help?",
    /// which decides whether a spare gun belongs on the ground or beside it.
    /// </summary>
    public bool StackHelps => Rules?.SurplusWeightScalesGain ?? false;

    /// <summary>
    /// True when enemy weight cancels this body's presence outright: standing
    /// there earns nothing, so removing the body contesting it <em>is</em> the
    /// capture. Under a weight-scaling policy a contested objective this team
    /// outnumbers still pays, and this is false — which is the whole difference
    /// between "shoot it off the tile" and "hold the tile and keep counting".
    /// </summary>
    public bool Nulled => EnemyWeight > 0 && GainIfPresent <= 0;

    /// <summary>
    /// True when this ground cannot be converted into an advance right now,
    /// whether because control is paused or because the capture it would
    /// complete is spent inside the opposing hold.
    /// </summary>
    public bool NoGroundToWin => ControlPaused || CaptureIsSpent;

    /// <summary>
    /// True when this team's claim should stop growing until the opposing hold
    /// expires, because the completion it is heading for would be discarded
    /// and waiting reaches a real advance sooner than grinding does.
    /// <para>
    /// Both plans are counted in ticks-from-now and compared:
    /// <em>grind</em> completes on the declared cadence and only the first
    /// completion at or after the hold expires actually moves the front;
    /// <em>wait</em> gives up the ground, lets the declared decay clock eat
    /// what it eats, and completes as soon as the hold is gone. Which one wins
    /// is arithmetic on contract values, and it turns over: at the far end of
    /// a hold a completion that just misses costs a whole extra cycle, while
    /// early in one, grinding lands on the expiry for free.
    /// </para>
    /// <para>
    /// Guarded on the observed enemy: ground left unattended for longer than a
    /// body needs to walk onto it is not banked, it is given away, and an
    /// opposing sole presence erodes a claim far faster than an empty
    /// objective decays.
    /// </para>
    /// </summary>
    public bool WithholdCompletion { get; private set; }

    /// <summary>
    /// Reads the front from the contract and this tick's frozen observation.
    /// <paramref name="ledger"/> is only consulted where a contract declares a
    /// hold and publishes no live one; the observation is the authority.
    /// </summary>
    public static Advance Read(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        FrontLedger ledger)
    {
        ArenaBasics.CaptureRules? rules = ArenaBasics.Capture(doctrine.Contract);
        (int ownWeight, int enemyWeight, bool selfPresent) =
            ArenaBasics.ObjectivePresence(doctrine.Contract, context);

        int selfWeight = doctrine.FormFor(field.FormId)?.ObjectiveWeight ?? 0;
        int ownWithSelf = selfPresent ? ownWeight : ownWeight + selfWeight;
        int gain = rules is null || field.ControlPaused
            ? 0
            : rules.SurplusWeightScalesGain
                ? Math.Max(0, ownWithSelf - enemyWeight) * rules.GainPerSoleTeamTick
                : ownWithSelf > 0 && enemyWeight <= 0
                    ? rules.GainPerSoleTeamTick
                    : 0;

        bool claimIsOurs = field.ClaimingTeam == doctrine.TeamId;
        (int? holdEnd, bool holdIsOurs) = ReadHold(
            doctrine,
            field,
            context,
            ledger,
            rules);

        int enemySteps = int.MaxValue;
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            if ((doctrine.FormFor(enemy.FormId)?.ObjectiveWeight ?? 1) <= 0)
                continue;
            foreach (Position tile in field.Objective)
            {
                int steps = tile.ChebyshevDistance(enemy.Position);
                if (steps < enemySteps)
                    enemySteps = steps;
            }
        }

        var advance = new Advance(
            rules,
            ownWeight,
            enemyWeight,
            selfPresent,
            gain,
            field.ControlPaused,
            claimIsOurs,
            field.CaptureProgress,
            holdEnd,
            holdIsOurs,
            context.Tick,
            enemySteps);

        advance.WithholdCompletion = ShouldWithhold(advance, rules);
        return advance;
    }

    private static bool ShouldWithhold(
        Advance advance,
        ArenaBasics.CaptureRules? rules)
    {
        if (rules is null
            || !advance.CaptureIsSpent
            || !advance.ClaimIsOurs
            || advance.GainIfPresent <= 0
            || advance.EnemyStepsToObjective <= advance.HoldRemaining)
        {
            return false;
        }

        int gain = advance.GainIfPresent;
        int cycle = Math.Max(1, Ticks(rules.Threshold, gain));
        int grind = Math.Max(1, Ticks(rules.Threshold - advance.Progress, gain));
        if (grind < advance.HoldRemaining)
        {
            // That completion lands inside the hold and is discarded; the next
            // one that counts is a whole cadence away, and possibly several.
            int missing = advance.HoldRemaining - grind;
            grind += cycle * ((missing + cycle - 1) / cycle);
        }

        int decayed = rules.OnlyEnemySolePresenceDecays || rules.DecayAmount <= 0
            ? advance.Progress
            : Math.Max(
                0,
                advance.Progress
                - advance.HoldRemaining
                    / Math.Max(1, rules.DecayIntervalTicks)
                    * rules.DecayAmount);
        int wait = advance.HoldRemaining
            + Math.Max(1, Ticks(rules.Threshold - decayed, gain));
        return wait < grind;
    }

    private static int Ticks(int progressNeeded, int gain) =>
        progressNeeded <= 0 ? 0 : (progressNeeded + gain - 1) / gain;

    /// <summary>
    /// Whose advance the ratchet is protecting and when the protection lifts —
    /// <em>asked</em>, not reconstructed.
    ///
    /// Revision 3 derived this, and the derivation was expensive and partly
    /// wrong. The start came out of the redeploy clock run backwards; the OWNER
    /// had no derivation at all, only a guess from the signed displacement of
    /// the front, which is wrong the first time an opponent regresses from a
    /// lead and is unavailable to a life born inside a hold, because private
    /// memory is life-scoped. The observation now publishes both fields, so this
    /// revision reads them and the guess is gone.
    ///
    /// <para>The inference survives as a fallback for exactly one case that the
    /// published fields cannot be told apart from by value alone: a ruleset
    /// whose capture definition declares a hold, sitting inside the redeploy
    /// pause that only an advance can start, reporting no owner. A hold longer
    /// than the pause must be running there, so a null is an absent field rather
    /// than an absent hold. Everywhere else null means what it says.</para>
    /// </summary>
    private static (int? EndsAtTick, bool IsOurs) ReadHold(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        FrontLedger ledger,
        ArenaBasics.CaptureRules? rules)
    {
        if (ArenaBasics.LiveHold(context) is ArenaBasics.Hold hold)
            return (hold.EndsAtTick, hold.Mine);
        if (rules?.HoldTicks is not int declared
            || declared <= rules.RedeployPauseTicks
            || !field.ControlPaused)
        {
            return (null, false);
        }
        return Infer(doctrine, field, ledger, rules);
    }

    /// <summary>
    /// Revision 3's derivation, retained only for a contract that declares a
    /// hold and does not publish the live one.
    /// </summary>
    private static (int? EndsAtTick, bool IsOurs) Infer(
        Doctrine doctrine,
        Field field,
        FrontLedger ledger,
        ArenaBasics.CaptureRules? rules)
    {
        if (rules?.HoldTicks is not int holdTicks
            || holdTicks <= 0
            || field.Frontline is null)
        {
            return (null, false);
        }

        int resumes = field.Frontline.ControlResumesAtTick;
        if (resumes <= 0)
            return (null, false);

        int advancedAt = Math.Max(0, resumes - 1 - rules.RedeployPauseTicks);
        int endsAt = advancedAt + holdTicks;

        bool? ours = ledger.LastAdvanceWasOurs ?? Displaced(doctrine, field);
        return ours is bool owner ? (endsAt, owner) : (null, false);
    }

    /// <summary>
    /// Which side the front currently sits on relative to the chain's centre,
    /// as the tick-cap ranking formula measures it. Null at the centre, where
    /// the displacement says nothing about who moved it there.
    /// </summary>
    private static bool? Displaced(Doctrine doctrine, Field field)
    {
        int positions = doctrine.ObjectivePositions.Length;
        if (positions == 0 || field.ActiveIndex < 0 || doctrine.AdvanceDelta == 0)
            return null;
        int offset = field.ActiveIndex - positions / 2;
        if (offset == 0)
            return null;
        return Math.Sign(offset) == Math.Sign(doctrine.AdvanceDelta);
    }
}

/// <summary>
/// Which team last moved the front, watched from the public objective index.
///
/// This used to be load-bearing: hold ownership had no other source. It is now
/// a fallback for a contract that declares a hold and publishes no live one,
/// because the observation carries <c>holdOwnerTeamId</c> and
/// <c>holdEndsAtTick</c> directly. Life-scoped like every other private memory
/// here — which is precisely why it was never good enough: a life born inside a
/// hold has watched nothing at all.
/// </summary>
internal sealed class FrontLedger
{
    private int _lastIndex = -1;

    /// <summary>
    /// True when the last observed advance was this team's, false when it was
    /// the opposition's, null when this life has not watched one.
    /// </summary>
    public bool? LastAdvanceWasOurs { get; private set; }

    /// <summary>Folds this tick's objective index into the ledger.</summary>
    public void Observe(Doctrine doctrine, Field field)
    {
        int index = field.ActiveIndex;
        if (index < 0)
            return;
        if (_lastIndex >= 0 && index != _lastIndex && doctrine.AdvanceDelta != 0)
        {
            LastAdvanceWasOurs =
                Math.Sign(index - _lastIndex) == Math.Sign(doctrine.AdvanceDelta);
        }
        _lastIndex = index;
    }
}
