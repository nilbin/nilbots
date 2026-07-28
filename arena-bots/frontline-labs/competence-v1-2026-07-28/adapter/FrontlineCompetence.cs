using BotArena.Sdk;

/// <summary>
/// Equal-budget calibration pass shared by the four post-reveal starter
/// doctrines. It supplies only contract-generic tactical fundamentals:
/// activate an immediately fabricable Ready slot, leave a projectile's next
/// straight advance, and take an unobstructed direct shot. Doctrine-specific
/// objective, transformation, and formation logic remains in each entrant.
/// </summary>
internal static class FrontlineCompetence
{
    private static readonly Direction[] Directions =
    [
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West,
    ];

    public static GenericActorDecision? TryImmediate(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        if (context.Self.PendingSameLifeTransition is not null)
            return null;

        return TryFabricateReady(contract, context)
            ?? TryDodge(contract, context)
            ?? TryDirectShot(contract, context);
    }

    private static GenericActorDecision? TryFabricateReady(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        HashSet<string> actionIds = contract.Rules.Actions
            .Where(action =>
                action.Kind
                    == GenericActorRulesContract.ActionKind.Fabrication)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        GenericActorActionLegality? action = context.ActionLegalities
            .Where(candidate =>
                candidate.Available
                && actionIds.Contains(candidate.ActionId))
            .OrderBy(candidate => candidate.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint>()
                .SingleOrDefault();
        if (action is null || targets is null || targets.AllowedValues.IsEmpty)
            return null;

        GenericActorActionArgument.UnitTarget target =
            targets.AllowedValues
                .OrderBy(value => value.TeamId)
                .ThenBy(value => value.UnitId)
                .First();
        return new GenericActorDecision(
            action.ActionId,
            action.ActionCode,
            [
                new GenericActorActionArgument.UnitTargetArgument(target),
            ],
            $"activating Ready slot {target.TeamId}:{target.UnitId}");
    }

    private static GenericActorDecision? TryDodge(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        GenericActorContext.ObservedProjectile[] hostile =
            context.VisibleProjectiles
                ?.Where(projectile =>
                    projectile.OwnerTeamId
                        != context.Self.ActorId.TeamId)
                .OrderBy(projectile => projectile.ProjectileId)
                .ToArray()
            ?? [];
        if (!hostile.Any(projectile =>
                ReachesOnNextAdvance(
                    projectile,
                    context.Self.Position)))
        {
            return null;
        }

        HashSet<string> movementActionIds = contract.Rules.Actions
            .Where(action =>
                action.Kind
                    == GenericActorRulesContract.ActionKind.Movement)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        GenericActorActionLegality? move = context.ActionLegalities
            .Where(action =>
                action.Available
                && movementActionIds.Contains(action.ActionId))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            constraint = move?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        if (move is null || constraint is null)
            return null;

        Position[] objectiveTiles = ActiveObjectiveTiles(contract, context);
        HashSet<Position> occupied = context.Allies
            .Select(ally => ally.Position)
            .Concat(context.Enemies.Select(enemy => enemy.Position))
            .Concat(hostile.Select(projectile => projectile.Position))
            .ToHashSet();
        Direction? selected = constraint.AllowedValues
            .Where(direction => Directions.Contains(direction))
            .Select(direction =>
            {
                (int dx, int dy) = direction.Vector();
                Position destination = context.Self.Position.Offset(dx, dy);
                return (Direction: direction, Destination: destination);
            })
            .Where(candidate =>
                IsWalkable(contract.Map, candidate.Destination)
                && !occupied.Contains(candidate.Destination)
                && !hostile.Any(projectile =>
                    ReachesOnNextAdvance(
                        projectile,
                        candidate.Destination)))
            .OrderBy(candidate =>
                DistanceToObjective(
                    candidate.Destination,
                    objectiveTiles))
            .ThenByDescending(candidate =>
                hostile.Min(projectile =>
                    candidate.Destination.ChebyshevDistance(
                        projectile.Position)))
            .ThenBy(candidate => candidate.Direction)
            .Select(candidate => (Direction?)candidate.Direction)
            .FirstOrDefault();
        if (selected is not Direction direction)
            return null;

        return new GenericActorDecision(
            move.ActionId,
            move.ActionCode,
            [
                new GenericActorActionArgument.DirectionArgument(direction),
            ],
            $"dodging imminent projectile path toward {direction}");
    }

    private static Position[] ActiveObjectiveTiles(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        if (context.Mode
                is not GenericActorContext.ModeObservationState.Frontline mode
            || contract.ModeMapBinding
                is not GenericActorResolvedMatchContract
                    .FrontlineModeMapBinding binding
            || mode.ActivePositionIndex < 0
            || mode.ActivePositionIndex
                >= binding.OrderedObjectiveRegionIds.Length)
        {
            return [];
        }

        string regionId =
            binding.OrderedObjectiveRegionIds[mode.ActivePositionIndex];
        return contract.Map.Regions
            .FirstOrDefault(region =>
                string.Equals(
                    region.RegionId,
                    regionId,
                    StringComparison.Ordinal))
            ?.Tiles
            .ToArray()
            ?? [];
    }

    private static int DistanceToObjective(
        Position position,
        IReadOnlyCollection<Position> objectiveTiles) =>
        objectiveTiles.Count == 0
            ? 0
            : objectiveTiles.Min(position.ChebyshevDistance);

    private static GenericActorDecision? TryDirectShot(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(candidate =>
                string.Equals(
                    candidate.Id,
                    context.Self.FormId,
                    StringComparison.Ordinal));
        GenericActorRulesContract.AttackProfile? attack =
            form?.AttackProfileId is string attackProfileId
                ? contract.Rules.AttackProfiles.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.Id,
                        attackProfileId,
                        StringComparison.Ordinal))
                : null;
        if (form is null || attack is null || context.Enemies.IsEmpty)
            return null;

