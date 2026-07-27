using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>Tagged binary codec for one actor's canonical tick context.</summary>
internal static class ActorWireObservationCodec
{
    public static byte[] Encode(ActorContext value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.SchemaVersion));
        writer.Field(2, ActorWireValue.Int32(value.Tick));
        writer.Field(
            3,
            ActorWireValue.String(value.MatchContractFingerprint, 64));
        writer.Field(4, ActorWireValue.Enum(value.TeamPerception));
        writer.Field(5, EncodeSelf(value.Self));
        writer.Field(6, Array(value.TeamUnits, EncodeUnitSlot));
        writer.Field(7, Array(value.Allies, EncodeAlly));
        writer.Field(8, Array(value.Enemies, EncodeEnemy));
        writer.Field(9, Array(value.VisibleTiles, EncodeMapTile));
        writer.Optional(
            10,
            value.VisibleProjectiles is { } projectiles
                ? Array(projectiles, EncodeProjectile)
                : null);
        writer.Field(11, Array(value.VisibleEvents, EncodeEvent));
        writer.Optional(
            12,
            value.HeardSounds is { } sounds
                ? Array(sounds, EncodeSound)
                : null);
        writer.Optional(
            13,
            value.FrontlineObjective is { } objective
                ? EncodeObjective(objective)
                : null);
        writer.Field(14, Array(value.Actions, EncodeActionAvailability));
        return writer.ToArray();
    }

    public static ActorContext Decode(byte[] bytes, int depth = 0)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? projectiles = reader.Optional(10);
        byte[]? sounds = reader.Optional(12);
        byte[]? objective = reader.Optional(13);
        return new ActorContext
        {
            SchemaVersion = Int(reader, 1),
            Tick = Int(reader, 2),
            MatchContractFingerprint = Text(reader, 3, 64),
            TeamPerception = ActorWireValue.Enum<TeamPerceptionMode>(
                reader.Required(4)),
            Self = DecodeSelf(reader.Required(5), depth + 1),
            TeamUnits = DecodeArray(
                reader,
                6,
                item => DecodeUnitSlot(item, depth + 1)),
            Allies = DecodeArray(
                reader,
                7,
                item => DecodeAlly(item, depth + 1)),
            Enemies = DecodeArray(
                reader,
                8,
                item => DecodeEnemy(item, depth + 1)),
            VisibleTiles = DecodeArray(
                reader,
                9,
                item => DecodeMapTile(item, depth + 1)),
            VisibleProjectiles = projectiles is null
                ? null
                : ActorWireValue.Array(
                    projectiles,
                    item => DecodeProjectile(item, depth + 1)),
            VisibleEvents = DecodeArray(
                reader,
                11,
                item => DecodeEvent(item, depth + 1)),
            HeardSounds = sounds is null
                ? null
                : ActorWireValue.Array(
                    sounds,
                    item => DecodeSound(item, depth + 1)),
            FrontlineObjective = objective is null
                ? null
                : DecodeObjective(objective, depth + 1),
            Actions = DecodeArray(
                reader,
                14,
                item => DecodeActionAvailability(item, depth + 1)),
        };
    }

    private static byte[] EncodeUnitSlot(ObservedUnitSlot value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.TeamId));
        writer.Field(2, ActorWireValue.Int32(value.UnitId));
        writer.Field(3, SemanticId(value.FormId));
        writer.Field(4, ActorWireValue.Enum(value.LifecycleStatus));
        writer.Optional(
            5,
            value.ActiveActorId is { } actor
                ? ActorWireContractCodec.EncodeIdentity(actor)
                : null);
        OptionalInt(writer, 6, value.RespawnAtTick);
        OptionalInt(writer, 7, value.UnlockAtTick);
        OptionalInt(writer, 8, value.RebuildReadyAtTick);
        OptionalInt(writer, 9, value.FabricationAtTick);
        return writer.ToArray();
    }

    private static ObservedUnitSlot DecodeUnitSlot(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? actor = reader.Optional(5);
        return new ObservedUnitSlot(
            Int(reader, 1),
            Int(reader, 2),
            SemanticText(reader, 3),
            ActorWireValue.Enum<FrontlineLifecycleStatus>(
                reader.Required(4)),
            actor is null
                ? null
                : ActorWireContractCodec.DecodeIdentity(actor, depth + 1),
            OptionalInt(reader, 6),
            OptionalInt(reader, 7),
            OptionalInt(reader, 8),
            OptionalInt(reader, 9));
    }

    private static byte[] EncodeSelf(ObservedSelf value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(
            1,
            ActorWireContractCodec.EncodeIdentity(value.ActorId));
        writer.Field(2, SemanticId(value.FormId));
        writer.Field(
            3,
            ActorWireContractCodec.EncodePosition(value.Position));
        writer.Field(4, ActorWireValue.Enum(value.Facing));
        writer.Field(5, ActorWireValue.Int32(value.Health));
        writer.Field(6, ActorWireValue.Int32(value.Cooldown));
        OptionalInt(writer, 7, value.Energy);
        writer.Field(8, ActorWireValue.Enum(value.PreviousActionResult));
        writer.Optional(
            9,
            value.PendingFormTransition is { } transition
                ? EncodeTransition(transition)
                : null);
        return writer.ToArray();
    }

    private static ObservedSelf DecodeSelf(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? transition = reader.Optional(9);
        return new ObservedSelf(
            ActorWireContractCodec.DecodeIdentity(
                reader.Required(1),
                depth + 1),
            SemanticText(reader, 2),
            ActorWireContractCodec.DecodePosition(
                reader.Required(3),
                depth + 1),
            ActorWireValue.Enum<Direction>(reader.Required(4)),
            Int(reader, 5),
            Int(reader, 6),
            OptionalInt(reader, 7),
            ActorWireValue.Enum<ActionResult>(reader.Required(8)))
        {
            PendingFormTransition = transition is null
                ? null
                : DecodeTransition(transition, depth + 1),
        };
    }

    private static byte[] EncodeAlly(ObservedAlly value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(
            1,
            ActorWireContractCodec.EncodeIdentity(value.ActorId));
        writer.Field(2, SemanticId(value.FormId));
        writer.Field(
            3,
            ActorWireContractCodec.EncodePosition(value.Position));
        writer.Field(4, ActorWireValue.Enum(value.Facing));
        writer.Field(5, ActorWireValue.Int32(value.Health));
        writer.Field(6, ActorWireValue.Int32(value.Cooldown));
        OptionalInt(writer, 7, value.Energy);
        writer.Field(8, ActorWireValue.Enum(value.PreviousActionResult));
        writer.Optional(
            9,
            value.PendingFormTransition is { } transition
                ? EncodeTransition(transition)
                : null);
        return writer.ToArray();
    }

    private static ObservedAlly DecodeAlly(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? transition = reader.Optional(9);
        return new ObservedAlly(
            ActorWireContractCodec.DecodeIdentity(
                reader.Required(1),
                depth + 1),
            SemanticText(reader, 2),
            ActorWireContractCodec.DecodePosition(
                reader.Required(3),
                depth + 1),
            ActorWireValue.Enum<Direction>(reader.Required(4)),
            Int(reader, 5),
            Int(reader, 6),
            OptionalInt(reader, 7),
            ActorWireValue.Enum<ActionResult>(reader.Required(8)))
        {
            PendingFormTransition = transition is null
                ? null
                : DecodeTransition(transition, depth + 1),
        };
    }

    private static byte[] EncodeEnemyReference(
        ObservedEnemyActorRef value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.TeamId));
        writer.Field(2, ActorWireValue.Int32(value.UnitId));
        writer.Field(3, Id(value.LifeHandle));
        return writer.ToArray();
    }

    private static ObservedEnemyActorRef DecodeEnemyReference(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new ObservedEnemyActorRef(
            Int(reader, 1),
            Int(reader, 2),
            Text(reader, 3));
    }

    private static byte[] EncodeEnemy(ObservedEnemy value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, EncodeEnemyReference(value.Actor));
        writer.Field(2, SemanticId(value.FormId));
        writer.Field(
            3,
            ActorWireContractCodec.EncodePosition(value.Position));
        writer.Field(4, ActorWireValue.Enum(value.Facing));
        writer.Field(5, ActorWireValue.Int32(value.Health));
        writer.Field(
            6,
            Array(
                value.ObservedBy,
                ActorWireContractCodec.EncodeIdentity));
        writer.Optional(
            7,
            value.PendingFormTransition is { } transition
                ? EncodeTransition(transition)
                : null);
        return writer.ToArray();
    }

    private static ObservedEnemy DecodeEnemy(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? transition = reader.Optional(7);
        return new ObservedEnemy(
            DecodeEnemyReference(reader.Required(1), depth + 1),
            SemanticText(reader, 2),
            ActorWireContractCodec.DecodePosition(
                reader.Required(3),
                depth + 1),
            ActorWireValue.Enum<Direction>(reader.Required(4)),
            Int(reader, 5),
            DecodeArray(
                reader,
                6,
                item => ActorWireContractCodec.DecodeIdentity(
                    item,
                    depth + 1)))
        {
            PendingFormTransition = transition is null
                ? null
                : DecodeTransition(transition, depth + 1),
        };
    }

    private static byte[] EncodeTransition(ObservedFormTransition value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, SemanticId(value.FromFormId));
        writer.Field(2, SemanticId(value.ToFormId));
        writer.Field(3, ActorWireValue.Int32(value.StartedAtTick));
        writer.Field(4, ActorWireValue.Int32(value.CompletesAtTick));
        return writer.ToArray();
    }

    private static ObservedFormTransition DecodeTransition(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new ObservedFormTransition(
            SemanticText(reader, 1),
            SemanticText(reader, 2),
            Int(reader, 3),
            Int(reader, 4));
    }

    private static byte[] EncodeMapTile(ObservedMapTile value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(
            1,
            ActorWireContractCodec.EncodePosition(value.Position));
        writer.Field(2, ActorWireValue.Boolean(value.IsWall));
        writer.Field(
            3,
            Array(
                value.ObservedBy,
                ActorWireContractCodec.EncodeIdentity));
        return writer.ToArray();
    }

    private static ObservedMapTile DecodeMapTile(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new ObservedMapTile(
            ActorWireContractCodec.DecodePosition(
                reader.Required(1),
                depth + 1),
            Bool(reader, 2),
            DecodeArray(
                reader,
                3,
                item => ActorWireContractCodec.DecodeIdentity(
                    item,
                    depth + 1)));
    }

    private static byte[] EncodeProjectile(ObservedActorProjectile value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, Id(value.ProjectileHandle));
        writer.Field(2, ActorWireValue.Int32(value.OwnerTeamId));
        writer.Optional(
            3,
            value.AlliedOwnerActorId is { } allied
                ? ActorWireContractCodec.EncodeIdentity(allied)
                : null);
        writer.Optional(
            4,
            value.VisibleEnemyOwner is { } enemy
                ? EncodeEnemyReference(enemy)
                : null);
        writer.Field(
            5,
            ActorWireContractCodec.EncodePosition(value.Position));
        writer.Field(6, ActorWireValue.Enum(value.Heading));
        writer.Field(7, ActorWireValue.Int32(value.TilesPerAdvance));
        writer.Field(8, ActorWireValue.Int32(value.TicksUntilAdvance));
        writer.Field(9, ActorWireValue.Int32(value.RemainingTiles));
        writer.Field(
            10,
            Array(
                value.ObservedBy,
                ActorWireContractCodec.EncodeIdentity));
        return writer.ToArray();
    }

    private static ObservedActorProjectile DecodeProjectile(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? allied = reader.Optional(3);
        byte[]? enemy = reader.Optional(4);
        return new ObservedActorProjectile(
            Text(reader, 1),
            Int(reader, 2),
            allied is null
                ? null
                : ActorWireContractCodec.DecodeIdentity(
                    allied,
                    depth + 1),
            enemy is null
                ? null
                : DecodeEnemyReference(enemy, depth + 1),
            ActorWireContractCodec.DecodePosition(
                reader.Required(5),
                depth + 1),
            ActorWireValue.Enum<ProjectileHeading>(reader.Required(6)),
            Int(reader, 7),
            Int(reader, 8),
            Int(reader, 9),
            DecodeArray(
                reader,
                10,
                item => ActorWireContractCodec.DecodeIdentity(
                    item,
                    depth + 1)));
    }

    private static byte[] EncodeEvent(ObservedMatchEvent value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, Id(value.EventHandle));
        writer.Field(2, ActorWireValue.Int32(value.SourceTick));
        writer.Field(3, ActorWireValue.Enum(value.Type));
        OptionalInt(writer, 4, value.TeamId);
        writer.Optional(
            5,
            value.AlliedActorId is { } allied
                ? ActorWireContractCodec.EncodeIdentity(allied)
                : null);
        writer.Optional(
            6,
            value.EnemyActor is { } enemy
                ? EncodeEnemyReference(enemy)
                : null);
        OptionalText(writer, 7, value.ProjectileHandle);
        writer.Optional(
            8,
            value.Position is { } position
                ? ActorWireContractCodec.EncodePosition(position)
                : null);
        OptionalEnum(writer, 9, value.Facing);
        OptionalInt(writer, 10, value.Amount);
        OptionalInt(writer, 11, value.NewHealth);
        writer.Field(
            12,
            Array(
                value.ObservedBy,
                ActorWireContractCodec.EncodeIdentity));
        OptionalEnum(writer, 13, value.ProjectileHeading);
        OptionalSemanticText(writer, 14, value.FromFormId);
        OptionalSemanticText(writer, 15, value.ToFormId);
        OptionalInt(writer, 16, value.FormTransitionStartedAtTick);
        OptionalInt(writer, 17, value.FormTransitionCompletesAtTick);
        OptionalSemanticText(writer, 18, value.ActionId);
        OptionalInt(writer, 19, value.ActionCode);
        OptionalSemanticText(writer, 20, value.FormTargetId);
        OptionalEnum(writer, 21, value.ActionResult);
        return writer.ToArray();
    }

    private static ObservedMatchEvent DecodeEvent(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? allied = reader.Optional(5);
        byte[]? enemy = reader.Optional(6);
        byte[]? position = reader.Optional(8);
        return new ObservedMatchEvent(
            Text(reader, 1),
            Int(reader, 2),
            ActorWireValue.Enum<ObservedMatchEventType>(
                reader.Required(3)),
            OptionalInt(reader, 4),
            allied is null
                ? null
                : ActorWireContractCodec.DecodeIdentity(
                    allied,
                    depth + 1),
            enemy is null
                ? null
                : DecodeEnemyReference(enemy, depth + 1),
            OptionalText(reader, 7),
            position is null
                ? null
                : ActorWireContractCodec.DecodePosition(
                    position,
                    depth + 1),
            OptionalEnum<Direction>(reader, 9),
            OptionalInt(reader, 10),
            OptionalInt(reader, 11),
            DecodeArray(
                reader,
                12,
                item => ActorWireContractCodec.DecodeIdentity(
                    item,
                    depth + 1)))
        {
            ProjectileHeading = OptionalEnum<ProjectileHeading>(
                reader,
                13),
            FromFormId = OptionalSemanticText(reader, 14),
            ToFormId = OptionalSemanticText(reader, 15),
            FormTransitionStartedAtTick = OptionalInt(reader, 16),
            FormTransitionCompletesAtTick = OptionalInt(reader, 17),
            ActionId = OptionalSemanticText(reader, 18),
            ActionCode = OptionalInt(reader, 19),
            FormTargetId = OptionalSemanticText(reader, 20),
            ActionResult = OptionalEnum<ActionResult>(reader, 21),
        };
    }

    private static byte[] EncodeSound(ObservedActorSound value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, Id(value.EventHandle));
        writer.Field(2, ActorWireValue.Int32(value.SourceTick));
        writer.Field(
            3,
            ActorWireContractCodec.EncodeIdentity(value.ObserverActorId));
        writer.Field(4, ActorWireValue.Enum(value.Type));
        writer.Field(5, ActorWireValue.Int32(value.Bearing));
        writer.Field(6, ActorWireValue.Int32(value.Distance));
        return writer.ToArray();
    }

    private static ObservedActorSound DecodeSound(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new ObservedActorSound(
            Text(reader, 1),
            Int(reader, 2),
            ActorWireContractCodec.DecodeIdentity(
                reader.Required(3),
                depth + 1),
            ActorWireValue.Enum<ObservedMatchEventType>(
                reader.Required(4)),
            Int(reader, 5),
            Int(reader, 6));
    }

    private static byte[] EncodeObjective(ObservedFrontlineObjective value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(
            1,
            ActorWireValue.Int32(value.ActivePositionIndex));
        OptionalInt(writer, 2, value.ClaimingTeamId);
        writer.Field(3, ActorWireValue.Int32(value.CaptureProgress));
        writer.Field(4, ActorWireValue.Int32(value.DecayTicksElapsed));
        writer.Field(5, ActorWireValue.Int32(value.ControlResumesAtTick));
        return writer.ToArray();
    }

    private static ObservedFrontlineObjective DecodeObjective(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new ObservedFrontlineObjective(
            Int(reader, 1),
            OptionalInt(reader, 2),
            Int(reader, 3),
            Int(reader, 4),
            Int(reader, 5));
    }

    private static byte[] EncodeActionAvailability(
        ObservedActionAvailability value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, SemanticId(value.ActionId));
        writer.Field(2, ActorWireValue.Int32(value.ActionCode));
        writer.Field(3, Array(value.ParameterKinds, ActorWireValue.Enum));
        writer.Field(4, ActorWireValue.Boolean(value.Enabled));
        writer.Field(5, ActorWireValue.Boolean(value.Available));
        OptionalBool(writer, 6, value.ShotProgramAvailable);
        writer.Optional(
            7,
            value.AllowedDirections is { } directions
                ? Array(directions, ActorWireValue.Enum)
                : null);
        writer.Optional(
            8,
            value.AllowedUnitTargets is { } targets
                ? Array(targets, EncodeUnitTarget)
                : null);
        writer.Optional(
            9,
            value.AllowedFormTargets is { } forms
                ? Array(forms, SemanticId)
                : null);
        writer.Optional(
            10,
            value.AllowedProjectileHeadings is { } headings
                ? Array(headings, ActorWireValue.Enum)
                : null);
        return writer.ToArray();
    }

    private static ObservedActionAvailability DecodeActionAvailability(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? directions = reader.Optional(7);
        byte[]? targets = reader.Optional(8);
        byte[]? forms = reader.Optional(9);
        byte[]? headings = reader.Optional(10);
        return new ObservedActionAvailability(
            SemanticText(reader, 1),
            Int(reader, 2),
            DecodeArray(
                reader,
                3,
                ActorWireValue.Enum<PublicActionParameterKind>),
            Bool(reader, 4),
            Bool(reader, 5),
            OptionalBool(reader, 6),
            directions is null
                ? null
                : ActorWireValue.Array(
                    directions,
                    ActorWireValue.Enum<Direction>),
            targets is null
                ? null
                : ActorWireValue.Array(
                    targets,
                    item => DecodeUnitTarget(item, depth + 1)),
            forms is null
                ? null
                : ActorWireValue.Array(
                    forms,
                    item => ActorWireValue.String(
                        item,
                        ActorWireProtocol.MaxSemanticIdBytes)))
        {
            AllowedProjectileHeadings = headings is null
                ? null
                : ActorWireValue.Array(
                    headings,
                    ActorWireValue.Enum<ProjectileHeading>),
        };
    }

    internal static byte[] EncodeUnitTarget(ObservedUnitTarget value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.TeamId));
        writer.Field(2, ActorWireValue.Int32(value.UnitId));
        return writer.ToArray();
    }

    internal static ObservedUnitTarget DecodeUnitTarget(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new ObservedUnitTarget(Int(reader, 1), Int(reader, 2));
    }

    private static byte[] Array<T>(
        IEnumerable<T> values,
        Func<T, byte[]> encode) =>
        ActorWireValue.Array(values, encode);

    private static ImmutableArray<T> DecodeArray<T>(
        ActorWireObjectReader reader,
        ushort fieldId,
        Func<byte[], T> decode) =>
        ActorWireValue.Array(reader.Required(fieldId), decode);

    private static byte[] Id(string value) =>
        ActorWireValue.String(value, 256);

    private static byte[] SemanticId(string value) =>
        ActorWireValue.String(value, ActorWireProtocol.MaxSemanticIdBytes);

    private static int Int(ActorWireObjectReader reader, ushort fieldId) =>
        ActorWireValue.Int32(reader.Required(fieldId));

    private static int? OptionalInt(
        ActorWireObjectReader reader,
        ushort fieldId)
    {
        byte[]? bytes = reader.Optional(fieldId);
        return bytes is null ? null : ActorWireValue.Int32(bytes);
    }

    private static void OptionalInt(
        ActorWireObjectWriter writer,
        ushort fieldId,
        int? value)
    {
        if (value is int present)
            writer.Field(fieldId, ActorWireValue.Int32(present));
    }

    private static bool Bool(
        ActorWireObjectReader reader,
        ushort fieldId) =>
        ActorWireValue.Boolean(reader.Required(fieldId));

    private static bool? OptionalBool(
        ActorWireObjectReader reader,
        ushort fieldId)
    {
        byte[]? bytes = reader.Optional(fieldId);
        return bytes is null ? null : ActorWireValue.Boolean(bytes);
    }

    private static void OptionalBool(
        ActorWireObjectWriter writer,
        ushort fieldId,
        bool? value)
    {
        if (value is bool present)
            writer.Field(fieldId, ActorWireValue.Boolean(present));
    }

    private static string Text(
        ActorWireObjectReader reader,
        ushort fieldId,
        int maxBytes = 256) =>
        ActorWireValue.String(reader.Required(fieldId), maxBytes);

    private static string SemanticText(
        ActorWireObjectReader reader,
        ushort fieldId) =>
        ActorWireValue.String(
            reader.Required(fieldId),
            ActorWireProtocol.MaxSemanticIdBytes);

    private static string? OptionalText(
        ActorWireObjectReader reader,
        ushort fieldId)
    {
        byte[]? bytes = reader.Optional(fieldId);
        return bytes is null
            ? null
            : ActorWireValue.String(bytes, 256);
    }

    private static string? OptionalSemanticText(
        ActorWireObjectReader reader,
        ushort fieldId)
    {
        byte[]? bytes = reader.Optional(fieldId);
        return bytes is null
            ? null
            : ActorWireValue.String(
                bytes,
                ActorWireProtocol.MaxSemanticIdBytes);
    }

    private static void OptionalText(
        ActorWireObjectWriter writer,
        ushort fieldId,
        string? value)
    {
        if (value is not null)
            writer.Field(fieldId, Id(value));
    }

    private static void OptionalSemanticText(
        ActorWireObjectWriter writer,
        ushort fieldId,
        string? value)
    {
        if (value is not null)
            writer.Field(fieldId, SemanticId(value));
    }

    private static T? OptionalEnum<T>(
        ActorWireObjectReader reader,
        ushort fieldId)
        where T : struct, Enum
    {
        byte[]? bytes = reader.Optional(fieldId);
        return bytes is null ? null : ActorWireValue.Enum<T>(bytes);
    }

    private static void OptionalEnum<T>(
        ActorWireObjectWriter writer,
        ushort fieldId,
        T? value)
        where T : struct, Enum
    {
        if (value is T present)
            writer.Field(fieldId, ActorWireValue.Enum(present));
    }
}
