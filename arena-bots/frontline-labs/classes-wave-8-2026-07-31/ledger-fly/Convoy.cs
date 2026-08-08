using BotArena.Sdk;

/// <summary>
/// The congestion line — the ledger entry revisions 3-5 never billed.
///
/// <para>The books priced exposure, blood and ground. What they never priced is a
/// tick this team loses to <b>itself</b>: a body whose step is refused because a
/// sibling wanted the same tile pays a full tick, the ledger records no cost, and
/// on a four-slot roster it happens over and over on the same tile. Measured on
/// the frozen wave-5 artifact playing itself: 230 of its 250 refused steps in 24
/// matches were caused by one of its OWN bodies, almost all of them two or three
/// bodies stepping onto one tile of the contested region, the same pair
/// re-colliding every third tick for as long as the exchange lasted. That is the
/// silliness the owner can see from the stands, and it is a debit the doctrine was
/// already equipped to price — it simply had no line for it.</para>
///
/// <para><b>Why this can be an agreement rather than a guess.</b> There is no
/// shared memory between our lives: each body is a fresh runtime with life-scoped
/// private state, and a life never sees a sibling's current action. What it does
/// see is the frozen team observation, and every life of this team receives the
/// SAME one — the same allied bodies, the same enemy union, the same slot states,
/// the same mode. So a plan computed from the observation ALONE, with no private
/// memory and no <c>context.Random</c>, is computed identically by every one of
/// our bodies on the same tick. That is common knowledge, and it is enough to
/// deconflict without a channel: each body derives the whole team's plan, obeys
/// the part of it that binds itself, and knows the others are deriving the same
/// one. Everything in this file is therefore restricted to observation and
/// contract inputs on purpose; adding a remembered term here — a ghost tile, a
/// stance history, a random tie-break — would silently break the agreement and
/// hand the collisions straight back.</para>
///
/// <para><b>Right of way is priced, not arbitrary</b> — this is the written rule
/// the general coordination bar asks for. Between two of our bodies that want one
/// tile, the order is:</para>
/// <list type="number">
/// <item>fewer ticks of route remaining to its own berth: it clears the tile
/// sooner, so asking IT to detour is the dearer of the two yields;</item>
/// <item>then the larger declared replacement cost — a body whose slot refills
/// slowly is worth more ticks than one that refills fast, and the bank carries the
/// rebuild clocks of every pipeline it feeds on top of its own return clock, so it
/// outranks a child at equal distance;</item>
/// <item>then stable unit id, then life id — a total order, so it is impossible
/// for two of our bodies to both believe they have right of way.</item>
/// </list>
///
/// <para>The plan is two things. A <b>berth</b>: one tile of the contested region
/// per body, picked in precedence order, so the second body walks to the second
/// tile instead of queueing behind the first — which is what stops a yield from
/// becoming a stall. And a <b>schedule</b>: for every body ahead of us in
/// precedence, the tiles its route needs and the tick it needs each one, so a step
/// of ours is refused (this tick and next) or merely priced (later) instead of
/// colliding.</para>
/// </summary>
internal sealed class Convoy
{
    /// <summary>Ticks of route a projection schedules and prices.</summary>
    private const int Horizon = 4;

    /// <summary>Search depth in ticks; a little past the horizon so a berth
    /// four steps away is still found under a locked profile.</summary>
    private const int Depth = 10;

    private readonly Dictionary<Position, int> _claim = [];
    private readonly HashSet<Position> _corridorHold = [];
    private readonly HashSet<Position> _arrivals = [];
    private readonly HashSet<Position> _otherBerths = [];

    private Convoy()
    {
    }

    /// <summary>
    /// A plan that binds nothing. It is what a body holds before its first tick
    /// and what a failed projection falls back to, and every reading below is
    /// wave-5 behaviour against it.
    /// </summary>
    public static Convoy None() => new();

    /// <summary>
    /// Tile of the contested region this body owns, or null when the region is
    /// empty, this body is not going there, or every tile is spoken for.
    /// </summary>
    public Position? Berth { get; private set; }

