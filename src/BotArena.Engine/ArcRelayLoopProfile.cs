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
        int ripenResumeTicks = 0)
    {
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
    /// Owner-selected hosted product map. Kept separate from H0 so historical
    /// contracts and golden replays never change when the product advances.
    /// </summary>
    public static ArcRelayLoopProfile Current => ForwardCombat;

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
}
