using BotArena.App.Accounts;
using BotArena.App.Bots;
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

        await db.Database.MigrateAsync(cancellationToken);
        await LegacyObjectImporter.ImportAsync(db, objectStore, cancellationToken);
        await BuiltInBotSeeder.SeedAsync(db, objectStore, cancellationToken);
        await ChampionSeeder.SeedAsync(db, objectStore, cancellationToken);
        await OpenIddictSetup.SeedClientAsync(scope.ServiceProvider);
    }
}