    /// <summary>How many of our bodies outrank this one for a shared tile.</summary>
    public int Ahead { get; private set; }

    /// <summary>
    /// Whether the plan has this body escorting rather than claiming: the
    /// declared stationary cap is already met by dearer siblings, so another
    /// berth would add nothing to the gain and this body is worth more on the
    /// firing line into one.
    /// </summary>
    public bool Screening { get; private set; }

    /// <summary>Tiles a dearer sibling's route needs within the horizon.</summary>
    public int Claims => _claim.Count;

    /// <summary>
    /// Tiles a dearer sibling needs THIS tick or the next one, every tile of a
    /// one-tile corridor it is committed to crossing, and the tile our own next
    /// arrival is about to land on. These are refusals rather than prices: the
    /// general bar is that no body of ours stands where a committed route needs to
    /// be, and the precedence ladder above is the written rule that says who
    /// yields.
    /// </summary>
    public HashSet<Position> Refused()
    {
        var refused = new HashSet<Position>();
        if (Coordination.Congestion)
        {
            foreach (KeyValuePair<Position, int> entry in _claim)
            {
                if (entry.Value <= 1)
                    refused.Add(entry.Key);
            }
        }
        if (Coordination.Corridor)
            refused.UnionWith(_corridorHold);
        if (Coordination.Traffic)
            refused.UnionWith(_arrivals);
        return refused;
    }

    /// <summary>
    /// What standing on a tile costs the team in ticks nothing else bills: the
    /// delay a dearer sibling eats because its route wanted this tile. Scaled into
    /// the same tile-score currency as the other terms, and zero on every tile no
    /// route needs — which on a two-body roster is nearly all of them.
    /// </summary>
    public int Congestion(Position tile)
    {
        if (!Coordination.Congestion)
            return 0;
        int price = 0;
        if (_claim.TryGetValue(tile, out int when))
        {
            // A tile wanted sooner is dearer: a refusal now costs the whole
            // detour, a refusal four ticks out will probably have moved on.
            price += Math.Max(1, Horizon - when) * 4;
        }
        // Another body's berth is not ours to stand on. Two bodies on one tile of
        // a four-tile region is also the exact shape that starves the region of
        // the second bearing the books have been paying for since revision 4.
        if (_otherBerths.Contains(tile) && Berth != tile)
            price += 8;
        return price;
    }

    /// <summary>Tiles our own next arrival is expected to need.</summary>
    public IReadOnlySet<Position> Arrivals => _arrivals;

    /// <summary>
    /// Whether a tile is a one-tile corridor: an open tile whose open cardinal
    /// neighbours are one opposed pair, or a dead end. Derived from the delivered
    /// map rather than from coordinates, so the same test answers it on the
    /// thin-fronts holdout and on any map a later arm ships.
    /// </summary>
    public static bool IsCorridor(MatchLens lens, Position tile)
    {
        if (lens.IsWall(tile))
            return false;
        Direction? first = null;
        Direction? second = null;
        int open = 0;
        foreach (Direction direction in Field.Steps)
        {
            (int dx, int dy) = direction.Vector();
            if (lens.IsWall(tile.Offset(dx, dy)))
                continue;
            open++;
            if (first is null)
                first = direction;
            else
                second = direction;
        }
        if (open == 1)
            return true;
        return open == 2
            && first is Direction a
            && second is Direction b
            && a == Opposite(b);
    }

    /// <summary>
    /// Builds this tick's team plan from the frozen observation. Contract and
    /// observation only — no remembered tile, no stance history, no random stream
    /// — so every one of our lives builds the identical plan.
    /// </summary>
    public static Convoy Build(
        MatchLens lens,
        GenericActorContext context,
        IReadOnlyList<Position> objective,
        Position focus,
        int standoff,
        int berths)
    {
        var convoy = new Convoy();
        try
        {
            convoy.Plan(lens, context, objective, focus, standoff, berths);
        }
        catch (Exception)
        {
            // A plan is an optimisation; the body still has to return a legal
            // action. An empty convoy refuses nothing and prices nothing, which
            // is the correct failure and is exactly wave-5 behaviour.
            convoy._claim.Clear();
            convoy._corridorHold.Clear();
            convoy._arrivals.Clear();
            convoy._otherBerths.Clear();
            convoy.Berth = null;
            convoy.Screening = false;
        }
        return convoy;
    }

