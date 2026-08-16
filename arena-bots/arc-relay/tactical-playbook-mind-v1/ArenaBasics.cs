using BotArena.Sdk;

/// <summary>
/// Contract-driven movement, targeting, and action helpers for Arc Relay.
/// Every command is selected from the body's current typed legality mask.
/// </summary>
internal static class ArenaBasics
{
    private static readonly ProjectileHeading[] EightWay =
    [
        ProjectileHeading.North,
        ProjectileHeading.NorthEast,
        ProjectileHeading.East,
        ProjectileHeading.SouthEast,
        ProjectileHeading.South,
        ProjectileHeading.SouthWest,
        ProjectileHeading.West,
        ProjectileHeading.NorthWest,
    ];

    private static readonly ProjectileHeading[] Cardinal =
    [
        ProjectileHeading.North,
        ProjectileHeading.East,
        ProjectileHeading.South,
        ProjectileHeading.West,
    ];

    /// <summary>
    /// The per-tick MOVEMENT PLANE: every reservation the arbiter reads
    /// before it chooses a tile, plus the one precedence list that says who
    /// gets the tile when two bodies want it.
    ///
    /// <para>It used to be a bag of tile claims that other systems consulted
    /// afterwards; the reservations were walls discovered on collision and
    /// the losers were shoved by separate movers. Now the plane is the
    /// arbiter's INPUT: lanes, ranks and roots all arrive before the first
    /// body is asked where it wants to go.</para>
    /// </summary>
    internal sealed class Claims
    {
        /// <summary>
        /// THE precedence list. One ordering, used for every contested tile:
        /// lane ownership, who yields to whom, and the executor's per-tick
        /// plan order. It is resolved before movement from authored urgency,
        /// accumulated allied-block debt, activation tick and stable unit id.
        /// Carrier, class, action kind and role never manufacture a hidden
        /// tier. Collision legality, action roots and oscillation protection
        /// remain separate executor invariants.
        /// </summary>
        private readonly HashSet<Position> _tiles = [];
        private readonly HashSet<Position> _committed = [];
        private readonly Dictionary<int, Position> _committedByUnit = [];
        private readonly Dictionary<Position, int> _lanes = [];
        private readonly Dictionary<Position, int> _demands = [];
        private readonly Dictionary<Position, int> _demandDepths = [];
        private readonly Dictionary<int, TacticalMovementPriority> _priorities = [];
        private readonly HashSet<int> _rooted = [];
        private readonly TacticalOscillationGovernor _oscillation;
        private readonly int _tick;

        private Claims(
            IArenaStepper stepper,
            TacticalOscillationGovernor oscillation,
            int tick)
        {
            Stepper = stepper;
            _oscillation = oscillation;
            _tick = tick;
        }

        /// <summary>
        /// The mind's movement arbiter for this tick. It rides on the plane
        /// because every movement wrapper already takes it, and because a
        /// static would be SHARED by both participants of an in-process
        /// mirror cell.
        /// </summary>
        public IArenaStepper Stepper { get; }

        public IReadOnlySet<Position> Tiles => _tiles;

        /// <summary>Tiles a body has actually committed to ENTERING this
        /// tick, as opposed to the tiles everyone merely occupies. The
        /// difference is what separates a queue from a wall.</summary>
        public IReadOnlySet<Position> Committed => _committed;

        /// <summary>Right-of-way tiles, keyed to the owning unit. Every
        /// selected movement plan publishes its next step under the common
        /// urgency/debt ordering: no lower-precedence own body may step into
        /// that lane this tick. A stronger body is never walled by a weaker
        /// body's lane — that mutual wall was one of the fights.
        /// </summary>
        public IReadOnlyDictionary<Position, int> Lanes => _lanes;

        public bool Reserve(Position tile) => _tiles.Add(tile);

        /// <summary>
        /// Root a unit for this tick: no movement wrapper may give it a
        /// step, whatever channel asks. One unit is rooted for one reason
        /// — it is winding up its own declared strike and has not latched
        /// disengage — and the guard lives at the seam precisely so no
        /// channel, present or future, can route around it.
        /// </summary>
        public void Root(int unitId) => _rooted.Add(unitId);

        public bool IsRooted(int unitId) => _rooted.Contains(unitId);

        /// <summary>Place a unit on the precedence list for this tick.
        /// </summary>
        public void Rank(TacticalMovementPriority priority) =>
            _priorities[priority.UnitId] = priority;

        /// <summary>Where a unit sits on the precedence list; anything the
        /// executor did not classify is free traffic.</summary>
        public int RankOf(int unitId) =>
            _priorities.TryGetValue(unitId, out TacticalMovementPriority? value)
                ? value.Rank
                : int.MaxValue;

        public string PriorityTrace(int unitId) =>
            _priorities.TryGetValue(unitId, out TacticalMovementPriority? value)
                ? value.Trace
                : "normal+b0";

        /// <summary>
        /// Whether <paramref name="unitId"/> gives way to
        /// <paramref name="otherUnitId"/>: STRICTLY better rank, nothing
        /// else. Every yield in the mind asks exactly this question.
        ///
        /// <para>Every body receives a distinct deterministic rank before
        /// lanes are published, so exact tile claims always have one winner.
        /// Authored urgency and accumulated block debt decide meaningful
        /// precedence; activation and unit id settle remaining ties.</para>
        /// </summary>
        public bool YieldsTo(int unitId, int otherUnitId) =>
            unitId != otherUnitId
            && RankOf(otherUnitId) < RankOf(unitId);

        public void ReserveLane(Position tile, int ownerUnitId)
        {
            if (!_lanes.TryGetValue(tile, out int held)
                || RankOf(ownerUnitId) < RankOf(held)
                || (RankOf(ownerUnitId) == RankOf(held)
                    && ownerUnitId < held))
            {
                _lanes[tile] = ownerUnitId;
            }
        }

        /// <summary>
        /// Tiles a mover WANTS this tick that a parked teammate is sitting
        /// on. Unlike a lane this reserves nothing and blocks nobody — it is
        /// a REQUEST, and the only thing it does is oblige the body standing
        /// there to step aside (owner ruling 2026-08-10: "that bot could
        /// just move out of the way").
        /// </summary>
        public IReadOnlyDictionary<Position, int> Demands => _demands;

        /// <summary>
        /// Ask for a tile. The strongest asker wins it — lower rank first,
        /// then lower unit id — so two movers wanting one parked body's
        /// ground produce one request with one author, and the body is not
        /// pulled in two directions.
        /// </summary>
        public void Demand(
            Position tile,
            int moverUnitId,
            int chainDepth = 0)
        {
            if (!_demands.TryGetValue(tile, out int held)
                || RankOf(moverUnitId) < RankOf(held)
                || (RankOf(moverUnitId) == RankOf(held) && moverUnitId < held))
            {
                _demands[tile] = moverUnitId;
                _demandDepths[tile] = chainDepth;
            }
            else if (held == moverUnitId
                && chainDepth < _demandDepths.GetValueOrDefault(
                    tile, int.MaxValue))
            {
                _demandDepths[tile] = chainDepth;
            }
        }

        public int DemandDepth(Position tile, int moverUnitId) =>
            _demands.GetValueOrDefault(tile, -1) == moverUnitId
                ? _demandDepths.GetValueOrDefault(tile)
                : 0;

        /// <summary>
        /// Whether the plane owes this body's tile to somebody, and to whom.
        /// Two sources, one question: a reserved right-of-way LANE it is
        /// standing in, and a REQUEST from a mover whose own next step lands
        /// exactly here. The stronger claimant wins.
        /// </summary>
        public bool Owes(MindBody body, out int moverUnitId)
        {
            moverUnitId = -1;
            if (_lanes.TryGetValue(body.Position, out int owner)
                && YieldsTo(body.UnitId, owner))
            {
                moverUnitId = owner;
            }
            if (_demands.TryGetValue(body.Position, out int mover)
                && YieldsTo(body.UnitId, mover)
                && (moverUnitId < 0 || RankOf(mover) < RankOf(moverUnitId)))
            {
                moverUnitId = mover;
            }
            return moverUnitId >= 0;
        }

        /// <summary>Claim a tile a body is actually stepping onto, and tell
        /// the arbiter so its own reservations match the executor's.
        /// </summary>
        public void Commit(
            MindBody body,
            Position tile,
            TacticalMovementAuthority authority)
        {
            Reserve(tile);
            _committed.Add(tile);
            _committedByUnit[body.UnitId] = tile;
            _oscillation.NoteCommitted(
                body.ActorId, _tick, body.Position, authority);
            Stepper.NoteCommitted(body, tile);
        }

        public bool CommittedDestination(int unitId, out Position destination) =>
            _committedByUnit.TryGetValue(unitId, out destination);

        public TacticalOscillationGovernor.Admission AdmitMovement(
            MindBody body,
            Position destination,
            TacticalMovementAuthority authority) =>
            _oscillation.Admit(
                body.ActorId,
                _tick,
                destination,
                authority);

        public TacticalOscillationGovernor.Avoidance? ActiveMovementAvoidance(
            MindBody body,
            TacticalMovementAuthority authority =
                TacticalMovementAuthority.Tactical) =>
            _oscillation.ActiveAvoidance(body.ActorId, _tick, authority);

        public static Claims ForTick(
            GenericActorResolvedMatchContract contract,
            MindContext mind,
            IArenaStepper stepper,
            TacticalOscillationGovernor oscillation)
        {
            stepper.BeginTick(contract, mind);
            var claims = new Claims(stepper, oscillation, mind.Tick);
            foreach (MindBody body in mind.Bodies)
                claims.Reserve(body.Position);
            return claims;
        }
    }

    public static GenericActorContext.ModeObservationState.ArcRelay? ArcState(
        MindContext mind) =>
        mind.Mode as GenericActorContext.ModeObservationState.ArcRelay;

    public static GenericActorRulesContract.ArcRelayGameMode? ArcRules(
        GenericActorResolvedMatchContract contract) =>
        contract.Rules.GameMode as GenericActorRulesContract.ArcRelayGameMode;

