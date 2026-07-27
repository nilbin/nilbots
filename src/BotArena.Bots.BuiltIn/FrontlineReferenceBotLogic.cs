using BotArena.Sdk;

namespace BotArena.Bots.BuiltIn;

/// <summary>
/// Small deterministic navigation and action helpers shared by the reference
/// Frontline policies. They reason only from the public SDK contract.
/// </summary>
internal static class FrontlineReferenceBotLogic
{
    private static readonly Direction[] CardinalDirections =
    [
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West,
    ];

    public static ActorDecision? TryFabricate(
        ActorContext context,
        int? onlyUnitId = null)
    {
        ObservedActionAvailability? action =
            context.Action(ActorActionIds.Fabricate);
        if (action is not
            {
                Available: true,
                AllowedUnitTargets: { Length: > 0 } targets,
            })
        {
            return null;
        }

        foreach (ObservedUnitTarget target in targets)
        {
            if (onlyUnitId is null || target.UnitId == onlyUnitId)
                return Actions.Fabricate(target);
        }
        return null;
    }

    public static bool HasInactiveChild(
        ActorMatchStart start,
        ActorContext context)
    {
        PublicFrontlineDefinition? frontline =
            start.Contract.Rules.Frontline;
        return frontline is not null
            && context.Self.ActorId.UnitId
                == frontline.Fabrication.FabricatorUnitId
            && context.TeamUnits.Any(unit =>
                unit.UnitId != frontline.Fabrication.FabricatorUnitId
                && unit.LifecycleStatus != FrontlineLifecycleStatus.Active);
    }

    public static bool IsFabricator(
        ActorMatchStart start,
        ActorContext context) =>
        start.Contract.Rules.Frontline is { } frontline
        && context.Self.ActorId.UnitId
            == frontline.Fabrication.FabricatorUnitId;

    public static ActorDecision? TryAttack(
        ActorMatchStart start,
        ActorContext context)
    {
        if (string.Equals(
                context.Self.FormId,
                start.Contract.Rules.Frontline?.TurretFire.FormId,
                StringComparison.Ordinal))
        {
            return TryTurretShot(start, context);
        }

        ObservedActionAvailability? shoot =
            context.Action(ActorActionIds.Shoot);
        if (shoot is not { Available: true })
            return null;

        foreach (ObservedEnemy enemy in OrderedEnemies(context))
        {
            if (!TryAlignedHeading(
                    context.Self.Position,
                    enemy.Position,
                    out ProjectileHeading heading)
                || !HasClearLine(
                    start.Contract.Map,
                    context.Self.Position,
                    enemy.Position,
                    heading))
            {
                continue;
            }

            int offset = SignedHeadingOffset(
                context.Self.Facing.ToProjectileHeading(),
                heading);
            if (offset == 0)
                return Actions.Shoot();
            if (shoot.ShotProgramAvailable == true
                && offset >= start.Contract.Rules.ShotPrograms
                    .MinInitialAimOctants
                && offset <= start.Contract.Rules.ShotPrograms
                    .MaxInitialAimOctants)
            {
                return Actions.Shoot(new ShotProgram(
                    offset,
                    BendDirection: 0,
                    BendAfterTiles: 0,
                    BendEveryTiles: 1,
                    BendCount: 0));
            }
        }

        return null;
    }

    public static ActorDecision? TryTurretShot(
        ActorMatchStart start,
        ActorContext context)
    {
        ObservedActionAvailability? action =
            context.Action(ActorActionIds.ShootDirection);
        if (action is not
            {
                Available: true,
                AllowedProjectileHeadings: { Length: > 0 } headings,
            })
        {
            return null;
        }

        ObservedEnemy? enemy = OrderedEnemies(context).FirstOrDefault();
        ProjectileHeading desired = enemy is null
            ? OwnHome(start, context.Self.ActorId.TeamId)
                .PrimeSpawnFacing
                .ToProjectileHeading()
            : HeadingToward(context.Self.Position, enemy.Position);
        ProjectileHeading selected = headings.Contains(desired)
            ? desired
            : headings[0];
        return Actions.ShootDirection(selected);
    }

