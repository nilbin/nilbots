using BotArena.Sdk;

/// <summary>
/// LedgerFly - the attrition banker, revision 4.
///
/// One body in this team is the bank: the slot the contract returns
/// automatically after destruction, and the only form with a fabrication route.
/// Children are the currency. That much has been true since revision 1.
///
/// Revision 3 stopped booking bodies and started booking convertible
/// objective-ticks: it read the hold, the control policy, and the arrival
/// placement, and it sent a second body onto the objective because surplus
/// weight scales capture pressure. Revision 4 keeps every one of those readings
/// and fixes what they left out - <b>a tick of presence is only worth what it
/// can keep standing for</b>. Three contract facts set that price, and none of
/// them is about how many bodies exist:
///
/// <list type="bullet">
/// <item><b>Shape.</b> A bolt is a ray that stops on the first enemy body, a fan
/// launches down a facing lane and its neighbours at once, and a guard's arc is a
/// fixed quadrant that never tracks. So weight stacked into one lane is not the
/// same asset as weight spread around the region, and every tile choice on
/// contested ground breaks its ties on bearing dispersion
/// (<see cref="Bearings"/>). Envelopment beats an arc and starves a fan;
/// clumping feeds both.</item>
/// <item><b>Blood.</b> <c>capture.decayClock</c> may say that only an enemy
/// standing ALONE erodes a claim, and every bolt now publishes
/// <c>damagePerHit</c> beside an exact arrival tick. Together they price a dodge
/// properly: stepping off a contested tile to avoid one survivable bolt converts
/// "contested, claim preserved" into "enemy sole, claim eroding". So a body that
/// can survive the hit and whose removal would flip control eats the bolt and
/// keeps the tile. The bank never does - see below.</item>
/// <item><b>Throughput.</b> The bank's survival premium scales with how many
/// slots it has to feed by hand (<see cref="MatchLens.Pipelines"/>), read from
/// the slots' own lifecycle profiles. Revision 3 repriced the standoff DOWN
/// because a forward rally makes dying cheap; that is still true of the walk and
/// false of the pipeline - a bank that owns four explicit-fabrication slots
/// stalls four rebuild clocks when it dies, so it stands one tile further back
/// and spends its ticks queueing, slowest-rebuilding slot first.</item>
/// </list>
///
/// The skill kit is priced on both sides in <see cref="Stances"/>: a stance is
/// any same-life route into a form that keeps its objective weight and adds a
/// fan or a guard, which is a gate that admits the kit's two stances and rejects
/// fortification into a zero-weight body - the banker does not delete its own
/// scoring presence for durability.
///
/// Nothing here is tuned to a particular arm, and no arm name appears in the
/// source. Fabrication routes, placement offsets, slot rosters, unlock and
/// rebuild clocks, declared classes, form stats, fan shape, guard arcs, stance
/// budgets, shot language and bend depth, movement facing coupling, capture
/// policy, the live hold, arrival placement, and the ranking channel are read
/// from the resolved contract, the per-tick legality mask, and the observation,
/// so one artifact plays the unmodified control, every counterweight level, the
/// kit on or off, and either bend envelope.
/// </summary>
public sealed class LedgerFly : IGenericActorBot
{
    private const int Standoff = 3;
    private const int SearchRadius = 12;

    private MatchLens? _lens;
    private Ledger _ledger = new();
    private Ratchet _ratchet = new();
    private Stances _stances = new();
    private bool _rotatedForPlacement;
    private Position? _dodgeOrigin;
    private int _avoidDodgeOriginThrough = -1;
    private Direction[] _order = Field.Steps;
    private Kinematics? _kin;
    private List<Position> _allies = [];
    private HashSet<int> _covered = [];
    private Position _focus;

    /// <inheritdoc />
    public void StartLife(GenericActorMatchStart start)
    {
        _lens = new MatchLens(start);
        _ledger = new Ledger();
        _ratchet = new Ratchet();
        _stances = new Stances();
        _rotatedForPlacement = false;
        _dodgeOrigin = null;
        _avoidDodgeOriginThrough = -1;
        _order = Field.Steps;
        _kin = null;
        _allies = [];
        _covered = [];
        _focus = default;
    }

    /// <inheritdoc />
    public GenericActorDecision Tick(GenericActorContext context)
    {
        if (_lens is not MatchLens lens)
            return Fallback(context, "no contract");
        try
        {
            return Decide(lens, context);
        }
        catch (Exception)
        {
            return Fallback(context, "falling back to a legal action");
        }
    }

