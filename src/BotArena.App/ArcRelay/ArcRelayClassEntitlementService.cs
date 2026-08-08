using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.ArcRelay;

public sealed class ArcRelayClassEntitlementService(
    AppDbContext db,
    ArcRelayClassCatalog catalog)
{
    public async Task<IReadOnlySet<string>> UnlockedAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        bool isSystem = await db.Users
            .Where(user => user.Id == userId)
            .Select(user => user.IsSystem)
            .SingleOrDefaultAsync(cancellationToken);
        if (isSystem)
        {
            return catalog.All.Select(value => value.Id)
                .ToHashSet(StringComparer.Ordinal);
        }

        HashSet<string> unlocked = catalog.StarterIds
            .ToHashSet(StringComparer.Ordinal);
        string[] keys = await db.EntitlementGrants
            .Where(grant =>
                grant.UserId == userId
                && grant.RevokedAt == null
                && grant.EntitlementKey.StartsWith(
                    ArcRelayClassCatalog.EntitlementPrefix))
            .Select(grant => grant.EntitlementKey)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        foreach (string key in keys)
        {
            string classId = key[ArcRelayClassCatalog.EntitlementPrefix.Length..];
            if (catalog.Contains(classId))
                unlocked.Add(classId);
        }
        return unlocked;
    }
}
