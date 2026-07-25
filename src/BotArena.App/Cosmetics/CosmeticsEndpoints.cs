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
                if (userId is Guid accountId)
                {
                    // One measurement per distinct milestone, shared by every item that
                    // milestone unlocks, so a paired look and projectile cost one query.
                    var measured = new Dictionary<(string, string), CosmeticProgress>();
                    foreach (CosmeticCatalogEntry item in items)
                    {
                        if (item.Owned || item.Unlock is null)
                            continue;
                        var milestone = (item.Unlock.SourceKind, item.Unlock.SourceId);
                        if (measured.ContainsKey(milestone))
                            continue;
                        Task<CosmeticProgress>? pending = achievements.ProgressForAsync(
                            item.Unlock.SourceKind,
                            item.Unlock.SourceId,
                            accountId,
                            cancellationToken);
                        if (pending is not null)
                            measured[milestone] = await pending;
                    }
                    if (measured.Count > 0)
                        items = items.Select(item =>
                                !item.Owned &&
                                item.Unlock is not null &&
                                measured.TryGetValue(
                                    (item.Unlock.SourceKind, item.Unlock.SourceId),
                                    out CosmeticProgress? progress)
                                    ? item with { Progress = progress }
                                    : item)
                            .ToArray();
                }
                return Results.Ok(new { catalog.Version, Items = items });
            });
    }
}
