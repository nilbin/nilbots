using BotArena.Engine;
using BotArena.Toolchain;

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
        string identity = string.Join(
            "|",
            ToolchainInfo.SdkVersion,
            ToolchainInfo.GuestAdapterVersion);
        Assert.Equal("0.10.11|0.10.11", identity);

        Assert.Equal(
            "generic-mind-match-1",
            BotArenaVersions.GenericMindContractProfileId);
        Assert.Equal(
            "2.0",
            BotArenaVersions.GenericMindRuntimeConfigurationVersion);
    }
}
