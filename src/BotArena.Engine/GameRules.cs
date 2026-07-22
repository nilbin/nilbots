namespace BotArena.Engine;

/// <summary>
/// All gameplay-affecting numeric values live here and are pinned by <see cref="RulesVersion"/>.
/// Changing any value requires a new rules version (plan §34.4).
/// </summary>
public sealed record GameRules
{
    public string RulesVersion { get; init; } = BotArenaVersions.GameRulesVersion;

    public int MaxTicks { get; init; } = 500;

    public int MaxHealth { get; init; } = 3;

    /// <summary>Vision range measured in Chebyshev distance (see docs/DECISIONS.md).</summary>
    public int VisionRange { get; init; } = 6;

    /// <summary>
    /// Cooldown set after a shot; it decrements at the end of every following tick and the
    /// bot may shoot again once it reaches zero (a shot every 3rd tick with the default of 2).
    /// </summary>
    public int ShootCooldownTicks { get; init; } = 2;

    public int DamagePerHit { get; init; } = 1;

    /// <summary>A bot is disqualified after this many runtime faults in one match (plan §10).</summary>
    public int FaultLimit { get; init; } = 3;

    public int MaxDebugBytesPerTick { get; init; } = 512;

    public int MaxDebugBytesPerMatch { get; init; } = 64 * 1024;

    /// <summary>When true, spawn positions/facings derive deterministically from the match
    /// seed (distance-constrained floor pair facing each other) instead of the map's fixed
    /// spawns — so different seeds genuinely produce different battles (GAME-DESIGN).</summary>
    public bool SeedSpawnVariation { get; init; }

    /// <summary>Energy a bot starts with and can hold. 0 disables the energy system
    /// entirely (rules 0.1 behavior): shots are then limited by cooldown alone.</summary>
    public int MaxEnergy { get; init; }

    /// <summary>Energy consumed per shot. A Shoot without enough energy becomes Wait with
    /// an OnCooldown result (deliberately reusing the enum — old bots stay compatible).</summary>
    public int ShotEnergyCost { get; init; }

    /// <summary>One energy point regenerates every N ticks (at end-of-tick, capped at
    /// MaxEnergy). Sustained fire is throttled below the pure-cooldown rate, so camping
    /// on a lane has a real cost — the anti-draw lever (GAME-DESIGN backlog #2).</summary>
    public int EnergyRegenTicks { get; init; }

    public static GameRules V0_1 => new() { RulesVersion = "0.1" };
}