    private GenericActorDecision Decide(
        MatchLens lens,
        GenericActorContext context)
    {
        _ledger.Observe(lens, context);
        _ratchet.Observe(lens, context);
        _stances.Observe(lens, context);

        // Equal-length routes and equal-score tiles must not break on an
        // absolute compass preference: on a mirror-symmetric map both teams
        // share it and the advancing side wins the tie every time. One ordered
        // preference per tick - advance first, retreat last, laterals decided
        // by this life's deterministic stream - is used by every search below.
        _order = ArenaBasics.OrderedDirections(lens.Contract, context);
        _kin = new Kinematics(lens, context);

        // A windup owns the tick: the legality mask allows nothing else.
        if (context.Self.PendingSameLifeTransition is not null)
            return Fallback(context, "committed to a form change");

        Position[] objective = lens.ActiveObjective(context);
        HashSet<Position> objectiveTiles = objective.ToHashSet();
        bool onObjective = objectiveTiles.Contains(context.Self.Position);
        HashSet<Position> blocked = Field.Blocked(lens, context);
        // A tile a queued lifecycle output has already claimed is a tile the
        // engine will refuse us, and standing on one blocks a replacement we
        // paid a combat action for. The claim is published on the tile.
        blocked.UnionWith(Field.Reserved(lens, context));
        HashSet<Position> threatened = Field.Threatened(lens, context, 2);
        GenericActorActionLegality? move = Field.MoveAction(lens, context);
        HashSet<Direction> steps = Field.LegalSteps(move);

        bool endgame = Endgame(lens, context);
        bool frontManned = FrontManned(lens, context, objectiveTiles);
        _allies = Bearings.AllyTiles(context);
        _focus = objective.Length == 0
            ? _ledger.ExchangeAnchor(lens, context)
            : MatchLens.Centroid(objective);
        _covered = Bearings.CoveredSectors(_focus, _allies);

        // The exchange rate on this tick's objective, entirely from the
        // contract: what our presence is worth against theirs, and whether the
        // ticks it buys can become an advance at all.
        (int ownWeight, int enemyWeight, bool _) =
            ArenaBasics.ObjectivePresence(lens.Contract, context);
        bool weightConverts = lens.Capture?.SurplusWeightScalesGain == true;
        bool paysNow = !_ratchet.Barren
            && _ratchet.Current != Ratchet.Phase.Paused;

        // Three separate reasons the bank leaves its standoff, each of them a
        // reading rather than a habit:
        //   - surplus weight converts and we are not yet clear of theirs, so
        //     the marginal body is the difference between capturing and being
        //     eroded (or, when the phase pays nothing, between denying and
        //     losing more ground);
        //   - our own ratchet is live, so the front cannot come back and a
        //     death costs only the return clock;
        //   - the classic one: nobody else is holding the line.
        bool weightBuysGround = weightConverts
            && ownWeight <= enemyWeight + 1
            && (paysNow || enemyWeight > 0);
        bool shelteredPush = _ratchet.Sheltered && lens.RallyForward;
        // Throughput is the bank's other job, and on a roster with several
        // explicit-fabrication slots it is the bigger one: its death stalls
        // every one of those rebuild clocks until the return lands and it walks
        // back onto a source tile. So the two DISCRETIONARY commits - adding our
        // own weight, and pushing behind our own hold - are outbid while the
        // pipeline is deep and the field already has bodies on it. The two
        // NON-discretionary ones are not: a clock-decided match and an unmanned
        // line still pull the bank forward. Inert on a two-pipeline roster,
        // which is exactly revision 3's behaviour.
        // ...and only when the front can be trusted with the exchange it is
        // being left to fight. A straight-only gun under a locked profile
        // answers an off-lane contact a tick late, so a front of those bodies
        // needs the bank's weight beside it and gets it; a bending front does
        // not, and the bank keeps its throughput instead. Measured, not assumed:
        // see DX.md - this condition is the difference between winning and
        // losing the deep-roster cells, in both directions.
        bool throughputHeavy = lens.Pipelines >= 3
            && lens.BendEnvelope
            && _ledger.FieldBodies >= 2;
        bool bankCommits = endgame
            || !frontManned
            || (!throughputHeavy && (weightBuysGround || shelteredPush));
        bool holdBack = lens.IsBankSlot && !bankCommits;

        // Deepest edge of the region while the ticks convert, own-side edge
        // while they cannot: inside their hold the objective is ground to hold,
        // not ground to take, and the near edge keeps the region between us and
        // the approach.
        bool pressForward =
            (!lens.IsBankSlot || endgame || _ratchet.Sheltered)
            && !_ratchet.Barren;

        // 0. A stance that nothing is shooting at is a body standing still. The
        //    exit is ours below the budget; above it the engine has already
        //    taken it. Inert on every chassis that owns no stance.
        GenericActorDecision? unstance = _stances.TryLeave(lens, context);
        if (unstance is not null)
            return unstance;

        // 0b. A guard outranks a dodge. Standing on contested ground with a bolt
        //     already inside our quadrant is the exact state the arc was built
        //     for: the bolt dies, a copy goes back at whoever fired it, and the
        //     tile is never given up. It has to be raised BEFORE the dodge rung
        //     or the dodge always wins the tick and the shield never goes up.
        //     Inert on every chassis whose routes lead nowhere guarded.
        GenericActorDecision? raise =
            _stances.TryRaise(lens, context, objectiveTiles);
        if (raise is not null)
            return raise;

        // 1. Nothing is worth a body - but a tile can be worth blood. The bolt
        //    publishes what one contact costs and the exact tick it lands, and
        //    the capture definition publishes whether contested ticks preserve a
        //    claim. When they do, stepping off a contested tile to dodge one
        //    survivable bolt is not caution: it converts "contested, claim
        //    preserved" into "enemy alone, claim eroding", and hands them the
        //    gain as well. So a body that survives the hit and whose removal
        //    would materially flip control stands its ground.
        bool eatTheBolt = WorthTheHit(
            lens,
            context,
            onObjective,
            ownWeight,
            enemyWeight);
        if (threatened.Contains(context.Self.Position)
            && move is not null
            && !eatTheBolt)
        {
            GenericActorDecision? evade = Evade(
                lens,
                context,
                move,
                steps,
                blocked,
                threatened,
                objectiveTiles,
                onObjective);
            if (evade is not null)
            {
                _dodgeOrigin = context.Self.Position;
                _avoidDodgeOriginThrough = context.Tick + 1;
                return evade;
            }
        }

        // 2. The books come before the duel: the bank queues a replacement.
        if (lens.IsBankSlot)
        {
            GenericActorDecision? economy = Economy(
                lens,
                context,
                move,
                blocked,
                threatened,
                objectiveTiles);
            if (economy is not null)
                return economy;
        }

        // 3. Fire only at a solution we have already simulated.
        List<GenericActorContext.ObservedEnemyState> targets =
            Targets(lens, context);

        // 3a. ...unless a fan buys more than one bolt does. One entry is one
        //     cast, so the spread has to be earning before the windup is paid.
        GenericActorDecision? cast = _stances.TryCast(lens, context, targets);
        if (cast is not null)
            return cast;

        bool loaded = Gunnery.GunLoaded(lens, context);
        if (loaded && targets.Count > 0)
        {
            GenericActorDecision? shot = Gunnery.TryFire(
                lens,
                context,
                targets,
                allowCurved: true,
                _stances);
            if (shot is not null)
                return shot;
        }

        // 4. No lane: turn into one when the turn is free (we are holding) or
        //    the body is close enough that a lost tick is cheaper than a miss.
        if (loaded
            && targets.Count > 0
            && (onObjective || Nearest(context, targets) <= 4))
        {
            GenericActorDecision? aim = Gunnery.TryRotateToAim(
                lens,
                context,
                targets,
                allowCurved: true,
                _order,
                _stances);
            if (aim is not null)
                return aim;
        }

        // 5. Suppress rather than concede while we are the ones holding, and if
        //    the facing we are holding has no lane worth a bolt, turn onto one
        //    that has. A rotation does not move the body, so the tile we are
        //    contesting is never given up to buy the shot.
        if (loaded && onObjective && _ledger.KnownEnemyTiles.Count > 0)
        {
            GenericActorDecision? suppress = Gunnery.TrySuppress(
                lens,
                context,
                _ledger.KnownEnemyTiles);
            if (suppress is not null)
                return suppress;
            // Only while nothing is actually in sight. A rotation also swings
            // the facing quadrant this body sees through, and trading a live
            // contact for a remembered one is how a holder loses the exchange
            // it was already watching.
            if (targets.Count == 0)
            {
                GenericActorDecision? turn = Gunnery.TryRotateToSuppress(
                    lens,
                    context,
                    _ledger.KnownEnemyTiles,
                    _order);
                if (turn is not null)
                    return turn;
            }
        }

        // 6. Walk: the bank to its standoff, everyone else onto the objective.
        if (move is not null)
        {
            GenericActorDecision? walk = holdBack
                ? Stage(
                    lens,
                    context,
                    move,
                    steps,
                    blocked,
                    threatened,
                    objectiveTiles)
                : Contest(
                    lens,
                    context,
                    move,
                    steps,
                    blocked,
                    threatened,
                    objective,
                    onObjective,
                    pressForward);
            if (walk is not null)
                return walk;
        }

        // 7. The empty-gun tick. Fire cooldown is two, so roughly every second
        //    combat tick has no shot to take, and in wave 1 those ticks became
        //    `wait` - the body stood still in the lane it had just been shot
        //    down. Step 6 could not help: its goal set was already satisfied.
        //    So spend the tick on footwork instead, inside the objective region
        //    when we are holding it, which breaks the straight or one-bend lane
        //    without conceding a tile of presence.
        if (move is not null)
        {
            GenericActorDecision? footwork = Posture(
                lens,
                context,
                move,
                steps,
                blocked,
                threatened,
                objectiveTiles,
                onObjective);
            if (footwork is not null)
                return footwork;
        }

        // 8. Idle at the standoff: keep the approach inside our facing quadrant
        //    so the bank sees the exchange coming and is already aimed at it.
        GenericActorDecision? watch = FaceAnchor(lens, context);
        if (watch is not null)
            return watch;

        return Fallback(
            context,
            (holdBack ? "banking the position" : "holding the objective")
                + $" [{_ratchet.Current}]");
    }

