using BotArena.Sdk;

/// <summary>
/// Wall-aware stepping toward a goal set. The first step is always drawn from
/// the movement action's own declared direction domain for this tick; the
/// search itself is a plain breadth-first walk over open tiles.
///
/// Two things here are contract reads rather than conventions. Search order is
/// <see cref="ArenaBasics.OrderedDirections"/> — an absolute N/E/S/W order is a
/// measured team-side bias on a mirror-symmetric map, because both teams share
/// it. And when the declared movement profile refuses the step we want, the
/// answer is a rotation, not a shrug: under a facing-locked arm the movement
/// mask offers exactly one direction, and a search that only ever starts there
/// walks a body into a corner for the rest of its life.
/// </summary>
internal static class Navigation
{
    public static GenericActorActionLegality? MoveAction(
        ContractView view,
        GenericActorContext context)
    {
        HashSet<string> moveIds =
            view.ActionIds(GenericActorRulesContract.ActionKind.Movement);
        return context.ActionLegalities
            .Where(action => action.Available && moveIds.Contains(action.ActionId))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public static GenericActorActionLegality? RotateAction(
        ContractView view,
        GenericActorContext context)
    {
        HashSet<string> rotateIds =
            view.ActionIds(GenericActorRulesContract.ActionKind.Rotation);
        return context.ActionLegalities
            .Where(action => action.Available && rotateIds.Contains(action.ActionId))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public static IReadOnlyList<Direction> AllowedDirections(
        GenericActorActionLegality action) =>
        action.Constraints
            .OfType<GenericActorActionLegality.ArgumentConstraint
                .DirectionConstraint>()
            .SingleOrDefault()
            ?.AllowedValues
            .ToArray()
        ?? [];

    /// <summary>
    /// R6. THE TEAM DRAW. Every tie-break in this bot — the first step of a
    /// route, a sibling's predicted route, an equally good emplacement site, the
    /// direction a yield picks — resolves through one shared direction order, and
    /// which stream that order is drawn from decides whether the wall agrees with
    /// itself.
    ///
    /// <para>Wave 6 drew it from <c>context.Random</c>, which is PER LIFE. Every
    /// coordination rule in <see cref="Column"/> is a pure function of the frozen
    /// shared observation, which is the only substrate a team without a channel
    /// has — and then it fed that function a stream each body draws differently,
    /// so a sibling's predicted route was computed with a different tie-break than
    /// the sibling actually uses. The wave-6 author noticed and worked around it
    /// by keeping <c>Column</c> away from the helper entirely. The team stream
    /// removes the workaround: draws are identical for every life on the team at
    /// the same tick, re-derived per tick so a body born mid-match agrees on its
    /// first tick, and unrelated to the opponent's stream so the order is still
    /// unpredictable from the other side of the board.</para>
    ///
    /// <para>Flip this to <see langword="false"/> to ablate: the order then comes
    /// from the per-life stream exactly as it did in wave 6.</para>
    /// </summary>
    public static bool TeamDraw => true;

    /// <summary>Mirror-fair direction order for this body's tie-breaks.</summary>
    public static Direction[] Order(
        ContractView view,
        GenericActorContext context) =>
        ArenaBasics.OrderedDirections(
            view.Contract,
            context,
            TeamDraw ? null : context.Random);

    /// <summary>
    /// Tiles another body occupies. Whether our own bolts belong here is the
    /// contract's joint answer across `projectilesBlockMovement`,
    /// `alliedProjectileContact` and the allied destination override — read it,
    /// because guessing wrong makes a body walk around its own covering fire.
    /// </summary>
    public static HashSet<Position> Occupied(
        ContractView view,
        GenericActorContext context)
    {
        HashSet<Position> tiles = context.Allies
            .Select(ally => ally.Position)
            .Concat(context.Enemies.Select(enemy => enemy.Position))
            .ToHashSet();
        if (view.AlliedBoltsBlockUs)
        {
            foreach (GenericActorContext.ObservedProjectile projectile
                     in context.VisibleProjectiles ?? [])
            {
                if (projectile.OwnerTeamId == context.Self.ActorId.TeamId)
                    tiles.Add(projectile.Position);
            }
        }
        return tiles;
    }

    /// <summary>
    /// One movement decision toward the nearest goal. Transient blockers are
    /// respected first; if they seal every route the search retries against
    /// walls alone, because bodies and bolts move. When the movement mask
    /// refuses the chosen first step but a rotation can unlock it, the rotation
    /// is the move.
    /// </summary>
    public static GenericActorDecision? Toward(
        ContractView view,
        GenericActorContext context,
        IReadOnlyCollection<Position> goals,
        IEnumerable<Position>? alsoAvoid,
        string reason,
        IEnumerable<Position>? softlyAvoid = null)
    {
        if (goals.Count == 0 || goals.Contains(context.Self.Position))
            return null;

        GenericActorActionLegality? move = MoveAction(view, context);
        if (move is null)
            return null;
        IReadOnlyList<Direction> allowed = AllowedDirections(move);
        if (allowed.Count == 0)
            return null;

        // Reserved deployment tiles and the tile a bolt sweeps this very tick are
        // never worth routing through, so both passes exclude them. Bodies, the
        // wider two-tick bolt corridor and any caller-supplied soft penalty are
        // dropped on the second pass: they move, and a wall that will not step
        // forward at all is worse than one that takes a hit doing it.
        HashSet<Position> denied = [.. view.ReservedSpawnTiles];
        if (alsoAvoid is not null)
            denied.UnionWith(alsoAvoid);
        denied.UnionWith(Threat.BoltTiles(context));
        denied.UnionWith(Threat.Sweep(view, context, 1));
        denied.Remove(context.Self.Position);

        HashSet<Position> soft = [.. denied];
        soft.UnionWith(Occupied(view, context));
        soft.UnionWith(Threat.Sweep(view, context, 2));
        if (softlyAvoid is not null)
            soft.UnionWith(softlyAvoid);
        soft.Remove(context.Self.Position);

        HashSet<Position> goalSet = goals.ToHashSet();
        Direction[] order = Order(view, context);
        Direction? step =
            FirstStep(view, context.Self.Position, goalSet, soft, order)
            ?? FirstStep(view, context.Self.Position, goalSet, denied, order);
        if (step is not Direction direction)
            return null;

        if (allowed.Contains(direction))
        {
            return new GenericActorDecision(
                move.ActionId,
                move.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(direction)],
                reason);
        }

        // The declared movement profile will not take this step from the pose we
        // hold. Turning into it is the legal way to take it next tick.
        return Face(view, context, direction, $"{reason} (turning into the step)");
    }

    /// <summary>
    /// Breadth-first first step over open tiles. Iteration order is the
    /// tie-break between equal-length routes, so callers pass a mirror-fair
    /// order rather than an absolute compass.
    /// </summary>
    public static Direction? FirstStep(
        ContractView view,
        Position start,
        IReadOnlySet<Position> goals,
        IReadOnlySet<Position> blocked,
        IReadOnlyList<Direction> order)
    {
        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Position, Direction First)>();
        foreach (Direction direction in order)
        {
            Position next = Geometry.Step(start, direction);
            if (view.IsWall(next) || blocked.Contains(next) || !visited.Add(next))
                continue;
            if (goals.Contains(next))
                return direction;
            queue.Enqueue((next, direction));
        }

        while (queue.Count > 0)
        {
            (Position position, Direction first) = queue.Dequeue();
            foreach (Direction direction in order)
            {
                Position next = Geometry.Step(position, direction);
                if (view.IsWall(next) || blocked.Contains(next) || !visited.Add(next))
                    continue;
                if (goals.Contains(next))
                    return first;
                queue.Enqueue((next, first));
            }
        }
        return null;
    }

