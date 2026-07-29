using System.Collections.Immutable;
using System.Globalization;

namespace BotArena.Engine;

/// <summary>
/// Lossless projection from validated generic chronology into replay-owned
/// immutable DTOs. It performs no JSON serialization or replay hashing.
/// </summary>
internal static class ReplayV3Projection
{
    public static ReplayV3 Project(
        GenericActorMatchChronology chronology,
        ReplayV3.PresentationMetadata? presentation = null)
    {
        ArgumentNullException.ThrowIfNull(chronology);

        return new ReplayV3(
            Header(
                chronology.Descriptor,
                Presentation(presentation)),
            InitialFrame(chronology.InitialFrame),
            chronology.Ticks
                .Select(TickFrame)
                .ToImmutableArray(),
            chronology.Result is null
                ? null
                : MatchResult(chronology.Result),
            ReplayHash: null,
            Partial: chronology.Partial);
    }

    private static ReplayV3.ReplayHeader Header(
        GenericActorMatchDescriptor descriptor,
        ReplayV3.PresentationMetadata? presentation)
    {
        ActorResolvedMatchDefinition definition = descriptor.Definition;
        ActorMatchCapabilityVersions versions =
            definition.CapabilityVersions;
        return new ReplayV3.ReplayHeader(
            BotArenaVersions.GenericActorReplayFormatVersion,
            descriptor.EngineVersion,
            definition.Rules.RulesetId,
            new ReplayV3.RuntimeVersions(
                versions.ContractProfileId,
                descriptor.ActorRuntimeProtocolVersion,
                descriptor.ActorRuntimeConfigurationVersion,
                versions.RuntimeContractVersion,
                versions.MatchStartSchemaVersion,
                versions.ObservationSchemaVersion,
                versions.DecisionSchemaVersion,
                versions.MatchContractSchemaVersion),
            Decimal(descriptor.MatchSeed),
            new ReplayV3.ResolvedContract(
                definition.SchemaVersion,
                descriptor.MatchContractFingerprint,
                ActorContractManifestSerializer.ToCanonicalJson(
                    definition)),
            presentation,
            new ReplayV3.ProvenanceMetadata(
                descriptor.Participants
                    .Select(Participant)
                    .ToImmutableArray()));
    }

    private static ReplayV3.PresentationMetadata? Presentation(
        ReplayV3.PresentationMetadata? presentation)
    {
        if (presentation is null)
            return null;

        ValidateOptionalPresentationId(
            presentation.ThemeId,
            nameof(presentation.ThemeId));
        var formIds = new HashSet<string>(StringComparer.Ordinal);
        ImmutableArray<ReplayV3.FormPresentationMetadata> forms =
            (presentation.Forms.IsDefault
                    ? []
                    : presentation.Forms)
                .Select(form =>
                {
                    ArgumentNullException.ThrowIfNull(form);
                    ValidatePresentationId(
                        form.FormId,
                        nameof(form.FormId));
                    ValidateOptionalPresentationId(
                        form.LookId,
                        nameof(form.LookId));
                    ValidateOptionalPresentationId(
                        form.ProjectileLookId,
                        nameof(form.ProjectileLookId));
                    if (!formIds.Add(form.FormId))
                    {
                        throw new ArgumentException(
                            $"Duplicate presentation form id '{form.FormId}'.",
                            nameof(presentation));
                    }
                    return new ReplayV3.FormPresentationMetadata(
                        form.FormId,
                        form.LookId,
                        form.ProjectileLookId);
                })
                .OrderBy(form => form.FormId, StringComparer.Ordinal)
                .ToImmutableArray();

        ReplayV3.MapPresentationMetadata? map =
            presentation.Map is null
                ? null
                : MapPresentation(presentation.Map);
        return new ReplayV3.PresentationMetadata(
            presentation.ThemeId,
            map,
            forms);
    }

    private static ReplayV3.MapPresentationMetadata MapPresentation(
        ReplayV3.MapPresentationMetadata map)
    {
        ValidatePresentationId(
            map.BoundaryWall,
            nameof(map.BoundaryWall));
        ValidatePresentationId(
            map.InteriorWall,
            nameof(map.InteriorWall));
        var families = new HashSet<string>(StringComparer.Ordinal);
        var claimedTiles = new HashSet<(int X, int Y)>();
        ImmutableArray<ReplayV3.WallGroupPresentationMetadata> groups =
            (map.WallGroups.IsDefault ? [] : map.WallGroups)
                .Select(group =>
                {
                    ArgumentNullException.ThrowIfNull(group);
                    ValidatePresentationId(
                        group.Family,
                        nameof(group.Family));
                    if (!families.Add(group.Family))
                    {
                        throw new ArgumentException(
                            $"Duplicate presentation wall family '{group.Family}'.",
                            nameof(map));
                    }
                    ImmutableArray<ReplayV3.PositionValue> tiles =
                        (group.Tiles.IsDefault ? [] : group.Tiles)
                            .Select(tile =>
                            {
                                ArgumentNullException.ThrowIfNull(tile);
                                if (!claimedTiles.Add((tile.X, tile.Y)))
                                {
                                    throw new ArgumentException(
                                        $"Presentation tile ({tile.X}, {tile.Y}) belongs to more than one wall-group entry.",
                                        nameof(map));
                                }
                                return new ReplayV3.PositionValue(
                                    tile.X,
                                    tile.Y);
                            })
                            .OrderBy(tile => tile.Y)
                            .ThenBy(tile => tile.X)
                            .ToImmutableArray();
                    return new ReplayV3.WallGroupPresentationMetadata(
                        group.Family,
                        tiles);
                })
                .OrderBy(
                    group => group.Family,
                    StringComparer.Ordinal)
                .ToImmutableArray();
        return new ReplayV3.MapPresentationMetadata(
            map.BoundaryWall,
            map.InteriorWall,
            groups);
    }

