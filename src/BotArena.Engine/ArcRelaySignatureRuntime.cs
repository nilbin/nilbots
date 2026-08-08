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
    /// The bolt-class LINE signatures that declare a LOCK on this ruleset —
    /// keyed by action id, presence-driven off the action's declared
    /// UnitTarget parameter (owner ruling 2026-08-09). Empty on every ruleset
    /// without the strike lock, which is what keeps their bytes and their
    /// behaviour exactly where they were.
    /// </summary>
    private readonly ImmutableHashSet<string> _lockDeclaringActions;

    /// <summary>
    /// The opposing unit slots per team, in canonical order — the same mask a
    /// windup gun publishes for its named target (DECISIONS #222).
    /// </summary>
    private readonly ImmutableDictionary<int,
        ImmutableArray<GenericActorRuntimeActionArgument.UnitTarget>>
        _opposingSlotsByTeam;

    private readonly bool _diagonalCornersMustBeClear;

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
        _lockDeclaringActions = definition.Rules.Actions
            .Where(action => action.Kind == ActorActionKind.Signature
                && action.ParameterKinds.Contains(
                    ActorActionParameterKind.UnitTarget)
                && _byAction.TryGetValue(
                    action.Id,
                    out ArcRelaySignatureDefinition? signature)
                && IsLineAttack(signature))
            .Select(action => action.Id)
            .ToImmutableHashSet(StringComparer.Ordinal);
        _opposingSlotsByTeam = definition.Topology.UnitSlots
            .Select(slot => slot.TeamId)
            .Distinct()
            .ToImmutableDictionary(
                teamId => teamId,
                teamId => definition.Topology.UnitSlots
                    .Where(slot => slot.TeamId != teamId)
                    .OrderBy(slot => slot.TeamId)
                    .ThenBy(slot => slot.UnitId)
                    .Select(slot => new GenericActorRuntimeActionArgument
                        .UnitTarget(slot.TeamId, slot.UnitId))
                    .ToImmutableArray());
        // The strike line's corner rule is a projectile fact, and the class
        // guns all author it the same way; a locked beam traces the same
        // geometry so a lit tile stays exactly a hittable tile.
        _diagonalCornersMustBeClear = definition.Rules.AttackProfiles
            .All(profile => profile.Projectile.DiagonalCornersMustBeClear);
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

    /// <summary>
    /// Whether this signature declares a LOCK on this ruleset: a bolt-class
    /// line attack whose action carries the optional UnitTarget parameter.
    /// </summary>
    public bool DeclaresLock(string actionId) =>
        _lockDeclaringActions.Contains(actionId);

    /// <summary>
    /// The signatures the lock ruling covers: LINE attacks that damage or
    /// displace, aimed down a heading. The sentinel plants a turret on a
    /// named TILE — it is a placement, not a line attack — so it keeps the
    /// plain telegraph of DECISIONS #226 and locks nothing.
    /// </summary>
    internal static bool IsLineAttack(ArcRelaySignatureDefinition definition) =>
        definition is ArcRelaySignatureDefinition.RailLine
            or ArcRelaySignatureDefinition.TractorHook2;

    public ImmutableArray<GenericActorRuntimeActionArgument.UnitTarget>
        UnitTargets(
            ActorIdentity actor,
            Position source,
            IReadOnlySet<Position> visibleTiles,
            IReadOnlyCollection<Life> lives)
    {
        ArcRelaySignatureDefinition signature = DefinitionFor(actor);
        // A locked line attack names the body it is for, and only an enemy
        // can be locked, so the mask is the opposing slots — the exact mask a
        // windup gun publishes (DECISIONS #222). It is deliberately not
        // narrowed to what is alive or visible: the declare is a decision the
        // mind is allowed to get wrong, and naming nobody lockable is the
        // theatrical whiff.
        if (_lockDeclaringActions.Contains(signature.ActionId))
            return _opposingSlotsByTeam[actor.TeamId];
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
        bool declaresLock = _lockDeclaringActions.Contains(actionId);
        string operationId = $"arc-signature-{_nextOperationOrdinal++}";
        // A named unit that is dead or absent is a legal declare with no
        // lock, so the lookup must tolerate it. Every other signature's mask
        // is built from live bodies, so its Single() never fired anyway.
        ActorIdentity? targetActor = arguments
            .OfType<GenericActorRuntimeActionArgument.UnitTargetArgument>()
            .Select(value => lives.FirstOrDefault(life =>
                life.ActorId.TeamId == value.Value.TeamId
                && life.ActorId.UnitId == value.Value.UnitId)?.ActorId)
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

        // The locked line attack (owner ruling 2026-08-09): the declare
        // freezes the 90-degree wedge of its declared heading exactly as a
        // strike does, and the LOCK is the body the mind named — nothing
        // else, and only when that body is an enemy standing inside the
        // frozen wedge at declare. Naming nobody, naming a friendly, naming
        // the dead, or naming a body outside the wedge locks NOTHING and
        // keeps the theatrical whiff down the declared heading.
        ImmutableArray<Position> coneTiles = [];
        if (declaresLock)
        {
            coneTiles = GenericActorStrikeCone.Tiles(
                _map,
                source,
                heading!.Value,
                LineAttackRange(definition),
                _diagonalCornersMustBeClear);
            Life? named = targetActor is ActorIdentity identity
                ? lives.FirstOrDefault(life => life.ActorId == identity)
                : null;
            targetActor = named is not null
                && named.ActorId.TeamId != actor.TeamId
                && coneTiles.Contains(named.Position)
                    ? named.ActorId
                    : null;
            targetPosition = targetActor is null ? null : named!.Position;
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
            tick,
            declaresLock,
            coneTiles);
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
        // A TELEGRAPHING declare has not happened yet: it rides the wire in
        // Tell for its windup, and nothing leaves the muzzle until it
        // matures. Only an instant cast resolves here. (The guard is what
        // makes DECISIONS #226's windup real for the hook and the sentinel:
        // both used to run their instant branch on the declare tick and
        // spend the telegraph they had just published.)
        else if (definition is ArcRelaySignatureDefinition.TractorHook2 hook2
                 && operation.Phase
                    != ArcRelaySignatureState.SignaturePhase.Tell)
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
        else if (operation.Phase
                     != ArcRelaySignatureState.SignaturePhase.Tell
                 && definition is ArcRelaySignatureDefinition.PrismWall
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
            // A LOCKED line attack matures where a declared strike matures —
            // in the attack phase, after this tick's movement — so its
            // maturity is owned by ResolveLockedLineStrikes and never by this
            // tick-start pass. What happens here is the FOLLOW: the published
            // line re-aims at the lock's current tile every tick, so the tiles
            // on the wire stay exactly the tiles that would resolve now.
            if (operation.DeclaresLock
                && operation.Phase
                    == ArcRelaySignatureState.SignaturePhase.Tell)
            {
                TrackLockedLine(operation, world);
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
                case ArcRelaySignatureDefinition.TractorHook2 hook
                    when operation.Phase
                        == ArcRelaySignatureState.SignaturePhase.Tell:
                    // The windup matured: the grapple bolt leaves down the
                    // line frozen at declare, and everything downstream is
                    // the historical instant cast. (A LOCKED hook never
                    // reaches here — it matures with the strikes.)
                    effects.Add(new Effect.HookBolt(
                        operation.OperationId,
                        operation.OwnerActorId,
                        operation.SourcePosition,
                        operation.Heading!.Value,
                        hook.Range,
                        hook.MaxPullTiles,
                        hook.BoltTilesPerAdvance));
                    Complete(operation, tick, "launched", events);
                    break;
                case ArcRelaySignatureDefinition.SentinelSeed2 seed2
                    when operation.Phase
                        == ArcRelaySignatureState.SignaturePhase.Tell:
                    operation.Phase = ArcRelaySignatureState.SignaturePhase
                        .Active;
                    operation.CompletesAtTick = null;
                    operation.EndsAtTick = checked(
                        tick + seed2.DurationTicks);
                    events.Add(SignatureEvent(operation, "deployed"));
                    break;
                case ArcRelaySignatureDefinition.RailLine:
                    effects.Add(new Effect.RailLine(
                        operation.OperationId,
                        operation.OwnerActorId,
                        operation.Heading!.Value,
                        []));
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

    /// <summary>
    /// Matures every locked line attack whose windup ends this tick, in the
    /// declared-strike phase and under the declared-strike rules (owner
    /// ruling 2026-08-09: "same lock on mechanism for some signatures with
    /// the windup etc similarly to regular striking").
    /// </summary>
    /// <remarks>
    /// The four cancels are the strike's, and all four are LOCK-side: the
    /// lock died, it left the frozen wedge, the shooter's TEAM lost sight of
    /// it, or a wall stands on the shooter-to-target ray. The declarer cannot
    /// drift out of its own beam, because commanding a move abandons the
    /// declare outright (DECISIONS #221). A declare that locked nothing keeps
    /// the theatrical whiff down its declared heading.
    /// <para>
    /// ONLY the delivery differs from a strike, and each signature keeps its
    /// own: rail fires the piercing beam down the line to the lock — every
    /// body on it takes the damage, so interposition SHARES the beam rather
    /// than stopping it — and the hook throws its grapple bolt down the
    /// eight-way heading that line starts on, which still catches the first
    /// body it meets.
    /// </para>
    /// </remarks>
    public TickResult ResolveLockedLineStrikes(
        int tick,
        IReadOnlyCollection<Life> lives,
        Func<int, Position, Position, bool> lockStaysTrackable)
    {
        ArgumentNullException.ThrowIfNull(lockStaysTrackable);
        var events = ImmutableArray.CreateBuilder<GenericActorModeEvent>();
        var effects = ImmutableArray.CreateBuilder<Effect>();
        Dictionary<ActorIdentity, Life> world = lives.ToDictionary(
            value => value.ActorId);
        foreach (Operation operation in _operations.Values
                     .Where(value => value.DeclaresLock
                         && value.Phase
                             == ArcRelaySignatureState.SignaturePhase.Tell
                         && value.CompletesAtTick <= tick)
                     .OrderBy(value => value.OwnerActorId)
                     .ThenBy(value => value.OperationId, StringComparer.Ordinal)
                     .ToArray())
        {
            if (!world.ContainsKey(operation.OwnerActorId))
            {
                Complete(operation, tick, "owner-destroyed", events);
                continue;
            }
            Position? aim = operation.TargetActorId is ActorIdentity locked
                ? world.TryGetValue(locked, out Life? target)
                    && operation.ConeTiles.Contains(target.Position)
                    && lockStaysTrackable(
                        operation.OwnerActorId.TeamId,
                        operation.SourcePosition,
                        target.Position)
                        ? target.Position
                        : null
                : LineAttackWhiff(operation);
            if (aim is null)
            {
                Complete(operation, tick, "lock-lost", events);
                continue;
            }
            ImmutableArray<Position> path = GenericActorStrikeCone.LineTo(
                _map,
                operation.SourcePosition,
                aim.Value,
                _diagonalCornersMustBeClear);
            if (path.IsEmpty)
            {
                Complete(operation, tick, "lock-lost", events);
                continue;
            }
            operation.Positions = path;
            switch (operation.Definition)
            {
                case ArcRelaySignatureDefinition.RailLine:
                    effects.Add(new Effect.RailLine(
                        operation.OperationId,
                        operation.OwnerActorId,
                        ProjectileHeadingExtensions.Between(
                            operation.SourcePosition,
                            path[0]),
                        path));
                    Complete(operation, tick, "completed", events);
                    break;
                case ArcRelaySignatureDefinition.TractorHook2 hook:
                    effects.Add(new Effect.HookBolt(
                        operation.OperationId,
                        operation.OwnerActorId,
                        operation.SourcePosition,
                        ProjectileHeadingExtensions.Between(
                            operation.SourcePosition,
                            path[0]),
                        hook.Range,
                        hook.MaxPullTiles,
                        hook.BoltTilesPerAdvance));
                    Complete(operation, tick, "launched", events);
                    break;
                default:
                    Complete(operation, tick, "lock-lost", events);
                    break;
            }
        }
        return new TickResult(events.ToImmutable(), effects.ToImmutable());
    }

    /// <summary>
    /// Every locked line attack still winding up, in the declared-strike wire
    /// shape: the frozen apex, the declared heading, the frozen wedge and the
    /// locked body. The viewer's tracking ray reads exactly this, so a
    /// winding-up beam draws the same sentence a winding-up gun does.
    /// </summary>
    public ImmutableArray<PendingLineStrike> PendingLineStrikes() =>
        [.. _operations.Values
            .Where(value => value.DeclaresLock
                && value.Phase == ArcRelaySignatureState.SignaturePhase.Tell)
            .OrderBy(value => value.OwnerActorId)
            .ThenBy(value => value.OperationId, StringComparer.Ordinal)
            .Select(value => new PendingLineStrike(
                value.OwnerActorId,
                value.CompletesAtTick!.Value,
                value.SourcePosition,
                value.Heading!.Value,
                value.TargetActorId,
                value.ConeTiles))];

    /// <summary>
    /// The follow: a locked line re-publishes itself at the lock's current
    /// tile every tick of the windup, so the announced tiles are the tiles
    /// that would resolve now. A lock that has left the wedge or died stops
    /// being tracked and the line falls back to its declared heading.
    /// </summary>
    private void TrackLockedLine(
        Operation operation,
        IReadOnlyDictionary<ActorIdentity, Life> world)
    {
        Position? aim = operation.TargetActorId is ActorIdentity locked
            && world.TryGetValue(locked, out Life? target)
            && operation.ConeTiles.Contains(target.Position)
                ? target.Position
                : LineAttackWhiff(operation);
        if (aim is null)
            return;
        ImmutableArray<Position> path = GenericActorStrikeCone.LineTo(
            _map,
            operation.SourcePosition,
            aim.Value,
            _diagonalCornersMustBeClear);
        if (!path.IsEmpty)
            operation.Positions = path;
    }

    /// <summary>
    /// Where a declare that locked nothing points: the far end of its own
    /// declared heading, so the whiff is theatre down the announced lane.
    /// </summary>
    private Position? LineAttackWhiff(Operation operation)
    {
        if (operation.TargetActorId is not null)
            return null;
        ImmutableArray<Position> ray = Ray(
            operation.SourcePosition,
            operation.Heading!.Value,
            LineAttackRange(operation.Definition));
        return ray.IsEmpty || ray[^1] == operation.SourcePosition
            ? null
            : ray[^1];
    }

    private static int LineAttackRange(
        ArcRelaySignatureDefinition definition) =>
        definition switch
        {
            ArcRelaySignatureDefinition.RailLine rail => rail.Range,
            ArcRelaySignatureDefinition.TractorHook2 hook => hook.Range,
            _ => throw new ArgumentOutOfRangeException(nameof(definition)),
        };

    private static int LineAttackWindup(
        ArcRelaySignatureDefinition definition) =>
        definition switch
        {
            ArcRelaySignatureDefinition.RailLine rail => rail.WindupTicks,
            ArcRelaySignatureDefinition.TractorHook2 hook => hook.WindupTicks,
            _ => throw new ArgumentOutOfRangeException(nameof(definition)),
        };

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

    /// <summary>
    /// The rooted windup, generalized to declared line attacks (owner ruling
    /// 2026-08-08, extending DECISIONS #221): a body that COMMANDS a move
    /// while one of its bolt-class signatures is winding up abandons that
    /// declare outright. The frozen line is a promise the declarer stands
    /// behind; walking away from it is the one voluntary way out, and the
    /// mind spends it through the disengage latch. Utility signatures are
    /// untouched — nothing about smoke asks its caster to hold still.
    /// </summary>
    public void AbandonWindupsOnMove(
        int tick,
        ActorIdentity actor,
        ImmutableArray<GenericActorModeEvent>.Builder events)
    {
        foreach (Operation operation in _operations.Values
                     .Where(value => value.OwnerActorId == actor
                         && value.Phase
                             == ArcRelaySignatureState.SignaturePhase.Tell
                         && IsBoltClass(value.Definition))
                     .OrderBy(value => value.OperationId, StringComparer.Ordinal)
                     .ToArray())
        {
            Remove(operation, "abandoned-move", events);
        }
    }

    /// <summary>
    /// The signatures the owner's telegraph ruling covers: line attacks that
    /// damage or displace, plus the seed that plants one. Everything else is
    /// utility and stays instant and untelegraphed.
    /// </summary>
    internal static bool IsBoltClass(ArcRelaySignatureDefinition definition) =>
        definition is ArcRelaySignatureDefinition.RailLine
            or ArcRelaySignatureDefinition.TractorHook2
            or ArcRelaySignatureDefinition.SentinelSeed2;

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
        int tick,
        bool declaresLock,
        ImmutableArray<Position> coneTiles)
    {
        ArcRelaySignatureState.SignaturePhase phase;
        int? completes = null;
        int? ends = null;
        int capacity = 0;
        ImmutableArray<Position> positions;
        // A locked line attack publishes the LINE it would resolve on — to
        // its lock when it has one, down its declared heading when it does
        // not. The frozen wedge is the reach fact and rides the pending-strike
        // wire beside it; painting the whole 90 degrees as ground is the
        // blinking apology DECISIONS #220 removed.
        if (declaresLock)
        {
            Position aim = targetPosition
                ?? Ray(source, heading!.Value, LineAttackRange(definition))[^1];
            ImmutableArray<Position> line = GenericActorStrikeCone.LineTo(
                _map,
                source,
                aim,
                _diagonalCornersMustBeClear);
            return new Operation(
                operationId,
                definition,
                actor,
                source,
                targetActor,
                targetPosition,
                heading,
                ArcRelaySignatureState.SignaturePhase.Tell,
                tick,
                checked(tick + LineAttackWindup(definition)),
                endsAtTick: null,
                line.IsEmpty ? [source] : line,
                remainingCapacity: 0)
            {
                DeclaresLock = true,
                ConeTiles = coneTiles,
            };
        }
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
                // A declared grapple freezes its LINE and then fires
                // (owner ruling 2026-08-08). No lock and no follow: a line
                // attack is not target-locked, so stepping off the frozen
                // tiles is the whole counterplay and stepping on is the
                // block. Zero windup keeps the historical instant cast.
                phase = value.WindupTicks > 0
                    ? ArcRelaySignatureState.SignaturePhase.Tell
                    : ArcRelaySignatureState.SignaturePhase.Active;
                completes = value.WindupTicks > 0
                    ? checked(tick + value.WindupTicks)
                    : null;
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
                completes = checked(tick + value.WindupTicks);
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
                // The seed announces itself before it lands, on the tile it
                // will occupy, and the declarer is rooted to it meanwhile.
                phase = value.WindupTicks > 0
                    ? ArcRelaySignatureState.SignaturePhase.Tell
                    : ArcRelaySignatureState.SignaturePhase.Active;
                completes = value.WindupTicks > 0
                    ? checked(tick + value.WindupTicks)
                    : null;
                ends = value.WindupTicks > 0
                    ? null
                    : checked(tick + value.DurationTicks);
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

    /// <summary>
    /// One locked line attack in windup, in the declared-strike wire shape.
    /// </summary>
    internal sealed record PendingLineStrike(
        ActorIdentity Shooter,
        int ResolveAtTick,
        Position Origin,
        ProjectileHeading CentralHeading,
        ActorIdentity? Target,
        ImmutableArray<Position> Tiles);

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
        /// <summary>
        /// The beam. <paramref name="Path"/> is the frozen line a LOCKED rail
        /// fires down — the tiles the lock's own position picked at maturity;
        /// it is empty for an unlocked rail, which still walks its declared
        /// heading from wherever its owner stands. Either way the delivery
        /// PIERCES: every body on the line takes the damage.
        /// </summary>
        internal sealed record RailLine(
            string Id,
            ActorIdentity Actor,
            ProjectileHeading Heading,
            ImmutableArray<Position> Path) : Effect(Id, Actor);
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

        /// <summary>
        /// Whether this operation is a LOCKED line attack: it matures in the
        /// declared-strike phase, against the body it named, under the
        /// strike's cancels (owner ruling 2026-08-09).
        /// </summary>
        public bool DeclaresLock { get; init; }

        /// <summary>
        /// The 90-degree wedge frozen at declare — reach, not a zone. The
        /// lock may be followed anywhere inside it and cancels the moment it
        /// steps out.
        /// </summary>
        public ImmutableArray<Position> ConeTiles { get; init; } = [];
    }
}
