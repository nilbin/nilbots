using BotArena.Sdk;

/// <summary>
/// Direct executor for Arc Relay Orders v2. The authored tree decides whether
/// a body moves, fights, uses its signature, hands off, or drops. This class
/// owns deterministic target resolution and micro only; it has no doctrine,
/// custody, group, operation, engagement, mode, or rung plane.
/// </summary>
public sealed class ArcRelayTacticalPlaybookMind : IGenericMindBot
{
    private readonly OrderTreeRuntime _orders = new();
    private readonly TacticalRouteGovernor _routes = new();
    private readonly TacticalTrafficGovernor _traffic = new();
    private readonly TacticalOscillationGovernor _oscillation = new();
    private readonly Dictionary<int, RouteCursor> _routeCursors = [];
    private readonly Dictionary<int, string> _selectedPaths = [];
    private readonly Dictionary<int, int> _stationaryTicks = [];
    private readonly Dictionary<int, (ActorIdentity Actor, int Level)> _levels = [];
    private readonly Dictionary<int, int> _pendingInvest = [];
    private readonly Dictionary<int, (ActorIdentity Actor, int Spent)> _invested = [];
    private readonly Dictionary<int, PendingOneShot> _pendingOneShots = [];
    private readonly Dictionary<(ActorIdentity Actor, string Path), Position>
        _fightAnchors = [];
    private readonly Dictionary<string, KnownCore> _knownLooseCores =
        new(StringComparer.Ordinal);
    private readonly Dictionary<ActorIdentity, RememberedEnemy> _rememberedEnemies = [];
    private readonly HashSet<string> _processedEvents = new(StringComparer.Ordinal);

    private GenericActorResolvedMatchContract? _contract;
    private OrdersPackage? _package;
    private IArenaStepper _stepper = new GreedyArenaStepper();
    private Position _ownReactor;
    private Position _enemyReactor;
    private Position[] _healTiles = [];
    private bool _mirrored;
    private int _teamId;
    private int _lastOwnCharge;
    private int _lastObjectiveProgressTick;

    public void StartMatch(MindStart start)
    {
        _contract = start.Contract;
        _teamId = start.TeamId;
        if (start.Contract.ModeMapBinding is not
            GenericActorResolvedMatchContract.ArcRelayModeMapBinding binding)
        {
            throw new InvalidOperationException("OrdersMind requires Arc Relay.");
        }
        GenericActorResolvedMatchContract.ParticipantRegionAssignment own =
            start.Contract.ParticipantRegionAssignments.Single(value =>
                value.ParticipantId == start.ParticipantId
                && string.Equals(value.RegionRoleId,
                    binding.ReactorRegionRoleId, StringComparison.Ordinal));
        _ownReactor = start.Contract.Map.Regions.Single(value =>
                string.Equals(value.RegionId, own.MapRegionId,
                    StringComparison.Ordinal))
            .Tiles.Single();
        _enemyReactor = start.Contract.Map.Regions
            .Where(value => value.Kind
                == GenericActorMapContract.RegionKind.Objective)
            .Where(value => value.Tiles.Length == 1
                && value.Tiles[0] != _ownReactor)
            .Where(value => value.RegionId.Contains(
                "reactor", StringComparison.OrdinalIgnoreCase))
            .OrderBy(value => value.RegionId, StringComparer.Ordinal)
            .Select(value => value.Tiles[0])
            .First();
        _healTiles = start.Contract.Map.Regions
            .Where(region => region.RegionId.StartsWith(
                "heal-", StringComparison.Ordinal))
            .SelectMany(region => region.Tiles)
            .OrderBy(value => value.Y)
            .ThenBy(value => value.X)
            .ToArray();
        _mirrored = _ownReactor.X > (start.Contract.Map.Width - 1) / 2;
        _package = OrdersPackage.Load(
            start.EvaluationData, start.Contract, _ownReactor);
        _stepper = _package.StepperMode == "coordinated"
            ? new CoordinatedArenaStepper()
            : new GreedyArenaStepper();
        ValidateComposition(start.Contract, start.TeamId, _package);
    }

    public void Think(MindContext mind)
    {
        GenericActorResolvedMatchContract contract = _contract
            ?? throw new InvalidOperationException("StartMatch was not called.");
        OrdersPackage package = _package
            ?? throw new InvalidOperationException("Sheet was not loaded.");
        if (mind.Mode is not GenericActorContext.ModeObservationState.ArcRelay arc)
        {
            foreach (MindBody body in mind.Bodies)
                body.Hold("orders:unsupported-mode");
            return;
        }

        UpdateMemory(contract, mind, arc);
        ResolvePreviousOneShots(mind);
        UpdateMotionMemory(mind);
        IReadOnlySet<ActorIdentity> live = mind.Bodies
            .Select(body => body.ActorId).ToHashSet();
        _orders.Prune(live);

        Dictionary<ActorIdentity, GenericActorContext.ArcRelayCoreState> carried =
            arc.VisibleCores
                .Where(core => core.Disposition
                    == GenericActorContext.ArcRelayCoreDisposition.Carried
                    && core.CarrierActorId is not null)
                .ToDictionary(core => core.CarrierActorId!, core => core);

        var cursors = new Dictionary<int, ProposalCursor>();
        foreach (MindBody body in mind.Bodies.OrderBy(value => value.UnitId))
        {
            OrdersPackage.Role role = package.RolesByUnit[body.UnitId];
            body.SetRole(role.Id.Length <= 24 ? role.Id : role.Id[..24]);
            IReadOnlyList<OrderTreeRuntime.EligibleLeaf> leaves = _orders.Eligible(
                body.ActorId,
                role,
                mind.Tick,
                (expression, orderTicks) => Evaluate(
                    contract, mind, arc, package, body, role,
                    expression, orderTicks, carried));
            List<Proposal> proposals = BuildProposals(
                contract, mind, arc, package, body, role, leaves, carried);
            cursors.Add(body.UnitId, new ProposalCursor(proposals));
        }
        Dictionary<int, Proposal> selected = Allocate(package, cursors);
        ReconcileSelections(mind, selected);
        Dictionary<int, MovePlan> movePlans = PrepareMovePlans(
            contract, mind, package, selected);

        _traffic.Observe(
            mind.Tick,
            BuildTrafficSamples(contract, mind, selected, movePlans));
        _oscillation.Observe(
            mind.Tick,
            mind.Bodies.Select(body => new TacticalOscillationGovernor.Sample(
                body.ActorId,
                body.Position,
                body.PreviousPosition,
                body.MovedLastTick)).ToArray());
        ArenaBasics.Claims claims = ArenaBasics.Claims.ForTick(
            contract, mind, _stepper, _oscillation);
        foreach (GenericActorContext.ArcRelayPendingStrike strike in arc.PendingStrikes)
        {
            if (strike.Shooter.TeamId == _teamId)
                claims.Root(strike.Shooter.UnitId);
        }
        RankBodies(mind, claims);
        PublishTrafficRequests(contract, mind, movePlans, claims);

        foreach (MindBody body in mind.Bodies
                     .OrderBy(value => claims.RankOf(value.UnitId))
                     .ThenBy(value => selected.GetValueOrDefault(value.UnitId)?.Priority
                         ?? int.MaxValue)
                     .ThenBy(value => value.UnitId))
        {
            OrdersPackage.Role role = package.RolesByUnit[body.UnitId];
            if (TryInvest(contract, mind, body, role, carried))
                continue;
            string prefix = selected.TryGetValue(body.UnitId, out Proposal? proposal)
                ? $"orders:{proposal.Leaf.Node.Path}"
                : $"orders:{role.Id}";
            string movementPrefix = $"{prefix}"
                + $":urgency-{claims.PriorityTrace(body.UnitId)}";
            if (ArenaBasics.TryMakeWay(
                    contract, mind, body, claims,
                    $"{movementPrefix}:make-way"))
                continue;
            if (ArenaBasics.TryEvade(contract, mind, body, claims))
                continue;
            if (proposal is null)
            {
                body.Hold($"{prefix}:no-eligible-order");
                continue;
            }
            Execute(
                contract, mind, arc, package, body, proposal,
                movePlans.GetValueOrDefault(body.UnitId), claims, carried);
        }
    }

