using BotArena.Sdk;

/// <summary>
/// COORDINATION AMONG THIS PARTICIPANT'S OWN BODIES.
///
/// <para>One artifact controls every body, but each body is an isolated life with
/// its own runtime and empty private memory, and a life never sees an ally's
/// current action — observations are frozen before any same-tick decision
/// executes. So there is no channel to negotiate over and nothing to remember.
/// What there IS, and what this file is built on, is that every one of my lives
/// receives the SAME frozen team-perception union on the same tick. Any function
/// of that union alone therefore evaluates identically in every sibling, which is
/// enough to assign right of way without shared state: not a negotiation, a
/// convention both sides compute.</para>
///
/// <para>The convention is <see cref="MyRank"/>, a strict total order over my own
/// live bodies. A body yields only to siblings ABOVE it in that order, so the
/// leader never yields and a cycle of mutual yielding cannot form — which is the
/// property a coordination rule has to have before it is safe to add, because the
/// failure mode of "everybody politely stops" is worse than the collision it was
/// meant to prevent.</para>
///
/// <para>Everything else here is read from the contract: which tiles are one-tile
/// corridors comes from the map, what a sibling's committed route is comes from
/// its form's declared facing coupling, where a companion will arrive comes from
/// the declared automatic-return placement, and what one enemy answer covers
/// comes from the attack profiles and the declared projectile guard.</para>
/// </summary>
internal sealed class Convoy
{
    /// <summary>
    /// The coordination rules, each independently measurable. Flipping one and
    /// rebuilding IS the ablation, which is why the rejected two are still here
    /// with their code intact: a rejection nobody can reproduce is an assertion,
    /// and every number in DX.md came from a build of this file with one of these
    /// five values changed and nothing else.
    /// </summary>
    public static readonly bool LaneYield = true;

    public static readonly bool ChokeEtiquette = true;

    /// <summary>
    /// Measured and REJECTED as a positional rule — see DX. Retained as a
    /// constant so the rejection is reproducible from this source rather than
    /// only from a report: flipping it back rebuilds the variant that lost.
    /// </summary>
    public static readonly bool RallyGuard = false;

    /// <summary>
    /// The deflection return as a shared enemy answer. Unexercised in every cell
    /// this isolation permits, because no striker form declares a projectile
    /// guard, so it is verified against the contract and costs nothing.
    /// </summary>
    public static readonly bool DeflectionSpacing = true;

    /// <summary>
    /// Discounting cover a sibling's pose already provides. Measured and
    /// REJECTED: it wins on its own and loses composed with the traffic rules —
    /// see DX. Kept as a constant for the same reason as <see cref="RallyGuard"/>.
    /// </summary>
    public static readonly bool CoverComplement = false;

    /// <summary>
    /// Whether a tile of the contested objective is exempt from the two traffic
    /// rules. Measured and REJECTED, against my own prediction — see
    /// <see cref="Scoring"/>.
    /// </summary>
    public static readonly bool ScoringExempt = false;

    /// <summary>
    /// Whether a sibling's lane claim requires EVIDENCE OF MOTION. Without it a
    /// claim is derived from facing alone, which under a facing-locked coupling
    /// is exact about what a body <em>may</em> do and silent about whether it
    /// intends to — and a standoff doctrine spends most of its ticks pointing a
    /// gun across an approach it has no intention of walking down. See
    /// <see cref="Route"/>.
    ///
    /// <para>Measured and REJECTED, and it is the most interesting of the four
    /// rejections: being exactly right about a sibling's intent is WORSE than
    /// over-claiming. A parked muzzle's two forward tiles turn out to be ground a
    /// sibling should not be standing on anyway, so the imprecise claim quietly
    /// does a second job the precise one gives up.</para>
    /// </summary>
    public static readonly bool MotionEvidence = false;

    /// <summary>Tiles of a sibling's route this rule looks ahead over.</summary>
    private const int ClaimDepth = 2;

    /// <summary>Corridor run length searched either way from a choke tile.</summary>
    private const int CorridorSpan = 4;

    /// <summary>
    /// How soon an arrival has to be due before its landing tile is worth
    /// keeping clear. Sized from the contract's own capture arithmetic rather
    /// than picked: a companion that lands after the point has already changed
    /// hands was never in my traffic.
    /// </summary>
    private readonly int _arrivalHorizon;

