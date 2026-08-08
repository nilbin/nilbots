using BotArena.Toolchain;

namespace BotArena.Cli.Tests;

/// <summary>
/// THE ABORT CONTRACT (DECISIONS #188's engineering queue; four authors,
/// independently).
///
/// <para>An aborted cell measures nothing. The campaign's most expensive DX
/// defect was harnesses scoring one as a completed match, so the CLI owes three
/// guarantees and this file is where they are pinned: no failure of any kind
/// can exit 0; the human-readable line goes to stderr, never stdout; and an
/// abort is distinguishable from a bad argument and from a real
/// disqualification, because those three call for three different reactions
/// from a sweep.</para>
/// </summary>
public sealed class MatchAbortExitCodeTests
{
    /// <summary>
    /// Everything the CLI can throw, including the exact shapes the wave-8
    /// population produced from the engine.
    /// </summary>
    public static TheoryData<Exception> EveryFailureShape() =>
    [
        // The two real wave-8 engine aborts, verbatim.
        new ArgumentException(
            "A retained projectile must preserve its exact resolved committed path.",
            "projectiles"),
        new ArgumentException(
            "A returned projectile is launched on the deflection tick with a fresh travel budget.",
            "ticks"),
        // The null pin's mind-startup abort.
        new ArgumentException(
            "Runtime fault evidence does not match its actor turn.",
            "resolution"),
        new InvalidOperationException("bad flag"),
        new FileNotFoundException("no such artifact"),
        new DirectoryNotFoundException("no such directory"),
        new IOException("the volume is full"),
        new BotBuildException("compile failed", "build.log"),
        new System.Net.Http.HttpRequestException("connection refused"),
        new TaskCanceledException("timed out"),
        new NotSupportedException("something nobody predicted"),
    ];

    [Theory]
    [MemberData(nameof(EveryFailureShape))]
    public void NoFailureCanExitZero(Exception failure)
    {
        Assert.NotEqual(0, CliFailure.Describe(failure).ExitCode);
        // And the same is true once the match boundary has labelled it.
        Assert.NotEqual(
            0,
            CliFailure.Describe(
                new MatchAbortedException("a cell", failure)).ExitCode);
    }

    [Theory]
    [MemberData(nameof(EveryFailureShape))]
    public void TheFailureLineGoesToStderrAndStdoutStaysClean(
        Exception failure)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        TextWriter previousOut = Console.Out;
        TextWriter previousError = Console.Error;
        int exitCode;
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            exitCode = CliFailure.Print(failure);
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
        }

        Assert.NotEqual(0, exitCode);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.StartsWith(
            "error: ",
            stderr.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AnAbortIsDistinguishableFromAUsageErrorAndFromAnOutcome()
    {
        // Three reactions a sweep needs to tell apart: 1 means "your
        // invocation was wrong", 2 means "a participant faulted — the match
        // has a real, verifiable document", and 4 means "this cell produced
        // NOTHING; re-run it, never score it".
        var abort = new MatchAbortedException(
            "bot vs opponent, seed 42",
            new ArgumentException("chronology refused", "projectiles"));
        Assert.Equal(4, MatchAbortedException.ExitCode);
        Assert.Equal(
            MatchAbortedException.ExitCode,
            CliFailure.Describe(abort).ExitCode);
        Assert.Equal(
            1,
            CliFailure.Describe(
                new InvalidOperationException("bad flag")).ExitCode);
    }

    [Fact]
    public void TheAbortMessageNamesTheCellAndSaysNothingWasMeasured()
    {
        var abort = new MatchAbortedException(
            MatchRun.Cell("march-wall", "gate-stone", 104729),
            new ArgumentException(
                "A retained projectile must preserve its exact resolved committed path.",
                "projectiles"));
        Assert.Contains(
            "march-wall vs gate-stone, seed 104729",
            abort.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "A retained projectile must preserve",
            abort.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "measured nothing",
            abort.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheGuardLabelsAnythingThatEscapesAMatchRun()
    {
        MatchAbortedException aborted =
            Assert.Throws<MatchAbortedException>(() =>
                MatchRun.Guard<int>(
                    MatchRun.Cell("a", "b", 7),
                    () => throw new ArgumentException(
                        "chronology refused",
                        "ticks")));
        Assert.Equal("a vs b, seed 7", aborted.Cell);
        Assert.IsType<ArgumentException>(aborted.InnerException);
    }

    [Fact]
    public void NestedGuardsDoNotRestateTheCell()
    {
        // A probe suite guards each run and the command may guard the suite;
        // the operator must still see ONE cell name, the innermost one.
        MatchAbortedException aborted =
            Assert.Throws<MatchAbortedException>(() =>
                MatchRun.Guard<int>(
                    "outer",
                    () => MatchRun.Guard<int>(
                        "inner",
                        () => throw new IOException("nope"))));
        Assert.Equal("inner", aborted.Cell);
    }

    [Fact]
    public void ACompletedMatchIsNeverRelabelledAsAnAbort()
    {
        // The guard is a failure boundary, not a wrapper: a run that produces
        // a result passes it through untouched.
        Assert.Equal(
            "result",
            MatchRun.Guard(MatchRun.Cell("a", "b", 1), () => "result"));
    }
}
