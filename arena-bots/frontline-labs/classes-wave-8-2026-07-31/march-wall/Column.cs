using BotArena.Sdk;

/// <summary>
/// THE MARCH ORDER. One reading, per tick, of where every body on our own side is
/// going — so that the wall's segments stop being each other's obstacles.
///
/// <para>Revisions 1–5 were written as if a body were alone on its side of the
/// board. They were not: a wall is several bodies, the map's approaches to the
/// centre run through two-tile pinches, and the ruleset is explicit that
/// <b>allied actors block movement</b>, that <b>same-destination moves all
/// block</b>, and — the one nobody expects — that <b>following a vacated actor
/// blocks</b>. That last clause is the whole bug: a column cannot advance in
/// lockstep. When the leader steps out of a corridor tile the follower's step into
/// it is refused anyway, so a wall marching nose-to-tail spends every second tick
/// submitting a move the engine has already decided to reject.</para>
///
/// <para><b>The substrate is that coordination here cannot be negotiated, only
/// re-derived.</b> Private memory is life-scoped, a fresh body inherits nothing,
/// observations are frozen before any decision executes, and the rules card says
/// plainly that a life never sees an ally's current action. There is no channel. So
/// every rule below is a pure function of the FROZEN SHARED OBSERVATION plus the
/// contract: each life runs this same code over the same inputs, derives the same
/// total precedence order over the same bodies, and therefore agrees with its
/// siblings about who yields without anyone being told. A rule that needed one
/// extra bit of private state would not be implementable at all, and the two rules
/// this pass cut were cut for exactly that reason.</para>
///
/// <para>What a sibling's ROUTE is, precisely, since the observation does not carry
/// intentions: the shortest wall-only walk from its tile to the contested
/// objective, computed with <see cref="Navigation.RouteTiles"/> — the same walk and
/// the same tie-break order the sibling's own pathfinder uses, so the first tile is
/// the tile it is about to step onto — widened by the continuation of its last
/// accepted move, which the observation publishes in
/// <c>previousActionResolution</c>. "This tick or next" is the first two tiles of
/// that. The prediction can be wrong for a sibling walking somewhere private, and
/// the cost of being wrong is one conservatively avoided tile.</para>
///
/// <para>Every clause is switchable in <see cref="Rules"/> and each was measured on
/// its own over the same sixteen cells; <c>DX.md</c> carries the table. Nothing
/// here names a form, a class, a transition, a tile or a map: a corridor is a shape
/// test, a fan is <c>projectilesPerAttack &gt; 1</c>, a guard declares itself, and
/// an arm with one body per side produces an empty sibling list and no rules at
/// all.</para>
/// </summary>
internal sealed class Column
{
    /// <summary>
    /// The coordination clauses, one switch each. An ablation is a one-line edit
    /// here, so the artifact under measurement differs from the shipped one by
    /// exactly one decision — which is the only way a per-rule attribution number
    /// means anything. They are properties rather than constants so that flipping
    /// one does not make the code it guards unreachable, which would turn a
    /// measurement into a compile error.
    /// </summary>
    internal static class Rules
    {
        /// <summary>
        /// C2. One body at a time in a one-tile corridor run, and never a step onto
        /// a tile a sibling occupies right now — including one that is visibly
        /// leaving, because the contract declares
        /// <c>followingVacatedActorAllowed: false</c>. Every half is conditional on
        /// the declared collision policy, so a ruleset where allied bodies do not
        /// block gets none of it.
        ///
        /// <para>This subsumes a clause that was measured separately and is not
        /// shipped as its own rule: "an immobile sibling is a wall, not a transient
        /// blocker". Denying every occupied tile already denies every pinned one, so
        /// with C2 on the separate clause was byte-identical to not having it, and
        /// with C2 off it measured two wins and 53 territorial WORSE than not having
        /// it — a longer detour around a turret beat one blocked tick. It survives
        /// only as the floor of this clause, for the arm that allows the follow,
        /// where nothing else would keep a route out of an emplacement.</para>
        /// </summary>
        public static bool ChokePrecedence => true;

        /// <summary>
        /// C3. Never stand or step where a higher-precedence sibling's committed
        /// route needs to pass this tick or next; vacate if we already do.
        /// </summary>
        public static bool RouteYield => true;

