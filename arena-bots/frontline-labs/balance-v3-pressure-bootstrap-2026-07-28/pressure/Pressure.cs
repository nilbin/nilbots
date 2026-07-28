using BotArena.Sdk;

/// <summary>
/// Takes the live Frontline position with one mobile body while surplus
/// mobile bodies stage toward the next position. Fire is used to clear the
/// capture route, and Split is accepted only as an immediately legal bonus;
/// the plan never waits for a later lifecycle capability to unlock.
/// </summary>
public sealed class Pressure : IGenericActorBot
{
    private GenericActorResolvedMatchContract? _contract;
    private GenericActorResolvedMatchContract.FrontlineModeMapBinding?
        _frontline;
    private string? _contractFingerprint;
    private int _advanceDelta;

    public void StartLife(GenericActorMatchStart start)
    {
        _contract = start.Contract;
        _contractFingerprint = start.Contract.MatchContractFingerprint;
        _frontline =
            start.Contract.ModeMapBinding
                as GenericActorResolvedMatchContract
                    .FrontlineModeMapBinding;
        _advanceDelta = _frontline?.TeamAdvances
            .FirstOrDefault(advance =>
                advance.TeamId == start.ActorId.TeamId)
            ?.ObjectiveIndexDelta ?? 0;
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        if (context.Self.PendingSameLifeTransition is not null)
            return Fallback(context, "finishing transition");

        GenericActorResolvedMatchContract? contract = _contract;
        GenericActorResolvedMatchContract.FrontlineModeMapBinding?
            binding = _frontline;
        if (contract is null
            || binding is null
            || !string.Equals(
                context.MatchContractFingerprint,
                _contractFingerprint,
                StringComparison.Ordinal)
            || context.Mode is not
                GenericActorContext.ModeObservationState.Frontline mode
            || mode.ActivePositionIndex < 0
            || mode.ActivePositionIndex
                >= binding.OrderedObjectiveRegionIds.Length)
        {
            return Fallback(context, "unsupported contract observation");
        }

        GenericActorMapContract.Region? activeObjective =
            FindRegion(
                contract.Map,
                binding.OrderedObjectiveRegionIds[
                    mode.ActivePositionIndex]);
        if (activeObjective is null || activeObjective.Tiles.IsEmpty)
            return Fallback(context, "active objective is unavailable");

        bool selfCanCapture =
            ObjectiveWeight(contract, context.Self.FormId) > 0;
        if (!selfCanCapture)
        {
            if (TryFire(
                    context,
                    contract,
                    activeObjective.Tiles.ToHashSet(),
                    out GenericActorDecision? supportAttack))
            {
                return supportAttack;
            }

            return FaceOrWait(
                context,
                activeObjective,
                "supporting pressure");
        }

        TeamBody self = new(
            context.Self.ActorId,
            context.Self.Position,
            context.Self.FormId);
        TeamBody[] mobileTeam = context.Allies
            .Select(ally =>
                new TeamBody(
                    ally.ActorId,
                    ally.Position,
                    ally.FormId))
            .Append(self)
            .Where(body =>
                ObjectiveWeight(contract, body.FormId) > 0)
            .OrderBy(body => body.ActorId)
            .ToArray();

        HashSet<Position> activeTiles =
            activeObjective.Tiles.ToHashSet();

        if (mobileTeam.Length == 1
            && TryParameterless(
                context,
                "split",
                "bootstrapping replication-led pressure",
                out GenericActorDecision? bootstrapSplit))
        {
            return bootstrapSplit;
        }

        if (TryFire(
                context,
                contract,
                activeTiles,
                out GenericActorDecision? attack))
        {
            return attack;
        }

        TeamBody lead = mobileTeam
            .OrderBy(body =>
                ShortestDistance(
                    contract.Map,
                    body.Position,
                    activeTiles))
            .ThenBy(body => body.ActorId)
            .First();
        bool selfIsLead = self.ActorId.Equals(lead.ActorId);
        bool allyOnObjective = mobileTeam.Any(body =>
            !body.ActorId.Equals(self.ActorId)
            && activeTiles.Contains(body.Position));
        bool enemyOnObjective = context.Enemies.Any(enemy =>
            activeTiles.Contains(enemy.Position));
        bool hostileClaim =
            mode.ClaimingTeamId is int claimingTeam
            && claimingTeam != context.Self.ActorId.TeamId;

        if (!activeTiles.Contains(context.Self.Position)
            && (allyOnObjective || !selfIsLead)
            && TryParameterless(
                context,
                "split",
                "splitting for immediate territorial pressure",
                out GenericActorDecision? split))
        {
            return split;
        }

        GenericActorMapContract.Region movementTarget = activeObjective;
        if (!selfIsLead
            && !enemyOnObjective
            && !hostileClaim
            && TryNextObjective(
                contract.Map,
                binding,
                mode.ActivePositionIndex,
                _advanceDelta,
                out GenericActorMapContract.Region? forwardObjective))
        {
            movementTarget = forwardObjective;
        }

        if (movementTarget.Tiles.Contains(context.Self.Position))
        {
            return FaceOrWait(
                context,
                ForwardReference(
                    contract.Map,
                    binding,
                    mode.ActivePositionIndex,
                    _advanceDelta) ?? movementTarget,
                selfIsLead
                    ? $"holding {movementTarget.RegionId}"
                    : $"staging at {movementTarget.RegionId}");
        }

        HashSet<Position> occupied = context.Allies
            .Select(ally => ally.Position)
            .Concat(context.Enemies.Select(enemy => enemy.Position))
            .Concat(
                context.VisibleProjectiles
                    ?.Select(projectile => projectile.Position)
                ?? [])
            .ToHashSet();
        if (TryMoveToward(
                context,
                contract.Map,
                movementTarget.Tiles.ToHashSet(),
                occupied,
                out GenericActorDecision? move))
        {
            return move;
        }

        return FaceOrWait(
            context,
            movementTarget,
            $"route to {movementTarget.RegionId} blocked");
    }

