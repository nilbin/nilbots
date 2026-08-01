using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// Split-control Arc Relay doctrine. Three independent theater pairs keep
/// working even when another route stalls, while a cadence reserve moves to
/// the next public Well event and a route-cover body shadows a live carrier.
/// </summary>
public sealed class SplitControlMind : IGenericMindBot
{
    private readonly Dictionary<GenericActorContext.ArcRelayCoreId, CoreMemory>
        _coreMemory = [];
    private readonly Dictionary<string, GenericActorRulesContract.ArcRelaySignature>
        _signatureByClass = new(StringComparer.Ordinal);

    private GenericActorResolvedMatchContract? _contract;
    private ImmutableArray<Theater> _theaters = [];
    private Position _reactor;
    private Direction _forward;
    private int _teamId;

    public void StartMatch(MindStart start)
    {
        _contract = start.Contract;
        _teamId = start.TeamId;

        if (start.Contract.Rules.GameMode
                is not GenericActorRulesContract.ArcRelayGameMode arcRules
            || start.Contract.ModeMapBinding
                is not GenericActorResolvedMatchContract.ArcRelayModeMapBinding
                    binding)
        {
            throw new InvalidOperationException(
                "SplitControlMind requires an Arc Relay contract.");
        }

        GenericActorResolvedMatchContract.ParticipantRegionAssignment reactor =
            start.Contract.ParticipantRegionAssignments.Single(assignment =>
                assignment.ParticipantId == start.ParticipantId
                && string.Equals(
                    assignment.RegionRoleId,
                    binding.ReactorRegionRoleId,
                    StringComparison.Ordinal));
        GenericActorMapContract.Region reactorRegion =
            start.Contract.Map.Regions.Single(region =>
                string.Equals(
                    region.RegionId,
                    reactor.MapRegionId,
                    StringComparison.Ordinal));
        _reactor = reactorRegion.Tiles
            .OrderBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .First();
        _forward = reactor.Facing;

        int theaterCount = Math.Min(
            binding.OrderedWellRegionIds.Length,
            arcRules.Wells.Length);
        var theaters = ImmutableArray.CreateBuilder<Theater>(theaterCount);
        for (int index = 0; index < theaterCount; index++)
        {
            string regionId = binding.OrderedWellRegionIds[index];
            GenericActorMapContract.Region region =
                start.Contract.Map.Regions.Single(candidate =>
                    string.Equals(
                        candidate.RegionId,
                        regionId,
                        StringComparison.Ordinal));
            Position position = region.Tiles
                .OrderBy(tile => tile.Y)
                .ThenBy(tile => tile.X)
                .First();
            theaters.Add(new Theater(
                index,
                arcRules.Wells[index].WellId,
                position));
        }
        _theaters = theaters.ToImmutable();

        foreach (GenericActorRulesContract.ArcRelaySignature signature
                 in arcRules.Signatures)
        {
            _signatureByClass[signature.ClassId] = signature;
        }
    }

    public void Think(MindContext mind)
    {
        GenericActorResolvedMatchContract contract = _contract
            ?? throw new InvalidOperationException("StartMatch was not called.");
        if (mind.Mode is not GenericActorContext.ModeObservationState.ArcRelay mode)
        {
            foreach (MindBody body in mind.Bodies)
                body.Hold("unsupported mode");
            return;
        }

        ObserveCores(mind.Tick, mode);
        var traffic = ArcNavigation.Traffic.ForTick(contract, mind);
        Dictionary<int, RolePlan> roles = mind.Bodies.ToDictionary(
            body => body.UnitId,
            body => PlanRole(body, mode));
        Dictionary<int, CoreMemory> pickupPlans = AssignPickups(mind, roles);
        Dictionary<ActorIdentity, GenericActorContext.ArcRelayCoreState> carried =
            mode.VisibleCores
                .Where(core => core.CarrierActorId is not null)
                .ToDictionary(core => core.CarrierActorId!, core => core);

        GenericActorContext.ObservedEnemyState? enemyCarrier =
            VisibleEnemyCarrier(mind, carried);
        MindBody? ownCarrier = mind.Bodies
            .Where(body => carried.ContainsKey(body.ActorId))
            .OrderBy(body => body.Position.ChebyshevDistance(_reactor))
            .ThenBy(body => body.UnitId)
            .FirstOrDefault();

        foreach (MindBody body in mind.Bodies
                     .OrderByDescending(candidate =>
                         carried.ContainsKey(candidate.ActorId))
                     .ThenBy(candidate => DutyOrder(roles[candidate.UnitId].Duty))
                     .ThenBy(candidate => candidate.UnitId))
        {
            RolePlan role = roles[body.UnitId];
            if (!string.Equals(body.RoleTag, role.Tag, StringComparison.Ordinal))
                body.SetRole(role.Tag);

            if (carried.TryGetValue(body.ActorId, out var core))
            {
                ActCarrier(contract, mind, mode, body, core, traffic);
                continue;
            }

            if (pickupPlans.TryGetValue(body.UnitId, out CoreMemory? pickup))
            {
                ActPickup(contract, mind, mode, body, pickup, traffic);
                continue;
            }

            if (role.Duty is Duty.Guard or Duty.Denial or Duty.Cover
                && enemyCarrier is not null)
            {
                ActDenial(
                    contract,
                    mind,
                    mode,
                    body,
                    role,
                    enemyCarrier,
                    traffic);
                continue;
            }

            if (role.Duty == Duty.Cover && ownCarrier is not null)
            {
                ActCover(
                    contract,
                    mind,
                    mode,
                    body,
                    ownCarrier,
                    traffic);
                continue;
            }

            ActTheater(contract, mind, mode, body, role, traffic);
        }

        mind.Debug.Write(
            $"split-control: {mind.Bodies.Length} live, "
            + $"{pickupPlans.Count} pickups, reserve={CadenceTheater(mode).WellId}");
    }

