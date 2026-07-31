using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// One closed typed capability for a Frontline BATTLEFIELD ECONOMY: scheduled
/// deposits in the map's dead lanes, wreckage at every death tile, a carried
/// resource banked at home, and a team store that converts the bank into typed
/// stat modifiers on the bodies it already fields
/// (<c>docs/DESIGN-SCRAP-ECONOMY-2026-07-30.md</c> §14, as amended by parts 2–3;
/// DECISIONS #187).
/// <para>
/// It is deliberately NOT the side-objective capability
/// (<see cref="FrontlineSecondaryControlDefinition"/>) with different numbers.
/// That one is a latch: a tile set, an owner, and a continuous effect while
/// you hold it. This one has no latch, no owner, and no claim — its sites are
/// CONSUMED rather than held, a vein is gone the tick somebody steps on it,
/// and the payoff is bought rather than occupied. The two are mutually
/// exclusive arms so that the measured factor space stays three-valued.
/// </para>
/// <para>
/// The whole block is absent on every ruleset that does not declare it, so the
/// canonical writer emits no bytes and every historical fingerprint stays
/// byte-exact. Nothing here is a score channel: Frontline hard-validates a
/// score catalog of exactly one signed <c>TerritorialProgress</c> channel and
/// ranks timeouts by exactly it, so a bank that paid score would be a way to
/// win without contesting the front. The bank is mode observation state.
/// </para>
/// </summary>
public sealed record FrontlineScrapEconomyDefinition
{
    /// <summary>
    /// What one purchased tier does. A closed enum, in the same shape as
    /// <see cref="FrontlineSecondaryControlDefinition.SecondaryEffectKind"/>:
    /// a fourth track later is one value here plus one application point, not
    /// a new capability.
    /// <para>Every value is an ADDITIVE integer step on a declared form stat.
    /// That is the whole ladder's legibility claim — the tier is resolved at
    /// the point of use against the form catalog's declared number, so the
    /// contract never has to carry an upgraded variant of every form.</para>
    /// </summary>
    public enum UpgradeEffectKind
    {
        /// <summary>
        /// EDGE. Adds tiles to the travel distance of the bolts an in-scope
        /// body fires. Gap-preserving across the class ladder: every chassis
        /// moves by the same integer, so the 2-tile striker/bulwark spread is
        /// still 2 tiles afterwards.
        /// </summary>
        MobileAttackTravelTilesDelta = 0,

        /// <summary>
        /// PLATE. Adds to the maximum health an in-scope body spawns with. It
        /// raises the ceiling and NEVER heals: a life that is already alive
        /// keeps its exact current health, so a purchase mid-duel is never a
        /// rescue.
        /// </summary>
        SpawnMaxHealthDelta = 1,

        /// <summary>
        /// OPTIC. Adds tiles to the sight range of an in-scope body. The
        /// omnidirectional proximity radius is untouched, so the tier widens
        /// what a body can see at distance without changing the shape of what
        /// it sees up close.
        /// </summary>
        VisionRangeDelta = 2,
    }

    /// <summary>Which of a team's bodies a purchased tier applies to.</summary>
    public enum UpgradeScopeKind
    {
        /// <summary>
        /// Every life of the team's PRIME unit slot — the slot the contract
        /// starts the match with — current and future, in every form that slot
        /// occupies. The reward is therefore flat per team: a five-slot
        /// fabricator buys exactly as much upgraded body as a three-slot
        /// striker, which is the same mitigation MUSTER made with
        /// <see cref="FrontlineSecondaryControlDefinition
        /// .SecondaryRallyScopeKind.PrimeAutomaticReturnOnly"/> and for the
        /// same reason. An all-bodies level and a per-track mix are the
        /// registered alternatives (<c>scrap-upgrade-scope</c>).
        /// </summary>
        PrimeSlotLivesOnly = 0,

        /// <summary>
        /// Every life of every slot the team fields, current and future, in
        /// every form those slots occupy. The registered alternative the
        /// PrimeSlotLivesOnly comment named, promoted to a shipped level by
        /// prime dissolution (DECISIONS #194): once no slot is the prime, a
        /// prime-scoped ladder has nothing to be scoped to, so the upgrade
        /// scope is not an independent choice under
        /// <see cref="FrontlineLabsChassisArm.Unified"/> — it is a forced
        /// consequence.
        /// <para>The mitigation the prime scope existed for — a nine-slot team
        /// buying more upgraded body than an eight-slot one — returns with it,
        /// and is answered on price instead: the tier cost doubles under the
        /// arm so a wider army pays for its width. Price is the registered
        /// factor (<c>chassis-unified-tier-price</c>), which is why it is a
        /// sweepable rules value rather than a constant.</para>
        /// </summary>
        AllSlotLives = 1,
    }

