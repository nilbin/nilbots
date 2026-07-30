namespace BotArena.Engine;

/// <summary>
/// The team-scoped deterministic stream, re-derived for every tick.
///
/// <para>
/// Teams have no communication channel. Coordination works only because every
/// life on a team receives the identical frozen team observation, so any pure
/// function of it is a shared plan. A per-life random stream breaks that: it
/// differs between teammates, so a plan that consumes one silently diverges.
/// This type restores the property for randomized plans — every life on the
/// team draws the SAME values at the same decision point in the same tick.
/// </para>
///
/// <para>
/// A single consumable stream cannot do that job: a life born mid-match has
/// not consumed the earlier draws, and two lives that draw at different rates
/// desync permanently. So the stream is stateless ACROSS ticks — at the start
/// of each tick it is reseeded from (team root seed, tick), which depends on
/// nothing a life did before. Agreement therefore needs only that teammates
/// run the same per-tick draw sequence, which is exactly the common-knowledge
/// property a shared observation already provides.
/// </para>
///
/// <para>
/// The guest ships its own copy of this arithmetic
/// (<c>BotArena.Guest.GuestTeamRandom</c>); the two are pinned bit-identical
/// by cross-runtime tests, the same discipline
/// <see cref="DeterministicRandom"/> and its guest twin already follow.
/// </para>
/// </summary>
public sealed class TeamTickRandom
{
    private readonly ulong _teamRandomSeed;
    private DeterministicRandom _tick;
    private int? _currentTick;

    /// <summary>Creates the team stream for one life.</summary>
    /// <param name="teamRandomSeed">
    /// The team root seed delivered at life start. Every life on the team
    /// receives the identical value.
    /// </param>
    public TeamTickRandom(ulong teamRandomSeed)
    {
        _teamRandomSeed = teamRandomSeed;
        _tick = new DeterministicRandom(
            SeedDerivation.DeriveTeamTickSeed(teamRandomSeed, tick: 0));
    }

    /// <summary>The team root seed this stream re-derives from.</summary>
    public ulong TeamRandomSeed => _teamRandomSeed;

    /// <summary>
    /// Re-derives the stream for <paramref name="tick"/>. Called once per tick
    /// before the bot runs, so the bot's first draw of a tick is the team's
    /// first draw of that tick regardless of what this life drew earlier.
    /// </summary>
    /// <param name="tick">Non-negative authoritative tick about to execute.</param>
    public void BeginTick(int tick)
    {
        if (_currentTick == tick)
            return;
        _tick = new DeterministicRandom(
            SeedDerivation.DeriveTeamTickSeed(_teamRandomSeed, tick));
        _currentTick = tick;
    }

    /// <summary>Draws the next team value in [min, max).</summary>
    /// <param name="minimumInclusive">Inclusive lower bound.</param>
    /// <param name="maximumExclusive">Exclusive upper bound.</param>
    /// <returns>The team's next integer for this tick.</returns>
    public int NextInt(int minimumInclusive, int maximumExclusive) =>
        _tick.NextInt(minimumInclusive, maximumExclusive);

    /// <summary>Draws the next team boolean.</summary>
    /// <returns>The team's next boolean for this tick.</returns>
    public bool NextBool() => _tick.NextBool();

    /// <summary>Draws the next team double in [0, 1).</summary>
    /// <returns>The team's next double for this tick.</returns>
    public double NextDouble() => _tick.NextDouble();
}
