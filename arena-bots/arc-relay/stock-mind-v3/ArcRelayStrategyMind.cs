using BotArena.Sdk;

/// <summary>
/// Evaluation-grade Arc Relay strategy mind v3. It preserves the complete v2
/// baseline and adds bounded, causal team operations as temporary overrides.
/// </summary>
public sealed class ArcRelayStrategyMind : IGenericMindBot
{
    private readonly Dictionary<int, CarrierMotion> _carrierMotion = [];
    private readonly Dictionary<int, int> _catchHoldUntil = [];
    private readonly Dictionary<int, Position> _catchHoldPosition = [];
    private GenericActorResolvedMatchContract? _contract;
    private StrategyDirector? _director;
    private StrategySheet? _sheet;
    private Strategy _strategy;
    private Position _ownReactor;
    private bool _mirror;
    private int _teamId;

    public void StartMatch(MindStart start)
    {
        _contract = start.Contract;
        _sheet = StrategySheet.Load(start.EvaluationData);
        _teamId = start.TeamId;
        _strategy = StrategyFrom(_sheet.SheetId);

        if (start.Contract.ModeMapBinding
                is not GenericActorResolvedMatchContract.ArcRelayModeMapBinding
                    binding)
        {
            throw new InvalidOperationException("AuditMind requires Arc Relay.");
        }

        GenericActorResolvedMatchContract.ParticipantRegionAssignment assignment =
            start.Contract.ParticipantRegionAssignments.Single(value =>
                value.ParticipantId == start.ParticipantId
                && string.Equals(
                    value.RegionRoleId,
                    binding.ReactorRegionRoleId,
                    StringComparison.Ordinal));
        _ownReactor = start.Contract.Map.Regions.Single(value =>
                string.Equals(
                    value.RegionId,
                    assignment.MapRegionId,
                    StringComparison.Ordinal))
            .Tiles.OrderBy(value => value.Y).ThenBy(value => value.X).First();
        _mirror = _ownReactor.X > (start.Contract.Map.Width - 1) / 2;
        _director = new StrategyDirector(
            _sheet,
            start.Contract,
            _teamId,
            _mirror,
            _ownReactor);
    }

