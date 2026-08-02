using BotArena.Sdk;

/// <summary>
/// Deterministic evaluation interpreter for the sheet's ordered gambits and
/// freeform spatial intents. It only consumes the mind's causal public view.
/// </summary>
internal sealed class StrategyDirector
{
    private static readonly ProjectileHeading[] Headings =
        Enum.GetValues<ProjectileHeading>();

    private readonly StrategySheet _sheet;
    private readonly GenericActorResolvedMatchContract _contract;
    private readonly int _teamId;
    private readonly bool _mirror;
    private readonly Position _ownReactor;
    private readonly Dictionary<string, bool> _entryWasActive = [];
    private readonly Dictionary<string, int> _cooldownUntil = [];
    private readonly Dictionary<(int UnitId, string IntentId), int>
        _pathIndexes = [];
    private readonly Dictionary<int, ActorIdentity> _lifeByUnit = [];
    private readonly Dictionary<(ActorIdentity Actor, string Zone), int>
        _zoneEnteredTick = [];
    private readonly IntelligentOperationMachine _operations;
    private readonly Dictionary<int, EnemyMemory> _enemyMemory = [];
    private readonly Dictionary<string, CausalCarrierTarget> _operationTargets = [];

    private string? _activeId;
    private int _activeStartedTick;
    private int _transitionBlockedUntil;
    private int? _handledPulseTick;

    internal StrategyDirector(
        StrategySheet sheet,
        GenericActorResolvedMatchContract contract,
        int teamId,
        bool mirror,
        Position ownReactor)
    {
        _sheet = sheet;
        _contract = contract;
        _teamId = teamId;
        _mirror = mirror;
        _ownReactor = ownReactor;
        _operations = new IntelligentOperationMachine(sheet.Operations);
        foreach (GambitPlan gambit in sheet.Gambits)
        {
            _entryWasActive[gambit.Id] = false;
            _cooldownUntil[gambit.Id] = 0;
        }
    }

    internal GambitPlan? Active => _activeId is null
        ? null
        : _sheet.Gambits.Single(value => string.Equals(
            value.Id, _activeId, StringComparison.Ordinal));

