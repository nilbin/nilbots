using BotArena.Sdk;

/// <summary>
/// Adapter continually reassesses territorial score, objective control,
/// visible force balance, and the capabilities currently offered to this
/// body. Nilbots creates one independent instance for every active body life.
/// </summary>
public sealed class Adapter : IGenericActorBot
{
    private GenericActorResolvedMatchContract? _contract;
    private GenericActorResolvedMatchContract.FrontlineModeMapBinding?
        _frontline;
    private int _teamId;
    private Direction _authoredForward;

    public void StartLife(GenericActorMatchStart start)
    {
        _contract = start.Contract;
        _frontline = start.Contract.ModeMapBinding
            as GenericActorResolvedMatchContract.FrontlineModeMapBinding;
        _teamId = start.ActorId.TeamId;
        _authoredForward = start.Contract.ParticipantRegionAssignments
            .FirstOrDefault(assignment =>
                assignment.ParticipantId == start.ParticipantId)
            ?.Facing
            ?? Direction.North;
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        GenericActorResolvedMatchContract? contract = _contract;
        if (contract is null)
            return Wait(context, "waiting for match contract");

        if (context.Self.PendingSameLifeTransition is not null)
            return Wait(context, "finishing form transition");

        if (context.Mode
                is not GenericActorContext.ModeObservationState.Frontline
                    frontline
            || _frontline is null
            || frontline.ActivePositionIndex < 0
            || frontline.ActivePositionIndex
                >= _frontline.OrderedObjectiveRegionIds.Length)
        {
            return TryVisibleAttack(context, contract, null)
                ?? Wait(context, "unsupported objective state");
        }

        string objectiveRegionId =
            _frontline.OrderedObjectiveRegionIds[
                frontline.ActivePositionIndex];
        GenericActorMapContract.Region? objective =
            contract.Map.Regions.FirstOrDefault(region =>
                string.Equals(
                    region.RegionId,
                    objectiveRegionId,
                    StringComparison.Ordinal));
        if (objective is null)
        {
            return TryVisibleAttack(context, contract, null)
                ?? Wait(context, "objective region is absent");
        }

        var objectiveTiles = objective.Tiles.ToHashSet();
        Situation situation = Assess(
            context,
            contract,
            frontline,
            objectiveTiles);

        GenericActorDecision? attack = TryVisibleAttack(
            context,
            contract,
            objectiveTiles);
        if (attack is not null)
            return attack;

        GenericActorDecision? evasion = TryEmergencyEvasion(
            context,
            contract,
            objectiveTiles,
            situation);
        if (evasion is not null)
            return evasion;

        GenericActorDecision? fabricate = TryFabricate(
            context,
            situation);
        if (fabricate is not null)
            return fabricate;

        GenericActorDecision? split = TrySplit(
            context,
            contract,
            situation);
        if (split is not null)
            return split;

        GenericActorDecision? transform = TryTransform(
            context,
            contract,
            objectiveTiles,
            situation);
        if (transform is not null)
            return transform;

        return Navigate(
            context,
            contract,
            objectiveRegionId,
            objectiveTiles,
            situation);
    }

