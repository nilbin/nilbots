using BotArena.Sdk;

/// <summary>Contract-map routing and one-tick joint movement reservations.</summary>
internal static class Navigation
{
    private static readonly ProjectileHeading[] AllHeadings =
        Enum.GetValues<ProjectileHeading>();

    internal sealed class Claims
    {
        private readonly HashSet<Position> _tiles = [];

        public bool IsClaimed(Position position) => _tiles.Contains(position);
        public bool Reserve(Position position) => _tiles.Add(position);

        public static Claims ForTick(
            GenericActorResolvedMatchContract contract,
            MindContext mind,
            GenericActorContext.ModeObservationState.ArcRelay arc)
        {
            var claims = new Claims();
            foreach (MindBody body in mind.Bodies)
                claims.Reserve(body.Position);
            foreach (GenericActorContext.ObservedEnemyState enemy in mind.Enemies)
                claims.Reserve(enemy.Position);
            foreach (GenericActorContext.ObservedTile tile in mind.VisibleTiles)
            {
                if (tile.SpawnReservation is not null)
                    claims.Reserve(tile.Position);
            }
            foreach (GenericActorContext.ArcRelaySignatureState signature
                     in arc.VisibleSignatures.Where(signature =>
                         string.Equals(
                             signature.Kind,
                             "hardlight-block",
                             StringComparison.Ordinal)))
            {
                foreach (Position position in signature.Positions)
                    claims.Reserve(position);
            }
            return claims;
        }
    }

    public static bool TryMove(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        IReadOnlyCollection<Position> goals,
        Claims claims,
        Direction forward,
        bool preferFriendlyCover,
        string why)
    {
        if (goals.Count == 0 || goals.Contains(body.Position))
            return false;

        GenericActorActionLegality? move = body.ActionLegalities
            .Where(candidate => candidate.Available)
            .Join(
                contract.Rules.Actions.Where(definition =>
                    definition.Kind
                        == GenericActorRulesContract.ActionKind.Movement),
                candidate => candidate.ActionId,
                definition => definition.Id,
                (candidate, _) => candidate,
                StringComparer.Ordinal)
            .OrderBy(candidate => candidate.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (move is null)
            return false;

        HashSet<Position> blocked = BuildBlocked(
            contract,
            mind,
            arc,
            body,
            claims);
        GenericActorRulesContract.MovementFacingCoupling coupling =
            FacingCoupling(contract, body);
        ProjectileHeading[] routeHeadings = coupling
            == GenericActorRulesContract.MovementFacingCoupling.FacingLocked
                ? CardinalHeadings(forward)
                : RelativeHeadings(forward);
        ProjectileHeading? planned = FindFirstStep(
            contract,
            mind,
            arc,
            body.Position,
            goals,
            blocked,
            routeHeadings,
            forward,
            body.ActorId.TeamId,
            preferFriendlyCover);
        if (planned is not ProjectileHeading step)
            return false;

        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = move.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        if (headings is not null && headings.AllowedValues.Contains(step))
        {
            (int dx, int dy) = step.Vector();
            Position destination = body.Position.Offset(dx, dy);
            if (!claims.Reserve(destination))
                return false;
            body.Command(
                move.ActionId,
                move.ActionCode,
                [new GenericActorActionArgument.ProjectileHeadingArgument(step)],
                $"{why} via {step}");
            return true;
        }

        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = move.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        Direction? cardinalStep = ToDirection(step);
        if (directions is not null
            && cardinalStep is Direction movementDirection
            && directions.AllowedValues.Contains(movementDirection))
        {
            (int dx, int dy) = movementDirection.Vector();
            Position destination = body.Position.Offset(dx, dy);
            if (!claims.Reserve(destination))
                return false;
            body.Command(
                move.ActionId,
                move.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(movementDirection)],
                $"{why} via {movementDirection}");
            return true;
        }

        Direction? turnTo = cardinalStep ?? CardinalComponent(step, forward);
        return turnTo is Direction direction
            && TryRotate(contract, body, direction, why);
    }

