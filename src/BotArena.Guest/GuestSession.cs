using System.Text;
using BotArena.Sdk;

namespace BotArena.Guest;

/// <summary>One bot instance living for exactly one match (plan §7), driven by tick lines.</summary>
internal sealed class GuestSession
{
    private readonly IBot _bot;
    private readonly GuestRandom _random;
    private readonly int _slot;

    private GuestSession(IBot bot, ulong seed, int slot)
    {
        _bot = bot;
        _random = new GuestRandom(seed);
        _slot = slot;
    }

    public static GuestSession Start(string initLine, Func<string, IBot> botFactory)
    {
        var init = GuestProtocol.ParseInit(initLine);
        return new GuestSession(botFactory(init.BotName), init.Seed, init.Slot);
    }

    public string HandleTick(string line)
    {
        var observation = GuestProtocol.ParseObservation(line);
        var debug = new GuestDebug();
        var context = new BotContext
        {
            Tick = observation.Tick,
            Slot = _slot,
            Position = observation.Position,
            Facing = observation.Facing,
            Health = observation.Health,
            Cooldown = observation.Cooldown,
            Energy = observation.Energy,
            MapWidth = observation.MapWidth,
            MapHeight = observation.MapHeight,
            ZoneTiles = observation.ZoneTiles,
            MyZoneTicks = observation.MyZoneTicks,
            EnemyZoneTicks = observation.EnemyZoneTicks,
            ControlPressure = observation.ControlPressure,
            ControlPressureLimit = observation.ControlPressureLimit,
            ShotPrograms = observation.ShotPrograms,
            VisibleProjectiles = observation.VisibleProjectiles,
            HeardSounds = observation.HeardSounds,
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
