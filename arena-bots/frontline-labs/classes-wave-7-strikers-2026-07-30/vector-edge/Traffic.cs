using BotArena.Sdk;

/// <summary>
/// The coordination layer: what this body may do given what its own siblings
/// are doing. Revision 6 is an IQ pass on exactly this and nothing else — the
/// doctrine, the sight-band standoff and the aperture tie-breaks are revision
/// 5's, unchanged.
///
/// <para>There is no shared state and there is no negotiation, because there
/// cannot be: every life is a fresh instance with empty private memory, a life
/// never sees an ally's current action, and the observation is frozen before any
/// same-tick decision executes. What every life DOES share is that frozen
/// picture, so a rule computed from it alone is computed identically by every
/// body of this team on the same tick. That is the whole mechanism: each rule
/// below is a function of the observation, and it is written so that two bodies
/// applying it reach complementary conclusions rather than the same one.</para>
///
/// <para>Each rule below is independently measured and gated by its own constant,
/// so an ablation build differs from the shipped one by a single symbol. One more
/// was built and is NOT here: PAIR SPACING, which refused a pose one enemy facing
/// covered together with a sibling. It reduced shared-fan poses about 10% and cost
/// 1.95 points of progress over twenty seeds in the one cell where it registered,
/// so it failed the wave's own test — remove the silliness WITHOUT losing — and it
/// was deleted rather than shipped behind a false constant. Its numbers are in
/// DX.md.</para>
///
/// <list type="number">
/// <item><b>PRECEDENCE (<see cref="YieldOrder"/>).</b> Own bodies are totally
/// ordered by <c>(walk distance to the active objective, then MORE health, then
/// lower actor identity)</c>. The nearer body holds its tile and its route; the
/// further body yields. Distance first because the nearest body is the one whose
/// route is load-bearing for the score — it is the capturer, by the same test
/// <see cref="Field.IsCapturer"/> already uses. Health second because the body
/// that can survive a corridor should be the one standing in it. Identity last
/// so the order is total and every life computes the same one. Revision 5
/// ordered on identity alone, which is deterministic but says nothing about the
/// game: it let a 1-health body two tiles further out claim the corridor from a
/// full-health capturer.</item>
/// <item><b>YIELDING IS A HOLD, NOT A DETOUR (<see cref="HoldNotDetour"/>).</b> A
/// route step must strictly reduce the route. When the only reducing step is
/// held or claimed by a sibling, the yielding body keeps its tile and spends the
/// tick on its gun. This is the single largest measured effect in the revision,
/// and the failure it removes is worth naming exactly: revision 5's route search
/// returned the CHEAPEST legal first step, and with the one reducing step
/// occupied that is a step AWAY — after which the step back is best again. Under
/// <c>facing-locked</c> each leg also costs a rotation, so the body spends four
/// ticks arriving where it started, forever, while its sibling stands in front
/// of it.</item>
/// <item><b>TWO-TICK ROUTE CLAIM (<see cref="RouteClaim"/>).</b> Every
/// higher-precedence sibling claims the tiles its shortest routes need THIS tick
/// and NEXT — the union over tied shortest paths, because a sibling's own
/// tie-break runs on its private random stream and is not derivable, while the
/// union is. Lower-precedence bodies treat those tiles as taken. Revision 5
/// claimed only the immediate distance-reducing ring, which is the depth-1 half
/// of this.</item>
/// <item><b>CHOKE PRECEDENCE (<see cref="ChokePrecedence"/>).</b> A choke is a
/// one-tile corridor — an open tile whose open cardinal neighbours lie on a
/// single axis — and connected chokes form a RUN. A run admits one of my bodies
/// at a time: the body inside keeps it, a body outside does not enter a run a
/// higher-precedence sibling occupies or claims, and when two of mine are
/// already inside one run the lower-precedence body backs out along the run
/// instead of turning in place. Derived from the wall grid, so it holds on any
/// map.</item>
/// <item><b>DO NOT RALLY INTO OWN TRAFFIC (<see cref="RallyClear"/>).</b> Where
/// the contract gives this team placement influence — a forward rally that lands
/// arrivals on the rear-most free tile of the own side of the active objective,
/// or bounded fabrication placement onto the first free pad tile — an imminent
/// arrival's tile and its only free exit are kept clear, so a fresh 3-health
/// body is not born boxed in by its own family or shoved a tile deeper into
/// contact.</item>
/// <item><b>DISTINCT RAYS (<see cref="DistinctRays"/>).</b> Two of my bodies
/// holding the same ray onto a contact are one firing seat used twice: the rear
/// gun adds a bolt the target dodges with the same step, and the front body eats
/// every answer. Among equal-value seats this body takes a ray no sibling holds,
/// asked over EVERY visible contact rather than only the nearest.</item>
/// </list>
/// </summary>
internal sealed class Traffic
{
    /// <summary>Rule 1: precedence by distance, then health, then identity.</summary>
    public static readonly bool YieldOrder = true;

