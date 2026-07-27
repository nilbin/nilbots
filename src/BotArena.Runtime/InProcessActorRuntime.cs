using System.Text;
using BotArena.Engine;

namespace BotArena.Runtime;

/// <summary>One diagnostic in-process SDK actor instance for one body life.</summary>
public sealed class InProcessActorRuntime(
    Func<Sdk.IActorBot> botFactory) : IActorRuntime
{
    private const int MaxDiagnosticBytes = 4096;

    private Sdk.IActorBot? _bot;
    private DeterministicRandom? _random;
    private bool _started;

    public void StartLife(ActorMatchStart start)
    {
        if (_started)
            throw new InvalidOperationException("An actor runtime can start only one life.");

        _started = true;
        _bot = botFactory()
            ?? throw new InvalidOperationException("Actor bot factory returned null.");
        _random = new DeterministicRandom(start.ActorRandomSeed);
        _bot.StartLife(ActorSdkModelMapper.ToSdk(start));
    }

    public ActorDecision ExecuteTick(ActorObservation observation)
    {
        if (_bot is null || _random is null)
            throw new InvalidOperationException(
                "StartLife must be called before ExecuteTick.");

        var debug = new DebugCollector();
        Sdk.ActorContext context = ActorSdkModelMapper.ToSdk(observation)
            with
            {
                Random = new SdkRandom(_random),
                Debug = debug,
            };
        Sdk.ActorDecision sdkDecision = _bot.Tick(context)
            ?? throw new InvalidOperationException(
                "Actor bot returned null.");
        string? combined = CombineDebug(
            sdkDecision.DebugMessage,
            debug.TextOrNull);
        return ActorSdkModelMapper.ToEngine(
            sdkDecision with
            {
                DebugMessage = TruncateUtf8(
                    combined,
                    MaxDiagnosticBytes),
                Faulted = false,
                FaultMessage = null,
            });
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

    private sealed class DebugCollector : Sdk.IBotDebug
    {
        private StringBuilder? _text;

        public string? TextOrNull => _text?.ToString();

        public void Write(string message)
        {
            _text ??= new StringBuilder();
            if (_text.Length > 0)
                _text.Append('\n');
            _text.Append(message);
        }

        public void Write(string format, params object?[] arguments) =>
            Write(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                format,
                arguments));
    }
}
