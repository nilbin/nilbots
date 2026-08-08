using BotArena.Sdk;

/// <summary>
/// Ground truth about tiles: what blocks, what a hostile bolt will cross in the
/// next couple of advances, which tiles an enemy gun already covers, and how to
/// take one legal step toward a goal set. Allied projectiles are deliberately
/// not obstacles when the contract says they pass through allies - treating our
/// own bolt as a wall is what pins a body in a one-tile corridor.
/// </summary>
internal static class Field
{
    /// <summary>
    /// Every cardinal step the map physically allows. This is the geometry, not
    /// a preference: an absolute order here would be the same systematic
    /// side bias the template warns about, so every search takes its iteration
    /// order from <see cref="ArenaBasics.OrderedDirections"/> instead.
    /// </summary>
    public static readonly Direction[] Steps =
    [
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West,
    ];

    /// <summary>Breadth-first reachability with the first step that reaches each tile.</summary>
    public sealed class Reach
    {
        /// <summary>Step count from the origin for every reachable tile.</summary>
        public Dictionary<Position, int> Distance { get; } = [];
        /// <summary>First cardinal step of a shortest route to each tile.</summary>
        public Dictionary<Position, Direction> FirstStep { get; } = [];
    }

    /// <summary>Movement legality for this tick, or null when we cannot move.</summary>
    public static GenericActorActionLegality? MoveAction(
        MatchLens lens,
        GenericActorContext context)
    {
        HashSet<string> ids = lens.Contract.Rules.Actions
            .Where(action =>
                action.Kind == GenericActorRulesContract.ActionKind.Movement)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        return context.ActionLegalities
            .Where(action => action.Available && ids.Contains(action.ActionId))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>Rotation legality that can set an exact facing, or null.</summary>
    public static GenericActorActionLegality? RotateAction(
        MatchLens lens,
        GenericActorContext context,
        Direction facing)
    {
        HashSet<string> ids = lens.Contract.Rules.Actions
            .Where(action =>
                action.Kind == GenericActorRulesContract.ActionKind.Rotation)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        return context.ActionLegalities
            .Where(action =>
                action.Available
                && ids.Contains(action.ActionId)
                && action.Constraints
                    .OfType<GenericActorActionLegality.ArgumentConstraint
                        .DirectionConstraint>()
                    .Any(constraint =>
                        constraint.AllowedValues.Contains(facing)))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>Legal first steps declared by the movement legality mask.</summary>
    public static HashSet<Direction> LegalSteps(
        GenericActorActionLegality? move) =>
        move?.Constraints
            .OfType<GenericActorActionLegality.ArgumentConstraint
                .DirectionConstraint>()
            .FirstOrDefault()
            ?.AllowedValues
            .ToHashSet()
        ?? [];

    /// <summary>Tiles no body of ours can enter this tick.</summary>
    public static HashSet<Position> Blocked(
        MatchLens lens,
        GenericActorContext context)
    {
        var blocked = context.Allies
            .Select(ally => ally.Position)
            .Concat(context.Enemies.Select(enemy => enemy.Position))
            .ToHashSet();
        foreach (GenericActorContext.ObservedProjectile projectile
                 in context.VisibleProjectiles ?? [])
        {
            bool hostile = projectile.OwnerTeamId != lens.TeamId;
            if (hostile || !lens.AlliedProjectilesPassAllies)
                blocked.Add(projectile.Position);
        }

        if (context.Self.PreviousActionResolution
                is
                {
                    Outcome: GenericActorActionResolution.ActionOutcome.Blocked,
                } previous
            && previous.AcceptedAction.Arguments
                    .OfType<GenericActorActionArgument.DirectionArgument>()
                    .FirstOrDefault()
                is { } stuck)
        {
            (int dx, int dy) = stuck.Value.Vector();
            blocked.Add(context.Self.Position.Offset(dx, dy));
        }
        return blocked;
    }

    /// <summary>Tiles a visible hostile bolt is expected to cross soon.</summary>
    public static HashSet<Position> Threatened(
        MatchLens lens,
        GenericActorContext context,
        int advances)
    {
        var threatened = new HashSet<Position>();
        foreach (GenericActorContext.ObservedProjectile projectile
                 in context.VisibleProjectiles ?? [])
        {
            if (projectile.OwnerTeamId == lens.TeamId)
                continue;
            int reach = Math.Min(
                projectile.TilesPerAdvance * Math.Max(1, advances),
                projectile.RemainingTiles);
            (int dx, int dy) = projectile.Heading.Vector();
            Position cursor = projectile.Position;
            threatened.Add(cursor);
            for (int step = 0; step < reach; step++)
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
                threatened.Add(cursor);
            }
        }
        return threatened;
    }

    /// <summary>
    /// How many enemy guns already cover a tile. A facing-quadrant sensor is
    /// blind on roughly a third of our ticks, so remembered contacts count too
    /// - weighing only what we can currently see is how a body walks into the
    /// lane of the gun that just shot it.
    /// </summary>
    public static int Covered(
        MatchLens lens,
        GenericActorContext context,
        Position tile,
        IReadOnlyList<Position>? remembered = null)
    {
        int covered = 0;
        var counted = new HashSet<Position>();
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            GenericActorRulesContract.AttackProfile? profile =
                lens.Attack(enemy.FormId);
            int range = profile?.Projectile.MaxTravelTiles ?? 8;
            if (enemy.Position.ChebyshevDistance(tile) <= range
                && RayReaches(lens, enemy.Position, tile))
            {
                covered++;
            }
            counted.Add(enemy.Position);
        }
        if (remembered is null)
            return covered;

        int reach = lens.LongestEnemyReach;
        foreach (Position ghost in remembered)
        {
            if (!counted.Add(ghost))
                continue;
            if (ghost.ChebyshevDistance(tile) <= reach
                && RayReaches(lens, ghost, tile))
            {
                covered++;
            }
        }
        return covered;
    }