    public static ActorDecision MoveToActiveObjective(
        ActorMatchStart start,
        ActorContext context) =>
        MoveToward(start, context, ObjectiveTargets(start, context));

    public static ActorDecision MoveToDefensiveLine(
        ActorMatchStart start,
        ActorContext context) =>
        MoveToward(start, context, DefensiveTargets(start, context));

    public static ActorDecision MoveToHomePad(
        ActorMatchStart start,
        ActorContext context) =>
        MoveToward(
            start,
            context,
            OwnHome(start, context.Self.ActorId.TeamId).ProtectedSpawnPad);

    public static ActorDecision MoveToVisibleEnemy(
        ActorMatchStart start,
        ActorContext context)
    {
        HashSet<Position> targets = [];
        foreach (ObservedEnemy enemy in OrderedEnemies(context))
        {
            foreach (Direction direction in CardinalDirections)
            {
                var (dx, dy) = direction.Vector();
                Position adjacent = enemy.Position.Offset(dx, dy);
                if (IsFloor(start.Contract.Map, adjacent))
                    targets.Add(adjacent);
            }
        }
        return MoveToward(start, context, targets);
    }

    public static ActorDecision MoveToAnchorSite(
        ActorMatchStart start,
        ActorContext context) =>
        MoveToward(start, context, AnchorTargets(start, context));

    public static ActorDecision? TryAnchor(
        ActorMatchStart start,
        ActorContext context)
    {
        PublicFrontlineAnchorDefinition? anchor =
            start.Contract.Rules.Frontline?.Anchor;
        ObservedActionAvailability? transform =
            context.Action(ActorActionIds.Transform);
        return anchor is not null
            && string.Equals(
                context.Self.FormId,
                anchor.SourceFormId,
                StringComparison.Ordinal)
            && transform is
            {
                Available: true,
                AllowedFormTargets: { Length: > 0 } forms,
            }
            && forms.Contains(anchor.TargetFormId)
                ? Actions.Transform(anchor.TargetFormId)
                : null;
    }

    private static ActorDecision MoveToward(
        ActorMatchStart start,
        ActorContext context,
        IEnumerable<Position> requestedTargets)
    {
        HashSet<Position> targets = requestedTargets
            .Where(position => IsFloor(start.Contract.Map, position))
            .ToHashSet();
        if (targets.Contains(context.Self.Position))
            return Actions.Wait();

        HashSet<Position> occupied = context.Allies
            .Select(ally => ally.Position)
            .Concat(context.Enemies.Select(enemy => enemy.Position))
            .ToHashSet();
        targets.ExceptWith(occupied);
        Position? next = FindNextStep(
            start.Contract.Map,
            context.Self.Position,
            targets,
            occupied);
        if (next is null)
            return TurnRightOrWait(context);

        Direction desired = DirectionBetween(
            context.Self.Position,
            next.Value);
        if (desired == context.Self.Facing)
        {
            return IsAvailable(context, ActorActionIds.MoveForward)
                ? Actions.MoveForward()
                : Actions.Wait();
        }

        int turn = (((int)desired - (int)context.Self.Facing) % 4 + 4) % 4;
        if (turn == 3 && IsAvailable(context, ActorActionIds.TurnLeft))
            return Actions.TurnLeft();
        return IsAvailable(context, ActorActionIds.TurnRight)
            ? Actions.TurnRight()
            : Actions.Wait();
    }

    private static IReadOnlyList<Position> ObjectiveTargets(
        ActorMatchStart start,
        ActorContext context)
    {
        PublicFrontlineMapDefinition? map = start.Contract.Map.Frontline;
        if (map is null || map.Positions.Length == 0)
            return start.Contract.Map.ObjectiveTiles;
        int index = Math.Clamp(
            context.FrontlineObjective?.ActivePositionIndex
                ?? map.Positions.Length / 2,
            0,
            map.Positions.Length - 1);
        return map.Positions.Single(position =>
            position.PositionIndex == index).Tiles;
    }