    public void Think(MindContext mind)
    {
        GenericActorResolvedMatchContract contract = _contract
            ?? throw new InvalidOperationException("StartMatch was not called.");
        StrategySheet sheet = _sheet
            ?? throw new InvalidOperationException("Evaluation data was not loaded.");
        if (mind.Mode is not GenericActorContext.ModeObservationState.ArcRelay arc)
        {
            foreach (MindBody body in mind.Bodies)
                body.Hold("unsupported mode");
            return;
        }

        StrategyDirector director = _director
            ?? throw new InvalidOperationException("Strategy director was not loaded.");
        director.Update(mind, arc);
        IReadOnlyDictionary<int, UnitPlan> basePlans = sheet.Units
            .ToDictionary(value => value.UnitId);
        IReadOnlyDictionary<int, UnitPlan> plans = basePlans.ToDictionary(
            value => value.Key,
            value => director.Effective(value.Value));
        var carried = arc.VisibleCores
            .Where(core =>
                core.Disposition
                    == GenericActorContext.ArcRelayCoreDisposition.Carried
                && core.CarrierActorId is not null)
            .ToDictionary(core => core.CarrierActorId!, core => core);
        Dictionary<int, GenericActorContext.ArcRelayCoreState> pickups =
            AssignPickups(mind, arc, plans, carried);
        MindBody[] ownCarriers = mind.Bodies
            .Where(body => carried.ContainsKey(body.ActorId))
            .OrderBy(body => body.Position.ChebyshevDistance(_ownReactor))
            .ThenBy(body => body.UnitId)
            .ToArray();
        GenericActorContext.ObservedEnemyState? enemyCarrier =
            ArenaBasics.VisibleEnemyCarrier(mind, _teamId);
        Position enemyReactor = arc.Reactors
            .Where(reactor => reactor.TeamId != _teamId)
            .OrderBy(reactor => reactor.TeamId)
            .Select(reactor => reactor.Position)
            .First();
        UpdateCarrierMotion(mind, carried);
        Dictionary<int, ActorIdentity> tosses = PlanArcTosses(
            mind,
            plans,
            carried);
        HashSet<int> tossReceivers = tosses.Values
            .Select(value => value.UnitId)
            .ToHashSet();
        Dictionary<int, ActorIdentity> exchanges = PlanExchanges(
            mind,
            plans,
            carried,
            tossReceivers);
        HashSet<int> exchangeTargets = exchanges.Values
            .Select(value => value.UnitId)
            .ToHashSet();
        var claims = ArenaBasics.Claims.ForTick(mind);
        Dictionary<int, Position> carrierSteps = ownCarriers
            .Where(carrier => carrier.Position != _ownReactor)
            .Select(carrier => (
                Carrier: carrier,
                Step: ArenaBasics.StaticFirstStep(
                    contract,
                    carrier,
                    _ownReactor)))
            .Where(value => value.Step is not null)
            .ToDictionary(
                value => value.Carrier.UnitId,
                value => value.Step!.Value);
        HashSet<Position> carrierClearance = carrierSteps.Values.ToHashSet();
        if (ownCarriers.Length > 0)
            carrierClearance.Add(_ownReactor);

        foreach (MindBody body in mind.Bodies
                     .OrderByDescending(candidate =>
                         carried.ContainsKey(candidate.ActorId))
                     .ThenBy(candidate => Priority(plans[candidate.UnitId].Role))
                     .ThenBy(candidate => candidate.UnitId))
        {
            UnitPlan basePlan = basePlans[body.UnitId];
            UnitPlan plan = plans[body.UnitId];
            string normalRole = $"a-{StrategyCode(_strategy)}-"
                + $"{RoleCode(plan.Role)}-{TheaterCode(plan.Theater)}";
            body.SetRole(director.RoleTag(basePlan, normalRole));

            if (_catchHoldUntil.GetValueOrDefault(body.UnitId, -1) >= mind.Tick
                && _catchHoldPosition.GetValueOrDefault(body.UnitId)
                    == body.Position)
            {
                body.Hold("holding an authored Arc Toss catch point");
                continue;
            }
            if (exchangeTargets.Contains(body.UnitId))
            {
                body.Hold("holding for emergency Exchange extraction");
                continue;
            }
            if (tosses.TryGetValue(body.UnitId, out ActorIdentity? receiver)
                && receiver is not null
                && TryArcToss(body, mind.Body(receiver.UnitId)!))
            {
                director.RecordOperationAction(
                    body.UnitId, "arc-toss", mind.Tick);
                continue;
            }
            if (exchanges.TryGetValue(body.UnitId, out ActorIdentity? target)
                && target is not null
                && ArenaBasics.TryUnitSignature(
                    contract,
                    body,
                    "exchange",
                    target,
                    "extract a pressured carrier with Exchange"))
            {
                director.RecordOperationAction(
                    body.UnitId, "exchange", mind.Tick);
                continue;
            }

            bool carriesCore = carried.ContainsKey(body.ActorId);
            if (!carriesCore
                && carrierClearance.Contains(body.Position)
                && ArenaBasics.TryMoveAside(
                    contract,
                    mind,
                    body,
                    claims,
                    carrierClearance,
                    "clearing an allied Core return lane"))
            {
                continue;
            }
            if (director.TryActPosition(
                    mind,
                    arc,
                    body,
                    basePlan,
                    carriesCore,
                    claims))
            {
                continue;
            }

            if (carried.TryGetValue(body.ActorId, out var core))
            {
                if (carrierSteps.TryGetValue(body.UnitId, out Position step)
                    && mind.Bodies.Any(ally =>
                        ally.ActorId != body.ActorId
                        && ally.Position == step))
                {
                    body.Hold("waiting one tick for allied return-lane clearance");
                    continue;
                }
                ActCarrier(contract, mind, body, core, claims);
                continue;
            }
            if (pickups.TryGetValue(body.UnitId, out var loose))
            {
                ActPickup(contract, mind, body, loose, claims);
                continue;
            }

            MindBody? partnerCarrier = ownCarriers.FirstOrDefault(candidate =>
                candidate.UnitId == plan.PartnerUnitId);
            MindBody? nearestCarrier = ownCarriers
                .OrderBy(candidate =>
                    candidate.Position.ChebyshevDistance(body.Position))
                .ThenBy(candidate => candidate.UnitId)
                .FirstOrDefault();

            if (ShouldIntercept(plan, body, enemyCarrier))
            {
                ActIntercept(
                    contract,
                    mind,
                    body,
                    enemyCarrier!,
                    enemyReactor,
                    claims);
                continue;
            }
            if (ShouldEscort(plan, partnerCarrier, nearestCarrier))
            {
                ActEscort(
                    contract,
                    mind,
                    body,
                    partnerCarrier ?? nearestCarrier!,
                    enemyCarrier,
                    director.EscortPolicy(basePlan).FollowDistance,
                    claims);
                continue;
            }

            ActPatrol(contract, mind, arc, body, plan, enemyCarrier, claims);
        }

        mind.Debug.Write(
            $"audit {_strategy}: {mind.Bodies.Length} live, "
            + $"{pickups.Count} pickup jobs, {ownCarriers.Length} carriers; "
            + director.OperationTrace);
    }

