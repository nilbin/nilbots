using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// Contract-driven movement and action helpers for the convoy. No action code,
/// map coordinate, cooldown, range, or legal target is reconstructed here: the
/// static catalog identifies the verb and each body's current mask supplies the
/// paired code and typed values.
/// </summary>
internal static class ArenaBasics
{
    private static readonly ProjectileHeading[] Headings =
        Enum.GetValues<ProjectileHeading>();

    internal sealed class Claims
    {
        private readonly HashSet<Position> _tiles = [];

        public IReadOnlySet<Position> Tiles => _tiles;

        public bool Reserve(Position position) => _tiles.Add(position);

        public static Claims ForTick(MindContext mind)
        {
            var claims = new Claims();
            foreach (MindBody body in mind.Bodies)
                claims.Reserve(body.Position);
            return claims;
        }
    }

    public static bool TryMoveToward(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        IReadOnlyCollection<Position> goals,
        Claims claims,
        string why)
    {
        if (goals.Count == 0 || goals.Contains(body.Position))
            return false;

        GenericActorActionLegality? move = ActionOfKind(
            contract,
            body,
            GenericActorRulesContract.ActionKind.Movement,
            requireAvailable: false);
        if (move is null)
            return false;

        HashSet<Position> blocked = BlockedTiles(contract, mind, claims, body);
        ProjectileHeading? first = FindFirstStep(
            contract.Map,
            body.Position,
            goals,
            blocked);
        if (first is not ProjectileHeading desired)
            return false;

        GenericActorActionLegality.ArgumentConstraint
            .ProjectileHeadingConstraint? movementHeadings = move.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        if (move.Available
            && movementHeadings is not null
            && movementHeadings.AllowedValues.Contains(desired))
        {
            (int dx, int dy) = desired.Vector();
            Position destination = body.Position.Offset(dx, dy);
            claims.Reserve(destination);
            body.Command(
                move.ActionId,
                move.ActionCode,
                [new GenericActorActionArgument.ProjectileHeadingArgument(
                    desired)],
                why);
            return true;
        }

        // Deliberate handling advertises only the current cardinal heading on
        // movement. Rotate through the contract action when the path asks for
        // another axis, then take the step on a later tick.
        Direction turn = CardinalFor(desired, body.Position, goals);
        GenericActorActionLegality? rotate = ActionOfKind(
            contract,
            body,
            GenericActorRulesContract.ActionKind.Rotation,
            requireAvailable: true);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = rotate?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        if (rotate is null
            || directions is null
            || body.Facing == turn
            || !directions.AllowedValues.Contains(turn))
        {
            return false;
        }

        body.Command(
            rotate.ActionId,
            rotate.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(turn)],
            $"turning to {why}");
        return true;
    }

    public static bool TryDodge(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        IReadOnlyCollection<Position> strategicGoals,
        Claims claims)
    {
        GenericActorContext.ObservedProjectile[] hostile =
            (mind.VisibleProjectiles ?? [])
                .Where(projectile =>
                    projectile.OwnerTeamId != body.ActorId.TeamId
                    && projectile.TicksUntilAdvance <= 1
                    && Reaches(projectile, body.Position))
                .ToArray();
        if (hostile.Length == 0)
            return false;

        GenericActorActionLegality? move = ActionOfKind(
            contract,
            body,
            GenericActorRulesContract.ActionKind.Movement,
            requireAvailable: true);
        GenericActorActionLegality.ArgumentConstraint
            .ProjectileHeadingConstraint? movementHeadings = move?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        if (move is null || movementHeadings is null)
            return false;

        HashSet<Position> blocked = BlockedTiles(contract, mind, claims, body);
        (ProjectileHeading Heading, Position Destination)? selected =
            movementHeadings.AllowedValues
                .Select(heading =>
                {
                    (int dx, int dy) = heading.Vector();
                    return (
                        Heading: heading,
                        Destination: body.Position.Offset(dx, dy));
                })
                .Where(candidate =>
                    CanEnter(
                        contract.Map,
                        body.Position,
                        candidate.Destination,
                        blocked)
                    && !hostile.Any(projectile =>
                        Reaches(projectile, candidate.Destination)))
                .OrderBy(candidate =>
                    Distance(candidate.Destination, strategicGoals))
                .ThenByDescending(candidate =>
                    hostile.Min(projectile =>
                        candidate.Destination.ChebyshevDistance(
                            projectile.Position)))
                .ThenBy(candidate => candidate.Heading)
                .Select(candidate =>
                    ((ProjectileHeading Heading, Position Destination)?)candidate)
                .FirstOrDefault();
        if (selected is not { } dodge)
            return false;

        claims.Reserve(dodge.Destination);
        body.Command(
            move.ActionId,
            move.ActionCode,
            [new GenericActorActionArgument.ProjectileHeadingArgument(
                dodge.Heading)],
            "formation-preserving dodge");
        return true;
    }

    public static bool TryShoot(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        GenericActorContext.ObservedEnemyState? preferred = null)
    {
        GenericActorActionLegality? attackAction = ActionOfKind(
            contract,
            body,
            GenericActorRulesContract.ActionKind.Attack,
            requireAvailable: true);
        GenericActorActionLegality.ArgumentConstraint
            .ProjectileHeadingConstraint? headings = attackAction?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Id, body.FormId,
                    StringComparison.Ordinal));
        GenericActorRulesContract.AttackProfile? attack =
            form?.AttackProfileId is string attackId
                ? contract.Rules.AttackProfiles.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, attackId,
                        StringComparison.Ordinal))
                : null;
        if (attackAction is null || headings is null || attack is null)
            return false;

        IEnumerable<GenericActorContext.ObservedEnemyState> targets =
            preferred is null
                ? mind.Enemies
                    .OrderBy(enemy => enemy.Health)
                    .ThenBy(enemy =>
                        body.Position.ChebyshevDistance(enemy.Position))
                    .ThenBy(enemy => enemy.ActorId)
                : mind.Enemies
                    .OrderByDescending(enemy => enemy.ActorId == preferred.ActorId)
                    .ThenBy(enemy => enemy.Health)
                    .ThenBy(enemy => enemy.ActorId);
        foreach (GenericActorContext.ObservedEnemyState enemy in targets)
        {
            if (!TryRay(body.Position, enemy.Position, out ProjectileHeading ray,
                    out int distance)
                || distance > attack.Projectile.MaxTravelTiles
                || !headings.AllowedValues.Contains(ray)
                || !ClearRay(contract.Map, body.Position, enemy.Position,
                    attack.Projectile.DiagonalCornersMustBeClear))
            {
                continue;
            }

            body.Command(
                attackAction.ActionId,
                attackAction.ActionCode,
                [new GenericActorActionArgument.ProjectileHeadingArgument(ray)],
                $"denying {enemy.ActorId}");
            return true;
        }
        return false;
    }

    public static bool TryHandoff(
        GenericActorResolvedMatchContract contract,
        MindBody source,
        MindBody receiver)
    {
        GenericActorActionLegality? action = ObjectiveAction(
            contract,
            source,
            GenericActorRulesContract.ActionParameterKind.UnitTarget);
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint>()
                .SingleOrDefault();
        GenericActorActionArgument.UnitTarget? target = targets?.AllowedValues
            .FirstOrDefault(candidate =>
                candidate.TeamId == receiver.ActorId.TeamId
                && candidate.UnitId == receiver.UnitId);
        if (action is null || targets is null || target is not { } legalTarget)
            return false;

        source.Command(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.UnitTargetArgument(legalTarget)],
            $"handoff to catch {receiver.UnitId}");
        return true;
    }

    public static bool CanHandoff(
        GenericActorResolvedMatchContract contract,
        MindBody source,
        MindBody receiver)
    {
        GenericActorActionLegality? action = ObjectiveAction(
            contract,
            source,
            GenericActorRulesContract.ActionParameterKind.UnitTarget);
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint>()
                .SingleOrDefault();
        return action is not null
            && targets is not null
            && targets.AllowedValues.Any(candidate =>
                candidate.TeamId == receiver.ActorId.TeamId
                && candidate.UnitId == receiver.UnitId);
    }

    public static bool TryArcToss(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Position reactor)
    {
        GenericActorActionLegality? action = SignatureAction(
            contract,
            body,
            "arc-toss");
        GenericActorActionLegality.ArgumentConstraint.PositionTargetConstraint?
            positions = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .PositionTargetConstraint>()
                .SingleOrDefault();
        Position? landing = positions?.AllowedValues
            .Where(position =>
                position.ChebyshevDistance(reactor)
                < body.Position.ChebyshevDistance(reactor))
            .OrderBy(position => position.ChebyshevDistance(reactor))
            .ThenBy(position => position.Y)
            .ThenBy(position => position.X)
            .Select(position => (Position?)position)
            .FirstOrDefault();
        if (action is null || landing is not Position target)
            return false;

        body.Command(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.PositionTargetArgument(target)],
            "emergency arc-toss toward reactor");
        return true;
    }

    public static bool TryPrismWall(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Position threat)
    {
        GenericActorActionLegality? action = SignatureAction(
            contract,
            body,
            "prism-wall");
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        Direction? selected = directions?.AllowedValues
            .OrderByDescending(direction =>
            {
                (int dx, int dy) = direction.Vector();
                return dx * (threat.X - body.Position.X)
                    + dy * (threat.Y - body.Position.Y);
            })
            .ThenBy(direction => direction)
            .Select(direction => (Direction?)direction)
            .FirstOrDefault();
        if (action is null || selected is not Direction direction)
            return false;

        body.Command(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(direction)],
            "raising convoy prism");
        return true;
    }

    public static bool TryNullField(
        GenericActorResolvedMatchContract contract,
        MindBody body)
    {
        GenericActorActionLegality? action = SignatureAction(
            contract,
            body,
            "null-field");
        if (action is null || !action.Constraints.IsEmpty)
            return false;
        body.Command(
            action.ActionId,
            action.ActionCode,
            [],
            "suppressing convoy contact");
        return true;
    }

    public static bool TryRepair(
        GenericActorResolvedMatchContract contract,
        MindBody repairer,
        IEnumerable<MindBody> priorities)
    {
        GenericActorActionLegality? action = SignatureAction(
            contract,
            repairer,
            "repair-beam");
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint>()
                .SingleOrDefault();
        if (action is null || targets is null)
            return false;

        HashSet<int> allowed = targets.AllowedValues
            .Where(target => target.TeamId == repairer.ActorId.TeamId)
            .Select(target => target.UnitId)
            .ToHashSet();
        MindBody? targetBody = priorities
            .Where(body => allowed.Contains(body.UnitId))
            .Where(body => body.Health < MaxHealth(contract, body))
            .OrderBy(body => body.Health * 100 / MaxHealth(contract, body))
            .ThenBy(body => body.UnitId)
            .FirstOrDefault();
        if (targetBody is null)
            return false;

        var target = new GenericActorActionArgument.UnitTarget(
            targetBody.ActorId.TeamId,
            targetBody.UnitId);
        repairer.Command(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.UnitTargetArgument(target)],
            $"repairing convoy {targetBody.UnitId}");
        return true;
    }

    public static bool TryTractor(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        GenericActorContext.ObservedEnemyState target)
    {
        GenericActorActionLegality? action = SignatureAction(
            contract,
            body,
            "tractor-hook");
        GenericActorActionLegality.ArgumentConstraint
            .ProjectileHeadingConstraint? headings = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        if (action is null
            || headings is null
            || !TryRay(body.Position, target.Position,
                out ProjectileHeading heading, out _)
            || !headings.AllowedValues.Contains(heading)
            || !ClearRay(contract.Map, body.Position, target.Position, true))
        {
            return false;
        }

        body.Command(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.ProjectileHeadingArgument(heading)],
            $"hooking carrier lane {target.ActorId}");
        return true;
    }

    public static bool TrySurveyFlare(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Position target)
    {
        GenericActorActionLegality? action = SignatureAction(
            contract,
            body,
            "survey-flare");
        GenericActorActionLegality.ArgumentConstraint.PositionTargetConstraint?
            positions = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .PositionTargetConstraint>()
                .SingleOrDefault();
        Position? selected = positions?.AllowedValues
            .OrderBy(position => position.ChebyshevDistance(target))
            .ThenBy(position => position.Y)
            .ThenBy(position => position.X)
            .Select(position => (Position?)position)
            .FirstOrDefault();
        if (action is null || selected is not Position positionTarget)
            return false;

        body.Command(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.PositionTargetArgument(
                positionTarget)],
            "surveying peripheral Well");
        return true;
    }

    public static GenericActorContext.ObservedEnemyState? EnemyCarrier(
        MindContext mind,
        int teamId)
    {
        if (mind.Mode is not GenericActorContext.ModeObservationState.ArcRelay arc)
            return null;
        HashSet<ActorIdentity> carriers = arc.VisibleCores
            .Where(core =>
                core.Disposition
                    == GenericActorContext.ArcRelayCoreDisposition.Carried
                && core.CarrierActorId is { TeamId: var owner }
                && owner != teamId)
            .Select(core => core.CarrierActorId!)
            .ToHashSet();
        return mind.Enemies
            .Where(enemy => carriers.Contains(enemy.ActorId))
            .OrderBy(enemy => enemy.Health)
            .ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault();
    }

    public static Position[] AdjacentToward(
        GenericActorMapContract map,
        Position anchor,
        Position goal)
    {
        return Headings
            .Select(heading =>
            {
                (int dx, int dy) = heading.Vector();
                return anchor.Offset(dx, dy);
            })
            .Where(position => IsFloor(map, position))
            .OrderBy(position => position.ChebyshevDistance(goal))
            .ThenBy(position => position.Y)
            .ThenBy(position => position.X)
            .ToArray();
    }

    public static Position[] ScreenTiles(
        GenericActorMapContract map,
        Position protectedPosition,
        Direction forward,
        bool upper)
    {
        (int fx, int fy) = forward.Vector();
        Direction side = upper ? forward.TurnedLeft() : forward.TurnedRight();
        (int sx, int sy) = side.Vector();
        Position[] candidates =
        [
            protectedPosition.Offset(fx + sx, fy + sy),
            protectedPosition.Offset(fx, fy),
            protectedPosition.Offset(sx, sy),
        ];
        return candidates.Where(position => IsFloor(map, position)).ToArray();
    }

    public static Position[] StageHomeward(
        GenericActorMapContract map,
        Position well,
        Position reactor) => AdjacentToward(map, well, reactor)
            .Take(3)
            .ToArray();

    public static int MaxHealth(
        GenericActorResolvedMatchContract contract,
        MindBody body) => contract.Rules.Forms
            .First(form => string.Equals(
                form.Id,
                body.FormId,
                StringComparison.Ordinal))
            .MaxHealth;

    public static bool IsFloor(GenericActorMapContract map, Position position) =>
        position.X >= 0
        && position.Y >= 0
        && position.X < map.Width
        && position.Y < map.Height
        && map.TileRows[position.Y][position.X] != '#';

    private static GenericActorActionLegality? ObjectiveAction(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        GenericActorRulesContract.ActionParameterKind parameterKind)
    {
        HashSet<string> ids = contract.Rules.Actions
            .Where(action =>
                action.Kind == GenericActorRulesContract.ActionKind.Objective
                && action.ParameterKinds.Contains(parameterKind))
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        return body.ActionLegalities
            .Where(action => action.Available && ids.Contains(action.ActionId))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static GenericActorActionLegality? SignatureAction(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind)
    {
        if (contract.Rules.GameMode
                is not GenericActorRulesContract.ArcRelayGameMode mode
            || body.ClassId is null)
        {
            return null;
        }
        string? actionId = mode.Signatures
            .Where(signature =>
                string.Equals(signature.Kind, signatureKind,
                    StringComparison.Ordinal)
                && string.Equals(signature.ClassId, body.ClassId,
                    StringComparison.Ordinal))
            .Select(signature => signature.ActionId)
            .FirstOrDefault();
        return actionId is null
            ? null
            : body.ActionLegalities.FirstOrDefault(action =>
                action.Available
                && string.Equals(action.ActionId, actionId,
                    StringComparison.Ordinal));
    }

    private static GenericActorActionLegality? ActionOfKind(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        GenericActorRulesContract.ActionKind kind,
        bool requireAvailable)
    {
        HashSet<string> actionIds = contract.Rules.Actions
            .Where(action => action.Kind == kind)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        return body.ActionLegalities
            .Where(action =>
                action.AllowedByForm
                && (!requireAvailable || action.Available)
                && actionIds.Contains(action.ActionId))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static HashSet<Position> BlockedTiles(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        Claims claims,
        MindBody body)
    {
        HashSet<Position> blocked = claims.Tiles.ToHashSet();
        foreach (GenericActorContext.ObservedEnemyState enemy in mind.Enemies)
            blocked.Add(enemy.Position);
        foreach (GenericActorContext.ObservedTile tile in mind.VisibleTiles)
        {
            if (tile.SpawnReservation is not null)
                blocked.Add(tile.Position);
        }
        if (contract.Rules.Collisions.ProjectilesBlockMovement)
        {
            foreach (GenericActorContext.ObservedProjectile projectile
                     in mind.VisibleProjectiles ?? [])
            {
                blocked.Add(projectile.Position);
            }
        }
        if (mind.Mode is GenericActorContext.ModeObservationState.ArcRelay arc)
        {
            foreach (GenericActorContext.ArcRelaySignatureState signature
                     in arc.VisibleSignatures.Where(signature =>
                         string.Equals(signature.Kind, "hardlight-block",
                             StringComparison.Ordinal)))
            {
                blocked.UnionWith(signature.Positions);
            }
        }
        blocked.Remove(body.Position);
        return blocked;
    }

    private static ProjectileHeading? FindFirstStep(
        GenericActorMapContract map,
        Position start,
        IReadOnlyCollection<Position> goals,
        HashSet<Position> blocked)
    {
        var queue = new Queue<Position>();
        var visited = new HashSet<Position> { start };
        var first = new Dictionary<Position, ProjectileHeading>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Position current = queue.Dequeue();
            ProjectileHeading[] ordered = Headings
                .OrderBy(heading =>
                {
                    (int dx, int dy) = heading.Vector();
                    return Distance(current.Offset(dx, dy), goals);
                })
                .ThenBy(heading => heading)
                .ToArray();
            foreach (ProjectileHeading heading in ordered)
            {
                (int dx, int dy) = heading.Vector();
                Position next = current.Offset(dx, dy);
                if (visited.Contains(next)
                    || !CanEnter(map, current, next, blocked))
                {
                    continue;
                }
                visited.Add(next);
                first[next] = current == start ? heading : first[current];
                if (goals.Contains(next))
                    return first[next];
                queue.Enqueue(next);
            }
        }
        return null;
    }

    private static bool CanEnter(
        GenericActorMapContract map,
        Position from,
        Position destination,
        HashSet<Position> blocked)
    {
        if (!IsFloor(map, destination) || blocked.Contains(destination))
            return false;
        int dx = destination.X - from.X;
        int dy = destination.Y - from.Y;
        return dx == 0
            || dy == 0
            || IsFloor(map, from.Offset(dx, 0))
                && IsFloor(map, from.Offset(0, dy));
    }

    private static bool Reaches(
        GenericActorContext.ObservedProjectile projectile,
        Position target)
    {
        Position cursor = projectile.Position;
        int steps = Math.Min(projectile.TilesPerAdvance,
            projectile.RemainingTiles);
        (int dx, int dy) = projectile.Heading.Vector();
        for (int i = 0; i < steps; i++)
        {
            cursor = cursor.Offset(dx, dy);
            if (cursor == target)
                return true;
        }
        return false;
    }

    private static int Distance(
        Position position,
        IReadOnlyCollection<Position> goals) =>
        goals.Count == 0
            ? 0
            : goals.Min(position.ChebyshevDistance);

    private static Direction CardinalFor(
        ProjectileHeading heading,
        Position from,
        IReadOnlyCollection<Position> goals)
    {
        if ((int)heading % 2 == 0)
            return (Direction)((int)heading / 2);
        Position goal = goals
            .OrderBy(from.ChebyshevDistance)
            .ThenBy(position => position.Y)
            .ThenBy(position => position.X)
            .First();
        int dx = goal.X - from.X;
        int dy = goal.Y - from.Y;
        if (Math.Abs(dx) >= Math.Abs(dy) && dx != 0)
            return dx > 0 ? Direction.East : Direction.West;
        return dy > 0 ? Direction.South : Direction.North;
    }

    private static bool TryRay(
        Position from,
        Position to,
        out ProjectileHeading heading,
        out int distance)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (distance == 0
            || dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy))
        {
            heading = default;
            return false;
        }
        int stepX = Math.Sign(dx);
        int stepY = Math.Sign(dy);
        heading = (stepX, stepY) switch
        {
            (0, -1) => ProjectileHeading.North,
            (1, -1) => ProjectileHeading.NorthEast,
            (1, 0) => ProjectileHeading.East,
            (1, 1) => ProjectileHeading.SouthEast,
            (0, 1) => ProjectileHeading.South,
            (-1, 1) => ProjectileHeading.SouthWest,
            (-1, 0) => ProjectileHeading.West,
            (-1, -1) => ProjectileHeading.NorthWest,
            _ => default,
        };
        return true;
    }

    private static bool ClearRay(
        GenericActorMapContract map,
        Position from,
        Position to,
        bool strictCorners)
    {
        int dx = Math.Sign(to.X - from.X);
        int dy = Math.Sign(to.Y - from.Y);
        Position cursor = from;
        while (cursor != to)
        {
            if (strictCorners
                && dx != 0
                && dy != 0
                && (!IsFloor(map, cursor.Offset(dx, 0))
                    || !IsFloor(map, cursor.Offset(0, dy))))
            {
                return false;
            }
            cursor = cursor.Offset(dx, dy);
            if (cursor != to && !IsFloor(map, cursor))
                return false;
        }
        return true;
    }
}