    private static void ValidatePresentationId(
        string value,
        string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Presentation identifiers cannot be blank.",
                name);
        }
    }

    private static void ValidateOptionalPresentationId(
        string? value,
        string name)
    {
        if (value is not null)
            ValidatePresentationId(value, name);
    }

    private static ReplayV3.ParticipantProvenance Participant(
        GenericActorParticipantProvenance participant) =>
        new(
            participant.ParticipantId,
            participant.TeamId,
            participant.Name,
            participant.RuntimeKind,
            participant.ArtifactHash,
            participant.Accent,
            participant.LookId,
            participant.ProjectileLookId);

    private static ReplayV3.ReplayInitialFrame InitialFrame(
        GenericActorMatchInitialFrame frame) =>
        new(
            WorldState(frame.State),
            frame.LifeStarts.Select(LifeStart).ToImmutableArray(),
            frame.Events.Select(Event).ToImmutableArray());

    private static ReplayV3.TickFrame TickFrame(
        GenericActorMatchTickFrame frame) =>
        new(
            frame.Tick,
            TickStart(frame.TickStart),
            frame.ActorTurns.Select(ActorTurn).ToImmutableArray(),
            frame.Events.Select(Event).ToImmutableArray(),
            frame.Traversals.Select(Traversal).ToImmutableArray(),
            WorldState(frame.PostState));

    private static ReplayV3.TickStart TickStart(
        GenericActorMatchTickStart tickStart) =>
        new(
            tickStart.Tick,
            WorldState(tickStart.State),
            tickStart.ActiveActorIds.Select(ActorId).ToImmutableArray(),
            tickStart.LifeStarts.Select(LifeStart).ToImmutableArray(),
            tickStart.Events.Select(Event).ToImmutableArray(),
            tickStart.Traversals.Select(Traversal).ToImmutableArray());

    private static ReplayV3.ActorTurn ActorTurn(
        GenericActorMatchActorTurn turn) =>
        new(
            turn.Tick,
            turn.ParticipantId,
            ActorId(turn.ActorId),
            Observation(turn.Observation),
            turn.SubmittedDecision is null
                ? null
                : SubmittedDecision(turn.SubmittedDecision),
            ActionResolution(turn.ActionResolution));

    private static ReplayV3.LifeStart LifeStart(
        GenericActorLifeStart start) =>
        new(
            start.SchemaVersion,
            start.RuntimeContractVersion,
            ActorId(start.ActorId),
            start.ParticipantId,
            Decimal(start.ActorRandomSeed),
            new ReplayV3.LifeOrigin(
                SpawnReason(start.Origin.Reason),
                start.Origin.Generation,
                start.Origin.ParentActorId is null
                    ? null
                    : ActorId(start.Origin.ParentActorId),
                start.Origin.SourceTransitionId,
                start.Origin.SourceOperationId),
            start.MatchContractFingerprint);

    private static ReplayV3.Observation Observation(
        GenericActorRuntimeObservation observation) =>
        new(
            observation.SchemaVersion,
            observation.Tick,
            observation.MatchContractFingerprint,
            ObservedSelf(observation.Self),
            observation.TeamUnits
                .Select(ObservedUnitSlot)
                .ToImmutableArray(),
            observation.Participants
                .Select(ParticipantStatus)
                .ToImmutableArray(),
            observation.Allies
                .Select(ObservedAlly)
                .ToImmutableArray(),
            observation.Enemies
                .Select(ObservedEnemy)
                .ToImmutableArray(),
            observation.VisibleTiles
                .Select(ObservedTile)
                .ToImmutableArray(),
            observation.VisibleProjectiles is { } projectiles
                ? projectiles
                    .Select(ObservedProjectile)
                    .ToImmutableArray()
                : null,
            observation.VisibleEvents
                .Select(ObservedEvent)
                .ToImmutableArray(),
            observation.HeardSounds is { } sounds
                ? sounds
                    .Select(ObservedSound)
                    .ToImmutableArray()
                : null,
            Scoreboard(observation.Scoreboard),
            ModeState(observation.Mode),
            observation.ActionLegalities
                .Select(ActionLegality)
                .ToImmutableArray());

    private static ReplayV3.ObservedSelf ObservedSelf(
        GenericActorRuntimeObservation.ObservedSelfState value) =>
        new(
            ActorId(value.ActorId),
            value.Generation,
            value.FormId,
            Position(value.Position),
            Direction(value.Facing),
            value.Health,
            value.Cooldown,
            value.Energy,
            value.PreviousActionResolution is null
                ? null
                : ActionResolution(value.PreviousActionResolution),
            PendingTransition(value.PendingSameLifeTransition));

    private static ReplayV3.ObservedAlly ObservedAlly(
        GenericActorRuntimeObservation.ObservedAllyState value) =>
        new(
            ActorId(value.ActorId),
            value.Generation,
            value.FormId,
            Position(value.Position),
            Direction(value.Facing),
            value.Health,
            value.Cooldown,
            value.Energy,
            value.PreviousActionResolution is null
                ? null
                : ActionResolution(value.PreviousActionResolution),
            PendingTransition(value.PendingSameLifeTransition));

    private static ReplayV3.ObservedEnemy ObservedEnemy(
        GenericActorRuntimeObservation.ObservedEnemyState value) =>
        new(
            ActorId(value.ActorId),
            value.FormId,
            Position(value.Position),
            Direction(value.Facing),
            value.Health,
            PendingTransition(value.PendingSameLifeTransition),
            value.ObservedBy.Select(ActorId).ToImmutableArray());

    private static ReplayV3.PendingSameLifeTransition? PendingTransition(
        GenericActorRuntimeObservation.PendingSameLifeTransition? value) =>
        value is null
            ? null
            : new ReplayV3.PendingSameLifeTransition(
                value.TransitionId,
                value.OperationId,
                value.TargetFormId,
                value.StartedTick,
                value.DueTick);

    private static ReplayV3.ObservedUnitSlot ObservedUnitSlot(
        GenericActorRuntimeObservation.ObservedUnitSlot value) =>
        new(
            value.TeamId,
            value.UnitId,
            UnitSlotState(value.State));

    private static ReplayV3.UnitSlotState UnitSlotState(
        GenericActorRuntimeObservation.UnitSlotState value) =>
        value switch
        {
            GenericActorRuntimeObservation.UnitSlotState.Active active =>
                new ReplayV3.UnitSlotState.Active(
                    ActorId(active.ActorId),
                    active.Generation,
                    active.FormId),
            GenericActorRuntimeObservation.UnitSlotState
                .AvailabilityPending pending =>
                new ReplayV3.UnitSlotState.AvailabilityPending(
                    AvailabilityReason(pending.Reason),
                    pending.DueTick),
            GenericActorRuntimeObservation.UnitSlotState
                .AutomaticReturnPending pending =>
                new ReplayV3.UnitSlotState.AutomaticReturnPending(
                    pending.DueTick,
                    pending.TargetFormId,
                    pending.Generation),
            GenericActorRuntimeObservation.UnitSlotState.Ready =>
                new ReplayV3.UnitSlotState.Ready(),
            GenericActorRuntimeObservation.UnitSlotState
                .FabricationPending pending =>
                new ReplayV3.UnitSlotState.FabricationPending(
                    pending.DueTick,
                    ActorId(pending.SourceActorId),
                    pending.TransitionId,
                    pending.OperationId,
                    pending.TargetFormId,
                    Position(pending.ReservedPosition)),
            GenericActorRuntimeObservation.UnitSlotState
                .ReplicationPending pending =>
                new ReplayV3.UnitSlotState.ReplicationPending(
                    pending.DueTick,
                    ActorId(pending.SourceActorId),
                    pending.TransitionId,
                    pending.OperationId,
                    pending.TargetFormId,
                    Position(pending.ReservedPosition)),
            GenericActorRuntimeObservation.UnitSlotState
                .PermanentlyDormant =>
                new ReplayV3.UnitSlotState.PermanentlyDormant(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Unknown stable-slot state."),
        };

    private static ReplayV3.ParticipantStatus ParticipantStatus(
        GenericActorRuntimeObservation.ObservedParticipantStatus value) =>
        new(
            value.ParticipantId,
            value.TeamId,
            Decimal(value.RuntimeFaultCount),
            value.Disqualified);

    private static ReplayV3.ObservedTile ObservedTile(
        GenericActorRuntimeObservation.ObservedTile value) =>
        new(
            Position(value.Position),
            value.IsWall,
            value.ObservedBy.Select(ActorId).ToImmutableArray());

    private static ReplayV3.ObservedProjectile ObservedProjectile(
        GenericActorRuntimeObservation.ObservedProjectile value) =>
        new(
            Decimal(value.ProjectileId),
            value.OwnerTeamId,
            value.OwnerActorId is null
                ? null
                : ActorId(value.OwnerActorId),
            Position(value.Position),
            ProjectileHeading(value.Heading),
            value.TilesPerAdvance,
            value.TicksUntilAdvance,
            value.RemainingTiles,
            value.ObservedBy.Select(ActorId).ToImmutableArray(),
            value.TicksPerAdvance,
            value.DamagePerHit);

    private static ReplayV3.ObservedEvent ObservedEvent(
        GenericActorRuntimeObservation.ObservedEvent value) =>
        new(
            value.EventHandle,
            value.SourceTick,
            value.SourceOrdinal,
            EventKind(value.Kind),
            EventPayload(value.Payload),
            value.ObservedBy.Select(ActorId).ToImmutableArray());

    private static ReplayV3.ObservedSound ObservedSound(
        GenericActorRuntimeObservation.ObservedSound value) =>
        new(
            value.EventHandle,
            value.SourceTick,
            value.SourceOrdinal,
            ActorId(value.ObserverActorId),
            EventKind(value.Kind),
            value.Bearing,
            value.Distance);

    private static ReplayV3.SubmittedDecision SubmittedDecision(
        GenericActorRuntimeDecision decision) =>
        new(
            decision.ActionId,
            decision.ActionCode,
            RawActionArguments(decision.Arguments),
            decision.DebugMessage);

    private static ImmutableArray<ReplayV3.RawActionArgument?>?
        RawActionArguments(
            ImmutableArray<GenericActorRuntimeActionArgument> values)
    {
        if (values.IsDefault)
            return null;
        return values
            .Select(static value => RawActionArgument(value))
            .ToImmutableArray();
    }

    private static ReplayV3.RawActionArgument? RawActionArgument(
        GenericActorRuntimeActionArgument? value) =>
        value switch
        {
            null => null,
            GenericActorRuntimeActionArgument.ShotProgramArgument argument =>
                new ReplayV3.RawActionArgument.ShotProgram(
                    ShotProgram(argument.Value)),
            GenericActorRuntimeActionArgument.DirectionArgument argument =>
                new ReplayV3.RawActionArgument.Direction(
                    (int)argument.Value),
            GenericActorRuntimeActionArgument.UnitTargetArgument argument =>
                new ReplayV3.RawActionArgument.UnitTarget(
                    argument.Value.TeamId,
                    argument.Value.UnitId),
            GenericActorRuntimeActionArgument.FormTargetArgument argument =>
                new ReplayV3.RawActionArgument.FormTarget(argument.FormId),
            GenericActorRuntimeActionArgument.ProjectileHeadingArgument
                argument =>
                new ReplayV3.RawActionArgument.ProjectileHeading(
                    (int)argument.Value),
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Unknown raw action argument."),
        };

    private static ReplayV3.ActionResolution ActionResolution(
        GenericActorRuntimeActionResolution value) =>
        new(
            value.SubmittedAction is null
                ? null
                : ResolvedAction(value.SubmittedAction),
            ResolvedAction(value.AcceptedAction),
            ResolvedAction(value.ValidatedAction),
            ActionOutcome(value.Outcome),
            value.RuntimeFault is null
                ? null
                : RuntimeFault(value.RuntimeFault));

    private static ReplayV3.ResolvedAction ResolvedAction(
        GenericActorRuntimeActionResolution.ResolvedAction value) =>
        new(
            value.ActionId,
            value.ActionCode,
            ActionArguments(value.Arguments));

    private static ImmutableArray<ReplayV3.ActionArgument> ActionArguments(
        ImmutableArray<GenericActorRuntimeActionArgument> values)
    {
        if (values.IsDefault)
            return default;
        return values.Select(ActionArgument).ToImmutableArray();
    }

    private static ReplayV3.ActionArgument ActionArgument(
        GenericActorRuntimeActionArgument value) =>
        value switch
        {
            GenericActorRuntimeActionArgument.ShotProgramArgument argument =>
                new ReplayV3.ActionArgument.ShotProgram(
                    ShotProgram(argument.Value)),
            GenericActorRuntimeActionArgument.DirectionArgument argument =>
                new ReplayV3.ActionArgument.Direction(
                    Direction(argument.Value)),
            GenericActorRuntimeActionArgument.UnitTargetArgument argument =>
                new ReplayV3.ActionArgument.UnitTarget(
                    argument.Value.TeamId,
                    argument.Value.UnitId),
            GenericActorRuntimeActionArgument.FormTargetArgument argument =>
                new ReplayV3.ActionArgument.FormTarget(argument.FormId),
            GenericActorRuntimeActionArgument.ProjectileHeadingArgument
                argument =>
                new ReplayV3.ActionArgument.ProjectileHeading(
                    ProjectileHeading(argument.Value)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Unknown action argument."),
        };

    private static ReplayV3.RuntimeFault RuntimeFault(
        GenericActorRuntimeFault value) =>
        new(
            value.ParticipantId,
            ActorId(value.ActorId),
            FaultStage(value.Stage),
            value.FaultCode,
            Decimal(value.CumulativeFaultCount),
            value.DisqualificationTriggered);

    private static ReplayV3.ActionLegality ActionLegality(
        GenericActorRuntimeActionLegality value) =>
        new(
            value.ActionId,
            value.ActionCode,
            value.AllowedByForm,
            value.Available,
            value.Constraints
                .Select(ActionConstraint)
                .ToImmutableArray());

    private static ReplayV3.ActionConstraint ActionConstraint(
        GenericActorRuntimeActionLegality.ArgumentConstraint value) =>
        value switch
        {
            GenericActorRuntimeActionLegality.ArgumentConstraint
                .ShotProgramConstraint constraint =>
                new ReplayV3.ActionConstraint.ShotProgram(
                    constraint.Allowed),
            GenericActorRuntimeActionLegality.ArgumentConstraint
                .DirectionConstraint constraint =>
                new ReplayV3.ActionConstraint.Direction(
                    constraint.AllowedValues
                        .Select(Direction)
                        .ToImmutableArray()),
            GenericActorRuntimeActionLegality.ArgumentConstraint
                .UnitTargetConstraint constraint =>
                new ReplayV3.ActionConstraint.UnitTarget(
                    constraint.AllowedValues
                        .Select(target =>
                            new ReplayV3.UnitTargetValue(
                                target.TeamId,
                                target.UnitId))
                        .ToImmutableArray()),
            GenericActorRuntimeActionLegality.ArgumentConstraint
                .FormTargetConstraint constraint =>
                new ReplayV3.ActionConstraint.FormTarget(
                    constraint.AllowedFormIds),
            GenericActorRuntimeActionLegality.ArgumentConstraint
                .ProjectileHeadingConstraint constraint =>
                new ReplayV3.ActionConstraint.ProjectileHeading(
                    constraint.AllowedValues
                        .Select(ProjectileHeading)
                        .ToImmutableArray()),
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Unknown action constraint."),
        };

    private static ReplayV3.EventPayload EventPayload(
        GenericActorRuntimeObservation.EventPayload value) =>
        value switch
        {
            GenericActorRuntimeObservation.EventPayload.Rotation payload =>
                new ReplayV3.EventPayload.Rotation(
                    ActorId(payload.ActorId),
                    ResolvedAction(payload.Action),
                    Position(payload.Position),
                    Direction(payload.FromFacing),
                    Direction(payload.ToFacing)),
            GenericActorRuntimeObservation.EventPayload.Movement payload =>
                new ReplayV3.EventPayload.Movement(
                    ActorId(payload.ActorId),
                    ResolvedAction(payload.Action),
                    Position(payload.From),
                    Position(payload.To),
                    Direction(payload.Facing)),
            GenericActorRuntimeObservation.EventPayload.MovementBlocked
                payload =>
                new ReplayV3.EventPayload.MovementBlocked(
                    ActorId(payload.ActorId),
                    ResolvedAction(payload.Action),
                    Position(payload.From),
                    Position(payload.AttemptedTo),
                    Direction(payload.Facing)),
            GenericActorRuntimeObservation.EventPayload.Attack payload =>
                new ReplayV3.EventPayload.Attack(
                    ActorId(payload.ActorId),
                    ResolvedAction(payload.Action),
                    Decimal(payload.ProjectileId),
                    Position(payload.Origin),
                    ProjectileHeading(payload.Heading)),
            GenericActorRuntimeObservation.EventPayload.Damage payload =>
                new ReplayV3.EventPayload.Damage(
                    payload.SourceTeamId,
                    payload.SourceActorId is null
                        ? null
                        : ActorId(payload.SourceActorId),
                    ActorId(payload.TargetActorId),
                    Decimal(payload.ProjectileId),
                    payload.Amount,
                    payload.NewHealth,
                    Position(payload.Position)),
            GenericActorRuntimeObservation.EventPayload.ProjectileDeflected
                payload =>
                new ReplayV3.EventPayload.ProjectileDeflected(
                    payload.SourceTeamId,
                    payload.SourceActorId is null
                        ? null
                        : ActorId(payload.SourceActorId),
                    ActorId(payload.TargetActorId),
                    Decimal(payload.ProjectileId),
                    Decimal(payload.DeflectedProjectileId),
                    payload.TargetFormId,
                    Direction(payload.TargetFacing),
                    ProjectileHeading(payload.Heading),
                    Position(payload.Position)),
            GenericActorRuntimeObservation.EventPayload.Destruction
                payload =>
                new ReplayV3.EventPayload.Destruction(
                    ActorId(payload.ActorId),
                    payload.SourceTeamId,
                    payload.SourceActorId is null
                        ? null
                        : ActorId(payload.SourceActorId),
                    payload.ProjectileId is null
                        ? null
                        : Decimal(payload.ProjectileId.Value),
                    payload.Generation,
                    payload.FormId,
                    Position(payload.Position)),
            GenericActorRuntimeObservation.EventPayload.LifeSpawned
                payload =>
                new ReplayV3.EventPayload.LifeSpawned(
                    ActorId(payload.ActorId),
                    payload.ParticipantId,
                    payload.ParentActorId is null
                        ? null
                        : ActorId(payload.ParentActorId),
                    payload.Generation,
                    payload.FormId,
                    payload.Health,
                    Position(payload.Position),
                    SpawnReason(payload.Reason),
                    payload.SourceTransitionId,
                    payload.SourceOperationId),
            GenericActorRuntimeObservation.EventPayload.LifeRetired
                payload =>
                new ReplayV3.EventPayload.LifeRetired(
                    ActorId(payload.ActorId),
                    payload.Generation,
                    payload.FormId,
                    Position(payload.Position),
                    payload.Reason,
                    payload.SourceTransitionId,
                    payload.SourceOperationId),
            GenericActorRuntimeObservation.EventPayload.RuntimeFault
                payload =>
                new ReplayV3.EventPayload.RuntimeFaultValue(
                    RuntimeFault(payload.Fault)),
            GenericActorRuntimeObservation.EventPayload.Participant
                payload =>
                new ReplayV3.EventPayload.Participant(
                    payload.ParticipantId,
                    payload.TeamId),
            GenericActorRuntimeObservation.EventPayload.Lifecycle payload =>
                new ReplayV3.EventPayload.Lifecycle(
                    payload.TransitionId,
                    payload.OperationId,
                    ActorId(payload.SourceActorId),
                    payload.TargetTeamId,
                    payload.TargetUnitId,
                    payload.DueTick,
                    payload.CancellationReason),
            GenericActorRuntimeObservation.EventPayload.FormTransition
                payload =>
                new ReplayV3.EventPayload.FormTransition(
                    ActorId(payload.ActorId),
                    payload.TransitionId,
                    payload.OperationId,
                    payload.FromFormId,
                    payload.ToFormId,
                    payload.StartedTick,
                    payload.DueTick,
                    FormTransitionReason(payload.Reason)),
            GenericActorRuntimeObservation.EventPayload.ScoreChanged
                payload =>
                new ReplayV3.EventPayload.ScoreChanged(
                    payload.TeamId,
                    payload.Channel,
                    Decimal(payload.NewValue)),
            GenericActorRuntimeObservation.EventPayload.ModeChanged
                payload =>
                new ReplayV3.EventPayload.ModeChanged(
                    ModeState(payload.State)),
            GenericActorRuntimeObservation.EventPayload
                .LifecycleClockCancelled payload =>
                new ReplayV3.EventPayload.LifecycleClockCancelled(
                    payload.TargetTeamId,
                    payload.TargetUnitId,
                    UnitSlotState(payload.CancelledState),
                    payload.CancellationReason),
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Unknown event payload."),
        };

    private static ReplayV3.AuthoritativeEvent Event(
        GenericActorAuthoritativeEvent value) =>
        new(
            value.EventHandle,
            value.Tick,
            Decimal(value.GlobalOrdinal),
            value.SourceOrdinal,
            EventKind(value.Kind),
            EventPayload(value.UnredactedPayload),
            EventAudience(value.EventAudience));

    private static ReplayV3.EventAudience EventAudience(
        GenericActorAuthoritativeEvent.Audience value) =>
        value switch
        {
            GenericActorAuthoritativeEvent.Audience.Public =>
                new ReplayV3.EventAudience.Public(),
            GenericActorAuthoritativeEvent.Audience.Spatial audience =>
                new ReplayV3.EventAudience.Spatial(
                    Position(audience.PrimaryPosition)),
            GenericActorAuthoritativeEvent.Audience.TeamPrivate audience =>
                new ReplayV3.EventAudience.TeamPrivate(audience.TeamId),
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Unknown event audience."),
        };

    private static ReplayV3.ProjectileTraversal Traversal(
        GenericActorProjectileTraversal value) =>
        new(
            value.Tick,
            Decimal(value.GlobalOrdinal),
            TraversalPhase(value.Phase),
            TraversalTrigger(value.Trigger),
            Decimal(value.ProjectileId),
            value.OwnerParticipantId,
            value.OwnerTeamId,
            ActorId(value.OwnerActorId),
            value.AttackProfileId,
            Position(value.From),
            value.Path.Select(Position).ToImmutableArray(),
            ProjectileHeading(value.LaunchHeading),
            ProjectileHeading(value.FinalHeading),
            value.ShotProgram is null
                ? null
                : ShotProgram(value.ShotProgram.Value),
            TraversalTerminal(value.Terminal));

    private static ReplayV3.TraversalTerminal TraversalTerminal(
        GenericActorProjectileTraversal.TerminalDisposition value) =>
        value switch
        {
            GenericActorProjectileTraversal.TerminalDisposition.Retained =>
                new ReplayV3.TraversalTerminal.Retained(),
            GenericActorProjectileTraversal.TerminalDisposition
                .WallOrPathExhausted =>
                new ReplayV3.TraversalTerminal.WallOrPathExhausted(),
            GenericActorProjectileTraversal.TerminalDisposition
                .RangeExhausted =>
                new ReplayV3.TraversalTerminal.RangeExhausted(),
            GenericActorProjectileTraversal.TerminalDisposition
                .ActorContact terminal =>
                new ReplayV3.TraversalTerminal.ActorContact(
                    ActorId(terminal.TargetActorId),
                    terminal.AppliedDamage),
            GenericActorProjectileTraversal.TerminalDisposition
                .MovementContact terminal =>
                new ReplayV3.TraversalTerminal.MovementContact(
                    ActorId(terminal.TargetActorId),
                    terminal.AppliedDamage),
            GenericActorProjectileTraversal.TerminalDisposition
                .LifecyclePlacementPurge terminal =>
                new ReplayV3.TraversalTerminal.LifecyclePlacementPurge(
                    Position(terminal.Position)),
            GenericActorProjectileTraversal.TerminalDisposition
                .ParticipantDisqualification terminal =>
                new ReplayV3.TraversalTerminal
                    .ParticipantDisqualification(
                        terminal.ParticipantId),
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Unknown projectile terminal disposition."),
        };

    private static ReplayV3.WorldState WorldState(
        GenericActorWorldSnapshot value) =>
        new(
            value.MatchContractFingerprint,
            value.NextTick,
            Decimal(value.NextProjectileId),
            value.Participants
                .Select(ParticipantStatus)
                .ToImmutableArray(),
            value.Slots.Select(SlotState).ToImmutableArray(),
            value.ActiveLives.Select(LifeState).ToImmutableArray(),
            value.PendingReplications
                .Select(PendingReplication)
                .ToImmutableArray(),
            value.Projectiles
                .Select(ProjectileState)
                .ToImmutableArray(),
            Scoreboard(value.Scoreboard),
            ModeState(value.Mode));

    private static ReplayV3.SlotState SlotState(
        GenericActorWorldSnapshot.SlotSnapshot value) =>
        new(
            value.TeamId,
            value.UnitId,
            value.ParticipantId,
            value.NextLifeId,
            UnitSlotState(value.State),
            value.PendingParentActorId is null
                ? null
                : ActorId(value.PendingParentActorId),
            value.SplitReservation is null
                ? null
                : PendingReplication(value.SplitReservation));

    private static ReplayV3.LifeState LifeState(
        GenericActorWorldSnapshot.LifeSnapshot value) =>
        new(
            ActorId(value.ActorId),
            value.ParticipantId,
            value.Generation,
            value.FormId,
            Position(value.Position),
            Direction(value.Facing),
            value.Health,
            value.Cooldown,
            value.Energy,
            value.SpawnedAtTick,
            SpawnReason(value.SpawnReason),
            value.ParentActorId is null
                ? null
                : ActorId(value.ParentActorId),
            value.SourceTransitionId,
            value.SourceOperationId,
            value.PreviousActionResolution is null
                ? null
                : ActionResolution(value.PreviousActionResolution),
            PendingTransition(value.PendingSameLifeTransition));

    private static ReplayV3.PendingReplication PendingReplication(
        SplitReplicationReservation value) =>
        new(
            ActorId(value.SourceActorId),
            value.ParticipantId,
            value.SourceGeneration,
            value.SourceFormId,
            Position(value.SourcePosition),
            Direction(value.SourceFacing),
            value.TransitionId,
            value.OperationId,
            value.QueuedTick,
            value.DueTick,
            value.Descendants
                .Select(descendant =>
                    new ReplayV3.ReservedDescendant(
                        descendant.TeamId,
                        descendant.UnitId,
                        descendant.FormId,
                        descendant.Generation,
                        Position(descendant.Position)))
                .ToImmutableArray());

    private static ReplayV3.ProjectileState ProjectileState(
        GenericActorWorldSnapshot.ProjectileSnapshot value) =>
        new(
            Decimal(value.ProjectileId),
            value.OwnerParticipantId,
            value.OwnerTeamId,
            ActorId(value.OwnerActorId),
            value.AttackProfileId,
            value.SpawnedAtTick,
            Position(value.Origin),
            Position(value.Position),
            ProjectileHeading(value.LaunchHeading),
            ProjectileHeading(value.Heading),
            value.ShotProgram is null
                ? null
                : ShotProgram(value.ShotProgram.Value),
            value.CommittedPath.Select(Position).ToImmutableArray(),
            value.NextPathIndex,
            value.RemainingTiles,
            value.TicksUntilAdvance);

    private static ReplayV3.Scoreboard Scoreboard(
        GenericActorRuntimeObservation.ScoreboardState value) =>
        new(
            value.Teams
                .Select(team =>
                    new ReplayV3.TeamScore(
                        team.TeamId,
                        team.Eligible,
                        team.Scores
                            .Select(score =>
                                new ReplayV3.ScoreValue(
                                    score.Channel,
                                    Decimal(score.Value)))
                            .ToImmutableArray()))
                .ToImmutableArray());

    private static ReplayV3.ModeState ModeState(
        GenericActorRuntimeObservation.ModeObservationState value) =>
        value switch
        {
            GenericActorRuntimeObservation.ModeObservationState.Deathmatch
                deathmatch =>
                new ReplayV3.ModeState.Deathmatch(deathmatch.ModeId),
            GenericActorRuntimeObservation.ModeObservationState.Frontline
                frontline =>
                FrontlineModeState(frontline),
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Unknown mode state."),
        };

    private static ReplayV3.ModeState.Frontline FrontlineModeState(
        GenericActorRuntimeObservation.ModeObservationState.Frontline
            value) =>
        new(
            value.ModeId,
            value.ActivePositionIndex,
            value.ClaimingTeamId,
            value.CaptureProgress,
            value.DecayTicksElapsed,
            value.ControlResumesAtTick,
            value.HoldOwnerTeamId,
            value.HoldEndsAtTick);

    private static ReplayV3.MatchResult MatchResult(
        GenericActorMatchResult value) =>
        new(
            value.CompletionReason,
            value.EndTick,
            Standings(value.Standings),
            value.EligibleTeamIds,
            value.Units.Select(UnitTerminalFact).ToImmutableArray(),
            ModeResult(value.Mode));

    private static ReplayV3.Standings Standings(
        TeamStandings value) =>
        new(
            value.WinnerTeamId,
            value.Standings
                .Select(standing =>
                    new ReplayV3.TeamStanding(
                        standing.TeamId,
                        standing.Rank,
                        StandingOutcome(standing.Outcome),
                        standing.Scores
                            .Select(score =>
                                new ReplayV3.ScoreValue(
                                    ScoreChannel(score.Channel),
                                    Decimal(score.Value)))
                            .ToImmutableArray()))
                .ToImmutableArray());

    private static ReplayV3.UnitTerminalFact UnitTerminalFact(
        GenericActorMatchResult.UnitTerminalFact value) =>
        new(
            SlotState(value.Slot),
            value.ActiveLife is null
                ? null
                : LifeState(value.ActiveLife));

    private static ReplayV3.ModeResult ModeResult(
        GenericActorMatchModeResult value) =>
        value switch
        {
            GenericActorMatchModeResult.Deathmatch deathmatch =>
                new ReplayV3.ModeResult.Deathmatch(
                    DeathmatchEndReason(deathmatch.Reason),
                    deathmatch.Scores.Teams
                        .Select(score =>
                            new ReplayV3.DeathmatchTeamScore(
                                score.TeamId,
                                Decimal(score.Kills),
                                Decimal(score.Deaths),
                                Decimal(score.DamageDealt)))
                        .ToImmutableArray()),
            GenericActorMatchModeResult.Frontline frontline =>
                new ReplayV3.ModeResult.Frontline(
                    FrontlineEndReason(frontline.Reason),
                    FrontlineModeState(frontline.Control),
                    frontline.Scores.Teams
                        .Select(score =>
                            new ReplayV3.FrontlineTeamScore(
                                score.TeamId,
                                Decimal(score.TerritorialProgress)))
                        .ToImmutableArray()),
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Unknown terminal mode result."),
        };

    private static ReplayV3.ActorId ActorId(ActorIdentity value) =>
        new(value.TeamId, value.UnitId, value.LifeId);

    private static ReplayV3.PositionValue Position(Position value) =>
        new(value.X, value.Y);

    private static ReplayV3.ShotProgramValue ShotProgram(
        ShotProgram value) =>
        new(
            value.InitialAimOffset,
            value.BendDirection,
            value.BendAfterTiles,
            value.BendEveryTiles,
            value.BendCount);

    private static string Decimal(long value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Decimal(ulong value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Direction(Direction value) =>
        value switch
        {
            BotArena.Engine.Direction.North => "north",
            BotArena.Engine.Direction.East => "east",
            BotArena.Engine.Direction.South => "south",
            BotArena.Engine.Direction.West => "west",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static string ProjectileHeading(ProjectileHeading value) =>
        value switch
        {
            BotArena.Engine.ProjectileHeading.North => "north",
            BotArena.Engine.ProjectileHeading.NorthEast => "north-east",
            BotArena.Engine.ProjectileHeading.East => "east",
            BotArena.Engine.ProjectileHeading.SouthEast => "south-east",
            BotArena.Engine.ProjectileHeading.South => "south",
            BotArena.Engine.ProjectileHeading.SouthWest => "south-west",
            BotArena.Engine.ProjectileHeading.West => "west",
            BotArena.Engine.ProjectileHeading.NorthWest => "north-west",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static string SpawnReason(
        GenericActorRuntimeStart.SpawnReason value) =>
        value switch
        {
            GenericActorRuntimeStart.SpawnReason.Initial => "initial",
            GenericActorRuntimeStart.SpawnReason.AutomaticReturn =>
                "automatic-return",
            GenericActorRuntimeStart.SpawnReason.Fabrication =>
                "fabrication",
            GenericActorRuntimeStart.SpawnReason.Replication =>
                "replication",
            GenericActorRuntimeStart.SpawnReason.AutomaticActivation =>
                "automatic-activation",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    /// <summary>
    /// Null for the inert cause, so the canonical document omits the property
    /// and every replay written before automatic returns keeps its bytes.
    /// </summary>
    private static string? FormTransitionReason(
        GenericActorRuntimeObservation.FormTransitionReason value) =>
        value switch
        {
            GenericActorRuntimeObservation.FormTransitionReason.Requested =>
                null,
            GenericActorRuntimeObservation.FormTransitionReason
                .AutomaticThresholdReturn => "automatic-threshold-return",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static string AvailabilityReason(
        GenericActorRuntimeObservation.AvailabilityReason value) =>
        value switch
        {
            GenericActorRuntimeObservation.AvailabilityReason
                .InitialUnlock => "initial-unlock",
            GenericActorRuntimeObservation.AvailabilityReason
                .DestructionRecovery => "destruction-recovery",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static string ActionOutcome(
        GenericActorRuntimeActionResolution.ActionOutcome value) =>
        value switch
        {
            GenericActorRuntimeActionResolution.ActionOutcome.Success =>
                "success",
            GenericActorRuntimeActionResolution.ActionOutcome.Blocked =>
                "blocked",
            GenericActorRuntimeActionResolution.ActionOutcome.Rejected =>
                "rejected",
            GenericActorRuntimeActionResolution.ActionOutcome.Faulted =>
                "faulted",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static string FaultStage(
        GenericActorRuntimeFault.FaultStage value) =>
        value switch
        {
            GenericActorRuntimeFault.FaultStage.RuntimeCreate =>
                "runtime-create",
            GenericActorRuntimeFault.FaultStage.LifeStart => "life-start",
            GenericActorRuntimeFault.FaultStage.TickExecution =>
                "tick-execution",
            GenericActorRuntimeFault.FaultStage.DecisionValidation =>
                "decision-validation",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static string EventKind(
        GenericActorRuntimeObservation.EventKind value) =>
        value switch
        {
            GenericActorRuntimeObservation.EventKind.Rotation => "rotation",
            GenericActorRuntimeObservation.EventKind.Movement => "movement",
            GenericActorRuntimeObservation.EventKind.MovementBlocked =>
                "movement-blocked",
            GenericActorRuntimeObservation.EventKind.Attack => "attack",
            GenericActorRuntimeObservation.EventKind.Damage => "damage",
            GenericActorRuntimeObservation.EventKind.Destruction =>
                "destruction",
            GenericActorRuntimeObservation.EventKind.LifeSpawned =>
                "life-spawned",
            GenericActorRuntimeObservation.EventKind.LifeRetired =>
                "life-retired",
            GenericActorRuntimeObservation.EventKind.RuntimeFault =>
                "runtime-fault",
            GenericActorRuntimeObservation.EventKind
                .ParticipantDisqualified =>
                "participant-disqualified",
            GenericActorRuntimeObservation.EventKind.LifecycleQueued =>
                "lifecycle-queued",
            GenericActorRuntimeObservation.EventKind.LifecycleCancelled =>
                "lifecycle-cancelled",
            GenericActorRuntimeObservation.EventKind.LifecycleCompleted =>
                "lifecycle-completed",
            GenericActorRuntimeObservation.EventKind
                .FormTransitionStarted =>
                "form-transition-started",
            GenericActorRuntimeObservation.EventKind
                .FormTransitionCompleted =>
                "form-transition-completed",
            GenericActorRuntimeObservation.EventKind
                .FormTransitionCancelled =>
                "form-transition-cancelled",
            GenericActorRuntimeObservation.EventKind.ScoreChanged =>
                "score-changed",
            GenericActorRuntimeObservation.EventKind.ModeChanged =>
                "mode-changed",
            GenericActorRuntimeObservation.EventKind
                .LifecycleClockCancelled =>
                "lifecycle-clock-cancelled",
            GenericActorRuntimeObservation.EventKind.ProjectileDeflected =>
                "projectile-deflected",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static string TraversalPhase(
        GenericActorProjectileTraversal.TraversalPhase value) =>
        value switch
        {
            GenericActorProjectileTraversal.TraversalPhase.TickStart =>
                "tick-start",
            GenericActorProjectileTraversal.TraversalPhase.Resolution =>
                "resolution",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static string TraversalTrigger(
        GenericActorProjectileTraversal.TraversalTrigger value) =>
        value switch
        {
            GenericActorProjectileTraversal.TraversalTrigger
                .LifecyclePlacement =>
                "lifecycle-placement",
            GenericActorProjectileTraversal.TraversalTrigger
                .MovementContact =>
                "movement-contact",
            GenericActorProjectileTraversal.TraversalTrigger
                .ScheduledAdvance =>
                "scheduled-advance",
            GenericActorProjectileTraversal.TraversalTrigger.AttackLaunch =>
                "attack-launch",
            GenericActorProjectileTraversal.TraversalTrigger
                .GuardDeflection =>
                "guard-deflection",
            GenericActorProjectileTraversal.TraversalTrigger
                .ParticipantDisqualification =>
                "participant-disqualification",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static string StandingOutcome(TeamStandingOutcome value) =>
        value switch
        {
            TeamStandingOutcome.Win => "win",
            TeamStandingOutcome.Loss => "loss",
            TeamStandingOutcome.Draw => "draw",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static string ScoreChannel(
        ScoreChannelDefinition.ChannelKind value) =>
        value switch
        {
            ScoreChannelDefinition.ChannelKind.Kills => "kills",
            ScoreChannelDefinition.ChannelKind.Deaths => "deaths",
            ScoreChannelDefinition.ChannelKind.DamageDealt =>
                "damage-dealt",
            ScoreChannelDefinition.ChannelKind.ActiveHealth =>
                "active-health",
            ScoreChannelDefinition.ChannelKind.TerritorialProgress =>
                "territorial-progress",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static string DeathmatchEndReason(
        GenericDeathmatchEndReason value) =>
        value switch
        {
            GenericDeathmatchEndReason.FaultEligibility =>
                "fault-eligibility",
            GenericDeathmatchEndReason.KillLimit => "kill-limit",
            GenericDeathmatchEndReason.MaxTicks => "max-ticks",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

    private static string FrontlineEndReason(
        GenericFrontlineEndReason value) =>
        value switch
        {
            GenericFrontlineEndReason.FaultEligibility =>
                "fault-eligibility",
            GenericFrontlineEndReason.BaseBreach => "base-breach",
            GenericFrontlineEndReason.MaxTicks => "max-ticks",
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };
}
