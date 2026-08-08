using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Raw mind reply before common-host admission: a decision MAP, not one action
/// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §2.4).
/// <para>
/// The per-life generation demanded exactly one decision per active life and
/// failed the batch atomically on a missing key. That strictness was right when
/// the host mapped N independent runtimes onto N keys — a missing key meant the
/// HOST had lost track of a life. Under the mind it would be actively hostile,
/// because a mind that forgets a body has an ergonomics bug and ergonomics bugs
/// are precisely what #190 removes. So:
/// </para>
/// <list type="bullet">
/// <item>every own live body defaults to <c>Wait</c>, and forgetting one costs
/// that body one tick, visibly, in the replay — not the match;</item>
/// <item>a command naming a body the participant does not own, or one that is
/// not live this tick, is <c>Rejected</c> — commanding a body that died this
/// tick is an easy and FORGIVABLE mistake under persistent memory;</item>
/// <item>two commands for the same body, a malformed action, or a malformed
/// argument is <c>Faulted</c>, exactly as today, and increments the
/// participant counter.</item>
/// </list>
/// </summary>
/// <param name="Commands">
/// The commands the mind wrote this tick, in submission order. May be shorter
/// than the live-body set and may name bodies that no longer exist; both are
/// recorded and neither is elided.
/// </param>
/// <param name="Intents">
/// RESERVED (§11). A non-empty submission is <c>Rejected</c> — recorded,
/// non-fatal — until a format with allied minds is admitted.
/// </param>
/// <param name="DebugMessage">
/// The mind's own diagnostic text for this tick, bounded at 4 KiB and
/// non-authoritative.
/// <para>
/// This is the MIND's diagnostics home, and it has to exist separately from
/// the per-command one: a mind reasons ONCE per tick over the whole army, so
/// the sentence that explains a tick — "front is held, sending unit 3 for the
/// vein" — belongs to nobody's command. Attaching it to an arbitrary body
/// would misfile it, and dropping it would leave a mind that owns no live body
/// with no way to say anything at all on the very ticks its planning is least
/// observable.
/// </para>
/// </param>
public sealed record GenericMindRuntimeDecisions(
    ImmutableArray<GenericMindCommand> Commands,
    ImmutableArray<GenericMindDeclaredIntent> Intents,
    string? DebugMessage = null)
{
    /// <summary>A mind that commanded nothing. Every live body waits.</summary>
    public static GenericMindRuntimeDecisions Empty { get; } =
        new([], []);

    public GenericMindRuntimeDecisions(
        ImmutableArray<GenericMindCommand> commands)
        : this(commands, [])
    {
    }
}
