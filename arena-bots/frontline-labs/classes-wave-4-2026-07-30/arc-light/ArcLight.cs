using BotArena.Sdk;

/// <summary>
/// arc-light — a striker whose identity is the skill, not a chassis with a skill
/// bolted on.
///
/// <para>One artifact plays every cell it is measured in because it asks the
/// contract four questions and never assumes an answer: is there a stance route
/// out of this form, and what does its budget cost; how deep may this gun bend;
/// does a step turn the body; and what is a completed capture actually worth
/// right now. In the kit-off cells the stance code declines because no route
/// exists. In the classless qualification profile the same code finds an anchor
/// route instead, fabricates from the pad, and never touches a volley.</para>
///
/// <para>Every active body life gets its own instance of this class with empty
/// private memory, so coordination is derived from the shared observation —
/// rank among the team's active bodies — and never from hidden state.</para>
/// </summary>
public sealed class ArcLight : IGenericActorBot
{
    private ArcFacts? _facts;
    private string _formLastTick = string.Empty;
    private int _formSince;

    public void StartLife(GenericActorMatchStart start)
    {
        _facts = new ArcFacts(
            start.Contract,
            start.ActorId.TeamId,
            start.ParticipantId);
        _formLastTick = string.Empty;
        _formSince = 0;
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        ArcFacts facts = _facts
            ?? throw new InvalidOperationException("StartLife was not called.");

        if (!string.Equals(
                _formLastTick,
                context.Self.FormId,
                StringComparison.Ordinal))
        {
            _formLastTick = context.Self.FormId;
            _formSince = context.Tick;
        }

        // A windup that permits nothing but waiting is not a decision point.
        // Submitting anything else is a blocked action and a wasted tick.
        if (context.Self.PendingSameLifeTransition is not null)
            return ArenaBasics.Wait(context, "committed to a windup");

        var threat = new ArcThreat(facts, context);
        var gun = new ArcGun(facts, context, threat);
        var keel = new ArcKeel(facts, context);
        var stance = new ArcStance(facts, context, threat, gun);

        if (stance.InStance)
        {
            return stance.Act(context.Tick - _formSince)
                ?? ArenaBasics.Wait(context, "holding the arc");
        }

        // Standing on a lane an enemy gun already owns is a cost, so it is a
        // sort key on every tile choice: a fan's three headings weigh most, an
        // ordinary gun's bend envelope still weighs something. Flanking beats a
        // locked arc, and this is where that preference lives.
        HashSet<Position> fanLanes = threat.FanLanes();
        HashSet<Position> hot = [.. fanLanes, .. threat.EnemyLanes()];
        HashSet<Position> blocked = threat.BlockedNow();
        Direction[] order = ArenaBasics.OrderedDirections(facts.Contract, context);
        Position[] goals = keel.Goals(Rank(context), hot);
        (int Ticks, int Damage)? incoming = threat.Incoming(context.Self.Position);

        // 1. A bolt that kills outweighs everything, including the objective:
        //    arrivals rally forward on this contract, but a dead body still
        //    stops contributing weight for the whole respawn clock.
        if (incoming is { } lethal
            && lethal.Damage >= context.Self.Health
            && lethal.Ticks <= 2
            && ArcMove.Escape(facts, context, threat, goals, blocked)
                is GenericActorDecision flee)
        {
            return flee;
        }

        // 2. More bodies is more objective weight, and on a contract where
        //    surplus weight scales capture pressure that is the cheapest
        //    pressure available. The legality mask owns the preconditions.
        if (ArenaBasics.TryFabricateReady(facts.Contract, context)
            is GenericActorDecision fabricate)
        {
            return fabricate;
        }

        ArcGun.Shot? shot = gun.Best();
        bool onStation = goals.Length == 0 || goals.Contains(context.Self.Position);

        // 3. Fire when the bolt is worth more than the step. Standing on the
        //    ground we want, a shot is nearly free. Walking to it, only a near
        //    certain interception is — and when this team holds NO objective
        //    weight at all, nothing is: an unheld objective is worth more than
        //    any exchange, and answering suppression with return fire instead of
        //    with a step is how a body never arrives.
        int fireBar = onStation ? 8 : keel.OwnWeight == 0 ? 400 : 55;
        if (shot is not null && shot.Score >= fireBar)
            return shot.Decision;

        // 4. The cast. Priced inside TryEnter against the aimed shot it replaces.
        if (stance.TryEnter(keel, shot) is GenericActorDecision cast)
            return cast;

        // 4b. The cast has a position. Objective tiles carry the map's
        //     transition-forbidden tag, so a fan is cast from the shoulder beside
        //     the objective, where three headings still rake its tile cluster.
        //     The trip is affordable exactly when the contract's own capture
        //     arithmetic says the objective cannot change hands while it lasts.
        // Never trade HELD ground for a cast. Measured: every configuration that
        // let a body standing on the objective walk to a cast tile lost to an
        // otherwise identical build that never casts, because a striker searching
        // its whole bend envelope delivers three AIMED bolts in the time a cast
        // delivers three unaimed ones. What survives measurement is a cast that
        // costs no ground: taken by a body that is not on the objective, and only
        // when the ordinary gun has no arc at all from here.
        bool gunBlind = shot is null
            && gun.LaneValue(context.Self.Facing, context.Self.Position, 0) == 0;
        bool massed = context.Enemies.Count(enemy =>
            context.Self.Position.ChebyshevDistance(enemy.Position) <= 5) >= 2;
        ArcStance.CastPost? post = !keel.SelfPresent && (gunBlind || massed)
            ? stance.BestPost(radius: 2, hot, blocked)
            : null;
        if (post is not null
            && post.Tile != context.Self.Position
            && post.Value >= 2
            && keel.AffordableAbsence(
                (post.Steps * 2) + 3,
                facts.ObjectiveWeight(context.Self.FormId))
            && ArcMove.Toward(
                    facts,
                    context,
                    threat,
                    [post.Tile],
                    blocked,
                    order)
                is GenericActorDecision reposition)
        {
            return reposition;
        }

        // 4c. Objective-preserving evasion: when a bolt is coming and this body's
        //     weight is holding the claim, the answer is a step to ANOTHER tile of
        //     the same objective. Presence is kept, the bolt is not eaten, and the
        //     claim never notices.
        if (incoming is { } onObjective
            && onObjective.Ticks <= 2
            && keel.SelfPresent
            && keel.ActiveTiles.Length > 1
            && ArcMove.Escape(
                    facts,
                    context,
                    threat,
                    keel.ActiveTiles,
                    blocked,
                    keel.ActiveTiles.ToHashSet())
                is GenericActorDecision shuffle)
        {
            return shuffle;
        }

        // 5. Ordinary evasion, but only when this body's presence is not the
        //    thing holding the claim. Leaving an objective no enemy stands alone
        //    on costs nothing here; leaving a contested one costs the claim.
        if (incoming is { } arriving
            && arriving.Ticks <= 2
            && !keel.PresenceIsLoadBearing
            && ArcMove.Escape(facts, context, threat, goals, blocked)
                is GenericActorDecision sidestep)
        {
            return sidestep;
        }

        // 5b. Unmask a lane without giving up the ground. The gun has no initial
        //     aim offset in this arm, so a diagonally adjacent body is simply
        //     unhittable; one step inside the objective cluster fixes that and
        //     keeps every point of objective weight.
        if (shot is null
            && onStation
            && ArcMove.Unmask(facts, context, gun, threat, goals.ToHashSet(), blocked)
                is GenericActorDecision unmask)
        {
            return unmask;
        }

        // 6. Take the ground.
        if (ArcMove.Toward(facts, context, threat, goals, blocked, order)
            is GenericActorDecision advance)
        {
            return advance;
        }

        // 7. Fortify only behind relief (declines on every class arm, because a
        //    striker has no weight-shedding route).
        if (stance.TryFortify(keel) is GenericActorDecision fortify)
            return fortify;

        // 8. Facing is a resource: it is this body's sight cone, its aim, and —
        //    under a facing-locked coupling — the only direction it may walk.
        if (Reorient(facts, context, gun, keel, goals, hot)
            is GenericActorDecision turn)
        {
            return turn;
        }

        if (shot is not null)
            return shot.Decision;
        // The wait carries WHY: the objective intent, and the priced reason the
        // cast declined. Bounded diagnostic output only, but it is what turns
        // "the bot ignored the skill" into a number.
        return ArenaBasics.Wait(
            context,
            $"holding station ({keel.Decision}/{stance.Veto}/post{post?.Value ?? -99})");
    }

