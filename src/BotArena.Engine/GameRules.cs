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

    /// <summary>Maximum shot travel in tiles; 0 = unlimited (rules ≤ 0.2). Capped rays
    /// end cross-map lane denial while staying above VisionRange, preserving the
    /// shots-outrange-sight information game (RULES-0.3-DESIGN §A).</summary>
    public int ShotRange { get; init; }

    /// <summary>Enables StrafeLeft/StrafeRight (move perpendicular without rotating).
    /// Under rules without strafe the actions validate to Wait with a Blocked result —
    /// never a fault, so newer bots degrade gracefully (RULES-0.3-DESIGN §B).</summary>
    public bool AllowStrafe { get; init; }

    /// <summary>King of the hill (RULES-0.3-DESIGN §C): active bots on zone tiles accrue
    /// zone-ticks; the MaxTicks tiebreak becomes zone → health → damage, and reaching
    /// ZoneDominationTicks wins outright (reason Domination).</summary>
    public bool ZoneControl { get; init; }

    public int ZoneDominationTicks { get; init; }

    /// <summary>When true, zone-ticks accrue only for a SOLE active occupant — a contested
    /// zone pays nobody. Without this, two zone-aware bots co-occupy the hill peacefully
    /// and spawn order decides the accrual race (gen-4 trial: every mirror game was a
    /// slot-1 Domination at the identical tick, 150-146, zero shots exchanged). Exclusive
    /// accrual makes the fight FOR the hill the game.</summary>
    public bool ZoneExclusiveAccrual { get; init; }

    /// <summary>Seed-spawn constraint: never spawn a pair sharing a clear firing lane
    /// within ShotRange (gen-3 finding: tick-0 hits before the first decision).</summary>
    public bool SpawnLaneSafety { get; init; }

    public static GameRules V0_1 => new() { RulesVersion = "0.1" };

    /// <summary>Rules 0.2 = 0.1 + seed-spawn variation. Pinned by the A/B balance run of
    /// 2026-07-22 (GAME-DESIGN): draws 42% → 28%, median game 196 → 151 ticks, more
    /// eliminations, across champions + gen-2 bots on fixed seeds. The energy candidate
    /// did NOT ship: with energy-unaware bots it cancelled the spawn gains (draws back
    /// to 42%) — it stays behind `--rules energy` until bots can manage a resource.</summary>
    public static GameRules V0_2 => V0_1 with
    {
        RulesVersion = "0.2",
        SeedSpawnVariation = true,
    };

    /// <summary>Rules 0.3 = 0.2 + shot range cap + lane-safe spawns — the slate subset
    /// the 300-game harness shipped (DECISIONS #49): draws 38% → 22%, median game
    /// 153 → 120 ticks, eliminations intact. Strafe and zone control measured
    /// draw-positive/length-positive with this population and stay behind their
    /// experiment arms (strafe: oscillation dodging; hill: needs zone-aware bots).</summary>
    public static GameRules V0_3 => V0_2 with
    {
        RulesVersion = "0.3",
        ShotRange = 8,
        SpawnLaneSafety = true,
    };

    /// <summary>The version new matches play. Historical versions stay constructible for
    /// replay verification and A/B harness runs.</summary>
    public static GameRules Current => V0_3;

    /// <summary>Named ruleset lookup, shared by the CLI's --rules flag and the server's
    /// BOTARENA_RULES eval knob. Experiment names carry visibly non-official version
    /// strings (they flow into replays and seed derivation); the held 0.3-slate
    /// mechanics layer on the shipped 0.3 for future re-tests.</summary>
    public static GameRules Resolve(string name) => name switch
    {
        "0.3" => V0_3,
        "0.2" => V0_2,
        "0.1" => V0_1,
        "strafe" => V0_3 with { RulesVersion = "0.4-exp-strafe", AllowStrafe = true },
        // hill v2 (exclusive accrual) superseded shared accrual after the gen-4 trial:
        // co-occupying equals turned the hill into a spawn-order race (DECISIONS #50).
        // hill-shared stays resolvable as the A/B baseline.
        "hill" => V0_3 with
        {
            RulesVersion = "0.4-exp-hill2",
            ZoneControl = true,
            ZoneDominationTicks = 150,
            ZoneExclusiveAccrual = true,
        },
        "hill-shared" => V0_3 with { RulesVersion = "0.4-exp-hill", ZoneControl = true, ZoneDominationTicks = 150 },
        "slate" => V0_3 with
        {
            RulesVersion = "0.4-exp-slate",
            AllowStrafe = true,
            ZoneControl = true,
            ZoneDominationTicks = 150,
        },
        "energy" => V0_2 with
        {
            RulesVersion = "0.3-exp-energy",
            MaxEnergy = 6,
            ShotEnergyCost = 2,
            EnergyRegenTicks = 3,
        },
        _ => throw new ArgumentException($"Unknown rules '{name}' (use 0.3, 0.2, 0.1, strafe, hill, hill-shared, slate, or energy)."),
    };
}
