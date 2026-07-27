using System.Collections.Immutable;
using System.Globalization;

namespace BotArena.Engine;

/// <summary>
/// Lossless copies from live engine objects into immutable replay-v2 DTOs.
/// Projection happens at the chronology boundary; the codec never reaches
/// back into mutable match state.
/// </summary>
internal static class ReplayV2Projection
{
    public static ReplayV2Header Header(
        ulong seed,
        PublicMatchContractManifest contract,
        string? themeId,
        MapPresentation? presentation,
        IEnumerable<ActorParticipantConfiguration> participants)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(participants);

        return new ReplayV2Header(
            BotArenaVersions.EntityReplayFormatVersion,
            BotArenaVersions.EngineVersion,
            contract.Rules.RulesetId,
            new ReplayV2ActorRuntimeContract(
                "nilbots-actor",
                BotArenaVersions.ActorRuntimeProtocolVersion,
                BotArenaVersions.ActorRuntimeConfigurationVersion,
                BotArenaVersions.ActorRuntimeContractVersion,
                BotArenaVersions.ActorMatchStartSchemaVersion,
                BotArenaVersions.ActorObservationSchemaVersion,
                BotArenaVersions.ActorDecisionSchemaVersion),
            seed.ToString(CultureInfo.InvariantCulture),
            contract,
            Presentation(themeId, presentation),
            participants
                .Select(Participant)
                .OrderBy(participant => participant.ParticipantId)
                .ToImmutableArray());
    }

    public static ReplayV2ObservationAliases ObservationAliases(
        ActorObservationReplayAliases aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);

        return new ReplayV2ObservationAliases(
            aliases.EnemyLives
                .OrderBy(value => ReplayV2AliasHandles.ParseOrdinal(
                    value.LifeHandle,
                    ReplayV2AliasHandles.EnemyLifePrefix))
                .Select(value => new ReplayV2EnemyLifeAlias(
                    value.LifeHandle,
                    ActorId(value.ActorId)))
                .ToImmutableArray(),
            aliases.Projectiles
                .OrderBy(value => ReplayV2AliasHandles.ParseOrdinal(
                    value.ProjectileHandle,
                    ReplayV2AliasHandles.ProjectilePrefix))
                .Select(value => new ReplayV2ProjectileAlias(
                    value.ProjectileHandle,
                    WireId(value.ProjectileId)))
                .ToImmutableArray(),
            aliases.Events
                .OrderBy(value => ReplayV2AliasHandles.ParseOrdinal(
                    value.EventHandle,
                    ReplayV2AliasHandles.EventPrefix))
                .Select(value => new ReplayV2EventAlias(
                    value.EventHandle,
                    value.EventId))
                .ToImmutableArray());
    }

    public static ReplayV2ParticipantController Participant(
        ActorParticipantConfiguration participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        return new ReplayV2ParticipantController(
            participant.ParticipantId,
            participant.TeamId,
            participant.Name,
            participant.RuntimeKind,
            participant.ArtifactHash,
            participant.Accent,
            participant.LookId,
            participant.ProjectileLookId);
    }

    public static ReplayV2Presentation? Presentation(
        string? themeId,
        MapPresentation? presentation)
    {
        if (themeId is null && presentation is null)
            return null;

        ReplayV2MapPresentation? map = presentation is null
            ? null
            : new ReplayV2MapPresentation(
                presentation.BoundaryWall,
                presentation.InteriorWall,
                presentation.WallGroups
                    .Select(group => new ReplayV2WallGroup(
                        group.Family,
                        group.Tiles
                            .OrderBy(tile => tile.Y)
                            .ThenBy(tile => tile.X)
                            .ToImmutableArray()))
                    .OrderBy(group => group.Family, StringComparer.Ordinal)
                    .ToImmutableArray());
        return new ReplayV2Presentation(themeId, map);
    }

    public static ReplayV2ActorObservation Observation(
        ActorObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        return new ReplayV2ActorObservation(
            observation.SchemaVersion,
            observation.Tick,
            observation.MatchContractFingerprint,
            observation.TeamPerception,
            new ReplayV2ObservedSelf(
                ActorId(observation.Self.ActorId),
                observation.Self.FormId,
                observation.Self.Position,
                observation.Self.Facing,
                observation.Self.Health,
                observation.Self.Cooldown,
                observation.Self.Energy,
                observation.Self.PreviousActionResult)
            {
                PendingFormTransition =
                    FormTransition(
                        observation.Self.PendingFormTransition),
            },
            observation.TeamUnits
                .OrderBy(unit => unit.TeamId)
                .ThenBy(unit => unit.UnitId)
                .Select(unit => new ReplayV2ObservedUnitSlot(
                    unit.TeamId,
                    unit.UnitId,
                    unit.FormId,
                    unit.LifecycleStatus,
                    unit.ActiveActorId is null
                        ? null
                        : ActorId(unit.ActiveActorId),
                    unit.RespawnAtTick,
                    unit.UnlockAtTick,
                    unit.RebuildReadyAtTick,
                    unit.FabricationAtTick))
                .ToImmutableArray(),
            observation.Allies
                .OrderBy(ally => ally.ActorId)
                .Select(ally => new ReplayV2ObservedAlly(
                    ActorId(ally.ActorId),
                    ally.FormId,
                    ally.Position,
                    ally.Facing,
                    ally.Health,
                    ally.Cooldown,
                    ally.Energy,
                    ally.PreviousActionResult)
                {
                    PendingFormTransition =
                        FormTransition(ally.PendingFormTransition),
                })
                .ToImmutableArray(),
            observation.Enemies
                .OrderBy(enemy => enemy.Actor.TeamId)
                .ThenBy(enemy => enemy.Actor.UnitId)
                .ThenBy(enemy => ReplayV2AliasHandles.ParseOrdinal(
                    enemy.Actor.LifeHandle,
                    ReplayV2AliasHandles.EnemyLifePrefix))
                .Select(enemy => new ReplayV2ObservedEnemy(
                    EnemyActorRef(enemy.Actor),
                    enemy.FormId,
                    enemy.Position,
                    enemy.Facing,
                    enemy.Health,
                    ActorIds(enemy.ObservedBy))
                {
                    PendingFormTransition =
                        FormTransition(enemy.PendingFormTransition),
                })
                .ToImmutableArray(),
            observation.VisibleTiles
                .OrderBy(tile => tile.Position.Y)
                .ThenBy(tile => tile.Position.X)
                .Select(tile => new ReplayV2ObservedMapTile(
                    tile.Position,
                    tile.IsWall,
                    ActorIds(tile.ObservedBy)))
                .ToImmutableArray(),
            observation.VisibleProjectiles is { } projectiles
                ? projectiles
                    .OrderBy(projectile =>
                        ReplayV2AliasHandles.ParseOrdinal(
                            projectile.ProjectileHandle,
                            ReplayV2AliasHandles.ProjectilePrefix))
                    .ThenBy(projectile => projectile.OwnerTeamId)
                    .Select(projectile => new ReplayV2ObservedProjectile(
                        projectile.ProjectileHandle,
                        projectile.OwnerTeamId,
                        projectile.AlliedOwnerActorId is null
                            ? null
                            : ActorId(projectile.AlliedOwnerActorId),
                        projectile.VisibleEnemyOwner is null
                            ? null
                            : EnemyActorRef(
                                projectile.VisibleEnemyOwner),
                        projectile.Position,
                        projectile.Heading,
                        projectile.TilesPerAdvance,
                        projectile.TicksUntilAdvance,
                        projectile.RemainingTiles,
                        ActorIds(projectile.ObservedBy)))
                    .ToImmutableArray()
                : null,
            observation.VisibleEvents
                .OrderBy(value => value.SourceTick)
                .ThenBy(value => ReplayV2AliasHandles.ParseOrdinal(
                    value.EventHandle,
                    ReplayV2AliasHandles.EventPrefix))
                .Select(value => new ReplayV2ObservedEvent(
                    value.EventHandle,
                    value.SourceTick,
                    value.Type,
                    value.TeamId,
                    value.AlliedActorId is null
                        ? null
                        : ActorId(value.AlliedActorId),
                    value.EnemyActor is null
                        ? null
                        : EnemyActorRef(value.EnemyActor),
                    value.ProjectileHandle,
                    value.Position,
                    value.Facing,
                    value.Amount,
                    value.NewHealth,
                    ActorIds(value.ObservedBy))
                {
                    ProjectileHeading = value.ProjectileHeading,
                    FromFormId = value.FromFormId,
                    ToFormId = value.ToFormId,
                    FormTransitionStartedAtTick =
                        value.FormTransitionStartedAtTick,
                    FormTransitionCompletesAtTick =
                        value.FormTransitionCompletesAtTick,
                    ActionId = value.ActionId,
                    ActionCode = value.ActionCode,
                    FormTargetId = value.FormTargetId,
                    ActionResult = value.ActionResult,
                })
                .ToImmutableArray(),
            observation.HeardSounds is { } sounds
                ? sounds
                    .OrderBy(sound => sound.SourceTick)
                    .ThenBy(sound => ReplayV2AliasHandles.ParseOrdinal(
                        sound.EventHandle,
                        ReplayV2AliasHandles.EventPrefix))
                    .ThenBy(sound => sound.ObserverActorId)
                    .Select(sound => new ReplayV2ObservedSound(
                        sound.EventHandle,
                        sound.SourceTick,
                        ActorId(sound.ObserverActorId),
                        sound.Type,
                        sound.Bearing,
                        sound.Distance))
                    .ToImmutableArray()
                : null,
            observation.FrontlineObjective is { } objective
                ? new ReplayV2ObservedFrontlineObjective(
                    objective.ActivePositionIndex,
                    objective.ClaimingTeamId,
                    objective.CaptureProgress,
                    objective.DecayTicksElapsed,
                    objective.ControlResumesAtTick)
                : null,
            observation.Actions
                .OrderBy(action => action.ActionCode)
                .ThenBy(action => action.ActionId, StringComparer.Ordinal)
                .Select(action => new ReplayV2ObservedActionAvailability(
                    action.ActionId,
                    action.ActionCode,
                    action.ParameterKinds
                        .OrderBy(kind => (int)kind)
                        .ToImmutableArray(),
                    action.Enabled,
                    action.Available,
                    action.ShotProgramAvailable,
                    action.AllowedDirections is { } directions
                        ? directions
                            .OrderBy(direction => (int)direction)
                            .ToImmutableArray()
                        : null,
                    action.AllowedUnitTargets is { } unitTargets
                        ? unitTargets
                            .Select(target => new ReplayV2ObservedUnitTarget(
                                target.TeamId,
                                target.UnitId))
                            .OrderBy(target => target.TeamId)
                            .ThenBy(target => target.UnitId)
                            .ToImmutableArray()
                        : null,
                    action.AllowedFormTargets is { } forms
                        ? forms
                            .Order(StringComparer.Ordinal)
                            .ToImmutableArray()
                        : null)
                {
                    AllowedProjectileHeadings =
                        action.AllowedProjectileHeadings is { } headings
                            ? headings
                                .OrderBy(heading => (int)heading)
                                .ToImmutableArray()
                            : null,
                })
                .ToImmutableArray());
    }

    public static ReplayV2LifeStart LifeStart(ActorMatchStart start)
    {
        ArgumentNullException.ThrowIfNull(start);
        return new ReplayV2LifeStart(
            start.SchemaVersion,
            start.RuntimeContractVersion,
            ActorId(start.ActorId),
            start.ParticipantId,
            start.ActorRandomSeed.ToString(CultureInfo.InvariantCulture),
            start.SpawnReason,
            start.Contract.MatchContractFingerprint);
    }

    public static ReplayV2ActorDecision Decision(
        ActorDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        return new ReplayV2ActorDecision(
            decision.ActionId,
            decision.ActionCode,
            ActionPayload(decision.Payload),
            decision.DebugMessage,
            decision.Faulted,
            decision.FaultMessage);
    }

    public static ReplayV2ActionResolution ActionResolution(
        FrontlineActionResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        return new ReplayV2ActionResolution(
            ActorId(resolution.ActorId),
            resolution.ChosenActionId,
            resolution.ChosenActionCode,
            ActionPayload(resolution.ChosenPayload),
            resolution.ValidatedActionId,
            resolution.ValidatedActionCode,
            ActionPayload(resolution.ValidatedPayload),
            resolution.Result);
    }

    public static ReplayV2TickStart TickStart(
        FrontlineTickStart tickStart,
        FrontlineMatchState preparedState)
    {
        ArgumentNullException.ThrowIfNull(tickStart);
        ArgumentNullException.ThrowIfNull(preparedState);
        if (preparedState.Tick != tickStart.Tick)
        {
            throw new ArgumentException(
                "Prepared state and tick-start chronology must match.",
                nameof(preparedState));
        }
        return new ReplayV2TickStart(
            WorldState(preparedState),
            tickStart.ActiveActors
                .Select(ActorId)
                .Order()
                .ToImmutableArray(),
            tickStart.Events
                .Select((value, ordinal) => Event(
                    ReplayV2Identifiers.LifecycleEventId(
                        value.Tick,
                        ordinal),
                    value))
                .ToImmutableArray());
    }

    public static ReplayV2AuthoritativeResolution Resolution(
        FrontlineStepResult step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return new ReplayV2AuthoritativeResolution(
            step.Events
                .Select((value, ordinal) => Event(
                    ReplayV2Identifiers.ResolutionEventId(
                        value.Tick,
                        ordinal),
                    value))
                .ToImmutableArray(),
            step.ProjectileTraversals
                .Select(ProjectileTraversal)
                .ToImmutableArray());
    }

    public static ReplayV2Event Event(
        string eventId,
        FrontlineMatchEvent value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentNullException.ThrowIfNull(value);

        (ReplayV2ActorId? source, ReplayV2ActorId? target) =
            CausalActors(value);
        return new ReplayV2Event(
            eventId,
            value.Tick,
            value.Type,
            value.TeamId,
            value.UnitId,
            source,
            target,
            value.ProjectileId is long projectileId
                ? WireId(projectileId)
                : null,
            value.From,
            value.To,
            value.FromFacing,
            value.ToFacing,
            value.ProjectileHeading,
            value.ActionId
                ?? (value.Action is BotAction action
                    ? ActionId(action)
                    : null),
            value.ActionCode
                ?? (value.Action is BotAction actionCode
                    ? (int)actionCode
                    : null),
            ActionPayload(value.ActionPayload)
                ?? ActionPayload(value.ShotProgram),
            value.ActionResult,
            value.Amount,
            value.NewHealth,
            value.LifecycleStatus,
            value.SpawnReason,
            value.RespawnAtTick,
            value.UnlockAtTick,
            value.RebuildReadyAtTick,
            value.FabricationAtTick,
            value.FromPositionIndex,
            value.ToPositionIndex,
            value.ClaimingTeamId,
            value.CaptureProgress,
            value.ControlResumesAtTick)
        {
            FromFormId = value.FromFormId,
            ToFormId = value.ToFormId,
            FormTransitionStartedAtTick =
                value.FormTransitionStartedAtTick,
            FormTransitionCompletesAtTick =
                value.FormTransitionCompletesAtTick,
        };
    }

    private static ReplayV2ActionPayload? ActionPayload(
        ActorActionPayload? payload)
    {
        if (payload is null
            || (payload.ShotProgram is null
                && payload.Direction is null
                && payload.UnitTarget is null
                && payload.FormTargetId is null
                && payload.LaunchHeading is null))
        {
            return null;
        }

        return new ReplayV2ActionPayload(
            payload.ShotProgram,
            payload.Direction,
            payload.UnitTarget is { } target
                ? new ReplayV2ObservedUnitTarget(
                    target.TeamId,
                    target.UnitId)
                : null,
            payload.FormTargetId)
        {
            LaunchHeading = payload.LaunchHeading,
        };
    }

    private static ReplayV2ActionPayload? ActionPayload(
        ShotProgram? shotProgram) =>
        shotProgram is null
            ? null
            : new ReplayV2ActionPayload(
                shotProgram,
                Direction: null,
                UnitTarget: null,
                FormTargetId: null)
            {
                LaunchHeading = null,
            };

    public static ReplayV2ProjectileTraversal ProjectileTraversal(
        FrontlineProjectileTraversal traversal)
    {
        ArgumentNullException.ThrowIfNull(traversal);
        return new ReplayV2ProjectileTraversal(
            WireId(traversal.Id),
            ActorId(traversal.OwnerActorId),
            traversal.Direction,
            traversal.From,
            traversal.Path.ToImmutableArray(),
            traversal.Heading,
            traversal.ShotProgram,
            traversal.ProgrammedPath?.ToImmutableArray());
    }

    public static ReplayV2WorldState WorldState(FrontlineMatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        bool energyEnabled = state.Definition.Rules.MaxEnergy > 0;

        return new ReplayV2WorldState(
            state.Teams
                .OrderBy(team => team.TeamId)
                .Select(team => new ReplayV2TeamState(
                    team.TeamId,
                    WireInt64(team.DamageDealt),
                    team.Units
                        .OrderBy(unit => unit.UnitId)
                        .Select(unit => new ReplayV2UnitState(
                            unit.TeamId,
                            unit.UnitId,
                            unit.DefaultFormId,
                            unit.LifecycleStatus,
                            unit.RespawnAtTick,
                            unit.UnlockAtTick,
                            unit.RebuildReadyAtTick,
                            unit.FabricationAtTick,
                            unit.ReservedSpawn,
                            unit.PendingSpawnReason,
                            unit.HasSpawned,
                            unit.NextLifeId,
                            WireInt64(unit.DamageDealt),
                            unit.ActiveLife is { } life
                                ? new ReplayV2LifeState(
                                    ActorId(life.ActorId),
                                    life.FormId,
                                    life.Position,
                                    life.Facing,
                                    life.Health,
                                    life.Cooldown,
                                    energyEnabled ? life.Energy : null,
                                    WireInt64(life.DamageDealt),
                                    life.LastActionResult,
                                    life.SpawnedAtTick)
                                {
                                    PendingFormTransition =
                                        FormTransition(
                                            life.PendingFormTransition),
                                }
                                : null))
                        .ToImmutableArray()))
                .ToImmutableArray(),
            state.Projectiles
                .OrderBy(projectile => projectile.Id)
                .Select(projectile => new ReplayV2ProjectileState(
                    WireId(projectile.Id),
                    ActorId(projectile.OwnerActorId),
                    projectile.Position,
                    projectile.Direction,
                    projectile.Heading,
                    projectile.ShotProgram,
                    projectile.ProgrammedPath?.ToImmutableArray(),
                    projectile.NextProgrammedPathIndex,
                    projectile.TilesTraveled,
                    projectile.Phase))
                .ToImmutableArray(),
            Control(state.Control));
    }

    public static ReplayV2Result Result(FrontlineMatchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new ReplayV2Result(
            result.WinnerTeamId,
            result.Reason,
            result.EndTick,
            WireInt64(result.TerritorialScore),
            Control(result.Control),
            result.Teams
                .OrderBy(team => team.TeamId)
                .Select(team => new ReplayV2TeamResult(
                    team.TeamId,
                    team.Outcome,
                    team.ActiveHealth,
                    WireInt64(team.DamageDealt),
                    team.Units
                        .OrderBy(unit => unit.UnitId)
                        .Select(unit => new ReplayV2UnitResult(
                            unit.TeamId,
                            unit.UnitId,
                            unit.DefaultFormId,
                            unit.FormId,
                            unit.LifecycleStatus,
                            unit.ActiveActorId is { } actorId
                                ? ActorId(actorId)
                                : null,
                            unit.Health,
                            WireInt64(unit.DamageDealt))
                        {
                            PendingFormTransition =
                                FormTransition(
                                    unit.PendingFormTransition),
                        })
                        .ToImmutableArray()))
                .ToImmutableArray());
    }

    private static ReplayV2ControlState Control(FrontlineControlState control) =>
        new(
            control.NextTick,
            control.ActivePositionIndex,
            control.ClaimingTeamId,
            control.CaptureProgress,
            control.DecayTicksElapsed,
            control.ControlResumesAtTick,
            control.WinnerTeamId);

    private static (ReplayV2ActorId? Source, ReplayV2ActorId? Target)
        CausalActors(FrontlineMatchEvent value)
    {
        ReplayV2ActorId? actor = value.ActorId is FrontlineActorId actorId
            ? ActorId(actorId)
            : null;
        ReplayV2ActorId? other = value.OtherActorId is FrontlineActorId otherId
            ? ActorId(otherId)
            : null;
        return value.Type switch
        {
            FrontlineMatchEventType.Damage
                or FrontlineMatchEventType.Destroyed => (other, actor),
            FrontlineMatchEventType.Shot => (actor, other),
            _ => (actor, null),
        };
    }

    private static ReplayV2ActorId ActorId(ActorIdentity actorId) =>
        new(actorId.TeamId, actorId.UnitId, actorId.LifeId);

    private static ReplayV2ActorId ActorId(FrontlineActorId actorId) =>
        new(actorId.TeamId, actorId.UnitId, actorId.LifeId);

    private static ReplayV2FormTransition? FormTransition(
        ObservedFormTransition? transition) =>
        transition is null
            ? null
            : new ReplayV2FormTransition(
                transition.FromFormId,
                transition.ToFormId,
                transition.StartedAtTick,
                transition.CompletesAtTick);

    private static ReplayV2FormTransition? FormTransition(
        FrontlinePendingFormTransition? transition) =>
        transition is null
            ? null
            : new ReplayV2FormTransition(
                transition.FromFormId,
                transition.ToFormId,
                transition.StartedAtTick,
                transition.CompletesAtTick);

    private static ImmutableArray<ReplayV2ActorId> ActorIds(
        IEnumerable<ActorIdentity> actorIds) =>
        actorIds
            .Select(ActorId)
            .Order()
            .ToImmutableArray();

    private static ReplayV2ObservedEnemyActorRef EnemyActorRef(
        ObservedEnemyActorRef actor) =>
        new(actor.TeamId, actor.UnitId, actor.LifeHandle);

    private static string WireId(long value) =>
        ReplayV2Identifiers.WireInt64(value);

    private static string WireInt64(long value) =>
        ReplayV2Identifiers.WireInt64(value);

    private static string ActionId(BotAction action) => action switch
    {
        BotAction.Wait => PublicActionIds.Wait,
        BotAction.MoveForward => PublicActionIds.MoveForward,
        BotAction.TurnLeft => PublicActionIds.TurnLeft,
        BotAction.TurnRight => PublicActionIds.TurnRight,
        BotAction.Shoot => PublicActionIds.Shoot,
        BotAction.StrafeLeft => PublicActionIds.StrafeLeft,
        BotAction.StrafeRight => PublicActionIds.StrafeRight,
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };
}
