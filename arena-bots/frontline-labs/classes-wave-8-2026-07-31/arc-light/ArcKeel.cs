using BotArena.Sdk;

/// <summary>
/// What a push is worth on THIS contract, and therefore where to stand.
///
/// <para>Four keel facts change the answer, and all four are read rather than
/// assumed. A completed advance is protected for a declared number of ticks and
/// the live hold is published with its owner, so a capture completed inside the
/// opponent's hold is SPENT — the claim resets and the front does not move.
/// Surplus objective weight scales capture pressure, so a second body on the
/// objective is worth as much as the first. Only an enemy standing alone erodes
/// a claim, which makes stepping off an empty objective free. Arrivals rally
/// forward, which makes dying cheap and makes the objective the densest tile
/// cluster on the map.</para>
///
/// <para>Put together they produce one doctrine that has no meaning on the
/// baseline contract: <b>bank the claim against the hold clock.</b> Inside an
/// enemy hold arc-light refuses to complete a capture it would not be paid for,
/// steps off an objective no enemy is standing alone on (which preserves the
/// claim exactly), and completes the push on the tick the hold lifts.</para>
/// </summary>
internal sealed class ArcKeel
{
    /// <summary>What this body is currently trying to accomplish.</summary>
    public enum Intent
    {
        /// <summary>Take and hold the active objective; captures are paid for.</summary>
        Push = 0,

        /// <summary>
        /// Hold the claim below completion because completing now is spent, and
        /// arrive with it the tick the enemy hold lifts.
        /// </summary>
        Bank = 1,

        /// <summary>
        /// Stop an enemy claim: match weight to freeze their gain, or take sole
        /// presence to erode it.
        /// </summary>
        Deny = 2,

        /// <summary>No objective state to read; stay useful and stay alive.</summary>
        Screen = 3,
    }

    private readonly ArcFacts _facts;
    private readonly GenericActorContext _context;
    private readonly GenericActorContext.ModeObservationState.Frontline? _mode;

