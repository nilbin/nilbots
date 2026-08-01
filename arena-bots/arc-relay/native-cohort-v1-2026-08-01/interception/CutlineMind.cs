using BotArena.Sdk;

/// <summary>
/// Cutline: find the enemy carrier, converge ahead of its reactor route,
/// displace or destroy it, and immediately recover the loose Core.
/// </summary>
public sealed class CutlineMind : IGenericMindBot
{
    private readonly Recall _recall = new();
    private GenericActorResolvedMatchContract? _contract;
    private int _participantId;
    private int _teamId;

    public void StartMatch(MindStart start)
    {
        _contract = start.Contract;
        _participantId = start.ParticipantId;
        _teamId = start.TeamId;

        _ = start.Contract.Rules.GameMode
            as GenericActorRulesContract.ArcRelayGameMode
            ?? throw new InvalidOperationException(
                "Cutline requires the contract's Arc Relay mode.");
        _ = start.Contract.ModeMapBinding
            as GenericActorResolvedMatchContract.ArcRelayModeMapBinding
            ?? throw new InvalidOperationException(
                "Cutline requires the contract's Arc Relay map binding.");
    }

    public void Think(MindContext mind)
    {
        GenericActorResolvedMatchContract contract = _contract
            ?? throw new InvalidOperationException("StartMatch was not called.");
        _recall.Observe(mind, _teamId);
        if (mind.Bodies.IsEmpty)
        {
            mind.Debug.Write($"cutline reset; {mind.Slots.Length} slots tracked");
            return;
        }

        GenericActorContext.ModeObservationState.ArcRelay? arc =
            ArenaBasics.ArcState(mind);
        if (arc is null)
        {
            foreach (MindBody body in mind.Bodies)
                body.Hold("waiting for Arc Relay state");
            return;
        }

        GenericActorContext.ObservedEnemyState? carrier =
            ArenaBasics.VisibleEnemyCarrier(mind, _teamId);
        Recall.CarrierSighting? remembered = _recall.RecentCarrier(mind.Tick);
        Position? ownReactor = ArenaBasics.Reactor(mind, _teamId);
        Position? enemyReactor = arc.Reactors
            .Where(reactor => reactor.TeamId != _teamId)
            .OrderBy(reactor => reactor.TeamId)
            .Select(reactor => (Position?)reactor.Position)
            .FirstOrDefault();
        RoleMap roles = Roles.Assign(contract, mind);
        var claims = ArenaBasics.Claims.ForTick(mind);

        foreach (MindBody body in mind.Bodies
                     .OrderBy(body => CommandPriority(roles[body]))
                     .ThenBy(body => body.UnitId))
        {
            Role role = roles[body];
            body.SetRole(Label(role));

            if (ArenaBasics.CarriedCore(mind, body.ActorId) is not null)
            {
                ReturnCore(contract, mind, body, ownReactor, claims);
                continue;
            }

            if (role == Role.CoreRecovery
                && roles.CoreTarget(body) is Position loose)
            {
                RecoverCore(contract, mind, body, loose, carrier, claims);
                continue;
            }

            if (carrier is not null && enemyReactor is Position hostileSocket)
            {
                InterceptVisibleCarrier(
                    contract,
                    mind,
                    body,
                    role,
                    carrier,
                    hostileSocket,
                    claims);
                continue;
            }

            if (remembered is not null && enemyReactor is Position rememberedSocket)
            {
                CutRememberedRoute(
                    contract,
                    mind,
                    body,
                    remembered,
                    rememberedSocket,
                    claims);
                continue;
            }

            PatrolWells(contract, mind, body, role, arc, claims);
        }
    }

    public void EndMatch(MindEnd end) => _ = end;

    private static void ReturnCore(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Position? reactor,
        ArenaBasics.Claims claims)
    {
        if (reactor is not Position home)
        {
            body.Hold("reactor binding unavailable");
            return;
        }

        bool underImmediatePressure = mind.Enemies.Any(enemy =>
            body.Position.ChebyshevDistance(enemy.Position) <= 2);
        if (body.Health == 1
            && underImmediatePressure
            && ArenaBasics.TryPositionSignature(
                contract,
                body,
                "arc-toss",
                home,
                "emergency Core relay toward home",
                target => target.ChebyshevDistance(home)
                    < body.Position.ChebyshevDistance(home)))
        {
            return;
        }

        if (ArenaBasics.TryMoveToward(
                contract,
                mind,
                body,
                [home],
                claims,
                "delivering Core"))
        {
            return;
        }
        body.Hold("Core recovery clock; keep possession");
    }

    private static void RecoverCore(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Position loose,
        GenericActorContext.ObservedEnemyState? carrier,
        ArenaBasics.Claims claims)
    {
        if (ArenaBasics.TryMoveToward(
                contract,
                mind,
                body,
                [loose],
                claims,
                "recovering loose Core"))
        {
            return;
        }
        if (ArenaBasics.TryShoot(contract, mind, body, carrier))
            return;
        body.Hold("guarding loose Core pickup");
    }

