using BotArena.App.Jobs;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;
using Matches = BotArena.App.Matches;

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

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task GenericActorWorkIsInvisibleToLegacyMatchLane()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        Guid matchId = Guid.NewGuid();
        await using (AppDbContext seed =
                     await database.CreateMigratedContextAsync())
        {
            BackgroundJob job =
                BackgroundJob.ExecuteGenericActorMatch(
                    matchId,
                    "frontline-labs",
                    1);
            job.AvailableAt = DateTime.UnixEpoch;
            seed.BackgroundJobs.Add(job);
            await seed.SaveChangesAsync();
        }

        await using AppDbContext db = database.CreateContext();
        var leases = new BackgroundJobLeaseStore(
            db,
            new FixedTimeProvider(QueueNow));
        Assert.Null(await leases.ClaimAsync(
            BackgroundJob.ExecuteMatchType,
            "legacy-worker",
            CancellationToken.None));

        BackgroundJob claimed = Assert.IsType<BackgroundJob>(
            await leases.ClaimAsync(
                GenericActorMatchJobType.ForPlaylist(
                    "frontline-labs",
                    1),
                "generic-worker",
                CancellationToken.None));
        Assert.Equal(matchId, claimed.PayloadId("matchId"));
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task GenericClaimsOnlyConsumeDefinitionsAdvertisedByWorker()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        string currentType = GenericActorMatchJobType.ForPlaylist(
            "frontline-labs",
            1);
        string futureType = GenericActorMatchJobType.ForPlaylist(
            "deathmatch-labs",
            1);
        await using (AppDbContext seed =
                     await database.CreateMigratedContextAsync())
        {
            BackgroundJob future =
                BackgroundJob.ExecuteGenericActorMatch(
                    Guid.NewGuid(),
                    "deathmatch-labs",
                    1);
            future.AvailableAt = DateTime.UnixEpoch;
            BackgroundJob current =
                BackgroundJob.ExecuteGenericActorMatch(
                    Guid.NewGuid(),
                    "frontline-labs",
                    1);
            current.AvailableAt = DateTime.UnixEpoch;
            seed.BackgroundJobs.AddRange(future, current);
            await seed.SaveChangesAsync();
        }

        await using (AppDbContext oldWorkerDb =
                     database.CreateContext())
        {
            var oldWorker = new BackgroundJobLeaseStore(
                oldWorkerDb,
                new FixedTimeProvider(QueueNow));
            BackgroundJob current = Assert.IsType<BackgroundJob>(
                await oldWorker.ClaimAnyAsync(
                    [currentType],
                    "old-generic-worker",
                    CancellationToken.None));
            Assert.Equal(currentType, current.Type);
        }

        await using (AppDbContext verifyOldWorker =
                     database.CreateContext())
        {
            BackgroundJob future =
                await verifyOldWorker.BackgroundJobs.SingleAsync(
                    job => job.Type == futureType);
            Assert.Equal(JobStatus.Pending, future.Status);
            Assert.Equal(0, future.Attempts);
        }

        await using (AppDbContext newWorkerDb =
                     database.CreateContext())
        {
            var newWorker = new BackgroundJobLeaseStore(
                newWorkerDb,
                new FixedTimeProvider(QueueNow));
            BackgroundJob future = Assert.IsType<BackgroundJob>(
                await newWorker.ClaimAnyAsync(
                    [currentType, futureType],
                    "new-generic-worker",
                    CancellationToken.None));
            Assert.Equal(futureType, future.Type);
        }
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task TerminalGenericFailureAtomicallyFailsJobAndMatch()
    {
        await using var database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        Guid matchId;
        await using (AppDbContext seed =
                     await database.CreateMigratedContextAsync())
        {
            var match = new Matches.Match
            {
                MapId = "generic-failure-test",
                Seed = 1,
                Status = Matches.MatchStatus.Running,
            };
            BackgroundJob job =
                BackgroundJob.ExecuteGenericActorMatch(
                    match.Id,
                    "frontline-labs",
                    1);
            job.AvailableAt = DateTime.UnixEpoch;
            seed.AddRange(match, job);
            await seed.SaveChangesAsync();
            matchId = match.Id;
        }

        for (int attempt = 1;
             attempt <= BackgroundJobLeaseStore.MaxAttempts;
             attempt++)
        {
            await using AppDbContext db = database.CreateContext();
            var leases = new BackgroundJobLeaseStore(
                db,
                new FixedTimeProvider(QueueNow));
            BackgroundJob claimed = Assert.IsType<BackgroundJob>(
                await leases.ClaimAsync(
                    GenericActorMatchJobType.ForPlaylist(
                        "frontline-labs",
                        1),
                    "generic-failure-worker",
                    CancellationToken.None));
            JobFailureOutcome outcome = await leases.FailAsync(
                claimed,
                "generic-failure-worker",
                new InvalidOperationException(
                    $"generic failure {attempt}"),
                CancellationToken.None);
            Assert.Equal(
                attempt < BackgroundJobLeaseStore.MaxAttempts
                    ? JobFailureOutcome.RetryScheduled
                    : JobFailureOutcome.TerminalFailure,
                outcome);

            await using AppDbContext verifyAttempt =
                database.CreateContext();
            Matches.Match storedMatch =
                await verifyAttempt.Matches.SingleAsync(
                    match => match.Id == matchId);
            if (attempt <
                BackgroundJobLeaseStore.MaxAttempts)
            {
                Assert.Equal(
                    Matches.MatchStatus.Running,
                    storedMatch.Status);
                Assert.Null(storedMatch.Error);
                Assert.Null(storedMatch.CompletedAt);
            }
            else
            {
                Assert.Equal(
                    Matches.MatchStatus.Failed,
                    storedMatch.Status);
                Assert.Equal(
                    $"generic failure {attempt}",
                    storedMatch.Error);
                Assert.NotNull(storedMatch.CompletedAt);
            }
        }

        await using AppDbContext verify = database.CreateContext();
        BackgroundJob storedJob =
            await verify.BackgroundJobs.SingleAsync();
        Assert.Equal(JobStatus.Failed, storedJob.Status);
        Assert.Equal(
            BackgroundJobLeaseStore.MaxAttempts,
            storedJob.Attempts);
        Assert.Equal(
            $"generic failure {BackgroundJobLeaseStore.MaxAttempts}",
            storedJob.LastError);
    }
}
