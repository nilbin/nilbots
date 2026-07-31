using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// The Frontline Labs battlefield economy, as adopted
/// (<c>docs/DESIGN-SCRAP-ECONOMY-2026-07-30.md</c> §14.1 with the part-2
/// significance re-tune, DECISIONS #187) and re-tuned to v1.1 on the wave-8
/// read (#188). Everything here is a decision rather than a suggestion, and
/// every number below is one the ablation registry names.
/// <para>v1.1 answers the owner's ruling on the measured arm — "the new
/// mechanism needs to be stronger and happen earlier". The wave-8 finding was
/// that the economy as priced is real but marginal: a committed team banks
/// roughly one ladder's worth over a whole match, so the allocation decision
/// the arm exists to create is mostly theoretical. Three numbers move — the
/// first deposit lands at 60 instead of 120, the cadence tightens 80 → 70, and
/// a deposit is worth 8 instead of 6 — wreckage doubles to 2, and the series
/// runs to nine events so it fills the LONG horizon. Income roughly triples
/// over a 750-tick match.
/// <para>The second owner ruling of the same window removes the three-tier
/// TOTAL cap: "ideally scraps should weigh in and decide the game / enable
/// overpowering the opponent". The full board is now six tiers — two on each
/// track, 60 scrap — so a team that wins the economy war visibly overpowers.
/// Nothing a tier DOES changes: the overpower is bought in BREADTH, which is
/// what keeps the class-gap admission rule (every track gap-preserving or
/// deliberately corrective) true tier for tier.</para>
/// <para>Behaviour moved on a measured arm, so the identities re-mint:
/// <c>forge</c>/<c>anvil</c>/<c>smelter</c> and
/// <c>bastion</c>/<c>redoubt</c>/<c>smithy</c> keep meaning the wave-8 bytes,
/// and v1.1 spells <c>foundry</c>/<c>bellows</c>/<c>furnace</c> and
/// <c>citadel</c>/<c>rampart</c>/<c>armoury</c>.</para>
/// </summary>
public static class FrontlineLabsScrapEconomy
{
    /// <summary>
    /// The two deposit addresses: the north lane at <c>(11,1)</c> and the
    /// south lane at <c>(11,13)</c>, in declared order.
    /// <para>They sit on the map's centre column, which is where the mirror
    /// comes from: the tile rows are palindromic about <c>x = 11</c>, so each
    /// site is exactly 16 facing-locked ticks from BOTH home pads. That
    /// costs no map edit, no new region, and no symmetry argument beyond the
    /// one the map validator already enforces — which is why this arm runs on
    /// the existing <c>frontline-labs-01</c> family and stays
    /// fingerprint-comparable to every arm measured to date.</para>
    /// <para>Both sites deposit on every event, deliberately. Two prizes and
    /// two teams make one-each the natural equilibrium, taking both costs a
    /// second body, and faking one is a real bluff; one alternating deposit
    /// would be a pure race whose loser paid a full trip for nothing and
    /// would snowball off the first race.</para>
    /// </summary>
    public static ImmutableArray<Position> VeinSites { get; } =
    [
        new Position(11, 1),
        new Position(11, 13),
    ];

    /// <summary>
    /// First deposit tick (v1.1: 120 → 60, the owner's "happen earlier").
    /// The v1 number was a class-fairness constraint — 120 was the tick at
    /// which every class had two bodies, so no deposit could ask a striker to
    /// send its ONLY body away from the front. That constraint is what the
    /// ROSTER arm dissolves: under <c>--roster legion</c> every class fields
    /// three bodies at tick 0 and the fabricator can field four, so 60 asks
    /// for a body nobody had at 60 in v1.
    /// <para>On a legion-less cell 60 is still the fabricator's own first
    /// unlock tick, and the striker/bulwark cost of the early errand is
    /// exactly the tension the arm is for: the first deposit is the one that
    /// most tempts a team into an unaffordable trip.</para>
    /// </summary>
    public const int VeinFirstSpawnTick = 60;

