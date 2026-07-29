namespace BotArena.Engine;

/// <summary>
/// Which mobile guns carry the one-bend curve grammar
/// (<c>docs/DESIGN-MECHANISM-SLATE-2026-07-29.md</c>, "Current kit"). The
/// owner ruling adopts the universal grammar — curves are the blind-validated
/// watchable primitive, and every class should own one — while the striker
/// keeps the deepest envelope so depth stays its identity. Specials never
/// curve either way: a volley profile refuses programmed shots structurally,
/// and turret guns stay straight.
///
/// Universal-versus-striker-only is a phase-2 factor, not an assumption, so
/// both levels stay expressible and separately identified. The
/// <see cref="StrikerOnly"/> level is the measured baseline and adds no token,
/// which is what keeps every existing arm's fingerprint byte-identical.
/// </summary>
public enum FrontlineLabsBendEnvelopeArm
{
    /// <summary>
    /// Today's measured contract: only a class whose own chassis declares
    /// shot programs bends, and it fires through the program-bearing
    /// <c>shoot</c> action while everyone else uses parameterless
    /// <c>shoot-straight</c>.
    /// </summary>
    StrikerOnly = 0,

    /// <summary>
    /// Every class's mobile gun gains the one-bend grammar at its own declared
    /// depth — the striker's full 1–4 tiles, the shallower 1–2 for a class
    /// that gains it here. Those classes' mobile guns move from
    /// <c>shoot-straight</c> to <c>shoot</c>, whose payload stays optional, so
    /// a straight shot remains one parameterless-equivalent decision.
    /// </summary>
    Universal = 1,
}