    public static int RiskAt(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        Position position,
        int ownTeamId,
        bool preferFriendlyCover)
    {
        int risk = 0;
        foreach (GenericActorContext.ObservedEnemyState enemy in mind.Enemies)
        {
            int distance = position.ChebyshevDistance(enemy.Position);
            risk += distance switch
            {
                0 => 100,
                1 => 50,
                <= 3 => 18,
                <= 6 => 5,
                _ => 0,
            };
        }

        foreach (GenericActorContext.ArcRelaySignatureState signature
                 in arc.VisibleSignatures)
        {
            int distance = signature.Positions.IsEmpty
                ? position.ChebyshevDistance(signature.OwnerActorId.TeamId
                    == ownTeamId
                        ? position
                        : mind.Enemies.FirstOrDefault(enemy =>
                            enemy.ActorId == signature.OwnerActorId)?.Position
                            ?? position)
                : signature.Positions.Min(position.ChebyshevDistance);
            bool friendly = signature.OwnerTeamId == ownTeamId;
            if (friendly && preferFriendlyCover
                && (string.Equals(
                        signature.Kind,
                        "smoke-canister",
                        StringComparison.Ordinal)
                    || string.Equals(
                        signature.Kind,
                        "survey-flare",
                        StringComparison.Ordinal))
                && distance <= 3)
            {
                risk -= 3;
                continue;
            }
            if (friendly)
                continue;

            if (string.Equals(
                    signature.Kind,
                    "trip-node",
                    StringComparison.Ordinal))
            {
                risk += distance switch { 0 => 90, 1 => 28, <= 2 => 8, _ => 0 };
            }
            else if (string.Equals(
                         signature.Kind,
                         "sentinel-seed",
                         StringComparison.Ordinal)
                     && distance <= 4)
            {
                risk += 12;
            }
            else if (string.Equals(
                         signature.Kind,
                         "smoke-canister",
                         StringComparison.Ordinal)
                     && distance <= 2)
            {
                risk += 8;
            }
            else if (string.Equals(
                         signature.Kind,
                         "null-field",
                         StringComparison.Ordinal)
                     && distance <= 3)
            {
                risk += 6;
            }
        }
        return Math.Max(0, risk);
    }

    public static int RelativeTie(
        Position position,
        Position origin,
        Direction forward)
    {
        ProjectileHeading? heading = ExactHeading(origin, position);
        if (heading is null)
            return position.Y * 4096 + position.X;
        ProjectileHeading[] order = RelativeHeadings(forward);
        return Array.IndexOf(order, heading.Value);
    }

