namespace BotArena.Engine;

/// <summary>
/// Registered Arc Relay loop profiles. H0 remains the historical engine
/// default; hosted product callers use <see cref="Current"/> and experiments
/// select a named immutable profile rather than injecting numbers.
/// </summary>
public sealed record ArcRelayLoopProfile
{
    private ArcRelayLoopProfile(
        string id,
        string rulesetId,
        string mapId,
        ArcRelayMapGeometry geometry,
        int respawnDelayTicks,
        int wellCadenceTicks,
        int firstGlobalBeatTicks,
        int scheduledBirthRounds,
        bool directionalCombat = false,
        int signatureGrammarVersion = 1,
        int wellBirthJitterTicks = 0,
        bool alternatingResolutionOrder = false,
        bool threefoldSockets = false,
        int coresPerPulse = 3,
        int coreBaseValue = 1,
        int ripenIntervalTicks = 0,
        int ripenMaxValue = 0,
        int ripenResumeTicks = 0,
        int rearArcDamageMultiplier = 1,
        int omniProximityRange = 1,
        int veterancyXpPerLevel = 0,
        int veterancyMaxLevel = 0,
        int healZoneTicksPerHp = 0,
        bool seedPhasedResolutionOrder = false,
        bool rotationalSpawnAssignment = false,
        bool seedPhasedWellLead = false,
        bool chiralWellBirths = false,
        int strikeWindupTicks = 0,
        bool followingVacatedActorAllowed = false,
        bool teamRelativeCoreCharge = false,
        bool veteranCommand = false,
        int respawnWavePeriodTicks = 0,
        int respawnMinimumDeadTicks = 0,
        int minimumDistinctClasses = 0,
        bool durableDeployments = false,
        int combatMagnitudeScale = 1,
        int bodyHullBonus = 0,
        int centreOpeningDelayTicks = 0,
        int arcTossTellTicks = 1,
        int arcTossTravelTilesPerTick = 2,
        ArcRelaySignatureDefinition.ActivationTimingKind
            arcTossActivationTiming = ArcRelaySignatureDefinition
                .ActivationTimingKind.BeforeCombat,
        int bankTriggeredWellRechargeTicks = 0,
        int visionRange = 7,
        int shortAttackRange = 4,
        int mediumAttackRange = 6,
        int longAttackRange = 9,
        bool unifiedCombatStrafeMovement = false,
        int swiftMoveOpportunitiesPerCycle = 1,
        int swiftMoveCycleTicks = 1,
        int standardMoveOpportunitiesPerCycle = 1,
        int standardMoveCycleTicks = 1,
        int deliberateMoveOpportunitiesPerCycle = 1,
        int deliberateMoveCycleTicks = 1,
        bool movingPrismScreen = false,
        bool contextualCombatStrafeMovement = false,
        bool sentryTeamVision = false,
        bool reactiveImpactFacing = false,
        int veterancyDeathRetentionPercent = 0,
        bool alliedProjectilesBlock = false,
        bool veterancyWorkshops = false,
        int veterancyWorkshopDormancyTicks = 45)
    {
        if (combatMagnitudeScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(combatMagnitudeScale));
        if (bodyHullBonus < 0)
            throw new ArgumentOutOfRangeException(nameof(bodyHullBonus));
        if (centreOpeningDelayTicks < 0)
            throw new ArgumentOutOfRangeException(nameof(centreOpeningDelayTicks));
        if (arcTossTellTicks < 0)
            throw new ArgumentOutOfRangeException(nameof(arcTossTellTicks));
        if (arcTossTravelTilesPerTick <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(arcTossTravelTilesPerTick));
        if (bankTriggeredWellRechargeTicks < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bankTriggeredWellRechargeTicks));
        }
        if (visionRange <= 0)
            throw new ArgumentOutOfRangeException(nameof(visionRange));
        if (shortAttackRange <= 0)
            throw new ArgumentOutOfRangeException(nameof(shortAttackRange));
        if (mediumAttackRange < shortAttackRange)
            throw new ArgumentOutOfRangeException(nameof(mediumAttackRange));
        if (longAttackRange < mediumAttackRange)
            throw new ArgumentOutOfRangeException(nameof(longAttackRange));
        ValidateMovementRate(
            swiftMoveOpportunitiesPerCycle,
            swiftMoveCycleTicks,
            nameof(swiftMoveOpportunitiesPerCycle));
        ValidateMovementRate(
            standardMoveOpportunitiesPerCycle,
            standardMoveCycleTicks,
            nameof(standardMoveOpportunitiesPerCycle));
        ValidateMovementRate(
            deliberateMoveOpportunitiesPerCycle,
            deliberateMoveCycleTicks,
            nameof(deliberateMoveOpportunitiesPerCycle));
        CombatMagnitudeScale = combatMagnitudeScale;
        BodyHullBonus = bodyHullBonus;
        CentreOpeningDelayTicks = centreOpeningDelayTicks;
        ArcTossTellTicks = arcTossTellTicks;
        ArcTossTravelTilesPerTick = arcTossTravelTilesPerTick;
        ArcTossActivationTiming = arcTossActivationTiming;
        BankTriggeredWellRechargeTicks = bankTriggeredWellRechargeTicks;
        VisionRange = visionRange;
        ShortAttackRange = shortAttackRange;
        MediumAttackRange = mediumAttackRange;
        LongAttackRange = longAttackRange;
        UnifiedCombatStrafeMovement = unifiedCombatStrafeMovement;
        SwiftMoveOpportunitiesPerCycle = swiftMoveOpportunitiesPerCycle;
        SwiftMoveCycleTicks = swiftMoveCycleTicks;
        StandardMoveOpportunitiesPerCycle = standardMoveOpportunitiesPerCycle;
        StandardMoveCycleTicks = standardMoveCycleTicks;
        DeliberateMoveOpportunitiesPerCycle =
            deliberateMoveOpportunitiesPerCycle;
        DeliberateMoveCycleTicks = deliberateMoveCycleTicks;
        MovingPrismScreen = movingPrismScreen;
        ContextualCombatStrafeMovement = contextualCombatStrafeMovement;
        SentryTeamVision = sentryTeamVision;
        ReactiveImpactFacing = reactiveImpactFacing;
        if (veterancyDeathRetentionPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(veterancyDeathRetentionPercent));
        }
        VeterancyDeathRetentionPercent = veterancyDeathRetentionPercent;
        AlliedProjectilesBlock = alliedProjectilesBlock;
        VeterancyWorkshops = veterancyWorkshops;
        if (veterancyWorkshopDormancyTicks < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(veterancyWorkshopDormancyTicks));
        }
        VeterancyWorkshopDormancyTicks =
            veterancyWorkshopDormancyTicks;
        TeamRelativeCoreCharge = teamRelativeCoreCharge;
        VeteranCommand = veteranCommand;
        RespawnWavePeriodTicks = respawnWavePeriodTicks;
        RespawnMinimumDeadTicks = respawnMinimumDeadTicks;
        MinimumDistinctClasses = minimumDistinctClasses;
        DurableDeployments = durableDeployments;
        RotationalSpawnAssignment = rotationalSpawnAssignment;
        SeedPhasedWellLead = seedPhasedWellLead;
        ChiralWellBirths = chiralWellBirths;
        StrikeWindupTicks = strikeWindupTicks;
        FollowingVacatedActorAllowed = followingVacatedActorAllowed;
        SeedPhasedResolutionOrder = seedPhasedResolutionOrder;
        RearArcDamageMultiplier = rearArcDamageMultiplier;
        OmniProximityRange = omniProximityRange;
        VeterancyXpPerLevel = veterancyXpPerLevel;
        VeterancyMaxLevel = veterancyMaxLevel;
        HealZoneTicksPerHp = healZoneTicksPerHp;
        SignatureGrammarVersion = signatureGrammarVersion;
        WellBirthJitterTicks = wellBirthJitterTicks;
        AlternatingResolutionOrder = alternatingResolutionOrder;
        ThreefoldSockets = threefoldSockets;
        CoresPerPulse = coresPerPulse;
        CoreBaseValue = coreBaseValue;
        RipenIntervalTicks = ripenIntervalTicks;
        RipenMaxValue = ripenMaxValue;
        RipenResumeTicks = ripenResumeTicks;
        Id = id;
        RulesetId = rulesetId;
        MapId = mapId;
        Geometry = geometry;
        RespawnDelayTicks = respawnDelayTicks;
        WellCadenceTicks = wellCadenceTicks;
        FirstGlobalBeatTicks = firstGlobalBeatTicks;
        ScheduledBirthRounds = scheduledBirthRounds;
        DirectionalCombat = directionalCombat;
    }

    public string Id { get; }
    public string RulesetId { get; }
    public string MapId { get; }
    internal ArcRelayMapGeometry Geometry { get; }
    public int RespawnDelayTicks { get; }
    public int WellCadenceTicks { get; }
    public int FirstGlobalBeatTicks { get; }
    public int ScheduledBirthRounds { get; }
    internal bool DirectionalCombat { get; }
    internal int SignatureGrammarVersion { get; }
    internal int WellBirthJitterTicks { get; }
    internal bool AlternatingResolutionOrder { get; }
    internal bool ThreefoldSockets { get; }
    internal int CoresPerPulse { get; }
    internal int CoreBaseValue { get; }
    internal int RipenIntervalTicks { get; }
    internal int RipenMaxValue { get; }
    internal int RipenResumeTicks { get; }
    internal int RearArcDamageMultiplier { get; }
    internal int OmniProximityRange { get; }
    internal int VeterancyXpPerLevel { get; }
    internal int VeterancyMaxLevel { get; }
    internal int VeterancyDeathRetentionPercent { get; }
    internal int HealZoneTicksPerHp { get; }
    internal bool SeedPhasedResolutionOrder { get; }
    internal bool RotationalSpawnAssignment { get; }
    internal bool SeedPhasedWellLead { get; }

    internal bool ChiralWellBirths { get; }

    /// <summary>
    /// Positional combat (DECISIONS #212): when positive, every class gun is
    /// a declared instant ray with this windup instead of a dodgeable
    /// discrete bolt. Zero keeps historical projectile combat.
    /// </summary>
    internal int StrikeWindupTicks { get; }

    /// <summary>
    /// Whether a joint move may advance an acyclic chain into a pre-tick
    /// vacancy. Versioned on the loop profile because it changes canonical
    /// rules and gameplay; historical profiles remain byte-exact.
    /// </summary>
    internal bool FollowingVacatedActorAllowed { get; }

    /// <summary>Experimental Tapline depth-pack capability switches.</summary>
    internal bool TeamRelativeCoreCharge { get; }
    internal bool VeteranCommand { get; }
    internal int RespawnWavePeriodTicks { get; }
    internal int RespawnMinimumDeadTicks { get; }
    internal int MinimumDistinctClasses { get; }
    internal bool DurableDeployments { get; }
    internal bool AlliedProjectilesBlock { get; }
    internal int CombatMagnitudeScale { get; }
    internal int BodyHullBonus { get; }
    internal int CentreOpeningDelayTicks { get; }
    internal int ArcTossTellTicks { get; }
    internal int ArcTossTravelTilesPerTick { get; }
    internal ArcRelaySignatureDefinition.ActivationTimingKind
        ArcTossActivationTiming { get; }

    /// <summary>
    /// When positive, only each Well's initial schedule is global. Banking
    /// that Well's one active Core starts this public source-local recharge;
    /// every historical profile keeps zero and its scheduled-beat semantics.
    /// </summary>
    internal int BankTriggeredWellRechargeTicks { get; }

    /// <summary>
    /// Versioned perception and basic-gun reach. Defaults retain the exact
    /// historical 7 / 4 / 6 / 9 contract; promoted profiles may tune the
    /// spatial combat envelope without rewriting prior replay identities.
    /// </summary>
    internal int VisionRange { get; }
    internal int ShortAttackRange { get; }
    internal int MediumAttackRange { get; }
    internal int LongAttackRange { get; }

    /// <summary>
    /// Versioned locomotion contract. Historical profiles retain their three
    /// distinct facing grammars and universal 1/1 travel. A promoted profile
    /// may instead give every body the same CombatStrafe controls and make
    /// swift/standard/deliberate honest movement rates.
    /// </summary>
    internal bool UnifiedCombatStrafeMovement { get; }
    internal int SwiftMoveOpportunitiesPerCycle { get; }
    internal int SwiftMoveCycleTicks { get; }
    internal int StandardMoveOpportunitiesPerCycle { get; }
    internal int StandardMoveCycleTicks { get; }
    internal int DeliberateMoveOpportunitiesPerCycle { get; }
    internal int DeliberateMoveCycleTicks { get; }
    internal bool MovingPrismScreen { get; }

    /// <summary>
    /// Ordinary movement faces its route heading while a separately declared
    /// strafe action lets an actively engaged body preserve its firing arc.
    /// This keeps navigation readable without removing combat footwork.
    /// </summary>
    internal bool ContextualCombatStrafeMovement { get; }

    /// <summary>
    /// Active Sentinel Seeds contribute their weapon-range omnidirectional
    /// sight to the owning team's observable union.
    /// </summary>
    internal bool SentryTeamVision { get; }

    /// <summary>
    /// Surviving projectile damage from a team-unseen attacker turns a body
    /// toward the strongest blind impact before its next decision.
    /// </summary>
    internal bool ReactiveImpactFacing { get; }
    internal bool VeterancyWorkshops { get; }

    /// <summary>
    /// Whole ticks a Workshop remains unavailable after a completed channel.
    /// Historical Broadloom profiles use 45; zero allows the next qualifying
    /// tick to begin the following channel immediately.
    /// </summary>
    internal int VeterancyWorkshopDormancyTicks { get; }

    private static void ValidateMovementRate(
        int opportunities,
        int cycleTicks,
        string parameterName)
    {
        if (opportunities <= 0 || cycleTicks <= 0
            || opportunities > cycleTicks)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Movement rates require 0 < opportunities <= cycle ticks.");
        }
    }

    public static ArcRelayLoopProfile H0 { get; } = new(
        "h0",
        ArcRelayH0Definition.RulesetId,
        ArcRelayH0Definition.MapId,
        ArcRelayMapGeometry.H0,
        respawnDelayTicks: 20,
        wellCadenceTicks: 75,
        firstGlobalBeatTicks: 25,
        scheduledBirthRounds: 7);

    public static ArcRelayLoopProfile HomeGatesWide { get; } = new(
        "home-gates-wide",
        ArcRelayH0Definition.RulesetId,
        "arc-relay-threefold-home-gates-wide-01",
        ArcRelayMapGeometry.HomeGatesWide,
        20,
        75,
        25,
        7);

    public static ArcRelayLoopProfile HomeGatesThree { get; } = new(
        "home-gates-three",
        ArcRelayH0Definition.RulesetId,
        "arc-relay-threefold-home-gates-three-01",
        ArcRelayMapGeometry.HomeGatesThree,
        20,
        75,
        25,
        7);

    public static ArcRelayLoopProfile HomeConcourse { get; } = new(
        "home-concourse",
        ArcRelayH0Definition.RulesetId,
        "arc-relay-threefold-home-concourse-01",
        ArcRelayMapGeometry.HomeConcourse,
        20,
        75,
        25,
        7);

    public static ArcRelayLoopProfile CoverTrim { get; } = new(
        "cover-trim",
        ArcRelayH0Definition.RulesetId,
        "arc-relay-threefold-cover-trim-01",
        ArcRelayMapGeometry.CoverTrim,
        20,
        75,
        25,
        7);

    /// <summary>
    /// Design-study arm: the Threefold relationships are retained on a
    /// 31-by-29 field, inside the frozen 32-by-32 engine ceiling. Rules and
    /// cadence remain H0.
    /// </summary>
    public static ArcRelayLoopProfile DepthLarger { get; } = new(
        "depth-larger",
        ArcRelayH0Definition.RulesetId,
        "arc-relay-threefold-depth-larger-01",
        ArcRelayMapGeometry.DepthLarger,
        20,
        75,
        25,
        7);

    /// <summary>
    /// Design-study arm: unequal north/south lane character with exact
    /// 180-degree fairness. Rules, dimensions, objectives, and cover count
    /// remain the home-gates-wide baseline.
    /// </summary>
    public static ArcRelayLoopProfile DepthCounterflow { get; } = new(
        "depth-counterflow",
        ArcRelayH0Definition.RulesetId,
        "arc-relay-threefold-depth-counterflow-01",
        ArcRelayMapGeometry.DepthCounterflow,
        20,
        75,
        25,
        7);

    /// <summary>
    /// Owner-approved combat-facing rules on the accepted Counterflow map.
    /// Kept beside the historical profile so prior contracts and replay hashes
    /// remain executable byte-for-byte.
    /// </summary>
    public static ArcRelayLoopProfile ForwardCombat { get; } = new(
        "forward-combat",
        "arc-relay-forward-combat-01",
        "arc-relay-threefold-depth-counterflow-01",
        ArcRelayMapGeometry.DepthCounterflow,
        20,
        75,
        25,
        7,
        directionalCombat: true);

    /// <summary>
    /// Signature grammar 2 on the accepted Counterflow combat rules (owner
    /// ruling 2026-08-05): dodgeable sentinel and hook bolts, telegraphed
    /// null-field, contract-projected signature metadata. Kept beside the
    /// grammar-1 profile so prior contracts and replay hashes remain
    /// executable byte-for-byte.
    /// </summary>
    public static ArcRelayLoopProfile ForwardCombat2 { get; } = new(
        "forward-combat-2",
        "arc-relay-forward-combat-02",
        "arc-relay-threefold-depth-counterflow-01",
        ArcRelayMapGeometry.DepthCounterflow,
        20,
        75,
        25,
        7,
        directionalCombat: true,
        signatureGrammarVersion: 2);

    /// <summary>
    /// Robust-play foundations (owner goal 2026-08-05): grammar-2 combat
    /// plus seed-derived well-birth jitter, so distinct seeds produce
    /// genuinely distinct games. Kept beside -02 so prior contracts and
    /// replay hashes remain executable byte-for-byte.
    /// </summary>
    public static ArcRelayLoopProfile ForwardCombat3 { get; } = new(
        "forward-combat-3",
        "arc-relay-forward-combat-03",
        "arc-relay-threefold-depth-counterflow-01",
        ArcRelayMapGeometry.DepthCounterflow,
        20,
        75,
        25,
        7,
        directionalCombat: true,
        signatureGrammarVersion: 2,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true);

    /// <summary>
    /// Threefold Pulse depth prototype (owner brief 2026-08-05,
    /// docs/briefs/THREEFOLD-PULSE-PROTOTYPE-BRIEF.md): the -03 foundations
    /// plus per-origin reactor sockets — a Pulse requires one banked Core
    /// from each Well. Experimental only; never the hosted/current profile.
    /// </summary>
    public static ArcRelayLoopProfile ThreefoldPulse { get; } = new(
        "threefold-pulse",
        "arc-relay-threefold-01",
        "arc-relay-threefold-depth-counterflow-01",
        ArcRelayMapGeometry.DepthCounterflow,
        20,
        75,
        25,
        7,
        directionalCombat: true,
        signatureGrammarVersion: 2,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        threefoldSockets: true);

    /// <summary>
    /// Charge-value control arm (owner direction 2026-08-05): the -03
    /// foundations with Cores worth 2 and a Pulse at 6 — three base Cores
    /// per Pulse, so the primitive alone must be behaviorally inert.
    /// </summary>
    public static ArcRelayLoopProfile ChargeValueControl { get; } = new(
        "charge-value-control",
        "arc-relay-charge-value-01",
        "arc-relay-threefold-depth-counterflow-01",
        ArcRelayMapGeometry.DepthCounterflow,
        20,
        75,
        25,
        7,
        directionalCombat: true,
        signatureGrammarVersion: 2,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        coresPerPulse: 6,
        coreBaseValue: 2);

    /// <summary>
    /// Ripening Cores depth prototype (owner direction 2026-08-05, depth
    /// memo #1, docs/briefs/RIPENING-CORES-PROTOTYPE-BRIEF.md): the
    /// charge-value primitive plus +1 value per 45 loose ticks, cap 4,
    /// freeze on pickup, 20-tick resumption after drops. Experimental only.
    /// </summary>
    public static ArcRelayLoopProfile RipeningCores { get; } = new(
        "ripening-cores",
        "arc-relay-ripening-01",
        "arc-relay-threefold-depth-counterflow-01",
        ArcRelayMapGeometry.DepthCounterflow,
        20,
        75,
        25,
        7,
        directionalCombat: true,
        signatureGrammarVersion: 2,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        coresPerPulse: 6,
        coreBaseValue: 2,
        ripenIntervalTicks: 45,
        ripenMaxValue: 4,
        ripenResumeTicks: 20);

    /// <summary>
    /// Tuned ripening (owner direction 2026-08-05 after the -01 REJECT):
    /// the accrual rate must sit near the well-cycle rate for patience to
    /// be a real choice. +1 per 12 loose ticks (1/12 per tick vs ~2/26
    /// cycling), cap 4, 8-tick resumption so contested standoffs still
    /// escalate. Minted beside -01; experimental only.
    /// </summary>
    public static ArcRelayLoopProfile RipeningCores2 { get; } = new(
        "ripening-cores-2",
        "arc-relay-ripening-02",
        "arc-relay-threefold-depth-counterflow-01",
        ArcRelayMapGeometry.DepthCounterflow,
        20,
        75,
        25,
        7,
        directionalCombat: true,
        signatureGrammarVersion: 2,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        coresPerPulse: 6,
        coreBaseValue: 2,
        ripenIntervalTicks: 12,
        ripenMaxValue: 4,
        ripenResumeTicks: 8);

    /// <summary>
    /// Ambush terrain prototype (owner direction 2026-08-05): the counterflow
    /// map plus chiral-paired sightline breakers and dead-end alcoves, so the
    /// facing-quadrant vision and team-union projectile visibility actually
    /// bite. Forward Combat 03 semantics otherwise; experimental only.
    /// </summary>
    public static ArcRelayLoopProfile AmbushWarren { get; } = new(
        "ambush-warren",
        "arc-relay-ambush-01",
        "arc-relay-ambush-warren-01",
        ArcRelayMapGeometry.AmbushWarren,
        20,
        75,
        25,
        7,
        directionalCombat: true,
        signatureGrammarVersion: 2,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true);

    /// <summary>
    /// Predation rules (owner direction 2026-08-05) on the warren terrain:
    /// front-only vision (no omnidirectional proximity ring — the quadrant
    /// stays, the sideways question stays an open knob), double damage from
    /// the victim's blind rear arc, and a costlier death. Together: flanking
    /// skill decides fights, dying matters, awareness is earned.
    /// </summary>
    public static ArcRelayLoopProfile AmbushWarren2 { get; } = new(
        "ambush-warren-2",
        "arc-relay-ambush-02",
        "arc-relay-ambush-warren-01",
        ArcRelayMapGeometry.AmbushWarren,
        30,
        75,
        25,
        7,
        directionalCombat: true,
        signatureGrammarVersion: 2,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0);

    /// <summary>
    /// Denser warren (owner review 2026-08-05: the map still played too
    /// open). Same predation rules as -02; the map roughly doubles the
    /// added wall mass — every horizontal corridor breaks, north-south
    /// transit weaves, alcoves and hooks stay. Chiral pairs throughout.
    /// </summary>
    public static ArcRelayLoopProfile AmbushWarren3 { get; } = new(
        "ambush-warren-3",
        "arc-relay-ambush-03",
        "arc-relay-ambush-warren-02",
        ArcRelayMapGeometry.AmbushWarrenDense,
        30,
        75,
        25,
        7,
        directionalCombat: true,
        signatureGrammarVersion: 2,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0);

    /// <summary>
    /// Serpentine warren (owner review 2026-08-05: well-to-base returns
    /// still ran straight). Adds single-tile return chokes at (7,11) and
    /// (23,11), lane serpentines, a south split-lane divider, and centre
    /// orbit blockers - returns now funnel through known squeeze tiles.
    /// Chiral pairs throughout; predation rules unchanged from -02/-03.
    /// </summary>
    public static ArcRelayLoopProfile AmbushWarren4 { get; } = new(
        "ambush-warren-4",
        "arc-relay-ambush-04",
        "arc-relay-ambush-warren-03",
        ArcRelayMapGeometry.AmbushWarrenSerpentine,
        30,
        75,
        25,
        7,
        directionalCombat: true,
        signatureGrammarVersion: 2,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0);

    /// <summary>
    /// Veterancy and heal zones (owner direction 2026-08-05) on the
    /// serpentine predation world: bodies start at level 1, earn XP per
    /// kill with a bounty for high-level victims, allocate skill points via
    /// the invest action (damage / vision / reach / vitality), lose it all
    /// on death, and can channel 1 health per 3 waited ticks on the
    /// contested midline heal tiles. Experimental only.
    /// </summary>
    public static ArcRelayLoopProfile AmbushWarren5 { get; } = new(
        "ambush-warren-5",
        "arc-relay-ambush-05",
        "arc-relay-ambush-warren-04",
        ArcRelayMapGeometry.AmbushWarrenSerpentine,
        30,
        75,
        25,
        7,
        directionalCombat: true,
        signatureGrammarVersion: 2,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 3,
        healZoneTicksPerHp: 3);

    /// <summary>
    /// Fair-alternation veterancy world (west-residual hunt round 2): the
    /// -05 stack plus seed-phased resolution parity, so the scripted
    /// opening collision - and every other symmetric contest at a fixed
    /// tick - splits evenly across seeds instead of always resolving west.
    /// </summary>
    public static ArcRelayLoopProfile AmbushWarren6 { get; } = new(
        "ambush-warren-6",
        "arc-relay-ambush-06",
        "arc-relay-ambush-warren-04",
        ArcRelayMapGeometry.AmbushWarrenSerpentine,
        30,
        75,
        25,
        7,
        directionalCombat: true,
        signatureGrammarVersion: 2,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 3,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true);

    /// <summary>
    /// Rotationally fair deployment (west-residual hunt round 3): the -06
    /// stack on a re-anchored serpentine map. The warren maps are 180-degree
    /// chiral, but team 1 spawns were still assigned by X-flip, so each
    /// eastern class stood in terrain inequivalent to its western
    /// counterpart's and no mind-side mirroring could ever be exact. Team 1
    /// unit N now spawns at the 180-degree rotation of team 0 unit N's
    /// anchor (the anchor tile set is unchanged - only eastern unit
    /// indexing moves), so identical minds mirroring by rotation play truly
    /// symmetric openings. Experimental only.
    /// </summary>
    public static ArcRelayLoopProfile AmbushWarren7 { get; } = new(
        "ambush-warren-7",
        "arc-relay-ambush-07",
        "arc-relay-ambush-warren-05",
        ArcRelayMapGeometry.AmbushWarrenSerpentine,
        30,
        75,
        25,
        7,
        directionalCombat: true,
        signatureGrammarVersion: 2,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 3,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true);

    /// <summary>
    /// Fair well lead (west-residual hunt round 4): the -07 stack plus a
    /// seed-derived swap of the north/south well schedules. The flank
    /// stagger (north births 25 ticks before south) otherwise hands the
    /// same canonical sheet different value by side on the chiral map -
    /// the wing that meets the first-born well converts tempo - which the
    /// wing-symmetric attribution probe isolated as the last structural
    /// residual. Experimental only.
    /// </summary>
    public static ArcRelayLoopProfile AmbushWarren8 { get; } = new(
        "ambush-warren-8",
        "arc-relay-ambush-08",
        "arc-relay-ambush-warren-05",
        ArcRelayMapGeometry.AmbushWarrenSerpentine,
        30,
        75,
        25,
        7,
        directionalCombat: true,
        signatureGrammarVersion: 2,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 3,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        seedPhasedWellLead: true);

    /// <summary>
    /// The apex arm (owner direction 2026-08-06): the -08 fairness stack
    /// with the veterancy ceiling raised to 5 so a dedicated hunter can
    /// out-level the field. The damage track stays capped at +3 by the
    /// invest rules, so levels beyond 4 buy vision, reach, or vitality;
    /// vitality headroom follows the ceiling automatically. Experimental
    /// only.
    /// </summary>
    public static ArcRelayLoopProfile AmbushWarren9 { get; } = new(
        "ambush-warren-9",
        "arc-relay-ambush-09",
        "arc-relay-ambush-warren-05",
        ArcRelayMapGeometry.AmbushWarrenSerpentine,
        30,
        75,
        25,
        7,
        directionalCombat: true,
        signatureGrammarVersion: 2,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        seedPhasedWellLead: true);

    /// <summary>
    /// The deep warren (owner map ruling 2026-08-06): the -09 apex stack on
    /// a 31x27 canvas that wraps the serpentine interior in true shadow
    /// lanes - an open perimeter corridor top and bottom behind a
    /// three-gate screen wall - so a hunting pack can transit the map
    /// without crossing well country. Experimental only.
    /// </summary>
    public static ArcRelayLoopProfile AmbushWarren10 { get; } = new(
        "ambush-warren-10",
        "arc-relay-ambush-10",
        "arc-relay-ambush-warren-06",
        ArcRelayMapGeometry.AmbushWarrenDeep,
        30,
        75,
        25,
        7,
        directionalCombat: true,
        signatureGrammarVersion: 2,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        seedPhasedWellLead: true);

    /// <summary>
    /// The positional combat remake (DECISIONS #212): the -10 deep warren
    /// with every gun a declared strike — announce the ray, hold two ticks,
    /// hit the first body still standing on it. Fights resolve from the
    /// board; the dodge duel is unrepresentable. Experimental only.
    /// </summary>
    public static ArcRelayLoopProfile AmbushWarren11 { get; } = new(
        "ambush-warren-11",
        "arc-relay-ambush-11",
        "arc-relay-ambush-warren-06",
        ArcRelayMapGeometry.AmbushWarrenDeep,
        30,
        75,
        25,
        7,
        directionalCombat: true,
        signatureGrammarVersion: 2,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        seedPhasedWellLead: true,
        strikeWindupTicks: 1);

    /// <summary>
    /// Crucible (owner map direction 2026-08-09): the -11 positional stack
    /// on a diagonal arena. Bases sit in opposite corners, so the contested
    /// line runs north-west to south-east and the map finally has TERRITORY
    /// - a near half, a far half, and a middle - instead of one mirrored
    /// axis on which every Well is equally neutral.
    ///
    /// Three structural departures from the warren line, each aimed at a
    /// behaviour the warren could not produce:
    ///
    /// 1. Tiered Wells. Each side owns a near Well seven steps from its
    ///    reactor; the centre Well is thirteen from both and the enemy's
    ///    near Well is twenty. Safe income and contested income are now
    ///    different decisions rather than the same scramble.
    /// 2. The delivery zone is NOT the spawn pad. Scoring happens on an
    ///    open shoulder four steps forward of the corner; the protected
    ///    pad behind it holds the reactor and the respawns. Camping a
    ///    spawn no longer camps the objective, and defending the objective
    ///    means standing outside protection.
    /// 3. Heal tiles sit on the CROSS diagonal, six from the centre Well
    ///    and off the main lane, so withdrawing to heal concedes position
    ///    and can be contested.
    ///
    /// The canvas is exactly 180-degree chiral by construction: only the
    /// upper half and a self-symmetric middle row are authored and the
    /// lower half is their rotation, so the existing rotational spawn
    /// assignment stays exact. 501 open tiles, all mutually reachable.
    /// Experimental only.
    /// </summary>
    public static ArcRelayLoopProfile Crucible1 { get; } = new(
        "crucible-1",
        "arc-relay-crucible-01",
        "arc-relay-crucible-01",
        ArcRelayMapGeometry.CrucibleDiagonal,
        30,
        75,
        25,
        7,
        directionalCombat: true,
        signatureGrammarVersion: 2,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1);

    /// <summary>
    /// Crucible's tiering on a HORIZONTAL frame. Corner-to-corner made every
    /// route a diagonal; here the reactors sit on the west and east edges but
    /// at opposite heights, so the main lane runs across the map while still
    /// asking each side to give up ground vertically. Same skeleton: an owned
    /// near Well, a contested centre, a scoring shoulder that is not the
    /// spawn pad, and one spawn cluster high with the other low.
    /// </summary>
    public static ArcRelayLoopProfile Drawbench1 { get; } = new(
        "drawbench-1",
        "arc-relay-drawbench-01",
        "arc-relay-drawbench-01",
        ArcRelayMapGeometry.DrawbenchHorizontal,
        30,
        75,
        25,
        7,
        directionalCombat: true,
        signatureGrammarVersion: 2,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1);

    /// <summary>
    /// The owner's verdict after Crucible and Drawbench: the well-line warren
    /// had the better geometry. Tapline keeps that grammar -- the 31x27
    /// canvas, an outer shadow corridor with three ways in per side, chunky
    /// wall clusters, corridors four to six wide -- and changes only what the
    /// warren could not express. The three Wells sit on the chiral line
    /// through the centre, so they are TIERED rather than all thirteen steps
    /// from both bases; the scoring shoulder is not the spawn pad; and west
    /// enters high while east enters low, so the Well line runs along the
    /// axis of play with yours up-line and theirs down-line.
    /// </summary>
    public static ArcRelayLoopProfile Tapline1 { get; } = new(
        "tapline-1",
        "arc-relay-tapline-01",
        "arc-relay-tapline-01",
        ArcRelayMapGeometry.TaplineWellLine,
        30,
        75,
        25,
        7,
        directionalCombat: true,
        signatureGrammarVersion: 2,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1);

    /// <summary>
    /// The complete grammar-3 Tapline profile. All sixteen classes participate
    /// in one grammar and retain the accepted Tapline geometry; four signature
    /// envelopes are versioned where their historical chronology contradicted
    /// their public presentation: Null Field, Target Paint, Kinetic Burst and
    /// Smoke Canister. Tapline-1 remains frozen beside it.
    /// </summary>
    public static ArcRelayLoopProfile Tapline2 { get; } = new(
        "tapline-2",
        "arc-relay-tapline-02",
        "arc-relay-tapline-01",
        ArcRelayMapGeometry.TaplineWellLine,
        30,
        75,
        25,
        7,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1);

    /// <summary>
    /// Tapline grammar 3 plus atomic acyclic convoy movement. The map,
    /// signatures and cadence stay unchanged; only the explicit collision
    /// profile advances. Tapline-1 and Tapline-2 remain immutable beside it.
    /// </summary>
    public static ArcRelayLoopProfile Tapline3 { get; } = new(
        "tapline-3",
        "arc-relay-tapline-03",
        "arc-relay-tapline-01",
        ArcRelayMapGeometry.TaplineWellLine,
        30,
        75,
        25,
        7,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true);

    /// <summary>
    /// Tapline depth-pack control. This is deliberately the exact Tapline-3
    /// profile object rather than a copied ruleset, so isolated experiments
    /// always compare against the shipped candidate's canonical identity.
    /// </summary>
    public static ArcRelayLoopProfile TaplineDepthControl => Tapline3;

    public static ArcRelayLoopProfile TaplineDepthScore124 { get; } = new(
        "tapline-depth-score-124",
        "arc-relay-tapline-depth-score-124-01",
        "arc-relay-tapline-01",
        ArcRelayMapGeometry.TaplineWellLine,
        30,
        65,
        25,
        9,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true,
        teamRelativeCoreCharge: true);

    public static ArcRelayLoopProfile TaplineDepthVeteranCommand { get; } = new(
        "tapline-depth-veteran-command",
        "arc-relay-tapline-depth-veteran-command-01",
        "arc-relay-tapline-01",
        ArcRelayMapGeometry.TaplineWellLine,
        30, 75, 25, 7,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true,
        veteranCommand: true);

    public static ArcRelayLoopProfile TaplineDepthRespawnWave { get; } = new(
        "tapline-depth-respawn-wave",
        "arc-relay-tapline-depth-respawn-wave-01",
        "arc-relay-tapline-01",
        ArcRelayMapGeometry.TaplineWellLine,
        30, 75, 25, 7,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true,
        respawnWavePeriodTicks: 30,
        respawnMinimumDeadTicks: 15);

    public static ArcRelayLoopProfile TaplineDepthRosterWildcard { get; } = new(
        "tapline-depth-roster-wildcard",
        "arc-relay-tapline-depth-roster-wildcard-01",
        "arc-relay-tapline-01",
        ArcRelayMapGeometry.TaplineWellLine,
        30, 75, 25, 7,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true,
        minimumDistinctClasses: 7);

    public static ArcRelayLoopProfile TaplineDepthDurableDeployments { get; } = new(
        "tapline-depth-durable-deployments",
        "arc-relay-tapline-depth-durable-deployments-01",
        "arc-relay-tapline-01",
        ArcRelayMapGeometry.TaplineWellLine,
        30, 75, 25, 7,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true,
        durableDeployments: true);

    /// <summary>
    /// Experimental unit conversion: health, damage, and repair magnitudes
    /// use tenfold integers while level-1 hits-to-kill stay unchanged.
    /// </summary>
    public static ArcRelayLoopProfile TaplineDepthCombatScale10 { get; } = new(
        "tapline-depth-combat-scale-10",
        "arc-relay-tapline-depth-combat-scale-10-01",
        "arc-relay-tapline-01",
        ArcRelayMapGeometry.TaplineWellLine,
        30, 75, 25, 7,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true,
        combatMagnitudeScale: 10);

    /// <summary>
    /// Isolated durability question on the tenfold scale: every chassis
    /// takes exactly one additional level-1 gun hit to destroy.
    /// </summary>
    public static ArcRelayLoopProfile TaplineDepthDurabilityPlus10 { get; } = new(
        "tapline-depth-durability-plus-10",
        "arc-relay-tapline-depth-durability-plus-10-01",
        "arc-relay-tapline-01",
        ArcRelayMapGeometry.TaplineWellLine,
        30, 75, 25, 7,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true,
        combatMagnitudeScale: 10,
        bodyHullBonus: 10);

    public static ArcRelayLoopProfile TaplineDepthPack { get; } = new(
        "tapline-depth-pack",
        "arc-relay-tapline-depth-pack-02",
        "arc-relay-tapline-01",
        ArcRelayMapGeometry.TaplineWellLine,
        30, 65, 25, 9,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true,
        teamRelativeCoreCharge: true,
        veteranCommand: true,
        respawnWavePeriodTicks: 30,
        respawnMinimumDeadTicks: 15,
        minimumDistinctClasses: 7,
        durableDeployments: true,
        combatMagnitudeScale: 10);

    /// <summary>
    /// The owner-promoted Tapline depth game. Tapline-4 carries the combined
    /// 1/2/4 Core economy, veteran command, public respawn waves, seven-class
    /// roster floor, durable deployments, and tenfold combat scale under a
    /// product identity rather than an experiment label.
    /// </summary>
    public static ArcRelayLoopProfile Tapline4 { get; } = new(
        "tapline-4",
        "arc-relay-tapline-04",
        "arc-relay-tapline-01",
        ArcRelayMapGeometry.TaplineWellLine,
        30, 65, 25, 9,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true,
        teamRelativeCoreCharge: true,
        veteranCommand: true,
        respawnWavePeriodTicks: 30,
        respawnMinimumDeadTicks: 15,
        minimumDistinctClasses: 7,
        durableDeployments: true,
        combatMagnitudeScale: 10);

    /// <summary>
    /// Tapline-4 with a readable opening phase. The centre Well joins the two
    /// outer Wells on the second global beat, giving both teams time to scout
    /// and establish counter-position before the first two-charge Core exists.
    /// Later births keep the promoted 65-tick cadence and nine-round supply.
    /// </summary>
    public static ArcRelayLoopProfile Tapline5 { get; } = new(
        "tapline-5",
        "arc-relay-tapline-05",
        "arc-relay-tapline-01",
        ArcRelayMapGeometry.TaplineWellLine,
        30, 65, 25, 9,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true,
        teamRelativeCoreCharge: true,
        veteranCommand: true,
        respawnWavePeriodTicks: 30,
        respawnMinimumDeadTicks: 15,
        minimumDistinctClasses: 7,
        durableDeployments: true,
        combatMagnitudeScale: 10,
        centreOpeningDelayTicks: 25);

    /// <summary>
    /// The owner-approved equal-return Tapline. It keeps Tapline-5's complete
    /// rules stack and landmark positions, but closes the centre shortcut so
    /// the two practical centre-Core returns are both 19 terrain steps. The
    /// new map identity preserves every Tapline-5 replay and fingerprint.
    /// </summary>
    public static ArcRelayLoopProfile Tapline6 { get; } = new(
        "tapline-6",
        "arc-relay-tapline-06",
        "arc-relay-tapline-02",
        ArcRelayMapGeometry.TaplineEqualReturn,
        30, 65, 25, 9,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true,
        teamRelativeCoreCharge: true,
        veteranCommand: true,
        respawnWavePeriodTicks: 30,
        respawnMinimumDeadTicks: 15,
        minimumDistinctClasses: 7,
        durableDeployments: true,
        combatMagnitudeScale: 10,
        centreOpeningDelayTicks: 25);

    /// <summary>
    /// Tapline-6 with an immediate Arc Toss release and three-tile flight
    /// cadence. The Core enters flight on the accepted action tick; range and
    /// cooldown are unchanged. The new rules identity preserves every
    /// Tapline-6 match and hosted rating contract while the faster no-windup
    /// line is reviewed locally.
    /// </summary>
    public static ArcRelayLoopProfile Tapline7 { get; } = new(
        "tapline-7",
        "arc-relay-tapline-07",
        "arc-relay-tapline-02",
        ArcRelayMapGeometry.TaplineEqualReturn,
        30, 65, 25, 9,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true,
        teamRelativeCoreCharge: true,
        veteranCommand: true,
        respawnWavePeriodTicks: 30,
        respawnMinimumDeadTicks: 15,
        minimumDistinctClasses: 7,
        durableDeployments: true,
        combatMagnitudeScale: 10,
        centreOpeningDelayTicks: 25,
        arcTossTellTicks: 0,
        arcTossTravelTilesPerTick: 3,
        arcTossActivationTiming: ArcRelaySignatureDefinition
            .ActivationTimingKind.AfterCombat);

    /// <summary>
    /// Tapline-7 with source-local supply. Each Well keeps its existing
    /// seed-jittered opening, then owns exactly one active Core until that
    /// Core is banked into either reactor. Banking starts a public 46-tick
    /// recharge; carrying, dropping, stealing, and tossing do not.
    /// </summary>
    public static ArcRelayLoopProfile Tapline8 { get; } = new(
        "tapline-8",
        "arc-relay-tapline-08",
        "arc-relay-tapline-02",
        ArcRelayMapGeometry.TaplineEqualReturn,
        30, 65, 25, 1,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true,
        teamRelativeCoreCharge: true,
        veteranCommand: true,
        respawnWavePeriodTicks: 30,
        respawnMinimumDeadTicks: 15,
        minimumDistinctClasses: 7,
        durableDeployments: true,
        combatMagnitudeScale: 10,
        centreOpeningDelayTicks: 25,
        arcTossTellTicks: 0,
        arcTossTravelTilesPerTick: 3,
        arcTossActivationTiming: ArcRelaySignatureDefinition
            .ActivationTimingKind.AfterCombat,
        bankTriggeredWellRechargeTicks: 46);

    /// <summary>
    /// Tapline-8's complete rules stack on the owner-approved sealed outer-Well
    /// entrances with staggered Mid cover. Six symmetric walls force the
    /// deliberate long approach between Mid and either outer Well without
    /// moving any landmark or changing scoring, supply, combat, signatures,
    /// or the 600-tick horizon.
    /// </summary>
    public static ArcRelayLoopProfile Tapline9 { get; } = new(
        "tapline-9",
        "arc-relay-tapline-09",
        "arc-relay-tapline-03",
        ArcRelayMapGeometry.TaplineSealedEntrancesStaggeredMidCover,
        30, 65, 25, 1,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true,
        teamRelativeCoreCharge: true,
        veteranCommand: true,
        respawnWavePeriodTicks: 30,
        respawnMinimumDeadTicks: 15,
        minimumDistinctClasses: 7,
        durableDeployments: true,
        combatMagnitudeScale: 10,
        centreOpeningDelayTicks: 25,
        arcTossTellTicks: 0,
        arcTossTravelTilesPerTick: 3,
        arcTossActivationTiming: ArcRelaySignatureDefinition
            .ActivationTimingKind.AfterCombat,
        bankTriggeredWellRechargeTicks: 46);

    /// <summary>
    /// Tapline-9's complete map, economy, lifecycle, and signature stack with
    /// a tighter positional-combat envelope. Facing-quadrant vision contracts
    /// from seven to five tiles; short, medium, and long guns contract from
    /// 4/6/9 to 3/4/6. Team perception remains the immediate union of allied
    /// sensors, so Longshot can deliberately fire one tile beyond its own
    /// sight when a teammate supplies contact.
    /// </summary>
    public static ArcRelayLoopProfile Tapline10 { get; } = new(
        "tapline-10",
        "arc-relay-tapline-10",
        "arc-relay-tapline-03",
        ArcRelayMapGeometry.TaplineSealedEntrancesStaggeredMidCover,
        30, 65, 25, 1,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true,
        teamRelativeCoreCharge: true,
        veteranCommand: true,
        respawnWavePeriodTicks: 30,
        respawnMinimumDeadTicks: 15,
        minimumDistinctClasses: 7,
        durableDeployments: true,
        combatMagnitudeScale: 10,
        centreOpeningDelayTicks: 25,
        arcTossTellTicks: 0,
        arcTossTravelTilesPerTick: 3,
        arcTossActivationTiming: ArcRelaySignatureDefinition
            .ActivationTimingKind.AfterCombat,
        bankTriggeredWellRechargeTicks: 46,
        visionRange: 5,
        shortAttackRange: 3,
        mediumAttackRange: 4,
        longAttackRange: 6);

    /// <summary>
    /// Tapline-10's compact 5 / 3 / 4 / 6 combat envelope with one movement
    /// grammar for every chassis and explicit public travel rates. Every body
    /// can use eight-way CombatStrafe movement: forward/lateral travel retains
    /// aim and rear travel turns the chassis. Swift moves every tick, Standard
    /// receives four move opportunities per five ticks, and Deliberate two per
    /// three. Stable unit IDs stagger closed ticks; opportunities never bank.
    /// </summary>
    public static ArcRelayLoopProfile Tapline11 { get; } = new(
        "tapline-11",
        "arc-relay-tapline-11",
        "arc-relay-tapline-03",
        ArcRelayMapGeometry.TaplineSealedEntrancesStaggeredMidCover,
        30, 65, 25, 1,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true,
        teamRelativeCoreCharge: true,
        veteranCommand: true,
        respawnWavePeriodTicks: 30,
        respawnMinimumDeadTicks: 15,
        minimumDistinctClasses: 7,
        durableDeployments: true,
        combatMagnitudeScale: 10,
        centreOpeningDelayTicks: 25,
        arcTossTellTicks: 0,
        arcTossTravelTilesPerTick: 3,
        arcTossActivationTiming: ArcRelaySignatureDefinition
            .ActivationTimingKind.AfterCombat,
        bankTriggeredWellRechargeTicks: 46,
        visionRange: 5,
        shortAttackRange: 3,
        mediumAttackRange: 4,
        longAttackRange: 6,
        unifiedCombatStrafeMovement: true,
        swiftMoveOpportunitiesPerCycle: 1,
        swiftMoveCycleTicks: 1,
        standardMoveOpportunitiesPerCycle: 4,
        standardMoveCycleTicks: 5,
        deliberateMoveOpportunitiesPerCycle: 2,
        deliberateMoveCycleTicks: 3);

    /// <summary>
    /// Tapline-11 with Palisade's static contact-count Prism Wall versioned
    /// into the owner-attached, facing-bound 30-hull Prism Screen. Every map,
    /// objective, gun, movement and remaining signature rule is retained.
    /// </summary>
    public static ArcRelayLoopProfile Tapline12 { get; } = new(
        "tapline-12",
        "arc-relay-tapline-12",
        "arc-relay-tapline-03",
        ArcRelayMapGeometry.TaplineSealedEntrancesStaggeredMidCover,
        30, 65, 25, 1,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true,
        teamRelativeCoreCharge: true,
        veteranCommand: true,
        respawnWavePeriodTicks: 30,
        respawnMinimumDeadTicks: 15,
        minimumDistinctClasses: 7,
        durableDeployments: true,
        combatMagnitudeScale: 10,
        centreOpeningDelayTicks: 25,
        arcTossTellTicks: 0,
        arcTossTravelTilesPerTick: 3,
        arcTossActivationTiming: ArcRelaySignatureDefinition
            .ActivationTimingKind.AfterCombat,
        bankTriggeredWellRechargeTicks: 46,
        visionRange: 5,
        shortAttackRange: 3,
        mediumAttackRange: 4,
        longAttackRange: 6,
        unifiedCombatStrafeMovement: true,
        swiftMoveOpportunitiesPerCycle: 1,
        swiftMoveCycleTicks: 1,
        standardMoveOpportunitiesPerCycle: 4,
        standardMoveCycleTicks: 5,
        deliberateMoveOpportunitiesPerCycle: 2,
        deliberateMoveCycleTicks: 3,
        movingPrismScreen: true);

    /// <summary>
    /// Tapline-12 with readable route-facing locomotion, engagement-only
    /// combat strafing, and Sentinel Seeds acting as honest range-matched
    /// omnidirectional team sensors. The accepted map and every other rule
    /// remain unchanged.
    /// </summary>
    public static ArcRelayLoopProfile Tapline13 { get; } = new(
        "tapline-13",
        "arc-relay-tapline-13",
        "arc-relay-tapline-03",
        ArcRelayMapGeometry.TaplineSealedEntrancesStaggeredMidCover,
        30, 65, 25, 1,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true,
        teamRelativeCoreCharge: true,
        veteranCommand: true,
        respawnWavePeriodTicks: 30,
        respawnMinimumDeadTicks: 15,
        minimumDistinctClasses: 7,
        durableDeployments: true,
        combatMagnitudeScale: 10,
        centreOpeningDelayTicks: 25,
        arcTossTellTicks: 0,
        arcTossTravelTilesPerTick: 3,
        arcTossActivationTiming: ArcRelaySignatureDefinition
            .ActivationTimingKind.AfterCombat,
        bankTriggeredWellRechargeTicks: 46,
        visionRange: 5,
        shortAttackRange: 3,
        mediumAttackRange: 4,
        longAttackRange: 6,
        swiftMoveOpportunitiesPerCycle: 1,
        swiftMoveCycleTicks: 1,
        standardMoveOpportunitiesPerCycle: 4,
        standardMoveCycleTicks: 5,
        deliberateMoveOpportunitiesPerCycle: 2,
        deliberateMoveCycleTicks: 3,
        movingPrismScreen: true,
        contextualCombatStrafeMovement: true,
        sentryTeamVision: true);

    /// <summary>
    /// Tapline-13 with the expected survival response to blind-side damage. A
    /// flank still earns its already-frozen rear bonus; the surviving hull
    /// then faces the strongest incoming projectile before the next decision.
    /// </summary>
    public static ArcRelayLoopProfile Tapline14 { get; } = new(
        "tapline-14",
        "arc-relay-tapline-14",
        "arc-relay-tapline-03",
        ArcRelayMapGeometry.TaplineSealedEntrancesStaggeredMidCover,
        30, 65, 25, 1,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true,
        teamRelativeCoreCharge: true,
        veteranCommand: true,
        respawnWavePeriodTicks: 30,
        respawnMinimumDeadTicks: 15,
        minimumDistinctClasses: 7,
        durableDeployments: true,
        combatMagnitudeScale: 10,
        centreOpeningDelayTicks: 25,
        arcTossTellTicks: 0,
        arcTossTravelTilesPerTick: 3,
        arcTossActivationTiming: ArcRelaySignatureDefinition
            .ActivationTimingKind.AfterCombat,
        bankTriggeredWellRechargeTicks: 46,
        visionRange: 5,
        shortAttackRange: 3,
        mediumAttackRange: 4,
        longAttackRange: 6,
        swiftMoveOpportunitiesPerCycle: 1,
        swiftMoveCycleTicks: 1,
        standardMoveOpportunitiesPerCycle: 4,
        standardMoveCycleTicks: 5,
        deliberateMoveOpportunitiesPerCycle: 2,
        deliberateMoveCycleTicks: 3,
        movingPrismScreen: true,
        contextualCombatStrafeMovement: true,
        sentryTeamVision: true,
        reactiveImpactFacing: true);

    /// <summary>
    /// Tapline-14 with stable-slot veteran careers and honest friendly firing
    /// lanes. A destroyed slot retains half of its capped total XP; living
    /// allies consume team projectiles without taking damage. Friendly
    /// deployments remain transparent to their owner's fire.
    /// </summary>
    public static ArcRelayLoopProfile Tapline15 { get; } = new(
        "tapline-15",
        "arc-relay-tapline-15",
        "arc-relay-tapline-03",
        ArcRelayMapGeometry.TaplineSealedEntrancesStaggeredMidCover,
        30, 65, 25, 1,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true,
        teamRelativeCoreCharge: true,
        veteranCommand: true,
        respawnWavePeriodTicks: 30,
        respawnMinimumDeadTicks: 15,
        minimumDistinctClasses: 7,
        durableDeployments: true,
        combatMagnitudeScale: 10,
        centreOpeningDelayTicks: 25,
        arcTossTellTicks: 0,
        arcTossTravelTilesPerTick: 3,
        arcTossActivationTiming: ArcRelaySignatureDefinition
            .ActivationTimingKind.AfterCombat,
        bankTriggeredWellRechargeTicks: 46,
        visionRange: 5,
        shortAttackRange: 3,
        mediumAttackRange: 4,
        longAttackRange: 6,
        swiftMoveOpportunitiesPerCycle: 1,
        swiftMoveCycleTicks: 1,
        standardMoveOpportunitiesPerCycle: 4,
        standardMoveCycleTicks: 5,
        deliberateMoveOpportunitiesPerCycle: 2,
        deliberateMoveCycleTicks: 3,
        movingPrismScreen: true,
        contextualCombatStrafeMovement: true,
        sentryTeamVision: true,
        reactiveImpactFacing: true,
        veterancyDeathRetentionPercent: 50,
        alliedProjectilesBlock: true);

    /// <summary>
    /// Probe-only profile: the complete Tapline-14 ruleset on the Broadloom
    /// 37x27 whole-map candidate, so the candidate's strategic questions can
    /// be settled with deterministic matches. Never a product line and never
    /// hosted; the map requires the bounded width-cap change this branch
    /// carries (ArenaMap.MaxWidth 37 within the 1,024-cell budget).
    /// </summary>
    public static ArcRelayLoopProfile BroadloomProbe { get; } = new(
        "broadloom-probe",
        "arc-relay-broadloom-probe",
        "arc-relay-broadloom-01",
        ArcRelayMapGeometry.BroadloomCross,
        30, 65, 25, 1,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true,
        teamRelativeCoreCharge: true,
        veteranCommand: true,
        respawnWavePeriodTicks: 30,
        respawnMinimumDeadTicks: 15,
        minimumDistinctClasses: 7,
        durableDeployments: true,
        combatMagnitudeScale: 10,
        centreOpeningDelayTicks: 25,
        arcTossTellTicks: 0,
        arcTossTravelTilesPerTick: 3,
        arcTossActivationTiming: ArcRelaySignatureDefinition
            .ActivationTimingKind.AfterCombat,
        bankTriggeredWellRechargeTicks: 46,
        visionRange: 5,
        shortAttackRange: 3,
        mediumAttackRange: 4,
        longAttackRange: 6,
        swiftMoveOpportunitiesPerCycle: 1,
        swiftMoveCycleTicks: 1,
        standardMoveOpportunitiesPerCycle: 4,
        standardMoveCycleTicks: 5,
        deliberateMoveOpportunitiesPerCycle: 2,
        deliberateMoveCycleTicks: 3,
        movingPrismScreen: true,
        contextualCombatStrafeMovement: true,
        sentryTeamVision: true,
        reactiveImpactFacing: true);

    /// <summary>
    /// The exact first Broadloom contract hosted in playlist v33, before
    /// Tapline-15 retention and allied firing lanes were folded into the
    /// product. It remains registered so its published identity is immutable.
    /// </summary>
    public static ArcRelayLoopProfile Broadloom1 { get; } = CreateBroadloom(
        "broadloom-1",
        "arc-relay-broadloom-1",
        tapline15CombatDepth: false);

    /// <summary>
    /// Historical Broadloom-2: the complete Tapline-15 ruleset on
    /// the accepted 37x27 whole-map candidate, plus the two neutral veterancy
    /// Workshops (unlock 25, 15 consecutive qualifying ticks, 1 XP, 45-tick
    /// global dormancy). New append-only identities; nothing historical
    /// moves.
    /// </summary>
    public static ArcRelayLoopProfile Broadloom2 { get; } = CreateBroadloom(
        "broadloom-2",
        "arc-relay-broadloom-2",
        tapline15CombatDepth: true);

    /// <summary>
    /// Current Broadloom product line. It preserves Broadloom-2 exactly except
    /// that Workshop training has no post-completion cooldown: a body may
    /// begin its next 15-tick channel on the following tick.
    /// </summary>
    public static ArcRelayLoopProfile Broadloom3 { get; } = CreateBroadloom(
        "broadloom-3",
        "arc-relay-broadloom-3",
        tapline15CombatDepth: true,
        workshopDormancyTicks: 0);

    private static ArcRelayLoopProfile CreateBroadloom(
        string id,
        string rulesetId,
        bool tapline15CombatDepth,
        int workshopDormancyTicks = 45) => new(
        id,
        rulesetId,
        "arc-relay-broadloom-01",
        ArcRelayMapGeometry.BroadloomCross,
        30, 65, 25, 1,
        directionalCombat: true,
        signatureGrammarVersion: 3,
        wellBirthJitterTicks: 6,
        alternatingResolutionOrder: true,
        rearArcDamageMultiplier: 2,
        omniProximityRange: 0,
        veterancyXpPerLevel: 2,
        veterancyMaxLevel: 5,
        healZoneTicksPerHp: 3,
        seedPhasedResolutionOrder: true,
        rotationalSpawnAssignment: true,
        chiralWellBirths: true,
        strikeWindupTicks: 1,
        followingVacatedActorAllowed: true,
        teamRelativeCoreCharge: true,
        veteranCommand: true,
        respawnWavePeriodTicks: 30,
        respawnMinimumDeadTicks: 15,
        minimumDistinctClasses: 7,
        durableDeployments: true,
        combatMagnitudeScale: 10,
        centreOpeningDelayTicks: 25,
        arcTossTellTicks: 0,
        arcTossTravelTilesPerTick: 3,
        arcTossActivationTiming: ArcRelaySignatureDefinition
            .ActivationTimingKind.AfterCombat,
        bankTriggeredWellRechargeTicks: 46,
        visionRange: 5,
        shortAttackRange: 3,
        mediumAttackRange: 4,
        longAttackRange: 6,
        swiftMoveOpportunitiesPerCycle: 1,
        swiftMoveCycleTicks: 1,
        standardMoveOpportunitiesPerCycle: 4,
        standardMoveCycleTicks: 5,
        deliberateMoveOpportunitiesPerCycle: 2,
        deliberateMoveCycleTicks: 3,
        movingPrismScreen: true,
        contextualCombatStrafeMovement: true,
        sentryTeamVision: true,
        reactiveImpactFacing: true,
        veterancyDeathRetentionPercent: tapline15CombatDepth ? 50 : 0,
        alliedProjectilesBlock: tapline15CombatDepth,
        veterancyWorkshops: true,
        veterancyWorkshopDormancyTicks: workshopDormancyTicks);

    /// <summary>
    /// Owner-selected product line. Kept separate from H0 and every historical
    /// hosted playlist so advancing the product never rewrites old contracts or
    /// golden replays.
    /// </summary>
    public static ArcRelayLoopProfile Current => Broadloom3;

    public static ArcRelayLoopProfile Return16 { get; } = new(
        "return-16",
        "arc-relay-return-16-01",
        ArcRelayH0Definition.MapId,
        ArcRelayMapGeometry.H0,
        16,
        75,
        25,
        7);

    public static ArcRelayLoopProfile Return24 { get; } = new(
        "return-24",
        "arc-relay-return-24-01",
        ArcRelayH0Definition.MapId,
        ArcRelayMapGeometry.H0,
        24,
        75,
        25,
        7);

    public static ArcRelayLoopProfile Hot60 { get; } = new(
        "hot-60",
        "arc-relay-hot-60-01",
        ArcRelayH0Definition.MapId,
        ArcRelayMapGeometry.H0,
        20,
        60,
        20,
        9);

    public static ArcRelayLoopProfile Spacious90 { get; } = new(
        "spacious-90",
        "arc-relay-spacious-90-01",
        ArcRelayH0Definition.MapId,
        ArcRelayMapGeometry.H0,
        20,
        90,
        30,
        6);

    public static IReadOnlyList<ArcRelayLoopProfile> Registered { get; } =
    [
        H0,
        HomeGatesWide,
        HomeGatesThree,
        HomeConcourse,
        CoverTrim,
        DepthLarger,
        DepthCounterflow,
        ForwardCombat,
        ForwardCombat2,
        ForwardCombat3,
        ThreefoldPulse,
        ChargeValueControl,
        RipeningCores,
        RipeningCores2,
        AmbushWarren,
        AmbushWarren2,
        AmbushWarren3,
        AmbushWarren4,
        AmbushWarren5,
        AmbushWarren6,
        AmbushWarren7,
        AmbushWarren8,
        AmbushWarren9,
        AmbushWarren10,
        AmbushWarren11,
        Crucible1,
        Drawbench1,
        Tapline1,
        Tapline2,
        Tapline3,
        Tapline4,
        Tapline5,
        Tapline6,
        Tapline7,
        Tapline8,
        Tapline9,
        Tapline10,
        Tapline11,
        Tapline12,
        Tapline13,
        Tapline14,
        Tapline15,
        BroadloomProbe,
        Broadloom1,
        Broadloom2,
        Broadloom3,
        TaplineDepthScore124,
        TaplineDepthVeteranCommand,
        TaplineDepthRespawnWave,
        TaplineDepthRosterWildcard,
        TaplineDepthDurableDeployments,
        TaplineDepthCombatScale10,
        TaplineDepthDurabilityPlus10,
        TaplineDepthPack,
        Return16,
        Return24,
        Hot60,
        Spacious90,
    ];

    public static ArcRelayLoopProfile Resolve(string id) =>
        Registered.SingleOrDefault(profile => string.Equals(
            profile.Id,
            id,
            StringComparison.Ordinal))
        ?? throw new ArgumentException(
            $"Unknown Arc Relay loop profile '{id}'. Registered: "
            + string.Join(", ", Registered.Select(profile => profile.Id)),
            nameof(id));
}

internal enum ArcRelayMapGeometry
{
    H0,
    HomeGatesWide,
    HomeGatesThree,
    HomeConcourse,
    CoverTrim,
    DepthLarger,
    DepthCounterflow,
    AmbushWarren,
    AmbushWarrenDense,
    AmbushWarrenSerpentine,
    AmbushWarrenDeep,
    CrucibleDiagonal,
    DrawbenchHorizontal,
    TaplineWellLine,
    TaplineEqualReturn,
    TaplineSealedEntrancesStaggeredMidCover,
    BroadloomCross,
}
