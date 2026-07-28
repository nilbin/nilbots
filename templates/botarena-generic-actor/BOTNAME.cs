using BotArena.Sdk;

/// <summary>
/// A small contract-driven Frontline Labs starter. Nilbots creates one
/// independent instance for every active body life.
/// </summary>
public sealed class BOTNAME : IGenericActorBot
{
    private GenericActorResolvedMatchContract? _contract;
    private GenericActorResolvedMatchContract.FrontlineModeMapBinding?
        _frontline;

    public void StartLife(GenericActorMatchStart start)
    {
        _contract = start.Contract;
        _frontline =
            start.Contract.ModeMapBinding
                as GenericActorResolvedMatchContract
                    .FrontlineModeMapBinding
            ?? throw new InvalidOperationException(
                "This starter expects a Frontline mode binding.");
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        GenericActorResolvedMatchContract contract = _contract
            ?? throw new InvalidOperationException(
                "StartLife was not called.");

        if (context.Self.PendingSameLifeTransition is not null)
            return Choose(context, "wait", "finishing transition");

        GenericActorContext.ObservedEnemyState? nearestEnemy =
            context.Enemies
                .OrderBy(enemy =>
                    context.Self.Position.ChebyshevDistance(
                        enemy.Position))
                .FirstOrDefault();

        if (nearestEnemy is not null
            && context.Action("shoot-direction") is
                { Available: true })
        {
            return Choose(
                context,
                "shoot-direction",
                $"turret fire at {nearestEnemy.Position}",
                new GenericActorActionArgument
                    .ProjectileHeadingArgument(
                        Heading(
                            context.Self.Position,
                            nearestEnemy.Position)));
        }

        if (nearestEnemy is not null
            && IsAhead(
                context.Self.Position,
                nearestEnemy.Position,
                context.Self.Facing)
            && context.Action("shoot") is { Available: true })
        {
            return Choose(
                context,
                "shoot",
                $"firing at {nearestEnemy.Position}");
        }

        GenericActorContext.ModeObservationState.Frontline frontline =
            context.Mode
                as GenericActorContext.ModeObservationState.Frontline
            ?? throw new InvalidOperationException(
                "This starter expects Frontline observations.");
        string regionId =
            _frontline!.OrderedObjectiveRegionIds[
                frontline.ActivePositionIndex];
        GenericActorMapContract.Region objective =
            contract.Map.Regions.Single(region =>
                string.Equals(
                    region.RegionId,
                    regionId,
                    StringComparison.Ordinal));

        HashSet<Position> occupied = context.Allies
            .Select(ally => ally.Position)
            .Concat(context.Enemies.Select(enemy => enemy.Position))
            .ToHashSet();
        Direction? step = FindFirstStep(
            contract.Map,
            context.Self.Position,
            objective.Tiles.ToHashSet(),
            occupied);
        if (step is Direction direction)
        {
            return Choose(
                context,
                "move",
                $"advancing toward {regionId}",
                new GenericActorActionArgument
                    .DirectionArgument(direction));
        }

        return Choose(context, "wait", $"holding {regionId}");
    }

    /// <summary>
    /// Resolve the current action code from the legality catalog instead of
    /// copying numeric codes into bot logic. New rules may add actions or
    /// assign different codes without invalidating this helper.
    /// </summary>
    private static GenericActorDecision Choose(
        GenericActorContext context,
        string actionId,
        string debug,
        params GenericActorActionArgument[] arguments)
    {
        GenericActorActionLegality? selected = context.Action(actionId);
        if (selected is { Available: true })
        {
            return new GenericActorDecision(
                selected.ActionId,
                selected.ActionCode,
                arguments,
                debug);
        }

        GenericActorActionLegality wait = context.Action("wait")
            ?? throw new InvalidOperationException(
                $"Neither '{actionId}' nor 'wait' exists in the action catalog.");
        return GenericActorDecision.WithoutArguments(
            wait.ActionId,
            wait.ActionCode,
            $"{actionId} unavailable; waiting");
    }

    private static Direction? FindFirstStep(
        GenericActorMapContract map,
        Position start,
        IReadOnlySet<Position> goals,
        IReadOnlySet<Position> occupied)
    {
        if (goals.Contains(start))
            return null;

        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Position, Direction First)>();
        foreach (Direction direction in Enum.GetValues<Direction>())
        {
            Position next = Offset(start, direction);
            if (!CanEnter(map, next, occupied)
                || !visited.Add(next))
            {
                continue;
            }
            if (goals.Contains(next))
                return direction;
            queue.Enqueue((next, direction));
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (Direction direction in Enum.GetValues<Direction>())
            {
                Position next = Offset(current.Position, direction);
                if (!CanEnter(map, next, occupied)
                    || !visited.Add(next))
                {
                    continue;
                }
                if (goals.Contains(next))
                    return current.First;
                queue.Enqueue((next, current.First));
            }
        }
        return null;
    }

    private static bool CanEnter(
        GenericActorMapContract map,
        Position position,
        IReadOnlySet<Position> occupied) =>
        position.X >= 0
        && position.Y >= 0
        && position.X < map.Width
        && position.Y < map.Height
        && map.TileRows[position.Y][position.X] != '#'
        && !occupied.Contains(position);

    private static Position Offset(
        Position position,
        Direction direction)
    {
        var (dx, dy) = direction.Vector();
        return position.Offset(dx, dy);
    }

    private static bool IsAhead(
        Position from,
        Position target,
        Direction facing) =>
        facing switch
        {
            Direction.North =>
                target.X == from.X && target.Y < from.Y,
            Direction.East =>
                target.Y == from.Y && target.X > from.X,
            Direction.South =>
                target.X == from.X && target.Y > from.Y,
            Direction.West =>
                target.Y == from.Y && target.X < from.X,
            _ => false,
        };

    private static ProjectileHeading Heading(
        Position from,
        Position target)
    {
        int dx = Math.Sign(target.X - from.X);
        int dy = Math.Sign(target.Y - from.Y);
        return (dx, dy) switch
        {
            (0, -1) => ProjectileHeading.North,
            (1, -1) => ProjectileHeading.NorthEast,
            (1, 0) => ProjectileHeading.East,
            (1, 1) => ProjectileHeading.SouthEast,
            (0, 1) => ProjectileHeading.South,
            (-1, 1) => ProjectileHeading.SouthWest,
            (-1, 0) => ProjectileHeading.West,
            (-1, -1) => ProjectileHeading.NorthWest,
            _ => ProjectileHeading.North,
        };
    }
}