    public void EndMatch(MindEnd end) => _ = end;

    private List<Proposal> BuildProposals(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        OrdersPackage package,
        MindBody body,
        OrdersPackage.Role role,
        IReadOnlyList<OrderTreeRuntime.EligibleLeaf> leaves,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        var result = new List<Proposal>();
        for (int priority = 0; priority < leaves.Count; priority++)
        {
            OrderTreeRuntime.EligibleLeaf leaf = leaves[priority];
            ResolvedTarget[] targets = ResolveTargets(
                contract, mind, arc, package, body, role, leaf.Node, carried);
            foreach (ResolvedTarget target in targets)
            {
                string? scope = leaf.Node.Capacity?.Id;
                int limit = leaf.Node.Capacity is null
                    ? 8
                    : leaf.Node.Capacity.InlineValue
                        ?? package.Capacities[leaf.Node.Capacity.Id];
                int distance = ArenaBasics.StaticDistance(
                        contract.Map, body.Position, target.Position)
                    ?? int.MaxValue;
                result.Add(new Proposal(
                    body, role, leaf, priority, target, scope, limit, distance));
            }
        }
        return result;
    }

    private ResolvedTarget[] ResolveTargets(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        OrdersPackage package,
        MindBody body,
        OrdersPackage.Role role,
        OrdersPackage.OrderNode node,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        switch (node.Action)
        {
            case OrdersPackage.MoveAction move:
                return ResolveMoveTargets(
                    contract, mind, arc, package, body, move, carried);
            case OrdersPackage.FightAction fight:
                return ResolveEnemyTargets(contract, mind, arc, body, fight);
            case OrdersPackage.UseAction use:
                return ResolveUseTargets(contract, mind, body, use, carried);
            case OrdersPackage.HandoffAction handoff:
                return ResolveHandoffTargets(
                    contract, mind, body, handoff, package, carried);
            case OrdersPackage.DropAction:
                if (!carried.TryGetValue(body.ActorId, out var core)
                    || body.Action("drop-core") is not { Available: true })
                    return [];
                return [new ResolvedTarget(
                    $"core:{CoreKey(core.CoreId)}", body.Position,
                    [body.Position], null, CoreKey(core.CoreId))];
            default:
                return [];
        }
    }

    private ResolvedTarget[] ResolveMoveTargets(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        OrdersPackage package,
        MindBody body,
        OrdersPackage.MoveAction move,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        if (move.Kind == "patrol")
        {
            Position[] points = package.RoutePoints(move.Target);
            Position nearest = points
                .OrderBy(point => ArenaBasics.StaticDistance(
                        contract.Map, body.Position, point) ?? int.MaxValue)
                .ThenBy(point => point.Y)
                .ThenBy(point => point.X)
                .First();
            return [new ResolvedTarget(
                $"route:{move.Target}", nearest, points, null, null)];
        }
        if (move.Kind == "follow")
        {
            return mind.Bodies
                .Where(candidate => candidate.UnitId != body.UnitId
                    && package.RolesByUnit[candidate.UnitId].Id == move.Target)
                .OrderBy(candidate => ArenaBasics.StaticDistance(
                        contract.Map, body.Position, candidate.Position)
                    ?? int.MaxValue)
                .ThenBy(candidate => candidate.UnitId)
                .Select(candidate => new ResolvedTarget(
                    $"body:{candidate.ActorId}",
                    candidate.Position,
                    [candidate.Position],
                    candidate.ActorId,
                    null))
                .ToArray();
        }
        if (move.TargetKind == "dynamic")
        {
            if (move.Target == "own-reactor")
                return [new ResolvedTarget(
                    "reactor:own", _ownReactor, [_ownReactor], null, null)];
            if (move.Target == "nearest-heal-zone")
                return _healTiles
                    .OrderBy(tile => ArenaBasics.StaticDistance(
                            contract.Map, body.Position, tile) ?? int.MaxValue)
                    .ThenBy(tile => tile.Y)
                    .ThenBy(tile => tile.X)
                    .Select(tile => new ResolvedTarget(
                        $"heal:{tile.X}:{tile.Y}", tile, [tile], null, null))
                    .ToArray();
            if (move.Target == "nearest-known-loose-core")
            {
                if (carried.ContainsKey(body.ActorId))
                    return [];
                return _knownLooseCores.Values
                    .OrderBy(core => ArenaBasics.StaticDistance(
                            contract.Map, body.Position, core.Position)
                        ?? int.MaxValue)
                    .ThenBy(core => core.Key, StringComparer.Ordinal)
                    .Select(core => new ResolvedTarget(
                        $"core:{core.Key}", core.Position, [core.Position],
                        null, core.Key))
                    .ToArray();
            }
        }
        if (move.TargetKind == "zone")
        {
            Position[] goals = PostureGoals(
                package.ZoneTiles(move.Target, contract.Map), move.Posture);
            Position nearest = Nearest(contract.Map, body.Position, goals);
            return [new ResolvedTarget(
                $"zone:{move.Target}", nearest, goals, null, null)];
        }
        if (move.TargetKind == "anchor")
        {
            Position target = package.AnchorPosition(move.Target);
            return [new ResolvedTarget(
                $"anchor:{move.Target}", target, [target], null, null)];
        }
        if (move.TargetKind == "well")
        {
            GenericActorContext.ArcRelayWellState well = ResolveWell(
                contract, arc, move.Target);
            return [new ResolvedTarget(
                $"well:{well.WellId}", well.Position, [well.Position], null, null)];
        }
        return [];
    }

