using System.Text;
using BotArena.Engine;
using Sdk = BotArena.Sdk;

namespace BotArena.Runtime;

/// <summary>
/// The diagnostic in-process twin of the WASM mind runtime: one SDK mind object
/// driving one participant's bodies for one whole match, with no sandbox.
///
/// <para>It exists for SDK development and for the parity tests that pin the
/// two runtimes against each other on the same scripted match. It enforces no
/// fuel, no memory ceiling and no wall clock, so it is never the competitive
/// path — but the SEQUENCE of calls it makes is identical to the sandboxed
/// one, which is what makes a divergence between them a real finding rather
/// than a difference in harness.</para>
/// </summary>
public sealed class InProcessGenericMindRuntime(
    Func<Sdk.IGenericMindBot> botFactory,
    bool trustedArcRelayStockProjection = false) : IGenericMindRuntime
{
    private const int MaxDiagnosticBytes = 4096;

    private Sdk.IGenericMindBot? _mind;
    private DeterministicRandom? _random;
    private TeamTickRandom? _teamRandom;
    private Sdk.MindWaitAction _waitAction;
    private bool _started;
    private bool _ended;

    public void StartMatch(GenericMindRuntimeStart start)
    {
        if (_started)
        {
            throw new InvalidOperationException(
                "A mind runtime can start only one match.");
        }

        ArgumentNullException.ThrowIfNull(start);
        _started = true;
        _mind = botFactory()
            ?? throw new InvalidOperationException(
                "Mind bot factory returned null.");
        _random = new DeterministicRandom(start.MindRandomSeed);
        _teamRandom = new TeamTickRandom(start.TeamRandomSeed);
        Sdk.MindStart sdkStart = GenericMindSdkModelMapper.ToSdk(start);
        _waitAction = GenericMindSdkModelMapper.WaitActionOf(
            sdkStart.Contract);
        try
        {
            _mind.StartMatch(sdkStart);
        }
        catch (Exception exception)
        {
            ReportMindException("StartMatch", exception);
            throw;
        }
    }

    public GenericMindRuntimeDecisions ExecuteTick(
        GenericMindRuntimeObservation observation)
    {
        if (_mind is null || _random is null || _teamRandom is null)
        {
            throw new InvalidOperationException(
                "StartMatch must be called before ExecuteTick.");
        }

        _teamRandom.BeginTick(observation.Tick);
        var debug = new DebugCollector();
        Sdk.MindContext context =
            (trustedArcRelayStockProjection
                ? GenericMindSdkModelMapper.ToTrustedArcRelayStockSdk(
                    observation,
                    _waitAction)
                : GenericMindSdkModelMapper.ToSdk(observation, _waitAction)) with
            {
                Random = new SdkRandom(_random),
                TeamRandom = new SdkTeamRandom(_teamRandom),
                Debug = debug,
            };
        try
        {
            _mind.Think(context);
        }
        catch (Exception exception)
        {
            ReportMindException($"Think tick {observation.Tick}", exception);
            throw;
        }

        // `with` copies the array reference, not the bodies, so the commands
        // written inside Think are on the very objects harvested here.
        return GenericMindSdkModelMapper.ToEngine(
            new Sdk.MindDecisions(
                Sdk.GenericMindContractVersions.DecisionSchemaVersion,
                observation.Tick,
                context.HarvestCommands(),
                intents: null,
                TruncateUtf8(debug.TextOrNull, MaxDiagnosticBytes)));
    }

    public void Dispose()
    {
        if (_ended || _mind is null)
            return;
        _ended = true;
        _mind.EndMatch(new Sdk.MindEnd("match-ended"));
    }

    /// <summary>
    /// The coordinator converts mind exceptions into canonical fault records
    /// that deliberately carry no message (replay bytes must stay
    /// deterministic). This runtime is the diagnostic lane, so before the
    /// exception reaches that conversion, say what actually broke — an
    /// unexplained tick-0 fault has cost real debugging time.
    /// </summary>
    private void ReportMindException(string stage, Exception exception) =>
        Console.Error.WriteLine(
            $"[in-process mind fault] {_mind?.GetType().Name ?? "mind"} "
            + $"threw during {stage}:\n{exception}");

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
        public int NextInt(int minimumInclusive, int maximumExclusive) =>
            inner.NextInt(minimumInclusive, maximumExclusive);

        public bool NextBool() => inner.NextBool();

        public double NextDouble() => inner.NextDouble();
    }

    private sealed class SdkTeamRandom(
        TeamTickRandom inner) : Sdk.IBotRandom
    {
        public int NextInt(int minimumInclusive, int maximumExclusive) =>
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
