using System.Buffers.Binary;
using System.Collections.Immutable;

namespace BotArena.Sdk;

internal static class GenericActorWireCodecValues
{
    public const int MaximumHostPayloadBytes =
        ActorWireProtocol.MaxHostFrameBytes - ActorWireProtocol.HeaderSize;
    public const int MaximumGuestPayloadBytes =
        ActorWireProtocol.MaxGuestFrameBytes - ActorWireProtocol.HeaderSize;

    public static byte[] Int64(long value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        return bytes;
    }

    public static long Int64(byte[] bytes)
    {
        if (bytes.Length != 8)
            throw new FormatException("Actor wire int64 must be exactly 8 bytes.");
        return BinaryPrimitives.ReadInt64LittleEndian(bytes);
    }

    public static byte[] SemanticId(string value) =>
        ActorWireValue.String(
            GenericActorDynamicValueRules.SemanticId(value, nameof(value)),
            ActorWireProtocol.MaxSemanticIdBytes);

    public static string SemanticId(byte[] bytes)
    {
        string value = ActorWireValue.String(
            bytes,
            ActorWireProtocol.MaxSemanticIdBytes);
        return Decode(
            () => GenericActorDynamicValueRules.SemanticId(
                value,
                nameof(value)),
            "semantic ID");
    }

    public static byte[] Fingerprint(string value) =>
        ActorWireValue.String(
            GenericActorDynamicValueRules.Fingerprint(value, nameof(value)),
            64);

    public static string Fingerprint(byte[] bytes)
    {
        string value = ActorWireValue.String(bytes, 64);
        return Decode(
            () => GenericActorDynamicValueRules.Fingerprint(
                value,
                nameof(value)),
            "fingerprint");
    }

    public static byte[] Handle(string value) =>
        ActorWireValue.String(
            GenericActorDynamicValueRules.Handle(value, nameof(value)),
            256);

    public static string Handle(byte[] bytes)
    {
        string value = ActorWireValue.String(bytes, 256);
        return Decode(
            () => GenericActorDynamicValueRules.Handle(value, nameof(value)),
            "opaque handle");
    }

    public static byte[] Array<T>(
        IEnumerable<T> values,
        Func<T, byte[]> encode) =>
        ActorWireValue.Array(values, encode);

    public static ImmutableArray<T> Array<T>(
        ActorWireObjectReader reader,
        ushort fieldId,
        Func<byte[], T> decode) =>
        ActorWireValue.Array(reader.Required(fieldId), decode);

    public static int Int32(
        ActorWireObjectReader reader,
        ushort fieldId) =>
        ActorWireValue.Int32(reader.Required(fieldId));

    public static int? OptionalInt32(
        ActorWireObjectReader reader,
        ushort fieldId)
    {
        byte[]? bytes = reader.Optional(fieldId);
        return bytes is null ? null : ActorWireValue.Int32(bytes);
    }

    public static void OptionalInt32(
        ActorWireObjectWriter writer,
        ushort fieldId,
        int? value)
    {
        if (value is int present)
            writer.Field(fieldId, ActorWireValue.Int32(present));
    }

    public static bool Boolean(
        ActorWireObjectReader reader,
        ushort fieldId) =>
        ActorWireValue.Boolean(reader.Required(fieldId));

    public static T Enum<T>(
        ActorWireObjectReader reader,
        ushort fieldId)
        where T : struct, Enum =>
        ActorWireValue.Enum<T>(reader.Required(fieldId));

    public static byte[] EncodeIdentity(ActorIdentity value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.TeamId));
        writer.Field(2, ActorWireValue.Int32(value.UnitId));
        writer.Field(3, ActorWireValue.Int32(value.LifeId));
        return writer.ToArray();
    }

    public static ActorIdentity DecodeIdentity(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return Decode(
            () => new ActorIdentity(
                Int32(reader, 1),
                Int32(reader, 2),
                Int32(reader, 3)),
            "actor identity");
    }

    public static byte[] EncodePosition(Position value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.X));
        writer.Field(2, ActorWireValue.Int32(value.Y));
        return writer.ToArray();
    }

    public static Position DecodePosition(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        int x = Int32(reader, 1);
        int y = Int32(reader, 2);
        if (x < 0 || y < 0)
            throw new FormatException("Actor position coordinates cannot be negative.");
        return new Position(x, y);
    }

    public static byte[] EncodeShotProgram(ShotProgram value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.InitialAimOffset));
        writer.Field(2, ActorWireValue.Int32(value.BendDirection));
        writer.Field(3, ActorWireValue.Int32(value.BendAfterTiles));
        writer.Field(4, ActorWireValue.Int32(value.BendEveryTiles));
        writer.Field(5, ActorWireValue.Int32(value.BendCount));
        return writer.ToArray();
    }

    public static ShotProgram DecodeShotProgram(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new ShotProgram(
            Int32(reader, 1),
            Int32(reader, 2),
            Int32(reader, 3),
            Int32(reader, 4),
            Int32(reader, 5));
    }

    public static T Decode<T>(Func<T> decode, string description)
    {
        try
        {
            return decode();
        }
        catch (FormatException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is ArgumentException
                or InvalidOperationException
                or OverflowException)
        {
            throw new FormatException(
                $"Malformed generic actor {description}.",
                exception);
        }
    }

    public static byte[] RequirePayloadLimit(
        byte[] payload,
        int maximum,
        string description)
    {
        if (payload.Length > maximum)
        {
            throw new InvalidOperationException(
                $"{description} exceeds its {maximum}-byte payload limit.");
        }
        return payload;
    }

    public static void RequirePayloadLimit(
        byte[] payload,
        int maximum,
        string description,
        bool decoding)
    {
        if (payload.Length > maximum)
        {
            throw new FormatException(
                $"{description} exceeds its {maximum}-byte payload limit.");
        }
    }
}
