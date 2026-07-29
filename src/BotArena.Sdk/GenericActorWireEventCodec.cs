namespace BotArena.Sdk;

/// <summary>Typed event and sound codec for generic observations.</summary>
internal static class GenericActorWireEventCodec
{
    public static byte[] EncodeEvent(
        GenericActorContext.ObservedEvent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var writer = new ActorWireObjectWriter();
        writer.Field(
            1,
            GenericActorWireCodecValues.Handle(value.EventHandle));
        writer.Field(2, ActorWireValue.Int32(value.SourceTick));
        writer.Field(3, ActorWireValue.Int32(value.SourceOrdinal));
        writer.Field(4, ActorWireValue.Enum(value.Kind));
        writer.Field(5, EncodePayload(value.Payload));
        writer.Field(
            6,
            GenericActorWireCodecValues.Array(
                value.ObservedBy,
                GenericActorWireCodecValues.EncodeIdentity));
        return writer.ToArray();
    }

    public static GenericActorContext.ObservedEvent DecodeEvent(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        GenericActorContext.EventKind kind =
            GenericActorWireCodecValues.Enum<
                GenericActorContext.EventKind>(reader, 4);
        return GenericActorWireCodecValues.Decode(
            () => new GenericActorContext.ObservedEvent(
                GenericActorWireCodecValues.Handle(reader.Required(1)),
                GenericActorWireCodecValues.Int32(reader, 2),
                GenericActorWireCodecValues.Int32(reader, 3),
                kind,
                DecodePayload(
                    kind,
                    reader.Required(5),
                    depth + 1),
                GenericActorWireCodecValues.Array(
                    reader,
                    6,
                    item => GenericActorWireCodecValues.DecodeIdentity(
                        item,
                        depth + 1))),
            "visible event");
    }

    public static byte[] EncodeSound(
        GenericActorContext.ObservedSound value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var writer = new ActorWireObjectWriter();
        writer.Field(
            1,
            GenericActorWireCodecValues.Handle(value.EventHandle));
        writer.Field(2, ActorWireValue.Int32(value.SourceTick));
        writer.Field(3, ActorWireValue.Int32(value.SourceOrdinal));
        writer.Field(
            4,
            GenericActorWireCodecValues.EncodeIdentity(
                value.ObserverActorId));
        writer.Field(5, ActorWireValue.Enum(value.Kind));
        writer.Field(6, ActorWireValue.Int32(value.Bearing));
        writer.Field(7, ActorWireValue.Int32(value.Distance));
        return writer.ToArray();
    }

    public static GenericActorContext.ObservedSound DecodeSound(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return GenericActorWireCodecValues.Decode(
            () => new GenericActorContext.ObservedSound(
                GenericActorWireCodecValues.Handle(reader.Required(1)),
                GenericActorWireCodecValues.Int32(reader, 2),
                GenericActorWireCodecValues.Int32(reader, 3),
                GenericActorWireCodecValues.DecodeIdentity(
                    reader.Required(4),
                    depth + 1),
                GenericActorWireCodecValues.Enum<
                    GenericActorContext.EventKind>(reader, 5),
                GenericActorWireCodecValues.Int32(reader, 6),
                GenericActorWireCodecValues.Int32(reader, 7)),
            "heard sound");
    }

