using System.Text.Json;

namespace BotArena.Toolchain;

public sealed record CompilerQueueRequest(
    Guid RequestId,
    string EntryType,
    string DisplayName,
    List<SourceFile> Sources);

public sealed record CompilerQueueResult(
    Guid RequestId,
    bool Succeeded,
    string? ArtifactHash,
    string? CacheKey,
    string BuildLog,
    string? Error);

/// <summary>
/// A deliberately small filesystem protocol between the networked coordinator
/// and the networkless compiler runner. JSON result publication is the commit
/// marker: an artifact is never consumed before its result file exists.
/// </summary>
public static class CompilerQueue
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string RequestDirectory(string root) => Path.Combine(root, "requests");
    public static string WorkingDirectory(string root) => Path.Combine(root, "working");
    public static string ResultDirectory(string root) => Path.Combine(root, "results");
    public static string RequestPath(string root, Guid id) =>
        Path.Combine(RequestDirectory(root), $"{id:N}.json");
    public static string WorkingPath(string root, Guid id) =>
        Path.Combine(WorkingDirectory(root), $"{id:N}.json");
    public static string ResultPath(string root, Guid id) =>
        Path.Combine(ResultDirectory(root), $"{id:N}.json");
    public static string ArtifactPath(string root, Guid id) =>
        Path.Combine(ResultDirectory(root), $"{id:N}.wasm");

    public static void EnsureDirectories(string root)
    {
        Directory.CreateDirectory(RequestDirectory(root));
        Directory.CreateDirectory(WorkingDirectory(root));
        Directory.CreateDirectory(ResultDirectory(root));
    }

    public static async Task EnqueueAsync(
        string root,
        CompilerQueueRequest request,
        CancellationToken cancellationToken)
    {
        EnsureDirectories(root);
        if (File.Exists(ResultPath(root, request.RequestId)) ||
            File.Exists(WorkingPath(root, request.RequestId)) ||
            File.Exists(RequestPath(root, request.RequestId)))
        {
            return;
        }

        await WriteJsonAtomicAsync(
            RequestPath(root, request.RequestId),
            request,
            overwrite: false,
            cancellationToken);
    }

    public static async Task<T> ReadJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 16 * 1024,
            useAsync: true);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException($"Empty compiler queue document: {path}");
    }

    public static Task WriteResultAsync(
        string root,
        CompilerQueueResult result,
        CancellationToken cancellationToken) =>
        WriteJsonAtomicAsync(
            ResultPath(root, result.RequestId),
            result,
            overwrite: true,
            cancellationToken);

    public static void Cleanup(string root, Guid id)
    {
        DeleteIfExists(RequestPath(root, id));
        DeleteIfExists(WorkingPath(root, id));
        DeleteIfExists(ResultPath(root, id));
        DeleteIfExists(ArtifactPath(root, id));
    }

    private static async Task WriteJsonAtomicAsync<T>(
        string destination,
        T value,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        string temporary = $"{destination}.tmp-{Guid.NewGuid():N}";
        try
        {
            await using (var stream = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporary, destination, overwrite);
        }
        catch (IOException) when (!overwrite && File.Exists(destination))
        {
            // Another coordinator published the same deterministic request.
        }
        finally
        {
            DeleteIfExists(temporary);
        }
    }

    private static void DeleteIfExists(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (FileNotFoundException)
        {
        }
    }
}

