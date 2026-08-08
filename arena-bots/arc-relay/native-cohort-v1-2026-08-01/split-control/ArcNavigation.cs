using BotArena.Sdk;

/// <summary>Contract-driven action selection and collision-aware routing.</summary>
internal static class ArcNavigation
{
    private static readonly ProjectileHeading[] EightWays =
        Enum.GetValues<ProjectileHeading>();
    private static readonly ProjectileHeading[] FourWays =
    [
        ProjectileHeading.North,
        ProjectileHeading.East,
        ProjectileHeading.South,
        ProjectileHeading.West,
    ];

    internal sealed class Traffic
    {
        private readonly HashSet<Position> _claimed;

        private Traffic(HashSet<Position> claimed) => _claimed = claimed;

        public IReadOnlySet<Position> Claimed => _claimed;

        public bool Reserve(Position position) => _claimed.Add(position);

        public static Traffic ForTick(
            GenericActorResolvedMatchContract contract,
            MindContext mind)
        {
            var claimed = mind.Bodies
                .Select(body => body.Position)
                .ToHashSet();
            foreach (GenericActorContext.ObservedAllyState ally in mind.Allies)
                claimed.Add(ally.Position);
            foreach (GenericActorContext.ObservedEnemyState enemy in mind.Enemies)
                claimed.Add(enemy.Position);
            foreach (GenericActorContext.ObservedTile tile in mind.VisibleTiles)
            {
                if (tile.SpawnReservation is not null)
                    claimed.Add(tile.Position);
            }
            foreach (GenericActorContext.ObservedProjectile projectile
                     in mind.VisibleProjectiles ?? [])
            {
                if (projectile.OwnerTeamId
                        != mind.Bodies.FirstOrDefault()?.ActorId.TeamId
                    || !contract.Rules.Collisions.AlliedProjectileContact
                        .Contains("pass-through", StringComparison.Ordinal))
                {
                    claimed.Add(projectile.Position);
                }
            }
            if (mind.Mode is GenericActorContext.ModeObservationState.ArcRelay arc)
            {
                foreach (GenericActorContext.ArcRelaySignatureState signature
                         in arc.VisibleSignatures.Where(BlocksGround))
                {
                    claimed.UnionWith(signature.Positions);
                }
            }
            return new Traffic(claimed);
        }
    }

    public static bool TryMove(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        IReadOnlyCollection<Position> goals,
        Traffic traffic,
        Direction forward,
        string why)
    {
        if (goals.Count == 0 || goals.Contains(body.Position))
            return false;

        GenericActorActionLegality? move = Available(
            contract,
            body,
            GenericActorRulesContract.ActionKind.Movement);
        if (move is null)
            return false;
        var constraint = move.Constraints
            .OfType<GenericActorActionLegality.ArgumentConstraint
                .ProjectileHeadingConstraint>()
            .SingleOrDefault();
        if (constraint is null)
            return false;

        GenericActorRulesContract.MovementFacingCoupling coupling =
            MovementCoupling(contract, body);
        IReadOnlyCollection<ProjectileHeading> routeHeadings =
            coupling == GenericActorRulesContract.MovementFacingCoupling.FacingLocked
                ? FourWays
                : EightWays;
        HashSet<Position> blocked = BlockedFirstStep(
            contract,
            mind,
            body,
            traffic);
        ProjectileHeading? step = FindFirstStep(
            contract.Map,
            body.Position,
            goals,
            blocked,
            routeHeadings,
            forward);
        if (step is not ProjectileHeading heading)
            return false;

        if (!constraint.AllowedValues.Contains(heading))
        {
            Direction? turn = Cardinal(heading);
            GenericActorActionLegality? rotate = Available(
                contract,
                body,
                GenericActorRulesContract.ActionKind.Rotation);
            var directions = rotate?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
            if (rotate is null
                || turn is not Direction direction
                || directions?.AllowedValues.Contains(direction) != true
                || direction == body.Facing)
            {
                return false;
            }
            body.Command(
                rotate,
                new GenericActorActionArgument.DirectionArgument(direction));
            return true;
        }

        (int dx, int dy) = heading.Vector();
        Position destination = body.Position.Offset(dx, dy);
        if (!traffic.Reserve(destination))
            return false;
        body.Command(
            move.ActionId,
            move.ActionCode,
            [new GenericActorActionArgument.ProjectileHeadingArgument(heading)],
            $"{why} via {heading}");
        return true;
    }