    private static byte[] EncodePayload(
        GenericActorContext.EventPayload value)
    {
        var writer = new ActorWireObjectWriter();
        switch (value)
        {
            case GenericActorContext.EventPayload.Rotation rotation:
                writer.Field(
                    1,
                    GenericActorWireCodecValues.EncodeIdentity(
                        rotation.ActorId));
                writer.Field(
                    2,
                    GenericActorWireActionCodec.EncodeResolvedAction(
                        rotation.Action));
                writer.Field(
                    3,
                    GenericActorWireCodecValues.EncodePosition(
                        rotation.Position));
                writer.Field(4, ActorWireValue.Enum(rotation.FromFacing));
                writer.Field(5, ActorWireValue.Enum(rotation.ToFacing));
                break;
            case GenericActorContext.EventPayload.Movement movement:
                writer.Field(
                    1,
                    GenericActorWireCodecValues.EncodeIdentity(
                        movement.ActorId));
                writer.Field(
                    2,
                    GenericActorWireActionCodec.EncodeResolvedAction(
                        movement.Action));
                writer.Field(
                    3,
                    GenericActorWireCodecValues.EncodePosition(movement.From));
                writer.Field(
                    4,
                    GenericActorWireCodecValues.EncodePosition(movement.To));
                writer.Field(5, ActorWireValue.Enum(movement.Facing));
                break;
            case GenericActorContext.EventPayload.MovementBlocked blocked:
                writer.Field(
                    1,
                    GenericActorWireCodecValues.EncodeIdentity(
                        blocked.ActorId));
                writer.Field(
                    2,
                    GenericActorWireActionCodec.EncodeResolvedAction(
                        blocked.Action));
                writer.Field(
                    3,
                    GenericActorWireCodecValues.EncodePosition(blocked.From));
                writer.Field(
                    4,
                    GenericActorWireCodecValues.EncodePosition(
                        blocked.AttemptedTo));
                writer.Field(5, ActorWireValue.Enum(blocked.Facing));
                break;
            case GenericActorContext.EventPayload.Attack attack:
                writer.Field(
                    1,
                    GenericActorWireCodecValues.EncodeIdentity(
                        attack.ActorId));
                writer.Field(
                    2,
                    GenericActorWireActionCodec.EncodeResolvedAction(
                        attack.Action));
                writer.Field(
                    3,
                    GenericActorWireCodecValues.Int64(attack.ProjectileId));
                writer.Field(
                    4,
                    GenericActorWireCodecValues.EncodePosition(attack.Origin));
                writer.Field(5, ActorWireValue.Enum(attack.Heading));
                break;
            case GenericActorContext.EventPayload.Damage damage:
                writer.Field(1, ActorWireValue.Int32(damage.SourceTeamId));
                writer.Optional(
                    2,
                    damage.SourceActorId is null
                        ? null
                        : GenericActorWireCodecValues.EncodeIdentity(
                            damage.SourceActorId));
                writer.Field(
                    3,
                    GenericActorWireCodecValues.EncodeIdentity(
                        damage.TargetActorId));
                writer.Field(
                    4,
                    GenericActorWireCodecValues.Int64(damage.ProjectileId));
                writer.Field(5, ActorWireValue.Int32(damage.Amount));
                writer.Field(6, ActorWireValue.Int32(damage.NewHealth));
                writer.Field(
                    7,
                    GenericActorWireCodecValues.EncodePosition(
                        damage.Position));
                break;
            case GenericActorContext.EventPayload.ProjectileDeflected deflected:
                writer.Field(1, ActorWireValue.Int32(deflected.SourceTeamId));
                writer.Optional(
                    2,
                    deflected.SourceActorId is null
                        ? null
                        : GenericActorWireCodecValues.EncodeIdentity(
                            deflected.SourceActorId));
                writer.Field(
                    3,
                    GenericActorWireCodecValues.EncodeIdentity(
                        deflected.TargetActorId));
                writer.Field(
                    4,
                    GenericActorWireCodecValues.Int64(deflected.ProjectileId));
                writer.Field(
                    5,
                    ActorWireValue.String(deflected.TargetFormId));
                writer.Field(6, ActorWireValue.Enum(deflected.TargetFacing));
                writer.Field(7, ActorWireValue.Enum(deflected.Heading));
                writer.Field(
                    8,
                    GenericActorWireCodecValues.EncodePosition(
                        deflected.Position));
                writer.Field(
                    9,
                    GenericActorWireCodecValues.Int64(
                        deflected.DeflectedProjectileId));
                break;
            case GenericActorContext.EventPayload.Destruction destruction:
                writer.Field(
                    1,
                    GenericActorWireCodecValues.EncodeIdentity(
                        destruction.ActorId));
                GenericActorWireCodecValues.OptionalInt32(
                    writer,
                    2,
                    destruction.SourceTeamId);
                writer.Optional(
                    3,
                    destruction.SourceActorId is null
                        ? null
                        : GenericActorWireCodecValues.EncodeIdentity(
                            destruction.SourceActorId));
                writer.Optional(
                    4,
                    destruction.ProjectileId is long projectileId
                        ? GenericActorWireCodecValues.Int64(projectileId)
                        : null);
                writer.Field(
                    5,
                    ActorWireValue.Int32(destruction.Generation));
                writer.Field(
                    6,
                    GenericActorWireCodecValues.SemanticId(
                        destruction.FormId));
                writer.Field(
                    7,
                    GenericActorWireCodecValues.EncodePosition(
                        destruction.Position));
                break;
            case GenericActorContext.EventPayload.LifeSpawned spawned:
                writer.Field(
                    1,
                    GenericActorWireCodecValues.EncodeIdentity(
                        spawned.ActorId));
                writer.Field(
                    2,
                    ActorWireValue.Int32(spawned.ParticipantId));
                writer.Optional(
                    3,
                    spawned.ParentActorId is null
                        ? null
                        : GenericActorWireCodecValues.EncodeIdentity(
                            spawned.ParentActorId));
                writer.Field(4, ActorWireValue.Int32(spawned.Generation));
                writer.Field(
                    5,
                    GenericActorWireCodecValues.SemanticId(spawned.FormId));
                writer.Field(6, ActorWireValue.Int32(spawned.Health));
                writer.Field(
                    7,
                    GenericActorWireCodecValues.EncodePosition(
                        spawned.Position));
                writer.Field(8, ActorWireValue.Enum(spawned.Reason));
                writer.Optional(
                    9,
                    spawned.SourceTransitionId is null
                        ? null
                        : GenericActorWireCodecValues.SemanticId(
                            spawned.SourceTransitionId));
                writer.Optional(
                    10,
                    spawned.SourceOperationId is null
                        ? null
                        : GenericActorWireCodecValues.Handle(
                            spawned.SourceOperationId));
                break;
            case GenericActorContext.EventPayload.LifeRetired retired:
                writer.Field(
                    1,
                    GenericActorWireCodecValues.EncodeIdentity(
                        retired.ActorId));
                writer.Field(2, ActorWireValue.Int32(retired.Generation));
                writer.Field(
                    3,
                    GenericActorWireCodecValues.SemanticId(retired.FormId));
                writer.Field(
                    4,
                    GenericActorWireCodecValues.EncodePosition(
                        retired.Position));
                writer.Field(
                    5,
                    GenericActorWireCodecValues.SemanticId(retired.Reason));
                writer.Optional(
                    6,
                    retired.SourceTransitionId is null
                        ? null
                        : GenericActorWireCodecValues.SemanticId(
                            retired.SourceTransitionId));
                writer.Optional(
                    7,
                    retired.SourceOperationId is null
                        ? null
                        : GenericActorWireCodecValues.Handle(
                            retired.SourceOperationId));
                break;
            case GenericActorContext.EventPayload.RuntimeFault fault:
                writer.Field(
                    1,
                    GenericActorWireActionCodec.EncodeRuntimeFault(
                        fault.Fault));
                break;
            case GenericActorContext.EventPayload.Participant participant:
                writer.Field(
                    1,
                    ActorWireValue.Int32(participant.ParticipantId));
                writer.Field(2, ActorWireValue.Int32(participant.TeamId));
                break;
            case GenericActorContext.EventPayload.Lifecycle lifecycle:
                writer.Field(
                    1,
                    GenericActorWireCodecValues.SemanticId(
                        lifecycle.TransitionId));
                writer.Field(
                    2,
                    GenericActorWireCodecValues.Handle(
                        lifecycle.OperationId));
                writer.Field(
                    3,
                    GenericActorWireCodecValues.EncodeIdentity(
                        lifecycle.SourceActorId));
                GenericActorWireCodecValues.OptionalInt32(
                    writer,
                    4,
                    lifecycle.TargetTeamId);
                GenericActorWireCodecValues.OptionalInt32(
                    writer,
                    5,
                    lifecycle.TargetUnitId);
                GenericActorWireCodecValues.OptionalInt32(
                    writer,
                    6,
                    lifecycle.DueTick);
                writer.Optional(
                    7,
                    lifecycle.CancellationReason is null
                        ? null
                        : GenericActorWireCodecValues.SemanticId(
                            lifecycle.CancellationReason));
                break;
            case GenericActorContext.EventPayload.FormTransition transition:
                writer.Field(
                    1,
                    GenericActorWireCodecValues.EncodeIdentity(
                        transition.ActorId));
                writer.Field(
                    2,
                    GenericActorWireCodecValues.SemanticId(
                        transition.TransitionId));
                writer.Field(
                    3,
                    GenericActorWireCodecValues.Handle(
                        transition.OperationId));
                writer.Field(
                    4,
                    GenericActorWireCodecValues.SemanticId(
                        transition.FromFormId));
                writer.Field(
                    5,
                    GenericActorWireCodecValues.SemanticId(
                        transition.ToFormId));
                writer.Field(
                    6,
                    ActorWireValue.Int32(transition.StartedTick));
                writer.Field(7, ActorWireValue.Int32(transition.DueTick));
                // Additive optional tag: a requested transition writes no
                // field, so an artifact compiled before automatic returns
                // decodes exactly the histories it always did.
                if (transition.Automatic)
                    writer.Field(8, ActorWireValue.Boolean(true));
                break;
            case GenericActorContext.EventPayload.ScoreChanged score:
                writer.Field(1, ActorWireValue.Int32(score.TeamId));
                writer.Field(
                    2,
                    GenericActorWireCodecValues.SemanticId(score.Channel));
                writer.Field(
                    3,
                    GenericActorWireCodecValues.Int64(score.NewValue));
                break;
            case GenericActorContext.EventPayload.ModeChanged mode:
                writer.Field(
                    1,
                    GenericActorWireObservationCodec.EncodeMode(mode.State));
                break;
            case GenericActorContext.EventPayload.LifecycleClockCancelled
                clock:
                writer.Field(
                    1,
                    ActorWireValue.Int32(clock.TargetTeamId));
                writer.Field(
                    2,
                    ActorWireValue.Int32(clock.TargetUnitId));
                writer.Field(
                    3,
                    GenericActorWireObservationCodec.EncodeUnitSlotState(
                        clock.CancelledState));
                writer.Field(
                    4,
                    GenericActorWireCodecValues.SemanticId(
                        clock.CancellationReason));
                break;
            default:
                throw new InvalidOperationException(
                    "Unknown generic actor event payload variant.");
        }
        return writer.ToArray();
    }

