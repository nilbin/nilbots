using BotArena.Sdk;

/// <summary>
/// The wave-6 coordination layer: how four bodies of one team stay out of each
/// other's way without any shared memory.
///
/// <para>Nothing here is a channel. Every life is a fresh instance with empty
/// private fields, so the only thing four bodies can agree on is a FUNCTION of
/// the frozen observation they all receive — and allied perception is an
/// immediate union, so they do all receive the same allied body set. This class
/// is that function: given the shared observation it produces one total
/// precedence order, one assignment of bodies to objective tiles, and one
/// estimate of what each sibling's next step needs. Four independent lives
/// running it reach identical conclusions, which is what makes a yield rule
/// possible at all. A rule that says "the other body goes first" is worthless
/// unless both bodies compute the same "other".</para>
///
/// <para>The five rules are stated where they are implemented, and each names
/// the contract fact it rests on. The precedence order in
/// <see cref="RightOfWay"/> is the one written rule the whole layer defers to,
/// so "who yields" is never ambiguous and never a coin flip.</para>
/// </summary>
internal sealed class Squad
{
    /// <summary>
    /// A body of mine, reduced to what coordination needs. Built for the self
    /// and for every visible ally, from the same observation fields, so the
    /// self is not a special case in any of the arithmetic below.
    /// </summary>
    private readonly struct Member
    {
        public Member(
            ActorIdentity actorId,
            Position position,
            Direction facing,
            int weight,
            bool canFabricate,
            int distance,
            bool isSelf)
        {
            ActorId = actorId;
            Position = position;
            Facing = facing;
            Weight = weight;
            CanFabricate = canFabricate;
            Distance = distance;
            IsSelf = isSelf;
        }

        public ActorIdentity ActorId { get; }
        public Position Position { get; }
        public Direction Facing { get; }
        public int Weight { get; }
        public bool CanFabricate { get; }
        public int Distance { get; }
        public bool IsSelf { get; }
    }

    private const int MaxMembers = 8;
    private const int MaxTiles = 8;
    private const int UnreachablePenalty = 1 << 16;

    private readonly List<Member> _members = [];
    private readonly int[] _assigned = new int[MaxMembers];
    private readonly int[] _trial = new int[MaxMembers];
    private readonly int[] _best = new int[MaxMembers];
    private readonly bool[] _used = new bool[MaxTiles];

    // Tiles a better-right-of-way sibling's route needs. Two lists, because
    // "needs this tick" and "needs next tick" are different claims: the first
    // is a same-destination block the engine would refuse outright, the second
    // is the jam that shows up as a body standing in a corridor.
    private readonly List<Position> _claimNow = [];
    private readonly List<Position> _claimNext = [];

    // Choke runs a better-right-of-way sibling already occupies or is entering.
    private readonly List<int> _takenRuns = [];

    // Weighted siblings' tiles, for the spacing rule.
    private readonly List<Position> _siblings = [];

    private Position[] _objective = [];
    private int _selfIndex = -1;
    private int _horizon = 8;

    /// <summary>This body's assigned objective tile, when it has one.</summary>
    public Position Bearing { get; private set; }

    /// <summary>True when the bearing assignment gave this body its own tile.</summary>
    public bool HasBearing { get; private set; }

    /// <summary>
    /// Walk field to <see cref="Bearing"/>, cached for the life by the lens.
    /// Null when this body has no assignment and should route to the region as
    /// a whole.
    /// </summary>
    public int[]? BearingField { get; private set; }

    /// <summary>Choke run this body currently stands in, or zero.</summary>
    public int RunHere { get; private set; }

