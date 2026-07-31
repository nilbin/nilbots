namespace BotArena.Engine;

/// <summary>
/// The engine-side budget facts of runtime configuration 2.0
/// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §4.2, §4.3). P1 computes
/// and records them per mind turn; P2's WASM runtime is what meters against
/// them.
/// <para>
/// Three candidate policies were weighed and only one tracks the work. A flat
/// 200M is a 9x cut disguised as parity, because a mind does nine bodies' work
/// in one call. A flat 1.8B gives a one-body mind nine times the compute PER
/// BODY of a nine-body mind, creating a weak gradient toward owning fewer
/// bodies and making the budget a function of the roster arm rather than of the
/// work. Scaling is what is left.
/// </para>
/// <para>
/// The <c>PerBody</c> term is EXACTLY today's per-life budget, which is what
/// keeps the §7.2 null pin from being confounded by a compute difference, and
/// the <c>Base</c> term funds the once-per-tick shared work that has no
/// per-body home — digesting the union, updating the belief map, assigning
/// roles. Because the base is available at zero bodies, the "ticks with no
/// bodies" invariant is affordable.
/// </para>
/// </summary>
public static class GenericMindTickBudget
{
    /// <summary>
    /// Once-per-tick shared-reasoning allowance, 1.25x one body's budget.
    /// </summary>
    public const long BaseTickFuel = 250_000_000;

    /// <summary>Exactly today's per-life budget, unchanged.</summary>
    public const long PerBodyTickFuel = 200_000_000;

    /// <summary>
    /// Paid once per participant per match rather than once per life. At the
    /// legion roster that is 5B instead of 125-200B.
    /// </summary>
    public const long StartupFuel = 5_000_000_000;

    /// <summary>
    /// 64 MiB -> 128 MiB. Even doubled, per-participant peak memory falls 4.5x
    /// because there is one instance instead of nine.
    /// </summary>
    public const long LinearMemoryBytes = 128L * 1024 * 1024;

    /// <summary>Unchanged from configuration 1.0.</summary>
    public const int TableElements = 16_384;

    /// <summary>Unchanged from configuration 1.0, and it spans BOTH halves of an exchange.</summary>
    public const int WallClockBackstopSeconds = 30;

    /// <summary>
    /// <c>250M + 200M x liveOwnBodies</c>. <paramref name="liveOwnBodies"/> is
    /// authoritative tick-start state fixed by <c>PrepareTick</c> before the
    /// call and recorded in the replay, so the budget is a pure function of
    /// replayable state and two hosts compute the same number.
    /// </summary>
    public static long TickFuel(int liveOwnBodies)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(liveOwnBodies);
        return checked(BaseTickFuel + (PerBodyTickFuel * liveOwnBodies));
    }
}