    /// <summary>
    /// Ticks between deposits (v1.1: 80 → 70). One harvester still services
    /// both sites of a cycle inside the cadence — 64 facing-locked ticks of
    /// the 70 — but there is no longer slack for a greedy double-run, so a
    /// team that wants both cycles' worth pays for a second body rather than
    /// scheduling one better.
    /// </summary>
    public const int VeinSpawnIntervalTicks = 70;

    /// <summary>
    /// Last deposit tick: the cadence-70 series runs 60 / 130 / 200 / 270 /
    /// 340 / 410 / 480 / 550 / 620 — nine events, filling the LONG horizon the
    /// owner opened (a deposit at 690 could not be banked, 16 ticks home, and
    /// converted before a 750-tick horn; it would be scenery, exactly as 440
    /// was under the old cadence and the old horn).
    /// <para>The schedule is one series rather than one per horizon: on a
    /// standard 500-tick cell the last three events simply never arrive, and
    /// a bot reads BOTH the schedule and <c>limits.maxTicks</c> from its
    /// contract, so nothing has to be inferred. The pot is 7 × 2 × 8 = 112
    /// scrap inside 500 ticks and 9 × 2 × 8 = 144 inside 750.</para>
    /// </summary>
    public const int VeinLastSpawnTick = 620;

    /// <summary>
    /// Scrap in one deposit (v1.1: 6 → 8). Total deposit supply is now
    /// 112 scrap inside a 500-tick match and 144 inside a 750-tick one, against
    /// v1's 48 — and that is the whole of "stronger": at a flat 10 per tier
    /// and a full board of six, a team that contests the lanes can now reach
    /// the whole board instead of finishing the match one tier in.
    /// <para>Eight is also one carry capacity plus a wreck's remainder, so a
    /// full deposit still cannot be lifted in one trip: the assay pays at the
    /// tile, the carry caps at 6, and the leftover stays on the floor for
    /// whoever comes back — the pile is still a place, not a package.</para>
    /// </summary>
    public const int VeinAmount = 8;

    /// <summary>
    /// Scrap a destroyed body leaves at its death tile (v1.1: 1 → 2). It does
    /// three jobs: it makes kills CONVERT, against the slate's own structural
    /// diagnosis that they do not; it keeps a team that never leaves the front
    /// in the economy, because corpses fall where it is standing and the assay
    /// pays in full at the tile with no transport; and it is the fabricator's
    /// damper, since the class that fields the most bodies and loses them
    /// fastest is the largest single supplier of scrap to its opponent.
    /// <para>Doubling it is the half of "stronger" that pays the team which
    /// never leaves the front, so the buff does not land only on the team
    /// that can afford errands — and it is the half that scales with the
    /// ROSTER arm, where there are twice as many bodies to leave wrecks.</para>
    /// </summary>
    public const int WreckAmount = 2;

    /// <summary>
    /// Banked instantly on stepping onto a pile. It is the floor under every
    /// trip — a fully-denied harvester still converted its walk into
    /// something — and it is what makes wreckage frictionless at the front.
    /// </summary>
    public const int AssayAmount = 1;

    /// <summary>
    /// Most scrap one body may carry. Deliberately UNCHANGED at v1.1's larger
    /// deposit: a body still cannot lift a whole vein, so the deposit is a
    /// place two trips (or two bodies) are spent on rather than a package one
    /// runner collects.
    /// </summary>
    public const int CarryCapacity = 6;

