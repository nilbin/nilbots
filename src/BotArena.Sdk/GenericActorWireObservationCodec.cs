using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>Tagged schema-3 codec for one generic actor observation.</summary>
internal static class GenericActorWireObservationCodec
{
    public static byte[] Encode(GenericActorContext value)
    {
        ArgumentNullException.ThrowIfNull(value);
        bool schema3 = value.SchemaVersion
            >= GenericActorContractVersions.ObservationSchemaVersion;
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.SchemaVersion));
        writer.Field(2, ActorWireValue.Int32(value.Tick));
        writer.Field(
            3,
            GenericActorWireCodecValues.Fingerprint(
                value.MatchContractFingerprint));
        writer.Field(4, EncodeSelf(value.Self, schema3));
        writer.Field(5, Array(value.TeamUnits, EncodeUnitSlot));
        writer.Field(
            6,
            Array(
                value.Participants,
                participant => EncodeParticipant(participant, schema3)));
        writer.Field(
            7,
            Array(value.Allies, ally => EncodeAlly(ally, schema3)));
        writer.Field(
            8,
            Array(value.Enemies, enemy => EncodeEnemy(enemy, schema3)));
        writer.Field(
            9,
            Array(value.VisibleTiles, tile => EncodeTile(tile, schema3)));
        writer.Optional(
            10,
            value.VisibleProjectiles is { } projectiles
                ? Array(
                    projectiles,
                    projectile => EncodeProjectile(projectile, schema3))
                : null);
        writer.Field(
            11,
            Array(value.VisibleEvents, GenericActorWireEventCodec.EncodeEvent));
        writer.Optional(
            12,
            value.HeardSounds is { } sounds
                ? Array(sounds, GenericActorWireEventCodec.EncodeSound)
                : null);
        writer.Field(13, EncodeScoreboard(value.Scoreboard));
        writer.Field(14, EncodeMode(value.Mode, schema3));
        writer.Field(
            15,
            Array(
                value.ActionLegalities,
                GenericActorWireActionCodec.EncodeLegality));
        return GenericActorWireCodecValues.RequirePayloadLimit(
            writer.ToArray(),
            GenericActorWireCodecValues.MaximumHostPayloadBytes,
            "Generic actor observation");
    }

    public static GenericActorContext Decode(byte[] bytes, int depth = 0)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        GenericActorWireCodecValues.RequirePayloadLimit(
            bytes,
            GenericActorWireCodecValues.MaximumHostPayloadBytes,
            "Generic actor observation",
            decoding: true);
        var reader = new ActorWireObjectReader(bytes, depth);
        int schemaVersion =
            GenericActorWireCodecValues.Int32(reader, 1);
        bool schema3 = schemaVersion
            >= GenericActorContractVersions.ObservationSchemaVersion;
        byte[]? projectiles = reader.Optional(10);
        byte[]? sounds = reader.Optional(12);
        return GenericActorWireCodecValues.Decode(
            () => new GenericActorContext(
                schemaVersion,
                GenericActorWireCodecValues.Int32(reader, 2),
                GenericActorWireCodecValues.Fingerprint(reader.Required(3)),
                DecodeSelf(reader.Required(4), depth + 1),
                Array(
                    reader,
                    5,
                    item => DecodeUnitSlot(item, depth + 1)),
                Array(
                    reader,
                    6,
                    item => DecodeParticipant(item, depth + 1)),
                Array(
                    reader,
                    7,
                    item => DecodeAlly(item, depth + 1)),
                Array(
                    reader,
                    8,
                    item => DecodeEnemy(item, depth + 1)),
                Array(
                    reader,
                    9,
                    item => DecodeTile(item, depth + 1)),
                projectiles is null
                    ? null
                    : ActorWireValue.Array(
                        projectiles,
                        item => DecodeProjectile(
                            item,
                            depth + 1,
                            schema3)),
                Array(
                    reader,
                    11,
                    item => GenericActorWireEventCodec.DecodeEvent(
                        item,
                        depth + 1)),
                sounds is null
                    ? null
                    : ActorWireValue.Array(
                        sounds,
                        item => GenericActorWireEventCodec.DecodeSound(
                            item,
                            depth + 1)),
                DecodeScoreboard(reader.Required(13), depth + 1),
                DecodeMode(reader.Required(14), depth + 1),
                Array(
                    reader,
                    15,
                    item => GenericActorWireActionCodec.DecodeLegality(
                        item,
                        depth + 1))),
            "observation");
    }

    internal static byte[] EncodeMode(
        GenericActorContext.ModeObservationState value,
        bool schema3 = true)
    {
        ArgumentNullException.ThrowIfNull(value);
        var writer = new ActorWireObjectWriter();
        writer.Field(1, GenericActorWireCodecValues.SemanticId(value.ModeId));
        writer.Field(2, ActorWireValue.Enum(value.Kind));
        writer.Field(3, EncodeModePayload(value, schema3));
        return writer.ToArray();
    }

    internal static GenericActorContext.ModeObservationState DecodeMode(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        string modeId =
            GenericActorWireCodecValues.SemanticId(reader.Required(1));
        GenericActorRulesContract.GameModeKind kind =
            GenericActorWireCodecValues.Enum<
                GenericActorRulesContract.GameModeKind>(reader, 2);
        var payload = new ActorWireObjectReader(
            reader.Required(3),
            depth + 1);
        return GenericActorWireCodecValues.Decode<
            GenericActorContext.ModeObservationState>(
            () => kind switch
            {
                GenericActorRulesContract.GameModeKind.Deathmatch =>
                    new GenericActorContext.ModeObservationState.Deathmatch(
                        modeId),
                GenericActorRulesContract.GameModeKind.Frontline =>
                    DecodeFrontlineMode(modeId, payload),
                _ => throw new FormatException(
                    "Unknown generic actor mode discriminator."),
            },
            "mode observation");
    }

    private static byte[] EncodeSelf(
        GenericActorContext.ObservedSelfState value,
        bool schema3)
    {
        var writer = new ActorWireObjectWriter();
        EncodeBody(
            writer,
            value.ActorId,
            value.Generation,
            value.FormId,
            value.Position,
            value.Facing,
            value.Health,
            value.Cooldown,
            value.Energy,
            value.PreviousActionResolution,
            value.PendingSameLifeTransition,
            schema3 ? value.ClassId : null);
        return writer.ToArray();
    }

    private static GenericActorContext.ObservedSelfState DecodeSelf(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? resolution = reader.Optional(9);
        byte[]? transition = reader.Optional(10);
        byte[]? classId = reader.Optional(11);
        return GenericActorWireCodecValues.Decode(
            () => new GenericActorContext.ObservedSelfState(
                GenericActorWireCodecValues.DecodeIdentity(
                    reader.Required(1),
                    depth + 1),
                GenericActorWireCodecValues.Int32(reader, 2),
                GenericActorWireCodecValues.SemanticId(reader.Required(3)),
                GenericActorWireCodecValues.DecodePosition(
                    reader.Required(4),
                    depth + 1),
                GenericActorWireCodecValues.Enum<Direction>(reader, 5),
                GenericActorWireCodecValues.Int32(reader, 6),
                GenericActorWireCodecValues.Int32(reader, 7),
                GenericActorWireCodecValues.OptionalInt32(reader, 8),
                resolution is null
                    ? null
                    : GenericActorWireActionCodec.DecodeResolution(
                        resolution,
                        depth + 1),
                transition is null
                    ? null
                    : DecodePendingTransition(
                        transition,
                        depth + 1),
                classId is null
                    ? null
                    : GenericActorWireCodecValues.SemanticId(classId)),
            "self observation");
    }

    private static byte[] EncodeAlly(
        GenericActorContext.ObservedAllyState value,
        bool schema3)
    {
        var writer = new ActorWireObjectWriter();
        EncodeBody(
            writer,
            value.ActorId,
            value.Generation,
            value.FormId,
            value.Position,
            value.Facing,
            value.Health,
            value.Cooldown,
            value.Energy,
            value.PreviousActionResolution,
            value.PendingSameLifeTransition,
            schema3 ? value.ClassId : null);
        return writer.ToArray();
    }

    private static GenericActorContext.ObservedAllyState DecodeAlly(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? resolution = reader.Optional(9);
        byte[]? transition = reader.Optional(10);
        byte[]? classId = reader.Optional(11);
        return GenericActorWireCodecValues.Decode(
            () => new GenericActorContext.ObservedAllyState(
                GenericActorWireCodecValues.DecodeIdentity(
                    reader.Required(1),
                    depth + 1),
                GenericActorWireCodecValues.Int32(reader, 2),
                GenericActorWireCodecValues.SemanticId(reader.Required(3)),
                GenericActorWireCodecValues.DecodePosition(
                    reader.Required(4),
                    depth + 1),
                GenericActorWireCodecValues.Enum<Direction>(reader, 5),
                GenericActorWireCodecValues.Int32(reader, 6),
                GenericActorWireCodecValues.Int32(reader, 7),
                GenericActorWireCodecValues.OptionalInt32(reader, 8),
                resolution is null
                    ? null
                    : GenericActorWireActionCodec.DecodeResolution(
                        resolution,
                        depth + 1),
                transition is null
                    ? null
                    : DecodePendingTransition(
                        transition,
                        depth + 1),
                classId is null
                    ? null
                    : GenericActorWireCodecValues.SemanticId(classId)),
            "ally observation");
    }

    private static void EncodeBody(
        ActorWireObjectWriter writer,
        ActorIdentity actorId,
        int generation,
        string formId,
        Position position,
        Direction facing,
        int health,
        int cooldown,
        int? energy,
        GenericActorActionResolution? previousActionResolution,
        GenericActorContext.PendingSameLifeTransition?
            pendingSameLifeTransition,
        string? classId)
    {
        writer.Field(
            1,
            GenericActorWireCodecValues.EncodeIdentity(actorId));
        writer.Field(2, ActorWireValue.Int32(generation));
        writer.Field(3, GenericActorWireCodecValues.SemanticId(formId));
        writer.Field(
            4,
            GenericActorWireCodecValues.EncodePosition(position));
        writer.Field(5, ActorWireValue.Enum(facing));
        writer.Field(6, ActorWireValue.Int32(health));
        writer.Field(7, ActorWireValue.Int32(cooldown));
        GenericActorWireCodecValues.OptionalInt32(writer, 8, energy);
        writer.Optional(
            9,
            previousActionResolution is null
                ? null
                : GenericActorWireActionCodec.EncodeResolution(
                    previousActionResolution));
        writer.Optional(
            10,
            pendingSameLifeTransition is null
                ? null
                : EncodePendingTransition(pendingSameLifeTransition));
        writer.Optional(
            11,
            classId is null
                ? null
                : GenericActorWireCodecValues.SemanticId(classId));
    }

    private static byte[] EncodePendingTransition(
        GenericActorContext.PendingSameLifeTransition value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(
            1,
            GenericActorWireCodecValues.SemanticId(value.TransitionId));
        writer.Field(
            2,
            GenericActorWireCodecValues.Handle(value.OperationId));
        writer.Field(
            3,
            GenericActorWireCodecValues.SemanticId(value.TargetFormId));
        writer.Field(4, ActorWireValue.Int32(value.StartedTick));
        writer.Field(5, ActorWireValue.Int32(value.DueTick));
        return writer.ToArray();
    }

    private static GenericActorContext.PendingSameLifeTransition
        DecodePendingTransition(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return GenericActorWireCodecValues.Decode(
            () => new GenericActorContext.PendingSameLifeTransition(
                GenericActorWireCodecValues.SemanticId(reader.Required(1)),
                GenericActorWireCodecValues.Handle(reader.Required(2)),
                GenericActorWireCodecValues.SemanticId(reader.Required(3)),
                GenericActorWireCodecValues.Int32(reader, 4),
                GenericActorWireCodecValues.Int32(reader, 5)),
            "pending same-life transition");
    }

    private static byte[] EncodeUnitSlot(
        GenericActorContext.ObservedUnitSlot value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.TeamId));
        writer.Field(2, ActorWireValue.Int32(value.UnitId));
        writer.Field(3, EncodeUnitSlotState(value.State));
        return writer.ToArray();
    }

    private static GenericActorContext.ObservedUnitSlot DecodeUnitSlot(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return GenericActorWireCodecValues.Decode(
            () => new GenericActorContext.ObservedUnitSlot(
                GenericActorWireCodecValues.Int32(reader, 1),
                GenericActorWireCodecValues.Int32(reader, 2),
                DecodeUnitSlotState(reader.Required(3), depth + 1)),
            "unit-slot observation");
    }

    internal static byte[] EncodeUnitSlotState(
        GenericActorContext.UnitSlotState value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Enum(value.Kind));
        writer.Field(2, EncodeUnitSlotStatePayload(value));
        return writer.ToArray();
    }

    internal static GenericActorContext.UnitSlotState DecodeUnitSlotState(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        GenericActorContext.UnitSlotStateKind kind =
            GenericActorWireCodecValues.Enum<
                GenericActorContext.UnitSlotStateKind>(reader, 1);
        var payload = new ActorWireObjectReader(
            reader.Required(2),
            depth + 1);
        return GenericActorWireCodecValues.Decode<
            GenericActorContext.UnitSlotState>(
            () => kind switch
            {
                GenericActorContext.UnitSlotStateKind.Active =>
                    new GenericActorContext.UnitSlotState.Active(
                        GenericActorWireCodecValues.DecodeIdentity(
                            payload.Required(1),
                            depth + 2),
                        GenericActorWireCodecValues.Int32(payload, 2),
                        GenericActorWireCodecValues.SemanticId(
                            payload.Required(3))),
                GenericActorContext.UnitSlotStateKind.AvailabilityPending =>
                    new GenericActorContext.UnitSlotState.AvailabilityPending(
                        GenericActorWireCodecValues.Enum<
                            GenericActorContext.AvailabilityReason>(
                                payload,
                                1),
                        GenericActorWireCodecValues.Int32(payload, 2)),
                GenericActorContext.UnitSlotStateKind.AutomaticReturnPending =>
                    new GenericActorContext.UnitSlotState
                        .AutomaticReturnPending(
                            GenericActorWireCodecValues.Int32(payload, 1),
                            GenericActorWireCodecValues.SemanticId(
                                payload.Required(2)),
                            GenericActorWireCodecValues.Int32(payload, 3)),
                GenericActorContext.UnitSlotStateKind.Ready =>
                    new GenericActorContext.UnitSlotState.Ready(),
                GenericActorContext.UnitSlotStateKind.FabricationPending =>
                    DecodeFabricationPending(payload, depth + 2),
                GenericActorContext.UnitSlotStateKind.ReplicationPending =>
                    DecodeReplicationPending(payload, depth + 2),
                GenericActorContext.UnitSlotStateKind.PermanentlyDormant =>
                    new GenericActorContext.UnitSlotState.PermanentlyDormant(),
                _ => throw new FormatException(
                    "Unknown generic actor unit-slot state discriminator."),
            },
            "unit-slot state");
    }

    private static byte[] EncodeUnitSlotStatePayload(
        GenericActorContext.UnitSlotState value)
    {
        var writer = new ActorWireObjectWriter();
        switch (value)
        {
            case GenericActorContext.UnitSlotState.Active active:
                writer.Field(
                    1,
                    GenericActorWireCodecValues.EncodeIdentity(
                        active.ActorId));
                writer.Field(2, ActorWireValue.Int32(active.Generation));
                writer.Field(
                    3,
                    GenericActorWireCodecValues.SemanticId(active.FormId));
                break;
            case GenericActorContext.UnitSlotState.AvailabilityPending pending:
                writer.Field(1, ActorWireValue.Enum(pending.Reason));
                writer.Field(2, ActorWireValue.Int32(pending.DueTick));
                break;
            case GenericActorContext.UnitSlotState.AutomaticReturnPending
                    pending:
                writer.Field(1, ActorWireValue.Int32(pending.DueTick));
                writer.Field(
                    2,
                    GenericActorWireCodecValues.SemanticId(
                        pending.TargetFormId));
                writer.Field(3, ActorWireValue.Int32(pending.Generation));
                break;
            case GenericActorContext.UnitSlotState.Ready:
            case GenericActorContext.UnitSlotState.PermanentlyDormant:
                break;
            case GenericActorContext.UnitSlotState.LifecyclePending pending:
                EncodeLifecyclePending(writer, pending);
                break;
            default:
                throw new InvalidOperationException(
                    "Unknown generic actor unit-slot state variant.");
        }
        return writer.ToArray();
    }

    private static void EncodeLifecyclePending(
        ActorWireObjectWriter writer,
        GenericActorContext.UnitSlotState.LifecyclePending value)
    {
        writer.Field(1, ActorWireValue.Int32(value.DueTick));
        writer.Field(
            2,
            GenericActorWireCodecValues.EncodeIdentity(value.SourceActorId));
        writer.Field(
            3,
            GenericActorWireCodecValues.SemanticId(value.TransitionId));
        writer.Field(
            4,
            GenericActorWireCodecValues.Handle(value.OperationId));
        writer.Field(
            5,
            GenericActorWireCodecValues.SemanticId(value.TargetFormId));
        writer.Field(
            6,
            GenericActorWireCodecValues.EncodePosition(
                value.ReservedPosition));
    }

    private static GenericActorContext.UnitSlotState.FabricationPending
        DecodeFabricationPending(
            ActorWireObjectReader reader,
            int depth) =>
        new(
            GenericActorWireCodecValues.Int32(reader, 1),
            GenericActorWireCodecValues.DecodeIdentity(
                reader.Required(2),
                depth + 1),
            GenericActorWireCodecValues.SemanticId(reader.Required(3)),
            GenericActorWireCodecValues.Handle(reader.Required(4)),
            GenericActorWireCodecValues.SemanticId(reader.Required(5)),
            GenericActorWireCodecValues.DecodePosition(
                reader.Required(6),
                depth + 1));

    private static GenericActorContext.UnitSlotState.ReplicationPending
        DecodeReplicationPending(
            ActorWireObjectReader reader,
            int depth) =>
        new(
            GenericActorWireCodecValues.Int32(reader, 1),
            GenericActorWireCodecValues.DecodeIdentity(
                reader.Required(2),
                depth + 1),
            GenericActorWireCodecValues.SemanticId(reader.Required(3)),
            GenericActorWireCodecValues.Handle(reader.Required(4)),
            GenericActorWireCodecValues.SemanticId(reader.Required(5)),
            GenericActorWireCodecValues.DecodePosition(
                reader.Required(6),
                depth + 1));

    private static byte[] EncodeParticipant(
        GenericActorContext.ObservedParticipantStatus value,
        bool schema3)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.ParticipantId));
        writer.Field(2, ActorWireValue.Int32(value.TeamId));
        writer.Field(
            3,
            GenericActorWireCodecValues.Int64(value.RuntimeFaultCount));
        writer.Field(4, ActorWireValue.Boolean(value.Disqualified));
        if (schema3)
        {
            writer.Optional(
                5,
                value.ClassId is null
                    ? null
                    : GenericActorWireCodecValues.SemanticId(value.ClassId));
        }
        return writer.ToArray();
    }

    private static GenericActorContext.ObservedParticipantStatus
        DecodeParticipant(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? classId = reader.Optional(5);
        return GenericActorWireCodecValues.Decode(
            () => new GenericActorContext.ObservedParticipantStatus(
                GenericActorWireCodecValues.Int32(reader, 1),
                GenericActorWireCodecValues.Int32(reader, 2),
                GenericActorWireCodecValues.Int64(reader.Required(3)),
                GenericActorWireCodecValues.Boolean(reader, 4),
                classId is null
                    ? null
                    : GenericActorWireCodecValues.SemanticId(classId)),
            "participant observation");
    }

    private static byte[] EncodeEnemy(
        GenericActorContext.ObservedEnemyState value,
        bool schema3)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(
            1,
            GenericActorWireCodecValues.EncodeIdentity(value.ActorId));
        writer.Field(
            2,
            GenericActorWireCodecValues.SemanticId(value.FormId));
        writer.Field(
            3,
            GenericActorWireCodecValues.EncodePosition(value.Position));
        writer.Field(4, ActorWireValue.Enum(value.Facing));
        writer.Field(5, ActorWireValue.Int32(value.Health));
        writer.Optional(
            6,
            value.PendingSameLifeTransition is null
                ? null
                : EncodePendingTransition(
                    value.PendingSameLifeTransition));
        writer.Field(
            7,
            Array(
                value.ObservedBy,
                GenericActorWireCodecValues.EncodeIdentity));
        if (schema3)
        {
            writer.Optional(
                8,
                value.ClassId is null
                    ? null
                    : GenericActorWireCodecValues.SemanticId(value.ClassId));
        }
        return writer.ToArray();
    }

    private static GenericActorContext.ObservedEnemyState DecodeEnemy(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? transition = reader.Optional(6);
        byte[]? classId = reader.Optional(8);
        return GenericActorWireCodecValues.Decode(
            () => new GenericActorContext.ObservedEnemyState(
                GenericActorWireCodecValues.DecodeIdentity(
                    reader.Required(1),
                    depth + 1),
                GenericActorWireCodecValues.SemanticId(reader.Required(2)),
                GenericActorWireCodecValues.DecodePosition(
                    reader.Required(3),
                    depth + 1),
                GenericActorWireCodecValues.Enum<Direction>(reader, 4),
                GenericActorWireCodecValues.Int32(reader, 5),
                transition is null
                    ? null
                    : DecodePendingTransition(
                        transition,
                        depth + 1),
                Array(
                    reader,
                    7,
                    item => GenericActorWireCodecValues.DecodeIdentity(
                        item,
                        depth + 1)),
                classId is null
                    ? null
                    : GenericActorWireCodecValues.SemanticId(classId)),
            "enemy observation");
    }

    private static byte[] EncodeTile(
        GenericActorContext.ObservedTile value,
        bool schema3)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(
            1,
            GenericActorWireCodecValues.EncodePosition(value.Position));
        writer.Field(2, ActorWireValue.Boolean(value.IsWall));
        writer.Field(
            3,
            Array(
                value.ObservedBy,
                GenericActorWireCodecValues.EncodeIdentity));
        if (schema3)
        {
            writer.Optional(
                4,
                value.SpawnReservation is null
                    ? null
                    : EncodeSpawnReservation(value.SpawnReservation));
        }
        return writer.ToArray();
    }

    private static GenericActorContext.ObservedTile DecodeTile(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? spawnReservation = reader.Optional(4);
        return GenericActorWireCodecValues.Decode(
            () => new GenericActorContext.ObservedTile(
                GenericActorWireCodecValues.DecodePosition(
                    reader.Required(1),
                    depth + 1),
                GenericActorWireCodecValues.Boolean(reader, 2),
                Array(
                    reader,
                    3,
                    item => GenericActorWireCodecValues.DecodeIdentity(
                        item,
                        depth + 1)),
                spawnReservation is null
                    ? null
                    : DecodeSpawnReservation(
                        spawnReservation,
                        depth + 1)),
            "tile observation");
    }

    private static byte[] EncodeProjectile(
        GenericActorContext.ObservedProjectile value,
        bool schema3)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(
            1,
            GenericActorWireCodecValues.Int64(value.ProjectileId));
        writer.Field(2, ActorWireValue.Int32(value.OwnerTeamId));
        writer.Optional(
            3,
            value.OwnerActorId is null
                ? null
                : GenericActorWireCodecValues.EncodeIdentity(
                    value.OwnerActorId));
        writer.Field(
            4,
            GenericActorWireCodecValues.EncodePosition(value.Position));
        writer.Field(5, ActorWireValue.Enum(value.Heading));
        writer.Field(6, ActorWireValue.Int32(value.TilesPerAdvance));
        writer.Field(7, ActorWireValue.Int32(value.TicksUntilAdvance));
        writer.Field(8, ActorWireValue.Int32(value.RemainingTiles));
        writer.Field(
            9,
            Array(
                value.ObservedBy,
                GenericActorWireCodecValues.EncodeIdentity));
        if (schema3)
        {
            writer.Field(10, ActorWireValue.Int32(value.TicksPerAdvance));
            writer.Field(11, ActorWireValue.Int32(value.Damage));
        }
        return writer.ToArray();
    }

    private static GenericActorContext.ObservedProjectile DecodeProjectile(
        byte[] bytes,
        int depth,
        bool schema3)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? ownerActorId = reader.Optional(3);
        byte[]? ticksPerAdvance = reader.Optional(10);
        byte[]? damage = reader.Optional(11);
        if (schema3 && (ticksPerAdvance is null || damage is null))
        {
            throw new FormatException(
                "Schema-3 projectile observations require timing and damage.");
        }
        if (!schema3 && (ticksPerAdvance is not null || damage is not null))
        {
            throw new FormatException(
                "Schema-2 projectile observations cannot encode schema-3 fields.");
        }
        int ticksUntilAdvance =
            GenericActorWireCodecValues.Int32(reader, 7);
        return GenericActorWireCodecValues.Decode(
            () => new GenericActorContext.ObservedProjectile(
                GenericActorWireCodecValues.Int64(reader.Required(1)),
                GenericActorWireCodecValues.Int32(reader, 2),
                ownerActorId is null
                    ? null
                    : GenericActorWireCodecValues.DecodeIdentity(
                        ownerActorId,
                        depth + 1),
                GenericActorWireCodecValues.DecodePosition(
                    reader.Required(4),
                    depth + 1),
                GenericActorWireCodecValues.Enum<ProjectileHeading>(
                    reader,
                    5),
                GenericActorWireCodecValues.Int32(reader, 6),
                ticksPerAdvance is null
                    ? ticksUntilAdvance
                    : ActorWireValue.Int32(ticksPerAdvance),
                ticksUntilAdvance,
                GenericActorWireCodecValues.Int32(reader, 8),
                damage is null ? 1 : ActorWireValue.Int32(damage),
                Array(
                    reader,
                    9,
                    item => GenericActorWireCodecValues.DecodeIdentity(
                        item,
                        depth + 1))),
            "projectile observation");
    }

    private static byte[] EncodeScoreboard(
        GenericActorContext.ScoreboardState value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, Array(value.Teams, EncodeTeamScore));
        return writer.ToArray();
    }

    private static GenericActorContext.ScoreboardState DecodeScoreboard(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return GenericActorWireCodecValues.Decode(
            () => new GenericActorContext.ScoreboardState(
                Array(
                    reader,
                    1,
                    item => DecodeTeamScore(item, depth + 1))),
            "scoreboard observation");
    }

    private static byte[] EncodeTeamScore(
        GenericActorContext.TeamScoreState value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.TeamId));
        writer.Field(2, ActorWireValue.Boolean(value.Eligible));
        writer.Field(3, Array(value.Scores, EncodeScoreValue));
        return writer.ToArray();
    }

    private static GenericActorContext.TeamScoreState DecodeTeamScore(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return GenericActorWireCodecValues.Decode(
            () => new GenericActorContext.TeamScoreState(
                GenericActorWireCodecValues.Int32(reader, 1),
                GenericActorWireCodecValues.Boolean(reader, 2),
                Array(
                    reader,
                    3,
                    item => DecodeScoreValue(item, depth + 1))),
            "team-score observation");
    }

    private static byte[] EncodeScoreValue(
        GenericActorContext.ScoreValue value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(
            1,
            GenericActorWireCodecValues.SemanticId(value.Channel));
        writer.Field(2, GenericActorWireCodecValues.Int64(value.Value));
        return writer.ToArray();
    }

    private static GenericActorContext.ScoreValue DecodeScoreValue(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return GenericActorWireCodecValues.Decode(
            () => new GenericActorContext.ScoreValue(
                GenericActorWireCodecValues.SemanticId(reader.Required(1)),
                GenericActorWireCodecValues.Int64(reader.Required(2))),
            "score value");
    }

    private static byte[] EncodeModePayload(
        GenericActorContext.ModeObservationState value,
        bool schema3)
    {
        var writer = new ActorWireObjectWriter();
        switch (value)
        {
            case GenericActorContext.ModeObservationState.Deathmatch:
                break;
            case GenericActorContext.ModeObservationState.Frontline frontline:
                writer.Field(
                    1,
                    ActorWireValue.Int32(frontline.ActivePositionIndex));
                GenericActorWireCodecValues.OptionalInt32(
                    writer,
                    2,
                    frontline.ClaimingTeamId);
                writer.Field(
                    3,
                    ActorWireValue.Int32(frontline.CaptureProgress));
                writer.Field(
                    4,
                    ActorWireValue.Int32(frontline.DecayTicksElapsed));
                writer.Field(
                    5,
                    ActorWireValue.Int32(frontline.ControlResumesAtTick));
                if (schema3
                    && frontline.HoldOwnerTeamId is int holdOwnerTeamId)
                {
                    writer.Field(6, ActorWireValue.Int32(holdOwnerTeamId));
                    writer.Field(
                        7,
                        ActorWireValue.Int32(
                            frontline.HoldRemainingTicks));
                }
                break;
            default:
                throw new InvalidOperationException(
                    "Unknown generic actor mode observation variant.");
        }
        return writer.ToArray();
    }

    private static GenericActorContext.ModeObservationState.Frontline
        DecodeFrontlineMode(
            string modeId,
            ActorWireObjectReader payload)
    {
        byte[]? holdOwner = payload.Optional(6);
        byte[]? holdRemaining = payload.Optional(7);
        if ((holdOwner is null) != (holdRemaining is null))
        {
            throw new FormatException(
                "Frontline hold owner and remaining ticks must be encoded together.");
        }
        return new GenericActorContext.ModeObservationState.Frontline(
            modeId,
            GenericActorWireCodecValues.Int32(payload, 1),
            GenericActorWireCodecValues.OptionalInt32(payload, 2),
            GenericActorWireCodecValues.Int32(payload, 3),
            GenericActorWireCodecValues.Int32(payload, 4),
            GenericActorWireCodecValues.Int32(payload, 5),
            holdOwner is null
                ? null
                : ActorWireValue.Int32(holdOwner),
            holdRemaining is null
                ? 0
                : ActorWireValue.Int32(holdRemaining));
    }

    private static byte[] EncodeSpawnReservation(
        GenericActorContext.SpawnReservation value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.TeamId));
        writer.Field(2, ActorWireValue.Int32(value.UnitId));
        writer.Field(3, ActorWireValue.Enum(value.Kind));
        GenericActorWireCodecValues.OptionalInt32(
            writer,
            4,
            value.DueTick);
        return writer.ToArray();
    }

    private static GenericActorContext.SpawnReservation
        DecodeSpawnReservation(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new GenericActorContext.SpawnReservation(
            GenericActorWireCodecValues.Int32(reader, 1),
            GenericActorWireCodecValues.Int32(reader, 2),
            GenericActorWireCodecValues.Enum<
                GenericActorContext.SpawnReservationKind>(reader, 3),
            GenericActorWireCodecValues.OptionalInt32(reader, 4));
    }

    private static byte[] Array<T>(
        IEnumerable<T> values,
        Func<T, byte[]> encode) =>
        GenericActorWireCodecValues.Array(values, encode);

    private static ImmutableArray<T> Array<T>(
        ActorWireObjectReader reader,
        ushort fieldId,
        Func<byte[], T> decode) =>
        GenericActorWireCodecValues.Array(reader, fieldId, decode);
}
