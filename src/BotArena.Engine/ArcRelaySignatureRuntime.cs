using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Deterministic match-scoped state machine for Arc Relay's sixteen class
/// signatures. It owns operation identities, tells, active lifetimes and
/// signature cooldowns; the common session applies the returned body/combat
/// effects so life and projectile chronology stays in one authority.
/// </summary>
internal sealed class ArcRelaySignatureRuntime
{
    private readonly ActorMapDefinition _map;
    private readonly ImmutableDictionary<string, ArcRelaySignatureDefinition>
        _byAction;
    private readonly ImmutableDictionary<(int TeamId, int UnitId),
        ArcRelaySignatureDefinition> _bySlot;
    private readonly Dictionary<string, Operation> _operations =
        new(StringComparer.Ordinal);
    private readonly Dictionary<ActorIdentity, int> _readyAtTick = [];
    private long _nextOperationOrdinal;

    private readonly bool _sideFairTargeting;
    private readonly ImmutableDictionary<int, Position> _ownReactorByTeam;

    /// <summary>
    /// Equal-distance target ties break toward the shooter's own reactor
    /// under -03 side-fair targeting: a raw ActorId tie-break always prefers
    /// the enemy's lowest slots, which spawn on the same world side for both
    /// teams and so pick opposite relative targets on a rotationally bound
    /// map. Historical rulesets keep the ActorId order byte-for-byte.
    /// </summary>
    private long TargetTieKey(int ownerTeamId, Life candidate) =>
        _sideFairTargeting
            && _ownReactorByTeam.TryGetValue(ownerTeamId, out Position own)
            ? candidate.Position.ChebyshevDistance(own)
            : 0;