    /// <summary>Whether a straight eight-way bolt from one tile can reach another.</summary>
    public static bool RayReaches(MatchLens lens, Position from, Position to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        if (dx == 0 && dy == 0)
            return true;
        if (dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy))
            return false;

        int stepX = Math.Sign(dx);
        int stepY = Math.Sign(dy);
        Position cursor = from;
        while (cursor != to)
        {
            Position next = cursor.Offset(stepX, stepY);
            if (lens.IsWall(next))
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
    /// Breadth-first search over walkable tiles from a start position. The
    /// search order is supplied by the caller so equal-length routes break
    /// toward the advance direction and then on the per-life random stream,
    /// never on an absolute compass preference.
    /// </summary>
    public static Reach Explore(
        MatchLens lens,
        Position start,
        IReadOnlySet<Position> blocked,
        IReadOnlySet<Direction> allowedFirstSteps,
        Direction[] order)
    {
        var reach = new Reach();
        var queue = new Queue<Position>();
        foreach (Direction direction in order)
        {
            if (!allowedFirstSteps.Contains(direction))
                continue;
            (int dx, int dy) = direction.Vector();
            Position next = start.Offset(dx, dy);
            if (lens.IsWall(next)
                || blocked.Contains(next)
                || reach.Distance.ContainsKey(next))
            {
                continue;
            }
            reach.Distance[next] = 1;
            reach.FirstStep[next] = direction;
            queue.Enqueue(next);
        }

        while (queue.Count > 0)
        {
            Position current = queue.Dequeue();
            int distance = reach.Distance[current] + 1;
            Direction first = reach.FirstStep[current];
            foreach (Direction direction in order)
            {
                (int dx, int dy) = direction.Vector();
                Position next = current.Offset(dx, dy);
                if (lens.IsWall(next)
                    || blocked.Contains(next)
                    || reach.Distance.ContainsKey(next))
                {
                    continue;
                }
                reach.Distance[next] = distance;
                reach.FirstStep[next] = first;
                queue.Enqueue(next);
            }
        }
        return reach;
    }

    /// <summary>Builds a movement decision for one cardinal step.</summary>
    public static GenericActorDecision Step(
        GenericActorActionLegality move,
        Direction direction,
        string reason) =>
        new(
            move.ActionId,
            move.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(direction)],
            reason);
}
