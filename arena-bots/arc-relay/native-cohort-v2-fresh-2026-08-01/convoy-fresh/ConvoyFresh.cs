using BotArena.Sdk;

/// <summary>
/// A participant-scoped Arc Relay convoy.  Four chassis form a strictly
/// one-way catch ladder from the primary Well to the home reactor, three
/// chassis screen that ladder, and one swift picket contests the other Wells.
/// </summary>
public sealed class ConvoyFresh : IGenericMindBot
{
    private static readonly ProjectileHeading[] Headings =
    [
        ProjectileHeading.North,
        ProjectileHeading.NorthEast,
        ProjectileHeading.East,
        ProjectileHeading.SouthEast,
        ProjectileHeading.South,
        ProjectileHeading.SouthWest,
        ProjectileHeading.West,
        ProjectileHeading.NorthWest,
    ];

    private readonly HashSet<string> _submittedHandoffs = [];
    private GenericActorResolvedMatchContract? _contract;
    private int _participantId;
    private int _teamId;
    private Position _homeReactor;
    private string _primaryWellId = string.Empty;
    private Position[] _homeToWellRoute = [];

    public void StartMatch(MindStart start)
    {
        _contract = start.Contract;
        _participantId = start.ParticipantId;
        _teamId = start.TeamId;

        GenericActorResolvedMatchContract.ArcRelayModeMapBinding binding =
            start.Contract.ModeMapBinding
                as GenericActorResolvedMatchContract.ArcRelayModeMapBinding
            ?? throw new InvalidOperationException(
                "ConvoyFresh requires the Arc Relay mode-map binding.");

        GenericActorResolvedMatchContract.ParticipantRegionAssignment?
            reactorAssignment = start.Contract.ParticipantRegionAssignments
                .FirstOrDefault(assignment =>
                    assignment.ParticipantId == start.ParticipantId
                    && string.Equals(
                        assignment.RegionRoleId,
                        binding.ReactorRegionRoleId,
                        StringComparison.Ordinal));
        GenericActorMapContract.Region? reactorRegion = reactorAssignment is null
            ? null
            : start.Contract.Map.Regions.FirstOrDefault(region =>
                string.Equals(
                    region.RegionId,
                    reactorAssignment.MapRegionId,
                    StringComparison.Ordinal));
        if (reactorRegion is null || reactorRegion.Tiles.IsEmpty)
        {
            throw new InvalidOperationException(
                "Arc Relay contract did not bind an own-reactor tile.");
        }

        _homeReactor = reactorRegion.Tiles[0];
        _primaryWellId = (start.Contract.Rules.GameMode
                as GenericActorRulesContract.ArcRelayGameMode)
            ?.Wells
            .OrderBy(well => well.FirstBirthTick)
            .ThenBy(well => well.WellId, StringComparer.Ordinal)
            .Select(well => well.WellId)
            .FirstOrDefault()
            ?? string.Empty;
    }

    public void Think(MindContext mind)
    {
        GenericActorResolvedMatchContract contract = _contract
            ?? throw new InvalidOperationException("StartMatch was not called.");
        if (mind.Mode is not GenericActorContext.ModeObservationState.ArcRelay arc)
        {
            foreach (MindBody body in mind.Bodies)
                body.Hold("waiting for Arc Relay state");
            return;
        }

        GenericActorContext.ArcRelayWellState? primary = arc.Wells
            .FirstOrDefault(well => string.Equals(
                well.WellId,
                _primaryWellId,
                StringComparison.Ordinal));
        if (primary is null)
        {
            foreach (MindBody body in mind.Bodies)
                body.Hold("primary Well unavailable");
            return;
        }

        if (_homeToWellRoute.Length == 0)
        {
            int preferredLaneY = Math.Max(
                1,
                Math.Min(contract.Map.Height - 2, primary.Position.Y - 2));
            _homeToWellRoute = FindRoute(
                contract.Map,
                _homeReactor,
                primary.Position,
                preferredLaneY);
        }

        foreach (MindBody body in mind.Bodies)
            body.SetRole(RoleTag(body));

        var claims = new TrafficClaims(contract, mind, _teamId);
        Dictionary<ActorIdentity, GenericActorContext.ArcRelayCoreState>
            carried = arc.VisibleCores
                .Where(core =>
                    core.Disposition
                        == GenericActorContext.ArcRelayCoreDisposition.Carried
                    && core.CarrierActorId is not null)
                .ToDictionary(core => core.CarrierActorId!, core => core);

        PlanOneWayHandoffs(contract, mind, carried);

        GenericActorContext.ObservedEnemyState? enemyCarrier =
            VisibleEnemyCarrier(mind, arc);
        foreach (MindBody body in mind.Bodies
                     .OrderByDescending(body => carried.ContainsKey(body.ActorId))
                     .ThenBy(body => StageRank(body))
                     .ThenBy(body => body.UnitId))
        {
            if (body.HasCommand)
                continue;

            if (carried.TryGetValue(body.ActorId, out var core))
            {
                DriveCarrier(
                    contract,
                    mind,
                    arc,
                    body,
                    core,
                    claims);
                continue;
            }

            switch (body.ClassId)
            {
                case "relay":
                    DrivePickupRunner(
                        contract,
                        mind,
                        arc,
                        body,
                        primary,
                        claims);
                    break;
                case "repulsor":
                case "palisade":
                case "patchbay":
                    HoldCatchStation(
                        contract,
                        mind,
                        arc,
                        body,
                        enemyCarrier,
                        claims);
                    break;
                case "lantern":
                    ContestFarWells(
                        contract,
                        mind,
                        arc,
                        body,
                        enemyCarrier,
                        claims);
                    break;
                default:
                    ScreenConvoy(
                        contract,
                        mind,
                        arc,
                        body,
                        enemyCarrier,
                        carried.Keys,
                        claims);
                    break;
            }
        }

        foreach (MindBody body in mind.Bodies.Where(body => !body.HasCommand))
            body.Hold("formation settled");
    }

