using BotArena.Sdk;

/// <summary>
/// Windowed Hierarchical Cooperative A* behind the movement seam.
///
/// <para>Greedy stepping asks each body, one at a time, "which neighbour
/// tile is best for me right now" — so two bodies routinely want the same
/// tile, a corridor deadlocks on whoever asked first, and the loser's only
/// vocabulary is a sidestep that undoes itself next tick. This stepper
/// answers the same question with a <b>space-time</b> path: it searches
/// (tile, tick) pairs over a short window, and every body that has already
/// moved this tick has RESERVED the tiles it will stand on for the rest of
/// that window. A body planning later therefore routes around where the
/// carrier is GOING TO BE, not merely where it is — and may deliberately
/// wait one tick to follow a corridor rather than take a detour it will
/// have to walk back.</para>
///
/// <para>This is prioritized cooperative planning, which is exactly what
/// the executor's body order already expresses: carriers first, then
/// bodies standing on a carrier's lane, then order priority, then unit id.
/// <see cref="WantsFightPrecedence"/> widens it with the tier the greedy
/// order never needed — a body in contact settles its tile before free
/// traffic claims it.</para>
///
/// <para>The flow field (<see cref="ArenaBasics.StaticDistance"/>) is the
/// heuristic and only the heuristic. Beyond the window the search stops
/// and scores the leaf by that distance, so a route far longer than the
/// window still makes exact progress.</para>
/// </summary>
internal sealed class CoordinatedArenaStepper : IArenaStepper
{
    /// <summary>How many ticks ahead a plan reserves. Five is enough to
    /// cross the warren's chokes, and short enough that stale enemy
    /// positions never dominate the plan.</summary>
    private const int Window = 5;

    /// <summary>Costs are doubled so a wait can cost 3 against a step's 2:
    /// a plan that waits must EARN it, and ties go to the body that
    /// moves.</summary>
    private const int StepCost = 2;
    private const int WaitCost = 3;

    private readonly GreedyArenaStepper _greedy = new();

    /// <summary>Space-time reservations: which unit owns a tile at a tick
    /// offset from now. Installed when a body actually commits a move.
    /// </summary>
    private readonly Dictionary<(Position Tile, int Tick), int> _reserved = [];

    /// <summary>Traversal reservations, so two bodies cannot swap tiles
    /// through each other — legal for the engine, nonsense on screen.
    /// </summary>
    private readonly Dictionary<(Position From, Position To, int Tick), int>
        _edges = [];

    /// <summary>The path each unit last planned, so committing its first
    /// step installs the whole route rather than one tile.</summary>
    private readonly Dictionary<int, Position[]> _plans = [];

    /// <summary>Within one body's turn the cascade asks the same question
    /// repeatedly; the answer cannot change until something commits.
    /// </summary>
    private readonly Dictionary<(int Unit, StepIntent Intent, int Goals),
        Position?> _memo = [];

    private HashSet<Position> _windowObstacles = [];
    private HashSet<Position> _enemies = [];
    private bool _mirrored;

    public int Replans { get; private set; }

    public int WaitsChosen { get; private set; }

    public bool WantsFightPrecedence => true;

    public void BeginTick(
        GenericActorResolvedMatchContract contract,
        MindContext mind)
    {
        _reserved.Clear();
        _edges.Clear();
        _plans.Clear();
        _memo.Clear();
        _mirrored = ArenaBasics.MirroredFrame(contract, mind);
        _enemies = mind.Enemies.Select(enemy => enemy.Position).ToHashSet();
        _windowObstacles = WindowObstacles(contract, mind);
    }

