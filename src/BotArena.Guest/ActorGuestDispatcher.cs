using BotArena.Sdk;

namespace BotArena.Guest;

/// <summary>
/// Pure actor-frame state machine kept separate from the WASI import loop so
/// negotiation and generation dispatch can be exercised without a WASM host.
/// </summary>
internal sealed class ActorGuestDispatcher(
    Func<string, IActorBot>? actorFactory,
    Func<string, IGenericActorBot>? genericActorFactory)
{
    private readonly Func<string, IActorBot>? _actorFactory = actorFactory;
    private readonly Func<string, IGenericActorBot>? _genericActorFactory =
        genericActorFactory;
    private ActorGuestSession? _actorSession;
    private GenericActorGuestSession? _genericActorSession;
    private ActorGuestContractGeneration _generation =
        ActorGuestContractGeneration.None;
    private bool _helloReceived;
    private bool _ended;

    public bool HelloReceived => _helloReceived;

    public bool Negotiated =>
        _generation != ActorGuestContractGeneration.None;

    public bool HasSession =>
        _actorSession is not null || _genericActorSession is not null;

    /// <summary>
    /// Handles one complete actor host frame. Null means MatchEnd completed
    /// the life and no guest reply should be posted.
    /// </summary>
    public byte[]? Handle(ReadOnlySpan<byte> message)
    {
        if (_ended)
            throw new FormatException("Actor session has already ended.");

        try
        {
            ActorGuestFrame frame =
                ActorGuestProtocol.ParseHostFrame(message);
            switch (frame.MessageType)
            {
                case ActorWireMessageType.Hello:
                    return HandleHello(frame);

                case ActorWireMessageType.MatchStart:
                    return HandleMatchStart(frame);

                case ActorWireMessageType.Observation:
                    return HandleObservation(frame);

                case ActorWireMessageType.MatchEnd:
                    if (!HasSession)
                    {
                        throw new FormatException(
                            "Actor MatchEnd received before MatchStart.");
                    }
                    _ = ActorWireProtocol.DecodeMatchEnd(frame.Bytes);
                    _ended = true;
                    return null;

                default:
                    throw new FormatException(
                        $"Unexpected host actor message {frame.MessageType}.");
            }
        }
        catch
        {
            // Actor Fault and Unsupported replies are terminal by protocol.
            // Latch before the outer WASI loop formats the typed reply.
            _ended = true;
            throw;
        }
    }

    private byte[] HandleHello(ActorGuestFrame frame)
    {
        if (_helloReceived || Negotiated || HasSession)
        {
            throw new FormatException(
                "Actor protocol Hello may occur only once.");
        }
        _helloReceived = true;

        // Parse and select the host's exact profile before inspecting which
        // bot factories the artifact contains. Unknown profiles therefore
        // receive a precise capability response rather than payload sniffing.
        ActorProtocolHello hello = ActorGuestProtocol.ParseHello(frame);
        ActorGuestContractGeneration selected =
            ActorGuestProtocol.SelectContractGeneration(hello);
        EnsureContractFactory(selected);
        byte[] reply = ActorGuestProtocol.FormatHelloAck(hello, selected);
        _generation = selected;
        return reply;
    }

    private byte[] HandleMatchStart(ActorGuestFrame frame)
    {
        if (!Negotiated || HasSession)
        {
            throw new FormatException(
                "Actor MatchStart is out of sequence.");
        }

        switch (_generation)
        {
            case ActorGuestContractGeneration.LegacyActorV1:
                _actorSession = ActorGuestSession.Start(
                    ActorGuestProtocol.ParseMatchStart(frame),
                    _actorFactory!);
                break;

            case ActorGuestContractGeneration.GenericActorV2:
                _genericActorSession = GenericActorGuestSession.Start(
                    ActorGuestProtocol.ParseGenericMatchStart(frame),
                    _genericActorFactory!);
                break;

            default:
                throw new FormatException(
                    "Actor MatchStart contract generation is invalid.");
        }

        return ActorGuestProtocol.FormatReady(_generation);
    }

    private byte[] HandleObservation(ActorGuestFrame frame) =>
        _generation switch
        {
            ActorGuestContractGeneration.LegacyActorV1
                when _actorSession is not null =>
                ActorGuestProtocol.FormatDecision(
                    _actorSession.HandleTick(
                        ActorGuestProtocol.ParseObservation(frame))),
            ActorGuestContractGeneration.GenericActorV2
                when _genericActorSession is not null =>
                ActorGuestProtocol.FormatGenericDecision(
                    _genericActorSession.HandleTick(
                        ActorGuestProtocol.ParseGenericObservation(frame))),
            _ => throw new FormatException(
                "Actor observation received before MatchStart."),
        };

    private void EnsureContractFactory(
        ActorGuestContractGeneration generation)
    {
        if (generation is ActorGuestContractGeneration.LegacyActorV1
            && _actorFactory is null)
        {
            throw new ActorCapabilityNotSupportedException(
                "actor-runtime",
                "This artifact does not contain a legacy actor bot.");
        }
        if (generation is ActorGuestContractGeneration.GenericActorV2
            && _genericActorFactory is null)
        {
            throw new ActorCapabilityNotSupportedException(
                "actor-contract-profile",
                $"This artifact does not contain a bot for profile " +
                $"'{GenericActorContractVersions.ContractProfileId}'.");
        }
    }
}