    internal string OperationTrace
    {
        get
        {
            string targets = _operationTargets.Count == 0
                ? ""
                : ";targets=" + string.Join(",", _operationTargets
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => $"{value.Key}:"
                        + $"{value.Value.CoreId.SourceWellId}-"
                        + $"{value.Value.CoreId.SourceOrdinal}@"
                        + value.Value.LastSeenTick));
            return _operations.TraceSummary + targets;
        }
    }

    internal void Update(
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc)
    {
        foreach (MindBody body in mind.Bodies)
        {
            if (!_lifeByUnit.TryGetValue(body.UnitId, out ActorIdentity? prior)
                || prior != body.ActorId)
            {
                _lifeByUnit[body.UnitId] = body.ActorId;
                foreach ((int unitId, string intentId) in _pathIndexes.Keys
                             .Where(key => key.UnitId == body.UnitId).ToArray())
                {
                    _pathIndexes.Remove((unitId, intentId));
                }
            }
        }
        UpdateZoneTenure(mind);
        UpdateEnemyMemory(mind);
        UpdateOperationTargets(mind, arc);

        bool newPulse = arc.LatestPulseTick is int pulseTick
            && pulseTick != _handledPulseTick;
        bool ownPulse = newPulse && arc.LatestPulseTeamId == _teamId;
        bool enemyPulse = newPulse && arc.LatestPulseTeamId != _teamId;
        Dictionary<string, bool> entry = _sheet.Gambits.ToDictionary(
            gambit => gambit.Id,
            gambit => gambit.EnterAll.All(clause => Evaluate(
                clause, mind, arc, ownPulse, enemyPulse)),
            StringComparer.Ordinal);

        GambitPlan? active = Active;
        if (active is not null)
        {
            int elapsed = mind.Tick - _activeStartedTick;
            bool minimumMet = elapsed >= active.MinimumTicks;
            bool exit = minimumMet && (
                elapsed >= active.MaximumTicks
                || active.ExitAny.Any(clause => Evaluate(
                    clause, mind, arc, ownPulse, enemyPulse)));
            GambitPlan? preemptor = minimumMet && !exit
                ? _sheet.Gambits
                    .Where(candidate => candidate.Priority < active.Priority)
                    .Where(candidate => mind.Tick >= _cooldownUntil[candidate.Id])
                    .Where(candidate => entry[candidate.Id])
                    .FirstOrDefault()
                : null;
            if (exit)
            {
                Deactivate(active, mind.Tick);
            }
            else if (preemptor is not null)
            {
                Deactivate(active, mind.Tick);
                Activate(preemptor, mind.Tick);
            }
        }

        if (_activeId is null && mind.Tick >= _transitionBlockedUntil)
        {
            foreach (GambitPlan gambit in _sheet.Gambits)
            {
                bool condition = entry[gambit.Id];
                bool eligible = gambit.Activation switch
                {
                    "while-true" => condition,
                    "rising-edge" => condition && !_entryWasActive[gambit.Id],
                    _ => throw new InvalidOperationException(
                        $"Unknown activation '{gambit.Activation}'."),
                };
                if (!eligible || mind.Tick < _cooldownUntil[gambit.Id])
                    continue;
                Activate(gambit, mind.Tick);
                break;
            }
        }

        foreach (GambitPlan gambit in _sheet.Gambits)
            _entryWasActive[gambit.Id] = entry[gambit.Id];
        if (newPulse)
            _handledPulseTick = arc.LatestPulseTick;

        HashSet<ActorIdentity> carriers = arc.VisibleCores
            .Where(core => core.CarrierActorId?.TeamId == _teamId)
            .Select(core => core.CarrierActorId!).ToHashSet();
        OperationActor[] operationActors = mind.Bodies.Select(body =>
        {
            UnitPlan plan = _sheet.Units.Single(value =>
                value.UnitId == body.UnitId);
            return new OperationActor(
                body.UnitId,
                $"{body.ActorId.TeamId}:{body.ActorId.UnitId}:"
                    + $"{body.ActorId.LifeId}",
                body.ClassId ?? "",
                plan.Role,
                carriers.Contains(body.ActorId),
                body.Position);
        }).ToArray();
        Dictionary<string, OperationPhase> priorPhases = _sheet.Operations
            .ToDictionary(
                value => value.Id,
                value => _operations.State(value.Id).Phase,
                StringComparer.Ordinal);
        _operations.Update(
            mind.Tick,
            operationActors,
            (_, state, condition) => EvaluateOperation(
                condition, state, mind, arc),
            (actor, task, remaining) => OperationFeasible(
                actor, task, remaining));
        foreach (IntelligentOperationPlan operation in _sheet.Operations)
        {
            OperationStateView state = _operations.State(operation.Id);
            if (state.Phase == OperationPhase.Commit
                && priorPhases[operation.Id] != OperationPhase.Commit)
            {
                BindOperationTarget(operation.Id, mind, arc);
            }
            else if (state.Phase == OperationPhase.Dormant)
            {
                _operationTargets.Remove(operation.Id);
            }
        }
    }

    internal UnitPlan Effective(UnitPlan plan)
    {
        OperationDirective? operation = _operations.DirectiveFor(plan.UnitId);
        if (operation is not null
            && !string.IsNullOrEmpty(operation.Task.RoleOverride))
        {
            return plan with { Role = operation.Task.RoleOverride };
        }
        GambitPlan? active = Active;
        return active is not null
            && Applies(active, plan)
            && !string.IsNullOrEmpty(active.RoleOverride)
                ? plan with { Role = active.RoleOverride }
                : plan;
    }

    internal string RoleTag(UnitPlan plan, string fallback)
    {
        OperationDirective? operation = _operations.DirectiveFor(plan.UnitId);
        if (operation is not null)
        {
            string phase = operation.Phase switch
            {
                OperationPhase.Prepare => "p",
                OperationPhase.Commit => "c",
                OperationPhase.Recover => "r",
                _ => "d",
            };
            return $"g-{OperationCode(operation.OperationId)}-{phase}-"
                + TaskCode(operation.Task.Id);
        }
        GambitPlan? active = Active;
        return active is not null && Applies(active, plan)
            ? active.Id
            : fallback;
    }

    internal CarrierPolicy CarrierPolicy(UnitPlan plan)
    {
        PolicyOverlay? overlay = OverlayFor(plan);
        return new CarrierPolicy(
            overlay is { HandoffHealthAtOrBelow: >= 0 }
                ? overlay.HandoffHealthAtOrBelow
                : _sheet.Carrier.HandoffHealthAtOrBelow,
            overlay is { PreferAssignedTheater: >= 0 }
                ? overlay.PreferAssignedTheater == 1
                : _sheet.Carrier.PreferAssignedTheater,
            overlay is { RouteFailureTicks: >= 0 }
                ? overlay.RouteFailureTicks
                : _sheet.Carrier.RouteFailureTicks);
    }

    internal EscortPolicy EscortPolicy(UnitPlan plan)
    {
        PolicyOverlay? overlay = OverlayFor(plan);
        return new EscortPolicy(
            overlay is { FollowDistance: >= 0 }
                ? overlay.FollowDistance
                : _sheet.Escort.FollowDistance,
            overlay is { EscortFocusEnemyCarrier: >= 0 }
                ? overlay.EscortFocusEnemyCarrier == 1
                : _sheet.Escort.FocusEnemyCarrier);
    }

    internal InterceptionPolicy InterceptionPolicy(UnitPlan plan)
    {
        PolicyOverlay? overlay = OverlayFor(plan);
        return new InterceptionPolicy(
            overlay is { InterceptionFocusEnemyCarrier: >= 0 }
                ? overlay.InterceptionFocusEnemyCarrier == 1
                : _sheet.Interception.FocusEnemyCarrier,
            overlay is { LooseCoreFallback: >= 0 }
                ? overlay.LooseCoreFallback == 1
                : _sheet.Interception.LooseCoreFallback);
    }

    internal bool TryActPosition(
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        UnitPlan basePlan,
        bool carriesCore,
        ArenaBasics.Claims claims)
    {
        OperationDirective? operation = _operations.DirectiveFor(body.UnitId);
        GambitPlan? active = Active;
        bool scoped = active is not null && Applies(active, basePlan);
        if (carriesCore && operation is null
            && !(scoped && active!.AppliesWhileCarrying))
            return false;

        PositionIntent intent = operation?.Task.Position
            ?? (scoped && active!.Position is not null
                ? active.Position
                : basePlan.DefaultPosition);
        string engagement = operation?.Task.EngagementIntent ?? (scoped
            ? active!.EngagementIntent
            : basePlan.DefaultEngagementIntent);
        string signature = operation?.Task.SignatureIntent ?? (scoped
            ? active!.SignatureIntent
            : basePlan.DefaultSignatureIntent);
        string intentId = operation is not null
            ? $"{operation.OperationId}:{operation.Task.Id}"
            : scoped ? active!.Id : "base";
        Position[]? goals = Goals(
            mind, arc, body, basePlan, intent, intentId,
            operation is null ? active : null);
        if (goals is null)
            return false;

        GenericActorContext.ObservedEnemyState? carrier =
            ArenaBasics.VisibleEnemyCarrier(mind, _teamId);
        GenericActorContext.ObservedEnemyState? threat = carrier
            ?? mind.Enemies
                .OrderBy(enemy => body.Position.ChebyshevDistance(enemy.Position))
                .ThenBy(enemy => enemy.ActorId)
                .FirstOrDefault();
        Position aim = threat?.Position
            ?? goals.OrderBy(goal => body.Position.ChebyshevDistance(goal))
                .ThenBy(goal => goal.Y).ThenBy(goal => goal.X).First();
        bool spendSignature = signature switch
        {
            "conserve" => false,
            "normal" => mind.Tick % 3 == body.UnitId % 3,
            "aggressive" => true,
            "defensive" => carriesCore || threat is not null
                && body.Position.ChebyshevDistance(threat.Position) <= 3,
            _ => throw new InvalidOperationException(
                $"Unknown signature intent '{signature}'."),
        };
        if (spendSignature && TrySignature(body, threat, aim))
            return true;

        bool shot = engagement switch
        {
            "hold-fire" or "conceal" or "break-contact" => false,
            "carrier-only" or "carrier-focus" => carrier is not null
                && TryShootOnly(body, carrier),
            "normal" or "aggressive" or "opportunistic" =>
                ArenaBasics.TryShoot(_contract, mind, body, threat),
            "defend-in-place" => threat is not null
                && body.Position.ChebyshevDistance(threat.Position) <= 3
                && ArenaBasics.TryShoot(_contract, mind, body, threat),
            _ => throw new InvalidOperationException(
                $"Unknown engagement intent '{engagement}'."),
        };
        if (shot)
            return true;
        if (ArenaBasics.TryMoveToward(
                _contract,
                mind,
                body,
                goals,
                claims,
                $"executing {intentId}"))
        {
            return true;
        }
        body.Hold($"holding {intentId} position");
        return true;
    }

    private bool TrySignature(
        MindBody body,
        GenericActorContext.ObservedEnemyState? threat,
        Position aim)
    {
        if (threat is not null
            && (ArenaBasics.TryUnitSignature(
                    _contract,
                    body,
                    "target-paint",
                    threat.ActorId,
                    "strategy target paint")
                || ArenaBasics.TryHeadingSignature(
                    _contract, body, "tractor-hook", threat.Position,
                    "strategy tractor hook")
                || ArenaBasics.TryHeadingSignature(
                    _contract, body, "rail-line", threat.Position,
                    "strategy rail line")))
        {
            return true;
        }
        foreach (string kind in new[]
                 {
                     "vector-dash", "falling-star", "trip-node", "survey-flare",
                     "hardlight-block", "smoke-canister", "sentinel-seed",
                 })
        {
            if (kind == "vector-dash"
                ? ArenaBasics.TryHeadingSignature(
                    _contract, body, kind, aim, $"strategy {kind}")
                : ArenaBasics.TryPositionSignature(
                    _contract, body, kind, aim, $"strategy {kind}"))
            {
                return true;
            }
        }
        return ArenaBasics.TryDirectionSignature(
                _contract, body, "prism-wall", aim, "strategy prism wall")
            || ArenaBasics.TryParameterlessSignature(
                _contract, body, "null-field", "strategy null field")
            || ArenaBasics.TryParameterlessSignature(
                _contract, body, "kinetic-burst", "strategy kinetic burst");
    }

    private bool TryShootOnly(
        MindBody body,
        GenericActorContext.ObservedEnemyState target)
    {
        GenericActorActionLegality? shoot = _contract.Rules.Actions
            .Where(action => action.Kind
                == GenericActorRulesContract.ActionKind.Attack)
            .Select(action => body.Action(action.Id))
            .FirstOrDefault(action => action is { Available: true });
        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            allowed = shoot?.Constraints.OfType<GenericActorActionLegality
                .ArgumentConstraint.ProjectileHeadingConstraint>()
            .SingleOrDefault();
        GenericActorRulesContract.Form form = _contract.Rules.Forms.Single(value =>
            string.Equals(value.Id, body.FormId, StringComparison.Ordinal));
        GenericActorRulesContract.AttackProfile? attack = form.AttackProfileId
            is string attackId
            ? _contract.Rules.AttackProfiles.Single(value => string.Equals(
                value.Id, attackId, StringComparison.Ordinal))
            : null;
        if (shoot is null || allowed is null || attack is null
            || !TryHeading(body.Position, target.Position, out var heading,
                out int distance)
            || distance > attack.Projectile.MaxTravelTiles
            || !allowed.AllowedValues.Contains(heading)
            || !ClearRay(body.Position, target.Position))
        {
            return false;
        }
        body.Command(
            shoot,
            new GenericActorActionArgument.ProjectileHeadingArgument(heading));
        return true;
    }

    private Position[]? Goals(
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        UnitPlan plan,
        PositionIntent intent,
        string intentId,
        GambitPlan? gambit)
    {
        Position[]? goals = intent.Kind switch
        {
            "base-assignment" => null,
            "zone" => ZoneTiles(intent.Target),
            "path" => PathGoals(body, intent, intentId),
            "anchor-offset" => Anchor(
                    mind, arc, body, plan, intent.Target)
                is Position anchor
                    ? [anchor]
                    : string.IsNullOrEmpty(intent.FallbackZone)
                        ? null
                        : ZoneTiles(intent.FallbackZone),
            _ => throw new InvalidOperationException(
                $"Unknown position intent '{intent.Kind}'."),
        };
        if (goals is null)
            return null;
        int dx = intent.OffsetX;
        int dy = intent.OffsetY;
        if (gambit is not null && gambit.FormationOffsets.Length > 0)
        {
            UnitPlan[] scoped = _sheet.Units.Where(value => Applies(gambit, value))
                .OrderBy(value => value.UnitId).ToArray();
            int index = Array.FindIndex(scoped, value => value.UnitId == plan.UnitId);
            if (index >= 0 && index < gambit.FormationOffsets.Length)
            {
                dx += gambit.FormationOffsets[index].X;
                dy += gambit.FormationOffsets[index].Y;
            }
        }
        return goals
            .Select(goal => NearestOpen(goal.Offset(_mirror ? -dx : dx, dy)))
            .Distinct()
            .ToArray();
    }

    private Position[]? PathGoals(
        MindBody body,
        PositionIntent intent,
        string intentId)
    {
        Position[] path = _sheet.Paths.TryGetValue(
                intent.Target, out Position[]? authored)
            ? authored
            : _sheet.RallyLines.TryGetValue(
                intent.Target, out Position[]? rally)
                ? rally
                : throw new InvalidOperationException(
                    $"Unknown path '{intent.Target}'.");
        var key = (body.UnitId, $"{intentId}:{intent.Target}");
        int index = _pathIndexes.GetValueOrDefault(key);
        while (index < path.Length
               && body.Position.ChebyshevDistance(Mirror(path[index])) <= 1)
        {
            index++;
        }
        _pathIndexes[key] = index;
        if (index < path.Length)
            return [Mirror(path[index])];
        return intent.Arrival switch
        {
            "base-assignment" => null,
            "zone" => ZoneTiles(intent.FallbackZone),
            "hold" when path.Length > 0 => [Mirror(path[^1])],
            _ => throw new InvalidOperationException(
                $"Unknown path arrival '{intent.Arrival}'."),
        };
    }

    private Position? Anchor(
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        MindBody body,
        UnitPlan plan,
        string target)
    {
        HashSet<ActorIdentity> ownCarriers = arc.VisibleCores
            .Where(core => core.CarrierActorId?.TeamId == _teamId)
            .Select(core => core.CarrierActorId!).ToHashSet();
        HashSet<ActorIdentity> enemyCarriers = arc.VisibleCores
            .Where(core => core.CarrierActorId is { } actor
                && actor.TeamId != _teamId)
            .Select(core => core.CarrierActorId!).ToHashSet();
        return target switch
        {
            "own-reactor" => _ownReactor,
            "enemy-reactor" => arc.Reactors.Single(value =>
                value.TeamId != _teamId).Position,
            "next-well" => arc.Wells
                .Where(value => value.NextScheduledBirthTick is not null)
                .OrderBy(value => value.NextScheduledBirthTick)
                .ThenBy(value => value.WellId, StringComparer.Ordinal)
                .Select(value => (Position?)value.Position).FirstOrDefault(),
            "nearest-loose-core" => arc.VisibleCores
                .Where(core => core.Disposition
                    == GenericActorContext.ArcRelayCoreDisposition.Loose)
                .OrderBy(core => body.Position.ChebyshevDistance(core.Position))
                .ThenBy(core => core.CoreId.SourceWellId, StringComparer.Ordinal)
                .ThenBy(core => core.CoreId.SourceOrdinal)
                .Select(core => (Position?)core.Position).FirstOrDefault(),
            "nearest-own-carrier" => mind.Bodies
                .Where(value => ownCarriers.Contains(value.ActorId))
                .OrderBy(value => body.Position.ChebyshevDistance(value.Position))
                .ThenBy(value => value.UnitId)
                .Select(value => (Position?)value.Position).FirstOrDefault(),
            "nearest-enemy-carrier" => mind.Enemies
                .Where(value => enemyCarriers.Contains(value.ActorId))
                .OrderBy(value => body.Position.ChebyshevDistance(value.Position))
                .ThenBy(value => value.ActorId)
                .Select(value => (Position?)value.Position).FirstOrDefault(),
            "nearest-visible-enemy" => mind.Enemies
                .OrderBy(value => body.Position.ChebyshevDistance(value.Position))
                .ThenBy(value => value.ActorId)
                .Select(value => (Position?)value.Position).FirstOrDefault(),
            "partner" => mind.Body(plan.PartnerUnitId)?.Position,
            _ when target.StartsWith("well-", StringComparison.Ordinal) =>
                arc.Wells.Single(value => string.Equals(
                    value.WellId, target[5..], StringComparison.Ordinal)).Position,
            _ when target.StartsWith("ally-role:", StringComparison.Ordinal) =>
                _sheet.Units
                    .Where(candidate => string.Equals(
                        Effective(candidate).Role,
                        target[10..],
                        StringComparison.Ordinal))
                    .Select(candidate => mind.Body(candidate.UnitId))
                    .Where(candidate => candidate is not null)
                    .OrderBy(candidate => body.Position.ChebyshevDistance(
                        candidate!.Position))
                    .ThenBy(candidate => candidate!.UnitId)
                    .Select(candidate => (Position?)candidate!.Position)
                    .FirstOrDefault(),
            _ => throw new InvalidOperationException(
                $"Unknown public anchor '{target}'."),
        };
    }

    private Position[] ZoneTiles(string zoneId)
    {
        Zone zone = _sheet.Zones.TryGetValue(zoneId, out Zone value)
            ? value
            : throw new InvalidOperationException($"Unknown zone '{zoneId}'.");
        return Enumerable.Range(zone.MinY, zone.MaxY - zone.MinY + 1)
            .SelectMany(y => Enumerable.Range(zone.MinX, zone.MaxX - zone.MinX + 1)
                .Select(x => Mirror(new Position(x, y))))
            .Where(position => !IsWall(position))
            .ToArray();
    }

    private void UpdateEnemyMemory(MindContext mind)
    {
        foreach (GenericActorContext.ObservedEnemyState enemy in mind.Enemies)
        {
            _enemyMemory[enemy.ActorId.UnitId] = new EnemyMemory(
                enemy.ActorId,
                enemy.ClassId ?? "",
                enemy.Position,
                mind.Tick);
        }
    }

    private void UpdateOperationTargets(
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc)
    {
        foreach ((string operationId, CausalCarrierTarget target)
                 in _operationTargets.ToArray())
        {
            GenericActorContext.ArcRelayCoreState? visible = arc.VisibleCores
                .SingleOrDefault(core => core.CoreId == target.CoreId);
            target.Observe(visible, mind.Enemies, _teamId, mind.Tick);
        }
    }

    private void BindOperationTarget(
        string operationId,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc)
    {
        if (!string.Equals(operationId, "rear-hook", StringComparison.Ordinal))
            return;
        Zone corridor = _sheet.Zones["north-return"];
        GenericActorContext.ArcRelayCoreState? core = arc.VisibleCores
            .Where(value => value.CarrierActorId is { } actor
                && actor.TeamId != _teamId)
            .Where(value => corridor.Contains(Unmirror(value.Position)))
            .OrderBy(value => value.CoreId.SourceWellId, StringComparer.Ordinal)
            .ThenBy(value => value.CoreId.SourceOrdinal)
            .FirstOrDefault();
        if (core?.CarrierActorId is not { } carrier)
            return;
        GenericActorContext.ObservedEnemyState? enemy = mind.Enemies
            .SingleOrDefault(value => value.ActorId == carrier);
        if (enemy is null)
            return;
        _operationTargets[operationId] = new CausalCarrierTarget(
            core.CoreId, carrier, enemy.Position, mind.Tick);
    }

    private bool OperationFeasible(
        OperationActor actor,
        OperationTask task,
        int remainingTicks)
    {
        Position[] goals = task.Position.Kind switch
        {
            "zone" => ZoneTiles(task.Position.Target),
            "path" => _sheet.Paths.TryGetValue(
                    task.Position.Target, out Position[]? path)
                ? path.Select(Mirror).ToArray()
                : _sheet.RallyLines.TryGetValue(
                    task.Position.Target, out Position[]? rally)
                    ? rally.Select(Mirror).ToArray()
                    : [],
            "base-assignment" or "anchor-offset" => [],
            _ => [],
        };
        if (goals.Length == 0)
            return true;
        int distance = goals.Min(goal =>
            actor.Position.ChebyshevDistance(goal));
        return distance <= remainingTicks + 2;
    }

    private OperationTruth EvaluateOperation(
        OperationCondition condition,
        OperationStateView state,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc)
    {
        if (condition.Fact == "target-core-loose-or-ours")
            return TargetCoreLooseOrOurs(condition, state, arc);
        if (condition.Fact == "target-invalid")
            return TargetInvalid(condition, state, mind, arc);
        if (condition.Fact == "zone-clear")
        {
            Zone zone = OperationZone(condition);
            bool occupied = mind.Enemies.Any(enemy =>
                zone.Contains(Unmirror(enemy.Position)));
            if (occupied)
                return OperationTruth.False;
            HashSet<Position> visible = mind.VisibleTiles
                .Select(tile => tile.Position).ToHashSet();
            bool complete = ZoneTiles(condition.Zone).All(visible.Contains);
            return complete ? OperationTruth.True : OperationTruth.Unknown;
        }

        int actual = condition.Fact switch
        {
            "always" => 1,
            "ticks-until-next-well" => arc.Wells
                .Where(well => well.NextScheduledBirthTick is not null)
                .Select(well => Math.Max(
                    0, well.NextScheduledBirthTick!.Value - mind.Tick))
                .DefaultIfEmpty(int.MaxValue).Min(),
            "visible-enemies-in-zone" => mind.Enemies.Count(enemy =>
                OperationZone(condition).Contains(
                    Unmirror(enemy.Position))),
            "visible-enemy-carriers-in-zone" => EnemyCarriers(mind, arc)
                .Count(enemy => OperationZone(condition).Contains(
                    Unmirror(enemy.Position))),
            "own-carriers-in-zone" => OwnCarriers(mind, arc)
                .Count(body => OperationZone(condition).Contains(
                    Unmirror(body.Position))),
            "own-carried-cores" => arc.VisibleCores.Count(core =>
                core.CarrierActorId?.TeamId == _teamId),
            "visible-loose-or-own-cores-in-zone" => arc.VisibleCores.Count(
                core => (core.Disposition
                        == GenericActorContext.ArcRelayCoreDisposition.Loose
                    || core.CarrierActorId?.TeamId == _teamId)
                    && OperationZone(condition).Contains(
                        Unmirror(core.Position))),
            "task-participants-in-zone" => state.Assignments
                .Where(value => string.IsNullOrEmpty(condition.Subject)
                    || value.TaskId == condition.Subject)
                .Select(value => mind.Body(value.UnitId))
                .Count(body => body is not null
                    && OperationZone(condition).Contains(
                        Unmirror(body.Position))),
            "recently-seen-enemies-in-zone" => _enemyMemory.Values.Count(
                memory => mind.Tick - memory.Tick
                        <= condition.FreshnessTicks
                    && OperationZone(condition).Contains(
                        Unmirror(memory.Position))),
            "unobserved-enemy-class-count" =>
                UnobservedEnemyClassCount(condition, mind),
            "visible-hostile-signatures-in-zone" =>
                arc.VisibleSignatures.Count(signature =>
                    signature.OwnerTeamId != _teamId
                    && signature.Positions.Any(position =>
                        OperationZone(condition).Contains(
                            Unmirror(position)))),
            _ => throw new InvalidOperationException(
                $"Unknown operation fact '{condition.Fact}'."),
        };
        bool result = condition.Operator switch
        {
            "at-least" => actual >= condition.Value,
            "at-most" => actual <= condition.Value,
            "equals" => actual == condition.Value,
            _ => throw new InvalidOperationException(
                $"Unknown operation operator '{condition.Operator}'."),
        };
        return result ? OperationTruth.True : OperationTruth.False;
    }

    private OperationTruth TargetCoreLooseOrOurs(
        OperationCondition condition,
        OperationStateView state,
        GenericActorContext.ModeObservationState.ArcRelay arc)
    {
        _ = condition;
        if (!_operationTargets.TryGetValue(
                state.OperationId, out CausalCarrierTarget? target))
        {
            return OperationTruth.Unknown;
        }
        return target.Success(arc.VisibleCores, _teamId);
    }

    private OperationTruth TargetInvalid(
        OperationCondition condition,
        OperationStateView state,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc)
    {
        if (!_operationTargets.TryGetValue(
                state.OperationId, out CausalCarrierTarget? target))
        {
            return OperationTruth.Unknown;
        }
        Zone zone = OperationZone(condition);
        return target.Invalid(
            arc.VisibleCores,
            mind.Enemies,
            _teamId,
            mind.Tick,
            condition.FreshnessTicks,
            position => zone.Contains(Unmirror(position)));
    }

    private int UnobservedEnemyClassCount(
        OperationCondition condition,
        MindContext mind)
    {
        HashSet<int> visible = mind.Enemies
            .Select(enemy => enemy.ActorId.UnitId).ToHashSet();
        return _contract.Topology.UnitSlots
            .Where(slot => slot.TeamId != _teamId)
            .Where(slot => condition.ClassIds.Length == 0
                || condition.ClassIds.Contains(
                    slot.ClassId ?? "", StringComparer.Ordinal))
            .Count(slot =>
            {
                int lastSeen = _enemyMemory.TryGetValue(
                        slot.UnitId, out EnemyMemory? memory)
                    ? memory.Tick
                    : 0;
                return !visible.Contains(slot.UnitId)
                    && mind.Tick - lastSeen >= condition.FreshnessTicks;
            });
    }

    private IEnumerable<MindBody> OwnCarriers(
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc)
    {
        HashSet<ActorIdentity> identities = arc.VisibleCores
            .Where(core => core.CarrierActorId?.TeamId == _teamId)
            .Select(core => core.CarrierActorId!).ToHashSet();
        return mind.Bodies.Where(body => identities.Contains(body.ActorId));
    }

    private IEnumerable<GenericActorContext.ObservedEnemyState> EnemyCarriers(
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc)
    {
        HashSet<ActorIdentity> identities = arc.VisibleCores
            .Where(core => core.CarrierActorId is { } actor
                && actor.TeamId != _teamId)
            .Select(core => core.CarrierActorId!).ToHashSet();
        return mind.Enemies.Where(enemy => identities.Contains(enemy.ActorId));
    }

    private Zone OperationZone(OperationCondition condition) =>
        !string.IsNullOrEmpty(condition.Zone)
        && _sheet.Zones.TryGetValue(condition.Zone, out Zone zone)
            ? zone
            : throw new InvalidOperationException(
                $"Operation fact '{condition.Fact}' needs known zone "
                + $"'{condition.Zone}'.");

    private bool Evaluate(
        ConditionClause clause,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        bool ownPulse,
        bool enemyPulse)
    {
        int actual = FactValue(clause, mind, arc, ownPulse, enemyPulse);
        return clause.Operator switch
        {
            "at-least" => actual >= clause.Value,
            "at-most" => actual <= clause.Value,
            "equals" => actual == clause.Value,
            _ => throw new InvalidOperationException(
                $"Unknown clause operator '{clause.Operator}'."),
        };
    }

    private int FactValue(
        ConditionClause clause,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        bool ownPulse,
        bool enemyPulse)
    {
        return clause.Fact switch
        {
            "tick" => mind.Tick,
            "own-pulse-event" => ownPulse ? 1 : 0,
            "enemy-pulse-event" => enemyPulse ? 1 : 0,
            "own-carried-cores" => arc.VisibleCores.Count(core =>
                core.CarrierActorId?.TeamId == _teamId),
            "enemy-carried-cores" => arc.VisibleCores.Count(core =>
                core.CarrierActorId is { } actor && actor.TeamId != _teamId),
            "visible-loose-cores" => arc.VisibleCores.Count(core =>
                core.Disposition
                    == GenericActorContext.ArcRelayCoreDisposition.Loose),
            "live-own-bodies" => mind.Bodies.Length,
            "visible-enemies" => mind.Enemies.Length,
            "own-bodies-in-zone" => mind.Bodies.Count(body =>
                ClauseZone(clause).Contains(Unmirror(body.Position))),
            "own-min-zone-tenure" => mind.Bodies
                .Where(body => ClauseZone(clause).Contains(
                    Unmirror(body.Position)))
                .Select(body => mind.Tick - _zoneEnteredTick.GetValueOrDefault(
                    (body.ActorId, clause.Zone),
                    mind.Tick))
                .DefaultIfEmpty(0)
                .Min(),
            "enemy-bodies-in-zone" => mind.Enemies.Count(enemy =>
                ClauseZone(clause).Contains(Unmirror(enemy.Position))),
            "own-carriers-in-zone" => CarrierPositions(mind, arc, own: true)
                .Count(position => ClauseZone(clause).Contains(Unmirror(position))),
            "enemy-carriers-in-zone" => CarrierPositions(mind, arc, own: false)
                .Count(position => ClauseZone(clause).Contains(Unmirror(position))),
            "loose-cores-in-zone" => arc.VisibleCores.Count(core =>
                core.Disposition
                    == GenericActorContext.ArcRelayCoreDisposition.Loose
                && ClauseZone(clause).Contains(Unmirror(core.Position))),
            "ticks-until-next-well" => arc.Wells
                .Where(well => well.NextScheduledBirthTick is not null)
                .Select(well => Math.Max(
                    0, well.NextScheduledBirthTick!.Value - mind.Tick))
                .DefaultIfEmpty(int.MaxValue).Min(),
            "own-pulses" => Pulses(arc, own: true),
            "enemy-pulses" => Pulses(arc, own: false),
            "pulse-deficit" => Math.Max(
                0, Pulses(arc, own: false) - Pulses(arc, own: true)),
            "route-failure-bodies" => 0,
            _ => throw new InvalidOperationException(
                $"Unknown strategy fact '{clause.Fact}'."),
        };
    }

    private Zone ClauseZone(ConditionClause clause) =>
        !string.IsNullOrEmpty(clause.Zone)
        && _sheet.Zones.TryGetValue(clause.Zone, out Zone zone)
            ? zone
            : throw new InvalidOperationException(
                $"Fact '{clause.Fact}' needs known zone '{clause.Zone}'.");

    private void UpdateZoneTenure(MindContext mind)
    {
        var present = new HashSet<(ActorIdentity Actor, string Zone)>();
        foreach (MindBody body in mind.Bodies)
        {
            Position authored = Unmirror(body.Position);
            foreach ((string zoneId, Zone zone) in _sheet.Zones)
            {
                if (!zone.Contains(authored))
                    continue;
                var key = (body.ActorId, zoneId);
                present.Add(key);
                _zoneEnteredTick.TryAdd(key, mind.Tick);
            }
        }
        foreach (var key in _zoneEnteredTick.Keys
                     .Where(key => !present.Contains(key)).ToArray())
        {
            _zoneEnteredTick.Remove(key);
        }
    }

    private IEnumerable<Position> CarrierPositions(
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        bool own)
    {
        HashSet<ActorIdentity> carriers = arc.VisibleCores
            .Where(core => core.CarrierActorId is { } actor
                && (actor.TeamId == _teamId) == own)
            .Select(core => core.CarrierActorId!).ToHashSet();
        return own
            ? mind.Bodies.Where(body => carriers.Contains(body.ActorId))
                .Select(body => body.Position)
            : mind.Enemies.Where(enemy => carriers.Contains(enemy.ActorId))
                .Select(enemy => enemy.Position);
    }

    private int Pulses(
        GenericActorContext.ModeObservationState.ArcRelay arc,
        bool own)
    {
        GenericActorRulesContract.ArcRelayGameMode mode =
            (GenericActorRulesContract.ArcRelayGameMode)_contract.Rules.GameMode;
        int teamId = own
            ? _teamId
            : arc.Reactors.Single(value => value.TeamId != _teamId).TeamId;
        int integrity = arc.Reactors.Single(value => value.TeamId == teamId)
            .IntegritySegments;
        return mode.ArcRelayVictory.PulsesToDestroyReactor - integrity;
    }

    private void Activate(GambitPlan gambit, int tick)
    {
        _activeId = gambit.Id;
        _activeStartedTick = tick;
        ResetPaths(gambit.Id);
    }

    private void Deactivate(GambitPlan gambit, int tick)
    {
        _activeId = null;
        _cooldownUntil[gambit.Id] = tick + gambit.CooldownTicks;
        _transitionBlockedUntil = tick + 1;
        ResetPaths(gambit.Id);
    }

    private void ResetPaths(string intentId)
    {
        foreach ((int unitId, string key) in _pathIndexes.Keys
                     .Where(value => value.IntentId.StartsWith(
                         intentId + ":", StringComparison.Ordinal)).ToArray())
        {
            _pathIndexes.Remove((unitId, key));
        }
    }

    private PolicyOverlay? OverlayFor(UnitPlan plan)
    {
        GambitPlan? active = Active;
        UnitPlan basePlan = _sheet.Units.Single(value =>
            value.UnitId == plan.UnitId);
        return active is not null && Applies(active, basePlan)
            ? active.Policies
            : null;
    }

    private static bool Applies(GambitPlan gambit, UnitPlan plan) =>
        gambit.ScopeUnitIds.Contains(plan.UnitId)
        || gambit.ScopeRoles.Contains(plan.Role, StringComparer.Ordinal);

    private static string OperationCode(string id) => id switch
    {
        "lantern-sweep" => "ls",
        "rear-hook" => "rh",
        _ => "op",
    };

    private static string TaskCode(string id) => id switch
    {
        "carrier" or "carrier-return" => "car",
        "lantern" => "lan",
        "screen" or "screen-return" => "scr",
        "north-hook" => "nh",
        "south-hook" => "sh",
        "extract" => "ext",
        _ => "task",
    };

    private Position Mirror(Position position) => _mirror
        ? new Position(_contract.Map.Width - 1 - position.X, position.Y)
        : position;

    private Position Unmirror(Position position) => Mirror(position);

    private Position NearestOpen(Position desired) =>
        Enumerable.Range(0, _contract.Map.Height)
            .SelectMany(y => Enumerable.Range(0, _contract.Map.Width)
                .Select(x => new Position(x, y)))
            .Where(position => !IsWall(position))
            .OrderBy(position => position.ChebyshevDistance(desired))
            .ThenBy(position => position.Y)
            .ThenBy(position => position.X)
            .First();

    private bool IsWall(Position position) =>
        position.X < 0 || position.Y < 0
        || position.X >= _contract.Map.Width
        || position.Y >= _contract.Map.Height
        || _contract.Map.TileRows[position.Y][position.X] == '#';

    private static bool TryHeading(
        Position from,
        Position to,
        out ProjectileHeading heading,
        out int distance)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        bool aligned = dx == 0 || dy == 0 || Math.Abs(dx) == Math.Abs(dy);
        distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
        heading = Headings.OrderBy(value =>
        {
            (int hx, int hy) = value.Vector();
            return Math.Abs(Math.Sign(dx) - hx) + Math.Abs(Math.Sign(dy) - hy);
        }).ThenBy(value => (int)value).First();
        return aligned && distance > 0;
    }

    private bool ClearRay(Position from, Position to)
    {
        int dx = Math.Sign(to.X - from.X);
        int dy = Math.Sign(to.Y - from.Y);
        Position cursor = from.Offset(dx, dy);
        while (cursor != to)
        {
            if (IsWall(cursor)
                || dx != 0 && dy != 0
                && (IsWall(cursor.Offset(-dx, 0))
                    || IsWall(cursor.Offset(0, -dy))))
            {
                return false;
            }
            cursor = cursor.Offset(dx, dy);
        }
        return !IsWall(to);
    }

    private sealed record EnemyMemory(
        ActorIdentity Actor,
        string ClassId,
        Position Position,
        int Tick);

}
