using System.Text.RegularExpressions;
using BotArena.Toolchain;

namespace BotArena.App.Tests;

/// <summary>
/// The CLI version is load-bearing for the release ordering (DECISIONS #85): the server
/// advertises it, `submit` prints it when it refuses, and the deploy guard resolves a
/// `cli-v&lt;version&gt;` tag from it. Two ways it could lie, both pinned here.
/// </summary>
public class PackagedCliVersionTests
{
    [Fact]
    public void ThePackagedVersionMatchesTheVersionTheCliReports()
    {
        string csproj = File.ReadAllText(Path.Combine(
            RepoPaths.ToolchainRoot(), "src", "BotArena.Cli", "BotArena.Cli.csproj"));
        var packaged = Regex.Match(csproj, @"<Version>([^<]+)</Version>");

        Assert.True(packaged.Success, "BotArena.Cli.csproj must declare a <Version>.");
        Assert.Equal(ToolchainInfo.CliVersion, packaged.Groups[1].Value.Trim());
    }

    /// <summary>0.4.0 is published on NuGet.org carrying a pre-0.8 SDK. Shipping a
    /// different toolchain under that same version would leave `dotnet tool update`
    /// with nothing to fetch, which is the exact failure the release guard exists to
    /// prevent — so the version has to move past it.</summary>
    [Fact]
    public void TheCliVersionHasMovedPastTheReleaseThatPredatesTheToolchainGuard()
    {
        Assert.NotEqual("0.4.0", ToolchainInfo.CliVersion);
    }
}
