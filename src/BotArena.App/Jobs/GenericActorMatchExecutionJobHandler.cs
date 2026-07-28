using BotArena.App.Competition;
using BotArena.App.Matches;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Jobs;

/// <summary>
/// Durable-job boundary for hosted generic actor matches. Each immutable
/// playlist version has an exact queue capability, so mixed-version workers
/// cannot claim definitions they do not have while the legacy Duel lane
/// remains separate.
/// </summary>
public sealed class GenericActorMatchExecutionJobHandler(
    AppDbContext db,
    GenericActorMatchExecutor executor,
    HostedGenericMatchDefinitionRegistry definitions)
{
    public async Task<JobExecutionResult> HandleAsync(
        Guid matchId,
        string executionJobType,
        CancellationToken cancellationToken)
    {
        IHostedGenericMatchDefinition queuedDefinition =
            definitions.ResolveJobType(executionJobType);
        Match match = await db.Matches
            .Include(candidate => candidate.Participants)
            .SingleAsync(
                candidate => candidate.Id == matchId,
                cancellationToken);

        if (match.PlaylistVersionId is not Guid playlistVersionId)
        {
            throw new InvalidOperationException(
                $"Generic actor match {match.Id} has no pinned " +
                "playlist version.");
        }

        PlaylistVersion playlistVersion =
            await db.PlaylistVersions
                .AsNoTracking()
                .SingleAsync(
                    candidate =>
                        candidate.Id == playlistVersionId,
                    cancellationToken);
        string playlistKey = await db.Playlists
            .AsNoTracking()
            .Where(
                candidate =>
                    candidate.Id == playlistVersion.PlaylistId)
            .Select(candidate => candidate.Key)
            .SingleAsync(cancellationToken);
        if (!string.Equals(
                queuedDefinition.PlaylistKey,
                playlistKey,
                StringComparison.Ordinal) ||
            queuedDefinition.Version != playlistVersion.Version)
        {
            throw new InvalidOperationException(
                $"Generic actor job type '{executionJobType}' does not " +
                $"match playlist '{playlistKey}' " +
                $"v{playlistVersion.Version} pinned by match {match.Id}.");
        }
        if (!string.Equals(
                playlistVersion.ExecutionPolicyId,
                PlaylistExecutionPolicyIds.GenericActor,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Generic actor job for match {match.Id} cannot execute " +
                $"playlist policy " +
                $"'{playlistVersion.ExecutionPolicyId}'.");
        }

        if (match.Status is MatchStatus.Completed or MatchStatus.Failed)
        {
            return new JobExecutionResult(
                match.Status == MatchStatus.Completed
                    ? "already_completed"
                    : "already_failed");
        }

        return await executor.HandleAsync(
            match,
            playlistVersion,
            cancellationToken);
    }
}