    private static GenericActorContext.EventPayload DecodePayload(
        GenericActorContext.EventKind kind,
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return GenericActorWireCodecValues.Decode<
            GenericActorContext.EventPayload>(
            () => kind switch
            {
                GenericActorContext.EventKind.Rotation =>
                    DecodeRotation(reader, depth),
                GenericActorContext.EventKind.Movement =>
                    DecodeMovement(reader, depth),
                GenericActorContext.EventKind.MovementBlocked =>
                    DecodeMovementBlocked(reader, depth),
                GenericActorContext.EventKind.Attack =>
                    DecodeAttack(reader, depth),
                GenericActorContext.EventKind.Damage =>
                    DecodeDamage(reader, depth),
                GenericActorContext.EventKind.ProjectileDeflected =>
                    DecodeProjectileDeflected(reader, depth),
                GenericActorContext.EventKind.Destruction =>
                    DecodeDestruction(reader, depth),
                GenericActorContext.EventKind.LifeSpawned =>
                    DecodeLifeSpawned(reader, depth),
                GenericActorContext.EventKind.LifeRetired =>
                    DecodeLifeRetired(reader, depth),
                GenericActorContext.EventKind.RuntimeFault =>
                    new GenericActorContext.EventPayload.RuntimeFault(
                        GenericActorWireActionCodec.DecodeRuntimeFault(
                            reader.Required(1),
                            depth + 1)),
                GenericActorContext.EventKind.ParticipantDisqualified =>
                    new GenericActorContext.EventPayload.Participant(
                        GenericActorWireCodecValues.Int32(reader, 1),
                        GenericActorWireCodecValues.Int32(reader, 2)),
                GenericActorContext.EventKind.LifecycleQueued
                    or GenericActorContext.EventKind.LifecycleCancelled
                    or GenericActorContext.EventKind.LifecycleCompleted =>
                    DecodeLifecycle(reader, depth),
                GenericActorContext.EventKind.FormTransitionStarted
                    or GenericActorContext.EventKind.FormTransitionCompleted
                    or GenericActorContext.EventKind.FormTransitionCancelled =>
                    DecodeFormTransition(reader, depth),
                GenericActorContext.EventKind.ScoreChanged =>
                    new GenericActorContext.EventPayload.ScoreChanged(
                        GenericActorWireCodecValues.Int32(reader, 1),
                        GenericActorWireCodecValues.SemanticId(
                            reader.Required(2)),
                        GenericActorWireCodecValues.Int64(
                            reader.Required(3))),
                GenericActorContext.EventKind.ModeChanged =>
                    new GenericActorContext.EventPayload.ModeChanged(
                        GenericActorWireObservationCodec.DecodeMode(
                            reader.Required(1),
                            depth + 1)),
                GenericActorContext.EventKind.LifecycleClockCancelled =>
                    new GenericActorContext.EventPayload
                        .LifecycleClockCancelled(
                            GenericActorWireCodecValues.Int32(reader, 1),
                            GenericActorWireCodecValues.Int32(reader, 2),
                            GenericActorWireObservationCodec
                                .DecodeUnitSlotState(
                                    reader.Required(3),
                                    depth + 1),
                            GenericActorWireCodecValues.SemanticId(
                                reader.Required(4))),
                _ => throw new FormatException(
                    "Unknown generic actor event discriminator."),
            },
            "event payload");
    }

