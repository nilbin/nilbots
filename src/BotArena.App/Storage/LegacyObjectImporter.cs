using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Storage;

/// <summary>
/// Preserves pre-object-store pilot data during the path-to-key migration.
/// Legacy files are copied, never deleted.
/// </summary>
public static class LegacyObjectImporter
{
    public static async Task ImportAsync(
        AppDbContext db,
        IObjectStore objectStore,
        CancellationToken cancellationToken = default)
    {
        var versions = await db.BotVersions
            .Where(v => v.ArtifactKey != null && v.ArtifactHash != null)
            .Select(v => new { v.ArtifactKey, v.ArtifactHash })
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var version in versions)
        {
            if (await objectStore.ExistsAsync(version.ArtifactKey!, cancellationToken))
                continue;
            string legacy = Path.Combine(DataPaths.Root, "artifacts", version.ArtifactHash! + ".wasm");
            if (!File.Exists(legacy))
                continue;
            await using var stream = File.OpenRead(legacy);
            await objectStore.PutAsync(
                version.ArtifactKey!,
                stream,
                version.ArtifactHash,
                cancellationToken);
        }

        var matches = await db.Matches
            .Where(m => m.ReplayKey != null)
            .Select(m => new { m.Id, m.ReplayKey })
            .ToListAsync(cancellationToken);
        foreach (var match in matches)
        {
            if (await objectStore.ExistsAsync(match.ReplayKey!, cancellationToken))
                continue;
            string legacy = Path.Combine(DataPaths.Root, "replays", match.Id + ".json");
            if (!File.Exists(legacy))
                continue;
            await using var stream = File.OpenRead(legacy);
            await objectStore.PutAsync(
                match.ReplayKey!,
                stream,
                expectedSha256: null,
                cancellationToken: cancellationToken);
        }
    }
}
