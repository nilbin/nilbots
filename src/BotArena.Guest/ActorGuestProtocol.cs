using BotArena.Sdk;

namespace BotArena.Guest;

/// <summary>Guest state-machine facade over the shared actor wire codec.</summary>
internal static class ActorGuestProtocol
{
    public const int HeaderSize = ActorWireProtocol.HeaderSize;
    public const int MajorVersion = ActorWireProtocol.MajorVersion;
    public const int MaxHostFrameBytes = ActorWireProtocol.MaxHostFrameBytes;
    public const int MaxGuestFrameBytes = ActorWireProtocol.MaxGuestFrameBytes;

    public static bool HasMagic(ReadOnlySpan<byte> bytes) =>
        ActorWireProtocol.HasMagic(bytes);

    public static ActorGuestFrame ParseHostFrame(ReadOnlySpan<byte> bytes)
    {
        byte[] frame = bytes.ToArray();
        return new ActorGuestFrame(
            ActorWireProtocol.PeekHostMessageType(frame),
            frame);
    }

    public static ActorProtocolHello ParseHello(ActorGuestFrame frame)
    {
        ActorWireHello hello = ActorWireProtocol.DecodeHello(frame.Bytes);
        return new ActorProtocolHello(
            hello.MinimumMajor,
            hello.MaximumMajor);
    }

    public static byte[] FormatHelloAck(ActorProtocolHello hello)
    {
        int selected = SelectMajor(
            hello.MinimumMajor,
            hello.MaximumMajor);
        return ActorWireProtocol.EncodeHelloAck(selected);
    }

    public static ActorMatchStartEnvelope ParseMatchStart(
        ActorGuestFrame frame)
    {
        ActorWireMatchStart start =
            ActorWireProtocol.DecodeMatchStart(frame.Bytes);
        return new ActorMatchStartEnvelope(start.BotName, start.Start);
    }

    public static ActorContext ParseObservation(ActorGuestFrame frame) =>
        ActorWireProtocol.DecodeObservation(frame.Bytes);

    public static byte[] FormatReady() =>
        ActorWireProtocol.EncodeReady(
            MajorVersion,
            ActorContractVersions.RuntimeContractVersion,
            ActorContractVersions.MatchStartSchemaVersion,
            ActorContractVersions.ObservationSchemaVersion,
            ActorContractVersions.DecisionSchemaVersion);

    public static byte[] FormatDecision(ActorDecision decision) =>
        ActorWireProtocol.EncodeDecision(decision);

    public static byte[] FormatFault(string message) =>
        ActorWireProtocol.EncodeFault(message);

    public static byte[] FormatUnsupported(
        string capability,
        string message) =>
        ActorWireProtocol.EncodeUnsupported(capability, message);

    private static int SelectMajor(int minimum, int maximum)
    {
        if (minimum < 0
            || maximum < minimum
            || MajorVersion < minimum
            || MajorVersion > maximum)
        {
            throw new NotSupportedException(
                $"Actor protocol {minimum}..{maximum} is not supported.");
        }
        return MajorVersion;
    }
}

internal readonly record struct ActorGuestFrame(
    ActorWireMessageType MessageType,
    byte[] Bytes);

internal readonly record struct ActorProtocolHello(
    int MinimumMajor,
    int MaximumMajor);

internal sealed record ActorMatchStartEnvelope(
    string BotName,
    ActorMatchStart Start);