    /// <summary>
    /// Rebuilds the whole coordination picture for this tick.
    /// </summary>
    /// <param name="lens">Contract lens for this life.</param>
    /// <param name="context">This tick's frozen observation.</param>
    /// <param name="objective">Active objective tiles.</param>
    /// <param name="objectiveField">Shared walk field to the active region.</param>
    /// <param name="coupling">This form's declared movement facing coupling.</param>
    public void Resolve(
        ContractLens lens,
        GenericActorContext context,
        Position[] objective,
        int[] objectiveField,
        GenericActorRulesContract.MovementFacingCoupling coupling)
    {
        _objective = objective;
        _members.Clear();
        _claimNow.Clear();
        _claimNext.Clear();
        _takenRuns.Clear();
        _siblings.Clear();
        _selfIndex = -1;
        HasBearing = false;
        BearingField = null;
        Bearing = default;
        RunHere = lens.ChokeRunAt(context.Self.Position);

        // The horizon is the widest gun the ruleset declares. Coordination is
        // an approach problem: two bodies a whole map apart are not competing
        // for a tile, and including them only makes the assignment jitter as
        // distant bodies wander. The reach at which bodies start interacting is
        // exactly the reach at which one of them can be shot.
        _horizon = Math.Max(4, lens.WidestAttackTiles);

        Add(
            lens,
            context.Self.ActorId,
            context.Self.Position,
            context.Self.Facing,
            context.Self.FormId,
            objectiveField,
            isSelf: true);
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            Add(
                lens,
                ally.ActorId,
                ally.Position,
                ally.Facing,
                PendingForm(ally),
                objectiveField,
                isSelf: false);
        }

        // Precedence order, ascending. Every later step reads this order and
        // nothing reads iteration order, so the answer does not depend on which
        // body is asking.
        _members.Sort(static (a, b) => Key(a).CompareTo(Key(b)));
        for (int index = 0; index < _members.Count; index++)
        {
            if (_members[index].IsSelf)
                _selfIndex = index;
            else if (_members[index].Weight > 0)
                _siblings.Add(_members[index].Position);
        }

