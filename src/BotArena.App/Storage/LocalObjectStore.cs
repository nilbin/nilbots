using System.Buffers;
using System.Security.Cryptography;
using BotArena.App.Shared;

namespace BotArena.App.Storage;

/// <summary>
/// Filesystem object-store backend for development and a single VPS. Writes
/// are atomic, keys cannot escape the root, and content-addressed artifacts
/// are verified before use.
/// </summary>
public sealed class LocalObjectStore : IObjectStore
{
    private readonly string root;
    private readonly string rootPrefix;

    public LocalObjectStore()
        : this(DataPaths.Objects)
    {
    }

    public LocalObjectStore(string root)
    {
        this.root = Path.GetFullPath(root);
        rootPrefix = this.root.EndsWith(Path.DirectorySeparatorChar)
            ? this.root
            : this.root + Path.DirectorySeparatorChar;
    }

    public async Task PutAsync(
        string key,
        Stream source,
        string? expectedSha256,
        CancellationToken cancellationToken = default)
    {
        string path = Resolve(key);
        if (expectedSha256 is not null &&
            File.Exists(path) &&
            await HashMatches(path, expectedSha256, cancellationToken))
        {
            return;
        }

        string? directory = Path.GetDirectoryName(path);
        Directory.CreateDirectory(directory!);
        string temporary = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using var output = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(81_920);
            try
            {
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
            await output.FlushAsync(cancellationToken);

            string actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            if (expectedSha256 is not null &&
                !actual.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Object '{key}' hash mismatch: expected {expectedSha256}, got {actual}.");
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public Task<Stream?> OpenReadAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = Resolve(key);
        Stream? stream = File.Exists(path)
            ? new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan)
            : null;
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(Resolve(key)));
    }

    public async Task<string> MaterializeAsync(
        string key,
        string expectedSha256,
        CancellationToken cancellationToken = default)
    {
        string path = Resolve(key);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Object '{key}' does not exist.", path);
        if (!await HashMatches(path, expectedSha256, cancellationToken))
            throw new InvalidDataException($"Object '{key}' does not match its recorded SHA-256 hash.");
        return path;
    }

    public async Task<bool> IsReadyAsync(
        bool requireWrite,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(root))
            {
                if (!requireWrite)
                    return false;
                Directory.CreateDirectory(root);
            }
            if (!requireWrite)
                return true;

            string probe = Path.Combine(root, $".health-{Guid.NewGuid():N}");
            try
            {
                await File.WriteAllTextAsync(probe, "ok", cancellationToken);
            }
            finally
            {
                if (File.Exists(probe))
                    File.Delete(probe);
            }
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private string Resolve(string key)
    {
        ObjectKeys.Validate(key);

        string path = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(rootPrefix, StringComparison.Ordinal))
            throw new ArgumentException($"Object key '{key}' escapes the store root.", nameof(key));
        return path;
    }

    private static async Task<bool> HashMatches(
        string path,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81_920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] actual = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(actual).Equals(expectedSha256, StringComparison.OrdinalIgnoreCase);
    }
}