    private static bool TryFire(
        GenericActorContext context,
        GenericActorResolvedMatchContract contract,
        IReadOnlySet<Position> activeObjective,
        out GenericActorDecision? decision)
    {
        decision = null;
        if (context.Enemies.IsEmpty)
            return false;

        GenericActorContext.ObservedEnemyState[] targets =
            context.Enemies
                .OrderByDescending(enemy =>
                    activeObjective.Contains(enemy.Position))
                .ThenBy(enemy =>
                    context.Self.Position.ChebyshevDistance(
                        enemy.Position))
                .ThenBy(enemy => enemy.Health)
                .ThenBy(enemy => enemy.ActorId)
                .ToArray();

        GenericActorActionLegality? directional =
            context.Action("shoot-direction");
        if (directional is { Available: true })
        {
            GenericActorActionLegality.ArgumentConstraint
                .ProjectileHeadingConstraint? constraint =
                    directional.Constraints
                        .OfType<GenericActorActionLegality.ArgumentConstraint
                            .ProjectileHeadingConstraint>()
                        .SingleOrDefault();
            if (constraint is not null)
            {
                foreach (GenericActorContext.ObservedEnemyState target
                    in targets)
                {
                    if (TryAlignedHeading(
                            context.Self.Position,
                            target.Position,
                            out ProjectileHeading heading)
                        && constraint.AllowedValues.Contains(heading)
                        && InAttackRange(
                            contract,
                            context.Self.FormId,
                            context.Self.Position,
                            target.Position)
                        && ClearRay(
                            contract,
                            context.Self.FormId,
                            context.Self.Position,
                            target.Position))
                    {
                        decision = new GenericActorDecision(
                            directional.ActionId,
                            directional.ActionCode,
                            [
                                new GenericActorActionArgument
                                    .ProjectileHeadingArgument(heading),
                            ],
                            $"pressuring {target.ActorId} at {target.Position}");
                        return true;
                    }
                }
            }
        }

        GenericActorActionLegality? shoot = context.Action("shoot");
        if (shoot is not { Available: true })
            return false;

        GenericActorRulesContract.AttackProfile? attackProfile =
            AttackProfile(contract, context.Self.FormId);
        foreach (GenericActorContext.ObservedEnemyState target in targets)
        {
            if (!TryAlignedHeading(
                    context.Self.Position,
                    target.Position,
                    out ProjectileHeading targetHeading)
                || !InAttackRange(
                    contract,
                    context.Self.FormId,
                    context.Self.Position,
                    target.Position)
                || !ClearRay(
                    contract,
                    context.Self.FormId,
                    context.Self.Position,
                    target.Position))
            {
                continue;
            }

            ProjectileHeading facing =
                context.Self.Facing.ToProjectileHeading();
            int aimOffset = SignedHeadingDifference(facing, targetHeading);
            if (aimOffset == 0
                && ParameterlessIsValid(shoot))
            {
                decision = GenericActorDecision.WithoutArguments(
                    shoot.ActionId,
                    shoot.ActionCode,
                    $"clearing {target.ActorId} from {target.Position}");
                return true;
            }

            GenericActorActionLegality.ArgumentConstraint
                .ShotProgramConstraint? shotConstraint =
                    shoot.Constraints
                        .OfType<GenericActorActionLegality.ArgumentConstraint
                            .ShotProgramConstraint>()
                        .SingleOrDefault();
            GenericActorRulesContract.ShotProgramDefinition? programRules =
                attackProfile?.ShotProgram;
            if (shotConstraint is not { Allowed: true }
                || programRules is not { Enabled: true }
                || aimOffset < programRules.MinInitialAimSteps
                || aimOffset > programRules.MaxInitialAimSteps)
            {
                continue;
            }

            GenericActorRulesContract.AimOnlyShotProgramValue aimOnly =
                programRules.AimOnlyProgram;
            ShotProgram program = new(
                aimOffset,
                aimOnly.BendDirection,
                aimOnly.BendAfterTiles,
                aimOnly.BendEveryTiles,
                aimOnly.BendCount);
            decision = new GenericActorDecision(
                shoot.ActionId,
                shoot.ActionCode,
                [
                    new GenericActorActionArgument
                        .ShotProgramArgument(program),
                ],
                $"angled pressure on {target.ActorId} at {target.Position}");
            return true;
        }

        return false;
    }