    private readonly Field _field;
    private readonly Doctrine _doctrine;
    private readonly List<Body> _bodies = [];
    private readonly List<Position> _rallyGuarded = [];
    private readonly List<Guard> _guards = [];
    private readonly HashSet<Position> _scoring;
    private int _myRank;

    /// <summary>One own body as every sibling sees it, plus its committed route.</summary>
    private readonly struct Body(
        ActorIdentity actorId,
        Position position,
        Direction facing,
        int cost,
        bool mobile,
        Position[] claim)
    {
        public ActorIdentity ActorId { get; } = actorId;

        public Position Position { get; } = position;

        public Direction Facing { get; } = facing;

        /// <summary>Route cost to the contested point; the rank's first key.</summary>
        public int Cost { get; } = cost;

        /// <summary>Whether this body can take a step at all this tick.</summary>
        public bool Mobile { get; } = mobile;

        /// <summary>Tiles this body's route needs this tick and next.</summary>
        public Position[] Claim { get; } = claim;
    }

    /// <summary>An enemy body that returns bolts along the bearing they arrived on.</summary>
    private readonly record struct Guard(Position Origin, int Range, bool Strict);

    public Convoy(
        Field field,
        Doctrine doctrine,
        GenericActorContext context,
        int[] objectiveCost,
        Position[] activeTiles)
    {
        _field = field;
        _doctrine = doctrine;
        _scoring = activeTiles.ToHashSet();
        _arrivalHorizon = Math.Max(4, doctrine.CaptureTicks(context.Tick) / 2);

        // The body list is built from SELF PLUS ALLIES and then ordered, so every
        // sibling assembles the identical list from its own vantage point. A rule
        // computed off "my allies" alone would be a different rule in each body
        // and the two halves would disagree about who yields.
        Add(
            context,
            context.Self.ActorId,
            context.Self.Position,
            context.Self.Facing,
            context.Self.FormId,
            context.Self.PendingSameLifeTransition is not null,
            context.Self.PreviousActionResolution,
            objectiveCost);
        foreach (var ally in context.Allies)
        {
            if (ally.ActorId.Equals(context.Self.ActorId))
                continue;
            Add(
                context,
                ally.ActorId,
                ally.Position,
                ally.Facing,
                ally.FormId,
                ally.PendingSameLifeTransition is not null,
                ally.PreviousActionResolution,
                objectiveCost);
        }

        // THE CONVENTION. Nearer the contested point leads, because that is the
        // body whose route is most nearly spent and the one a corridor exists to
        // deliver. Stable-slot identity breaks a tie, and the life ID breaks a
        // tie inside one slot, so the order is total and every sibling derives
        // the same one from the same frozen union.
        _bodies.Sort(Compare);
        for (int index = 0; index < _bodies.Count; index++)
        {
            if (_bodies[index].ActorId.Equals(context.Self.ActorId))
                _myRank = index;
        }

        ResolveRallyGuard(context, activeTiles);
        ResolveGuards(context);
    }

    private static int Compare(Body left, Body right)
    {
        if (left.Cost != right.Cost)
            return left.Cost.CompareTo(right.Cost);
        if (left.ActorId.UnitId != right.ActorId.UnitId)
            return left.ActorId.UnitId.CompareTo(right.ActorId.UnitId);
        return left.ActorId.LifeId.CompareTo(right.ActorId.LifeId);
    }

    private void Add(
        GenericActorContext context,
        ActorIdentity actorId,
        Position position,
        Direction facing,
        string formId,
        bool winding,
        GenericActorActionResolution? previous,
        int[] objectiveCost)
    {
        bool mobile = !winding
            && _doctrine.FormAllows(
                formId,
                GenericActorRulesContract.ActionKind.Movement);
        _bodies.Add(
            new Body(
                actorId,
                position,
                facing,
                _field.Cost(objectiveCost, position),
                mobile,
                mobile
                    ? Route(position, facing, formId, previous, objectiveCost)
                    : []));
    }