    private static GenericActorContext.EventPayload.Rotation DecodeRotation(
        ActorWireObjectReader reader,
        int depth) =>
        new(
            GenericActorWireCodecValues.DecodeIdentity(
                reader.Required(1),
                depth + 1),
            GenericActorWireActionCodec.DecodeResolvedAction(
                reader.Required(2),
                depth + 1),
            GenericActorWireCodecValues.DecodePosition(
                reader.Required(3),
                depth + 1),
            GenericActorWireCodecValues.Enum<Direction>(reader, 4),
            GenericActorWireCodecValues.Enum<Direction>(reader, 5));

    private static GenericActorContext.EventPayload.Movement DecodeMovement(
        ActorWireObjectReader reader,
        int depth) =>
        new(
            GenericActorWireCodecValues.DecodeIdentity(
                reader.Required(1),
                depth + 1),
            GenericActorWireActionCodec.DecodeResolvedAction(
                reader.Required(2),
                depth + 1),
            GenericActorWireCodecValues.DecodePosition(
                reader.Required(3),
                depth + 1),
            GenericActorWireCodecValues.DecodePosition(
                reader.Required(4),
                depth + 1),
            GenericActorWireCodecValues.Enum<Direction>(reader, 5));

    private static GenericActorContext.EventPayload.MovementBlocked
        DecodeMovementBlocked(
            ActorWireObjectReader reader,
            int depth) =>
        new(
            GenericActorWireCodecValues.DecodeIdentity(
                reader.Required(1),
                depth + 1),
            GenericActorWireActionCodec.DecodeResolvedAction(
                reader.Required(2),
                depth + 1),
            GenericActorWireCodecValues.DecodePosition(
                reader.Required(3),
                depth + 1),
            GenericActorWireCodecValues.DecodePosition(
                reader.Required(4),
                depth + 1),
            GenericActorWireCodecValues.Enum<Direction>(reader, 5));