    private ResolvedTarget[] ResolveEnemyTargets(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        OrdersPackage.FightAction fight)
    {
        HashSet<ActorIdentity> carriers = arc.VisibleCores
            .Where(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Carried
                && core.CarrierActorId?.TeamId != _teamId)
            .Select(core => core.CarrierActorId!)
            .ToHashSet();
        HashSet<ActorIdentity> marked = arc.VisibleSignatures
            .Where(signature => signature.SignatureId == "target-paint"
                && signature.TargetActorId is not null)
            .Select(signature => signature.TargetActorId!)
            .ToHashSet();
        var ordered = new List<GenericActorContext.ObservedEnemyState>();
        foreach (string selector in fight.Targets)
        {
            IEnumerable<GenericActorContext.ObservedEnemyState> candidates =
                selector switch
                {
                    "carrier" => mind.Enemies.Where(enemy =>
                        carriers.Contains(enemy.ActorId)),
                    "weakest" => mind.Enemies.OrderBy(enemy => enemy.Health)
                        .ThenBy(enemy => enemy.ActorId),
                    "nearest" => mind.Enemies.OrderBy(enemy =>
                            ArenaBasics.StaticDistance(
                                contract.Map, body.Position, enemy.Position)
                            ?? int.MaxValue)
                        .ThenBy(enemy => enemy.ActorId),
                    "medic" => mind.Enemies.Where(enemy =>
                            enemy.ClassId == "patchbay")
                        .OrderBy(enemy => enemy.ActorId),
                    "marked" => mind.Enemies.Where(enemy =>
                            marked.Contains(enemy.ActorId))
                        .OrderBy(enemy => enemy.ActorId),
                    _ => [],
                };
            foreach (var candidate in candidates)
            {
                if (ordered.All(existing => existing.ActorId != candidate.ActorId))
                    ordered.Add(candidate);
            }
        }
        return ordered.Select(enemy => new ResolvedTarget(
            $"enemy:{enemy.ActorId}", enemy.Position, [enemy.Position],
            enemy.ActorId, null)).ToArray();
    }

    private ResolvedTarget[] ResolveUseTargets(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        OrdersPackage.UseAction use,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        GenericActorRulesContract.ArcRelaySignature? signature =
            ArenaBasics.ArcRules(contract)?.Signatures.FirstOrDefault(value =>
                value.SignatureId == use.Skill || value.Kind == use.Skill);
        if (signature is null
            || body.Action(signature.ActionId) is not { Available: true })
            return [];
        // A mixed-class role may state one skill order for each class it casts.
        // Applicability belongs to the body: an unrelated class falls through
        // to its next authored order instead of forcing an artificial role split.
        if (signature.ClassId != body.ClassId)
            return [];
        if (use.Target == "self")
        {
            var self = new ResolvedTarget(
                $"body:{body.ActorId}", body.Position, [body.Position],
                body.ActorId, null);
            return CanExecuteUse(contract, body, self, use) ? [self] : [];
        }
        if (use.Target == "own-reactor")
        {
            var reactor = new ResolvedTarget(
                "reactor:own", _ownReactor, [_ownReactor], null, null);
            return CanExecuteUse(contract, body, reactor, use) ? [reactor] : [];
        }
        IEnumerable<(ActorIdentity Actor, Position Position, int Health)> values =
            use.Target switch
            {
                "nearest-ally" => mind.Bodies
                    .Where(candidate => candidate.UnitId != body.UnitId)
                    .OrderBy(candidate => ArenaBasics.StaticDistance(
                            contract.Map, body.Position, candidate.Position)
                        ?? int.MaxValue)
                    .ThenBy(candidate => candidate.UnitId)
                    .Select(candidate => (candidate.ActorId,
                        candidate.Position, candidate.Health)),
                "weakest-ally" => mind.Bodies
                    .Where(candidate => candidate.UnitId != body.UnitId)
                    .OrderBy(candidate => candidate.Health)
                    .ThenBy(candidate => candidate.UnitId)
                    .Select(candidate => (candidate.ActorId,
                        candidate.Position, candidate.Health)),
                "healthiest-ally" => mind.Bodies
                    .Where(candidate => candidate.UnitId != body.UnitId)
                    .OrderByDescending(candidate => candidate.Health)
                    .ThenBy(candidate => candidate.UnitId)
                    .Select(candidate => (candidate.ActorId,
                        candidate.Position, candidate.Health)),
                "nearest-ally-toward-own-reactor" => mind.Bodies
                    .Where(candidate => candidate.UnitId != body.UnitId
                        && !carried.ContainsKey(candidate.ActorId)
                        && (ArenaBasics.StaticDistance(
                                contract.Map, candidate.Position, _ownReactor)
                            ?? int.MaxValue) < (ArenaBasics.StaticDistance(
                                contract.Map, body.Position, _ownReactor)
                            ?? int.MaxValue))
                    .OrderBy(candidate => ArenaBasics.StaticDistance(
                            contract.Map, body.Position, candidate.Position)
                        ?? int.MaxValue)
                    .ThenBy(candidate => candidate.UnitId)
                    .Select(candidate => (candidate.ActorId,
                        candidate.Position, candidate.Health)),
                "nearest-enemy" => mind.Enemies
                    .OrderBy(candidate => ArenaBasics.StaticDistance(
                            contract.Map, body.Position, candidate.Position)
                        ?? int.MaxValue)
                    .ThenBy(candidate => candidate.ActorId)
                    .Select(candidate => (candidate.ActorId,
                        candidate.Position, candidate.Health)),
                "weakest-enemy" => mind.Enemies
                    .OrderBy(candidate => candidate.Health)
                    .ThenBy(candidate => candidate.ActorId)
                    .Select(candidate => (candidate.ActorId,
                        candidate.Position, candidate.Health)),
                "enemy-carrier" => mind.Enemies.Where(candidate =>
                        _rememberedEnemies.GetValueOrDefault(candidate.ActorId)
                            ?.Carrier == true)
                    .OrderBy(candidate => candidate.ActorId)
                    .Select(candidate => (candidate.ActorId,
                        candidate.Position, candidate.Health)),
                _ => [],
            };
        return values.Select(value => new ResolvedTarget(
                $"body:{value.Actor}", value.Position, [value.Position],
                value.Actor, null))
            .Where(target => CanExecuteUse(contract, body, target, use))
            .ToArray();
    }

    private static bool CanExecuteUse(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        ResolvedTarget target,
        OrdersPackage.UseAction use)
    {
        GenericActorRulesContract.ArcRelaySignature? signature =
            ArenaBasics.ArcRules(contract)?.Signatures.FirstOrDefault(value =>
                value.SignatureId == use.Skill || value.Kind == use.Skill);
        if (signature is null || signature.ClassId != body.ClassId)
            return false;
        GenericActorActionLegality? action = body.Action(signature.ActionId);
        if (action is not { Available: true })
            return false;
        bool unit = action.Constraints.OfType<GenericActorActionLegality
            .ArgumentConstraint.UnitTargetConstraint>().Any();
        bool heading = action.Constraints.OfType<GenericActorActionLegality
            .ArgumentConstraint.ProjectileHeadingConstraint>().Any();
        bool direction = action.Constraints.OfType<GenericActorActionLegality
            .ArgumentConstraint.DirectionConstraint>().Any();
        bool position = action.Constraints.OfType<GenericActorActionLegality
            .ArgumentConstraint.PositionTargetConstraint>().Any();
        if (unit && heading && target.Actor is ActorIdentity locked)
            return ArenaBasics.CanUseLockedLineSignature(
                contract, body, signature.Kind, locked, target.Position);
        if (unit && target.Actor is ActorIdentity actor)
            return ArenaBasics.CanUseUnitSignature(
                contract, body, signature.Kind, actor);
        if (heading)
            return ArenaBasics.CanUseHeadingSignature(
                contract, body, signature.Kind, target.Position);
        if (direction)
            return ArenaBasics.CanUseDirectionSignature(
                contract, body, signature.Kind);
        if (position)
        {
            // Arc Toss is a pass, not a blind throw in an ally's general
            // direction. A named receiver must be an exact legal landing tile
            // so the engine can complete the catch authoritatively.
            if (signature.Kind == "arc-toss"
                && target.Actor is not null
                && action.Constraints.OfType<GenericActorActionLegality
                    .ArgumentConstraint.PositionTargetConstraint>()
                    .Single().AllowedValues.Contains(target.Position) == false)
            {
                return false;
            }
            return ArenaBasics.CanUsePositionSignature(
                contract, body, signature.Kind);
        }
        return ArenaBasics.CanUseParameterlessSignature(
            contract, body, signature.Kind);
    }

