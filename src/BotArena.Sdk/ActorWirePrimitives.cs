using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;

namespace BotArena.Sdk;

/// <summary>Canonical tagged-object writer used only by actor protocol vNext.</summary>
internal sealed class ActorWireObjectWriter
{
    private readonly MemoryStream _stream = new();
    private ushort _lastFieldId;

    public void Field(ushort fieldId, byte[] value)
    {
        if (fieldId == 0 || fieldId <= _lastFieldId)
        {
            throw new InvalidOperationException(
                "Actor wire fields must be written once in increasing ID order.");
        }
        if (value.Length > ActorWireProtocol.MaxHostFrameBytes
            || _stream.Length
                > ActorWireProtocol.MaxHostFrameBytes - 6L - value.Length)
            throw new InvalidOperationException("Actor wire field is too large.");

        Span<byte> header = stackalloc byte[6];
        BinaryPrimitives.WriteUInt16LittleEndian(header, fieldId);
        BinaryPrimitives.WriteInt32LittleEndian(header[2..], value.Length);
        _stream.Write(header);
        _stream.Write(value);
        _lastFieldId = fieldId;
    }

    public void Optional(ushort fieldId, byte[]? value)
    {
        if (value is not null)
            Field(fieldId, value);
    }

    public byte[] ToArray() => _stream.ToArray();
}

/// <summary>
/// Tagged-object reader. Unknown IDs remain in the table and are skipped by
/// callers; duplicate fields, truncation, and excessive nesting fail early.
/// </summary>
internal sealed class ActorWireObjectReader
{
    private readonly Dictionary<ushort, byte[]> _fields = [];

    public ActorWireObjectReader(byte[] bytes, int depth)
    {
        if (depth > ActorWireProtocol.MaxDepth)
            throw new FormatException("Actor wire nesting limit exceeded.");
        if (bytes.Length > ActorWireProtocol.MaxHostFrameBytes)
            throw new FormatException("Actor wire object is too large.");

        int offset = 0;
        while (offset < bytes.Length)
        {
            if (bytes.Length - offset < 6)
                throw new FormatException("Truncated actor wire field header.");
            ushort fieldId = BinaryPrimitives.ReadUInt16LittleEndian(
                bytes.AsSpan(offset, 2));
            int length = BinaryPrimitives.ReadInt32LittleEndian(
                bytes.AsSpan(offset + 2, 4));
            offset += 6;
            if (fieldId == 0
                || length < 0
                || length > bytes.Length - offset)
            {
                throw new FormatException("Malformed actor wire field.");
            }
            if (!_fields.TryAdd(
                    fieldId,
                    bytes.AsSpan(offset, length).ToArray()))
            {
                throw new FormatException(
                    $"Duplicate actor wire field {fieldId}.");
            }
            offset += length;
        }
    }

    public byte[] Required(ushort fieldId) =>
        _fields.TryGetValue(fieldId, out byte[]? value)
            ? value
            : throw new FormatException(
                $"Missing required actor wire field {fieldId}.");

    public byte[]? Optional(ushort fieldId) =>
        _fields.GetValueOrDefault(fieldId);
}

/// <summary>Primitive and collection encodings shared by every actor message.</summary>
internal static class ActorWireValue
{
    private static readonly UTF8Encoding StrictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static byte[] Int32(int value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return bytes;
    }

    public static int Int32(byte[] bytes)
    {
        Exact(bytes, 4, "int32");
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }

    public static byte[] UInt64(ulong value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        return bytes;
    }

    public static ulong UInt64(byte[] bytes)
    {
        Exact(bytes, 8, "uint64");
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }

    public static byte[] Boolean(bool value) => [value ? (byte)1 : (byte)0];

    public static bool Boolean(byte[] bytes)
    {
        Exact(bytes, 1, "boolean");
        return bytes[0] switch
        {
            0 => false,
            1 => true,
            _ => throw new FormatException("Actor wire boolean is not 0 or 1."),
        };
    }

