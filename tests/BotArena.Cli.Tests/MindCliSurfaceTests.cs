using System.Text.Json;

namespace BotArena.Cli.Tests;

/// <summary>
/// The player-reachable mind surface, end to end through the real commands.
///
/// <para>The three things worth pinning are the three that would silently stop
/// being true: the scaffold a player is handed compiles and drives an army, a
/// MIXED match runs (a native mind against a per-life bot, which is the whole
/// migration claim), and the profile is a match-level choice rather than a
/// per-entrant one.</para>
///
/// <para>These run on the diagnostic in-process runtime, deliberately: they are
/// about the CLI's wiring — template copy, entry-type detection, wrap-adapter
/// selection, profile plumbing, replay verification — not about the sandbox,
/// which the WASM suites already cover. Paying a NativeAOT compile per case
/// would buy nothing and cost minutes.</para>
/// </summary>
[Collection("Console")]
public sealed class MindCliSurfaceTests
{
    [Fact]
    public void TheScaffoldedMindProjectCompilesAndDrivesAnArmy()
    {
        string temporary = Temporary("mind-scaffold");
        try
        {
            string project = Scaffold(temporary, "Apprentice", "generic-mind");

            // Everything the memo names as the scaffold's shape, present and
            // in the file an author is told to open first.
            Assert.True(File.Exists(Path.Combine(project, "Roles.cs")));
            Assert.True(File.Exists(Path.Combine(project, "Recall.cs")));
            Assert.True(File.Exists(Path.Combine(project, "ArenaBasics.cs")));
            Assert.Contains(
                "IGenericMindBot",
                File.ReadAllText(
                    Path.Combine(project, "Apprentice.cs")),
                StringComparison.Ordinal);

            string output = Path.Combine(temporary, "mirror");
            Assert.Equal(0, RunExperiment(project, project, output));

            JsonElement replay = ReadReplay(output);
            Assert.Equal(
                "generic-mind-match-1",
                replay
                    .GetProperty("header")
                    .GetProperty("runtime")
                    .GetProperty("contractProfileId")
                    .GetString());

            // A mind that never commanded anything would also produce a
            // document, so the pin is that bodies were actually driven: the
            // scaffold assigns roles and publishes them.
            JsonElement[] ticks =
            [
                .. replay.GetProperty("ticks").EnumerateArray(),
            ];
            Assert.NotEmpty(ticks);
            Assert.All(
                ticks,
                tick => Assert.False(
                    tick.TryGetProperty("actorTurns", out JsonElement actor)
                    && actor.ValueKind == JsonValueKind.Array
                    && actor.GetArrayLength() > 0));
            Assert.Contains(
                ticks,
                tick => tick
                    .GetProperty("mindTurns")
                    .EnumerateArray()
                    .Any(turn => turn
                        .GetProperty("commands")
                        .EnumerateArray()
                        .Any(command =>
                            command.TryGetProperty(
                                "roleTag",
                                out JsonElement tag)
                            && tag.GetString() is { Length: > 0 })));
        }
        finally
        {
            Cleanup(temporary);
        }
    }

    [Fact]
    public void AMindPlaysAPerLifeArtifactInOneMatchAndTheReplayVerifies()
    {
        string temporary = Temporary("mind-mixed");
        try
        {
            // A native mind on one side, an ordinary per-life bot on the
            // other, with NO edit to the per-life sources: the guest wraps it.
            // This is the migration claim, run rather than asserted.
            string mind = Scaffold(temporary, "TheMind", "generic-mind");
            string perLife = Scaffold(temporary, "PerLife", "generic-actor");

            string output = Path.Combine(temporary, "mixed");
            Assert.Equal(0, RunExperiment(mind, perLife, output));

            JsonElement replay = ReadReplay(output);
            Assert.Equal(
                "generic-mind-match-1",
                replay
                    .GetProperty("header")
                    .GetProperty("runtime")
                    .GetProperty("contractProfileId")
                    .GetString());

            // One contract, so BOTH participants answer the mind observation —
            // the per-life side through the wrap adapter, indistinguishably.
            JsonElement[] ticks =
            [
                .. replay.GetProperty("ticks").EnumerateArray(),
            ];
            Assert.All(
                ticks,
                tick => Assert.Equal(
                    2,
                    tick.GetProperty("mindTurns").GetArrayLength()));

            // And it is a real, verifiable document rather than merely a
            // well-shaped one.
            Assert.Equal(
                0,
                VerifyCommand.Run(
                    Path.Combine(output, "replay.json")));
        }
        finally
        {
            Cleanup(temporary);
        }
    }

    [Fact]
    public void AnUnknownProfileIsRefusedRatherThanQuietlyDefaulted()
    {
        // Selecting a profile that does not exist must not silently play the
        // other one: a cohort run on the wrong generation is evidence nobody
        // can retract.
        InvalidOperationException actor =
            Assert.Throws<InvalidOperationException>(() =>
                CliSupport.ParseLabsProfile("brain"));
        Assert.Contains("brain", actor.Message, StringComparison.Ordinal);

        Assert.False(CliSupport.ParseLabsProfile(null));
        Assert.False(CliSupport.ParseLabsProfile("actor"));
        Assert.True(CliSupport.ParseLabsProfile("mind"));
        Assert.True(CliSupport.ParseLabsProfile("generic-mind-match-1"));
    }

    private static int RunExperiment(
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
                "--seed", "42",
                "--out", output,
            ]);
        }
        finally
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
        }
    }

    private static JsonElement ReadReplay(string output)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(output, "replay.json")));
        return document.RootElement.Clone();
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
