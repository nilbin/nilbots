using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// The Frontline Labs battlefield economy, as adopted
/// (<c>docs/DESIGN-SCRAP-ECONOMY-2026-07-30.md</c> §14.1 with the part-2
/// significance re-tune, DECISIONS #187). Everything here is a decision rather
/// than a suggestion, and every number below is one the ablation registry
/// names.
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
    /// First deposit tick. Not 60: striker and bulwark receive their first
    /// companion at 120 and the fabricator at 60, so a deposit at 60 would
    /// ask a striker to send its ONLY body away from the front and hand the
    /// fabricator a free two-body window. 120 is the tick at which every
    /// class has exactly two bodies — a class-fairness constraint rather than
    /// a pacing one.
    /// </summary>
    public const int VeinFirstSpawnTick = 120;

    /// <summary>
    /// Ticks between deposits. One harvester services both sites of a cycle
    /// in 64 of the 80 ticks without a greedy double-run.
    /// </summary>
    public const int VeinSpawnIntervalTicks = 80;

    /// <summary>
    /// Last deposit tick, so the schedule is 120 / 200 / 280 / 360. A deposit
    /// at 440 could not be banked (16 ticks home) and converted before the
    /// 500-tick horn; it would be scenery.
    /// </summary>
    public const int VeinLastSpawnTick = 360;

    /// <summary>
    /// Scrap in one deposit. Doubled from the first draft because the errand
    /// must be worth its risk once the capture channel makes it affordable at
    /// all: total deposit supply is 4 events × 2 sites × 6 = 48 scrap.
    /// </summary>
    public const int VeinAmount = 6;

    /// <summary>
    /// Scrap a destroyed body leaves at its death tile. It does three jobs:
    /// it makes kills CONVERT, against the slate's own structural diagnosis
    /// that they do not; it keeps a team that never leaves the front in the
    /// economy, because corpses fall where it is standing and the assay pays
    /// in full at the tile with no transport; and it is the fabricator's
    /// damper, since the class that fields the most bodies and loses them
    /// fastest is the largest single supplier of scrap to its opponent.
    /// </summary>
    public const int WreckAmount = 1;

    /// <summary>
    /// Banked instantly on stepping onto a pile. It is the floor under every
    /// trip — a fully-denied harvester still converted its walk into
    /// something — and it is what makes wreckage frictionless at the front.
    /// </summary>
    public const int AssayAmount = 1;

    /// <summary>
    /// Most scrap one body may carry: one full deposit's remainder plus a
    /// wreck.
    /// </summary>
    public const int CarryCapacity = 6;

    /// <summary>
    /// Ticks a pile survives — exactly one cadence, so an untaken deposit
    /// disappears as the next pair arrives and at most two deposit piles are
    /// ever live at once. It caps the RATE at which map control converts into
    /// bank: a dominant team cannot stockpile corpses near its front and cash
    /// them later, and a losing team has a real window to come back for
    /// ground it briefly lost.
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
    /// Hard cap on one team's total tiers. The ceiling is enforced by rules
    /// rather than by income, so even a total economic wipeout converts to
    /// three integer stat steps on one body — there is no quantity of
    /// harvesting that buys a capture.
    /// </summary>
    public const int MaxTotalTiers = 3;

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
    public static FrontlineScrapEconomyDefinition? For(
        FrontlineLabsEconomyArm economy) =>
        economy switch
        {
            FrontlineLabsEconomyArm.None => null,
            FrontlineLabsEconomyArm.Scrap => Create(
                FrontlineScrapEconomyDefinition.PurchaseModeKind
                    .InvestAction),
            FrontlineLabsEconomyArm.ScrapFlat => Create(
                FrontlineScrapEconomyDefinition.PurchaseModeKind
                    .AutomaticGreedyDeclaredOrder),
            _ => throw new ArgumentOutOfRangeException(nameof(economy)),
        };

    private static FrontlineScrapEconomyDefinition Create(
        FrontlineScrapEconomyDefinition.PurchaseModeKind purchaseMode) =>
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
            FrontlineScrapEconomyDefinition.UpgradeScopeKind
                .PrimeSlotLivesOnly,
            MaxTotalTiers,
            purchaseMode,
            Tracks);

    private static FrontlineScrapTrackDefinition Track(
        string trackId,
        FrontlineScrapEconomyDefinition.UpgradeEffectKind effect) =>
        new(
            trackId,
            effect,
            perTierMagnitude: 1,
            MaxTierPerTrack,
            [.. Enumerable.Repeat(TierCost, MaxTierPerTrack)]);
}