    public static bool TryShoot(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        GenericActorContext.ObservedEnemyState? target)
    {
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Id, body.FormId, StringComparison.Ordinal));
        GenericActorRulesContract.AttackProfile? attack =
            form?.AttackProfileId is string profileId
                ? contract.Rules.AttackProfiles.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, profileId, StringComparison.Ordinal))
                : null;
        if (attack is null || mind.Enemies.IsEmpty)
            return false;

        GenericActorActionLegality? action = Available(
            contract,
            body,
            GenericActorRulesContract.ActionKind.Attack);
        var headings = action?.Constraints
            .OfType<GenericActorActionLegality.ArgumentConstraint
                .ProjectileHeadingConstraint>()
            .SingleOrDefault();
        if (action is null || headings is null)
            return false;

        IEnumerable<GenericActorContext.ObservedEnemyState> enemies =
            target is null
                ? mind.Enemies
                    .OrderBy(enemy => enemy.Health)
                    .ThenBy(enemy =>
                        body.Position.ChebyshevDistance(enemy.Position))
                    .ThenBy(enemy => enemy.ActorId)
                : [target];
        foreach (GenericActorContext.ObservedEnemyState enemy in enemies)
        {
            if (!TryHeading(body.Position, enemy.Position, out var heading)
                || !headings.AllowedValues.Contains(heading)
                || body.Position.ChebyshevDistance(enemy.Position)
                    > attack.Projectile.MaxTravelTiles
                || !ClearRay(contract.Map, body.Position, enemy.Position))
            {
                continue;
            }
            body.Command(
                action.ActionId,
                action.ActionCode,
                [new GenericActorActionArgument.ProjectileHeadingArgument(heading)],
                $"fire at {enemy.ActorId}");
            return true;
        }
        return false;
    }

    public static Position[] Ring(
        GenericActorMapContract map,
        Position centre,
        int radius) =>
        EightWays
            .Select(heading =>
            {
                (int dx, int dy) = heading.Vector();
                return centre.Offset(dx * radius, dy * radius);
            })
            .Where(position => CanEnter(map, position))
            .Distinct()
            .ToArray();

    public static bool TryHeading(
        Position source,
        Position target,
        out ProjectileHeading heading)
    {
        int dx = target.X - source.X;
        int dy = target.Y - source.Y;
        if (dx == 0 && dy == 0
            || dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy))
        {
            heading = default;
            return false;
        }
        heading = (Math.Sign(dx), Math.Sign(dy)) switch
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

    public static bool ClearRay(
        GenericActorMapContract map,
        Position source,
        Position target)
    {
        if (!TryHeading(source, target, out var heading))
            return false;
        (int dx, int dy) = heading.Vector();
        Position cursor = source;
        while (cursor != target)
        {
            Position next = cursor.Offset(dx, dy);
            if (next != target && !CanEnter(map, next))
                return false;
            if (dx != 0 && dy != 0
                && (!CanEnter(map, cursor.Offset(dx, 0))
                    || !CanEnter(map, cursor.Offset(0, dy))))
            {
                return false;
            }
            cursor = next;
        }
        return true;
    }

    private static GenericActorActionLegality? Available(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        GenericActorRulesContract.ActionKind kind)
    {
        HashSet<string> actionIds = contract.Rules.Actions
            .Where(action => action.Kind == kind)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        return body.ActionLegalities
            .Where(action => action.Available && actionIds.Contains(action.ActionId))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static GenericActorRulesContract.MovementFacingCoupling
        MovementCoupling(
            GenericActorResolvedMatchContract contract,
            MindBody body)
    {
        string? profileId = contract.Rules.Forms.FirstOrDefault(form =>
            string.Equals(form.Id, body.FormId, StringComparison.Ordinal))
            ?.MovementProfileId;
        return contract.Rules.MovementProfiles.FirstOrDefault(profile =>
            string.Equals(profile.Id, profileId, StringComparison.Ordinal))
            ?.FacingCoupling
            ?? GenericActorRulesContract.MovementFacingCoupling.PreserveFacing;
    }

    private static HashSet<Position> BlockedFirstStep(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Traffic traffic)
    {
        var blocked = new HashSet<Position>(traffic.Claimed);
        blocked.Remove(body.Position);
        foreach (GenericActorContext.ObservedProjectile projectile
                 in mind.VisibleProjectiles ?? [])
        {
            if (projectile.OwnerTeamId == body.ActorId.TeamId)
                continue;
            Position cursor = projectile.Position;
            int advances = Math.Min(2, Math.Max(1, projectile.RemainingTiles));
            int tiles = Math.Min(
                projectile.RemainingTiles,
                advances * projectile.TilesPerAdvance);
            (int dx, int dy) = projectile.Heading.Vector();
            for (int step = 0; step < tiles; step++)
            {
                cursor = cursor.Offset(dx, dy);
                blocked.Add(cursor);
            }
        }

        if (body.PreviousActionResolution is
            {
                Outcome: GenericActorActionResolution.ActionOutcome.Blocked,
            } previous
            && previous.AcceptedAction.Arguments
                .OfType<GenericActorActionArgument.ProjectileHeadingArgument>()
                .SingleOrDefault() is { } prior)
        {
            (int dx, int dy) = prior.Value.Vector();
            blocked.Add(body.Position.Offset(dx, dy));
        }
        _ = contract;
        return blocked;
    }

    private static ProjectileHeading? FindFirstStep(
        GenericActorMapContract map,
        Position start,
        IReadOnlyCollection<Position> goals,
        IReadOnlySet<Position> blockedFirst,
        IReadOnlyCollection<ProjectileHeading> headings,
        Direction forward)
    {
        var goalSet = goals.ToHashSet();
        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Position, ProjectileHeading First)>();

        foreach (ProjectileHeading heading in Ordered(
                     start,
                     goalSet,
                     headings,
                     forward))
        {
            Position next = Offset(start, heading);
            if (!CanTraverse(map, start, next, heading)
                || blockedFirst.Contains(next)
                || !visited.Add(next))
            {
                continue;
            }
            if (goalSet.Contains(next))
                return heading;
            queue.Enqueue((next, heading));
        }

        while (queue.Count > 0)
        {
            (Position position, ProjectileHeading first) = queue.Dequeue();
            foreach (ProjectileHeading heading in Ordered(
                         position,
                         goalSet,
                         headings,
                         forward))
            {
                Position next = Offset(position, heading);
                if (!CanTraverse(map, position, next, heading)
                    || !visited.Add(next))
                {
                    continue;
                }
                if (goalSet.Contains(next))
                    return first;
                queue.Enqueue((next, first));
            }
        }
        return null;
    }

    private static IEnumerable<ProjectileHeading> Ordered(
        Position from,
        IReadOnlyCollection<Position> goals,
        IReadOnlyCollection<ProjectileHeading> headings,
        Direction forward) =>
        headings
            .OrderBy(heading => Distance(Offset(from, heading), goals))
            .ThenBy(heading => LocalRank(heading, forward));

    private static int Distance(
        Position position,
        IReadOnlyCollection<Position> goals) =>
        goals.Min(goal => position.ChebyshevDistance(goal));

    private static int LocalRank(
        ProjectileHeading heading,
        Direction forward)
    {
        int forwardSector = (int)forward.ToProjectileHeading();
        int delta = ((int)heading - forwardSector + 8) % 8;
        return delta switch
        {
            0 => 0,
            7 => 1,
            1 => 2,
            6 => 3,
            2 => 4,
            5 => 5,
            3 => 6,
            _ => 7,
        };
    }

    private static bool CanTraverse(
        GenericActorMapContract map,
        Position from,
        Position to,
        ProjectileHeading heading)
    {
        if (!CanEnter(map, to))
            return false;
        (int dx, int dy) = heading.Vector();
        return dx == 0
            || dy == 0
            || CanEnter(map, from.Offset(dx, 0))
                && CanEnter(map, from.Offset(0, dy));
    }

    private static bool CanEnter(
        GenericActorMapContract map,
        Position position) =>
        position.X >= 0
        && position.Y >= 0
        && position.X < map.Width
        && position.Y < map.Height
        && map.TileRows[position.Y][position.X] != '#';

    private static Position Offset(
        Position position,
        ProjectileHeading heading)
    {
        (int dx, int dy) = heading.Vector();
        return position.Offset(dx, dy);
    }

    private static Direction? Cardinal(ProjectileHeading heading) =>
        heading switch
        {
            ProjectileHeading.North => Direction.North,
            ProjectileHeading.East => Direction.East,
            ProjectileHeading.South => Direction.South,
            ProjectileHeading.West => Direction.West,
            _ => null,
        };

    private static bool BlocksGround(
        GenericActorContext.ArcRelaySignatureState signature) =>
        string.Equals(signature.Kind, "hardlight-block", StringComparison.Ordinal)
        || string.Equals(signature.Kind, "trip-node", StringComparison.Ordinal)
        || string.Equals(signature.Kind, "sentinel-seed", StringComparison.Ordinal);
}