    private static bool TryMoveToward(
        GenericActorContext context,
        GenericActorMapContract map,
        IReadOnlySet<Position> goals,
        IReadOnlySet<Position> occupied,
        out GenericActorDecision? decision)
    {
        decision = null;
        GenericActorActionLegality? move = context.Action("move");
        if (move is not { Available: true })
            return false;

        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            constraint = move.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        if (constraint is null || constraint.AllowedValues.IsEmpty)
            return false;

        Direction? first = FindFirstStep(
            map,
            context.Self.Position,
            goals,
            occupied,
            constraint.AllowedValues);
        if (first is not Direction direction)
            return false;

        decision = new GenericActorDecision(
            move.ActionId,
            move.ActionCode,
            [
                new GenericActorActionArgument.DirectionArgument(
                    direction),
            ],
            $"advancing {direction}");
        return true;
    }

    private static GenericActorDecision FaceOrWait(
        GenericActorContext context,
        GenericActorMapContract.Region target,
        string reason)
    {
        Position centre = new(
            (int)target.Tiles.Average(tile => tile.X),
            (int)target.Tiles.Average(tile => tile.Y));
        Direction desired = DirectionToward(
            context.Self.Position,
            centre,
            context.Self.Facing);
        if (desired != context.Self.Facing
            && TryDirection(
                context,
                "rotate",
                desired,
                $"{reason}; facing {desired}",
                out GenericActorDecision? rotate))
        {
            return rotate;
        }
        return Fallback(context, reason);
    }

    private static bool TryDirection(
        GenericActorContext context,
        string actionId,
        Direction direction,
        string debug,
        out GenericActorDecision? decision)
    {
        decision = null;
        GenericActorActionLegality? action = context.Action(actionId);
        if (action is not { Available: true })
            return false;
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            constraint = action.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        if (constraint is null
            || !constraint.AllowedValues.Contains(direction))
        {
            return false;
        }
        decision = new GenericActorDecision(
            action.ActionId,
            action.ActionCode,
            [
                new GenericActorActionArgument.DirectionArgument(
                    direction),
            ],
            debug);
        return true;
    }

    private static bool TryParameterless(
        GenericActorContext context,
        string actionId,
        string debug,
        out GenericActorDecision? decision)
    {
        decision = null;
        GenericActorActionLegality? action = context.Action(actionId);
        if (action is not { Available: true }
            || !ParameterlessIsValid(action))
        {
            return false;
        }
        decision = GenericActorDecision.WithoutArguments(
            action.ActionId,
            action.ActionCode,
            debug);
        return true;
    }

    private static bool ParameterlessIsValid(
        GenericActorActionLegality action) =>
        action.Constraints.All(constraint =>
            constraint is GenericActorActionLegality.ArgumentConstraint
                .ShotProgramConstraint);