    /// <summary>
    /// Whether the tile under this body is worth the bolt that is about to land
    /// on it. Everything here is delivered rather than assumed: the bolt's
    /// <c>damagePerHit</c> and exact arrival tick come from the projectile, the
    /// objective weights from the observed bodies' declared forms, and
    /// "contested ticks preserve a claim" from <c>capture.decayClock</c>.
    ///
    /// <para>Three conditions, all of them necessary. The hit has to be
    /// survivable - eating a lethal bolt is not a doctrine. Somebody hostile has
    /// to be standing on the objective, because with an enemy-sole decay clock
    /// that is the only configuration in which leaving costs anything beyond our
    /// own gain. And our own weight has to be pivotal: with a comfortable
    /// surplus one body stepping out of a lane changes nothing, so it steps.</para>
    ///
    /// <para>The bank is excluded outright. It is the throughput asset, it
    /// carries the lowest health on this chassis, and its death stalls every
    /// pipeline it feeds - the one body whose survival is worth more than any
    /// single tile.</para>
    /// </summary>
    private bool WorthTheHit(
        MatchLens lens,
        GenericActorContext context,
        bool onObjective,
        int ownWeight,
        int enemyWeight)
    {
        if (!onObjective || lens.IsBankSlot)
            return false;
        if (lens.Capture?.OnlyEnemySolePresenceDecays != true)
            return false;
        if (enemyWeight <= 0 || ownWeight - enemyWeight > 1)
            return false;
        return Field.SoonestIncoming(lens, context, context.Self.Position)
                is ArenaBasics.Incoming incoming
            && context.Self.Health > incoming.Damage;
    }