    private static GenericActorContext.EventPayload.Attack DecodeAttack(
        ActorWireObjectReader reader,
        int depth) =>
        new(
            GenericActorWireCodecValues.DecodeIdentity(
                reader.Required(1),
                depth + 1),
            GenericActorWireActionCodec.DecodeResolvedAction(
                reader.Required(2),
                depth + 1),
            GenericActorWireCodecValues.Int64(reader.Required(3)),
            GenericActorWireCodecValues.DecodePosition(
                reader.Required(4),
                depth + 1),
            GenericActorWireCodecValues.Enum<ProjectileHeading>(reader, 5));

    private static GenericActorContext.EventPayload.Damage DecodeDamage(
        ActorWireObjectReader reader,
        int depth)
    {
        byte[]? sourceActorId = reader.Optional(2);
        return new GenericActorContext.EventPayload.Damage(
            GenericActorWireCodecValues.Int32(reader, 1),
            sourceActorId is null
                ? null
                : GenericActorWireCodecValues.DecodeIdentity(
                    sourceActorId,
                    depth + 1),
            GenericActorWireCodecValues.DecodeIdentity(
                reader.Required(3),
                depth + 1),
            GenericActorWireCodecValues.Int64(reader.Required(4)),
            GenericActorWireCodecValues.Int32(reader, 5),
            GenericActorWireCodecValues.Int32(reader, 6),
            GenericActorWireCodecValues.DecodePosition(
                reader.Required(7),
                depth + 1));
    }

