using System.Security.Cryptography;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Storage;

public static class ObjectStoreMigrator
{
    public static async Task<int> MigrateAsync(
        AppDbContext db,
        IObjectStore destination,
        string sourceRoot,
        CancellationToken cancellationToken = default)
    {
        var objects = new Dictionary<string, string?>(StringComparer.Ordinal);

        var versions = await db.BotVersions
            .Where(version => version.ArtifactKey != null && version.ArtifactHash != null)
            .Select(version => new { version.ArtifactKey, version.ArtifactHash })
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var version in versions)
            Add(objects, version.ArtifactKey!, version.ArtifactHash);

        var replayKeys = await db.Matches
            .Where(match => match.ReplayKey != null)
            .Select(match => match.ReplayKey!)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (string replayKey in replayKeys)
            Add(objects, replayKey, expectedSha256: null);

        var source = new LocalObjectStore(sourceRoot);
        int migrated = 0;
        foreach ((string key, string? recordedHash) in objects)
        {
            if (!await source.ExistsAsync(key, cancellationToken))
            {
                if (!await destination.ExistsAsync(key, cancellationToken))
                {
                    throw new FileNotFoundException(
                        $"Object '{key}' is referenced by PostgreSQL but exists in neither " +
                        "the migration source nor the destination.");
                }
                continue;
            }

            string expectedHash = recordedHash ?? await HashAsync(source, key, cancellationToken);
            await using Stream input = (await source.OpenReadAsync(key, cancellationToken))!;
            await destination.PutAsync(key, input, expectedHash, cancellationToken);
            await destination.MaterializeAsync(key, expectedHash, cancellationToken);
            migrated++;
        }

        return migrated;
    }

    private static void Add(
        IDictionary<string, string?> objects,
        string key,
        string? expectedSha256)
    {
        if (objects.TryGetValue(key, out string? existing) &&
            existing is not null &&
            expectedSha256 is not null &&
            !existing.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Object key '{key}' is associated with conflicting hashes.");
        }
        objects[key] = existing ?? expectedSha256;
    }

    private static async Task<string> HashAsync(
        IObjectStore source,
        string key,
        CancellationToken cancellationToken)
    {
        await using Stream stream = (await source.OpenReadAsync(key, cancellationToken))!;
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
