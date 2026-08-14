namespace BotArena.Engine;

/// <summary>
/// Canonical Arc Relay ordering for ordinary projectile, veterancy, rear-arc,
/// and Target Paint damage. Kept as one pure function so breakpoint tests and
/// the live session cannot quietly disagree.
/// </summary>
internal static class ArcRelayCombatMagnitude
{
    internal static int ProjectileDamage(
        ArcRelayGameModeDefinition mode,
        int baseDamage,
        int veterancyDamagePoints,
        bool rearArc,
        int targetPaintBonus)
    {
        int veteranBonus = Math.Min(
            checked(veterancyDamagePoints * mode.VeterancyDamagePerPoint),
            mode.VeterancyDamageBonusCap);
        int rearMultiplier = rearArc
            ? mode.RearArcDamageMultiplier
            : 1;
        int damage = mode.VeterancyBonusAfterRearArc
            ? checked(baseDamage * rearMultiplier + veteranBonus)
            : checked((baseDamage + veteranBonus) * rearMultiplier);
        return checked(damage + targetPaintBonus);
    }

    internal static int ConstructDamage(
        ArcRelayGameModeDefinition mode,
        int baseDamage,
        int veterancyDamagePoints) =>
        checked(
            baseDamage
            + Math.Min(
                checked(veterancyDamagePoints
                    * mode.VeterancyDamagePerPoint),
                mode.VeterancyDamageBonusCap));
}