    private static GenericActorContext.EventPayload.ProjectileDeflected
        DecodeProjectileDeflected(
            ActorWireObjectReader reader,
            int depth)
    {
        byte[]? sourceActorId = reader.Optional(2);
        return new GenericActorContext.EventPayload.ProjectileDeflected(
            GenericActorWireCodecValues.Int32(reader, 1),
            sourceActorId is null
                ? null
                : GenericActorWireCodecValues.DecodeIdentity(
                    sourceActorId,
                    depth + 1),
            GenericActorWireCodecValues.DecodeIdentity(
                reader.Required(3),
                depth + 1),
            GenericActorWireCodecValues.Int64(reader.Required(4)),
            GenericActorWireCodecValues.Int64(reader.Required(9)),
            GenericActorWireCodecValues.SemanticId(reader.Required(5)),
            GenericActorWireCodecValues.Enum<Direction>(reader, 6),
            GenericActorWireCodecValues.Enum<ProjectileHeading>(reader, 7),
            GenericActorWireCodecValues.DecodePosition(
                reader.Required(8),
                depth + 1));
    }

    private static GenericActorContext.EventPayload.Destruction
        DecodeDestruction(
            ActorWireObjectReader reader,
            int depth)
    {
        byte[]? sourceActorId = reader.Optional(3);
        byte[]? projectileId = reader.Optional(4);
        return new GenericActorContext.EventPayload.Destruction(
            GenericActorWireCodecValues.DecodeIdentity(
                reader.Required(1),
                depth + 1),
            GenericActorWireCodecValues.OptionalInt32(reader, 2),
            sourceActorId is null
                ? null
                : GenericActorWireCodecValues.DecodeIdentity(
                    sourceActorId,
                    depth + 1),
            projectileId is null
                ? null
                : GenericActorWireCodecValues.Int64(projectileId),
            GenericActorWireCodecValues.Int32(reader, 5),
            GenericActorWireCodecValues.SemanticId(reader.Required(6)),
            GenericActorWireCodecValues.DecodePosition(
                reader.Required(7),
                depth + 1));
    }

