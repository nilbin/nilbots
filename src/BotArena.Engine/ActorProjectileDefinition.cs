namespace BotArena.Engine;

/// <summary>
/// Explicit projectile, damage, range, and traversal values for an attack
/// profile. Collision policy shared by all actors belongs to the rules
/// aggregate rather than this value.
/// </summary>
public sealed record ActorProjectileDefinition
{
    public ActorProjectileDefinition(
        ActorProjectileMode mode,
        int damagePerHit,
        int maxTravelTiles,
        int ticksPerAdvance,
        int tilesPerAdvance,
        int launchTiles,
        bool advancesOnLaunchTick,
        bool damageAppliedSimultaneously,
        bool diagonalCornersMustBeClear)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (damagePerHit <= 0)
            throw new ArgumentOutOfRangeException(nameof(damagePerHit));
        if (maxTravelTiles <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTravelTiles));
        if (ticksPerAdvance < 0)
            throw new ArgumentOutOfRangeException(nameof(ticksPerAdvance));
        if (tilesPerAdvance <= 0)
            throw new ArgumentOutOfRangeException(nameof(tilesPerAdvance));
        if (launchTiles <= 0 || launchTiles > maxTravelTiles)
            throw new ArgumentOutOfRangeException(nameof(launchTiles));
        if (mode == ActorProjectileMode.InstantRay && ticksPerAdvance != 0)
        {
            throw new ArgumentException(
                "Instant-ray attacks must use zero ticks per advance.",
                nameof(ticksPerAdvance));
        }
        if (mode == ActorProjectileMode.Discrete && ticksPerAdvance == 0)
        {
            throw new ArgumentException(
                "Discrete projectiles need a positive advance cadence.",
                nameof(ticksPerAdvance));
        }
        if (mode == ActorProjectileMode.InstantRay
            && (tilesPerAdvance != 1
                || launchTiles != 1
                || advancesOnLaunchTick))
        {
            throw new ArgumentException(
                "Instant rays use canonical inert traversal values: one tile " +
                "per advance, one launch tile, and no launch-tick advance.");
        }
        if (mode == ActorProjectileMode.Discrete && advancesOnLaunchTick)
        {
            throw new ArgumentException(
                "Generation 3 launches after existing-projectile advancement; " +
                "a new discrete projectile cannot advance again on its launch tick.",
                nameof(advancesOnLaunchTick));
        }
        if (!damageAppliedSimultaneously)
        {
            throw new ArgumentException(
                "Generation 3 collects all valid damage contacts before health mutation.",
                nameof(damageAppliedSimultaneously));
        }

        Mode = mode;
        DamagePerHit = damagePerHit;
        MaxTravelTiles = maxTravelTiles;
        TicksPerAdvance = ticksPerAdvance;
        TilesPerAdvance = tilesPerAdvance;
        LaunchTiles = launchTiles;
        AdvancesOnLaunchTick = advancesOnLaunchTick;
        DamageAppliedSimultaneously = damageAppliedSimultaneously;
        DiagonalCornersMustBeClear = diagonalCornersMustBeClear;
    }

    public ActorProjectileMode Mode { get; }
    public int DamagePerHit { get; }
    public int MaxTravelTiles { get; }
    public int TicksPerAdvance { get; }
    public int TilesPerAdvance { get; }
    public int LaunchTiles { get; }
    public bool AdvancesOnLaunchTick { get; }
    public bool DamageAppliedSimultaneously { get; }
    public bool DiagonalCornersMustBeClear { get; }
}
