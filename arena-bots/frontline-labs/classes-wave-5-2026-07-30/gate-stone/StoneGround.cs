using BotArena.Sdk;

/// <summary>
/// Ground: what a push is worth right now, which tile this body owes the team,
/// and how to get there when a step costs a turn. Two contract facts drive all
/// of it — the live hold (a capture completed inside an enemy hold is SPENT, so
/// pace matters more than speed) and weight-scaled control (a second body on the
/// objective is real pressure, not a spectator).
/// </summary>
internal static class StoneGround
{
    private const int Unreachable = 1 << 20;

    /// <summary>One hostile bolt priced against a tile.</summary>
    /// <param name="Ticks">Ticks until it arrives.</param>
    /// <param name="Damage">Health one contact removes.</param>
    /// <param name="Heading">Heading it arrives on.</param>
    public sealed record Incoming(
        int Ticks,
        int Damage,
        ProjectileHeading Heading);

    /// <summary>What the objective is worth to this team on this tick.</summary>
    /// <param name="Deny">Enemy weight is present and must be answered.</param>
    /// <param name="WantSurplus">A second body on the objective earns its keep.</param>
    /// <param name="Throttled">
    /// We are on pace to complete a capture inside an enemy hold, where it would
    /// be spent — so pressure is worth more than progress until it lapses.
    /// </param>
    /// <param name="Hold">The live hold, or null when none binds.</param>
    /// <param name="OwnWeight">Own objective weight observed on the objective.</param>
    /// <param name="EnemyWeight">Enemy weight observed (a lower bound).</param>
    /// <param name="ClaimNearlyDone">
    /// Our own claim is within a couple of ticks of completing — the state in
    /// which finishing inside an enemy hold burns the whole window.
    /// </param>
    /// <param name="Frozen">
    /// Capture cannot move at all this tick — the redeploy pause after an
    /// advance. Objective weight buys literally nothing while it runs, so this
    /// is the window in which fortifying is FREE.
    /// </param>
    /// <param name="WeightTick">
    /// What one point of our own objective weight is worth this tick, in
    /// capture progress. Under a net-scaling control policy the signed gain is
    /// linear in net weight, so the marginal value of this body is exactly its
    /// own weight — and it is zero when progress is frozen or would be spent.
    /// This single number is the currency the whole doctrine trades in.
    /// </param>
    /// <param name="ReliefTicks">
    /// Ticks until our body count can next grow, from the slots' own declared
    /// clocks. It decides whether a body may leave the point at all.
    /// </param>
    public sealed record Push(
        bool Deny,
        bool WantSurplus,
        bool Throttled,
        ArenaBasics.Hold? Hold,
        int OwnWeight,
        int EnemyWeight,
        bool ClaimNearlyDone,
        bool Frozen,
        int WeightTick,
        int ReliefTicks);

    /// <summary>The tile set this body is trying to stand on, and why.</summary>
    /// <param name="Tiles">Goal tiles in preference order.</param>
    /// <param name="StandFast">
    /// True when holding this tile is the point — the case where a shield beats
    /// a sidestep.
    /// </param>
    /// <param name="Reason">Diagnostic text.</param>
    public sealed record Station(
        Position[] Tiles,
        bool StandFast,
        string Reason);

    /// <summary>Hostile bolts that reach a tile, cheapest question first.</summary>
    public static List<Incoming> Inbound(
        StoneContract lens,
        GenericActorContext context,
        Position tile)
    {
        var threats = new List<Incoming>();
        foreach (GenericActorContext.ObservedProjectile bolt
                 in context.VisibleProjectiles ?? [])
        {
            if (bolt.OwnerTeamId == lens.TeamId)
                continue;
            ArenaBasics.Incoming? incoming = ArenaBasics.Threat(bolt, tile);
            if (incoming is not null)
            {
                threats.Add(new Incoming(
                    incoming.TicksUntilArrival,
                    incoming.Damage,
                    bolt.Heading));
            }
        }
        return threats;
    }

    /// <summary>Damage that lands on a tile within a horizon.</summary>
    public static int DamageWithin(List<Incoming> threats, int ticks)
    {
        int total = 0;
        foreach (Incoming threat in threats)
        {
            if (threat.Ticks <= ticks)
                total += threat.Damage;
        }
        return total;
    }