    public void EndMatch(MindEnd end) => _ = end;

    private void ActCarrier(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay mode,
        MindBody body,
        GenericActorContext.ArcRelayCoreState core,
        ArcNavigation.Traffic traffic)
    {
        if (TryCarrierSignature(contract, mind, mode, body, core))
            return;
        if (ArcNavigation.TryMove(
                contract,
                mind,
                body,
                [_reactor],
                traffic,
                _forward,
                "returning core"))
        {
            return;
        }
        body.Hold(core.NextRelocationTick > mind.Tick
            ? "core recovery"
            : "reactor route held");
    }

    private void ActPickup(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay mode,
        MindBody body,
        CoreMemory pickup,
        ArcNavigation.Traffic traffic)
    {
        Position target = pickup.Position;
        if (TrySignature(contract, mind, mode, body, target, null))
            return;
        if (ArcNavigation.TryShoot(contract, mind, body, PriorityTarget(mind, null)))
            return;
        if (ArcNavigation.TryMove(
                contract,
                mind,
                body,
                [target],
                traffic,
                _forward,
                $"distributed pickup {pickup.CoreId.SourceWellId}"))
        {
            return;
        }
        body.Hold("holding pickup lane");
    }

    private void ActDenial(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay mode,
        MindBody body,
        RolePlan role,
        GenericActorContext.ObservedEnemyState carrier,
        ArcNavigation.Traffic traffic)
    {
        if (TrySignature(contract, mind, mode, body, carrier.Position, carrier))
            return;
        if (ArcNavigation.TryShoot(contract, mind, body, carrier))
            return;

        Position[] approach = ArcNavigation.Ring(
            contract.Map,
            carrier.Position,
            1);
        if (ArcNavigation.TryMove(
                contract,
                mind,
                body,
                approach,
                traffic,
                _forward,
                $"{role.Tag} intercept"))
        {
            return;
        }
        body.Hold("carrier lane denied");
    }

    private void ActCover(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay mode,
        MindBody body,
        MindBody carrier,
        ArcNavigation.Traffic traffic)
    {
        GenericActorContext.ObservedEnemyState? threat = mind.Enemies
            .OrderBy(enemy =>
                enemy.Position.ChebyshevDistance(carrier.Position))
            .ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault();
        if (TrySignature(
                contract,
                mind,
                mode,
                body,
                carrier.Position,
                threat))
        {
            return;
        }
        if (ArcNavigation.TryShoot(contract, mind, body, threat))
            return;
        Position[] escort = ArcNavigation.Ring(
            contract.Map,
            carrier.Position,
            1);
        if (ArcNavigation.TryMove(
                contract,
                mind,
                body,
                escort,
                traffic,
                _forward,
                "covering carrier"))
        {
            return;
        }
        body.Hold("carrier cover");
    }

    private void ActTheater(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay mode,
        MindBody body,
        RolePlan role,
        ArcNavigation.Traffic traffic)
    {
        Theater theater = role.Duty is Duty.Reserve or Duty.Cover
            ? CadenceTheater(mode)
            : TheaterAt(role.TheaterIndex);
        Position target = Well(mode, theater).Position;

        if (TrySignature(contract, mind, mode, body, target, null))
            return;
        if (ArcNavigation.TryShoot(contract, mind, body, PriorityTarget(mind, null)))
            return;

        Position[] goals = role.Duty is Duty.Guard or Duty.Denial
            ? ArcNavigation.Ring(contract.Map, target, 1)
            : [target];
        if (ArcNavigation.TryMove(
                contract,
                mind,
                body,
                goals,
                traffic,
                _forward,
                role.Duty == Duty.Reserve
                    ? $"rotating for {theater.WellId} cadence"
                    : $"holding {theater.WellId} theater"))
        {
            return;
        }
        body.Hold($"{role.Tag} set");
    }