    public static ProjectileHeading? ExactHeading(Position from, Position to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        if (dx == 0 && dy == 0)
            return null;
        if (dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy))
            return null;
        int sx = Math.Sign(dx);
        int sy = Math.Sign(dy);
        return (sx, sy) switch
        {
            (0, -1) => ProjectileHeading.North,
            (1, -1) => ProjectileHeading.NorthEast,
            (1, 0) => ProjectileHeading.East,
            (1, 1) => ProjectileHeading.SouthEast,
            (0, 1) => ProjectileHeading.South,
            (-1, 1) => ProjectileHeading.SouthWest,
            (-1, 0) => ProjectileHeading.West,
            (-1, -1) => ProjectileHeading.NorthWest,
            _ => null,
        };
    }

    public static bool ClearShot(
        GenericActorMapContract map,
        Position from,
        Position to)
    {
        ProjectileHeading? heading = ExactHeading(from, to);
        if (heading is not ProjectileHeading direction)
            return false;
        (int dx, int dy) = direction.Vector();
        Position cursor = from.Offset(dx, dy);
        while (cursor != to)
        {
            if (!Walkable(map, cursor))
                return false;
            if (dx != 0 && dy != 0
                && (!Walkable(map, cursor.Offset(-dx, 0))
                    || !Walkable(map, cursor.Offset(0, -dy))))
            {
                return false;
            }
            cursor = cursor.Offset(dx, dy);
        }
        return Walkable(map, to);
    }

    public static IEnumerable<Position> AdjacentFloor(
        GenericActorMapContract map,
        Position centre) => AllHeadings
        .Select(heading =>
        {
            (int dx, int dy) = heading.Vector();
            return centre.Offset(dx, dy);
        })
        .Where(position => Walkable(map, position));

    private static ProjectileHeading? FindFirstStep(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        Position start,
        IReadOnlyCollection<Position> goals,
        HashSet<Position> blocked,
        IReadOnlyList<ProjectileHeading> headings,
        Direction forward,
        int ownTeamId,
        bool preferFriendlyCover)
    {
        HashSet<Position> goalSet = goals
            .Where(position => Walkable(contract.Map, position))
            .ToHashSet();
        if (goalSet.Count == 0)
            return null;

        var frontier = new PriorityQueue<RouteNode, RoutePriority>();
        Dictionary<Position, int> best = new() { [start] = 0 };
        int sequence = 0;
        frontier.Enqueue(
            new RouteNode(start, null, 0),
            new RoutePriority(0, 0, sequence++));

        while (frontier.TryDequeue(out RouteNode? node, out _))
        {
            if (node is null)
                continue;
            if (best.GetValueOrDefault(node.Position, int.MaxValue) != node.Cost)
                continue;
            if (goalSet.Contains(node.Position))
                return node.First;

            foreach (ProjectileHeading heading in headings)
            {
                (int dx, int dy) = heading.Vector();
                Position next = node.Position.Offset(dx, dy);
                if (!Walkable(contract.Map, next)
                    || blocked.Contains(next)
                    || IsBlockedDiagonal(contract.Map, node.Position, heading))
                {
                    continue;
                }

                int cost = node.Cost + 10 + RiskAt(
                    contract,
                    mind,
                    arc,
                    next,
                    ownTeamId,
                    preferFriendlyCover);
                if (cost >= best.GetValueOrDefault(next, int.MaxValue))
                    continue;
                best[next] = cost;
                ProjectileHeading first = node.First ?? heading;
                int estimate = goalSet.Min(next.ChebyshevDistance) * 10;
                frontier.Enqueue(
                    new RouteNode(next, first, cost),
                    new RoutePriority(
                        cost + estimate,
                        Array.IndexOf(RelativeHeadings(forward), first),
                        sequence++));
            }
        }
        return null;
    }

    private static HashSet<Position> BuildBlocked(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        Claims claims)
    {
        HashSet<Position> blocked = [];
        for (int y = 0; y < contract.Map.Height; y++)
        {
            for (int x = 0; x < contract.Map.Width; x++)
            {
                Position position = new(x, y);
                if (claims.IsClaimed(position))
                    blocked.Add(position);
            }
        }
        blocked.Remove(body.Position);

        foreach (GenericActorContext.ObservedProjectile projectile
                 in mind.VisibleProjectiles ?? [])
        {
            if (projectile.OwnerTeamId == body.ActorId.TeamId
                && contract.Rules.Collisions.AlliedProjectileContact.Contains(
                    "pass-through",
                    StringComparison.Ordinal))
            {
                continue;
            }
            blocked.Add(projectile.Position);
            if (projectile.TicksUntilAdvance != 1)
                continue;
            (int dx, int dy) = projectile.Heading.Vector();
            Position cursor = projectile.Position;
            int steps = Math.Min(
                projectile.TilesPerAdvance,
                projectile.RemainingTiles);
            for (int step = 0; step < steps; step++)
            {
                cursor = cursor.Offset(dx, dy);
                if (!Walkable(contract.Map, cursor))
                    break;
                blocked.Add(cursor);
            }
        }

        foreach (GenericActorContext.ArcRelaySignatureState signature
                 in arc.VisibleSignatures.Where(signature =>
                     signature.OwnerTeamId != body.ActorId.TeamId
                     && string.Equals(
                         signature.Kind,
                         "trip-node",
                         StringComparison.Ordinal)))
        {
            foreach (Position position in signature.Positions)
                blocked.Add(position);
        }
        return blocked;
    }

    private static bool TryRotate(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Direction direction,
        string why)
    {
        GenericActorActionLegality? rotate = body.ActionLegalities
            .Where(candidate => candidate.Available)
            .Join(
                contract.Rules.Actions.Where(definition =>
                    definition.Kind
                        == GenericActorRulesContract.ActionKind.Rotation),
                candidate => candidate.ActionId,
                definition => definition.Id,
                (candidate, _) => candidate,
                StringComparer.Ordinal)
            .OrderBy(candidate => candidate.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            constraint = rotate?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        if (rotate is null
            || constraint is null
            || !constraint.AllowedValues.Contains(direction)
            || body.Facing == direction)
        {
            return false;
        }
        body.Command(
            rotate.ActionId,
            rotate.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(direction)],
            $"turning {direction} to unlock {why}");
        return true;
    }

    private static GenericActorRulesContract.MovementFacingCoupling
        FacingCoupling(
            GenericActorResolvedMatchContract contract,
            MindBody body)
    {
        string? profileId = contract.Rules.Forms
            .FirstOrDefault(form =>
                string.Equals(form.Id, body.FormId, StringComparison.Ordinal))
            ?.MovementProfileId;
        return contract.Rules.MovementProfiles
            .FirstOrDefault(profile =>
                string.Equals(profile.Id, profileId, StringComparison.Ordinal))
            ?.FacingCoupling
            ?? GenericActorRulesContract.MovementFacingCoupling.PreserveFacing;
    }

    private static bool IsBlockedDiagonal(
        GenericActorMapContract map,
        Position origin,
        ProjectileHeading heading)
    {
        (int dx, int dy) = heading.Vector();
        return dx != 0 && dy != 0
            && (!Walkable(map, origin.Offset(dx, 0))
                || !Walkable(map, origin.Offset(0, dy)));
    }

    private static bool Walkable(GenericActorMapContract map, Position position) =>
        position.X >= 0
        && position.X < map.Width
        && position.Y >= 0
        && position.Y < map.Height
        && map.TileRows[position.Y][position.X] != '#';

    private static Direction? ToDirection(ProjectileHeading heading) => heading switch
    {
        ProjectileHeading.North => Direction.North,
        ProjectileHeading.East => Direction.East,
        ProjectileHeading.South => Direction.South,
        ProjectileHeading.West => Direction.West,
        _ => null,
    };

    private static Direction CardinalComponent(
        ProjectileHeading heading,
        Direction forward)
    {
        (int dx, int dy) = heading.Vector();
        (int fx, int fy) = forward.Vector();
        if (dx == fx && dx != 0)
            return forward;
        if (dy == fy && dy != 0)
            return forward;
        if (dx != 0)
            return dx > 0 ? Direction.East : Direction.West;
        return dy > 0 ? Direction.South : Direction.North;
    }

    private static ProjectileHeading[] CardinalHeadings(Direction forward) =>
        RelativeHeadings(forward)
            .Where(heading => (int)heading % 2 == 0)
            .ToArray();

    private static ProjectileHeading[] RelativeHeadings(Direction forward) =>
        forward switch
        {
            Direction.East =>
            [
                ProjectileHeading.East,
                ProjectileHeading.NorthEast,
                ProjectileHeading.SouthEast,
                ProjectileHeading.North,
                ProjectileHeading.South,
                ProjectileHeading.NorthWest,
                ProjectileHeading.SouthWest,
                ProjectileHeading.West,
            ],
            Direction.West =>
            [
                ProjectileHeading.West,
                ProjectileHeading.NorthWest,
                ProjectileHeading.SouthWest,
                ProjectileHeading.North,
                ProjectileHeading.South,
                ProjectileHeading.NorthEast,
                ProjectileHeading.SouthEast,
                ProjectileHeading.East,
            ],
            Direction.North =>
            [
                ProjectileHeading.North,
                ProjectileHeading.NorthWest,
                ProjectileHeading.NorthEast,
                ProjectileHeading.West,
                ProjectileHeading.East,
                ProjectileHeading.SouthWest,
                ProjectileHeading.SouthEast,
                ProjectileHeading.South,
            ],
            _ =>
            [
                ProjectileHeading.South,
                ProjectileHeading.SouthEast,
                ProjectileHeading.SouthWest,
                ProjectileHeading.East,
                ProjectileHeading.West,
                ProjectileHeading.NorthEast,
                ProjectileHeading.NorthWest,
                ProjectileHeading.North,
            ],
        };

    private sealed record RouteNode(
        Position Position,
        ProjectileHeading? First,
        int Cost);

    private readonly record struct RoutePriority(
        int Estimate,
        int RelativeDirection,
        int Sequence) : IComparable<RoutePriority>
    {
        public int CompareTo(RoutePriority other)
        {
            int estimate = Estimate.CompareTo(other.Estimate);
            if (estimate != 0)
                return estimate;
            int direction = RelativeDirection.CompareTo(other.RelativeDirection);
            return direction != 0 ? direction : Sequence.CompareTo(other.Sequence);
        }
    }
}
