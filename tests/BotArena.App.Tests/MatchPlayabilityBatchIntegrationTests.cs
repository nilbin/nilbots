using System.Data.Common;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Cosmetics;
using BotArena.App.Matches;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BotArena.App.Tests;

public sealed class MatchPlayabilityBatchIntegrationTests
{
    [SkippableFact]
    [Trait("Category", PostgreSqlDatabaseFixture.Category)]
    public async Task ProjectionUsesBoundedQueriesAsRosterGrows()
    {
        await using PostgreSqlDatabaseFixture database =
            await PostgreSqlDatabaseFixture.CreateAsync();
        await using (AppDbContext migration =
                     await database.CreateMigratedContextAsync())
        {
            Assert.Empty(migration.ChangeTracker.Entries());
        }

        Guid accountId = Guid.NewGuid();
        await using (AppDbContext seed = database.CreateContext())
        {
            var owner = new User
            {
                Id = accountId,
                DisplayName = "Batch Owner",
                Email = "batch-owner@example.test",
                PasswordHash = "not-used",
            };
            seed.Users.Add(owner);
            for (int index = 0; index < 40; index++)
            {
                var bot = new Bot
                {
                    OwnerUserId = accountId,
                    Name = $"Batch Bot {index}",
                    Slug = $"batch-bot-{index}",
                    LookId = "helio-kite",
                };
                seed.Bots.Add(bot);
                seed.BotVersions.Add(new BotVersion
                {
                    BotId = bot.Id,
                    VersionNumber = 1,
                    EntryType = "Bot",
                    SourcesJson = "[]",
                    SourceHash = $"source-{index}",
                    Status = BuildStatus.Built,
                    ArtifactHash = $"artifact-{index}",
                    SupportedContractProfiles =
                        [BotContractProfiles.LegacyDuel],
                    IsActive = true,
                });
            }
            await seed.SaveChangesAsync();
        }

        var commands = new CountingCommandInterceptor();
        await using AppDbContext db =
            database.CreateContext(commands);
        var entitlements = new CosmeticEntitlementService(
            db,
            CosmeticCatalog.LoadDefault());
        var appearances =
            new BotAppearancePolicy(entitlements);
        var admission =
            new MatchAdmissionService(db, appearances);
        var playability =
            new MatchPlayabilityService(db, admission);

        IReadOnlyList<MatchPlayabilityResponse> projected =
            await playability.ProjectAsync(accountId);

        Assert.Equal(40, projected.Count);
        Assert.All(
            projected,
            row => Assert.Equal(
                ApplicationErrorCodes.BotLookLocked,
                row.RefusalCode));
        Assert.InRange(commands.Executed, 1, 6);
    }

    private sealed class CountingCommandInterceptor :
        DbCommandInterceptor
    {
        private int executed;

        public int Executed => Volatile.Read(ref executed);

        public override ValueTask<InterceptionResult<DbDataReader>>
            ReaderExecutingAsync(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<DbDataReader> result,
                CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref executed);
            return base.ReaderExecutingAsync(
                command,
                eventData,
                result,
                cancellationToken);
        }
    }
}
