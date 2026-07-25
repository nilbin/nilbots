using System.Security.Claims;
using BotArena.App.Accounts;

namespace BotArena.App.Cosmetics;

public static class CosmeticsEndpoints
{
    public static void MapCosmetics(this IEndpointRouteBuilder routes)
    {
        routes.MapGet(
            "/api/cosmetics",
            async (
                ClaimsPrincipal principal,
                CosmeticCatalog catalog,
                CosmeticEntitlementService entitlements,
                CosmeticAchievementService achievements,
                CancellationToken cancellationToken) =>
            {
                Guid? userId = principal.UserId();
                IReadOnlyList<CosmeticCatalogEntry> items =
                    await entitlements.CatalogForAsync(
                        userId,
                        cancellationToken);
                if (userId is Guid accountId &&
                    items.Any(item =>
                        !item.Owned &&
                        item.Unlock?.SourceKind == CosmeticUnlockEvents.Achievement &&
                        item.Unlock.SourceId == CosmeticUnlockEvents.RankedMatches100))
                {
                    CosmeticProgress progress =
                        await achievements.RankedMatchesProgressAsync(
                            accountId,
                            cancellationToken);
                    items = items.Select(item =>
                            !item.Owned &&
                            item.Unlock?.SourceKind == CosmeticUnlockEvents.Achievement &&
                            item.Unlock.SourceId == CosmeticUnlockEvents.RankedMatches100
                                ? item with { Progress = progress }
                                : item)
                        .ToArray();
                }
                return Results.Ok(new { catalog.Version, Items = items });
            });
    }
}