    /// <summary>How a bank turns into tiers.</summary>
    public enum PurchaseModeKind
    {
        /// <summary>
        /// A live body spends its action tick on the <c>invest</c> verb,
        /// naming a track. WHICH branch and WHEN — relative to what the enemy
        /// has just been seen to buy — is the only decision the spend side
        /// has, and it is the RTS aspect the whole design rests on.
        /// </summary>
        InvestAction = 0,

        /// <summary>
        /// The bank buys by itself: at the end of every joint tick, while the
        /// bank can afford one, it takes the cheapest legal next tier and
        /// breaks ties by declared track order. No action is spent and no body
        /// is involved. This is the pre-registered FALSIFICATION control for
        /// the invest action (<c>scrap-flat-control-arm</c>) — if it measures
        /// the same as the action on both the balance edges and the pacing
        /// gates, the allocation decision is inert and the entire new action
        /// family is unjustified.
        /// </summary>
        AutomaticGreedyDeclaredOrder = 1,
    }

    /// <summary>Creates the capability declaration.</summary>
    /// <param name="veinSites">
    /// The deposit addresses, in declared order. Tile addresses in the RULES
    /// rather than map regions, which is what keeps the map fingerprint — and
    /// therefore comparability with every arm measured to date — unmoved.
    /// </param>
    /// <param name="veinFirstSpawnTick">First scheduled deposit tick.</param>
    /// <param name="veinSpawnIntervalTicks">Ticks between deposits.</param>
    /// <param name="veinLastSpawnTick">
    /// Last scheduled deposit tick. It must sit on the schedule, and it exists
    /// so a deposit nobody could bank and convert before the horn is never
    /// scheduled at all.
    /// </param>
    /// <param name="veinAmount">Scrap in one deposit.</param>
    /// <param name="wreckAmount">Scrap a destroyed body leaves behind.</param>
    /// <param name="assayAmount">
    /// Scrap banked instantly on stepping onto a pile, before the remainder is
    /// loaded. The floor under every trip: a fully-denied harvester still
    /// converted its walk into something.
    /// </param>
    /// <param name="carryCapacity">Most scrap one body may carry.</param>
    /// <param name="pileLifetimeTicks">
    /// Ticks a pile survives. The pile is gone the first tick
    /// <c>tick &gt;= expiresAtTick</c>, the established clock grammar.
    /// </param>
    /// <param name="maxSimultaneousPiles">
    /// Hard engine bound on live piles, so the published collection is
    /// provably small.
    /// </param>
    /// <param name="bankRegionIds">
    /// Each scoring team's banking region, indexed by team ID. A body of that
    /// team standing on one of its tiles banks its whole load automatically.
    /// </param>
    /// <param name="upgradeScope">Which bodies a tier applies to.</param>
    /// <param name="maxTotalTiers">
    /// Hard cap on the tiers one team may ever hold across all tracks. The
    /// ceiling is enforced by rules rather than by income, so even a total
    /// economic wipeout converts to a bounded number of integer stat steps.
    /// </param>
    /// <param name="purchaseMode">How the bank turns into tiers.</param>
    /// <param name="tracks">
    /// The ladder, in declared order. Order is load bearing twice: it is the
    /// order tier levels are published in, and it is the automatic buyer's
    /// tie-break.
    /// </param>
    public FrontlineScrapEconomyDefinition(
        IReadOnlyList<Position> veinSites,
        int veinFirstSpawnTick,
        int veinSpawnIntervalTicks,
        int veinLastSpawnTick,
        int veinAmount,
        int wreckAmount,
        int assayAmount,
        int carryCapacity,
        int pileLifetimeTicks,
        int maxSimultaneousPiles,
        IReadOnlyList<string> bankRegionIds,
        UpgradeScopeKind upgradeScope,
        int maxTotalTiers,
        PurchaseModeKind purchaseMode,
        IReadOnlyList<FrontlineScrapTrackDefinition> tracks)
    {
        ArgumentNullException.ThrowIfNull(veinSites);
        ArgumentNullException.ThrowIfNull(bankRegionIds);
        ArgumentNullException.ThrowIfNull(tracks);

        Position[] sites = [.. veinSites];
        if (sites.Length == 0 || sites.Distinct().Count() != sites.Length)
        {
            throw new ArgumentException(
                "A scrap economy must declare at least one distinct vein site.",
                nameof(veinSites));
        }
        if (veinFirstSpawnTick < 0)
            throw new ArgumentOutOfRangeException(nameof(veinFirstSpawnTick));
        if (veinSpawnIntervalTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(veinSpawnIntervalTicks));
        }
        if (veinLastSpawnTick < veinFirstSpawnTick
            || (veinLastSpawnTick - veinFirstSpawnTick)
                % veinSpawnIntervalTicks != 0)
        {
            throw new ArgumentException(
                "The last scheduled vein tick must sit on the declared cadence.",
                nameof(veinLastSpawnTick));
        }
        if (veinAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(veinAmount));
        if (wreckAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(wreckAmount));
        if (assayAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(assayAmount));
        if (carryCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(carryCapacity));
        if (pileLifetimeTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(pileLifetimeTicks));
        if (maxSimultaneousPiles <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxSimultaneousPiles));
        }

        string[] bankRegions = [.. bankRegionIds];
        if (bankRegions.Length == 0
            || bankRegions.Any(string.IsNullOrWhiteSpace)
            || bankRegions.Distinct(StringComparer.Ordinal).Count()
                != bankRegions.Length)
        {
            throw new ArgumentException(
                "A scrap economy needs one distinct non-blank banking region "
                + "per scoring team, indexed by team ID.",
                nameof(bankRegionIds));
        }
        if (!Enum.IsDefined(upgradeScope))
            throw new ArgumentOutOfRangeException(nameof(upgradeScope));
        if (!Enum.IsDefined(purchaseMode))
            throw new ArgumentOutOfRangeException(nameof(purchaseMode));
        if (maxTotalTiers <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTotalTiers));

        FrontlineScrapTrackDefinition[] ladder = [.. tracks];
        if (ladder.Length == 0
            || ladder.Any(track => track is null)
            || ladder
                .Select(track => track.TrackId)
                .Distinct(StringComparer.Ordinal)
                .Count() != ladder.Length)
        {
            throw new ArgumentException(
                "A scrap ladder must declare at least one track with unique IDs.",
                nameof(tracks));
        }
        if (ladder
            .Select(track => track.Effect)
            .Distinct()
            .Count() != ladder.Length)
        {
            throw new ArgumentException(
                "Two scrap tracks cannot move the same declared stat: a bot "
                + "reads the effective number as one addition with both "
                + "operands published.",
                nameof(tracks));
        }

        // Declared order, deliberately NOT sorted: tier levels are published
        // positionally against this sequence, exactly like the objective
        // chain and the secondary site's regions.
        VeinSites = sites.ToImmutableArray();
        VeinFirstSpawnTick = veinFirstSpawnTick;
        VeinSpawnIntervalTicks = veinSpawnIntervalTicks;
        VeinLastSpawnTick = veinLastSpawnTick;
        VeinAmount = veinAmount;
        WreckAmount = wreckAmount;
        AssayAmount = assayAmount;
        CarryCapacity = carryCapacity;
        PileLifetimeTicks = pileLifetimeTicks;
        MaxSimultaneousPiles = maxSimultaneousPiles;
        BankRegionIds = bankRegions.ToImmutableArray();
        UpgradeScope = upgradeScope;
        MaxTotalTiers = maxTotalTiers;
        PurchaseMode = purchaseMode;
        Tracks = ladder.ToImmutableArray();
    }

    /// <summary>The deposit addresses, in declared order.</summary>
    public ImmutableArray<Position> VeinSites { get; }

    /// <summary>First scheduled deposit tick.</summary>
    public int VeinFirstSpawnTick { get; }

    /// <summary>Ticks between scheduled deposits.</summary>
    public int VeinSpawnIntervalTicks { get; }

    /// <summary>Last scheduled deposit tick, on the cadence.</summary>
    public int VeinLastSpawnTick { get; }

    /// <summary>Scrap in one deposit.</summary>
    public int VeinAmount { get; }

    /// <summary>Scrap a destroyed body leaves at its death tile.</summary>
    public int WreckAmount { get; }

    /// <summary>Scrap banked instantly on stepping onto a pile.</summary>
    public int AssayAmount { get; }

    /// <summary>Most scrap one body may carry.</summary>
    public int CarryCapacity { get; }

    /// <summary>Ticks one pile survives.</summary>
    public int PileLifetimeTicks { get; }

    /// <summary>Hard bound on simultaneously live piles.</summary>
    public int MaxSimultaneousPiles { get; }

    /// <summary>Each team's banking region, indexed by team ID.</summary>
    public ImmutableArray<string> BankRegionIds { get; }

    /// <summary>Which bodies a purchased tier applies to.</summary>
    public UpgradeScopeKind UpgradeScope { get; }

    /// <summary>Hard cap on one team's total tiers.</summary>
    public int MaxTotalTiers { get; }

    /// <summary>How the bank turns into tiers.</summary>
    public PurchaseModeKind PurchaseMode { get; }

    /// <summary>The ladder, in declared order.</summary>
    public ImmutableArray<FrontlineScrapTrackDefinition> Tracks { get; }

    /// <summary>
    /// The most one declared effect can ever add to a body, derived from the
    /// contract alone: the deepest tier of the track that moves it, bounded by
    /// the team's total-tier cap, times that track's per-tier step. Zero when
    /// no declared track moves that stat.
    /// <para>It exists so that invariants which used to be exact against a
    /// form's declared number stay CHECKED rather than merely relaxed: a
    /// life's health, or a bolt's reach, may exceed the declared value by at
    /// most this much and never by more.</para>
    /// </summary>
    public int Headroom(UpgradeEffectKind effect) =>
        Tracks
            .Where(track => track.Effect == effect)
            .Select(track =>
                Math.Min(track.MaxTier, MaxTotalTiers)
                * track.PerTierMagnitude)
            .DefaultIfEmpty(0)
            .Max();

    /// <summary>
    /// The declared headroom for one effect on one resolved contract, or zero
    /// when the mode declares no economy at all.
    /// </summary>
    public static int HeadroomOn(
        GameModeDefinition gameMode,
        UpgradeEffectKind effect) =>
        gameMode is FrontlineGameModeDefinition frontline
        && frontline.ScrapEconomy is { } economy
            ? economy.Headroom(effect)
            : 0;

    /// <summary>
    /// Whether a scheduled deposit is due on this tick. Fully derivable from
    /// the contract before tick zero, which is the point: a bot knows every
    /// vein's address and due tick in advance and never has to discover the
    /// mechanic by watching it happen. What is NOT derivable is whether a
    /// given vein is still there — that is published.
    /// </summary>
    public bool IsVeinSpawnTick(int tick) =>
        tick >= VeinFirstSpawnTick
        && tick <= VeinLastSpawnTick
        && (tick - VeinFirstSpawnTick) % VeinSpawnIntervalTicks == 0;
}