    public ArcKeel(ArcFacts facts, GenericActorContext context, ArcMemory memory)
    {
        _facts = facts;
        _context = context;
        _mode = context.Mode
            as GenericActorContext.ModeObservationState.Frontline;
        Hold = ArenaBasics.LiveHold(context);
        (OwnWeight, EnemyWeight, SelfPresent) =
            ArenaBasics.ObjectivePresence(facts.Contract, context);
        ActiveTiles = _mode is null
            ? []
            : facts.ObjectiveTiles(_mode.ActivePositionIndex);

        // THE CHANNEL, in three lines. Claim weight counts only bodies whose
        // tile did not change; denial weight counts every body. So the same
        // three bodies on the same three tiles are worth a capture or worth
        // nothing depending on one bit per body, and that bit is the difference
        // between this arm and every arm before it.
        SelfWeight = facts.ObjectiveWeight(context.Self.FormId);
        var tiles = ActiveTiles.ToHashSet();
        int alliesStill = 0;
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (tiles.Contains(ally.Position)
                && !memory.MovedLastTick(ally.ActorId, ally.Position))
            {
                alliesStill += facts.ObjectiveWeight(ally.FormId);
            }
        }
        AlliedStationaryWeight = alliesStill;
        Decision = Decide();
    }

    public ArenaBasics.Hold? Hold { get; }
    public int OwnWeight { get; }
    public int EnemyWeight { get; }
    public bool SelfPresent { get; }
    public Position[] ActiveTiles { get; }
    public Intent Decision { get; }

    /// <summary>This body's own objective weight in its current form.</summary>
    public int SelfWeight { get; }

    /// <summary>
    /// Allied weight on the active objective that did NOT change tile last tick,
    /// self excluded. Under the channel this — plus this body's own weight if it
    /// holds still — is the claim; everything else on the point is only denial.
    /// </summary>
    public int AlliedStationaryWeight { get; }

    /// <summary>
    /// Capture progress this team gains per tick at a given weight split, using
    /// the contract's own control policy: channelled when the policy channels,
    /// weight-scaled when surplus counts, binary otherwise.
    ///
    /// <para>Under the channel <paramref name="ownWeight"/> is CLAIM weight —
    /// stationary bodies only — and <paramref name="enemyWeight"/> is DENIAL
    /// weight, which counts movers too. Control needs claim strictly above
    /// denial, and the multiplier is the surplus capped at the declared
    /// ceiling.</para>
    /// </summary>
    public int GainAt(int ownWeight, int enemyWeight)
    {
        if (_facts.Capture is not { } capture)
            return 0;
        if (_facts.Channel is { } channel)
        {
            int surplus = ownWeight - enemyWeight;
            if (surplus <= 0)
                return 0;
            return Math.Min(surplus, channel.StationaryCap)
                * capture.GainPerSoleTeamTick;
        }
        if (capture.SurplusWeightScalesGain)
        {
            int net = ownWeight - enemyWeight;
            return net > 0 ? net * capture.GainPerSoleTeamTick : 0;
        }
        return ownWeight > 0 && enemyWeight == 0
            ? capture.GainPerSoleTeamTick
            : 0;
    }

    /// <summary>
    /// True when a standing enemy claim is what controlling the point would
    /// spend itself on. Erosion runs at the declared multiple, so on this arm
    /// the first ticks of a retake are worth four times what a fresh build is —
    /// and being interrupted mid-erosion throws that back, which is why the
    /// erosion tick is screened exactly like a capture tick.
    /// </summary>
    public bool Eroding =>
        _mode?.ClaimingTeamId is int claimant
        && claimant != _facts.TeamId
        && _mode.CaptureProgress > 0;

    /// <summary>
    /// Progress this team moves this tick if this body holds its tile, and if it
    /// steps instead. The whole of W1 and W2 is the comparison between these
    /// two numbers plus the interrupt, and both are computed from the contract's
    /// own arithmetic rather than from a preference for standing still.
    /// </summary>
    public int GainIfStill =>
        SelfPresent
            ? Scaled(GainAt(AlliedStationaryWeight + SelfWeight, EnemyWeight))
            : GainIfStepping;

    /// <summary>Progress per tick when this body's tile changes.</summary>
    public int GainIfStepping =>
        Scaled(GainAt(AlliedStationaryWeight, EnemyWeight));

    private int Scaled(int gain) =>
        gain > 0 && Eroding && _facts.Channel is { } channel
            ? gain * channel.ErosionMultiplier
            : gain;

    /// <summary>
    /// True when this body's stillness is buying progress a step would throw
    /// away, and that progress is wanted. The Bank intent is deliberately
    /// excluded: inside an enemy hold a completed capture is spent, so freezing
    /// to build one faster is paying twice for a reset.
    /// </summary>
    public bool Channeling =>
        ArcRules.StillnessIsTheCapture
        && _facts.Channel is not null
        && SelfPresent
        && Decision != Intent.Bank
        && GainIfStill > GainIfStepping;

    /// <summary>
    /// Progress the interrupt takes back when <paramref name="damage"/> lands on
    /// a body of the controlling team standing on the active objective. Zero
    /// when this ruleset declares no interrupt, and zero for damage taken
    /// anywhere else — which is the entire reason a screen works.
    /// </summary>
    public int RevertFor(int damage) =>
        _facts.Channel is { } channel && damage > 0
            ? damage * channel.RevertPerDamagePoint
            : 0;

    /// <summary>
    /// True when this team currently owns the run the interrupt would revert:
    /// we are the claiming team with work on the board. Damage to us on the
    /// point costs that work; damage to us anywhere else costs nothing.
    /// </summary>
    public bool OwnRunAtRisk =>
        _facts.Channel is not null
        && _mode?.ClaimingTeamId == _facts.TeamId
        && _mode.CaptureProgress > 0;

    /// <summary>
    /// True when leaving the active objective preserves this team's claim: the
    /// decay clock only runs for an enemy standing alone, so stepping off an
    /// objective with no enemy on it costs exactly nothing. On a contract whose
    /// clock also runs while empty, this is false and repositioning is a real
    /// cost.
    /// </summary>
    /// <summary>
    /// Objective weight the opposition currently nets on the active objective.
    /// On a surplus-scaling contract this is literally their capture gain per
    /// tick, so it is the number a body decides its errands against: every point
    /// of it is one progress per tick that one more of our bodies would cancel.
    /// </summary>
    public int Deficit => Math.Max(0, EnemyWeight - OwnWeight);

    /// <summary>
    /// Objective weight this team nets. Positive is progress being made,
    /// zero is a full stop, negative is ground being lost.
    /// </summary>
    public int NetWeight => OwnWeight - EnemyWeight;

    public bool LeavingIsFree =>
        _facts.Capture?.OnlyEnemySolePresenceDecays == true && EnemyWeight == 0;

    /// <summary>
    /// Whether this body can be somewhere else for <paramref name="ticks"/> ticks
    /// without the objective changing hands. Priced from the contract's own
    /// arithmetic: the opposition first has to erode this team's claim toward
    /// zero (overshoot does not carry into theirs) and only then builds its own
    /// at its net weight. If that total outlasts the absence, the trip is free —
    /// and if the opposition has no net weight at all, the objective is not going
    /// anywhere.
    /// </summary>
    public bool AffordableAbsence(int ticks, int myWeight)
    {
        if (_facts.Capture is not { } capture || _mode is null)
            return true;
        int enemyNet = EnemyWeight - (OwnWeight - (SelfPresent ? myWeight : 0));
        if (enemyNet <= 0)
            return true;
        int ourClaim = _mode.ClaimingTeamId == _facts.TeamId
            ? _mode.CaptureProgress
            : 0;
        int theirClaim = _mode.ClaimingTeamId is int claimant
            && claimant != _facts.TeamId
            ? _mode.CaptureProgress
            : 0;
        // The opposition's own gain under the SAME control arithmetic, so a
        // channelled cap that stops their third body from buying speed is
        // priced into the trip rather than ignored.
        int perTick = Math.Max(
            1,
            GainAt(EnemyWeight, OwnWeight - (SelfPresent ? myWeight : 0)));
        int erosion = Math.Max(1, capture.DecayAmount) > 0
            ? ourClaim * Math.Max(1, capture.DecayIntervalTicks)
                / Math.Max(1, capture.DecayAmount)
            : 0;
        int untilTheirCapture = erosion
            + ((capture.Threshold - theirClaim + perTick - 1) / perTick);
        return untilTheirCapture > ticks + 2;
    }

    private int? TicksToComplete()
    {
        if (_mode is null
            || _facts.Capture is not { } capture
            || _mode.ClaimingTeamId != _facts.TeamId)
        {
            return null;
        }
        int gain = _facts.Channel is not null
            ? Math.Max(GainIfStill, 1)
            : GainAt(Math.Max(OwnWeight, 1), EnemyWeight);
        if (gain <= 0)
            return null;
        int remaining = capture.Threshold - _mode.CaptureProgress;
        return remaining <= 0 ? 0 : (remaining + gain - 1) / gain;
    }

    private Intent Decide()
    {
        if (_mode is null || ActiveTiles.Length == 0)
            return Intent.Screen;

        // Inside another team's live hold a completed capture is spent. Do not
        // pay a full capture window for a reset: hold the claim short of the
        // threshold and complete it once the protection lifts.
        if (Hold is { Mine: false } enemyHold
            && TicksToComplete() is int ticks
            && ticks <= enemyHold.RemainingTicks)
        {
            return Intent.Bank;
        }

        // Under a surplus-scaling control policy every tick spent outnumbered on
        // the point is progress against this team at the FULL size of the
        // deficit, and one more body of ours cancels exactly one point of it.
        // Against a chassis that fields more slots than we do, that arithmetic
        // is the whole matchup: matching weight is worth more than any exchange,
        // and it does not wait for the enemy claim to look urgent.
        if (Deficit > 0 && _facts.WeightScalesGain)
            return Intent.Deny;

        bool enemyClaiming = _mode.ClaimingTeamId is int claimant
            && claimant != _facts.TeamId
            && _mode.CaptureProgress > 0;
        if (enemyClaiming && _facts.Capture is { } capture)
        {
            bool urgent = _mode.CaptureProgress * 2 >= capture.Threshold;
            if (urgent || Hold is { Mine: true })
                return Intent.Deny;
        }
        return Intent.Push;
    }

    /// <summary>
    /// Tiles this body should be standing on. Envelopment is deliberate: the
    /// candidate list is rotated by this body's rank among its active team
    /// bodies, so independent lives with no shared state converge on DIFFERENT
    /// tiles and approach from different bearings. Clumping into one lane is
    /// what feeds a three-bolt fan, and standing on a lane an enemy stance
    /// already covers is worse than standing one tile off it.
    /// </summary>
    public Position[] Goals(
        int rank,
        IReadOnlySet<Position> fanLanes,
        ArcTraffic traffic,
        ArcMemory memory)
    {
        if (_mode is null)
            return [];

        // W7. The approach bearing is the one choice in this doctrine an
        // opponent could pre-aim, and the team stream is the only randomness
        // three independent lives agree on. The draw is taken HERE, before any
        // branch on private state, so every life that reaches this point draws
        // the same value; the epoch is the contract's own capture threshold, and
        // a life born mid-epoch runs on the fresh draw until the next boundary
        // rather than pretending to remember one.
        bool mirror = false;
        if (ArcRules.TeamFlankJitter)
        {
            bool fresh = _context.TeamRandom.NextBool();
            int epoch = Math.Max(1, _facts.Capture?.Threshold ?? 8);
            mirror = memory.SharedFlank(_context.Tick / epoch, fresh);
        }

        Position[] candidates = Decision switch
        {
            Intent.Bank => BankTiles(fanLanes),
            Intent.Screen => ArenaBasics.OwnSideObjectiveTiles(
                _facts.Contract,
                _context),
            _ => ActiveTiles,
        };
        if (candidates.Length == 0)
            candidates = ActiveTiles;
        if (candidates.Length == 0)
            return [];

        // C2/C3/C4 all live in this ordering, because a goal is where a body
        // will STAND, and the three bars are about where standing costs the team
        // something: a doorway is never a destination, the tile the next arrival
        // is due on is not mine to sit on, and a tile that shares an enemy fan
        // pose with an ally is the opposition's break-even count handed over.
        // They are sort keys rather than filters on purpose — the brief's bar is
        // "when an equal-value adjacent pose exists", and a rank-rotated ordered
        // list is exactly the structure that prefers the clean pose and still
        // takes the contested one when it is the only ground there is.
        Position[] ordered = candidates
            .OrderBy(tile => ArcRules.ChokePrecedence && _facts.IsChoke(tile)
                ? 1
                : 0)
            .ThenBy(tile => traffic.RallyClear(tile, 3) ? 0 : 1)
            .ThenBy(tile => fanLanes.Contains(tile) ? 1 : 0)
            .ThenByDescending(tile => AllyClearance(tile))
            .ThenBy(tile =>
                ArcBoard.StepDistance(_facts, _context.Self.Position, [tile], 24)
                    ?? int.MaxValue)
            // C4 is deliberately the LAST key before the canonical order, which
            // is the whole of what the coordination bar asks for: separate two
            // bodies out of one fan pose only when an EQUAL-VALUE pose exists.
            // Priced any higher it is a real cost, and it measured like one —
            // 22-10-0 with co-exposure weighted into lane values and goal
            // precedence, 27-4-1 with it removed entirely, on the same 32
            // matches. This ordering keeps the bar and spends nothing for it.
            .ThenBy(tile => traffic.CoExposure(tile))
            .ThenBy(tile => mirror ? -tile.Y : tile.Y)
            .ThenBy(tile => mirror ? -tile.X : tile.X)
            .ToArray();
        int offset = ordered.Length == 0
            ? 0
            : ((rank % ordered.Length) + ordered.Length) % ordered.Length;
        return [.. ordered.Skip(offset), .. ordered.Take(offset)];
    }

    private int AllyClearance(Position tile) =>
        _context.Allies.IsEmpty
            ? 9
            : Math.Min(
                9,
                _context.Allies.Min(ally => tile.ChebyshevDistance(ally.Position)));

    /// <summary>
    /// Where to wait out an enemy hold: one step off the objective, still
    /// covering it, and off any lane a fan already owns. Legal only because the
    /// contract says an empty objective preserves the claim.
    /// </summary>
    private Position[] BankTiles(IReadOnlySet<Position> fanLanes)
    {
        if (!LeavingIsFree)
            return ActiveTiles;
        var ring = new HashSet<Position>();
        foreach (Position tile in ActiveTiles)
        {
            foreach (Direction direction in ArcBoard.Cardinals)
            {
                Position candidate = ArcBoard.Step(tile, direction);
                if (!_facts.Impassable(candidate)
                    && !ActiveTiles.Contains(candidate)
                    && !fanLanes.Contains(candidate))
                {
                    ring.Add(candidate);
                }
            }
        }
        return ring.Count == 0 ? ActiveTiles : ring.ToArray();
    }

    /// <summary>
    /// True when this body must not leave the active objective, because its
    /// weight is the thing keeping the claim alive or the enemy's claim frozen.
    /// This is the turret bargain generalised: never spend presence that is
    /// currently load-bearing.
    /// </summary>
    public bool PresenceIsLoadBearing =>
        SelfPresent
        && (EnemyWeight > 0
            || Decision == Intent.Deny
            || (Decision == Intent.Push && GainAt(OwnWeight, EnemyWeight) > 0
                && OwnWeight <= _facts.ObjectiveWeight(_context.Self.FormId)));

    /// <summary>
    /// The wave-8 half of the same question, and the reason two kiting defenders
    /// hold three stationary attackers: under the channel a body that MOVES
    /// still counts for denial, so leaving a tile of the objective for another
    /// tile of the objective concedes nothing at all while the opposition is the
    /// one trying to build. Presence is only spendable off the region.
    /// </summary>
    public bool DenialSurvivesAStep => _facts.Channel is not null;

    /// <summary>
    /// Signed territorial progress for this team from the authoritative
    /// scoreboard, used to decide whether a late-match risk is worth taking.
    /// </summary>
    public long TerritorialLead()
    {
        long own = 0;
        long best = long.MinValue;
        foreach (GenericActorContext.TeamScoreState team
                 in _context.Scoreboard.Teams)
        {
            long value = team.Scores.Length == 0 ? 0 : team.Scores[0].Value;
            if (team.TeamId == _facts.TeamId)
                own = value;
            else if (value > best)
                best = value;
        }
        return best == long.MinValue ? own : own - best;
    }
}
