using BotArena.Sdk;

namespace BotArena.Guest;

/// <summary>
/// Guest-side team stream — must produce bit-identical values to the engine's
/// <c>TeamTickRandom</c> (pinned by cross-runtime tests, the same discipline
/// <see cref="GuestRandom"/> follows for the per-life stream).
///
/// <para>
/// The stream is re-derived from (team root seed, tick) at the start of every
/// tick instead of being advanced from the previous one. That is what lets a
/// life born mid-match agree with its teammates immediately: agreement depends
/// only on the tick number and the draw order within it, never on how many
/// values this life has drawn before.
/// </para>
/// </summary>
internal sealed class GuestTeamRandom : IBotRandom
{
    private const ulong Step = 0x9E3779B97F4A7C15UL;

    private readonly ulong _teamRandomSeed;
    private ulong _state;
    private int? _currentTick;

    public GuestTeamRandom(ulong teamRandomSeed)
    {
        _teamRandomSeed = teamRandomSeed;
        _state = TickSeed(teamRandomSeed, tick: 0);
    }

    /// <summary>Re-derives the stream for one authoritative tick.</summary>
    public void BeginTick(int tick)
    {
        if (_currentTick == tick)
            return;
        _state = TickSeed(_teamRandomSeed, tick);
        _currentTick = tick;
    }

    public int NextInt(int minimumInclusive, int maximumExclusive)
    {
        if (maximumExclusive <= minimumInclusive)
        {
            throw new ArgumentException(
                "maximumExclusive must be greater than minimumInclusive.");
        }
        ulong range = (ulong)((long)maximumExclusive - minimumInclusive);
        return (int)((long)(NextUInt64() % range) + minimumInclusive);
    }

    public bool NextBool() => (NextUInt64() >> 63) != 0;

    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    private static ulong TickSeed(ulong teamRandomSeed, int tick)
    {
        if (tick < 0)
            throw new ArgumentOutOfRangeException(nameof(tick));
        unchecked
        {
            return Mix(teamRandomSeed + Step * ((ulong)tick + 1));
        }
    }

    private ulong NextUInt64()
    {
        unchecked
        {
            _state += Step;
            return Mix(_state);
        }
    }

    private static ulong Mix(ulong z)
    {
        unchecked
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