/// <summary>
/// One purchasable track: a stable ID, one closed typed effect, the integer
/// step each tier adds, the deepest tier, and the price of each tier in
/// declared order.
/// </summary>
/// <param name="TrackId">Stable published identifier.</param>
/// <param name="Effect">The declared stat this track moves.</param>
/// <param name="PerTierMagnitude">Integer step one tier adds.</param>
/// <param name="MaxTier">Deepest reachable tier on this track.</param>
/// <param name="TierCosts">
/// Price of tier 1, tier 2, … in order. Its length is <paramref
/// name="MaxTier"/>.
/// </param>
public sealed record FrontlineScrapTrackDefinition
{
    public FrontlineScrapTrackDefinition(
        string trackId,
        FrontlineScrapEconomyDefinition.UpgradeEffectKind effect,
        int perTierMagnitude,
        int maxTier,
        IReadOnlyList<int> tierCosts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);
        ArgumentNullException.ThrowIfNull(tierCosts);
        if (!Enum.IsDefined(effect))
            throw new ArgumentOutOfRangeException(nameof(effect));
        if (perTierMagnitude <= 0)
            throw new ArgumentOutOfRangeException(nameof(perTierMagnitude));
        if (maxTier <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTier));

        int[] costs = [.. tierCosts];
        if (costs.Length != maxTier || costs.Any(cost => cost <= 0))
        {
            throw new ArgumentException(
                "A scrap track prices every tier it declares, positively.",
                nameof(tierCosts));
        }

        TrackId = trackId;
        Effect = effect;
        PerTierMagnitude = perTierMagnitude;
        MaxTier = maxTier;
        TierCosts = costs.ToImmutableArray();
    }

    public string TrackId { get; }
    public FrontlineScrapEconomyDefinition.UpgradeEffectKind Effect { get; }
    public int PerTierMagnitude { get; }
    public int MaxTier { get; }
    public ImmutableArray<int> TierCosts { get; }

    /// <summary>
    /// What the next tier past <paramref name="currentTier"/> costs, or null
    /// when this track is already at its declared maximum.
    /// </summary>
    public int? NextTierCost(int currentTier) =>
        currentTier < 0 || currentTier >= MaxTier
            ? null
            : TierCosts[currentTier];
}