    public static byte[] String(string value, int maxBytes = 4096)
    {
        ArgumentNullException.ThrowIfNull(value);
        int byteCount = StrictUtf8.GetByteCount(value);
        if (byteCount > maxBytes)
            throw new InvalidOperationException(
                $"Actor wire string exceeds {maxBytes} UTF-8 bytes.");
        return StrictUtf8.GetBytes(value);
    }

    public static string String(byte[] bytes, int maxBytes = 4096)
    {
        if (bytes.Length > maxBytes)
            throw new FormatException(
                $"Actor wire string exceeds {maxBytes} UTF-8 bytes.");
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new FormatException(
                "Actor wire string is not valid UTF-8.",
                exception);
        }
    }

    public static byte[] Enum<T>(T value) where T : struct, Enum
    {
        if (!System.Enum.IsDefined(value))
            throw new InvalidOperationException(
                $"Undefined actor wire enum {typeof(T).Name}.{value}.");
        return Int32(Convert.ToInt32(value));
    }

    public static T Enum<T>(byte[] bytes) where T : struct, Enum
    {
        int value = Int32(bytes);
        var result = (T)System.Enum.ToObject(typeof(T), value);
        if (!System.Enum.IsDefined(result))
            throw new FormatException(
                $"Undefined actor wire enum {typeof(T).Name} value {value}.");
        return result;
    }

    public static byte[] Array<T>(
        IEnumerable<T> values,
        Func<T, byte[]> encode)
    {
        ArgumentNullException.ThrowIfNull(values);
        T[] materialized = values.ToArray();
        if (materialized.Length > ActorWireProtocol.MaxCollectionCount)
            throw new InvalidOperationException(
                "Actor wire collection exceeds its item limit.");

        using var stream = new MemoryStream();
        Span<byte> integer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(integer, materialized.Length);
        stream.Write(integer);
        foreach (T item in materialized)
        {
            byte[] encoded = encode(item);
            if (encoded.Length > ActorWireProtocol.MaxHostFrameBytes
                || stream.Length
                    > ActorWireProtocol.MaxHostFrameBytes
                        - 4L
                        - encoded.Length)
            {
                throw new InvalidOperationException(
                    "Actor wire collection exceeds the frame limit.");
            }
            BinaryPrimitives.WriteInt32LittleEndian(integer, encoded.Length);
            stream.Write(integer);
            stream.Write(encoded);
        }
        return stream.ToArray();
    }

    public static ImmutableArray<T> Array<T>(
        byte[] bytes,
        Func<byte[], T> decode)
    {
        if (bytes.Length < 4)
            throw new FormatException("Truncated actor wire collection.");
        int count = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (count < 0 || count > ActorWireProtocol.MaxCollectionCount)
            throw new FormatException("Actor wire collection count is invalid.");

        var result = ImmutableArray.CreateBuilder<T>(count);
        int offset = 4;
        for (int index = 0; index < count; index++)
        {
            if (bytes.Length - offset < 4)
                throw new FormatException("Truncated actor wire collection item.");
            int length = BinaryPrimitives.ReadInt32LittleEndian(
                bytes.AsSpan(offset, 4));
            offset += 4;
            if (length < 0 || length > bytes.Length - offset)
                throw new FormatException("Malformed actor wire collection item.");
            result.Add(decode(bytes.AsSpan(offset, length).ToArray()));
            offset += length;
        }
        if (offset != bytes.Length)
            throw new FormatException("Actor wire collection has trailing bytes.");
        return result.MoveToImmutable();
    }

    public static byte[] Nullable<T>(
        T? value,
        Func<T, byte[]> encode)
        where T : struct =>
        value is T present
            ? encode(present)
            : throw new InvalidOperationException(
                "Null values are represented by an absent tagged field.");

    private static void Exact(byte[] bytes, int expected, string kind)
    {
        if (bytes.Length != expected)
            throw new FormatException(
                $"Actor wire {kind} must be exactly {expected} bytes.");
    }
}
