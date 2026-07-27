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
}