    /// <summary>
    /// The tiles a body's route needs this tick and next, derived from its form's
    /// DECLARED facing coupling rather than from a habit.
    ///
    /// <para>Under a facing-locked coupling this is exact and it is the whole
    /// reason the rule can be written at all: the movement mask offers only the
    /// current facing, so the single tile a sibling may step to is the one ahead
    /// of it, and the tile after that is the one ahead of THAT. Nothing needs to
    /// be guessed. Under a coupling that lets a body step any cardinal there is
    /// no single committed tile, so the claim falls back to the continuation of
    /// the step it was last seen to take — a body already walking a line is the
    /// only body whose next tile is predictable — and to nothing at all when it
    /// has not moved. A claim this code is not sure of is not a claim.</para>
    /// </summary>
    private Position[] Route(
        Position position,
        Direction facing,
        string formId,
        GenericActorActionResolution? previous,
        int[] objectiveCost)
    {
        Direction heading;
        Direction? stepped = LastStep(previous);
        if (_doctrine.Coupling(formId)
            == GenericActorRulesContract.MovementFacingCoupling.FacingLocked)
        {
            heading = facing;
        }
        else if (stepped is Direction walked)
        {
            heading = walked;
        }
        else
        {
            return [];
        }

        (int dx, int dy) = heading.Vector();

        // A MUZZLE BEARING IS NOT A ROUTE, and this is the correction that
        // distinguishes the two. Under a locked coupling the facing is exact
        // about what a body MAY do next and says nothing about whether it means
        // to: this doctrine spends most of its ticks holding a station with the
        // gun laid across an approach it has no intention of walking down, and a
        // claim taken off facing alone reserves two tiles in front of every
        // parked muzzle on the team. So a claim needs evidence of travel —
        // either the body was last seen taking exactly this step, or the tile
        // ahead of it strictly shortens its own route to the contested point.
        if (MotionEvidence)
        {
            bool walking = stepped == heading;
            Position ahead = position.Offset(dx, dy);
            bool progressing = _field.Cost(objectiveCost, ahead)
                < _field.Cost(objectiveCost, position);
            if (!walking && !progressing)
                return [];
        }
        var claim = new List<Position>(ClaimDepth);
        Position cursor = position;
        for (int step = 0; step < ClaimDepth; step++)
        {
            cursor = cursor.Offset(dx, dy);
            if (_field.IsWall(cursor))
                break;
            claim.Add(cursor);
        }
        return claim.ToArray();
    }

    private static Direction? LastStep(GenericActorActionResolution? previous)
    {
        if (previous is not
            {
                Outcome: GenericActorActionResolution.ActionOutcome.Success,
            } resolved)
        {
            return null;
        }
        foreach (var argument in resolved.AcceptedAction.Arguments)
        {
            if (argument is GenericActorActionArgument.DirectionArgument direction)
                return direction.Value;
        }
        return null;
    }

    /// <summary>Whether more than one of my bodies is on the board at all.</summary>
    public bool Crowded => _bodies.Count > 1;

    /// <summary>This body's place in the convention; 0 leads and never yields.</summary>
    public int MyRank => _myRank;

    /// <summary>
    /// Where a named body sits in the same convention, or null when it is not in
    /// the frozen union this tick. Every sibling computes the identical list, so
    /// this answers "does that body outrank me" the same way in both bodies.
    /// </summary>
    public int? RankOf(ActorIdentity actorId)
    {
        for (int index = 0; index < _bodies.Count; index++)
        {
            if (_bodies[index].ActorId.Equals(actorId))
                return index;
        }
        return null;
    }

    /// <summary>
    /// RULE 1 — THE LANE CLAIM. What standing on or stepping onto this tile costs
    /// because a better-ranked sibling's route needs it.
    ///
    /// <para>The engine's own movement rules are what make this expensive rather
    /// than merely untidy: a same-destination move blocks BOTH bodies, and
    /// following a vacated actor blocks too — so a body queued directly behind a
    /// sibling cannot even inherit the tile the sibling just left. The tick is
    /// simply gone. And because the mask under a locked coupling offers exactly
    /// one direction, a sibling standing in that direction does not slow the
    /// body down, it removes movement from its vocabulary until it spends a tick
    /// turning somewhere it did not want to face.</para>
    ///
    /// <para>Holding such a tile is charged MORE than stepping onto it, which is
    /// the asymmetry the owner-visible failure needs: a body that walks across a
    /// sibling's lane is in the way for one tick, and a body that parks there is
    /// in the way until something else moves it.</para>
    /// </summary>
    public double LaneCost(Position tile, bool isStay)
    {
        if (!LaneYield || _bodies.Count < 2 || Scoring(tile))
            return 0;
        double cost = 0;
        for (int rank = 0; rank < _myRank; rank++)
        {
            Body leader = _bodies[rank];
            Position[] claim = leader.Claim;
            for (int step = 0; step < claim.Length; step++)
            {
                if (claim[step] != tile)
                    continue;
                // The tile it needs THIS tick outranks the tile it needs next.
                double weight = step == 0 ? 3.2 : 1.3;
                cost += isStay ? weight * 1.6 : weight;
            }
        }
        return cost;
    }

