using BotArena.Toolchain;

namespace BotArena.Cli;

/// <summary>
/// The single mapping from "something went wrong" to what the process reports.
///
/// <para>It lives outside <c>Program.cs</c>'s top-level statements so it can be
/// tested directly: the invariant worth pinning is that NO failure — not one
/// exception type, not one command — can produce a zero exit code, and that the
/// human-readable line always goes to stderr and never to stdout. A harness that
/// trusts the return code has to be able to.</para>
/// </summary>
public static class CliFailure
{
    /// <summary>
    /// The line to print on stderr, the process exit code, and whether the
    /// failure is an unexpected bug worth offering a stack trace for.
    /// </summary>
    public readonly record struct Report(
        string Message,
        int ExitCode,
        bool Unexpected);

    public static Report Describe(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error switch
        {
            // An abort measures nothing. Its own code, so a sweep can tell
            // "this cell did not run" from "your flags were wrong".
            MatchAbortedException abort => new Report(
                abort.Message,
                MatchAbortedException.ExitCode,
                Unexpected: false),
            // The message already carries the extracted compiler diagnostics
            // and the log path.
            BotBuildException => new Report(error.Message, 1, false),
            System.Net.Http.HttpRequestException or TaskCanceledException =>
                new Report(
                    $"could not reach the server: {error.Message.TrimEnd('.')}. "
                    + "Check the URL (--server) and your connection; "
                    + "`nilbots doctor` shows the configured server.",
                    1,
                    false),
            // Expected user-facing failures (bad argument, unreachable server,
            // missing file): one clean line, never a stack trace — those leaked
            // CI build paths to players.
            InvalidOperationException or FileNotFoundException
                or ArgumentException or DirectoryNotFoundException
                or IOException => new Report(error.Message, 1, false),
            // Last resort: an unexpected fault is still a bug, but a player
            // should get a readable line and a way to produce the full trace.
            _ => new Report(error.Message, 1, Unexpected: true),
        };
    }

    /// <summary>Prints the failure to stderr and returns the exit code.</summary>
    public static int Print(Exception error)
    {
        Report report = Describe(error);
        Console.Error.WriteLine($"error: {report.Message}");
        if (report.Unexpected)
        {
            Console.Error.WriteLine(
                "This looks like a bug. Set NILBOTS_DEBUG=1 and re-run for "
                + "the full trace.");
            if (Environment.GetEnvironmentVariable("NILBOTS_DEBUG")
                is "1" or "true")
            {
                Console.Error.WriteLine(error);
            }
        }
        return report.ExitCode;
    }
}
