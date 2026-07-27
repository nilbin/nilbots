using System.Text;
using BotArena.Sdk;

namespace BotArena.Guest;

/// <summary>One actor-bot instance and deterministic service set for one life.</summary>
internal sealed class ActorGuestSession
{
    private readonly IActorBot _bot;
    private readonly GuestRandom _random;
    private readonly ActorIdentity _actorId;
    private readonly string _contractFingerprint;
    private int? _lastTick;
    private const int MaxDiagnosticBytes = 4096;

    private ActorGuestSession(IActorBot bot, ActorMatchStart start)
    {
        _bot = bot;
        _random = new GuestRandom(start.ActorRandomSeed);
        _actorId = start.ActorId;
        _contractFingerprint = start.Contract.MatchContractFingerprint;
        _bot.StartLife(start);
    }

    public static ActorGuestSession Start(
        ActorMatchStartEnvelope envelope,
        Func<string, IActorBot> botFactory)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(envelope.Start);
        ValidateStart(envelope.Start);
        IActorBot bot = botFactory(envelope.BotName)
            ?? throw new InvalidOperationException(
                "Actor bot factory returned null.");
        return new ActorGuestSession(bot, envelope.Start);
    }

    public ActorDecision HandleTick(ActorContext observation)
    {
        if (observation.SchemaVersion
            != ActorContractVersions.ObservationSchemaVersion)
        {
            throw new FormatException(
                $"Actor observation schema {observation.SchemaVersion} is unsupported.");
        }
        if (observation.Self.ActorId != _actorId)
            throw new FormatException("Actor observation identity changed within a life.");
        if (!string.Equals(
                observation.MatchContractFingerprint,
                _contractFingerprint,
                StringComparison.Ordinal))
        {
            throw new FormatException(
                "Actor observation contract fingerprint does not match MatchStart.");
        }
        if (_lastTick is int lastTick && observation.Tick <= lastTick)
            throw new FormatException("Actor observation ticks must increase.");

        var debug = new ActorGuestDebug();
        ActorDecision decision = _bot.Tick(
            observation with { Random = _random, Debug = debug })
            ?? throw new InvalidOperationException("Actor bot returned null.");
        _lastTick = observation.Tick;

        string? combined = TruncateUtf8(
            CombineDebug(
            decision.DebugMessage,
            debug.TextOrNull),
            MaxDiagnosticBytes);
        return decision with
        {
            DebugMessage = combined,
            Faulted = false,
            FaultMessage = null,
        };
    }

    private static void ValidateStart(ActorMatchStart start)
    {
        if (start.SchemaVersion
                != ActorContractVersions.MatchStartSchemaVersion
            || start.RuntimeContractVersion
                != ActorContractVersions.RuntimeContractVersion)
        {
            throw new FormatException(
                "Actor MatchStart contract or schema version is unsupported.");
        }
        if (start.ParticipantId < 0)
            throw new FormatException("Actor participant ID cannot be negative.");
        if (start.Contract.SchemaVersion
                != ActorContractVersions.MatchContractSchemaVersion
            || string.IsNullOrWhiteSpace(
                start.Contract.MatchContractFingerprint))
        {
            throw new FormatException(
                "Actor MatchStart public contract schema is unsupported or incomplete.");
        }
        if (!IsCanonicalSha256(
                start.Contract.MatchContractFingerprint))
        {
            throw new FormatException(
                "Actor match-contract fingerprint must be canonical SHA-256 hex.");
        }
        if (!Enum.IsDefined(start.SpawnReason))
            throw new FormatException("Actor MatchStart spawn reason is unknown.");
    }

    private static string? CombineDebug(string? returned, string? collected)
    {
        if (string.IsNullOrEmpty(returned))
            return collected;
        if (string.IsNullOrEmpty(collected))
            return returned;
        return returned + "\n" + collected;
    }

    private static bool IsCanonicalSha256(string value) =>
        value.Length == 64
        && value.All(character =>
            character is >= '0' and <= '9'
                or >= 'a' and <= 'f');

    private static string? TruncateUtf8(string? value, int maxBytes)
    {
        if (value is null
            || Encoding.UTF8.GetByteCount(value) <= maxBytes)
        {
            return value;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(value);
        int length = maxBytes;
        while (length > 0 && (bytes[length] & 0xC0) == 0x80)
            length--;
        return Encoding.UTF8.GetString(bytes, 0, length);
    }

    private sealed class ActorGuestDebug : IBotDebug
    {
        private const int MaxCharacters = 4096;
        private StringBuilder? _text;

        public string? TextOrNull => _text?.ToString();

        public void Write(string message)
        {
            _text ??= new StringBuilder();
            if (_text.Length >= MaxCharacters)
                return;
            if (_text.Length > 0)
                _text.Append('\n');
            int remaining = MaxCharacters - _text.Length;
            _text.Append(message.AsSpan(0, Math.Min(message.Length, remaining)));
        }

        public void Write(string format, params object?[] arguments) =>
            Write(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                format,
                arguments));
    }
}
