using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Cosmetics;
using BotArena.App.Matches;
using BotArena.App.Notifications;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Tests;

public class CosmeticEntitlementServiceIntegrationTests
{
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task PostgreSqlGrant_IsIdempotentAndAuthorizesTheMappedItem()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using var db = await database.CreateMigratedContextAsync();

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
        UserNotification notification =
            Assert.Single(await db.UserNotifications.ToListAsync());
        Assert.Equal(UserNotificationKinds.EntitlementEarned, notification.Kind);
        UserNotificationResponse response =
            UserNotificationContracts.ToResponse(notification);
        Assert.Equal(
            "lancer",
            response.Payload.GetProperty("items")[0].GetProperty("id").GetString());
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task AdditionalGrantSource_DoesNotAnnounceAnAlreadyOwnedItem()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using var db = await database.CreateMigratedContextAsync();

        var user = new User
        {
            DisplayName = "existing-cosmetic-test",
            Email = $"existing-cosmetic-{Guid.NewGuid():N}@example.test",
            PasswordHash = "not-used",
        };
        db.Users.Add(user);
        db.EntitlementGrants.Add(new EntitlementGrant
        {
            UserId = user.Id,
            EntitlementKey = "bot-look:lancer",
            SourceKind = "promotion",
            SourceId = "existing-owner",
        });
        await db.SaveChangesAsync();

        var service = new CosmeticEntitlementService(
            db,
            CosmeticCatalog.LoadDefault());
        Assert.Equal(
            1,
            await service.GrantForEventAsync(
                user.Id,
                CosmeticUnlockEvents.Achievement,
                CosmeticUnlockEvents.FirstSuccessfulBuild));

        Assert.Equal(
            2,
            await db.EntitlementGrants.CountAsync(
                grant =>
                    grant.UserId == user.Id &&
                    grant.EntitlementKey == "bot-look:lancer"));
        Assert.Empty(await db.UserNotifications.ToListAsync());
    }

    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task HundredCompletedRankedMatches_GrantThePrestigePairExactlyOnce()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using var db = await database.CreateMigratedContextAsync();

        string suffix = Guid.NewGuid().ToString("N");
        var user = new User
        {
            DisplayName = "ranked-cosmetic-test",
            Email = $"ranked-cosmetic-{suffix}@example.test",
            PasswordHash = "not-used",
        };
        var bot = new Bot
        {
            OwnerUserId = user.Id,
            Name = "Milestone Bot",
            Slug = $"milestone-{suffix}",
        };
        var opponent = new Bot
        {
            OwnerUserId = user.Id,
            Name = "Milestone Opponent",
            Slug = $"milestone-opponent-{suffix}",
        };
        db.Users.Add(user);
        db.Bots.AddRange(bot, opponent);

        var matches = Enumerable.Range(
                1,
                CosmeticAchievementService.RankedMatchesTarget)
            .Select(_ => new MatchSet
            {
                BotAId = bot.Id,
                BotBId = opponent.Id,
                BotAVersionId = Guid.NewGuid(),
                BotBVersionId = Guid.NewGuid(),
                Status = MatchSetStatus.Completed,
                CompletedAt = DateTime.UtcNow,
            })
            .ToArray();
        db.MatchSets.AddRange(matches.Take(matches.Length - 1));
        await db.SaveChangesAsync();

        CosmeticCatalog catalog = CosmeticCatalog.LoadDefault();
        var entitlements = new CosmeticEntitlementService(db, catalog);
        var achievements = new CosmeticAchievementService(db, entitlements);
        Assert.Equal(
            CosmeticAchievementService.RankedMatchesTarget - 1,
            await achievements.CountRankedMatchesAsync(user.Id));
        Assert.Equal(
            0,
            await achievements.AwardForCompletedRankedSetAsync(
                matches[^2].Id));

        db.MatchSets.Add(matches[^1]);
        await db.SaveChangesAsync();

        Assert.Equal(
            2,
            await achievements.AwardForCompletedRankedSetAsync(
                matches[^1].Id));
        Assert.Equal(
            0,
            await achievements.AwardForCompletedRankedSetAsync(
                matches[^1].Id));
        Assert.True((await entitlements.CheckAccessAsync(
            user.Id,
            CosmeticCatalog.BotLookKind,
            "aureate-warden")).Owned);
        Assert.True((await entitlements.CheckAccessAsync(
            user.Id,
            CosmeticCatalog.ProjectileLookKind,
            "regent-lance")).Owned);
        UserNotification notification =
            Assert.Single(await db.UserNotifications.ToListAsync());
        UserNotificationResponse response =
            UserNotificationContracts.ToResponse(notification);
        Assert.Equal(
            2,
            response.Payload.GetProperty("items").GetArrayLength());
    }
}
