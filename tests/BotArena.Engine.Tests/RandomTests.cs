namespace BotArena.Engine.Tests;

public class RandomTests
{
    [Fact]
    public void SameSeed_ProducesIdenticalSequence()
    {
        var a = new DeterministicRandom(12345);
        var b = new DeterministicRandom(12345);
        for (int i = 0; i < 1000; i++)
            Assert.Equal(a.NextUInt64(), b.NextUInt64());
    }

    [Fact]
    public void NextInt_StaysInsideBounds()
    {
        var random = new DeterministicRandom(7);
        for (int i = 0; i < 1000; i++)
        {
            int value = random.NextInt(-3, 9);
            Assert.InRange(value, -3, 8);
        }
    }

    [Fact]
    public void NextInt_RejectsEmptyRange()
    {
        var random = new DeterministicRandom(7);
        Assert.Throws<ArgumentException>(() => random.NextInt(5, 5));
    }

    [Fact]
    public void NextDouble_StaysInUnitInterval()
    {
        var random = new DeterministicRandom(11);
        for (int i = 0; i < 1000; i++)
        {
            double value = random.NextDouble();
            Assert.True(value is >= 0.0 and < 1.0);
        }
    }

    /// <summary>
    /// Golden values pin the PRNG algorithm across platforms and .NET versions. If one of
    /// these fails, the random stream changed — that is a game-rules version bump, not a
    /// test to update casually (plan §6).
    /// </summary>
    [Fact]
    public void GoldenValues_PinTheAlgorithm()
    {
        var r = new DeterministicRandom(1);
        Assert.Equal(10451216379200822465UL, r.NextUInt64());
        Assert.Equal(13757245211066428519UL, r.NextUInt64());
        Assert.Equal(17911839290282890590UL, r.NextUInt64());
        Assert.Equal(8196980753821780235UL, r.NextUInt64());

        var r2 = new DeterministicRandom(42);
        Assert.Equal(new[] { 3, 1, 8, 4, 0, 2, 5, 8 },
            Enumerable.Range(0, 8).Select(_ => r2.NextInt(0, 10)).ToArray());

        Assert.Equal(2073946379187239140UL, SeedDerivation.DeriveBotSeed(42, 0, "0.1"));
        Assert.Equal(16906781275567899400UL, SeedDerivation.DeriveBotSeed(42, 1, "0.1"));
    }

    [Fact]
    public void SeedDerivation_DependsOnSlot()
    {
        Assert.NotEqual(
            SeedDerivation.DeriveBotSeed(42, 0, "0.1"),
            SeedDerivation.DeriveBotSeed(42, 1, "0.1"));
    }

    [Fact]
    public void SeedDerivation_DependsOnRulesVersion()
    {
        Assert.NotEqual(
            SeedDerivation.DeriveBotSeed(42, 0, "0.1"),
            SeedDerivation.DeriveBotSeed(42, 0, "0.2"));
    }

    [Fact]
    public void SeedDerivation_DependsOnMatchSeed()
    {
        Assert.NotEqual(
            SeedDerivation.DeriveBotSeed(42, 0, "0.1"),
            SeedDerivation.DeriveBotSeed(43, 0, "0.1"));
    }

    [Fact]
    public void ActorSeedDerivation_IsDomainSeparatedAndUsesEveryIdentityCoordinate()
    {
        var baseline = new ActorIdentity(0, 0, 0);
        ulong seed = SeedDerivation.DeriveActorSeed(42, baseline, "frontline");

        Assert.Equal(17818462576779120251UL, seed);
        Assert.NotEqual(
            SeedDerivation.DeriveBotSeed(42, 0, "frontline"),
            seed);
        Assert.NotEqual(
            seed,
            SeedDerivation.DeriveActorSeed(
                42,
                new ActorIdentity(1, 0, 0),
                "frontline"));
        Assert.NotEqual(
            seed,
            SeedDerivation.DeriveActorSeed(
                42,
                new ActorIdentity(0, 1, 0),
                "frontline"));
        Assert.NotEqual(
            seed,
            SeedDerivation.DeriveActorSeed(
                42,
                new ActorIdentity(0, 0, 1),
                "frontline"));
        Assert.NotEqual(
            seed,
            SeedDerivation.DeriveActorSeed(42, baseline, "next-season"));
        Assert.NotEqual(
            seed,
            SeedDerivation.DeriveActorSeed(43, baseline, "frontline"));
    }

