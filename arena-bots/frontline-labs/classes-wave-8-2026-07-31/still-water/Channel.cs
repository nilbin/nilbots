using BotArena.Sdk;

/// <summary>
/// THE CHANNEL PASS. Everything wave 8 changed about taking and denying ground
/// lives here or is gated by a switch declared here, so every rule can be
/// removed from the working whole one at a time and measured.
///
/// <para>The capture arm rewrote the front for both teams. Four declared facts
/// moved, and the whole of wave 7's positional doctrine was priced against the
/// old ones:</para>
///
/// <list type="bullet">
/// <item><description><b>Standing still is what captures.</b> Claim weight
/// counts only bodies whose tile did not change this tick; denial weight counts
/// all of them. So attacking the point costs stillness and defending it does
/// not — a body that keeps walking still subtracts. This is readable as
/// <c>capture.controlPolicy</c> plus a declared
/// <c>stationaryGainMultiplierCap</c>.</description></item>
/// <item><description><b>Damage on the point reverts the run.</b>
/// <c>capture.claimInterrupt</c> declares the mechanism, the revert per damage
/// point, the scope, and the granularity. For a chassis that fires several
/// simultaneous bolts, that turns the gun into a TERRITORIAL verb: it is the
/// only way this doctrine can move the objective number without standing on
/// the tile.</description></item>
/// <item><description><b>Erosion is a channel at a declared
/// multiple.</b> <c>opposingErosionMultiplier</c> says how much faster a
/// standing enemy claim comes off than a fresh one goes on.</description>
/// </item>
/// <item><description><b>The threshold moved.</b> It is read, as it always
/// was — but everything the doctrine budgeted in ticks (when the ledger closes,
/// what a push costs, how long a body can stand off) is denominated in
/// it.</description></item>
/// </list>
///
/// <para>Every one of those fields is inert-omitted on a ruleset without the
/// arm, exactly like <c>ratchetHoldTicks</c>, so the whole file branches on
/// presence and disappears when the mechanic does not exist.</para>
/// </summary>
internal static class ChannelRules
{
    /// <summary>
    /// RULE C1 — STILLNESS IS A DECISION. On a channel ruleset the tile a body
    /// ends the tick on decides whether it is claiming or merely denying. A body
    /// whose stillness would put its team's claim weight strictly above the
    /// enemy's denial weight is paid for staying, and one whose stillness buys
    /// nothing is not. Rotating, shooting and entering a stance never break it —
    /// the test is the TILE, not the verb — so this is a bonus on standing
    /// ground, not a refusal to act. Off: the feet score exactly as wave 7
    /// scored them and the bot shuffles through its own captures.
    /// </summary>
    public static readonly bool StillClaim = true;

    /// <summary>
    /// RULE C2 — A BOLT ONTO THE POINT IS TERRITORY. Hostile damage to a body of
    /// the controlling team standing on the active objective reverts that team's
    /// work on the current run, at the declared rate. So a bolt aimed there is
    /// worth its damage in objective progress on top of its damage in health,
    /// and a fan covering several controlling bodies is worth the sum. This is
    /// the interrupt, and it is the reason a standoff doctrine has a game at all
    /// on this arm. Off: the shot ladder prices health only, as wave 7 did.
    /// </summary>
    public static readonly bool Interrupt = true;

    /// <summary>
    /// RULE C3 — DENIAL IS PRESENCE, NOT STILLNESS. Denial weight counts every
    /// body, moving or not, so one body that merely stands in the region — or
    /// walks through it dodging — nulls one stationary attacker. That makes
    /// contesting far cheaper than wave 7 priced it, and it is why the posture
    /// ladder now steps onto a point it used to cover from a band. Off: the
    /// objective is worth what wave 7's binary-control reading said.
    /// </summary>
    public static readonly bool DenyPresence = true;

    /// <summary>
    /// RULE C4 — SCREEN THE CHANNELER. A body standing on the firing line to a
    /// channelling teammate, OFF the objective region, eats the bolt that would
    /// have reverted the run — the collision model already does this, and
    /// allied bolts pass through, so the screen costs nothing in return fire.
    /// Off: a second body reinforces the point instead, where its stillness is
    /// capped and its damage is not absorbed.
    /// </summary>
    public static readonly bool Screen = true;

