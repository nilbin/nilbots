using BotArena.App.Bots;
using BotArena.Runtime.Wasm;
using BotArena.Toolchain;
using Microsoft.Extensions.Configuration;

namespace BotArena.App.Tests;

public class CompilerRunnerIntegrationTests
{
    [SkippableFact]
    public async Task NetworklessContainer_CompilesThroughBoundedQueue()
    {
        string? root = Environment.GetEnvironmentVariable("BOTARENA_COMPILER_SMOKE_ROOT");
        Skip.If(string.IsNullOrWhiteSpace(root),
            "Set BOTARENA_COMPILER_SMOKE_ROOT while a compiler-runner container shares that directory.");

        var configuration = new ConfigurationManager
        {
            ["BOTARENA_COMPILER_IPC"] = root,
        };
        var compiler = new QueuedSubmissionCompiler(configuration);
        Guid id = Guid.NewGuid();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(6));
        try
        {
            CompiledSubmission built = await compiler.CompileAsync(
                id,
                [
                    new SourceFile(
                        "SmokeBot.cs",
                        """
                        using BotArena.Sdk;
                        public sealed class SmokeBot : IBot
                        {
                            public BotAction Tick(BotContext context) => Actions.Wait();
                        }
                        """),
                ],
                "SmokeBot",
                "compiler sandbox smoke test",
                timeout.Token);

            Assert.Equal(built.ArtifactHash, BotBuilder.Sha256File(built.WasmPath));
            _ = WasmArtifactValidator.Validate(built.WasmPath);
        }
        finally
        {
            await compiler.CleanupAsync(id);
        }
    }
}