    private void Plan(
        MatchLens lens,
        GenericActorContext context,
        IReadOnlyList<Position> objective,
        Position focus,
        int standoff,
        int berths)
    {
        List<Body> bodies = Bodies(lens, context);

        // Static obstacles for every projection: walls, every body on the board,
        // and the tiles a queued lifecycle output has already reserved. All of it
        // is team-wide observation, so all the projections agree.
        //
        // `foreign` deliberately EXCLUDES our own bodies. It is what the arrival
        // policy would see if we got out of its way, which is the only stable
        // reading of "the tile our next body needs": a set that moved with our own
        // feet would name a different tile every tick and would not be the same
        // tile in two of our lives.
        var foreign = new HashSet<Position>();
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
            foreign.Add(enemy.Position);
        foreach (GenericActorContext.ObservedUnitSlot slot in context.TeamUnits)
        {
            if (slot.State
                is GenericActorContext.UnitSlotState.LifecyclePending pending)
            {
                foreign.Add(pending.ReservedPosition);
            }
        }
        ArrivalTiles(lens, context, foreign);
        var solid = new HashSet<Position>(foreign);
        foreach (Body body in bodies)
            solid.Add(body.Tile);

        var goals = objective.ToHashSet();
        var empty = new HashSet<Position>();
        // One search per body, reused for the ranking and for the berth pick.
        var reaches = new List<Reach>(bodies.Count);
        var ranked = new List<(int Remaining, int Value, Body Body, int Index)>(
            bodies.Count);
        for (int index = 0; index < bodies.Count; index++)
        {
            Body body = bodies[index];
            Reach reach = Explore(lens, body, solid);
            reaches.Add(reach);
            Walk estimate = Choose(
                lens, body, reach, goals, empty, focus, standoff, false);
            ranked.Add((estimate.Remaining, body.Value, body, index));
        }
        ranked.Sort(Compare);

        var taken = new HashSet<Position>();
        int claimed = 0;
        foreach ((int _, int _, Body body, int index) in ranked)
        {
            // THE ESCORT LINE, in the one place the team agrees on anything.
            // Gain is capped, so past the declared cap another berth on the
            // region buys nothing at all: the bodies past it stop asking for one
            // and are planned onto the band just off the region instead, which
            // is where a screen stands and where damage reverts nothing.
            bool screen = !body.Bank && claimed >= berths;
            Walk walk = Choose(
                lens, body, reaches[index], goals, taken, focus, standoff,
                screen);
            if (walk.Goal is Position berth && goals.Contains(berth))
            {
                taken.Add(berth);
                if (!body.Bank)
                    claimed++;
            }
            if (body.Self)
            {
                Screening = screen;
                // The berth is the destination half of the congestion line, so it
                // stands or falls with it: a leave-one-out build that kept the
                // berth and dropped the schedule would be measuring neither.
                if (!Coordination.Congestion)
                    return;
                Berth = walk.Goal is Position mine && goals.Contains(mine)
                    ? mine
                    : null;
                _otherBerths.UnionWith(taken);
                if (Berth is Position held)
                    _otherBerths.Remove(held);
                return;
            }
            Ahead++;
            Schedule(lens, walk);
        }
    }

    private static int Compare(
        (int Remaining, int Value, Body Body, int Index) left,
        (int Remaining, int Value, Body Body, int Index) right)
    {
        if (left.Remaining != right.Remaining)
            return left.Remaining.CompareTo(right.Remaining);
        if (left.Value != right.Value)
            return right.Value.CompareTo(left.Value);
        return left.Body.ActorId.CompareTo(right.Body.ActorId);
    }

