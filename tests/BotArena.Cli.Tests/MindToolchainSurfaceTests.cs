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
    public void TrackedTacticalMindMirrorHasAnInternallyConsistentReceipt()
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
            Sha256(Path.Combine(projectDirectory, "bot.wasm")),
            receipt.ArtifactSha256);
        Assert.Matches("^[0-9a-f]{64}$", receipt.BuildInputSha256);
        Assert.Matches("^[0-9a-f]{64}$", receipt.ArtifactSha256);
        Assert.False(string.IsNullOrWhiteSpace(receipt.SdkVersion));
        Assert.False(string.IsNullOrWhiteSpace(receipt.GuestAdapterVersion));
        Assert.False(string.IsNullOrWhiteSpace(receipt.BuildPipelineVersion));
    }

    [Fact]
    [Trait("Category", "ReleaseWasm")]
    public void ReleaseGate_TacticalMindMirrorMatchesCurrentBuildInputs()
    {
        if (!ReleaseWasmRequired())
            return;

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
        Assert.False(string.IsNullOrWhiteSpace(frozen.SdkVersion));
        Assert.False(string.IsNullOrWhiteSpace(frozen.GuestAdapterVersion));
        Assert.False(string.IsNullOrWhiteSpace(frozen.BuildPipelineVersion));
    }

    [Fact]
    public void FrozenBroadloom3ArtifactRetainsItsOwnControlledBuildReceipt()
    {
        string root = FindRepoRoot();
        string frozenDirectory = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "broadloom-3-stock-v1");
        TrackedBuildReceipt frozen = ReadReceipt(Path.Combine(
            frozenDirectory,
            "bot.build-receipt.json"));

        Assert.Equal(
            frozen.ArtifactSha256,
            Sha256(Path.Combine(frozenDirectory, "bot.wasm")));
        Assert.Equal(
            "2657d3505603635417006a4206671ab190a92e83b4921ec09a3b555ce076b1da",
            frozen.ArtifactSha256);
        Assert.Equal(
            "4a9a7351da0892bf18bc40c9db7e1e6ebf59522fa36090e0554beff0f6d96872",
            frozen.BuildInputSha256);
    }

    [Fact]
    public void BroadloomCurrentAndHistoricalReleaseArtifactsRemainPinned()
    {
        string root = FindRepoRoot();
        string projectDirectory = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-mind-v1");
        string historicalDirectory = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tapline-1-stock-v1");
        string tapline3Directory = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tapline-3-stock-v1");
        string historicalTapline4Directory = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tapline-4-stock-v1");
        string currentReleaseDirectory = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tapline-11-stock-v1");
        string broadloomInitialReleaseDirectory = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "broadloom-1-stock-v1");
        TrackedBuildReceipt current = ReadReceipt(Path.Combine(
            projectDirectory,
            "bot.build-receipt.json"));
        TrackedBuildReceipt historical = ReadReceipt(Path.Combine(
            historicalDirectory,
            "bot.build-receipt.json"));
        TrackedBuildReceipt tapline3 = ReadReceipt(Path.Combine(
            tapline3Directory,
            "bot.build-receipt.json"));
        TrackedBuildReceipt historicalTapline4 = ReadReceipt(Path.Combine(
            historicalTapline4Directory,
            "bot.build-receipt.json"));
        TrackedBuildReceipt currentRelease = ReadReceipt(Path.Combine(
            currentReleaseDirectory,
            "bot.build-receipt.json"));
        TrackedBuildReceipt broadloomInitialRelease = ReadReceipt(Path.Combine(
            broadloomInitialReleaseDirectory,
            "bot.build-receipt.json"));

        Assert.Equal(
            "4f2fbab6bce9565d936a22dd0d80a1533fda0d438835e0a5030496cf2445992d",
            historicalTapline4.ArtifactSha256);
        Assert.Equal(
            "2b1f32c1dc1459923507d38ffe3993802c1c1dfe7c4e44c2bbf945254966e5e4",
            historicalTapline4.BuildInputSha256);
        Assert.Equal(
            historicalTapline4.ArtifactSha256,
            Sha256(Path.Combine(historicalTapline4Directory, "bot.wasm")));
        Assert.Equal("0.10.16", current.SdkVersion);
        Assert.Equal(
            "d9929b5622e99f4db0bb0efa1539e990baea3442f7f1b0ec0b2d297de4667209",
            current.ArtifactSha256);
        Assert.Equal(
            "39bcea043afd862f5cfd7ce4fe27dc7d9eba5bf82b317597d7ebb9a251a679df",
            current.BuildInputSha256);
        Assert.Equal(
            currentRelease.ArtifactSha256,
            Sha256(Path.Combine(currentReleaseDirectory, "bot.wasm")));
        Assert.Equal(
            "457e0e1e391557c18cbf0616d345c7749c7aaf7f7973c8e41bf0e56513184630",
            broadloomInitialRelease.ArtifactSha256);
        Assert.Equal(
            "e5a2f950fa81d05c769afd04b627e341bc0a7b0d3bb877867ed231bcf7899ffb",
            broadloomInitialRelease.BuildInputSha256);
        Assert.Equal(
            broadloomInitialRelease.ArtifactSha256,
            Sha256(Path.Combine(broadloomInitialReleaseDirectory, "bot.wasm")));
        Assert.Equal(
            current.ArtifactSha256,
            Sha256(Path.Combine(projectDirectory, "bot.wasm")));
        Assert.NotEqual(
            broadloomInitialRelease.ArtifactSha256,
            current.ArtifactSha256);
        Assert.NotEqual(currentRelease.ArtifactSha256, current.ArtifactSha256);
        Assert.NotEqual(currentRelease.BuildInputSha256, current.BuildInputSha256);
        Assert.NotEqual(
            historicalTapline4.ArtifactSha256,
            currentRelease.ArtifactSha256);
        Assert.NotEqual(
            historicalTapline4.ArtifactSha256,
            historical.ArtifactSha256);
        Assert.NotEqual(
            historicalTapline4.ArtifactSha256,
            tapline3.ArtifactSha256);
        Assert.Equal(
            tapline3.ArtifactSha256,
            Sha256(Path.Combine(tapline3Directory, "bot.wasm")));
        Assert.Equal(
            historical.ArtifactSha256,
            Sha256(Path.Combine(historicalDirectory, "bot.wasm")));
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

    private static bool ReleaseWasmRequired() =>
        string.Equals(
            Environment.GetEnvironmentVariable(
                "BOTARENA_REQUIRE_CURRENT_TACTICAL_WASM"),
            "true",
            StringComparison.OrdinalIgnoreCase);

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
