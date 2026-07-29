using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.App.Store;

namespace BotArena.App.Tests;

/// <summary>
/// How many ranked sets an account may start.
/// <para>
/// A set is six WASM matches, which makes this the most expensive thing a single request
/// can ask for — more than a build, which already had a durable limit. Until this existed
/// the only ceiling was the in-memory HTTP limiter shared with unranked challenges: 20 a
/// minute, per web process, forgotten on restart.
/// </para>
/// </summary>
public class RankedSetPolicyTests
{
    private static readonly RankedSetLimits Base =
        new(AccountDailyLimit: 10, AccountConcurrentLimit: 2);

    [Fact]
    public void AQuietAccountMayStartASet()
    {
        Assert.Null(RankedSetPolicy.Evaluate(new RankedSetSnapshot(0, 0), Base));
    }

    [Fact]
    public void ConcurrencyIsRefusedBeforeTheDailyCap()
    {
        ApplicationError? refusal =
            RankedSetPolicy.EvaluateError(new RankedSetSnapshot(10, 2), Base);

        Assert.NotNull(refusal);
        Assert.Equal(
            ApplicationErrorCodes.MatchRankedConcurrentLimit,
            refusal.Code);
        Assert.Contains("in progress", refusal.Detail);
    }

    [Fact]
    public void TheDailyCapRefusesWithItsOwnReason()
    {
        ApplicationError? refusal =
            RankedSetPolicy.EvaluateError(new RankedSetSnapshot(10, 0), Base);

        Assert.NotNull(refusal);
        Assert.Equal(
            ApplicationErrorCodes.MatchRankedDailyLimit,
            refusal.Code);
        Assert.Contains("24 hours", refusal.Detail);
    }

    [Fact]
    public void BoughtCapacityRaisesTheDailyCapOnly()
    {
        RankedSetLimits raised = Base.ForAccount([AccountCapacity.ExtraDailyRankedSetsKey]);

        Assert.Equal(15, raised.AccountDailyLimit);
        // Concurrency is a claim on the shared match worker. Selling it would let a payer
        // push everyone else's sets down the queue rather than merely play more of their
        // own, which is a different thing from removing a wait.
        Assert.Equal(Base.AccountConcurrentLimit, raised.AccountConcurrentLimit);
    }

    [Fact]
    public void BoughtRankedCapacityIsCapped()
    {
        string[] many = Enumerable.Repeat(AccountCapacity.ExtraDailyRankedSetsKey, 40).ToArray();

        Assert.Equal(
            Base.AccountDailyLimit + AccountCapacity.MaxPurchasedDailyRankedSets,
            Base.ForAccount(many).AccountDailyLimit);
    }

    [Fact]
    public void BuildCapacityDoesNotBuyRankedSets()
    {
        // Two capacity products share one entitlement table, so this is worth asserting
        // rather than assuming: buying builds must not silently buy ladder time.
        Assert.Equal(
            Base.AccountDailyLimit,
            Base.ForAccount([AccountCapacity.ExtraDailyBuildsKey]).AccountDailyLimit);
    }

    [Fact]
    public void ConfiguredLimitsAreClamped()
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationManager();
        configuration["BOTARENA_RANKED_ACCOUNT_DAILY"] = "0";
        configuration["BOTARENA_RANKED_ACCOUNT_CONCURRENT"] = "99999";

        RankedSetLimits limits = RankedSetLimits.FromConfiguration(configuration);

        // A misconfigured zero would close the ladder to everyone, and an unbounded
        // concurrency would let one account fill the match queue — neither should be
        // reachable by typing a number into an environment variable.
        Assert.Equal(1, limits.AccountDailyLimit);
        Assert.Equal(50, limits.AccountConcurrentLimit);
    }
}