    /// <summary>
    /// RULE 2 — CHOKE PRECEDENCE. A one-tile corridor cannot be shared, only
    /// taken turns in, so it gets an explicit rule rather than a preference.
    ///
    /// <para>A choke tile is read off the map: an open tile whose two
    /// perpendicular neighbours are both walls, so a body on it cannot be passed
    /// and cannot dodge sideways. The rule has three clauses, and the third is
    /// the one that answers the owner's complaint:</para>
    ///
    /// <list type="number">
    /// <item>a body may CROSS a corridor a better-ranked sibling is not using;</item>
    /// <item>a body may not enter a corridor run a better-ranked sibling already
    /// occupies or whose route enters it — the leader clears it first;</item>
    /// <item>a body may not PARK in a corridor at all while a sibling exists.
    /// Crossing costs the sibling a tick; parking costs it the whole route, and
    /// a standoff doctrine whose station happens to sit in a corridor will park
    /// there for a hundred ticks without ever noticing what it has walled
    /// off.</item>
    /// </list>
    /// </summary>
    public double ChokeCost(Position tile, bool isStay)
    {
        if (!ChokeEtiquette
            || _bodies.Count < 2
            || Scoring(tile)
            || !IsChoke(tile))
        {
            return 0;
        }

        double cost = isStay ? 2.6 : 0.0;
        for (int rank = 0; rank < _myRank; rank++)
        {
            Body leader = _bodies[rank];
            if (SameCorridor(tile, leader.Position))
            {
                cost += 4.0;
                continue;
            }
            foreach (Position claimed in leader.Claim)
            {
                if (SameCorridor(tile, claimed))
                {
                    cost += 2.0;
                    break;
                }
            }
        }
        return cost;
    }

    /// <summary>
    /// THE EXEMPTION I EXPECTED TO NEED AND MEASURED AWAY. The argument for it
    /// was strong: a tile of the contested objective is the only ground the match
    /// is scored on, so a body standing there should never yield it to a sibling
    /// merely walking past, and the engine makes the handover lossy anyway —
    /// following a vacated actor blocks, so the sibling cannot even take the tile
    /// on the tick it is given up.
    ///
    /// <para>It loses. Exempting the point costs about three quarters of a point
    /// of mean territory and puts a third of the sibling-blocking back (DX has
    /// the numbers). The reason is that the objective is a REGION, not a tile:
    /// the yielding body steps to another tile of the same region, the team's
    /// objective weight never drops, and what the yield actually buys is that the
    /// better-ranked body stops being stalled on the approach. So the honest rule
    /// is the plain one — no ground is exempt — and this switch exists so the
    /// variant that lost can be rebuilt rather than merely believed.</para>
    /// </summary>
    private bool Scoring(Position tile) =>
        ScoringExempt && _scoring.Contains(tile);

    /// <summary>
    /// Whether a tile is one tile wide: both neighbours across some axis are
    /// walls, so it admits one body at a time and offers no lateral dodge.
    /// </summary>
    public bool IsChoke(Position tile)
    {
        if (!_field.IsOpen(tile))
            return false;
        bool vertical = _field.IsWall(tile.Offset(0, -1))
            && _field.IsWall(tile.Offset(0, 1));
        bool horizontal = _field.IsWall(tile.Offset(-1, 0))
            && _field.IsWall(tile.Offset(1, 0));
        return vertical || horizontal;
    }