    /// <summary>
    /// RULE C5 — NEVER FEED A MIRROR. A guarded form returns the bolt that
    /// arrives inside its facing quadrant, from its own tile, along the exactly
    /// reversed heading, owned by its team and carrying the damage class of the
    /// bolt it returned. Wave 7 priced that as a score penalty on the mobile
    /// gun, and a penalty loses to a large enough prediction: measured, this
    /// lineage shot itself to death against an aegis shell in a straight
    /// corridor, three bolts, no reply. A returned bolt is not a worse shot, it
    /// is a self-inflicted hit — so a contact that deflects is refused outright
    /// unless it is the one that spends the guard's declared budget. Off: wave
    /// 7's graded penalty.
    /// </summary>
    public static readonly bool MirrorRefusal = true;

    /// <summary>
    /// RULE C0 — THE TIE-BREAK IS A TEAM DECISION. The scaffold's direction
    /// order used to draw from the PER-LIFE stream, so two of my bodies routing
    /// around the same obstacle broke the tie differently and the convoy
    /// conventions layered on top were agreeing about a plan the router had
    /// already split. The scaffold now draws from <c>TeamRandom</c>, which every
    /// life on the team reproduces at the same point of the same tick — a
    /// respawn included, on its first tick. Off: the per-life stream, which is
    /// wave 7's behaviour exactly.
    /// </summary>
    public static readonly bool TeamTieBreak = false;

    /// <summary>
    /// RULE C6 — THE ERODE CLOCK IS THE DECLARED ONE. Every "can the clock
    /// still pay for this?" question this doctrine asks budgets erosion at the
    /// plain gain rate, because that is what it cost on every previous arm.
    /// A channel ruleset declares <c>opposingErosionMultiplier</c>, and taking
    /// a standing claim back is that many times faster — so the budget was
    /// wrong by exactly that factor, which made the ledger read as closing
    /// while there was still time to win it back. Off: the plain rate, which is
    /// also what the arithmetic reverts to when the field is absent.
    /// </summary>
    public static readonly bool ErosionClock = true;

    /// <summary>
    /// Score units one point of objective progress per tick is worth against the
    /// positional ladder. The ladder's own units are roughly "one step of route",
    /// and one progress point is one eighth of a position on this arm's declared
    /// threshold, so a coefficient near one is the honest exchange rate. This is
    /// the pass's single tuned coefficient on the capture side.
    /// </summary>
    public const double ProgressWeight = 1.25;

    /// <summary>
    /// Score units one point of reverted enemy progress is worth in the shot
    /// ladder, whose units are prediction weight rather than route steps. A
    /// revert is strictly better than the damage that carries it — the damage
    /// happens anyway — so the coefficient is additive on top of the ordinary
    /// pressure valuation.
    /// </summary>
    public const double InterruptWeight = 0.9;
}

/// <summary>
/// Life-scoped memory the channel arithmetic needs and the observation does not
/// carry: which allied bodies held their tile last tick, and where the current
/// controller's RUN began. Both are ordinary private history, so a fresh body
/// starts blank and degrades to the conservative reading rather than guessing.
/// </summary>
internal sealed class ChannelMemory
{
    private readonly Dictionary<ActorIdentity, Position> _allies = [];
    private readonly HashSet<ActorIdentity> _heldTile = [];
    private int _lastTick = int.MinValue;

    /// <summary>Team whose run is currently being tracked, or null.</summary>
    public int? RunTeamId { get; private set; }

    /// <summary>Progress the current controller found when its run began.</summary>
    public int RunBaseline { get; private set; }