    /// <summary>
    /// One tile of footwork that improves the body's posture without giving up
    /// what it is standing on. Candidates are the single legal steps only, so
    /// it can never wander off a contested region, and while we hold the
    /// objective the candidate set is restricted to objective tiles. A move is
    /// taken only on a clear improvement, which is what stops the shuffle from
    /// oscillating between two equally poor tiles.
    /// </summary>
    private GenericActorDecision? Posture(
        MatchLens lens,
        GenericActorContext context,
        GenericActorActionLegality move,
        IReadOnlySet<Direction> steps,
        IReadOnlySet<Position> blocked,
        IReadOnlySet<Position> threatened,
        IReadOnlySet<Position> objectiveTiles,
        bool onObjective)
    {
        const int Margin = 2;
        Position anchor = _ledger.ExchangeAnchor(lens, context);
        int bestScore = PostureScore(
            lens,
            context,
            context.Self.Position,
            anchor,
            objectiveTiles,
            threatened,
            onObjective);
        Direction? chosen = null;

        foreach (Direction direction in _order)
        {
            if (!steps.Contains(direction))
                continue;
            (int dx, int dy) = direction.Vector();
            Position destination = context.Self.Position.Offset(dx, dy);
            if (lens.IsWall(destination)
                || blocked.Contains(destination)
                || threatened.Contains(destination))
            {
                continue;
            }
            // Holding the region is worth more than any posture gain.
            if (onObjective && !objectiveTiles.Contains(destination))
                continue;
            if (_dodgeOrigin is Position origin
                && context.Tick <= _avoidDodgeOriginThrough
                && destination == origin)
            {
                continue;
            }

            int score = PostureScore(
                lens,
                context,
                destination,
                anchor,
                objectiveTiles,
                threatened,
                onObjective);
            score += _kin?.TurnAwayPenalty(
                context.Self.Position,
                direction,
                anchor,
                3) ?? 0;
            if (score + Margin <= bestScore)
            {
                bestScore = score;
                chosen = direction;
            }
        }

        return chosen is Direction step
            ? Field.Step(move, step, "footwork off the covered tile")
            : null;
    }

    private int PostureScore(
        MatchLens lens,
        GenericActorContext context,
        Position tile,
        Position anchor,
        IReadOnlySet<Position> objectiveTiles,
        IReadOnlySet<Position> threatened,
        bool onObjective)
    {
        int score = Field.Covered(lens, context, tile, _ledger.KnownEnemyTiles) * 10;
        score += threatened.Contains(tile) ? 12 : 0;
        score += Bearings.Crowding(
            lens,
            _stances,
            tile,
            _focus,
            _allies,
            _covered);
        if (!onObjective && objectiveTiles.Count > 0)
        {
            score += objectiveTiles.Min(
                objectiveTile => objectiveTile.ChebyshevDistance(tile));
        }
        if (lens.IsBankSlot)
        {
            score += Math.Abs(tile.ChebyshevDistance(anchor) - Standoff);
        }
        // A tile that already holds a lane on a body we know about is worth
        // standing on once the gun reloads.
        GenericActorRulesContract.AttackProfile? profile =
            lens.Attack(context.Self.FormId);
        int range = profile?.Projectile.MaxTravelTiles ?? 0;
        foreach (Position enemy in _ledger.KnownEnemyTiles)
        {
            if (tile.ChebyshevDistance(enemy) <= range
                && Field.RayReaches(lens, tile, enemy))
            {
                score -= 4;
                break;
            }
        }
        return score;
    }

