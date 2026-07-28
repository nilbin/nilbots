using System.Collections.Immutable;
using System.Text;

namespace BotArena.Sdk;

internal static class GenericActorDynamicValueRules
{
    private static readonly UTF8Encoding StrictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static string SemanticId(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (value.Length == 0
            || value.Length > ActorWireProtocol.MaxSemanticIdBytes)
        {
            throw new ArgumentException(
                "A semantic ID must contain 1-64 lowercase-kebab characters.",
                parameterName);
        }

        bool needsSegmentStart = true;
        foreach (char character in value)
        {
            if (character == '-')
            {
                if (needsSegmentStart)
                    throw InvalidSemanticId(parameterName);
                needsSegmentStart = true;
                continue;
            }

            if (character is not (>= 'a' and <= 'z')
                and not (>= '0' and <= '9'))
            {
                throw InvalidSemanticId(parameterName);
            }
            needsSegmentStart = false;
        }

        if (needsSegmentStart)
            throw InvalidSemanticId(parameterName);
        return value;
    }

    public static string Fingerprint(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (value.Length != 64
            || value.Any(character =>
                character is not (>= '0' and <= '9')
                    and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "A fingerprint must be exactly 64 lowercase hexadecimal characters.",
                parameterName);
        }
        return value;
    }

    public static string Text(
        string value,
        int maximumUtf8Bytes,
        string parameterName,
        bool allowEmpty = true)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (!allowEmpty && value.Length == 0)
            throw new ArgumentException("Text cannot be empty.", parameterName);

        try
        {
            if (StrictUtf8.GetByteCount(value) > maximumUtf8Bytes)
            {
                throw new ArgumentException(
                    $"Text exceeds {maximumUtf8Bytes} UTF-8 bytes.",
                    parameterName);
            }
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException(
                "Text contains invalid Unicode.",
                parameterName,
                exception);
        }
        return value;
    }

    public static string Handle(string value, string parameterName) =>
        Text(value, 256, parameterName, allowEmpty: false);

    public static T EnumValue<T>(T value, string parameterName)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(parameterName);
        return value;
    }

    public static ImmutableArray<T> Snapshot<T>(
        IEnumerable<T> values,
        string parameterName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        T[] snapshot = [.. values];
        if (snapshot.Any(value => value is null))
        {
            throw new ArgumentException(
                "Collections cannot contain null entries.",
                parameterName);
        }
        if (snapshot.Length > ActorWireProtocol.MaxCollectionCount)
        {
            throw new ArgumentException(
                "Collection exceeds the actor wire item limit.",
                parameterName);
        }
        return snapshot.ToImmutableArray();
    }

    public static ImmutableArray<T> SnapshotValues<T>(
        IEnumerable<T> values,
        string parameterName)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        T[] snapshot = [.. values];
        if (snapshot.Length > ActorWireProtocol.MaxCollectionCount)
        {
            throw new ArgumentException(
                "Collection exceeds the actor wire item limit.",
                parameterName);
        }
        return snapshot.ToImmutableArray();
    }

    public static ImmutableArray<ActorIdentity> CanonicalActors(
        IEnumerable<ActorIdentity> values,
        string parameterName)
    {
        ImmutableArray<ActorIdentity> snapshot = Snapshot(
            values,
            parameterName);
        ActorIdentity[] ordered = snapshot.Order().ToArray();
        EnsureUnique(ordered, parameterName);
        return ordered.ToImmutableArray();
    }

    public static void EnsureUnique<T>(
        IEnumerable<T> values,
        string parameterName)
    {
        var seen = new HashSet<T>();
        foreach (T value in values)
        {
            if (!seen.Add(value))
            {
                throw new ArgumentException(
                    "Collection entries must be unique.",
                    parameterName);
            }
        }
    }

    private static ArgumentException InvalidSemanticId(
        string parameterName) =>
        new(
            "A semantic ID must use non-empty lowercase-kebab segments.",
            parameterName);
}
