using BotArena.Toolchain;

namespace BotArena.App.Bots;

public sealed class QueuedSubmissionCompiler(IConfiguration configuration) : ISubmissionCompiler
{
    private readonly string root = configuration["BOTARENA_COMPILER_IPC"]
        ?? throw new InvalidOperationException(
            "BOTARENA_COMPILER_IPC is required for the production compile coordinator.");

    public async Task<CompiledSubmission> CompileAsync(
        Guid requestId,
        IReadOnlyList<SourceFile> sources,
        string entryType,
        string displayName,
        CancellationToken cancellationToken)
    {
        await CompilerQueue.EnqueueAsync(
            root,
            new CompilerQueueRequest(requestId, entryType, displayName, [.. sources]),
            cancellationToken);

        string resultPath = CompilerQueue.ResultPath(root, requestId);
        while (!File.Exists(resultPath))
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);

        CompilerQueueResult result =
            await CompilerQueue.ReadJsonAsync<CompilerQueueResult>(resultPath, cancellationToken);
        if (result.RequestId != requestId)
            throw new InvalidDataException("Compiler result request ID did not match.");
        if (!result.Succeeded)
            throw new BotBuildException(
                result.Error ?? "The isolated compiler failed.",
                result.BuildLog);
        if (result.ArtifactHash is null || result.CacheKey is null)
            throw new InvalidDataException("Successful compiler result omitted its artifact identity.");

        string artifactPath = CompilerQueue.ArtifactPath(root, requestId);
        var artifact = new FileInfo(artifactPath);
        if (!artifact.Exists)
            throw new FileNotFoundException("Compiler result artifact is missing.", artifactPath);
        if (artifact.Length > BotBuilder.MaxArtifactBytes)
            throw new InvalidDataException("Compiler result artifact exceeds its size limit.");
        string actualHash = BotBuilder.Sha256File(artifactPath);
        if (!string.Equals(actualHash, result.ArtifactHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Compiler result artifact hash did not match.");

        return new CompiledSubmission(
            artifactPath,
            actualHash,
            result.CacheKey,
            result.BuildLog);
    }

    public Task CleanupAsync(Guid requestId)
    {
        CompilerQueue.Cleanup(root, requestId);
        return Task.CompletedTask;
    }
}