    /// <summary>
    /// Ticks a pile survives. Unchanged at v1.1, which means it is now ten
    /// ticks LONGER than the 70-tick cadence rather than exactly one cadence.
    /// Two consequences, both deliberate and both worth stating:
    /// <list type="bullet">
    /// <item>a deposit nobody took is still standing when the next one lands
    /// on the same tile, so the two MERGE and carry the later expiry — a
    /// neglected lane ACCUMULATES rather than evaporating. What grows is the
    /// prize, never the pace: extraction is still the assay plus a carry of
    /// six per visit, so a fat site is a reason to send a second body, which
    /// is exactly the allocation decision the arm exists to create;</item>
    /// <item>a pile nothing feeds — every wreck, and any displaced deposit —
    /// still dies on its own eighty ticks, so the front cannot stockpile
    /// corpses and cash them later.</item>
    /// </list>
    /// <para>
    /// Ablation debt: <c>scrap-pile-lifetime</c> now bundles the roll-over
    /// that the cadence change created.
    /// </para>
    /// </summary>
    public const int PileLifetimeTicks = 80;

    /// <summary>
    /// Hard bound on live piles, so the published collection is provably
    /// small and legible.
    /// </summary>
    public const int MaxSimultaneousPiles = 16;

    /// <summary>
    /// Where a load banks: the existing home-pad regions, indexed by team ID.
    /// Reusing them is the second reason this arm moves no map identity.
    /// </summary>
    public static ImmutableArray<string> BankRegionIds { get; } =
    [
        "team-0-home-pad",
        "team-1-home-pad",
    ];

    /// <summary>
    /// The whole board: two tiers on each of the three tracks. v1 capped the
    /// total at THREE, which was a philosophy — the economy supplements the
    /// game and never decides it — and the owner has overturned it: "ideally
    /// scraps should weigh in and decide the game / enable overpowering the
    /// opponent." A team that wins the economy war is now meant to look like
    /// it won something.
    /// <para>The overpower is bought in BREADTH, never in step size: the
    /// per-track cap stays 2 and every tier is still +1, so the class-gap
    /// admission rule holds tier for tier — a full board is +2 gun travel,
    /// +2 spawn health and +2 sight, at 60 scrap, against a v1.1 pot of 112
    /// inside 500 ticks and 144 inside 750. Winning every deposit and every
    /// wreck is therefore a real, reachable, visible advantage, and it is
    /// still bounded: six integer steps, and none of them buys a capture.</para>
    /// <para>The cap remains a declared contract fact rather than a removed
    /// one, so a bot still reads one number for "how much board is there" and
    /// the legality mask still closes when it is full.</para>
    /// </summary>
    public const int MaxTotalTiers = 6;

    /// <summary>
    /// Every tier costs the same. Flat pricing is what makes going DEEP (two
    /// in one track) and going BROAD (one in each of three) cost the same 30
    /// at every point in the match: tier 2 is never a discount for being
    /// ahead, volume discounts are structurally impossible, and the choice is
    /// made on effects rather than on economics. Escalating prices were
    /// redundant anyway, because tier 2's EFFECT is already naturally
    /// diminishing on two of the three tracks.
    /// </summary>
    public const int TierCost = 10;

    /// <summary>
    /// The tier price under <see cref="FrontlineLabsChassisArm.Unified"/>,
    /// where a purchased tier applies to EVERY body of the buying team rather
    /// than to the prime slot's lives (DECISIONS #194). Doubling is the
    /// conservative call: a legion team fields eight or nine bodies, so a
    /// scope change from one slot to all of them is worth far more than 2×,
    /// and pricing it at 2× deliberately leaves the economy stronger under the
    /// arm rather than pretending to neutralise it — the owner already ruled
    /// that scrap is allowed to decide the match.
    /// <para>It is a REGISTERED FACTOR (<c>chassis-unified-tier-price</c>)
    /// with 10 and 30 pre-registered beside it, which is why it is a sweepable
    /// value on the arm rather than a second constant: 10 is "the widened
    /// scope is free", 30 is "the widened scope costs a whole extra board",
    /// and a full board moves 60 → 120 here.</para>
    /// </summary>
    public const int UnifiedChassisTierCost = 20;

    /// <summary>Deepest tier on any one track.</summary>
    public const int MaxTierPerTrack = 2;

