using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;

namespace BotArena.Sdk;

/// <summary>Canonical tagged-object writer used only by actor protocol vNext.</summary>
internal sealed class ActorWireObjectWriter
{
    private readonly List<FieldValue> _fields = [];
    private ushort _lastFieldId;
    private int _length;

    public void Field(ushort fieldId, byte[] value)
    {
        if (fieldId == 0 || fieldId <= _lastFieldId)
        {
            throw new InvalidOperationException(
                "Actor wire fields must be written once in increasing ID order.");
        }
        if (value.Length > ActorWireProtocol.MaxHostFrameBytes
            || _length > ActorWireProtocol.MaxHostFrameBytes - 6 - value.Length)
            throw new InvalidOperationException("Actor wire field is too large.");
        _fields.Add(new FieldValue(fieldId, value));
        _length += 6 + value.Length;
        _lastFieldId = fieldId;
    }

    public void Optional(ushort fieldId, byte[]? value)
    {
        if (value is not null)
            Field(fieldId, value);
    }

    public int Length => _length;

    public byte[] ToArray()
    {
        byte[] result = new byte[_length];
        WriteTo(result, 0);
        return result;
    }

    public void WriteTo(byte[] destination, int destinationOffset)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (destinationOffset < 0
            || destinationOffset > destination.Length - _length)
        {
            throw new ArgumentOutOfRangeException(nameof(destinationOffset));
        }
        int offset = destinationOffset;
        foreach (FieldValue field in _fields)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(
                destination.AsSpan(offset, 2),
                field.FieldId);
            BinaryPrimitives.WriteInt32LittleEndian(
                destination.AsSpan(offset + 2, 4),
                field.Value.Length);
            offset += 6;
            field.Value.CopyTo(destination, offset);
            offset += field.Value.Length;
        }
    }

    private readonly record struct FieldValue(ushort FieldId, byte[] Value);
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
        ArgumentNullException.ThrowIfNull(encode);

        IEnumerable<T> items = values;
        if (!values.TryGetNonEnumeratedCount(out int count))
        {
            T[] materialized = values.ToArray();
            items = materialized;
            count = materialized.Length;
        }
        if (count > ActorWireProtocol.MaxCollectionCount)
            throw new InvalidOperationException(
                "Actor wire collection exceeds its item limit.");

        byte[][] encodedItems = count == 0
            ? []
            : new byte[count][];
        int totalLength = 4;
        int index = 0;
        foreach (T item in items)
        {
            byte[] encoded = encode(item);
            if (encoded.Length > ActorWireProtocol.MaxHostFrameBytes
                || totalLength > ActorWireProtocol.MaxHostFrameBytes
                    - 4
                    - encoded.Length)
            {
                throw new InvalidOperationException(
                    "Actor wire collection exceeds the frame limit.");
            }
            if (index >= encodedItems.Length)
            {
                throw new InvalidOperationException(
                    "Actor wire collection count changed while encoding.");
            }
            encodedItems[index++] = encoded;
            totalLength += 4 + encoded.Length;
        }
        if (index != encodedItems.Length)
        {
            throw new InvalidOperationException(
                "Actor wire collection count changed while encoding.");
        }

        byte[] result = new byte[totalLength];
        BinaryPrimitives.WriteInt32LittleEndian(result, count);
        int offset = 4;
        foreach (byte[] encoded in encodedItems)
        {
            BinaryPrimitives.WriteInt32LittleEndian(
                result.AsSpan(offset, 4),
                encoded.Length);
            offset += 4;
            encoded.CopyTo(result, offset);
            offset += encoded.Length;
        }
        return result;
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
