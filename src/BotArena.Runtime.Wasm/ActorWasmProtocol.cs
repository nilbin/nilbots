using System.Text;
using BotArena.Engine;
using Sdk = BotArena.Sdk;

namespace BotArena.Runtime.Wasm;

/// <summary>
/// Host facade for actor protocol 1.0. The historical line protocol stays in
/// <see cref="WasmProtocol"/>; the tagged payload codec is shared with the
/// guest through the SDK assembly so the two halves cannot drift.
/// </summary>
public static class ActorWasmProtocol
{
    public const string ProtocolVersion =
        BotArenaVersions.ActorRuntimeProtocolVersion;
    public const int MajorVersion = Sdk.ActorWireProtocol.MajorVersion;
    public const int HeaderSize = Sdk.ActorWireProtocol.HeaderSize;
    public const int MaxHostFrameBytes =
        Sdk.ActorWireProtocol.MaxHostFrameBytes;
    public const int MaxGuestFrameBytes =
        Sdk.ActorWireProtocol.MaxGuestFrameBytes;
    public const int MaxSemanticIdBytes =
        Sdk.ActorWireProtocol.MaxSemanticIdBytes;

    public static byte[] FormatHello() =>
        Sdk.ActorWireProtocol.EncodeHello(MajorVersion, MajorVersion);

    public static int ParseHelloAck(ReadOnlySpan<byte> bytes)
    {
        byte[] frame = bytes.ToArray();
        if (!Sdk.ActorWireProtocol.HasMagic(frame))
        {
            throw new ActorProtocolNotSupportedException(
                "Artifact did not negotiate actor protocol 1.0 and is ineligible for Frontline.");
        }
        ThrowIfUnsupported(frame);
        ThrowIfFault(frame);
        int selected = Sdk.ActorWireProtocol.DecodeHelloAck(frame);
        if (selected != MajorVersion)
        {
            throw new FormatException(
                $"Guest selected unsupported actor protocol major {selected}.");
        }
        return selected;
    }

    public static byte[] FormatMatchStart(
        ActorMatchStart start,
        string botName) =>
        Sdk.ActorWireProtocol.EncodeMatchStart(
            botName,
            ActorWasmModelMapper.ToSdk(start));

    public static void ParseReady(
        ReadOnlySpan<byte> bytes,
        ActorMatchStart start)
    {
        byte[] frame = bytes.ToArray();
        ThrowIfUnsupported(frame);
        ThrowIfFault(frame);
        Sdk.ActorWireReady ready =
            Sdk.ActorWireProtocol.DecodeReady(frame);
        if (ready.SelectedMajor != MajorVersion
            || ready.RuntimeContractVersion != start.RuntimeContractVersion
            || ready.MatchStartSchemaVersion != start.SchemaVersion
            || ready.ObservationSchemaVersion
                != BotArenaVersions.ActorObservationSchemaVersion
            || ready.DecisionSchemaVersion
                != BotArenaVersions.ActorDecisionSchemaVersion)
        {
            throw new FormatException(
                "Guest Ready does not match the selected actor contract.");
        }
    }

    public static byte[] FormatObservation(ActorObservation observation) =>
        Sdk.ActorWireProtocol.EncodeObservation(
            ActorWasmModelMapper.ToSdk(observation));

    public static ActorDecision ParseDecision(ReadOnlySpan<byte> bytes)
    {
        byte[] frame = bytes.ToArray();
        ThrowIfFault(frame);
        Sdk.ActorWireMessageType messageType =
            Sdk.ActorWireProtocol.PeekGuestMessageType(frame);
        if (messageType != Sdk.ActorWireMessageType.Decision)
        {
            throw new FormatException(
                $"Expected actor Decision, got {messageType}.");
        }

        Sdk.ActorDecision sdkDecision =
            Sdk.ActorWireProtocol.DecodeDecision(frame);
        ValidateDecision(sdkDecision);
        return ActorWasmModelMapper.ToEngine(sdkDecision);
    }

    public static byte[] FormatMatchEnd(string reason = "life-ended") =>
        Sdk.ActorWireProtocol.EncodeMatchEnd(reason);

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

    private static void ValidateDecision(Sdk.ActorDecision decision)
    {
        if (decision.ActionId is { } actionId
            && !IsCanonicalSemanticId(actionId))
        {
            throw new FormatException("Guest action ID is not canonical.");
        }
        if (decision.Payload?.FormTargetId is { } formTargetId
            && !IsCanonicalSemanticId(formTargetId))
        {
            throw new FormatException(
                "Guest form target ID is not canonical.");
        }
        if (decision.DebugMessage is { } debug
            && Encoding.UTF8.GetByteCount(debug) > 4096)
        {
            throw new FormatException(
                "Guest diagnostic message exceeds 4096 UTF-8 bytes.");
        }
        if (decision.Faulted || decision.FaultMessage is not null)
        {
            throw new FormatException(
                "A Decision frame cannot carry guest fault state.");
        }
        Sdk.ActorActionPayload? payload = decision.Payload;
        if (payload?.Direction is Sdk.Direction direction
            && !Enum.IsDefined(direction))
        {
            throw new FormatException("Guest action direction is undefined.");
        }
        if (payload?.LaunchHeading is Sdk.ProjectileHeading heading
            && !Enum.IsDefined(heading))
        {
            throw new FormatException(
                "Guest projectile heading is undefined.");
        }
        if (payload?.UnitTarget is Sdk.ObservedUnitTarget target
            && (target.TeamId < 0 || target.UnitId < 0))
        {
            throw new FormatException("Guest unit target cannot be negative.");
        }
    }

    private static bool IsCanonicalSemanticId(string value) =>
        value.Length is > 0 and <= MaxSemanticIdBytes
        && value.All(character =>
            character is >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '-');
}