    /// <summary>
    /// Durable hazards a plan must respect for its whole window: authored
    /// spawn reservations, armed hostile trip nodes, and the opponent's
    /// protected home pads. Transient things (allies, enemies, projectiles,
    /// this tick's claims) belong to the first step only and arrive through
    /// <see cref="ArenaBasics.BlockedNow"/>.
    /// </summary>
    private static HashSet<Position> WindowObstacles(
        GenericActorResolvedMatchContract contract,
        MindContext mind)
    {
        var blocked = mind.VisibleTiles
            .Where(tile => tile.SpawnReservation is not null)
            .Select(tile => tile.Position)
            .ToHashSet();
        int team = mind.Bodies.FirstOrDefault()?.ActorId.TeamId ?? -1;
        if (mind.Mode is GenericActorContext.ModeObservationState.ArcRelay arc)
        {
            blocked.UnionWith(arc.VisibleSignatures
                .Where(signature => string.Equals(
                        signature.SignatureId, "trip-node",
                        StringComparison.Ordinal)
                    && signature.OwnerTeamId != team
                    && !signature.Suppressed
                    && signature.Phase
                        == GenericActorContext.ArcRelaySignaturePhase.Active)
                .SelectMany(signature => signature.Positions));
        }
        if (contract.ModeMapBinding
            is GenericActorResolvedMatchContract.ArcRelayModeMapBinding binding)
        {
            HashSet<int> hostileParticipants = contract.Topology.Participants
                .Where(participant => participant.TeamId != team)
                .Select(participant => participant.ParticipantId)
                .ToHashSet();
            HashSet<string> protectedRegions = contract
                .ParticipantRegionAssignments
                .Where(assignment =>
                    hostileParticipants.Contains(assignment.ParticipantId)
                    && string.Equals(
                        assignment.RegionRoleId,
                        binding.HomePadRegionRoleId,
                        StringComparison.Ordinal))
                .Select(assignment => assignment.MapRegionId)
                .ToHashSet(StringComparer.Ordinal);
            blocked.UnionWith(contract.Map.Regions
                .Where(region => protectedRegions.Contains(region.RegionId))
                .SelectMany(region => region.Tiles));
        }
        return blocked;
    }

    public Position? Step(StepRequest request)
    {
        // Kiting, yielding and already-chosen steps stay exactly as they
        // were: each is a single reactive tile with its own obstacle rules,
        // and routing them through a route planner would only make a dodge
        // into a commitment. The route plane — Toward, which TryEvade also
        // rides, and Homeward — is where coordination pays.
        if (request.Intent is StepIntent.Away or StepIntent.Aside
            or StepIntent.Vacate or StepIntent.Direct)
        {
            return _greedy.Step(request);
        }

        // A routed delivery is the one plan this stepper does not get to
        // make. The corridor is the CARRIER'S commitment — sticky by
        // construction, chosen against durable spawn reservations for its
        // whole length — and a five-tick window that re-decides it every
        // tick is exactly the re-derivation that produced the w-9002 dance.
        // So the arbiter takes the committed step and PUBLISHES the
        // corridor: committing it reserves the ground the Core is going to
        // walk, and the rest of the team plans around where it will be.
        if (request.Intent is StepIntent.Routed)
        {
            Position? committed = _greedy.Step(request);
            if (committed is Position first)
            {
                _plans[request.Body.UnitId] = Corridor(
                    request.Contract,
                    request.Body,
                    first,
                    request.Positions.First());
            }
            return committed;
        }

        IReadOnlyCollection<Position> goals = request.Positions;
        if (goals.Count == 0 || goals.Contains(request.Body.Position))
            return null;

        var key = (
            request.Body.UnitId,
            request.Intent,
            GoalKey(goals));
        if (_memo.TryGetValue(key, out Position? cached))
            return cached;

        Position? chosen = Plan(request, goals);
        // A plan that cannot see its goal at all falls back to the greedy
        // chooser rather than standing still: coordination is an
        // improvement on the answer, never a veto on moving.
        chosen ??= _greedy.Step(request);
        _memo[key] = chosen;
        return chosen;
    }

    public void NoteCommitted(MindBody body, Position destination)
    {
        int unit = body.UnitId;
        Position[] path = _plans.TryGetValue(unit, out Position[]? planned)
            && planned.Length > 0
            && planned[0] == destination
                ? planned
                : [destination];
        Position previous = body.Position;
        for (int tick = 1; tick <= Window; tick++)
        {
            Position tile = path[Math.Min(tick - 1, path.Length - 1)];
            _reserved[(tile, tick)] = unit;
            if (tile != previous)
                _edges[(tile, previous, tick)] = unit;
            previous = tile;
        }
        // Another body's move invalidates every cached answer: the claims
        // it just took are part of the next question.
        _memo.Clear();
    }