    /// <summary>
    /// Writes one projected route into the schedule: each tile against the tick
    /// its owner steps onto it, and each one-tile corridor tile as a hold for the
    /// whole crossing, because a corridor is the one shape two bodies cannot share
    /// at any offset. The mouths are held with it — standing on the tile a
    /// corridor lets out onto is the same block, one tick later.
    /// </summary>
    private void Schedule(MatchLens lens, Walk walk)
    {
        for (int index = 0; index < walk.Tiles.Count; index++)
        {
            Position tile = walk.Tiles[index];
            int when = walk.Ticks[index];
            if (when > Horizon)
                continue;
            if (!_claim.TryGetValue(tile, out int existing) || when < existing)
                _claim[tile] = when;
            if (!Coordination.Corridor || !IsCorridor(lens, tile))
                continue;
            // The corridor tile itself, and NOTHING adjacent to it. Two wider
            // versions were built and both measured worse on the opponent stable:
            // holding all four neighbours of every corridor tile refused most of
            // this map's central lane to our own bodies (its middle row is a chain
            // of six one-tile corridors), and holding just the exit mouth still
            // cost 6 of 64 points against nothing - the mouth is where the
            // objective approach is. The tile itself is the whole rule, because
            // the tile itself is the whole shape: a corridor is the one place two
            // bodies cannot pass, and the mouths are ordinary ground where they
            // can.
            _corridorHold.Add(tile);
        }
    }

    /// <summary>
    /// Where our own next arrival lands. A fabrication publishes its reserved tile
    /// and is already an obstacle everywhere in this bot; an automatic return
    /// publishes only a due tick, so the tile has to come from the declared
    /// placement policy — and under a forward rally that tile is ON the contested
    /// region, which is exactly where our bodies are standing. A body of ours
    /// parked there either blocks the arrival or pushes it deeper into the region
    /// than the policy intended.
    /// </summary>
    private void ArrivalTiles(
        MatchLens lens,
        GenericActorContext context,
        IReadOnlySet<Position> solid)
    {
        // Inert unless the contract actually rallies arrivals forward. Where it
        // does not, an arrival lands on the slot's own protected spawn, which the
        // contract already reserves permanently against our own children and this
        // bot already reads off the tile - there is nothing here to add.
        if (!Coordination.Traffic || !lens.RallyForward)
            return;
        int active = context.Mode
            is GenericActorContext.ModeObservationState.Frontline mode
            ? mode.ActivePositionIndex
            : -1;
        foreach (GenericActorContext.ObservedUnitSlot slot in context.TeamUnits)
        {
            if (slot.TeamId != lens.TeamId)
                continue;
            if (slot.State
                is not GenericActorContext.UnitSlotState.AutomaticReturnPending
                    pending)
            {
                continue;
            }
            // Only an arrival that is actually about to happen: a clock with
            // twenty ticks left is not traffic, it is a plan.
            if (pending.DueTick - context.Tick > 2)
                continue;
            // ...and only the tile the declared policy will actually pick. The
            // helper answers WHICH REGION an arrival lands in; the policy is
            // narrower than that - the rear-most free tile of the region measured
            // along our own advance direction - and the difference matters. The
            // first version of this rule refused the whole region and cost real
            // games: the bank returns on a short clock, so a whole own-side
            // region went off limits to every body of ours for three ticks out of
            // every eighteen. Rear-most is minimum forwardness; where two free
            // tiles tie on it the policy's own order is not published, so both
            // are held, which is two tiles rather than four to six.
            Position[] region = ArenaBasics.ExpectedArrivalTiles(
                lens.Contract,
                slot.TeamId,
                slot.UnitId,
                active);
            int rear = int.MaxValue;
            foreach (Position tile in region)
            {
                if (solid.Contains(tile) || lens.IsWall(tile))
                    continue;
                rear = Math.Min(rear, lens.Forwardness(tile));
            }
            if (rear == int.MaxValue)
                continue;
            foreach (Position tile in region)
            {
                if (solid.Contains(tile) || lens.IsWall(tile))
                    continue;
                if (lens.Forwardness(tile) == rear)
                    _arrivals.Add(tile);
            }
        }
        _arrivals.Remove(context.Self.Position);
    }