    public void EndMatch(MindEnd end) => _ = end;

    private void PlanOneWayHandoffs(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        foreach ((ActorIdentity actorId, GenericActorContext.ArcRelayCoreState core)
                 in carried.OrderBy(pair => pair.Key))
        {
            MindBody? source = mind.Bodies.FirstOrDefault(body =>
                body.ActorId == actorId);
            if (source is null || source.HasCommand)
                continue;

            int sourceRank = StageRank(source);
            if (sourceRank is < 0 or >= 3)
                continue;

            GenericActorActionLegality? handoff = ActionOfKind(
                contract,
                source,
                GenericActorRulesContract.ActionKind.Objective,
                "handoff-core");
            var targets = handoff?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint>()
                .SingleOrDefault();
            if (handoff is null || !handoff.Available || targets is null)
                continue;

            foreach (MindBody receiver in mind.Bodies
                         .Where(body =>
                             !body.HasCommand
                             && StageRank(body) > sourceRank
                             && !carried.ContainsKey(body.ActorId)
                             && body.Position.ChebyshevDistance(source.Position) == 1
                             && body.Position.ChebyshevDistance(_homeReactor)
                                < source.Position.ChebyshevDistance(_homeReactor))
                         .OrderBy(StageRank)
                         .ThenByDescending(body => body.Health)
                         .ThenBy(body => body.UnitId))
            {
                GenericActorActionArgument.UnitTarget? target =
                    targets.AllowedValues
                        .Where(value =>
                            value.TeamId == _teamId
                            && value.UnitId == receiver.UnitId)
                        .Select(value =>
                            (GenericActorActionArgument.UnitTarget?)value)
                        .FirstOrDefault();
                if (target is null)
                    continue;

                string handoffKey = CoreKey(core)
                    + $"/{source.UnitId}>{receiver.UnitId}";
                if (!_submittedHandoffs.Add(handoffKey))
                    continue;

                receiver.Hold("catching one-way handoff");
                source.Command(
                    handoff,
                    new GenericActorActionArgument.UnitTargetArgument(
                        target.Value));
                break;
            }
        }
    }

    private void DriveCarrier(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        GenericActorContext.ArcRelayCoreState core,
        TrafficClaims claims)
    {
        if (body.Position == _homeReactor)
        {
            body.Hold("banking at reactor");
            return;
        }

        if (body.ClassId == "relay"
            && body.Health == 1
            && core.NextRelocationTick <= mind.Tick
            && TryEmergencyArcToss(contract, body, claims))
        {
            return;
        }

        int rank = StageRank(body);
        MindBody? next = rank >= 0
            ? mind.Bodies
                .Where(candidate =>
                    StageRank(candidate) > rank
                    && candidate.Position.ChebyshevDistance(_homeReactor)
                        < body.Position.ChebyshevDistance(_homeReactor))
                .OrderBy(StageRank)
                .ThenBy(candidate => candidate.UnitId)
                .FirstOrDefault()
            : null;

        Position[] goals = next is null
            ? [_homeReactor]
            : AdjacentGoals(contract.Map, next.Position)
                .Where(position =>
                    position.ChebyshevDistance(_homeReactor)
                        <= body.Position.ChebyshevDistance(_homeReactor))
                .ToArray();
        if (goals.Length == 0)
            goals = [_homeReactor];

        if (!TryMoveToward(
                contract,
                mind,
                body,
                goals,
                claims,
                next is null ? "carrying home" : "closing on catcher"))
        {
            body.Hold(core.NextRelocationTick > mind.Tick
                ? "Core relocation recovering"
                : "holding protected Core");
        }
    }