    private ResolvedTarget[] ResolveHandoffTargets(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        OrdersPackage.HandoffAction handoff,
        OrdersPackage package,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        if (!carried.ContainsKey(body.ActorId)
            || body.Action("handoff-core") is not { Available: true })
            return [];
        IEnumerable<MindBody> candidates = mind.Bodies.Where(candidate =>
            candidate.UnitId != body.UnitId
            && !carried.ContainsKey(candidate.ActorId)
            && candidate.Position.ChebyshevDistance(body.Position) <= 1);
        if (handoff.TargetKind == "role")
            candidates = candidates.Where(candidate =>
                package.RolesByUnit[candidate.UnitId].Id == handoff.Target);
        if (handoff.Target == "nearest-ally-toward-own-reactor")
        {
            int sourceDistance = ArenaBasics.StaticDistance(
                    contract.Map, body.Position, _ownReactor) ?? int.MaxValue;
            candidates = candidates.Where(candidate =>
                (ArenaBasics.StaticDistance(
                    contract.Map, candidate.Position, _ownReactor)
                    ?? int.MaxValue) < sourceDistance);
        }
        return candidates
            .OrderBy(candidate => ArenaBasics.StaticDistance(
                    contract.Map, candidate.Position, _ownReactor)
                ?? int.MaxValue)
            .ThenBy(candidate => candidate.UnitId)
            .Select(candidate => new ResolvedTarget(
                $"body:{candidate.ActorId}", candidate.Position,
                [candidate.Position], candidate.ActorId, null))
            .ToArray();
    }

    private static Dictionary<int, Proposal> Allocate(
        OrdersPackage package,
        IReadOnlyDictionary<int, ProposalCursor> cursors)
    {
        var selected = new Dictionary<int, Proposal>();
        var open = cursors.ToDictionary(value => value.Key, value => value.Value);
        while (open.Count > 0)
        {
            foreach (int exhausted in open
                         .Where(value => value.Value.Current is null)
                         .Select(value => value.Key).ToArray())
                open.Remove(exhausted);
            if (open.Count == 0)
                break;
            Proposal[] current = open.Values.Select(value => value.Current!)
                .ToArray();
            foreach (Proposal proposal in current.Where(value =>
                         value.CapacityScope is null))
            {
                selected[proposal.Body.UnitId] = proposal;
                open.Remove(proposal.Body.UnitId);
            }
            foreach (IGrouping<(string Scope, string Target), Proposal> group in
                     current.Where(value => value.CapacityScope is not null)
                         .GroupBy(value => (
                             value.CapacityScope!, value.Target.Key)))
            {
                int limit = group.First().CapacityLimit;
                Proposal[] winners = group
                    .OrderBy(value => value.Distance)
                    .ThenBy(value => value.Body.UnitId)
                    .Take(limit)
                    .ToArray();
                HashSet<int> winnerIds = winners.Select(value => value.Body.UnitId)
                    .ToHashSet();
                foreach (Proposal winner in winners)
                {
                    selected[winner.Body.UnitId] = winner;
                    open.Remove(winner.Body.UnitId);
                }
                foreach (Proposal loser in group.Where(value =>
                             !winnerIds.Contains(value.Body.UnitId)))
                    open[loser.Body.UnitId].Advance();
            }
        }
        return selected;
    }

    private Dictionary<int, MovePlan> PrepareMovePlans(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        OrdersPackage package,
        IReadOnlyDictionary<int, Proposal> selected)
    {
        var result = new Dictionary<int, MovePlan>();
        foreach ((int unitId, Proposal proposal) in selected)
        {
            if (proposal.Leaf.Node.Action is OrdersPackage.FightAction fight)
            {
                GenericActorContext.ObservedEnemyState? target = mind.Enemies
                    .FirstOrDefault(value => value.ActorId
                        == proposal.Target.Actor);
                if (target is null || fight.Style == "hold"
                    || ArenaBasics.CanAimAt(contract, proposal.Body, target))
                {
                    continue;
                }
                (ActorIdentity, string) key = (
                    proposal.Body.ActorId, proposal.Leaf.Node.Path);
                Position anchor = _fightAnchors.GetValueOrDefault(
                    key, proposal.Body.Position);
                bool inLeash = fight.ChaseLimit == 0
                    ? proposal.Body.Position == anchor
                    : ArenaBasics.StaticDistance(
                        contract.Map, proposal.Body.Position, anchor)
                        <= fight.ChaseLimit;
                if (inLeash && fight.Style == "advance")
                {
                    result.Add(unitId, new MovePlan(
                        proposal.Leaf.Node.Path,
                        target.Position,
                        [target.Position],
                        null,
                        Arrived: false));
                }
                continue;
            }
            if (proposal.Leaf.Node.Action is not OrdersPackage.MoveAction move)
                continue;
            MindBody body = proposal.Body;
            string routeState = proposal.Leaf.Node.Path;
            if (move.Via is string ingress
                && !_routeCursors.GetValueOrDefault(unitId)?.CompletedIngresses
                    .Contains($"{proposal.Leaf.Node.Path}:{ingress}") == true)
            {
                MovePlan ingressPlan = RoutePlan(
                    contract, mind, package, body, proposal, ingress,
                    $"{proposal.Leaf.Node.Path}:via:{ingress}", loop: false);
                if (!ingressPlan.Arrived)
                {
                    result.Add(unitId, ingressPlan);
                    continue;
                }
                RouteCursor cursor = _routeCursors.GetValueOrDefault(unitId)
                    ?? RouteCursor.For(body.ActorId, routeState);
                cursor.CompletedIngresses.Add(
                    $"{proposal.Leaf.Node.Path}:{ingress}");
                _routeCursors[unitId] = cursor;
            }
            if (move.Kind == "patrol")
            {
                result.Add(unitId, RoutePlan(
                    contract, mind, package, body, proposal, move.Target,
                    proposal.Leaf.Node.Path, loop: true));
                continue;
            }
            Position[] goals = PostureGoals(proposal.Target.Goals, move.Posture);
            Position goal = Nearest(contract.Map, body.Position, goals);
            result.Add(unitId, new MovePlan(
                proposal.Leaf.Node.Path, goal, goals, null,
                goals.Contains(body.Position)));
        }
        return result;
    }