    public void EndMatch(MindEnd end) => _ = end;

    private Dictionary<int, ActorIdentity> PlanArcTosses(
        MindContext mind,
        IReadOnlyDictionary<int, UnitPlan> plans,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        var result = new Dictionary<int, ActorIdentity>();
        if (_strategy is not Strategy.RelayChain and not Strategy.Feint)
            return result;

        var claimedReceivers = new HashSet<int>();
        foreach (MindBody carrier in mind.Bodies
                     .Where(body => carried.ContainsKey(body.ActorId))
                     .OrderBy(body => body.UnitId))
        {
            GenericActorContext.ArcRelayCoreState core = carried[carrier.ActorId];
            if (core.NextRelocationTick > mind.Tick)
                continue;
            UnitPlan plan = plans[carrier.UnitId];
            MindBody? receiver = mind.Body(plan.PartnerUnitId);
            if (receiver is null
                || carried.ContainsKey(receiver.ActorId)
                || claimedReceivers.Contains(receiver.UnitId)
                || receiver.Position.ChebyshevDistance(_ownReactor) + 2
                    > carrier.Position.ChebyshevDistance(_ownReactor))
            {
                continue;
            }
            GenericActorActionLegality? toss = carrier.Action("arc-toss");
            GenericActorActionLegality.ArgumentConstraint
                .PositionTargetConstraint? targets = toss?.Constraints
                    .OfType<GenericActorActionLegality.ArgumentConstraint
                        .PositionTargetConstraint>()
                    .SingleOrDefault();
            if (toss is not { Available: true }
                || targets is null
                || !targets.AllowedValues.Contains(receiver.Position))
            {
                continue;
            }
            claimedReceivers.Add(receiver.UnitId);
            result[carrier.UnitId] = receiver.ActorId;
            _catchHoldUntil[receiver.UnitId] = mind.Tick + 1;
            _catchHoldPosition[receiver.UnitId] = receiver.Position;
        }
        return result;
    }

    private Dictionary<int, ActorIdentity> PlanExchanges(
        MindContext mind,
        IReadOnlyDictionary<int, UnitPlan> plans,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried,
        IReadOnlySet<int> unavailableTargets)
    {
        StrategyDirector director = _director!;
        var result = new Dictionary<int, ActorIdentity>();
        var claimedTargets = new HashSet<int>();
        MindBody[] pressured = mind.Bodies
            .Where(body => carried.ContainsKey(body.ActorId))
            .Where(body => body.Health <= director.CarrierPolicy(
                plans[body.UnitId]).HandoffHealthAtOrBelow)
            .Where(body => mind.Enemies.Any(enemy =>
                enemy.Position.ChebyshevDistance(body.Position) <= 3))
            .OrderBy(body => body.Health)
            .ThenBy(body => body.Position.ChebyshevDistance(_ownReactor))
            .ThenBy(body => body.UnitId)
            .ToArray();
        foreach (MindBody source in mind.Bodies.OrderBy(body => body.UnitId))
        {
            if (carried.ContainsKey(source.ActorId)
                || unavailableTargets.Contains(source.UnitId))
            {
                continue;
            }
            GenericActorActionLegality? exchange = source.Action("exchange");
            GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
                targets = exchange?.Constraints
                    .OfType<GenericActorActionLegality.ArgumentConstraint
                        .UnitTargetConstraint>()
                    .SingleOrDefault();
            if (exchange is not { Available: true } || targets is null)
                continue;
            UnitPlan plan = plans[source.UnitId];
            MindBody? target = pressured
                .Where(body => !claimedTargets.Contains(body.UnitId))
                .Where(body => targets.AllowedValues.Contains(
                    new GenericActorActionArgument.UnitTarget(
                        body.ActorId.TeamId,
                        body.ActorId.UnitId)))
                .OrderBy(body => body.UnitId == plan.PartnerUnitId ? 0 : 1)
                .ThenBy(body => body.Health)
                .ThenBy(body => body.UnitId)
                .FirstOrDefault();
            if (target is null)
                continue;
            claimedTargets.Add(target.UnitId);
            result[source.UnitId] = target.ActorId;
        }
        return result;
    }

    private bool TryArcToss(MindBody carrier, MindBody receiver) =>
        ArenaBasics.TryPositionSignature(
            _contract!,
            carrier,
            "arc-toss",
            receiver.Position,
            "advance Core through an authored Arc Toss catch",
            position => position == receiver.Position);