    private Situation Assess(
        GenericActorContext context,
        GenericActorResolvedMatchContract contract,
        GenericActorContext.ModeObservationState.Frontline frontline,
        IReadOnlySet<Position> objectiveTiles)
    {
        int ownMobileBodies =
            (ObjectiveWeight(contract, context.Self.FormId) > 0 ? 1 : 0)
            + context.Allies.Count(ally =>
                ObjectiveWeight(contract, ally.FormId) > 0);
        int alliedMobileOnObjective = context.Allies.Count(ally =>
            objectiveTiles.Contains(ally.Position)
            && ObjectiveWeight(contract, ally.FormId) > 0);
        int ownObjectiveBodies = alliedMobileOnObjective
            + (objectiveTiles.Contains(context.Self.Position)
                && ObjectiveWeight(contract, context.Self.FormId) > 0
                    ? 1
                    : 0);
        int visibleEnemyObjectiveBodies = context.Enemies.Count(enemy =>
            objectiveTiles.Contains(enemy.Position)
            && ObjectiveWeight(contract, enemy.FormId) > 0);
        int visibleEnemyMobileBodies = context.Enemies.Count(enemy =>
            ObjectiveWeight(contract, enemy.FormId) > 0);

        long scoreMargin = ScoreMargin(context, contract);
        bool ownClaim = frontline.ClaimingTeamId == _teamId;
        bool enemyClaim = frontline.ClaimingTeamId is int claimant
            && claimant != _teamId;
        int threshold =
            (contract.Rules.GameMode
                as GenericActorRulesContract.FrontlineGameMode)
            ?.Capture.Threshold
            ?? 1;
        int remainingTicks = Math.Max(
            0,
            contract.Rules.Limits.MaxTicks - context.Tick);
        bool late = remainingTicks
            <= contract.Map.Width + Math.Max(1, threshold);
        bool secure = ownClaim
            && frontline.CaptureProgress >= Math.Max(1, threshold / 2)
            && scoreMargin >= 0
            && visibleEnemyObjectiveBodies == 0;
        bool pressing = scoreMargin < 0
            || enemyClaim
            || visibleEnemyObjectiveBodies >= Math.Max(1, ownObjectiveBodies)
            || late && !ownClaim;

        return new Situation(
            scoreMargin,
            ownClaim,
            enemyClaim,
            secure,
            pressing,
            late,
            ownMobileBodies,
            alliedMobileOnObjective,
            ownObjectiveBodies,
            visibleEnemyMobileBodies,
            visibleEnemyObjectiveBodies);
    }

    private long ScoreMargin(
        GenericActorContext context,
        GenericActorResolvedMatchContract contract)
    {
        GenericActorRulesContract.ScoreRanking? ranking =
            contract.Rules.GameMode.Victory.TimeoutRanking.FirstOrDefault();
        if (ranking is null)
            return 0;

        GenericActorContext.TeamScoreState? own =
            context.Scoreboard.Teams.FirstOrDefault(team =>
                team.TeamId == _teamId);
        if (own is null)
            return 0;

        long ownValue = ScoreValue(own, ranking.Channel);
        long[] opponentValues = context.Scoreboard.Teams
            .Where(team => team.TeamId != _teamId && team.Eligible)
            .Select(team => ScoreValue(team, ranking.Channel))
            .ToArray();
        if (opponentValues.Length == 0)
            return 0;

        bool lowerIsBetter =
            ranking.Direction.Contains(
                "ascending",
                StringComparison.OrdinalIgnoreCase)
            || ranking.Direction.Contains(
                "lower",
                StringComparison.OrdinalIgnoreCase);
        long strongestOpponent = lowerIsBetter
            ? opponentValues.Min()
            : opponentValues.Max();
        return lowerIsBetter
            ? strongestOpponent - ownValue
            : ownValue - strongestOpponent;
    }

    private static long ScoreValue(
        GenericActorContext.TeamScoreState team,
        string channel) =>
        team.Scores.FirstOrDefault(score =>
            string.Equals(
                score.Channel,
                channel,
                StringComparison.Ordinal))
        ?.Value
        ?? 0;