    public ArcRelaySignatureRuntime(
        ActorResolvedMatchDefinition definition,
        ArcRelayGameModeDefinition mode)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(mode);
        _map = definition.Map;
        _sideFairTargeting = mode.AlternatingResolutionOrder;
        if (definition.ModeMapBinding is
                ArcRelayActorModeMapBindingDefinition reactorBinding)
        {
            Dictionary<int, int> participantTeam = definition.Topology
                .Participants.ToDictionary(
                    value => value.ParticipantId,
                    value => value.TeamId);
            Dictionary<string, ActorMapRegionDefinition> regions =
                definition.Map.Regions.ToDictionary(
                    value => value.RegionId,
                    StringComparer.Ordinal);
            _ownReactorByTeam = definition.ParticipantRegionAssignments
                .Where(value => string.Equals(
                    value.RegionRoleId,
                    reactorBinding.ReactorRegionRoleId,
                    StringComparison.Ordinal))
                .ToImmutableDictionary(
                    value => participantTeam[value.ParticipantId],
                    value => regions[value.MapRegionId].Tiles.Single());
        }
        else
        {
            _ownReactorByTeam = ImmutableDictionary<int, Position>.Empty;
        }
        _byAction = mode.Signatures.ToImmutableDictionary(
            value => value.ActionId,
            StringComparer.Ordinal);
        _bySlot = definition.Topology.UnitSlots.ToImmutableDictionary(
            value => (value.TeamId, value.UnitId),
            value => mode.Signatures.Single(signature => string.Equals(
                signature.ClassId,
                value.ClassId,
                StringComparison.Ordinal)));
    }

    public ArcRelaySignatureDefinition DefinitionFor(ActorIdentity actor) =>
        _bySlot[(actor.TeamId, actor.UnitId)];

    public ArcRelaySignatureDefinition DefinitionForAction(string actionId) =>
        _byAction[actionId];

    public bool CanStart(
        ActorIdentity actor,
        string actionId,
        int tick,
        Position position)
    {
        ArcRelaySignatureDefinition declared = DefinitionFor(actor);
        return string.Equals(
                declared.ActionId,
                actionId,
                StringComparison.Ordinal)
            && (!_readyAtTick.TryGetValue(actor, out int ready) || tick >= ready)
            && !SuppressedByHostileNullField(actor.TeamId, position, tick);
    }

    public ImmutableArray<Position> PositionTargets(
        ActorIdentity actor,
        Position source,
        IReadOnlySet<Position> visibleTiles,
        IReadOnlyCollection<Life> lives,
        bool carriesCore)
    {
        ArcRelaySignatureDefinition signature = DefinitionFor(actor);
        IEnumerable<Position> candidates = AllFloorTiles();
        candidates = signature switch
        {
            ArcRelaySignatureDefinition.SurveyFlare value =>
                candidates.Where(target =>
                    source.ChebyshevDistance(target) is > 0
                        and var distance
                    && distance <= value.Range),
            ArcRelaySignatureDefinition.FallingStar value =>
                candidates.Where(target =>
                    source.ChebyshevDistance(target) <= value.Range
                    && visibleTiles.Contains(target)),
            ArcRelaySignatureDefinition.TripNode =>
                PlacementTargets(source, lives, forbidTaggedTile: true),
            ArcRelaySignatureDefinition.ArcToss value when carriesCore =>
                candidates.Where(target =>
                    IsStraight(source, target)
                    && source.ChebyshevDistance(target) is > 0
                        and var distance
                    && distance <= value.Range
                    // A target behind an adjacent wall clips back to the
                    // source tile. Offering it would create a degenerate
                    // [source,source] tell and abort the authoritative state
                    // before the mind can observe another frame.
                    && ClipStraightLanding(source, target) != source),
            ArcRelaySignatureDefinition.HardlightBlock =>
                PlacementTargets(source, lives, forbidTaggedTile: true),
            ArcRelaySignatureDefinition.SmokeCanister value =>
                candidates.Where(target =>
                    source.ChebyshevDistance(target) <= value.Range),
            ArcRelaySignatureDefinition.SentinelSeed
                or ArcRelaySignatureDefinition.SentinelSeed2 =>
                PlacementTargets(source, lives, forbidTaggedTile: true),
            _ => [],
        };
        return candidates.OrderBy(value => value.Y)
            .ThenBy(value => value.X)
            .ToImmutableArray();
    }

    public ImmutableArray<GenericActorRuntimeActionArgument.UnitTarget>
        UnitTargets(
            ActorIdentity actor,
            Position source,
            IReadOnlySet<Position> visibleTiles,
            IReadOnlyCollection<Life> lives)
    {
        ArcRelaySignatureDefinition signature = DefinitionFor(actor);
        return lives.Where(target => signature switch
            {
                ArcRelaySignatureDefinition.RepairBeam value =>
                    target.ActorId.TeamId == actor.TeamId
                    && target.ActorId != actor
                    && target.Health < target.MaxHealth
                    && source.ChebyshevDistance(target.Position) <= value.Range
                    && visibleTiles.Contains(target.Position),
                ArcRelaySignatureDefinition.Exchange value =>
                    target.ActorId.TeamId == actor.TeamId
                    && target.ActorId != actor
                    && source.ChebyshevDistance(target.Position) <= value.Range
                    && visibleTiles.Contains(target.Position),
                ArcRelaySignatureDefinition.TargetPaint value =>
                    target.ActorId.TeamId != actor.TeamId
                    && source.ChebyshevDistance(target.Position) <= value.Range
                    && visibleTiles.Contains(target.Position),
                _ => false,
            })
            .OrderBy(value => value.ActorId)
            .Select(value => new GenericActorRuntimeActionArgument.UnitTarget(
                value.ActorId.TeamId,
                value.ActorId.UnitId))
            .ToImmutableArray();
    }

    public TickResult Start(
        int tick,
        ActorIdentity actor,
        Position source,
        string actionId,
        ImmutableArray<GenericActorRuntimeActionArgument> arguments,
        IReadOnlyCollection<Life> lives)
    {
        ArcRelaySignatureDefinition definition = _byAction[actionId];
        string operationId = $"arc-signature-{_nextOperationOrdinal++}";
        ActorIdentity? targetActor = arguments
            .OfType<GenericActorRuntimeActionArgument.UnitTargetArgument>()
            .Select(value => lives.Single(life =>
                life.ActorId.TeamId == value.Value.TeamId
                && life.ActorId.UnitId == value.Value.UnitId).ActorId)
            .SingleOrDefault();
        Position? targetPosition = arguments
            .OfType<GenericActorRuntimeActionArgument.PositionTargetArgument>()
            .Select(value => (Position?)value.Value)
            .SingleOrDefault();
        if (targetPosition is null && targetActor is not null)
        {
            targetPosition = lives.Single(value =>
                value.ActorId == targetActor).Position;
        }
        ProjectileHeading? heading = arguments
            .OfType<GenericActorRuntimeActionArgument.ProjectileHeadingArgument>()
            .Select(value => (ProjectileHeading?)value.Value)
            .SingleOrDefault();
        Direction? direction = arguments
            .OfType<GenericActorRuntimeActionArgument.DirectionArgument>()
            .Select(value => (Direction?)value.Value)
            .SingleOrDefault();

        if (definition is ArcRelaySignatureDefinition.ArcToss
            && targetPosition is Position requestedLanding)
        {
            targetPosition = ClipStraightLanding(source, requestedLanding);
        }

        Operation operation = CreateOperation(
            operationId,
            definition,
            actor,
            source,
            targetActor,
            targetPosition,
            heading,
            direction,
            tick);
        var events = ImmutableArray.CreateBuilder<GenericActorModeEvent>();
        ReplaceOwnedConstruct(operation, events);
        _operations.Add(operationId, operation);
        if (CooldownStartsAtActivation(definition))
            _readyAtTick[actor] = checked(tick + definition.CooldownTicks);

        events.Add(SignatureEvent(operation, "started"));
        var effects = ImmutableArray.CreateBuilder<Effect>();
        if (definition is ArcRelaySignatureDefinition.TractorHook)
        {
            effects.Add(new Effect.TractorHook(
                operationId,
                actor,
                heading!.Value));
            Complete(operation, tick, "completed", events);
        }
        else if (definition is ArcRelaySignatureDefinition.TractorHook2 hook2)
        {
            effects.Add(new Effect.HookBolt(
                operationId,
                actor,
                source,
                heading!.Value,
                hook2.Range,
                hook2.MaxPullTiles,
                hook2.BoltTilesPerAdvance));
            Complete(operation, tick, "launched", events);
        }
        else if (definition is ArcRelaySignatureDefinition.PrismWall
                 or ArcRelaySignatureDefinition.TripNode
                 or ArcRelaySignatureDefinition.NullField
                 or ArcRelaySignatureDefinition.HardlightBlock
                 or ArcRelaySignatureDefinition.TargetPaint
                 or ArcRelaySignatureDefinition.SmokeCanister
                 or ArcRelaySignatureDefinition.SentinelSeed
                 or ArcRelaySignatureDefinition.SentinelSeed2)
        {
            operation.Phase = ArcRelaySignatureState.SignaturePhase.Active;
        }
        return new TickResult(events.ToImmutable(), effects.ToImmutable());
    }

    public TickResult Advance(int tick, IReadOnlyCollection<Life> lives)
    {
        var events = ImmutableArray.CreateBuilder<GenericActorModeEvent>();
        var effects = ImmutableArray.CreateBuilder<Effect>();
        Dictionary<ActorIdentity, Life> world = lives.ToDictionary(
            value => value.ActorId);

        foreach (Operation operation in _operations.Values
                     .OrderBy(value => value.OperationId, StringComparer.Ordinal)
                     .ToArray())
        {
            bool suppressed = SuppressedByHostileNullField(
                operation.OwnerActorId.TeamId,
                operation.Positions,
                tick,
                operation.OperationId);
            if (suppressed
                && operation.Phase
                    == ArcRelaySignatureState.SignaturePhase.Channel)
            {
                Complete(operation, tick, "ended-null-field", events);
                continue;
            }
            operation.Suppressed = suppressed;
            if (operation.EndsAtTick == tick)
            {
                Complete(operation, tick, "expired", events);
                continue;
            }
            if (operation.CompletesAtTick != tick)
            {
                AdvanceMaintained(operation, tick, world, effects, events);
                continue;
            }

            switch (operation.Definition)
            {
                case ArcRelaySignatureDefinition.VectorDash:
                    effects.Add(new Effect.VectorDash(
                        operation.OperationId,
                        operation.OwnerActorId,
                        operation.Heading!.Value));
                    Complete(operation, tick, "completed", events);
                    break;
                case ArcRelaySignatureDefinition.FallingStar:
                    effects.Add(new Effect.FallingStar(
                        operation.OperationId,
                        operation.OwnerActorId,
                        operation.TargetPosition!.Value));
                    Complete(operation, tick, "completed", events);
                    break;
                case ArcRelaySignatureDefinition.ArcToss toss
                    when operation.Phase
                        == ArcRelaySignatureState.SignaturePhase.Tell:
                    operation.Phase = ArcRelaySignatureState.SignaturePhase
                        .InFlight;
                    operation.CompletesAtTick = checked(
                        tick + Math.Max(
                            1,
                            (operation.SourcePosition.ChebyshevDistance(
                                operation.TargetPosition!.Value)
                             + toss.TravelTilesPerTick - 1)
                            / toss.TravelTilesPerTick));
                    effects.Add(new Effect.ArcTossLaunch(
                        operation.OperationId,
                        operation.OwnerActorId,
                        operation.TargetPosition.Value,
                        operation.CompletesAtTick.Value));
                    _readyAtTick[operation.OwnerActorId] = checked(
                        tick + toss.CooldownTicks);
                    events.Add(SignatureEvent(operation, "launched"));
                    break;
                case ArcRelaySignatureDefinition.Exchange:
                    effects.Add(new Effect.Exchange(
                        operation.OperationId,
                        operation.OwnerActorId,
                        operation.TargetActorId!,
                        operation.SourcePosition,
                        operation.TargetPosition!.Value));
                    Complete(operation, tick, "completed", events);
                    break;
                case ArcRelaySignatureDefinition.RailLine:
                    effects.Add(new Effect.RailLine(
                        operation.OperationId,
                        operation.OwnerActorId,
                        operation.Heading!.Value));
                    Complete(operation, tick, "completed", events);
                    break;
                case ArcRelaySignatureDefinition.NullField2 field
                    when operation.Phase
                        == ArcRelaySignatureState.SignaturePhase.Tell:
                    operation.Phase = ArcRelaySignatureState.SignaturePhase
                        .Active;
                    operation.CompletesAtTick = null;
                    operation.EndsAtTick = checked(
                        tick + field.DurationTicks);
                    events.Add(SignatureEvent(operation, "activated"));
                    break;
                case ArcRelaySignatureDefinition.KineticBurst:
                    effects.Add(new Effect.KineticBurst(
                        operation.OperationId,
                        operation.OwnerActorId,
                        operation.SourcePosition));
                    Complete(operation, tick, "completed", events);
                    break;
                case ArcRelaySignatureDefinition.SurveyFlare flare
                    when operation.Phase
                        == ArcRelaySignatureState.SignaturePhase.InFlight:
                    operation.Phase = ArcRelaySignatureState.SignaturePhase
                        .Active;
                    operation.CompletesAtTick = null;
                    operation.EndsAtTick = checked(tick + flare.DurationTicks);
                    operation.Positions = Radius(
                        operation.TargetPosition!.Value,
                        flare.RevealRadius);
                    events.Add(SignatureEvent(operation, "landed"));
                    break;
                case ArcRelaySignatureDefinition.ArcToss:
                    effects.Add(new Effect.ArcTossLand(
                        operation.OperationId,
                        operation.OwnerActorId,
                        operation.TargetPosition!.Value));
                    Complete(operation, tick, "landed", events);
                    break;
            }
        }
        return new TickResult(events.ToImmutable(), effects.ToImmutable());
    }

    public TickResult ResolvePostMovement(
        int tick,
        IReadOnlyCollection<Life> lives)
    {
        var events = ImmutableArray.CreateBuilder<GenericActorModeEvent>();
        var effects = ImmutableArray.CreateBuilder<Effect>();
        foreach (Operation operation in _operations.Values
                     .Where(value => value.Definition
                         is ArcRelaySignatureDefinition.TripNode
                         && !value.Suppressed)
                     .OrderBy(value => value.OperationId, StringComparer.Ordinal)
                     .ToArray())
        {
            Life? target = lives.Where(value =>
                    value.ActorId.TeamId != operation.OwnerActorId.TeamId
                    && value.Position == operation.Positions[0])
                .OrderBy(value => value.ActorId)
                .FirstOrDefault();
            if (target is null)
                continue;
            int damage = ((ArcRelaySignatureDefinition.TripNode)
                operation.Definition).TriggerDamage;
            effects.Add(new Effect.TripNode(
                operation.OperationId,
                operation.OwnerActorId,
                target.ActorId,
                damage));
            Remove(operation, "triggered", events);
        }
        return new TickResult(events.ToImmutable(), effects.ToImmutable());
    }

    public void NotifyDamaged(
        int tick,
        IReadOnlyCollection<ActorIdentity> damaged,
        ImmutableArray<GenericActorModeEvent>.Builder events)
    {
        HashSet<ActorIdentity> set = damaged.ToHashSet();
        foreach (Operation operation in _operations.Values.ToArray())
        {
            if (operation.Definition is ArcRelaySignatureDefinition.RailLine rail
                && operation.Phase == ArcRelaySignatureState.SignaturePhase.Tell
                && set.Contains(operation.OwnerActorId))
            {
                _readyAtTick[operation.OwnerActorId] = checked(
                    tick + rail.CancelCooldownTicks);
                Remove(operation, "cancelled-damage", events);
            }
            else if (operation.Definition
                        is ArcRelaySignatureDefinition.RepairBeam
                     && (set.Contains(operation.OwnerActorId)
                         || operation.TargetActorId is ActorIdentity target
                         && set.Contains(target)))
            {
                Complete(operation, tick, "interrupted-damage", events);
            }
        }
    }

    public void NotifyMoved(
        int tick,
        IReadOnlyCollection<ActorIdentity> moved,
        ImmutableArray<GenericActorModeEvent>.Builder events)
    {
        HashSet<ActorIdentity> set = moved.ToHashSet();
        foreach (Operation operation in _operations.Values
                     .Where(value => value.Definition
                         is ArcRelaySignatureDefinition.RepairBeam
                         && (set.Contains(value.OwnerActorId)
                             || value.TargetActorId is ActorIdentity target
                             && set.Contains(target)))
                     .ToArray())
        {
            Complete(operation, tick, "interrupted-movement", events);
        }
    }

    public void NotifyDestroyed(
        int tick,
        IReadOnlyCollection<ActorIdentity> destroyed,
        ImmutableArray<GenericActorModeEvent>.Builder events)
    {
        HashSet<ActorIdentity> set = destroyed.ToHashSet();
        foreach (Operation operation in _operations.Values
                     .Where(value =>
                         set.Contains(value.OwnerActorId)
                         && value.Phase is ArcRelaySignatureState.SignaturePhase
                             .Tell or ArcRelaySignatureState.SignaturePhase
                             .Channel
                         || value.Definition
                             is ArcRelaySignatureDefinition.RepairBeam
                         && value.TargetActorId is ActorIdentity target
                         && set.Contains(target))
                     .ToArray())
        {
            Complete(
                operation,
                tick,
                set.Contains(operation.OwnerActorId)
                    ? "owner-destroyed"
                    : "target-destroyed",
                events);
        }
    }

    public ImmutableArray<ArcRelaySignatureState> Project(int tick) =>
        _operations.Values.OrderBy(value => value.OperationId, StringComparer.Ordinal)
            .Select(value => new ArcRelaySignatureState(
                value.OperationId,
                value.Definition.SignatureId,
                value.Definition.Kind,
                value.OwnerActorId,
                value.OwnerActorId.TeamId,
                value.Phase,
                value.StartedTick,
                value.CompletesAtTick,
                value.EndsAtTick,
                value.Positions,
                value.TargetActorId,
                value.RemainingCapacity,
                SuppressedByHostileNullField(
                    value.OwnerActorId.TeamId,
                    value.Positions,
                    tick)))
            .ToImmutableArray();

    public bool IsSmokeAt(Position position, int tick) =>
        _operations.Values.Any(value =>
            value.Definition is ArcRelaySignatureDefinition.SmokeCanister
            && value.Phase == ArcRelaySignatureState.SignaturePhase.Active
            && value.EndsAtTick > tick
            && value.Positions.Contains(position));

    /// <summary>
    /// Materializes the smoke tiles that actually occlude one team's vision at
    /// this tick. Projection asks this once per team instead of scanning every
    /// live signature for every tile of every sensor ray.
    /// </summary>
    public HashSet<Position> OccludingSmokeForTeam(int teamId, int tick)
    {
        var smoke = new HashSet<Position>();
        var revealed = new HashSet<Position>();
        foreach (Operation operation in _operations.Values)
        {
            if (operation.Phase
                    != ArcRelaySignatureState.SignaturePhase.Active
                || operation.EndsAtTick <= tick)
            {
                continue;
            }
            if (operation.Definition
                    is ArcRelaySignatureDefinition.SmokeCanister)
            {
                smoke.UnionWith(operation.Positions);
            }
            else if (operation.Definition
                        is ArcRelaySignatureDefinition.SurveyFlare
                     && operation.OwnerActorId.TeamId == teamId)
            {
                revealed.UnionWith(operation.Positions);
            }
        }
        smoke.ExceptWith(revealed);
        return smoke;
    }

    public bool IsRevealedForTeam(Position position, int teamId, int tick) =>
        _operations.Values.Any(value =>
            value.Definition is ArcRelaySignatureDefinition.SurveyFlare
            && value.OwnerActorId.TeamId == teamId
            && value.Phase == ArcRelaySignatureState.SignaturePhase.Active
            && value.EndsAtTick > tick
            && value.Positions.Contains(position));

    public bool BlocksBody(Position position, int tick) =>
        _operations.Values.Any(value =>
            value.Definition is ArcRelaySignatureDefinition.HardlightBlock
            && value.Phase == ArcRelaySignatureState.SignaturePhase.Active
            && value.EndsAtTick > tick
            && !value.Suppressed
            && value.Positions.Contains(position));

    public bool TryConsumeProjectile(
        Position position,
        int projectileTeamId,
        int tick,
        out GenericActorModeEvent? modeEvent)
    {
        Operation? construct = _operations.Values
            .Where(value =>
                value.OwnerActorId.TeamId != projectileTeamId
                && value.Phase == ArcRelaySignatureState.SignaturePhase.Active
                && !value.Suppressed
                && value.Positions.Contains(position)
                && value.Definition is ArcRelaySignatureDefinition.PrismWall
                    or ArcRelaySignatureDefinition.TripNode
                    or ArcRelaySignatureDefinition.HardlightBlock
                    or ArcRelaySignatureDefinition.SentinelSeed
                    or ArcRelaySignatureDefinition.SentinelSeed2)
            .OrderBy(value => value.OperationId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (construct is null)
        {
            modeEvent = null;
            return false;
        }
        construct.RemainingCapacity--;
        string reason = construct.RemainingCapacity == 0
            ? "destroyed-projectile"
            : "projectile-contact";
        modeEvent = SignatureEvent(construct, reason);
        if (construct.RemainingCapacity == 0)
            _operations.Remove(construct.OperationId);
        return true;
    }

    public int TargetPaintBonus(
        ActorIdentity attacker,
        ActorIdentity target,
        int tick,
        out string? operationId)
    {
        Operation? paint = _operations.Values.FirstOrDefault(value =>
            value.Definition is ArcRelaySignatureDefinition.TargetPaint
            && value.OwnerActorId.TeamId == attacker.TeamId
            && value.TargetActorId == target
            && value.EndsAtTick > tick
            && value.RemainingCapacity > 0);
        operationId = paint?.OperationId;
        return paint?.Definition is ArcRelaySignatureDefinition.TargetPaint rule
            ? rule.BonusDamage
            : 0;
    }

    public GenericActorModeEvent? ConsumeTargetPaint(string operationId)
    {
        if (!_operations.TryGetValue(operationId, out Operation? paint))
            return null;
        paint.RemainingCapacity--;
        if (paint.RemainingCapacity > 0)
            return SignatureEvent(paint, "segment-consumed");
        _operations.Remove(operationId);
        return SignatureEvent(paint, "consumed");
    }

    private Operation CreateOperation(
        string operationId,
        ArcRelaySignatureDefinition definition,
        ActorIdentity actor,
        Position source,
        ActorIdentity? targetActor,
        Position? targetPosition,
        ProjectileHeading? heading,
        Direction? direction,
        int tick)
    {
        ArcRelaySignatureState.SignaturePhase phase;
        int? completes = null;
        int? ends = null;
        int capacity = 0;
        ImmutableArray<Position> positions;
        switch (definition)
        {
            case ArcRelaySignatureDefinition.VectorDash value:
                phase = ArcRelaySignatureState.SignaturePhase.Tell;
                completes = checked(tick + value.TellTicks);
                positions = Ray(source, heading!.Value, value.MaxTiles);
                break;
            case ArcRelaySignatureDefinition.PrismWall value:
                phase = ArcRelaySignatureState.SignaturePhase.Active;
                ends = checked(tick + value.DurationTicks);
                capacity = value.ContactCapacity;
                positions = WallShape(source, direction!.Value, value.SegmentCount);
                break;
            case ArcRelaySignatureDefinition.TractorHook value:
                phase = ArcRelaySignatureState.SignaturePhase.Active;
                positions = Ray(source, heading!.Value, value.Range);
                break;
            case ArcRelaySignatureDefinition.TractorHook2 value:
                phase = ArcRelaySignatureState.SignaturePhase.Active;
                positions = Ray(source, heading!.Value, value.Range);
                break;
            case ArcRelaySignatureDefinition.RepairBeam:
                phase = ArcRelaySignatureState.SignaturePhase.Channel;
                positions = Endpoints(source, targetPosition!.Value);
                break;
            case ArcRelaySignatureDefinition.SurveyFlare value:
                phase = ArcRelaySignatureState.SignaturePhase.InFlight;
                completes = checked(tick + Math.Max(
                    1,
                    (source.ChebyshevDistance(targetPosition!.Value)
                     + value.TravelTilesPerTick - 1)
                    / value.TravelTilesPerTick));
                positions = Endpoints(source, targetPosition.Value);
                break;
            case ArcRelaySignatureDefinition.FallingStar value:
                phase = ArcRelaySignatureState.SignaturePhase.Tell;
                completes = checked(tick + value.TellTicks);
                positions = Cross(targetPosition!.Value);
                break;
            case ArcRelaySignatureDefinition.TripNode value:
                phase = ArcRelaySignatureState.SignaturePhase.Active;
                capacity = value.Hull;
                positions = [targetPosition!.Value];
                break;
            case ArcRelaySignatureDefinition.NullField value:
                phase = ArcRelaySignatureState.SignaturePhase.Active;
                ends = checked(tick + value.DurationTicks);
                positions = Radius(source, value.Radius);
                break;
            case ArcRelaySignatureDefinition.NullField2 value:
                phase = ArcRelaySignatureState.SignaturePhase.Tell;
                completes = checked(tick + value.TellTicks);
                positions = Radius(source, value.Radius);
                break;
            case ArcRelaySignatureDefinition.ArcToss value:
                phase = ArcRelaySignatureState.SignaturePhase.Tell;
                completes = checked(tick + value.TellTicks);
                positions = Endpoints(source, targetPosition!.Value);
                break;
            case ArcRelaySignatureDefinition.Exchange value:
                phase = ArcRelaySignatureState.SignaturePhase.Tell;
                completes = checked(tick + value.TellTicks);
                positions = Endpoints(source, targetPosition!.Value);
                break;
            case ArcRelaySignatureDefinition.RailLine value:
                phase = ArcRelaySignatureState.SignaturePhase.Tell;
                completes = checked(tick + value.TellTicks);
                positions = Ray(source, heading!.Value, value.Range);
                break;
            case ArcRelaySignatureDefinition.HardlightBlock value:
                phase = ArcRelaySignatureState.SignaturePhase.Active;
                ends = checked(tick + value.DurationTicks);
                capacity = value.Hull;
                positions = [targetPosition!.Value];
                break;
            case ArcRelaySignatureDefinition.TargetPaint value:
                phase = ArcRelaySignatureState.SignaturePhase.Active;
                ends = checked(tick + value.DurationTicks);
                capacity = value.EnhancedHitCount;
                positions = [targetPosition!.Value];
                break;
            case ArcRelaySignatureDefinition.KineticBurst value:
                phase = ArcRelaySignatureState.SignaturePhase.Tell;
                completes = checked(tick + value.TellTicks);
                positions = Radius(source, 1);
                break;
            case ArcRelaySignatureDefinition.SmokeCanister value:
                phase = ArcRelaySignatureState.SignaturePhase.Active;
                ends = checked(tick + value.DurationTicks);
                positions = Radius(targetPosition!.Value, value.Radius);
                break;
            case ArcRelaySignatureDefinition.SentinelSeed value:
                phase = ArcRelaySignatureState.SignaturePhase.Active;
                ends = checked(tick + value.DurationTicks);
                capacity = value.Hull;
                positions = [targetPosition!.Value];
                break;
            case ArcRelaySignatureDefinition.SentinelSeed2 value:
                phase = ArcRelaySignatureState.SignaturePhase.Active;
                ends = checked(tick + value.DurationTicks);
                capacity = value.Hull;
                positions = [targetPosition!.Value];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(definition));
        }
        return new Operation(
            operationId,
            definition,
            actor,
            source,
            targetActor,
            targetPosition,
            heading,
            phase,
            tick,
            completes,
            ends,
            positions,
            capacity);
    }

    private static ImmutableArray<Position> Endpoints(
        Position source,
        Position target) =>
        source == target ? [source] : [source, target];

    private void AdvanceMaintained(
        Operation operation,
        int tick,
        IReadOnlyDictionary<ActorIdentity, Life> lives,
        ImmutableArray<Effect>.Builder effects,
        ImmutableArray<GenericActorModeEvent>.Builder events)
    {
        if (operation.Definition is ArcRelaySignatureDefinition.RepairBeam beam)
        {
            if (!lives.TryGetValue(operation.OwnerActorId, out Life? owner)
                || operation.TargetActorId is not ActorIdentity targetId
                || !lives.TryGetValue(targetId, out Life? target)
                || owner.Position.ChebyshevDistance(target.Position) > beam.Range
                || !HasUnoccludedLine(
                    owner.Position,
                    target.Position,
                    owner.ActorId.TeamId,
                    tick)
                || target.Health >= target.MaxHealth)
            {
                Complete(operation, tick, "channel-ended", events);
                return;
            }
            int elapsed = tick - operation.StartedTick;
            if (elapsed > 0 && elapsed % beam.TicksPerRepair == 0)
            {
                effects.Add(new Effect.Repair(
                    operation.OperationId,
                    operation.OwnerActorId,
                    targetId,
                    beam.HullPerRepair));
                operation.AppliedAmount += beam.HullPerRepair;
                events.Add(SignatureEvent(operation, "repair-tick"));
                if (operation.AppliedAmount >= beam.MaxHullPerActivation)
                    Complete(operation, tick, "completed", events);
            }
        }
        else if (operation.Definition
                     is ArcRelaySignatureDefinition.SentinelSeed2 turret
                 && operation.Phase
                    == ArcRelaySignatureState.SignaturePhase.Active
                 && !operation.Suppressed
                 && (tick - operation.StartedTick)
                    % turret.FireCooldownTicks == 0)
        {
            // Grammar 2: the turret launches a bolt along an eight-way ray.
            // Only ray-aligned enemies can be engaged, and the bolt itself
            // is dodgeable and construct-blockable like any projectile.
            Position muzzle = operation.Positions[0];
            Life? aligned = lives.Values.Where(candidate =>
                    candidate.ActorId.TeamId
                        != operation.OwnerActorId.TeamId
                    && muzzle.ChebyshevDistance(candidate.Position)
                        is > 0 and var boltDistance
                    && boltDistance <= turret.Range
                    && IsStraight(muzzle, candidate.Position)
                    && HasUnoccludedLine(
                        muzzle,
                        candidate.Position,
                        operation.OwnerActorId.TeamId,
                        tick))
                .OrderBy(value => muzzle.ChebyshevDistance(value.Position))
                .ThenBy(value => TargetTieKey(
                    operation.OwnerActorId.TeamId, value))
                .ThenBy(value => value.ActorId)
                .FirstOrDefault();
            if (aligned is not null)
            {
                effects.Add(new Effect.SentinelBolt(
                    operation.OperationId,
                    operation.OwnerActorId,
                    muzzle,
                    ProjectileHeadingExtensions.Between(
                        muzzle,
                        aligned.Position),
                    turret.Range,
                    turret.Damage,
                    turret.BoltTilesPerAdvance));
            }
        }
        else if (operation.Definition
                     is ArcRelaySignatureDefinition.SentinelSeed sentinel
                 && operation.Phase
                    == ArcRelaySignatureState.SignaturePhase.Active
                 && !operation.Suppressed
                 && (tick - operation.StartedTick)
                    % sentinel.FireCooldownTicks == 0)
        {
            Position origin = operation.Positions[0];
            Life? target = lives.Values.Where(target =>
                    target.ActorId.TeamId
                        != operation.OwnerActorId.TeamId
                    && origin.ChebyshevDistance(target.Position)
                        <= sentinel.Range
                    && HasUnoccludedLine(
                        origin,
                        target.Position,
                        operation.OwnerActorId.TeamId,
                        tick))
                .OrderBy(value => origin.ChebyshevDistance(value.Position))
                .ThenBy(value => TargetTieKey(
                    operation.OwnerActorId.TeamId, value))
                .ThenBy(value => value.ActorId)
                .FirstOrDefault();
            if (target is not null)
            {
                effects.Add(new Effect.SentinelFire(
                    operation.OperationId,
                    operation.OwnerActorId,
                    origin,
                    target.ActorId));
            }
        }
    }

    private void ReplaceOwnedConstruct(
        Operation replacement,
        ImmutableArray<GenericActorModeEvent>.Builder events)
    {
        if (replacement.Definition is not (
                ArcRelaySignatureDefinition.PrismWall
                or ArcRelaySignatureDefinition.TripNode
                or ArcRelaySignatureDefinition.HardlightBlock
                or ArcRelaySignatureDefinition.SentinelSeed
                or ArcRelaySignatureDefinition.SentinelSeed2))
        {
            return;
        }
        foreach (Operation existing in _operations.Values.Where(value =>
                     value.OwnerActorId == replacement.OwnerActorId
                     && value.Definition.Kind == replacement.Definition.Kind)
                 .ToArray())
        {
            Remove(existing, "replaced", events);
        }
    }

    private void Complete(
        Operation operation,
        int tick,
        string reason,
        ImmutableArray<GenericActorModeEvent>.Builder events)
    {
        if (!CooldownStartsAtActivation(operation.Definition))
        {
            _readyAtTick[operation.OwnerActorId] = checked(
                tick + operation.Definition.CooldownTicks);
        }
        Remove(operation, reason, events);
    }

    private void Remove(
        Operation operation,
        string reason,
        ImmutableArray<GenericActorModeEvent>.Builder events)
    {
        _operations.Remove(operation.OperationId);
        events.Add(new GenericActorModeEvent(
            new GenericActorRuntimeObservation.EventPayload.ArcRelay(
                new ArcRelayEvent.SignatureChanged(
                    operation.OperationId,
                    operation.Definition.SignatureId,
                    operation.OwnerActorId,
                    Phase: null,
                    reason)),
            SpatialPosition: null));
    }

    private static bool CooldownStartsAtActivation(
        ArcRelaySignatureDefinition definition) =>
        definition is ArcRelaySignatureDefinition.PrismWall
            or ArcRelaySignatureDefinition.TractorHook
            or ArcRelaySignatureDefinition.TractorHook2
            or ArcRelaySignatureDefinition.SurveyFlare
            or ArcRelaySignatureDefinition.FallingStar
            or ArcRelaySignatureDefinition.TripNode
            or ArcRelaySignatureDefinition.NullField
            or ArcRelaySignatureDefinition.NullField2
            or ArcRelaySignatureDefinition.HardlightBlock
            or ArcRelaySignatureDefinition.TargetPaint
            or ArcRelaySignatureDefinition.SmokeCanister
            or ArcRelaySignatureDefinition.SentinelSeed
            or ArcRelaySignatureDefinition.SentinelSeed2;

    private GenericActorModeEvent SignatureEvent(
        Operation operation,
        string reason) =>
        new(
            new GenericActorRuntimeObservation.EventPayload.ArcRelay(
                new ArcRelayEvent.SignatureChanged(
                    operation.OperationId,
                    operation.Definition.SignatureId,
                    operation.OwnerActorId,
                    operation.Phase,
                    reason)),
            SpatialPosition: null);

    private bool SuppressedByHostileNullField(
        int teamId,
        Position position,
        int tick,
        string? excludedOperationId = null) =>
        _operations.Values.Any(value =>
            !string.Equals(
                value.OperationId,
                excludedOperationId,
                StringComparison.Ordinal)
            &&
            value.Definition is ArcRelaySignatureDefinition.NullField
                or ArcRelaySignatureDefinition.NullField2
            && value.OwnerActorId.TeamId != teamId
            && value.EndsAtTick > tick
            && value.Positions.Contains(position));

    private bool SuppressedByHostileNullField(
        int teamId,
        IEnumerable<Position> positions,
        int tick,
        string? excludedOperationId = null) =>
        positions.Any(position => SuppressedByHostileNullField(
            teamId,
            position,
            tick,
            excludedOperationId));

    private bool HasUnoccludedLine(
        Position origin,
        Position target,
        int teamId,
        int tick)
    {
        int distance = origin.ChebyshevDistance(target);
        foreach (Position position in Visibility.SupercoverLine(origin, target))
        {
            if (position == origin || position == target)
                continue;
            if (_map.IsWall(position))
                return false;
        }
        if (distance <= 1)
            return true;
        foreach (Position position in Visibility.SupercoverLine(origin, target))
        {
            if (position == origin)
                continue;
            if (IsSmokeAt(position, tick)
                && !IsRevealedForTeam(position, teamId, tick))
            {
                return false;
            }
        }
        return true;
    }

    private Position ClipStraightLanding(Position source, Position requested)
    {
        ProjectileHeading heading = ProjectileHeadingExtensions.Between(
            source,
            requested);
        var (dx, dy) = heading.Vector();
        Position current = source;
        int distance = source.ChebyshevDistance(requested);
        for (int step = 0; step < distance; step++)
        {
            Position next = current.Offset(dx, dy);
            if (_map.IsWall(next))
                break;
            current = next;
        }
        return current;
    }

    private IEnumerable<Position> PlacementTargets(
        Position source,
        IReadOnlyCollection<Life> lives,
        bool forbidTaggedTile)
    {
        HashSet<Position> occupied = lives.Select(value => value.Position)
            .ToHashSet();
        HashSet<Position> forbidden = forbidTaggedTile
            ? _map.TileTags.Where(value => value.Kind
                    == ActorMapTileTagDefinition.TileTagKind
                        .SignaturePlacementForbidden)
                .SelectMany(value => value.Tiles).ToHashSet()
            : [];
        return AllFloorTiles().Where(target =>
            source.ChebyshevDistance(target) == 1
            && !occupied.Contains(target)
            && !forbidden.Contains(target));
    }

    private IEnumerable<Position> AllFloorTiles()
    {
        for (int y = 0; y < _map.Height; y++)
        for (int x = 0; x < _map.Width; x++)
        {
            var position = new Position(x, y);
            if (!_map.IsWall(position))
                yield return position;
        }
    }

    private ImmutableArray<Position> Radius(Position centre, int radius) =>
        AllFloorTiles().Where(value =>
                centre.ChebyshevDistance(value) <= radius)
            .OrderBy(value => value.Y).ThenBy(value => value.X)
            .ToImmutableArray();

    private ImmutableArray<Position> Ray(
        Position source,
        ProjectileHeading heading,
        int range)
    {
        var result = ImmutableArray.CreateBuilder<Position>();
        var (dx, dy) = heading.Vector();
        for (int step = 1; step <= range; step++)
        {
            Position position = source.Offset(dx * step, dy * step);
            if (_map.IsWall(position))
                break;
            result.Add(position);
        }
        return result.Count == 0 ? [source] : result.ToImmutable();
    }

    private static ImmutableArray<Position> Cross(Position centre) =>
        [
            centre,
            centre.Offset(0, -1),
            centre.Offset(1, 0),
            centre.Offset(0, 1),
            centre.Offset(-1, 0),
        ];

    private ImmutableArray<Position> WallShape(
        Position source,
        Direction direction,
        int segments)
    {
        var (dx, dy) = direction.ToProjectileHeading().Vector();
        var (rx, ry) = (-dy, dx);
        Position centre = source.Offset(dx, dy);
        int offset = segments / 2;
        Position[] shape = Enumerable.Range(0, segments)
            .Select(index => centre.Offset(
                (index - offset) * rx,
                (index - offset) * ry))
            .Where(value => !_map.IsWall(value))
            .ToArray();
        return shape.Length == 0 ? [source] : [.. shape];
    }

    private static bool IsStraight(Position from, Position to)
    {
        int dx = Math.Abs(to.X - from.X);
        int dy = Math.Abs(to.Y - from.Y);
        return dx == 0 || dy == 0 || dx == dy;
    }

    internal sealed record Life(
        ActorIdentity ActorId,
        Position Position,
        int Health,
        int MaxHealth);

    internal sealed record TickResult(
        ImmutableArray<GenericActorModeEvent> Events,
        ImmutableArray<Effect> Effects);

    internal abstract record Effect(string OperationId, ActorIdentity Owner)
    {
        internal sealed record VectorDash(
            string Id,
            ActorIdentity Actor,
            ProjectileHeading Heading) : Effect(Id, Actor);
        internal sealed record TractorHook(
            string Id,
            ActorIdentity Actor,
            ProjectileHeading Heading) : Effect(Id, Actor);
        internal sealed record Repair(
            string Id,
            ActorIdentity Actor,
            ActorIdentity Target,
            int Amount) : Effect(Id, Actor);
        internal sealed record FallingStar(
            string Id,
            ActorIdentity Actor,
            Position Target) : Effect(Id, Actor);
        internal sealed record TripNode(
            string Id,
            ActorIdentity Actor,
            ActorIdentity Target,
            int Damage) : Effect(Id, Actor);
        internal sealed record ArcTossLaunch(
            string Id,
            ActorIdentity Actor,
            Position Target,
            int CompletesAtTick) : Effect(Id, Actor);
        internal sealed record ArcTossLand(
            string Id,
            ActorIdentity Actor,
            Position Target) : Effect(Id, Actor);
        internal sealed record Exchange(
            string Id,
            ActorIdentity Actor,
            ActorIdentity Target,
            Position SourceStart,
            Position TargetStart) : Effect(Id, Actor);
        internal sealed record RailLine(
            string Id,
            ActorIdentity Actor,
            ProjectileHeading Heading) : Effect(Id, Actor);
        internal sealed record KineticBurst(
            string Id,
            ActorIdentity Actor,
            Position Origin) : Effect(Id, Actor);
        internal sealed record SentinelFire(
            string Id,
            ActorIdentity Actor,
            Position Origin,
            ActorIdentity Target) : Effect(Id, Actor);
        internal sealed record SentinelBolt(
            string Id,
            ActorIdentity Actor,
            Position Origin,
            ProjectileHeading Heading,
            int Range,
            int Damage,
            int TilesPerAdvance) : Effect(Id, Actor);
        internal sealed record HookBolt(
            string Id,
            ActorIdentity Actor,
            Position Origin,
            ProjectileHeading Heading,
            int Range,
            int MaxPullTiles,
            int TilesPerAdvance) : Effect(Id, Actor);
    }

    private sealed class Operation
    {
        public Operation(
            string operationId,
            ArcRelaySignatureDefinition definition,
            ActorIdentity ownerActorId,
            Position sourcePosition,
            ActorIdentity? targetActorId,
            Position? targetPosition,
            ProjectileHeading? heading,
            ArcRelaySignatureState.SignaturePhase phase,
            int startedTick,
            int? completesAtTick,
            int? endsAtTick,
            ImmutableArray<Position> positions,
            int remainingCapacity)
        {
            OperationId = operationId;
            Definition = definition;
            OwnerActorId = ownerActorId;
            SourcePosition = sourcePosition;
            TargetActorId = targetActorId;
            TargetPosition = targetPosition;
            Heading = heading;
            Phase = phase;
            StartedTick = startedTick;
            CompletesAtTick = completesAtTick;
            EndsAtTick = endsAtTick;
            Positions = positions;
            RemainingCapacity = remainingCapacity;
        }
        public string OperationId { get; }
        public ArcRelaySignatureDefinition Definition { get; }
        public ActorIdentity OwnerActorId { get; }
        public Position SourcePosition { get; }
        public ActorIdentity? TargetActorId { get; }
        public Position? TargetPosition { get; }
        public ProjectileHeading? Heading { get; }
        public ArcRelaySignatureState.SignaturePhase Phase { get; set; }
        public int StartedTick { get; }
        public int? CompletesAtTick { get; set; }
        public int? EndsAtTick { get; set; }
        public ImmutableArray<Position> Positions { get; set; }
        public int RemainingCapacity { get; set; }
        public int AppliedAmount { get; set; }
        public bool Suppressed { get; set; }
    }
}
