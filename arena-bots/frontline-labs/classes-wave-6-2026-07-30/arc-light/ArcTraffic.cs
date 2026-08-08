using BotArena.Sdk;

/// <summary>
/// arc-light's own bodies, modelled as traffic.
///
/// <para>This is the whole wave-6 change and it rests on one contract fact: a
/// life never sees an ally's current decision, but every life on the team
/// receives the SAME frozen observation. So any function of that observation is
/// a shared answer — no hidden state, no parent-memory copy, no negotiation
/// protocol. Precedence between two of my bodies is therefore not something
/// they agree on; it is something they both compute, and they cannot disagree.
/// That is what makes a right-of-way rule implementable in a contract where
/// coordination is explicitly forbidden to be stateful.</para>
///
/// <para>A body's <b>committed route</b> comes from the same place. Under a
/// facing-locked coupling the movement legality mask offers only the current
/// facing, so a body that still has ground to cover needs exactly one tile next
/// tick: the one it is facing. Under any other coupling that inference is not
/// available, and this class says so rather than inventing it — claims collapse
/// to the body's own tile, precedence still resolves destinations, and nothing
/// is asserted that the contract did not publish.</para>
/// </summary>
internal sealed class ArcTraffic
{
    /// <summary>One of my own bodies, with everything precedence needs.</summary>
    private readonly record struct Body(
        ActorIdentity ActorId,
        Position Position,
        Direction Facing,
        string FormId,
        bool Frozen,
        int Distance,
        bool InChoke,
        bool IsSelf);

    private readonly ArcFacts _facts;
    private readonly GenericActorContext _context;
    private readonly List<Body> _bodies = [];
    private readonly HashSet<Position> _yieldNow = [];
    private readonly HashSet<Position> _yieldSoon = [];
    private readonly HashSet<int> _yieldChokeRuns = [];
    private readonly HashSet<Position> _reserved = [];
    private readonly List<(Position Tile, int DueIn)> _rally = [];
    private readonly List<HashSet<Position>> _fanPoses = [];
    private Body _self;

    public ArcTraffic(ArcFacts facts, GenericActorContext context)
    {
        _facts = facts;
        _context = context;

        Position[] front = context.Mode
            is GenericActorContext.ModeObservationState.Frontline mode
            ? facts.ObjectiveTiles(mode.ActivePositionIndex)
            : [];

        _self = Describe(
            context.Self.ActorId,
            context.Self.Position,
            context.Self.Facing,
            context.Self.FormId,
            context.Self.PendingSameLifeTransition is not null,
            front,
            isSelf: true);
        _bodies.Add(_self);
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            _bodies.Add(Describe(
                ally.ActorId,
                ally.Position,
                ally.Facing,
                ally.FormId,
                ally.PendingSameLifeTransition is not null,
                front,
                isSelf: false));
        }