    private GenericActorDecision? TryVisibleAttack(
        GenericActorContext context,
        GenericActorResolvedMatchContract contract,
        IReadOnlySet<Position>? objectiveTiles)
    {
        GenericActorRulesContract.AttackProfile? attackProfile =
            AttackProfile(contract, context.Self.FormId);
        if (attackProfile is null || context.Enemies.IsEmpty)
            return null;

        IEnumerable<GenericActorContext.ObservedEnemyState> targets =
            context.Enemies
                .OrderByDescending(enemy =>
                    objectiveTiles?.Contains(enemy.Position) == true)
                .ThenByDescending(enemy =>
                    enemy.PendingSameLifeTransition is not null)
                .ThenBy(enemy => enemy.Health)
                .ThenBy(enemy =>
                    context.Self.Position.ChebyshevDistance(
                        enemy.Position))
                .ThenBy(enemy => enemy.ActorId);

        GenericActorActionLegality? absolute =
            Available(context, "shoot-direction");
        GenericActorActionLegality.ArgumentConstraint
            .ProjectileHeadingConstraint? headings =
            absolute?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        if (absolute is not null && headings is not null)
        {
            foreach (GenericActorContext.ObservedEnemyState target in targets)
            {
                ProjectileHeading? heading = RayHeading(
                    context.Self.Position,
                    target.Position);
                if (heading is not ProjectileHeading value
                    || !headings.AllowedValues.Contains(value)
                    || !CanHit(
                        contract.Map,
                        context.Self.Position,
                        target.Position,
                        value,
                        attackProfile.Projectile.MaxTravelTiles))
                {
                    continue;
                }

                return Choose(
                    absolute,
                    $"absolute fire at {target.Position}",
                    new GenericActorActionArgument
                        .ProjectileHeadingArgument(value));
            }
        }

        GenericActorActionLegality? shoot = Available(context, "shoot");
        if (shoot is null)
            return null;

        bool programPayloadAllowed =
            shoot.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ShotProgramConstraint>()
                .SingleOrDefault()
            ?.Allowed
            == true;
        foreach (GenericActorContext.ObservedEnemyState target in targets)
        {
            ProjectileHeading? heading = RayHeading(
                context.Self.Position,
                target.Position);
            if (heading is not ProjectileHeading value
                || !CanHit(
                    contract.Map,
                    context.Self.Position,
                    target.Position,
                    value,
                    attackProfile.Projectile.MaxTravelTiles))
            {
                continue;
            }

            int aimOffset = SignedHeadingDifference(
                context.Self.Facing.ToProjectileHeading(),
                value);
            if (aimOffset == 0)
            {
                return Choose(
                    shoot,
                    $"straight fire at {target.Position}");
            }

            GenericActorRulesContract.ShotProgramDefinition program =
                attackProfile.ShotProgram;
            if (!programPayloadAllowed
                || !program.Enabled
                || aimOffset < program.MinInitialAimSteps
                || aimOffset > program.MaxInitialAimSteps)
            {
                continue;
            }

            var shot = new ShotProgram(
                aimOffset,
                program.AimOnlyProgram.BendDirection,
                program.AimOnlyProgram.BendAfterTiles,
                program.AimOnlyProgram.BendEveryTiles,
                program.AimOnlyProgram.BendCount);
            return Choose(
                shoot,
                $"offset fire at {target.Position}",
                new GenericActorActionArgument.ShotProgramArgument(
                    shot));
        }

        return null;
    }

    private GenericActorDecision? TryEmergencyEvasion(
        GenericActorContext context,
        GenericActorResolvedMatchContract contract,
        IReadOnlySet<Position> objectiveTiles,
        Situation situation)
    {
        if (context.VisibleProjectiles is not { } projectiles)
            return null;

        int incomingDamage = contract.Rules.AttackProfiles
            .Select(profile => profile.Projectile.DamagePerHit)
            .DefaultIfEmpty(1)
            .Max();
        if (context.Self.Health > incomingDamage
            || situation.EnemyClaim && objectiveTiles.Contains(
                context.Self.Position))
        {
            return null;
        }

        var danger = new HashSet<Position>();
        foreach (GenericActorContext.ObservedProjectile projectile
            in projectiles.Where(projectile =>
                projectile.OwnerTeamId != _teamId))
        {
            danger.Add(projectile.Position);
            if (projectile.TicksUntilAdvance > 1)
                continue;

            Position position = projectile.Position;
            var (dx, dy) = projectile.Heading.Vector();
            for (int step = 0;
                step < Math.Max(1, projectile.TilesPerAdvance);
                step++)
            {
                position = position.Offset(dx, dy);
                danger.Add(position);
            }
        }

        if (!danger.Any(tile =>
            tile.ChebyshevDistance(context.Self.Position) <= 1))
        {
            return null;
        }

        GenericActorActionLegality? move = Available(context, "move");
        IReadOnlySet<Direction> allowed = AllowedDirections(move);
        if (move is null || allowed.Count == 0)
            return null;

        HashSet<Position> occupied = Occupied(context, includeProjectiles: true);
        Direction? escape = allowed
            .Select(direction => new
            {
                Direction = direction,
                Position = Offset(context.Self.Position, direction),
            })
            .Where(candidate =>
                CanEnter(contract.Map, candidate.Position, occupied)
                && !danger.Contains(candidate.Position))
            .OrderByDescending(candidate =>
                danger.Select(tile =>
                        tile.ChebyshevDistance(candidate.Position))
                    .DefaultIfEmpty(contract.Map.Width + contract.Map.Height)
                    .Min())
            .ThenBy(candidate =>
                ObjectiveWeight(contract, context.Self.FormId) > 0
                    ? objectiveTiles.Select(tile =>
                            tile.ChebyshevDistance(candidate.Position))
                        .DefaultIfEmpty(0)
                        .Min()
                    : 0)
            .ThenBy(candidate => candidate.Direction)
            .Select(candidate => (Direction?)candidate.Direction)
            .FirstOrDefault();

        return escape is Direction direction
            ? Choose(
                move,
                "evading a lethal visible projectile",
                new GenericActorActionArgument.DirectionArgument(
                    direction))
            : null;
    }