    /// <summary>Rule 2: a route step must reduce the route; otherwise hold.</summary>
    public static readonly bool HoldNotDetour = true;

    /// <summary>Rule 3a: a senior sibling's route is claimed for THIS tick.</summary>
    public static readonly bool RouteClaim = true;

    /// <summary>
    /// Rule 3b: and for the NEXT tick as well — the second half of bar 1, and the
    /// only genuinely new half, since revision 5 already avoided the tiles a
    /// higher-identity ally could reach this tick. Gated separately so the two
    /// depths can be attributed apart.
    /// </summary>
    public static readonly bool RouteClaimNextTick = true;

    /// <summary>Rule 4: a one-tile corridor run admits one own body at a time.</summary>
    public static readonly bool ChokePrecedence = true;

    /// <summary>Rule 5: keep an imminent own arrival's tile and exit clear.</summary>
    public static readonly bool RallyClear = true;

    /// <summary>Rule 7: among equal seats, take a ray no sibling already holds.</summary>
    public static readonly bool DistinctRays = true;

    /// <summary>
    /// Rule 8: after a step is BLOCKED by this team's own traffic, the junior body
    /// stops trying that tile for a couple of ticks while the senior one keeps
    /// going.
    ///
    /// <para>This is the rule that finally resolved bar 1, and it took three
    /// attempts to find because the first two were about intent and this one is
    /// about a FACT. Two bodies whose routes meet at one tile from opposite sides
    /// both take it and both block; next tick each remembers the block for exactly
    /// one tick through <c>PreviousActionResolution</c>, holds, forgets, and tries
    /// again — a three-tick cycle measured running to the tick cap, 55 blocked
    /// ticks in one match. Refusing a sibling's INTENDED tile fixes it and loses
    /// the mirror 0-4-0, and so does refusing only its forced one, because
    /// hesitating in open ground is how a striker stops contesting ground. What
    /// costs nothing is remembering a collision that already happened, and
    /// breaking the symmetry with the written precedence: the senior body retries
    /// immediately, the junior waits two ticks, so the tile clears in one tick
    /// instead of never.</para>
    /// </summary>
    public static readonly bool RaceMemory = true;

    /// <summary>Ticks a junior body leaves a tile alone after colliding on it.</summary>
    public const int RaceMemoryTicks = 2;

    /// <summary>
    /// How far ahead an arrival has to be before its tile is worth keeping
    /// clear. Beyond this the ground is worth more than the courtesy.
    /// </summary>
    private const int ArrivalHorizon = 4;

    private readonly Doctrine _doctrine;
    private readonly Field _field;
    private readonly GenericActorContext _context;
    private readonly HashSet<Position> _claimed = [];
    private readonly HashSet<Position> _chokeClaimed = [];
    private readonly HashSet<Position> _arrival = [];
    private readonly List<GenericActorContext.ObservedAllyState> _senior = [];
    private readonly List<GenericActorContext.ObservedAllyState> _siblings = [];

    public Traffic(
        Doctrine doctrine,
        Field field,
        GenericActorContext context)
    {
        _doctrine = doctrine;
        _field = field;
        _context = context;

        (int Distance, int Health, ActorIdentity Id) mine = Key(
            context.Self.Position,
            context.Self.Health,
            context.Self.ActorId);
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            _siblings.Add(ally);
            if (Compare(
                    Key(ally.Position, ally.Health, ally.ActorId),
                    mine)
                < 0)
            {
                _senior.Add(ally);
            }
        }