    private bool TryCarrierSignature(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay mode,
        MindBody body,
        GenericActorContext.ArcRelayCoreState core)
    {
        if (!_signatureByClass.TryGetValue(body.ClassId ?? "", out var signature))
            return false;
        GenericActorActionLegality? action = body.Action(signature.ActionId);
        if (action is not { Available: true })
            return false;

        _ = contract;
        _ = mode;
        _ = core;
        return false;
    }

    private bool TrySignature(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay mode,
        MindBody body,
        Position focus,
        GenericActorContext.ObservedEnemyState? priorityEnemy)
    {
        if (!_signatureByClass.TryGetValue(body.ClassId ?? "", out var signature))
            return false;
        GenericActorActionLegality? action = body.Action(signature.ActionId);
        if (action is not { Available: true })
            return false;

        if (string.Equals(signature.Kind, "vector-dash", StringComparison.Ordinal)
            && body.Position.ChebyshevDistance(focus) >= 7
            && ArcNavigation.TryHeading(body.Position, focus, out var dashHeading))
        {
            var headings = action.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
            if (headings?.AllowedValues.Contains(dashHeading) == true
                && ArcNavigation.ClearRay(contract.Map, body.Position, focus))
            {
                body.Command(
                    action,
                    new GenericActorActionArgument.ProjectileHeadingArgument(
                        dashHeading));
                return true;
            }
        }

        if (string.Equals(signature.Kind, "tractor-hook", StringComparison.Ordinal)
            && priorityEnemy is not null
            && ArcNavigation.TryHeading(
                body.Position,
                priorityEnemy.Position,
                out var hookHeading)
            && body.Position.ChebyshevDistance(priorityEnemy.Position)
                <= (signature.Range ?? 0))
        {
            var headings = action.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
            if (headings?.AllowedValues.Contains(hookHeading) == true)
            {
                body.Command(
                    action,
                    new GenericActorActionArgument.ProjectileHeadingArgument(
                        hookHeading));
                return true;
            }
        }

        _ = mind;
        _ = mode;
        return false;
    }

    private Dictionary<int, CoreMemory> AssignPickups(
        MindContext mind,
        IReadOnlyDictionary<int, RolePlan> roles)
    {
        CoreMemory[] available = _coreMemory.Values
            .Where(core =>
                core.Disposition == GenericActorContext.ArcRelayCoreDisposition.Loose)
            .OrderBy(core => TheaterIndex(core.CoreId.SourceWellId))
            .ThenBy(core => core.CoreId.SourceOrdinal)
            .ToArray();
        MindBody[] candidates = mind.Bodies
            .Where(body => roles[body.UnitId].Duty
                is Duty.Runner or Duty.Reserve or Duty.Cover)
            .ToArray();

        var result = new Dictionary<int, CoreMemory>();
        var assignedUnits = new HashSet<int>();
        foreach (CoreMemory core in available)
        {
            int theater = TheaterIndex(core.CoreId.SourceWellId);
            MindBody? selected = candidates
                .Where(body => !assignedUnits.Contains(body.UnitId))
                .OrderBy(body => roles[body.UnitId].TheaterIndex == theater ? 0 : 1)
                .ThenBy(body => roles[body.UnitId].Duty == Duty.Runner ? 0 : 1)
                .ThenBy(body => body.Position.ChebyshevDistance(core.Position))
                .ThenBy(body => body.UnitId)
                .FirstOrDefault();
            if (selected is null)
                continue;
            assignedUnits.Add(selected.UnitId);
            result[selected.UnitId] = core;
        }
        return result;
    }

    private void ObserveCores(
        int tick,
        GenericActorContext.ModeObservationState.ArcRelay mode)
    {
        HashSet<GenericActorContext.ArcRelayCoreId> outstanding = mode.Wells
            .Where(well => well.OutstandingCoreId is not null)
            .Select(well => well.OutstandingCoreId!)
            .ToHashSet();
        foreach (var stale in _coreMemory.Keys
                     .Where(coreId => !outstanding.Contains(coreId))
                     .ToArray())
        {
            _coreMemory.Remove(stale);
        }

        foreach (GenericActorContext.ArcRelayCoreState core in mode.VisibleCores)
        {
            _coreMemory[core.CoreId] = new CoreMemory(
                core.CoreId,
                core.Position,
                core.Disposition,
                core.CarrierActorId,
                core.NextRelocationTick,
                tick);
        }

        foreach (GenericActorContext.ArcRelayWellState well in mode.Wells)
        {
            if (well.OutstandingCoreId is not { } coreId
                || _coreMemory.ContainsKey(coreId))
            {
                continue;
            }
            _coreMemory[coreId] = new CoreMemory(
                coreId,
                well.Position,
                GenericActorContext.ArcRelayCoreDisposition.Loose,
                null,
                tick,
                tick);
        }
    }