    private static GenericActorContext.EventPayload.LifeSpawned
        DecodeLifeSpawned(
            ActorWireObjectReader reader,
            int depth)
    {
        byte[]? parentActorId = reader.Optional(3);
        byte[]? transitionId = reader.Optional(9);
        byte[]? operationId = reader.Optional(10);
        return new GenericActorContext.EventPayload.LifeSpawned(
            GenericActorWireCodecValues.DecodeIdentity(
                reader.Required(1),
                depth + 1),
            GenericActorWireCodecValues.Int32(reader, 2),
            parentActorId is null
                ? null
                : GenericActorWireCodecValues.DecodeIdentity(
                    parentActorId,
                    depth + 1),
            GenericActorWireCodecValues.Int32(reader, 4),
            GenericActorWireCodecValues.SemanticId(reader.Required(5)),
            GenericActorWireCodecValues.Int32(reader, 6),
            GenericActorWireCodecValues.DecodePosition(
                reader.Required(7),
                depth + 1),
            GenericActorWireCodecValues.Enum<
                GenericActorMatchStart.SpawnReason>(reader, 8),
            transitionId is null
                ? null
                : GenericActorWireCodecValues.SemanticId(transitionId),
            operationId is null
                ? null
                : GenericActorWireCodecValues.Handle(operationId));
    }

    private static GenericActorContext.EventPayload.LifeRetired
        DecodeLifeRetired(
            ActorWireObjectReader reader,
            int depth)
    {
        byte[]? transitionId = reader.Optional(6);
        byte[]? operationId = reader.Optional(7);
        return new GenericActorContext.EventPayload.LifeRetired(
            GenericActorWireCodecValues.DecodeIdentity(
                reader.Required(1),
                depth + 1),
            GenericActorWireCodecValues.Int32(reader, 2),
            GenericActorWireCodecValues.SemanticId(reader.Required(3)),
            GenericActorWireCodecValues.DecodePosition(
                reader.Required(4),
                depth + 1),
            GenericActorWireCodecValues.SemanticId(reader.Required(5)),
            transitionId is null
                ? null
                : GenericActorWireCodecValues.SemanticId(transitionId),
            operationId is null
                ? null
                : GenericActorWireCodecValues.Handle(operationId));
    }

    private static GenericActorContext.EventPayload.Lifecycle
        DecodeLifecycle(
            ActorWireObjectReader reader,
            int depth)
    {
        byte[]? cancellationReason = reader.Optional(7);
        return new GenericActorContext.EventPayload.Lifecycle(
            GenericActorWireCodecValues.SemanticId(reader.Required(1)),
            GenericActorWireCodecValues.Handle(reader.Required(2)),
            GenericActorWireCodecValues.DecodeIdentity(
                reader.Required(3),
                depth + 1),
            GenericActorWireCodecValues.OptionalInt32(reader, 4),
            GenericActorWireCodecValues.OptionalInt32(reader, 5),
            GenericActorWireCodecValues.OptionalInt32(reader, 6),
            cancellationReason is null
                ? null
                : GenericActorWireCodecValues.SemanticId(
                    cancellationReason));
    }

    private static GenericActorContext.EventPayload.FormTransition
        DecodeFormTransition(
            ActorWireObjectReader reader,
            int depth)
    {
        byte[]? automatic = reader.Optional(8);
        return new GenericActorContext.EventPayload.FormTransition(
            GenericActorWireCodecValues.DecodeIdentity(
                reader.Required(1),
                depth + 1),
            GenericActorWireCodecValues.SemanticId(reader.Required(2)),
            GenericActorWireCodecValues.Handle(reader.Required(3)),
            GenericActorWireCodecValues.SemanticId(reader.Required(4)),
            GenericActorWireCodecValues.SemanticId(reader.Required(5)),
            GenericActorWireCodecValues.Int32(reader, 6),
            GenericActorWireCodecValues.Int32(reader, 7),
            automatic is null
                ? false
                : ActorWireValue.Boolean(automatic)
                    ? true
                    : throw new FormatException(
                        "A requested form transition omits the automatic "
                        + "field; an explicit false is a second encoding of "
                        + "the same event."));
    }
}
