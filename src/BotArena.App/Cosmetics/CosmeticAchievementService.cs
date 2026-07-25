using BotArena.App.Matches;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Cosmetics;

public sealed record CosmeticProgress(int Current, int Target, string Unit);

/// <summary>
/// Evaluates explicit cosmetic achievements from durable product data. This is
/// intentionally not a generic rules engine: each milestone owns its query and
/// emits an idempotent event through the shared entitlement ledger.
/// </summary>
public sealed class CosmeticAchievementService(
    AppDbContext db,
    CosmeticEntitlementService entitlements)
{
    public const int RankedMatchesTarget = 100;
    public const string RankedMatchesUnit = "ranked-matches";

    public async Task<int> CountRankedMatchesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await db.MatchSets
            .Where(set => set.Status == MatchSetStatus.Completed)
            .Where(set => db.Bots.Any(bot =>
                bot.OwnerUserId == userId &&
                (bot.Id == set.BotAId || bot.Id == set.BotBId)))
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// Re-evaluates every account represented in a completed ranked set. A set
    /// is one user-facing ranked match even though it contains six mirrored
    /// simulations. Ledger uniqueness makes worker retries safe.
    /// </summary>
    public async Task<int> AwardForCompletedRankedSetAsync(
        Guid matchSetId,
        CancellationToken cancellationToken = default)
    {
        var set = await db.MatchSets
            .Where(set =>
                set.Id == matchSetId &&
                set.Status == MatchSetStatus.Completed)
            .Select(set => new { set.BotAId, set.BotBId })
            .SingleOrDefaultAsync(cancellationToken);
        if (set is null)
            return 0;
        Guid[] botIds = [set.BotAId, set.BotBId];

        Guid[] userIds = await db.Bots
            .Where(bot => botIds.Contains(bot.Id))
            .Select(bot => bot.OwnerUserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        int inserted = 0;
        foreach (Guid userId in userIds)
        {
            int rankedMatches =
                await CountRankedMatchesAsync(userId, cancellationToken);
            if (rankedMatches < RankedMatchesTarget)
                continue;

            inserted += await entitlements.GrantForEventAsync(
                userId,
                CosmeticUnlockEvents.Achievement,
                CosmeticUnlockEvents.RankedMatches100,
                new { rankedMatches, matchSetId },
                cancellationToken);
        }
        return inserted;
    }

    public async Task<CosmeticProgress> RankedMatchesProgressAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        int current = await CountRankedMatchesAsync(userId, cancellationToken);
        return new CosmeticProgress(
            Math.Min(current, RankedMatchesTarget),
            RankedMatchesTarget,
            RankedMatchesUnit);
    }
}
