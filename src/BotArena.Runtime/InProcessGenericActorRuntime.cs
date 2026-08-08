using System.Text;
using BotArena.Engine;
using Sdk = BotArena.Sdk;

namespace BotArena.Runtime;

/// <summary>One diagnostic in-process generic SDK bot for one body life.</summary>
public sealed class InProcessGenericActorRuntime(
    Func<Sdk.IGenericActorBot> botFactory) : IGenericActorRuntime
{
    private const int MaxDiagnosticBytes = 4096;

    private Sdk.IGenericActorBot? _bot;
    private DeterministicRandom? _random;
    private TeamTickRandom? _teamRandom;
    private bool _started;

    public void StartLife(GenericActorRuntimeStart start)
    {
        if (_started)
        {
            throw new InvalidOperationException(
                "A generic actor runtime can start only one life.");
        }

        ArgumentNullException.ThrowIfNull(start);
        _started = true;
        _bot = botFactory()
            ?? throw new InvalidOperationException(
                "Generic actor bot factory returned null.");
        _random = new DeterministicRandom(start.ActorRandomSeed);
        _teamRandom = new TeamTickRandom(start.TeamRandomSeed);
        _bot.StartLife(GenericActorSdkModelMapper.ToSdk(start));
    }

    public GenericActorRuntimeDecision ExecuteTick(
        GenericActorRuntimeObservation observation)
    {
        if (_bot is null || _random is null || _teamRandom is null)
        {
            throw new InvalidOperationException(
                "StartLife must be called before ExecuteTick.");
        }

        // Re-derive the team stream for this exact tick before the bot runs,
        // so its first team draw is the team's first draw of the tick no
        // matter what this life drew on earlier ticks.
        _teamRandom.BeginTick(observation.Tick);
        var debug = new DebugCollector();
        Sdk.GenericActorContext context =
            GenericActorSdkModelMapper.ToSdk(observation) with
            {
                Random = new SdkRandom(_random),
                TeamRandom = new SdkTeamRandom(_teamRandom),
                Debug = debug,
            };
        Sdk.GenericActorDecision sdkDecision = _bot.Tick(context)
            ?? throw new InvalidOperationException(
                "Generic actor bot returned null.");
        return GenericActorSdkModelMapper.ToEngine(
            sdkDecision,
            TruncateUtf8(
                CombineDebug(
                    sdkDecision.DebugMessage,
                    debug.TextOrNull),
                MaxDiagnosticBytes));
    }

    private static string? CombineDebug(string? returned, string? collected)
    {
        if (string.IsNullOrEmpty(returned))
            return collected;
        if (string.IsNullOrEmpty(collected))
            return returned;
        return returned + "\n" + collected;
    }

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

    private sealed class SdkRandom(
        DeterministicRandom inner) : Sdk.IBotRandom
    {
        public int NextInt(
            int minimumInclusive,
            int maximumExclusive) =>
            inner.NextInt(minimumInclusive, maximumExclusive);

        public bool NextBool() => inner.NextBool();

        public double NextDouble() => inner.NextDouble();
    }

    private sealed class SdkTeamRandom(
        TeamTickRandom inner) : Sdk.IBotRandom
    {
        public int NextInt(
            int minimumInclusive,
            int maximumExclusive) =>
            inner.NextInt(minimumInclusive, maximumExclusive);

        public bool NextBool() => inner.NextBool();

        public double NextDouble() => inner.NextDouble();
    }

    private sealed class DebugCollector : Sdk.IBotDebug
    {
        private StringBuilder? _text;
        private int _byteCount;

        public string? TextOrNull => _text?.ToString();

        public void Write(string message)
        {
            ArgumentNullException.ThrowIfNull(message);
            _text ??= new StringBuilder();
            if (_byteCount >= MaxDiagnosticBytes)
                return;
            if (_text.Length > 0)
            {
                _text.Append('\n');
                _byteCount++;
            }

            int remaining = MaxDiagnosticBytes - _byteCount;
            if (remaining == 0)
                return;

            Span<byte> buffer = stackalloc byte[remaining];
            Encoding.UTF8.GetEncoder().Convert(
                message.AsSpan(),
                buffer,
                flush: true,
                out int charsUsed,
                out int bytesUsed,
                out _);
            _text.Append(message.AsSpan(0, charsUsed));
            _byteCount += bytesUsed;
        }

        public void Write(string format, params object?[] arguments) =>
            Write(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                format,
                arguments));
    }
}
