using System.Text;
using BotArena.Sdk;

namespace BotArena.Guest;

/// <summary>
/// One generic actor-bot instance and deterministic service set for one life.
/// </summary>
internal sealed class GenericActorGuestSession
{
    private const int MaxDiagnosticBytes = 4096;

    private readonly IGenericActorBot _bot;
    private readonly GuestRandom _random;
    private readonly ActorIdentity _actorId;
    private readonly int _generation;
    private readonly string _contractFingerprint;
    private int? _lastTick;

    private GenericActorGuestSession(
        IGenericActorBot bot,
        GenericActorMatchStart start)
    {
        _bot = bot;
        _random = new GuestRandom(start.ActorRandomSeed);
        _actorId = start.ActorId;
        _generation = start.Origin.Generation;
        _contractFingerprint = start.Contract.MatchContractFingerprint;
        _bot.StartLife(start);
    }

    public static GenericActorGuestSession Start(
        GenericActorMatchStartEnvelope envelope,
        Func<string, IGenericActorBot> botFactory)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(envelope.Start);
        ArgumentNullException.ThrowIfNull(botFactory);
        ValidateStart(envelope.Start);
        IGenericActorBot bot = botFactory(envelope.BotName)
            ?? throw new InvalidOperationException(
                "Generic actor bot factory returned null.");
        return new GenericActorGuestSession(bot, envelope.Start);
    }

    public GenericActorDecision HandleTick(GenericActorContext observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.SchemaVersion
            != GenericActorContractVersions.ObservationSchemaVersion)
        {
            throw new FormatException(
                $"Generic actor observation schema {observation.SchemaVersion} is unsupported.");
        }
        if (observation.Self.ActorId != _actorId)
        {
            throw new FormatException(
                "Generic actor observation identity changed within a life.");
        }
        if (observation.Self.Generation != _generation)
        {
            throw new FormatException(
                "Generic actor generation changed within a life.");
        }
        if (!string.Equals(
                observation.MatchContractFingerprint,
                _contractFingerprint,
                StringComparison.Ordinal))
        {
            throw new FormatException(
                "Generic actor observation contract fingerprint does not match MatchStart.");
        }
        if (_lastTick is int lastTick && observation.Tick <= lastTick)
        {
            throw new FormatException(
                "Generic actor observation ticks must increase.");
        }

        var debug = new GenericActorGuestDebug();
        GenericActorDecision decision = _bot.Tick(
            observation with { Random = _random, Debug = debug })
            ?? throw new InvalidOperationException(
                "Generic actor bot returned null.");
        _lastTick = observation.Tick;

        return new GenericActorDecision(
            decision.ActionId,
            decision.ActionCode,
            decision.Arguments,
            TruncateUtf8(
                CombineDebug(decision.DebugMessage, debug.TextOrNull),
                MaxDiagnosticBytes));
    }

    private static void ValidateStart(GenericActorMatchStart start)
    {
        if (start.SchemaVersion
                != GenericActorContractVersions.MatchStartSchemaVersion
            || start.RuntimeContractVersion
                != GenericActorContractVersions.RuntimeContractVersion)
        {
            throw new FormatException(
                "Generic actor MatchStart contract or schema version is unsupported.");
        }
        if (start.ParticipantId < 0)
        {
            throw new FormatException(
                "Generic actor participant ID cannot be negative.");
        }
        if (start.Contract.SchemaVersion
                != GenericActorContractVersions.MatchContractSchemaVersion
            || !string.Equals(
                start.Contract.CapabilityVersions.ContractProfileId,
                GenericActorContractVersions.ContractProfileId,
                StringComparison.Ordinal)
            || start.Contract.CapabilityVersions.RuntimeContractVersion
                != GenericActorContractVersions.RuntimeContractVersion
            || start.Contract.CapabilityVersions.MatchStartSchemaVersion
                != GenericActorContractVersions.MatchStartSchemaVersion
            || start.Contract.CapabilityVersions.ObservationSchemaVersion
                != GenericActorContractVersions.ObservationSchemaVersion
            || start.Contract.CapabilityVersions.DecisionSchemaVersion
                != GenericActorContractVersions.DecisionSchemaVersion
            || start.Contract.CapabilityVersions.MatchContractSchemaVersion
                != GenericActorContractVersions.MatchContractSchemaVersion)
        {
            throw new FormatException(
                "Generic actor MatchStart capability profile is unsupported.");
        }
        if (!IsCanonicalSha256(start.Contract.MatchContractFingerprint))
        {
            throw new FormatException(
                "Generic actor match-contract fingerprint must be canonical SHA-256 hex.");
        }
        if (!Enum.IsDefined(start.Origin.Reason)
            || start.Origin.Generation < 0)
        {
            throw new FormatException(
                "Generic actor MatchStart life origin is invalid.");
        }
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

    private sealed class GenericActorGuestDebug : IBotDebug
    {
        private const int MaxCharacters = 4096;
        private StringBuilder? _text;

        public string? TextOrNull => _text?.ToString();

        public void Write(string message)
        {
            ArgumentNullException.ThrowIfNull(message);
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