    private GenericActorDecision? TryFabricate(
        GenericActorContext context,
        Situation situation)
    {
        GenericActorActionLegality? fabricate =
            Available(context, "fabricate");
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = fabricate?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint>()
                .SingleOrDefault();
        if (fabricate is null || targets is null)
            return null;

        HashSet<(int TeamId, int UnitId)> ready = context.TeamUnits
            .Where(slot =>
                slot.State is GenericActorContext.UnitSlotState.Ready)
            .Select(slot => (slot.TeamId, slot.UnitId))
            .ToHashSet();
        GenericActorActionArgument.UnitTarget? target =
            targets.AllowedValues
                .Where(value =>
                    value.TeamId == _teamId
                    && ready.Contains((value.TeamId, value.UnitId)))
                .OrderBy(value => value.UnitId)
                .Select(value =>
                    (GenericActorActionArgument.UnitTarget?)value)
                .FirstOrDefault();
        if (target is not GenericActorActionArgument.UnitTarget value)
            return null;

        bool alreadyNumericallyComfortable =
            situation.OwnMobileBodies
                > situation.VisibleEnemyMobileBodies
            && situation.OwnClaim
            && situation.ScoreMargin >= 0;
        bool protectLateLead = situation.Late
            && situation.Secure
            && alreadyNumericallyComfortable;
        if (protectLateLead)
            return null;

        return Choose(
            fabricate,
            $"fabricating ready allied slot {value.UnitId}",
            new GenericActorActionArgument.UnitTargetArgument(value));
    }

    private GenericActorDecision? TrySplit(
        GenericActorContext context,
        GenericActorResolvedMatchContract contract,
        Situation situation)
    {
        GenericActorActionLegality? split = Available(context, "split");
        if (split is null)
            return null;

        bool contractSupportsCurrentForm =
            contract.Rules.ReplicationTransitions
                .OfType<GenericActorRulesContract
                    .SplitReplicationTransition>()
                .Any(transition =>
                    string.Equals(
                        transition.ActionId,
                        split.ActionId,
                        StringComparison.Ordinal)
                    && transition.SourceFormIds.Contains(
                        context.Self.FormId,
                        StringComparer.Ordinal));
        if (!contractSupportsCurrentForm)
            return null;

        bool objectiveEmergency = situation.EnemyClaim
            && situation.VisibleEnemyObjectiveBodies
                >= Math.Max(1, situation.OwnObjectiveBodies);
        bool forceDeficit =
            situation.VisibleEnemyMobileBodies
                > situation.OwnMobileBodies;
        bool comeback = situation.ScoreMargin < 0
            && situation.AlliedMobileOnObjective == 0;
        bool lastPush = situation.Late && !situation.OwnClaim;
        if (!objectiveEmergency && !forceDeficit && !comeback && !lastPush)
            return null;

        return Choose(split, "splitting to answer territorial pressure");
    }

