namespace BotArena.Sdk;

/// <summary>
/// The participant-scoped MIND programming model: one instance drives every
/// body you own, for the whole match.
///
/// <para><b>The mental model.</b> A mind is not a body. It has no position, no
/// health, and it cannot die. Your bodies are data inside it —
/// <see cref="MindContext.Bodies"/> — and you drive them by WRITING COMMANDS
/// ONTO THEM rather than by returning a decision. That inversion is the whole
/// ergonomic point: there is no key set to get right, no dictionary to build,
/// and no <c>KeyNotFoundException</c> class of bug.</para>
///
/// <para><b>Forgetting a body costs a tick, never the match.</b> Every own live
/// body is pre-filled with <c>Wait</c> before <see cref="Think"/> runs. A body
/// you never touch simply waits — visibly, in the replay. Commanding a body
/// that died this tick is <c>Rejected</c>, recorded and harmless, because under
/// persistent memory that is an easy and forgivable mistake. Only a genuinely
/// malformed submission — two commands for the same body, an unknown action, a
/// malformed argument — is a fault, and a fault is participant-scoped.</para>
///
/// <para><b>The mind ticks even with zero bodies.</b>
/// <see cref="Think"/> is called exactly once per tick for EVERY tick of the
/// match, from tick 0 to the terminal tick, no matter how many bodies you own
/// or whether any of them has a legal action. Total body loss is a data
/// question (<c>mind.Bodies.Length == 0</c>), not a control-flow question:
/// <see cref="MindContext.Slots"/> still shows every pending return with its
/// due tick, and the mode, scoreboard and economy all keep changing while you
/// are dead. Plan the return.</para>
///
/// <para><b>Your fields are your memory.</b> The instance is constructed once
/// per participant per match, before tick 0, and lives until after the terminal
/// tick. There is no memory API to learn — a plan written to a field on tick 44
/// is still there on tick 700, and it OUTLIVES the body executing it, which is
/// the thing a per-life bot structurally could not do. The one honest cost: a
/// mind that traps forgets the match, and under a zero-fault-allowance contract
/// it loses it. Write robust code.</para>
///
/// <code>
/// public sealed class MyMind : IGenericMindBot
/// {
///     private readonly Recall _recall = new();          // lives the whole match
///
///     public void Think(MindContext mind)
///     {
///         _recall.Observe(mind);                        // team perception, ONCE
///
///         MindBody? channeler = mind.Bodies
///             .OrderByDescending(body =&gt; body.Health)
///             .ThenBy(body =&gt; body.UnitId)
///             .FirstOrDefault();
///         channeler?.SetRole("channeler");
///         channeler?.Hold("stationary claim");
///
///         foreach (MindBody screen in mind.Bodies.Where(b =&gt; b != channeler))
///             Screen(mind, screen, channeler);
///     }
/// }
/// </code>
/// </summary>
public interface IGenericMindBot
{
    /// <summary>
    /// Called exactly once, before tick 0. The mind instance then lives the
    /// entire match. Read the frozen rules, map, topology and mode binding from
    /// <see cref="MindStart.Contract"/> here — none of it is repeated in
    /// observations.
    /// </summary>
    /// <param name="start">Immutable initialization for this participant.</param>
    void StartMatch(MindStart start)
    {
    }

    /// <summary>
    /// Called exactly once per tick, unconditionally, for every tick of the
    /// match — including ticks on which you own no live body.
    ///
    /// <para>Returns <see langword="void"/>: commands are written onto
    /// <see cref="MindContext.Bodies"/> with
    /// <see cref="MindBody.Command(string, int, GenericActorActionArgument[])"/>,
    /// <see cref="MindBody.Hold(string?)"/> and
    /// <see cref="MindBody.SetRole(string?)"/>, and harvested after you
    /// return. A second command on the same body throws immediately, so the
    /// mistake is yours to see rather than a silent last-writer-wins.</para>
    /// </summary>
    /// <param name="mind">This tick's frozen public state and command surface.</param>
    void Think(MindContext mind);

    /// <summary>
    /// Called exactly once, after the terminal tick. Nothing you do here can
    /// affect the match; it exists so a mind can flush diagnostics.
    /// </summary>
    /// <param name="end">Why the match ended.</param>
    void EndMatch(MindEnd end)
    {
    }
}
