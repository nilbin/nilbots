using System.Net;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Cosmetics;
using BotArena.App.Shared;
using BotArena.Toolchain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BotArena.App.Tests;

public class BotLifecyclePostgreSqlIntegrationTests
{
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task CreateUpdateAndSubmit_ShareCatalogAndEntitlementPolicy()
    {
        await using var database = await PostgreSqlDatabaseFixture.CreateAsync();
        await using var db = await database.CreateMigratedContextAsync();
        var user = new User
        {
            DisplayName = "lifecycle-test",
            Email = "lifecycle@example.test",
            PasswordHash = "not-used",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        DateTimeOffset now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        CosmeticCatalog catalog = CosmeticCatalog.LoadDefault();
        var entitlements = new CosmeticEntitlementService(db, catalog);
        var appearancePolicy = new BotAppearancePolicy(entitlements);
        var create = new CreateBotUseCase(
            db,
            new BotClassPolicy(),
            appearancePolicy,
            timeProvider,
            NullLogger<CreateBotUseCase>.Instance);
        var actor = new ApplicationActor(
            user.Id,
            IsSystemAccount: false,
            new HashSet<string>(StringComparer.Ordinal));

        ApplicationResult<CreatedBot> lockedCreate = await create.ExecuteAsync(
            actor,
            new CreateBotCommand("Locked Bot", "#ABCDEF", "lancer", "pulse-bolt"));
        Assert.Equal(ApplicationErrorCodes.BotLookLocked, lockedCreate.Error!.Code);
        Assert.Empty(await db.Bots.ToListAsync());

        ApplicationResult<CreatedBot> created = await create.ExecuteAsync(
            actor,
            new CreateBotCommand("Lifecycle Bot", "#ABCDEF", "VANGUARD", "PULSE-BOLT"));
        Assert.True(created.Succeeded);
        Assert.Equal("#abcdef", created.Value!.Accent);
        Bot bot = await db.Bots.SingleAsync();
        Assert.Equal(now.UtcDateTime, bot.CreatedAt);

        await entitlements.GrantForEventAsync(
            user.Id,
            CosmeticUnlockEvents.Achievement,
            CosmeticUnlockEvents.FirstSuccessfulBuild);
        var update = new UpdateBotAppearanceUseCase(
            db,
            appearancePolicy,
            NullLogger<UpdateBotAppearanceUseCase>.Instance);
        ApplicationResult<UpdatedBotAppearance> updated = await update.ExecuteAsync(
            actor,
            new UpdateBotAppearanceCommand(
                bot.Id,
                "#123456",
                "lancer",
                "pulse-bolt"));
        Assert.True(updated.Succeeded);
        Assert.Equal("lancer", bot.LookId);

        var limits = new CompilerSubmissionLimits(6, 30, 12, 60, 2, 20);
        var submissionService = new CompilerSubmissionService(
            db,
            limits,
            new SubmissionNetwork("test-only-network-hmac-key-32-characters"),
            timeProvider);
        var submit = new SubmitBotVersionUseCase(
            db,
            appearancePolicy,
            submissionService,
            NullLogger<SubmitBotVersionUseCase>.Instance);
        SourceFile[] sources =
        [
            new(
                "Bot.cs",
                "using BotArena.Sdk; public sealed class Bot : IBot " +
                "{ public BotAction Tick(BotContext c) => Actions.Wait(); }"),
        ];

        ApplicationResult<SubmittedBotVersion> lockedSubmit = await submit.ExecuteAsync(
            actor,
            new SubmitBotVersionCommand(
                bot.Id,
                "Bot",
                sources,
                null,
                "arc-spark",
                IPAddress.Parse("203.0.113.30")));
        Assert.Equal(ApplicationErrorCodes.ProjectileLookLocked, lockedSubmit.Error!.Code);
        Assert.Empty(await db.BotVersions.ToListAsync());

        await entitlements.GrantForEventAsync(
            user.Id,
            CosmeticUnlockEvents.Challenge,
            CosmeticUnlockEvents.FirstUnrankedMatch);
        ApplicationResult<SubmittedBotVersion> submitted = await submit.ExecuteAsync(
            actor,
            new SubmitBotVersionCommand(
                bot.Id,
                "Bot",
                sources,
                null,
                "arc-spark",
                IPAddress.Parse("203.0.113.30")));

        Assert.True(submitted.Succeeded);
        Assert.Equal("arc-spark", bot.ProjectileLookId);
        Assert.Equal(1, await db.BotVersions.CountAsync());
        Assert.Equal(1, await db.BackgroundJobs.CountAsync());
        Assert.Equal(now.UtcDateTime, (await db.BotVersions.SingleAsync()).CreatedAt);
    }
}