    /// <summary>
    /// True when any currently visible enemy's facing-quadrant vision covers
    /// the tile, mirroring the engine cone (point-blank ring always seen;
    /// beyond it forward ≥ 0 with |lateral| ≤ forward out to range 7).
    /// Walls are ignored, deliberately erring toward "seen": an ambusher
    /// that hides one tile more than necessary loses nothing.
    /// </summary>
    public static bool SeenByVisibleEnemy(MindContext mind, Position tile)
    {
        foreach (GenericActorContext.ObservedEnemyState enemy in mind.Enemies)
        {
            int dx = tile.X - enemy.Position.X;
            int dy = tile.Y - enemy.Position.Y;
            int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (distance <= 1)
                return true;
            if (distance > 7)
                continue;
            (int forward, int lateral) = enemy.Facing switch
            {
                Direction.North => (-dy, dx),
                Direction.South => (dy, dx),
                Direction.East => (dx, dy),
                Direction.West => (-dx, dy),
                _ => (distance, 0),
            };
            if (forward >= 0 && Math.Abs(lateral) <= forward)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Nearest standable tile from which a shot at the target enters its
    /// blind rear quadrant: tiles along the three rear-approach rays out to
    /// the shooter's projectile range, wall-checked including the firing
    /// ray. Null when the target's back is to a wall or the shooter has no
    /// ranged attack.
    /// </summary>
    public static Position? NearestRearFiringTile(
        GenericActorResolvedMatchContract contract,
        MindBody shooter,
        GenericActorContext.ObservedEnemyState target)
    {
        GenericActorRulesContract.AttackProfile? attack = contract.Rules.Forms
            .FirstOrDefault(form => string.Equals(
                form.Id, shooter.FormId, StringComparison.Ordinal))
            ?.AttackProfileId is string attackId
            ? contract.Rules.AttackProfiles.FirstOrDefault(value =>
                string.Equals(value.Id, attackId, StringComparison.Ordinal))
            : null;
        if (attack is null)
            return null;
        (int fx, int fy) = target.Facing switch
        {
            Direction.North => (0, -1),
            Direction.South => (0, 1),
            Direction.East => (1, 0),
            Direction.West => (-1, 0),
            _ => (0, 0),
        };
        (int Hx, int Hy)[] rearHeadings = fx != 0
            ? [(fx, 0), (fx, -1), (fx, 1)]
            : [(0, fy), (-1, fy), (1, fy)];
        Position? best = null;
        int bestScore = int.MaxValue;
        foreach ((int hx, int hy) in rearHeadings)
        {
            for (int k = 1; k <= attack.Projectile.MaxTravelTiles; k++)
            {
                var tile = new Position(
                    target.Position.X - hx * k,
                    target.Position.Y - hy * k);
                if (!CanEnter(contract.Map, tile)
                    || !ClearRay(
                        contract.Map, tile, target.Position,
                        attack.Projectile.DiagonalCornersMustBeClear))
                {
                    continue;
                }
                int score = shooter.Position.ChebyshevDistance(tile) * 8 + k;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = tile;
                }
            }
        }
        return best;
    }

    /// <summary>
    /// 0 when the nearest shooter's 8-way heading toward the target lands in
    /// the target's blind rear quadrant (a backstab under predation rules),
    /// 1 otherwise — an ascending late tie-break for target choice.
    /// </summary>
    public static int RearExposedRank(
        IReadOnlyCollection<MindBody> shooters,
        GenericActorContext.ObservedEnemyState target)
    {
        MindBody? nearest = shooters
            .OrderBy(body => body.Position.ChebyshevDistance(target.Position))
            .ThenBy(body => body.UnitId)
            .FirstOrDefault();
        if (nearest is null)
            return 1;
        int dx = Math.Sign(target.Position.X - nearest.Position.X);
        int dy = Math.Sign(target.Position.Y - nearest.Position.Y);
        (int forward, int lateral) = target.Facing switch
        {
            Direction.North => (-dy, dx),
            Direction.South => (dy, dx),
            Direction.East => (dx, dy),
            Direction.West => (-dx, dy),
            _ => (-1, 0),
        };
        return forward >= 0 && Math.Abs(lateral) <= forward ? 0 : 1;
    }

    public static Position? Reactor(
        MindContext mind,
        int teamId) =>
        ArcState(mind)?.Reactors
            .FirstOrDefault(reactor => reactor.TeamId == teamId)
            ?.Position;

    public static GenericActorContext.ArcRelayCoreState? CarriedCore(
        MindContext mind,
        ActorIdentity actorId) =>
        ArcState(mind)?.VisibleCores.FirstOrDefault(core =>
            core.Disposition == GenericActorContext.ArcRelayCoreDisposition.Carried
            && core.CarrierActorId == actorId);

    public static GenericActorContext.ObservedEnemyState? VisibleEnemyCarrier(
        MindContext mind,
        int ownTeamId)
    {
        GenericActorContext.ModeObservationState.ArcRelay? arc = ArcState(mind);
        if (arc is null)
            return null;

        HashSet<ActorIdentity> carrierIds = arc.VisibleCores
            .Where(core =>
                core.Disposition
                    == GenericActorContext.ArcRelayCoreDisposition.Carried
                && core.CarrierActorId is { } carrier
                && carrier.TeamId != ownTeamId)
            .Select(core => core.CarrierActorId!)
            .ToHashSet();
        return mind.Enemies
            .Where(enemy => carrierIds.Contains(enemy.ActorId))
            .OrderBy(enemy => enemy.Health)
            .ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault();
    }

    public static GenericActorContext.ArcRelayCoreState[] LooseCores(
        MindContext mind) =>
        ArcState(mind)?.VisibleCores
            .Where(core =>
                core.Disposition == GenericActorContext.ArcRelayCoreDisposition.Loose)
            .OrderBy(core => core.CoreId.SourceWellId, StringComparer.Ordinal)
            .ThenBy(core => core.CoreId.SourceOrdinal)
            .ToArray()
        ?? [];

    /// <summary>
    /// The one gate every movement wrapper passes through before a tile is
    /// chosen. A rooted body gets nothing — not an evacuation step, not a
    /// formation step, not a lane clearance — because "rooted shooter,
    /// committed unless disengage triggers" has to hold against channels
    /// that have not been written yet, and a policy spelled out six times
    /// is a policy with five places to forget it.
    /// </summary>
    private static Position? ChooseStep(StepRequest request) =>
        request.Claims.IsRooted(request.Body.ActorId.UnitId)
            ? null
            : request.Claims.Stepper.Step(request);

    public static bool IsLegalTerrainStep(
        GenericActorMapContract map,
        Position from,
        Position to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        return Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1
            && (dx != 0 || dy != 0)
            && CanStep(map, from, to, FromVector(dx, dy));
    }

    public static bool TryMoveToward(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        IReadOnlyCollection<Position> goals,
        Claims claims,
        string reason,
        TacticalMovementAuthority authority =
            TacticalMovementAuthority.Tactical) =>
        Submit(
            contract,
            mind,
            body,
            ChooseStep(new StepRequest(
                StepIntent.Toward, contract, mind, body, goals, claims)),
            claims,
            reason,
            authority);

    /// <summary>
    /// Submit the arbiter's answer. The ONE place a movement command is
    /// emitted: the arbiter picks the tile, this turns it into the legal
    /// action for the body's current mask (heading form, cardinal form, or
    /// the preparation turn a facing-locked body needs first) and claims the
    /// tile. Every wrapper shares it, so "who moved this body and why" has
    /// exactly one answer and exactly one reason string.
    /// </summary>
    private static bool Submit(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Position? chosen,
        Claims claims,
        string reason,
        TacticalMovementAuthority authority =
            TacticalMovementAuthority.Tactical)
    {
        if (chosen is not Position destination)
            return false;
        TacticalOscillationGovernor.Admission admission =
            claims.AdmitMovement(body, destination, authority);
        if (!admission.Admitted)
        {
            body.Hold($"{reason}:{admission.Reason}");
            return true;
        }
        ProjectileHeading heading = FromVector(
            destination.X - body.Position.X,
            destination.Y - body.Position.Y);

        GenericActorActionLegality? move = MovementAction(
            contract,
            mind,
            body,
            heading,
            destination,
            requireAvailable: false);
        if (move is null)
        {
            // Facing-locked bodies expose only their current forward heading
            // in the movement mask. A different cardinal route step therefore
            // needs an explicit preparation turn before it can be submitted.
            Direction? routeFacing = ToDirection(heading);
            return routeFacing is Direction routeTurn
                && TryRotate(contract, body, routeTurn, $"turn for {reason}");
        }

        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = move.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        if (move.Available
            && headings is not null
            && headings.AllowedValues.Contains(heading))
        {
            claims.Commit(body, destination, authority);
            body.Command(
                move.ActionId,
                move.ActionCode,
                [new GenericActorActionArgument.ProjectileHeadingArgument(heading)],
                $"{reason} via {heading}");
            return true;
        }

        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = move.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        Direction? cardinal = ToDirection(heading);
        if (move.Available
            && cardinal is Direction direction
            && directions is not null
            && directions.AllowedValues.Contains(direction))
        {
            claims.Commit(body, destination, authority);
            body.Command(
                move.ActionId,
                move.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(direction)],
                $"{reason} via {direction}");
            return true;
        }

        return cardinal is Direction turn
            && TryRotate(contract, body, turn, $"turn for {reason}");
    }

    /// <summary>
    /// The first terrain-only step toward a goal. Traffic may occupy it now;
    /// callers use this to ask that traffic to yield instead of routing a
    /// carrier in circles around its own bank.
    /// </summary>
    public static Position? StaticFirstStep(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Position goal)
    {
        if (body.Position == goal)
            return null;
        ProjectileHeading? heading = FindFirstStep(
            contract.Map,
            body.Position,
            new HashSet<Position> { goal },
            new HashSet<Position>(),
            RouteHeadings(contract, body),
            PreferredHeadings(body, [goal], MirroredFrame(contract, mind)));
        return heading is ProjectileHeading step
            ? Step(body.Position, step)
            : null;
    }

    /// <summary>
    /// Propagate a mover's existing right-of-way request through an
    /// occupied allied chain until it reaches the nearest body that can step
    /// into a real vacancy. This does not invent a move or a destination:
    /// every body in the returned chain already yields to the mover, and
    /// <see cref="TryMakeWay"/> remains the sole displacement authority.
    /// </summary>
    public static int DemandClearanceChain(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody mover,
        Position wanted,
        Claims claims)
    {
        MindBody? first = mind.Bodies.FirstOrDefault(body =>
            body.UnitId != mover.UnitId
            && body.Position == wanted
            && claims.YieldsTo(body.UnitId, mover.UnitId)
            && !claims.IsRooted(body.UnitId));
        if (first is null)
            return 0;

        MindBody[] eligible = mind.Bodies
            .Where(body => body.UnitId != mover.UnitId
                && claims.YieldsTo(body.UnitId, mover.UnitId)
                && !claims.IsRooted(body.UnitId))
            .OrderBy(body => body.UnitId)
            .ToArray();
        Dictionary<Position, MindBody> byPosition = eligible.ToDictionary(
            body => body.Position);
        var nodes = new List<TacticalClearancePlanner.Node>(eligible.Length);
        foreach (MindBody body in eligible)
        {
            HashSet<Position> blocked = BlockedNow(
                contract,
                mind,
                body,
                claims,
                avoidHostileProjectiles: false);
            bool hasExit = RouteHeadings(contract, body).Any(heading =>
            {
                Position next = Step(body.Position, heading);
                return CanStep(contract.Map, body.Position, next, heading)
                    && !blocked.Contains(next)
                    && !claims.Demands.ContainsKey(next);
            });
            int[] successors = RouteHeadings(contract, body)
                .Select(heading => (
                    Heading: heading,
                    Position: Step(body.Position, heading)))
                .Where(candidate => CanStep(
                    contract.Map,
                    body.Position,
                    candidate.Position,
                    candidate.Heading))
                .Select(candidate => byPosition.GetValueOrDefault(
                    candidate.Position))
                .Where(candidate => candidate is not null)
                .Select(candidate => candidate!.UnitId)
                .Distinct()
                .Order()
                .ToArray();
            nodes.Add(new TacticalClearancePlanner.Node(
                body.UnitId, hasExit, successors));
        }

        int[] chain = TacticalClearancePlanner.Resolve(first.UnitId, nodes);
        for (int index = 0; index < chain.Length; index++)
        {
            MindBody body = eligible.Single(candidate =>
                candidate.UnitId == chain[index]);
            claims.Demand(body.Position, mover.UnitId, index + 1);
        }
        return chain.Length;
    }

    /// <summary>
    /// The first terrain-shortest step toward a goal while treating visible
    /// spawn reservations as durable obstacles for the entire route. Carrier
    /// recovery uses this after repeated non-progress: a reservation beside a
    /// reactor must select one deterministic route around it, rather than let
    /// equal-distance local choices bounce between adjacent tiles.
    /// </summary>
    public static Position? StaticFirstStepAvoidingReservations(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Position goal)
    {
        if (body.Position == goal)
            return null;
        HashSet<Position> reservations = mind.VisibleTiles
            .Where(tile => tile.SpawnReservation is not null)
            .Select(tile => tile.Position)
            .ToHashSet();
        reservations.Remove(body.Position);
        reservations.Remove(goal);
        ProjectileHeading? heading = FindFirstStep(
            contract.Map,
            body.Position,
            new HashSet<Position> { goal },
            reservations,
            RouteHeadings(contract, body),
            PreferredHeadings(body, [goal], MirroredFrame(contract, mind)),
            blockThroughout: true);
        return heading is ProjectileHeading step
            ? Step(body.Position, step)
            : null;
    }

    /// <summary>
    /// One deterministic terrain course for an authored route target. Unlike
    /// ordinary chase movement, persistent obstacles are walls for the whole
    /// search and the resulting path is returned in full so the route governor
    /// can hold it across ticks.
    /// </summary>
    internal static Position[]? StaticRouteCourse(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Position goal,
        IReadOnlySet<Position> persistentObstacles,
        bool mirrored)
    {
        if (body.Position == goal)
            return [body.Position];

        var blocked = new HashSet<Position>(persistentObstacles);
        blocked.Remove(body.Position);
        blocked.Remove(goal);
        ProjectileHeading[] preference = PreferredHeadings(
            body, [goal], mirrored);
        IReadOnlyCollection<ProjectileHeading> allowed = RouteHeadings(
            contract, body);

        int width = contract.Map.Width;
        int area = width * contract.Map.Height;
        var parents = new int[area];
        Array.Fill(parents, -1);
        var visited = new bool[area];
        var queue = new int[area];
        var allowedHeadings = new bool[8];
        foreach (ProjectileHeading heading in allowed)
            allowedHeadings[(int)heading] = true;
        int startIndex = body.Position.Y * width + body.Position.X;
        int goalIndex = goal.Y * width + goal.X;
        int read = 0;
        int write = 1;
        queue[0] = startIndex;
        visited[startIndex] = true;
        while (read < write)
        {
            int currentIndex = queue[read++];
            var current = new Position(
                currentIndex % width,
                currentIndex / width);
            foreach (ProjectileHeading heading in preference)
            {
                if (!allowedHeadings[(int)heading])
                    continue;
                Position next = Step(current, heading);
                if (!CanStep(contract.Map, current, next, heading)
                    || blocked.Contains(next))
                {
                    continue;
                }
                int nextIndex = next.Y * width + next.X;
                if (visited[nextIndex])
                    continue;
                visited[nextIndex] = true;
                parents[nextIndex] = currentIndex;
                if (nextIndex == goalIndex)
                    return ReconstructCourse(
                        startIndex, goalIndex, parents, width);
                queue[write++] = nextIndex;
            }
        }
        return null;
    }

