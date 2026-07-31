using System.Text;

namespace BotArena.Engine;

/// <summary>
/// The nilbots-owned PRNG (plan §6): SplitMix64. Pure 64-bit integer arithmetic, so the
/// stream is identical on every OS, architecture and .NET runtime. The algorithm is pinned
/// by the game-rules version; changing it requires a new rules version.
/// </summary>
public sealed class DeterministicRandom
{
    private ulong _state;

    public DeterministicRandom(ulong seed) => _state = seed;

    public ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        return Mix(_state);
    }

    public static ulong Mix(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Uniform-enough integer in [minInclusive, maxExclusive). Uses modulo reduction;
    /// the bias is negligible for game-sized ranges and the mapping is pinned by the rules version.</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            throw new ArgumentException("maxExclusive must be greater than minInclusive.");
        ulong range = (ulong)((long)maxExclusive - minInclusive);
        return (int)((long)(NextUInt64() % range) + minInclusive);
    }

    public bool NextBool() => (NextUInt64() >> 63) != 0;

    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));
}

/// <summary>
/// Derives each participant's independent random stream from
/// match seed + participant slot + game-rules version (plan §6).
/// </summary>
public static class SeedDerivation
{
    public static ulong DeriveBotSeed(ulong matchSeed, int participantSlot, string gameRulesVersion)
    {
        ulong h = Fnv1a64(gameRulesVersion);
        ulong x = DeterministicRandom.Mix(matchSeed ^ h);
        x = DeterministicRandom.Mix(x + 0x9E3779B97F4A7C15UL * ((ulong)participantSlot + 1));
        return x;
    }

    /// <summary>
    /// Independent stream for one entity life. The actor domain is deliberately
    /// distinct from historical slot streams, and every team/unit/life
    /// coordinate participates so replicated instances never share randomness.
    /// The exact formula is the behavior named by
    /// <see cref="ActorSeedMechanicsDefinition.SeedDerivationKind.MatchSeedProfileTeamUnitLifeMix64V1"/>.
    /// </summary>
    public static ulong DeriveActorSeed(
        ulong matchSeed,
        ActorIdentity actorId,
        string seedProfile)
    {
        ArgumentNullException.ThrowIfNull(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(seedProfile);

        unchecked
        {
            const ulong step = 0x9E3779B97F4A7C15UL;
            ulong x = DeterministicRandom.Mix(
                matchSeed ^ Fnv1a64("actors:" + seedProfile));
            x = DeterministicRandom.Mix(
                x + step * ((ulong)actorId.TeamId + 1));
            x = DeterministicRandom.Mix(
                x + step * ((ulong)actorId.UnitId + 1));
            x = DeterministicRandom.Mix(
                x + step * ((ulong)actorId.LifeId + 1));
            return x;
        }
    }

    /// <summary>
    /// Independent stream for one SCORING TEAM. Every life on the team is
    /// handed this exact value at life start, so a pure function of it is
    /// common knowledge inside the team without any communication channel.
    /// The "teams:" domain label keeps it from colliding with the per-life
    /// "actors:" domain or the "spawns:" domain, and each team's value passes
    /// through the SplitMix64 finalizer, so one team's seed reveals nothing
    /// about another's.
    /// </summary>
    /// <param name="matchSeed">The match's authoritative seed.</param>
    /// <param name="teamId">Non-negative scoring-team identifier.</param>
    /// <param name="seedProfile">
    /// The ruleset's fingerprinted seed-profile comparison namespace.
    /// </param>
    /// <returns>The team's deterministic root seed.</returns>
    public static ulong DeriveTeamSeed(
        ulong matchSeed,
        int teamId,
        string seedProfile)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(teamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(seedProfile);

        unchecked
        {
            const ulong step = 0x9E3779B97F4A7C15UL;
            ulong x = DeterministicRandom.Mix(
                matchSeed ^ Fnv1a64("teams:" + seedProfile));
            return DeterministicRandom.Mix(x + step * ((ulong)teamId + 1));
        }
    }

    /// <summary>
    /// The team stream's state for ONE tick. Re-derived from the team root
    /// seed and the observed tick rather than advanced from the previous
    /// tick, which is what lets a life born mid-match agree with teammates
    /// on its very first tick: agreement depends only on the tick number,
    /// never on how many values a life has drawn before.
    /// </summary>
    /// <param name="teamRandomSeed">The team's root seed.</param>
    /// <param name="tick">Non-negative authoritative tick.</param>
    /// <returns>The SplitMix64 state this team uses on that tick.</returns>
    public static ulong DeriveTeamTickSeed(ulong teamRandomSeed, int tick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);

        unchecked
        {
            const ulong step = 0x9E3779B97F4A7C15UL;
            return DeterministicRandom.Mix(
                teamRandomSeed + step * ((ulong)tick + 1));
        }
    }

    /// <summary>
    /// Independent stream for one SUBMITTED PARTICIPANT under the mind profile
    /// (DECISIONS #191). The "minds:" domain label keeps it clear of the
    /// per-life "actors:", the "teams:" and the "spawns:" domains, so a mind's
    /// private stream can never collide with the per-life stream of a body it
    /// happens to command — which matters because the two profiles are meant
    /// to be compared, not merged.
    /// <para>Derived in the PARTICIPANT domain rather than the life domain is
    /// the whole point: one stream advancing across the whole match instead of
    /// a fresh one per life.</para>
    /// </summary>
    /// <param name="matchSeed">The match's authoritative seed.</param>
    /// <param name="participantId">Non-negative submitted participant.</param>
    /// <param name="seedProfile">
    /// The ruleset's fingerprinted seed-profile comparison namespace.
    /// </param>
    public static ulong DeriveMindSeed(
        ulong matchSeed,
        int participantId,
        string seedProfile)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(participantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(seedProfile);

        unchecked
        {
            const ulong step = 0x9E3779B97F4A7C15UL;
            ulong x = DeterministicRandom.Mix(
                matchSeed ^ Fnv1a64("minds:" + seedProfile));
            return DeterministicRandom.Mix(
                x + step * ((ulong)participantId + 1));
        }
    }

    /// <summary>Independent stream for seed-spawn variation — labeled so it can never
    /// collide with a bot's own stream (same shape as DeriveBotSeed, distinct domain).</summary>
    public static ulong DeriveSpawnSeed(ulong matchSeed, string gameRulesVersion) =>
        DeterministicRandom.Mix(matchSeed ^ Fnv1a64("spawns:" + gameRulesVersion));

    private static ulong Fnv1a64(string value)
    {
        ulong hash = 14695981039346656037UL;
        foreach (byte b in Encoding.UTF8.GetBytes(value))
        {
            hash ^= b;
            hash *= 1099511628211UL;
        }
        return hash;
    }
}
