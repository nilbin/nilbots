using BotArena.Toolchain;

namespace BotArena.App.Bots;

public sealed record CompiledSubmission(
    string WasmPath,
    string ArtifactHash,
    string CacheKey,
    string BuildLog);

public interface ISubmissionCompiler
{
    Task<CompiledSubmission> CompileAsync(
        Guid requestId,
        IReadOnlyList<SourceFile> sources,
        string entryType,
        string displayName,
        CancellationToken cancellationToken);

    Task CleanupAsync(Guid requestId);
}