        BuildRouteClaims();
        BuildChokeClaims();
        BuildArrivalClear();
    }

    /// <summary>
    /// Tiles a sibling of higher precedence is standing on or has a committed
    /// route through this tick or next. A step onto one of these spends a tick
    /// for nothing at best and jams a corridor at worst.
    /// </summary>
    public IReadOnlySet<Position> Claimed => _claimed;

    /// <summary>
    /// Tiles inside a one-tile corridor run that a sibling of higher precedence
    /// already owns. Refused outright rather than merely disliked, because a
    /// corridor has no second lane to fall back to.
    /// </summary>
    public IReadOnlySet<Position> ChokeClaimed => _chokeClaimed;

    /// <summary>
    /// Tiles an imminent own arrival needs — its landing tile and, when it has
    /// exactly one, its exit.
    /// </summary>
    public IReadOnlySet<Position> ArrivalClear => _arrival;

    /// <summary>
    /// True when this ally outranks this body under the written precedence rule,
    /// so its tile and its route are its own and this body is the one that
    /// yields. Every life computes this from the same frozen observation, so two
    /// bodies asking it about each other always get complementary answers.
    /// </summary>
    public bool IsSenior(GenericActorContext.ObservedAllyState ally) =>
        Compare(
            Key(ally.Position, ally.Health, ally.ActorId),
            Key(
                _context.Self.Position,
                _context.Self.Health,
                _context.Self.ActorId))
        < 0;

    /// <summary>
    /// Every tile this body should treat as another body's business: a senior
    /// sibling's two-tick route, the corridor runs it owns, and the landing tile
    /// of an imminent arrival of this team's own.
    ///
    /// <para>It is a PREFERENCE, and that tier is a measured finding of the wave
    /// rather than a shortcut. Two stricter versions were built and thrown away.
    /// Making every claim BINDING — hold the tile rather than take a sibling's
    /// route, even when it is the only route — cut jammed route steps by 85% and
    /// rotation thrash by 61%, and lost the striker mirror 0-4-0 at the breach
    /// floor: a body that will not contest open ground because a sibling might
    /// want it has stopped playing. Making only the CORRIDOR claims binding is
    /// the principled middle, and lost too (mirror 2-2-0, +3.25, with MORE
    /// corridor jams than the preference), because on this map the chokes ARE the
    /// routes to the objective, so yielding a passage concedes the front. What
    /// wins is: prefer to leave a sibling's ground alone, take it anyway when it
    /// is the only way forward, and never take a step that does not go
    /// forward.</para>
    /// </summary>
    public IReadOnlySet<Position> Avoid
    {
        get
        {
            var all = new HashSet<Position>();
            if (RouteClaim)
                all.UnionWith(_claimed);
            if (ChokePrecedence)
                all.UnionWith(_chokeClaimed);
            if (RallyClear)
                all.UnionWith(_arrival);
            return all;
        }
    }

    /// <summary>
    /// True when this body is inside a one-tile corridor run that a sibling of
    /// higher precedence is also inside — the case the corridor cannot resolve
    /// by waiting, because the senior body may be walking toward this one.
    /// </summary>
    public bool JammedInChoke =>
        ChokePrecedence
        && _doctrine.IsChoke(_field.Self)
        && _senior.Any(ally =>
            _doctrine.IsChoke(ally.Position)
            && _doctrine.ChokeRun(ally.Position)
                == _doctrine.ChokeRun(_field.Self));

    /// <summary>
    /// True when a sibling of higher precedence already holds the same launch
    /// ray onto some visible contact that <paramref name="tile"/> would hold.
    /// Two guns on one ray are one firing seat used twice.
    /// </summary>
    public bool SharesRay(Position tile)
    {
        if (!DistinctRays || _senior.Count == 0)
            return false;
        GenericActorRulesContract.AttackProfile? attack =
            _doctrine.AttackFor(_field.FormId);
        if (attack is null)
            return false;
        foreach (GenericActorContext.ObservedEnemyState enemy in _context.Enemies)
        {
            int here = Ray(tile, enemy.Position);
            if (here < 0 || !OnLaunchRay(attack, tile, enemy.Position))
                continue;
            foreach (GenericActorContext.ObservedAllyState ally in _senior)
            {
                if (ally.Position == tile)
                    continue;
                if (Ray(ally.Position, enemy.Position) != here)
                    continue;
                GenericActorRulesContract.AttackProfile? theirs =
                    _doctrine.AttackFor(ally.FormId);
                if (theirs is not null
                    && OnLaunchRay(theirs, ally.Position, enemy.Position))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// The step that backs this body out of a corridor run it is sharing with a
    /// senior sibling: the neighbouring tile inside the same run further from
    /// that sibling, or the first tile outside the run. Null when the corridor
    /// has no way out that this body may take.
    /// </summary>
    public Direction? BackOut(IReadOnlySet<Direction> allowed)
    {
        if (!JammedInChoke)
            return null;
        GenericActorContext.ObservedAllyState? senior = _senior
            .Where(ally =>
                _doctrine.IsChoke(ally.Position)
                && _doctrine.ChokeRun(ally.Position)
                    == _doctrine.ChokeRun(_field.Self))
            .OrderBy(ally => ally.Position.ChebyshevDistance(_field.Self))
            .FirstOrDefault();
        if (senior is null)
            return null;
        int run = _doctrine.ChokeRun(_field.Self);
        return allowed
            .Select(direction =>
            {
                (int dx, int dy) = direction.Vector();
                return (Direction: direction, Tile: _field.Self.Offset(dx, dy));
            })
            .Where(candidate =>
                _field.CanEnter(candidate.Tile)
                && candidate.Tile.ChebyshevDistance(senior.Position)
                    > _field.Self.ChebyshevDistance(senior.Position))
            .OrderBy(candidate =>
                _doctrine.IsChoke(candidate.Tile)
                && _doctrine.ChokeRun(candidate.Tile) == run
                    ? 1
                    : 0)
            .ThenBy(candidate => _field.LanePressure(candidate.Tile))
            .ThenBy(candidate =>
                Array.IndexOf(_field.Order, candidate.Direction))
            .Select(candidate => (Direction?)candidate.Direction)
            .FirstOrDefault();
    }

    private void BuildRouteClaims()
    {
        if (!RouteClaim)
            return;
        foreach (GenericActorContext.ObservedAllyState ally in _senior)
        {
            _claimed.Add(ally.Position);
            int distance = _field.DistanceToObjective(ally.Position);
            if (distance <= 0)
                continue;
            // The union over every tied shortest route, two steps deep. A
            // sibling's own tie-break runs on its private stream, so the exact
            // tile is not derivable; the union is, and it is the honest claim.
            foreach (Position first in Downhill(ally.Position, distance))
            {
                _claimed.Add(first);
                if (!RouteClaimNextTick)
                    continue;
                foreach (Position second in Downhill(first, distance - 1))
                    _claimed.Add(second);
            }
        }
    }

    private void BuildChokeClaims()
    {
        if (!ChokePrecedence)
            return;
        foreach (GenericActorContext.ObservedAllyState ally in _senior)
        {
            foreach (Position tile in _claimed.Concat([ally.Position]))
            {
                if (!_doctrine.IsChoke(tile))
                    continue;
                // A corridor is claimed as a whole. Entering the far end of a
                // run a senior body is walking through is the same jam as
                // stepping onto its tile, one tick later.
                foreach (Position member in _doctrine.ChokeRunTiles(tile))
                    _chokeClaimed.Add(member);
            }
        }
        _chokeClaimed.Remove(_field.Self);
    }

    private void BuildArrivalClear()
    {
        if (!RallyClear)
            return;
        // Only where the contract actually gives this team placement influence.
        // A reserved operation publishes its exact tile, which needs no
        // derivation. An automatic return or an unlocking slot does not, and
        // there the influence is real but indirect: a forward rally takes the
        // rear-most FREE tile of the own-side objective and bounded fabrication
        // the first FREE pad tile, so which tile is free is partly this body's
        // choice. Where neither applies the rule adds nothing and says so by
        // claiming nothing.
        var landing = new List<Position>();
        bool forward = ArenaBasics.ArrivalsRallyForward(_doctrine.Contract);
        foreach (GenericActorContext.ObservedUnitSlot slot in _context.TeamUnits)
        {
            if (slot.State is GenericActorContext.UnitSlotState.LifecyclePending
                reserved)
            {
                landing.Add(reserved.ReservedPosition);
                continue;
            }
            if (!Imminent(slot.State))
                continue;
            if (forward)
            {
                // ONE tile, not the region. The contract says which: the
                // rear-most free tile of the own-side chain-adjacent objective
                // measured along this team's own advance direction. Claiming the
                // whole region was built and measured, and it cost 23.5 points of
                // progress and two wins in one cell — of course it did: that
                // region is ground, and a rule that tells a body to vacate four
                // objective tiles so a companion can land on one of them has
                // stopped playing the mode.
                if (RearMost(
                        _doctrine.TilesAt(
                            _field.ActiveIndex - _doctrine.AdvanceDelta))
                    is Position rear)
                {
                    landing.Add(rear);
                }
            }
            else if (RearMost(_doctrine.FabricationSourceTiles) is Position pad)
            {
                landing.Add(pad);
            }
        }

        foreach (Position tile in landing)
        {
            if (!_field.IsPassable(tile))
                continue;
            _arrival.Add(tile);
            // A fresh body with one exit and a sibling on it is born jammed.
            List<Position> exits = Field.Cardinals
                .Select(direction =>
                {
                    (int dx, int dy) = direction.Vector();
                    return tile.Offset(dx, dy);
                })
                .Where(_field.IsPassable)
                .ToList();
            if (exits.Count == 1)
                _arrival.Add(exits[0]);
        }
        _arrival.Remove(_field.Self);
    }

    /// <summary>
    /// The free tile of a region that an arrival would take: the rear-most one
    /// measured along this team's own declared advance direction, which is the
    /// placement the contract describes. Null when the region has no free tile,
    /// in which case this body is not what is standing in the way.
    /// </summary>
    private Position? RearMost(IEnumerable<Position> region)
    {
        Direction? advance = ArenaBasics.AdvanceDirection(
            _doctrine.Contract,
            _context);
        (int ax, int ay) = advance?.Vector() ?? (0, 0);
        Position? best = null;
        int bestRank = int.MaxValue;
        foreach (Position tile in region)
        {
            if (!_field.IsPassable(tile) || _field.IsOccupied(tile))
                continue;
            int rank = tile.X * ax + tile.Y * ay;
            if (best is null
                || rank < bestRank
                || rank == bestRank
                && (tile.Y, tile.X) is var key
                && key.CompareTo((best.Value.Y, best.Value.X)) < 0)
            {
                best = tile;
                bestRank = rank;
            }
        }
        return best;
    }

    private bool Imminent(GenericActorContext.UnitSlotState state) =>
        state switch
        {
            GenericActorContext.UnitSlotState.Ready => true,
            GenericActorContext.UnitSlotState.AutomaticReturnPending pending =>
                pending.DueTick - _context.Tick <= ArrivalHorizon,
            GenericActorContext.UnitSlotState.AvailabilityPending pending =>
                pending.DueTick - _context.Tick <= ArrivalHorizon,
            _ => false,
        };

    private List<Position> Downhill(Position from, int distance)
    {
        var next = new List<Position>();
        foreach (Direction direction in Field.Cardinals)
        {
            (int dx, int dy) = direction.Vector();
            Position tile = from.Offset(dx, dy);
            if (_field.IsPassable(tile)
                && _field.DistanceToObjective(tile) == distance - 1)
            {
                next.Add(tile);
            }
        }
        return next;
    }

    private (int Distance, int Health, ActorIdentity Id) Key(
        Position position,
        int health,
        ActorIdentity id) =>
        (_field.DistanceToObjective(position), health, id);

    private static int Compare(
        (int Distance, int Health, ActorIdentity Id) left,
        (int Distance, int Health, ActorIdentity Id) right)
    {
        if (!YieldOrder)
            return left.Id.CompareTo(right.Id);
        if (left.Distance != right.Distance)
            return left.Distance.CompareTo(right.Distance);
        if (left.Health != right.Health)
            return right.Health.CompareTo(left.Health);
        return left.Id.CompareTo(right.Id);
    }

    private bool OnLaunchRay(
        GenericActorRulesContract.AttackProfile attack,
        Position from,
        Position target)
    {
        foreach (Direction facing in Field.Cardinals)
        {
            foreach (ProjectileHeading heading in _doctrine.Arms
                         .Aperture(_field.FormId, facing))
            {
                foreach (Position step in Ballistics.Trace(
                             _doctrine,
                             from,
                             heading,
                             bendDirection: 0,
                             bendAfterTiles: 0,
                             bendEveryTiles: 1,
                             bendCount: 0,
                             attack.Projectile.MaxTravelTiles,
                             attack.Projectile.DiagonalCornersMustBeClear))
                {
                    if (step == target)
                        return true;
                }
            }
        }
        return false;
    }

    private static int Ray(Position from, Position target)
    {
        int dx = target.X - from.X;
        int dy = target.Y - from.Y;
        if (dx == 0 && dy == 0)
            return -1;
        return (Math.Sign(dx) + 1) * 3 + Math.Sign(dy) + 1;
    }
}