    /// <summary>The battlefield economy's plain arm token.</summary>
    public const string ArmToken = "scrap";

    /// <summary>
    /// The control arm's plain identity token. It is <c>flat</c> rather than
    /// the flag's own <c>scrap-flat</c>: the composite it appends to already
    /// names the economy, and the six extra characters do not fit beside the
    /// worst class pair inside the 64-character canonical budget.
    /// </summary>
    public const string FlatArmToken = "flat";

    /// <summary>
    /// EDGE: gun reach. Gap-preserving by the arithmetic that governs the
    /// whole ladder — every chassis moves by the same integer, so the 2-tile
    /// striker/bulwark spread is still 2 tiles afterwards. It buys the
    /// opening shot rather than the kill, which is a positional buff rather
    /// than a damage one, and its value is matchup-conditional in exactly the
    /// way a CHOOSABLE track should be: decisive in mirrors (9 out-ranges 8),
    /// corrective in fabricator-versus-striker (7→8 finally answers at the
    /// striker's maximum), and at tier 2 it covers all 21 columns of a side
    /// lane from the lane's centre.
    /// </summary>
    public const string EdgeTrackId = "edge";

    /// <summary>
    /// PLATE: maximum health, applied at spawn and never as a heal. The one
    /// deliberately corrective track — it compresses the shots-to-kill ratio
    /// toward one, which helps the class at the ladder floor most and the
    /// class at the top least. Against damage-1 guns, which is every gun in
    /// the game except the salvo fan, every tier moves the bolt count for
    /// every class; tier 1 is the single largest breakpoint in the design for
    /// the fabricator, whose 2-HP prime otherwise dies to ONE fan bolt.
    /// </summary>
    public const string PlateTrackId = "plate";

    /// <summary>
    /// OPTIC: sight range. Gap-preserving, and naturally terminal: every
    /// class closes its see-versus-shoot gap to zero at tier 2, which is
    /// exactly why a third tier would be worthless by construction. Its
    /// second job under this arm is interdiction — a carrier crossing the
    /// middle is exposed for 22 ticks, and a wider watcher nets a third more
    /// of that crossing.
    /// </summary>
    public const string OpticTrackId = "optic";

    /// <summary>
    /// The declared ladder, in declared order. Order is load bearing twice:
    /// tier vectors are published positionally against it, and it is the
    /// automatic buyer's tie-break under the control arm.
    /// <para>Fire cadence is deliberately NOT here. It is the one axis where
    /// an additive step to everybody WIDENS the class gap rather than
    /// preserving it — a striker needs 5 bolts to kill a bulwark prime and a
    /// bulwark needs 3 to kill a striker's, so at cooldowns 2 and 3 the base
    /// kill race is bulwark by two ticks, and giving both sides −1 makes it a
    /// dead tie. A single tier would erase the entire duel asymmetry the
    /// bulwark-versus-striker leg is priced on, and it would do so BECAUSE
    /// both teams bought it, which is the property that would make it
    /// undetectable as a one-sided problem.</para>
    /// </summary>
    public static ImmutableArray<FrontlineScrapTrackDefinition> Tracks { get; }
        =
        [
            Track(
                EdgeTrackId,
                FrontlineScrapEconomyDefinition.UpgradeEffectKind
                    .MobileAttackTravelTilesDelta),
            Track(
                PlateTrackId,
                FrontlineScrapEconomyDefinition.UpgradeEffectKind
                    .SpawnMaxHealthDelta),
            Track(
                OpticTrackId,
                FrontlineScrapEconomyDefinition.UpgradeEffectKind
                    .VisionRangeDelta),
        ];

