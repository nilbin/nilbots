using System.Data;
using BotArena.App.Matches;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Jobs;

/// <summary>
/// PostgreSQL queue claim and lease transitions. Domain handlers never mutate
/// job rows, so a lease can be retried independently of match/build state.
/// </summary>
public sealed class BackgroundJobLeaseStore(
    AppDbContext db,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);
    public const int MaxAttempts = 3;

    public Task<BackgroundJob?> ClaimAsync(
        string jobType,
        string workerId,
        CancellationToken cancellationToken) =>
        ClaimAnyAsync([jobType], workerId, cancellationToken);

    public async Task<BackgroundJob?> ClaimAnyAsync(
        IReadOnlyCollection<string> jobTypes,
        string workerId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(jobTypes);
        if (jobTypes.Count == 0 ||
            jobTypes.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "At least one non-blank job type is required.",
                nameof(jobTypes));
        }
        string[] supportedJobTypes = jobTypes
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        List<BackgroundJob> jobs = await db.BackgroundJobs
            .FromSqlInterpolated($"""
                UPDATE "BackgroundJobs"
                SET "Status" = 'Running',
                    "LockedUntil" = now() + {LeaseDuration},
                    "LockedBy" = {workerId}
                WHERE "Id" = (
                    SELECT "Id" FROM "BackgroundJobs"
                    WHERE "Type" = ANY ({supportedJobTypes})
                      AND (("Status" = 'Pending' AND "AvailableAt" <= now())
                       OR ("Status" = 'Running' AND "LockedUntil" < now()))
                    ORDER BY "Id"
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED)
                RETURNING *
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return jobs.FirstOrDefault();
    }

    public async Task<bool> CompleteAsync(
        long jobId,
        string workerId,
        CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        int updated = await db.BackgroundJobs
            .Where(job => job.Id == jobId && job.LockedBy == workerId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(job => job.Status, JobStatus.Completed)
                .SetProperty(job => job.CompletedAt, now)
                .SetProperty(job => job.LockedUntil, (DateTime?)null)
                .SetProperty(job => job.LockedBy, (string?)null)
                .SetProperty(job => job.LastError, (string?)null), cancellationToken);
        return updated == 1;
    }

    public async Task<JobFailureOutcome> FailAsync(
        BackgroundJob job,
        string workerId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        int attempts = job.Attempts + 1;
        bool retry = attempts < MaxAttempts;
        DateTime availableAt =
            timeProvider.GetUtcNow().UtcDateTime.Add(RetryDelay);
        string error = exception.Message.Length <= 4000
            ? exception.Message
            : exception.Message[^4000..];
        await using var transaction =
            await db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
        if (!retry &&
            GenericActorMatchJobType.IsGenericActorMatch(job.Type) &&
            TryPayloadId(job, "matchId") is Guid matchId)
        {
            DateTime completedAt =
                timeProvider.GetUtcNow().UtcDateTime;
            await db.Matches
                .Where(match =>
                    match.Id == matchId &&
                    match.Status != MatchStatus.Completed &&
                    match.Status != MatchStatus.Failed)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            match => match.Status,
                            MatchStatus.Failed)
                        .SetProperty(
                            match => match.Error,
                            error)
                        .SetProperty(
                            match => match.CompletedAt,
                            completedAt),
                    cancellationToken);
        }

        int updated = await db.BackgroundJobs
            .Where(candidate =>
                candidate.Id == job.Id &&
                candidate.LockedBy == workerId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        candidate => candidate.Status,
                        retry
                            ? JobStatus.Pending
                            : JobStatus.Failed)
                    .SetProperty(
                        candidate => candidate.Attempts,
                        attempts)
                    .SetProperty(
                        candidate => candidate.AvailableAt,
                        availableAt)
                    .SetProperty(
                        candidate => candidate.LockedUntil,
                        (DateTime?)null)
                    .SetProperty(
                        candidate => candidate.LockedBy,
                        (string?)null)
                    .SetProperty(
                        candidate => candidate.LastError,
                        error),
                cancellationToken);
        if (updated == 0)
        {
            await transaction.RollbackAsync(
                CancellationToken.None);
            return JobFailureOutcome.LeaseLost;
        }
        await transaction.CommitAsync(cancellationToken);
        return retry
            ? JobFailureOutcome.RetryScheduled
            : JobFailureOutcome.TerminalFailure;
    }

    public async Task<bool> RenewAsync(
        long jobId,
        string workerId,
        CancellationToken cancellationToken)
    {
        DateTime lockedUntil =
            timeProvider.GetUtcNow().UtcDateTime.Add(LeaseDuration);
        int updated = await db.BackgroundJobs
            .Where(job =>
                job.Id == jobId &&
                job.Status == JobStatus.Running &&
                job.LockedBy == workerId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    job => job.LockedUntil,
                    lockedUntil),
                cancellationToken);
        return updated == 1;
    }

    private static Guid? TryPayloadId(
        BackgroundJob job,
        string property)
    {
        try
        {
            return job.PayloadId(property);
        }
        catch (Exception exception) when (
            exception is FormatException
            or InvalidOperationException
            or KeyNotFoundException
            or System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
