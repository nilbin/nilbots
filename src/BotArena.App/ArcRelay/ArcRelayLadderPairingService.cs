using BotArena.App.Competition;
using BotArena.App.Matches;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.ArcRelay;

public sealed class ArcRelayLadderPairingService(
    AppDbContext db,
    ArcRelayMatchAdmissionService admission,
    ArcRelayEntrantProjector projector,
    TimeProvider timeProvider)
{
    public async Task<int> PairAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({AdmissionLocks.LabsMatchPool})", cancellationToken);
        int active = await db.Matches.CountAsync(value =>
            value.ArcRelayLane == ArcRelayMatchLane.Ranked &&
            (value.Status == MatchStatus.Pending || value.Status == MatchStatus.Running), cancellationToken);
        int room = Math.Min(ArcRelayLadderPolicy.MaximumPairingsPerPass,
            Math.Max(0, ArcRelayLadderPolicy.MaximumQueuedOrRunningMatches - active));
        if (room == 0) return 0;
        Guid ladderId = await projector.LadderIdAsync(cancellationToken);
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        DateTime dayAgo = now.AddDays(-1);
        DateTime recentCutoff = now.AddHours(-ArcRelayLadderPolicy.RecentOpponentAvoidanceHours);
        Candidate[] candidates = await (
            from entrant in db.ArcRelayEntrants
            join rating in db.ArcRelayEntrantRatings on entrant.Id equals rating.EntrantId
            where rating.LadderId == ladderId && entrant.LadderOptedIn && entrant.SuspensionReason == null &&
                (entrant.Kind == ArcRelayEntrantKind.Sheet || entrant.PreflightStatus == ArcRelayPreflightStatus.Passed)
            orderby rating.Rating, entrant.Id
            select new Candidate(entrant, rating.Rating)).ToArrayAsync(cancellationToken);
        var used = new HashSet<Guid>();
        int paired = 0;
        foreach (Candidate first in candidates)
        {
            if (paired >= room || used.Contains(first.Entrant.Id)) continue;
            if (await MatchesToday(first.Entrant.Id) >= ArcRelayLadderPolicy.MaximumMatchesPerEntrantPerDay) continue;
            Candidate? second = null;
            foreach (Candidate option in candidates
                .Where(value => !used.Contains(value.Entrant.Id) && value.Entrant.Id != first.Entrant.Id && value.Entrant.OwnerUserId != first.Entrant.OwnerUserId)
                .OrderBy(value => Math.Abs(value.Rating - first.Rating)).ThenBy(value => value.Entrant.Id))
            {
                if (await MatchesToday(option.Entrant.Id) >= ArcRelayLadderPolicy.MaximumMatchesPerEntrantPerDay) continue;
                bool recent = await (from ranked in db.ArcRelayRankedMatches
                    join priorMatch in db.Matches on ranked.MatchId equals priorMatch.Id
                    where priorMatch.CreatedAt >= recentCutoff &&
                        ((ranked.EntrantAId == first.Entrant.Id && ranked.EntrantBId == option.Entrant.Id) ||
                         (ranked.EntrantAId == option.Entrant.Id && ranked.EntrantBId == first.Entrant.Id))
                    select ranked.MatchId).AnyAsync(cancellationToken);
                if (!recent) { second = option; break; }
            }
            if (second is null) continue;
            Match match = await admission.CreateAsync(first.Entrant, second.Entrant,
                ArcRelayMatchLane.Ranked, null, null, cancellationToken);
            db.ArcRelayRankedMatches.Add(new ArcRelayRankedMatch
            {
                MatchId = match.Id, LadderId = ladderId,
                EntrantAId = first.Entrant.Id, EntrantBId = second.Entrant.Id,
                RatingABefore = first.Rating, RatingBBefore = second.Rating,
            });
            used.Add(first.Entrant.Id); used.Add(second.Entrant.Id); paired++;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return paired;

        Task<int> MatchesToday(Guid entrantId) =>
            (from ranked in db.ArcRelayRankedMatches
             join match in db.Matches on ranked.MatchId equals match.Id
             where match.CreatedAt >= dayAgo &&
                 (ranked.EntrantAId == entrantId || ranked.EntrantBId == entrantId)
             select ranked.MatchId).CountAsync(cancellationToken);
    }

    private sealed record Candidate(ArcRelayEntrant Entrant, double Rating);
}

public sealed class ArcRelayLadderPairingWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ArcRelayLadderPairingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                int count = await scope.ServiceProvider.GetRequiredService<ArcRelayLadderPairingService>()
                    .PairAsync(stoppingToken);
                if (count > 0) logger.LogInformation("Paired {Count} Arc Relay ranked matches", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogError(exception, "Arc Relay passive pairing pass failed"); }
            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
}
