using BotArena.App.Jobs;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Tests;

public sealed class BackgroundJobLeaseStoreIntegrationTests
{
    private static readonly DateTimeOffset QueueNow =
        new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task ConcurrentClaimsAndFailureTransitions_PreserveLeaseOwnership()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        long jobId;
        await using (AppDbContext seed = await database.CreateMigratedContextAsync())
        {
            BackgroundJob job = BackgroundJob.ExecuteMatch(Guid.NewGuid());
            job.AvailableAt = DateTime.UnixEpoch;
            seed.BackgroundJobs.Add(job);
            await seed.SaveChangesAsync();
            jobId = job.Id;
        }

        await using AppDbContext firstDb = database.CreateContext();
        await using AppDbContext secondDb = database.CreateContext();
        var first = new BackgroundJobLeaseStore(
            firstDb,
            new FixedTimeProvider(QueueNow));
        var second = new BackgroundJobLeaseStore(
            secondDb,
            new FixedTimeProvider(QueueNow));
        BackgroundJob?[] claims = await Task.WhenAll(
            first.ClaimAsync(
                BackgroundJob.ExecuteMatchType,
                "worker-a",
                CancellationToken.None),
            second.ClaimAsync(
                BackgroundJob.ExecuteMatchType,
                "worker-b",
                CancellationToken.None));

        BackgroundJob claimed =
            Assert.Single(claims, job => job is not null)!;
        string owner = claims[0] is not null ? "worker-a" : "worker-b";
        BackgroundJobLeaseStore ownerStore =
            claims[0] is not null ? first : second;
        BackgroundJobLeaseStore otherStore =
            claims[0] is not null ? second : first;
        Assert.Equal(jobId, claimed.Id);
        Assert.True(await ownerStore.RenewAsync(
            jobId,
            owner,
            CancellationToken.None));
        Assert.False(await otherStore.CompleteAsync(
            jobId,
            "not-the-owner",
            CancellationToken.None));

        JobFailureOutcome firstFailure = await ownerStore.FailAsync(
            claimed,
            owner,
            new InvalidOperationException("injected"),
            CancellationToken.None);
        Assert.Equal(JobFailureOutcome.RetryScheduled, firstFailure);

        await using AppDbContext retryDb = database.CreateContext();
        var retries = new BackgroundJobLeaseStore(
            retryDb,
            new FixedTimeProvider(QueueNow));
        BackgroundJob secondAttempt = Assert.IsType<BackgroundJob>(
            await retries.ClaimAsync(
                BackgroundJob.ExecuteMatchType,
                "worker-retry",
                CancellationToken.None));
        Assert.Equal(
            JobFailureOutcome.RetryScheduled,
            await retries.FailAsync(
                secondAttempt,
                "worker-retry",
                new InvalidOperationException("injected again"),
                CancellationToken.None));

        BackgroundJob thirdAttempt = Assert.IsType<BackgroundJob>(
            await retries.ClaimAsync(
                BackgroundJob.ExecuteMatchType,
                "worker-retry",
                CancellationToken.None));
        Assert.Equal(
            JobFailureOutcome.TerminalFailure,
            await retries.FailAsync(
                thirdAttempt,
                "worker-retry",
                new InvalidOperationException("injected finally"),
                CancellationToken.None));

        await using AppDbContext verify = database.CreateContext();
        BackgroundJob stored = await verify.BackgroundJobs.SingleAsync();
        Assert.Equal(JobStatus.Failed, stored.Status);
        Assert.Equal(3, stored.Attempts);
        Assert.Null(stored.LockedBy);
        Assert.Null(stored.LockedUntil);
        Assert.Equal("injected finally", stored.LastError);
    }
}
