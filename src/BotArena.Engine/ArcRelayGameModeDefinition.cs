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
        int ripenResumeTicks = 0)
        : base(modeId, victory, scoreCatalog)
    {
        if (signatureGrammarVersion is not (1 or 2))
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
    public int CoreRelocationIntervalTicks { get; }
    public int CoresPerPulse { get; }
    public int FieldedSlotsPerTeam { get; }
    public int MaxCopiesPerClass { get; }
    public int RespawnDelayTicks { get; }
    public ImmutableArray<ArcRelaySignatureDefinition> Signatures { get; }

    /// <summary>
    /// 1 is the historical launch grammar. 2 (owner ruling 2026-08-05) makes
    /// every enemy-affecting signature dodgeable — sentinel and hook fire
    /// real bolts, null-field telegraphs — and projects designed-role
    /// metadata into the public contract. Canonically written only when not
    /// 1, so historical rules bytes are unchanged.
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

    public override bool AlternatingResolutionOrder =>
        _alternatingResolutionOrder;

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
