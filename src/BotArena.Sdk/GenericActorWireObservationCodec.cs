using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>Tagged schema-2 codec for one generic actor observation.</summary>
internal static class GenericActorWireObservationCodec
{
    public static byte[] Encode(GenericActorContext value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.SchemaVersion));
        writer.Field(2, ActorWireValue.Int32(value.Tick));
        writer.Field(
            3,
            GenericActorWireCodecValues.Fingerprint(
                value.MatchContractFingerprint));
        writer.Field(4, EncodeSelf(value.Self));
        writer.Field(5, Array(value.TeamUnits, EncodeUnitSlot));
        writer.Field(6, Array(value.Participants, EncodeParticipant));
        writer.Field(7, Array(value.Allies, EncodeAlly));
        writer.Field(8, Array(value.Enemies, EncodeEnemy));
        writer.Field(9, Array(value.VisibleTiles, EncodeTile));
        writer.Optional(
            10,
            value.VisibleProjectiles is { } projectiles
                ? Array(projectiles, EncodeProjectile)
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
        writer.Field(14, EncodeMode(value.Mode));
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
        byte[]? projectiles = reader.Optional(10);
        byte[]? sounds = reader.Optional(12);
        return GenericActorWireCodecValues.Decode(
            () => new GenericActorContext(
                GenericActorWireCodecValues.Int32(reader, 1),
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
                        item => DecodeProjectile(item, depth + 1)),
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
        GenericActorContext.ModeObservationState value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var writer = new ActorWireObjectWriter();
        writer.Field(1, GenericActorWireCodecValues.SemanticId(value.ModeId));
        writer.Field(2, ActorWireValue.Enum(value.Kind));
        writer.Field(3, EncodeModePayload(value));
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
                    new GenericActorContext.ModeObservationState.Frontline(
                        modeId,
                        GenericActorWireCodecValues.Int32(payload, 1),
                        GenericActorWireCodecValues.OptionalInt32(payload, 2),
                        GenericActorWireCodecValues.Int32(payload, 3),
                        GenericActorWireCodecValues.Int32(payload, 4),
                        GenericActorWireCodecValues.Int32(payload, 5),
                        GenericActorWireCodecValues.OptionalInt32(payload, 6),
                        GenericActorWireCodecValues.OptionalInt32(payload, 7),
                        GenericActorWireCodecValues.OptionalInt32(payload, 8),
                        GenericActorWireCodecValues.OptionalInt32(payload, 9)
                            ?? 0,
                        DecodeScrapTeams(payload.Optional(10), depth),
                        DecodeScrapPiles(payload.Optional(11), depth)),
                GenericActorRulesContract.GameModeKind.ArcRelay =>
                    new GenericActorContext.ModeObservationState.ArcRelay(
                        modeId,
                        Array(
                            payload,
                            1,
                            item => DecodeArcWell(item, depth + 1)),
                        Array(
                            payload,
                            2,
                            item => DecodeArcReactor(item, depth + 1)),
                        Array(
                            payload,
                            3,
                            item => DecodeArcCore(item, depth + 1)),
                        Array(
                            payload,
                            4,
                            item => DecodeArcSignature(item, depth + 1)),
                        GenericActorWireCodecValues.OptionalInt32(payload, 5),
                        GenericActorWireCodecValues.OptionalInt32(payload, 6)),
                _ => throw new FormatException(
                    "Unknown generic actor mode discriminator."),
            },
            "mode observation");
    }

    private static byte[] EncodeSelf(
        GenericActorContext.ObservedSelfState value)
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
            value.ClassId,
            value.RouteCooldowns,
            value.CarriedScrap);
        return writer.ToArray();
    }

    private static GenericActorContext.ObservedSelfState DecodeSelf(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return GenericActorWireCodecValues.Decode(
            () =>
            {
                SharedBodyState body = DecodeSharedBody(reader, depth);
                return new GenericActorContext.ObservedSelfState(
                    body.ActorId,
                    body.Generation,
                    body.FormId,
                    body.Position,
                    body.Facing,
                    body.Health,
                    body.Cooldown,
                    body.Energy,
                    body.PreviousActionResolution,
                    body.PendingSameLifeTransition,
                    body.ClassId,
                    body.RouteCooldowns,
                    body.CarriedScrap);
            },
            "self observation");
    }

    /// <summary>
    /// Fields 1..13 of the SHARED body encoding, decoded once. Self, ally, and
    /// the mind profile's own bodies all carry these exact fields in these exact
    /// slots, which is what makes a mind body and a per-life self comparable
    /// field by field rather than merely equivalent in spirit.
    /// </summary>
    internal readonly record struct SharedBodyState(
        ActorIdentity ActorId,
        int Generation,
        string FormId,
        Position Position,
        Direction Facing,
        int Health,
        int Cooldown,
        int? Energy,
        GenericActorActionResolution? PreviousActionResolution,
        GenericActorContext.PendingSameLifeTransition?
            PendingSameLifeTransition,
        string? ClassId,
        ImmutableArray<GenericActorContext.ObservedRouteCooldown>
            RouteCooldowns,
        int CarriedScrap);

    internal static SharedBodyState DecodeSharedBody(
        ActorWireObjectReader reader,
        int depth)
    {
        byte[]? resolution = reader.Optional(9);
        byte[]? transition = reader.Optional(10);
        byte[]? classId = reader.Optional(11);
        byte[]? routeCooldowns = reader.Optional(12);
        return new SharedBodyState(
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
                : GenericActorWireCodecValues.SemanticId(classId),
            DecodeRouteCooldowns(routeCooldowns, depth),
            GenericActorWireCodecValues.OptionalInt32(reader, 13) ?? 0);
    }

    internal static byte[] EncodeAlly(
        GenericActorContext.ObservedAllyState value)
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
            value.ClassId,
            value.RouteCooldowns,
            value.CarriedScrap);
        return writer.ToArray();
    }

    internal static GenericActorContext.ObservedAllyState DecodeAlly(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return GenericActorWireCodecValues.Decode(
            () =>
            {
                SharedBodyState body = DecodeSharedBody(reader, depth);
                return new GenericActorContext.ObservedAllyState(
                    body.ActorId,
                    body.Generation,
                    body.FormId,
                    body.Position,
                    body.Facing,
                    body.Health,
                    body.Cooldown,
                    body.Energy,
                    body.PreviousActionResolution,
                    body.PendingSameLifeTransition,
                    body.ClassId,
                    body.RouteCooldowns,
                    body.CarriedScrap);
            },
            "ally observation");
    }

    internal static void EncodeBody(
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
        string? classId,
        ImmutableArray<GenericActorContext.ObservedRouteCooldown>
            routeCooldowns,
        int carriedScrap)
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
        writer.Optional(
            12,
            routeCooldowns.IsEmpty
                ? null
                : Array(routeCooldowns, EncodeRouteCooldown));
        // Absent means "carrying nothing", so a ruleset with no declared
        // economy spends no wire bytes on the load.
        GenericActorWireCodecValues.OptionalInt32(
            writer,
            13,
            carriedScrap == 0 ? null : carriedScrap);
    }

    private static byte[] EncodeRouteCooldown(
        GenericActorContext.ObservedRouteCooldown value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(
            1,
            GenericActorWireCodecValues.SemanticId(value.TransitionId));
        writer.Field(2, ActorWireValue.Int32(value.ReadyAtTick));
        return writer.ToArray();
    }

    private static ImmutableArray<GenericActorContext.ObservedRouteCooldown>
        DecodeRouteCooldowns(byte[]? bytes, int depth) =>
        bytes is null
            ? []
            : ActorWireValue.Array(
                bytes,
                item => GenericActorWireCodecValues.Decode(
                    () =>
                    {
                        var reader = new ActorWireObjectReader(
                            item,
                            depth + 1);
                        return new GenericActorContext
                            .ObservedRouteCooldown(
                                GenericActorWireCodecValues.SemanticId(
                                    reader.Required(1)),
                                GenericActorWireCodecValues.Int32(
                                    reader,
                                    2));
                    },
                    "route cooldown"));

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

    internal static byte[] EncodeParticipant(
        GenericActorContext.ObservedParticipantStatus value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.ParticipantId));
        writer.Field(2, ActorWireValue.Int32(value.TeamId));
        writer.Field(
            3,
            GenericActorWireCodecValues.Int64(value.RuntimeFaultCount));
        writer.Field(4, ActorWireValue.Boolean(value.Disqualified));
        writer.Optional(
            5,
            value.ClassId is null
                ? null
                : GenericActorWireCodecValues.SemanticId(value.ClassId));
        return writer.ToArray();
    }

    internal static GenericActorContext.ObservedParticipantStatus
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

    internal static byte[] EncodeEnemy(
        GenericActorContext.ObservedEnemyState value)
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
        writer.Optional(
            8,
            value.ClassId is null
                ? null
                : GenericActorWireCodecValues.SemanticId(value.ClassId));
        GenericActorWireCodecValues.OptionalInt32(
            writer,
            9,
            value.CarriedScrap == 0 ? null : value.CarriedScrap);
        // Trailing additive field (§12.2): a visible enemy's declared job is
        // published, and the key costs nothing when nobody has tagged one.
        writer.Optional(
            10,
            value.RoleTag is null
                ? null
                : ActorWireValue.String(
                    value.RoleTag,
                    GenericMindContractVersions.MaxRoleTagUtf8Bytes));
        return writer.ToArray();
    }

    internal static GenericActorContext.ObservedEnemyState DecodeEnemy(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? transition = reader.Optional(6);
        byte[]? classId = reader.Optional(8);
        byte[]? roleTag = reader.Optional(10);
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
                    : GenericActorWireCodecValues.SemanticId(classId),
                GenericActorWireCodecValues.OptionalInt32(reader, 9) ?? 0,
                roleTag is null
                    ? null
                    : ActorWireValue.String(
                        roleTag,
                        GenericMindContractVersions.MaxRoleTagUtf8Bytes)),
            "enemy observation");
    }

    internal static byte[] EncodeTile(
        GenericActorContext.ObservedTile value)
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
        writer.Optional(
            4,
            value.SpawnReservation is null
                ? null
                : EncodeSpawnReservation(value.SpawnReservation));
        return writer.ToArray();
    }

    internal static GenericActorContext.ObservedTile DecodeTile(
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

    internal static byte[] EncodeProjectile(
        GenericActorContext.ObservedProjectile value)
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
        // Trailing additive tags, exactly as every prior observation growth
        // rode the wire: an old guest ignores an unknown field ID, so
        // observation schema 2 stays negotiable.
        writer.Field(10, ActorWireValue.Int32(value.TicksPerAdvance));
        writer.Field(11, ActorWireValue.Int32(value.DamagePerHit));
        return writer.ToArray();
    }

    internal static GenericActorContext.ObservedProjectile DecodeProjectile(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? ownerActorId = reader.Optional(3);
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
                GenericActorWireCodecValues.Int32(reader, 7),
                GenericActorWireCodecValues.Int32(reader, 8),
                Array(
                    reader,
                    9,
                    item => GenericActorWireCodecValues.DecodeIdentity(
                        item,
                        depth + 1)),
                GenericActorWireCodecValues.Int32(reader, 10),
                GenericActorWireCodecValues.Int32(reader, 11)),
            "projectile observation");
    }

    internal static byte[] EncodeScoreboard(
        GenericActorContext.ScoreboardState value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, Array(value.Teams, EncodeTeamScore));
        return writer.ToArray();
    }

    internal static GenericActorContext.ScoreboardState DecodeScoreboard(
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
        GenericActorContext.ModeObservationState value)
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
                // Absent means "no live hold", which is the same encoding an
                // absent claiming team already uses: one field, one meaning.
                GenericActorWireCodecValues.OptionalInt32(
                    writer,
                    6,
                    frontline.HoldOwnerTeamId);
                GenericActorWireCodecValues.OptionalInt32(
                    writer,
                    7,
                    frontline.HoldEndsAtTick);
                // Absent means "the side objective is neutral" and, for the
                // claim, "no claim stands" — so a ruleset that declares no
                // side objective spends no wire bytes on one.
                GenericActorWireCodecValues.OptionalInt32(
                    writer,
                    8,
                    frontline.SecondaryOwnerTeamId);
                GenericActorWireCodecValues.OptionalInt32(
                    writer,
                    9,
                    frontline.SecondaryClaimProgress == 0
                        ? null
                        : frontline.SecondaryClaimProgress);
                // Both economy collections are empty exactly when the mode
                // declares no economy, so an absent field is the inert
                // default and a non-economy ruleset spends no wire bytes.
                writer.Optional(
                    10,
                    frontline.ScrapTeams.IsEmpty
                        ? null
                        : Array(frontline.ScrapTeams, EncodeScrapTeam));
                writer.Optional(
                    11,
                    frontline.ScrapPiles.IsEmpty
                        ? null
                        : Array(frontline.ScrapPiles, EncodeScrapPile));
                break;
            case GenericActorContext.ModeObservationState.ArcRelay arcRelay:
                writer.Field(1, Array(arcRelay.Wells, EncodeArcWell));
                writer.Field(2, Array(arcRelay.Reactors, EncodeArcReactor));
                writer.Field(3, Array(arcRelay.VisibleCores, EncodeArcCore));
                writer.Field(
                    4,
                    Array(arcRelay.VisibleSignatures, EncodeArcSignature));
                GenericActorWireCodecValues.OptionalInt32(
                    writer,
                    5,
                    arcRelay.LatestPulseTeamId);
                GenericActorWireCodecValues.OptionalInt32(
                    writer,
                    6,
                    arcRelay.LatestPulseTick);
                break;
            default:
                throw new InvalidOperationException(
                    "Unknown generic actor mode observation variant.");
        }
        return writer.ToArray();
    }

    internal static byte[] EncodeArcCoreId(
        GenericActorContext.ArcRelayCoreId value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(
            1,
            GenericActorWireCodecValues.SemanticId(value.SourceWellId));
        writer.Field(2, ActorWireValue.Int32(value.SourceOrdinal));
        return writer.ToArray();
    }

    internal static GenericActorContext.ArcRelayCoreId DecodeArcCoreId(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new GenericActorContext.ArcRelayCoreId(
            GenericActorWireCodecValues.SemanticId(reader.Required(1)),
            GenericActorWireCodecValues.Int32(reader, 2));
    }

    private static byte[] EncodeArcWell(
        GenericActorContext.ArcRelayWellState value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, GenericActorWireCodecValues.SemanticId(value.WellId));
        writer.Field(2, GenericActorWireCodecValues.EncodePosition(value.Position));
        GenericActorWireCodecValues.OptionalInt32(
            writer,
            3,
            value.NextScheduledBirthTick);
        writer.Optional(
            4,
            value.OutstandingCoreId is { } coreId
                ? EncodeArcCoreId(coreId)
                : null);
        writer.Field(5, ActorWireValue.Boolean(value.PendingCharge));
        GenericActorWireCodecValues.OptionalInt32(
            writer,
            6,
            value.RearmCompletesAtTick);
        return writer.ToArray();
    }

    private static GenericActorContext.ArcRelayWellState DecodeArcWell(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? coreId = reader.Optional(4);
        return new GenericActorContext.ArcRelayWellState(
            GenericActorWireCodecValues.SemanticId(reader.Required(1)),
            GenericActorWireCodecValues.DecodePosition(
                reader.Required(2), depth + 1),
            GenericActorWireCodecValues.OptionalInt32(reader, 3),
            coreId is null ? null : DecodeArcCoreId(coreId, depth + 1),
            GenericActorWireCodecValues.Boolean(reader, 5),
            GenericActorWireCodecValues.OptionalInt32(reader, 6));
    }

    private static byte[] EncodeArcReactor(
        GenericActorContext.ArcRelayReactorState value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.TeamId));
        writer.Field(2, GenericActorWireCodecValues.EncodePosition(value.Position));
        writer.Field(3, ActorWireValue.Int32(value.ChargePips));
        writer.Field(4, ActorWireValue.Int32(value.IntegritySegments));
        // Tagged and emitted only when non-empty (threefold rulesets), so
        // historical observation bytes are untouched.
        if (!value.FilledSocketWellIds.IsEmpty)
        {
            writer.Field(5, Array(
                value.FilledSocketWellIds,
                GenericActorWireCodecValues.SemanticId));
        }
        return writer.ToArray();
    }

    private static GenericActorContext.ArcRelayReactorState DecodeArcReactor(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new GenericActorContext.ArcRelayReactorState(
            GenericActorWireCodecValues.Int32(reader, 1),
            GenericActorWireCodecValues.DecodePosition(
                reader.Required(2), depth + 1),
            GenericActorWireCodecValues.Int32(reader, 3),
            GenericActorWireCodecValues.Int32(reader, 4))
        {
            FilledSocketWellIds = reader.Optional(5) is { } sockets
                ? ActorWireValue.Array(
                    sockets,
                    GenericActorWireCodecValues.SemanticId)
                : [],
        };
    }

    private static byte[] EncodeArcCore(
        GenericActorContext.ArcRelayCoreState value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, EncodeArcCoreId(value.CoreId));
        writer.Field(2, GenericActorWireCodecValues.EncodePosition(value.Position));
        writer.Field(3, ActorWireValue.Enum(value.Disposition));
        writer.Optional(
            4,
            value.CarrierActorId is { } carrier
                ? GenericActorWireCodecValues.EncodeIdentity(carrier)
                : null);
        writer.Field(5, ActorWireValue.Int32(value.NextRelocationTick));
        writer.Optional(
            6,
            value.FlightTarget is { } target
                ? GenericActorWireCodecValues.EncodePosition(target)
                : null);
        GenericActorWireCodecValues.OptionalInt32(
            writer,
            7,
            value.FlightCompletesAtTick);
        // Tagged and emitted only when not 1 (charge-value rulesets), so
        // historical observation bytes are untouched.
        if (value.ChargeValue != 1)
            writer.Field(8, ActorWireValue.Int32(value.ChargeValue));
        return writer.ToArray();
    }

    private static GenericActorContext.ArcRelayCoreState DecodeArcCore(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? carrier = reader.Optional(4);
        byte[]? target = reader.Optional(6);
        return new GenericActorContext.ArcRelayCoreState(
            DecodeArcCoreId(reader.Required(1), depth + 1),
            GenericActorWireCodecValues.DecodePosition(
                reader.Required(2), depth + 1),
            GenericActorWireCodecValues.Enum<
                GenericActorContext.ArcRelayCoreDisposition>(reader, 3),
            carrier is null
                ? null
                : GenericActorWireCodecValues.DecodeIdentity(
                    carrier, depth + 1),
            GenericActorWireCodecValues.Int32(reader, 5),
            target is null
                ? null
                : GenericActorWireCodecValues.DecodePosition(
                    target, depth + 1),
            GenericActorWireCodecValues.OptionalInt32(reader, 7))
        {
            ChargeValue =
                GenericActorWireCodecValues.OptionalInt32(reader, 8) ?? 1,
        };
    }

    private static byte[] EncodeArcSignature(
        GenericActorContext.ArcRelaySignatureState value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, GenericActorWireCodecValues.Handle(value.OperationId));
        writer.Field(2, GenericActorWireCodecValues.SemanticId(value.SignatureId));
        writer.Field(3, GenericActorWireCodecValues.SemanticId(value.Kind));
        writer.Field(4, GenericActorWireCodecValues.EncodeIdentity(value.OwnerActorId));
        writer.Field(5, ActorWireValue.Int32(value.OwnerTeamId));
        writer.Field(6, ActorWireValue.Enum(value.Phase));
        writer.Field(7, ActorWireValue.Int32(value.StartedTick));
        GenericActorWireCodecValues.OptionalInt32(writer, 8, value.CompletesAtTick);
        GenericActorWireCodecValues.OptionalInt32(writer, 9, value.EndsAtTick);
        writer.Field(
            10,
            Array(value.Positions, GenericActorWireCodecValues.EncodePosition));
        writer.Optional(
            11,
            value.TargetActorId is { } target
                ? GenericActorWireCodecValues.EncodeIdentity(target)
                : null);
        writer.Field(12, ActorWireValue.Int32(value.RemainingCapacity));
        writer.Field(13, ActorWireValue.Boolean(value.Suppressed));
        return writer.ToArray();
    }

    private static GenericActorContext.ArcRelaySignatureState DecodeArcSignature(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? target = reader.Optional(11);
        return new GenericActorContext.ArcRelaySignatureState(
            GenericActorWireCodecValues.Handle(reader.Required(1)),
            GenericActorWireCodecValues.SemanticId(reader.Required(2)),
            GenericActorWireCodecValues.SemanticId(reader.Required(3)),
            GenericActorWireCodecValues.DecodeIdentity(
                reader.Required(4), depth + 1),
            GenericActorWireCodecValues.Int32(reader, 5),
            GenericActorWireCodecValues.Enum<
                GenericActorContext.ArcRelaySignaturePhase>(reader, 6),
            GenericActorWireCodecValues.Int32(reader, 7),
            GenericActorWireCodecValues.OptionalInt32(reader, 8),
            GenericActorWireCodecValues.OptionalInt32(reader, 9),
            Array(
                reader,
                10,
                item => GenericActorWireCodecValues.DecodePosition(
                    item, depth + 1)),
            target is null
                ? null
                : GenericActorWireCodecValues.DecodeIdentity(
                    target, depth + 1),
            GenericActorWireCodecValues.Int32(reader, 12),
            GenericActorWireCodecValues.Boolean(reader, 13));
    }

    private static byte[] EncodeScrapTeam(
        GenericActorContext.ScrapTeamState value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.TeamId));
        writer.Field(2, ActorWireValue.Int32(value.Bank));
        writer.Field(
            3,
            Array(value.TierLevels, tier => ActorWireValue.Int32(tier)));
        return writer.ToArray();
    }

    private static ImmutableArray<GenericActorContext.ScrapTeamState>
        DecodeScrapTeams(byte[]? bytes, int depth) =>
        bytes is null
            ? []
            : ActorWireValue.Array(
                bytes,
                item => GenericActorWireCodecValues.Decode(
                    () =>
                    {
                        var reader = new ActorWireObjectReader(
                            item,
                            depth + 1);
                        return new GenericActorContext.ScrapTeamState(
                            GenericActorWireCodecValues.Int32(reader, 1),
                            GenericActorWireCodecValues.Int32(reader, 2),
                            ActorWireValue.Array(
                                reader.Required(3),
                                tier => ActorWireValue.Int32(tier)));
                    },
                    "scrap team"));

    private static byte[] EncodeScrapPile(
        GenericActorContext.ScrapPile value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(
            1,
            GenericActorWireCodecValues.EncodePosition(value.Position));
        writer.Field(2, ActorWireValue.Int32(value.Amount));
        writer.Field(3, ActorWireValue.Int32(value.ExpiresAtTick));
        return writer.ToArray();
    }

    private static ImmutableArray<GenericActorContext.ScrapPile>
        DecodeScrapPiles(byte[]? bytes, int depth) =>
        bytes is null
            ? []
            : ActorWireValue.Array(
                bytes,
                item => GenericActorWireCodecValues.Decode(
                    () =>
                    {
                        var reader = new ActorWireObjectReader(
                            item,
                            depth + 1);
                        return new GenericActorContext.ScrapPile(
                            GenericActorWireCodecValues.DecodePosition(
                                reader.Required(1),
                                depth + 2),
                            GenericActorWireCodecValues.Int32(reader, 2),
                            GenericActorWireCodecValues.Int32(reader, 3));
                    },
                    "scrap pile"));

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
