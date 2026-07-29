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
            hello.MaximumMajor,
            hello.RequiredProfile);
    }

    public static ActorGuestContractGeneration SelectContractGeneration(
        ActorProtocolHello hello)
    {
        _ = SelectMajor(
            hello.MinimumMajor,
            hello.MaximumMajor);
        if (hello.RequiredProfile is null)
            return ActorGuestContractGeneration.LegacyActorV1;
        if (hello.RequiredProfile == ActorContractProfile.GenericV2)
            return ActorGuestContractGeneration.GenericActorV2;
        if (hello.RequiredProfile == ActorContractProfile.GenericV3)
            return ActorGuestContractGeneration.GenericActorV3;

        throw new ActorCapabilityNotSupportedException(
            "actor-contract-profile",
            $"This artifact does not support actor contract profile " +
            $"'{hello.RequiredProfile.ProfileId}'.");
    }

    public static byte[] FormatHelloAck(
        ActorProtocolHello hello,
        ActorGuestContractGeneration generation)
    {
        int selected = SelectMajor(
            hello.MinimumMajor,
            hello.MaximumMajor);
        return generation switch
        {
            ActorGuestContractGeneration.LegacyActorV1
                when hello.RequiredProfile is null =>
                ActorWireProtocol.EncodeHelloAck(selected),
            ActorGuestContractGeneration.GenericActorV2
                when hello.RequiredProfile == ActorContractProfile.GenericV2 =>
                ActorWireProtocol.EncodeHelloAck(
                    selected,
                    ActorContractProfile.GenericV2),
            ActorGuestContractGeneration.GenericActorV3
                when hello.RequiredProfile == ActorContractProfile.GenericV3 =>
                ActorWireProtocol.EncodeHelloAck(
                    selected,
                    ActorContractProfile.GenericV3),
            _ => throw new FormatException(
                "Actor contract selection does not match the host Hello."),
        };
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

    public static GenericActorMatchStartEnvelope ParseGenericMatchStart(
        ActorGuestFrame frame)
    {
        ActorWireGenericMatchStart start =
            ActorWireProtocol.DecodeGenericMatchStart(frame.Bytes);
        return new GenericActorMatchStartEnvelope(
            start.BotName,
            start.Start);
    }

    public static GenericActorContext ParseGenericObservation(
        ActorGuestFrame frame) =>
        ActorWireProtocol.DecodeGenericObservation(frame.Bytes);

    public static byte[] FormatReady(
        ActorGuestContractGeneration generation) =>
        generation switch
        {
            ActorGuestContractGeneration.LegacyActorV1 =>
                ActorWireProtocol.EncodeReady(
                    MajorVersion,
                    ActorContractVersions.RuntimeContractVersion,
                    ActorContractVersions.MatchStartSchemaVersion,
                    ActorContractVersions.ObservationSchemaVersion,
                    ActorContractVersions.DecisionSchemaVersion),
            ActorGuestContractGeneration.GenericActorV2 =>
                ActorWireProtocol.EncodeReady(
                    MajorVersion,
                    ActorContractProfile.GenericV2.RuntimeContractVersion,
                    ActorContractProfile.GenericV2.MatchStartSchemaVersion,
                    ActorContractProfile.GenericV2.ObservationSchemaVersion,
                    ActorContractProfile.GenericV2.DecisionSchemaVersion,
                    ActorContractProfile.GenericV2),
            ActorGuestContractGeneration.GenericActorV3 =>
                ActorWireProtocol.EncodeReady(
                    MajorVersion,
                    ActorContractProfile.GenericV3.RuntimeContractVersion,
                    ActorContractProfile.GenericV3.MatchStartSchemaVersion,
                    ActorContractProfile.GenericV3.ObservationSchemaVersion,
                    ActorContractProfile.GenericV3.DecisionSchemaVersion,
                    ActorContractProfile.GenericV3),
            _ => throw new InvalidOperationException(
                "Actor Ready requires a negotiated contract generation."),
        };

    public static byte[] FormatDecision(ActorDecision decision) =>
        ActorWireProtocol.EncodeDecision(decision);

    public static byte[] FormatGenericDecision(
        GenericActorDecision decision) =>
        ActorWireProtocol.EncodeGenericDecision(decision);

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
    int MaximumMajor,
    ActorContractProfile? RequiredProfile);

internal sealed record ActorMatchStartEnvelope(
    string BotName,
    ActorMatchStart Start);
