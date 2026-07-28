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
    public const int PeakRatingTarget = 1300;
    public const string PeakRatingUnit = "rating";

    /// <summary>
    /// The highest rating any of the account's bots holds on an official ladder.
    /// The ladder's persisted policy is authoritative, so a closed official era
    /// still counts while an experimental or otherwise ineligible ladder does not.
    /// </summary>
    public async Task<double> BestOfficialRatingAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await db.BotRatings
            .Where(rating => db.Ladders.Any(ladder =>
                ladder.Id == rating.LadderId &&
                ladder.AwardsAchievements))
            .Where(rating => db.Bots.Any(bot =>
                bot.Id == rating.BotId &&
                bot.OwnerUserId == userId))
            .Select(rating => (double?)rating.Rating)
            .MaxAsync(cancellationToken) ?? 0;
    }

    public async Task<int> CountRankedMatchesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await db.MatchSets
            .Where(set => set.Status == MatchSetStatus.Completed)
            .Where(set => db.Ladders.Any(ladder =>
                ladder.Id == set.LadderId &&
                ladder.AwardsAchievements))
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
                set.Status == MatchSetStatus.Completed &&
                db.Ladders.Any(ladder =>
                    ladder.Id == set.LadderId &&
                    ladder.AwardsAchievements))
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
            if (rankedMatches >= RankedMatchesTarget)
                inserted += await entitlements.GrantForEventAsync(
                    userId,
                    CosmeticUnlockEvents.Achievement,
                    CosmeticUnlockEvents.RankedMatches100,
                    new { rankedMatches, matchSetId },
                    cancellationToken);

            // Evaluated after the set has been rated, so the rating read here is the
            // one this match produced. Crossing the line at any point is permanent.
            double bestRating =
                await BestOfficialRatingAsync(userId, cancellationToken);
            if (bestRating >= PeakRatingTarget)
                inserted += await entitlements.GrantForEventAsync(
                    userId,
                    CosmeticUnlockEvents.Achievement,
                    CosmeticUnlockEvents.Rating1300,
                    new { bestRating, matchSetId },
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

    public async Task<CosmeticProgress> PeakRatingProgressAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        double current = await BestOfficialRatingAsync(userId, cancellationToken);
        return new CosmeticProgress(
            Math.Min((int)Math.Floor(current), PeakRatingTarget),
            PeakRatingTarget,
            PeakRatingUnit);
    }

    /// <summary>
    /// Progress for a milestone the account has not yet earned, or null when the
    /// unlock has no measurable progress. Keeps the endpoint from growing a branch
    /// per achievement.
    /// </summary>
    public Task<CosmeticProgress>? ProgressForAsync(
        string sourceKind,
        string sourceId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (sourceKind != CosmeticUnlockEvents.Achievement)
            return null;
        return sourceId switch
        {
            CosmeticUnlockEvents.RankedMatches100 =>
                RankedMatchesProgressAsync(userId, cancellationToken),
            CosmeticUnlockEvents.Rating1300 =>
                PeakRatingProgressAsync(userId, cancellationToken),
            _ => null,
        };
    }
}
