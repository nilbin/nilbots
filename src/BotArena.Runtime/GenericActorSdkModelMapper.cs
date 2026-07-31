using System.Collections.Immutable;
using BotArena.Engine;
using Sdk = BotArena.Sdk;

namespace BotArena.Runtime;

/// <summary>
/// Explicit adapter between deliberately independent Engine and SDK generic
/// actor DTOs. Static contracts cross as canonical Engine-authored JSON;
/// dynamic unions are mapped case by case so additions fail closed.
/// </summary>
internal static class GenericActorSdkModelMapper
{
    public static Sdk.GenericActorMatchStart ToSdk(
        GenericActorRuntimeStart start)
    {
        ArgumentNullException.ThrowIfNull(start);
        return new Sdk.GenericActorMatchStart
        {
            SchemaVersion = start.SchemaVersion,
            RuntimeContractVersion = start.RuntimeContractVersion,
            ActorId = ToSdk(start.ActorId),
            ParticipantId = start.ParticipantId,
            ActorRandomSeed = start.ActorRandomSeed,
            TeamRandomSeed = start.TeamRandomSeed,
            Origin = ToSdk(start.Origin),
            Contract = Sdk.ActorCanonicalContractReader.Parse(
                ActorContractManifestSerializer.ToCanonicalJson(
                    start.Contract)),
        };
    }

    public static Sdk.GenericActorContext ToSdk(
        GenericActorRuntimeObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return new Sdk.GenericActorContext(
            observation.SchemaVersion,
            observation.Tick,
            observation.MatchContractFingerprint,
            ToSdk(observation.Self),
            observation.TeamUnits.Select(ToSdk),
            observation.Participants.Select(value =>
                ToSdk(value)),
            observation.Allies.Select(value =>
                ToSdk(value)),
            observation.Enemies.Select(value =>
                ToSdk(value)),
            observation.VisibleTiles.Select(value =>
                ToSdk(value)),
            observation.VisibleProjectiles?.Select(value =>
                ToSdk(value)),
            observation.VisibleEvents.Select(value =>
                ToSdk(value)),
            observation.HeardSounds?.Select(ToSdk),
            ToSdk(observation.Scoreboard),
            ToSdk(observation.Mode),
            observation.ActionLegalities.Select(ToSdk));
    }