    private void ActCarrier(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        GenericActorContext.ArcRelayCoreState core,
        ArenaBasics.Claims claims)
    {
        CarrierMotion? motion = _carrierMotion.GetValueOrDefault(body.UnitId);
        int stuckTicks = motion?.StationaryTicks ?? 0;
        int noProgressTicks = motion?.NoProgressTicks ?? 0;
        bool pressure = mind.Enemies.Any(enemy =>
            enemy.Position.ChebyshevDistance(body.Position) <= 2);
        if ((stuckTicks >= 5 || body.Health == 1 && pressure)
            && core.NextRelocationTick <= mind.Tick
            && ArenaBasics.TryPositionSignature(
                contract,
                body,
                "arc-toss",
                _ownReactor,
                "escape a pinned return with Arc Toss",
                target => target.ChebyshevDistance(_ownReactor)
                    < body.Position.ChebyshevDistance(_ownReactor)))
        {
            return;
        }
        if (stuckTicks >= 8 && TryDrop(body))
            return;
        bool commitShortestStep = noProgressTicks >= 6
            && noProgressTicks % 16 is >= 6 and <= 13;
        if (commitShortestStep)
        {
            Position? returnStep = ArenaBasics.StaticFirstStep(
                contract,
                body,
                _ownReactor);
            if (returnStep is Position destination
                && ArenaBasics.TryMoveDirect(
                    contract,
                    mind,
                    body,
                    destination,
                    claims,
                    "breaking a non-progress orbit on the committed lane"))
            {
                return;
            }
            if (!pressure)
            {
                body.Hold(
                    "holding the shortest return lane through transient traffic");
                return;
            }
        }
        if (ArenaBasics.TryMoveToward(
                contract,
                mind,
                body,
                [_ownReactor],
                claims,
                "returning Core"))
        {
            return;
        }
        if (pressure && ArenaBasics.TryShoot(
                contract,
                mind,
                body,
                mind.Enemies.OrderBy(enemy => enemy.Health).FirstOrDefault()))
        {
            return;
        }
        if (ArenaBasics.TryEvade(contract, mind, body, claims))
            return;
        body.Hold(core.NextRelocationTick > mind.Tick
            ? "Core recovery"
            : "return lane temporarily occupied");
    }

    private static void ActPickup(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        GenericActorContext.ArcRelayCoreState core,
        ArenaBasics.Claims claims)
    {
        GenericActorContext.ObservedEnemyState? threat = mind.Enemies
            .OrderBy(enemy => enemy.Position.ChebyshevDistance(core.Position))
            .ThenBy(enemy => enemy.Health)
            .FirstOrDefault();
        if (threat is not null
            && body.Position.ChebyshevDistance(threat.Position) <= 5
            && ArenaBasics.TryShoot(contract, mind, body, threat))
        {
            return;
        }
        if (TryAdvanceSignature(
                contract,
                body,
                core.Position,
                "dash to a contested loose Core"))
        {
            return;
        }
        if (ArenaBasics.TryMoveToward(
                contract,
                mind,
                body,
                [core.Position],
                claims,
                $"recovering {core.CoreId.SourceWellId} Core"))
        {
            return;
        }
        if (ArenaBasics.TryEvade(contract, mind, body, claims))
            return;
        body.Hold("contesting loose Core");
    }

    private static void ActIntercept(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        GenericActorContext.ObservedEnemyState carrier,
        Position enemyReactor,
        ArenaBasics.Claims claims)
    {
        if (TryDenialSignature(contract, mind, body, carrier))
            return;
        if (ArenaBasics.TryShoot(contract, mind, body, carrier))
            return;
        Position cut = ArenaBasics.Cutoff(
            contract.Map,
            carrier.Position,
            enemyReactor,
            leadTiles: 2);
        Position[] goals = ArenaBasics.ApproachTiles(contract.Map, cut);
        if (TryAdvanceSignature(
                contract,
                body,
                cut,
                "dash onto the carrier cutline"))
        {
            return;
        }
        if (ArenaBasics.TryMoveToward(
                contract,
                mind,
                body,
                goals,
                claims,
                "closing a carrier cutline"))
        {
            return;
        }
        if (ArenaBasics.TryEvade(contract, mind, body, claims))
            return;
        body.Hold("holding active cutline");
    }

    private static void ActEscort(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        MindBody carrier,
        GenericActorContext.ObservedEnemyState? enemyCarrier,
        int followDistance,
        ArenaBasics.Claims claims)
    {
        GenericActorContext.ObservedEnemyState? threat = mind.Enemies
            .OrderBy(enemy => enemy.Position.ChebyshevDistance(carrier.Position))
            .ThenBy(enemy => enemy.Health)
            .ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault();
        if (TrySupportSignature(contract, mind, body, carrier, threat))
            return;
        if (enemyCarrier is not null
            && TryDenialSignature(contract, mind, body, enemyCarrier))
        {
            return;
        }
        if (ArenaBasics.TryShoot(contract, mind, body, threat))
            return;
        if (body.Position.ChebyshevDistance(carrier.Position)
            <= Math.Max(1, followDistance))
        {
            body.Hold("maintaining authored escort spacing");
            return;
        }
        Position[] escort = ArenaBasics.ApproachTiles(
            contract.Map,
            carrier.Position);
        if (ArenaBasics.TryMoveToward(
                contract,
                mind,
                body,
                escort,
                claims,
                "moving carrier screen"))
        {
            return;
        }
        if (ArenaBasics.TryEvade(contract, mind, body, claims))
            return;
        body.Hold("screen set beside live carrier");
    }

