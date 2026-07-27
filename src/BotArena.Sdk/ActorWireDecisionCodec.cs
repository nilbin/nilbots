namespace BotArena.Sdk;

/// <summary>Tagged binary codec for one actor decision.</summary>
internal static class ActorWireDecisionCodec
{
    public static byte[] Encode(ActorDecision value)
    {
        var writer = new ActorWireObjectWriter();
        OptionalText(
            writer,
            1,
            value.ActionId,
            ActorWireProtocol.MaxSemanticIdBytes);
        OptionalInt(writer, 2, value.ActionCode);
        writer.Optional(
            3,
            value.Payload is { } payload
                ? EncodePayload(payload)
                : null);
        OptionalText(writer, 4, value.DebugMessage, 4096);
        writer.Field(5, ActorWireValue.Boolean(value.Faulted));
        OptionalText(writer, 6, value.FaultMessage, 4096);
        return writer.ToArray();
    }

    public static ActorDecision Decode(byte[] bytes, int depth = 0)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? payload = reader.Optional(3);
        return new ActorDecision
        {
            ActionId = OptionalText(
                reader,
                1,
                ActorWireProtocol.MaxSemanticIdBytes),
            ActionCode = OptionalInt(reader, 2),
            Payload = payload is null
                ? null
                : DecodePayload(payload, depth + 1),
            DebugMessage = OptionalText(reader, 4, 4096),
            Faulted = ActorWireValue.Boolean(reader.Required(5)),
            FaultMessage = OptionalText(reader, 6, 4096),
        };
    }

    private static byte[] EncodePayload(ActorActionPayload value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Optional(
            1,
            value.ShotProgram is { } program
                ? ActorWireContractCodec.EncodeShotProgram(program)
                : null);
        OptionalEnum(writer, 2, value.Direction);
        writer.Optional(
            3,
            value.UnitTarget is { } target
                ? ActorWireObservationCodec.EncodeUnitTarget(target)
                : null);
        OptionalText(
            writer,
            4,
            value.FormTargetId,
            ActorWireProtocol.MaxSemanticIdBytes);
        OptionalEnum(writer, 5, value.LaunchHeading);
        return writer.ToArray();
    }

    private static ActorActionPayload DecodePayload(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? shotProgram = reader.Optional(1);
        byte[]? target = reader.Optional(3);
        return new ActorActionPayload
        {
            ShotProgram = shotProgram is null
                ? null
                : ActorWireContractCodec.DecodeShotProgram(
                    shotProgram,
                    depth + 1),
            Direction = OptionalEnum<Direction>(reader, 2),
            UnitTarget = target is null
                ? null
                : ActorWireObservationCodec.DecodeUnitTarget(
                    target,
                    depth + 1),
            FormTargetId = OptionalText(
                reader,
                4,
                ActorWireProtocol.MaxSemanticIdBytes),
            LaunchHeading = OptionalEnum<ProjectileHeading>(reader, 5),
        };
    }

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

    private static string? OptionalText(
        ActorWireObjectReader reader,
        ushort fieldId,
        int maxBytes)
    {
        byte[]? bytes = reader.Optional(fieldId);
        return bytes is null
            ? null
            : ActorWireValue.String(bytes, maxBytes);
    }

    private static void OptionalText(
        ActorWireObjectWriter writer,
        ushort fieldId,
        string? value,
        int maxBytes)
    {
        if (value is not null)
        {
            writer.Field(
                fieldId,
                ActorWireValue.String(value, maxBytes));
        }
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
