using BotArena.App.Accounts;
using BotArena.App.Cosmetics;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Tests;

public class CosmeticEntitlementServiceIntegrationTests
{
    [SkippableFact]
    public async Task PostgreSqlGrant_IsIdempotentAndAuthorizesTheMappedItem()
    {
        string? connection = Environment.GetEnvironmentVariable("BOTARENA_TEST_DB");
        Skip.If(
            string.IsNullOrWhiteSpace(connection),
            "Set BOTARENA_TEST_DB to a disposable PostgreSQL database.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection)
            .UseOpenIddict()
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var user = new User
        {
            DisplayName = "cosmetic-test",
            Email = $"cosmetic-{Guid.NewGuid():N}@example.test",
            PasswordHash = "not-used",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        CosmeticCatalog catalog = CosmeticCatalog.LoadDefault();
        var service = new CosmeticEntitlementService(db, catalog);

        CosmeticAccess starter = await service.CheckAccessAsync(
            user.Id,
            CosmeticCatalog.BotLookKind,
            "vanguard");
        CosmeticAccess locked = await service.CheckAccessAsync(
            user.Id,
            CosmeticCatalog.BotLookKind,
            "lancer");
        Assert.True(starter.Owned);
        Assert.False(locked.Owned);

        int first = await service.GrantForEventAsync(
            user.Id,
            CosmeticUnlockEvents.Achievement,
            CosmeticUnlockEvents.FirstSuccessfulBuild,
            new { botVersionId = Guid.NewGuid() });
        int retry = await service.GrantForEventAsync(
            user.Id,
            CosmeticUnlockEvents.Achievement,
            CosmeticUnlockEvents.FirstSuccessfulBuild,
            new { botVersionId = Guid.NewGuid() });

        Assert.Equal(1, first);
        Assert.Equal(0, retry);
        Assert.True((await service.CheckAccessAsync(
            user.Id,
            CosmeticCatalog.BotLookKind,
            "lancer")).Owned);
        Assert.Single(await db.EntitlementGrants
            .Where(grant =>
                grant.UserId == user.Id &&
                grant.EntitlementKey == "bot-look:lancer")
            .ToListAsync());
    }
}
