using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotArena.App.Notifications;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BotArena.App.Cosmetics;

public sealed record CosmeticAccess(
    CosmeticCatalogItem? Item,
    bool Owned);

public readonly record struct CosmeticAccessRequest(
    Guid UserId,
    string Kind,
    string Id);

public sealed record CosmeticCatalogEntry(
    string Key,
    string Kind,
    string Id,
    string Label,
    string Availability,
    CosmeticUnlock? Unlock,
    bool Owned,
    CosmeticProgress? Progress = null);

public sealed class CosmeticEntitlementService
{
    private readonly AppDbContext db;
    private readonly CosmeticCatalog catalog;
    private readonly TimeProvider timeProvider;

    public CosmeticEntitlementService(
        AppDbContext db,
        CosmeticCatalog catalog,
        TimeProvider? timeProvider = null)
    {
        this.db = db;
        this.catalog = catalog;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

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

    /// <summary>
    /// Resolves many account/item checks with a constant number of database
    /// queries. This is the batch counterpart to <see cref="CheckAccessAsync"/>
    /// for roster and matchmaking projections; catalog-only starter and unknown
    /// items still require no database access.
    /// </summary>
    public async Task<IReadOnlyDictionary<CosmeticAccessRequest, CosmeticAccess>>
        CheckAccessBatchAsync(
            IReadOnlyCollection<CosmeticAccessRequest> requests,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);

        CosmeticAccessRequest[] distinctRequests =
            [.. requests.Distinct()];
        if (distinctRequests.Length == 0)
        {
            return new Dictionary<CosmeticAccessRequest, CosmeticAccess>();
        }

        var items = distinctRequests.ToDictionary(
            request => request,
            request => catalog.Find(request.Kind, request.Id));
        CosmeticAccessRequest[] gatedRequests =
        [
            .. distinctRequests.Where(request =>
                items[request] is
                {
                    Availability: not CosmeticCatalog.StarterAvailability,
                }),
        ];
        if (gatedRequests.Length == 0)
        {
            return distinctRequests.ToDictionary(
                request => request,
                request => new CosmeticAccess(
                    items[request],
                    items[request] is not null));
        }

        Guid[] accountIds =
            [.. gatedRequests.Select(request => request.UserId).Distinct()];
        HashSet<Guid> systemAccounts = (await db.Users
                .Where(user =>
                    accountIds.Contains(user.Id) &&
                    user.IsSystem)
                .Select(user => user.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        string[] entitlementKeys =
        [
            .. gatedRequests
                .Select(request => items[request]!.Key)
                .Distinct(StringComparer.Ordinal),
        ];
        var activeGrants = (await db.EntitlementGrants
                .Where(grant =>
                    accountIds.Contains(grant.UserId) &&
                    entitlementKeys.Contains(grant.EntitlementKey) &&
                    grant.RevokedAt == null)
                .Select(grant => new
                {
                    grant.UserId,
                    grant.EntitlementKey,
                })
                .Distinct()
                .ToListAsync(cancellationToken))
            .Select(grant => (grant.UserId, grant.EntitlementKey))
            .ToHashSet();

        return distinctRequests.ToDictionary(
            request => request,
            request =>
            {
                CosmeticCatalogItem? item = items[request];
                bool owned = item is not null &&
                    (item.Availability ==
                        CosmeticCatalog.StarterAvailability ||
                     systemAccounts.Contains(request.UserId) ||
                     activeGrants.Contains((request.UserId, item.Key)));
                return new CosmeticAccess(item, owned);
            });
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
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        int inserted = 0;
        var newlyOwned = new List<CosmeticCatalogItem>();
        IDbContextTransaction? ownTransaction = null;
        try
        {
            if (db.Database.CurrentTransaction is null)
                ownTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

            // Serialize entitlement transitions for one account. This distinguishes a
            // genuinely new entitlement from another source granting something the
            // account already owns, even when workers finish concurrently.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT 1
                FROM "Users"
                WHERE "Id" = {userId}
                FOR UPDATE
                """, cancellationToken);

            foreach (CosmeticCatalogItem item in items)
            {
                bool ownedBefore = await db.EntitlementGrants.AnyAsync(
                    grant =>
                        grant.UserId == userId &&
                        grant.EntitlementKey == item.Key &&
                        grant.RevokedAt == null,
                    cancellationToken);
                int grantInserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "EntitlementGrants"
                        ("Id", "UserId", "EntitlementKey", "SourceKind", "SourceId",
                         "GrantedAt", "RevokedAt", "MetadataJson")
                    VALUES
                        ({Guid.NewGuid()}, {userId}, {item.Key}, {sourceKind}, {sourceId},
                         {now}, NULL, CAST({metadataJson} AS jsonb))
                    ON CONFLICT ("UserId", "EntitlementKey", "SourceKind", "SourceId")
                    DO NOTHING
                    """, cancellationToken);
                inserted += grantInserted;
                if (grantInserted == 1 && !ownedBefore)
                    newlyOwned.Add(item);
            }

            if (newlyOwned.Count > 0)
                await CreateNotificationAsync(
                    userId,
                    sourceKind,
                    sourceId,
                    newlyOwned,
                    now,
                    cancellationToken);

            if (ownTransaction is not null)
                await ownTransaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (ownTransaction is not null)
                await ownTransaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (ownTransaction is not null)
                await ownTransaction.DisposeAsync();
        }
        return inserted;
    }

    private async Task CreateNotificationAsync(
        Guid userId,
        string sourceKind,
        string sourceId,
        IReadOnlyList<CosmeticCatalogItem> items,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        Guid notificationId = Guid.NewGuid();
        string fingerprint = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(string.Join(
                    "|",
                    items.Select(item => item.Key).Order(StringComparer.Ordinal)))))
            .ToLowerInvariant()[..16];
        string dedupeKey =
            $"{UserNotificationKinds.EntitlementEarned}:{sourceKind}:{sourceId}:{fingerprint}";
        var payload = new EntitlementEarnedPayload(
            sourceKind,
            sourceId,
            CosmeticUnlockEvents.NotificationReason(sourceKind, sourceId),
            items.Select(item => new EntitlementNotificationItem(
                    item.Key,
                    item.Kind,
                    item.Id,
                    item.Label))
                .ToArray());
        string payloadJson = UserNotificationContracts.Serialize(payload);

        int notificationInserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "UserNotifications"
                ("Id", "UserId", "Kind", "DedupeKey", "PayloadJson",
                 "CreatedAt", "ReadAt")
            VALUES
                ({notificationId}, {userId}, {UserNotificationKinds.EntitlementEarned},
                 {dedupeKey}, CAST({payloadJson} AS jsonb), {createdAt}, NULL)
            ON CONFLICT ("UserId", "DedupeKey")
            DO NOTHING
            """, cancellationToken);
        if (notificationInserted == 1)
        {
            // PostgreSQL emits this only when the surrounding transaction commits.
            // Every web process LISTENs and forwards it to its local SignalR clients.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT pg_notify(
                    {PostgresNotificationListener.Channel},
                    {notificationId.ToString()})
                """, cancellationToken);
        }
    }
}
