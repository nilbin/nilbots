using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace BotArena.Cli;

/// <summary>
/// Serializes `dotnet build` for one player project across CLI processes.
/// Seed matrices can run matches concurrently, but MSBuild must not have two
/// processes writing the same bin/obj outputs.
/// </summary>
internal sealed class ProjectBuildLock : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);
    private readonly FileStream _stream;

    private ProjectBuildLock(FileStream stream)
    {
        _stream = stream;
    }

    public static ProjectBuildLock Acquire(string projectDirectory, string displayName, bool quiet)
    {
        string identity = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(Path.GetFullPath(projectDirectory))));
        string lockDirectory = Path.Combine(Path.GetTempPath(), "botarena", "in-process-locks");
        Directory.CreateDirectory(lockDirectory);
        string lockPath = Path.Combine(lockDirectory, identity + ".lock");
        var elapsed = Stopwatch.StartNew();
        bool announcedWait = false;

        while (true)
        {
            try
            {
                return new ProjectBuildLock(new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None));
            }
            catch (IOException)
            {
                if (elapsed.Elapsed >= Timeout)
                    throw new InvalidOperationException(
                        $"Timed out waiting for another process to build {displayName}.");
                if (!quiet && !announcedWait)
                {
                    Console.WriteLine(
                        $"Another process is building {displayName} in-process; waiting...");
                    announcedWait = true;
                }
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
            }
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
    }
}