        AssignBearings(lens);
        ResolveClaims(lens, coupling, objectiveField);
    }

    private void Add(
        ContractLens lens,
        ActorIdentity actorId,
        Position position,
        Direction facing,
        string formId,
        int[] objectiveField,
        bool isSelf)
    {
        if (_members.Count >= MaxMembers)
            return;
        _members.Add(new Member(
            actorId,
            position,
            facing,
            lens.Form(formId)?.ObjectiveWeight ?? 1,
            lens.CanFabricate(formId),
            Tactics.DistanceAt(lens, objectiveField, position),
            isSelf));
    }

    private static string PendingForm(GenericActorContext.ObservedAllyState ally) =>
        ally.PendingSameLifeTransition?.TargetFormId ?? ally.FormId;

    /// <summary>
    /// THE WRITTEN PRECEDENCE RULE, and the only tie-break anything in this
    /// class is allowed to invent. Lower wins right of way:
    ///
    /// <list type="number">
    /// <item><b>Nearer the active objective</b>, by cardinal walk distance. The
    /// body already closer to the ground is the body whose route the team is
    /// actually spending; making the leader wait for the follower converts one
    /// body's progress into none.</item>
    /// <item><b>Fabrication-capable first</b>, on a tie. Read from the
    /// contract's fabrication source forms, not from a name. It is the only
    /// body that can replace a loss, so a tick it spends blocked costs the team
    /// a body later, and a tie means the two are equally placed for the
    /// ground.</item>
    /// <item><b>Lower unit slot</b>, then <b>lower life</b>. Both are exact
    /// identity fields, both are visible on every ally, and together they make
    /// the order TOTAL — which is the property the whole layer needs. A partial
    /// order leaves a pair of bodies each waiting for the other.</item>
    /// </list>
    ///
    /// <para>Every term is a function of the shared observation, so all four of
    /// my lives compute the same number for the same body.</para>
    /// </summary>
    private static long Key(in Member member) => RightOfWay(
        member.Distance,
        member.CanFabricate,
        member.ActorId.UnitId,
        member.ActorId.LifeId);

    private static long RightOfWay(
        int distance,
        bool canFabricate,
        int unitId,
        int lifeId) =>
        ((long)Math.Clamp(distance, 0, 4095) << 32)
        + ((long)(canFabricate ? 0 : 1) << 31)
        + ((long)(unitId & 0xFFFF) << 15)
        + (lifeId & 0x7FFF);

    /// <summary>
    /// RULE 1 — ENVELOPMENT ORDER. Assign my objective-bound bodies to DISTINCT
    /// tiles of the active region, minimising the TOTAL walk.
    ///
    /// <para>This is the rule the owner's complaint is really about. Wave 5
    /// routed every body to the nearest tile of the region, which on a four-tile
    /// region with a shared approach means every body descends the same field
    /// through the same corridor and two of them submit the same destination.
    /// The engine refuses same-destination moves outright, so the pair simply
    /// stops — 82 % of the predecessor's blocked moves are exactly this.</para>
    ///
    /// <para>Minimising the total walk rather than each body's own walk is what
    /// makes the routes non-crossing: if two bodies' assignments crossed,
    /// swapping their targets would strictly shorten the sum, so the minimum
    /// contains no crossing pair. That is the whole "assign bearings so no body
    /// crosses another's route" instruction, discharged as an assignment
    /// problem rather than as a heuristic. Ties are resolved by trying tiles in
    /// the region's own declared order for bodies in precedence order and
    /// keeping the first strict minimum, so every life picks the same optimum
    /// out of a tie.</para>
    ///
    /// <para>Walk distances come from <see cref="ContractLens.FieldToTile"/>,
    /// a real cardinal field, not a Chebyshev estimate: around a wall block the
    /// estimate assigns two bodies to targets whose actual routes cross.</para>
    /// </summary>
    private void AssignBearings(ContractLens lens)
    {
        for (int index = 0; index < MaxMembers; index++)
            _assigned[index] = -1;
        if (_objective.Length == 0 || _selfIndex < 0)
            return;

        // Only bodies that can hold ground and are close enough to be competing
        // for a tile take part. A body with objective weight zero is not going
        // to stand on the region at all.
        int count = 0;
        Span<int> pool = stackalloc int[MaxMembers];
        for (int index = 0; index < _members.Count; index++)
        {
            Member member = _members[index];
            if (member.Weight <= 0 || member.Distance > _horizon)
                continue;
            if (count < MaxMembers)
                pool[count++] = index;
        }
        if (count == 0)
            return;

        int tiles = Math.Min(_objective.Length, MaxTiles);
        for (int index = 0; index < tiles; index++)
            _used[index] = false;
        for (int index = 0; index < MaxMembers; index++)
            _best[index] = -1;

        int bestCost = int.MaxValue;
        Search(lens, pool, count, tiles, 0, 0, ref bestCost);

        for (int slot = 0; slot < count; slot++)
            _assigned[pool[slot]] = _best[slot];

        int mine = -1;
        for (int slot = 0; slot < count; slot++)
        {
            if (pool[slot] == _selfIndex)
                mine = _best[slot];
        }
        if (mine < 0)
            return;
        Bearing = _objective[mine];
        BearingField = lens.FieldToTile(Bearing);
        HasBearing = true;
    }

    private void Search(
        ContractLens lens,
        Span<int> pool,
        int count,
        int tiles,
        int slot,
        int cost,
        ref int bestCost)
    {
        if (cost >= bestCost)
            return;
        if (slot >= count)
        {
            bestCost = cost;
            for (int index = 0; index < count; index++)
                _best[index] = _trial[index];
            return;
        }
        Member member = _members[pool[slot]];
        for (int tile = 0; tile < tiles; tile++)
        {
            if (_used[tile])
                continue;
            int walk = Tactics.DistanceAt(
                lens,
                lens.FieldToTile(_objective[tile]),
                member.Position);
            int step = walk >= Tactics.Unreachable ? UnreachablePenalty : walk;
            _used[tile] = true;
            _trial[slot] = tile;
            Search(lens, pool, count, tiles, slot + 1, cost + step, ref bestCost);
            _used[tile] = false;
        }
    }

    /// <summary>
    /// RULE 2 — ROUTE CLAIMS. What each better-right-of-way sibling's next step
    /// needs, derived from the movement rule the contract declares rather than
    /// from a guess.
    ///
    /// <para>Under <c>facing-locked</c> the movement legality mask offers a body
    /// exactly one direction — its current facing — so a sibling's only possible
    /// step this tick is the tile in front of it. That makes the claim EXACT
    /// rather than probabilistic, and it is the reason this rule is cheap on
    /// this arm: I do not have to model my sibling's policy, only the mask the
    /// engine will hand it. A tile is claimed when the sibling faces it, it is
    /// walkable, and stepping there shortens the sibling's own walk to its own
    /// assigned bearing — a sibling facing away from its route is not going to
    /// step forward.</para>
    ///
    /// <para>The NEXT-tick claim is the continuation along the same facing, and
    /// only that. A tile the sibling could reach only by rotating first is two
    /// ticks away, not one, and claiming it would freeze half the map. Where the
    /// contract declares no facing coupling the mask offers every cardinal, so
    /// the claim is made only when exactly one cardinal shortens the sibling's
    /// walk — an unambiguous need. Ambiguity yields no claim, deliberately: a
    /// coordination rule that guesses wrong costs a tick of progress on every
    /// body it guesses about.</para>
    /// </summary>
    private void ResolveClaims(
        ContractLens lens,
        GenericActorRulesContract.MovementFacingCoupling coupling,
        int[] objectiveField)
    {
        if (_selfIndex <= 0)
            return;
        bool locked = coupling
            == GenericActorRulesContract.MovementFacingCoupling.FacingLocked;

        for (int index = 0; index < _selfIndex; index++)
        {
            Member member = _members[index];
            int[] field = _assigned[index] >= 0
                ? lens.FieldToTile(_objective[_assigned[index]])
                : objectiveField;
            int here = Tactics.DistanceAt(lens, field, member.Position);

            Position? step = locked
                ? Descends(lens, field, member.Position, member.Facing, here)
                : SoleDescent(lens, field, member.Position, here);
            if (step is not Position now)
            {
                // Even a sibling that is not walking forward is standing
                // somewhere, and a choke it stands in is a choke I do not
                // enter behind it.
                Take(lens, member.Position);
                continue;
            }
            _claimNow.Add(now);
            Take(lens, now);
            Take(lens, member.Position);

            int after = Tactics.DistanceAt(lens, field, now);
            Position? follow = locked
                ? Descends(lens, field, now, member.Facing, after)
                : SoleDescent(lens, field, now, after);
            if (follow is Position next)
                _claimNext.Add(next);
        }
    }

    private void Take(ContractLens lens, Position tile)
    {
        int run = lens.ChokeRunAt(tile);
        if (run != 0 && !_takenRuns.Contains(run))
            _takenRuns.Add(run);
    }

    private static Position? Descends(
        ContractLens lens,
        int[] field,
        Position from,
        Direction facing,
        int here)
    {
        (int dx, int dy) = facing.Vector();
        Position tile = from.Offset(dx, dy);
        if (lens.IsClosed(tile))
            return null;
        return Tactics.DistanceAt(lens, field, tile) < here ? tile : null;
    }

    private static Position? SoleDescent(
        ContractLens lens,
        int[] field,
        Position from,
        int here)
    {
        Position? found = null;
        foreach (Direction direction in Tactics.Cardinals)
        {
            (int dx, int dy) = direction.Vector();
            Position tile = from.Offset(dx, dy);
            if (lens.IsClosed(tile))
                continue;
            if (Tactics.DistanceAt(lens, field, tile) >= here)
                continue;
            if (found is not null)
                return null;
            found = tile;
        }
        return found;
    }

    /// <summary>
    /// True when a better-right-of-way sibling's route needs this tile now or
    /// next tick. Stepping onto a NOW claim is a same-destination block the
    /// engine refuses; stepping onto a NEXT claim is the jam that reads as one
    /// body standing in front of another.
    /// </summary>
    public bool Claimed(Position tile)
    {
        for (int index = 0; index < _claimNow.Count; index++)
        {
            if (_claimNow[index] == tile)
                return true;
        }
        for (int index = 0; index < _claimNext.Count; index++)
        {
            if (_claimNext[index] == tile)
                return true;
        }
        return false;
    }

    /// <summary>True when only the NEXT-tick claim list holds the tile.</summary>
    public bool ClaimedNow(Position tile)
    {
        for (int index = 0; index < _claimNow.Count; index++)
        {
            if (_claimNow[index] == tile)
                return true;
        }
        return false;
    }

    /// <summary>
    /// RULE 3 — CHOKE PRECEDENCE. Inside one connected run of one-tile corridor,
    /// only the best-right-of-way body of mine may be present.
    ///
    /// <para>The geometry is <see cref="ContractLens.ChokeRunAt"/> — walls only,
    /// computed once. The rule it carries is explicit and one-directional: a
    /// body of mine does not ENTER a run a better-right-of-way sibling occupies
    /// or is committed to enter this tick. Nothing negotiates; the order in
    /// <see cref="RightOfWay"/> decides, and both bodies compute it the same
    /// way.</para>
    ///
    /// <para>Why a run and not a tile: the movement rules refuse
    /// same-destination moves, swaps, and following a vacated actor, so two
    /// bodies anywhere inside one straight corridor cannot resolve past each
    /// other at all. Refusing the tile would still let the second body queue
    /// into the corridor's far end and meet the first head-on with no legal
    /// resolution for either. Refusing the RUN is the rule that actually holds.
    /// A tile I am already standing on is never refused — leaving is
    /// <see cref="Blocking"/>'s job, not this one's.</para>
    /// </summary>
    public bool ChokeTaken(ContractLens lens, Position tile, Position self)
    {
        int run = lens.ChokeRunAt(tile);
        if (run == 0 || run == lens.ChokeRunAt(self))
            return false;
        for (int index = 0; index < _takenRuns.Count; index++)
        {
            if (_takenRuns[index] == run)
                return true;
        }
        return false;
    }

    /// <summary>
    /// True when this body is standing on a tile a better-right-of-way sibling
    /// needs THIS tick — the owner-visible case, and the one a pure entry rule
    /// cannot fix. Refusing to enter a corridor does nothing for the body
    /// already parked in it.
    /// </summary>
    public bool Blocking(Position self) => ClaimedNow(self);

    /// <summary>
    /// RULE 5 — SPACING, as a tie-break only. Two contract facts, both of which
    /// make one enemy decision answer two of my bodies:
    ///
    /// <list type="bullet">
    /// <item>A volley profile launches <c>projectilesPerAttack</c> bolts down
    /// contiguous adjacent headings from one tile, so bodies inside that spread
    /// are one cast rather than two. The separation is the widest fan the
    /// contract declares, so the term is inert on every cell without a
    /// volley.</item>
    /// <item>An ordinary bolt stops on the FIRST enemy body, and a guard's
    /// deflection returns along the exact reverse heading. Two of my bodies on
    /// one eight-way lane inside the widest declared travel are therefore one
    /// bolt's problem, the rear gun is masked by the front one, and a
    /// deflection sent back down that lane passes through both.</item>
    /// </list>
    ///
    /// <para>It is a COST, entering at the lowest order of every score that uses
    /// it, never a veto. The doctrine's own branch —
    /// concentrate what the gun can defend — is what wave 5 measured as winning
    /// and this pass is not allowed to relitigate it; the brief's bar is only
    /// that an EQUAL-VALUE pose should not stack two bodies under one answer.
    /// A tie-break is exactly that bar and nothing more.</para>
    /// </summary>
    public int SpacingCost(ContractLens lens, Position tile)
    {
        int cost = 0;
        int fan = Math.Max(1, lens.WidestFan);
        for (int index = 0; index < _siblings.Count; index++)
        {
            Position other = _siblings[index];
            if (other == tile)
            {
                cost += 4;
                continue;
            }
            int dx = tile.X - other.X;
            int dy = tile.Y - other.Y;
            int gap = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (fan > 1 && gap <= fan - 1)
                cost += 2;
            bool lane = dx == 0 || dy == 0 || Math.Abs(dx) == Math.Abs(dy);
            if (lane
                && gap <= lens.WidestAttackTiles
                && Tactics.ClearRay(lens, other, tile, lens.StrictCorners))
            {
                cost += 2;
            }
        }
        return cost;
    }
}
