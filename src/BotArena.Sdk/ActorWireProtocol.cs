using System.Buffers.Binary;

namespace BotArena.Sdk;

/// <summary>
/// Shared actor protocol 1.0 framing and payload codec. It is internal to the
/// framework assemblies so bot authors see typed SDK models, not wire details.
/// </summary>
internal static class ActorWireProtocol
{
    public const string Version =
        ActorContractVersions.RuntimeProtocolVersion;
    public const int MajorVersion = 1;
    public const int HeaderSize = 12;
    // A 32x32 ImmediateUnion observation repeats five-sensor provenance for
    // every visible tile and can also carry the live projectile/event field.
    // One MiB keeps that declared future topology inside protocol 1.0 while
    // remaining a hard allocation and copy bound.
    public const int MaxHostFrameBytes = 1024 * 1024;
    public const int MaxGuestFrameBytes = 64 * 1024;
    public const int MaxCollectionCount = 4096;
    public const int MaxDepth = 64;
    public const int MaxSemanticIdBytes = 64;

    private static ReadOnlySpan<byte> Magic => "NBV2"u8;

    public static bool HasMagic(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= Magic.Length
        && bytes[..Magic.Length].SequenceEqual(Magic);

    public static byte[] EncodeHello(int minimumMajor, int maximumMajor)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(minimumMajor));
        writer.Field(2, ActorWireValue.Int32(maximumMajor));
        return Frame(ActorWireMessageType.Hello, writer.ToArray());
    }

    public static byte[] EncodeHello(
        int minimumMajor,
        int maximumMajor,
        ActorContractProfile requiredProfile)
    {
        ArgumentNullException.ThrowIfNull(requiredProfile);

        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(minimumMajor));
        writer.Field(2, ActorWireValue.Int32(maximumMajor));
        writer.Field(3, EncodeContractProfile(requiredProfile));
        return Frame(ActorWireMessageType.Hello, writer.ToArray());
    }

    public static ActorWireHello DecodeHello(byte[] frame)
    {
        ActorWireFrame decoded = DecodeHostFrame(frame);
        Require(decoded, ActorWireMessageType.Hello);
        var reader = new ActorWireObjectReader(decoded.Payload, 0);
        byte[]? requiredProfile = reader.Optional(3);
        return new ActorWireHello(
            ActorWireValue.Int32(reader.Required(1)),
            ActorWireValue.Int32(reader.Required(2)),
            requiredProfile is null
                ? null
                : DecodeContractProfile(requiredProfile, 1));
    }

    public static byte[] EncodeHelloAck(int selectedMajor)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(selectedMajor));
        return Frame(ActorWireMessageType.HelloAck, writer.ToArray());
    }

    public static byte[] EncodeHelloAck(
        int selectedMajor,
        ActorContractProfile selectedProfile)
    {
        ArgumentNullException.ThrowIfNull(selectedProfile);
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(selectedMajor));
        writer.Field(2, EncodeContractProfile(selectedProfile));
        return Frame(ActorWireMessageType.HelloAck, writer.ToArray());
    }

    public static int DecodeHelloAck(byte[] frame) =>
        DecodeHelloAckContract(frame).SelectedMajor;

    public static ActorWireHelloAck DecodeHelloAckContract(byte[] frame)
    {
        ActorWireFrame decoded = DecodeGuestFrame(frame);
        Require(decoded, ActorWireMessageType.HelloAck);
        var reader = new ActorWireObjectReader(decoded.Payload, 0);
        byte[]? selectedProfile = reader.Optional(2);
        return new ActorWireHelloAck(
            ActorWireValue.Int32(reader.Required(1)),
            selectedProfile is null
                ? null
                : DecodeContractProfile(selectedProfile, 1));
    }

    public static byte[] EncodeMatchStart(
        string botName,
        ActorMatchStart start)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.String(botName, 256));
        writer.Field(2, ActorWireContractCodec.EncodeMatchStart(start));
        return Frame(ActorWireMessageType.MatchStart, writer.ToArray());
    }

    public static ActorWireMatchStart DecodeMatchStart(byte[] frame)
    {
        ActorWireFrame decoded = DecodeHostFrame(frame);
        Require(decoded, ActorWireMessageType.MatchStart);
        var reader = new ActorWireObjectReader(decoded.Payload, 0);
        return new ActorWireMatchStart(
            ActorWireValue.String(reader.Required(1), 256),
            ActorWireContractCodec.DecodeMatchStart(
                reader.Required(2),
                1));
    }

    public static byte[] EncodeGenericMatchStart(
        string botName,
        GenericActorMatchStart start)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.String(botName, 256));
        writer.Field(
            2,
            GenericActorWireContractCodec.EncodeMatchStart(start));
        return Frame(ActorWireMessageType.MatchStart, writer.ToArray());
    }

    public static ActorWireGenericMatchStart DecodeGenericMatchStart(
        byte[] frame)
    {
        ActorWireFrame decoded = DecodeHostFrame(frame);
        Require(decoded, ActorWireMessageType.MatchStart);
        var reader = new ActorWireObjectReader(decoded.Payload, 0);
        return new ActorWireGenericMatchStart(
            ActorWireValue.String(reader.Required(1), 256),
            GenericActorWireContractCodec.DecodeMatchStart(
                reader.Required(2),
                1));
    }

    public static byte[] EncodeReady(
        int selectedMajor,
        int runtimeContractVersion,
        int matchStartSchemaVersion,
        int observationSchemaVersion,
        int decisionSchemaVersion)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(selectedMajor));
        writer.Field(2, ActorWireValue.Int32(runtimeContractVersion));
        writer.Field(3, ActorWireValue.Int32(matchStartSchemaVersion));
        writer.Field(4, ActorWireValue.Int32(observationSchemaVersion));
        writer.Field(5, ActorWireValue.Int32(decisionSchemaVersion));
        return Frame(ActorWireMessageType.Ready, writer.ToArray());
    }

    public static byte[] EncodeReady(
        int selectedMajor,
        int runtimeContractVersion,
        int matchStartSchemaVersion,
        int observationSchemaVersion,
        int decisionSchemaVersion,
        ActorContractProfile selectedProfile)
    {
        ArgumentNullException.ThrowIfNull(selectedProfile);
        if (selectedProfile.RuntimeContractVersion != runtimeContractVersion
            || selectedProfile.MatchStartSchemaVersion
                != matchStartSchemaVersion
            || selectedProfile.ObservationSchemaVersion
                != observationSchemaVersion
            || selectedProfile.DecisionSchemaVersion
                != decisionSchemaVersion)
        {
            throw new ArgumentException(
                "Actor Ready versions must match the selected contract profile.",
                nameof(selectedProfile));
        }

        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(selectedMajor));
        writer.Field(2, ActorWireValue.Int32(runtimeContractVersion));
        writer.Field(3, ActorWireValue.Int32(matchStartSchemaVersion));
        writer.Field(4, ActorWireValue.Int32(observationSchemaVersion));
        writer.Field(5, ActorWireValue.Int32(decisionSchemaVersion));
        writer.Field(6, EncodeContractProfile(selectedProfile));
        return Frame(ActorWireMessageType.Ready, writer.ToArray());
    }

    public static ActorWireReady DecodeReady(byte[] frame)
    {
        ActorWireFrame decoded = DecodeGuestFrame(frame);
        Require(decoded, ActorWireMessageType.Ready);
        var reader = new ActorWireObjectReader(decoded.Payload, 0);
        byte[]? selectedProfile = reader.Optional(6);
        var ready = new ActorWireReady(
            ActorWireValue.Int32(reader.Required(1)),
            ActorWireValue.Int32(reader.Required(2)),
            ActorWireValue.Int32(reader.Required(3)),
            ActorWireValue.Int32(reader.Required(4)),
            ActorWireValue.Int32(reader.Required(5)),
            selectedProfile is null
                ? null
                : DecodeContractProfile(selectedProfile, 1));
        if (ready.SelectedProfile is { } profile
            && (profile.RuntimeContractVersion
                    != ready.RuntimeContractVersion
                || profile.MatchStartSchemaVersion
                    != ready.MatchStartSchemaVersion
                || profile.ObservationSchemaVersion
                    != ready.ObservationSchemaVersion
                || profile.DecisionSchemaVersion
                    != ready.DecisionSchemaVersion))
        {
            throw new FormatException(
                "Actor Ready versions do not match its selected contract profile.");
        }
        return ready;
    }

    public static byte[] EncodeObservation(ActorContext observation) =>
        Frame(
            ActorWireMessageType.Observation,
            ActorWireObservationCodec.Encode(observation));

    public static ActorContext DecodeObservation(byte[] frame)
    {
        ActorWireFrame decoded = DecodeHostFrame(frame);
        Require(decoded, ActorWireMessageType.Observation);
        return ActorWireObservationCodec.Decode(decoded.Payload);
    }

    public static byte[] EncodeDecision(ActorDecision decision) =>
        Frame(
            ActorWireMessageType.Decision,
            ActorWireDecisionCodec.Encode(decision));

    public static ActorDecision DecodeDecision(byte[] frame)
    {
        ActorWireFrame decoded = DecodeGuestFrame(frame);
        Require(decoded, ActorWireMessageType.Decision);
        return ActorWireDecisionCodec.Decode(decoded.Payload);
    }

    public static byte[] EncodeGenericObservation(
        GenericActorContext observation) =>
        Frame(
            ActorWireMessageType.Observation,
            GenericActorWireObservationCodec.Encode(observation));

    public static GenericActorContext DecodeGenericObservation(byte[] frame)
    {
        ActorWireFrame decoded = DecodeHostFrame(frame);
        Require(decoded, ActorWireMessageType.Observation);
        return GenericActorWireObservationCodec.Decode(decoded.Payload);
    }

    public static byte[] EncodeGenericDecision(
        GenericActorDecision decision) =>
        Frame(
            ActorWireMessageType.Decision,
            GenericActorWireDecisionCodec.Encode(decision));

    public static GenericActorDecision DecodeGenericDecision(byte[] frame)
    {
        ActorWireFrame decoded = DecodeGuestFrame(frame);
        Require(decoded, ActorWireMessageType.Decision);
        return GenericActorWireDecisionCodec.Decode(decoded.Payload);
    }

    // ---- the MIND profile ------------------------------------------------
    // MindStart, MindObservation and MindDecisions replace MatchStart,
    // Observation and Decision FOR THIS PROFILE, riding the same framing
    // message types with a different payload codec. That is the same shape the
    // per-life generation already uses: framing is a protocol matter, message
    // payloads are a profile matter, and the negotiated profile decides which
    // codec reads the bytes.

    public static byte[] EncodeMindStart(string botName, MindStart start)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.String(botName, 256));
        writer.Field(2, GenericMindWireStartCodec.Encode(start));
        return Frame(ActorWireMessageType.MatchStart, writer.ToArray());
    }

    public static ActorWireMindStart DecodeMindStart(byte[] frame)
    {
        ActorWireFrame decoded = DecodeHostFrame(frame);
        Require(decoded, ActorWireMessageType.MatchStart);
        var reader = new ActorWireObjectReader(decoded.Payload, 0);
        return new ActorWireMindStart(
            ActorWireValue.String(reader.Required(1), 256),
            GenericMindWireStartCodec.Decode(reader.Required(2), 1));
    }

    public static byte[] EncodeMindObservation(MindContext observation) =>
        Frame(
            ActorWireMessageType.Observation,
            GenericMindWireObservationCodec.EncodeFields(observation));

    public static MindContext DecodeMindObservation(
        byte[] frame,
        MindWaitAction waitAction)
    {
        ActorWireFrame decoded = DecodeHostFrame(frame);
        Require(decoded, ActorWireMessageType.Observation);
        return GenericMindWireObservationCodec.Decode(
            decoded.Payload,
            waitAction);
    }

    public static byte[] EncodeMindDecisions(MindDecisions decisions) =>
        Frame(
            ActorWireMessageType.Decision,
            GenericMindWireDecisionCodec.Encode(decisions));

    public static MindDecisions DecodeMindDecisions(byte[] frame)
    {
        ActorWireFrame decoded = DecodeGuestFrame(frame);
        Require(decoded, ActorWireMessageType.Decision);
        return GenericMindWireDecisionCodec.Decode(decoded.Payload);
    }

    public static byte[] EncodeFault(string message)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.String(message, 4096));
        return Frame(ActorWireMessageType.Fault, writer.ToArray());
    }

    public static string DecodeFault(byte[] frame)
    {
        ActorWireFrame decoded = DecodeGuestFrame(frame);
        Require(decoded, ActorWireMessageType.Fault);
        var reader = new ActorWireObjectReader(decoded.Payload, 0);
        return ActorWireValue.String(reader.Required(1), 4096);
    }

    public static byte[] EncodeUnsupported(
        string capability,
        string message)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(
            1,
            ActorWireValue.String(capability, MaxSemanticIdBytes));
        writer.Field(2, ActorWireValue.String(message, 4096));
        return Frame(
            ActorWireMessageType.Unsupported,
            writer.ToArray());
    }

    public static ActorWireUnsupported DecodeUnsupported(byte[] frame)
    {
        ActorWireFrame decoded = DecodeGuestFrame(frame);
        Require(decoded, ActorWireMessageType.Unsupported);
        var reader = new ActorWireObjectReader(decoded.Payload, 0);
        return new ActorWireUnsupported(
            ActorWireValue.String(
                reader.Required(1),
                MaxSemanticIdBytes),
            ActorWireValue.String(reader.Required(2), 4096));
    }

    public static byte[] EncodeMatchEnd(string reason)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.String(reason, 256));
        return Frame(ActorWireMessageType.MatchEnd, writer.ToArray());
    }

    public static string DecodeMatchEnd(byte[] frame)
    {
        ActorWireFrame decoded = DecodeHostFrame(frame);
        Require(decoded, ActorWireMessageType.MatchEnd);
        var reader = new ActorWireObjectReader(decoded.Payload, 0);
        return ActorWireValue.String(reader.Required(1), 256);
    }

    public static ActorWireMessageType PeekHostMessageType(byte[] frame) =>
        DecodeHostFrame(frame).MessageType;

    public static ActorWireMessageType PeekGuestMessageType(byte[] frame) =>
        DecodeGuestFrame(frame).MessageType;

    private static byte[] EncodeContractProfile(
        ActorContractProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.ProfileId)
            || profile.RuntimeContractVersion <= 0
            || profile.MatchStartSchemaVersion <= 0
            || profile.ObservationSchemaVersion <= 0
            || profile.DecisionSchemaVersion <= 0
            || profile.MatchContractSchemaVersion <= 0)
        {
            throw new InvalidOperationException(
                "Actor contract profile IDs must be present and versions positive.");
        }

        var writer = new ActorWireObjectWriter();
        writer.Field(
            1,
            ActorWireValue.String(
                profile.ProfileId,
                MaxSemanticIdBytes));
        writer.Field(
            2,
            ActorWireValue.Int32(profile.RuntimeContractVersion));
        writer.Field(
            3,
            ActorWireValue.Int32(profile.MatchStartSchemaVersion));
        writer.Field(
            4,
            ActorWireValue.Int32(profile.ObservationSchemaVersion));
        writer.Field(
            5,
            ActorWireValue.Int32(profile.DecisionSchemaVersion));
        writer.Field(
            6,
            ActorWireValue.Int32(profile.MatchContractSchemaVersion));
        return writer.ToArray();
    }

    private static ActorContractProfile DecodeContractProfile(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        var profile = new ActorContractProfile(
            ActorWireValue.String(
                reader.Required(1),
                MaxSemanticIdBytes),
            ActorWireValue.Int32(reader.Required(2)),
            ActorWireValue.Int32(reader.Required(3)),
            ActorWireValue.Int32(reader.Required(4)),
            ActorWireValue.Int32(reader.Required(5)),
            ActorWireValue.Int32(reader.Required(6)));
        if (string.IsNullOrWhiteSpace(profile.ProfileId)
            || profile.RuntimeContractVersion <= 0
            || profile.MatchStartSchemaVersion <= 0
            || profile.ObservationSchemaVersion <= 0
            || profile.DecisionSchemaVersion <= 0
            || profile.MatchContractSchemaVersion <= 0)
        {
            throw new FormatException(
                "Actor contract profile IDs must be present and versions positive.");
        }
        return profile;
    }

    private static byte[] Frame(
        ActorWireMessageType messageType,
        byte[] payload)
    {
        int totalLength = checked(HeaderSize + payload.Length);
        int limit = messageType is ActorWireMessageType.HelloAck
            or ActorWireMessageType.Ready
            or ActorWireMessageType.Decision
            or ActorWireMessageType.Fault
            or ActorWireMessageType.Unsupported
            ? MaxGuestFrameBytes
            : MaxHostFrameBytes;
        if (totalLength > limit)
            throw new InvalidOperationException(
                $"Actor frame exceeds its {limit}-byte limit.");

        byte[] frame = new byte[totalLength];
        Magic.CopyTo(frame);
        frame[4] = MajorVersion;
        frame[5] = (byte)messageType;
        BinaryPrimitives.WriteInt32LittleEndian(
            frame.AsSpan(8, 4),
            payload.Length);
        payload.CopyTo(frame, HeaderSize);
        return frame;
    }

    private static byte[] Frame(
        ActorWireMessageType messageType,
        ActorWireObjectWriter payload)
    {
        int totalLength = checked(HeaderSize + payload.Length);
        int limit = messageType is ActorWireMessageType.HelloAck
            or ActorWireMessageType.Ready
            or ActorWireMessageType.Decision
            or ActorWireMessageType.Fault
            or ActorWireMessageType.Unsupported
            ? MaxGuestFrameBytes
            : MaxHostFrameBytes;
        if (totalLength > limit)
        {
            throw new InvalidOperationException(
                $"Actor frame exceeds its {limit}-byte limit.");
        }

        byte[] frame = new byte[totalLength];
        Magic.CopyTo(frame);
        frame[4] = MajorVersion;
        frame[5] = (byte)messageType;
        BinaryPrimitives.WriteInt32LittleEndian(
            frame.AsSpan(8, 4),
            payload.Length);
        payload.WriteTo(frame, HeaderSize);
        return frame;
    }

    private static ActorWireFrame DecodeHostFrame(byte[] bytes) =>
        DecodeFrame(bytes, MaxHostFrameBytes);

    private static ActorWireFrame DecodeGuestFrame(byte[] bytes) =>
        DecodeFrame(bytes, MaxGuestFrameBytes);

    private static ActorWireFrame DecodeFrame(byte[] bytes, int limit)
    {
        if (bytes.Length > limit)
            throw new FormatException(
                $"Actor frame exceeds its {limit}-byte limit.");
        if (bytes.Length < HeaderSize || !HasMagic(bytes))
            throw new FormatException("Malformed actor frame header.");
        if (bytes[4] != MajorVersion)
            throw new FormatException(
                $"Unsupported actor protocol major {bytes[4]}.");
        if (bytes[6] != 0 || bytes[7] != 0)
            throw new FormatException("Actor frame uses unsupported flags.");

        int length = BinaryPrimitives.ReadInt32LittleEndian(
            bytes.AsSpan(8, 4));
        if (length < 0 || length != bytes.Length - HeaderSize)
            throw new FormatException("Actor frame length is invalid.");

        var messageType = (ActorWireMessageType)bytes[5];
        if (!Enum.IsDefined(messageType))
            throw new FormatException($"Unknown actor message type {bytes[5]}.");
        return new ActorWireFrame(
            messageType,
            bytes.AsSpan(HeaderSize).ToArray());
    }

    private static void Require(
        ActorWireFrame frame,
        ActorWireMessageType expected)
    {
        if (frame.MessageType != expected)
        {
            throw new FormatException(
                $"Expected actor message {expected}, got {frame.MessageType}.");
        }
    }
}

internal enum ActorWireMessageType : byte
{
    Hello = 1,
    HelloAck = 2,
    MatchStart = 3,
    Ready = 4,
    Observation = 5,
    Decision = 6,
    MatchEnd = 7,
    Fault = 8,
    Unsupported = 9,
}

internal readonly record struct ActorWireFrame(
    ActorWireMessageType MessageType,
    byte[] Payload);

internal readonly record struct ActorWireHello(
    int MinimumMajor,
    int MaximumMajor,
    ActorContractProfile? RequiredProfile);

internal readonly record struct ActorWireMatchStart(
    string BotName,
    ActorMatchStart Start);

internal readonly record struct ActorWireGenericMatchStart(
    string BotName,
    GenericActorMatchStart Start);

internal readonly record struct ActorWireMindStart(
    string BotName,
    MindStart Start);

internal readonly record struct ActorWireReady(
    int SelectedMajor,
    int RuntimeContractVersion,
    int MatchStartSchemaVersion,
    int ObservationSchemaVersion,
    int DecisionSchemaVersion,
    ActorContractProfile? SelectedProfile);

internal readonly record struct ActorWireUnsupported(
    string Capability,
    string Message);