        BuildYields();
        BuildLifecycleTraffic();
        BuildFanPoses();
    }

    /// <summary>Own bodies other than this one that are currently alive.</summary>
    public int Siblings => _bodies.Count - 1;

    /// <summary>
    /// Diagnostic: why a coordination gate last refused something. Bounded
    /// string, never read by a decision.
    /// </summary>
    public string Note { get; private set; } = "clear";

    private Body Describe(
        ActorIdentity actorId,
        Position position,
        Direction facing,
        string formId,
        bool frozen,
        Position[] front,
        bool isSelf)
    {
        int distance = front.Length == 0
            ? 0
            : ArcBoard.StepDistance(_facts, position, front, 24) ?? 99;
        return new Body(
            actorId,
            position,
            facing,
            formId,
            frozen,
            distance,
            _facts.IsChoke(position),
            isSelf);
    }

    /// <summary>
    /// The written precedence order, lower is stronger. Every term is a fact
    /// from the shared observation, so two siblings never compute it
    /// differently, and the last term guarantees it is total — which is what
    /// stops "both yield" from being the new deadlock.
    /// </summary>
    private static (int, int, int, int, int) Rank(Body body) =>
        (
            body.Frozen ? 0 : 1,
            body.InChoke ? 0 : 1,
            body.Distance,
            body.ActorId.UnitId,
            body.ActorId.LifeId);

    private static bool Outranks(Body a, Body b) =>
        Rank(a).CompareTo(Rank(b)) < 0;

    /// <summary>
    /// The tiles a body's committed route needs, in order, for the next two
    /// ticks. Empty for a body that has arrived (it is not going anywhere, and
    /// treating its facing as a claim would block a sibling for nothing) and for
    /// a frozen body (it cannot move at all; its own tile is already an
    /// obstacle every consumer knows about).
    /// </summary>
    private List<Position> Route(Body body)
    {
        var route = new List<Position>();
        if (body.Frozen || body.Distance == 0)
            return route;
        if (!_facts.FacingLocked(body.FormId))
            return route;
        Position cursor = body.Position;
        for (int step = 0; step < 2; step++)
        {
            cursor = ArcBoard.Step(cursor, body.Facing);
            if (_facts.Impassable(cursor))
                break;
            route.Add(cursor);
        }
        return route;
    }

    private void BuildYields()
    {
        foreach (Body body in _bodies)
        {
            if (body.IsSelf || !Outranks(body, _self))
                continue;
            List<Position> route = Route(body);
            for (int index = 0; index < route.Count; index++)
            {
                if (index == 0)
                    _yieldNow.Add(route[index]);
                else
                    _yieldSoon.Add(route[index]);
            }
            // C2: the corridor run a stronger body stands in, or is entering,
            // belongs to it until it is out.
            int run = _facts.ChokeRun(body.Position);
            if (run >= 0)
                _yieldChokeRuns.Add(run);
            foreach (Position tile in route)
            {
                int entering = _facts.ChokeRun(tile);
                if (entering >= 0)
                    _yieldChokeRuns.Add(entering);
            }
        }
    }

    /// <summary>
    /// Tiles a new body of mine is already committed to, and tiles the next
    /// arrival will take. Both are contract reads: a pending fabrication or
    /// replication publishes its <c>ReservedPosition</c>, and a forward rally
    /// fills the own-side objective region rear-most-first along this team's
    /// advance direction, so the slot clocks say when and the chain says where.
    /// </summary>
    private void BuildLifecycleTraffic()
    {
        Position[] order = _facts.RallyOrder(_context);
        foreach (GenericActorContext.ObservedUnitSlot slot in _context.TeamUnits)
        {
            if (slot.TeamId != _facts.TeamId)
                continue;
            switch (slot.State)
            {
                case GenericActorContext.UnitSlotState.LifecyclePending pending:
                    _reserved.Add(pending.ReservedPosition);
                    break;
                case GenericActorContext.UnitSlotState.AvailabilityPending wait
                    when order.Length > 0:
                    Add(order, wait.DueTick - _context.Tick);
                    break;
                case GenericActorContext.UnitSlotState.AutomaticReturnPending back
                    when order.Length > 0:
                    Add(order, back.DueTick - _context.Tick);
                    break;
            }
        }

        void Add(Position[] tiles, int dueIn)
        {
            if (dueIn < 0)
                return;
            // Only the FIRST free tile of the fill order is the one an arrival
            // will actually take, and "free" is judged against the bodies that
            // are standing there now — which is exactly the influence this rule
            // exists to spend well.
            foreach (Position tile in tiles)
            {
                if (Occupied(tile) && !tile.Equals(_context.Self.Position))
                    continue;
                _rally.Add((tile, dueIn));
                return;
            }
        }
    }

    private bool Occupied(Position tile)
    {
        foreach (GenericActorContext.ObservedAllyState ally in _context.Allies)
        {
            if (ally.Position.Equals(tile))
                return true;
        }
        foreach (GenericActorContext.ObservedEnemyState enemy in _context.Enemies)
        {
            if (enemy.Position.Equals(tile))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Every fan a visible enemy could launch from one rotation away: for each
    /// enemy that owns, or has a route into, a multi-bolt gun, the tile set of
    /// its declared spread from each cardinal facing. Two of my bodies inside
    /// one of these sets is one enemy cast that meets its own break-even count
    /// on my clumping, which is the exact trade wave 5 taught me to refuse when
    /// I was the one casting.
    /// </summary>
    private void BuildFanPoses()
    {
        if (!ArcRules.FanSpacing)
            return;
        foreach (GenericActorContext.ObservedEnemyState enemy in _context.Enemies)
        {
            string fanForm = FanFormOf(enemy);
            if (fanForm.Length == 0)
                continue;
            GenericActorRulesContract.AttackProfile? attack =
                _facts.Attack(fanForm);
            if (attack is null)
                continue;
            int wing = (attack.ProjectilesPerAttack - 1) / 2;
            if (wing == 0)
                continue;
            int reach = attack.Projectile.MaxTravelTiles;
            foreach (Direction facing in ArcBoard.Cardinals)
            {
                var pose = new HashSet<Position>();
                ProjectileHeading centre = facing.ToProjectileHeading();
                for (int lane = -wing; lane <= wing; lane++)
                {
                    Position cursor = enemy.Position;
                    ProjectileHeading heading = centre.Turned(lane);
                    for (int tile = 0; tile < reach; tile++)
                    {
                        cursor = ArcBoard.Step(cursor, heading);
                        if (_facts.IsWall(cursor))
                            break;
                        pose.Add(cursor);
                    }
                }
                if (pose.Count > 0)
                    _fanPoses.Add(pose);
            }
        }

        // A deflection return is the same shape with one body: a guard sends the
        // bolt back down the ray it arrived on, so the tiles BEHIND an ally that
        // is poking a shell's face are covered by that ally's own shot.
        foreach (GenericActorContext.ObservedEnemyState enemy in _context.Enemies)
        {
            if (!_facts.IsGuardForm(enemy.FormId))
                continue;
            foreach (GenericActorContext.ObservedAllyState ally in _context.Allies)
            {
                if (!ArcBoard.InFacingQuadrant(
                        enemy.Position,
                        enemy.Facing,
                        ally.Position))
                {
                    continue;
                }
                if (!ArcBoard.TryHeading(
                        enemy.Position,
                        ally.Position,
                        out ProjectileHeading heading,
                        out int _))
                {
                    continue;
                }
                var ray = new HashSet<Position>();
                Position cursor = enemy.Position;
                for (int tile = 0; tile < 8; tile++)
                {
                    cursor = ArcBoard.Step(cursor, heading);
                    if (_facts.IsWall(cursor))
                        break;
                    ray.Add(cursor);
                }
                if (ray.Count > 0)
                    _fanPoses.Add(ray);
            }
        }
    }

    private string FanFormOf(GenericActorContext.ObservedEnemyState enemy)
    {
        if (_facts.IsFanForm(enemy.FormId))
            return enemy.FormId;
        if (enemy.PendingSameLifeTransition?.TargetFormId is string pending
            && _facts.IsFanForm(pending))
        {
            return pending;
        }
        // A fan the enemy can still ENTER is worth spacing against, because the
        // entry windup is the only warning there is and it is shorter than the
        // walk out of a shared lane.
        return _facts.FanStanceRoute(enemy.FormId) is { } route
            ? route.TargetFormId
            : string.Empty;
    }

    // ---------------------------------------------------------------- queries

    /// <summary>
    /// C1: may this body step onto <paramref name="tile"/>? False when a
    /// higher-precedence sibling's committed route needs it on the very next
    /// tick — which is the tick a same-destination collision would happen on,
    /// and the collision the engine resolves by blocking BOTH of us.
    /// </summary>
    public bool MayEnter(Position tile)
    {
        if (!ArcRules.YieldPrecedence)
            return true;
        if (_yieldNow.Contains(tile))
        {
            Note = "yield-now";
            return false;
        }
        return true;
    }

    /// <summary>
    /// C1/C2 together, for a step: yields the tile, and refuses to enter a
    /// corridor run a stronger sibling already owns.
    /// </summary>
    public bool MayTravel(Position tile)
    {
        if (!MayEnter(tile))
            return false;
        if (!ArcRules.ChokePrecedence)
            return true;
        int run = _facts.ChokeRun(tile);
        if (run >= 0 && _yieldChokeRuns.Contains(run))
        {
            Note = "choke-owned";
            return false;
        }
        return true;
    }

    /// <summary>
    /// C1: is this body currently standing on a tile a stronger sibling needs
    /// next tick? Then the useful thing to do with the tick is get out of the
    /// way, and every caller that was about to hold station should know.
    /// </summary>
    public bool BlockingSibling =>
        ArcRules.YieldPrecedence && _yieldNow.Contains(_context.Self.Position);

    /// <summary>
    /// C5: may this body become terrain on <paramref name="tile"/> for
    /// <paramref name="ticks"/> ticks? This is the cast gate, and it is the one
    /// question wave 5 never asked. Refused on a corridor run at all — a
    /// doorway held for a stance cycle is a sealed reinforcement route — on a
    /// tile any sibling's committed route needs, and on a tile an arrival is due
    /// to take inside the window.
    /// </summary>
    public bool MayCommit(Position tile, int ticks)
    {
        // C5a: the named gap. A doorway held for a stance cycle is a sealed
        // reinforcement route, and it is sealed against bodies that do not exist
        // yet as surely as against the ones that do — hence PendingBodies.
        if (ArcRules.CastPricesOwnPaths
            && _facts.ChokeRun(tile) >= 0
            && Siblings + PendingBodies > 0)
        {
            Note = "commit-choke";
            return false;
        }
        if (!ArcRules.CastYieldsSiblingRoutes)
            return true;
        if (_yieldNow.Contains(tile) || _yieldSoon.Contains(tile))
        {
            Note = "commit-route";
            return false;
        }
        // Precedence is about who goes first; a commitment is about who is
        // STILL there in four ticks, so it answers to every sibling rather
        // than only to the stronger ones.
        foreach (Body body in _bodies)
        {
            if (body.IsSelf)
                continue;
            foreach (Position step in Route(body))
            {
                if (step.Equals(tile))
                {
                    Note = "commit-route";
                    return false;
                }
            }
        }
        if (_reserved.Contains(tile))
        {
            Note = "commit-reserved";
            return false;
        }
        foreach ((Position rally, int dueIn) in _rally)
        {
            if (rally.Equals(tile) && dueIn <= ticks)
            {
                Note = "commit-rally";
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Slots of mine that hold no body yet but are scheduled to produce one.
    /// A choke sealed against nobody is not sealed, so the cast gate counts
    /// them.
    /// </summary>
    public int PendingBodies
    {
        get
        {
            int pending = 0;
            foreach (GenericActorContext.ObservedUnitSlot slot
                     in _context.TeamUnits)
            {
                if (slot.TeamId != _facts.TeamId)
                    continue;
                if (slot.State.Kind
                        is GenericActorContext.UnitSlotStateKind
                            .AvailabilityPending
                        or GenericActorContext.UnitSlotStateKind
                            .AutomaticReturnPending
                        or GenericActorContext.UnitSlotStateKind
                            .FabricationPending
                        or GenericActorContext.UnitSlotStateKind
                            .ReplicationPending)
                {
                    pending++;
                }
            }
            return pending;
        }
    }

    /// <summary>
    /// C3: is <paramref name="tile"/> a tile a body of mine is about to appear
    /// on within <paramref name="ticks"/> ticks, or one a pending lifecycle
    /// operation has already reserved? Standing there does not merely crowd the
    /// arrival — the rally fill order moves on to the next tile, and when the
    /// region runs out the contract falls back to the home anchor, which is the
    /// far end of the map from the fight.
    /// </summary>
    public bool RallyClear(Position tile, int ticks)
    {
        if (!ArcRules.RallyTraffic)
            return true;
        if (_reserved.Contains(tile))
        {
            Note = "reserved";
            return false;
        }
        foreach ((Position rally, int dueIn) in _rally)
        {
            if (rally.Equals(tile) && dueIn <= ticks)
            {
                Note = "rally-due";
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Every tile adjacent to <paramref name="from"/> that coordination refuses
    /// as a first step, folded into an existing first-step blocker set. Doing it
    /// this way rather than vetoing a chosen step means the route search plans
    /// AROUND the traffic instead of stalling in front of it — a yield that
    /// turns into a wait is still a lost tick, and the whole point of a
    /// precedence rule is that only one of the two bodies loses one.
    /// </summary>
    public void AddRefusedFirstSteps(Position from, HashSet<Position> into)
    {
        foreach (Direction direction in ArcBoard.Cardinals)
        {
            Position tile = ArcBoard.Step(from, direction);
            if (!MayTravel(tile))
                into.Add(tile);
        }
    }

    /// <summary>
    /// A step penalty rather than a refusal, for the one caller where refusing
    /// could be fatal: an escape. Colliding with a sibling on an escape tick
    /// leaves this body standing exactly where the bolt is going, so the
    /// preference is strong, but it never removes the last exit.
    /// </summary>
    public int TravelPenalty(Position tile) => MayTravel(tile) ? 0 : 40;

    /// <summary>
    /// C4: how many of my other bodies would share one enemy fan pose, or one
    /// deflection return ray, with a body standing on <paramref name="tile"/>.
    /// Zero is the answer a well-spaced pair gives; one is a cast the
    /// opposition gets for free.
    /// </summary>
    public int CoExposure(Position tile)
    {
        if (!ArcRules.FanSpacing || _fanPoses.Count == 0)
            return 0;
        int worst = 0;
        foreach (HashSet<Position> pose in _fanPoses)
        {
            if (!pose.Contains(tile))
                continue;
            int shared = 0;
            foreach (GenericActorContext.ObservedAllyState ally in _context.Allies)
            {
                if (pose.Contains(ally.Position))
                    shared++;
            }
            if (shared > worst)
                worst = shared;
        }
        return worst;
    }
}
