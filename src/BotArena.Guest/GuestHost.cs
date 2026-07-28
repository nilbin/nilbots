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

    /// <summary>
    /// Controlled-build entry point. The closed bot type determines which
    /// programming models the artifact can negotiate without constructing a
    /// throwaway bot instance. A type may deliberately implement more than one
    /// interface; the host's negotiated contract selects the invoked factory.
    /// </summary>
    public static int RunDetected<TBot>(Func<TBot> botFactory)
    {
        ArgumentNullException.ThrowIfNull(botFactory);
        bool supportsLegacy = typeof(IBot).IsAssignableFrom(typeof(TBot));
        bool supportsActor = typeof(IActorBot).IsAssignableFrom(typeof(TBot));
        bool supportsGeneric =
            typeof(IGenericActorBot).IsAssignableFrom(typeof(TBot));
        if (!supportsLegacy && !supportsActor && !supportsGeneric)
        {
            throw new InvalidOperationException(
                $"{typeof(TBot).FullName} does not implement a Nilbots bot interface.");
        }

        object Create()
        {
            TBot bot = botFactory();
            return (object?)bot
                ?? throw new InvalidOperationException(
                    "Bot factory returned null.");
        }

        return RunCore(
            supportsLegacy
                ? _ => (IBot)Create()
                : null,
            supportsActor
                ? _ => (IActorBot)Create()
                : null,
            supportsGeneric
                ? _ => (IGenericActorBot)Create()
                : null);
    }

    /// <summary>Single-bot artifact (the normal player case).</summary>
    public static int Run(Func<IBot> botFactory) =>
        RunCore(
            _ => botFactory(),
            actorFactory: null,
            genericActorFactory: null);

    /// <summary>Multi-bot artifact: the factory receives the bot name from the init line
    /// (used by the built-in opponents artifact).</summary>
    public static int Run(Func<string, IBot> botFactory) =>
        RunCore(
            botFactory,
            actorFactory: null,
            genericActorFactory: null);

    /// <summary>Single entity-bot artifact for the legacy actor contract.</summary>
    public static int Run(Func<IActorBot> botFactory) =>
        RunCore(
            legacyFactory: null,
            _ => botFactory(),
            genericActorFactory: null);

    /// <summary>
    /// Multi-bot entity artifact. The factory receives the framework-owned bot
    /// selector carried beside MatchStart.
    /// </summary>
    public static int Run(Func<string, IActorBot> botFactory) =>
        RunCore(
            legacyFactory: null,
            botFactory,
            genericActorFactory: null);

    /// <summary>
    /// Single generic entity-bot artifact. The explicit method name avoids
    /// overload ambiguity for bots that intentionally implement more than one
    /// SDK interface.
    /// </summary>
    public static int RunGeneric(Func<IGenericActorBot> botFactory) =>
        RunCore(
            legacyFactory: null,
            actorFactory: null,
            _ => botFactory());

    /// <summary>
    /// Multi-bot generic entity artifact. The factory receives the
    /// framework-owned selector carried beside MatchStart.
    /// </summary>
    public static int RunGeneric(
        Func<string, IGenericActorBot> botFactory) =>
        RunCore(
            legacyFactory: null,
            actorFactory: null,
            botFactory);

    /// <summary>
    /// Framework/test artifact supporting both historical duel bots and
    /// entity bots. Player artifacts normally use one of the single-family
    /// overloads above.
    /// </summary>
    public static int Run(
        Func<string, IBot> legacyFactory,
        Func<string, IActorBot> actorFactory) =>
        RunCore(
            legacyFactory,
            actorFactory,
            genericActorFactory: null);

    private static unsafe int RunCore(
        Func<string, IBot>? legacyFactory,
        Func<string, IActorBot>? actorFactory,
        Func<string, IGenericActorBot>? genericActorFactory)
    {
        // The first actor Hello is tiny. Preserve the historical 128 KiB
        // allocation for legacy-only artifacts and grow only after vNext
        // negotiation succeeds.
        var buffer = new byte[LegacyBufferBytes];
        GuestSession? legacySession = null;
        var actorDispatcher = new ActorGuestDispatcher(
            actorFactory,
            genericActorFactory);
        while (true)
        {
            int length;
            fixed (byte* pointer = buffer)
                length = next_observation(pointer, buffer.Length);
            if (length <= 0)
                return 0;

            byte[] replyBytes;
            bool actorMessage = false;
            bool terminateAfterReply = false;
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

                    byte[]? actorReply = actorDispatcher.Handle(message);
                    if (actorReply is null)
                        return 0;
                    replyBytes = actorReply;
                }
                else
                {
                    if (actorDispatcher.HelloReceived
                        || actorDispatcher.Negotiated
                        || actorDispatcher.HasSession)
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
                terminateAfterReply = true;
            }
            catch (Exception ex)
            {
                string message = BoundProtocolFault(
                    $"{ex.GetType().Name}: {ex.Message}");
                bool actorFault = actorMessage
                    || actorDispatcher.HelloReceived
                    || actorDispatcher.Negotiated
                    || actorDispatcher.HasSession;
                replyBytes = actorFault
                    ? ActorGuestProtocol.FormatFault(message)
                    : Encoding.UTF8.GetBytes(
                        GuestProtocol.FormatFault(message));
                terminateAfterReply = actorFault;
            }

            fixed (byte* pointer = replyBytes)
                post_decision(pointer, replyBytes.Length);

            if (terminateAfterReply)
                return 0;

            if (actorDispatcher.Negotiated
                && buffer.Length < ActorGuestProtocol.MaxHostFrameBytes)
            {
                buffer = new byte[ActorGuestProtocol.MaxHostFrameBytes];
            }
        }
    }

    private static string BoundProtocolFault(string message)
    {
        const int maxBytes = 4096;
        // Encoding.UTF8 replaces invalid UTF-16 (for example a lone
        // surrogate from an exception message). Always round-trip, even when
        // already under the byte limit, so formatting the fault cannot itself
        // fail in the strict wire string encoder.
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        if (bytes.Length <= maxBytes)
            return Encoding.UTF8.GetString(bytes);

        int length = maxBytes;
        while (length > 0 && (bytes[length] & 0xC0) == 0x80)
            length--;
        return Encoding.UTF8.GetString(bytes, 0, length);
    }
}