        HashSet<string> attackActionIds = contract.Rules.Actions
            .Where(action =>
                action.Kind == GenericActorRulesContract.ActionKind.Attack)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        GenericActorActionLegality[] actions = context.ActionLegalities
            .Where(action =>
                action.Available
                && attackActionIds.Contains(action.ActionId))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .ToArray();
        if (actions.Length == 0)
            return null;

        foreach (GenericActorContext.ObservedEnemyState target
                 in context.Enemies
                     .OrderBy(enemy => enemy.Health)
                     .ThenBy(enemy =>
                         context.Self.Position.ChebyshevDistance(
                             enemy.Position))
                     .ThenBy(enemy => enemy.ActorId))
        {
            if (!TryRay(
                    context.Self.Position,
                    target.Position,
                    out ProjectileHeading heading,
                    out int distance)
                || distance > attack.Projectile.MaxTravelTiles
                || !ClearRay(
                    contract.Map,
                    context.Self.Position,
                    target.Position,
                    attack.Projectile.DiagonalCornersMustBeClear))
            {
                continue;
            }

            foreach (GenericActorActionLegality action in actions)
            {
                GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint? headings =
                    action.Constraints
                        .OfType<GenericActorActionLegality.ArgumentConstraint
                            .ProjectileHeadingConstraint>()
                        .SingleOrDefault();
                if (headings is not null
                    && headings.AllowedValues.Contains(heading))
                {
                    return new GenericActorDecision(
                        action.ActionId,
                        action.ActionCode,
                        [
                            new GenericActorActionArgument
                                .ProjectileHeadingArgument(heading),
                        ],
                        $"direct fire at {target.ActorId}");
                }

                GenericActorActionLegality.ArgumentConstraint
                    .ShotProgramConstraint? programs =
                    action.Constraints
                        .OfType<GenericActorActionLegality.ArgumentConstraint
                            .ShotProgramConstraint>()
                        .SingleOrDefault();
                int aimOffset = SignedHeadingDifference(
                    context.Self.Facing.ToProjectileHeading(),
                    heading);
                if (aimOffset < attack.ShotProgram.MinInitialAimSteps
                    || aimOffset > attack.ShotProgram.MaxInitialAimSteps)
                {
                    continue;
                }

                if (aimOffset == 0
                    && attack.ShotProgram.PayloadOptional)
                {
                    return GenericActorDecision.WithoutArguments(
                        action.ActionId,
                        action.ActionCode,
                        $"straight fire at {target.ActorId}");
                }
                if (programs is not { Allowed: true }
                    || !attack.ShotProgram.Enabled)
                {
                    continue;
                }

                GenericActorRulesContract.AimOnlyShotProgramValue aimOnly =
                    attack.ShotProgram.AimOnlyProgram;
                return new GenericActorDecision(
                    action.ActionId,
                    action.ActionCode,
                    [
                        new GenericActorActionArgument.ShotProgramArgument(
                            new ShotProgram(
                                aimOffset,
                                aimOnly.BendDirection,
                                aimOnly.BendAfterTiles,
                                aimOnly.BendEveryTiles,
                                aimOnly.BendCount)),
                    ],
                    $"aimed direct fire at {target.ActorId}");
            }
        }
        return null;
    }

    private static bool ReachesOnNextAdvance(
        GenericActorContext.ObservedProjectile projectile,
        Position target)
    {
        if (!TryRay(
                projectile.Position,
                target,
                out ProjectileHeading heading,
                out int distance)
            || heading != projectile.Heading)
        {
            return false;
        }
        return distance <= Math.Min(
            projectile.TilesPerAdvance,
            projectile.RemainingTiles);
    }

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

        (int StepX, int StepY) step = (
            Math.Sign(dx),
            Math.Sign(dy));
        heading = step switch
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
        Position source,
        Position target,
        bool strictDiagonalCorners)
    {
        int stepX = Math.Sign(target.X - source.X);
        int stepY = Math.Sign(target.Y - source.Y);
        Position cursor = source;
        while (cursor != target)
        {
            Position next = cursor.Offset(stepX, stepY);
            if (next != target && !IsWalkable(map, next))
                return false;
            if (strictDiagonalCorners
                && stepX != 0
                && stepY != 0
                && (
                    !IsWalkable(map, cursor.Offset(stepX, 0))
                    || !IsWalkable(map, cursor.Offset(0, stepY))
                ))
            {
                return false;
            }
            cursor = next;
        }
        return true;
    }

    private static bool IsWalkable(
        GenericActorMapContract map,
        Position position) =>
        position.X >= 0
        && position.Y >= 0
        && position.X < map.Width
        && position.Y < map.Height
        && map.TileRows[position.Y][position.X] != '#';

    private static int SignedHeadingDifference(
        ProjectileHeading from,
        ProjectileHeading to)
    {
        int difference = ((int)to - (int)from + 8) % 8;
        return difference > 4 ? difference - 8 : difference;
    }
}
