using System.Text.Json;
using BotArena.App.Shared;
using BotArena.App.Storage;
using BotArena.Toolchain;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Bots;

/// <summary>
/// Registers reigning tournament champions (champions/&lt;slug&gt;/ in the repo,
/// DECISIONS #43) as system-owned bots, so every deployment — and every
/// agent-arena run — has the champion on the ladder. The committed bot.wasm is
/// the artifact (it IS the crowned bot, bit for bit); sources ride along as the
/// viewable SourcesJson. Players challenge it like any other bot.
/// </summary>
public static class ChampionSeeder
{
    private sealed record ChampionManifest(string Name, string EntryType, string? Accent);

    private static readonly JsonSerializerOptions ManifestJson = new(JsonSerializerDefaults.Web);

    public static async Task SeedAsync(
        AppDbContext db,
        IObjectStore objectStore,
        CancellationToken cancellationToken = default)
    {
        string? championsRoot = RepoPaths.FindUpward("champions");
        if (championsRoot is null)
            return;

        var system = await BuiltInBotSeeder.GetOrCreateSystemUser(db, cancellationToken);
        foreach (string dir in Directory.GetDirectories(championsRoot).OrderBy(d => d, StringComparer.Ordinal))
        {
            string manifestPath = Path.Combine(dir, "champion.json");
            string wasmPath = Path.Combine(dir, "bot.wasm");
            if (!File.Exists(manifestPath) || !File.Exists(wasmPath))
                continue;
            var manifest = JsonSerializer.Deserialize<ChampionManifest>(
                await File.ReadAllTextAsync(manifestPath, cancellationToken), ManifestJson)!;

            string slug = Path.GetFileName(dir);
            string artifactHash = BotBuilder.Sha256File(wasmPath);
            string artifactKey = ObjectKeys.Artifact(artifactHash);
            await using (var stream = File.OpenRead(wasmPath))
                await objectStore.PutAsync(artifactKey, stream, artifactHash, cancellationToken);

            var bot = await db.Bots.Include(b => b.Versions)
                .SingleOrDefaultAsync(b => b.Slug == slug, cancellationToken);
            if (bot is null)
            {
                bot = new Bot
                {
                    OwnerUserId = system.Id,
                    Name = manifest.Name,
                    Slug = slug,
                    Accent = manifest.Accent ?? "#f59e0b",
                };
                db.Bots.Add(bot);
            }
            if (!bot.Versions.Any(v => v.ArtifactHash == artifactHash))
            {
                foreach (var old in bot.Versions)
                    old.IsActive = false;
                var sources = Directory.GetFiles(dir, "*.cs")
                    .OrderBy(f => f, StringComparer.Ordinal)
                    .Select(f => new SourceFile(Path.GetFileName(f), File.ReadAllText(f)))
                    .ToArray();
                var version = new BotVersion
                {
                    BotId = bot.Id,
                    VersionNumber = bot.Versions.Count + 1,
                    EntryType = manifest.EntryType,
                    SourcesJson = JsonSerializer.Serialize(sources),
                    SourceHash = "",
                    Status = BuildStatus.Built,
                    ArtifactKey = artifactKey,
                    ArtifactHash = artifactHash,
                    BuiltAt = DateTime.UtcNow,
                    IsActive = true,
                };
                bot.Versions.Add(version);
                // Explicit Add — pre-set Guid keys make graph discovery on a tracked
                // bot save the child as an UPDATE (same trap as BuiltInBotSeeder).
                db.BotVersions.Add(version);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