    private static void InterceptVisibleCarrier(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Role role,
        GenericActorContext.ObservedEnemyState carrier,
        Position enemyReactor,
        ArenaBasics.Claims claims)
    {
        if (TryInterceptionSignature(contract, body, role, carrier))
            return;
        if (ArenaBasics.TryShoot(contract, mind, body, carrier))
            return;

        Position cut = ArenaBasics.Cutoff(
            contract.Map,
            carrier.Position,
            enemyReactor);
        Position[] goals = ArenaBasics.ApproachTiles(contract.Map, cut);
        if (goals.Length == 0)
            goals = ArenaBasics.ApproachTiles(contract.Map, carrier.Position);
        if (ArenaBasics.TryMoveToward(
                contract,
                mind,
                body,
                goals,
                claims,
                "closing the carrier cutline"))
        {
            return;
        }
        if (ArenaBasics.TryEvade(contract, mind, body, claims))
            return;
        body.Hold("holding the cutline");
    }

    private static bool TryInterceptionSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Role role,
        GenericActorContext.ObservedEnemyState carrier) =>
        role switch
        {
            Role.FocusPaint => ArenaBasics.TryUnitSignature(
                contract,
                body,
                "target-paint",
                carrier.ActorId,
                "paint enemy carrier for the firing line"),
            Role.CarrierHook => ArenaBasics.TryHeadingSignature(
                contract,
                body,
                "tractor-hook",
                carrier.Position,
                "pull enemy carrier off the return line"),
            Role.CutlineBurst when
                body.Position.ChebyshevDistance(carrier.Position) <= 1 =>
                ArenaBasics.TryParameterlessSignature(
                    contract,
                    body,
                    "kinetic-burst",
                    "burst enemy carrier out of route"),
            // The authored rail-line branch is mechanically disabled after the
            // permitted smoke aborted while projecting a zero-health body.
            // Longshot still fills the rail-cut with its contract-declared
            // long basic gun; no match outcome informed this repair.
            Role.RailCut => false,
            _ => false,
        };

    private static void CutRememberedRoute(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Recall.CarrierSighting remembered,
        Position enemyReactor,
        ArenaBasics.Claims claims)
    {
        Position cut = ArenaBasics.Cutoff(
            contract.Map,
            remembered.Position,
            enemyReactor,
            leadTiles: 3);
        Position[] goals = ArenaBasics.ApproachTiles(contract.Map, cut);
        GenericActorContext.ObservedEnemyState? fallback = mind.Enemies
            .OrderBy(enemy => enemy.Position.ChebyshevDistance(cut))
            .ThenBy(enemy => enemy.Health)
            .ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault();
        if (ArenaBasics.TryShoot(contract, mind, body, fallback))
            return;
        if (ArenaBasics.TryMoveToward(
                contract,
                mind,
                body,
                goals,
                claims,
                "cutting last-seen carrier route"))
        {
            return;
        }
        body.Hold("last-seen cutline established");
    }

    private void PatrolWells(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Role role,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        ArenaBasics.Claims claims)
    {
        GenericActorContext.ArcRelayWellState[] wells = arc.Wells
            .OrderBy(well => well.NextScheduledBirthTick ?? int.MaxValue)
            .ThenBy(well => well.WellId, StringComparer.Ordinal)
            .ToArray();
        if (wells.Length == 0)
        {
            body.Hold("no Wells in public mode state");
            return;
        }

        HashSet<GenericActorContext.ArcRelayCoreId> visibleIds =
            arc.VisibleCores.Select(core => core.CoreId).ToHashSet();
        GenericActorContext.ArcRelayWellState? hiddenOutstanding = wells
            .FirstOrDefault(well =>
                well.OutstandingCoreId is { } coreId
                && !visibleIds.Contains(coreId));
        GenericActorContext.ArcRelayWellState target = hiddenOutstanding
            ?? wells[(body.UnitId + _participantId) % wells.Length];

        if (role == Role.FlareWatch
            && hiddenOutstanding is not null
            && ArenaBasics.TryPositionSignature(
                contract,
                body,
                "survey-flare",
                hiddenOutstanding.Position,
                "flare the missing Core's route"))
        {
            return;
        }

        GenericActorContext.ObservedEnemyState? wellThreat = mind.Enemies
            .OrderBy(enemy => enemy.Position.ChebyshevDistance(target.Position))
            .ThenBy(enemy => enemy.Health)
            .ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault();
        if (ArenaBasics.TryShoot(contract, mind, body, wellThreat))
            return;
        if (ArenaBasics.TryMoveToward(
                contract,
                mind,
                body,
                [target.Position],
                claims,
                "patrolling Core birth lane"))
        {
            return;
        }
        if (ArenaBasics.TryEvade(contract, mind, body, claims))
            return;
        body.Hold("watching Well and return routes");
    }

    private static int CommandPriority(Role role) =>
        role switch
        {
            Role.ReturnCarrier => 0,
            Role.CoreRecovery => 1,
            Role.FocusPaint => 2,
            Role.CarrierHook => 3,
            Role.RailCut => 4,
            Role.CutlineBurst => 5,
            Role.FlareWatch => 6,
            Role.RelayRunner => 7,
            _ => 8,
        };

    private static string Label(Role role) =>
        role switch
        {
            Role.ReturnCarrier => "return-carrier",
            Role.CoreRecovery => "core-recovery",
            Role.CarrierHook => "carrier-hook",
            Role.FocusPaint => "focus-paint",
            Role.CutlineBurst => "cutline-burst",
            Role.RailCut => "rail-cut",
            Role.FlareWatch => "flare-watch",
            Role.RelayRunner => "relay-runner",
            _ => "route-guard",
        };
}
