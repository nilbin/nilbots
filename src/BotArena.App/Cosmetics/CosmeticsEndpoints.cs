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
                CancellationToken cancellationToken) =>
            {
                IReadOnlyList<CosmeticCatalogEntry> items =
                    await entitlements.CatalogForAsync(
                        principal.UserId(),
                        cancellationToken);
                return Results.Ok(new { catalog.Version, Items = items });
            });
    }
}