        /// <summary>
        /// C4. A fortification is permanent traffic. Never anchor, and never raise
        /// a shield, on a tile a sibling's route needs — and never seal a corridor
        /// run our own side still has to walk through.
        /// </summary>
        public static bool GateDiscipline => true;

        /// <summary>
        /// C5. Keep the tile a due arrival will materialize on clear — of an
        /// emplacement always, and of our own feet.
        /// </summary>
        public static bool RallyTraffic => true;

        /// <summary>
        /// C6. Do not stand two bodies where ONE accepted enemy action reaches
        /// both — a fan's simultaneous bolts, or the bolt a guarded arc sends back
        /// down the lane we shot down.
        /// </summary>
        public static bool Spacing => true;
    }

    /// <summary>How far ahead a due arrival counts as traffic already on the board.</summary>
    private const int ArrivalHorizonTicks = 3;

    private readonly HashSet<Position> _occupied = [];
    private readonly HashSet<Position> _pinned = [];
    private readonly HashSet<Position> _claimed = [];
    private readonly HashSet<Position> _claimedRuns = [];
    private readonly HashSet<Position> _siblingRoutes = [];
    private readonly HashSet<Position> _arrivals = [];
    private readonly HashSet<Position> _shared = [];

    private Column()
    {
    }

    /// <summary>One body on our own side, as every other body on our side sees it.</summary>
    internal sealed record Body(
        int UnitId,
        int LifeId,
        Position Tile,
        string FormId,
        bool CanWalk,
        bool Pinned,
        int Weight,
        int StepsToGoal,
        bool InRun,
        IReadOnlyList<Position> Route);

    /// <summary>This body, ranked among its own siblings.</summary>
    public Body Self { get; private set; } = null!;

    /// <summary>
    /// Siblings whose precedence is strictly higher than ours, so their claims bind
    /// on us and ours do not bind on them.
    /// </summary>
    public IReadOnlyList<Body> Better { get; private set; } = [];

    /// <summary>Every sibling, ordered by precedence, highest first.</summary>
    public IReadOnlyList<Body> Siblings { get; private set; } = [];

    /// <summary>
    /// Tiles a movement search must treat as walls on BOTH passes. A transient
    /// blocker is worth routing optimistically through; a body that will not move,
    /// and a tile a better sibling has to walk through, are not.
    /// </summary>
    public HashSet<Position> Denied { get; } = [];

    /// <summary>
    /// True when we are standing on a tile a better sibling needs this tick or
    /// next. Somebody has to move and the written rule says it is us.
    /// </summary>
    public bool InTheWay { get; private set; }

    /// <summary>
    /// The one written exemption from yielding, and the reason it is written rather
    /// than inferred: a body that is our side's ONLY objective weight on a
    /// contested objective is the scoring channel, and a march order that walks the
    /// scorer off the point to unblock a gun has coordinated its way into a loss.
    /// When this holds the better sibling routes around us instead.
    /// </summary>
    public bool Exempt { get; private set; }

    /// <summary>
    /// Reads the whole side once. <paramref name="activeIndex"/> is the contested
    /// objective, which is the goal every sibling's route is predicted against.
    /// </summary>
    public static Column Read(
        ContractView view,
        GenericActorContext context,
        int activeIndex)
    {
        var column = new Column();
        HashSet<Position> goals = view.ObjectiveTiles(activeIndex).ToHashSet();
        Direction[] order = MarchOrder(view);
        HashSet<Position> walls = [];

        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            column._occupied.Add(ally.Position);
            if (IsPinned(view, ally.FormId, ally.PendingSameLifeTransition))
            {
                column._pinned.Add(ally.Position);
                walls.Add(ally.Position);
            }
        }

