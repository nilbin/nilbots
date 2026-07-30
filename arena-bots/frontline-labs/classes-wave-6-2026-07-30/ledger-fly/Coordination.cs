/// <summary>
/// Which coordination lines this build bills. Every one of them is a single static
/// reading so that a leave-one-out build is a one-line diff and the removed line
/// costs nothing at runtime — the same discipline revisions 2-5 used for their
/// ablations, moved into the source because wave 6 ships four rules at once and
/// each of them has to be answerable for its own games.
///
/// <para>All four are TRUE in the frozen artifact. A build with one of them false
/// is a measurement instrument, never a submission.</para>
/// </summary>
internal static class Coordination
{
    /// <summary>
    /// The congestion line: a planned route reserves its tiles against our own
    /// bodies for the ticks it needs them, and one berth on the contested region
    /// belongs to one body.
    /// </summary>
    public static readonly bool Congestion = true;

    /// <summary>
    /// The corridor line: a one-tile corridor cannot be shared, so the body with
    /// right of way owns the whole of it for the ticks its route is inside, and
    /// nobody parks on a mouth it has to come out of.
    /// </summary>
    public static readonly bool Corridor = true;

    /// <summary>
    /// The traffic line: we do not put a new body, or leave a standing one, on
    /// the tile our own next arrival needs.
    /// </summary>
    public static readonly bool Traffic = true;

    /// <summary>
    /// The spacing line: two of our bodies are not left where ONE declared enemy
    /// fan, or one deflection sent back down our own firing lane, covers both.
    /// </summary>
    public static readonly bool Spacing = true;
}
