namespace BotArena.Cli;

/// <summary>
/// A match stopped before it produced a result, because the engine refused the
/// history it was building (a chronology or replay-validation invariant) or the
/// run failed for any other reason that is not a participant fault.
///
/// <para>An ABORT is not a loss, a draw, or a disqualification: those are match
/// OUTCOMES with a complete, verifiable document. An abort measures NOTHING,
/// and the campaign's most expensive DX defect (three waves, #188's engineering
/// queue) was harnesses scoring aborted cells as completed ones. So aborts get
/// their own exit code — <see cref="ExitCode"/> — distinct from a bad argument
/// (1) and from a participant fault/disqualification (2), and the message says
/// plainly that no replay was written.</para>
/// </summary>
public sealed class MatchAbortedException : Exception
{
    /// <summary>
    /// The CLI's dedicated abort code. Distinct from 1 (usage/environment), 2
    /// (a participant faulted or was disqualified — a real outcome with a real
    /// replay) and 3 (a qualification probe failed).
    /// </summary>
    public const int ExitCode = 4;

    public MatchAbortedException(string cell, Exception inner)
        : base(
            $"match aborted ({cell}): {inner.Message} "
            + "— no replay was written, so this cell measured nothing. "
            + "Re-run it; never score it.",
            inner)
    {
        Cell = cell;
    }

    /// <summary>The cell that measured nothing, in the operator's own terms.</summary>
    public string Cell { get; }
}