    private void ActPatrol(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        UnitPlan plan,
        GenericActorContext.ObservedEnemyState? enemyCarrier,
        ArenaBasics.Claims claims)
    {
        string theater = EffectiveTheater(plan.Theater, mind.Tick);
        GenericActorContext.ArcRelayWellState well = WellFor(arc, theater);
        if (string.Equals(plan.Role, "carrier", StringComparison.Ordinal)
            && body.Position.ChebyshevDistance(well.Position) > 2)
        {
            Position staging = PatrolPoint(
                contract.Map,
                well.Position,
                body.UnitId,
                mind.Tick);
            if (TryAdvanceSignature(
                    contract,
                    body,
                    staging,
                    $"dash into the {theater} carrier lane"))
            {
                return;
            }
            if (ArenaBasics.TryMoveToward(
                    contract,
                    mind,
                    body,
                    [staging],
                    claims,
                    $"staging for the {theater} Core"))
            {
                return;
            }
        }
        GenericActorContext.ArcRelaySignatureState? hostileSignature =
            arc.VisibleSignatures
                .Where(signature => signature.OwnerTeamId != _teamId)
                .Where(signature => !signature.Suppressed)
                .Where(signature => !signature.Positions.IsEmpty)
                .OrderBy(signature => signature.Positions.Min(position =>
                    body.Position.ChebyshevDistance(position)))
                .ThenBy(signature => signature.OperationId, StringComparer.Ordinal)
                .FirstOrDefault();
        if (_strategy == Strategy.Sustain
            && hostileSignature is not null
            && hostileSignature.Positions.Any(position =>
                body.Position.ChebyshevDistance(position) <= 3)
            && ArenaBasics.TryParameterlessSignature(
                contract,
                body,
                "null-field",
                "collapse a visible hostile signature cluster"))
        {
            return;
        }
        bool outstandingHidden = well.OutstandingCoreId is not null
            && arc.VisibleCores.All(core => core.CoreId != well.OutstandingCoreId);
        if (outstandingHidden
            && ArenaBasics.TryPositionSignature(
                contract,
                body,
                "survey-flare",
                well.Position,
                "reveal an outstanding Core route"))
        {
            return;
        }
        GenericActorContext.ObservedEnemyState? threat = mind.Enemies
            .OrderBy(enemy => enemy.Position.ChebyshevDistance(well.Position))
            .ThenBy(enemy => enemy.Health)
            .FirstOrDefault();
        if (threat is not null
            && body.Position.ChebyshevDistance(threat.Position) <= 4
            && TryDenialSignature(contract, mind, body, threat))
        {
            return;
        }
        if (ArenaBasics.TryShoot(contract, mind, body, threat ?? enemyCarrier))
            return;
        if (body.Position.ChebyshevDistance(well.Position) <= 2
            && TryControlSignature(contract, body, well.Position))
        {
            return;
        }
        Position target = _strategy switch
        {
            Strategy.Fortress when plan.Role is not "carrier" =>
                PatrolPoint(
                    contract.Map,
                    well.Position,
                    body.UnitId,
                    mind.Tick),
            Strategy.Fireline when plan.Role == "intercept" =>
                Mirror(plan.OutboundPath[Math.Min(1, plan.OutboundPath.Length - 1)]),
            Strategy.Sustain when hostileSignature is not null =>
                hostileSignature.Positions
                    .OrderBy(position =>
                        body.Position.ChebyshevDistance(position))
                    .ThenBy(position => position.Y)
                    .ThenBy(position => position.X)
                    .First(),
            _ => PatrolPoint(
                contract.Map,
                well.Position,
                body.UnitId,
                mind.Tick),
        };
        if (TryAdvanceSignature(
                contract,
                body,
                target,
                $"dash into the {theater} assignment"))
        {
            return;
        }
        if (ArenaBasics.TryMoveToward(
                contract,
                mind,
                body,
                [target],
                claims,
                $"patrolling {theater} theater"))
        {
            return;
        }
        if (ArenaBasics.TryEvade(contract, mind, body, claims))
            return;
        body.Hold($"watching {theater} contest");
    }

