using System.Text.Json;

namespace BotArena.Cli.Tests;

[Collection("Console")]
public sealed class ArcRelayExperimentCommandTests
{
    [Theory]
    [InlineData("stock-mind-v0/sheet.json")]
    [InlineData("dynamic-strategy-v4-2026-08-02/sheets/rear-ambush-dynamic.json")]
    public void EvaluationSheetVersionsLinkDeterministicallyIntoTheContract(
        string relativeSheet)
    {
        string sheet = Path.Combine(
            FindRepoRoot(),
            "arena-bots",
            "arc-relay",
            relativeSheet);
        TextWriter stdout = Console.Out;
        TextWriter stderr = Console.Error;
        using var output = new StringWriter();
        Console.SetOut(output);
        Console.SetError(TextWriter.Null);
        try
        {
            Assert.Equal(
                0,
                ArcRelayExperimentCommand.Run(
                [
                    "--sheet0", sheet,
                    "--sheet1", sheet,
                    "--loop-profile", "depth-counterflow",
                    "--print-contract",
                ]));
        }
        finally
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
        }

        using JsonDocument document = JsonDocument.Parse(output.ToString());
        Assert.Equal(
            "arc-relay-threefold-depth-counterflow-01",
            document.RootElement.GetProperty("map").GetProperty("mapId")
                .GetString());
    }

    [Fact]
    public void UnknownEvaluationSheetVersionsAreRefused()
    {
        string source = Path.Combine(
            FindRepoRoot(),
            "arena-bots",
            "arc-relay",
            "dynamic-strategy-v4-2026-08-02",
            "sheets",
            "rear-ambush-dynamic.json");
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-strategy-sheet-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(
                temporary,
                File.ReadAllText(source).Replace(
                    "arc-relay-evaluation-sheet-v1",
                    "arc-relay-evaluation-sheet-v999",
                    StringComparison.Ordinal));
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayExperimentCommand.Run(
                [
                    "--sheet0", temporary,
                    "--sheet1", temporary,
                    "--print-contract",
                ]));
            Assert.Contains("Unsupported evaluation sheet schema", failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "BotArena.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root not found.");
    }
}
