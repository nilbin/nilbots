namespace BotArena.App.Jobs;

/// <summary>
/// Typed dispatch for the durable job kinds. This deliberately stays a
/// concrete modular-monolith service rather than becoming a generic bus.
/// </summary>
public sealed class BackgroundJobDispatcher(
    CompileSubmissionJobHandler compileSubmission,
    MatchExecutionJobHandler matchExecution,
    AnnounceMatchResultJobHandler announceMatchResult)
{
    public Task<JobExecutionResult> DispatchAsync(
        BackgroundJob job,
        CancellationToken cancellationToken) =>
        job.Type switch
        {
            BackgroundJob.CompileSubmissionType =>
                compileSubmission.HandleAsync(
                    job.PayloadId("botVersionId"),
                    cancellationToken),
            BackgroundJob.ExecuteMatchType =>
                matchExecution.HandleAsync(
                    job.PayloadId("matchId"),
                    cancellationToken),
            BackgroundJob.AnnounceMatchResultType =>
                announceMatchResult.HandleAsync(
                    job.PayloadId("matchId"),
                    cancellationToken),
            _ => throw new InvalidOperationException(
                $"Unknown job type '{job.Type}'."),
        };
}
