using System.Text.Json;
using BotArena.Cli;

namespace BotArena.Cli.Tests;

public class FrontlineExperimentCommandTests
{
    [Fact]
    public void InProcessRun_WritesDeterministicCompleteReplayV2()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-frontline-cli-{Guid.NewGuid():N}");
        string first = Path.Combine(temporary, "first");
        string second = Path.Combine(temporary, "second");
        try
        {
            Assert.Equal(0, Run(first));
            Assert.Equal(0, Run(second));

            string firstJson = File.ReadAllText(
                Path.Combine(first, "replay.json"));
            string secondJson = File.ReadAllText(
                Path.Combine(second, "replay.json"));
            Assert.Equal(firstJson, secondJson);

            using JsonDocument document = JsonDocument.Parse(firstJson);
            JsonElement root = document.RootElement;
            Assert.False(root.GetProperty("partial").GetBoolean());
            Assert.Equal(
                2,
                root.GetProperty("header")
                    .GetProperty("replayVersion")
                    .GetInt32());
            Assert.Equal(
                "frontline-alpha-1",
                root.GetProperty("header")
                    .GetProperty("gameRulesVersion")
                    .GetString());
            Assert.Equal(
                "frontline-01",
                root.GetProperty("header")
                    .GetProperty("contract")
                    .GetProperty("map")
                    .GetProperty("mapId")
                    .GetString());
            Assert.Equal(
                2,
                root.GetProperty("header")
                    .GetProperty("participants")
                    .GetArrayLength());
            Assert.Equal(
                64,
                root.GetProperty("replayHash").GetString()!.Length);
            Assert.Equal(
                root.GetProperty("result")
                    .GetProperty("endTick")
                    .GetInt32()
                + 1,
                root.GetProperty("ticks").GetArrayLength());
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void UnknownOptionAndBatchOpen_FailBeforeRunning()
    {
        Assert.Throws<InvalidOperationException>(
            () => FrontlineExperimentCommand.Run(
                ["--future-option"]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineExperimentCommand.Run(
                ["--seeds", "1,2", "--open"]));
    }

    private static int Run(string output)
    {
        TextWriter original = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            return FrontlineExperimentCommand.Run(
                [
                    "--bot",
                    "frontline-rusher",
                    "--opponent",
                    "frontline-bastion",
                    "--runtime",
                    "in-process",
                    "--map",
                    MapPath(),
                    "--seed",
                    "42",
                    "--out",
                    output,
                ]);
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private static string MapPath() => Path.Combine(
        FindRepoRoot(),
        "maps",
        "experimental",
        "frontline-01.json");

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
