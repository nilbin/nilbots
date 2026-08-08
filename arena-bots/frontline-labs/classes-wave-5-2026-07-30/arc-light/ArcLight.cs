using BotArena.Sdk;

/// <summary>
/// arc-light — a striker whose identity is the skill, not a chassis with a skill
/// bolted on.
///
/// <para>One artifact plays every cell it is measured in because it asks the
/// contract, every tick, what it is actually holding: is there a stance route out
/// of this form and what does its budget cost; how far off facing may this gun
/// launch and how deep may it bend; does a step turn the body; which enemy form
/// can create more bodies; and what is a completed capture worth right now. In
/// the kit-off cells the stance code declines because no route exists. In the
/// classless qualification profile the same code finds an anchor route instead,
/// fabricates from the pad, and never touches a volley.</para>
///
/// <para>Wave-5 revision. The one broken leg was the swarm matchup, and the fix
/// was not more aggression — it was three contract facts the doctrine had been
/// getting from the wrong place. The gun's launch offsets are read from the shot
/// program instead of assumed to be the straight lane, so the diagonal blind spot
/// the fan used to exist for is closed by the GUN and the fan is freed for what
/// it is actually good at. The cast's legality is read from the stance route's
/// own placement instead of the map's tag, so the fan may rise on the objective
/// it is denying. And the cast is priced in bodies against the contract's own
/// tempo arithmetic instead of in lane crossings, which is what makes it the
/// right answer against four bodies and the wrong one against a duel.</para>
///
/// <para>Every active body life gets its own instance of this class with empty
/// private memory, so coordination is derived from the shared observation — rank
/// among the team's active bodies — and never from hidden state. The little that
/// IS remembered lives exactly one life and is declared as such.</para>
/// </summary>
public sealed class ArcLight : IGenericActorBot
{
    private ArcFacts? _facts;
    private ArcMemory _memory = new();
    private string _formLastTick = string.Empty;
    private int _formSince;

    public void StartLife(GenericActorMatchStart start)
    {
        _facts = new ArcFacts(
            start.Contract,
            start.ActorId.TeamId,
            start.ParticipantId);
        _memory = new ArcMemory();
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
        _memory.Observe(context);

        // A windup that permits nothing but waiting is not a decision point.
        // Submitting anything else is a blocked action and a wasted tick.
        if (context.Self.PendingSameLifeTransition is not null)
            return ArenaBasics.Wait(context, "committed to a windup");

        var threat = new ArcThreat(facts, context, _memory);
        var gun = new ArcGun(facts, context, threat);
        var keel = new ArcKeel(facts, context);
        var stance = new ArcStance(facts, context, threat, gun, _memory);

        if (stance.InStance)
        {
            return stance.Act(context.Tick - _formSince)
                ?? ArenaBasics.Wait(context, "holding the arc");
        }

        // Standing on a lane an enemy gun already owns is a cost, so it is a
        // sort key on every tile choice: a fan's three headings weigh most, an
        // ordinary gun's aim-and-bend envelope still weighs something. Flanking
        // beats a locked arc, and this is where that preference lives.
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
        //    pressure available. The legality mask owns the preconditions, so
        //    this declines silently on a chassis whose children arrive by
        //    themselves.
        if (ArenaBasics.TryFabricateReady(facts.Contract, context)
            is GenericActorDecision fabricate)
        {
            return fabricate;
        }

        ArcGun.Shot? shot = gun.Best();
        bool onStation = goals.Length == 0 || goals.Contains(context.Self.Position);

        // MEASURED, and it is the one place this doctrine still refuses its own
        // signature move: a cast that PRE-EMPTS an available aimed bolt loses,
        // even when the fan is forecast to connect with strictly more bodies than
        // the contract's break-even count. 16 seeds against the same swarm, the
        // same build with the ordering flipped: 13-3 and +16.19 with the premium
        // cast ahead of the gun, 15-1 and +23.38 with it behind. One bolt now
        // beats three bolts in four ticks, because the four ticks are immobile and
        // the swarm is still walking. The cast keeps its place BELOW the shot.

        // 3. Fire when the bolt is worth more than the step. Standing on the
        //    ground we want, a shot is nearly free. Walking to it, only a near
        //    certain interception is — and the bar rises with the objective
        //    deficit, because on a surplus-scaling contract every tick spent
        //    outnumbered on the point is a full point of progress against us and
        //    a body of ours in the right place cancels exactly one of them.
        //    Answering that with return fire instead of with a step is how a
        //    three-slot chassis loses to a four-slot one while winning the
        //    shooting.
        int fireBar = onStation
            ? 8
            : keel.OwnWeight == 0
                ? 400
                : 55 + (keel.Deficit * 60);
        if (shot is not null && shot.Score >= fireBar)
            return shot.Decision;

        // 4. The cast, priced inside TryEnter against the gun it replaces and
        //    against the contract's own tempo arithmetic. Open ground means it is
        //    cast from where the body already stands — including the objective —
        //    so it never trades presence for position any more.
        if (stance.TryEnter(keel, shot) is GenericActorDecision cast)
        {
            _memory.RecordCast(context.Self.Position, context.Tick);
            return cast;
        }

        // 4b. A fan aims by facing and a stance tick cannot dodge, so when the
        //     only thing between here and a qualifying cast is a bearing, buy the
        //     bearing on this tick — while the body can still step — rather than
        //     inside the windup.
        if (onStation
            && shot is null
            && stance.CastBearing() is Direction bearing
            && ArcMove.Rotate(facts, context, bearing, "lining up the fan")
                is GenericActorDecision aimFan)
        {
            return aimFan;
        }

        // 4bb. A deflecting stance, on a chassis that has one. Declines on every
        //      striker form because no striker route leads to a guarding form; it
        //      is here because the artifact is measured on chassis it was not
        //      written for, and because a shield raised into an arriving bolt
        //      turns a poke into the poker's own bolt.
        if (stance.TryGuard() is GenericActorDecision shield)
            return shield;

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

        // 5b. Unmask a lane without giving up the ground. One step inside the
        //     objective cluster can turn an unreachable bearing into a lane, and
        //     with the launch offsets restored the search knows about the
        //     diagonals it is buying.
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

        // 6b. On station with nothing to shoot and guns pointed at this tile:
        //     do not stand in the bearing. A kite step keeps a diagonal lane on
        //     the enemy while leaving the tile it has already ranged, and it is
        //     restricted to the objective whenever presence is load-bearing so it
        //     never pays for tempo with ground.
        if (shot is null
            && (hot.Contains(context.Self.Position)
                || threat.Bearing(context.Self.Position) > 0)
            && ArcMove.Kite(
                    facts,
                    context,
                    gun,
                    threat,
                    hot,
                    blocked,
                    keel.PresenceIsLoadBearing
                        ? keel.ActiveTiles.ToHashSet()
                        : null)
                is GenericActorDecision kite)
        {
            return kite;
        }

        // 7. Fortify only behind relief (declines on every class arm in which
        //    this chassis has no weight-shedding route).
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
            $"holding station ({keel.Decision}/{stance.Veto}/d{keel.Deficit})");
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
    /// into either vision or a threat — and with the launch offsets restored, the
    /// lane a facing owns is three headings wide, so the swing is worth more than
    /// it used to be and the pre-aim search finally prices it that way.
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
