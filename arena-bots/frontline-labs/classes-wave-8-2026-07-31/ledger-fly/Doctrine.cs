/// <summary>
/// Which wave-8 lines this build bills. Same discipline as
/// <see cref="Coordination"/>: one static reading per rule so a leave-one-out
/// build is a one-line diff and the removed line costs nothing at runtime.
///
/// <para>Five are TRUE in the frozen artifact and <see cref="Courier"/> is
/// FALSE, which is a measured verdict rather than an omission — see its own
/// note and `DX.md`. A build with any other setting is a measurement
/// instrument, never a submission.</para>
/// </summary>
internal static class Doctrine
{
    /// <summary>
    /// THE STILL LINE. Where a capture counts only bodies that did not change
    /// tile, a claimer's discretionary footwork is no longer free footwork —
    /// it is the whole tick's gain. So while this body is one of the claimers
    /// and the point is paying, posture, berth-shuffling and every other
    /// optional step are refused.
    /// </summary>
    public static readonly bool Still = true;

    /// <summary>
    /// THE ESCORT LINE. Gain is capped at a declared surplus, so the third
    /// body on the point buys nothing. Beyond the cap our bodies stop asking
    /// for a berth and start standing on the firing lines INTO the berths —
    /// off the region, where damage reverts nothing and a bolt dies on them
    /// for free.
    /// </summary>
    public static readonly bool Escort = true;

    /// <summary>
    /// THE INTERRUPT LINE. Damage to a controlling body ON the objective
    /// reverts the run. A step INSIDE the region avoids the bolt, keeps the
    /// denial weight (which counts movers) and costs exactly one tick of
    /// gain — so under a declared interrupt a claimer dodges in-region rather
    /// than eating the hit, which is the exact inverse of the wave-5 rule.
    /// </summary>
    public static readonly bool Interrupt = true;

    /// <summary>
    /// THE LEDGER LINE, extended to the store. One designated body converts
    /// bank into declared tiers, in an order derived from the contract's own
    /// effect policies and stopping conditions rather than from track names.
    /// </summary>
    public static readonly bool Invest = true;

    /// <summary>
    /// THE COURIER LINE, AND THE ONE THIS REVISION DOES NOT SHIP.
    ///
    /// <para>The rule is complete and it works: one body at a time carries,
    /// named by a rule every one of our lives evaluates identically, gated on a
    /// declared roster surplus, on no enemy claim standing, and on not being
    /// behind on the only channel that scores. It fetched 85 scrap and 2.7
    /// tiers a match against a live striker.</para>
    ///
    /// <para>It still does not pay. Leave-one-out against the wave-8 bulwark
    /// artifact: <b>-22.3 territorial margin and 3W3L to 0W6L</b> for having it
    /// on, against <b>+6.0</b> for it in the fabricator mirror and <b>0.0</b> in
    /// every other measured cell. The ledger's own rule decides it — a body
    /// sold for less ground than it costs to replace is a loss, and the tier it
    /// fetches does not buy the front back. So the line stays in the source as
    /// the instrument that measured it, switched off, with the number written
    /// down. The assay still pays in full at the tile: this team banks about
    /// nine scrap a match by stepping on wreckage it was standing next to
    /// anyway, and <see cref="Invest"/> spends it.</para>
    /// </summary>
    public static readonly bool Courier = false;

    /// <summary>
    /// THE LETHAL LINE. A declared hit that meets or exceeds this body's
    /// health is not a price, it is a hole in the floor: those tiles leave the
    /// route search entirely instead of being scored expensive.
    /// </summary>
    public static readonly bool Lethal = true;
}

