using BotArena.Sdk;

/// <summary>
/// A participant-scoped Arc Relay convoy: two Relay bodies leapfrog a Core
/// along the centre route, Palisades screen the return, Patchbay and Hush keep
/// the package intact, and one Towline/Lantern pair provides the deliberately
/// minimal contest at the remaining Wells.
/// </summary>
public sealed class ArcConvoy : IGenericMindBot
{
    private readonly Recall _recall = new();
    private readonly Dictionary<string, WellRoute> _routes =
        new(StringComparer.Ordinal);

    private GenericActorResolvedMatchContract? _contract;
    private string _mainWellId = string.Empty;
    private string[] _wingWellIds = [];
    private Position _reactor;
    private Direction _forward;
    private int _teamId;

    public void StartMatch(MindStart start)
    {
        _contract = start.Contract;
        _teamId = start.TeamId;

        if (start.Contract.Rules.GameMode
                is not GenericActorRulesContract.ArcRelayGameMode mode
            || start.Contract.ModeMapBinding
                is not GenericActorResolvedMatchContract.ArcRelayModeMapBinding
                    binding)
        {
            throw new InvalidOperationException(
                "ArcConvoy requires the public Arc Relay contract.");
        }

        for (int index = 0;
             index < Math.Min(mode.Wells.Length,
                 binding.OrderedWellRegionIds.Length);
             index++)
        {
            GenericActorMapContract.Region region = start.Contract.Map.Regions
                .Single(candidate => string.Equals(
                    candidate.RegionId,
                    binding.OrderedWellRegionIds[index],
                    StringComparison.Ordinal));
            _routes[mode.Wells[index].WellId] = new WellRoute(
                mode.Wells[index].WellId,
                region.Tiles.Single());
        }
        _mainWellId = mode.Wells[0].WellId;
        _wingWellIds = mode.Wells
            .Skip(1)
            .Select(well => well.WellId)
            .ToArray();

        GenericActorResolvedMatchContract.ParticipantRegionAssignment reactorRole =
            start.Contract.ParticipantRegionAssignments.Single(assignment =>
                assignment.ParticipantId == start.ParticipantId
                && string.Equals(
                    assignment.RegionRoleId,
                    binding.ReactorRegionRoleId,
                    StringComparison.Ordinal));
        _reactor = start.Contract.Map.Regions
            .Single(region => string.Equals(
                region.RegionId,
                reactorRole.MapRegionId,
                StringComparison.Ordinal))
            .Tiles
            .Single();
        _forward = reactorRole.Facing;
    }

    public void Think(MindContext mind)
    {
        GenericActorResolvedMatchContract contract = _contract
            ?? throw new InvalidOperationException("StartMatch was not called.");
        _recall.Observe(mind);
        if (mind.Bodies.IsEmpty)
            return;
        if (mind.Mode
            is not GenericActorContext.ModeObservationState.ArcRelay arc)
        {
            foreach (MindBody body in mind.Bodies)
                body.Hold("waiting for Arc Relay state");
            return;
        }

        WellRoute main = _routes[_mainWellId];
        Roles.Plan plan = Roles.Assign(
            contract,
            mind,
            _teamId,
            _mainWellId,
            main.Position,
            _wingWellIds);
        GenericActorContext.ObservedEnemyState? enemyCarrier =
            ArenaBasics.EnemyCarrier(mind, _teamId);
        var claims = ArenaBasics.Claims.ForTick(mind);

        foreach (MindBody body in mind.Bodies
                     .OrderBy(body => Priority(plan[body]))
                     .ThenBy(body => body.UnitId))
        {
            Roles.Role role = plan[body];
            body.SetRole(Roles.Label(role));
            switch (role)
            {
                case Roles.Role.MainCarrier:
                    MainCarrier(contract, mind, body, plan, enemyCarrier,
                        claims);
                    break;
                case Roles.Role.RelayCatcher:
                    Catcher(contract, mind, body, plan, enemyCarrier, claims);
                    break;
                case Roles.Role.MainPickup:
                    Pickup(contract, mind, body, main, arc, enemyCarrier,
                        claims);
                    break;
                case Roles.Role.UpperScreen:
                    Screen(contract, mind, body, plan, enemyCarrier, claims,
                        upper: true);
                    break;
                case Roles.Role.LowerScreen:
                    Screen(contract, mind, body, plan, enemyCarrier, claims,
                        upper: false);
                    break;
                case Roles.Role.ConvoyMedic:
                    Medic(contract, mind, body, plan, enemyCarrier, claims);
                    break;
                case Roles.Role.ConvoySuppressor:
                    Suppressor(contract, mind, body, plan, enemyCarrier,
                        claims);
                    break;
                case Roles.Role.FirstPicket:
                case Roles.Role.SecondPicket:
                    Picket(contract, mind, body, plan, arc, enemyCarrier,
                        claims);
                    break;
                case Roles.Role.WingReturn:
                    ReturnWingCore(contract, mind, body, enemyCarrier, claims);
                    break;
                default:
                    Reserve(contract, mind, body, plan, enemyCarrier, claims);
                    break;
            }
        }
    }

