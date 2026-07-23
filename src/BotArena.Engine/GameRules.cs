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

    /// <summary>Seed-spawn constraint for zone rules: both spawns must be within
    /// SpawnVariation.ZoneDistanceTolerance walking steps of the same distance to the
    /// zone, so the opening race is decided by play rather than spawn luck (gen-4:
    /// under exclusive accrual first arrival is a real per-game edge).</summary>
    public bool ZoneSpawnFairness { get; init; }

    /// <summary>Seed-spawn sampler attempts before falling back to the map's fixed
    /// spawns (which bypass every constraint — gen-5 finding #1). Gameplay-affecting,
    /// hence a rules value: 64 legacy. Irrelevant when <see cref="ExhaustiveSpawns"/>
    /// replaces sampling entirely.</summary>
    public int SpawnAttempts { get; init; } = 64;

    /// <summary>Replaces spawn sampling with exhaustive enumeration (§H item 3): every
    /// floor pair satisfying ALL constraints is precomputed and the seed picks one —
    /// no attempt budget, no silent unfair fallback. A map with an empty valid set is
    /// rejected loudly instead of played unfairly.</summary>
    public bool ExhaustiveSpawns { get; init; }

    /// <summary>Replay-surface flag: per-tick bot snapshots carry cumulative zone-ticks,
    /// so viewers read the tally instead of re-deriving accrual rules (the source of a
    /// real scoreboard bug). Rules-gated only to keep official 0.4 replay bytes stable;
    /// no gameplay effect.</summary>
    public bool ReplayZoneTallies { get; init; }

    /// <summary>Directional vision (RULES-0.5-DESIGN §A): sight is the 90° quadrant in
    /// the facing direction plus a Chebyshev-1 omnidirectional proximity ring, still
    /// range- and LOS-limited. Off = classic omnidirectional sight.</summary>
    public bool VisionCone { get; init; }

    /// <summary>Loud events (Shot/Damage/Destroyed/Disqualified) are delivered within
    /// this Chebyshev radius regardless of sight (RULES-0.5-DESIGN §A: the Decoy Shot
    /// needs out-of-cone signals). 0 = off; quiet events stay sight-gated always.</summary>
    public int HearingRadius { get; init; }

    /// <summary>Projectile travel (RULES-0.5-DESIGN §B): a shot spawns a bolt that
    /// occupies its tile (lethal to non-owners standing on or entering it) and advances
    /// one tile every this-many ticks, despawning on walls or after ShotRange tiles.
    /// 0 = instant rays (legacy). Deliberately slow values make bolts zoning tools.</summary>
    public int ProjectileTicksPerTile { get; init; }

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

    /// <summary>Rules 0.4 = 0.3 + zone control (exclusive accrual, domination at 150,
    /// zone-first tiebreak, zone-distance-fair spawns) — the gen-4 hill experiment
    /// graduated on harness + bracket data (DECISIONS #53): draws 37% → 12%, decisive
    /// endings 63% → 88%, three distinct zone doctrines all viable. Median game length
    /// rose 77 → 158 ticks — the accepted trade for decided games.</summary>
    public static GameRules V0_4 => V0_3 with
    {
        RulesVersion = "0.4",
        ZoneControl = true,
        ZoneDominationTicks = 150,
        ZoneExclusiveAccrual = true,
        ZoneSpawnFairness = true,
    };

    /// <summary>The version new matches play. Historical versions stay constructible for
    /// replay verification and A/B harness runs.</summary>
    public static GameRules Current => V0_4;

    /// <summary>Every name <see cref="Resolve"/> accepts — the single source for the
    /// error message, the CLI help (pinned by DocDriftTests), and future listings.</summary>
    public static readonly IReadOnlyList<string> KnownNames =
        ["0.4", "0.3", "0.2", "0.1", "0.5-control", "cone", "bolts", "conebolts", "conebolts1", "strafe", "hill", "hill-shared", "slate", "energy"];

    /// <summary>Named ruleset lookup, shared by the CLI's --rules flag and the server's
    /// BOTARENA_RULES eval knob. Experiment names carry visibly non-official version
    /// strings (they flow into replays and seed derivation); the held 0.3-slate
    /// mechanics layer on the shipped 0.3 for future re-tests.</summary>
    public static GameRules Resolve(string name) => name switch
    {
        "0.4" => V0_4,
        "0.3" => V0_3,
        "0.2" => V0_2,
        "0.1" => V0_1,
        // The 0.5 watchability slate (RULES-0.5-DESIGN), hardened revision v2 (§H,
        // DECISIONS #58): redacted hearing, both-checks bolt collision, computable
        // bolt timing on the wire, exhaustive fair spawns. The v1 strings
        // (0.5-exp-cone/-bolts/-conebolts/-control) are retired, not preserved —
        // experiments carry no bit-compat promise, and gen-6 artifacts cannot parse
        // the widened P section anyway (hill v1→v2 precedent: new behavior, new
        // string). 0.5-control is the spawn-matched A/B baseline (§H item 3);
        // conebolts1 is the §G counter-tune (bolts at movement speed).
        "0.5-control" => V0_4 with
        {
            RulesVersion = "0.5-exp-control-v2",
            ExhaustiveSpawns = true,
            ReplayZoneTallies = true,
        },
        "cone" => V0_4 with
        {
            RulesVersion = "0.5-exp-cone-v2",
            VisionCone = true,
            HearingRadius = 8,
            ExhaustiveSpawns = true,
            ReplayZoneTallies = true,
        },
        "bolts" => V0_4 with
        {
            RulesVersion = "0.5-exp-bolts-v2",
            ProjectileTicksPerTile = 2,
            ExhaustiveSpawns = true,
            ReplayZoneTallies = true,
        },
        "conebolts" => V0_4 with
        {
            RulesVersion = "0.5-exp-conebolts-v2",
            VisionCone = true,
            HearingRadius = 8,
            ProjectileTicksPerTile = 2,
            ExhaustiveSpawns = true,
            ReplayZoneTallies = true,
        },
        "conebolts1" => V0_4 with
        {
            RulesVersion = "0.5-exp-conebolts1-v2",
            VisionCone = true,
            HearingRadius = 8,
            ProjectileTicksPerTile = 1,
            ExhaustiveSpawns = true,
            ReplayZoneTallies = true,
        },
        "strafe" => V0_3 with { RulesVersion = "0.4-exp-strafe", AllowStrafe = true },
        // The hill experiment graduated to official 0.4 (DECISIONS #53); "hill" stays
        // as an alias so gen-4 project pins and scripts keep working. hill-shared
        // remains the shared-accrual A/B baseline (DECISIONS #50).
        "hill" => V0_4,
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
        _ => throw new ArgumentException($"Unknown rules '{name}' (use {string.Join(", ", KnownNames)})."),
    };
}
