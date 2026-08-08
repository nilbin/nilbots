using BotArena.Sdk;

/// <summary>
/// Wall-aware stepping toward a goal set. The first step is always drawn from
/// the movement action's own declared direction domain for this tick; the
/// search itself is a plain breadth-first walk over open tiles.
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
    /// Tiles another body occupies. Our own bolts are deliberately absent: the
    /// declared allied-contact policy passes them through without blocking, and
    /// treating them as obstacles makes a body walk around its own covering
    /// fire.
    /// </summary>
    public static HashSet<Position> Occupied(GenericActorContext context) =>
        context.Allies
            .Select(ally => ally.Position)
            .Concat(context.Enemies.Select(enemy => enemy.Position))
            .ToHashSet();

    /// <summary>
    /// One movement decision toward the nearest goal. Transient blockers are
    /// respected first; if they seal every route the search retries against
    /// walls alone, because bodies and bolts move.
    /// </summary>
    public static GenericActorDecision? Toward(
        ContractView view,
        GenericActorContext context,
        IReadOnlyCollection<Position> goals,
        IEnumerable<Position>? alsoAvoid,
        string reason)
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
        // never worth routing through, so both passes exclude them. Bodies and
        // the wider two-tick bolt corridor are dropped on the second pass: they
        // move, and a wall that will not step forward at all is worse than one
        // that takes a hit doing it.
        HashSet<Position> denied = [.. view.ReservedSpawnTiles];
        if (alsoAvoid is not null)
            denied.UnionWith(alsoAvoid);
        denied.UnionWith(Threat.BoltTiles(context));
        denied.UnionWith(Threat.Sweep(view, context, 1));
        denied.Remove(context.Self.Position);

        HashSet<Position> soft = [.. denied];
        soft.UnionWith(Occupied(context));
        soft.UnionWith(Threat.Sweep(view, context, 2));
        soft.Remove(context.Self.Position);

        HashSet<Position> goalSet = goals.ToHashSet();
        Direction? step =
            FirstStep(view, context.Self.Position, goalSet, soft, allowed)
            ?? FirstStep(view, context.Self.Position, goalSet, denied, allowed);
        if (step is not Direction direction)
            return null;

        return new GenericActorDecision(
            move.ActionId,
            move.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(direction)],
            reason);
    }

    public static Direction? FirstStep(
        ContractView view,
        Position start,
        IReadOnlySet<Position> goals,
        IReadOnlySet<Position> blocked,
        IReadOnlyList<Direction> allowedFirstSteps)
    {
        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Position, Direction First)>();
        foreach (Direction direction in Geometry.Cardinals)
        {
            if (!allowedFirstSteps.Contains(direction))
                continue;
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
            foreach (Direction direction in Geometry.Cardinals)
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

    /// <summary>Tiles reachable from a start tile ignoring transient bodies.</summary>
    public static HashSet<Position> Reachable(ContractView view, Position start)
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
                if (view.IsWall(next) || !visited.Add(next))
                    continue;
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
