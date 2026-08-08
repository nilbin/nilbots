namespace BotArena.Engine;

/// <summary>
/// Registered chassis-shape levels (owner ruling 2026-07-31, DECISIONS #194;
/// <c>docs/DESIGN-MECHANISM-SLATE-2026-07-29.md</c> "The prime dissolves").
/// The arm exists because the PRIME was the per-life world's avatar: a body
/// slot 0 that was tougher, respawned on its own clock, owned the class verb,
/// and owned the upgrade ladder. Under the mind the player IS the mind and
/// slot 0 is just a slot, so the prime/child split is a shape the game no
/// longer has a reason for — and dissolving it changes the statline, the
/// lifecycle, the fabrication origin and the upgrade scope at once, which is
/// exactly what a registered arm is for rather than four tunings.
/// </summary>
public enum FrontlineLabsChassisArm
{
    /// <summary>
    /// The measured shape: every class declares a PRIME statline and a CHILD
    /// statline, a prime lifecycle and a child lifecycle, prime-only
    /// fabrication, and a prime-scoped upgrade ladder. Not an arm — selecting
    /// it changes nothing, writes no contract bytes, and keeps every
    /// historical rules, topology and match fingerprint exact.
    /// </summary>
    Split = 0,

    /// <summary>
    /// ONE CHASSIS. Every body of a class shares one statline, one form, one
    /// lifecycle profile and one action catalog; the class's exclusive verb
    /// belongs to the CHASSIS rather than to a slot. Four consequences travel
    /// together because they are one design step, not four:
    /// <list type="bullet">
    /// <item><b>Statline.</b> Each class unifies at its CHILD value — bulwark
    /// 4, fabricator 3, striker 3 (already equal) — and the bulwark's anchor
    /// windup unifies at the child's 1. The prime-value arm is the registered
    /// alternative (<c>chassis-unified-statline</c>).</item>
    /// <item><b>Lifecycle.</b> One profile per class, the one the MAJORITY of
    /// bodies use today: automatic respawn on the child rebuild clock for a
    /// class that receives companions, and explicit fabrication for the
    /// fabricator. The 18-tick prime return no longer names anything, which is
    /// why a numbers-only prime-respawn level is refused on this arm rather
    /// than silently ignored.</item>
    /// <item><b>Fabrication becomes a NETWORK.</b> One form means the
    /// fabricate verb sits on every fabricator body, so any live body is a
    /// fabrication origin and killing one never kills the factory. The
    /// bootstrap the owner ruled travels with it: at TOTAL body loss the home
    /// base acts as the root factory and seeds ONE body at the class's own
    /// respawn delay, at no cost. Total-loss-as-elimination stays registered
    /// as the sharper alternative arm and is not implemented.</item>
    /// <item><b>Upgrade scope.</b> The prime-scoped ladder dies with the
    /// prime: a purchased tier applies to every body of the buying team, live
    /// and future. Tier price doubles to absorb the widened scope
    /// (<c>chassis-unified-tier-price</c>, arms 10 / 20 / 30).</item>
    /// </list>
    /// <para>It is a class-chassis arm, so — exactly like the skills, the aim
    /// grammar, the curve envelope and the cooldown clock — it has no meaning
    /// without a class pair.</para>
    /// </summary>
    Unified,
}