    private Dictionary<int, GenericActorContext.ArcRelayCoreState> AssignPickups(
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        IReadOnlyDictionary<int, UnitPlan> plans,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        var result = new Dictionary<int, GenericActorContext.ArcRelayCoreState>();
        var assigned = new HashSet<int>();
        GenericActorContext.ArcRelayCoreState[] loose = arc.VisibleCores
            .Where(core =>
                core.Disposition == GenericActorContext.ArcRelayCoreDisposition.Loose)
            .OrderBy(core => CoreOrder(core.CoreId.SourceWellId))
            .ThenBy(core => core.CoreId.SourceOrdinal)
            .ToArray();
        foreach (GenericActorContext.ArcRelayCoreState core in loose)
        {
            string theater = Theater(core.CoreId.SourceWellId);
            MindBody? selected = mind.Bodies
                .Where(body =>
                    !carried.ContainsKey(body.ActorId)
                    && !assigned.Contains(body.UnitId))
                .OrderBy(body => PickupRoleRank(
                    plans[body.UnitId], theater, mind.Tick))
                .ThenBy(body =>
                    body.Position.ChebyshevDistance(core.Position))
                .ThenByDescending(body => body.Health)
                .ThenBy(body => body.UnitId)
                .FirstOrDefault();
            if (selected is null)
                continue;
            assigned.Add(selected.UnitId);
            result[selected.UnitId] = core;
        }
        return result;
    }

    private bool ShouldIntercept(
        UnitPlan plan,
        MindBody body,
        GenericActorContext.ObservedEnemyState? carrier)
    {
        if (carrier is null
            || !_director!.InterceptionPolicy(plan).FocusEnemyCarrier)
            return false;
        if (_strategy == Strategy.Interception)
            return true;
        bool assignedInterceptor = string.Equals(
            plan.Role,
            "intercept",
            StringComparison.Ordinal);
        int distance = body.Position.ChebyshevDistance(carrier.Position);
        if (_strategy == Strategy.Disruption)
        {
            return plan.Role is not "carrier"
                && distance <= 8;
        }
        if (!assignedInterceptor)
        {
            return _strategy == Strategy.Split
                && string.Equals(
                    plan.Theater,
                    Theater(carrier.Position),
                    StringComparison.Ordinal)
                && plan.Role is not "carrier"
                && distance <= 7;
        }
        return _strategy switch
        {
            Strategy.Fortress =>
                carrier.Position.ChebyshevDistance(_ownReactor) <= 9,
            Strategy.Ambush => string.Equals(
                    plan.Theater,
                    Theater(carrier.Position),
                    StringComparison.Ordinal)
                && distance <= 7,
            Strategy.Fireline => distance <= 12,
            _ => true,
        };
    }

    private bool ShouldEscort(
        UnitPlan plan,
        MindBody? partnerCarrier,
        MindBody? nearestCarrier)
    {
        if (partnerCarrier is not null
            && string.Equals(plan.Role, "screen", StringComparison.Ordinal))
        {
            return true;
        }
        return (_strategy == Strategy.Escort
                || _strategy == Strategy.Sustain
                || _strategy == Strategy.Fortress
                || _strategy == Strategy.RelayChain)
            && nearestCarrier is not null
            && string.Equals(plan.Role, "screen", StringComparison.Ordinal);
    }

    private static bool TryDenialSignature(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        GenericActorContext.ObservedEnemyState carrier)
    {
        if (ArenaBasics.TryUnitSignature(
                contract,
                body,
                "target-paint",
                carrier.ActorId,
                "paint enemy carrier"))
        {
            return true;
        }
        if (ArenaBasics.TryHeadingSignature(
                contract,
                body,
                "tractor-hook",
                carrier.Position,
                "pull enemy carrier off route"))
        {
            return true;
        }
        bool allyAdjacent = mind.Allies.Any(ally =>
            ally.Position.ChebyshevDistance(body.Position) <= 1);
        if (!allyAdjacent
            && body.Position.ChebyshevDistance(carrier.Position) <= 1
            && ArenaBasics.TryParameterlessSignature(
                contract,
                body,
                "kinetic-burst",
                "burst isolated carrier"))
        {
            return true;
        }
        if (body.Position.ChebyshevDistance(carrier.Position) <= 3
            && ArenaBasics.TryParameterlessSignature(
                contract,
                body,
                "null-field",
                "suppress carrier signatures"))
        {
            return true;
        }
        if (ArenaBasics.TryPositionSignature(
                contract,
                body,
                "falling-star",
                carrier.Position,
                "lead enemy carrier with Falling Star"))
        {
            return true;
        }
        if (ArenaBasics.TryHeadingSignature(
                contract,
                body,
                "rail-line",
                carrier.Position,
                "charge carrier cutline"))
        {
            return true;
        }
        return ArenaBasics.TryPositionSignature(
            contract,
            body,
            "hardlight-block",
            carrier.Position,
            "block carrier return lane");
    }

