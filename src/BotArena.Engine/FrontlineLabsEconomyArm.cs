namespace BotArena.Engine;

/// <summary>
/// Registered battlefield-economy levels
/// (<c>docs/DESIGN-SCRAP-ECONOMY-2026-07-30.md</c> §14 as amended by parts
/// 2–3, DECISIONS #187). The arm exists because the economy attaches its
/// payoff to BODIES rather than to the team's clock, and because the transport
/// leg is the piece that makes the map's dead side lanes a place where fights
/// happen in both directions.
/// <para>It is mutually exclusive with <c>--side-objective muster</c>: both
/// claim the side lanes' attention, so a cell carrying both could attribute
/// neither.</para>
/// </summary>
public enum FrontlineLabsEconomyArm
{
    /// <summary>
    /// No economy. Not an arm — selecting it changes nothing and writes no
    /// contract bytes, and the two scrap observation facts stay empty for the
    /// whole match.
    /// </summary>
    None = 0,

    /// <summary>
    /// SCRAP. Deposits arrive on a public metronome at mirror-fair addresses
    /// in both side lanes; every destroyed body drops a wreck at its death
    /// tile; stepping onto a pile banks the assay instantly and loads the
    /// rest, which banks in full at your own home pad and drops with you if
    /// you die; and the team bank buys typed tiers on edge, plate, and optic
    /// through the new <c>invest</c> verb, which costs the casting body its
    /// action for that tick.
    /// <para>It is a real arm on every pair — the veins, the wreckage, and
    /// the ladder are the same whatever chassis are in the cell — so it is
    /// never inert-omitted. Its cost is three additive observation facts and
    /// one new player verb.</para>
    /// </summary>
    Scrap,

    /// <summary>
    /// SCRAP-FLAT, the pre-registered falsification CONTROL for the invest
    /// action (<c>scrap-flat-control-arm</c>). Same deposits, same carrying,
    /// same wreckage, same ladder, same prices — but the bank buys by itself,
    /// greedily and in declared track order, at no action cost and with no
    /// body involved, and the <c>invest</c> verb does not exist.
    /// <para>If this measures the same as <see cref="Scrap"/> on both the
    /// balance edges and the pacing gates, the allocation decision is inert
    /// and the entire new action family is unjustified. That is the design's
    /// central claim made falsifiable rather than asserted.</para>
    /// </summary>
    ScrapFlat,
}
