using BotArena.Toolchain;

namespace BotArena.App.Bots;

/// <summary>Local-development compiler. Public production uses the queue-backed,
/// networkless runner instead.</summary>
public sealed class DirectSubmissionCompiler : ISubmissionCompiler
{
    public Task<CompiledSubmission> CompileAsync(
        Guid requestId,
        IReadOnlyList<SourceFile> sources,
        string entryType,
        string displayName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BuiltBot built = BotBuilder.BuildFromSources(sources, entryType, displayName, quiet: true);
        string logPath = Path.Combine(ToolchainInfo.CacheRoot, built.CacheKey[..24], "build.log");
        string log = File.Exists(logPath) ? File.ReadAllText(logPath) : "(cached build)";
        return Task.FromResult(new CompiledSubmission(
            built.WasmPath,
            built.ArtifactHash,
            built.CacheKey,
            log));
    }

    public Task CleanupAsync(Guid requestId) => Task.CompletedTask;
}