    private static bool TrySupportSignature(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        MindBody carrier,
        GenericActorContext.ObservedEnemyState? threat)
    {
        if (ArenaBasics.TryUnitSignature(
                contract,
                body,
                "repair-beam",
                carrier.ActorId,
                "repair live carrier"))
        {
            return true;
        }
        if (threat is not null
            && threat.Position.ChebyshevDistance(carrier.Position) <= 3
            && ArenaBasics.TryPositionSignature(
                contract,
                body,
                "smoke-canister",
                carrier.Position,
                "screen threatened carrier"))
        {
            return true;
        }
        if (threat is not null
            && ArenaBasics.TryDirectionSignature(
                contract,
                body,
                "prism-wall",
                threat.Position,
                "raise projectile cover toward a carrier threat"))
        {
            return true;
        }
        if (threat is not null
            && threat.Position.ChebyshevDistance(carrier.Position) <= 3
            && ArenaBasics.TryParameterlessSignature(
                contract,
                body,
                "null-field",
                "suppress threats beside carrier"))
        {
            return true;
        }
        return threat is not null
            && ArenaBasics.TryPositionSignature(
                contract,
                body,
                "hardlight-block",
                carrier.Position,
                "cover carrier return lane");
    }

    private static bool TryControlSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Position well) =>
        ArenaBasics.TryPositionSignature(
            contract,
            body,
            "trip-node",
            well,
            "seed a Well approach")
        || ArenaBasics.TryPositionSignature(
            contract,
            body,
            "sentinel-seed",
            well,
            "guard a Well approach");

    private static bool TryAdvanceSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Position target,
        string reason) =>
        body.Position.ChebyshevDistance(target) >= 3
        && ArenaBasics.TryHeadingSignature(
            contract,
            body,
            "vector-dash",
            target,
            reason);

    private static bool TryDrop(MindBody body)
    {
        GenericActorActionLegality? drop = body.Action("drop-core");
        if (drop is not { Available: true } || !drop.Constraints.IsEmpty)
            return false;
        body.Command(drop);
        return true;
    }

    private void UpdateCarrierMotion(
        MindContext mind,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        HashSet<int> current = [];
        foreach ((ActorIdentity actor,
                     GenericActorContext.ArcRelayCoreState core) in carried
                     .Where(value => value.Key.TeamId == _teamId))
        {
            MindBody? body = mind.Bodies.FirstOrDefault(value =>
                value.ActorId == actor);
            if (body is null)
                continue;
            current.Add(body.UnitId);
            string coreKey = $"{core.CoreId.SourceWellId}:"
                + core.CoreId.SourceOrdinal;
            int distance = body.Position.ChebyshevDistance(_ownReactor);
            CarrierMotion? prior = _carrierMotion.GetValueOrDefault(body.UnitId);
            if (prior is not null
                && prior.ActorId == actor
                && string.Equals(prior.CoreKey, coreKey,
                    StringComparison.Ordinal))
            {
                bool improved = distance < prior.BestDistance;
                _carrierMotion[body.UnitId] = prior with
                {
                    Position = body.Position,
                    StationaryTicks = prior.Position == body.Position
                        ? prior.StationaryTicks + 1
                        : 1,
                    BestDistance = Math.Min(prior.BestDistance, distance),
                    NoProgressTicks = improved
                        ? 0
                        : prior.NoProgressTicks + 1,
                };
            }
            else
            {
                _carrierMotion[body.UnitId] = new CarrierMotion(
                    actor,
                    coreKey,
                    body.Position,
                    1,
                    distance,
                    0);
            }
        }
        foreach (int stale in _carrierMotion.Keys
                     .Where(unitId => !current.Contains(unitId)).ToArray())
        {
            _carrierMotion.Remove(stale);
        }
    }

    private GenericActorContext.ArcRelayWellState WellFor(
        GenericActorContext.ModeObservationState.ArcRelay arc,
        string theater) =>
        arc.Wells.FirstOrDefault(well => string.Equals(
            Theater(well.WellId), theater, StringComparison.Ordinal))
        ?? arc.Wells.OrderBy(well => well.WellId, StringComparer.Ordinal).First();

    private static Position PatrolPoint(
        GenericActorMapContract map,
        Position well,
        int unitId,
        int tick)
    {
        Position[] ring = ArenaBasics.ApproachTiles(map, well);
        if (ring.Length == 0)
            return well;
        int patrolStep = (tick + unitId * 2) / 6;
        int index = Math.Abs(unitId * 3 + patrolStep) % ring.Length;
        return ring[index];
    }

    private static Strategy StrategyFrom(string sheetId)
    {
        if (sheetId.Contains("rear-ambush", StringComparison.OrdinalIgnoreCase))
            return Strategy.Ambush;
        if (sheetId.Contains("well-rotation", StringComparison.OrdinalIgnoreCase))
            return Strategy.Split;
        if (sheetId.Contains(
                "escort-counterpunch",
                StringComparison.OrdinalIgnoreCase))
        {
            return Strategy.Interception;
        }
        if (sheetId.Contains("relay-chain", StringComparison.OrdinalIgnoreCase))
            return Strategy.RelayChain;
        if (sheetId.Contains("fortress", StringComparison.OrdinalIgnoreCase))
            return Strategy.Fortress;
        if (sheetId.Contains("trap-punish", StringComparison.OrdinalIgnoreCase))
            return Strategy.Ambush;
        if (sheetId.Contains("fireline", StringComparison.OrdinalIgnoreCase))
            return Strategy.Fireline;
        if (sheetId.Contains("displacement", StringComparison.OrdinalIgnoreCase))
            return Strategy.Disruption;
        if (sheetId.Contains("sustain", StringComparison.OrdinalIgnoreCase))
            return Strategy.Sustain;
        if (sheetId.Contains("feint", StringComparison.OrdinalIgnoreCase))
            return Strategy.Feint;
        if (sheetId.Contains("control-grid", StringComparison.OrdinalIgnoreCase))
            return Strategy.ControlGrid;
        if (sheetId.Contains("convoy", StringComparison.OrdinalIgnoreCase))
            return Strategy.Escort;
        if (sheetId.Contains("intercept", StringComparison.OrdinalIgnoreCase))
            return Strategy.Interception;
        return sheetId.Contains("split", StringComparison.OrdinalIgnoreCase)
            ? Strategy.Split
            : Strategy.Balanced;
    }

    private static string StrategyCode(Strategy strategy) => strategy switch
    {
        Strategy.Balanced => "bal",
        Strategy.Split => "spl",
        Strategy.Escort => "esc",
        Strategy.Interception => "int",
        Strategy.ControlGrid => "grd",
        Strategy.RelayChain => "rly",
        Strategy.Fortress => "for",
        Strategy.Ambush => "amb",
        Strategy.Fireline => "fir",
        Strategy.Disruption => "dsp",
        Strategy.Sustain => "sus",
        _ => "fnt",
    };

    private static string RoleCode(string role) => role switch
    {
        "carrier" => "car",
        "screen" => "scr",
        "intercept" => "int",
        "reserve" => "res",
        _ => "pat",
    };

    private static string TheaterCode(string theater) => theater switch
    {
        "north" => "n",
        "south" => "s",
        _ => "c",
    };

    private int PickupRoleRank(
        UnitPlan plan,
        string theater,
        int tick) =>
        (string.Equals(
            EffectiveTheater(plan.Theater, tick),
            theater,
            StringComparison.Ordinal)
                ? 0
                : (_director!.CarrierPolicy(plan).PreferAssignedTheater ? 4 : 1))
        + (plan.Role switch
        {
            "carrier" => 0,
            "reserve" => 1,
            "screen" => 2,
            _ => 3,
        });

    private static int Priority(string role) => role switch
    {
        "carrier" => 0,
        "screen" => 1,
        "intercept" => 2,
        _ => 3,
    };

    private static int CoreOrder(string wellId) => Theater(wellId) switch
    {
        "centre" => 0,
        "north" => 1,
        _ => 2,
    };

    private static string Theater(string wellId) =>
        wellId.Contains("north", StringComparison.OrdinalIgnoreCase)
            ? "north"
            : wellId.Contains("south", StringComparison.OrdinalIgnoreCase)
                ? "south"
                : "centre";

    private static string Theater(Position position) =>
        position.Y <= 7 ? "north" : position.Y >= 15 ? "south" : "centre";

    private string EffectiveTheater(string theater, int tick)
    {
        if (_strategy != Strategy.Feint || tick < 150)
            return theater;
        return theater switch
        {
            "north" => "south",
            "south" => "north",
            _ => theater,
        };
    }

    private Position Mirror(Position position) => _mirror
        ? new Position(_contract!.Map.Width - 1 - position.X, position.Y)
        : position;

    private enum Strategy
    {
        Balanced,
        Split,
        Escort,
        Interception,
        ControlGrid,
        RelayChain,
        Fortress,
        Ambush,
        Fireline,
        Disruption,
        Sustain,
        Feint,
    }

    private sealed record CarrierMotion(
        ActorIdentity ActorId,
        string CoreKey,
        Position Position,
        int StationaryTicks,
        int BestDistance,
        int NoProgressTicks);
}
