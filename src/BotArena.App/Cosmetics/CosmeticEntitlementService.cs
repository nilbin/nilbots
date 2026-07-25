using System.Text.Json;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Cosmetics;

public sealed record CosmeticAccess(
    CosmeticCatalogItem? Item,
    bool Owned);

public sealed record CosmeticCatalogEntry(
    string Key,
    string Kind,
    string Id,
    string Label,
    string Availability,
    CosmeticUnlock? Unlock,
    bool Owned);

public sealed class CosmeticEntitlementService(
    AppDbContext db,
    CosmeticCatalog catalog)
{
    public async Task<CosmeticAccess> CheckAccessAsync(
        Guid userId,
        string kind,
        string id,
        CancellationToken cancellationToken = default)
    {
        CosmeticCatalogItem? item = catalog.Find(kind, id);
        if (item is null)
            return new CosmeticAccess(null, false);
        if (item.Availability == CosmeticCatalog.StarterAvailability)
            return new CosmeticAccess(item, true);

        bool owned = await db.Users
            .Where(user => user.Id == userId)
            .Select(user =>
                user.IsSystem ||
                db.EntitlementGrants.Any(grant =>
                    grant.UserId == userId &&
                    grant.EntitlementKey == item.Key &&
                    grant.RevokedAt == null))
            .SingleOrDefaultAsync(cancellationToken);
        return new CosmeticAccess(item, owned);
    }

    public async Task<IReadOnlyList<CosmeticCatalogEntry>> CatalogForAsync(
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        bool isSystem = false;
        HashSet<string> active = [];
        if (userId is Guid accountId)
        {
            isSystem = await db.Users
                .Where(user => user.Id == accountId)
                .Select(user => user.IsSystem)
                .SingleOrDefaultAsync(cancellationToken);
            active = (await db.EntitlementGrants
                    .Where(grant => grant.UserId == accountId && grant.RevokedAt == null)
                    .Select(grant => grant.EntitlementKey)
                    .Distinct()
                    .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.Ordinal);
        }

        return catalog.Items.Select(item => new CosmeticCatalogEntry(
            item.Key,
            item.Kind,
            item.Id,
            item.Label,
            item.Availability,
            item.Unlock,
            item.Availability == CosmeticCatalog.StarterAvailability ||
            isSystem ||
            active.Contains(item.Key)))
            .ToArray();
    }

    /// <summary>
    /// Emits every catalog grant mapped to a durable product event. PostgreSQL's
    /// conflict handling makes worker retries and concurrent first-builds idempotent.
    /// </summary>
    public async Task<int> GrantForEventAsync(
        Guid userId,
        string sourceKind,
        string sourceId,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CosmeticCatalogItem> items =
            catalog.EntitlementsFor(sourceKind, sourceId);
        string? metadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata);
        int inserted = 0;
        foreach (CosmeticCatalogItem item in items)
        {
            inserted += await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "EntitlementGrants"
                    ("Id", "UserId", "EntitlementKey", "SourceKind", "SourceId",
                     "GrantedAt", "RevokedAt", "MetadataJson")
                VALUES
                    ({Guid.NewGuid()}, {userId}, {item.Key}, {sourceKind}, {sourceId},
                     {DateTime.UtcNow}, NULL, CAST({metadataJson} AS jsonb))
                ON CONFLICT ("UserId", "EntitlementKey", "SourceKind", "SourceId")
                DO NOTHING
                """, cancellationToken);
        }
        return inserted;
    }
}
