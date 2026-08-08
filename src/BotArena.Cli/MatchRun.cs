namespace BotArena.Cli;

/// <summary>
/// The one boundary every CLI command runs a generic match through.
///
/// <para>Its whole job is to make the abort path UNIFORM. A match that stops
/// mid-run — an engine chronology invariant, a replay-validation refusal, a
/// runtime the host could not drive — must reach the operator the same way from
/// every command: a named cell, one line on stderr, and a non-zero exit code
/// that is distinct from "your arguments were wrong". The alternative, which
/// this exists to prevent, is the failure mode four authors hit independently:
/// a sweep whose cells silently produced nothing while the return code said
/// otherwise.</para>
///
/// <para>Everything the guard wraps happens strictly BEFORE the replay is
/// written, so an aborted cell leaves no partial document on disk for a harness
/// to mistake for a completed one. A PARTICIPANT FAULT is deliberately not an
/// abort: it produces a real, verifying document recording a disqualification,
/// and its commands report it with exit code 2.</para>
/// </summary>
public static class MatchRun
{
    /// <summary>
    /// Runs one match body, re-labelling anything that escapes it as a
    /// <see cref="MatchAbortedException"/> naming <paramref name="cell"/>.
    /// </summary>
    public static T Guard<T>(string cell, Func<T> body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cell);
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            return body();
        }
        catch (MatchAbortedException)
        {
            // Already labelled by an inner guard; nesting must not restate it.
            throw;
        }
        catch (Exception error)
        {
            throw new MatchAbortedException(cell, error);
        }
    }

    /// <summary>A cell label from the parts an operator actually greps for.</summary>
    public static string Cell(string bot, string opponent, ulong seed) =>
        $"{bot} vs {opponent}, seed {seed}";

    /// <summary>A cell label for a probe run, which names no opponent pair.</summary>
    public static string Probe(string suiteId, string probeId, ulong seed) =>
        $"{suiteId}/{probeId}, seed {seed}";
}
