using BotArena.Engine;
using BotArena.Runtime;
using Sdk = BotArena.Sdk;

namespace BotArena.Runtime.Wasm;

/// <summary>
/// Exact-profile host facade for the schema-1 MIND contract over actor
/// protocol 1.0. Framing is unchanged; the negotiated profile decides which
/// payload codec reads the bytes.
/// </summary>
public static class GenericMindWasmProtocol
{
    public const string ProtocolVersion =
        BotArenaVersions.GenericMindRuntimeProtocolVersion;
    public const string ConfigurationVersion =
        BotArenaVersions.GenericMindRuntimeConfigurationVersion;
    public const int MajorVersion = Sdk.ActorWireProtocol.MajorVersion;
    public const int HeaderSize = Sdk.ActorWireProtocol.HeaderSize;
    public const int MaxHostFrameBytes =
        Sdk.ActorWireProtocol.MaxHostFrameBytes;
    public const int MaxGuestFrameBytes =
        Sdk.ActorWireProtocol.MaxGuestFrameBytes;

    public static byte[] FormatHello() =>
        Sdk.ActorWireProtocol.EncodeHello(
            MajorVersion,
            MajorVersion,
            Sdk.ActorContractProfile.MindV1);

    public static int ParseHelloAck(ReadOnlySpan<byte> bytes)
    {
        byte[] frame = bytes.ToArray();
        if (!Sdk.ActorWireProtocol.HasMagic(frame))
        {
            throw new ActorProtocolNotSupportedException(
                "Artifact did not negotiate actor protocol 1.0 and is "
                + "ineligible for mind matches.");
        }

        ThrowIfUnsupported(frame);
        ThrowIfFault(frame);
        Sdk.ActorWireHelloAck ack =
            Sdk.ActorWireProtocol.DecodeHelloAckContract(frame);
        if (ack.SelectedMajor != MajorVersion)
        {
            throw new FormatException(
                $"Guest selected unsupported actor protocol major "
                + $"{ack.SelectedMajor}.");
        }
        if (ack.SelectedProfile != Sdk.ActorContractProfile.MindV1)
        {
            throw new FormatException(
                "Guest HelloAck did not select the exact mind contract "
                + "profile.");
        }
        return ack.SelectedMajor;
    }

    public static byte[] FormatMindStart(
        GenericMindRuntimeStart start,
        string botName) =>
        Sdk.ActorWireProtocol.EncodeMindStart(
            botName,
            GenericMindSdkModelMapper.ToSdk(start));

    /// <summary>
    /// Checks that the guest attested the EXACT mind schema tuple compiled into
    /// its own artifact.
    ///
    /// <para>Two of the four attested numbers are what this profile exists to
    /// pin. The mind RUNTIME CONTRACT version is what fixes the fuel and memory
    /// semantics of configuration 2.0 — a guest built against the per-life
    /// budgets would run under the wrong ceiling. And the DECISION schema is
    /// what separates a decision map from a single decision: an artifact
    /// compiled against a one-action reply cannot answer a mind observation at
    /// all, and finding that out at tick 0 rather than mid-match is the whole
    /// point of attestation.</para>
    /// </summary>
    public static void ParseReady(
        ReadOnlySpan<byte> bytes,
        GenericMindRuntimeStart start)
    {
        ArgumentNullException.ThrowIfNull(start);
        byte[] frame = bytes.ToArray();
        ThrowIfUnsupported(frame);
        ThrowIfFault(frame);
        Sdk.ActorWireReady ready = Sdk.ActorWireProtocol.DecodeReady(frame);
        if (ready.SelectedMajor != MajorVersion
            || ready.SelectedProfile != Sdk.ActorContractProfile.MindV1
            || ready.RuntimeContractVersion != start.RuntimeContractVersion
            || ready.MatchStartSchemaVersion != start.SchemaVersion
            || ready.ObservationSchemaVersion
                != BotArenaVersions.GenericMindObservationSchemaVersion
            || ready.DecisionSchemaVersion
                != BotArenaVersions.GenericMindDecisionSchemaVersion)
        {
            throw new FormatException(
                "Guest Ready does not attest the exact mind contract profile.");
        }
    }

    public static byte[] FormatObservation(
        GenericMindRuntimeObservation observation,
        Sdk.MindWaitAction waitAction) =>
        Sdk.ActorWireProtocol.EncodeMindObservation(
            GenericMindSdkModelMapper.ToSdk(observation, waitAction));

    /// <summary>
    /// Decodes one mind reply and refuses a stale echoed tick. Under a
    /// correlated request/reply protocol there is exactly one reply per
    /// released request, so a reply naming another tick is not a late answer —
    /// it is a broken guest, and treating it as one is what keeps the exchange
    /// honest.
    /// </summary>
    public static GenericMindRuntimeDecisions ParseDecisions(
        ReadOnlySpan<byte> bytes,
        int expectedTick)
    {
        byte[] frame = bytes.ToArray();
        ThrowIfUnsupported(frame);
        ThrowIfFault(frame);
        Sdk.ActorWireMessageType messageType =
            Sdk.ActorWireProtocol.PeekGuestMessageType(frame);
        if (messageType != Sdk.ActorWireMessageType.Decision)
        {
            throw new FormatException(
                $"Expected mind Decisions, got {messageType}.");
        }

        Sdk.MindDecisions decisions =
            Sdk.ActorWireProtocol.DecodeMindDecisions(frame);
        if (decisions.SchemaVersion
            != BotArenaVersions.GenericMindDecisionSchemaVersion)
        {
            throw new FormatException(
                $"Mind decision schema {decisions.SchemaVersion} is "
                + "unsupported.");
        }
        if (decisions.Tick != expectedTick)
        {
            throw new FormatException(
                $"Mind replied for tick {decisions.Tick} while tick "
                + $"{expectedTick} was outstanding.");
        }
        return GenericMindSdkModelMapper.ToEngine(decisions);
    }

    public static byte[] FormatMatchEnd(string reason = "match-ended") =>
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
            $"Artifact does not support required capability "
            + $"'{unsupported.Capability}': {unsupported.Message}");
    }
}