    [Fact]
    public void TeamSeedDerivation_IsDomainSeparatedAndPerTeam()
    {
        ulong seed = SeedDerivation.DeriveTeamSeed(42, teamId: 0, "frontline");

        Assert.Equal(14_665_529_337_849_397_758UL, seed);
        // The team domain must not collide with the per-life, per-slot, or
        // spawn domains that share the same match seed and profile string.
        Assert.NotEqual(
            SeedDerivation.DeriveActorSeed(
                42,
                new ActorIdentity(0, 0, 0),
                "frontline"),
            seed);
        Assert.NotEqual(
            SeedDerivation.DeriveBotSeed(42, 0, "frontline"),
            seed);
        Assert.NotEqual(SeedDerivation.DeriveSpawnSeed(42, "frontline"), seed);
        // Neither team can derive the other's stream from its own.
        Assert.NotEqual(
            seed,
            SeedDerivation.DeriveTeamSeed(42, teamId: 1, "frontline"));
        Assert.NotEqual(
            seed,
            SeedDerivation.DeriveTeamSeed(42, teamId: 0, "next-season"));
        Assert.NotEqual(
            seed,
            SeedDerivation.DeriveTeamSeed(43, teamId: 0, "frontline"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SeedDerivation.DeriveTeamSeed(42, -1, "frontline"));
    }

    [Fact]
    public void TeamTickSeedDerivation_IsPerTickAndPathIndependent()
    {
        ulong team = SeedDerivation.DeriveTeamSeed(42, teamId: 0, "frontline");

        Assert.Equal(
            3_550_680_682_936_803_696UL,
            SeedDerivation.DeriveTeamTickSeed(team, tick: 0));
        // The property that makes a mid-match birth safe: the state for a tick
        // is a pure function of (team seed, tick) — nothing accumulates.
        Assert.Equal(
            SeedDerivation.DeriveTeamTickSeed(team, tick: 7),
            SeedDerivation.DeriveTeamTickSeed(team, tick: 7));
        Assert.Equal(
            250,
            Enumerable.Range(0, 250)
                .Select(tick =>
                    SeedDerivation.DeriveTeamTickSeed(team, tick))
                .Distinct()
                .Count());
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SeedDerivation.DeriveTeamTickSeed(team, -1));
    }

    [Fact]
    public void TeamTickRandom_ReDerivesEachTickRegardlessOfPriorDraws()
    {
        ulong team = SeedDerivation.DeriveTeamSeed(9, teamId: 1, "frontline");

        var veteran = new TeamTickRandom(team);
        for (int tick = 0; tick < 5; tick++)
        {
            veteran.BeginTick(tick);
            // A life that consumes at a wildly different rate every tick.
            for (int index = 0; index <= tick * 3; index++)
                veteran.NextInt(0, 1_000);
        }
        veteran.BeginTick(5);
        int[] veteranDraws =
        [
            .. Enumerable.Range(0, 4).Select(_ => veteran.NextInt(0, 1_000)),
        ];

        var newborn = new TeamTickRandom(team);
        newborn.BeginTick(5);
        int[] newbornDraws =
        [
            .. Enumerable.Range(0, 4).Select(_ => newborn.NextInt(0, 1_000)),
        ];

        Assert.Equal(veteranDraws, newbornDraws);
        Assert.Equal(team, veteran.TeamRandomSeed);

        // Re-entering the same tick is idempotent; a new tick moves the stream.
        veteran.BeginTick(5);
        newborn.BeginTick(6);
        Assert.NotEqual(
            veteranDraws,
            [.. Enumerable.Range(0, 4).Select(_ => newborn.NextInt(0, 1_000))]);
    }
}
