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
    bool Owned,
    /// <summary>
    /// True when owning it again would do something.
    /// <para>
    /// Appearance is owned once and then owned forever, but capacity stacks — a second
    /// grant of extra builds adds a second thirty. Without this the store would either grey
    /// out a pack that is still worth buying, or offer one that would take money for
    /// nothing.
    /// </para>
    /// </summary>
    bool Repeatable);

public sealed record StoreCategoryResponse(
    string Id,
    string Label,
    IReadOnlyList<StorePackResponse> Packs);

public sealed record StoreResponse(
    /// <summary>Whether anything can actually be bought right now.</summary>
    bool Open,
    IReadOnlyList<StoreCategoryResponse> Categories);

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

            StorePackResponse Describe(CosmeticPack pack) => new(
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
                pack.Items.All(owned.Contains),
                pack.Category == CosmeticCatalog.CapacityCategory);

            // Empty categories are dropped rather than rendered as a heading with nothing
            // under it, and the order is the catalog's rather than the dictionary's.
            var categories = CosmeticCatalog.Categories
                .Select(category => new StoreCategoryResponse(
                    category,
                    CategoryLabel(category),
                    catalog.Packs
                        .Where(pack => pack.Category == category)
                        .Select(Describe)
                        .ToArray()))
                .Where(category => category.Packs.Count > 0)
                .ToArray();

            return Results.Ok(new StoreResponse(payments.IsConfigured, categories));
        }).Produces<StoreResponse>().AllowAnonymous();
    }

    /// <summary>
    /// Shelf headings, server-side so the store reads the same on the site and in the app.
    /// </summary>
    private static string CategoryLabel(string category) => category switch
    {
        CosmeticCatalog.AppearanceCategory => "Appearance",
        CosmeticCatalog.CapacityCategory => "Your account",
        _ => category,
    };
}