    public void EndMatch(MindEnd end) => _ = end;

    private void MainCarrier(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Roles.Plan plan,
        GenericActorContext.ObservedEnemyState? enemyCarrier,
        ArenaBasics.Claims claims)
    {
        if (plan.HandoffSourceUnitId == body.UnitId
            && plan.Catcher is not null
            && ArenaBasics.TryHandoff(contract, body, plan.Catcher))
        {
            return;
        }

        bool contact = mind.Enemies.Any(enemy =>
            enemy.Position.ChebyshevDistance(body.Position) <= 2);
        if ((body.Health * 2 <= ArenaBasics.MaxHealth(contract, body) || contact)
            && ArenaBasics.TryArcToss(contract, body, _reactor))
        {
            return;
        }
        if (ArenaBasics.TryDodge(contract, mind, body, [_reactor], claims))
            return;
        if (ArenaBasics.TryMoveToward(
                contract,
                mind,
                body,
                [_reactor],
                claims,
                "carrying centre Core home"))
        {
            return;
        }
        body.Hold(enemyCarrier is null
            ? "carrier recovery hold"
            : "carrier protected hold");
    }

    private void Catcher(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Roles.Plan plan,
        GenericActorContext.ObservedEnemyState? enemyCarrier,
        ArenaBasics.Claims claims)
    {
        if (plan.HandoffTargetUnitId == body.UnitId)
        {
            body.Hold("committed handoff catch");
            return;
        }
        if (plan.Protected is not MindBody protectedBody)
        {
            Reserve(contract, mind, body, plan, enemyCarrier, claims);
            return;
        }

        Position[] catchTiles = ArenaBasics.AdjacentToward(
                contract.Map,
                protectedBody.Position,
                _reactor)
            .Take(3)
            .ToArray();
        if (catchTiles.Contains(body.Position))
        {
            body.Hold("waiting in catch pocket");
            return;
        }
        if (ArenaBasics.TryDodge(contract, mind, body, catchTiles, claims))
            return;
        if (ArenaBasics.TryMoveToward(
                contract,
                mind,
                body,
                catchTiles,
                claims,
                "leapfrogging to catch pocket"))
        {
            return;
        }
        if (ArenaBasics.TryShoot(contract, mind, body, enemyCarrier))
            return;
        body.Hold("catch lane blocked");
    }

    private void Pickup(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        WellRoute route,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        GenericActorContext.ObservedEnemyState? enemyCarrier,
        ArenaBasics.Claims claims)
    {
        GenericActorContext.ArcRelayWellState? well = arc.Wells
            .FirstOrDefault(candidate => string.Equals(
                candidate.WellId,
                route.WellId,
                StringComparison.Ordinal));
        GenericActorContext.ArcRelayCoreState? core = _recall.CoreFrom(
            route.WellId);
        Position[] goals;
        if (core is { Disposition: GenericActorContext.ArcRelayCoreDisposition.Loose }
            && core.Position == body.Position)
        {
            // A birth can occur under a staged body; step off once so the next
            // return step can perform the movement-ending pickup.
            goals = ArenaBasics.StageHomeward(
                contract.Map,
                route.Position,
                _reactor);
        }
        else if (well?.OutstandingCoreId is not null && core is not null)
        {
            goals = [core.Position];
        }
        else if (well?.OutstandingCoreId is not null)
        {
            goals = [route.Position];
        }
        else
        {
            goals = ArenaBasics.StageHomeward(
                contract.Map,
                route.Position,
                _reactor);
        }

        if (ArenaBasics.TryDodge(contract, mind, body, goals, claims))
            return;
        if (ArenaBasics.TryMoveToward(
                contract,
                mind,
                body,
                goals,
                claims,
                "acquiring centre Core"))
        {
            return;
        }
        if (ArenaBasics.TryShoot(contract, mind, body, enemyCarrier))
            return;
        body.Hold("staged for centre birth");
    }