    private GenericActorDecision? TryTransform(
        GenericActorContext context,
        GenericActorResolvedMatchContract contract,
        IReadOnlySet<Position> objectiveTiles,
        Situation situation)
    {
        GenericActorActionLegality? transform =
            Available(context, "transform");
        GenericActorActionLegality.ArgumentConstraint.FormTargetConstraint?
            targets = transform?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .FormTargetConstraint>()
                .SingleOrDefault();
        if (transform is null
            || targets is null
            || situation.AlliedMobileOnObjective == 0
            || situation.Pressing)
        {
            return null;
        }

        GenericActorRulesContract.Form? targetForm =
            targets.AllowedFormIds
                .Select(formId => Form(contract, formId))
                .Where(form =>
                    form is not null
                    && form.ObjectiveWeight == 0
                    && form.AttackProfileId is not null)
                .OrderBy(form =>
                    AttackProfile(contract, form!.Id)
                        ?.CooldownTicks
                    ?? int.MaxValue)
                .ThenByDescending(form => form!.MaxHealth)
                .ThenBy(form => form!.Id, StringComparer.Ordinal)
                .FirstOrDefault();
        if (targetForm is null)
            return null;

        GenericActorRulesContract.AttackProfile? targetAttack =
            AttackProfile(contract, targetForm.Id);
        int supportDistance = objectiveTiles
            .Select(tile =>
                tile.ChebyshevDistance(context.Self.Position))
            .DefaultIfEmpty(int.MaxValue)
            .Min();
        bool enemyInCoverage = context.Enemies.Any(enemy =>
            enemy.Position.ChebyshevDistance(context.Self.Position)
                <= (targetAttack?.Projectile.MaxTravelTiles ?? 0));
        bool objectiveInCoverage = targetAttack is not null
            && supportDistance <= targetAttack.Projectile.MaxTravelTiles;
        if (!situation.Secure && !enemyInCoverage)
            return null;
        if (!objectiveInCoverage && !enemyInCoverage)
            return null;

        return Choose(
            transform,
            $"adapting into support form {targetForm.Id}",
            new GenericActorActionArgument.FormTargetArgument(
                targetForm.Id));
    }

    private GenericActorDecision Navigate(
        GenericActorContext context,
        GenericActorResolvedMatchContract contract,
        string objectiveRegionId,
        IReadOnlySet<Position> objectiveTiles,
        Situation situation)
    {
        if (ObjectiveWeight(contract, context.Self.FormId) <= 0)
        {
            return FaceUsefulDirection(context, _authoredForward)
                ?? Wait(context, $"holding support near {objectiveRegionId}");
        }

        IReadOnlySet<Position> goals = objectiveTiles;
        bool takeSupportPosition = situation.Secure
            && situation.AlliedMobileOnObjective > 0
            && !situation.EnemyClaim;
        if (takeSupportPosition)
        {
            HashSet<Position> support = SupportTiles(
                contract.Map,
                objectiveTiles);
            if (support.Count > 0)
                goals = support;
        }

        if (goals.Contains(context.Self.Position))
        {
            Direction desiredFacing = context.Enemies
                .OrderBy(enemy =>
                    enemy.Position.ChebyshevDistance(
                        context.Self.Position))
                .ThenBy(enemy => enemy.ActorId)
                .Select(enemy => CardinalBearing(
                    context.Self.Position,
                    enemy.Position,
                    _authoredForward))
                .FirstOrDefault(_authoredForward);
            return FaceUsefulDirection(context, desiredFacing)
                ?? Wait(
                    context,
                    takeSupportPosition
                        ? $"screening {objectiveRegionId}"
                        : $"controlling {objectiveRegionId}");
        }

        GenericActorActionLegality? move = Available(context, "move");
        IReadOnlySet<Direction> allowed = AllowedDirections(move);
        HashSet<Position> occupied = Occupied(
            context,
            includeProjectiles: true);
        Direction? step = move is null
            ? null
            : FindFirstStep(
                contract.Map,
                context.Self.Position,
                goals,
                occupied,
                allowed);
        if (step is Direction direction)
        {
            return Choose(
                move!,
                takeSupportPosition
                    ? $"screening around {objectiveRegionId}"
                    : $"moving to active objective {objectiveRegionId}",
                new GenericActorActionArgument.DirectionArgument(
                    direction));
        }

        Direction fallbackFacing = context.Enemies
            .OrderBy(enemy =>
                enemy.Position.ChebyshevDistance(
                    context.Self.Position))
            .ThenBy(enemy => enemy.ActorId)
            .Select(enemy => CardinalBearing(
                context.Self.Position,
                enemy.Position,
                _authoredForward))
            .FirstOrDefault(_authoredForward);
        return FaceUsefulDirection(context, fallbackFacing)
            ?? Wait(context, $"no clear route to {objectiveRegionId}");
    }

