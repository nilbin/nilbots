using System.Text.Json;
using BotArena.Toolchain;

namespace BotArena.Cli.Tests;

/// <summary>
/// The declared <c>sdkVersion</c> advisory (wave-8 friction).
///
/// <para>The field decides nothing — the build cache key carries the PINNED SDK
/// and the staged Sdk/Guest DLL bytes (DECISIONS #84) — so a project that
/// declares a version it was never built against still produces the right
/// artifact. That is why the whole campaign shipped with mismatched
/// declarations and nothing complained. `nilbots build` now says so once, and
/// deliberately keeps building: refusing would break every frozen
/// <c>arena-bots/</c> tree the moment the SDK moves, and rebuilding a frozen
/// source tree is a thing the campaign does on purpose.</para>
/// </summary>
[Collection("Console")]
public sealed class SdkVersionAdvisoryTests
{
    [Fact]
    public void AnAgreeingDeclarationSaysNothing()
    {
        Assert.Null(SdkVersionAdvisory.Describe(ToolchainInfo.SdkVersion));
        Assert.Null(SdkVersionAdvisory.Describe(Manifest(
            ToolchainInfo.SdkVersion)));
    }

    [Fact]
    public void AStaleDeclarationNamesBothVersionsAndTheOneLineFix()
    {
        string advisory = Assert.IsType<string>(
            SdkVersionAdvisory.Describe(Manifest("0.10.6")));
        Assert.Contains("0.10.6", advisory, StringComparison.Ordinal);
        Assert.Contains(
            ToolchainInfo.SdkVersion,
            advisory,
            StringComparison.Ordinal);
        // The suggestion, literally pasteable.
        Assert.Contains(
            $"\"sdkVersion\": \"{ToolchainInfo.SdkVersion}\"",
            advisory,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheWarningGoesToStderrAndNeverFailsTheBuild()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        TextWriter previousOut = Console.Out;
        TextWriter previousError = Console.Error;
        bool warned;
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            warned = BuildCommand.WarnOnStaleSdkVersion(Manifest("0.10.6"));
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
        }

        Assert.True(warned);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.StartsWith(
            "warning: ",
            stderr.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AFreshlyScaffoldedProjectNeverWarns()
    {
        // The scaffold substitutes the live SDK version, so the advisory can
        // only ever fire on a tree that has outlived a bump — which is the
        // case it exists for.
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-sdk-advisory-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(temporary);
            string previous = Directory.GetCurrentDirectory();
            TextWriter stdout = Console.Out;
            Console.SetOut(TextWriter.Null);
            try
            {
                Directory.SetCurrentDirectory(temporary);
                Assert.Equal(
                    0,
                    NewCommand.Run("Fresh", ["--profile", "generic-mind"]));
            }
            finally
            {
                Directory.SetCurrentDirectory(previous);
                Console.SetOut(stdout);
            }

            using JsonDocument manifest = JsonDocument.Parse(
                File.ReadAllText(
                    Path.Combine(temporary, "Fresh", "botarena.json")));
            Assert.Equal(
                ToolchainInfo.SdkVersion,
                manifest.RootElement.GetProperty("sdkVersion").GetString());
            Assert.Null(
                SdkVersionAdvisory.Describe(
                    BotProject.Load(Path.Combine(temporary, "Fresh"))
                        .Manifest));
        }
        finally
        {
            try
            {
                if (Directory.Exists(temporary))
                    Directory.Delete(temporary, recursive: true);
            }
            catch (IOException)
            {
                // Disposable either way.
            }
        }
    }

    private static BotManifest Manifest(string sdkVersion) =>
        new()
        {
            Name = "Declarer",
            EntryType = "Declarer",
            SdkVersion = sdkVersion,
        };
}