    /// <summary>Prices the objective from the capture contract and the hold.</summary>
    public static Push Price(StoneContract lens, GenericActorContext context)
    {
        ArenaBasics.Hold? hold = ArenaBasics.LiveHold(context);
        (int own, int enemy, bool _) =
            ArenaBasics.ObjectivePresence(lens.Raw, context);
        ArenaBasics.CaptureRules? capture = lens.Capture;
        int relief =
            lens.TicksToRelief(lens.TeamId, context.Tick, context.TeamUnits);
        if (capture is null
            || context.Mode
                is not GenericActorContext.ModeObservationState.Frontline mode)
        {
            return new Push(
                enemy > 0,
                true,
                false,
                hold,
                own,
                enemy,
                false,
                false,
                lens.Weight(context.Self.FormId),
                relief);
        }

        bool claimIsMine = mode.ClaimingTeamId == lens.TeamId;
        int remaining = claimIsMine
            ? Math.Max(capture.Threshold - mode.CaptureProgress, 1)
            : capture.Threshold + mode.CaptureProgress;
        int pause = Math.Max(mode.ControlResumesAtTick - context.Tick, 0);
        int myWeight = lens.Weight(context.Self.FormId);
        bool onObjective = false;
        foreach (Position tile in StoneAim.ActiveObjective(lens, context))
        {
            if (tile == context.Self.Position)
                onObjective = true;
        }
        int netNow = own - enemy;
        int netWithMe = onObjective ? netNow : netNow + myWeight;
        int netWithSurplus = netWithMe + myWeight;

        int ticks = TicksToComplete(capture, netWithMe, remaining) + pause;
        int ticksSurplus =
            TicksToComplete(capture, netWithSurplus, remaining) + pause;

        bool enemyHold = hold is not null && !hold.Mine;
        // Inside an enemy hold the completion is spent, so the honest question
        // is not "can I capture faster" but "will this land after the lapse".
        bool wantSurplus =
            !enemyHold
            || (hold is not null && ticksSurplus >= hold.RemainingTicks);
        bool throttled =
            enemyHold && hold is not null && ticks < hold.RemainingTicks;

        // The redeploy pause is published as a clock, and while it runs NO
        // amount of weight moves the claim. That makes the pause the cheapest
        // fortify window on the board: the turret's faster gun costs nothing at
        // all, because the thing it is spending was already worth zero.
        bool frozen = pause > 0;
        int weightTick = frozen || throttled ? 0 : myWeight;
        return new Push(
            enemy > 0,
            wantSurplus,
            throttled,
            hold,
            own,
            enemy,
            claimIsMine && ticks <= 2,
            frozen,
            weightTick,
            relief);
    }

    private static int TicksToComplete(
        ArenaBasics.CaptureRules capture,
        int net,
        int remaining)
    {
        int gain = capture.SurplusWeightScalesGain
            ? Math.Max(net, 0) * Math.Max(capture.GainPerSoleTeamTick, 1)
            : (net > 0 ? Math.Max(capture.GainPerSoleTeamTick, 1) : 0);
        return gain <= 0 ? Unreachable : (remaining + gain - 1) / gain;
    }