    private GenericActorDecision? FaceAnchor(
        MatchLens lens,
        GenericActorContext context)
    {
        Position anchor = _ledger.ExchangeAnchor(lens, context);
        int dx = anchor.X - context.Self.Position.X;
        int dy = anchor.Y - context.Self.Position.Y;
        if (dx == 0 && dy == 0)
            return null;
        Direction desired = Math.Abs(dx) >= Math.Abs(dy)
            ? dx > 0 ? Direction.East : Direction.West
            : dy > 0 ? Direction.South : Direction.North;
        if (desired == context.Self.Facing)
            return null;
        GenericActorActionLegality? rotate = Rotation(lens, context, desired);
        return rotate is null
            ? null
            : new GenericActorDecision(
                rotate.ActionId,
                rotate.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(desired)],
                "watching the exchange");
    }

    private GenericActorDecision? Economy(
        MatchLens lens,
        GenericActorContext context,
        GenericActorActionLegality? move,
        IReadOnlySet<Position> blocked,
        IReadOnlySet<Position> threatened,
        IReadOnlySet<Position> objectiveTiles)
    {
        // The template's capability digest is the cheap precondition: no
        // explicit fabrication route for this form (an automatic-companion arm,
        // or a chassis without the verb) means the whole economy rung is inert
        // and the body falls through to the trader ladder unchanged.
        (bool _, bool _, bool fabricationRoute, int? _) =
            ArenaBasics.Capabilities(lens.Contract, context);
        FabricationRoute? route = fabricationRoute
            ? lens.FabricationFor(context.Self.FormId)
            : null;
        if (route is null || !_ledger.WantsReplacement(lens, context))
        {
            _rotatedForPlacement = false;
            return null;
        }

        GenericActorActionLegality? fabricate = context.ActionLegalities
            .Where(action =>
                action.Available
                && string.Equals(
                    action.ActionId,
                    route.ActionId,
                    StringComparison.Ordinal))
            .FirstOrDefault();
        var targets = fabricate?.Constraints
            .OfType<GenericActorActionLegality.ArgumentConstraint
                .UnitTargetConstraint>()
            .FirstOrDefault();

        if (fabricate is not null
            && targets is not null
            && !targets.AllowedValues.IsEmpty)
        {
            Position anchor = _ledger.ExchangeAnchor(lens, context);
            Position? current = route.Predict(
                lens,
                context.Self.Position,
                context.Self.Facing,
                blocked);
            (Direction Facing, int Distance)? best = route.BestFacing(
                lens,
                context.Self.Position,
                context.Self.Facing,
                blocked,
                anchor,
                _order);

            // Placement is evaluated against queue-time facing. Under a profile
            // that turns the body on a successful step, the approach direction
            // already IS the queue-time facing, so buying it with a separate
            // rotation is usually a wasted tick: prefer to walk into the pose,
            // and only pay the rotation when a step in that direction is not
            // available. Under a locked profile the rotation has to be paid
            // either way, so it stays exactly as cheap as it was.
            if (!_rotatedForPlacement
                && best is (Direction facing, int distance)
                && facing != context.Self.Facing
                && !threatened.Contains(context.Self.Position)
                && (current is null
                    || current.Value.ChebyshevDistance(anchor) - distance >= 2))
            {
                if (_kin is { TurnsOnMove: true }
                    && move is not null
                    && Field.LegalSteps(move).Contains(facing))
                {
                    (int fx, int fy) = facing.Vector();
                    Position ahead = context.Self.Position.Offset(fx, fy);
                    if (!lens.IsWall(ahead)
                        && !blocked.Contains(ahead)
                        && !threatened.Contains(ahead))
                    {
                        return Field.Step(
                            move,
                            facing,
                            "stepping into the pose the replacement needs");
                    }
                }

                GenericActorActionLegality? rotate =
                    Rotation(lens, context, facing);
                if (rotate is not null)
                {
                    _rotatedForPlacement = true;
                    return new GenericActorDecision(
                        rotate.ActionId,
                        rotate.ActionCode,
                        [
                            new GenericActorActionArgument.DirectionArgument(
                                facing),
                        ],
                        "turning so the replacement lands on the exchange");
                }
            }

            // Not every Ready slot is the same currency. A slot's rebuild clock
            // is declared on its own lifecycle profile, and on a roster whose
            // late slots rebuild on a slower clock the expensive asset is the
            // one whose idle time costs most - so it goes onto the field first,
            // and the fast-churning slots stay as the float. On a roster where
            // every slot shares one clock this resolves to the old unit order.
            GenericActorActionArgument.UnitTarget slot = targets.AllowedValues
                .OrderBy(target => target.TeamId)
                .ThenByDescending(target => RebuildCost(lens, target))
                .ThenBy(target => target.UnitId)
                .First();
            _rotatedForPlacement = false;
            _ledger.RecordQueued();
            return new GenericActorDecision(
                fabricate.ActionId,
                fabricate.ActionCode,
                [new GenericActorActionArgument.UnitTargetArgument(slot)],
                $"paying {slot.TeamId}:{slot.UnitId} toward "
                    + $"{_ledger.SolvencyTarget} bodies vs {lens.EnemyClassLabel}");
        }

        // The route exists and a slot is waiting, but we are standing off its
        // declared source region: walk back to the counter.
        if (move is null
            || route.SourceTiles.Count == 0
            || route.SourceTiles.Contains(context.Self.Position)
            || !HasReadySlot(lens, context))
        {
            return null;
        }

        Position exchange = _ledger.ExchangeAnchor(lens, context);
        return Walk(
            lens,
            context,
            move,
            Field.LegalSteps(move),
            blocked,
            threatened,
            route.SourceTiles,
            tile =>
                (tile.ChebyshevDistance(exchange) * 2)
                + (Field.Covered(lens, context, tile, _ledger.KnownEnemyTiles)
                    * 6)
                + (objectiveTiles.Contains(tile) ? 4 : 0),
            "returning to the fabrication counter");
    }

    private GenericActorDecision? Stage(
        MatchLens lens,
        GenericActorContext context,
        GenericActorActionLegality move,
        IReadOnlySet<Direction> steps,
        IReadOnlySet<Position> blocked,
        IReadOnlySet<Position> threatened,
        IReadOnlySet<Position> objectiveTiles)
    {
        Position anchor = _ledger.ExchangeAnchor(lens, context);
        FabricationRoute? route = lens.FabricationFor(context.Self.FormId);
        Field.Reach reach = Reachable(lens, context, blocked, threatened, steps);

        Position bestTile = context.Self.Position;
        int bestScore = StagingScore(
            lens,
            context,
            context.Self.Position,
            anchor,
            objectiveTiles,
            threatened,
            route,
            0);
        foreach (KeyValuePair<Position, int> entry in reach.Distance)
        {
            if (entry.Value > SearchRadius)
                continue;
            int score = StagingScore(
                lens,
                context,
                entry.Key,
                anchor,
                objectiveTiles,
                threatened,
                route,
                entry.Value);
            if (reach.FirstStep.TryGetValue(entry.Key, out Direction first))
            {
                score += _kin?.TurnAwayPenalty(
                    context.Self.Position,
                    first,
                    anchor,
                    2) ?? 0;
            }
            if (score < bestScore
                || (score == bestScore && Before(entry.Key, bestTile)))
            {
                bestScore = score;
                bestTile = entry.Key;
            }
        }

        if (bestTile == context.Self.Position
            || !reach.FirstStep.TryGetValue(bestTile, out Direction step))
        {
            return null;
        }
        string reason = $"banking behind the exchange [{_ratchet.Current}]";
        return _kin?.Step(context, move, step, reason)
            ?? Field.Step(move, step, reason);
    }

    private int StagingScore(
        MatchLens lens,
        GenericActorContext context,
        Position tile,
        Position anchor,
        IReadOnlySet<Position> objectiveTiles,
        IReadOnlySet<Position> threatened,
        FabricationRoute? route,
        int distance)
    {
        // Re-priced retreat, twice over. When a step turns the body, every tile
        // of standoff is also a tile of lost facing. And when arrivals rally
        // onto the own-side objective, dying costs the return clock instead of
        // a walk across the map, so the whole premium the standoff was buying
        // shrinks with it.
        int standoff = Standoff;
        if (_kin is { TurnsOnMove: true })
            standoff--;
        if (lens.RallyForward)
            standoff--;
        // ...and back up again when this bank is a high-throughput one. A forward
        // rally repriced the WALK, not the pipeline: a slot that only ever
        // receives a body through an explicit fabrication is stalled for its
        // whole rebuild clock plus our return clock every time the bank dies,
        // and a deep roster multiplies that by every such slot.
        if (lens.Pipelines >= 3 && lens.BendEnvelope)
            standoff++;
        standoff = Math.Max(1, standoff);
        int score = Math.Abs(tile.ChebyshevDistance(anchor) - standoff) * 3;
        score += Field.Covered(lens, context, tile, _ledger.KnownEnemyTiles) * 10;
        score += threatened.Contains(tile) ? 12 : 0;
        score += objectiveTiles.Contains(tile) ? 8 : 0;
        score += distance;
        score += Bearings.Crowding(
            lens,
            _stances,
            tile,
            _focus,
            _allies,
            _covered);
        // The leash stays on the SPAWN anchor even where arrivals rally
        // forward, and that is deliberate: "which half of the map is mine" and
        // "where does my next body appear" are two different questions with two
        // different answers on a rallying contract. Anchoring the leash on the
        // rally point makes it follow the fight, which stops it being a leash -
        // measured as an eight-seed swing from 16-0 to 8-8 on the sticky arm.
        score += tile.ChebyshevDistance(lens.HomeAnchor) / 3;
        if (route is not null && route.SourceTiles.Contains(tile))
            score -= 3;
        if (_dodgeOrigin is Position origin
            && context.Tick <= _avoidDodgeOriginThrough
            && tile == origin)
        {
            score += 6;
        }
        return score;
    }

    private GenericActorDecision? Contest(
        MatchLens lens,
        GenericActorContext context,
        GenericActorActionLegality move,
        IReadOnlySet<Direction> steps,
        IReadOnlySet<Position> blocked,
        IReadOnlySet<Position> threatened,
        Position[] objective,
        bool onObjective,
        bool pressForward)
    {
        if (objective.Length == 0 || onObjective)
            return null;

        // Which end of the region is "forward" is chain-derived, not spawn-
        // derived: on a contract that puts a returning body in front of its own
        // spawn, distance-from-home stops meaning depth into their ground.
        //
        // Crowding rides on top of that, and only on top: the goal set is still
        // every tile of the contested region, and the route is still chosen by
        // step count first, so this decides WHICH tile of the objective this
        // body takes - the one whose bearing nobody is covering and whose firing
        // lane no ally is already standing in - and never whether to take one.
        // With a single companion the term is close to inert; it is the
        // five-body case it exists for, where the shortest route puts everybody
        // on the same two tiles and one bolt line covers the column.
        return Walk(
            lens,
            context,
            move,
            steps,
            blocked,
            threatened,
            objective.ToHashSet(),
            tile => (pressForward
                    ? -lens.Forwardness(tile)
                    : lens.Forwardness(tile))
                * 2
                + Bearings.Crowding(
                    lens,
                    _stances,
                    tile,
                    _focus,
                    _allies,
                    _covered),
            $"entering the contested position [{_ratchet.Current}]");
    }

    private GenericActorDecision? Walk(
        MatchLens lens,
        GenericActorContext context,
        GenericActorActionLegality move,
        IReadOnlySet<Direction> steps,
        IReadOnlySet<Position> blocked,
        IReadOnlySet<Position> threatened,
        IReadOnlySet<Position> goals,
        Func<Position, int> preference,
        string reason)
    {
        if (goals.Count == 0)
            return null;
        Field.Reach reach = Reachable(lens, context, blocked, threatened, steps);
        Position? goal = Pick(reach, goals, preference);
        if (goal is null)
        {
            reach = Field.Explore(
                lens,
                context.Self.Position,
                blocked,
                RouteSteps(steps),
                RouteOrder);
            goal = Pick(reach, goals, preference);
        }
        if (goal is null)
        {
            // Nothing in the goal set is reachable: close the distance instead.
            Position focus = goals
                .OrderBy(tile => tile.ChebyshevDistance(context.Self.Position))
                .ThenBy(tile => tile.Y)
                .ThenBy(tile => tile.X)
                .First();
            goal = reach.Distance.Keys
                .OrderBy(tile => tile.ChebyshevDistance(focus))
                .ThenBy(tile => reach.Distance[tile])
                .ThenBy(tile => tile.Y)
                .ThenBy(tile => tile.X)
                .Cast<Position?>()
                .FirstOrDefault();
        }
        if (goal is not Position destination
            || !reach.FirstStep.TryGetValue(destination, out Direction step))
        {
            return null;
        }
        return _kin?.Step(context, move, step, reason)
            ?? Field.Step(move, step, reason);
    }

    private static Position? Pick(
        Field.Reach reach,
        IReadOnlySet<Position> goals,
        Func<Position, int> preference) =>
        goals
            .Where(reach.Distance.ContainsKey)
            .OrderBy(tile => reach.Distance[tile])
            .ThenBy(preference)
            .ThenBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .Cast<Position?>()
            .FirstOrDefault();

    private Field.Reach Reachable(
        MatchLens lens,
        GenericActorContext context,
        IReadOnlySet<Position> blocked,
        IReadOnlySet<Position> threatened,
        IReadOnlySet<Direction> steps)
    {
        var avoid = blocked.ToHashSet();
        avoid.UnionWith(threatened);
        if (_dodgeOrigin is Position origin
            && context.Tick <= _avoidDodgeOriginThrough)
        {
            avoid.Add(origin);
        }
        return Field.Explore(
            lens,
            context.Self.Position,
            avoid,
            RouteSteps(steps),
            RouteOrder);
    }

    /// <summary>
    /// First steps a route may plan on. A facing-locked profile offers only the
    /// current facing to a movement action, but every cardinal is still
    /// physically reachable - it just costs a rotation first. Planning against
    /// the legality mask there would make three quarters of the map look
    /// unreachable and pin the body facing whichever way it happened to spawn;
    /// the route is planned on the geometry and paid for at emit time.
    /// </summary>
    private IReadOnlySet<Direction> RouteSteps(IReadOnlySet<Direction> steps) =>
        _kin is { MoveLocked: true } ? Field.Steps.ToHashSet() : steps;

    /// <summary>
    /// Tie-break order for route search. Randomising equal-length ties is what
    /// keeps a mirror-symmetric map fair, but on a locked profile it would also
    /// let two equally short routes alternate every tick and spend the whole
    /// match rotating between them, so there the current facing is tried first
    /// and the randomised laterals only break the remaining ties.
    /// </summary>
    private Direction[] RouteOrder =>
        _kin is { MoveLocked: true }
            ? [
                _kin.Facing,
                .. _order.Where(direction => direction != _kin.Facing),
            ]
            : _order;

    private GenericActorDecision? Evade(
        MatchLens lens,
        GenericActorContext context,
        GenericActorActionLegality move,
        IReadOnlySet<Direction> steps,
        IReadOnlySet<Position> blocked,
        IReadOnlySet<Position> threatened,
        IReadOnlySet<Position> objectiveTiles,
        bool onObjective)
    {
        Direction? chosen = null;
        int bestScore = int.MaxValue;
        Position anchor = _ledger.ExchangeAnchor(lens, context);
        // Only directions the mask actually offers: under a facing-locked
        // profile a rotation is not a dodge, it is a tick spent standing in
        // the bolt's path.
        foreach (Direction direction in _order)
        {
            if (!steps.Contains(direction))
                continue;
            (int dx, int dy) = direction.Vector();
            Position destination = context.Self.Position.Offset(dx, dy);
            if (lens.IsWall(destination)
                || blocked.Contains(destination)
                || threatened.Contains(destination))
            {
                continue;
            }

            int score = 0;
            if (onObjective && !objectiveTiles.Contains(destination))
                score += 20;
            if (objectiveTiles.Count > 0)
            {
                score += objectiveTiles
                    .Min(tile => tile.ChebyshevDistance(destination));
            }
            score += Field.Covered(
                lens,
                context,
                destination,
                _ledger.KnownEnemyTiles) * 4;
            // Dodging into a lane something is already aimed down, or onto a
            // ray an ally is standing on, is a dodge that buys one tick.
            score += Bearings.Crowding(
                lens,
                _stances,
                destination,
                _focus,
                _allies,
                _covered);
            score += _kin?.TurnAwayPenalty(
                context.Self.Position,
                direction,
                anchor,
                2) ?? 0;
            if (lens.IsBankSlot)
            {
                // Do not dodge backwards into the tiles our own arrivals are
                // about to appear on - a returning body reserves them, and on a
                // rallying contract they are the objective one step behind the
                // front rather than a pad nobody contests.
                score += Math.Max(
                    0,
                    6 - destination.ChebyshevDistance(lens.RearAnchor(context)));
            }
            if (score < bestScore)
            {
                bestScore = score;
                chosen = direction;
            }
        }
        return chosen is Direction step
            ? Field.Step(move, step, "stepping off the bolt's path")
            : null;
    }

    private static List<GenericActorContext.ObservedEnemyState> Targets(
        MatchLens lens,
        GenericActorContext context)
    {
        GenericActorRulesContract.AttackProfile? profile =
            lens.Attack(context.Self.FormId);
        int damage = profile?.Projectile.DamagePerHit ?? 1;
        return context.Enemies
            .OrderBy(enemy => enemy.Health <= damage ? 0 : 1)
            .ThenBy(enemy => lens.IsEnemyBankUnit(enemy.ActorId.UnitId) ? 0 : 1)
            .ThenBy(enemy => enemy.Health)
            .ThenBy(enemy =>
                context.Self.Position.ChebyshevDistance(enemy.Position))
            .ThenBy(enemy => enemy.ActorId)
            .ToList();
    }

    private static int Nearest(
        GenericActorContext context,
        List<GenericActorContext.ObservedEnemyState> targets) =>
        targets.Count == 0
            ? int.MaxValue
            : targets.Min(enemy =>
                context.Self.Position.ChebyshevDistance(enemy.Position));

    private static bool FrontManned(
        MatchLens lens,
        GenericActorContext context,
        IReadOnlySet<Position> objectiveTiles)
    {
        if (objectiveTiles.Count == 0)
            return false;
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (lens.IsAlliedBankUnit(ally.ActorId.UnitId))
                continue;
            if (objectiveTiles.Any(tile =>
                    tile.ChebyshevDistance(ally.Position) <= 2))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Ticks a slot spends unavailable after the body in it dies, read from the
    /// slot's own lifecycle profile. Zero for a slot the roster does not name.
    /// </summary>
    private static int RebuildCost(
        MatchLens lens,
        GenericActorActionArgument.UnitTarget target)
    {
        if (target.TeamId != lens.TeamId)
            return 0;
        foreach (MatchLens.SlotRole slot in lens.OwnSlots)
        {
            if (slot.UnitId == target.UnitId)
                return slot.RebuildDelayTicks;
        }
        return 0;
    }

    private static bool HasReadySlot(
        MatchLens lens,
        GenericActorContext context) =>
        context.TeamUnits.Any(slot =>
            slot.TeamId == lens.TeamId
            && slot.State.Kind == GenericActorContext.UnitSlotStateKind.Ready);

    private static GenericActorActionLegality? Rotation(
        MatchLens lens,
        GenericActorContext context,
        Direction facing)
    {
        HashSet<string> ids = lens.Contract.Rules.Actions
            .Where(action =>
                action.Kind == GenericActorRulesContract.ActionKind.Rotation)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        return context.ActionLegalities
            .Where(action =>
                action.Available
                && ids.Contains(action.ActionId)
                && action.Constraints
                    .OfType<GenericActorActionLegality.ArgumentConstraint
                        .DirectionConstraint>()
                    .Any(constraint =>
                        constraint.AllowedValues.Contains(facing)))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool Endgame(MatchLens lens, GenericActorContext context)
    {
        long mine = lens.Score(context, lens.TeamId);
        long best = context.Scoreboard.Teams
            .Where(team => team.TeamId != lens.TeamId)
            .Select(team => lens.Score(context, team.TeamId))
            .DefaultIfEmpty(0)
            .Max();
        int remaining = lens.MaxTicks - context.Tick;
        return mine <= best && remaining <= lens.CaptureThreshold * 3;
    }

    private static bool Before(Position left, Position right) =>
        left.Y < right.Y || (left.Y == right.Y && left.X < right.X);

    private static GenericActorDecision Fallback(
        GenericActorContext context,
        string reason)
    {
        foreach (GenericActorActionLegality action in context.ActionLegalities)
        {
            if (action.Available
                && string.Equals(
                    action.ActionId,
                    "wait",
                    StringComparison.Ordinal))
            {
                return GenericActorDecision.WithoutArguments(
                    action.ActionId,
                    action.ActionCode,
                    reason);
            }
        }

        foreach (GenericActorActionLegality action in context.ActionLegalities
                     .Where(entry => entry.Available)
                     .OrderBy(entry => entry.ActionCode))
        {
            var arguments = new List<GenericActorActionArgument>();
            bool usable = true;
            foreach (GenericActorActionLegality.ArgumentConstraint constraint
                     in action.Constraints)
            {
                switch (constraint)
                {
                    case GenericActorActionLegality.ArgumentConstraint
                        .ShotProgramConstraint:
                        break;
                    case GenericActorActionLegality.ArgumentConstraint
                        .DirectionConstraint direction
                        when !direction.AllowedValues.IsEmpty:
                        arguments.Add(
                            new GenericActorActionArgument.DirectionArgument(
                                direction.AllowedValues[0]));
                        break;
                    case GenericActorActionLegality.ArgumentConstraint
                        .UnitTargetConstraint unit
                        when !unit.AllowedValues.IsEmpty:
                        arguments.Add(
                            new GenericActorActionArgument.UnitTargetArgument(
                                unit.AllowedValues[0]));
                        break;
                    case GenericActorActionLegality.ArgumentConstraint
                        .FormTargetConstraint form
                        when !form.AllowedFormIds.IsEmpty:
                        arguments.Add(
                            new GenericActorActionArgument.FormTargetArgument(
                                form.AllowedFormIds[0]));
                        break;
                    case GenericActorActionLegality.ArgumentConstraint
                        .ProjectileHeadingConstraint heading
                        when !heading.AllowedValues.IsEmpty:
                        arguments.Add(
                            new GenericActorActionArgument
                                .ProjectileHeadingArgument(
                                heading.AllowedValues[0]));
                        break;
                    default:
                        usable = false;
                        break;
                }
                if (!usable)
                    break;
            }
            if (usable)
            {
                return new GenericActorDecision(
                    action.ActionId,
                    action.ActionCode,
                    arguments,
                    reason);
            }
        }

        // Last resort: the template's own never-throw wait resolution.
        return context.ActionLegalities.IsEmpty
            ? new GenericActorDecision("wait", 0, [], reason)
            : ArenaBasics.Wait(context, reason);
    }
}