    private MovePlan RoutePlan(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        OrdersPackage package,
        MindBody body,
        Proposal proposal,
        string routeId,
        string stateId,
        bool loop)
    {
        Position[] points = package.RoutePoints(routeId);
        RouteCursor state = _routeCursors.GetValueOrDefault(body.UnitId)
            ?? RouteCursor.For(body.ActorId, stateId);
        if (state.Actor != body.ActorId || state.StateId != stateId)
            state = RouteCursor.For(body.ActorId, stateId);
        if (!state.Initialized)
        {
            state.Index = loop
                ? Enumerable.Range(0, points.Length)
                    .OrderBy(index => ArenaBasics.StaticDistance(
                            contract.Map, body.Position, points[index])
                        ?? int.MaxValue)
                    .ThenBy(index => index)
                    .First()
                : 0;
            state.Initialized = true;
        }
        if (body.Position == points[state.Index])
        {
            if (state.Index + 1 < points.Length)
                state.Index++;
            else if (loop)
            {
                state.Index = 0;
                state.Lap++;
            }
            else
            {
                _routeCursors[body.UnitId] = state;
                return new MovePlan(
                    proposal.Leaf.Node.Path, body.Position, [body.Position],
                    null, Arrived: true);
            }
        }
        Position destination = points[state.Index];
        HashSet<Position> obstacles = ArenaBasics.PersistentRouteObstacles(
            contract, mind, body.ActorId.TeamId);
        TacticalRouteGovernor.CourseStep course = _routes.Resolve(
            body.ActorId,
            stateId,
            routeId,
            state.Lap,
            state.Index,
            body.Position,
            destination,
            () => ArenaBasics.StaticRouteCourse(
                contract, body, destination, obstacles, _mirrored),
            tile => (tile != destination && obstacles.Contains(tile))
                || !ArenaBasics.IsLegalTerrainStep(
                    contract.Map, body.Position, tile));
        _routeCursors[body.UnitId] = state;
        return new MovePlan(
            proposal.Leaf.Node.Path,
            destination,
            [destination],
            course,
            Arrived: false);
    }

    private TacticalTrafficGovernor.Sample[] BuildTrafficSamples(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        IReadOnlyDictionary<int, Proposal> selected,
        IReadOnlyDictionary<int, MovePlan> movePlans)
    {
        return mind.Bodies.OrderBy(value => value.UnitId).Select(body =>
        {
            MovePlan? plan = movePlans.GetValueOrDefault(body.UnitId);
            Position goal = plan?.Goal ?? body.Position;
            Position? first = plan?.Course?.Next
                ?? (plan is null ? null : ArenaBasics.StaticFirstStep(
                    contract, mind, body, goal));
            bool blocked = first is Position wanted
                && mind.Bodies.Any(other => other.UnitId != body.UnitId
                    && other.Position == wanted);
            return new TacticalTrafficGovernor.Sample(
                body.ActorId,
                selected.GetValueOrDefault(body.UnitId)?.Leaf.Node.Path
                    ?? "no-order",
                goal,
                ArenaBasics.StaticDistance(contract.Map, body.Position, goal),
                AttemptedMovementLastTick(contract, body),
                blocked,
                MayAge: plan is not null && !plan.Arrived,
                Urgency: selected.GetValueOrDefault(body.UnitId)?.Leaf.Node.Urgency
                    ?? TacticalMovementUrgency.Normal,
                ActivatedTick: selected.GetValueOrDefault(body.UnitId)
                    ?.Leaf.ActivatedTick ?? mind.Tick);
        }).ToArray();
    }

    private void RankBodies(
        MindContext mind,
        ArenaBasics.Claims claims)
    {
        foreach (MindBody body in mind.Bodies)
            claims.Rank(_traffic.Priority(body.UnitId));
    }

    private static void PublishTrafficRequests(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        IReadOnlyDictionary<int, MovePlan> movePlans,
        ArenaBasics.Claims claims)
    {
        foreach ((int unitId, MovePlan plan) in movePlans)
        {
            MindBody body = mind.Bodies.Single(value => value.UnitId == unitId);
            if (claims.IsRooted(unitId))
                continue;
            Position? first = plan.Course?.Next
                ?? ArenaBasics.StaticFirstStep(contract, mind, body, plan.Goal);
            if (first is not Position wanted)
                continue;
            claims.Demand(wanted, unitId);
            claims.ReserveLane(wanted, unitId);
            ArenaBasics.DemandClearanceChain(
                contract, mind, body, wanted, claims);
        }
    }

    private void Execute(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        OrdersPackage package,
        MindBody body,
        Proposal proposal,
        MovePlan? movePlan,
        ArenaBasics.Claims claims,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        string reason = $"orders:{proposal.Leaf.Node.Path}";
        string urgency = $"urgency-{claims.PriorityTrace(body.UnitId)}";
        switch (proposal.Leaf.Node.Action)
        {
            case OrdersPackage.MoveAction:
                ExecuteMove(
                    contract, mind, body, proposal, movePlan!, claims,
                    reason, urgency,
                    carried.ContainsKey(body.ActorId));
                return;
            case OrdersPackage.FightAction fight:
                ExecuteFight(contract, mind, body, proposal, fight, claims, reason);
                return;
            case OrdersPackage.UseAction use:
                if (ExecuteUse(contract, body, proposal.Target, use, reason))
                    RememberOneShot(body, proposal, use.Skill);
                else
                    body.Hold($"{reason}:use-unavailable");
                return;
            case OrdersPackage.HandoffAction:
                if (proposal.Target.Actor is ActorIdentity receiver
                    && body.Action("handoff-core") is { Available: true } action)
                {
                    body.Command(
                        action.ActionId,
                        action.ActionCode,
                        [new GenericActorActionArgument.UnitTargetArgument(
                            new GenericActorActionArgument.UnitTarget(
                                receiver.TeamId, receiver.UnitId))],
                        $"{reason}:handoff:{receiver}");
                    RememberOneShot(body, proposal, action.ActionId);
                }
                else
                    body.Hold($"{reason}:handoff-no-target");
                return;
            case OrdersPackage.DropAction:
                if (body.Action("drop-core") is { Available: true } drop)
                {
                    body.Command(drop.ActionId, drop.ActionCode, [],
                        $"{reason}:drop");
                    RememberOneShot(body, proposal, drop.ActionId);
                }
                else
                    body.Hold($"{reason}:drop-unavailable");
                return;
            default:
                body.Hold($"{reason}:unknown-action");
                return;
        }
    }

    private void ExecuteMove(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Proposal proposal,
        MovePlan plan,
        ArenaBasics.Claims claims,
        string reason,
        string urgency,
        bool carrying)
    {
        string movement = $"{reason}:march@{plan.Goal.X},{plan.Goal.Y}"
            + $":{urgency}";
        if (plan.Arrived || plan.Goals.Contains(body.Position))
        {
            body.Hold($"{movement}:at-target:{proposal.Target.Key}");
            return;
        }
        if (plan.Course?.Next is Position step)
        {
            bool occupied = mind.Bodies.Any(candidate =>
                    candidate.UnitId != body.UnitId
                    && candidate.Position == step)
                || claims.Committed.Contains(step);
            if (occupied && _stationaryTicks.GetValueOrDefault(body.UnitId) >= 2)
            {
                HashSet<Position> blocked = ArenaBasics.BlockedNow(
                    contract, mind, body, claims, avoidHostileProjectiles: false);
                blocked.Remove(body.Position);
                TacticalRouteGovernor.CourseStep detour = _routes.TryTrafficDetour(
                    body.ActorId,
                    body.Position,
                    maximumExtraSteps: 2,
                    rejoin => blocked.Contains(rejoin)
                        ? null
                        : ArenaBasics.StaticRouteCourse(
                            contract, body, rejoin, blocked, _mirrored));
                if (detour.Next is Position alternate
                    && detour.Trace.StartsWith(
                        "route-detour-commit-", StringComparison.Ordinal)
                    && ArenaBasics.TryMoveDirect(
                        contract, mind, body, alternate, claims,
                        $"{movement}:{detour.Trace}"))
                    return;
            }
            if (ArenaBasics.TryMoveDirect(
                    contract, mind, body, step, claims,
                    $"{movement}:{plan.Course.Trace}",
                    carrying
                        ? TacticalMovementAuthority.Safety
                        : TacticalMovementAuthority.Tactical))
                return;
            body.Hold($"{movement}:{plan.Course.Trace}:blocked");
            return;
        }
        bool moved = carrying && proposal.Target.Key == "reactor:own"
            ? ArenaBasics.TryMoveRouted(
                contract, mind, body, plan.Goal, claims,
                $"{movement}:carry-home")
            : ArenaBasics.TryMoveToward(
                contract, mind, body, plan.Goals, claims,
                $"{movement}:move:{proposal.Target.Key}");
        if (!moved)
            body.Hold($"{movement}:move-blocked:{proposal.Target.Key}");
    }

