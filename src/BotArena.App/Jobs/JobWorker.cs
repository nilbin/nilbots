using System.Diagnostics;
using BotArena.App.Matches;
using BotArena.App.Shared;

namespace BotArena.App.Jobs;

/// <summary>
/// Runs typed durable-job lanes. Queue ownership and lease transitions live in
/// <see cref="BackgroundJobLeaseStore"/>; domain work is delegated through
/// <see cref="BackgroundJobDispatcher"/>.
/// </summary>
public sealed class JobWorker(
    IServiceScopeFactory scopeFactory,
    ApplicationMode mode,
    MatchExecutionSettings matchSettings,
    ILogger<JobWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan LeaseRefreshInterval = TimeSpan.FromMinutes(1);
    private static readonly int CompileWorkers =
        ReadEnv("BOTARENA_COMPILE_WORKERS", fallback: 1, min: 1, max: 8);
    private static readonly int MatchWorkers =
        ReadEnv("BOTARENA_MATCH_WORKERS", fallback: 1, min: 1, max: 8);
    private readonly string workerId = ResolveWorkerId();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int matchWorkers = mode.RunsMatchWorker ? MatchWorkers : 0;
        int compileWorkers = mode.RunsCompileWorker ? CompileWorkers : 0;
        logger.LogInformation(
            "Job worker {WorkerId} started in {Role} role: match={MatchWorkers}, compile={CompileWorkers}, broadcast {Tps} ticks/s + {Delay}s countdown, rules {Rules}",
            workerId,
            mode.Name,
            matchWorkers,
            compileWorkers,
            matchSettings.BroadcastTicksPerSecond,
            matchSettings.BroadcastDelaySeconds,
            matchSettings.MatchRules.RulesVersion);

        List<Task> lanes = [];
        for (int index = 0; index < matchWorkers; index++)
            lanes.Add(RunLane(BackgroundJob.ExecuteMatchType, stoppingToken));
        for (int index = 0; index < compileWorkers; index++)
            lanes.Add(RunLane(BackgroundJob.CompileSubmissionType, stoppingToken));
        if (lanes.Count == 0)
            throw new InvalidOperationException($"Role '{mode.Name}' has no background job lanes.");
        await Task.WhenAll(lanes);
    }

    private async Task RunLane(
        string jobType,
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            bool didWork = false;
            try
            {
                didWork = await RunOneJobAsync(jobType, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Job lane ({Type}) iteration failed",
                    jobType);
            }

            if (!didWork)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }

    private async Task<bool> RunOneJobAsync(
        string jobType,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        BackgroundJobLeaseStore leases =
            scope.ServiceProvider.GetRequiredService<BackgroundJobLeaseStore>();
        BackgroundJob? job = await leases.ClaimAsync(
            jobType,
            workerId,
            cancellationToken);
        if (job is null)
            return false;

        JobTelemetry.RecordClaim(job.Type);
        logger.LogInformation("Running job {JobId} ({Type})", job.Id, job.Type);
        using Activity? activity =
            ApplicationTelemetry.ActivitySource.StartActivity("jobs.execute");
        activity?.SetTag("job.id", job.Id);
        activity?.SetTag("job.type", job.Type);
        var stopwatch = Stopwatch.StartNew();
        using var workCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task leaseHeartbeat = KeepLeaseAsync(job.Id, workCancellation);
        string outcome = "exception";
        try
        {
            BackgroundJobDispatcher dispatcher =
                scope.ServiceProvider.GetRequiredService<BackgroundJobDispatcher>();
            JobExecutionResult result = await dispatcher.DispatchAsync(
                job,
                workCancellation.Token);
            outcome = result.Outcome;
            bool completed = await leases.CompleteAsync(
                job.Id,
                workerId,
                workCancellation.Token);
            if (!completed)
            {
                throw new InvalidOperationException(
                    $"Job {job.Id} lease was lost before completion.");
            }
            logger.LogInformation(
                "Completed job {JobId} ({Type}) with outcome {Outcome}",
                job.Id,
                job.Type,
                outcome);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = "worker_stopping";
            throw;
        }
        catch (OperationCanceledException) when (workCancellation.IsCancellationRequested)
        {
            outcome = "lease_lost";
            logger.LogWarning(
                "Stopped job {JobId} ({Type}) after worker {WorkerId} lost its lease",
                job.Id,
                job.Type,
                workerId);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Job {JobId} ({Type}) failed",
                job.Id,
                job.Type);
            JobFailureOutcome failure = await leases.FailAsync(
                job,
                workerId,
                exception,
                CancellationToken.None);
            outcome = failure switch
            {
                JobFailureOutcome.RetryScheduled => "retry_scheduled",
                JobFailureOutcome.TerminalFailure => "terminal_failure",
                _ => "lease_lost",
            };
        }
        finally
        {
            workCancellation.Cancel();
            await leaseHeartbeat;
            stopwatch.Stop();
            JobTelemetry.RecordJob(job.Type, outcome, stopwatch.Elapsed);
            activity?.SetTag("application.outcome", outcome);
        }
        return true;
    }

    private async Task KeepLeaseAsync(
        long jobId,
        CancellationTokenSource workCancellation)
    {
        try
        {
            while (!workCancellation.IsCancellationRequested)
            {
                await Task.Delay(LeaseRefreshInterval, workCancellation.Token);
                try
                {
                    using IServiceScope scope = scopeFactory.CreateScope();
                    BackgroundJobLeaseStore leases =
                        scope.ServiceProvider.GetRequiredService<BackgroundJobLeaseStore>();
                    bool renewed = await leases.RenewAsync(
                        jobId,
                        workerId,
                        workCancellation.Token);
                    if (!renewed)
                    {
                        logger.LogWarning(
                            "Worker {WorkerId} lost lease for job {JobId}",
                            workerId,
                            jobId);
                        workCancellation.Cancel();
                        return;
                    }
                }
                catch (OperationCanceledException) when (
                    workCancellation.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    logger.LogWarning(
                        exception,
                        "Worker {WorkerId} could not refresh lease for job {JobId}",
                        workerId,
                        jobId);
                }
            }
        }
        catch (OperationCanceledException) when (workCancellation.IsCancellationRequested)
        {
        }
    }

    private static int ReadEnv(string name, int fallback, int min, int max) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out int value)
            ? Math.Clamp(value, min, max)
            : fallback;

    private static string ResolveWorkerId()
    {
        string configured =
            Environment.GetEnvironmentVariable("BOTARENA_INSTANCE_ID") ?? "";
        string value = string.IsNullOrWhiteSpace(configured)
            ? $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}"
            : configured.Trim();
        return value.Length <= 160 ? value : value[..160];
    }
}
