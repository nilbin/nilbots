using System.Diagnostics;

namespace BotArena.Toolchain;

/// <summary>
/// Serializes builds of one content-cache key across processes. The in-memory
/// lock in <see cref="BotBuilder"/> cannot protect a workspace shared by a
/// local CLI and the server's compile worker.
/// </summary>
internal sealed class BuildCacheLock : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);
    private readonly FileStream _stream;

    private BuildCacheLock(FileStream stream)
    {
        _stream = stream;
    }

    public static BuildCacheLock Acquire(string cacheDirectory, string displayName, bool quiet)
    {
        Directory.CreateDirectory(cacheDirectory);
        string lockPath = Path.Combine(cacheDirectory, "build.lock");
        var elapsed = Stopwatch.StartNew();
        bool announcedWait = false;

        while (true)
        {
            try
            {
                return new BuildCacheLock(new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None));
            }
            catch (IOException)
            {
                if (elapsed.Elapsed >= Timeout)
                    throw new BotBuildException(
                        $"Timed out waiting for another process to finish compiling {displayName}.",
                        "");
                if (!quiet && !announcedWait)
                {
                    Console.WriteLine(
                        $"Another process is compiling {displayName}; waiting for its cached artifact...");
                    announcedWait = true;
                }
                Thread.Sleep(RetryDelay);
            }
            catch (UnauthorizedAccessException)
            {
                if (elapsed.Elapsed >= Timeout)
                    throw new BotBuildException(
                        $"Timed out waiting for the build cache for {displayName}.",
                        "");
                // A just-created cache directory can briefly retain the permissions
                // of the isolated build user while ownership is being normalized.
                Thread.Sleep(RetryDelay);
            }
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
    }
}
