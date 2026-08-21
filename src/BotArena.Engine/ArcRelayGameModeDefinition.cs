using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>Immutable Arc Relay H0 objective and launch-class contract.</summary>
public sealed record ArcRelayGameModeDefinition : GameModeDefinition
{
    public const string Id = "arc-relay-h0";

    public ArcRelayGameModeDefinition(
        string modeId,
        ArcRelayVictoryDefinition victory,
        ImmutableArray<ScoreChannelDefinition> scoreCatalog,
        IEnumerable<ArcRelayWellScheduleDefinition> wells,
        int pendingRearmTicks,
        int coreRelocationIntervalTicks,
        int coresPerPulse,
        int fieldedSlotsPerTeam,
        int maxCopiesPerClass,
        int respawnDelayTicks,
        IEnumerable<ArcRelaySignatureDefinition> signatures,
        int signatureGrammarVersion = 1,
        int wellBirthJitterTicks = 0,
        bool alternatingResolutionOrder = false,
        bool threefoldSockets = false,
        int coreBaseValue = 1,
        int ripenIntervalTicks = 0,
        int ripenMaxValue = 0,
        int ripenResumeTicks = 0,
        int rearArcDamageMultiplier = 1,
        int veterancyXpPerLevel = 0,
        int veterancyMaxLevel = 0,
        int healZoneTicksPerHp = 0,
        bool seedPhasedResolutionOrder = false,
        bool seedPhasedWellLead = false,
        bool chiralWellBirths = false,
        int ownWellCoreCharge = 1,
        int centreWellCoreCharge = 1,
        int enemyWellCoreCharge = 1,
        bool veteranCommand = false,
        int respawnWavePeriodTicks = 0,
        int respawnMinimumDeadTicks = 0,
        int minimumDistinctClasses = 0,
        bool durableDeployments = false,
        int veterancyDamagePerPoint = 1,
        int veterancyDamageBonusCap = 2,
        bool veterancyBonusAfterRearArc = false,
        int vitalityHealthPerPoint = 1,
        int healZoneHealthPerActivation = 1,
        int bankTriggeredWellRechargeTicks = 0,
        bool sentryTeamVision = false)
        : base(modeId, victory, scoreCatalog)
    {
        RequirePositive(
            veterancyDamagePerPoint,
            nameof(veterancyDamagePerPoint));
        RequirePositive(
            veterancyDamageBonusCap,
            nameof(veterancyDamageBonusCap));
        RequirePositive(
            vitalityHealthPerPoint,
            nameof(vitalityHealthPerPoint));
        RequirePositive(
            healZoneHealthPerActivation,
            nameof(healZoneHealthPerActivation));
        VeterancyDamagePerPoint = veterancyDamagePerPoint;
        VeterancyDamageBonusCap = veterancyDamageBonusCap;
        VeterancyBonusAfterRearArc = veterancyBonusAfterRearArc;
        VitalityHealthPerPoint = vitalityHealthPerPoint;
        HealZoneHealthPerActivation = healZoneHealthPerActivation;
        if (bankTriggeredWellRechargeTicks < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bankTriggeredWellRechargeTicks));
        }
        BankTriggeredWellRechargeTicks = bankTriggeredWellRechargeTicks;
        SentryTeamVision = sentryTeamVision;
        if (ownWellCoreCharge < 1
            || centreWellCoreCharge < 1
            || enemyWellCoreCharge < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ownWellCoreCharge),
                "Team-relative Core charge values must be positive.");
        }
        OwnWellCoreCharge = ownWellCoreCharge;
        CentreWellCoreCharge = centreWellCoreCharge;
        EnemyWellCoreCharge = enemyWellCoreCharge;
        VeteranCommand = veteranCommand;
        if ((respawnWavePeriodTicks == 0) != (respawnMinimumDeadTicks == 0)
            || respawnWavePeriodTicks < 0
            || respawnMinimumDeadTicks < 0)
        {
            throw new ArgumentException(
                "Respawn waves require both a positive period and minimum dead time.");
        }
        RespawnWavePeriodTicks = respawnWavePeriodTicks;
        RespawnMinimumDeadTicks = respawnMinimumDeadTicks;
        if (minimumDistinctClasses < 0
            || minimumDistinctClasses > fieldedSlotsPerTeam)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumDistinctClasses));
        }
        MinimumDistinctClasses = minimumDistinctClasses;
        DurableDeployments = durableDeployments;
        if (signatureGrammarVersion is not (1 or 2 or 3))
            throw new ArgumentOutOfRangeException(
                nameof(signatureGrammarVersion));
        SignatureGrammarVersion = signatureGrammarVersion;
        if (wellBirthJitterTicks < 0)
            throw new ArgumentOutOfRangeException(
                nameof(wellBirthJitterTicks));
        WellBirthJitterTicks = wellBirthJitterTicks;
        _alternatingResolutionOrder = alternatingResolutionOrder;
        ThreefoldSockets = threefoldSockets;
        if (coreBaseValue < 1)
            throw new ArgumentOutOfRangeException(nameof(coreBaseValue));
        if (ripenIntervalTicks < 0 || ripenResumeTicks < 0)
            throw new ArgumentOutOfRangeException(nameof(ripenIntervalTicks));
        if (ripenIntervalTicks > 0
            && (ripenMaxValue < coreBaseValue || threefoldSockets))
        {
            throw new ArgumentException(
                "Ripening needs a max value at or above the base and is "
                + "never combined with threefold sockets.",
                nameof(ripenMaxValue));
        }
        CoreBaseValue = coreBaseValue;
        RipenIntervalTicks = ripenIntervalTicks;
        RipenMaxValue = ripenMaxValue;
        RipenResumeTicks = ripenResumeTicks;
        if (rearArcDamageMultiplier is < 1 or > 4)
            throw new ArgumentOutOfRangeException(
                nameof(rearArcDamageMultiplier));
        RearArcDamageMultiplier = rearArcDamageMultiplier;
        if (veterancyXpPerLevel < 0 || healZoneTicksPerHp < 0)
            throw new ArgumentOutOfRangeException(
                nameof(veterancyXpPerLevel));
        if (veterancyXpPerLevel > 0 && veterancyMaxLevel < 2)
        {
            throw new ArgumentException(
                "Veterancy needs a maximum level of at least 2.",
                nameof(veterancyMaxLevel));
        }
        VeterancyXpPerLevel = veterancyXpPerLevel;
        VeterancyMaxLevel = veterancyMaxLevel;
        HealZoneTicksPerHp = healZoneTicksPerHp;
        if (seedPhasedResolutionOrder && !alternatingResolutionOrder)
        {
            throw new ArgumentException(
                "Seed-phased resolution needs alternating resolution.",
                nameof(seedPhasedResolutionOrder));
        }
        _seedPhasedResolutionOrder = seedPhasedResolutionOrder;
        SeedPhasedWellLead = seedPhasedWellLead;
        if (chiralWellBirths && seedPhasedWellLead)
        {
            throw new ArgumentException(
                "Chiral well births already give a rotation pair one "
                + "schedule, so there is no lead left to phase.",
                nameof(chiralWellBirths));
        }
        ChiralWellBirths = chiralWellBirths;
        ArgumentNullException.ThrowIfNull(wells);
        ArgumentNullException.ThrowIfNull(signatures);
        ArcRelayWellScheduleDefinition[] wellSnapshot = [.. wells];
        ArcRelaySignatureDefinition[] signatureSnapshot = [.. signatures];
        if (wellSnapshot.Length == 0
            || wellSnapshot.Any(well => well is null)
            || wellSnapshot.Select(well => well.WellId)
                .Distinct(StringComparer.Ordinal).Count() != wellSnapshot.Length)
        {
            throw new ArgumentException(
                "Arc Relay Wells must be non-null with unique IDs.",
                nameof(wells));
        }
        if (bankTriggeredWellRechargeTicks > 0
            && (threefoldSockets
                || wellSnapshot.Any(well =>
                    well.FirstBirthTick != well.FinalBirthTick)))
        {
            throw new ArgumentException(
                "Bank-triggered Well recharge requires one initial birth per "
                + "Well and cannot combine with Threefold sockets.",
                nameof(bankTriggeredWellRechargeTicks));
        }
        if (wellBirthJitterTicks > 0
            && wellSnapshot.Any(well =>
                2 * wellBirthJitterTicks >= well.CadenceTicks
                || well.FirstBirthTick - wellBirthJitterTicks < 1))
        {
            throw new ArgumentException(
                "Well-birth jitter must keep every birth window disjoint and after tick zero.",
                nameof(wellBirthJitterTicks));
        }
        if (threefoldSockets && wellSnapshot.Length != 3)
        {
            throw new ArgumentException(
                "Threefold sockets require exactly three Wells.",
                nameof(threefoldSockets));
        }
        if (signatureSnapshot.Length == 0
            || signatureSnapshot.Any(signature => signature is null)
            || signatureSnapshot.Select(signature => signature.SignatureId)
                .Distinct(StringComparer.Ordinal).Count() != signatureSnapshot.Length
            || signatureSnapshot.Select(signature => signature.ClassId)
                .Distinct(StringComparer.Ordinal).Count() != signatureSnapshot.Length
            || signatureSnapshot.Select(signature => signature.ActionId)
                .Distinct(StringComparer.Ordinal).Count() != signatureSnapshot.Length)
        {
            throw new ArgumentException(
                "Arc Relay signatures must map unique IDs, classes, and actions.",
                nameof(signatures));
        }
        RequirePositive(pendingRearmTicks, nameof(pendingRearmTicks));
        RequirePositive(coreRelocationIntervalTicks,
            nameof(coreRelocationIntervalTicks));
        RequirePositive(coresPerPulse, nameof(coresPerPulse));
        RequirePositive(fieldedSlotsPerTeam, nameof(fieldedSlotsPerTeam));
        RequirePositive(maxCopiesPerClass, nameof(maxCopiesPerClass));
        RequirePositive(respawnDelayTicks, nameof(respawnDelayTicks));
        if (maxCopiesPerClass > fieldedSlotsPerTeam)
            throw new ArgumentOutOfRangeException(nameof(maxCopiesPerClass));

        ValidatePrimaryTimeoutRanking(
            ScoreChannelDefinition.ChannelKind.Pulses);
        ValidateSupportedScoreCatalog(
            ScoreChannelDefinition.ChannelKind.Pulses,
            ScoreChannelDefinition.ChannelKind.ReactorCharge);
        ScoreRankingDefinition[] ranking = victory.TimeoutRanking.ToArray();
        if (ranking.Length != 2
            || ranking[0].Channel != ScoreChannelDefinition.ChannelKind.Pulses
            || ranking[1].Channel
                != ScoreChannelDefinition.ChannelKind.ReactorCharge
            || ranking.Any(item => item.Direction
                != ScoreRankingDefinition.SortDirection.HigherWins))
        {
            throw new ArgumentException(
                "Arc Relay timeout ranking must be higher Pulses, then higher ReactorCharge.",
                nameof(victory));
        }

        ArcRelayVictory = victory;
        Wells = wellSnapshot.ToImmutableArray();
        PendingRearmTicks = pendingRearmTicks;
        CoreRelocationIntervalTicks = coreRelocationIntervalTicks;
        CoresPerPulse = coresPerPulse;
        FieldedSlotsPerTeam = fieldedSlotsPerTeam;
        MaxCopiesPerClass = maxCopiesPerClass;
        RespawnDelayTicks = respawnDelayTicks;
        Signatures = signatureSnapshot
            .OrderBy(signature => signature.ClassId, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    public override GameModeDefinitionKind Kind =>
        GameModeDefinitionKind.ArcRelay;
    public ArcRelayVictoryDefinition ArcRelayVictory { get; }
    public ImmutableArray<ArcRelayWellScheduleDefinition> Wells { get; }
    public int PendingRearmTicks { get; }

    /// <summary>
    /// Positive when banking a Well's active Core starts its next public
    /// recharge. Zero retains historical global beats and pending rearm.
    /// </summary>
    public int BankTriggeredWellRechargeTicks { get; }
    public int CoreRelocationIntervalTicks { get; }
    public int CoresPerPulse { get; }
    public int FieldedSlotsPerTeam { get; }
    public int MaxCopiesPerClass { get; }
    public int RespawnDelayTicks { get; }
    public ImmutableArray<ArcRelaySignatureDefinition> Signatures { get; }

    /// <summary>
    /// Charge awarded when a team banks a Core from its near, centre, or far
    /// Well. Historical rules use 1/1/1; Tapline depth score uses 1/2/4.
    /// </summary>
    public int OwnWellCoreCharge { get; }
    public int CentreWellCoreCharge { get; }
    public int EnemyWellCoreCharge { get; }

    /// <summary>Whether Orders may use the veteran-command vocabulary.</summary>
    public bool VeteranCommand { get; }

    /// <summary>
    /// Shared public respawn clock. Zero disables waves and preserves the
    /// ordinary per-slot delay.
    /// </summary>
    public int RespawnWavePeriodTicks { get; }
    public int RespawnMinimumDeadTicks { get; }

    /// <summary>Minimum distinct class IDs in the eight-slot roster.</summary>
    public int MinimumDistinctClasses { get; }

    /// <summary>Persistent, damageable depth-pack deployment semantics.</summary>
    public bool DurableDeployments { get; }

    /// <summary>
    /// Active, unsuppressed Sentinel Seeds add omnidirectional sight out to
    /// their own attack range to the owning team's observable union.
    /// </summary>
    public bool SentryTeamVision { get; }

    /// <summary>
    /// Resolved veterancy and recovery magnitudes. Historical contracts omit
    /// these fields and retain 1 damage per point (capped at +2), vitality +1,
    /// heal +1, with veterancy applied before rear-arc multiplication.
    /// </summary>
    public int VeterancyDamagePerPoint { get; }
    public int VeterancyDamageBonusCap { get; }
    public bool VeterancyBonusAfterRearArc { get; }
    public int VitalityHealthPerPoint { get; }
    public int HealZoneHealthPerActivation { get; }

    /// <summary>
    /// 1 is the historical launch grammar. 2 (owner ruling 2026-08-05) makes
    /// every enemy-affecting signature dodgeable — sentinel and hook fire
    /// real bolts, null-field telegraphs — and projects designed-role
    /// metadata into the public contract. Canonically written only when not
    /// 1, so historical rules bytes are unchanged. 3 is the complete
    /// Tapline-2 grammar: every class remains in the same public signature
    /// vocabulary, while the four presence-versioned envelopes with new
    /// chronology mature according to their own canonical fields.
    /// </summary>
    public int SignatureGrammarVersion { get; }

    /// <summary>
    /// Half-width of the seed-derived window each scheduled well birth may
    /// shift within (owner goal 2026-08-05): round k of a well fires at
    /// nominal + jitter, where jitter is drawn deterministically from the
    /// match seed in [-J, +J]. Zero for historical rulesets, and canonically
    /// written only when non-zero, so their rules bytes are unchanged.
    /// </summary>
    public int WellBirthJitterTicks { get; }

    private readonly bool _alternatingResolutionOrder;

    /// <summary>
    /// Threefold Pulse (owner brief 2026-08-05): a Pulse requires one banked
    /// Core from EACH Well origin; a duplicate-origin Core cannot be
    /// consumed and stays contestable; sockets reset on the Pulse.
    /// Canonically written only when true.
    /// </summary>
    public bool ThreefoldSockets { get; }

    /// <summary>
    /// Charge a freshly born Core is worth (1 historically; 2 under the
    /// charge-value primitive so three base Cores still make a Pulse at
    /// six). Canonically written only when not 1.
    /// </summary>
    public int CoreBaseValue { get; }

    /// <summary>
    /// Ripening (owner direction 2026-08-05, depth memo #1): a loose Core
    /// gains +1 value per this many uninterrupted loose ticks, up to
    /// <see cref="RipenMaxValue"/>; first pickup freezes the value; after a
    /// drop, ripening resumes only after <see cref="RipenResumeTicks"/>
    /// loose ticks. Zero disables ripening; fields are canonically written
    /// only when active.
    /// </summary>
    public int RipenIntervalTicks { get; }
    public int RipenMaxValue { get; }
    public int RipenResumeTicks { get; }

    /// <summary>
    /// Predation (owner direction 2026-08-05): projectile damage is
    /// multiplied by this when the shot's heading lies inside the victim's
    /// rear quadrant — fired from its blind arc. 1 disables the bonus;
    /// canonically written only when not 1.
    /// </summary>
    public int RearArcDamageMultiplier { get; }

    /// <summary>
    /// Veterancy (owner direction 2026-08-05): bodies start at level 1 and
    /// earn XP per kill (1 plus the victim's level above 1); each
    /// <see cref="VeterancyXpPerLevel"/> XP grants a level up to
    /// <see cref="VeterancyMaxLevel"/>, each level past 1 grants one skill
    /// point spent via the invest action (damage/vision/reach/vitality),
    /// and death resets everything with the life. 0 disables; fields are
    /// canonically written only when active.
    /// </summary>
    public int VeterancyXpPerLevel { get; }
    public int VeterancyMaxLevel { get; }

    /// <summary>
    /// Heal zones (owner direction 2026-08-05): a body that Waits on a tile
    /// of a map region whose id starts with <c>heal-</c> recovers
    /// <see cref="HealZoneHealthPerActivation"/> health per this many
    /// consecutive waiting ticks, up to its effective maximum (which the
    /// vitality track can raise). 0 disables; canonically written only when
    /// active.
    /// </summary>
    public int HealZoneTicksPerHp { get; }

    public override bool AlternatingResolutionOrder =>
        _alternatingResolutionOrder;

    private readonly bool _seedPhasedResolutionOrder;

    public override bool SeedPhasedResolutionOrder =>
        _seedPhasedResolutionOrder;

    /// <summary>
    /// When true, the driver swaps the birth schedules of each pair of
    /// wells whose positions are mutual 180-degree rotations on a
    /// seed-derived coin. A staggered schedule (one flank well leading the
    /// other) otherwise gives the same canonical strategy different value
    /// by side on a rotationally chiral map: the side whose fast wing meets
    /// the first-born well converts tempo every seed. Canonically written
    /// only when set.
    /// </summary>
    public bool SeedPhasedWellLead { get; }

    /// <summary>
    /// Whether wells whose positions are mutual 180-degree rotations share
    /// one birth schedule -- base tick AND jitter draw. On a chiral map
    /// where each side OWNS its near Well, staggering that pair hands one
    /// player income first; a seed coin only launders that across matches,
    /// it never removes it from one. Neutral flank wells do not need this.
    /// </summary>
    public bool ChiralWellBirths { get; }

    public override ImmutableArray<string> ModeOwnedAttackProfileIds =>
        SignatureGrammarVersion >= 2
            ? ["sentinel-bolt", "hook-bolt"]
            : [];

    private static void RequirePositive(int value, string parameterName)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(parameterName);
    }
}
