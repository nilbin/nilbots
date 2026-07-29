namespace BotArena.Engine;

/// <summary>
/// The pre-registered class-skill kit from the mechanism slate
/// (<c>docs/DESIGN-MECHANISM-SLATE-2026-07-29.md</c>, "Current kit"). Each
/// flag is one class-owned candidate skill; they compose, and
/// <see cref="None"/> is the measured baseline rather than an arm. A skill is
/// owned by exactly one class, so an arm only carries the tokens whose owning
/// class is actually present in the cell — the arm identity stays a function
/// of the contract content.
/// </summary>
[Flags]
public enum FrontlineLabsSkillKit
{
    /// <summary>Today's classes: no skills, no ruleset suffix.</summary>
    None = 0,

    /// <summary>
    /// Striker VOLLEY. A reversible, windup-gated same-life stance whose gun
    /// fires three simultaneous damage-1 bolts down the facing lane and the
    /// two adjacent 45-degree headings, on a cadence meaningfully slower than
    /// the mobile gun. Zoning width, not a wipe: width is what beats the
    /// covering number the skill-shot forensics measured.
    /// </summary>
    StrikerVolley = 1,

    /// <summary>
    /// Bulwark AEGIS SHELL. A reversible, windup-gated same-life stance that
    /// consumes enemy projectiles arriving inside its facing quadrant without
    /// damage. Tenure is priced by the form — immobile, unable to attack —
    /// rather than by a rule-enforced clock, and objective weight stays 1.
    /// </summary>
    BulwarkAegisShell = 2,

    /// <summary>
    /// Fabricator FIVE SLOTS. The numbers class literally has numbers: a
    /// fabricator-controlled team fields a prime plus four children while the
    /// opposing class keeps three slots. This is a deliberate, owner-approved
    /// amendment of DECISIONS #153's same-topology reading and mints its own
    /// topology profile and fingerprint.
    /// </summary>
    FabricatorFiveSlots = 4,
}
