namespace BotArena.Engine.Tests;

/// <summary>
/// Compatibility shield for the checked-in official replay consumed by the
/// web viewer. Generic actor contracts must never relabel or rewrite it.
/// </summary>
public sealed class OfficialReplayV1CompatibilityTests
{
    private const string ExpectedReplayHash =
        "fa0bf33327556b7d539667eabfc5dc1d7a3fcbf0b66babdd9f82dbc910a460b9";

    [Fact]
    public void Official05Fixture_ReproducesPinnedReplayV1Hash()
    {
        string json = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "web",
            "tests",
            "fixtures",
            "golden-replay.json"));
        ReplayDocument document = ReplaySerializer.FromJson(json);
        var replay = new Replay
        {
            Header = document.Header,
            Ticks = document.Ticks,
            Result = document.Result,
        };

        Assert.Equal(BotArenaVersions.ReplayFormatVersion, document.Header.ReplayVersion);
        Assert.Equal("0.5", document.Header.GameRulesVersion);
        Assert.Equal(ExpectedReplayHash, document.ReplayHash);
        Assert.Equal(ExpectedReplayHash, ReplaySerializer.ComputeHash(replay));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(
                   directory.FullName,
                   "BotArena.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new InvalidOperationException(
                "BotArena.sln not found above the test directory.");
    }
}
