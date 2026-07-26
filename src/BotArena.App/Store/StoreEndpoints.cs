using System.Security.Claims;
using BotArena.App.Accounts;
using BotArena.App.Cosmetics;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Store;

public sealed record StorePackItemResponse(string Key, string Kind, string Id, string Label);

public sealed record StorePackResponse(
    string Id,
    string Label,
    string Description,
    IReadOnlyList<StorePackItemResponse> Items,
    /// <summary>Whether this account already owns everything in the pack.</summary>
    bool Owned);

public sealed record StoreResponse(
    /// <summary>Whether anything can actually be bought right now.</summary>
    bool Open,
    IReadOnlyList<StorePackResponse> Packs);

/// <summary>
/// What is for sale.
/// <para>
/// Anonymous-readable on purpose: a shop nobody can look at without an account is a worse
/// shop, and ownership is simply false for a visitor who is not signed in.
/// </para>
/// </summary>
public static class StoreEndpoints
{
    public static void MapStore(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/store", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            CosmeticCatalog catalog,
            IStorePaymentProvider payments,
            CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.UserId();
            // Revoked grants do not count as owned — nothing revokes today, but reading
            // the column here means a future revocation does not silently keep the pack
            // looking bought.
            HashSet<string> owned = userId is Guid id
                ? await db.EntitlementGrants
                    .Where(grant => grant.UserId == id && grant.RevokedAt == null)
                    .Select(grant => grant.EntitlementKey)
                    .ToHashSetAsync(cancellationToken)
                : [];

            var packs = catalog.Packs.Select(pack => new StorePackResponse(
                pack.Id,
                pack.Label,
                pack.Description,
                pack.Items
                    .Select(key => catalog.Find(
                        key.Split(':')[0], key[(key.IndexOf(':') + 1)..]))
                    .OfType<CosmeticCatalogItem>()
                    .Select(item => new StorePackItemResponse(
                        item.Key, item.Kind, item.Id, item.Label))
                    .ToArray(),
                // Every item, not any: a pack half-owned through some future overlap is
                // still worth buying, and showing it as owned would hide the rest.
                pack.Items.All(owned.Contains))).ToArray();

            return Results.Ok(new StoreResponse(payments.IsConfigured, packs));
        }).Produces<StoreResponse>().AllowAnonymous();
    }
}
