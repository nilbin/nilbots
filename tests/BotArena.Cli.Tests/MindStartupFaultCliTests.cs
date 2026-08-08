using System.Text.Json;

namespace BotArena.Cli.Tests;

/// <summary>
/// THE MIND-STARTUP FAULT, end to end through the real command (DECISIONS #192,
/// the pre-friction pass).
///
/// <para>The null pin's first run hit this with a pre-mind artifact: a mind that
/// traps on its first tick while owning MORE THAN ONE body used to abort
/// document recording outright ("Runtime fault evidence does not match its actor
/// turn"), so the match produced nothing at all. It must instead complete as an
/// ordinary participant DISQUALIFICATION, with a real, verifying document — the
/// distinction the abort exit code exists to preserve: a disqualification is an
/// OUTCOME (exit 2, replay written), an abort measures nothing (exit 4, no
/// replay).</para>
///
/// <para>The legion roster is what makes the repro: it fields three bodies per
/// participant at tick 0, and the single-body default is exactly the case the
/// old code happened to validate. Run on the diagnostic in-process runtime,
/// like the rest of the CLI surface tests — this is about the recording, not
/// the sandbox.</para>
/// </summary>
[Collection("Console")]
public sealed class MindStartupFaultCliTests
{
    [Fact]
    public void AMultiBodyMindThatTrapsIsRecordedAsADisqualification()
    {
        string temporary = Temporary("mind-startup-fault");
        try
        {
            string trapping = Scaffold(temporary, "Trapper", "generic-mind");
            Trap(trapping, "Trapper");
            string healthy = Scaffold(temporary, "Steady", "generic-mind");

            string output = Path.Combine(temporary, "fault");
            // Exit 2 is "a participant faulted or was disqualified" — a real
            // outcome. It is NOT the abort code, and it is not zero.
            Assert.Equal(2, RunLegionMatch(trapping, healthy, output));

            string replayPath = Path.Combine(output, "replay.json");
            Assert.True(
                File.Exists(replayPath),
                "a disqualification still owes a document");
            // The document is complete and verifies — not a partial one that
            // a harness could score as a finished match.
            Assert.Equal(0, VerifyCommand.Run(replayPath));

            using var document = JsonDocument.Parse(
                File.ReadAllText(replayPath));
            JsonElement root = document.RootElement;
            Assert.Equal(
                "generic-mind-match-1",
                root
                    .GetProperty("header")
                    .GetProperty("runtime")
                    .GetProperty("contractProfileId")
                    .GetString());
            Assert.Equal(
                "fault-eligibility",
                root
                    .GetProperty("result")
                    .GetProperty("completionReason")
                    .GetString());

            // The repro's shape, pinned: the faulted turn owned MORE THAN ONE
            // body. Without that this test would pass against the old code.
            JsonElement faulted = root
                .GetProperty("ticks")
                .EnumerateArray()
                .SelectMany(tick => tick
                    .GetProperty("mindTurns")
                    .EnumerateArray())
                .First(turn =>
                    turn.GetProperty("runtimeFault").ValueKind
                    != JsonValueKind.Null);
            Assert.True(faulted.GetProperty("liveBodyCount").GetInt32() > 1);
        }
        finally
        {
            Cleanup(temporary);
        }
    }

    /// <summary>Makes the scaffolded mind trap on its very first tick.</summary>
    private static void Trap(string project, string name)
    {
        string path = Path.Combine(project, $"{name}.cs");
        string source = File.ReadAllText(path);
        const string signature = "public void Think(MindContext mind)\n    {";
        string normalized = source.Replace("\r\n", "\n");
        Assert.Contains(signature, normalized, StringComparison.Ordinal);
        File.WriteAllText(
            path,
            normalized.Replace(
                signature,
                signature
                + "\n        throw new InvalidOperationException("
                + "\"a pre-mind guest traps at startup\");",
                StringComparison.Ordinal));
    }

    private static int RunLegionMatch(
        string bot,
        string opponent,
        string output)
    {
        TextWriter stdout = Console.Out;
        TextWriter stderr = Console.Error;
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
        try
        {
            return FrontlineLabsExperimentCommand.Run(
            [
                "--profile", "mind",
                "--runtime", "in-process",
                "--bot", bot,
                "--opponent", opponent,
                // Three bodies per participant from tick 0 — the roster the
                // shipped game runs and the one the old recording refused.
                "--classes", "bulwark-vs-striker",
                "--roster", "legion",
                "--seed", "930011",
                "--out", output,
            ]);
        }
        finally
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
        }
    }

    private static string Scaffold(
        string root,
        string name,
        string profile)
    {
        Directory.CreateDirectory(root);
        string previous = Directory.GetCurrentDirectory();
        TextWriter stdout = Console.Out;
        Console.SetOut(TextWriter.Null);
        try
        {
            Directory.SetCurrentDirectory(root);
            Assert.Equal(0, NewCommand.Run(name, ["--profile", profile]));
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
            Console.SetOut(stdout);
        }
        return Path.Combine(root, name);
    }

    private static string Temporary(string label) =>
        Path.Combine(
            Path.GetTempPath(),
            $"nilbots-{label}-{Guid.NewGuid():N}");

    private static void Cleanup(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // A build server holding a lock on an output file is not a test
            // failure; the temp directory is disposable either way.
        }
    }
}