    public static GenericActorRuntimeDecision ToEngine(
        Sdk.GenericActorDecision decision,
        string? debugMessage)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return new GenericActorRuntimeDecision(
            decision.ActionId,
            decision.ActionCode,
            decision.Arguments
                .Select(ToEngine)
                .ToImmutableArray(),
            debugMessage);
    }

    internal static Sdk.GenericActorMatchStart.LifeOrigin ToSdk(
        GenericActorRuntimeStart.LifeOrigin value) =>
        new(
            ToSdk(value.Reason),
            value.Generation,
            ToSdkOptional(value.ParentActorId),
            value.SourceTransitionId,
            value.SourceOperationId);

    internal static Sdk.GenericActorContext.ObservedSelfState ToSdk(
        GenericActorRuntimeObservation.ObservedSelfState value) =>
        new(
            ToSdk(value.ActorId),
            value.Generation,
            value.FormId,
            ToSdk(value.Position),
            ToSdk(value.Facing),
            value.Health,
            value.Cooldown,
            value.Energy,
            ToSdk(value.PreviousActionResolution),
            ToSdk(value.PendingSameLifeTransition),
            value.ClassId,
            ToSdk(value.RouteCooldowns),
            value.CarriedScrap);

    internal static Sdk.GenericActorContext.ObservedAllyState ToSdk(
        GenericActorRuntimeObservation.ObservedAllyState value) =>
        new(
            ToSdk(value.ActorId),
            value.Generation,
            value.FormId,
            ToSdk(value.Position),
            ToSdk(value.Facing),
            value.Health,
            value.Cooldown,
            value.Energy,
            ToSdk(value.PreviousActionResolution),
            ToSdk(value.PendingSameLifeTransition),
            value.ClassId,
            ToSdk(value.RouteCooldowns),
            value.CarriedScrap);

    internal static ImmutableArray<
        Sdk.GenericActorContext.ObservedRouteCooldown> ToSdk(
        ImmutableArray<GenericActorRuntimeObservation.ObservedRouteCooldown>
            value) =>
        [
            .. value.Select(cooldown =>
                new Sdk.GenericActorContext.ObservedRouteCooldown(
                    cooldown.TransitionId,
                    cooldown.ReadyAtTick)),
        ];

    internal static Sdk.GenericActorContext.PendingSameLifeTransition? ToSdk(
        GenericActorRuntimeObservation.PendingSameLifeTransition? value) =>
        value is null
            ? null
            : new(
                value.TransitionId,
                value.OperationId,
                value.TargetFormId,
                value.StartedTick,
                value.DueTick);

    internal static Sdk.GenericActorContext.ObservedUnitSlot ToSdk(
        GenericActorRuntimeObservation.ObservedUnitSlot value) =>
        new(
            value.TeamId,
            value.UnitId,
            ToSdk(value.State));

    internal static Sdk.GenericActorContext.UnitSlotState ToSdk(
        GenericActorRuntimeObservation.UnitSlotState value) =>
        value switch
        {
            GenericActorRuntimeObservation.UnitSlotState.Active active =>
                new Sdk.GenericActorContext.UnitSlotState.Active(
                    ToSdk(active.ActorId),
                    active.Generation,
                    active.FormId),
            GenericActorRuntimeObservation.UnitSlotState.AvailabilityPending pending =>
                new Sdk.GenericActorContext.UnitSlotState.AvailabilityPending(
                    ToSdk(pending.Reason),
                    pending.DueTick),
            GenericActorRuntimeObservation.UnitSlotState.AutomaticReturnPending
                pending =>
                new Sdk.GenericActorContext.UnitSlotState
                    .AutomaticReturnPending(
                        pending.DueTick,
                        pending.TargetFormId,
                        pending.Generation),
            GenericActorRuntimeObservation.UnitSlotState.Ready =>
                new Sdk.GenericActorContext.UnitSlotState.Ready(),
            GenericActorRuntimeObservation.UnitSlotState.FabricationPending pending =>
                new Sdk.GenericActorContext.UnitSlotState.FabricationPending(
                    pending.DueTick,
                    ToSdk(pending.SourceActorId),
                    pending.TransitionId,
                    pending.OperationId,
                    pending.TargetFormId,
                    ToSdk(pending.ReservedPosition)),
            GenericActorRuntimeObservation.UnitSlotState.ReplicationPending pending =>
                new Sdk.GenericActorContext.UnitSlotState.ReplicationPending(
                    pending.DueTick,
                    ToSdk(pending.SourceActorId),
                    pending.TransitionId,
                    pending.OperationId,
                    pending.TargetFormId,
                    ToSdk(pending.ReservedPosition)),
            GenericActorRuntimeObservation.UnitSlotState.PermanentlyDormant =>
                new Sdk.GenericActorContext.UnitSlotState.PermanentlyDormant(),
            _ => throw UnknownUnion(value),
        };

    internal static Sdk.GenericActorContext.ObservedParticipantStatus ToSdk(
        GenericActorRuntimeObservation.ObservedParticipantStatus value) =>
        new(
            value.ParticipantId,
            value.TeamId,
            value.RuntimeFaultCount,
            value.Disqualified,
            value.ClassId);

    internal static Sdk.GenericActorContext.ObservedEnemyState ToSdk(
        GenericActorRuntimeObservation.ObservedEnemyState value) =>
        new(
            ToSdk(value.ActorId),
            value.FormId,
            ToSdk(value.Position),
            ToSdk(value.Facing),
            value.Health,
            ToSdk(value.PendingSameLifeTransition),
            value.ObservedBy.Select(ToSdk),
            value.ClassId,
            value.CarriedScrap);

    internal static Sdk.GenericActorContext.ObservedTile ToSdk(
        GenericActorRuntimeObservation.ObservedTile value) =>
        new(
            ToSdk(value.Position),
            value.IsWall,
            value.ObservedBy.Select(ToSdk),
            ToSdk(value.SpawnReservation));

    internal static Sdk.GenericActorContext.SpawnReservation? ToSdk(
        GenericActorRuntimeObservation.SpawnReservation? value) =>
        value is null
            ? null
            : new(
                value.TeamId,
                value.UnitId,
                ToSdk(value.Kind),
                value.DueTick);

    internal static Sdk.GenericActorContext.ObservedProjectile ToSdk(
        GenericActorRuntimeObservation.ObservedProjectile value) =>
        new(
            value.ProjectileId,
            value.OwnerTeamId,
            ToSdkOptional(value.OwnerActorId),
            ToSdk(value.Position),
            ToSdk(value.Heading),
            value.TilesPerAdvance,
            value.TicksUntilAdvance,
            value.RemainingTiles,
            value.ObservedBy.Select(ToSdk),
            value.TicksPerAdvance,
            value.DamagePerHit);

    internal static Sdk.GenericActorContext.ObservedSound ToSdk(
        GenericActorRuntimeObservation.ObservedSound value) =>
        new(
            value.EventHandle,
            value.SourceTick,
            value.SourceOrdinal,
            ToSdk(value.ObserverActorId),
            ToSdk(value.Kind),
            value.Bearing,
            value.Distance);

    internal static Sdk.GenericActorContext.ObservedEvent ToSdk(
        GenericActorRuntimeObservation.ObservedEvent value) =>
        new(
            value.EventHandle,
            value.SourceTick,
            value.SourceOrdinal,
            ToSdk(value.Kind),
            ToSdk(value.Payload),
            value.ObservedBy.Select(ToSdk));

    internal static Sdk.GenericActorContext.EventPayload ToSdk(
        GenericActorRuntimeObservation.EventPayload value) =>
        value switch
        {
            GenericActorRuntimeObservation.EventPayload.Rotation payload =>
                new Sdk.GenericActorContext.EventPayload.Rotation(
                    ToSdk(payload.ActorId),
                    ToSdkRequired(payload.Action),
                    ToSdk(payload.Position),
                    ToSdk(payload.FromFacing),
                    ToSdk(payload.ToFacing)),
            GenericActorRuntimeObservation.EventPayload.Movement payload =>
                new Sdk.GenericActorContext.EventPayload.Movement(
                    ToSdk(payload.ActorId),
                    ToSdkRequired(payload.Action),
                    ToSdk(payload.From),
                    ToSdk(payload.To),
                    ToSdk(payload.Facing)),
            GenericActorRuntimeObservation.EventPayload.MovementBlocked payload =>
                new Sdk.GenericActorContext.EventPayload.MovementBlocked(
                    ToSdk(payload.ActorId),
                    ToSdkRequired(payload.Action),
                    ToSdk(payload.From),
                    ToSdk(payload.AttemptedTo),
                    ToSdk(payload.Facing)),
            GenericActorRuntimeObservation.EventPayload.Attack payload =>
                new Sdk.GenericActorContext.EventPayload.Attack(
                    ToSdk(payload.ActorId),
                    ToSdkRequired(payload.Action),
                    payload.ProjectileId,
                    ToSdk(payload.Origin),
                    ToSdk(payload.Heading)),
            GenericActorRuntimeObservation.EventPayload.Damage payload =>
                new Sdk.GenericActorContext.EventPayload.Damage(
                    payload.SourceTeamId,
                    ToSdkOptional(payload.SourceActorId),
                    ToSdk(payload.TargetActorId),
                    payload.ProjectileId,
                    payload.Amount,
                    payload.NewHealth,
                    ToSdk(payload.Position)),
            GenericActorRuntimeObservation.EventPayload.Destruction payload =>
                new Sdk.GenericActorContext.EventPayload.Destruction(
                    ToSdk(payload.ActorId),
                    payload.SourceTeamId,
                    ToSdkOptional(payload.SourceActorId),
                    payload.ProjectileId,
                    payload.Generation,
                    payload.FormId,
                    ToSdk(payload.Position)),
            GenericActorRuntimeObservation.EventPayload.LifeSpawned payload =>
                new Sdk.GenericActorContext.EventPayload.LifeSpawned(
                    ToSdk(payload.ActorId),
                    payload.ParticipantId,
                    ToSdkOptional(payload.ParentActorId),
                    payload.Generation,
                    payload.FormId,
                    payload.Health,
                    ToSdk(payload.Position),
                    ToSdk(payload.Reason),
                    payload.SourceTransitionId,
                    payload.SourceOperationId),
            GenericActorRuntimeObservation.EventPayload.LifeRetired payload =>
                new Sdk.GenericActorContext.EventPayload.LifeRetired(
                    ToSdk(payload.ActorId),
                    payload.Generation,
                    payload.FormId,
                    ToSdk(payload.Position),
                    payload.Reason,
                    payload.SourceTransitionId,
                    payload.SourceOperationId),
            GenericActorRuntimeObservation.EventPayload.RuntimeFault payload =>
                new Sdk.GenericActorContext.EventPayload.RuntimeFault(
                    ToSdkRequired(payload.Fault)),
            GenericActorRuntimeObservation.EventPayload.Participant payload =>
                new Sdk.GenericActorContext.EventPayload.Participant(
                    payload.ParticipantId,
                    payload.TeamId),
            GenericActorRuntimeObservation.EventPayload.Lifecycle payload =>
                new Sdk.GenericActorContext.EventPayload.Lifecycle(
                    payload.TransitionId,
                    payload.OperationId,
                    ToSdk(payload.SourceActorId),
                    payload.TargetTeamId,
                    payload.TargetUnitId,
                    payload.DueTick,
                    payload.CancellationReason),
            GenericActorRuntimeObservation.EventPayload.FormTransition payload =>
                new Sdk.GenericActorContext.EventPayload.FormTransition(
                    ToSdk(payload.ActorId),
                    payload.TransitionId,
                    payload.OperationId,
                    payload.FromFormId,
                    payload.ToFormId,
                    payload.StartedTick,
                    payload.DueTick,
                    payload.Reason
                        == GenericActorRuntimeObservation
                            .FormTransitionReason.AutomaticThresholdReturn),
            GenericActorRuntimeObservation.EventPayload.ScoreChanged payload =>
                new Sdk.GenericActorContext.EventPayload.ScoreChanged(
                    payload.TeamId,
                    payload.Channel,
                    payload.NewValue),
            GenericActorRuntimeObservation.EventPayload.ModeChanged payload =>
                new Sdk.GenericActorContext.EventPayload.ModeChanged(
                    ToSdk(payload.State)),
            GenericActorRuntimeObservation.EventPayload
                .LifecycleClockCancelled payload =>
                new Sdk.GenericActorContext.EventPayload
                    .LifecycleClockCancelled(
                        payload.TargetTeamId,
                        payload.TargetUnitId,
                        ToSdk(payload.CancelledState),
                        payload.CancellationReason),
            GenericActorRuntimeObservation.EventPayload
                .ProjectileDeflected payload =>
                new Sdk.GenericActorContext.EventPayload.ProjectileDeflected(
                    payload.SourceTeamId,
                    payload.SourceActorId is null
                        ? null
                        : ToSdk(payload.SourceActorId),
                    ToSdk(payload.TargetActorId),
                    payload.ProjectileId,
                    payload.DeflectedProjectileId,
                    payload.TargetFormId,
                    ToSdk(payload.TargetFacing),
                    ToSdk(payload.Heading),
                    ToSdk(payload.Position)),
            _ => throw UnknownUnion(value),
        };

    internal static Sdk.GenericActorContext.ScoreboardState ToSdk(
        GenericActorRuntimeObservation.ScoreboardState value) =>
        new(value.Teams.Select(ToSdk));

    internal static Sdk.GenericActorContext.TeamScoreState ToSdk(
        GenericActorRuntimeObservation.TeamScoreState value) =>
        new(
            value.TeamId,
            value.Eligible,
            value.Scores.Select(ToSdk));

    internal static Sdk.GenericActorContext.ScoreValue ToSdk(
        GenericActorRuntimeObservation.ScoreValue value) =>
        new(value.Channel, value.Value);

    internal static Sdk.GenericActorContext.ModeObservationState ToSdk(
        GenericActorRuntimeObservation.ModeObservationState value) =>
        value switch
        {
            GenericActorRuntimeObservation.ModeObservationState.Deathmatch mode =>
                new Sdk.GenericActorContext.ModeObservationState.Deathmatch(
                    mode.ModeId),
            GenericActorRuntimeObservation.ModeObservationState.Frontline mode =>
                new Sdk.GenericActorContext.ModeObservationState.Frontline(
                    mode.ModeId,
                    mode.ActivePositionIndex,
                    mode.ClaimingTeamId,
                    mode.CaptureProgress,
                    mode.DecayTicksElapsed,
                    mode.ControlResumesAtTick,
                    mode.HoldOwnerTeamId,
                    mode.HoldEndsAtTick,
                    mode.SecondaryOwnerTeamId,
                    mode.SecondaryClaimProgress,
                    [.. mode.ScrapTeams.Select(ToSdk)],
                    [.. mode.ScrapPiles.Select(ToSdk)]),
            _ => throw UnknownUnion(value),
        };

    internal static Sdk.GenericActorContext.ScrapTeamState ToSdk(
        GenericActorRuntimeObservation.ScrapTeamState value) =>
        new(value.TeamId, value.Bank, value.TierLevels);

    internal static Sdk.GenericActorContext.ScrapPile ToSdk(
        GenericActorRuntimeObservation.ScrapPile value) =>
        new(ToSdk(value.Position), value.Amount, value.ExpiresAtTick);

    internal static Sdk.GenericActorActionLegality ToSdk(
        GenericActorRuntimeActionLegality value) =>
        new(
            value.ActionId,
            value.ActionCode,
            value.AllowedByForm,
            value.Available,
            value.Constraints.Select(ToSdk));

    internal static Sdk.GenericActorActionLegality.ArgumentConstraint ToSdk(
        GenericActorRuntimeActionLegality.ArgumentConstraint value) =>
        value switch
        {
            GenericActorRuntimeActionLegality.ArgumentConstraint
                .ShotProgramConstraint constraint =>
                new Sdk.GenericActorActionLegality.ArgumentConstraint
                    .ShotProgramConstraint(constraint.Allowed),
            GenericActorRuntimeActionLegality.ArgumentConstraint
                .DirectionConstraint constraint =>
                new Sdk.GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint(
                        constraint.AllowedValues.Select(ToSdk)),
            GenericActorRuntimeActionLegality.ArgumentConstraint
                .UnitTargetConstraint constraint =>
                new Sdk.GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint(
                        constraint.AllowedValues.Select(ToSdk)),
            GenericActorRuntimeActionLegality.ArgumentConstraint
                .FormTargetConstraint constraint =>
                new Sdk.GenericActorActionLegality.ArgumentConstraint
                    .FormTargetConstraint(constraint.AllowedFormIds),
            GenericActorRuntimeActionLegality.ArgumentConstraint
                .ProjectileHeadingConstraint constraint =>
                new Sdk.GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint(
                        constraint.AllowedValues.Select(ToSdk)),
            GenericActorRuntimeActionLegality.ArgumentConstraint
                .UpgradeTrackConstraint constraint =>
                new Sdk.GenericActorActionLegality.ArgumentConstraint
                    .UpgradeTrackConstraint(constraint.AllowedTrackIds),
            _ => throw UnknownUnion(value),
        };

    internal static Sdk.GenericActorActionResolution? ToSdk(
        GenericActorRuntimeActionResolution? value) =>
        value is null
            ? null
            : new(
                ToSdkOptional(value.SubmittedAction),
                ToSdkRequired(value.AcceptedAction),
                ToSdkRequired(value.ValidatedAction),
                ToSdk(value.Outcome),
                ToSdkOptional(value.RuntimeFault));

    internal static Sdk.GenericActorActionResolution.ResolvedAction?
        ToSdkOptional(
        GenericActorRuntimeActionResolution.ResolvedAction? value) =>
        value is null
            ? null
            : ToSdkRequired(value);

    internal static Sdk.GenericActorActionResolution.ResolvedAction
        ToSdkRequired(
            GenericActorRuntimeActionResolution.ResolvedAction value) =>
        new(
            value.ActionId,
            value.ActionCode,
            value.Arguments.Select(ToSdk));

    internal static Sdk.GenericActorRuntimeFaultContext? ToSdkOptional(
        GenericActorRuntimeFault? value) =>
        value is null
            ? null
            : ToSdkRequired(value);

    internal static Sdk.GenericActorRuntimeFaultContext ToSdkRequired(
        GenericActorRuntimeFault value) =>
        new(
            value.ParticipantId,
            ToSdk(value.ActorId),
            ToSdk(value.Stage),
            value.FaultCode,
            value.CumulativeFaultCount,
            value.DisqualificationTriggered);

    internal static Sdk.GenericActorActionArgument ToSdk(
        GenericActorRuntimeActionArgument value) =>
        value switch
        {
            GenericActorRuntimeActionArgument.ShotProgramArgument argument =>
                new Sdk.GenericActorActionArgument.ShotProgramArgument(
                    ToSdk(argument.Value)),
            GenericActorRuntimeActionArgument.DirectionArgument argument =>
                new Sdk.GenericActorActionArgument.DirectionArgument(
                    ToSdk(argument.Value)),
            GenericActorRuntimeActionArgument.UnitTargetArgument argument =>
                new Sdk.GenericActorActionArgument.UnitTargetArgument(
                    ToSdk(argument.Value)),
            GenericActorRuntimeActionArgument.FormTargetArgument argument =>
                new Sdk.GenericActorActionArgument.FormTargetArgument(
                    argument.FormId),
            GenericActorRuntimeActionArgument.ProjectileHeadingArgument argument =>
                new Sdk.GenericActorActionArgument.ProjectileHeadingArgument(
                    ToSdk(argument.Value)),
            GenericActorRuntimeActionArgument.UpgradeTrackArgument argument =>
                new Sdk.GenericActorActionArgument.UpgradeTrackArgument(
                    argument.TrackId),
            _ => throw UnknownUnion(value),
        };

    internal static GenericActorRuntimeActionArgument ToEngine(
        Sdk.GenericActorActionArgument value) =>
        value switch
        {
            Sdk.GenericActorActionArgument.ShotProgramArgument argument =>
                new GenericActorRuntimeActionArgument.ShotProgramArgument(
                    ToEngine(argument.Value)),
            Sdk.GenericActorActionArgument.DirectionArgument argument =>
                new GenericActorRuntimeActionArgument.DirectionArgument(
                    ToEngine(argument.Value)),
            Sdk.GenericActorActionArgument.UnitTargetArgument argument =>
                new GenericActorRuntimeActionArgument.UnitTargetArgument(
                    new GenericActorRuntimeActionArgument.UnitTarget(
                        argument.Value.TeamId,
                        argument.Value.UnitId)),
            Sdk.GenericActorActionArgument.FormTargetArgument argument =>
                new GenericActorRuntimeActionArgument.FormTargetArgument(
                    argument.FormId),
            Sdk.GenericActorActionArgument.ProjectileHeadingArgument argument =>
                new GenericActorRuntimeActionArgument.ProjectileHeadingArgument(
                    ToEngine(argument.Value)),
            Sdk.GenericActorActionArgument.UpgradeTrackArgument argument =>
                new GenericActorRuntimeActionArgument.UpgradeTrackArgument(
                    argument.TrackId),
            _ => throw UnknownUnion(value),
        };

    internal static Sdk.ActorIdentity ToSdk(ActorIdentity value) =>
        new(value.TeamId, value.UnitId, value.LifeId);

    internal static Sdk.ActorIdentity? ToSdkOptional(ActorIdentity? value) =>
        value is null ? null : ToSdk(value);

    internal static Sdk.Position ToSdk(Position value) =>
        new(value.X, value.Y);

    internal static Sdk.ShotProgram ToSdk(ShotProgram value) =>
        new(
            value.InitialAimOffset,
            value.BendDirection,
            value.BendAfterTiles,
            value.BendEveryTiles,
            value.BendCount);

    internal static ShotProgram ToEngine(Sdk.ShotProgram value) =>
        new(
            value.InitialAimOffset,
            value.BendDirection,
            value.BendAfterTiles,
            value.BendEveryTiles,
            value.BendCount);

    internal static Sdk.GenericActorActionArgument.UnitTarget ToSdk(
        GenericActorRuntimeActionArgument.UnitTarget value) =>
        new(value.TeamId, value.UnitId);

    internal static Sdk.Direction ToSdk(Direction value) =>
        value switch
        {
            Direction.North => Sdk.Direction.North,
            Direction.East => Sdk.Direction.East,
            Direction.South => Sdk.Direction.South,
            Direction.West => Sdk.Direction.West,
            _ => throw UnknownEnum(value),
        };

    internal static Direction ToEngine(Sdk.Direction value) =>
        value switch
        {
            Sdk.Direction.North => Direction.North,
            Sdk.Direction.East => Direction.East,
            Sdk.Direction.South => Direction.South,
            Sdk.Direction.West => Direction.West,
            _ => throw UnknownEnum(value),
        };

    internal static Sdk.ProjectileHeading ToSdk(ProjectileHeading value) =>
        value switch
        {
            ProjectileHeading.North => Sdk.ProjectileHeading.North,
            ProjectileHeading.NorthEast => Sdk.ProjectileHeading.NorthEast,
            ProjectileHeading.East => Sdk.ProjectileHeading.East,
            ProjectileHeading.SouthEast => Sdk.ProjectileHeading.SouthEast,
            ProjectileHeading.South => Sdk.ProjectileHeading.South,
            ProjectileHeading.SouthWest => Sdk.ProjectileHeading.SouthWest,
            ProjectileHeading.West => Sdk.ProjectileHeading.West,
            ProjectileHeading.NorthWest => Sdk.ProjectileHeading.NorthWest,
            _ => throw UnknownEnum(value),
        };

    internal static ProjectileHeading ToEngine(Sdk.ProjectileHeading value) =>
        value switch
        {
            Sdk.ProjectileHeading.North => ProjectileHeading.North,
            Sdk.ProjectileHeading.NorthEast => ProjectileHeading.NorthEast,
            Sdk.ProjectileHeading.East => ProjectileHeading.East,
            Sdk.ProjectileHeading.SouthEast => ProjectileHeading.SouthEast,
            Sdk.ProjectileHeading.South => ProjectileHeading.South,
            Sdk.ProjectileHeading.SouthWest => ProjectileHeading.SouthWest,
            Sdk.ProjectileHeading.West => ProjectileHeading.West,
            Sdk.ProjectileHeading.NorthWest => ProjectileHeading.NorthWest,
            _ => throw UnknownEnum(value),
        };

    internal static Sdk.GenericActorMatchStart.SpawnReason ToSdk(
        GenericActorRuntimeStart.SpawnReason value) =>
        value switch
        {
            GenericActorRuntimeStart.SpawnReason.Initial =>
                Sdk.GenericActorMatchStart.SpawnReason.Initial,
            GenericActorRuntimeStart.SpawnReason.AutomaticReturn =>
                Sdk.GenericActorMatchStart.SpawnReason.AutomaticReturn,
            GenericActorRuntimeStart.SpawnReason.Fabrication =>
                Sdk.GenericActorMatchStart.SpawnReason.Fabrication,
            GenericActorRuntimeStart.SpawnReason.Replication =>
                Sdk.GenericActorMatchStart.SpawnReason.Replication,
            GenericActorRuntimeStart.SpawnReason.AutomaticActivation =>
                Sdk.GenericActorMatchStart.SpawnReason
                    .AutomaticActivation,
            _ => throw UnknownEnum(value),
        };

    internal static Sdk.GenericActorContext.AvailabilityReason ToSdk(
        GenericActorRuntimeObservation.AvailabilityReason value) =>
        value switch
        {
            GenericActorRuntimeObservation.AvailabilityReason.InitialUnlock =>
                Sdk.GenericActorContext.AvailabilityReason.InitialUnlock,
            GenericActorRuntimeObservation.AvailabilityReason.DestructionRecovery =>
                Sdk.GenericActorContext.AvailabilityReason
                    .DestructionRecovery,
            _ => throw UnknownEnum(value),
        };

    internal static Sdk.GenericActorContext.SpawnReservationKind ToSdk(
        GenericActorRuntimeObservation.SpawnReservationKind value) =>
        value switch
        {
            GenericActorRuntimeObservation.SpawnReservationKind
                .AutomaticReturn =>
                Sdk.GenericActorContext.SpawnReservationKind.AutomaticReturn,
            GenericActorRuntimeObservation.SpawnReservationKind.Fabrication =>
                Sdk.GenericActorContext.SpawnReservationKind.Fabrication,
            GenericActorRuntimeObservation.SpawnReservationKind.Replication =>
                Sdk.GenericActorContext.SpawnReservationKind.Replication,
            _ => throw UnknownEnum(value),
        };

    internal static Sdk.GenericActorContext.EventKind ToSdk(
        GenericActorRuntimeObservation.EventKind value) =>
        value switch
        {
            GenericActorRuntimeObservation.EventKind.Rotation =>
                Sdk.GenericActorContext.EventKind.Rotation,
            GenericActorRuntimeObservation.EventKind.Movement =>
                Sdk.GenericActorContext.EventKind.Movement,
            GenericActorRuntimeObservation.EventKind.MovementBlocked =>
                Sdk.GenericActorContext.EventKind.MovementBlocked,
            GenericActorRuntimeObservation.EventKind.Attack =>
                Sdk.GenericActorContext.EventKind.Attack,
            GenericActorRuntimeObservation.EventKind.Damage =>
                Sdk.GenericActorContext.EventKind.Damage,
            GenericActorRuntimeObservation.EventKind.Destruction =>
                Sdk.GenericActorContext.EventKind.Destruction,
            GenericActorRuntimeObservation.EventKind.LifeSpawned =>
                Sdk.GenericActorContext.EventKind.LifeSpawned,
            GenericActorRuntimeObservation.EventKind.LifeRetired =>
                Sdk.GenericActorContext.EventKind.LifeRetired,
            GenericActorRuntimeObservation.EventKind.RuntimeFault =>
                Sdk.GenericActorContext.EventKind.RuntimeFault,
            GenericActorRuntimeObservation.EventKind.ParticipantDisqualified =>
                Sdk.GenericActorContext.EventKind.ParticipantDisqualified,
            GenericActorRuntimeObservation.EventKind.LifecycleQueued =>
                Sdk.GenericActorContext.EventKind.LifecycleQueued,
            GenericActorRuntimeObservation.EventKind.LifecycleCancelled =>
                Sdk.GenericActorContext.EventKind.LifecycleCancelled,
            GenericActorRuntimeObservation.EventKind.LifecycleCompleted =>
                Sdk.GenericActorContext.EventKind.LifecycleCompleted,
            GenericActorRuntimeObservation.EventKind.FormTransitionStarted =>
                Sdk.GenericActorContext.EventKind.FormTransitionStarted,
            GenericActorRuntimeObservation.EventKind.FormTransitionCompleted =>
                Sdk.GenericActorContext.EventKind.FormTransitionCompleted,
            GenericActorRuntimeObservation.EventKind.FormTransitionCancelled =>
                Sdk.GenericActorContext.EventKind.FormTransitionCancelled,
            GenericActorRuntimeObservation.EventKind.ScoreChanged =>
                Sdk.GenericActorContext.EventKind.ScoreChanged,
            GenericActorRuntimeObservation.EventKind.ModeChanged =>
                Sdk.GenericActorContext.EventKind.ModeChanged,
            GenericActorRuntimeObservation.EventKind.LifecycleClockCancelled =>
                Sdk.GenericActorContext.EventKind.LifecycleClockCancelled,
            GenericActorRuntimeObservation.EventKind.ProjectileDeflected =>
                Sdk.GenericActorContext.EventKind.ProjectileDeflected,
            _ => throw UnknownEnum(value),
        };

    internal static Sdk.GenericActorActionResolution.ActionOutcome ToSdk(
        GenericActorRuntimeActionResolution.ActionOutcome value) =>
        value switch
        {
            GenericActorRuntimeActionResolution.ActionOutcome.Success =>
                Sdk.GenericActorActionResolution.ActionOutcome.Success,
            GenericActorRuntimeActionResolution.ActionOutcome.Blocked =>
                Sdk.GenericActorActionResolution.ActionOutcome.Blocked,
            GenericActorRuntimeActionResolution.ActionOutcome.Rejected =>
                Sdk.GenericActorActionResolution.ActionOutcome.Rejected,
            GenericActorRuntimeActionResolution.ActionOutcome.Faulted =>
                Sdk.GenericActorActionResolution.ActionOutcome.Faulted,
            _ => throw UnknownEnum(value),
        };

    internal static Sdk.GenericActorRuntimeFaultContext.FaultStage ToSdk(
        GenericActorRuntimeFault.FaultStage value) =>
        value switch
        {
            GenericActorRuntimeFault.FaultStage.RuntimeCreate =>
                Sdk.GenericActorRuntimeFaultContext.FaultStage.RuntimeCreate,
            GenericActorRuntimeFault.FaultStage.LifeStart =>
                Sdk.GenericActorRuntimeFaultContext.FaultStage.LifeStart,
            GenericActorRuntimeFault.FaultStage.TickExecution =>
                Sdk.GenericActorRuntimeFaultContext.FaultStage.TickExecution,
            GenericActorRuntimeFault.FaultStage.DecisionValidation =>
                Sdk.GenericActorRuntimeFaultContext.FaultStage
                    .DecisionValidation,
            _ => throw UnknownEnum(value),
        };

    internal static ArgumentOutOfRangeException UnknownEnum<T>(T value)
        where T : struct, Enum =>
        new(nameof(value), value, "Unknown generic actor enum value.");

    internal static ArgumentException UnknownUnion(object value) =>
        new(
            $"Unknown generic actor union case '{value.GetType().FullName}'.",
            nameof(value));
}
