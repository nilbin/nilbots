using System.Text;
using BotArena.Bots.BuiltIn;
using BotArena.Sdk;

namespace BotArena.WasmGuest;

/// <summary>One bot instance living for exactly one match (plan §7), driven by tick lines.</summary>
internal sealed class GuestSession
{
    private readonly IBot _bot;
    private readonly GuestRandom _random;

    private GuestSession(IBot bot, ulong seed)
    {
        _bot = bot;
        _random = new GuestRandom(seed);
    }

    public static GuestSession Start(string initLine)
    {
        var init = GuestProtocol.ParseInit(initLine);
        IBot bot = init.BotName switch
        {
            // Deliberately hostile test bots, available only in the guest artifact:
            "guest-faulty" => new FaultyBot(),
            "guest-hog" => new HogBot(),
            _ => BuiltInBotCatalog.Create(init.BotName),
        };
        return new GuestSession(bot, init.Seed);
    }

    public string HandleTick(string line)
    {
        var observation = GuestProtocol.ParseObservation(line);
        var debug = new GuestDebug();
        var context = new BotContext
        {
            Tick = observation.Tick,
            Position = observation.Position,
            Facing = observation.Facing,
            Health = observation.Health,
            Cooldown = observation.Cooldown,
            PreviousActionResult = observation.PreviousActionResult,
            VisibleTiles = observation.VisibleTiles,
            VisibleEnemies = observation.VisibleEnemies,
            VisibleEvents = observation.VisibleEvents,
            Random = _random,
            Debug = debug,
        };
        var action = _bot.Tick(context);
        return GuestProtocol.FormatDecision(action, debug.TextOrNull);
    }

    /// <summary>Throws on every tick — exercises the fault path end to end.</summary>
    private sealed class FaultyBot : IBot
    {
        public BotAction Tick(BotContext context) =>
            throw new InvalidOperationException("FaultyBot always fails");
    }

    /// <summary>Burns fuel until the limit trips. The instance-field LCG gives every
    /// iteration an observable side effect so the optimizer cannot elide the loop.</summary>
    private sealed class HogBot : IBot
    {
        private ulong _state = 1;

        public BotAction Tick(BotContext context)
        {
            while (_state != 0)
                _state = _state * 6364136223846793005UL + 1442695040888963407UL;
            return Actions.Wait();
        }
    }

    private sealed class GuestDebug : IBotDebug
    {
        private StringBuilder? _text;

        public string? TextOrNull => _text?.ToString();

        public void Write(string message)
        {
            _text ??= new StringBuilder();
            if (_text.Length > 0)
                _text.Append('\n');
            if (_text.Length < 4096)
                _text.Append(message);
        }

        public void Write(string format, params object?[] arguments) =>
            Write(string.Format(System.Globalization.CultureInfo.InvariantCulture, format, arguments));
    }
}

/// <summary>
/// Guest-side SplitMix64 — must produce bit-identical streams to the engine's
/// DeterministicRandom (pinned by cross-runtime golden tests in BotArena.Runtime.Wasm.Tests).
/// </summary>
internal sealed class GuestRandom : IBotRandom
{
    private ulong _state;

    public GuestRandom(ulong seed) => _state = seed;

    private ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        ulong z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public int NextInt(int minimumInclusive, int maximumExclusive)
    {
        if (maximumExclusive <= minimumInclusive)
            throw new ArgumentException("maximumExclusive must be greater than minimumInclusive.");
        ulong range = (ulong)((long)maximumExclusive - minimumInclusive);
        return (int)((long)(NextUInt64() % range) + minimumInclusive);
    }

    public bool NextBool() => (NextUInt64() >> 63) != 0;

    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));
}