    private static GenericActorDecision Fallback(
        GenericActorContext context,
        string reason)
    {
        if (TryParameterless(
                context,
                "wait",
                reason,
                out GenericActorDecision? wait))
        {
            return wait;
        }

        foreach (GenericActorActionLegality action
            in context.ActionLegalities
                .Where(action => action.Available)
                .OrderBy(action => action.ActionId, StringComparer.Ordinal))
        {
            var arguments = new List<GenericActorActionArgument>();
            bool valid = true;
            foreach (GenericActorActionLegality.ArgumentConstraint constraint
                in action.Constraints)
            {
                switch (constraint)
                {
                    case GenericActorActionLegality.ArgumentConstraint
                        .ShotProgramConstraint:
                        break;
                    case GenericActorActionLegality.ArgumentConstraint
                        .DirectionConstraint direction
                        when !direction.AllowedValues.IsEmpty:
                        arguments.Add(
                            new GenericActorActionArgument.DirectionArgument(
                                direction.AllowedValues[0]));
                        break;
                    case GenericActorActionLegality.ArgumentConstraint
                        .UnitTargetConstraint unit
                        when !unit.AllowedValues.IsEmpty:
                        arguments.Add(
                            new GenericActorActionArgument.UnitTargetArgument(
                                unit.AllowedValues[0]));
                        break;
                    case GenericActorActionLegality.ArgumentConstraint
                        .FormTargetConstraint form
                        when !form.AllowedFormIds.IsEmpty:
                        arguments.Add(
                            new GenericActorActionArgument.FormTargetArgument(
                                form.AllowedFormIds[0]));
                        break;
                    case GenericActorActionLegality.ArgumentConstraint
                        .ProjectileHeadingConstraint heading
                        when !heading.AllowedValues.IsEmpty:
                        arguments.Add(
                            new GenericActorActionArgument
                                .ProjectileHeadingArgument(
                                    heading.AllowedValues[0]));
                        break;
                    default:
                        valid = false;
                        break;
                }
                if (!valid)
                    break;
            }
            if (valid)
            {
                return new GenericActorDecision(
                    action.ActionId,
                    action.ActionCode,
                    arguments,
                    $"{reason}; deterministic legal fallback");
            }
        }

        GenericActorActionLegality lastResort =
            context.ActionLegalities
                .OrderBy(action => action.ActionId, StringComparer.Ordinal)
                .First();
        return GenericActorDecision.WithoutArguments(
            lastResort.ActionId,
            lastResort.ActionCode,
            $"{reason}; no available action");
    }

    private static GenericActorMapContract.Region? FindRegion(
        GenericActorMapContract map,
        string regionId) =>
        map.Regions.FirstOrDefault(region =>
            string.Equals(
                region.RegionId,
                regionId,
                StringComparison.Ordinal));

    private static bool TryNextObjective(
        GenericActorMapContract map,
        GenericActorResolvedMatchContract.FrontlineModeMapBinding binding,
        int activeIndex,
        int advanceDelta,
        out GenericActorMapContract.Region? region)
    {
        region = null;
        int next = activeIndex + Math.Sign(advanceDelta);
        if (advanceDelta == 0
            || next < 0
            || next >= binding.OrderedObjectiveRegionIds.Length)
        {
            return false;
        }
        region = FindRegion(
            map,
            binding.OrderedObjectiveRegionIds[next]);
        return region is not null && !region.Tiles.IsEmpty;
    }

    private static GenericActorMapContract.Region? ForwardReference(
        GenericActorMapContract map,
        GenericActorResolvedMatchContract.FrontlineModeMapBinding binding,
        int activeIndex,
        int advanceDelta) =>
        TryNextObjective(
            map,
            binding,
            activeIndex,
            advanceDelta,
            out GenericActorMapContract.Region? next)
            ? next
            : null;

    private static int ObjectiveWeight(
        GenericActorResolvedMatchContract contract,
        string formId) =>
        contract.Rules.Forms
            .FirstOrDefault(form =>
                string.Equals(form.Id, formId, StringComparison.Ordinal))
            ?.ObjectiveWeight ?? 0;

    private static GenericActorRulesContract.AttackProfile? AttackProfile(
        GenericActorResolvedMatchContract contract,
        string formId)
    {
        string? attackId = contract.Rules.Forms
            .FirstOrDefault(form =>
                string.Equals(form.Id, formId, StringComparison.Ordinal))
            ?.AttackProfileId;
        return attackId is null
            ? null
            : contract.Rules.AttackProfiles.FirstOrDefault(profile =>
                string.Equals(
                    profile.Id,
                    attackId,
                    StringComparison.Ordinal));
    }

    private static bool InAttackRange(
        GenericActorResolvedMatchContract contract,
        string formId,
        Position from,
        Position target)
    {
        GenericActorRulesContract.AttackProfile? profile =
            AttackProfile(contract, formId);
        return profile is not null
            && from.ChebyshevDistance(target)
                <= profile.Projectile.MaxTravelTiles;
    }

