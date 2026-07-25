using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Shared;

public sealed class ApplicationActorFactory(AppDbContext db)
{
    public async Task<ApplicationActor> ResolveAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        Guid? accountId = Guid.TryParse(
            principal.FindFirstValue(ClaimTypes.NameIdentifier),
            out Guid parsed)
            ? parsed
            : null;
        bool isSystem = accountId is Guid id &&
            await db.Users
                .Where(user => user.Id == id)
                .Select(user => user.IsSystem)
                .SingleOrDefaultAsync(cancellationToken);
        IReadOnlySet<string> roles = principal.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.Ordinal);
        return new ApplicationActor(accountId, isSystem, roles);
    }
}