    /// <summary>
    /// Whether two tiles lie in one continuous run of corridor tiles — the unit
    /// a precedence rule has to reason about, because entering either end of a
    /// two-tile corridor commits the whole thing.
    /// </summary>
    private bool SameCorridor(Position tile, Position other)
    {
        if (tile == other)
            return true;
        if (!IsChoke(other))
            return false;
        foreach (Direction direction in Field.Cardinals)
        {
            (int dx, int dy) = direction.Vector();
            Position cursor = tile;
            for (int step = 1; step <= CorridorSpan; step++)
            {
                cursor = cursor.Offset(dx, dy);
                if (_field.IsWall(cursor))
                    break;
                if (cursor == other)
                    return true;
                if (!IsChoke(cursor))
                    break;
            }
        }
        return false;
    }

    /// <summary>
    /// RULE 3 — DO NOT STAND ON YOUR OWN LANDING PAD. Where the contract rallies
    /// automatic arrivals forward, a companion or a returning body does not
    /// appear at home: the declared placement puts it on the rear-most FREE tile
    /// of this team's own-side objective region, measured along this team's own
    /// advance direction. Which tile that is therefore depends on where my bodies
    /// are standing, and this is the one place the contract hands me placement
    /// influence at all.
    ///
    /// <para>Standing on the rear tile does not block the arrival — it pushes it
    /// forward, into the rank my own bodies are contesting and one tile nearer
    /// the opposing gun. So the rule is narrow on purpose: only the tiles that
    /// are currently the landing choice, and only while an arrival is actually
    /// due inside a window the capture arithmetic says matters.</para>
    /// </summary>
    public double RallyCost(Position tile)
    {
        if (!RallyGuard || _rallyGuarded.Count == 0)
            return 0;
        foreach (Position guarded in _rallyGuarded)
        {
            if (guarded == tile)
                return 2.2;
        }
        return 0;
    }

    private void ResolveRallyGuard(
        GenericActorContext context,
        Position[] activeTiles)
    {
        if (!RallyGuard || !_doctrine.RallyForward)
            return;

        bool due = false;
        foreach (var slot in context.TeamUnits)
        {
            if (slot.TeamId != _doctrine.TeamId)
                continue;
            int dueTick = slot.State switch
            {
                GenericActorContext.UnitSlotState.AvailabilityPending pending =>
                    pending.DueTick,
                GenericActorContext.UnitSlotState.AutomaticReturnPending returning =>
                    returning.DueTick,
                _ => int.MaxValue,
            };
            if (dueTick != int.MaxValue
                && dueTick - context.Tick <= _arrivalHorizon)
            {
                due = true;
                break;
            }
        }
        if (!due)
            return;

        // The declared arrival region, read through the same helper the contract
        // documents rather than reconstructed from a spawn or an index.
        Position[] region = ArenaBasics.ExpectedArrivalTiles(
            _doctrine.Contract,
            context);
        if (region.Length == 0)
            return;

        var taken = new HashSet<Position>();
        foreach (var ally in context.Allies)
            taken.Add(ally.Position);
        foreach (var enemy in context.Enemies)
            taken.Add(enemy.Position);

        // "Rear-most along my own advance" is the smallest projection onto the
        // forward axis. The tiles this body could vacate are exactly the free
        // ones plus the one it is standing on.
        int rear = int.MaxValue;
        foreach (Position tile in region)
        {
            if (taken.Contains(tile) && tile != context.Self.Position)
                continue;
            rear = Math.Min(rear, _doctrine.Project(tile));
        }
        if (rear == int.MaxValue)
            return;
        foreach (Position tile in region)
        {
            if (_doctrine.Project(tile) == rear
                && (!taken.Contains(tile) || tile == context.Self.Position))
            {
                _rallyGuarded.Add(tile);
            }
        }

        // An arrival landing on the contested point itself is not traffic, it is
        // reinforcement; never push a body off ground it is actually holding.
        foreach (Position active in activeTiles)
            _rallyGuarded.Remove(active);
    }