    private void Screen(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Roles.Plan plan,
        GenericActorContext.ObservedEnemyState? enemyCarrier,
        ArenaBasics.Claims claims,
        bool upper)
    {
        if (plan.Protected is not MindBody protectedBody)
        {
            Reserve(contract, mind, body, plan, enemyCarrier, claims);
            return;
        }
        GenericActorContext.ObservedEnemyState? threat = enemyCarrier
            ?? mind.Enemies
                .OrderBy(enemy =>
                    enemy.Position.ChebyshevDistance(protectedBody.Position))
                .ThenBy(enemy => enemy.ActorId)
                .FirstOrDefault();
        bool incoming = (mind.VisibleProjectiles ?? []).Any(projectile =>
            projectile.OwnerTeamId != _teamId
            && projectile.Position.ChebyshevDistance(protectedBody.Position) <= 4);
        if (body.Position.ChebyshevDistance(protectedBody.Position) <= 3
            && (incoming
                || threat is not null
                    && threat.Position.ChebyshevDistance(
                        protectedBody.Position) <= 6)
            && ArenaBasics.TryPrismWall(
                contract,
                body,
                threat?.Position ?? protectedBody.Position.Offset(
                    _forward.Vector().Dx * 4,
                    _forward.Vector().Dy * 4)))
        {
            return;
        }

        Position[] screenTiles = ArenaBasics.ScreenTiles(
            contract.Map,
            protectedBody.Position,
            _forward,
            upper);
        if (ArenaBasics.TryDodge(contract, mind, body, screenTiles, claims))
            return;
        if (ArenaBasics.TryMoveToward(
                contract,
                mind,
                body,
                screenTiles,
                claims,
                upper ? "forming upper screen" : "forming lower screen"))
        {
            return;
        }
        if (ArenaBasics.TryShoot(contract, mind, body, enemyCarrier))
            return;
        body.Hold("screen locked");
    }

    private void Medic(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Roles.Plan plan,
        GenericActorContext.ObservedEnemyState? enemyCarrier,
        ArenaBasics.Claims claims)
    {
        MindBody[] priorities = mind.Bodies
            .OrderBy(candidate => RepairPriority(plan[candidate]))
            .ThenBy(candidate => candidate.UnitId)
            .ToArray();
        if (ArenaBasics.TryRepair(contract, body, priorities))
            return;
        if (plan.Protected is MindBody protectedBody
            && body.Position.ChebyshevDistance(protectedBody.Position) > 2)
        {
            Position[] goals = ArenaBasics.AdjacentToward(
                    contract.Map,
                    protectedBody.Position,
                    _reactor)
                .Take(5)
                .ToArray();
            if (ArenaBasics.TryDodge(contract, mind, body, goals, claims))
                return;
            if (ArenaBasics.TryMoveToward(
                    contract,
                    mind,
                    body,
                    goals,
                    claims,
                    "closing repair envelope"))
            {
                return;
            }
        }
        if (ArenaBasics.TryShoot(contract, mind, body, enemyCarrier))
            return;
        body.Hold("medic inside convoy");
    }

    private void Suppressor(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Roles.Plan plan,
        GenericActorContext.ObservedEnemyState? enemyCarrier,
        ArenaBasics.Claims claims)
    {
        MindBody? protectedBody = plan.Protected;
        bool closeContact = protectedBody is not null
            && body.Position.ChebyshevDistance(protectedBody.Position) <= 3
            && mind.Enemies.Any(enemy =>
                enemy.Position.ChebyshevDistance(protectedBody.Position) <= 3);
        bool hostileSignature = protectedBody is not null
            && mind.Mode is GenericActorContext.ModeObservationState.ArcRelay arc
            && arc.VisibleSignatures.Any(signature =>
                signature.OwnerTeamId != _teamId
                && signature.Positions.Any(position =>
                    position.ChebyshevDistance(protectedBody.Position) <= 3));
        if ((closeContact || hostileSignature)
            && ArenaBasics.TryNullField(contract, body))
        {
            return;
        }
        if (protectedBody is not null)
        {
            Position[] goals = ArenaBasics.ScreenTiles(
                contract.Map,
                protectedBody.Position,
                _forward,
                upper: false);
            if (ArenaBasics.TryDodge(contract, mind, body, goals, claims))
                return;
            if (ArenaBasics.TryMoveToward(
                    contract,
                    mind,
                    body,
                    goals,
                    claims,
                    "covering signature lane"))
            {
                return;
            }
        }
        if (ArenaBasics.TryShoot(contract, mind, body, enemyCarrier))
            return;
        body.Hold("suppression escort hold");
    }

