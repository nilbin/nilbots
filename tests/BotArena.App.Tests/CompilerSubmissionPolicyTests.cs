using BotArena.App.Bots;

namespace BotArena.App.Tests;

public class CompilerSubmissionPolicyTests
{
    private static readonly CompilerSubmissionLimits Limits = new(
        AccountTenMinuteLimit: 6,
        AccountDailyLimit: 30,
        NetworkTenMinuteLimit: 12,
        NetworkDailyLimit: 60,
        AccountQueuedLimit: 2,
        GlobalQueuedLimit: 20);

    [Fact]
    public void Evaluate_AllowsSubmissionBelowEveryLimit()
    {
        var snapshot = new CompilerSubmissionSnapshot(
            BotHasActiveBuild: false,
            AccountTenMinuteCount: 5,
            AccountDailyCount: 29,
            NetworkTenMinuteCount: 11,
            NetworkDailyCount: 59,
            AccountQueuedCount: 1,
            GlobalQueuedCount: 19);

        Assert.Null(CompilerSubmissionPolicy.Evaluate(snapshot, Limits));
    }

    [Theory]
    [InlineData(true, 0, 0, 0, 0, 0, 0, "already has")]
    [InlineData(false, 0, 0, 0, 0, 2, 0, "account already")]
    [InlineData(false, 0, 0, 0, 0, 0, 20, "queue is currently full")]
    [InlineData(false, 6, 6, 0, 0, 0, 0, "6")]
    [InlineData(false, 0, 30, 0, 0, 0, 0, "24 hours")]
    [InlineData(false, 0, 0, 12, 12, 0, 0, "network")]
    [InlineData(false, 0, 0, 0, 60, 0, 0, "network")]
    public void Evaluate_RejectsAtEachLimit(
        bool botBusy,
        int accountTenMinutes,
        int accountDaily,
        int networkTenMinutes,
        int networkDaily,
        int accountQueued,
        int globalQueued,
        string expectedMessage)
    {
        var snapshot = new CompilerSubmissionSnapshot(
            botBusy,
            accountTenMinutes,
            accountDaily,
            networkTenMinutes,
            networkDaily,
            accountQueued,
            globalQueued);

        CompilerSubmissionDenial? denial = CompilerSubmissionPolicy.Evaluate(snapshot, Limits);

        Assert.NotNull(denial);
        Assert.Contains(expectedMessage, denial.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(denial.RetryAfter > TimeSpan.Zero);
    }
}