    private void ExecuteFight(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Proposal proposal,
        OrdersPackage.FightAction fight,
        ArenaBasics.Claims claims,
        string reason)
    {
        GenericActorContext.ObservedEnemyState? target = mind.Enemies
            .FirstOrDefault(value => value.ActorId == proposal.Target.Actor);
        if (target is null)
        {
            body.Hold($"{reason}:target-lost");
            return;
        }
        (ActorIdentity, string) key = (body.ActorId, proposal.Leaf.Node.Path);
        Position anchor = _fightAnchors.GetValueOrDefault(key, body.Position);
        _fightAnchors[key] = anchor;
        bool inLeash = fight.ChaseLimit == 0
            ? body.Position == anchor
            : ArenaBasics.StaticDistance(
                contract.Map, body.Position, anchor) <= fight.ChaseLimit;

        if (fight.Style == "kite"
            && target.Position.ChebyshevDistance(body.Position) <= 2
            && ArenaBasics.TryStepAway(
                contract, mind, body, [target.Position], claims,
                $"{reason}:kite:{target.ActorId}"))
            return;
        if (ArenaBasics.TryShootAtPosition(
                contract,
                mind,
                body,
                target.Position,
                $"{reason}:fight:{target.ActorId}",
                target.ActorId))
            return;
        if (ArenaBasics.CanAimAt(contract, body, target))
        {
            body.Hold($"{reason}:cadence:{target.ActorId}");
            return;
        }
        if (fight.Style != "hold" && inLeash
            && ArenaBasics.TryMoveToward(
                contract, mind, body, [target.Position], claims,
                $"{reason}:close:{target.ActorId}"))
            return;
        body.Hold($"{reason}:hold:{target.ActorId}"
            + (inLeash ? "" : ":chase-limit"));
    }

    private static bool ExecuteUse(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        ResolvedTarget target,
        OrdersPackage.UseAction use,
        string reason)
    {
        GenericActorRulesContract.ArcRelaySignature? signature =
            ArenaBasics.ArcRules(contract)?.Signatures.FirstOrDefault(value =>
                value.SignatureId == use.Skill || value.Kind == use.Skill);
        if (signature is null)
            return false;
        GenericActorActionLegality? action = body.Action(signature.ActionId);
        if (action is not { Available: true })
            return false;
        bool unit = action.Constraints.OfType<GenericActorActionLegality
            .ArgumentConstraint.UnitTargetConstraint>().Any();
        bool heading = action.Constraints.OfType<GenericActorActionLegality
            .ArgumentConstraint.ProjectileHeadingConstraint>().Any();
        bool direction = action.Constraints.OfType<GenericActorActionLegality
            .ArgumentConstraint.DirectionConstraint>().Any();
        bool position = action.Constraints.OfType<GenericActorActionLegality
            .ArgumentConstraint.PositionTargetConstraint>().Any();
        string trace = $"{reason}:use:{use.Skill}:{target.Key}";
        if (unit && heading && target.Actor is ActorIdentity locked)
            return ArenaBasics.TryLockedLineSignature(
                contract, body, signature.Kind, locked, target.Position, trace);
        if (unit && target.Actor is ActorIdentity actor)
            return ArenaBasics.TryUnitSignature(
                contract, body, signature.Kind, actor, trace);
        if (heading)
            return ArenaBasics.TryHeadingSignature(
                contract, body, signature.Kind, target.Position, trace);
        if (direction)
            return ArenaBasics.TryDirectionSignature(
                contract, body, signature.Kind, target.Position, trace);
        if (position)
        {
            Func<Position, bool>? exactReceiver = signature.Kind == "arc-toss"
                && target.Actor is not null
                ? candidate => candidate == target.Position
                : null;
            return ArenaBasics.TryPositionSignature(
                contract, body, signature.Kind, target.Position, trace,
                exactReceiver);
        }
        return ArenaBasics.TryParameterlessSignature(
            contract, body, signature.Kind, trace);
    }

    private bool Evaluate(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        OrdersPackage package,
        MindBody body,
        OrdersPackage.Role role,
        OrdersPackage.ConditionExpression expression,
        int orderTicks,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        if (expression.Names.Length == 1 && expression.Names[0] == "always")
            return true;
        IEnumerable<bool> values = expression.Names.Select(name =>
        {
            if (name == "always")
                return true;
            OrdersPackage.Condition condition = package.Conditions[name];
            int actual = Fact(
                contract, mind, arc, package, body, role, condition,
                orderTicks, carried);
            return Compare(actual, condition.Operator, condition.Value);
        });
        return expression.Kind == "any" ? values.Any(value => value)
            : values.All(value => value);
    }