    /// <summary>
    /// Static shortest distance under the same movement profile and durable
    /// obstacles as <see cref="StaticRouteCourse"/>, from an arbitrary tile.
    /// Delivery corridors use this to ask whether their next waypoint really
    /// shortens the traversable trip to the reactor; geometric distance alone
    /// mistakes the inside of a cul-de-sac for progress.
    /// </summary>
    internal static int? StaticRouteDistance(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Position start,
        Position goal,
        IReadOnlySet<Position> persistentObstacles)
    {
        if (start == goal)
            return 0;
        var blocked = new HashSet<Position>(persistentObstacles);
        blocked.Remove(start);
        blocked.Remove(goal);
        int[] field = StaticRouteDistanceField(
            contract, body, goal, blocked);
        return DistanceFromField(contract.Map, field, start);
    }

    /// <summary>
    /// One reverse shortest-path field for a fixed goal and durable obstacle
    /// set. The route step relation is symmetric, so this is byte-equivalent
    /// to flooding outward from every candidate start while avoiding the
    /// thousands of repeated map searches a three-action planner would
    /// otherwise perform for one loaded carrier in one tick.
    /// </summary>
    internal static int[] StaticRouteDistanceField(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Position goal,
        IReadOnlySet<Position> persistentObstacles)
    {
        var blocked = new HashSet<Position>(persistentObstacles);
        blocked.Remove(goal);
        IReadOnlyCollection<ProjectileHeading> allowed = RouteHeadings(
            contract, body);
        int width = contract.Map.Width;
        int area = width * contract.Map.Height;
        var distances = new int[area];
        Array.Fill(distances, -1);
        var queue = new int[area];
        var allowedHeadings = new bool[8];
        foreach (ProjectileHeading heading in allowed)
            allowedHeadings[(int)heading] = true;
        int goalIndex = goal.Y * width + goal.X;
        int read = 0;
        int write = 1;
        queue[0] = goalIndex;
        distances[goalIndex] = 0;
        while (read < write)
        {
            int currentIndex = queue[read++];
            var current = new Position(
                currentIndex % width,
                currentIndex / width);
            int nextDistance = distances[currentIndex] + 1;
            foreach (ProjectileHeading heading in EightWay)
            {
                if (!allowedHeadings[(int)heading])
                    continue;
                Position next = Step(current, heading);
                if (!CanStep(contract.Map, current, next, heading)
                    || blocked.Contains(next))
                {
                    continue;
                }
                int nextIndex = next.Y * width + next.X;
                if (distances[nextIndex] >= 0)
                    continue;
                distances[nextIndex] = nextDistance;
                queue[write++] = nextIndex;
            }
        }
        return distances;
    }

    internal static int? DistanceFromField(
        GenericActorMapContract map,
        IReadOnlyList<int> distances,
        Position start)
    {
        int index = start.Y * map.Width + start.X;
        return index >= 0
            && index < distances.Count
            && distances[index] >= 0
                ? distances[index]
                : null;
    }