    private static IReadOnlyList<Position> DefensiveTargets(
        ActorMatchStart start,
        ActorContext context)
    {
        PublicFrontlineMapDefinition? map = start.Contract.Map.Frontline;
        PublicFrontlineDefinition? rules = start.Contract.Rules.Frontline;
        if (map is null || rules is null || map.Positions.Length == 0)
            return ObjectiveTargets(start, context);

        int active = Math.Clamp(
            context.FrontlineObjective?.ActivePositionIndex
                ?? map.Positions.Length / 2,
            0,
            map.Positions.Length - 1);
        int advance = rules.Victory.TeamAdvances
            .Single(item => item.TeamId == context.Self.ActorId.TeamId)
            .PositionIndexDelta;
        int defensive = Math.Clamp(active - advance, 0, map.Positions.Length - 1);
        return map.Positions.Single(position =>
            position.PositionIndex == defensive).Tiles;
    }

    private static IReadOnlyList<Position> AnchorTargets(
        ActorMatchStart start,
        ActorContext context)
    {
        PublicFrontlineMapDefinition? frontline = start.Contract.Map.Frontline;
        PublicFrontlineDefinition? rules = start.Contract.Rules.Frontline;
        if (frontline is null || rules is null)
            return [];

        HashSet<Position> forbidden =
            frontline.AnchorForbiddenTiles.ToHashSet();
        HashSet<Position> objectiveTiles = frontline.Positions
            .SelectMany(position => position.Tiles)
            .ToHashSet();
        PublicFrontlineTeamHome home =
            OwnHome(start, context.Self.ActorId.TeamId);
        PublicUnitSlot[] childSlots = start.Contract.Topology.UnitSlots
            .Where(slot =>
                slot.TeamId == context.Self.ActorId.TeamId
                && slot.UnitId != rules.Fabrication.FabricatorUnitId)
            .OrderBy(slot => slot.UnitId)
            .ToArray();
        int rank = Math.Max(
            0,
            Array.FindIndex(childSlots, slot =>
                slot.UnitId == context.Self.ActorId.UnitId));
        int stride = Math.Max(1, childSlots.Length);

        Position[] candidates = AllFloorTiles(start.Contract.Map)
            .Where(position =>
                !forbidden.Contains(position)
                && !objectiveTiles.Contains(position))
            .OrderBy(position =>
                position.ChebyshevDistance(home.PrimeSpawnPosition))
            .ThenBy(position => position.Y)
            .ThenBy(position => position.X)
            .ToArray();
        return candidates
            .Where((_, index) => index % stride == rank)
            .ToArray();
    }

    private static Position? FindNextStep(
        PublicMapManifest map,
        Position origin,
        ISet<Position> targets,
        ISet<Position> occupied)
    {
        if (targets.Count == 0)
            return null;

        var queue = new Queue<Position>();
        var previous = new Dictionary<Position, Position?>();
        queue.Enqueue(origin);
        previous.Add(origin, null);
        Position? reached = null;
        while (queue.Count > 0)
        {
            Position current = queue.Dequeue();
            if (targets.Contains(current))
            {
                reached = current;
                break;
            }

            foreach (Direction direction in CardinalDirections)
            {
                var (dx, dy) = direction.Vector();
                Position next = current.Offset(dx, dy);
                if (previous.ContainsKey(next)
                    || !IsFloor(map, next)
                    || occupied.Contains(next))
                {
                    continue;
                }
                previous.Add(next, current);
                queue.Enqueue(next);
            }
        }

        if (reached is null)
            return null;
        Position step = reached.Value;
        while (previous[step] is { } prior && prior != origin)
            step = prior;
        return step;
    }