    /// <summary>
    /// RULE 4 — SPACING, INCLUDING THE ANSWER WAVE 5 DID NOT COUNT. Two of my
    /// bodies inside one enemy answer are two bodies one enemy decision handles.
    /// Wave 5 charged for a shared muzzle cone and a shared fan; it did not charge
    /// for the DEFLECTION RETURN, which is a whole third answer with different
    /// geometry: a guarded form sends the arriving bolt back from its own tile
    /// along the exactly reversed heading, so two of my bodies on one ray out of
    /// that body are both on the return's lane, and the second one is hit by a
    /// bolt my own team fired.
    ///
    /// <para>It is a cost, not a prohibition — the argmax only pays it when an
    /// equal-value pose is not available elsewhere.</para>
    /// </summary>
    public bool SharesDeflectionLane(Position tile, Position other)
    {
        if (!DeflectionSpacing || _guards.Count == 0 || tile == other)
            return false;
        foreach (Guard guard in _guards)
        {
            if (OnSameRay(guard, tile, other))
                return true;
        }
        return false;
    }

    private bool OnSameRay(Guard guard, Position first, Position second)
    {
        for (int sector = 0; sector < 8; sector++)
        {
            (int dx, int dy) = ((ProjectileHeading)sector).Vector();
            bool sawFirst = false;
            bool sawSecond = false;
            Position cursor = guard.Origin;
            for (int step = 1; step <= guard.Range; step++)
            {
                Position next = cursor.Offset(dx, dy);
                if (_field.IsWall(next))
                    break;
                if (guard.Strict
                    && dx != 0
                    && dy != 0
                    && (_field.IsWall(cursor.Offset(dx, 0))
                        || _field.IsWall(cursor.Offset(0, dy))))
                {
                    break;
                }
                sawFirst |= next == first;
                sawSecond |= next == second;
                cursor = next;
            }
            if (sawFirst && sawSecond)
                return true;
        }
        return false;
    }

    private void ResolveGuards(GenericActorContext context)
    {
        if (!DeflectionSpacing || context.Enemies.IsEmpty)
            return;
        foreach (var enemy in context.Enemies)
        {
            if (!_doctrine.Guarded(enemy.FormId))
                continue;
            var attack = _doctrine.Attack(enemy.FormId);
            _guards.Add(
                new Guard(
                    enemy.Position,
                    attack?.Projectile.MaxTravelTiles ?? _doctrine.OpposingAnyRange,
                    attack?.Projectile.DiagonalCornersMustBeClear ?? true));
        }
    }

    /// <summary>
    /// RULE 4b — DO NOT WATCH THE APPROACH A SIBLING IS ALREADY WATCHING. With
    /// three launch lanes per facing, two bodies pointed the same way cover very
    /// nearly the same ground, and the second one has bought nothing: the answer
    /// to a body that may arrive from two bearings is one gun on each bearing,
    /// not two guns on the likelier one. So cover a sibling's CURRENT pose
    /// already provides at the same quality is discounted rather than counted
    /// twice.
    ///
    /// <para>The discount is deliberately partial. Two guns on one body still
    /// kill it faster, and a body worth killing is worth double-covering; what
    /// the rule removes is the case where a rotation is chosen because it
    /// duplicates a line that already exists.</para>
    /// </summary>
    public double SiblingAlreadyCovers(
        GenericActorRulesContract.AttackProfile attack,
        Position target,
        double quality)
    {
        if (!CoverComplement || _bodies.Count < 2 || quality <= 0)
            return 0;
        for (int rank = 0; rank < _bodies.Count; rank++)
        {
            if (rank == _myRank)
                continue;
            Body sibling = _bodies[rank];
            ForkPlanner.Cover cover = ForkPlanner.CoverKind(
                _field,
                sibling.Position,
                target,
                attack,
                sibling.Facing);
            if (cover == ForkPlanner.Cover.Direct)
                return quality * 0.45;
            if (cover == ForkPlanner.Cover.Curved)
                return quality * 0.2;
        }
        return 0;
    }

    /// <summary>
    /// Whether this station tile is one a sibling has already taken or walled
    /// off. A standoff band is a set of tiles, not a tile, and two bodies wanting
    /// the same one is the difference between two guns across an approach and one
    /// gun with a queue behind it.
    /// </summary>
    public bool StationTaken(Position tile)
    {
        if (!LaneYield || _bodies.Count < 2)
            return false;
        for (int rank = 0; rank < _bodies.Count; rank++)
        {
            if (rank == _myRank)
                continue;
            Body sibling = _bodies[rank];
            if (sibling.Position == tile)
                return true;
            if (rank < _myRank)
            {
                foreach (Position claimed in sibling.Claim)
                {
                    if (claimed == tile)
                        return true;
                }
            }
        }
        return false;
    }
}
