using System.Diagnostics;
using BotArena.Cli;

namespace BotArena.Cli.Tests;

public sealed class GenericActorScaffoldTests
{
    [Fact]
    public void NewGenericActorProfile_CreatesBuildableContractDrivenProject()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-generic-scaffold-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            ProcessResult created = Run(
                temporary,
                "dotnet",
                [
                    typeof(NewCommand).Assembly.Location,
                    "new",
                    "Starter",
                    "--profile",
                    "generic-actor",
                ]);

            Assert.Equal(0, created.ExitCode);
            string project = Path.Combine(temporary, "Starter");
            string source = File.ReadAllText(
                Path.Combine(project, "Starter.cs"));
            string basics = File.ReadAllText(
                Path.Combine(project, "ArenaBasics.cs"));
            Assert.Contains("IGenericActorBot", source);
            Assert.Contains("TryFabricateReady", source);
            Assert.Contains("TryDodge", source);
            Assert.Contains("TryDirectShot", source);
            Assert.Contains("TryAdvanceToActiveObjective", source);
            Assert.Contains("action.ActionCode", basics);
            // The contract readers ArenaBasicsTemplateTests exercises must
            // reach the player, not just the test project's linked copy.
            Assert.Contains("CaptureRules", basics);
            Assert.Contains("ObjectivePresence", basics);
            Assert.Contains("ExpectedArrivalTiles", basics);

            ProcessResult built = Run(
                project,
                "dotnet",
                ["build", "-c", "Release", "--nologo"]);
            Assert.True(
                built.ExitCode == 0,
                built.StandardOutput + built.StandardError);
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    private static ProcessResult Run(
        string workingDirectory,
        string fileName,
        IEnumerable<string> arguments)
    {
        var start = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in arguments)
            start.ArgumentList.Add(argument);

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException(
                $"Could not start {fileName}.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, output, error);
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
