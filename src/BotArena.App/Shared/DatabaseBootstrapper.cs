using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Competition;
using BotArena.App.Matches;
using BotArena.App.Storage;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Shared;

public static class DatabaseBootstrapper
{
    public static async Task RunAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var objectStore = scope.ServiceProvider.GetRequiredService<IObjectStore>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(DatabaseBootstrapper));

        await db.Database.MigrateAsync(cancellationToken);
        var matchSettings =
            scope.ServiceProvider.GetRequiredService<MatchExecutionSettings>();
        var identityBackfiller =
            scope.ServiceProvider
                .GetRequiredService<LegacyCompetitionIdentityBackfiller>();
        await identityBackfiller.RunAsync(
            matchSettings.MatchRules.RulesVersion,
            cancellationToken);
        logger.LogInformation(
            "Verified legacy competition identities for current rules {RulesVersion}",
            matchSettings.MatchRules.RulesVersion);
        if (configuration["BOTARENA_OBJECT_MIGRATION_SOURCE"] is { Length: > 0 } source)
        {
            int count = await ObjectStoreMigrator.MigrateAsync(
                db,
                objectStore,
                source,
                cancellationToken);
            logger.LogInformation(
                "Verified or migrated {ObjectCount} database-referenced objects from {Source}",
                count,
                source);
        }
        await LegacyObjectImporter.ImportAsync(db, objectStore, cancellationToken);
        await BuiltInBotSeeder.SeedAsync(db, objectStore, cancellationToken);
        await ChampionSeeder.SeedAsync(db, objectStore, cancellationToken);
        await OpenIddictSetup.SeedClientAsync(scope.ServiceProvider);
    }
}
