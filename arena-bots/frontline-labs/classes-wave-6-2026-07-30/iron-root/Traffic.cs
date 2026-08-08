using BotArena.Sdk;

/// <summary>
/// The five coordination rules wave 6 ships, each switchable so that each one
/// can be ABLATED and priced on the same seeds. A rule nobody measured is a
/// rule nobody should believe, and the only way to measure one is to build the
/// same artifact without it.
///
/// <para>These are <c>static readonly</c> rather than <c>const</c> on purpose:
/// a const false turns every guarded block into unreachable code, which this
/// solution compiles as an error, and an ablation that cannot be built is not
/// an ablation.</para>
/// </summary>
internal static class Coordination
{
    /// <summary>
    /// C1a — never freeze on a 1-tile lane my own traffic needs, and leave one
    /// that becomes needed. The brief's first option.
    /// </summary>
    public static readonly bool ChokeRefusal = true;

    /// <summary>
    /// C1b — freeze deliberately on a 1-tile lane only the OPPOSITION needs,
    /// and relax the coverage floor to do it. The brief's second option.
    ///
    /// <para>Split from C1a after measurement, because the two halves are not one
    /// rule: they have opposite signs on the board and bundling them made the
    /// pair unpriceable. One refuses a wall, the other buys one.</para>
    /// </summary>
    public static readonly bool ChokeGate = true;

    /// <summary>C2 — a junior body never takes a tile a senior sibling's route
    /// needs this tick or next.</summary>
    public static readonly bool RightOfWay = true;

    /// <summary>C3 — one body of mine is committed to a corridor run at a
    /// time; the body already inside it outranks the one outside.</summary>
    public static readonly bool ChokePrecedence = true;

    /// <summary>C4 — do not stand on the tile my own next arrival will take.</summary>
    public static readonly bool RallyClearance = true;

    /// <summary>C5 — do not put two bodies under one muzzle's envelope, or two
    /// bodies on one deflection-return lane, when an equal post exists.</summary>
    public static readonly bool Spacing = true;
}

/// <summary>
/// THE COORDINATION LAYER — wave 6. Everything in this file answers one
/// question: <i>where are my OWN bodies going, and am I in the way?</i>
///
/// <para><b>Why it is a separate file and not a clause.</b> Every rule the wave-6
/// brief asks for needs the same three primitives — what a 1-tile corridor is,
/// what a route costs, and what walling a tile costs somebody — and revision 5
/// had none of them. It had the opposite: a REACTIVE blacklist
/// (<c>_refusals</c>, <c>_denied</c>, <c>_blockedTile</c>) that learned a tile
/// was unusable by walking into it and losing the tick. That is the whole
/// mechanism of the silliness the owner watched: the doctrine did not know a
/// sibling existed as an obstacle until the sibling had already cost it a
/// move.</para>
///
/// <para><b>The coordination substrate is the frozen observation, and that is
/// the only thing it can be.</b> Every life gets a fresh instance with empty
/// private memory, a life never sees an ally's current action, and there is no
/// shared state to write to. So the only way two of my bodies can agree about
/// who yields is for both of them to DERIVE the same answer from the same
/// frozen bytes. Every method here is therefore a pure function of the
/// observation plus the contract: no memory, no ordering assumption, and — the
/// clause that took a measurement to find — <b>no <c>context.Random</c></b>,
/// because the random stream is PER LIFE and two of my bodies drawing from it
/// get different answers to the same question. Revision 5's direction order
/// consumed a random bool per tick, so its two bodies genuinely disagreed about
/// which of two equal routes each was taking; a claim derived through that order
/// would have been a guess wearing a derivation's clothes. Every claim below is
/// order-FREE: it names the union of every tile a shortest route could use,
/// which is a fact about the observation and not about who is asking.</para>
/// </summary>
internal sealed class Traffic
{
    /// <summary>Walling a tile that disconnects a body from its goal entirely.</summary>
    public const int Severed = 1000;

    private readonly GenericActorMapContract _map;

    /// <summary>
    /// Tiles that are 1-tile-wide corridor cells: both neighbours on one axis
    /// are walls, and at least one neighbour on the other axis is open. A body
    /// standing here is a wall across the only lane at this point, for BOTH
    /// teams — which is exactly why it is worth knowing about, in both
    /// directions. Derived from the resolved map, never a coordinate list.
    /// </summary>
    private readonly HashSet<Position> _choke = [];

    /// <summary>
    /// Which maximal connected group of corridor cells each choke tile belongs
    /// to. A "run" is the unit of precedence: two of my bodies entering opposite
    /// ends of the same run collide in the middle, and the fix is that only one
    /// of them is ever committed to the run at a time.
    /// </summary>
    private readonly Dictionary<Position, int> _run = [];
    private readonly List<List<Position>> _runs = [];

