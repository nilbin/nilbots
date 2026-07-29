namespace BotArena.Engine;

/// <summary>
/// A form-level defensive capability applied when an enemy projectile makes
/// contact with a life in that form. <see cref="None"/> is today's behaviour
/// and the inert default; the canonical writer emits nothing for it, so every
/// contract authored before guards existed keeps its exact fingerprint (the
/// additive-field discipline of DECISIONS #156).
/// </summary>
public enum ActorFormProjectileGuardKind
{
    /// <summary>Contacts resolve by the collision contract alone.</summary>
    None = 0,

    /// <summary>
    /// An enemy projectile whose direction of travel approaches from inside
    /// the life's facing quadrant is consumed without damage; flank and rear
    /// contacts damage normally. The quadrant is the vision cone's convention
    /// (forward &gt;= 0, |lateral| &lt;= forward, diagonal edges included)
    /// evaluated on the projectile's approach vector rather than on a tile,
    /// because a contact puts both bodies on the same tile.
    /// </summary>
    FacingQuadrantContactsConsumedWithoutDamage = 1,
}