    /// <summary>
    /// The first <paramref name="maxTiles"/> tiles of a shortest route from a
    /// start tile to the nearest goal, start excluded. Same walk and same
    /// tie-break order as <see cref="FirstStep"/>, so the tile it returns first
    /// is exactly the tile that search would step onto — which is what makes this
    /// usable as a PREDICTION of a sibling's route rather than merely as a plan
    /// for our own.
    ///
    /// <para>This exists because "do not stand where a sibling has to walk" needs
    /// the sibling's next two tiles, and the only thing every life on the team
    /// shares is the frozen observation: one body cannot ask another what it
    /// intends, so it re-derives the answer from the same inputs with the same
    /// code. An empty list means no route exists, which for a coordination rule is
    /// the same answer as "needs nothing from us".</para>
    /// </summary>
    public static List<Position> RouteTiles(
        ContractView view,
        Position start,
        IReadOnlySet<Position> goals,
        IReadOnlySet<Position> blocked,
        IReadOnlyList<Direction> order)
    {
        var route = new List<Position>();
        if (goals.Count == 0 || goals.Contains(start))
            return route;

        var parent = new Dictionary<Position, Position> { [start] = start };
        var queue = new Queue<Position>();
        queue.Enqueue(start);
        Position? found = null;
        while (queue.Count > 0 && found is null)
        {
            Position current = queue.Dequeue();
            foreach (Direction direction in order)
            {
                Position next = Geometry.Step(current, direction);
                if (view.IsWall(next)
                    || blocked.Contains(next)
                    || parent.ContainsKey(next))
                {
                    continue;
                }
                parent[next] = current;
                if (goals.Contains(next))
                {
                    found = next;
                    break;
                }
                queue.Enqueue(next);
            }
        }
        if (found is not Position goal)
            return route;

        // Walk the parent chain back to the start, then reverse: a route is only
        // useful to a yield rule in the order it will be walked.
        var reversed = new List<Position>();
        Position cursor = goal;
        int guard = 0;
        while (cursor != start && guard++ < 4096)
        {
            reversed.Add(cursor);
            cursor = parent[cursor];
        }
        for (int index = reversed.Count - 1; index >= 0; index--)
            route.Add(reversed[index]);
        return route;
    }