    public void Observe(
        GenericActorContext context,
        GenericActorContext.ModeObservationState.Frontline? mode)
    {
        bool contiguous = _lastTick == context.Tick - 1;
        _heldTile.Clear();
        foreach (var ally in context.Allies)
        {
            if (contiguous
                && _allies.TryGetValue(ally.ActorId, out Position previous)
                && previous == ally.Position)
            {
                _heldTile.Add(ally.ActorId);
            }
            else if (!contiguous || !_allies.ContainsKey(ally.ActorId))
            {
                // A body with no history — including one that spawned this
                // tick — counts as stationary, which is exactly what the rule
                // says about a life with no previous position.
                _heldTile.Add(ally.ActorId);
            }
        }
        _allies.Clear();
        foreach (var ally in context.Allies)
            _allies[ally.ActorId] = ally.Position;
        _lastTick = context.Tick;

        // The run. A run is one team's continuous stretch of control; it ends
        // the moment nobody controls and on any completed capture. The claiming
        // team is published, so the transitions are observable — what is not
        // published is where the number stood when that stretch began, which is
        // the only thing a revert is measured against.
        int? claimer = mode?.ClaimingTeamId;
        int progress = mode?.CaptureProgress ?? 0;
        if (claimer is null)
        {
            RunTeamId = null;
            RunBaseline = progress;
            return;
        }
        if (RunTeamId != claimer)
        {
            RunTeamId = claimer;
            // Conservative: the run may have started before this life could
            // see it, in which case the true baseline is at most this.
            RunBaseline = progress;
        }
        else if (progress < RunBaseline)
        {
            RunBaseline = progress;
        }
    }

    /// <summary>Whether this ally's tile was unchanged on the observed tick.</summary>
    public bool HeldTile(ActorIdentity actorId) => _heldTile.Contains(actorId);

    /// <summary>
    /// Progress the current controller has put on this run, which is the most a
    /// single interrupt can take off. Zero when nobody is running.
    /// </summary>
    public int RunWork(GenericActorContext.ModeObservationState.Frontline? mode)
    {
        if (RunTeamId is null || mode is null)
            return 0;
        return Math.Max(0, mode.CaptureProgress - RunBaseline);
    }
}

/// <summary>
/// The channel arithmetic for one tick: what this contract declares, who holds
/// the point, and what a body ending the tick on a given tile would be worth to
/// the objective number. Constructed fresh every tick like the threat field.
/// </summary>
internal sealed class ChannelState
{
    private readonly HashSet<Position> _objective;
    private readonly GenericActorContext _context;
    private readonly Doctrine _doctrine;
    private readonly int _selfWeight;

    public ChannelState(
        Doctrine doctrine,
        GenericActorContext context,
        ChannelMemory memory,
        QuarryTracker tracker,
        HashSet<Position> objective,
        GenericActorContext.ModeObservationState.Frontline? mode)
    {
        _doctrine = doctrine;
        _context = context;
        _objective = objective;
        _selfWeight = doctrine.Form(context.Self.FormId)?.ObjectiveWeight ?? 0;

        var capture = doctrine.Capture;
        // Presence, not inference. Every one of these is inert-omitted on a
        // ruleset that does not channel, so their presence IS the arm.
        Declared = capture is { StationaryGainMultiplierCap: > 0 };
        GainCap = capture?.StationaryGainMultiplierCap ?? 0;
        ErosionMultiplier = Math.Max(1, capture?.OpposingErosionMultiplier ?? 1);
        Gain = Math.Max(1, capture?.GainPhaseAtTick(context.Tick).GainPerSoleTeamTick ?? 1);
        Threshold = capture?.Threshold ?? 15;
        RevertPerDamage = capture?.ClaimInterrupt?.RevertPerDamagePoint ?? 0;
        WholeRunRevert = capture?.ClaimInterrupt is { } interrupt
            && interrupt.Granularity.Contains("run", StringComparison.Ordinal);

        ClaimingTeamId = mode?.ClaimingTeamId;
        Progress = mode?.CaptureProgress ?? 0;
        RunWork = memory.RunWork(mode);

        SelfOnPoint = objective.Contains(context.Self.Position);
        foreach (var ally in context.Allies)
        {
            if (!objective.Contains(ally.Position))
                continue;
            int weight = doctrine.Form(ally.FormId)?.ObjectiveWeight ?? 0;
            AlliedTotal += weight;
            if (memory.HeldTile(ally.ActorId))
                AlliedStationary += weight;
        }
        foreach (var enemy in context.Enemies)
        {
            if (!objective.Contains(enemy.Position))
                continue;
            int weight = doctrine.Form(enemy.FormId)?.ObjectiveWeight ?? 0;
            EnemyTotal += weight;
            // StillTicks is only meaningful on a body seen on consecutive
            // ticks; an unseen or newly seen body is read as stationary, the
            // pessimistic side for a defender and the honest side for an
            // attacker who has to beat it.
            QuarryTracker.Trace? trace = tracker.Get(enemy.ActorId);
            if (trace is null || trace.StillTicks > 0)
                EnemyStationary += weight;
        }
    }