    /// <summary>
    /// Which tile this body owes the team. The holder takes the objective's
    /// rear-most tile — the one the enemy has to walk into — and everyone else
    /// takes a bearing the holder does not already cover, because two bodies in
    /// one lane are one target for a fan.
    /// </summary>
    public static Station Choose(
        StoneContract lens,
        GenericActorContext context,
        Push push)
    {
        Position[] objective = StoneAim.ActiveObjective(lens, context);
        if (objective.Length == 0)
            return new Station([context.Self.Position], false, "no objective");

        bool holder = IsHolder(lens, context, objective);
        bool presenceUrgent = push.EnemyWeight > 0
            && OwnWeightExcludingSelf(lens, context, objective) == 0;

        if (holder || presenceUrgent)
        {
            // Banking a claim. An enemy hold makes our completion SPENT, and
            // this decay clock preserves a claim on an empty objective — so with
            // nobody contesting and one tick of progress left to give, the
            // cheapest thing a holder can do is step off its own objective and
            // come back when the hold lapses. Both halves are contract reads:
            // the hold clock from the observation, the decay policy from the
            // capture definition.
            if (holder
                && push.Throttled
                && push.ClaimNearlyDone
                && push.EnemyWeight == 0
                && push.Hold is { Mine: false })
            {
                Position[] banked = Covering(lens, context, objective);
                if (banked.Length > 0)
                    return new Station(banked, true, "banking the claim");
            }
            Position[] tiles = Ranked(
                lens,
                context,
                objective,
                preferRear: true);
            return new Station(
                tiles,
                true,
                presenceUrgent ? "deny" : "hold");
        }

        // Boots or fire? Weight-scaled control answers it exactly, and the answer
        // is the difference between a doctrine and a habit.
        //
        // While our NET weight is positive the claim already builds, and the
        // marginal body earns more on a shoulder tile with a lane onto the same
        // ground — the ground where this class's shield is also legal. The moment
        // net goes to zero or below, every point of weight is worth more than any
        // amount of fire: at zero nobody gains, and below zero the OPPONENT is
        // capturing while we shoot at it. The scaffold starter simply piles every
        // body onto the objective, and in the straight-only arms that beat a
        // shoulder-camping GateStone 6-0 for exactly this reason — it stood at
        // two weight against our one and the arithmetic did the rest.
        int myWeight = lens.Weight(context.Self.FormId);
        int net = push.OwnWeight - push.EnemyWeight;
        if (net <= 0 && myWeight > 0 && push.WantSurplus && !push.Throttled)
        {
            return new Station(
                Ranked(lens, context, objective, preferRear: false),
                true,
                "surplus");
        }

        Position[] cover = Covering(lens, context, objective);
        return cover.Length > 0
            ? new Station(cover, true, "shoulder")
            : new Station(
                Ranked(lens, context, objective, preferRear: false),
                true,
                "surplus");
    }