    /// <summary>
    /// The indivisible one-tile-corridor RUN a tile belongs to: itself plus every
    /// corridor tile reachable from it along the corridor axis. A run is the unit
    /// a march order has to allocate, because two bodies inside one run heading the
    /// same way are a jam no tie-break can fix — the follower cannot pass and the
    /// leader gains nothing from company. Empty when the tile is not a corridor.
    /// </summary>
    public static HashSet<Position> CorridorRun(ContractView view, Position tile)
    {
        HashSet<Position> run = [];
        if (!Geometry.IsCorridor(view.IsWall, tile))
            return run;
        run.Add(tile);
        var queue = new Queue<Position>();
        queue.Enqueue(tile);
        int guard = 0;
        while (queue.Count > 0 && guard++ < 64)
        {
            Position current = queue.Dequeue();
            foreach (Direction direction in Geometry.Cardinals)
            {
                Position next = Geometry.Step(current, direction);
                if (view.IsWall(next)
                    || !Geometry.IsCorridor(view.IsWall, next)
                    || !run.Add(next))
                {
                    continue;
                }
                queue.Enqueue(next);
            }
        }
        return run;
    }

    /// <summary>
    /// Tiles reachable from a start tile ignoring transient bodies.
    /// <paramref name="blocked"/> is the hypothetical: tiles treated as walls for
    /// this walk only, which is how the doctrine asks whether putting a body in a
    /// pinch would also seal its own side out of the ground it wants.
    /// </summary>
    public static HashSet<Position> Reachable(
        ContractView view,
        Position start,
        IReadOnlySet<Position>? blocked)
    {
        var visited = new HashSet<Position> { start };
        var queue = new Queue<Position>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            Position current = queue.Dequeue();
            foreach (Direction direction in Geometry.Cardinals)
            {
                Position next = Geometry.Step(current, direction);
                if (view.IsWall(next)
                    || blocked?.Contains(next) == true
                    || !visited.Add(next))
                {
                    continue;
                }
                queue.Enqueue(next);
            }
        }
        return visited;
    }

    public static GenericActorDecision? Face(
        ContractView view,
        GenericActorContext context,
        Direction direction,
        string reason)
    {
        if (context.Self.Facing == direction)
            return null;
        GenericActorActionLegality? rotate = RotateAction(view, context);
        if (rotate is null || !AllowedDirections(rotate).Contains(direction))
            return null;
        return new GenericActorDecision(
            rotate.ActionId,
            rotate.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(direction)],
            reason);
    }

    /// <summary>
    /// One legal step in an exact direction. When the movement mask refuses it,
    /// the rotation that would make it legal is submitted instead.
    /// </summary>
    public static GenericActorDecision? Step(
        ContractView view,
        GenericActorContext context,
        Direction direction,
        string reason)
    {
        GenericActorActionLegality? move = MoveAction(view, context);
        if (move is null)
            return null;
        if (!AllowedDirections(move).Contains(direction))
        {
            return Face(
                view,
                context,
                direction,
                $"{reason} (turning into the step)");
        }
        return new GenericActorDecision(
            move.ActionId,
            move.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(direction)],
            reason);
    }

    /// <summary>The cardinal direction that best points from one tile at another.</summary>
    public static Direction Toward(Position from, Position to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        if (Math.Abs(dx) >= Math.Abs(dy))
            return dx >= 0 ? Direction.East : Direction.West;
        return dy >= 0 ? Direction.South : Direction.North;
    }
}