    private void DrivePickupRunner(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        GenericActorContext.ArcRelayWellState primary,
        TrafficClaims claims)
    {
        GenericActorContext.ArcRelayCoreState? loosePrimary = arc.VisibleCores
            .Where(core =>
                core.Disposition
                    == GenericActorContext.ArcRelayCoreDisposition.Loose
                && string.Equals(
                    core.CoreId.SourceWellId,
                    primary.WellId,
                    StringComparison.Ordinal))
            .OrderBy(core => body.Position.ChebyshevDistance(core.Position))
            .FirstOrDefault();
        Position target = loosePrimary?.Position ?? primary.Position;
        if (body.Position != target
            && TryMoveToward(
                contract,
                mind,
                body,
                [target],
                claims,
                "collecting primary Core"))
        {
            return;
        }

        GenericActorContext.ObservedEnemyState? threat = mind.Enemies
            .OrderBy(enemy => enemy.Position.ChebyshevDistance(target))
            .ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault();
        if (TryShoot(contract, mind, body, threat))
            return;
        body.Hold("guarding primary pickup");
    }

    private void HoldCatchStation(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        GenericActorContext.ObservedEnemyState? enemyCarrier,
        TrafficClaims claims)
    {
        Position station = StageStation(body);
        if (body.Position.ChebyshevDistance(station) > 1
            && TryMoveToward(
                contract,
                mind,
                body,
                [station],
                claims,
                "taking catch station"))
        {
            return;
        }

        if (TryClassSignature(
                contract,
                mind,
                arc,
                body,
                enemyCarrier,
                claims))
        {
            return;
        }
        if (TryShoot(contract, mind, body, enemyCarrier))
            return;
        body.Hold("ready to catch");
    }

    private void ScreenConvoy(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        GenericActorContext.ObservedEnemyState? enemyCarrier,
        IEnumerable<ActorIdentity> friendlyCarrierIds,
        TrafficClaims claims)
    {
        MindBody? convoyCarrier = friendlyCarrierIds
            .Select(id => mind.Bodies.FirstOrDefault(candidate =>
                candidate.ActorId == id))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .OrderBy(candidate =>
                candidate.Position.ChebyshevDistance(_homeReactor))
            .FirstOrDefault()
            ?? mind.Bodies.FirstOrDefault(candidate =>
                candidate.ClassId == "relay");

        if (TryClassSignature(
                contract,
                mind,
                arc,
                body,
                enemyCarrier,
                claims))
        {
            return;
        }
        if (TryShoot(contract, mind, body, enemyCarrier))
            return;

        Position[] goals;
        if (enemyCarrier is not null
            && body.Position.ChebyshevDistance(enemyCarrier.Position) <= 8)
        {
            goals = AdjacentGoals(contract.Map, enemyCarrier.Position);
        }
        else if (convoyCarrier is not null)
        {
            goals = AdjacentGoals(contract.Map, convoyCarrier.Position);
        }
        else
        {
            goals = [StageAtFraction(0.58)];
        }

        if (TryMoveToward(
                contract,
                mind,
                body,
                goals,
                claims,
                enemyCarrier is null ? "screening convoy" : "cutting carrier"))
        {
            return;
        }
        if (TryShoot(contract, mind, body))
            return;
        body.Hold("holding screen");
    }

