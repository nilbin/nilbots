using BotArena.App.Accounts;
using BotArena.App.Shared;
using BotArena.App.Storage;
using BotArena.Bots.BuiltIn;
using BotArena.Toolchain;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Bots;

/// <summary>
/// Registers the built-in opponents (plan §26) as system-owned bots so players always
/// have someone to challenge. Their versions point at the shared catalog artifact with a
/// guest-side bot name selector.
/// </summary>
public static class BuiltInBotSeeder
{
    public static async Task SeedAsync(
        AppDbContext db,
        IObjectStore objectStore,
        CancellationToken cancellationToken = default)
    {
        string? artifact = RepoPaths.FindUpward(Path.Combine("artifacts", "wasm", "builtin-bots.wasm"));
        if (artifact is null)
            return; // Nothing to seed against; doctor/scripts surface this.

        var system = await GetOrCreateSystemUser(db, cancellationToken);

        string artifactHash = BotBuilder.Sha256File(artifact);
        string artifactKey = ObjectKeys.Artifact(artifactHash);
        await using (var stream = File.OpenRead(artifact))
            await objectStore.PutAsync(artifactKey, stream, artifactHash, cancellationToken);

        foreach (string name in BuiltInBotCatalog.Names)
        {
            var bot = await db.Bots.Include(b => b.Versions)
                .SingleOrDefaultAsync(b => b.Slug == name, cancellationToken);
            if (bot is null)
            {
                bot = new Bot
                {
                    OwnerUserId = system.Id,
                    Name = name,
                    Slug = name,
                    Accent = BuiltInBotCatalog.Accent(name),
                };
                db.Bots.Add(bot);
            }
            // One active version per catalog artifact hash: reseeded when the artifact changes.
            if (!bot.Versions.Any(v => v.ArtifactHash == artifactHash))
            {
                foreach (var old in bot.Versions)
                    old.IsActive = false;
                var version = new BotVersion
                {
                    BotId = bot.Id,
                    VersionNumber = bot.Versions.Count + 1,
                    EntryType = name,
                    SourcesJson = "[]",
                    SourceHash = "",
                    Status = BuildStatus.Built,
                    ArtifactKey = artifactKey,
                    ArtifactHash = artifactHash,
                    GuestBotName = name,
                    BuiltAt = DateTime.UtcNow,
                    IsActive = true,
                };
                bot.Versions.Add(version);
                // Explicit Add: the pre-set Guid key means graph discovery on a tracked
                // existing bot would classify this as Modified (an UPDATE of a row that
                // doesn't exist) — the artifact-rotation path crashed on exactly that.
                db.BotVersions.Add(version);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    internal static async Task<User> GetOrCreateSystemUser(
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        var system = await db.Users.SingleOrDefaultAsync(u => u.IsSystem, cancellationToken);
        if (system is null)
        {
            system = new User
            {
                DisplayName = "Bot Arena",
                Email = "system@botarena.local",
                PasswordHash = "!locked",
                IsSystem = true,
            };
            db.Users.Add(system);
        }
        return system;
    }
}