    private int Fact(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        OrdersPackage package,
        MindBody body,
        OrdersPackage.Role role,
        OrdersPackage.Condition condition,
        int orderTicks,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        string fact = condition.Fact;
        if (fact == "self-health-percent")
            return body.Health * 100 / Math.Max(1, MaxHealth(contract, body));
        if (fact == "self-level")
            return CurrentLevel(body);
        if (fact == "self-carrying")
            return carried.ContainsKey(body.ActorId) ? 1 : 0;
        if (fact == "self-stuck-ticks")
            return _stationaryTicks.GetValueOrDefault(body.UnitId);
        if (fact == "self-order-ticks")
            return orderTicks;
        if (fact == "self-in-zone")
            return package.Contains(condition.Zone!, body.Position) ? 1 : 0;
        if (fact == "signature-ready")
        {
            GenericActorRulesContract.ArcRelaySignature? signature =
                ArenaBasics.ArcRules(contract)?.Signatures.FirstOrDefault(value =>
                    value.ClassId == body.ClassId);
            return signature is not null
                && body.Action(signature.ActionId) is { Available: true } ? 1 : 0;
        }
        if (fact.StartsWith("role-", StringComparison.Ordinal))
        {
            MindBody[] members = mind.Bodies.Where(candidate =>
                    package.RolesByUnit[candidate.UnitId].Id == condition.Role)
                .ToArray();
            if (fact == "role-live-count")
                return members.Length;
            if (fact == "role-lowest-health-percent")
                return members.Length == 0 ? 100 : members.Min(candidate =>
                    candidate.Health * 100
                    / Math.Max(1, MaxHealth(contract, candidate)));
            return members.Count(candidate =>
                package.Contains(condition.Zone!, candidate.Position));
        }
        if (fact == "live-friendlies")
            return mind.Bodies.Length;
        if (fact == "friendly-carriers")
            return carried.Keys.Count(actor => actor.TeamId == _teamId);
        if (fact == "visible-enemy-carriers")
            return arc.VisibleCores.Count(core =>
                core.CarrierActorId?.TeamId != _teamId
                && core.Disposition
                    == GenericActorContext.ArcRelayCoreDisposition.Carried);
        if (fact == "known-enemy-carriers")
            return _rememberedEnemies.Values.Count(enemy => enemy.Carrier
                && mind.Tick - enemy.Tick <= condition.FreshnessTicks);
        if (fact == "known-loose-cores")
            return _knownLooseCores.Count;
        if (fact == "visible-loose-cores")
            return arc.VisibleCores.Count(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Loose);
        if (fact == "secured-cores")
            return carried.Keys.Count(actor => actor.TeamId == _teamId);
        GenericActorContext.ArcRelayReactorState own = arc.Reactors.Single(value =>
            value.TeamId == _teamId);
        if (fact == "reactor-integrity")
            return own.IntegritySegments;
        if (fact == "reactor-charge")
            return own.ChargePips;
        if (fact == "ticks-without-objective-progress")
            return mind.Tick - _lastObjectiveProgressTick;
        if (fact == "friendlies-in-zone")
            return mind.Bodies.Count(candidate =>
                package.Contains(condition.Zone!, candidate.Position));
        if (fact == "visible-enemies-in-zone")
            return mind.Enemies.Count(enemy =>
                package.Contains(condition.Zone!, enemy.Position));
        if (fact == "remembered-enemies-in-zone")
            return _rememberedEnemies.Values.Count(enemy =>
                mind.Tick - enemy.Tick <= condition.FreshnessTicks
                && package.Contains(condition.Zone!, enemy.Position));
        if (fact == "known-loose-cores-in-zone")
            return _knownLooseCores.Values.Count(core =>
                package.Contains(condition.Zone!, core.Position));
        GenericActorContext.ArcRelayWellState well = ResolveWell(
            contract, arc, condition.Well!);
        if (fact == "well-has-outstanding-core")
            return well.OutstandingCoreId is null ? 0 : 1;
        if (fact == "well-ticks-until-birth")
            return well.NextScheduledBirthTick is int birth
                ? Math.Max(0, birth - mind.Tick)
                : int.MaxValue;
        return own.FilledSocketWellIds.Contains(well.WellId,
            StringComparer.Ordinal) ? 1 : 0;
    }

    private static bool Compare(int actual, string op, int expected) => op switch
    {
        "at-least" => actual >= expected,
        "at-most" => actual <= expected,
        "equals" => actual == expected,
        "less-than" => actual < expected,
        "greater-than" => actual > expected,
        _ => false,
    };

    private void UpdateMemory(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc)
    {
        HashSet<ActorIdentity> visibleCarriers = arc.VisibleCores
            .Where(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Carried
                && core.CarrierActorId?.TeamId != _teamId)
            .Select(core => core.CarrierActorId!)
            .ToHashSet();
        foreach (var enemy in mind.Enemies)
            _rememberedEnemies[enemy.ActorId] = new RememberedEnemy(
                enemy.Position, mind.Tick, visibleCarriers.Contains(enemy.ActorId));

        foreach (GenericActorContext.ArcRelayCoreState core in arc.VisibleCores)
        {
            string key = CoreKey(core.CoreId);
            if (core.Disposition == GenericActorContext.ArcRelayCoreDisposition.Loose)
                _knownLooseCores[key] = new KnownCore(key, core.Position, mind.Tick);
            else
                _knownLooseCores.Remove(key);
        }
        foreach (GenericActorContext.ObservedEvent observed in mind.VisibleEvents)
        {
            if (!_processedEvents.Add(observed.EventHandle)
                || observed.Payload is not GenericActorContext.EventPayload.ArcRelay mode)
                continue;
            switch (mode.Fact)
            {
                case GenericActorContext.ArcRelayEvent.CoreBorn born:
                    Remember(born.CoreId, born.Position, observed.SourceTick);
                    break;
                case GenericActorContext.ArcRelayEvent.CoreRipened ripened:
                    Remember(ripened.CoreId, ripened.Position, observed.SourceTick);
                    break;
                case GenericActorContext.ArcRelayEvent.CoreDropped dropped:
                    Remember(dropped.CoreId, dropped.Position, observed.SourceTick);
                    break;
                case GenericActorContext.ArcRelayEvent.CoreRelocated relocated
                    when relocated.CarrierActorId is null:
                    Remember(relocated.CoreId, relocated.To, observed.SourceTick);
                    break;
                case GenericActorContext.ArcRelayEvent.CorePickedUp picked:
                    _knownLooseCores.Remove(CoreKey(picked.CoreId));
                    break;
                case GenericActorContext.ArcRelayEvent.CoreHandedOff handed:
                    _knownLooseCores.Remove(CoreKey(handed.CoreId));
                    break;
                case GenericActorContext.ArcRelayEvent.CoreBanked banked:
                    _knownLooseCores.Remove(CoreKey(banked.CoreId));
                    _lastObjectiveProgressTick = observed.SourceTick;
                    break;
                case GenericActorContext.ArcRelayEvent.LeveledUp level
                    when level.ActorId.TeamId == _teamId:
                    _levels[level.ActorId.UnitId] = (level.ActorId, level.Level);
                    _pendingInvest[level.ActorId.UnitId] =
                        _pendingInvest.GetValueOrDefault(level.ActorId.UnitId) + 1;
                    break;
            }
        }
        HashSet<Position> visible = mind.VisibleTiles.Select(tile => tile.Position)
            .ToHashSet();
        HashSet<Position> looseVisible = arc.VisibleCores
            .Where(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Loose)
            .Select(core => core.Position).ToHashSet();
        foreach (string stale in _knownLooseCores
                     .Where(value => visible.Contains(value.Value.Position)
                         && !looseVisible.Contains(value.Value.Position))
                     .Select(value => value.Key).ToArray())
            _knownLooseCores.Remove(stale);

        int charge = arc.Reactors.Single(value => value.TeamId == _teamId).ChargePips;
        if (charge != _lastOwnCharge)
        {
            _lastOwnCharge = charge;
            _lastObjectiveProgressTick = mind.Tick;
        }
    }

    private void Remember(
        GenericActorContext.ArcRelayCoreId core,
        Position position,
        int tick)
    {
        string key = CoreKey(core);
        _knownLooseCores[key] = new KnownCore(key, position, tick);
    }

    private void ResolvePreviousOneShots(MindContext mind)
    {
        foreach (MindBody body in mind.Bodies)
        {
            if (!_pendingOneShots.TryGetValue(body.UnitId, out PendingOneShot? pending)
                || pending.Actor != body.ActorId)
                continue;
            GenericActorActionResolution? resolution = body.PreviousActionResolution;
            if (resolution?.SubmittedAction?.ActionId == pending.ActionId
                && resolution.Outcome
                    == GenericActorActionResolution.ActionOutcome.Success)
            {
                _orders.Complete(body.ActorId, pending.Node, mind.Tick - 1);
            }
            _pendingOneShots.Remove(body.UnitId);
        }
    }

