using System.Text;
using BotArena.Sdk;

namespace BotArena.Guest;

/// <summary>
/// One mind instance and its deterministic service set, for one whole match.
///
/// <para>The contrast with <see cref="GenericActorGuestSession"/> is the point
/// of the profile: that class exists once per LIFE and validates that the
/// observation's identity never changes, because a life is exactly one body.
/// This class exists once per PARTICIPANT and validates nothing about identity,
/// because bodies arrive and die inside it. What it does validate is the
/// invariant a mind depends on — ticks strictly increase, and the contract
/// fingerprint never changes under it.</para>
/// </summary>
internal sealed class GenericMindGuestSession
{
    private const int MaxDiagnosticBytes = 4096;

    private readonly IGenericMindBot _bot;
    private readonly GuestRandom _random;
    private readonly GuestTeamRandom _teamRandom;
    private readonly string _contractFingerprint;
    private int? _lastTick;
    private bool _ended;

    private GenericMindGuestSession(IGenericMindBot bot, MindStart start)
    {
        _bot = bot;
        _random = new GuestRandom(start.MindRandomSeed);
        _teamRandom = new GuestTeamRandom(start.TeamRandomSeed);
        _contractFingerprint = start.Contract.MatchContractFingerprint;
        WaitAction = ResolveWaitAction(start);
        _bot.StartMatch(start);
    }

    /// <summary>
    /// The contract's wait action, resolved once so every decoded
    /// <see cref="MindBody"/> can hold itself without searching the catalog.
    /// </summary>
    public MindWaitAction WaitAction { get; }

    public static GenericMindGuestSession Start(
        MindStartEnvelope envelope,
        Func<string, IGenericMindBot> botFactory)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(envelope.Start);
        ArgumentNullException.ThrowIfNull(botFactory);
        ValidateStart(envelope.Start);
        IGenericMindBot bot = botFactory(envelope.BotName)
            ?? throw new InvalidOperationException(
                "Mind bot factory returned null.");
        return new GenericMindGuestSession(bot, envelope.Start);
    }

    public MindDecisions HandleTick(MindContext observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.SchemaVersion
            != GenericMindContractVersions.ObservationSchemaVersion)
        {
            throw new FormatException(
                $"Mind observation schema {observation.SchemaVersion} is unsupported.");
        }
        if (!string.Equals(
                observation.MatchContractFingerprint,
                _contractFingerprint,
                StringComparison.Ordinal))
        {
            throw new FormatException(
                "Mind observation contract fingerprint does not match MindStart.");
        }
        if (_lastTick is int lastTick && observation.Tick <= lastTick)
        {
            throw new FormatException(
                "Mind observation ticks must increase.");
        }

        // Re-derive the team stream for this exact tick before the mind runs,
        // so its first team draw is the team's first draw of the tick. Inside
        // one mind that guarantee is inert; it exists for allied minds.
        _teamRandom.BeginTick(observation.Tick);
        var debug = new MindGuestDebug();
        MindContext context = observation with
        {
            Random = _random,
            TeamRandom = _teamRandom,
            Debug = debug,
        };
        _bot.Think(context);
        _lastTick = observation.Tick;

        // Harvest from the SAME body objects the mind wrote onto: `with`
        // copies the array reference, not the bodies, so a command written
        // inside Think is on the object this reads.
        return new MindDecisions(
            GenericMindContractVersions.DecisionSchemaVersion,
            observation.Tick,
            context.HarvestCommands(),
            intents: null,
            TruncateUtf8(debug.TextOrNull, MaxDiagnosticBytes));
    }

    public void EndMatch(string reason)
    {
        if (_ended)
            return;
        _ended = true;
        _bot.EndMatch(new MindEnd(reason));
    }

    private static void ValidateStart(MindStart start)
    {
        if (start.SchemaVersion
                != GenericMindContractVersions.MatchStartSchemaVersion
            || start.RuntimeContractVersion
                != GenericMindContractVersions.RuntimeContractVersion)
        {
            throw new FormatException(
                "MindStart contract or schema version is unsupported.");
        }
        if (start.ParticipantId < 0 || start.TeamId < 0)
        {
            throw new FormatException(
                "Mind participant and team IDs cannot be negative.");
        }
        // The resolved match contract is CARRIED at schema 2 and its capability
        // tuple must name the mind profile: the game is unchanged, only the
        // driver is, and a contract that says otherwise is not this profile's.
        if (start.Contract.SchemaVersion
                != GenericMindContractVersions.MatchContractSchemaVersion
            || !string.Equals(
                start.Contract.CapabilityVersions.ContractProfileId,
                GenericMindContractVersions.ContractProfileId,
                StringComparison.Ordinal)
            || start.Contract.CapabilityVersions.RuntimeContractVersion
                != GenericMindContractVersions.RuntimeContractVersion
            || start.Contract.CapabilityVersions.MatchStartSchemaVersion
                != GenericMindContractVersions.MatchStartSchemaVersion
            || start.Contract.CapabilityVersions.ObservationSchemaVersion
                != GenericMindContractVersions.ObservationSchemaVersion
            || start.Contract.CapabilityVersions.DecisionSchemaVersion
                != GenericMindContractVersions.DecisionSchemaVersion
            || start.Contract.CapabilityVersions.MatchContractSchemaVersion
                != GenericMindContractVersions.MatchContractSchemaVersion)
        {
            throw new FormatException(
                "MindStart capability profile is unsupported.");
        }
        if (!IsCanonicalSha256(start.Contract.MatchContractFingerprint))
        {
            throw new FormatException(
                "Mind match-contract fingerprint must be canonical SHA-256 hex.");
        }
    }

    private static MindWaitAction ResolveWaitAction(MindStart start)
    {
        GenericActorRulesContract.ActionDefinition? wait =
            start.Contract.Rules.Actions.FirstOrDefault(action =>
                action.Kind == GenericActorRulesContract.ActionKind.Wait);
        return wait is null
            ? new MindWaitAction(null, 0)
            : new MindWaitAction(wait.Id, wait.Code);
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

    private sealed class MindGuestDebug : IBotDebug
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
            _text.Append(
                message.AsSpan(0, Math.Min(message.Length, remaining)));
        }

        public void Write(string format, params object?[] arguments) =>
            Write(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                format,
                arguments));
    }
}