    /// <summary>
    /// The declared economy for one arm level, or null for the inert level —
    /// in which case the canonical writer emits no bytes at all.
    /// </summary>
    /// <param name="chassis">
    /// The chassis arm the cell runs. Under
    /// <see cref="FrontlineLabsChassisArm.Unified"/> the ladder's scope widens
    /// from the prime slot's lives to every body of the team — a forced
    /// consequence of dissolving the prime, not a separable choice — and the
    /// price moves with it.
    /// </param>
    /// <param name="tierCost">
    /// The flat per-tier price, or null for the arm's own default (10 on the
    /// split chassis, <see cref="UnifiedChassisTierCost"/> on the unified
    /// one). A caller that names a price is running the registered
    /// <c>chassis-unified-tier-price</c> ablation, which spells its number in
    /// the ruleset identity exactly as every numbers-only level always has.
    /// </param>
    public static FrontlineScrapEconomyDefinition? For(
        FrontlineLabsEconomyArm economy,
        FrontlineLabsChassisArm chassis = FrontlineLabsChassisArm.Split,
        int? tierCost = null) =>
        economy switch
        {
            FrontlineLabsEconomyArm.None => null,
            FrontlineLabsEconomyArm.Scrap => Create(
                FrontlineScrapEconomyDefinition.PurchaseModeKind
                    .InvestAction,
                chassis,
                tierCost),
            FrontlineLabsEconomyArm.ScrapFlat => Create(
                FrontlineScrapEconomyDefinition.PurchaseModeKind
                    .AutomaticGreedyDeclaredOrder,
                chassis,
                tierCost),
            _ => throw new ArgumentOutOfRangeException(nameof(economy)),
        };

    /// <summary>
    /// The flat per-tier price one chassis arm ships with. Named here rather
    /// than inlined so the CLI, the ruleset identity and the ablation debt all
    /// read one number.
    /// </summary>
    public static int DefaultTierCost(FrontlineLabsChassisArm chassis) =>
        chassis == FrontlineLabsChassisArm.Unified
            ? UnifiedChassisTierCost
            : TierCost;

    private static FrontlineScrapEconomyDefinition Create(
        FrontlineScrapEconomyDefinition.PurchaseModeKind purchaseMode,
        FrontlineLabsChassisArm chassis,
        int? tierCost) =>
        new(
            VeinSites,
            VeinFirstSpawnTick,
            VeinSpawnIntervalTicks,
            VeinLastSpawnTick,
            VeinAmount,
            WreckAmount,
            AssayAmount,
            CarryCapacity,
            PileLifetimeTicks,
            MaxSimultaneousPiles,
            BankRegionIds,
            chassis == FrontlineLabsChassisArm.Unified
                ? FrontlineScrapEconomyDefinition.UpgradeScopeKind
                    .AllSlotLives
                : FrontlineScrapEconomyDefinition.UpgradeScopeKind
                    .PrimeSlotLivesOnly,
            MaxTotalTiers,
            purchaseMode,
            TracksAt(tierCost ?? DefaultTierCost(chassis)));

    private static ImmutableArray<FrontlineScrapTrackDefinition> TracksAt(
        int tierCost) =>
        tierCost == TierCost
            ? Tracks
            :
            [
                Track(
                    EdgeTrackId,
                    FrontlineScrapEconomyDefinition.UpgradeEffectKind
                        .MobileAttackTravelTilesDelta,
                    tierCost),
                Track(
                    PlateTrackId,
                    FrontlineScrapEconomyDefinition.UpgradeEffectKind
                        .SpawnMaxHealthDelta,
                    tierCost),
                Track(
                    OpticTrackId,
                    FrontlineScrapEconomyDefinition.UpgradeEffectKind
                        .VisionRangeDelta,
                    tierCost),
            ];

    private static FrontlineScrapTrackDefinition Track(
        string trackId,
        FrontlineScrapEconomyDefinition.UpgradeEffectKind effect,
        int tierCost = TierCost) =>
        new(
            trackId,
            effect,
            perTierMagnitude: 1,
            MaxTierPerTrack,
            [.. Enumerable.Repeat(tierCost, MaxTierPerTrack)]);
}
