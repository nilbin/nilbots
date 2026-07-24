using BotArena.Toolchain;

namespace BotArena.App.Tests;

public class CompilerQueueTests : IDisposable
{
    private readonly string root =
        Path.Combine(Path.GetTempPath(), "botarena-compiler-queue-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RequestAndResult_ArePublishedAtomicallyAndCleaned()
    {
        Guid id = Guid.NewGuid();
        var request = new CompilerQueueRequest(
            id,
            "Example.Bot",
            "example",
            [new SourceFile("Bot.cs", "class Bot {}")]);

        await CompilerQueue.EnqueueAsync(root, request, CancellationToken.None);
        await CompilerQueue.EnqueueAsync(root, request, CancellationToken.None);
        CompilerQueueRequest stored = await CompilerQueue.ReadJsonAsync<CompilerQueueRequest>(
            CompilerQueue.RequestPath(root, id),
            CancellationToken.None);
        Assert.Equal(id, stored.RequestId);

        var result = new CompilerQueueResult(
            id,
            Succeeded: false,
            ArtifactHash: null,
            CacheKey: null,
            BuildLog: "bad source",
            Error: "Build failed.");
        await CompilerQueue.WriteResultAsync(root, result, CancellationToken.None);
        CompilerQueueResult storedResult = await CompilerQueue.ReadJsonAsync<CompilerQueueResult>(
            CompilerQueue.ResultPath(root, id),
            CancellationToken.None);
        Assert.Equal("Build failed.", storedResult.Error);
        Assert.Empty(Directory.EnumerateFiles(root, "*.tmp-*", SearchOption.AllDirectories));

        CompilerQueue.Cleanup(root, id);
        Assert.False(File.Exists(CompilerQueue.RequestPath(root, id)));
        Assert.False(File.Exists(CompilerQueue.ResultPath(root, id)));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
        GC.SuppressFinalize(this);
    }
}

