using System.Security.Cryptography;
using BotArena.Toolchain;

namespace BotArena.App.Tests;

/// <summary>
/// BotArena.Sdk and BotArena.Guest are compiled into every player artifact, so a bot's
/// bytes are only reproducible if every host stages the same two assemblies. They did
/// not: the CLI shipped them beside itself while the server had only Sdk.dll and quietly
/// built its own pair from the repo (DECISIONS #84). These tests pin the shape of the
/// fix rather than the symptom, because the symptom — a submission whose local and
/// server hashes disagree — is only observable with a running server.
/// </summary>
public class StagedToolchainAssemblyTests
{
    private static readonly string[] StagedAssemblies = ["BotArena.Sdk.dll", "BotArena.Guest.dll"];

    [Fact]
    public void EveryHostThatCompilesBotsShipsTheWholeStagedClosure()
    {
        foreach (string host in new[] { "BotArena.Cli", "BotArena.App" })
        {
            string directory = HostOutputDirectory(host);
            foreach (string assembly in StagedAssemblies)
                Assert.True(
                    File.Exists(Path.Combine(directory, assembly)),
                    $"{host} must ship {assembly} beside itself; without it the host falls " +
                    "back to building its own copy, which is a different build than the " +
                    "other host stages.");
        }
    }

    [Fact]
    public void TheStagedAssembliesAreByteIdenticalAcrossHosts()
    {
        foreach (string assembly in StagedAssemblies)
        {
            var hashes = new[] { "BotArena.Cli", "BotArena.App" }
                .Select(host => Sha256(Path.Combine(HostOutputDirectory(host), assembly)))
                .Distinct()
                .ToList();
            Assert.True(
                hashes.Count == 1,
                $"{assembly} differs between the CLI and the server. Identical bot sources " +
                "cannot compile to identical artifacts while this is true.");
        }
    }

    /// <summary>The staged assemblies are part of the artifact, so they must be part of
    /// its cache key — otherwise an SDK edit is served from cache under the old bytes.</summary>
    [Fact]
    public void TheCacheKeyCoversTheStagedAssemblies()
    {
        SourceFile[] sources = [new("Bot.cs", "class Bot {}")];
        string key = BotBuilder.ComputeCacheKey(sources, "Bot");
        string identity = BotBuilder.StagedAssemblyIdentity();

        Assert.Equal(StagedAssemblies.Length, identity.Split('|').Length);
        Assert.Equal(key, BotBuilder.ComputeCacheKey(sources, "Bot"));
        Assert.DoesNotContain(identity.Split('|'), part => part.Length != 64);
    }

    /// <summary>Mirrors the running test host's configuration so a stale bin/ directory
    /// from some other configuration cannot fail an unrelated run.</summary>
    private static string HostOutputDirectory(string project)
    {
        string testBin = AppContext.BaseDirectory;
        string configuration = testBin.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}")
            ? "Release"
            : "Debug";
        string repoRoot = RepoPaths.ToolchainRoot();
        return Path.Combine(repoRoot, "src", project, "bin", configuration, "net10.0");
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