    /// <summary>
    /// This body's index among its team's active bodies, ordered by stable slot
    /// then life. Derived entirely from the shared observation, so independent
    /// instances agree without shared memory and spread across the objective
    /// instead of stacking into one lane.
    /// </summary>
    private static int Rank(GenericActorContext context)
    {
        int rank = 0;
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (ally.ActorId.CompareTo(context.Self.ActorId) < 0)
                rank++;
        }
        return rank;
    }

    /// <summary>
    /// Spend a rotation when it buys a shot next tick, or when nothing is
    /// visible and the body is facing away from where the fight is. Under a
    /// facing-locked coupling this is the cheapest way to convert a wasted tick
    /// into either vision or a threat.
    /// </summary>
    private static GenericActorDecision? Reorient(
        ArcFacts facts,
        GenericActorContext context,
        ArcGun gun,
        ArcKeel keel,
        Position[] goals,
        IReadOnlySet<Position> hot)
    {
        if (gun.BestRotation() is { } aim && aim.Score >= 40)
        {
            return ArcMove.Rotate(
                facts,
                context,
                aim.Facing,
                "swinging the gun onto a lane");
        }

        // Pre-aim while the cooldown drains. Availability decides whether a bolt
        // leaves this tick; it does not decide where to point, and a facing-locked
        // body that turns late has already lost the exchange.
        if (!context.Enemies.IsEmpty)
        {
            int current = gun.LaneValue(
                context.Self.Facing,
                context.Self.Position,
                ticksFromNow: 0);
            Direction? swing = null;
            int bestLane = current;
            foreach (Direction facing in ArcBoard.Cardinals)
            {
                if (facing == context.Self.Facing)
                    continue;
                int value = gun.LaneValue(
                    facing,
                    context.Self.Position,
                    ticksFromNow: 1);
                if (value > bestLane)
                {
                    bestLane = value;
                    swing = facing;
                }
            }
            if (swing is Direction lane
                && ArcMove.Rotate(facts, context, lane, "pre-aiming the lane")
                    is GenericActorDecision preaim)
            {
                return preaim;
            }
            // Nothing to aim at from here. If this tile is inside an enemy arc
            // and stepping off it is free, take the flank instead of the trade.
            if (hot.Contains(context.Self.Position) && keel.LeavingIsFree)
                return null;
            return null;
        }

        // Look where the opposition must come from: down the chain toward their
        // side, derived from the objective order rather than from a spawn.
        Direction? forward = ArenaBasics.AdvanceDirection(facts.Contract, context);
        Position anchor = keel.ActiveTiles.Length > 0
            ? keel.ActiveTiles[0]
            : goals.Length > 0
                ? goals[0]
                : context.Self.Position;
        Direction watch = anchor == context.Self.Position
            ? forward ?? context.Self.Facing
            : Math.Abs(anchor.X - context.Self.Position.X)
                >= Math.Abs(anchor.Y - context.Self.Position.Y)
                ? anchor.X > context.Self.Position.X
                    ? Direction.East
                    : Direction.West
                : anchor.Y > context.Self.Position.Y
                    ? Direction.South
                    : Direction.North;
        return ArcMove.Rotate(facts, context, watch, "watching the approach");
    }
}
