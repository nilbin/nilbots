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
        /// THE precedence list. One ordering, used for every contested tile
        /// — lane ownership, who yields to whom, and the executor's per-tick
        /// plan order:
        ///
        /// <list type="number">
        /// <item><description><b>0 carriers</b> — a body holding a Core.
        /// Its walk home is the match; nothing outranks it and it never
        /// yields.</description></item>
        /// <item><description><b>1 escort leaders</b> — a leader never
        /// negotiates with its own escort for the ground it wants.
        /// </description></item>
        /// <item><description><b>2 fighters</b> — a body holding a focus
        /// assignment or standing in contact. Its tile is the one everyone
        /// else must route around, so it settles first.</description></item>
        /// <item><description><b>3 rest</b> — free traffic.</description>
        /// </item>
        /// <item><description><b>4 + ordinal followers</b> — escort
        /// followers, in their order's declared list order. Last, and among
        /// themselves ordered, so a file walks a corridor as a file.
        /// </description></item>
        /// </list>
        ///
        /// Equal ranks resolve by lower unit id, so exactly one of a mutual
        /// pair ever moves. The fighter tier reaches the executor's plan
        /// ORDER only under a stepper that asks for it
        /// (<see cref="IArenaStepper.WantsFightPrecedence"/>) — right-of-way
        /// always reads the full list.
        /// </summary>
        public const int CarrierRank = 0;
        public const int LeaderRank = 1;
        public const int FighterRank = 2;
        public const int RestRank = 3;
        public const int FollowerRank = 4;

        private readonly HashSet<Position> _tiles = [];
        private readonly HashSet<Position> _committed = [];
        private readonly Dictionary<Position, int> _lanes = [];
        private readonly Dictionary<Position, int> _demands = [];
        private readonly Dictionary<int, int> _ranks = [];
        private readonly HashSet<int> _rooted = [];

        private Claims(IArenaStepper stepper) => Stepper = stepper;

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

        /// <summary>Right-of-way tiles, keyed to the owning unit. A loaded
        /// carrier's next route step and an escort leader's next step are
        /// reserved for that body alone: no LOWER-PRECEDENCE own body may
        /// step into one this tick, so an orbiting escort can never steal
        /// the lane the moment its blocker finally yields (owner direction
        /// 2026-08: prioritize whose movement weighs most and adjust the
        /// rest). A stronger body is never walled by a weaker body's lane —
        /// that mutual wall was one of the fights.
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
        public void Rank(int unitId, int rank) => _ranks[unitId] = rank;

        /// <summary>Where a unit sits on the precedence list; anything the
        /// executor did not classify is free traffic.</summary>
        public int RankOf(int unitId) =>
            _ranks.TryGetValue(unitId, out int rank) ? rank : RestRank;

        /// <summary>
        /// Whether <paramref name="unitId"/> gives way to
        /// <paramref name="otherUnitId"/>: STRICTLY better rank, nothing
        /// else. Every yield in the mind asks exactly this question.
        ///
        /// <para>Equal weight never yields. Two carriers walking the same
        /// corridor are settled by the plan order and the tile claims — a
        /// mutual yield between equals is how a second carrier ends up
        /// walled out of its own bank by the first one's lane, which is a
        /// stalemate rather than a courtesy.</para>
        /// </summary>
        public bool YieldsTo(int unitId, int otherUnitId) =>
            unitId != otherUnitId
            && RankOf(otherUnitId) < RankOf(unitId);

        public void ReserveLane(Position tile, int ownerUnitId) =>
            _lanes[tile] = ownerUnitId;

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
        public void Demand(Position tile, int moverUnitId)
        {
            if (!_demands.TryGetValue(tile, out int held)
                || RankOf(moverUnitId) < RankOf(held)
                || (RankOf(moverUnitId) == RankOf(held) && moverUnitId < held))
            {
                _demands[tile] = moverUnitId;
            }
        }

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
        public void Commit(MindBody body, Position tile)
        {
            Reserve(tile);
            _committed.Add(tile);
            Stepper.NoteCommitted(body, tile);
        }

        public static Claims ForTick(
            GenericActorResolvedMatchContract contract,
            MindContext mind,
            IArenaStepper stepper)
        {
            stepper.BeginTick(contract, mind);
            var claims = new Claims(stepper);
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
        string reason) =>
        Submit(
            contract,
            mind,
            body,
            ChooseStep(new StepRequest(
                StepIntent.Toward, contract, mind, body, goals, claims)),
            claims,
            reason);

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
        string reason)
    {
        if (chosen is not Position destination)
            return false;
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
            claims.Commit(body, destination);
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
            claims.Commit(body, destination);
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
                reason);
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
            reason);

    /// <summary>
    /// THE POLITENESS RULE, and the movement plane's only displacement:
    /// this body is standing on ground somebody stronger needs this tick, so
    /// the arbiter steps it aside. Owner ruling 2026-08-10, watching a
    /// walker curve around a parked teammate with open ground beside it —
    /// <b>"that bot could just move out of the way"</b>.
    ///
    /// <para>Two things can owe the ground, and they are the same question
    /// asked at two distances: a reserved right-of-way LANE (a carrier's
    /// next route step, an escort leader's, a plugged homeward ring), and a
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
        return Submit(
            contract,
            mind,
            body,
            ChooseStep(new StepRequest(
                StepIntent.MakeWay, contract, mind, body, owed, claims,
                mover?.Position)),
            claims,
            $"{reason}:u{moverUnitId}");
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
                contract, mind, body, destination, claims, reason);
    }

    public static bool TryMoveDirect(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Position destination,
        Claims claims,
        string reason) =>
        Submit(
            contract,
            mind,
            body,
            ChooseStep(new StepRequest(
                StepIntent.Direct, contract, mind, body, [destination], claims)),
            claims,
            reason);

    public static bool TryEvade(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Claims claims)
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
            "evading carrier-lane fire");
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

    public static bool CanAimAt(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        GenericActorContext.ObservedEnemyState target) =>
        CanAimAtPosition(contract, body, target.Position);

    public static bool CanAimAtPosition(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Position target)
    {
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(candidate => candidate.Id == body.FormId);
        GenericActorRulesContract.AttackProfile? attack =
            form?.AttackProfileId is string attackId
                ? contract.Rules.AttackProfiles.FirstOrDefault(candidate =>
                    candidate.Id == attackId)
                : null;
        // No mask: this asks whether the shot exists at all, for a body free to
        // turn first. A wedge gun answers for its whole cone.
        return attack is not null
            && TryAimSolution(
                contract.Map, attack, body.Position, target,
                allowed: null, out _, out int distance)
            && distance <= attack.Projectile.MaxTravelTiles;
    }

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
    /// <param name="pierceBlockers">When non-null this cast PIERCES: the
    /// whole segment it would damage must be clear of these tiles. Null for
    /// a signature that stops at its target.</param>
    public static bool TryLockedLineSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind,
        ActorIdentity targetActor,
        Position target,
        string reason,
        IReadOnlySet<Position>? pierceBlockers = null)
    {
        GenericActorRulesContract.ArcRelaySignature? signature = Signature(
            contract,
            signatureKind);
        GenericActorActionLegality? action = signature is null
            ? null
            : body.Action(signature.ActionId);
        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint>()
                .SingleOrDefault();
        var named = new GenericActorActionArgument.UnitTarget(
            targetActor.TeamId,
            targetActor.UnitId);
        if (action is not { Available: true }
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
                out ProjectileHeading heading))
        {
            return false;
        }
        if (pierceBlockers is not null
            && FriendlyOnPierceLine(
                contract.Map, body.Position, heading, reach, pierceBlockers))
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

    /// <param name="pierceBlockers">When non-null this cast PIERCES: the
    /// whole segment it would damage must be clear of these tiles.</param>
    public static bool TryHeadingSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind,
        Position target,
        string reason,
        IReadOnlySet<Position>? pierceBlockers = null)
    {
        GenericActorRulesContract.ArcRelaySignature? signature = Signature(
            contract,
            signatureKind);
        GenericActorActionLegality? action = signature is null
            ? null
            : body.Action(signature.ActionId);
        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        if (action is not { Available: true }
            || headings is null
            || !TryRay(body.Position, target, out ProjectileHeading heading,
                out int distance)
            || signature?.Range is int range && distance > range
            || !headings.AllowedValues.Contains(heading)
            || !ClearRay(contract.Map, body.Position, target, true))
        {
            return false;
        }
        if (pierceBlockers is not null
            && FriendlyOnPierceLine(
                contract.Map,
                body.Position,
                heading,
                signature?.Range ?? distance,
                pierceBlockers))
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

    private static bool TryRotate(
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

    private static Direction FacingForCone(
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
            // bodies that yield to its owner and nobody else. A carrier is
            // never routed around its own escort's reservation.
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
                .Where(signature => string.Equals(
                        signature.SignatureId, "trip-node",
                        StringComparison.Ordinal)
                    && signature.OwnerTeamId != moving.ActorId.TeamId
                    && !signature.Suppressed
                    && signature.Phase
                        == GenericActorContext.ArcRelaySignaturePhase.Active)
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
    /// Whether an own body stands anywhere on the segment a PIERCING line
    /// would damage: from the caster out along the declared heading to the
    /// signature's reach, stopping at the first wall.
    ///
    /// <para>Ordinary fire needs no such test on this ruleset — Arc Relay
    /// sets <c>AlliedProjectileContactKind.PassThrough</c>
    /// (<c>ArcRelayH0Definition.cs:388</c>), so a bolt or a matured strike
    /// flies THROUGH its own team untouched. The rail is the exception and
    /// the reason this exists: <c>ApplyRailLine</c>
    /// (<c>GenericActorMatchSession.cs:2425</c>) damages every life on every
    /// tile of its path with no team filter at all — compare
    /// <c>ApplySentinelFire</c> two methods down, which explicitly requires
    /// <c>target.ActorId.TeamId != effect.Owner.TeamId</c>. The rail does not
    /// stop at first contact either, so "is a teammate between me and the
    /// target" is the wrong question for it: everything on the line is hit,
    /// including the ally standing one tile PAST the enemy.</para>
    /// </summary>
    internal static bool FriendlyOnPierceLine(
        GenericActorMapContract map,
        Position source,
        ProjectileHeading heading,
        int reach,
        IReadOnlySet<Position> friendlies)
    {
        if (friendlies.Count == 0)
            return false;
        (int dx, int dy) = heading.Vector();
        Position cursor = source;
        for (int step = 0; step < reach; step++)
        {
            Position next = cursor.Offset(dx, dy);
            if (!CanEnter(map, next))
                return false;
            if (friendlies.Contains(next))
                return true;
            cursor = next;
        }
        return false;
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
