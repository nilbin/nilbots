using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Matches;

/// <summary>
/// Projects the same durable rolling-window facts used by Arena mutations.
/// The result is advisory: mutations still re-evaluate while holding their
/// account admission lock.
/// </summary>
public sealed class ArenaAllowanceService(
    AppDbContext db,
    UnrankedMatchLimits unrankedLimits,
    RankedSetLimits rankedLimits,
    TimeProvider timeProvider)
{
    public const int RollingWindowHours = 24;

    public async Task<(
        ArenaAllowanceResponse Unranked,
        RankedArenaAllowanceResponse Ranked)> ProjectAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        DateTime windowStart = now.AddHours(-RollingWindowHours);

        List<DateTime> unrankedStarts = await db.Matches
            .AsNoTracking()
            .Where(match =>
                match.InitiatedByUserId == userId &&
                match.MatchSetId == null &&
                match.CreatedAt >= windowStart)
            .OrderBy(match => match.CreatedAt)
            .Select(match => match.CreatedAt)
            .ToListAsync(cancellationToken);

        var ownedSets =
            from set in db.MatchSets.AsNoTracking()
            join bot in db.Bots.AsNoTracking()
                on set.BotAId equals bot.Id
            where bot.OwnerUserId == userId
            select set;
        List<DateTime> rankedStarts = await ownedSets
            .Where(set => set.CreatedAt >= windowStart)
            .OrderBy(set => set.CreatedAt)
            .Select(set => set.CreatedAt)
            .ToListAsync(cancellationToken);
        int rankedInProgress = await ownedSets.CountAsync(
            set => set.Status == MatchSetStatus.Running,
            cancellationToken);

        List<string> entitlementKeys = await db.EntitlementGrants
            .AsNoTracking()
            .Where(grant =>
                grant.UserId == userId &&
                grant.RevokedAt == null)
            .Select(grant => grant.EntitlementKey)
            .ToListAsync(cancellationToken);
        RankedSetLimits effectiveRankedLimits =
            rankedLimits.ForAccount(entitlementKeys);

        ArenaAllowanceResponse unranked = ProjectUnranked(
            unrankedStarts,
            unrankedLimits.AccountDailyLimit);
        RankedArenaAllowanceResponse ranked = ProjectRanked(
            rankedStarts,
            effectiveRankedLimits.AccountDailyLimit,
            rankedInProgress,
            effectiveRankedLimits.AccountConcurrentLimit);
        return (unranked, ranked);
    }

    private static ArenaAllowanceResponse ProjectUnranked(
        IReadOnlyList<DateTime> starts,
        int limit)
    {
        int used = starts.Count;
        DateTime? nextDailySlotAt = NextDailySlotAt(starts, limit);
        bool canStart = used < limit;
        return new ArenaAllowanceResponse(
            used,
            limit,
            Math.Max(0, limit - used),
            RollingWindowHours,
            nextDailySlotAt,
            canStart,
            canStart
                ? null
                : ApplicationErrorCodes.MatchUnrankedDailyLimit,
            canStart ? null : nextDailySlotAt);
    }

    private static RankedArenaAllowanceResponse ProjectRanked(
        IReadOnlyList<DateTime> starts,
        int dailyLimit,
        int inProgress,
        int concurrencyLimit)
    {
        int used = starts.Count;
        DateTime? nextDailySlotAt = NextDailySlotAt(
            starts,
            dailyLimit);
        ApplicationError? refusal = RankedSetPolicy.EvaluateError(
            new RankedSetSnapshot(used, inProgress),
            new RankedSetLimits(dailyLimit, concurrencyLimit));
        string? refusalCode = refusal?.Code;
        return new RankedArenaAllowanceResponse(
            used,
            dailyLimit,
            Math.Max(0, dailyLimit - used),
            RollingWindowHours,
            nextDailySlotAt,
            inProgress,
            concurrencyLimit,
            refusalCode is null,
            refusalCode,
            refusalCode == ApplicationErrorCodes.MatchRankedDailyLimit
                ? nextDailySlotAt
                : null);
    }

    private static DateTime? NextDailySlotAt(
        IReadOnlyList<DateTime> orderedStarts,
        int limit)
    {
        if (orderedStarts.Count < limit)
            return null;

        // A deployment can lower a configured limit below recent use. In that
        // case enough starts must leave the window to create one actual slot.
        int firstStartThatMustExpire = orderedStarts.Count - limit;
        return orderedStarts[firstStartThatMustExpire]
            .AddHours(RollingWindowHours);
    }
}