    /// <summary>
    /// Space-time A* from the body's tile to any goal tile. Nodes are
    /// (tile, tick) with tick bounded by the window; the search returns the
    /// first step of the best path it found, or of the best leaf when the
    /// goal lies beyond the window. Null means "no legal first step at
    /// all", which hands the question back to greedy.
    /// </summary>
    private Position? Plan(
        StepRequest request,
        IReadOnlyCollection<Position> goals)
    {
        Replans++;
        MindBody body = request.Body;
        GenericActorMapContract map = request.Contract.Map;
        HashSet<Position> goalSet = goals.ToHashSet();
        HashSet<Position> blockedNow = ArenaBasics.BlockedNow(
            request.Contract, request.Mind, body, request.Claims);
        ProjectileHeading[] headings = ArenaBasics.RouteHeadings(
            request.Contract, body);
        int unit = body.UnitId;

        var start = new Node(body.Position, 0);
        var cost = new Dictionary<Node, int> { [start] = 0 };
        var origin = new Dictionary<Node, Node>();
        var open = new SortedSet<Ranked>
        {
            new(Heuristic(map, body.Position, goalSet), 0, start, _mirrored),
        };
        Node? bestLeaf = null;
        int bestLeafRank = int.MaxValue;

        while (open.Count > 0)
        {
            Ranked current = open.Min;
            open.Remove(current);
            Node node = current.Node;
            int here = cost[node];

            if (node.Tick > 0 && goalSet.Contains(node.Tile))
                return Commit(unit, node, origin, body.Position, goalSet);

            if (node.Tick == Window)
            {
                // Leaf: score by what is left to walk, then by what it
                // cost to get here. Ties are already broken canonically by
                // the ranking key.
                int rank = Heuristic(map, node.Tile, goalSet);
                if (rank < bestLeafRank
                    || (rank == bestLeafRank && bestLeaf is Node leaf
                        && here < cost[leaf]))
                {
                    bestLeafRank = rank;
                    bestLeaf = node;
                }
                continue;
            }

            foreach (Position next in Successors(map, node, headings))
            {
                bool waiting = next == node.Tile;
                int tick = node.Tick + 1;
                if (node.Tick == 0)
                {
                    if (!waiting && blockedNow.Contains(next))
                        continue;
                }
                else if (_windowObstacles.Contains(next)
                    || (tick <= 1 && _enemies.Contains(next)))
                {
                    continue;
                }
                if (_reserved.TryGetValue((next, tick), out int owner)
                    && owner != unit)
                {
                    continue;
                }
                if (_edges.TryGetValue((node.Tile, next, tick), out int swapper)
                    && swapper != unit)
                {
                    continue;
                }

                var candidate = new Node(next, tick);
                int through = here + (waiting ? WaitCost : StepCost);
                if (cost.TryGetValue(candidate, out int known)
                    && known <= through)
                {
                    continue;
                }
                cost[candidate] = through;
                origin[candidate] = node;
                open.Add(new Ranked(
                    through + Heuristic(map, next, goalSet),
                    through,
                    candidate,
                    _mirrored));
            }
        }

        return bestLeaf is Node best
            ? Commit(unit, best, origin, body.Position, goalSet)
            : null;
    }

    /// <summary>
    /// The tiles a committed delivery is about to walk: the first step it
    /// actually chose, then a descent of the flow field toward the same
    /// goal, one tile per tick, for the length of the window. It is a
    /// PROJECTION of somebody else's plan rather than a plan of our own —
    /// traffic is deliberately ignored, because what the rest of the team
    /// needs to know is where the Core is heading, not who is in its way.
    /// </summary>
    private static Position[] Corridor(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Position first,
        Position goal)
    {
        GenericActorMapContract map = contract.Map;
        ProjectileHeading[] headings = ArenaBasics.RouteHeadings(contract, body);
        var route = new List<Position> { first };
        Position cursor = first;
        for (int tick = 1; tick < Window && cursor != goal; tick++)
        {
            int? here = ArenaBasics.StaticDistance(map, cursor, goal);
            Position? best = null;
            int bestDistance = int.MaxValue;
            foreach (ProjectileHeading heading in headings)
            {
                Position next = ArenaBasics.Step(cursor, heading);
                if (!ArenaBasics.CanStep(map, cursor, next, heading)
                    || ArenaBasics.StaticDistance(map, next, goal)
                        is not int distance
                    || distance >= here
                    || distance >= bestDistance)
                {
                    continue;
                }
                bestDistance = distance;
                best = next;
            }
            if (best is not Position step)
                break;
            route.Add(step);
            cursor = step;
        }
        return [.. route];
    }

