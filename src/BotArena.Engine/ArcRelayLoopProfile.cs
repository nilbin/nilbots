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
        int scheduledBirthRounds)
    {
        Id = id;
        RulesetId = rulesetId;
        MapId = mapId;
        Geometry = geometry;
        RespawnDelayTicks = respawnDelayTicks;
        WellCadenceTicks = wellCadenceTicks;
        FirstGlobalBeatTicks = firstGlobalBeatTicks;
        ScheduledBirthRounds = scheduledBirthRounds;
    }

    public string Id { get; }
    public string RulesetId { get; }
    public string MapId { get; }
    internal ArcRelayMapGeometry Geometry { get; }
    public int RespawnDelayTicks { get; }
    public int WellCadenceTicks { get; }
    public int FirstGlobalBeatTicks { get; }
    public int ScheduledBirthRounds { get; }

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
    /// Owner-selected hosted product map. Kept separate from H0 so historical
    /// contracts and golden replays never change when the product advances.
    /// </summary>
    public static ArcRelayLoopProfile Current => DepthCounterflow;

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