    public Traffic(GenericActorMapContract map)
    {
        _map = map;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var tile = new Position(x, y);
                if (!ArenaGeometry.IsOpen(map, tile))
                    continue;
                bool north = ArenaGeometry.IsOpen(map, tile.Offset(0, -1));
                bool south = ArenaGeometry.IsOpen(map, tile.Offset(0, 1));
                bool east = ArenaGeometry.IsOpen(map, tile.Offset(1, 0));
                bool west = ArenaGeometry.IsOpen(map, tile.Offset(-1, 0));
                if (!east && !west && (north || south))
                    _choke.Add(tile);
                else if (!north && !south && (east || west))
                    _choke.Add(tile);
            }
        }

        foreach (Position tile in _choke)
        {
            if (_run.ContainsKey(tile))
                continue;
            var run = new List<Position>();
            var stack = new Stack<Position>();
            stack.Push(tile);
            _run[tile] = _runs.Count;
            while (stack.Count > 0)
            {
                Position current = stack.Pop();
                run.Add(current);
                foreach (Direction direction in ArenaGeometry.Cardinals)
                {
                    Position neighbour = ArenaGeometry.Step(current, direction);
                    if (_choke.Contains(neighbour) && !_run.ContainsKey(neighbour))
                    {
                        _run[neighbour] = _runs.Count;
                        stack.Push(neighbour);
                    }
                }
            }
            _runs.Add(run);
        }
    }

    /// <summary>Is this tile a 1-tile-wide corridor cell?</summary>
    public bool IsChoke(Position tile) => _choke.Contains(tile);

    /// <summary>Number of corridor cells the map declares, for forensics.</summary>
    public int ChokeCount => _choke.Count;

    /// <summary>
    /// Every corridor cell connected to this one, including itself. Empty for a
    /// tile that is not a choke. This is the set a precedence rule reserves,
    /// because reserving only the entry cell lets a second body walk into the
    /// far end of the same run.
    /// </summary>
    public IReadOnlyList<Position> RunOf(Position tile) =>
        _run.TryGetValue(tile, out int id) ? _runs[id] : [];

    /// <summary>
    /// Walking distance from <paramref name="from"/> to the nearest goal,
    /// treating <paramref name="blocked"/> as walls. −1 when no route exists.
    /// </summary>
    public int Route(
        Position from,
        IReadOnlyCollection<Position> goals,
        IReadOnlySet<Position> blocked)
    {
        if (goals.Count == 0)
            return -1;
        Dictionary<Position, int> distances =
            ArenaGeometry.Distances(_map, from, blocked);
        int best = -1;
        foreach (Position goal in goals)
        {
            if (!distances.TryGetValue(goal, out int distance))
                continue;
            if (best < 0 || distance < best)
                best = distance;
        }
        return best;
    }

    /// <summary>
    /// What walling <paramref name="tile"/> costs a body at
    /// <paramref name="from"/> that is trying to reach <paramref name="goals"/>:
    /// the extra steps it would have to walk. <see cref="Severed"/> when the
    /// goal becomes unreachable, 0 when the tile was not on any shortest route
    /// anyway.
    ///
    /// <para>This is the whole arithmetic behind the assignment. A rooted turret
    /// or a raised shell is a permanent wall on its tile — for my own traffic as
    /// much as the enemy's — so before taking one, the doctrine asks the map what
    /// that wall costs, per body, on each side. It is a subtraction of two
    /// breadth-first searches, and it needs no opponent model and no map
    /// knowledge at all.</para>
    /// </summary>
    public int WallCost(
        Position from,
        IReadOnlyCollection<Position> goals,
        IReadOnlySet<Position> blocked,
        Position tile)
    {
        if (from == tile)
            return 0;
        int before = Route(from, goals, blocked);
        if (before < 0)
            return 0;   // already stuck: this tile is not what is stopping it
        var walled = new HashSet<Position>(blocked) { tile };
        int after = Route(from, goals, walled);
        return after < 0 ? Severed : Math.Max(0, after - before);
    }

    /// <summary>
    /// The tiles a body at <paramref name="from"/> heading for
    /// <paramref name="goals"/> could need on its NEXT step — the union of every
    /// cardinal neighbour that starts a shortest route.
    ///
    /// <para>The union, and not "the step it will take", is the point. Two lives
    /// choose between equal-length routes with per-life state (revision 5 used a
    /// per-life random bool), so no life can know which of two equal steps a
    /// sibling picked. Claiming all of them is the only derivation that is
    /// identical in every life, which is the only kind of derivation that can
    /// coordinate anything here.</para>
    /// </summary>
    public void FirstSteps(
        Position from,
        IReadOnlyCollection<Position> goals,
        IReadOnlySet<Position> blocked,
        HashSet<Position> into)
    {
        int best = Route(from, goals, blocked);
        if (best <= 0)
            return;
        foreach (Direction direction in ArenaGeometry.Cardinals)
        {
            Position next = ArenaGeometry.Step(from, direction);
            if (!ArenaGeometry.IsOpen(_map, next) || blocked.Contains(next))
                continue;
            if (Route(next, goals, blocked) == best - 1)
                into.Add(next);
        }
    }

    /// <summary>
    /// The rear-most free tile of an arrival region measured along a team's own
    /// advance direction — the exact tile a forward-rallied return will take.
    ///
    /// <para>The contract declares WHERE arrivals land as a region and HOW they
    /// are ordered as a policy ("the rear-most free tile of that region measured
    /// along your own advance direction"). Which tile that resolves to depends on
    /// what is free, which is the one part of it a doctrine controls: stand on
    /// the rear-most tile and your own reinforcement appears one tile FURTHER
    /// FORWARD — deeper into the fight it was returning from. That is rallying
    /// into your own traffic, and it is caused by standing still in the wrong
    /// place rather than by any decision the arriving body makes.</para>
    ///
    /// <para>Null when the region is fully occupied, which is itself the answer:
    /// there is no lane to keep clear and the placement is out of my hands.</para>
    /// </summary>
    public static Position? RearMostFree(
        IReadOnlyList<Position> region,
        int advanceDelta,
        IReadOnlySet<Position> occupied)
    {
        Position? best = null;
        int bestKey = 0;
        foreach (Position tile in region)
        {
            if (occupied.Contains(tile))
                continue;
            // Rear-most along our own advance: advancing toward higher x means
            // the rear is the lowest x. The delta's sign is a contract fact.
            int key = advanceDelta >= 0 ? tile.X : -tile.X;
            if (best is null || key < bestKey
                || (key == bestKey && tile.Y < best.Value.Y))
            {
                best = tile;
                bestKey = key;
            }
        }
        return best;
    }
}