        // Every route is predicted with the immobile bodies of BOTH sides treated
        // as walls, because that is what they are for the length of a march. A
        // route predicted through a turret is not a route.
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            if (IsPinned(view, enemy.FormId, enemy.PendingSameLifeTransition))
                walls.Add(enemy.Position);
        }

        var bodies = new List<Body>();
        column.Self = Describe(
            view,
            goals,
            walls,
            order,
            context.Self.ActorId.UnitId,
            context.Self.ActorId.LifeId,
            context.Self.Position,
            context.Self.FormId,
            context.Self.PendingSameLifeTransition,
            context.Self.PreviousActionResolution);
        bodies.Add(column.Self);
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            bodies.Add(
                Describe(
                    view,
                    goals,
                    walls,
                    order,
                    ally.ActorId.UnitId,
                    ally.ActorId.LifeId,
                    ally.Position,
                    ally.FormId,
                    ally.PendingSameLifeTransition,
                    ally.PreviousActionResolution));
        }

        bodies.Sort(Compare);
        column.Siblings = bodies.Where(body => body != column.Self).ToArray();
        int mine = bodies.IndexOf(column.Self);
        column.Better = bodies.Take(mine).ToArray();

        foreach (Body better in column.Better)
        {
            foreach (Position tile in better.Route.Take(2))
                column._claimed.Add(tile);
            // A corridor run is claimed whole. Two bodies inside one pinch going
            // the same way is the jam this pass exists to remove, and it is not a
            // jam a two-tile lookahead notices: the follower's next tile is free
            // right up to the tick it is refused.
            foreach (Position tile in better.Route.Take(4))
                column._claimedRuns.UnionWith(Navigation.CorridorRun(view, tile));
            if (better.InRun)
                column._claimedRuns.UnionWith(Navigation.CorridorRun(view, better.Tile));
        }
        foreach (Body sibling in column.Siblings)
        {
            foreach (Position tile in sibling.Route.Take(2))
                column._siblingRoutes.Add(tile);
        }

        column._arrivals.UnionWith(Arrivals(view, context, activeIndex));
        column._shared.UnionWith(SharedCoverage(view, context));

        column.Exempt = SoleWeightOnAContestedObjective(view, context);
        column.InTheWay =
            Rules.RouteYield
            && view.BodiesBlockBodies
            && !column.Exempt
            && column.Self.CanWalk
            && column._claimed.Contains(column.Self.Tile);

        // Nothing below is a preference. Each clause is one declared collision
        // fact turned into a routing exclusion, which is why an arm that declares
        // the fact differently gets a different march order out of the same source.
        if (Rules.ChokePrecedence && view.BodiesBlockBodies)
        {
            // A sibling's CURRENT tile is refused whether or not it is leaving,
            // because `followingVacatedActorAllowed` is false. Where a ruleset
            // allows the follow, that denial is wrong and only the bodies that will
            // not move at all — and the corridor runs — still need allocating.
            if (view.LockstepIsPossible)
                column.Denied.UnionWith(column._pinned);
            else
                column.Denied.UnionWith(column._occupied);
            column.Denied.UnionWith(column._claimedRuns);
        }
        if (Rules.RouteYield && view.BodiesBlockBodies)
            column.Denied.UnionWith(column._claimed);
        if (Rules.RallyTraffic)
            column.Denied.UnionWith(column._arrivals);
        column.Denied.Remove(column.Self.Tile);
        return column;
    }

    /// <summary>
    /// True when this destination is refused to us by the march order. Asked by
    /// every candidate loop that scores neighbouring tiles, so the exclusions are
    /// the same ones the route search uses.
    /// </summary>
    public bool Refuses(Position destination) => Denied.Contains(destination);

    /// <summary>
    /// True when putting a body on this tile PERMANENTLY — an anchor, or a shield
    /// that cannot move for its whole budget — would stand it in our own way. A
    /// transient body on a sibling's route costs a tick; an emplacement on it costs
    /// the rest of the match, so the test is wider than <see cref="Refuses"/>: any
    /// sibling's route, not only a better one, and the whole corridor run rather
    /// than the tile.
    /// </summary>
    public bool RefusesEmplacement(ContractView view, Position tile)
    {
        if (Rules.RallyTraffic && _arrivals.Contains(tile))
            return true;
        if (!Rules.GateDiscipline || !view.BodiesBlockBodies)
            return false;
        if (_siblingRoutes.Contains(tile))
            return true;
        HashSet<Position> run = Navigation.CorridorRun(view, tile);
        if (run.Count == 0)
            return false;
        // Sealing a pinch is the strongest placement this class owns and the
        // easiest way to wall in its own advance. It is refused while any sibling
        // still has to walk the run — which is a different and stricter question
        // than "can our side reach the objective at all", the check the planner
        // already made against the map.
        return run.Any(_siblingRoutes.Contains);
    }

    /// <summary>
    /// True when a sibling already stands inside one enemy's simultaneous
    /// coverage of this tile — one accepted action over there reaching two bodies
    /// of ours. See <see cref="SharedCoverage"/> for what "simultaneous" is read
    /// from.
    /// </summary>
    public bool Stacked(Position tile) =>
        Rules.Spacing && _shared.Contains(tile);

    /// <summary>Tiles a due arrival will need. Diagnostics and the anchor planner.</summary>
    public IReadOnlySet<Position> ArrivalTiles => _arrivals;

    // ------------------------------------------------------------- precedence

    /// <summary>
    /// THE PRECEDENCE ORDER, and the reason each term is in it. Every term reads
    /// only the frozen shared observation, so all of our bodies compute the same
    /// order and agree on who yields without a message.
    ///
    /// <list type="number">
    /// <item><b>An occupant of a pinch outranks everyone outside it.</b> Backing a
    /// body out of a doorway costs two ticks and leaves the doorway blocked for
    /// both of them; letting it through costs one and clears it.</item>
    /// <item><b>Nearer the objective goes first.</b> The front of a column is the
    /// only body whose step unblocks the ones behind it, so the march order is
    /// front-to-back and never the reverse.</item>
    /// <item><b>A scorer outranks a gun.</b> Objective weight is the scoring
    /// channel on this ruleset; a weight-zero body waiting a tick costs nothing a
    /// weight-one body waiting a tick does not cost more of.</item>
    /// <item><b>The published identity breaks the tie.</b> Stable slot then life,
    /// both in the observation, so two bodies never both think they are first.</item>
    /// </list>
    /// </summary>
    private static int Compare(Body left, Body right)
    {
        int result = (left.InRun ? 0 : 1).CompareTo(right.InRun ? 0 : 1);
        if (result != 0)
            return result;
        result = left.StepsToGoal.CompareTo(right.StepsToGoal);
        if (result != 0)
            return result;
        result = right.Weight.CompareTo(left.Weight);
        if (result != 0)
            return result;
        result = left.UnitId.CompareTo(right.UnitId);
        return result != 0 ? result : left.LifeId.CompareTo(right.LifeId);
    }

    private static Body Describe(
        ContractView view,
        HashSet<Position> goals,
        HashSet<Position> walls,
        Direction[] order,
        int unitId,
        int lifeId,
        Position tile,
        string formId,
        GenericActorContext.PendingSameLifeTransition? pending,
        GenericActorActionResolution? previous)
    {
        bool pinned = IsPinned(view, formId, pending);
        bool walks = view.IsMobile(formId) && pending is null;
        HashSet<Position> blocked = [.. walls];
        blocked.Remove(tile);
        List<Position> route = pinned
            ? []
            : Navigation.RouteTiles(view, tile, goals, blocked, order);

        // The observation publishes the last accepted action, so a body that has
        // been walking east is evidence about the tile east of it that no route
        // prediction can supply: it may be crossing toward something private. The
        // continuation is added ahead of the predicted route, never instead of it.
        if (walks
            && route.Count > 0
            && Continuation(view, tile, previous) is Position ahead
            && ahead != route[0])
        {
            route.Insert(0, ahead);
        }

        return new Body(
            unitId,
            lifeId,
            tile,
            formId,
            walks,
            pinned,
            view.ObjectiveWeight(formId),
            route.Count == 0 ? int.MaxValue : route.Count,
            Geometry.IsCorridor(view.IsWall, tile),
            route);
    }

    /// <summary>
    /// A body that will still be exactly here for the length of a march: a form
    /// with no movement action, or any body inside a declared transition windup —
    /// the windup is Wait-only and its completion tick is published, so this is a
    /// read rather than an assumption.
    /// </summary>
    private static bool IsPinned(
        ContractView view,
        string formId,
        GenericActorContext.PendingSameLifeTransition? pending) =>
        !view.IsMobile(formId) || pending is not null;

    /// <summary>The tile a body would reach by repeating its last accepted move.</summary>
    private static Position? Continuation(
        ContractView view,
        Position tile,
        GenericActorActionResolution? previous)
    {
        if (previous is not
            {
                Outcome: GenericActorActionResolution.ActionOutcome.Success,
            })
        {
            return null;
        }
        GenericActorActionArgument.DirectionArgument? direction =
            previous.AcceptedAction.Arguments
                .OfType<GenericActorActionArgument.DirectionArgument>()
                .SingleOrDefault();
        if (direction is null)
            return null;
        Position ahead = Geometry.Step(tile, direction.Value);
        return view.IsWall(ahead) ? null : ahead;
    }

    // ---------------------------------------------------------------- arrivals

    /// <summary>
    /// Tiles a body of ours is about to appear on, and the first step it will take.
    /// This is the one place the contract hands an author placement INFLUENCE
    /// rather than placement control: an arrival takes the rear-most FREE tile of
    /// its region measured along our own advance direction, so which tile it gets
    /// depends on where our own bodies are standing when it lands. Standing there
    /// does not merely crowd it — it relocates it, forward, into whatever the front
    /// happens to be.
    ///
    /// <para>Read rather than assumed on both halves: the rally policy comes from
    /// the contract (<see cref="ContractView.ArrivalsRallyForward"/>), and the
    /// arrival is only traffic while a slot's own published clock says a body is
    /// due. An arm that returns bodies to their spawn anchors instead yields
    /// nothing here, because the anchors are already permanently avoided.</para>
    /// </summary>
    private static HashSet<Position> Arrivals(
        ContractView view,
        GenericActorContext context,
        int activeIndex)
    {
        HashSet<Position> tiles = [];
        if (!view.ArrivalsRallyForward)
            return tiles;

        bool due = context.TeamUnits.Any(slot =>
            slot.State is GenericActorContext.UnitSlotState.Ready
            || (slot.State is GenericActorContext.UnitSlotState
                    .AutomaticReturnPending pending
                && pending.DueTick <= context.Tick + ArrivalHorizonTicks));
        if (!due)
            return tiles;

        IReadOnlyList<Position> region = view.ObjectiveTiles(activeIndex);
        if (region.Count == 0)
            return tiles;

        HashSet<Position> taken = context.Allies
            .Select(ally => ally.Position)
            .Concat(context.Enemies.Select(enemy => enemy.Position))
            .ToHashSet();
        taken.Add(context.Self.Position);

        // Rear-most along OUR advance direction, which the contract publishes as a
        // signed objective-index delta rather than as a compass bearing — so the
        // two sides' answers are exact reflections and neither is the map's.
        int sign = Math.Sign(view.AdvanceDelta) == 0 ? 1 : Math.Sign(view.AdvanceDelta);
        bool horizontal =
            Math.Abs(view.EnemyReference.X - view.HomeReference.X)
            >= Math.Abs(view.EnemyReference.Y - view.HomeReference.Y);
        int forward = horizontal
            ? sign * Math.Sign(view.EnemyReference.X - view.HomeReference.X)
            : sign * Math.Sign(view.EnemyReference.Y - view.HomeReference.Y);
        if (forward == 0)
            forward = 1;

        Position? landing = region
            .Where(tile => !taken.Contains(tile) && view.IsOpen(tile))
            .OrderBy(tile => (horizontal ? tile.X : tile.Y) * forward)
            .ThenBy(tile => horizontal ? tile.Y : tile.X)
            .Cast<Position?>()
            .FirstOrDefault();
        if (landing is not Position arrival)
            return tiles;

        tiles.Add(arrival);
        return tiles;
    }

    // ----------------------------------------------------------------- spacing

    /// <summary>
    /// Tiles that share ONE accepted enemy action with a body of ours already
    /// standing on the board. Two shapes qualify, and both are contract reads
    /// rather than guesses about what an opponent might do:
    ///
    /// <list type="bullet">
    /// <item>a <b>fan</b> — an attack profile declaring
    /// <c>projectilesPerAttack &gt; 1</c> launches several bolts at once, so two
    /// of our bodies inside one fan is one decision over there costing two bodies
    /// here. Every facing is considered, because a fan aims by rotating and a
    /// rotation is free next tick.</item>
    /// <item>a <b>deflection return</b> — a guarded arc kills the bolt and
    /// relaunches it from the guard's tile along the exactly reversed heading under
    /// the guard's own team. So the ray our shooter is standing on is a ray the
    /// return comes back down, and a second body of ours behind the first is
    /// standing in the queue for it. This is the shape a bulwark mirror actually
    /// produces, and it is the reason this rule is not striker-only.</item>
    /// </list>
    ///
    /// <para>Deliberately NOT included: an ordinary single-bolt gun's whole reach.
    /// One bolt cannot hit two bodies — it stops on the first — and treating a
    /// bend-and-aim envelope as shared coverage would refuse most of the room,
    /// which is a doctrine change dressed as a safety rule.</para>
    /// </summary>
    private static HashSet<Position> SharedCoverage(
        ContractView view,
        GenericActorContext context)
    {
        HashSet<Position> shared = [];
        if (context.Allies.IsEmpty)
            return shared;
        HashSet<Position> ours = context.Allies
            .Select(ally => ally.Position)
            .ToHashSet();

        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            if (view.ProjectilesPerAttack(enemy.FormId) > 1)
            {
                HashSet<Position> fan = Lane.Reach(
                    view,
                    enemy.FormId,
                    enemy.Position,
                    Geometry.Cardinals,
                    includeBends: false);
                if (fan.Any(ours.Contains))
                    shared.UnionWith(fan);
                continue;
            }

            if (!view.HasGuard(enemy.FormId))
                continue;

            // The return flies out of the guard's tile along every bearing its arc
            // covers. Anything on one of those rays is in the queue for a bolt one
            // of ours sent.
            for (int heading = 0; heading < 8; heading++)
            {
                var travel = (ProjectileHeading)heading;
                if (!Stance.GuardsAgainst(enemy.Facing, Stance.Reversed(travel)))
                    continue;
                (int dx, int dy) = travel.Vector();
                Position cursor = enemy.Position;
                var ray = new List<Position>();
                for (int step = 0; step < 8; step++)
                {
                    Position next = cursor.Offset(dx, dy);
                    if (view.IsWall(next))
                        break;
                    if (dx != 0
                        && dy != 0
                        && (view.IsWall(cursor.Offset(dx, 0))
                            || view.IsWall(cursor.Offset(0, dy))))
                    {
                        break;
                    }
                    cursor = next;
                    ray.Add(cursor);
                }
                if (ray.Any(ours.Contains))
                    shared.UnionWith(ray);
            }
        }
        return shared;
    }

    /// <summary>
    /// The tie-break order every route prediction here uses, and the reason it is
    /// NOT the shared helper's.
    ///
    /// <para><see cref="Navigation.Order"/> wraps
    /// <c>ArenaBasics.OrderedDirections</c>, which draws from
    /// <c>context.Random</c> to shuffle the two lateral directions. That is a good
    /// rule for a single body — it converts a systematic side bias on a
    /// mirror-symmetric map into seed noise — and it is exactly the wrong rule
    /// here, twice over.</para>
    ///
    /// <para>First, <b>the stream is per LIFE</b>. A life asking that helper for the
    /// order gets its OWN shuffle, not its sibling's, so predicting a sibling's
    /// route with it is predicting against the wrong tie-break: the whole layer
    /// rests on every body deriving the same answer from the same observation, and
    /// a per-life coin flip breaks precisely that. Second, <b>the helper is not
    /// pure</b> — each call ADVANCES the stream, so merely consulting it once per
    /// tick for a diagnostic shifts every later draw in the match. A coordination
    /// layer must be able to look at the board without changing it.</para>
    ///
    /// <para>So this order is derived from the contract alone: our own advance
    /// bearing first, then the lateral 90 degrees clockwise of it, then the other
    /// lateral, then the retreat. It is life-independent, which is what makes the
    /// prediction agree across siblings, and it is still mirror-fair, because the
    /// two teams' forward bearings are opposites and therefore so are their
    /// clockwise laterals — neither side shares an absolute compass preference with
    /// the other.</para>
    /// </summary>
    private static Direction[] MarchOrder(ContractView view)
    {
        Direction forward =
            Navigation.Toward(view.HomeReference, view.EnemyReference);
        Direction lateral = Clockwise(forward);
        return [forward, lateral, Clockwise(Clockwise(lateral)), Clockwise(Clockwise(forward))];
    }

    private static Direction Clockwise(Direction direction) =>
        direction switch
        {
            Direction.North => Direction.East,
            Direction.East => Direction.South,
            Direction.South => Direction.West,
            _ => Direction.North,
        };

    private static bool SoleWeightOnAContestedObjective(
        ContractView view,
        GenericActorContext context)
    {
        if (view.ObjectiveWeight(context.Self.FormId) <= 0)
            return false;
        (int own, int enemy, bool selfPresent) =
            ArenaBasics.ObjectivePresence(view.Contract, context);
        return selfPresent
            && enemy > 0
            && own - view.ObjectiveWeight(context.Self.FormId) <= 0;
    }
}