    /// <summary>One of our live bodies, with what it is worth in ticks.</summary>
    private readonly record struct Body(
        ActorIdentity ActorId,
        Position Tile,
        Direction Facing,
        string FormId,
        bool Bank,
        int Value,
        bool Self);

    private static List<Body> Bodies(MatchLens lens, GenericActorContext context)
    {
        var bodies = new List<Body>
        {
            Describe(
                lens,
                context.Self.ActorId,
                context.Self.Position,
                context.Self.Facing,
                context.Self.FormId,
                self: true),
        };
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            bodies.Add(Describe(
                lens,
                ally.ActorId,
                ally.Position,
                ally.Facing,
                ally.FormId,
                self: false));
        }
        return bodies;
    }

    private static Body Describe(
        MatchLens lens,
        ActorIdentity actorId,
        Position tile,
        Direction facing,
        string formId,
        bool self)
    {
        bool bank = lens.IsAlliedBankUnit(actorId.UnitId);
        // What losing this body costs in ticks, from its own slot's lifecycle
        // profile — and for the bank, plus every rebuild clock it feeds by hand,
        // because none of those slots refills while it is dead. It is the same
        // accounting the doctrine already uses to price a trade.
        int value = lens.ReplacementTicks(actorId.UnitId);
        if (bank)
            value += lens.PipelineStallTicks;
        return new Body(actorId, tile, facing, formId, bank, value, self);
    }

    /// <summary>A finished breadth-first search over (tile, facing) states.</summary>
    private sealed record Reach(
        Dictionary<int, int> Cost,
        Dictionary<int, int> Parent,
        int Start,
        bool Locked);

    /// <summary>A chosen route: its goal, ticks remaining, and its schedule.</summary>
    private readonly record struct Walk(
        Position? Goal,
        int Remaining,
        List<Position> Tiles,
        List<int> Ticks);

    /// <summary>
    /// Breadth-first search with the tick cost the movement profile actually
    /// declares. Under a facing-locked profile a rotation is a whole tick and the
    /// facing IS the movement lane, so the search runs over (tile, facing) pairs
    /// and a route that turns twice costs two ticks more than one that does not.
    /// Every action costs exactly one tick, so plain breadth-first order gives
    /// exact tick counts, and the blocked set is the team-wide observation so
    /// every life computes the same numbers.
    /// </summary>
    private static Reach Explore(
        MatchLens lens,
        Body body,
        IReadOnlySet<Position> solid)
    {
        bool locked = lens.Coupling(body.FormId)
            == GenericActorRulesContract.MovementFacingCoupling.FacingLocked;
        int width = lens.Width;
        var cost = new Dictionary<int, int>();
        var parent = new Dictionary<int, int>();
        var queue = new Queue<int>();
        int start = StateKey(width, body.Tile, body.Facing, locked);
        cost[start] = 0;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            int spent = cost[current];
            if (spent >= Depth)
                continue;
            int cell = current / 4;
            var tile = new Position(cell % width, cell / width);
            var facing = (Direction)(current % 4);

            foreach (Direction direction in Field.Steps)
            {
                if (locked && direction != facing)
                {
                    int rotated = StateKey(width, tile, direction, locked);
                    if (cost.ContainsKey(rotated))
                        continue;
                    cost[rotated] = spent + 1;
                    parent[rotated] = current;
                    queue.Enqueue(rotated);
                    continue;
                }
                (int dx, int dy) = direction.Vector();
                Position next = tile.Offset(dx, dy);
                if (lens.IsWall(next) || solid.Contains(next))
                    continue;
                int key = StateKey(
                    width,
                    next,
                    locked ? direction : facing,
                    locked);
                if (cost.ContainsKey(key))
                    continue;
                cost[key] = spent + 1;
                parent[key] = current;
                queue.Enqueue(key);
            }
        }
        return new Reach(cost, parent, start, locked);
    }

    private static int StateKey(
        int width,
        Position tile,
        Direction facing,
        bool locked) =>
        (((tile.Y * width) + tile.X) * 4) + (locked ? (int)facing : 0);

    /// <summary>
    /// Picks this body's berth out of a finished search and reconstructs the route
    /// to it. A body that is not the bank is going to the contested region; the
    /// bank is going to its standoff band around the same focus, so one scoring
    /// function serves both. A tile a dearer body has already taken is not
    /// forbidden, only expensive — refusing to contest a region because a sibling
    /// got there first would be a worse bug than the collision.
    /// </summary>
    private static Walk Choose(
        MatchLens lens,
        Body body,
        Reach reach,
        IReadOnlySet<Position> goals,
        IReadOnlySet<Position> taken,
        Position focus,
        int standoff,
        bool screen)
    {
        int width = lens.Width;
        int bestKey = reach.Start;
        int bestScore = Score(
            lens, body, body.Tile, 0, goals, taken, focus, standoff, screen);
        Position bestTile = body.Tile;
        foreach (KeyValuePair<int, int> entry in reach.Cost)
        {
            int cell = entry.Key / 4;
            var tile = new Position(cell % width, cell / width);
            if (tile == body.Tile)
                continue;
            int score = Score(
                lens, body, tile, entry.Value, goals, taken, focus, standoff,
                screen);
            if (score < bestScore
                || (score == bestScore
                    && (entry.Value < reach.Cost[bestKey]
                        || (entry.Value == reach.Cost[bestKey]
                            && Before(tile, bestTile)))))
            {
                bestScore = score;
                bestKey = entry.Key;
                bestTile = tile;
            }
        }

        var tiles = new List<Position>();
        var ticks = new List<int>();
        if (bestKey == reach.Start)
            return new Walk(null, 0, tiles, ticks);

        var chain = new List<int>();
        int cursor = bestKey;
        while (cursor != reach.Start
            && reach.Parent.TryGetValue(cursor, out int previous))
        {
            chain.Add(cursor);
            cursor = previous;
        }
        chain.Reverse();
        Position last = body.Tile;
        foreach (int key in chain)
        {
            int cell = key / 4;
            var tile = new Position(cell % width, cell / width);
            if (tile == last)
                continue;   // a rotation costs a tick but claims no tile
            tiles.Add(tile);
            ticks.Add(reach.Cost[key] - 1);
            last = tile;
        }
        return new Walk(bestTile, reach.Cost[bestKey], tiles, ticks);
    }

    private static int Score(
        MatchLens lens,
        Body body,
        Position tile,
        int cost,
        IReadOnlySet<Position> goals,
        IReadOnlySet<Position> taken,
        Position focus,
        int standoff,
        bool screen)
    {
        int score;
        if (goals.Count > 0 && screen)
        {
            // One tile off the region: close enough to stand on the lane into
            // it, off it so a bolt that lands here costs the run nothing.
            int gap = Nearest(goals, tile);
            score = goals.Contains(tile) ? 12 : Math.Abs(gap - 1) * 6;
        }
        else if (goals.Count > 0 && !body.Bank)
        {
            score = goals.Contains(tile)
                ? 0
                : 16 + Nearest(goals, tile);
        }
        else
        {
            score = Math.Abs(tile.ChebyshevDistance(focus) - standoff) * 4;
        }
        if (taken.Contains(tile))
            score += 6;
        _ = lens;
        return (score * 8) + cost;
    }

    private static int Nearest(IReadOnlySet<Position> goals, Position tile)
    {
        int best = int.MaxValue;
        foreach (Position goal in goals)
            best = Math.Min(best, goal.ChebyshevDistance(tile));
        return best == int.MaxValue ? 0 : best;
    }

    private static bool Before(Position left, Position right) =>
        left.Y < right.Y || (left.Y == right.Y && left.X < right.X);

    private static Direction Opposite(Direction direction) =>
        direction switch
        {
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.East => Direction.West,
            _ => Direction.East,
        };
}
