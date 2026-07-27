using System.Runtime.InteropServices;
using System.Text;
using BotArena.Sdk;

namespace BotArena.Guest;

/// <summary>
/// The guest-side runtime loop. A bot artifact's Program.cs is one line:
/// <c>return GuestHost.Run(() => new MyBot());</c>
/// The host feeds protocol messages through botarena::next_observation and reads replies
/// from botarena::post_decision; a zero-length read ends the life loop.
/// </summary>
public static class GuestHost
{
    private const int LegacyBufferBytes = 128 * 1024;

    [WasmImportLinkage]
    [DllImport("botarena")]
    private static extern unsafe int next_observation(byte* buffer, int capacity);

    [WasmImportLinkage]
    [DllImport("botarena")]
    private static extern unsafe void post_decision(byte* buffer, int length);

    /// <summary>Single-bot artifact (the normal player case).</summary>
    public static int Run(Func<IBot> botFactory) =>
        RunCore(_ => botFactory(), actorFactory: null);

    /// <summary>Multi-bot artifact: the factory receives the bot name from the init line
    /// (used by the built-in opponents artifact).</summary>
    public static int Run(Func<string, IBot> botFactory) =>
        RunCore(botFactory, actorFactory: null);

    /// <summary>Single entity-bot artifact for actor protocol vNext.</summary>
    public static int Run(Func<IActorBot> botFactory) =>
        RunCore(legacyFactory: null, _ => botFactory());

    /// <summary>
    /// Multi-bot entity artifact. The factory receives the framework-owned bot
    /// selector carried beside MatchStart.
    /// </summary>
    public static int Run(Func<string, IActorBot> botFactory) =>
        RunCore(legacyFactory: null, botFactory);

    /// <summary>
    /// Framework/test artifact supporting both historical duel bots and
    /// entity bots. Player artifacts normally use one of the single-family
    /// overloads above.
    /// </summary>
    public static int Run(
        Func<string, IBot> legacyFactory,
        Func<string, IActorBot> actorFactory) =>
        RunCore(legacyFactory, actorFactory);

    private static unsafe int RunCore(
        Func<string, IBot>? legacyFactory,
        Func<string, IActorBot>? actorFactory)
    {
        // The first actor Hello is tiny. Preserve the historical 128 KiB
        // allocation for legacy-only artifacts and grow only after vNext
        // negotiation succeeds.
        var buffer = new byte[LegacyBufferBytes];
        GuestSession? legacySession = null;
        ActorGuestSession? actorSession = null;
        bool actorNegotiated = false;
        while (true)
        {
            int length;
            fixed (byte* pointer = buffer)
                length = next_observation(pointer, buffer.Length);
            if (length <= 0)
                return 0;

            byte[] replyBytes;
            bool actorMessage = false;
            try
            {
                ReadOnlySpan<byte> message = buffer.AsSpan(0, length);
                actorMessage = ActorGuestProtocol.HasMagic(message);
                if (actorMessage)
                {
                    if (legacySession is not null)
                    {
                        throw new FormatException(
                            "A legacy session cannot switch to actor protocol.");
                    }
                    if (actorFactory is null)
                    {
                        throw new ActorCapabilityNotSupportedException(
                            "actor-runtime",
                            "This artifact does not contain an actor bot.");
                    }

                    ActorGuestFrame frame =
                        ActorGuestProtocol.ParseHostFrame(message);
                    switch (frame.MessageType)
                    {
                        case ActorWireMessageType.Hello:
                            if (actorNegotiated || actorSession is not null)
                                throw new FormatException(
                                    "Actor protocol Hello may occur only once.");
                            ActorProtocolHello hello =
                                ActorGuestProtocol.ParseHello(frame);
                            replyBytes =
                                ActorGuestProtocol.FormatHelloAck(hello);
                            actorNegotiated = true;
                            break;

                        case ActorWireMessageType.MatchStart:
                            if (!actorNegotiated || actorSession is not null)
                                throw new FormatException(
                                    "Actor MatchStart is out of sequence.");
                            ActorMatchStartEnvelope envelope =
                                ActorGuestProtocol.ParseMatchStart(frame);
                            actorSession = ActorGuestSession.Start(
                                envelope,
                                actorFactory);
                            replyBytes = ActorGuestProtocol.FormatReady();
                            break;

                        case ActorWireMessageType.Observation:
                            if (actorSession is null)
                                throw new FormatException(
                                    "Actor observation received before MatchStart.");
                            ActorContext observation =
                                ActorGuestProtocol.ParseObservation(frame);
                            ActorDecision decision =
                                actorSession.HandleTick(observation);
                            replyBytes =
                                ActorGuestProtocol.FormatDecision(decision);
                            break;

                        case ActorWireMessageType.MatchEnd:
                            return 0;

                        default:
                            throw new FormatException(
                                $"Unexpected host actor message {frame.MessageType}.");
                    }
                }
                else
                {
                    if (actorNegotiated || actorSession is not null)
                    {
                        throw new FormatException(
                            "Actor protocol switched to a legacy text message.");
                    }

                    string line = Encoding.UTF8.GetString(buffer, 0, length);
                    string legacyReply;
                    if (line.StartsWith("I ", StringComparison.Ordinal)
                        && legacyFactory is not null)
                    {
                        legacySession = GuestSession.Start(
                            line,
                            legacyFactory);
                        legacyReply = "R " + GuestProtocol.ProtocolVersion;
                    }
                    else if (legacySession is not null)
                    {
                        legacyReply = legacySession.HandleTick(line);
                    }
                    else
                    {
                        legacyReply = GuestProtocol.FormatFault(
                            "Tick received before init.");
                    }
                    replyBytes = Encoding.UTF8.GetBytes(legacyReply);
                }
            }
            catch (ActorCapabilityNotSupportedException ex)
            {
                replyBytes = ActorGuestProtocol.FormatUnsupported(
                    ex.Capability,
                    BoundProtocolFault(ex.Message));
            }
            catch (Exception ex)
            {
                string message = BoundProtocolFault(
                    $"{ex.GetType().Name}: {ex.Message}");
                replyBytes = actorMessage
                    || actorNegotiated
                    || actorSession is not null
                    ? ActorGuestProtocol.FormatFault(message)
                    : Encoding.UTF8.GetBytes(
                        GuestProtocol.FormatFault(message));
            }

            fixed (byte* pointer = replyBytes)
                post_decision(pointer, replyBytes.Length);

            if (actorNegotiated
                && buffer.Length < ActorGuestProtocol.MaxHostFrameBytes)
            {
                buffer = new byte[ActorGuestProtocol.MaxHostFrameBytes];
            }
        }
    }

    private static string BoundProtocolFault(string message)
    {
        const int maxBytes = 4096;
        if (Encoding.UTF8.GetByteCount(message) <= maxBytes)
            return message;

        byte[] bytes = Encoding.UTF8.GetBytes(message);
        int length = maxBytes;
        while (length > 0 && (bytes[length] & 0xC0) == 0x80)
            length--;
        return Encoding.UTF8.GetString(bytes, 0, length);
    }

    private sealed class ActorCapabilityNotSupportedException(
        string capability,
        string message)
        : NotSupportedException(message)
    {
        public string Capability { get; } = capability;
    }
}