    private GenericActorContext.ObservedEnemyState? VisibleEnemyCarrier(
        MindContext mind,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        return mind.Enemies
            .Where(enemy =>
                enemy.ActorId.TeamId != _teamId
                && carried.ContainsKey(enemy.ActorId))
            .OrderBy(enemy => enemy.Position.ChebyshevDistance(_reactor))
            .ThenBy(enemy => enemy.Health)
            .ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault();
    }

    private static GenericActorContext.ObservedEnemyState? PriorityTarget(
        MindContext mind,
        GenericActorContext.ObservedEnemyState? priority) =>
        priority ?? mind.Enemies
            .OrderBy(enemy => enemy.Health)
            .ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault();

    private RolePlan PlanRole(
        MindBody body,
        GenericActorContext.ModeObservationState.ArcRelay mode)
    {
        int north = Math.Min(1, Math.Max(0, _theaters.Length - 1));
        int south = Math.Min(2, Math.Max(0, _theaters.Length - 1));
        return body.ClassId switch
        {
            "kestrel" => new RolePlan("north-runner", north, Duty.Runner),
            "palisade" => new RolePlan("north-guard", north, Duty.Guard),
            "relay" => new RolePlan("centre-runner", 0, Duty.Runner),
            "minesmith" => new RolePlan("centre-denial", 0, Duty.Denial),
            "towline" => new RolePlan("south-runner", south, Duty.Runner),
            "hush" => new RolePlan("south-denial", south, Duty.Denial),
            "lantern" => new RolePlan(
                "cadence-reserve",
                CadenceTheater(mode).Index,
                Duty.Reserve),
            "veil" => new RolePlan(
                "route-cover",
                CadenceTheater(mode).Index,
                Duty.Cover),
            _ => new RolePlan(
                "distributed-reserve",
                body.UnitId % Math.Max(1, _theaters.Length),
                Duty.Reserve),
        };
    }

    private Theater CadenceTheater(
        GenericActorContext.ModeObservationState.ArcRelay mode)
    {
        GenericActorContext.ArcRelayWellState? selected = mode.Wells
            .OrderBy(well => well.PendingCharge ? 0 : 1)
            .ThenBy(well => well.NextScheduledBirthTick ?? int.MaxValue)
            .ThenBy(well => TheaterIndex(well.WellId))
            .FirstOrDefault();
        return selected is null
            ? TheaterAt(0)
            : TheaterAt(TheaterIndex(selected.WellId));
    }

    private GenericActorContext.ArcRelayWellState Well(
        GenericActorContext.ModeObservationState.ArcRelay mode,
        Theater theater) =>
        mode.Wells.FirstOrDefault(well =>
            string.Equals(well.WellId, theater.WellId, StringComparison.Ordinal))
        ?? new GenericActorContext.ArcRelayWellState(
            theater.WellId,
            theater.Position,
            null,
            null,
            false,
            null);

    private int TheaterIndex(string wellId)
    {
        Theater? theater = _theaters.FirstOrDefault(candidate =>
            string.Equals(candidate.WellId, wellId, StringComparison.Ordinal));
        return theater?.Index ?? 0;
    }

    private Theater TheaterAt(int index) =>
        _theaters.Length == 0
            ? new Theater(0, "unknown", _reactor)
            : _theaters[Math.Clamp(index, 0, _theaters.Length - 1)];

    private int LocalX(Position position) =>
        _forward == Direction.East ? position.X : -position.X;

    private static int DutyOrder(Duty duty) => duty switch
    {
        Duty.Runner => 0,
        Duty.Guard => 1,
        Duty.Denial => 2,
        Duty.Reserve => 3,
        _ => 4,
    };

    private enum Duty
    {
        Runner,
        Guard,
        Denial,
        Reserve,
        Cover,
    }

    private sealed record RolePlan(string Tag, int TheaterIndex, Duty Duty);

    private sealed record Theater(int Index, string WellId, Position Position);

    private sealed record CoreMemory(
        GenericActorContext.ArcRelayCoreId CoreId,
        Position Position,
        GenericActorContext.ArcRelayCoreDisposition Disposition,
        ActorIdentity? CarrierActorId,
        int NextRelocationTick,
        int SeenAtTick);
}
