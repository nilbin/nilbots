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

    /// <summary>When true, legacy per-bot zone-tick accrual is replaced by a shared
    /// signed control-pressure meter. A bot exerts control only by successfully
    /// validating Wait while alive on a zone tile; movement, turning, shooting,
    /// blocked actions, and faults do not hold. Positive pressure favors slot 0 and
    /// negative pressure favors slot 1.</summary>
    public bool ActiveZoneControl { get; init; }

    /// <summary>Absolute pressure required for a domination win. 0 disables the
    /// active-control meter even when <see cref="ActiveZoneControl"/> is true.</summary>
    public int ControlPressureLimit { get; init; }

    /// <summary>Pressure gained per tick by the sole active holder.</summary>
    public int ControlPressureGain { get; init; } = 1;

    /// <summary>When neither bot actively holds, non-zero pressure moves one point
    /// toward zero every N ticks. 0 disables decay.</summary>
    public int ControlPressureDecayInterval { get; init; }

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

    /// <summary>Loud events (Shot/Damage/Destroyed) beyond sight arrive within this
    /// Chebyshev radius as REDACTED sounds — type + bearing octant + distance band,
    /// never coordinates (RULES-0.5-DESIGN §A: the Decoy Shot needs out-of-cone
    /// signals, not a radar). 0 = off; quiet events stay sight-gated always.
    /// Disqualification is not loud: it has no world position and ends the match.</summary>
    public int HearingRadius { get; init; }

    /// <summary>Namespace for seed-derived randomness (spawn selection AND per-bot RNG
    /// streams); null = the RulesVersion, the historical behavior. Experiment arms share
    /// one profile so the SAME map + seed gives every arm identical spawns and identical
    /// bot streams — paired A/B rows then differ ONLY by the tested mechanic (a
    /// mechanics-blind bot plays bit-identical games across arms). External review
    /// finding: version-salted spawn seeds silently unpaired the harness.</summary>
    public string? SeedProfile { get; init; }

    /// <summary>Projectile travel (RULES-0.5-DESIGN §B): a shot spawns a bolt that
    /// occupies its tile (lethal to non-owners standing on or entering it) and advances
    /// every this-many ticks, taking <see cref="ProjectileTilesPerAdvance"/> ordered
    /// tile substeps, and despawning on walls or after ShotRange tiles.
    /// 0 = instant rays (legacy).</summary>
    public int ProjectileTicksPerTile { get; init; }

    /// <summary>Ordered tile substeps resolved whenever a projectile advances.
    /// Every intermediate tile checks walls, range, and victims, preventing tunnelling.
    /// Defaults to 1 so the gen-7 cadence arms retain their behavior.</summary>
    public int ProjectileTilesPerAdvance { get; init; } = 1;

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
        ["0.4", "0.3", "0.2", "0.1",
         "control", "cone-control", "cone-active", "cone-active-bolt1", "cone-active-bolt2",
         "0.5-control", "cone", "bolts", "conebolts", "conebolts1",
         "strafe", "hill", "hill-shared", "slate", "energy"];

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
        // Gen-7 redesign arms (RULES-0.5-DESIGN §J / DECISIONS #62). They share
        // seeds and differ only in the mechanic named by the arm. `control` and
        // `cone-control` retain passive 0.4 scoring; the active arms replace it
        // with successful-Wait holding and a decaying signed pressure meter.
        "control" => RedesignBase("0.5-exp-control-v4"),
        "cone-control" => RedesignBase("0.5-exp-cone-control-v4") with
        {
            VisionCone = true,
            HearingRadius = 8,
        },
        "cone-active" => ActiveControlBase("0.5-exp-cone-active-v4"),
        "cone-active-bolt1" => ActiveControlBase("0.5-exp-cone-active-bolt1-v4") with
        {
            ProjectileTicksPerTile = 1,
            ProjectileTilesPerAdvance = 1,
        },
        "cone-active-bolt2" => ActiveControlBase("0.5-exp-cone-active-bolt2-v4") with
        {
            ProjectileTicksPerTile = 1,
            ProjectileTilesPerAdvance = 2,
        },
        // The 0.5 watchability slate (RULES-0.5-DESIGN), hardened revision v3 (§H,
        // DECISIONS #58-#60): redacted hearing, both-checks bolt collision, computable
        // bolt timing on the wire, exhaustive fair spawns, and a SHARED seed profile —
        // same map + seed gives every arm identical spawns and bot streams, so paired
        // A/B rows differ only by the tested mechanic. Earlier revision strings are
        // retired, not preserved — experiments carry no bit-compat promise (hill
        // v1→v2 precedent: new behavior, new string). 0.5-control is the matched
        // baseline; conebolts1 is the §G counter-tune (bolts at movement speed).
        "0.5-control" => HardenedBase("0.5-exp-control-v3"),
        "cone" => HardenedBase("0.5-exp-cone-v3") with
        {
            VisionCone = true,
            HearingRadius = 8,
        },
        "bolts" => HardenedBase("0.5-exp-bolts-v3") with
        {
            ProjectileTicksPerTile = 2,
        },
        "conebolts" => HardenedBase("0.5-exp-conebolts-v3") with
        {
            VisionCone = true,
            HearingRadius = 8,
            ProjectileTicksPerTile = 2,
        },
        "conebolts1" => HardenedBase("0.5-exp-conebolts1-v3") with
        {
            VisionCone = true,
            HearingRadius = 8,
            ProjectileTicksPerTile = 1,
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

    /// <summary>The shared foundation of every hardened 0.5 arm — one place for the
    /// cross-arm invariants, so no arm can silently drop the seed profile that keeps
    /// the A/B harness paired.</summary>
    private static GameRules HardenedBase(string rulesVersion) => V0_4 with
    {
        RulesVersion = rulesVersion,
        SeedProfile = "0.5-exp-shared",
        ExhaustiveSpawns = true,
        ReplayZoneTallies = true,
    };

    private static GameRules RedesignBase(string rulesVersion) => V0_4 with
    {
        RulesVersion = rulesVersion,
        SeedProfile = "0.5-redesign-shared",
        ExhaustiveSpawns = true,
        ReplayZoneTallies = true,
    };

    private static GameRules ActiveControlBase(string rulesVersion) => RedesignBase(rulesVersion) with
    {
        VisionCone = true,
        HearingRadius = 8,
        ActiveZoneControl = true,
        ZoneDominationTicks = 0,
        ControlPressureLimit = 100,
        ControlPressureGain = 1,
        ControlPressureDecayInterval = 2,
    };
}
