using BotArena.App.Bots;
using BotArena.App.Store;

namespace BotArena.App.Tests;

/// <summary>
/// What buying capacity actually buys.
/// <para>
/// Selling something and then not delivering it is the worst failure a store has, and it is
/// invisible from the catalog: the pack validates, the entitlement grants, and the limit
/// stays where it was. These pin the last step.
/// </para>
/// </summary>
public class AccountCapacityTests
{
    private static readonly CompilerSubmissionLimits Base =
        new(AccountTenMinuteLimit: 6,
            AccountDailyLimit: 30,
            NetworkTenMinuteLimit: 12,
            NetworkDailyLimit: 60,
            AccountQueuedLimit: 2,
            GlobalQueuedLimit: 20);

    [Fact]
    public void AnAccountWithNothingKeepsTheStandardLimits()
    {
        Assert.Equal(Base, Base.ForAccount([]));
    }

    [Fact]
    public void ExtraDailyBuildsRaisesTheDailyCapAndNothingElse()
    {
        CompilerSubmissionLimits raised =
            Base.ForAccount([AccountCapacity.ExtraDailyBuildsKey]);

        Assert.Equal(60, raised.AccountDailyLimit);
        // Everything else is deliberately untouched. The ten-minute and queued caps are
        // burst protection and would sell tempo during a tuning session; the network limits
        // belong to a connection, so raising them would lift the ceiling for everyone
        // behind a shared IP.
        Assert.Equal(Base.AccountTenMinuteLimit, raised.AccountTenMinuteLimit);
        Assert.Equal(Base.AccountQueuedLimit, raised.AccountQueuedLimit);
        Assert.Equal(Base.NetworkDailyLimit, raised.NetworkDailyLimit);
        Assert.Equal(Base.GlobalQueuedLimit, raised.GlobalQueuedLimit);
    }

    [Fact]
    public void GrantsStack()
    {
        CompilerSubmissionLimits raised = Base.ForAccount(
            [AccountCapacity.ExtraDailyBuildsKey, AccountCapacity.ExtraDailyBuildsKey]);

        // Capacity is the one thing worth buying twice, which is why the store marks it
        // repeatable while an appearance pack is owned once and then owned forever.
        Assert.Equal(90, raised.AccountDailyLimit);
    }

    [Fact]
    public void PurchasedCapacityIsCapped()
    {
        string[] many = Enumerable.Repeat(AccountCapacity.ExtraDailyBuildsKey, 50).ToArray();

        CompilerSubmissionLimits raised = Base.ForAccount(many);

        // The compiler is a real machine. Without a ceiling, enough purchases would commit
        // the build farm to work it cannot do, and the person who paid would be the one who
        // found out.
        Assert.Equal(Base.AccountDailyLimit + AccountCapacity.MaxPurchasedDailyBuilds,
            raised.AccountDailyLimit);
    }

    [Fact]
    public void OtherEntitlementsDoNotChangeLimits()
    {
        CompilerSubmissionLimits raised =
            Base.ForAccount(["bot-look:helio-kite", "projectile-look:helix-dart"]);

        // A chassis is not a capacity grant. Sharing one entitlement table between the two
        // is what makes this worth asserting rather than obvious.
        Assert.Equal(Base.AccountDailyLimit, raised.AccountDailyLimit);
    }

    [Fact]
    public void TheCapIsWhatActuallyAdmitsTheBuild()
    {
        var atOldCap = new CompilerSubmissionSnapshot(
            BotHasActiveBuild: false,
            AccountTenMinuteCount: 0,
            AccountDailyCount: 30,
            NetworkTenMinuteCount: 0,
            NetworkDailyCount: 0,
            AccountQueuedCount: 0,
            GlobalQueuedCount: 0);

        // End to end through the policy, because the limits object being right and the
        // build still being refused would be a store that takes money for nothing.
        Assert.NotNull(CompilerSubmissionPolicy.Evaluate(atOldCap, Base));
        Assert.Null(CompilerSubmissionPolicy.Evaluate(
            atOldCap, Base.ForAccount([AccountCapacity.ExtraDailyBuildsKey])));
    }
}