    private static bool ClearRay(
        GenericActorResolvedMatchContract contract,
        string formId,
        Position from,
        Position target)
    {
        if (!TryAlignedHeading(from, target, out ProjectileHeading heading))
            return false;
        var (dx, dy) = heading.Vector();
        Position current = from.Offset(dx, dy);
        bool strictCorners = AttackProfile(contract, formId)
            ?.Projectile.DiagonalCornersMustBeClear ?? true;
        while (current != target)
        {
            if (IsWall(contract.Map, current))
                return false;
            if (strictCorners && dx != 0 && dy != 0)
            {
                Position prior = current.Offset(-dx, -dy);
                if (IsWall(contract.Map, prior.Offset(dx, 0))
                    || IsWall(contract.Map, prior.Offset(0, dy)))
                {
                    return false;
                }
            }
            current = current.Offset(dx, dy);
        }
        return !IsWall(contract.Map, target);
    }

    private static bool TryAlignedHeading(
        Position from,
        Position target,
        out ProjectileHeading heading)
    {
        int rawDx = target.X - from.X;
        int rawDy = target.Y - from.Y;
        int dx = Math.Sign(rawDx);
        int dy = Math.Sign(rawDy);
        bool aligned = rawDx == 0
            || rawDy == 0
            || Math.Abs(rawDx) == Math.Abs(rawDy);
        heading = (dx, dy) switch
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
        return aligned && (rawDx != 0 || rawDy != 0);
    }

    private static int SignedHeadingDifference(
        ProjectileHeading from,
        ProjectileHeading to)
    {
        int difference = (int)to - (int)from;
        while (difference > 4)
            difference -= 8;
        while (difference < -4)
            difference += 8;
        return difference;
    }

    private static Direction? FindFirstStep(
        GenericActorMapContract map,
        Position start,
        IReadOnlySet<Position> goals,
        IReadOnlySet<Position> occupied,
        IReadOnlyCollection<Direction> allowedFirstSteps)
    {
        if (goals.Contains(start))
            return null;

        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Position, Direction First)>();
        foreach (Direction direction in allowedFirstSteps.Order())
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

    private static int ShortestDistance(
        GenericActorMapContract map,
        Position start,
        IReadOnlySet<Position> goals)
    {
        if (goals.Contains(start))
            return 0;
        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Position, int Distance)>();
        queue.Enqueue((start, 0));
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (Direction direction in Enum.GetValues<Direction>())
            {
                Position next = Offset(current.Position, direction);
                if (!CanEnter(map, next, EmptyPositions.Instance)
                    || !visited.Add(next))
                {
                    continue;
                }
                if (goals.Contains(next))
                    return current.Distance + 1;
                queue.Enqueue((next, current.Distance + 1));
            }
        }
        return int.MaxValue;
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

    private static bool IsWall(
        GenericActorMapContract map,
        Position position) =>
        position.X < 0
        || position.Y < 0
        || position.X >= map.Width
        || position.Y >= map.Height
        || map.TileRows[position.Y][position.X] == '#';

    private static Position Offset(
        Position position,
        Direction direction)
    {
        var (dx, dy) = direction.Vector();
        return position.Offset(dx, dy);
    }

    private static Direction DirectionToward(
        Position from,
        Position target,
        Direction fallback)
    {
        int dx = target.X - from.X;
        int dy = target.Y - from.Y;
        if (Math.Abs(dx) >= Math.Abs(dy) && dx != 0)
            return dx > 0 ? Direction.East : Direction.West;
        if (dy != 0)
            return dy > 0 ? Direction.South : Direction.North;
        return fallback;
    }

    private readonly record struct TeamBody(
        ActorIdentity ActorId,
        Position Position,
        string FormId);

    private sealed class EmptyPositions : IReadOnlySet<Position>
    {
        public static EmptyPositions Instance { get; } = new();
        public int Count => 0;
        public bool Contains(Position item) => false;
        public IEnumerator<Position> GetEnumerator() =>
            Enumerable.Empty<Position>().GetEnumerator();
        System.Collections.IEnumerator
            System.Collections.IEnumerable.GetEnumerator() =>
                GetEnumerator();
        public bool IsProperSubsetOf(IEnumerable<Position> other) => true;
        public bool IsProperSupersetOf(IEnumerable<Position> other) => false;
        public bool IsSubsetOf(IEnumerable<Position> other) => true;
        public bool IsSupersetOf(IEnumerable<Position> other) =>
            !other.Any();
        public bool Overlaps(IEnumerable<Position> other) => false;
        public bool SetEquals(IEnumerable<Position> other) =>
            !other.Any();
    }
}
