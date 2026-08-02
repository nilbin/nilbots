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
    }

    internal UnitPlan Effective(UnitPlan plan)
    {
        GambitPlan? active = Active;
        return active is not null
            && Applies(active, plan)
            && !string.IsNullOrEmpty(active.RoleOverride)
                ? plan with { Role = active.RoleOverride }
                : plan;
    }

    internal string RoleTag(UnitPlan plan, string fallback)
    {
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
        GambitPlan? active = Active;
        bool scoped = active is not null && Applies(active, basePlan);
        if (carriesCore && !(scoped && active!.AppliesWhileCarrying))
            return false;

        PositionIntent intent = scoped && active!.Position is not null
            ? active.Position
            : basePlan.DefaultPosition;
        string engagement = scoped
            ? active!.EngagementIntent
            : basePlan.DefaultEngagementIntent;
        string signature = scoped
            ? active!.SignatureIntent
            : basePlan.DefaultSignatureIntent;
        string intentId = scoped ? active!.Id : "base";
        Position[]? goals = Goals(
            mind, arc, body, basePlan, intent, intentId, active);
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
            "hold-fire" => false,
            "carrier-only" => carrier is not null
                && TryShootOnly(body, carrier),
            "normal" or "aggressive" =>
                ArenaBasics.TryShoot(_contract, mind, body, threat),
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
}