    /// <summary>
    /// Obstacles that outlive one movement tick and therefore may invalidate
    /// a held route course. Allies, enemies and projectiles are deliberately
    /// absent: those are traffic or reactions, not reasons to silently choose
    /// the other way around an authored patrol.
    /// </summary>
    internal static HashSet<Position> PersistentRouteObstacles(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        int teamId)
    {
        var blocked = mind.VisibleTiles
            .Where(tile => tile.SpawnReservation is not null)
            .Select(tile => tile.Position)
            .ToHashSet();
        if (mind.Mode is GenericActorContext.ModeObservationState.ArcRelay arc)
        {
            blocked.UnionWith(arc.VisibleSignatures
                .Where(signature => SignatureBlocksMovement(
                    signature, teamId))
                .SelectMany(signature => signature.Positions));
        }
        if (contract.ModeMapBinding
            is GenericActorResolvedMatchContract.ArcRelayModeMapBinding binding)
        {
            HashSet<int> hostileParticipants = contract.Topology.Participants
                .Where(participant => participant.TeamId != teamId)
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

    private static Position[] ReconstructCourse(
        int start,
        int goal,
        IReadOnlyList<int> parents,
        int width)
    {
        var reverse = new List<Position>();
        int cursor = goal;
        while (cursor != start)
        {
            reverse.Add(new Position(cursor % width, cursor / width));
            cursor = parents[cursor];
        }
        reverse.Add(new Position(start % width, start / width));
        reverse.Reverse();
        return reverse.ToArray();
    }

    /// <summary>
    /// Static eight-way walk distance with the contract's strict diagonal
    /// corner rule. Carrier progress uses map distance rather than Chebyshev
    /// distance so moving around cover cannot masquerade as home progress.
    /// </summary>
    /// <summary>
    /// Whether the candidate stands on one of the loaded carrier's
    /// admissible homeward steps while every such step is taken. The
    /// carrier's movement policy (TryMoveHomeward) only takes steps whose
    /// static distance to the bank does not increase - winding detours
    /// are deliberately refused - so the plug question is asked about
    /// exactly that ring, not about theoretical alternative routes
    /// (owner point 2026-08: multiple paths can exist; a free admissible
    /// step means nobody needs to yield).
    /// </summary>
    public static bool PlugsCarrierRoute(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody carrier,
        MindBody candidate,
        Position bank)
    {
        int? current = StaticDistance(contract.Map, carrier.Position, bank);
        if (current is null)
            return false;
        var occupied = mind.Bodies
            .Where(other => other.UnitId != carrier.UnitId)
            .Select(other => other.Position)
            .ToHashSet();
        foreach (GenericActorContext.ObservedEnemyState enemy in mind.Enemies)
            occupied.Add(enemy.Position);
        bool candidateOnStep = false;
        foreach (ProjectileHeading heading in EightWay)
        {
            Position next = Step(carrier.Position, heading);
            if (!CanStep(contract.Map, carrier.Position, next, heading))
                continue;
            if (StaticDistance(contract.Map, next, bank)
                    is not int distance
                || distance > current)
            {
                continue;
            }
            if (next == candidate.Position)
            {
                candidateOnStep = true;
                continue;
            }
            if (!occupied.Contains(next))
                return false;
        }
        return candidateOnStep;
    }

    private static readonly System.Runtime.CompilerServices
        .ConditionalWeakTable<
            GenericActorMapContract,
            Dictionary<Position, Dictionary<Position, int>>>
        DistanceFields = new();

    /// <summary>
    /// Static shortest-step distance over the map's 8-way step relation.
    /// Backed by a flow field: one BFS FROM the goal serves every query
    /// against that goal for the whole match (the map never changes), so
    /// a unit weighing its candidate steps costs lookups instead of
    /// floods. The step relation is symmetric — a diagonal's clear-corner
    /// pair is the same two tiles read from either end — so the field's
    /// values are byte-identical to the historical start-out search.
    /// </summary>
    public static int? StaticDistance(
        GenericActorMapContract map,
        Position start,
        Position goal)
    {
        if (start == goal)
            return 0;
        // The historical start-out search could never ENTER a wall goal.
        if (!CanEnter(map, goal))
            return null;
        Dictionary<Position, Dictionary<Position, int>> fields =
            DistanceFields.GetOrCreateValue(map);
        if (!fields.TryGetValue(
                goal, out Dictionary<Position, int>? field))
        {
            field = DistanceField(map, goal);
            fields.Add(goal, field);
        }
        if (field.TryGetValue(start, out int distance))
            return distance;
        // The historical search never checked the START's own tile, so a
        // query FROM a wall legally walks out of it: one step to any
        // reachable neighbour, then the symmetric field takes over.
        int? best = null;
        foreach (ProjectileHeading heading in EightWay)
        {
            Position next = Step(start, heading);
            if (!CanStep(map, start, next, heading)
                || !field.TryGetValue(next, out int through))
            {
                continue;
            }
            if (best is null || through + 1 < best)
                best = through + 1;
        }
        return best;
    }

    private static Dictionary<Position, int> DistanceField(
        GenericActorMapContract map,
        Position goal)
    {
        var field = new Dictionary<Position, int> { [goal] = 0 };
        var queue = new Queue<Position>();
        queue.Enqueue(goal);
        while (queue.Count > 0)
        {
            Position position = queue.Dequeue();
            int distance = field[position];
            foreach (ProjectileHeading heading in EightWay)
            {
                Position next = Step(position, heading);
                if (!CanStep(map, position, next, heading)
                    || field.ContainsKey(next))
                {
                    continue;
                }
                field[next] = distance + 1;
                queue.Enqueue(next);
            }
        }
        return field;
    }

    /// <summary>
    /// Take a legal traffic-aware step whose static map distance does not
    /// increase. Equal-distance sidesteps are permitted so a permanently
    /// reserved spawn tile cannot pin a carrier on the nominal shortest ray.
    /// </summary>
    public static bool TryMoveHomeward(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Position goal,
        Claims claims,
        string reason)
    {
        return ChooseStep(new StepRequest(
                StepIntent.Homeward, contract, mind, body, [goal], claims))
                is Position destination
            && TryMoveDirect(
                contract,
                mind,
                body,
                destination,
                claims,
                reason,
                TacticalMovementAuthority.Safety);
    }

    /// <summary>
    /// Walk a carrier's OWN committed plan — its sticky delivery corridor
    /// waypoint, or the reactor — through the arbiter instead of beside it.
    /// The carrier used to compute this step itself and hand the arbiter a
    /// fait accompli, which made the delivery a second movement authority;
    /// asking for it as an intent is what lets a cooperative planner reserve
    /// the whole corridor the Core is about to walk.
    /// </summary>
    public static bool TryMoveRouted(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Position goal,
        Claims claims,
        string reason) =>
        Submit(
            contract,
            mind,
            body,
            ChooseStep(new StepRequest(
                StepIntent.Routed, contract, mind, body, [goal], claims)),
            claims,
            reason,
            TacticalMovementAuthority.Safety);

    /// <summary>
    /// THE POLITENESS RULE, and the movement plane's only displacement:
    /// this body is standing on ground somebody stronger needs this tick, so
    /// the arbiter steps it aside. Owner ruling 2026-08-10, watching a
    /// walker curve around a parked teammate with open ground beside it —
    /// <b>"that bot could just move out of the way"</b>.
    ///
    /// <para>Two things can owe the ground, and they are the same question
    /// asked at two distances: a reserved next-step right-of-way LANE and a
    /// REQUEST from any mover whose own next step lands exactly on this
    /// tile. One precedence list decides both, equals never yield, and the
    /// reason string names the mover: <c>...:make-way:u&lt;mover&gt;</c>.</para>
    ///
    /// <para>It is REQUEST-DRIVEN and nothing else. Nobody asks, nobody
    /// moves: a body parked where no one needs it stays parked forever,
    /// because standing is sheet policy (#232 removed the no-idle watchdog
    /// and this must not smuggle it back). There is no patience counter, no
    /// idle timer and no notion of a body being in the wrong place — only of
    /// a body being in somebody's way THIS TICK.</para>
    ///
    /// <para>The sidestep goes to a free adjacent tile that is not itself
    /// wanted or reserved, so making way for one body never puts this one in
    /// front of another. Afterwards nothing drags it back: its target is
    /// unchanged, so ordinary movement drifts it home next tick.</para>
    ///
    /// <para>Returns false when the plane owes nothing, which is the common
    /// case and costs two dictionary lookups.</para>
    /// </summary>
    public static bool TryMakeWay(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Claims claims,
        string reason)
    {
        if (!claims.Owes(body, out int moverUnitId))
            return false;
        MindBody? mover = mind.Bodies.FirstOrDefault(
            other => other.UnitId == moverUnitId);
        // Ground this body must not step INTO: its own tile, the mover's,
        // every lane it yields to, and every tile any mover has asked for.
        // Stepping out of one body's way and into another's is how a
        // courtesy turns into a shuffle.
        var owed = new HashSet<Position> { body.Position };
        foreach ((Position tile, int lane) in claims.Lanes)
        {
            if (claims.YieldsTo(body.UnitId, lane))
                owed.Add(tile);
        }
        foreach (Position tile in claims.Demands.Keys)
            owed.Add(tile);
        if (mover is not null)
            owed.Add(mover.Position);
        int chainDepth = claims.DemandDepth(
            body.Position, moverUnitId);
        string trace = chainDepth > 0
            ? $"{reason}:chain-{chainDepth}:u{moverUnitId}"
                + $":to-{claims.PriorityTrace(moverUnitId)}"
            : $"{reason}:u{moverUnitId}"
                + $":to-{claims.PriorityTrace(moverUnitId)}";
        return Submit(
            contract,
            mind,
            body,
            ChooseStep(new StepRequest(
                StepIntent.MakeWay, contract, mind, body, owed, claims,
                mover?.Position)),
            claims,
            trace,
            TacticalMovementAuthority.Clearance);
    }

    /// <summary>
    /// One kiting backstep: the legal step that most improves the body's
    /// distance to the given threats, taken without turning - movement never
    /// changes facing, so the front arc (and with it the rear-arc
    /// protection) stays on the enemy while the body opens the range.
    /// Returns false when no step strictly improves the distance, so callers
    /// fall through to their normal movement instead of jittering in place.
    /// </summary>
    public static bool TryStepAway(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        IReadOnlyCollection<Position> threats,
        Claims claims,
        string reason)
    {
        return ChooseStep(new StepRequest(
                StepIntent.Away, contract, mind, body, threats, claims))
                is Position destination
            && TryMoveDirect(
                contract,
                mind,
                body,
                destination,
                claims,
                reason,
                TacticalMovementAuthority.Safety);
    }

    public static bool TryMoveDirect(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Position destination,
        Claims claims,
        string reason,
        TacticalMovementAuthority authority =
            TacticalMovementAuthority.Tactical) =>
        Submit(
            contract,
            mind,
            body,
            ChooseStep(new StepRequest(
                StepIntent.Direct, contract, mind, body, [destination], claims)),
            claims,
            reason,
            authority);

    public static bool IsThreatened(
        MindContext mind,
        MindBody body) => (mind.VisibleProjectiles ?? [])
        .Where(projectile => projectile.OwnerTeamId != body.ActorId.TeamId)
        .Any(projectile => Threatens(projectile, body.Position, advances: 2));

    public static Position[] SafeEvasionGoals(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body)
    {
        GenericActorContext.ObservedProjectile[] hostile =
            mind.VisibleProjectiles?
                .Where(projectile => projectile.OwnerTeamId != body.ActorId.TeamId)
                .ToArray()
            ?? [];
        if (!hostile.Any(projectile =>
                Threatens(projectile, body.Position, advances: 2)))
        {
            return [];
        }
        return RouteHeadings(contract, body)
            .Select(heading => Step(body.Position, heading))
            .Where(tile => CanEnter(contract.Map, tile)
                && !hostile.Any(projectile => Threatens(projectile, tile, 2)))
            .Distinct()
            .ToArray();
    }

    public static Position[] AwayGoals(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        IReadOnlyCollection<Position> threats)
    {
        if (threats.Count == 0)
            return [];
        int current = threats.Min(threat =>
            threat.ChebyshevDistance(body.Position));
        return RouteHeadings(contract, body)
            .Select(heading => (
                Heading: heading,
                Tile: Step(body.Position, heading)))
            .Where(candidate => CanStep(
                    contract.Map, body.Position, candidate.Tile,
                    candidate.Heading)
                && threats.Min(threat =>
                    threat.ChebyshevDistance(candidate.Tile)) > current)
            .Select(candidate => candidate.Tile)
            .Distinct()
            .ToArray();
    }

    public static bool TryEvade(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Claims claims,
        string decisionTrace = "")
    {
        GenericActorContext.ObservedProjectile[] hostile =
            mind.VisibleProjectiles?
                .Where(projectile => projectile.OwnerTeamId != body.ActorId.TeamId)
                .ToArray()
            ?? [];
        if (!hostile.Any(projectile =>
                Threatens(projectile, body.Position, advances: 2)))
        {
            return false;
        }

        HashSet<Position> safe = [];
        foreach (ProjectileHeading heading in RouteHeadings(contract, body))
        {
            (int dx, int dy) = heading.Vector();
            Position tile = body.Position.Offset(dx, dy);
            if (CanEnter(contract.Map, tile)
                && !hostile.Any(projectile => Threatens(projectile, tile, 2)))
            {
                safe.Add(tile);
            }
        }
        return TryMoveToward(
            contract,
            mind,
            body,
            safe,
            claims,
            "evading carrier-lane fire"
                + (decisionTrace.Length == 0 ? "" : $":{decisionTrace}"),
            TacticalMovementAuthority.Safety);
    }

    /// <summary>
    /// The payload of one declared shot: the heading its ray flies down and,
    /// when the gun winds up, the identity that windup LOCKS (DECISIONS #222,
    /// owner correction — "the lock is the target picked by the MIND and
    /// nothing else"). Naming the focus is what makes a strike follow the body
    /// it was fired at instead of whiffing the moment that body steps a tile
    /// off the aimed line; an unnamed declare stays a lane-suppression shot.
    /// The mask is consulted so an instant gun, which offers no UnitTarget,
    /// keeps its historical single-argument shape.
    /// </summary>
    private static GenericActorActionArgument[] AttackArguments(
        GenericActorActionLegality action,
        ProjectileHeading heading,
        ActorIdentity? declaredTarget)
    {
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = action.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint>()
                .SingleOrDefault();
        var heading_ = new GenericActorActionArgument
            .ProjectileHeadingArgument(heading);
        if (declaredTarget is not ActorIdentity target || targets is null)
            return [heading_];
        var named = new GenericActorActionArgument.UnitTarget(
            target.TeamId, target.UnitId);
        return targets.AllowedValues.Contains(named)
            ? [heading_, new GenericActorActionArgument.UnitTargetArgument(named)]
            : [heading_];
    }

    public static bool TryShoot(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        GenericActorContext.ObservedEnemyState? preferred,
        bool preferredOnly = false)
    {
        GenericActorActionLegality? action = AvailableAction(
            contract,
            body,
            GenericActorRulesContract.ActionKind.Attack,
            requireAvailable: false);
        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Id, body.FormId, StringComparison.Ordinal));
        GenericActorRulesContract.AttackProfile? attack =
            form?.AttackProfileId is string attackId
                ? contract.Rules.AttackProfiles.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, attackId, StringComparison.Ordinal))
                : null;
        if (action is null || headings is null || attack is null)
            return false;

        IEnumerable<GenericActorContext.ObservedEnemyState> targets =
            preferred is null
                ? mind.Enemies
                    .OrderBy(enemy => enemy.Health)
                    .ThenBy(enemy =>
                        body.Position.ChebyshevDistance(enemy.Position))
                    .ThenBy(enemy => enemy.ActorId)
                : preferredOnly
                    ? [preferred]
                    : [preferred, .. mind.Enemies
                        .Where(enemy => enemy != preferred)
                        .OrderBy(enemy => enemy.Health)
                        .ThenBy(enemy => enemy.ActorId)];

        foreach (GenericActorContext.ObservedEnemyState target in targets)
        {
            if (!TryAimSolution(
                    contract.Map, attack, body.Position, target.Position,
                    headings.AllowedValues,
                    out ProjectileHeading heading, out int distance)
                || distance > attack.Projectile.MaxTravelTiles)
            {
                continue;
            }

            if (action.Available)
            {
                body.Command(
                    action.ActionId,
                    action.ActionCode,
                    AttackArguments(action, heading, target.ActorId),
                    $"focus fire on {target.ActorId}");
                return true;
            }
        }

        if (CarriedCore(mind, body.ActorId) is not null)
            return false;

        // A route-facing turn is a one-tick commitment: a facing-locked body
        // must actually take its promised forward step instead of swivelling
        // away immediately. After a successful route move, however, that same
        // body must be allowed to prepare an otherwise legal visible shot;
        // blocking both states made moving Deliberate formations patrol
        // forever without taking the promised next-tick aim opportunity.
        // Standard and Swift retain the stricter move commitment.
        GenericActorRulesContract.MovementProfile? movement =
            form?.MovementProfileId is string movementId
                ? contract.Rules.MovementProfiles.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, movementId,
                        StringComparison.Ordinal))
                : null;
        GenericActorRulesContract.ActionKind? previousKind =
            PreviousActionKind(contract, body);
        if (previousKind == GenericActorRulesContract.ActionKind.Rotation
            || (previousKind == GenericActorRulesContract.ActionKind.Movement
                && movement?.FacingCoupling
                    != GenericActorRulesContract.MovementFacingCoupling
                        .FacingLocked))
            return false;
        foreach (GenericActorContext.ObservedEnemyState target in targets)
        {
            // The turn pass: only for a target this body cannot ALREADY point
            // at. A wedge gun with a legal covering heading has nothing to
            // prepare — it either fires or it is on cadence.
            if (TryAimSolution(
                    contract.Map, attack, body.Position, target.Position,
                    headings.AllowedValues, out _, out _)
                || !TryAimSolution(
                    contract.Map, attack, body.Position, target.Position,
                    allowed: null,
                    out ProjectileHeading heading, out int distance)
                || distance > attack.Projectile.MaxTravelTiles)
            {
                continue;
            }
            Direction desired = FacingForCone(
                body.Facing, heading, MirroredFrame(contract, mind));
            if (TryRotate(
                    contract,
                    body,
                    desired,
                    $"prepare aim on {target.ActorId}"))
            {
                return true;
            }
        }
        return false;
    }

    public static bool CanFireAt(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        GenericActorContext.ObservedEnemyState target) =>
        CanFireAtPosition(contract, body, target.Position);

    public static bool CanFireAtPosition(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Position target)
    {
        GenericActorActionLegality? action = AvailableAction(
            contract, body, GenericActorRulesContract.ActionKind.Attack,
            requireAvailable: false);
        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(candidate => candidate.Id == body.FormId);
        GenericActorRulesContract.AttackProfile? attack =
            form?.AttackProfileId is string attackId
                ? contract.Rules.AttackProfiles.FirstOrDefault(candidate =>
                    candidate.Id == attackId)
                : null;
        return action is { Available: true }
            && headings is not null
            && attack is not null
            && TryAimSolution(
                contract.Map, attack, body.Position, target,
                headings.AllowedValues, out _, out int distance)
            && distance <= attack.Projectile.MaxTravelTiles;
    }

    /// <summary>
    /// Whether <see cref="TryShootAtPosition"/> will keep this body's floor
    /// tile this tick by either firing or rotating to prepare the shot. The
    /// squad planner needs the same answer before it plans another body into
    /// the apparent vacancy. Cooldown holds are deliberately false: an aimed
    /// body that cannot fire may still yield physical ground.
    /// </summary>
    public static bool CommitsAttackPositionThisTick(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Position target)
    {
        GenericActorActionLegality? action = AvailableAction(
            contract, body, GenericActorRulesContract.ActionKind.Attack,
            requireAvailable: false);
        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(candidate => candidate.Id == body.FormId);
        GenericActorRulesContract.AttackProfile? attack =
            form?.AttackProfileId is string attackId
                ? contract.Rules.AttackProfiles.FirstOrDefault(candidate =>
                    candidate.Id == attackId)
                : null;
        if (action is null || headings is null || attack is null)
            return false;

        bool aimed = TryAimSolution(
                contract.Map, attack, body.Position, target,
                headings.AllowedValues,
                out _, out int aimedDistance)
            && aimedDistance <= attack.Projectile.MaxTravelTiles;
        if (action.Available && aimed)
            return true;
        if (CarriedCore(mind, body.ActorId) is not null || aimed
            || !TryAimSolution(
                contract.Map, attack, body.Position, target,
                allowed: null,
                out ProjectileHeading prepared, out int reach)
            || reach > attack.Projectile.MaxTravelTiles)
        {
            return false;
        }

        Direction desired = FacingForCone(
            body.Facing, prepared, MirroredFrame(contract, mind));
        GenericActorActionLegality? rotate = AvailableAction(
            contract, body, GenericActorRulesContract.ActionKind.Rotation);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = rotate?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        return rotate is not null
            && directions is not null
            && body.Facing != desired
            && directions.AllowedValues.Contains(desired);
    }

    public static bool CanAimAt(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        GenericActorContext.ObservedEnemyState target) =>
        CanAimAtPosition(contract, body, target.Position);

    public static bool CanAimAtPosition(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Position target) => CanAimFrom(
            contract, body.FormId, body.Position, target);

    public static bool CanAimFrom(
        GenericActorResolvedMatchContract contract,
        string formId,
        Position source,
        Position target)
    {
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(candidate => candidate.Id == formId);
        GenericActorRulesContract.AttackProfile? attack =
            form?.AttackProfileId is string attackId
                ? contract.Rules.AttackProfiles.FirstOrDefault(candidate =>
                    candidate.Id == attackId)
                : null;
        // No mask: this asks whether the shot exists at all, for a body free to
        // turn first. A wedge gun answers for its whole cone.
        return attack is not null
            && TryAimSolution(
                contract.Map, attack, source, target,
                allowed: null, out _, out int distance)
            && distance <= attack.Projectile.MaxTravelTiles;
    }

    /// <summary>
    /// The fewest movement steps this body needs to reach a tile from which
    /// it can attack any currently team-visible enemy. Walls, protected
    /// regions, spawn reservations and visible movement-blocking constructs
    /// are durable obstacles; bodies are deliberately absent because the
    /// squad planner owns transient traffic.
    /// </summary>
    internal static int? NearestFiringApproachDistance(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Func<GenericActorContext.ObservedEnemyState, bool>? qualifies = null)
    {
        GenericActorContext.ObservedEnemyState[] enemies = qualifies is null
            ? mind.Enemies.ToArray()
            : mind.Enemies.Where(qualifies).ToArray();
        if (enemies.Length == 0)
            return null;
        HashSet<Position> obstacles = PersistentRouteObstacles(
            contract, mind, body.ActorId.TeamId);
        obstacles.Remove(body.Position);
        int[] reachable = StaticRouteDistanceField(
            contract, body, body.Position, obstacles);
        int reach = AttackRange(contract, body.FormId);
        int? best = null;
        foreach (GenericActorContext.ObservedEnemyState enemy in enemies)
        {
            int minimumY = Math.Max(0, enemy.Position.Y - reach);
            int maximumY = Math.Min(
                contract.Map.Height - 1, enemy.Position.Y + reach);
            int minimumX = Math.Max(0, enemy.Position.X - reach);
            int maximumX = Math.Min(
                contract.Map.Width - 1, enemy.Position.X + reach);
            for (int y = minimumY; y <= maximumY; y++)
            {
                for (int x = minimumX; x <= maximumX; x++)
                {
                    var tile = new Position(x, y);
                    int? distance = DistanceFromField(
                        contract.Map, reachable, tile);
                    if (distance is not int steps
                        || best is int known && steps >= known
                        || !CanAimFrom(
                            contract, body.FormId, tile, enemy.Position))
                    {
                        continue;
                    }
                    if (steps == 0)
                        return 0;
                    best = steps;
                }
            }
        }
        return best;
    }

    /// <summary>
    /// Every reachable tile inside the fight episode from which this form can
    /// contribute a real shot. Episode range remains anchored to the position
    /// where the authored fight began; reachability uses the same known durable
    /// obstacles as ordinary planning and ignores transient body occupancy.
    /// The squad planner assigns these to attackers jointly so "fight the same
    /// enemy" does not mean "stand on the same choke tile".
    /// </summary>
    internal static int[] FiringEpisodeReachability(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Position episodeAnchor,
        int chaseLimit)
    {
        HashSet<Position> obstacles = PersistentRouteObstacles(
            contract, mind, body.ActorId.TeamId);
        obstacles.Remove(body.Position);
        // The chase leash is a movement boundary, not merely a filter on the
        // final firing tile. A course may not duck outside the episode and
        // re-enter behind a wall while still claiming to respect the author.
        for (int y = 0; y < contract.Map.Height; y++)
        {
            for (int x = 0; x < contract.Map.Width; x++)
            {
                var tile = new Position(x, y);
                if (tile != body.Position
                    && (chaseLimit == 0 && tile != episodeAnchor
                        || chaseLimit > 0
                        && (StaticDistance(
                                contract.Map, episodeAnchor, tile)
                                is not int fromAnchor
                            || fromAnchor > chaseLimit)))
                {
                    obstacles.Add(tile);
                }
            }
        }
        return StaticRouteDistanceField(
            contract, body, body.Position, obstacles);
    }

    internal static Position[] FiringContributionGoals(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Position target,
        IReadOnlyList<int> episodeReachability)
    {
        var result = new List<Position>();
        int reach = AttackRange(contract, body.FormId);
        int minimumY = Math.Max(0, target.Y - reach);
        int maximumY = Math.Min(
            contract.Map.Height - 1, target.Y + reach);
        int minimumX = Math.Max(0, target.X - reach);
        int maximumX = Math.Min(
            contract.Map.Width - 1, target.X + reach);
        for (int y = minimumY; y <= maximumY; y++)
        {
            for (int x = minimumX; x <= maximumX; x++)
            {
                var tile = new Position(x, y);
                if (DistanceFromField(
                        contract.Map, episodeReachability, tile) is null)
                {
                    continue;
                }
                if (!CanAimFrom(contract, body.FormId, tile, target))
                    continue;
                result.Add(tile);
            }
        }
        return result.ToArray();
    }

    private static int AttackRange(
        GenericActorResolvedMatchContract contract,
        string formId) => contract.Rules.Forms
            .FirstOrDefault(candidate => candidate.Id == formId)
            ?.AttackProfileId is string attackId
                ? contract.Rules.AttackProfiles.FirstOrDefault(candidate =>
                    candidate.Id == attackId)?.Projectile.MaxTravelTiles ?? 0
                : 0;

    public static bool TryShootAtPosition(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Position target,
        string reason,
        ActorIdentity? declaredTarget = null)
    {
        GenericActorActionLegality? action = AvailableAction(
            contract, body, GenericActorRulesContract.ActionKind.Attack,
            requireAvailable: false);
        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(candidate => candidate.Id == body.FormId);
        GenericActorRulesContract.AttackProfile? attack =
            form?.AttackProfileId is string attackId
                ? contract.Rules.AttackProfiles.FirstOrDefault(candidate =>
                    candidate.Id == attackId)
                : null;
        if (action is null || headings is null || attack is null)
            return false;
        // Ask the MASK first. A wedge gun very often already has a legal
        // heading covering the target, and that is the whole fix: the tick
        // that used to be spent turning onto an exact ray becomes the declare.
        bool aimed = TryAimSolution(
                contract.Map, attack, body.Position, target,
                headings.AllowedValues,
                out ProjectileHeading heading, out int distance)
            && distance <= attack.Projectile.MaxTravelTiles;
        if (action.Available && aimed)
        {
            body.Command(
                action.ActionId,
                action.ActionCode,
                AttackArguments(action, heading, declaredTarget),
                $"{reason}; cover {target}");
            return true;
        }
        // Already aimed and still not firing means the gun is on cadence, not
        // mis-pointed; turning would only throw the tick away.
        if (CarriedCore(mind, body.ActorId) is not null
            || aimed
            || !TryAimSolution(
                contract.Map, attack, body.Position, target,
                allowed: null,
                out ProjectileHeading turned, out int reach)
            || reach > attack.Projectile.MaxTravelTiles)
        {
            return false;
        }
        return TryRotate(
            contract, body,
            FacingForCone(body.Facing, turned, MirroredFrame(contract, mind)),
            $"prepare {reason}; cover {target}");
    }

    public static bool CanUseUnitSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind,
        ActorIdentity target)
    {
        GenericActorRulesContract.ArcRelaySignature? signature = Signature(
            contract, signatureKind);
        GenericActorActionLegality? action = signature is null
            ? null
            : body.Action(signature.ActionId);
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint>()
                .SingleOrDefault();
        var wanted = new GenericActorActionArgument.UnitTarget(
            target.TeamId, target.UnitId);
        return action is { Available: true }
            && targets?.AllowedValues.Contains(wanted) == true;
    }

    public static bool TryUnitSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind,
        ActorIdentity target,
        string reason)
    {
        GenericActorRulesContract.ArcRelaySignature? signature = Signature(
            contract, signatureKind);
        GenericActorActionLegality? action = signature is null
            ? null
            : body.Action(signature.ActionId);
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint>()
                .SingleOrDefault();
        GenericActorActionArgument.UnitTarget wanted =
            new(target.TeamId, target.UnitId);
        if (!CanUseUnitSignature(contract, body, signatureKind, target)
            || action is null || targets is null)
        {
            return false;
        }

        body.Command(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.UnitTargetArgument(wanted)],
            reason);
        return true;
    }

    /// <summary>
    /// Declares a LOCKED line attack at a named enemy — rail or hook on a
    /// ruleset that carries the strike lock (owner ruling 2026-08-09).
    /// </summary>
    /// <remarks>
    /// The declare is the strike's declare: the wedge of the chosen heading,
    /// not an exact ray, plus the NAME of the body it is for. Both halves
    /// matter. The wedge is ninety degrees wide, so a body in reach almost
    /// always has a legal heading already and spends no tick turning; and the
    /// name is what survives the target's step, because movement resolves
    /// before the declare is read (DECISIONS #222b). A signature that does
    /// not publish a UnitTarget is not a lock signature and falls through to
    /// the plain heading cast.
    /// </remarks>
    public static bool TryLockedLineSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind,
        ActorIdentity targetActor,
        Position target,
        string reason)
    {
        if (!TryResolveLockedLineSignature(
                contract, body, signatureKind, targetActor, target,
                out GenericActorActionLegality? action,
                out ProjectileHeading heading,
                out GenericActorActionArgument.UnitTarget named))
        {
            return false;
        }
        body.Command(
            action.ActionId,
            action.ActionCode,
            [
                new GenericActorActionArgument.ProjectileHeadingArgument(
                    heading),
                new GenericActorActionArgument.UnitTargetArgument(named),
            ],
            reason);
        return true;
    }

    public static bool CanUseLockedLineSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind,
        ActorIdentity targetActor,
        Position target) => TryResolveLockedLineSignature(
            contract, body, signatureKind, targetActor, target,
            out _, out _, out _);

    private static bool TryResolveLockedLineSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind,
        ActorIdentity targetActor,
        Position target,
        out GenericActorActionLegality action,
        out ProjectileHeading heading,
        out GenericActorActionArgument.UnitTarget named)
    {
        GenericActorRulesContract.ArcRelaySignature? signature = Signature(
            contract,
            signatureKind);
        GenericActorActionLegality? candidateAction = signature is null
            ? null
            : body.Action(signature.ActionId);
        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = candidateAction?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = candidateAction?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint>()
                .SingleOrDefault();
        named = new GenericActorActionArgument.UnitTarget(
            targetActor.TeamId,
            targetActor.UnitId);
        heading = default;
        action = null!;
        if (candidateAction is not { Available: true }
            || headings is null
            || targets is null
            || !targets.AllowedValues.Contains(named)
            || signature?.Range is not int reach
            || !TryWedgeSolution(
                contract.Map,
                body.Position,
                target,
                headings.AllowedValues,
                reach,
                strictCorners: true,
                out heading))
        {
            return false;
        }
        action = candidateAction;
        return true;
    }

    /// <summary>
    /// Whether a signature declares the strike lock on this ruleset: its
    /// action publishes a UnitTarget beside its heading.
    /// </summary>
    public static bool DeclaresLock(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind)
    {
        GenericActorRulesContract.ArcRelaySignature? signature = Signature(
            contract,
            signatureKind);
        GenericActorActionLegality? action = signature is null
            ? null
            : body.Action(signature.ActionId);
        return action is not null
            && action.Constraints.OfType<GenericActorActionLegality
                .ArgumentConstraint.ProjectileHeadingConstraint>().Any()
            && action.Constraints.OfType<GenericActorActionLegality
                .ArgumentConstraint.UnitTargetConstraint>().Any();
    }

    public static bool TryHeadingSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind,
        Position target,
        string reason)
    {
        if (!TryResolveHeadingSignature(
                contract, body, signatureKind, target,
                out GenericActorActionLegality? action,
                out ProjectileHeading heading))
        {
            return false;
        }
        body.Command(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.ProjectileHeadingArgument(heading)],
            reason);
        return true;
    }

    public static bool CanUseHeadingSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind,
        Position target) => TryResolveHeadingSignature(
            contract, body, signatureKind, target, out _, out _);

    private static bool TryResolveHeadingSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind,
        Position target,
        out GenericActorActionLegality action,
        out ProjectileHeading heading)
    {
        GenericActorRulesContract.ArcRelaySignature? signature = Signature(
            contract,
            signatureKind);
        GenericActorActionLegality? candidateAction = signature is null
            ? null
            : body.Action(signature.ActionId);
        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = candidateAction?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        action = null!;
        heading = default;
        if (candidateAction is not { Available: true }
            || headings is null
            || !TryRay(body.Position, target, out heading,
                out int distance)
            || signature?.Range is int range && distance > range
            || !headings.AllowedValues.Contains(heading)
            || !ClearRay(contract.Map, body.Position, target, true))
        {
            return false;
        }
        action = candidateAction;
        return true;
    }

    public static bool CanUseDirectionSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind)
    {
        GenericActorRulesContract.ArcRelaySignature? signature = Signature(
            contract, signatureKind);
        GenericActorActionLegality? action = signature is null
            ? null
            : body.Action(signature.ActionId);
        return action is { Available: true }
            && action.Constraints.OfType<GenericActorActionLegality
                .ArgumentConstraint.DirectionConstraint>()
                .SingleOrDefault()?.AllowedValues.Length > 0;
    }

    public static bool CanUsePositionSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind)
    {
        GenericActorRulesContract.ArcRelaySignature? signature = Signature(
            contract, signatureKind);
        GenericActorActionLegality? action = signature is null
            ? null
            : body.Action(signature.ActionId);
        return action is { Available: true }
            && action.Constraints.OfType<GenericActorActionLegality
                .ArgumentConstraint.PositionTargetConstraint>()
                .SingleOrDefault()?.AllowedValues.Length > 0;
    }

    public static bool CanUseParameterlessSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind)
    {
        GenericActorRulesContract.ArcRelaySignature? signature = Signature(
            contract, signatureKind);
        GenericActorActionLegality? action = signature is null
            ? null
            : body.Action(signature.ActionId);
        return action is { Available: true } && action.Constraints.IsEmpty;
    }

    public static bool TryDirectionSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind,
        Position target,
        string reason,
        bool mirrored = false)
    {
        GenericActorRulesContract.ArcRelaySignature? signature = Signature(
            contract,
            signatureKind);
        GenericActorActionLegality? action = signature is null
            ? null
            : body.Action(signature.ActionId);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        Direction? selected = directions?.AllowedValues
            .OrderBy(direction =>
            {
                (int dx, int dy) = direction.Vector();
                return body.Position.Offset(dx, dy).ChebyshevDistance(target);
            })
            .ThenBy(direction => mirrored
                ? ((int)direction + 2) % 4
                : (int)direction)
            .Select(direction => (Direction?)direction)
            .FirstOrDefault();
        if (action is not { Available: true } || selected is not Direction value)
            return false;

        body.Command(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(value)],
            reason);
        return true;
    }

    public static bool TryPositionSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind,
        Position target,
        string reason,
        Func<Position, bool>? extraFilter = null,
        bool mirrored = false)
    {
        GenericActorRulesContract.ArcRelaySignature? signature = Signature(
            contract,
            signatureKind);
        GenericActorActionLegality? action = signature is null
            ? null
            : body.Action(signature.ActionId);
        GenericActorActionLegality.ArgumentConstraint.PositionTargetConstraint?
            targets = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .PositionTargetConstraint>()
                .SingleOrDefault();
        Position? selected = targets?.AllowedValues
            .Where(position => extraFilter?.Invoke(position) ?? true)
            .OrderBy(position => position.ChebyshevDistance(target))
            .ThenBy(position => FrameY(position, mirrored))
            .ThenBy(position => FrameX(position, mirrored))
            .Select(position => (Position?)position)
            .FirstOrDefault();
        if (action is not { Available: true } || selected is not Position position)
            return false;

        body.Command(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.PositionTargetArgument(position)],
            reason);
        return true;
    }

    public static bool TryParameterlessSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind,
        string reason)
    {
        GenericActorRulesContract.ArcRelaySignature? signature = Signature(
            contract,
            signatureKind);
        GenericActorActionLegality? action = signature is null
            ? null
            : body.Action(signature.ActionId);
        if (action is not { Available: true } || !action.Constraints.IsEmpty)
            return false;
        body.Command(action.ActionId, action.ActionCode, [], reason);
        return true;
    }

    public static Position Cutoff(
        GenericActorMapContract map,
        Position carrier,
        Position reactor,
        int leadTiles = 2)
    {
        Position cursor = carrier;
        for (int index = 0; index < leadTiles; index++)
        {
            int dx = Math.Sign(reactor.X - cursor.X);
            int dy = Math.Sign(reactor.Y - cursor.Y);
            Position diagonal = cursor.Offset(dx, dy);
            Position horizontal = cursor.Offset(dx, 0);
            Position vertical = cursor.Offset(0, dy);
            Position next = CanEnter(map, diagonal) ? diagonal
                : CanEnter(map, horizontal) ? horizontal
                : CanEnter(map, vertical) ? vertical
                : cursor;
            if (next == cursor)
                break;
            cursor = next;
        }
        return cursor;
    }

    public static Position[] ApproachTiles(
        GenericActorMapContract map,
        Position target) =>
        EightWay
            .Select(heading =>
            {
                (int dx, int dy) = heading.Vector();
                return target.Offset(dx, dy);
            })
            .Where(position => CanEnter(map, position))
            .Distinct()
            .ToArray();

    public static GenericActorRulesContract.ArcRelaySignature? Signature(
        GenericActorResolvedMatchContract contract,
        string kind) =>
        ArcRules(contract)?.Signatures.FirstOrDefault(signature =>
            string.Equals(signature.Kind, kind, StringComparison.Ordinal));

    public static bool HasSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string kind)
    {
        GenericActorRulesContract.ArcRelaySignature? signature =
            Signature(contract, kind);
        return signature is not null
            && body.Action(signature.ActionId) is { AllowedByForm: true };
    }

    internal static bool TryRotate(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Direction direction,
        string reason)
    {
        GenericActorActionLegality? rotate = AvailableAction(
            contract,
            body,
            GenericActorRulesContract.ActionKind.Rotation);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = rotate?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        if (rotate is null
            || directions is null
            || body.Facing == direction
            || !directions.AllowedValues.Contains(direction))
        {
            return false;
        }
        body.Command(
            rotate.ActionId,
            rotate.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(direction)],
            reason);
        return true;
    }

    private static GenericActorActionLegality? AvailableAction(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        GenericActorRulesContract.ActionKind kind,
        bool requireAvailable = true)
    {
        HashSet<string> ids = contract.Rules.Actions
            .Where(action => action.Kind == kind)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        return body.ActionLegalities
            .Where(action =>
                ids.Contains(action.ActionId)
                && action.AllowedByForm
                && (!requireAvailable || action.Available))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static GenericActorActionLegality? MovementAction(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        ProjectileHeading heading,
        Position destination,
        bool requireAvailable)
    {
        Dictionary<string, GenericActorRulesContract.ActionDefinition> actions =
            contract.Rules.Actions
                .Where(action => action.Kind
                    == GenericActorRulesContract.ActionKind.Movement)
                .ToDictionary(action => action.Id, StringComparer.Ordinal);
        GenericActorActionLegality[] candidates = body.ActionLegalities
            .Where(legality => actions.ContainsKey(legality.ActionId)
                // The legality surface includes catalog actions this chassis
                // does not own so clients can reason over one stable action
                // vocabulary. They are never executable candidates. Without
                // this guard a Standard carrier near contact preferred the
                // unavailable Swift strafe, then correctly refused to submit
                // it and silently waited forever while its trace still named
                // the planner's valid move.
                && legality.AllowedByForm
                && (!requireAvailable || legality.Available)
                && legality.Constraints
                    .OfType<GenericActorActionLegality.ArgumentConstraint
                        .ProjectileHeadingConstraint>()
                    .SingleOrDefault()?.AllowedValues.Contains(heading) == true)
            .OrderBy(legality => legality.ActionCode)
            .ToArray();
        if (candidates.Length <= 1)
            return candidates.SingleOrDefault();

        GenericActorActionLegality? strafe = candidates.FirstOrDefault(
            legality => actions[legality.ActionId].MovementFacingOverride
                == GenericActorRulesContract.MovementFacingCoupling
                    .PreserveFacing);
        GenericActorActionLegality? turn = candidates.FirstOrDefault(
            legality => actions[legality.ActionId].MovementFacingOverride
                is null);
        if (strafe is not null
            && turn is not null
            && WouldTurn(body.Facing, heading)
            && PreservesImmediateEngagement(
                contract,
                mind,
                body,
                destination))
        {
            return strafe;
        }
        return turn ?? strafe ?? candidates[0];
    }

    private static bool PreservesImmediateEngagement(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Position destination)
    {
        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            cone = AvailableAction(
                    contract,
                    body,
                    GenericActorRulesContract.ActionKind.Attack,
                    requireAvailable: false)
                ?.Constraints.OfType<GenericActorActionLegality
                    .ArgumentConstraint.ProjectileHeadingConstraint>()
                .SingleOrDefault();
        string? attackId = contract.Rules.Forms.FirstOrDefault(form =>
            string.Equals(form.Id, body.FormId, StringComparison.Ordinal))
            ?.AttackProfileId;
        GenericActorRulesContract.AttackProfile? attack =
            contract.Rules.AttackProfiles.FirstOrDefault(profile =>
                string.Equals(profile.Id, attackId, StringComparison.Ordinal));
        if (cone is null || attack is null)
            return false;

        return mind.Enemies.Any(enemy =>
            TryRay(destination, enemy.Position, out ProjectileHeading heading,
                out int distance)
            && distance <= attack.Projectile.MaxTravelTiles + 1
            && cone.AllowedValues.Contains(heading)
            && ClearRay(
                contract.Map,
                destination,
                enemy.Position,
                attack.Projectile.DiagonalCornersMustBeClear));
    }

    private static bool WouldTurn(
        Direction facing,
        ProjectileHeading heading)
    {
        int facingSector = (int)facing * 2;
        int clockwise = ((int)heading - facingSector + 8) % 8;
        return heading is ProjectileHeading.North
                or ProjectileHeading.East
                or ProjectileHeading.South
                or ProjectileHeading.West
            ? (int)heading != facingSector
            : clockwise is not (1 or 7);
    }

    internal static Direction FacingForCone(
        Direction current,
        ProjectileHeading target,
        bool mirrored)
    {
        Direction[] candidates = Enum.GetValues<Direction>()
            .Where(direction => HeadingDistance(
                (ProjectileHeading)((int)direction * 2), target) <= 1)
            .OrderBy(direction => HeadingDistance(
                (ProjectileHeading)((int)current * 2),
                (ProjectileHeading)((int)direction * 2)))
            .ThenBy(direction => mirrored
                ? ((int)direction + 2) % 4
                : (int)direction)
            .ToArray();
        return candidates[0];
    }

    internal static Direction FacingTowardAnchor(
        Direction current,
        Position from,
        Position anchor,
        bool mirrored)
    {
        int dx = Math.Sign(anchor.X - from.X);
        int dy = Math.Sign(anchor.Y - from.Y);
        if (dx == 0 && dy == 0)
            return current;
        ProjectileHeading target = FromVector(dx, dy);
        ProjectileHeading currentHeading =
            (ProjectileHeading)((int)current * 2);
        return HeadingDistance(currentHeading, target) <= 1
            ? current
            : FacingForCone(current, target, mirrored);
    }

    private static int HeadingDistance(
        ProjectileHeading first,
        ProjectileHeading second)
    {
        int difference = Math.Abs((int)first - (int)second);
        return Math.Min(difference, 8 - difference);
    }

    internal static ProjectileHeading[] RouteHeadings(
        GenericActorResolvedMatchContract contract,
        MindBody body)
    {
        string? profileId = contract.Rules.Forms
            .FirstOrDefault(form =>
                string.Equals(form.Id, body.FormId, StringComparison.Ordinal))
            ?.MovementProfileId;
        GenericActorRulesContract.MovementProfile? profile =
            contract.Rules.MovementProfiles.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, profileId, StringComparison.Ordinal));
        return profile?.FacingCoupling
                == GenericActorRulesContract.MovementFacingCoupling.FacingLocked
            ? Cardinal
            : EightWay;
    }

    internal static GenericActorRulesContract.MovementFacingCoupling
        MovementCoupling(
            GenericActorResolvedMatchContract contract,
            MindBody body)
    {
        string? profileId = contract.Rules.Forms
            .FirstOrDefault(form => string.Equals(
                form.Id, body.FormId, StringComparison.Ordinal))
            ?.MovementProfileId;
        return contract.Rules.MovementProfiles
            .FirstOrDefault(profile => string.Equals(
                profile.Id, profileId, StringComparison.Ordinal))
            ?.FacingCoupling
            ?? GenericActorRulesContract.MovementFacingCoupling.PreserveFacing;
    }

    internal static Direction FacingAfterMove(
        Direction before,
        ProjectileHeading heading,
        GenericActorRulesContract.MovementFacingCoupling coupling) =>
        coupling switch
        {
            GenericActorRulesContract.MovementFacingCoupling.PreserveFacing =>
                before,
            GenericActorRulesContract.MovementFacingCoupling.FacingLocked =>
                before,
            GenericActorRulesContract.MovementFacingCoupling
                    .FaceMovementDirection =>
                CardinalDirection(heading) ?? before,
            GenericActorRulesContract.MovementFacingCoupling
                    .FaceMovementHeadingProjected =>
                ProjectedFacing(before, heading),
            GenericActorRulesContract.MovementFacingCoupling.CombatStrafe =>
                HeadingDistance(
                    (ProjectileHeading)((int)before * 2), heading) >= 3
                    ? Opposite(before)
                    : before,
            _ => before,
        };

    internal static Direction? CardinalDirection(ProjectileHeading heading) =>
        heading switch
        {
            ProjectileHeading.North => Direction.North,
            ProjectileHeading.East => Direction.East,
            ProjectileHeading.South => Direction.South,
            ProjectileHeading.West => Direction.West,
            _ => null,
        };

    private static Direction ProjectedFacing(
        Direction before,
        ProjectileHeading heading)
    {
        if (CardinalDirection(heading) is Direction cardinal)
            return cardinal;
        int clockwise = ((int)heading - (int)before * 2 + 8) % 8;
        return clockwise is 1 or 7 ? before : Opposite(before);
    }

    private static Direction Opposite(Direction direction) =>
        (Direction)(((int)direction + 2) % 4);

    internal static ProjectileHeading[] PreferredHeadings(
        MindBody body,
        IReadOnlyCollection<Position> goals,
        bool mirrored)
    {
        Position nearest = goals
            .OrderBy(goal => body.Position.ChebyshevDistance(goal))
            // Tie-breaks are expressed in the team's canonical frame: an
            // absolute lowest-Y/lowest-X preference would make mirrored
            // positions produce different decisions, which measured as a
            // 6-7/8 east skew in -03 mirror matches.
            .ThenBy(goal => mirrored ? -goal.Y : goal.Y)
            .ThenBy(goal => mirrored ? -goal.X : goal.X)
            .First();
        int dx = Math.Sign(nearest.X - body.Position.X);
        int dy = Math.Sign(nearest.Y - body.Position.Y);
        ProjectileHeading facing =
            (ProjectileHeading)((int)body.Facing * 2);
        return EightWay
            .OrderBy(heading =>
            {
                (int hx, int hy) = heading.Vector();
                return Math.Abs(hx - dx) + Math.Abs(hy - dy);
            })
            // Among equally direct shortest-path choices, continue the
            // current bearing. This preserves straight Deliberate runs
            // without lengthening the route or changing any game rule.
            .ThenBy(heading => heading == facing ? 0 : 1)
            .ThenBy(heading => mirrored
                ? ((int)heading + 4) % 8
                : (int)heading)
            .ToArray();
    }

    /// <summary>
    /// Tie-break keys expressed in the team's canonical frame: absolute
    /// lowest-Y/lowest-X preferences pick opposite relative tiles for the
    /// two sides of a rotationally bound map.
    /// </summary>
    /// <summary>Rotation-canonical heading order for tie-breaks.</summary>
    public static int FrameHeading(ProjectileHeading heading, bool mirrored) =>
        mirrored ? ((int)heading + 4) % 8 : (int)heading;

    public static int FrameY(Position position, bool mirrored) =>
        mirrored ? -position.Y : position.Y;

    public static int FrameX(Position position, bool mirrored) =>
        mirrored ? -position.X : position.X;

    /// <summary>
    /// True when this mind's canonical frame is the 180-degree rotation of
    /// the world (own reactor on the east half). Decision tie-breaks route
    /// through this so mirrored situations produce mirrored choices.
    /// </summary>
    public static bool MirroredFrame(
        GenericActorResolvedMatchContract contract,
        MindContext mind)
    {
        if (mind.Mode is not
                GenericActorContext.ModeObservationState.ArcRelay arc)
            return false;
        MindBody? first = mind.Bodies.FirstOrDefault();
        if (first is null)
            return false;
        int team = first.ActorId.TeamId;
        GenericActorContext.ArcRelayReactorState? own = arc.Reactors
            .FirstOrDefault(reactor => reactor.TeamId == team);
        return own is not null
            && own.Position.X > (contract.Map.Width - 1) / 2;
    }

    private static GenericActorRulesContract.ActionKind? PreviousActionKind(
        GenericActorResolvedMatchContract contract,
        MindBody body)
    {
        string? previousActionId = body.PreviousActionResolution
            ?.AcceptedAction.ActionId;
        if (previousActionId is null)
            return null;
        return contract.Rules.Actions
            .FirstOrDefault(action => string.Equals(
                action.Id,
                previousActionId,
                StringComparison.Ordinal))
            ?.Kind;
    }

    internal static HashSet<Position> BlockedNow(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody moving,
        Claims claims,
        bool avoidHostileProjectiles = true) =>
        BlockedNow(
            contract, mind, moving, claims, out _, avoidHostileProjectiles);

    /// <summary>
    /// Everything this body may not step onto right now, and — separately —
    /// the part of it that is only a QUEUE.
    /// </summary>
    /// <param name="queued">Tiles blocked purely because a teammate holds
    /// or has reserved them for this one tick. They are walkable ground
    /// that will be free again next tick, which is exactly why the arbiter
    /// must not treat them like walls: routing around a teammate that is
    /// about to leave is how a body ends up taking back the tile it just
    /// walked (owner scene 2026-08-10, the dance beside a passing carrier).
    /// </param>
    internal static HashSet<Position> BlockedNow(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody moving,
        Claims claims,
        out HashSet<Position> queued,
        bool avoidHostileProjectiles = true)
    {
        // The queue is exactly the ground a teammate is walking THROUGH:
        // a reserved right-of-way lane, and a tile somebody has already
        // committed to entering this tick. A teammate standing still is a
        // wall like any other — it is not going anywhere.
        queued = [];
        foreach (Position tile in claims.Committed)
            queued.Add(tile);
        foreach ((Position lane, int owner) in claims.Lanes)
        {
            // Right-of-way runs down ONE precedence list: a lane walls the
            // bodies that yield to its owner and nobody else. A stronger
            // movement intent is never routed around a weaker reservation.
            if (claims.YieldsTo(moving.UnitId, owner))
                queued.Add(lane);
        }
        // A request this body made and the plane granted: the parked body
        // standing there is stepping aside this tick, so the ground is a
        // queue rather than a wall. This is what stops the mover detouring
        // around the teammate it just asked to move.
        foreach ((Position tile, int mover) in claims.Demands)
        {
            if (mover == moving.UnitId)
                queued.Add(tile);
        }
        queued.Remove(moving.Position);
        var blocked = new HashSet<Position>(claims.Tiles);
        blocked.Remove(moving.Position);
        foreach ((Position lane, int owner) in claims.Lanes)
        {
            if (claims.YieldsTo(moving.UnitId, owner))
                blocked.Add(lane);
        }
        // Durable refusals, kept apart so the queue can be subtracted from
        // them at the end: a lane that is also a mine is still a mine.
        var durable = new HashSet<Position>();
        durable.UnionWith(mind.Allies.Select(ally => ally.Position));
        durable.UnionWith(mind.Enemies.Select(enemy => enemy.Position));
        durable.UnionWith(mind.VisibleTiles
            .Where(tile => tile.SpawnReservation is not null)
            .Select(tile => tile.Position));
        // An armed hostile mine is a visible kill tile; walk around it the
        // way the stock mind always has.
        if (mind.Mode is GenericActorContext.ModeObservationState.ArcRelay
            arcMode)
        {
            durable.UnionWith(arcMode.VisibleSignatures
                .Where(signature => SignatureBlocksMovement(
                    signature, moving.ActorId.TeamId))
                .SelectMany(signature => signature.Positions));
        }

        foreach (GenericActorContext.ObservedProjectile projectile
                 in avoidHostileProjectiles ? mind.VisibleProjectiles ?? [] : [])
        {
            if (projectile.OwnerTeamId == moving.ActorId.TeamId)
                continue;
            durable.Add(projectile.Position);
            Position cursor = projectile.Position;
            int remaining = Math.Min(
                projectile.RemainingTiles,
                projectile.TilesPerAdvance * 2);
            (int dx, int dy) = projectile.Heading.Vector();
            for (int step = 0; step < remaining; step++)
            {
                cursor = cursor.Offset(dx, dy);
                durable.Add(cursor);
            }
        }

        if (contract.ModeMapBinding
            is GenericActorResolvedMatchContract.ArcRelayModeMapBinding binding)
        {
            HashSet<int> hostileParticipants = contract.Topology.Participants
                .Where(participant => participant.TeamId != moving.ActorId.TeamId)
                .Select(participant => participant.ParticipantId)
                .ToHashSet();
            HashSet<string> protectedRegions = contract.ParticipantRegionAssignments
                .Where(assignment =>
                    hostileParticipants.Contains(assignment.ParticipantId)
                    && string.Equals(
                        assignment.RegionRoleId,
                        binding.HomePadRegionRoleId,
                        StringComparison.Ordinal))
                .Select(assignment => assignment.MapRegionId)
                .ToHashSet(StringComparer.Ordinal);
            durable.UnionWith(contract.Map.Regions
                .Where(region => protectedRegions.Contains(region.RegionId))
                .SelectMany(region => region.Tiles));
        }

        if (moving.PreviousActionResolution
                is { Outcome: GenericActorActionResolution.ActionOutcome.Blocked }
                previous
            && previous.AcceptedAction.Arguments
                .OfType<GenericActorActionArgument.ProjectileHeadingArgument>()
                .SingleOrDefault() is { } oldMove)
        {
            (int dx, int dy) = oldMove.Value.Vector();
            durable.Add(moving.Position.Offset(dx, dy));
        }
        // Anything ALSO refused for a durable reason is a wall, not a
        // queue: waiting for it to clear would be waiting forever.
        queued.ExceptWith(durable);
        blocked.UnionWith(durable);
        return blocked;
    }

    /// <summary>Mirror ArcRelaySignatureRuntime.BlocksBody for constructs the
    /// mind can observe. Hardlight is solid to both teams; an armed trip node
    /// is avoided only by its opponent.</summary>
    internal static bool SignatureBlocksMovement(
        GenericActorContext.ArcRelaySignatureState signature,
        int movingTeamId) => !signature.Suppressed
            && signature.Phase
                == GenericActorContext.ArcRelaySignaturePhase.Active
            && (string.Equals(
                    signature.Kind, "hardlight-block",
                    StringComparison.Ordinal)
                || string.Equals(
                    signature.Kind, "trip-node",
                    StringComparison.Ordinal)
                    && signature.OwnerTeamId != movingTeamId);

    internal static ProjectileHeading? FindFirstStep(
        GenericActorMapContract map,
        Position start,
        IReadOnlySet<Position> goals,
        IReadOnlySet<Position> blockedNow,
        IReadOnlyCollection<ProjectileHeading> allowed,
        ProjectileHeading[] preference,
        bool blockThroughout = false)
    {
        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Position, ProjectileHeading First)>();
        foreach (ProjectileHeading heading in preference)
        {
            if (!allowed.Contains(heading))
                continue;
            Position next = Step(start, heading);
            if (!CanStep(map, start, next, heading)
                || blockedNow.Contains(next)
                || !visited.Add(next))
            {
                continue;
            }
            if (goals.Contains(next))
                return heading;
            queue.Enqueue((next, heading));
        }

        while (queue.Count > 0)
        {
            (Position position, ProjectileHeading first) = queue.Dequeue();
            foreach (ProjectileHeading heading in preference)
            {
                if (!allowed.Contains(heading))
                    continue;
                Position next = Step(position, heading);
                if (!CanStep(map, position, next, heading)
                    || blockThroughout && blockedNow.Contains(next)
                    || !visited.Add(next))
                    continue;
                if (goals.Contains(next))
                    return first;
                queue.Enqueue((next, first));
            }
        }
        return null;
    }

    private static bool Threatens(
        GenericActorContext.ObservedProjectile projectile,
        Position target,
        int advances) =>
        TryRay(projectile.Position, target, out ProjectileHeading heading,
            out int distance)
        && heading == projectile.Heading
        && distance <= Math.Min(
            projectile.RemainingTiles,
            projectile.TilesPerAdvance * advances);

    private static bool TryRay(
        Position source,
        Position target,
        out ProjectileHeading heading,
        out int distance)
    {
        int dx = target.X - source.X;
        int dy = target.Y - source.Y;
        distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (distance == 0
            || dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy))
        {
            heading = default;
            return false;
        }
        heading = FromVector(Math.Sign(dx), Math.Sign(dy));
        return true;
    }

    /// <summary>
    /// The eight headings, in canonical enum order — the candidate set when
    /// nothing has narrowed it.
    /// </summary>
    private static readonly ProjectileHeading[] AllHeadings =
        Enum.GetValues<ProjectileHeading>().Order().ToArray();

    /// <summary>
    /// Whether this gun declares a WEDGE rather than a ray.
    /// </summary>
    /// <remarks>
    /// A declared strike freezes a filled 90-degree cone at declare, and since
    /// the named-lock ruling (DECISIONS #222) it resolves against the body the
    /// MIND named anywhere inside that cone. So "can I shoot that" is wedge
    /// membership — and asking for exact 8-way alignment first, which is what
    /// this executor did, was a fossil of the bolt gun it was originally
    /// written for. It threw away all but eight rays of a 90-degree weapon and
    /// spent the difference standing in range, rotating.
    ///
    /// An instant bolt gun keeps the ray test, because its bolt really does
    /// travel the ray: a target off the line is a miss, not a lock. So does a
    /// VOLLEY strike, which the engine resolves per frozen ray and which locks
    /// nothing at all.
    /// </remarks>
    private static bool DeclaresWedge(
        GenericActorRulesContract.AttackProfile attack) =>
        attack.Projectile.StrikeWindupTicks > 0 && attack.Volley is null;

    /// <summary>
    /// Whether a tile lies inside the declared wedge of <paramref name="heading"/>.
    /// </summary>
    /// <remarks>
    /// The exact twin of the engine's <c>GenericActorStrikeCone</c>: within
    /// ±45° of the central heading (<c>dot >= |cross|</c>, boundary inclusive),
    /// within Chebyshev reach, and reachable by the canonical Bresenham strike
    /// line without crossing a wall. That line IS the delivery path at
    /// maturation, so a covered tile is a hittable tile — the two must agree or
    /// the mind declares strikes the engine will not lock.
    /// </remarks>
    private static bool WithinStrikeWedge(
        GenericActorMapContract map,
        Position source,
        Position target,
        ProjectileHeading heading,
        int reach,
        bool strictCorners)
    {
        int dx = target.X - source.X;
        int dy = target.Y - source.Y;
        if (dx == 0 && dy == 0)
            return false;
        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) > reach)
            return false;
        (int ux, int uy) = heading.Vector();
        if (dx * ux + dy * uy < Math.Abs(dx * uy - dy * ux))
            return false;
        return StrikeLineReaches(map, source, target, strictCorners);
    }

    /// <summary>
    /// Whether the canonical integer-Bresenham strike line from
    /// <paramref name="source"/> arrives at <paramref name="target"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="ClearRay"/> cannot answer this: it walks by the sign of the
    /// delta, which only ever lands on an exactly aligned target. The wedge is
    /// full of tiles that are on no ray at all, and the engine reaches them by
    /// Bresenham — so this mirrors that walk step for step, including the
    /// cut-corner rule.
    /// </remarks>
    private static bool StrikeLineReaches(
        GenericActorMapContract map,
        Position source,
        Position target,
        bool strictCorners)
    {
        int x = source.X;
        int y = source.Y;
        int dx = Math.Abs(target.X - x);
        int dy = Math.Abs(target.Y - y);
        int sx = Math.Sign(target.X - x);
        int sy = Math.Sign(target.Y - y);
        int error = dx - dy;
        while (x != target.X || y != target.Y)
        {
            int doubled = 2 * error;
            int stepX = 0;
            int stepY = 0;
            if (doubled > -dy)
            {
                error -= dy;
                stepX = sx;
            }
            if (doubled < dx)
            {
                error += dx;
                stepY = sy;
            }
            var from = new Position(x, y);
            var next = new Position(x + stepX, y + stepY);
            if (!CanEnter(map, next)
                || stepX != 0 && stepY != 0 && strictCorners
                && (!CanEnter(map, from.Offset(stepX, 0))
                    || !CanEnter(map, from.Offset(0, stepY))))
            {
                return false;
            }
            x = next.X;
            y = next.Y;
        }
        return true;
    }

    /// <summary>
    /// Whether a lock declared at <paramref name="tile"/> would stay
    /// TRACKABLE — the shooter-side half of the engine's cancel rule.
    /// </summary>
    /// <remarks>
    /// Owner ruling 2026-08-09, spotter doctrine: a lock lives while the
    /// shooter's TEAM sees the target AND a clear physical ray joins shooter
    /// and target, facing ignored. Team vision is free here — the mind's
    /// observation IS the team union, so anything it can name it can see —
    /// which leaves exactly one geometric question for the declare gate: is
    /// there a wall or a cut corner between the two? That is the engine's
    /// canonical corner-strict supercover ray, mirrored step for step.
    /// <para>
    /// It replaces the own-eyes quadrant this gate used to carry (DECISIONS
    /// #234). A body may now declare into its own blind flank and TRACK
    /// there, as long as a teammate is watching and nothing solid is in the
    /// way.
    /// </para>
    /// </remarks>
    private static bool HasClearSightRay(
        GenericActorMapContract map,
        Position origin,
        Position target)
    {
        int dx = target.X - origin.X;
        int dy = target.Y - origin.Y;
        int nx = Math.Abs(dx);
        int ny = Math.Abs(dy);
        int sx = Math.Sign(dx);
        int sy = Math.Sign(dy);
        int x = origin.X;
        int y = origin.Y;
        int ix = 0;
        int iy = 0;
        while (ix < nx || iy < ny)
        {
            long crossX = (long)(1 + 2 * ix) * ny;
            long crossY = (long)(1 + 2 * iy) * nx;
            if (crossX == crossY)
            {
                // Exact corner crossing: both side tiles count, so sight
                // cannot slip diagonally between two walls.
                if (!Clear(map, new Position(x + sx, y), target)
                    || !Clear(map, new Position(x, y + sy), target))
                {
                    return false;
                }
                x += sx;
                y += sy;
                ix++;
                iy++;
            }
            else if (crossX < crossY)
            {
                x += sx;
                ix++;
            }
            else
            {
                y += sy;
                iy++;
            }
            if (!Clear(map, new Position(x, y), target))
                return false;
        }
        return true;

        static bool Clear(
            GenericActorMapContract map,
            Position tile,
            Position target) => tile == target || CanEnter(map, tile);
    }

    /// <summary>
    /// The heading a shot at <paramref name="target"/> would declare, and
    /// whether there is one at all.
    /// </summary>
    /// <param name="allowed">
    /// The mask's legal headings, or null to consider all eight — which is what
    /// "could I aim at this if I turned" means.
    /// </param>
    /// <remarks>
    /// A ray gun has at most one answer. A wedge gun usually has two or three,
    /// and picks the one whose central ray is BEARING-CLOSEST to the target:
    /// comparing <c>cos</c> of the offset angle, kept integral as
    /// <c>dot² · |u'|²</c> against <c>dot'² · |u|²</c> so a cardinal heading and
    /// a diagonal one are weighed on the same scale. Ties break on canonical
    /// heading order, because a declare is a recorded decision and two bodies
    /// in the same situation must make the same one.
    ///
    /// Centring matters beyond taste: the wedge is frozen at declare and the
    /// target is free to move inside it for the whole windup, so the most
    /// central heading is the one that keeps the most room around the lock.
    /// </remarks>
    private static bool TryAimSolution(
        GenericActorMapContract map,
        GenericActorRulesContract.AttackProfile attack,
        Position source,
        Position target,
        IReadOnlyCollection<ProjectileHeading>? allowed,
        out ProjectileHeading heading,
        out int distance)
    {
        heading = default;
        distance = Math.Max(
            Math.Abs(target.X - source.X),
            Math.Abs(target.Y - source.Y));
        if (!DeclaresWedge(attack))
        {
            return TryRay(source, target, out heading, out _)
                && (allowed is null || allowed.Contains(heading))
                && ClearRay(
                    map, source, target,
                    attack.Projectile.DiagonalCornersMustBeClear);
        }
        return TryWedgeSolution(
            map,
            source,
            target,
            allowed,
            attack.Projectile.MaxTravelTiles,
            attack.Projectile.DiagonalCornersMustBeClear,
            out heading);
    }

    /// <summary>
    /// The heading a WEDGE declare would take at <paramref name="target"/>:
    /// the legal heading whose ninety-degree wedge covers it and whose
    /// central ray is bearing-closest, or none.
    /// </summary>
    /// <remarks>
    /// One shape for both wedge declares — the gun's strike and the locked
    /// line signature — because the engine resolves them through one piece of
    /// geometry and a mind that disagreed would declare things the engine
    /// throws away. Ties break on canonical heading order, because a declare
    /// is a recorded decision and two bodies in the same situation must make
    /// the same one.
    /// </remarks>
    private static bool TryWedgeSolution(
        GenericActorMapContract map,
        Position source,
        Position target,
        IReadOnlyCollection<ProjectileHeading>? allowed,
        int reach,
        bool strictCorners,
        out ProjectileHeading heading)
    {
        heading = default;
        // A declare whose lock stops being trackable cancels at maturation,
        // so the tracking rule is part of the declare gate and not a nicety.
        // Since the spotter ruling (owner, 2026-08-09) that rule is team
        // vision plus a clear physical ray — and the mind's observation IS
        // team vision, so all that is left to check here is the ray. Facing
        // no longer enters it: declaring into a blind flank is legal AND
        // trackable while a teammate watches.
        if (!HasClearSightRay(map, source, target))
            return false;
        int dx = target.X - source.X;
        int dy = target.Y - source.Y;
        bool found = false;
        long bestDot = 0;
        long bestLengthSquared = 1;
        foreach (ProjectileHeading candidate in allowed ?? AllHeadings)
        {
            if (!WithinStrikeWedge(
                    map, source, target, candidate, reach, strictCorners))
            {
                continue;
            }
            (int ux, int uy) = candidate.Vector();
            long dot = dx * ux + dy * uy;
            long lengthSquared = ux * ux + uy * uy;
            // cos(candidate) > cos(best): dot/|u| > bestDot/|best|, squared to
            // stay in integers (both dots are non-negative inside a wedge).
            if (found
                && dot * dot * bestLengthSquared
                    <= bestDot * bestDot * lengthSquared)
            {
                continue;
            }
            found = true;
            bestDot = dot;
            bestLengthSquared = lengthSquared;
            heading = candidate;
        }
        return found;
    }

    /// <summary>
    /// Every own body a shot could meet, as tiles. Other participants on the
    /// same team count: a cast that kills an ally is a cast that killed an
    /// ally, whoever was driving it.
    /// </summary>
    internal static HashSet<Position> Friendlies(MindContext mind)
    {
        var friends = new HashSet<Position>();
        foreach (MindBody body in mind.Bodies)
            friends.Add(body.Position);
        foreach (GenericActorContext.ObservedAllyState ally in mind.Allies)
            friends.Add(ally.Position);
        return friends;
    }

    /// <summary>
    /// Who eats a declared bolt first: walking the engine's canonical strike
    /// line from the shooter's frozen origin to the tile it locked, the first
    /// OWN-TEAM body met. Null when the line reaches the lock without meeting
    /// one.
    ///
    /// <para>This is the reading that makes bodyguarding real. Delivery is
    /// first-body-contact against the shooter's enemies — which is us — and
    /// the engine says so itself at the matured-strike path
    /// (<c>GenericActorMatchSession.cs:3191</c>): "Bodyguarding is stepping
    /// onto the firing line, never proximity". So a body standing on a lit
    /// tile is only actually in danger if it is the FIRST of ours on that
    /// line.</para>
    /// </summary>
    internal static Position? FirstOwnBodyOnLine(
        Position origin,
        Position lockTile,
        IReadOnlySet<Position> friendlies)
    {
        int x = origin.X;
        int y = origin.Y;
        int dx = Math.Abs(lockTile.X - x);
        int dy = Math.Abs(lockTile.Y - y);
        int sx = Math.Sign(lockTile.X - x);
        int sy = Math.Sign(lockTile.Y - y);
        int error = dx - dy;
        while (x != lockTile.X || y != lockTile.Y)
        {
            int doubled = 2 * error;
            if (doubled > -dy)
            {
                error -= dy;
                x += sx;
            }
            if (doubled < dx)
            {
                error += dx;
                y += sy;
            }
            var tile = new Position(x, y);
            if (friendlies.Contains(tile))
                return tile;
        }
        return null;
    }

    private static bool ClearRay(
        GenericActorMapContract map,
        Position source,
        Position target,
        bool strictCorners)
    {
        int dx = Math.Sign(target.X - source.X);
        int dy = Math.Sign(target.Y - source.Y);
        Position cursor = source;
        while (cursor != target)
        {
            Position next = cursor.Offset(dx, dy);
            if (next != target && !CanEnter(map, next))
                return false;
            if (strictCorners && dx != 0 && dy != 0
                && (!CanEnter(map, cursor.Offset(dx, 0))
                    || !CanEnter(map, cursor.Offset(0, dy))))
            {
                return false;
            }
            cursor = next;
        }
        return true;
    }

    internal static bool CanStep(
        GenericActorMapContract map,
        Position from,
        Position to,
        ProjectileHeading heading)
    {
        if (!CanEnter(map, to))
            return false;
        (int dx, int dy) = heading.Vector();
        return dx == 0 || dy == 0
            || CanEnter(map, from.Offset(dx, 0))
                && CanEnter(map, from.Offset(0, dy));
    }

    internal static bool CanEnter(GenericActorMapContract map, Position tile) =>
        tile.X >= 0
        && tile.Y >= 0
        && tile.X < map.Width
        && tile.Y < map.Height
        && map.TileRows[tile.Y][tile.X] != '#';

    internal static Position Step(Position position, ProjectileHeading heading)
    {
        (int dx, int dy) = heading.Vector();
        return position.Offset(dx, dy);
    }

    internal static ProjectileHeading FromVector(int dx, int dy) =>
        (dx, dy) switch
        {
            (0, -1) => ProjectileHeading.North,
            (1, -1) => ProjectileHeading.NorthEast,
            (1, 0) => ProjectileHeading.East,
            (1, 1) => ProjectileHeading.SouthEast,
            (0, 1) => ProjectileHeading.South,
            (-1, 1) => ProjectileHeading.SouthWest,
            (-1, 0) => ProjectileHeading.West,
            (-1, -1) => ProjectileHeading.NorthWest,
            _ => throw new ArgumentOutOfRangeException(nameof(dx)),
        };

    private static Direction? ToDirection(ProjectileHeading heading) =>
        heading switch
        {
            ProjectileHeading.North => Direction.North,
            ProjectileHeading.East => Direction.East,
            ProjectileHeading.South => Direction.South,
            ProjectileHeading.West => Direction.West,
            _ => null,
        };
}