    /// <summary>Whether this ruleset channels captures at all.</summary>
    public bool Declared { get; }

    public int GainCap { get; }

    public int ErosionMultiplier { get; }

    public int Gain { get; }

    public int Threshold { get; }

    /// <summary>Progress reverted per point of health removed, or zero.</summary>
    public int RevertPerDamage { get; }

    /// <summary>Whether one hit reverts the controller's whole run.</summary>
    public bool WholeRunRevert { get; }

    public int? ClaimingTeamId { get; }

    public int Progress { get; }

    /// <summary>Progress on the current run: the most one interrupt can take.</summary>
    public int RunWork { get; }

    public bool SelfOnPoint { get; }

    public int AlliedTotal { get; private set; }

    public int AlliedStationary { get; private set; }

    public int EnemyTotal { get; private set; }

    public int EnemyStationary { get; private set; }

    /// <summary>
    /// Whether the contract says a marginal body on the point stops paying.
    ///
    /// <para>This is the fact that decides whether ANY body can be spent away
    /// from the front, and it is declared rather than felt. A channel ruleset
    /// publishes <c>stationaryGainMultiplierCap</c>: surplus past that ceiling
    /// buys no capture speed at all, so the body past it is free and the
    /// deposit lane is worth its walk. A weight-scaled policy publishes no cap
    /// — every body's weight scales the rate — so no body is ever spare and a
    /// detour of one tile is a real cut in the rate. Measured on this
    /// lineage's own replays the difference is not marginal: against the
    /// fabricator cohort, spending bodies on scrap is worth about +5 territory
    /// where the cap exists and about −26 where it does not, on the same
    /// artifact and the same seeds.</para>
    /// </summary>
    public bool MarginalBodyIsFree => Declared && GainCap > 0;

    /// <summary>An enemy claim is standing on the live point.</summary>
    public bool EnemyClaimStands =>
        ClaimingTeamId is int claimer
        && claimer != _doctrine.TeamId
        && Progress > 0;

    /// <summary>The enemy is the controlling team RIGHT NOW, so its run is live.</summary>
    public bool EnemyControls =>
        Declared
            ? EnemyStationary > AlliedTotal + (SelfOnPoint ? _selfWeight : 0)
            : EnemyTotal > 0
                && AlliedTotal + (SelfOnPoint ? _selfWeight : 0) == 0;

    /// <summary>
    /// Progress THIS team would put on (or take off) the objective on a tick
    /// that ends with this body on <paramref name="tile"/>. Zero unless my
    /// stationary claim weight strictly exceeds the enemy's total denial
    /// weight, which is the whole of what "standing still is what captures"
    /// means. The units are progress points per tick, which is what makes the
    /// coefficient that scales it into the positional ladder a single readable
    /// exchange rate.
    /// </summary>
    public double Build(Position tile)
    {
        if (!Declared)
            return 0;
        bool onPoint = _objective.Contains(tile);
        bool moved = tile != _context.Self.Position;
        int mineStill = AlliedStationary + (onPoint && !moved ? _selfWeight : 0);
        if (mineStill <= EnemyTotal)
            return 0;
        double mine = Math.Min(GainCap, mineStill - EnemyTotal) * Gain;
        // Eroding a standing enemy claim is the same channel at the declared
        // multiple, and it is where this arm pays a counter-attack.
        return EnemyClaimStands ? mine * ErosionMultiplier : mine;
    }