    private void ContestFarWells(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        GenericActorContext.ObservedEnemyState? enemyCarrier,
        TrafficClaims claims)
    {
        GenericActorContext.ArcRelayWellState[] far = arc.Wells
            .Where(well => !string.Equals(
                well.WellId,
                _primaryWellId,
                StringComparison.Ordinal))
            .ToArray();
        GenericActorContext.ArcRelayWellState? targetWell = far
            .OrderByDescending(well => well.OutstandingCoreId is not null)
            .ThenBy(well => well.NextScheduledBirthTick ?? int.MaxValue)
            .ThenBy(well => body.Position.ChebyshevDistance(well.Position))
            .ThenBy(well => well.WellId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (targetWell is null)
        {
            body.Hold("no far Well");
            return;
        }

        GenericActorContext.ArcRelayCoreState? loose = arc.VisibleCores
            .Where(core =>
                core.Disposition
                    == GenericActorContext.ArcRelayCoreDisposition.Loose
                && string.Equals(
                    core.CoreId.SourceWellId,
                    targetWell.WellId,
                    StringComparison.Ordinal))
            .OrderBy(core => body.Position.ChebyshevDistance(core.Position))
            .FirstOrDefault();
        Position goal = loose?.Position ?? targetWell.Position;
        if (body.Position.ChebyshevDistance(goal) > 1
            && TryMoveToward(
                contract,
                mind,
                body,
                [goal],
                claims,
                "contesting far Well"))
        {
            return;
        }

        if (TryClassSignature(
                contract,
                mind,
                arc,
                body,
                enemyCarrier,
                claims))
        {
            return;
        }
        if (TryShoot(contract, mind, body, enemyCarrier))
            return;
        body.Hold("picketing far Well");
    }

    private bool TryClassSignature(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        GenericActorContext.ObservedEnemyState? enemyCarrier,
        TrafficClaims claims)
    {
        GenericActorRulesContract.ArcRelayGameMode? mode =
            contract.Rules.GameMode
                as GenericActorRulesContract.ArcRelayGameMode;
        GenericActorRulesContract.ArcRelaySignature? signature = mode?.Signatures
            .FirstOrDefault(candidate => string.Equals(
                candidate.ClassId,
                body.ClassId,
                StringComparison.Ordinal));
        GenericActorActionLegality? action = signature is null
            ? null
            : body.Action(signature.ActionId);
        if (signature is null || action is null || !action.Available)
            return false;

        switch (signature.Kind)
        {
            case "null-field":
                if (mind.Enemies.Count(enemy =>
                        enemy.Position.ChebyshevDistance(body.Position) <= 3) >= 2
                    || enemyCarrier is not null
                       && enemyCarrier.Position.ChebyshevDistance(body.Position) <= 3)
                {
                    body.Command(action);
                    return true;
                }
                break;

            case "tractor-hook":
                if (enemyCarrier is not null
                    && TryHeadingArgument(action, body.Position,
                        enemyCarrier.Position, out var hookHeading))
                {
                    body.Command(action, hookHeading);
                    return true;
                }
                break;

            case "target-paint":
                GenericActorContext.ObservedEnemyState? paintTarget =
                    enemyCarrier
                    ?? mind.Enemies
                        .OrderBy(enemy => enemy.Health)
                        .ThenBy(enemy => enemy.ActorId)
                        .FirstOrDefault();
                if (paintTarget is not null
                    && TryUnitTargetArgument(
                        action,
                        paintTarget.ActorId,
                        out var paintArgument))
                {
                    body.Command(action, paintArgument);
                    return true;
                }
                break;

            case "repair-beam":
                MindBody? repairTarget = mind.Bodies
                    .Where(candidate => candidate.ActorId != body.ActorId)
                    .Where(candidate => candidate.Health < MaxHealth(
                        contract,
                        candidate.FormId))
                    .OrderBy(candidate => candidate.Health)
                    .ThenBy(candidate => candidate.UnitId)
                    .FirstOrDefault(candidate => TryUnitTargetArgument(
                        action,
                        candidate.ActorId,
                        out _));
                if (repairTarget is not null
                    && TryUnitTargetArgument(
                        action,
                        repairTarget.ActorId,
                        out var repairArgument))
                {
                    body.Command(action, repairArgument);
                    return true;
                }
                break;

            case "prism-wall":
                MindBody? carrier = arc.VisibleCores
                    .Where(core => core.CarrierActorId?.TeamId == _teamId)
                    .Select(core => mind.Bodies.FirstOrDefault(candidate =>
                        candidate.ActorId == core.CarrierActorId))
                    .FirstOrDefault(candidate => candidate is not null);
                GenericActorContext.ObservedEnemyState? wallThreat =
                    mind.Enemies
                        .OrderBy(enemy =>
                            enemy.Position.ChebyshevDistance(body.Position))
                        .ThenBy(enemy => enemy.ActorId)
                        .FirstOrDefault();
                if (carrier is not null
                    && wallThreat is not null
                    && carrier.Position.ChebyshevDistance(body.Position) <= 4
                    && wallThreat.Position.ChebyshevDistance(body.Position) <= 7
                    && TryDirectionArgument(
                        action,
                        CardinalToward(body.Position, wallThreat.Position),
                        out var wallDirection))
                {
                    body.Command(action, wallDirection);
                    return true;
                }
                break;

            case "kinetic-burst":
                bool adjacentEnemy = mind.Enemies.Any(enemy =>
                    enemy.Position.ChebyshevDistance(body.Position) == 1);
                bool adjacentAlly = mind.Bodies.Any(candidate =>
                    candidate.ActorId != body.ActorId
                    && candidate.Position.ChebyshevDistance(body.Position) == 1);
                if (adjacentEnemy && !adjacentAlly)
                {
                    body.Command(action);
                    return true;
                }
                break;

            case "survey-flare":
                GenericActorContext.ArcRelayWellState? farSoon = arc.Wells
                    .Where(well => !string.Equals(
                        well.WellId,
                        _primaryWellId,
                        StringComparison.Ordinal))
                    .OrderBy(well => well.NextScheduledBirthTick ?? int.MaxValue)
                    .FirstOrDefault();
                if (farSoon is not null
                    && (farSoon.OutstandingCoreId is not null
                        || farSoon.NextScheduledBirthTick is int birth
                           && birth - mind.Tick <= 10)
                    && TryPositionTargetArgument(
                        action,
                        farSoon.Position,
                        claims,
                        out var flareTarget))
                {
                    body.Command(action, flareTarget);
                    return true;
                }
                break;
        }

        return false;
    }

    private bool TryEmergencyArcToss(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        TrafficClaims claims)
    {
        GenericActorRulesContract.ArcRelaySignature? signature =
            (contract.Rules.GameMode
                as GenericActorRulesContract.ArcRelayGameMode)
            ?.Signatures.FirstOrDefault(candidate =>
                candidate.Kind == "arc-toss"
                && candidate.ClassId == body.ClassId);
        GenericActorActionLegality? action = signature is null
            ? null
            : body.Action(signature.ActionId);
        if (action is null || !action.Available)
            return false;

        var positions = action.Constraints
            .OfType<GenericActorActionLegality.ArgumentConstraint
                .PositionTargetConstraint>()
            .SingleOrDefault();
        Position? landing = positions?.AllowedValues
            .Where(position => !claims.IsBlocked(position))
            .Where(position => position.ChebyshevDistance(_homeReactor)
                + 1 < body.Position.ChebyshevDistance(_homeReactor))
            .OrderBy(position => position.ChebyshevDistance(_homeReactor))
            .ThenBy(position => position.Y)
            .ThenBy(position => position.X)
            .Select(position => (Position?)position)
            .FirstOrDefault();
        if (landing is not Position target)
            return false;

        body.Command(
            action,
            new GenericActorActionArgument.PositionTargetArgument(target));
        return true;
    }

    private bool TryMoveToward(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        IReadOnlyCollection<Position> goals,
        TrafficClaims claims,
        string reason)
    {
        if (goals.Count == 0 || goals.Contains(body.Position))
            return false;

        GenericActorActionLegality? move = ActionOfKind(
            contract,
            body,
            GenericActorRulesContract.ActionKind.Movement);
        var headings = move?.Constraints
            .OfType<GenericActorActionLegality.ArgumentConstraint
                .ProjectileHeadingConstraint>()
            .SingleOrDefault();
        if (move is not null && move.Available && headings is not null)
        {
            ProjectileHeading? chosen = headings.AllowedValues
                .Select(heading =>
                {
                    (int dx, int dy) = heading.Vector();
                    Position destination = body.Position.Offset(dx, dy);
                    return (Heading: heading, Destination: destination);
                })
                .Where(candidate => CanStep(
                    contract.Map,
                    body.Position,
                    candidate.Destination,
                    claims))
                .OrderBy(candidate => RouteDistance(
                    contract.Map,
                    candidate.Destination,
                    goals))
                .ThenBy(candidate => goals.Min(goal =>
                    candidate.Destination.ChebyshevDistance(goal)))
                .ThenBy(candidate => candidate.Heading)
                .Select(candidate => (ProjectileHeading?)candidate.Heading)
                .FirstOrDefault();
            if (chosen is ProjectileHeading heading)
            {
                (int dx, int dy) = heading.Vector();
                Position destination = body.Position.Offset(dx, dy);
                claims.Reserve(destination);
                body.Command(
                    move,
                    new GenericActorActionArgument.ProjectileHeadingArgument(
                        heading));
                mind.Debug.Write($"{body.UnitId}:{reason}:{destination}");
                return true;
            }
        }

        GenericActorActionLegality? rotate = ActionOfKind(
            contract,
            body,
            GenericActorRulesContract.ActionKind.Rotation);
        if (rotate is null || !rotate.Available)
            return false;

        Position nearest = goals
            .OrderBy(goal => body.Position.ChebyshevDistance(goal))
            .ThenBy(goal => goal.Y)
            .ThenBy(goal => goal.X)
            .First();
        Direction desired = CardinalToward(body.Position, nearest);
        if (!TryDirectionArgument(rotate, desired, out var direction))
            return false;
        body.Command(rotate, direction);
        return true;
    }

    private static bool TryShoot(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        GenericActorContext.ObservedEnemyState? preferred = null)
    {
        GenericActorActionLegality? attack = ActionOfKind(
            contract,
            body,
            GenericActorRulesContract.ActionKind.Attack);
        if (attack is null || !attack.Available)
            return false;

        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(candidate => candidate.Id == body.FormId);
        GenericActorRulesContract.AttackProfile? profile =
            form?.AttackProfileId is string attackId
                ? contract.Rules.AttackProfiles.FirstOrDefault(candidate =>
                    candidate.Id == attackId)
                : null;
        if (profile is null)
            return false;

        IEnumerable<GenericActorContext.ObservedEnemyState> targets =
            preferred is null
                ? mind.Enemies
                    .OrderBy(enemy => enemy.Health)
                    .ThenBy(enemy => enemy.ActorId)
                : [preferred, .. mind.Enemies.Where(enemy =>
                    enemy.ActorId != preferred.ActorId)
                    .OrderBy(enemy => enemy.Health)
                    .ThenBy(enemy => enemy.ActorId)];
        foreach (var target in targets)
        {
            if (body.Position.ChebyshevDistance(target.Position)
                > profile.Projectile.MaxTravelTiles)
            {
                continue;
            }
            if (!TryHeadingArgument(
                    attack,
                    body.Position,
                    target.Position,
                    out var heading))
            {
                continue;
            }
            if (!ClearLine(contract.Map, body.Position, target.Position))
                continue;
            body.Command(attack, heading);
            return true;
        }
        return false;
    }

    private static GenericActorActionLegality? ActionOfKind(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        GenericActorRulesContract.ActionKind kind,
        string? preferredId = null)
    {
        HashSet<string> ids = contract.Rules.Actions
            .Where(action => action.Kind == kind)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        return body.ActionLegalities
            .Where(action => ids.Contains(action.ActionId))
            .OrderByDescending(action => preferredId is not null
                && action.ActionId == preferredId)
            .ThenBy(action => action.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool TryHeadingArgument(
        GenericActorActionLegality action,
        Position from,
        Position to,
        out GenericActorActionArgument.ProjectileHeadingArgument argument)
    {
        argument = null!;
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        if (dx == 0 && dy == 0)
            return false;
        if (dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy))
            return false;
        ProjectileHeading heading = HeadingFromSigns(dx, dy);
        var constraint = action.Constraints
            .OfType<GenericActorActionLegality.ArgumentConstraint
                .ProjectileHeadingConstraint>()
            .SingleOrDefault();
        if (constraint is null || !constraint.AllowedValues.Contains(heading))
            return false;
        argument = new GenericActorActionArgument.ProjectileHeadingArgument(
            heading);
        return true;
    }

    private static bool TryDirectionArgument(
        GenericActorActionLegality action,
        Direction desired,
        out GenericActorActionArgument.DirectionArgument argument)
    {
        argument = null!;
        var constraint = action.Constraints
            .OfType<GenericActorActionLegality.ArgumentConstraint
                .DirectionConstraint>()
            .SingleOrDefault();
        if (constraint is null || !constraint.AllowedValues.Contains(desired))
            return false;
        argument = new GenericActorActionArgument.DirectionArgument(desired);
        return true;
    }

    private static bool TryUnitTargetArgument(
        GenericActorActionLegality action,
        ActorIdentity target,
        out GenericActorActionArgument.UnitTargetArgument argument)
    {
        argument = null!;
        var constraint = action.Constraints
            .OfType<GenericActorActionLegality.ArgumentConstraint
                .UnitTargetConstraint>()
            .SingleOrDefault();
        GenericActorActionArgument.UnitTarget? allowed = constraint is null
            ? null
            : constraint.AllowedValues
                .Where(value =>
                    value.TeamId == target.TeamId
                    && value.UnitId == target.UnitId)
                .Select(value =>
                    (GenericActorActionArgument.UnitTarget?)value)
                .FirstOrDefault();
        if (allowed is null)
            return false;
        argument = new GenericActorActionArgument.UnitTargetArgument(
            allowed.Value);
        return true;
    }

    private static bool TryPositionTargetArgument(
        GenericActorActionLegality action,
        Position preferred,
        TrafficClaims claims,
        out GenericActorActionArgument.PositionTargetArgument argument)
    {
        argument = null!;
        var constraint = action.Constraints
            .OfType<GenericActorActionLegality.ArgumentConstraint
                .PositionTargetConstraint>()
            .SingleOrDefault();
        Position? chosen = constraint?.AllowedValues
            .Where(position => !claims.IsBlocked(position))
            .OrderBy(position => position.ChebyshevDistance(preferred))
            .ThenBy(position => position.Y)
            .ThenBy(position => position.X)
            .Select(position => (Position?)position)
            .FirstOrDefault();
        if (chosen is not Position value)
            return false;
        argument = new GenericActorActionArgument.PositionTargetArgument(value);
        return true;
    }

    private GenericActorContext.ObservedEnemyState? VisibleEnemyCarrier(
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc)
    {
        HashSet<ActorIdentity> carrierIds = arc.VisibleCores
            .Where(core => core.CarrierActorId?.TeamId != _teamId)
            .Select(core => core.CarrierActorId!)
            .ToHashSet();
        return mind.Enemies
            .Where(enemy => carrierIds.Contains(enemy.ActorId))
            .OrderBy(enemy => enemy.Position.ChebyshevDistance(_homeReactor))
            .ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault();
    }

    private Position StageStation(MindBody body) => StageRank(body) switch
    {
        0 => StageAtFraction(1.0),
        1 => StageAtFraction(0.72),
        2 => StageAtFraction(0.46),
        3 => StageAtFraction(0.20),
        _ => StageAtFraction(0.58),
    };

    private Position StageAtFraction(double fraction)
    {
        if (_homeToWellRoute.Length == 0)
            return _homeReactor;
        int index = (int)Math.Round(
            (_homeToWellRoute.Length - 1) * Math.Clamp(fraction, 0.0, 1.0));
        return _homeToWellRoute[index];
    }

    private static int StageRank(MindBody body) => body.ClassId switch
    {
        "relay" => 0,
        "repulsor" => 1,
        "palisade" => 2,
        "patchbay" => 3,
        _ => -1,
    };

    private static string RoleTag(MindBody body) => body.ClassId switch
    {
        "relay" => "pickup-carrier",
        "repulsor" => "forward-catcher",
        "palisade" => "armored-catcher",
        "patchbay" => "home-catcher",
        "hush" => "null-screen",
        "towline" => "hook-screen",
        "sunder" => "paint-screen",
        "lantern" => "far-well-picket",
        _ => "convoy-reserve",
    };

    private static int MaxHealth(
        GenericActorResolvedMatchContract contract,
        string formId) => contract.Rules.Forms
        .FirstOrDefault(form => form.Id == formId)?.MaxHealth ?? 1;

    private static string CoreKey(GenericActorContext.ArcRelayCoreState core) =>
        $"{core.CoreId.SourceWellId}:{core.CoreId.SourceOrdinal}";

    private static Direction CardinalToward(Position from, Position to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        if (Math.Abs(dx) >= Math.Abs(dy))
            return dx >= 0 ? Direction.East : Direction.West;
        return dy >= 0 ? Direction.South : Direction.North;
    }

    private static ProjectileHeading HeadingFromSigns(int dx, int dy) =>
        (Math.Sign(dx), Math.Sign(dy)) switch
        {
            (0, -1) => ProjectileHeading.North,
            (1, -1) => ProjectileHeading.NorthEast,
            (1, 0) => ProjectileHeading.East,
            (1, 1) => ProjectileHeading.SouthEast,
            (0, 1) => ProjectileHeading.South,
            (-1, 1) => ProjectileHeading.SouthWest,
            (-1, 0) => ProjectileHeading.West,
            (-1, -1) => ProjectileHeading.NorthWest,
            _ => throw new ArgumentOutOfRangeException(nameof(dx)),
        };

    private static Position[] AdjacentGoals(
        GenericActorMapContract map,
        Position centre) => Headings
        .Select(heading => heading.Vector())
        .Select(vector => centre.Offset(vector.Dx, vector.Dy))
        .Where(position => IsWalkable(map, position))
        .ToArray();

    private static bool CanStep(
        GenericActorMapContract map,
        Position from,
        Position to,
        TrafficClaims claims)
    {
        if (!IsWalkable(map, to) || claims.IsBlocked(to))
            return false;
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        return dx == 0 || dy == 0
            || IsWalkable(map, from.Offset(dx, 0))
               && IsWalkable(map, from.Offset(0, dy));
    }

    private static bool IsWalkable(GenericActorMapContract map, Position position) =>
        position.X >= 0
        && position.Y >= 0
        && position.X < map.Width
        && position.Y < map.Height
        && map.TileRows[position.Y][position.X] != '#';

    private static bool ClearLine(
        GenericActorMapContract map,
        Position from,
        Position to)
    {
        ProjectileHeading heading = HeadingFromSigns(to.X - from.X, to.Y - from.Y);
        (int dx, int dy) = heading.Vector();
        Position position = from;
        while (position != to)
        {
            Position next = position.Offset(dx, dy);
            if (!IsWalkable(map, next))
                return false;
            if (dx != 0 && dy != 0
                && (!IsWalkable(map, position.Offset(dx, 0))
                    || !IsWalkable(map, position.Offset(0, dy))))
            {
                return false;
            }
            position = next;
        }
        return true;
    }

    private static Position[] FindRoute(
        GenericActorMapContract map,
        Position start,
        Position goal,
        int preferredLaneY)
    {
        var frontier = new Queue<Position>();
        var previous = new Dictionary<Position, Position?> { [start] = null };
        frontier.Enqueue(start);
        while (frontier.Count > 0)
        {
            Position current = frontier.Dequeue();
            if (current == goal)
                break;
            foreach (Position next in Headings
                         .Select(heading => heading.Vector())
                         .Select(vector => current.Offset(vector.Dx, vector.Dy))
                         .Where(position => IsWalkable(map, position))
                         .Where(position => !previous.ContainsKey(position))
                         .OrderBy(position => position.ChebyshevDistance(goal))
                         .ThenBy(position => Math.Abs(position.Y - preferredLaneY))
                         .ThenBy(position => position.Y)
                         .ThenBy(position => position.X))
            {
                int dx = next.X - current.X;
                int dy = next.Y - current.Y;
                if (dx != 0 && dy != 0
                    && (!IsWalkable(map, current.Offset(dx, 0))
                        || !IsWalkable(map, current.Offset(0, dy))))
                {
                    continue;
                }
                previous[next] = current;
                frontier.Enqueue(next);
            }
        }

        if (!previous.ContainsKey(goal))
            return [start];
        var path = new List<Position>();
        for (Position? cursor = goal;
             cursor is Position position;
             cursor = previous[position])
        {
            path.Add(position);
        }
        path.Reverse();
        return [.. path];
    }

    private static int RouteDistance(
        GenericActorMapContract map,
        Position start,
        IReadOnlyCollection<Position> goals)
    {
        if (goals.Contains(start))
            return 0;
        var frontier = new Queue<(Position Position, int Distance)>();
        var seen = new HashSet<Position> { start };
        frontier.Enqueue((start, 0));
        while (frontier.Count > 0)
        {
            (Position current, int distance) = frontier.Dequeue();
            foreach (var heading in Headings)
            {
                (int dx, int dy) = heading.Vector();
                Position next = current.Offset(dx, dy);
                if (!IsWalkable(map, next) || !seen.Add(next))
                    continue;
                if (dx != 0 && dy != 0
                    && (!IsWalkable(map, current.Offset(dx, 0))
                        || !IsWalkable(map, current.Offset(0, dy))))
                {
                    continue;
                }
                if (goals.Contains(next))
                    return distance + 1;
                frontier.Enqueue((next, distance + 1));
            }
        }
        return int.MaxValue;
    }

    private sealed class TrafficClaims
    {
        private readonly HashSet<Position> _blocked = [];

        public TrafficClaims(
            GenericActorResolvedMatchContract contract,
            MindContext mind,
            int teamId)
        {
            foreach (MindBody body in mind.Bodies)
                _blocked.Add(body.Position);
            foreach (var enemy in mind.Enemies)
                _blocked.Add(enemy.Position);
            foreach (var tile in mind.VisibleTiles.Where(tile =>
                         tile.SpawnReservation is not null))
            {
                _blocked.Add(tile.Position);
            }
            if (mind.VisibleProjectiles is { } projectiles)
            {
                foreach (var projectile in projectiles.Where(projectile =>
                             projectile.OwnerTeamId != teamId))
                {
                    _blocked.Add(projectile.Position);
                }
            }

            _ = contract;
        }

        public bool IsBlocked(Position position) => _blocked.Contains(position);

        public void Reserve(Position position) => _blocked.Add(position);
    }
}