    private static GenericActorDecision? FaceUsefulDirection(
        GenericActorContext context,
        Direction direction)
    {
        if (context.Self.Facing == direction)
            return null;

        GenericActorActionLegality? rotate =
            Available(context, "rotate");
        IReadOnlySet<Direction> allowed = AllowedDirections(rotate);
        return rotate is not null && allowed.Contains(direction)
            ? Choose(
                rotate,
                $"facing {direction}",
                new GenericActorActionArgument.DirectionArgument(
                    direction))
            : null;
    }

    private static GenericActorDecision Choose(
        GenericActorActionLegality legality,
        string debug,
        params GenericActorActionArgument[] arguments) =>
        new(
            legality.ActionId,
            legality.ActionCode,
            arguments,
            debug);

    private static GenericActorDecision Wait(
        GenericActorContext context,
        string debug)
    {
        GenericActorActionLegality? wait = context.Action("wait");
        if (wait is not null)
            return GenericActorDecision.WithoutArguments(
                wait.ActionId,
                wait.ActionCode,
                debug);

        GenericActorActionLegality? parameterless =
            context.ActionLegalities.FirstOrDefault(action =>
                action.Available && action.Constraints.IsEmpty);
        if (parameterless is not null)
        {
            return GenericActorDecision.WithoutArguments(
                parameterless.ActionId,
                parameterless.ActionCode,
                debug);
        }

        GenericActorActionLegality fallback =
            context.ActionLegalities.First();
        return GenericActorDecision.WithoutArguments(
            fallback.ActionId,
            fallback.ActionCode,
            debug);
    }

    private static GenericActorActionLegality? Available(
        GenericActorContext context,
        string actionId) =>
        context.Action(actionId) is { Available: true } action
            ? action
            : null;

    private static IReadOnlySet<Direction> AllowedDirections(
        GenericActorActionLegality? legality) =>
        legality?.Constraints
            .OfType<GenericActorActionLegality.ArgumentConstraint
                .DirectionConstraint>()
            .SingleOrDefault()
            ?.AllowedValues
            .ToHashSet()
        ?? new HashSet<Direction>();

    private static GenericActorRulesContract.Form? Form(
        GenericActorResolvedMatchContract contract,
        string formId) =>
        contract.Rules.Forms.FirstOrDefault(form =>
            string.Equals(
                form.Id,
                formId,
                StringComparison.Ordinal));

    private static int ObjectiveWeight(
        GenericActorResolvedMatchContract contract,
        string formId) =>
        Form(contract, formId)?.ObjectiveWeight ?? 0;

    private static GenericActorRulesContract.AttackProfile? AttackProfile(
        GenericActorResolvedMatchContract contract,
        string formId)
    {
        string? profileId = Form(contract, formId)?.AttackProfileId;
        return profileId is null
            ? null
            : contract.Rules.AttackProfiles.FirstOrDefault(profile =>
                string.Equals(
                    profile.Id,
                    profileId,
                    StringComparison.Ordinal));
    }

    private static HashSet<Position> Occupied(
        GenericActorContext context,
        bool includeProjectiles)
    {
        var occupied = context.Allies
            .Select(ally => ally.Position)
            .Concat(context.Enemies.Select(enemy => enemy.Position))
            .ToHashSet();
        if (includeProjectiles
            && context.VisibleProjectiles is { } projectiles)
        {
            occupied.UnionWith(
                projectiles.Select(projectile => projectile.Position));
        }
        return occupied;
    }

