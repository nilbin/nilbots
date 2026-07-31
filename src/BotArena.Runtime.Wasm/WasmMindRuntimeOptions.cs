using BotArena.Engine;

namespace BotArena.Runtime.Wasm;

/// <summary>
/// Runtime CONFIGURATION 2.0 — the sandbox limits enforced on a mind. It sits
/// beside <see cref="WasmRuntimeOptions"/> rather than extending it because
/// exactly two numbers move and one of them stops being a number at all.
///
/// <list type="bullet">
/// <item><b>Fuel is a formula, not a constant.</b> A mind does N bodies' work
/// in one call, so a flat per-life budget would be an N-fold cut disguised as
/// parity, and a flat roster-max budget would hand a one-body mind N times the
/// compute PER BODY of a full one. The budget therefore tracks the work:
/// <c>base + perBody x liveOwnBodies</c>, with <c>perBody</c> set to EXACTLY
/// the per-life budget so that comparing the two profiles can never be
/// confounded by a compute difference.</item>
/// <item><b>Linear memory doubles to 128 MiB</b>, because the mind is now the
/// only instance the participant owns and holds match-long belief state. Even
/// doubled, peak memory per participant falls several-fold: one instance
/// replaces one per live body.</item>
/// </list>
///
/// <para>Everything else is carried unchanged from configuration 1.0 —
/// table-element ceiling, one instance/table/memory per Store, deterministic
/// clock and entropy shims, immediate <c>NOSYS</c> for <c>poll_oneoff</c>, no
/// start section, an <c>_start</c> export, and the wall-clock backstop that
/// spans both halves of an exchange.</para>
/// </summary>
public sealed record WasmMindRuntimeOptions
{
    public required string ModulePath { get; init; }

    /// <summary>
    /// Guest-side bot selection carried beside MindStart. Multi-bot framework
    /// artifacts read it; single-bot player artifacts ignore it.
    /// </summary>
    public string BotName { get; init; } = "";

    /// <summary>
    /// Once-per-tick shared-reasoning allowance, granted even on a tick the
    /// mind owns no live body — which is what makes the "the mind ticks even
    /// with zero bodies" invariant affordable rather than a subsidy.
    /// </summary>
    public ulong BaseTickFuel { get; init; } =
        (ulong)GenericMindTickBudget.BaseTickFuel;

    /// <summary>Exactly the per-life budget, per live own body.</summary>
    public ulong PerBodyTickFuel { get; init; } =
        (ulong)GenericMindTickBudget.PerBodyTickFuel;

    /// <summary>
    /// One-time budget for runtime startup plus match initialization, paid once
    /// per participant per match rather than once per life.
    /// </summary>
    public ulong StartupFuel { get; init; } =
        (ulong)GenericMindTickBudget.StartupFuel;

    /// <summary>64 MiB -&gt; 128 MiB (configuration 2.0).</summary>
    public long MaxMemoryBytes { get; init; } =
        GenericMindTickBudget.LinearMemoryBytes;

    /// <summary>Carried unchanged from configuration 1.0.</summary>
    public uint MaxTableElements { get; init; } =
        (uint)GenericMindTickBudget.TableElements;

    /// <summary>
    /// Wall-clock backstop only; fuel is the deterministic limit. A tick that
    /// hits this without exhausting fuel is a permanent runtime failure.
    /// </summary>
    public int TickTimeoutMs { get; init; } =
        GenericMindTickBudget.WallClockBackstopSeconds * 1000;

    /// <summary>
    /// The authoritative per-tick budget for a given tick-start live-body
    /// count. It is a pure function of state fixed before the call and recorded
    /// in the replay, so two hosts compute the same number.
    /// </summary>
    public ulong TickFuel(int liveOwnBodies)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(liveOwnBodies);
        return checked(
            BaseTickFuel + (PerBodyTickFuel * (ulong)liveOwnBodies));
    }
}
