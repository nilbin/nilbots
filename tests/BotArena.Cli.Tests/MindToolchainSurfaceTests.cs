using BotArena.Engine;
using BotArena.Toolchain;
using System.Security.Cryptography;
using System.Text.Json;

namespace BotArena.Cli.Tests;

/// <summary>
/// The controlled build path's mind surface. There is deliberately almost
/// nothing here, and that is the finding: the generated entry point is
/// <c>GuestHost.RunDetected(() =&gt; new TheirType())</c>, which selects the
/// programming model from the TYPE. A mind source tree therefore compiles
/// through the existing pipeline with no manifest field, no toolchain branch
/// and no second build path — the same property that lets an unmodified
/// per-life bot reach the mind profile.
/// </summary>
public sealed class MindToolchainSurfaceTests
{
    private static readonly SourceFile[] MindSources =
    [
        new(
            "Mind.cs",
            """
            using BotArena.Sdk;

            public sealed class MyMind : IGenericMindBot
            {
                public void Think(MindContext mind)
                {
                    foreach (MindBody body in mind.Bodies)
                        body.Hold("reserve");
                }
            }
            """),
    ];

    [Fact]
    public void AMindEntryTypeIsAcceptedByTheControlledBuildPath()
    {
        // No interface check anywhere in the submission validator: the entry
        // type is a name, and what it implements is decided at run time by the
        // guest's static analysis.
        BotBuilder.ValidateSubmission(MindSources, "MyMind");

        string key = BotBuilder.ComputeCacheKey(MindSources, "MyMind");
        Assert.Equal(64, key.Length);
        Assert.NotEqual(
            key,
            BotBuilder.ComputeCacheKey(MindSources, "SomeOtherMind"));
    }

    [Fact]
    public void TheArtifactIdentityCoversTheMindContractToo()
    {
        // Every artifact built from this SDK attests both profiles, so the
        // mind's protocol, configuration and schemas belong in the cache key —
        // otherwise a mind-side contract change would keep serving artifacts
        // that promise the old one.
        //
        // The two versions move INDEPENDENTLY and that is the point: #192's
        // wrapper fix moved the guest adapter alone (0.10.11 -> 0.10.12) with
        // the SDK unmoved. This assertion used to hard-code both literals and
        // broke on exactly that legitimate bump, so what it pins now is the
        // rule rather than the numbers — each version is real, and each is
        // documented where a frozen artifact's capabilities are read back.
        string toolchain = ReadToolchainSource();
        foreach (string version in new[]
                 {
                     ToolchainInfo.SdkVersion,
                     ToolchainInfo.GuestAdapterVersion,
                 })
        {
            Assert.Matches(@"^\d+\.\d+\.\d+$", version);
            Assert.Contains(
                $"// {version}:",
                toolchain,
                StringComparison.Ordinal);
        }

        Assert.Equal(
            "generic-mind-match-1",
            BotArenaVersions.GenericMindContractProfileId);
        Assert.Equal(
            "2.0",
            BotArenaVersions.GenericMindRuntimeConfigurationVersion);
    }

    [Fact]
    public void TrackedTacticalMindArtifactMatchesItsCurrentBuildInputs()
    {
        string root = FindRepoRoot();
        string projectDirectory = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-mind-v1");
        TrackedBuildReceipt receipt = ReadReceipt(Path.Combine(
            projectDirectory,
            "bot.build-receipt.json"));

        Assert.Equal(
            BotProject.Load(projectDirectory).ComputeCacheKey(),
            receipt.BuildInputSha256);
        Assert.Equal(
            Sha256(Path.Combine(projectDirectory, "bot.wasm")),
            receipt.ArtifactSha256);
        Assert.Equal(ToolchainInfo.SdkVersion, receipt.SdkVersion);
        Assert.Equal(
            ToolchainInfo.GuestAdapterVersion,
            receipt.GuestAdapterVersion);
        Assert.Equal(
            ToolchainInfo.BuildPipelineVersion,
            receipt.BuildPipelineVersion);
    }

    [Fact]
    public void FrozenAmbushV8ArtifactRetainsItsOwnControlledBuildReceipt()
    {
        string root = FindRepoRoot();
        string frozenDirectory = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "ambush-warren-11-stock-v2");
        TrackedBuildReceipt frozen = ReadReceipt(Path.Combine(
            frozenDirectory,
            "bot.build-receipt.json"));

        Assert.Equal(
            frozen.ArtifactSha256,
            Sha256(Path.Combine(frozenDirectory, "bot.wasm")));
        Assert.Equal(ToolchainInfo.SdkVersion, frozen.SdkVersion);
        Assert.Equal(
            ToolchainInfo.GuestAdapterVersion,
            frozen.GuestAdapterVersion);
        Assert.Equal(
            ToolchainInfo.BuildPipelineVersion,
            frozen.BuildPipelineVersion);
    }

    [Fact]
    public void FrozenAmbushV10ArtifactMatchesTheCurrentSemanticIntentBuild()
    {
        string root = FindRepoRoot();
        string projectDirectory = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-mind-v1");
        string frozenDirectory = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "ambush-warren-11-stock-v4");
        TrackedBuildReceipt current = ReadReceipt(Path.Combine(
            projectDirectory,
            "bot.build-receipt.json"));
        TrackedBuildReceipt frozen = ReadReceipt(Path.Combine(
            frozenDirectory,
            "bot.build-receipt.json"));

        Assert.Equal(current, frozen);
        Assert.Equal(
            Sha256(Path.Combine(projectDirectory, "bot.wasm")),
            Sha256(Path.Combine(frozenDirectory, "bot.wasm")));
    }

    /// <summary>The version notes live beside the constants they explain.</summary>
    private static string ReadToolchainSource()
    {
        string directory = FindRepoRoot();
        return File.ReadAllText(
            Path.Combine(
                directory,
                "src",
                "BotArena.Toolchain",
                "BotProject.cs"));
    }

    private static string FindRepoRoot()
    {
        string directory = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(directory, ".git"))
               && !File.Exists(Path.Combine(directory, "BotArena.sln")))
        {
            string? parent = Path.GetDirectoryName(
                directory.TrimEnd(Path.DirectorySeparatorChar));
            Assert.NotNull(parent);
            directory = parent!;
        }
        return directory;
    }

    private static TrackedBuildReceipt ReadReceipt(string path) =>
        JsonSerializer.Deserialize<TrackedBuildReceipt>(
            File.ReadAllText(path),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }) ?? throw new InvalidDataException($"Empty build receipt: {path}");

    private static string Sha256(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private sealed record TrackedBuildReceipt(
        int SchemaVersion,
        string BuildInputSha256,
        string ArtifactSha256,
        string SdkVersion,
        string GuestAdapterVersion,
        string CompilerVersion,
        string BuildPipelineVersion);
}