    private static IEnumerable<Position> Successors(
        GenericActorMapContract map,
        Node node,
        ProjectileHeading[] headings)
    {
        // Waiting is offered first so its cost, not its position in the
        // list, is what decides against it.
        yield return node.Tile;
        foreach (ProjectileHeading heading in headings)
        {
            Position next = ArenaBasics.Step(node.Tile, heading);
            if (ArenaBasics.CanStep(map, node.Tile, next, heading))
                yield return next;
        }
    }

    /// <summary>
    /// Walk the search tree back to the first move, record the whole route
    /// so committing it reserves the corridor, and return the tile to take
    /// now. A plan whose first act is to wait returns null — that IS the
    /// cooperative answer, and the executor's next channel gets its say.
    /// </summary>
    private Position? Commit(
        int unit,
        Node terminal,
        IReadOnlyDictionary<Node, Node> origin,
        Position from,
        IReadOnlySet<Position> goals)
    {
        var route = new List<Position>();
        Node cursor = terminal;
        while (origin.TryGetValue(cursor, out Node previous))
        {
            route.Add(cursor.Tile);
            cursor = previous;
        }
        route.Reverse();
        if (route.Count == 0)
            return null;
        _plans[unit] = [.. route];
        if (route[0] == from)
        {
            // A deliberate yield: the route needs a tile somebody else has
            // reserved this tick, and waiting one tick is shorter than
            // going around. Only honoured when the plan actually reaches a
            // goal — otherwise standing still is just standing still.
            if (!goals.Contains(route[^1]))
                return null;
            WaitsChosen++;
            return null;
        }
        return route[0];
    }

    private static int Heuristic(
        GenericActorMapContract map,
        Position from,
        IReadOnlySet<Position> goals)
    {
        int best = int.MaxValue;
        foreach (Position goal in goals)
        {
            if (ArenaBasics.StaticDistance(map, from, goal) is int distance
                && distance < best)
            {
                best = distance;
            }
        }
        return best == int.MaxValue ? int.MaxValue / 4 : best * StepCost;
    }

    private static int GoalKey(IReadOnlyCollection<Position> goals)
    {
        // Order-independent, so the same goal set asked twice memoizes.
        int key = goals.Count;
        foreach (Position goal in goals)
            key ^= goal.X * 73856093 ^ goal.Y * 19349663;
        return key;
    }

    private readonly record struct Node(Position Tile, int Tick);

    /// <summary>
    /// Open-list ordering. Total and canonical: f, then remaining cost,
    /// then tick, then the tile read in the team's own frame — an absolute
    /// y/x preference would make the two sides of a rotationally bound map
    /// choose opposite tiles from identical situations.
    /// </summary>
    private readonly record struct Ranked(
        int Estimate,
        int Spent,
        Node Node,
        bool Mirrored) : IComparable<Ranked>
    {
        public int CompareTo(Ranked other)
        {
            int compared = Estimate.CompareTo(other.Estimate);
            if (compared != 0)
                return compared;
            compared = (Estimate - Spent).CompareTo(
                other.Estimate - other.Spent);
            if (compared != 0)
                return compared;
            compared = Node.Tick.CompareTo(other.Node.Tick);
            if (compared != 0)
                return compared;
            compared = ArenaBasics.FrameY(Node.Tile, Mirrored)
                .CompareTo(ArenaBasics.FrameY(other.Node.Tile, Mirrored));
            return compared != 0
                ? compared
                : ArenaBasics.FrameX(Node.Tile, Mirrored)
                    .CompareTo(ArenaBasics.FrameX(other.Node.Tile, Mirrored));
        }
    }
}
