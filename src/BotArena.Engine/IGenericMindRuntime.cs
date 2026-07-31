namespace BotArena.Engine;

/// <summary>
/// One isolated runtime instance for exactly one SUBMITTED PARTICIPANT, alive
/// for the whole match (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c>
/// §4.1). Bodies are data inside it. A body's destruction disposes nothing; a
/// participant's disqualification or the match's end disposes it.
/// </summary>
public interface IGenericMindRuntime : IDisposable
{
    /// <summary>Called once, before tick 0.</summary>
    void StartMatch(GenericMindRuntimeStart start);

    /// <summary>
    /// Called exactly ONCE PER TICK, unconditionally, for every tick of the
    /// match — including ticks on which the mind owns no live body. The
    /// invariant converts "am I alive?" from a control-flow question into a
    /// data question (§2.7).
    /// </summary>
    GenericMindRuntimeDecisions ExecuteTick(
        GenericMindRuntimeObservation observation);

    void IDisposable.Dispose() { }
}