    private void RememberOneShot(MindBody body, Proposal proposal, string actionId) =>
        _pendingOneShots[body.UnitId] = new PendingOneShot(
            body.ActorId, proposal.Leaf.Node, actionId);

    private void UpdateMotionMemory(MindContext mind)
    {
        HashSet<int> live = mind.Bodies.Select(body => body.UnitId).ToHashSet();
        foreach (int stale in _stationaryTicks.Keys.Where(value => !live.Contains(value))
                     .ToArray())
            _stationaryTicks.Remove(stale);
        foreach (MindBody body in mind.Bodies)
        {
            _stationaryTicks[body.UnitId] = body.MovedLastTick
                ? 0
                : _stationaryTicks.GetValueOrDefault(body.UnitId) + 1;
        }
    }

    private void ReconcileSelections(
        MindContext mind,
        IReadOnlyDictionary<int, Proposal> selected)
    {
        HashSet<int> live = mind.Bodies.Select(body => body.UnitId).ToHashSet();
        foreach (int stale in _selectedPaths.Keys.Where(value => !live.Contains(value))
                     .ToArray())
        {
            _selectedPaths.Remove(stale);
            _routeCursors.Remove(stale);
            _routes.Reset(stale);
        }
        foreach (MindBody body in mind.Bodies)
        {
            string path = selected.GetValueOrDefault(body.UnitId)?.Leaf.Node.Path
                ?? "";
            if (!_selectedPaths.TryGetValue(body.UnitId, out string? prior)
                || prior != path)
            {
                _routeCursors.Remove(body.UnitId);
                _routes.Reset(body.UnitId);
                _fightAnchors.Remove((body.ActorId, prior ?? ""));
            }
            _selectedPaths[body.UnitId] = path;
        }
    }

    private bool TryInvest(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        OrdersPackage.Role role,
        IReadOnlyDictionary<ActorIdentity,
            GenericActorContext.ArcRelayCoreState> carried)
    {
        if (_pendingInvest.GetValueOrDefault(body.UnitId) <= 0
            || carried.ContainsKey(body.ActorId)
            || mind.Enemies.Any(enemy =>
                enemy.Position.ChebyshevDistance(body.Position) <= 5)
            || body.Action("invest") is not { Available: true } invest)
            return false;
        (ActorIdentity Actor, int Spent) state = _invested.GetValueOrDefault(
            body.UnitId, (body.ActorId, 0));
        if (state.Actor != body.ActorId)
            state = (body.ActorId, 0);
        string track = role.UpgradePath[Math.Min(
            state.Spent, role.UpgradePath.Length - 1)];
        body.Command(
            invest.ActionId,
            invest.ActionCode,
            [new GenericActorActionArgument.UpgradeTrackArgument(track)],
            $"orders:{role.Id}:upgrade:{track}");
        _invested[body.UnitId] = (body.ActorId, state.Spent + 1);
        _pendingInvest[body.UnitId]--;
        return true;
    }

    private int CurrentLevel(MindBody body) =>
        _levels.TryGetValue(body.UnitId, out (ActorIdentity Actor, int Level) level)
            && level.Actor == body.ActorId ? level.Level : 1;

    private Position[] PostureGoals(Position[] goals, string? posture)
        => TacticalPosturePlanner.Resolve(
            goals, posture, _ownReactor, _enemyReactor);

    private static Position Nearest(
        GenericActorMapContract map,
        Position from,
        IReadOnlyCollection<Position> targets) => targets
        .OrderBy(target => ArenaBasics.StaticDistance(map, from, target)
            ?? int.MaxValue)
        .ThenBy(target => target.Y)
        .ThenBy(target => target.X)
        .First();

    private GenericActorContext.ArcRelayWellState ResolveWell(
        GenericActorResolvedMatchContract contract,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        string name)
    {
        if (name is "north" or "centre" or "south")
            return arc.Wells.Single(well => string.Equals(
                well.WellId, name, StringComparison.Ordinal));
        IEnumerable<GenericActorContext.ArcRelayWellState> ordered = arc.Wells
            .OrderBy(well => ArenaBasics.StaticDistance(
                    contract.Map, _ownReactor, well.Position) ?? int.MaxValue)
            .ThenBy(well => well.WellId, StringComparer.Ordinal);
        return name == "own" ? ordered.First() : ordered.Last();
    }

    private static int MaxHealth(
        GenericActorResolvedMatchContract contract,
        MindBody body) => contract.Rules.Forms.Single(value =>
            value.Id == body.FormId).MaxHealth;

    private static bool AttemptedMovementLastTick(
        GenericActorResolvedMatchContract contract,
        MindBody body)
    {
        string? actionId = body.PreviousActionResolution?.AcceptedAction.ActionId;
        return actionId is not null
            && contract.Rules.Actions.Any(action =>
                action.Id == actionId
                && action.Kind == GenericActorRulesContract.ActionKind.Movement);
    }

    private static void ValidateComposition(
        GenericActorResolvedMatchContract contract,
        int teamId,
        OrdersPackage package)
    {
        string[] actual = contract.Topology.UnitSlots
            .Where(slot => slot.TeamId == teamId)
            .OrderBy(slot => slot.UnitId)
            .Select(slot => slot.ClassId ?? "")
            .ToArray();
        string[] expected = package.Roles
            .SelectMany(role => role.Classes).ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidDataException(
                "Arc Relay sheet composition does not match the resolved team.");
    }

    private static string CoreKey(GenericActorContext.ArcRelayCoreId id) =>
        $"{id.SourceWellId}:{id.SourceOrdinal}";

    private sealed class ProposalCursor
    {
        private readonly List<Proposal> _values;
        private int _index;

        public ProposalCursor(List<Proposal> values) => _values = values;
        public Proposal? Current => _index < _values.Count ? _values[_index] : null;
        public void Advance() => _index++;
    }

    private sealed record Proposal(
        MindBody Body,
        OrdersPackage.Role Role,
        OrderTreeRuntime.EligibleLeaf Leaf,
        int Priority,
        ResolvedTarget Target,
        string? CapacityScope,
        int CapacityLimit,
        int Distance);

    private sealed record ResolvedTarget(
        string Key,
        Position Position,
        Position[] Goals,
        ActorIdentity? Actor,
        string? CoreKey);

    private sealed record MovePlan(
        string Path,
        Position Goal,
        Position[] Goals,
        TacticalRouteGovernor.CourseStep? Course,
        bool Arrived);

    private sealed class RouteCursor
    {
        private RouteCursor(ActorIdentity actor, string stateId)
        {
            Actor = actor;
            StateId = stateId;
        }

        public ActorIdentity Actor { get; }
        public string StateId { get; }
        public int Index { get; set; }
        public int Lap { get; set; }
        public bool Initialized { get; set; }
        public HashSet<string> CompletedIngresses { get; } =
            new(StringComparer.Ordinal);

        public static RouteCursor For(ActorIdentity actor, string stateId) =>
            new(actor, stateId);
    }

    private sealed record PendingOneShot(
        ActorIdentity Actor,
        OrdersPackage.OrderNode Node,
        string ActionId);
    private sealed record KnownCore(string Key, Position Position, int SeenTick);
    private sealed record RememberedEnemy(Position Position, int Tick, bool Carrier);
}