    private static IEnumerable<Position> AllFloorTiles(PublicMapManifest map)
    {
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var position = new Position(x, y);
                if (IsFloor(map, position))
                    yield return position;
            }
        }
    }

    private static bool IsFloor(PublicMapManifest map, Position position) =>
        position.X >= 0
        && position.Y >= 0
        && position.X < map.Width
        && position.Y < map.Height
        && map.TileRows[position.Y][position.X] != '#';

    private static bool IsAvailable(ActorContext context, string actionId) =>
        context.Action(actionId) is { Available: true };

    private static ActorDecision TurnRightOrWait(ActorContext context) =>
        IsAvailable(context, ActorActionIds.TurnRight)
            ? Actions.TurnRight()
            : Actions.Wait();

    private static Direction DirectionBetween(Position from, Position to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        return (dx, dy) switch
        {
            (0, -1) => Direction.North,
            (1, 0) => Direction.East,
            (0, 1) => Direction.South,
            (-1, 0) => Direction.West,
            _ => throw new InvalidOperationException(
                "Pathfinding produced a non-cardinal step."),
        };
    }

    private static IEnumerable<ObservedEnemy> OrderedEnemies(
        ActorContext context) =>
        context.Enemies
            .OrderBy(enemy =>
                context.Self.Position.ChebyshevDistance(enemy.Position))
            .ThenBy(enemy => enemy.Actor.TeamId)
            .ThenBy(enemy => enemy.Actor.UnitId)
            .ThenBy(enemy => enemy.Actor.LifeHandle, StringComparer.Ordinal);

    private static bool TryAlignedHeading(
        Position from,
        Position to,
        out ProjectileHeading heading)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        if (dx == 0 && dy < 0)
            heading = ProjectileHeading.North;
        else if (dx > 0 && dy < 0 && dx == -dy)
            heading = ProjectileHeading.NorthEast;
        else if (dx > 0 && dy == 0)
            heading = ProjectileHeading.East;
        else if (dx > 0 && dy > 0 && dx == dy)
            heading = ProjectileHeading.SouthEast;
        else if (dx == 0 && dy > 0)
            heading = ProjectileHeading.South;
        else if (dx < 0 && dy > 0 && -dx == dy)
            heading = ProjectileHeading.SouthWest;
        else if (dx < 0 && dy == 0)
            heading = ProjectileHeading.West;
        else if (dx < 0 && dy < 0 && dx == dy)
            heading = ProjectileHeading.NorthWest;
        else
        {
            heading = default;
            return false;
        }
        return true;
    }

    private static ProjectileHeading HeadingToward(Position from, Position to)
    {
        int dx = Math.Sign(to.X - from.X);
        int dy = Math.Sign(to.Y - from.Y);
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

    private static bool HasClearLine(
        PublicMapManifest map,
        Position from,
        Position to,
        ProjectileHeading heading)
    {
        var (dx, dy) = heading.Vector();
        Position current = from;
        while (current != to)
        {
            Position next = current.Offset(dx, dy);
            if (!IsFloor(map, next))
                return false;
            if (dx != 0 && dy != 0
                && (!IsFloor(map, current.Offset(dx, 0))
                    || !IsFloor(map, current.Offset(0, dy))))
            {
                return false;
            }
            current = next;
        }
        return true;
    }

    private static int SignedHeadingOffset(
        ProjectileHeading from,
        ProjectileHeading to)
    {
        int clockwise = ((int)to - (int)from + 8) % 8;
        return clockwise <= 4 ? clockwise : clockwise - 8;
    }

    private static PublicFrontlineTeamHome OwnHome(
        ActorMatchStart start,
        int teamId) =>
        start.Contract.Map.Frontline?.TeamHomes.Single(home =>
            home.TeamId == teamId)
        ?? throw new InvalidOperationException(
            "Frontline reference bots require Frontline team homes.");
}