    private void Picket(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Roles.Plan plan,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        GenericActorContext.ObservedEnemyState? enemyCarrier,
        ArenaBasics.Claims claims)
    {
        string? wellId = plan.PicketWell(body);
        if (wellId is null || !_routes.TryGetValue(wellId, out WellRoute? route))
        {
            Reserve(contract, mind, body, plan, enemyCarrier, claims);
            return;
        }
        GenericActorContext.ArcRelayWellState? well = arc.Wells
            .FirstOrDefault(candidate => string.Equals(
                candidate.WellId,
                wellId,
                StringComparison.Ordinal));
        GenericActorContext.ArcRelayCoreState? currentVisible = arc.VisibleCores
            .FirstOrDefault(core => string.Equals(
                core.CoreId.SourceWellId,
                wellId,
                StringComparison.Ordinal));
        GenericActorContext.ArcRelayCoreState? remembered = _recall.CoreFrom(wellId);

        GenericActorContext.ObservedEnemyState? laneTarget = enemyCarrier
            ?? mind.Enemies
                .Where(enemy =>
                    enemy.Position.ChebyshevDistance(route.Position) <= 6)
                .OrderBy(enemy => enemy.Health)
                .ThenBy(enemy => enemy.ActorId)
                .FirstOrDefault();
        if (laneTarget is not null
            && ArenaBasics.TryTractor(contract, body, laneTarget))
        {
            return;
        }
        if (well?.OutstandingCoreId is not null
            && currentVisible is null
            && ArenaBasics.TrySurveyFlare(contract, body, route.Position))
        {
            return;
        }

        Position[] goals = well?.OutstandingCoreId is not null
            ? [remembered?.Position ?? route.Position]
            : ArenaBasics.StageHomeward(contract.Map, route.Position, _reactor);
        if (remembered is
                { Disposition: GenericActorContext.ArcRelayCoreDisposition.Loose }
            && remembered.Position == body.Position)
        {
            goals = ArenaBasics.StageHomeward(
                contract.Map,
                route.Position,
                _reactor);
        }
        if (ArenaBasics.TryDodge(contract, mind, body, goals, claims))
            return;
        if (ArenaBasics.TryMoveToward(
                contract,
                mind,
                body,
                goals,
                claims,
                "minimal peripheral Well contest"))
        {
            return;
        }
        if (ArenaBasics.TryShoot(contract, mind, body, enemyCarrier))
            return;
        body.Hold("picketing peripheral Well");
    }

    private void ReturnWingCore(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        GenericActorContext.ObservedEnemyState? enemyCarrier,
        ArenaBasics.Claims claims)
    {
        if (ArenaBasics.TryDodge(contract, mind, body, [_reactor], claims))
            return;
        if (ArenaBasics.TryMoveToward(
                contract,
                mind,
                body,
                [_reactor],
                claims,
                "returning contested wing Core"))
        {
            return;
        }
        body.Hold(enemyCarrier is null
            ? "wing Core recovery hold"
            : "wing Core protected hold");
    }

    private void Reserve(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Roles.Plan plan,
        GenericActorContext.ObservedEnemyState? enemyCarrier,
        ArenaBasics.Claims claims)
    {
        if (ArenaBasics.TryShoot(contract, mind, body, enemyCarrier))
            return;
        Position[] goals = plan.Protected is MindBody protectedBody
            ? ArenaBasics.AdjacentToward(
                contract.Map,
                protectedBody.Position,
                _reactor)
            : [_routes[_mainWellId].Position];
        if (ArenaBasics.TryDodge(contract, mind, body, goals, claims))
            return;
        if (ArenaBasics.TryMoveToward(
                contract,
                mind,
                body,
                goals,
                claims,
                "reinforcing convoy"))
        {
            return;
        }
        body.Hold("convoy reserve hold");
    }

    private static int Priority(Roles.Role role) => role switch
    {
        Roles.Role.RelayCatcher => 0,
        Roles.Role.MainCarrier => 1,
        Roles.Role.MainPickup => 2,
        Roles.Role.UpperScreen => 3,
        Roles.Role.LowerScreen => 4,
        Roles.Role.ConvoyMedic => 5,
        Roles.Role.ConvoySuppressor => 6,
        Roles.Role.FirstPicket => 7,
        Roles.Role.SecondPicket => 8,
        Roles.Role.WingReturn => 9,
        _ => 10,
    };

    private static int RepairPriority(Roles.Role role) => role switch
    {
        Roles.Role.MainCarrier => 0,
        Roles.Role.MainPickup => 1,
        Roles.Role.UpperScreen => 2,
        Roles.Role.LowerScreen => 3,
        Roles.Role.RelayCatcher => 4,
        _ => 5,
    };

    private sealed record WellRoute(string WellId, Position Position);
}