    /// <summary>
    /// Progress the OPPOSING team would put on (or take off) the objective on a
    /// tick that ends with this body on <paramref name="tile"/>. Their claim
    /// weight is their stationary bodies; the weight that stops it is EVERY
    /// body of mine in the region, moving or not — which is why a defender
    /// never has to stand still and why one body of mine nulls one of theirs.
    /// </summary>
    public double Deny(Position tile)
    {
        if (!Declared)
            return 0;
        bool onPoint = _objective.Contains(tile);
        int mineTotal = AlliedTotal + (onPoint ? _selfWeight : 0);
        if (EnemyStationary <= mineTotal)
            return 0;
        double theirs = Math.Min(GainCap, EnemyStationary - mineTotal) * Gain;
        bool mineStands = ClaimingTeamId == _doctrine.TeamId && Progress > 0;
        return mineStands ? theirs * ErosionMultiplier : theirs;
    }

    /// <summary>
    /// Whether standing on <paramref name="tile"/> puts this body on the firing
    /// line to a channelling teammate, off the objective region — the escort
    /// pattern. The collision model already does the work: a hostile bolt stops
    /// on the first enemy actor it meets, and allied bolts pass through, so the
    /// screen absorbs the interrupt without blocking the return fire. Nothing
    /// new was added for this; the arm gave an existing behaviour a purpose.
    /// </summary>
    public double ScreenValue(
        Field field,
        Position tile,
        List<EnemyForecast> forecasts,
        int enemyReach)
    {
        if (!Declared || _objective.Contains(tile) || forecasts.Count == 0)
            return 0;
        // Only worth doing while the team is actually taking ground: a screen
        // for a run that is not running is a body standing in the open.
        if (AlliedStationary <= 0 || AlliedStationary <= EnemyTotal)
            return 0;

        foreach (var ally in _context.Allies)
        {
            if (!_objective.Contains(ally.Position))
                continue;
            foreach (EnemyForecast forecast in forecasts)
            {
                Position muzzle = forecast.State.Position;
                if (muzzle.ChebyshevDistance(ally.Position) > enemyReach)
                    continue;
                if (!Between(muzzle, tile, ally.Position))
                    continue;
                if (!field.ClearRay(muzzle, ally.Position, strictCorners: false))
                    continue;
                return Gain;
            }
        }
        return 0;
    }

    /// <summary>
    /// Whether <paramref name="middle"/> lies strictly between the two ends on
    /// one of the eight projectile headings — which is the only geometry a bolt
    /// can travel and therefore the only geometry a screen can stand on.
    /// </summary>
    private static bool Between(Position from, Position middle, Position to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        if (dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy))
            return false;
        if (dx == 0 && dy == 0)
            return false;
        int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
        int sx = Math.Sign(dx);
        int sy = Math.Sign(dy);
        for (int step = 1; step < steps; step++)
        {
            if (middle == new Position(from.X + (sx * step), from.Y + (sy * step)))
                return true;
        }
        return false;
    }

    /// <summary>
    /// What a bolt landing on <paramref name="tile"/> is worth to the objective
    /// number beyond its damage: the declared revert, bounded by the work the
    /// controller has actually put on this run. Zero everywhere the interrupt is
    /// not declared, the tile is off the objective, or the enemy is not the
    /// controlling team — in which cases nothing reverts.
    /// </summary>
    public double InterruptValue(Position tile, int damage)
    {
        if (!ChannelRules.Interrupt || RevertPerDamage <= 0)
            return 0;
        if (!_objective.Contains(tile) || !EnemyControls || !EnemyClaimStands)
            return 0;
        int reverted = damage * RevertPerDamage;
        if (WholeRunRevert)
            reverted = Math.Min(reverted, Math.Max(RunWork, 1));
        return reverted;
    }

    /// <summary>
    /// Ticks of uninterrupted control one capture costs from here, using this
    /// team's actual stationary surplus rather than the sole-presence figure the
    /// baseline budget assumes. Used to decide whether the clock can still pay
    /// for a push.
    /// </summary>
    public int TicksToTake(int fromProgress)
    {
        int surplus = Math.Max(1, Math.Min(GainCap, Math.Max(1, AlliedStationary + _selfWeight - EnemyTotal)));
        int rate = Math.Max(1, surplus * Gain);
        return ((Threshold - fromProgress) + rate - 1) / rate;
    }
}