    private static HashSet<Position> SupportTiles(
        GenericActorMapContract map,
        IReadOnlySet<Position> objectiveTiles)
    {
        HashSet<Position> forbidden = map.TileTags
            .Where(tag =>
                tag.Kind
                    == GenericActorMapContract.TileTagKind
                        .TransitionPlacementForbidden)
            .SelectMany(tag => tag.Tiles)
            .ToHashSet();
        var support = new HashSet<Position>();
        foreach (Position tile in objectiveTiles)
        {
            foreach (Direction direction in Enum.GetValues<Direction>())
            {
                Position candidate = Offset(tile, direction);
                if (!objectiveTiles.Contains(candidate)
                    && !forbidden.Contains(candidate)
                    && IsOpen(map, candidate))
                {
                    support.Add(candidate);
                }
            }
        }
        return support;
    }

    private static Direction? FindFirstStep(
        GenericActorMapContract map,
        Position start,
        IReadOnlySet<Position> goals,
        IReadOnlySet<Position> occupied,
        IReadOnlySet<Direction> allowedFirstSteps)
    {
        if (goals.Contains(start))
            return null;

        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Position, Direction First)>();
        foreach (Direction direction in Enum.GetValues<Direction>())
        {
            if (!allowedFirstSteps.Contains(direction))
                continue;

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
        IsOpen(map, position) && !occupied.Contains(position);

    private static bool IsOpen(
        GenericActorMapContract map,
        Position position) =>
        position.X >= 0
        && position.Y >= 0
        && position.X < map.Width
        && position.Y < map.Height
        && map.TileRows[position.Y][position.X] != '#';

    private static Position Offset(
        Position position,
        Direction direction)
    {
        var (dx, dy) = direction.Vector();
        return position.Offset(dx, dy);
    }

    private static ProjectileHeading? RayHeading(
        Position from,
        Position target)
    {
        int dx = target.X - from.X;
        int dy = target.Y - from.Y;
        if (dx == 0 && dy == 0)
            return null;
        if (dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy))
            return null;

        return (Math.Sign(dx), Math.Sign(dy)) switch
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

    private static bool CanHit(
        GenericActorMapContract map,
        Position from,
        Position target,
        ProjectileHeading heading,
        int maxTravelTiles)
    {
        int distance = from.ChebyshevDistance(target);
        if (distance <= 0 || distance > maxTravelTiles)
            return false;

        Position position = from;
        var (dx, dy) = heading.Vector();
        for (int step = 0; step < distance; step++)
        {
            Position next = position.Offset(dx, dy);
            if (!IsOpen(map, next))
                return false;
            if (dx != 0 && dy != 0
                && (!IsOpen(map, position.Offset(dx, 0))
                    || !IsOpen(map, position.Offset(0, dy))))
            {
                return false;
            }
            position = next;
        }
        return position == target;
    }

    private static int SignedHeadingDifference(
        ProjectileHeading from,
        ProjectileHeading to)
    {
        int difference = ((int)to - (int)from + 8) % 8;
        return difference > 4 ? difference - 8 : difference;
    }

    private static Direction CardinalBearing(
        Position from,
        Position target,
        Direction tieBreak)
    {
        int dx = target.X - from.X;
        int dy = target.Y - from.Y;
        if (Math.Abs(dx) > Math.Abs(dy))
            return dx >= 0 ? Direction.East : Direction.West;
        if (Math.Abs(dy) > Math.Abs(dx))
            return dy >= 0 ? Direction.South : Direction.North;

        return tieBreak;
    }

    private sealed record Situation(
        long ScoreMargin,
        bool OwnClaim,
        bool EnemyClaim,
        bool Secure,
        bool Pressing,
        bool Late,
        int OwnMobileBodies,
        int AlliedMobileOnObjective,
        int OwnObjectiveBodies,
        int VisibleEnemyMobileBodies,
        int VisibleEnemyObjectiveBodies);
}
