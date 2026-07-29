using BotArena.Engine;
using BotArena.Runtime;
using Sdk = BotArena.Sdk;

namespace BotArena.Runtime.Wasm;

/// <summary>
/// Exact-profile host facade for frozen schema-2 and current schema-3 generic
/// actor contracts over actor protocol 1.0.
/// </summary>
public static class GenericActorWasmProtocol
{
    public const string ProtocolVersion =
        BotArenaVersions.GenericActorRuntimeProtocolVersion;
    public const int MajorVersion = Sdk.ActorWireProtocol.MajorVersion;
    public const int HeaderSize = Sdk.ActorWireProtocol.HeaderSize;
    public const int MaxHostFrameBytes =
        Sdk.ActorWireProtocol.MaxHostFrameBytes;
    public const int MaxGuestFrameBytes =
        Sdk.ActorWireProtocol.MaxGuestFrameBytes;

    public static byte[] FormatHello() =>
        FormatHello(Sdk.ActorContractProfile.GenericV3);

    public static byte[] FormatHello(Sdk.ActorContractProfile profile) =>
        Sdk.ActorWireProtocol.EncodeHello(
            MajorVersion,
            MajorVersion,
            profile);

    public static int ParseHelloAck(ReadOnlySpan<byte> bytes) =>
        ParseHelloAck(bytes, Sdk.ActorContractProfile.GenericV3);

    public static int ParseHelloAck(
        ReadOnlySpan<byte> bytes,
        Sdk.ActorContractProfile requiredProfile)
    {
        byte[] frame = bytes.ToArray();
        if (!Sdk.ActorWireProtocol.HasMagic(frame))
        {
            throw new ActorProtocolNotSupportedException(
                "Artifact did not negotiate actor protocol 1.0 and is " +
                "ineligible for generic actor matches.");
        }

        ThrowIfUnsupported(frame);
        ThrowIfFault(frame);
        Sdk.ActorWireHelloAck ack =
            Sdk.ActorWireProtocol.DecodeHelloAckContract(frame);
        if (ack.SelectedMajor != MajorVersion)
        {
            throw new FormatException(
                $"Guest selected unsupported actor protocol major " +
                $"{ack.SelectedMajor}.");
        }
        if (ack.SelectedProfile != requiredProfile)
        {
            throw new FormatException(
                "Guest HelloAck did not select the exact generic actor " +
                "contract profile.");
        }
        return ack.SelectedMajor;
    }

    public static byte[] FormatMatchStart(
        GenericActorRuntimeStart start,
        string botName) =>
        Sdk.ActorWireProtocol.EncodeGenericMatchStart(
            botName,
            GenericActorSdkModelMapper.ToSdk(start));

    public static void ParseReady(
        ReadOnlySpan<byte> bytes,
        GenericActorRuntimeStart start)
    {
        ArgumentNullException.ThrowIfNull(start);
        byte[] frame = bytes.ToArray();
        ThrowIfUnsupported(frame);
        ThrowIfFault(frame);
        Sdk.ActorWireReady ready =
            Sdk.ActorWireProtocol.DecodeReady(frame);
        Sdk.ActorContractProfile requiredProfile =
            RequiredProfile(start);
        if (ready.SelectedMajor != MajorVersion
            || ready.SelectedProfile != requiredProfile
            || ready.RuntimeContractVersion != start.RuntimeContractVersion
            || ready.MatchStartSchemaVersion != start.SchemaVersion
            || ready.ObservationSchemaVersion
                != requiredProfile.ObservationSchemaVersion
            || ready.DecisionSchemaVersion
                != requiredProfile.DecisionSchemaVersion)
        {
            throw new FormatException(
                "Guest Ready does not attest the exact generic actor " +
                "contract profile.");
        }
    }

    public static byte[] FormatObservation(
        GenericActorRuntimeObservation observation) =>
        Sdk.ActorWireProtocol.EncodeGenericObservation(
            GenericActorSdkModelMapper.ToSdk(observation));

    public static GenericActorRuntimeDecision ParseDecision(
        ReadOnlySpan<byte> bytes)
    {
        byte[] frame = bytes.ToArray();
        ThrowIfUnsupported(frame);
        ThrowIfFault(frame);
        Sdk.ActorWireMessageType messageType =
            Sdk.ActorWireProtocol.PeekGuestMessageType(frame);
        if (messageType != Sdk.ActorWireMessageType.Decision)
        {
            throw new FormatException(
                $"Expected generic actor Decision, got {messageType}.");
        }

        Sdk.GenericActorDecision decision =
            Sdk.ActorWireProtocol.DecodeGenericDecision(frame);
        return GenericActorSdkModelMapper.ToEngine(
            decision,
            decision.DebugMessage);
    }

    public static byte[] FormatMatchEnd(string reason = "life-ended") =>
        Sdk.ActorWireProtocol.EncodeMatchEnd(reason);

    public static Sdk.ActorContractProfile RequiredProfile(
        GenericActorRuntimeStart start)
    {
        ArgumentNullException.ThrowIfNull(start);
        ActorMatchCapabilityVersions capabilities =
            start.Contract.CapabilityVersions;
        Sdk.ActorContractProfile profile;
        if (capabilities == ActorMatchCapabilityVersions.GenericV2)
        {
            profile = Sdk.ActorContractProfile.GenericV2;
        }
        else if (capabilities == ActorMatchCapabilityVersions.Current)
        {
            profile = Sdk.ActorContractProfile.GenericV3;
        }
        else
        {
            throw new FormatException(
                "MatchStart does not use a supported generic actor profile.");
        }
        if (start.RuntimeContractVersion != profile.RuntimeContractVersion
            || start.SchemaVersion != profile.MatchStartSchemaVersion)
        {
            throw new FormatException(
                "MatchStart versions do not match its generic actor profile.");
        }
        return profile;
    }

    private static void ThrowIfFault(byte[] frame)
    {
        if (Sdk.ActorWireProtocol.PeekGuestMessageType(frame)
            == Sdk.ActorWireMessageType.Fault)
        {
            throw new ActorWasmGuestException(
                Sdk.ActorWireProtocol.DecodeFault(frame));
        }
    }

    private static void ThrowIfUnsupported(byte[] frame)
    {
        if (Sdk.ActorWireProtocol.PeekGuestMessageType(frame)
            != Sdk.ActorWireMessageType.Unsupported)
        {
            return;
        }

        Sdk.ActorWireUnsupported unsupported =
            Sdk.ActorWireProtocol.DecodeUnsupported(frame);
        throw new ActorProtocolNotSupportedException(
            $"Artifact does not support required capability " +
            $"'{unsupported.Capability}': {unsupported.Message}");
    }
}
