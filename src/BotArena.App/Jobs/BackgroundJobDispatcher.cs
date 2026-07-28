using BotArena.App.Competition;

namespace BotArena.App.Jobs;

/// <summary>
/// Typed dispatch for the durable job kinds. This deliberately stays a
/// concrete modular-monolith service rather than becoming a generic bus.
/// </summary>
public sealed class BackgroundJobDispatcher(
    CompileSubmissionJobHandler compileSubmission,
    MatchExecutionJobHandler matchExecution,
    GenericActorMatchExecutionJobHandler genericActorMatchExecution,
    AnnounceMatchResultJobHandler announceMatchResult,
    AnnounceSetResultJobHandler announceSetResult,
    DeliverPushJobHandler deliverPush,
    HostedGenericMatchDefinitionRegistry genericDefinitions)
{
    public Task<JobExecutionResult> DispatchAsync(
        BackgroundJob job,
        CancellationToken cancellationToken)
    {
        if (genericDefinitions.SupportsJobType(job.Type))
        {
            return genericActorMatchExecution.HandleAsync(
                job.PayloadId("matchId"),
                job.Type,
                cancellationToken);
        }

        return job.Type switch
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
            BackgroundJob.AnnounceSetResultType =>
                announceSetResult.HandleAsync(
                    job.PayloadId("matchSetId"),
                    cancellationToken),
            BackgroundJob.DeliverPushType =>
                deliverPush.HandleAsync(
                    job.PayloadId("notificationId"),
                    cancellationToken),
            _ => throw new InvalidOperationException(
                $"Unknown job type '{job.Type}'."),
        };
    }
}