    /// <summary>Own objective weight on the active objective, minus this life.</summary>
    public static int OwnWeightExcludingSelf(
        StoneContract lens,
        GenericActorContext context,
        Position[] objective)
    {
        int weight = 0;
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            foreach (Position tile in objective)
            {
                if (ally.Position == tile)
                {
                    weight += lens.Weight(ally.FormId);
                    break;
                }
            }
        }
        return weight;
    }

    private static bool IsHolder(
        StoneContract lens,
        GenericActorContext context,
        Position[] objective)
    {
        // Whoever already stands on the objective keeps the job. Recomputing the
        // role from distance every tick makes two bodies swap duties mid-contact
        // and neither of them holds anything.
        bool selfOn = false;
        foreach (Position tile in objective)
        {
            if (tile == context.Self.Position)
                selfOn = true;
        }
        if (selfOn)
        {
            foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
            {
                if (lens.Weight(ally.FormId) == 0
                    || ally.ActorId.CompareTo(context.Self.ActorId) >= 0)
                {
                    continue;
                }
                foreach (Position tile in objective)
                {
                    if (tile == ally.Position)
                        return false;
                }
            }
            return true;
        }

        int mine = Nearest(context.Self.Position, objective);
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (lens.Weight(ally.FormId) == 0)
                continue;
            int theirs = Nearest(ally.Position, objective);
            if (theirs < mine)
                return false;
            if (theirs == mine
                && ally.ActorId.CompareTo(context.Self.ActorId) < 0)
            {
                return false;
            }
        }
        return true;
    }

    private static int Nearest(Position from, Position[] tiles)
    {
        int best = Unreachable;
        foreach (Position tile in tiles)
            best = Math.Min(best, from.ChebyshevDistance(tile));
        return best;
    }

    private static Position[] Ranked(
        StoneContract lens,
        GenericActorContext context,
        Position[] objective,
        bool preferRear)
    {
        Direction? advance = ArenaBasics.AdvanceDirection(lens.Raw, context);
        int range = lens.Attack(context.Self.FormId)?.Projectile.MaxTravelTiles
            ?? 6;
        var scored = new List<(Position Tile, int Score)>();
        foreach (Position tile in objective)
        {
            int score = 0;
            if (advance is Direction forward)
            {
                (int dx, int dy) = forward.Vector();
                int projection = tile.X * dx + tile.Y * dy;
                score += preferRear ? -projection * 3 : projection * 2;
            }
            // Not every tile of an objective is equally cheap to stand on: the
            // ones with fewer lanes into them cost the enemy a walk to reach.
            score -= Exposure(lens, tile, advance, range) * 2;
            score -= Clumping(context, tile) * 6;
            score -= context.Self.Position.ChebyshevDistance(tile);
            foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
            {
                if (ally.Position == tile)
                    score -= 60;
            }
            scored.Add((tile, score));
        }
        scored.Sort((left, right) =>
        {
            int compare = right.Score.CompareTo(left.Score);
            if (compare != 0)
                return compare;
            compare = left.Tile.Y.CompareTo(right.Tile.Y);
            return compare != 0
                ? compare
                : left.Tile.X.CompareTo(right.Tile.X);
        });
        var tiles = new Position[scored.Count];
        for (int index = 0; index < scored.Count; index++)
            tiles[index] = scored[index].Tile;
        return tiles;
    }

    /// <summary>
    /// Off-objective tiles that keep a bolt on the objective without spending a
    /// body's presence on it — where a body waits out a hold it cannot profit
    /// from.
    /// </summary>
    private static Position[] Covering(
        StoneContract lens,
        GenericActorContext context,
        Position[] objective)
    {
        GenericActorRulesContract.AttackProfile? attack =
            lens.Attack(context.Self.FormId);
        int range = attack?.Projectile.MaxTravelTiles ?? 4;
        Direction? advance = ArenaBasics.AdvanceDirection(lens.Raw, context);
        var scored = new List<(Position Tile, int Score)>();
        foreach (Position tile in Neighbourhood(lens, objective, radius: 2))
        {
            bool isObjective = false;
            foreach (Position own in objective)
            {
                if (own == tile)
                    isObjective = true;
            }
            if (isObjective)
                continue;

            int covered = 0;
            foreach (Position own in objective)
            {
                int distance = tile.ChebyshevDistance(own);
                if (distance == 0 || distance > range)
                    continue;
                if (Aligned(tile, own) && Clear(lens, tile, own))
                    covered++;
            }
            if (covered == 0)
                continue;

            int score = covered * 10 - Clumping(context, tile) * 8;
            // A covering tile that also permits the stance is worth more than
            // one that does not: it is where the shield can actually go up.
            GenericActorRulesContract.FormTransition? guard =
                lens.GuardRoute(context.Self.FormId);
            if (guard is not null && lens.RouteAllowedOn(guard, tile))
                score += 14;
            if (advance is Direction forward)
            {
                (int dx, int dy) = forward.Vector();
                int ahead = (tile.X - objective[0].X) * dx
                    + (tile.Y - objective[0].Y) * dy;
                // Waiting behind the objective keeps the retreat and keeps the
                // enemy walking into the lane rather than around it.
                score -= ahead * 4;
            }
            score -= context.Self.Position.ChebyshevDistance(tile);
            scored.Add((tile, score));
        }
        scored.Sort((left, right) =>
        {
            int compare = right.Score.CompareTo(left.Score);
            if (compare != 0)
                return compare;
            compare = left.Tile.Y.CompareTo(right.Tile.Y);
            return compare != 0
                ? compare
                : left.Tile.X.CompareTo(right.Tile.X);
        });
        int take = Math.Min(scored.Count, 6);
        var tiles = new Position[take];
        for (int index = 0; index < take; index++)
            tiles[index] = scored[index].Tile;
        return tiles;
    }

    private static IEnumerable<Position> Neighbourhood(
        StoneContract lens,
        Position[] centres,
        int radius)
    {
        var seen = new HashSet<Position>();
        foreach (Position centre in centres)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    Position tile = centre.Offset(dx, dy);
                    if (lens.IsWall(tile) || !seen.Add(tile))
                        continue;
                    yield return tile;
                }
            }
        }
    }

    /// <summary>
    /// How many tiles on the enemy's side of this one hold a clear eight-way
    /// firing lane onto it — the honest cost of standing there.
    /// </summary>
    private static int Exposure(
        StoneContract lens,
        Position tile,
        Direction? advance,
        int range)
    {
        int lanes = 0;
        (int fx, int fy) = advance?.Vector() ?? (0, 0);
        for (int heading = 0; heading < 8; heading++)
        {
            (int dx, int dy) = ((ProjectileHeading)heading).Vector();
            if (fx * dx + fy * dy <= 0)
                continue;
            Position cursor = tile;
            for (int step = 0; step < range; step++)
            {
                Position next = cursor.Offset(dx, dy);
                if (lens.IsWall(next))
                    break;
                if (dx != 0
                    && dy != 0
                    && (lens.IsWall(cursor.Offset(dx, 0))
                        || lens.IsWall(cursor.Offset(0, dy))))
                {
                    break;
                }
                cursor = next;
                lanes++;
            }
        }
        return lanes;
    }

    /// <summary>How badly a tile shares a firing lane with an ally.</summary>
    private static int Clumping(GenericActorContext context, Position tile)
    {
        int penalty = 0;
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            int distance = tile.ChebyshevDistance(ally.Position);
            if (distance is > 0 and <= 3 && Aligned(tile, ally.Position))
                penalty += 4 - distance;
        }
        return penalty;
    }

    private static bool Aligned(Position from, Position to)
    {
        int dx = Math.Abs(to.X - from.X);
        int dy = Math.Abs(to.Y - from.Y);
        return dx == 0 || dy == 0 || dx == dy;
    }

    private static bool Clear(StoneContract lens, Position from, Position to)
    {
        int stepX = Math.Sign(to.X - from.X);
        int stepY = Math.Sign(to.Y - from.Y);
        Position cursor = from;
        while (cursor != to)
        {
            Position next = cursor.Offset(stepX, stepY);
            if (next != to && lens.IsWall(next))
                return false;
            if (stepX != 0
                && stepY != 0
                && (lens.IsWall(cursor.Offset(stepX, 0))
                    || lens.IsWall(cursor.Offset(0, stepY))))
            {
                return false;
            }
            cursor = next;
        }
        return true;
    }

    /// <summary>
    /// One step of a cheapest route to <paramref name="goals"/>, planned over
    /// (tile, facing) when the contract locks movement to facing, so a rotation
    /// is scheduled as the real cost it is instead of being discovered by a
    /// refused mask. Plans on map geometry and on ONE piece of doctrine: ground
    /// where our stance route is legal is cheaper than ground where it is not,
    /// because a shield we cannot raise is a shield we do not have. The mask only
    /// decides how the planned step is spelled.
    ///
    /// <para>Transient occupants — bodies and bolts — never move the plan. They
    /// only decide whether this tick's step happens: a body queued behind an ally
    /// in a single-file choke keeps its gun pointed forward and waits, instead of
    /// spending two ticks turning into a detour that closes before it arrives.
    /// That oscillation cost a measured 8 ticks per contact before this rule
    /// existed.</para>
    /// </summary>
    public static GenericActorDecision? Step(
        StoneContract lens,
        GenericActorContext context,
        StoneMemory memory,
        Position[] goals,
        IEnumerable<Position>? avoid)
    {
        if (goals.Length == 0)
            return null;
        var targets = new HashSet<Position>(goals);
        if (targets.Contains(context.Self.Position))
            return null;

        GenericActorActionLegality? move = Available(lens, context, isMove: true);
        GenericActorActionLegality? rotate =
            Available(lens, context, isMove: false);
        if (move is null && rotate is null)
            return null;

        var avoided = new HashSet<Position>();
        if (avoid is not null)
        {
            foreach (Position tile in avoid)
                avoided.Add(tile);
        }
        Direction[] order = ArenaBasics.OrderedDirections(lens.Raw, context);
        bool facingLocked = lens.Coupling(context.Self.FormId)
            == GenericActorRulesContract.MovementFacingCoupling.FacingLocked;

        (Direction Direction, bool Rotation)? first = Route(
            lens,
            context,
            memory,
            targets,
            avoided,
            order,
            facingLocked);
        if (first is not (Direction step, bool rotation))
            return null;

        if (rotation)
        {
            return rotate is not null
                && Allows(rotate, step)
                && context.Self.Facing != step
                ? new GenericActorDecision(
                    rotate.ActionId,
                    rotate.ActionCode,
                    [new GenericActorActionArgument.DirectionArgument(step)],
                    $"turning {step} to unlock the step")
                : null;
        }

        if (move is not null && Allows(move, step))
        {
            return new GenericActorDecision(
                move.ActionId,
                move.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(step)],
                $"advancing {step}");
        }
        return rotate is not null
            && Allows(rotate, step)
            && context.Self.Facing != step
            ? new GenericActorDecision(
                rotate.ActionId,
                rotate.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(step)],
                $"turning {step} to unlock the step")
            : null;
    }

    /// <summary>
    /// Cheapest first action toward a goal. Dijkstra rather than breadth-first,
    /// because entering a tile is not always worth the same: a rotation and a
    /// step both cost one tick, and a tile this life just dodged off costs more.
    /// </summary>
    private static (Direction Direction, bool Rotation)? Route(
        StoneContract lens,
        GenericActorContext context,
        StoneMemory memory,
        HashSet<Position> goals,
        HashSet<Position> avoided,
        Direction[] order,
        bool facingLocked)
    {
        int width = lens.Width;
        int states = width * lens.Height * 4;
        var cost = new int[states];
        var firstStep = new (Direction Direction, bool Rotation)?[states];
        for (int index = 0; index < states; index++)
            cost[index] = Unreachable;

        // Occupied tiles are EXPENSIVE, not impossible. Treating a body as a
        // wall and treating it as free are both wrong, and both were measured:
        // as a wall, a holder standing in a single-file objective mouth blocks
        // its own reinforcement forever and the team fields one weight against
        // the opponent's two (0-6 in both straight-only arms); as free, the
        // planner walks into it and eats a Blocked outcome. A soft cost routes
        // around when there is a way around and queues when there is not.
        //
        // Routing is otherwise deliberately NEUTRAL about stance legality.
        // Preferring shield-legal ground here measured 365-494 ticks to win
        // against 170 for the neutral router over the same six games: two routes
        // within a tick of each other flip whenever the per-life lateral coin
        // does, and the body dithers instead of walking. Stations carry that
        // preference instead, where it is evaluated once.
        var bodies = new HashSet<Position>();
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
            bodies.Add(ally.Position);
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
            bodies.Add(enemy.Position);

        // A permanent lifecycle claim on a tile is published on the tile, and it
        // is not ours to walk through: a slot's authored return anchor is
        // reserved against its own team's children for the whole match. Read it
        // rather than discovering it as an endless Blocked outcome.
        var claimed = new HashSet<Position>();
        foreach (GenericActorContext.ObservedTile tile in context.VisibleTiles)
        {
            if (tile.SpawnReservation is not
                { Kind: GenericActorContext.SpawnReservationKind.AutomaticReturn }
                claim)
            {
                continue;
            }
            if (claim.TeamId != context.Self.ActorId.TeamId
                || claim.UnitId != context.Self.ActorId.UnitId)
            {
                claimed.Add(tile.Position);
            }
        }

        bool Passable(Position tile) =>
            !lens.IsWall(tile)
            && !claimed.Contains(tile)
            && !memory.Refused(tile);
        int Enter(Position tile) =>
            avoided.Contains(tile) ? 5 : bodies.Contains(tile) ? 6 : 1;

        int start = Key(lens, context.Self.Position, context.Self.Facing);
        cost[start] = 0;
        var frontier = new SortedSet<(int Cost, int State)> { (0, start) };
        while (frontier.Count > 0)
        {
            (int Cost, int State) current = frontier.Min;
            frontier.Remove(current);
            if (current.Cost > cost[current.State])
                continue;

            int packed = current.State;
            var facing = (Direction)(packed % 4);
            int cell = packed / 4;
            var tile = new Position(cell % width, cell / width);
            if (goals.Contains(tile) && packed != start)
                return firstStep[packed];

            void Relax(int nextState, int nextCost, Direction direction, bool spin)
            {
                if (nextCost >= cost[nextState])
                    return;
                cost[nextState] = nextCost;
                firstStep[nextState] = firstStep[packed]
                    ?? (direction, spin);
                frontier.Add((nextCost, nextState));
            }

            if (facingLocked)
            {
                (int dx, int dy) = facing.Vector();
                Position ahead = tile.Offset(dx, dy);
                if (Passable(ahead))
                {
                    Relax(
                        Key(lens, ahead, facing),
                        current.Cost + Enter(ahead),
                        facing,
                        false);
                }
                foreach (Direction candidate in order)
                {
                    if (candidate == facing)
                        continue;
                    Relax(
                        Key(lens, tile, candidate),
                        current.Cost + 1,
                        candidate,
                        true);
                }
                continue;
            }

            foreach (Direction candidate in order)
            {
                (int dx, int dy) = candidate.Vector();
                Position next = tile.Offset(dx, dy);
                if (!Passable(next))
                    continue;
                Relax(
                    Key(lens, next, candidate),
                    current.Cost + Enter(next),
                    candidate,
                    false);
            }
        }
        return null;
    }

    /// <summary>A rotation to an exact facing, when one is legal and needed.</summary>
    public static GenericActorDecision? Turn(
        StoneContract lens,
        GenericActorContext context,
        Direction facing,
        string reason)
    {
        if (context.Self.Facing == facing)
            return null;
        GenericActorActionLegality? rotate =
            Available(lens, context, isMove: false);
        return rotate is not null && Allows(rotate, facing)
            ? new GenericActorDecision(
                rotate.ActionId,
                rotate.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(facing)],
                reason)
            : null;
    }

    /// <summary>A step onto any tile that no inbound bolt reaches.</summary>
    public static GenericActorDecision? Sidestep(
        StoneContract lens,
        GenericActorContext context,
        StoneMemory memory,
        Position[] preferred)
    {
        GenericActorActionLegality? move = Available(lens, context, isMove: true);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = Directions(move);
        if (move is null || directions is null)
            return null;

        var occupied = new HashSet<Position>();
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
            occupied.Add(ally.Position);
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
            occupied.Add(enemy.Position);
        foreach (GenericActorContext.ObservedProjectile bolt
                 in context.VisibleProjectiles ?? [])
        {
            occupied.Add(bolt.Position);
        }

        Direction? best = null;
        int bestScore = int.MinValue;
        foreach (Direction direction
                 in ArenaBasics.OrderedDirections(lens.Raw, context))
        {
            if (!directions.AllowedValues.Contains(direction))
                continue;
            (int dx, int dy) = direction.Vector();
            Position tile = context.Self.Position.Offset(dx, dy);
            if (lens.IsWall(tile)
                || occupied.Contains(tile)
                || memory.Refused(tile))
            {
                continue;
            }
            int damage = DamageWithin(Inbound(lens, context, tile), 2);
            if (damage > 0)
                continue;
            int score = 0;
            foreach (Position want in preferred)
            {
                if (want == tile)
                {
                    score += 40;
                    break;
                }
            }
            // Retreating onto ground where the shield is legal turns one dodge
            // into a position: next tick the arc can go up instead of the body
            // running again.
            GenericActorRulesContract.FormTransition? guard =
                lens.GuardRoute(context.Self.FormId);
            if (guard is not null && lens.RouteAllowedOn(guard, tile))
                score += 18;
            score -= Clumping(context, tile) * 4;
            if (score > bestScore)
            {
                bestScore = score;
                best = direction;
            }
        }
        return best is Direction chosen
            ? new GenericActorDecision(
                move.ActionId,
                move.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(chosen)],
                $"stepping out of the lane toward {chosen}")
            : null;
    }

    private static int Key(StoneContract lens, Position tile, Direction facing) =>
        ((tile.Y * lens.Width) + tile.X) * 4 + (int)facing;

    private static GenericActorActionLegality? Available(
        StoneContract lens,
        GenericActorContext context,
        bool isMove)
    {
        foreach (GenericActorActionLegality legality in context.ActionLegalities)
        {
            if (!legality.Available)
                continue;
            if (isMove
                ? lens.IsMovement(legality.ActionId)
                : lens.IsRotation(legality.ActionId))
            {
                return legality;
            }
        }
        return null;
    }

    private static GenericActorActionLegality.ArgumentConstraint
        .DirectionConstraint? Directions(GenericActorActionLegality? legality)
    {
        if (legality is null)
            return null;
        foreach (GenericActorActionLegality.ArgumentConstraint constraint
                 in legality.Constraints)
        {
            if (constraint
                is GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint directions)
            {
                return directions;
            }
        }
        return null;
    }

    private static bool Allows(
        GenericActorActionLegality legality,
        Direction direction)
    {
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = Directions(legality);
        return directions is not null
            && directions.AllowedValues.Contains(direction);
    }
}
