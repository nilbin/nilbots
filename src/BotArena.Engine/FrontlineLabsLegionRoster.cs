using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Every number the LEGION roster ships with, in one place. None of them is
/// sacred: each is a lever registered as its own unattributed factor in
/// <c>balance/frontline-ablation-debt-v1.json</c>, so a measured legion effect
/// is not attributable to any one of them until the registered isolations run.
/// <para>The shape the owner asked for: three live bodies at tick zero (four
/// slots for the fabricator), two more slots around 150, three more around
/// 300 — eight at the horn, nine for the fabricator.</para>
/// </summary>
public static class FrontlineLabsLegionRoster
{
    /// <summary>
    /// Companion slots every class has at tick zero. For a class that receives
    /// companions automatically these are LIVE bodies in the initial
    /// deployment; for the fabricator they are unlocked slots its prime may
    /// fabricate from tick zero.
    /// <para>
    /// Ablation debt: <c>legion-opening-roster</c>.
    /// </para>
    /// </summary>
    public const int OpeningCompanionSlots = 2;

    /// <summary>
    /// The fabricator's extra opening slot — the owner's "even 4 for Fab".
    /// It is a FABRICABLE slot rather than a fourth free body, and that is
    /// the class-identity ruling: DECISIONS #154 made explicit forward
    /// fabrication the fabricator's one exclusive verb, and #187 kept body
    /// count as its monopoly. Handing it free pad-spawned companions would
    /// make it a striker with a wider chassis.
    /// <para>So its opening is priced exactly as its class prices everything:
    /// three prime actions, at one tick of fabrication delay each, spent
    /// before contact — and the bodies arrive in the FIELD beside the prime
    /// rather than on a pad twelve tiles behind the front, which is what the
    /// verb has always bought. It reaches four bodies inside the first ten
    /// ticks of a 500-tick match if it wants them, and it may instead spend
    /// those actions on the walk.</para>
    /// <para>
    /// Ablation debt: <c>legion-fabricator-opening</c>.
    /// </para>
    /// </summary>
    public const int FabricatorExtraOpeningSlots = 1;

    /// <summary>
    /// The mid-game tranche's absolute unlock tick. Deliberately 150 rather
    /// than the measured 120/130 companion ticks: it lands after the first
    /// front has been contested and after the first two SCRAP deposits
    /// (60 and 130), so the reinforcement answers a position that already
    /// exists instead of arriving with the opening.
    /// <para>
    /// Ablation debt: <c>legion-tranche-schedule</c> (both ticks and both
    /// widths are one bundled factor).
    /// </para>
    /// </summary>
    public const int MidTrancheUnlockTick = 150;

    /// <summary>Slots the mid-game tranche adds to every team.</summary>
    public const int MidTrancheSlots = 2;

    /// <summary>
    /// The late-game tranche's absolute unlock tick — three fifths through a
    /// 500-tick match, so the endgame roster has to be defended for 200 ticks
    /// rather than paraded for twenty.
    /// </summary>
    public const int LateTrancheUnlockTick = 300;

    /// <summary>Slots the late-game tranche adds to every team.</summary>
    public const int LateTrancheSlots = 3;

    /// <summary>
    /// The arm's own map generation. A roster is never an edit to an existing
    /// map: the shipped map goldens stay byte-exact and this third generation
    /// carries the extra mirror-fair spawn anchors and the pad that protects
    /// them, exactly as the MUSTER arm minted its own for the widened alcoves.
    /// </summary>
    public const string MapId = "frontline-labs-03-legion";

    /// <summary>The roster arm's plain identity token.</summary>
    public const string ArmToken = "levy";

    /// <summary>
    /// Companion spawn anchors for team 0, in slot order, on the legion map.
    /// Slots 1–2 keep the measured classes map's anchors exactly, so the
    /// opening geometry is unchanged; the tranches then walk outward in pairs
    /// that mirror across the map's centre ROW, with the late tranche's odd
    /// third slot landing on the centre row itself.
    /// <para>Team 1's anchors are these reflected across the map's fairness
    /// axis (<c>x → width-1-x</c>), which is the same construction the two
    /// prime spawns and every objective region use, and the arm's tests
    /// verify it the way the muster sites are verified.</para>
    /// <para>
    /// Ablation debt: <c>legion-pad-geometry</c> — the anchor tiles, the
    /// widened protected pad that covers them, and the widened banking region
    /// that comes with it are one bundled factor.
    /// </para>
    /// </summary>
    public static ImmutableArray<(int X, int Y)> TeamZeroCompanionAnchors
    { get; } =
    [
        (1, 6),
        (1, 8),
        (2, 6),
        (2, 8),
        (1, 5),
        (1, 9),
        (1, 7),
    ];

    /// <summary>
    /// The legion pad: the measured six-tile home pad plus the four tiles the
    /// extra anchors stand on, for team 0. It is SpawnProtected and it is the
    /// team's banking region, exactly as the six-tile pad was — a spawn anchor
    /// an enemy body can stand on is a reserved return that can be denied, and
    /// mirror fairness would then depend on which team got camped first.
    /// </summary>
    public static ImmutableArray<(int X, int Y)> TeamZeroPad { get; } =
    [
        (1, 5),
        (2, 5),
        (1, 6),
        (2, 6),
        (1, 7),
        (2, 7),
        (1, 8),
        (2, 8),
        (1, 9),
        (2, 9),
    ];

    /// <summary>
    /// Companion slots one class fields under this arm: two openers for every
    /// class, three for the fabricator, plus both tranches.
    /// </summary>
    public static int CompanionSlots(bool explicitForwardFabrication) =>
        OpeningCompanionSlots
        + (explicitForwardFabrication ? FabricatorExtraOpeningSlots : 0)
        + MidTrancheSlots
        + LateTrancheSlots;

    /// <summary>
    /// The absolute tick each companion slot becomes available, in slot order
    /// (unit IDs 1..N). Zero means "from the first tick": a live body in the
    /// initial deployment for an automatic class, and a fabricable slot for
    /// the fabricator.
    /// </summary>
    public static ImmutableArray<int> CompanionUnlockTicks(
        bool explicitForwardFabrication) =>
    [
        .. Enumerable.Repeat(
            0,
            OpeningCompanionSlots
            + (explicitForwardFabrication ? FabricatorExtraOpeningSlots : 0)),
        .. Enumerable.Repeat(MidTrancheUnlockTick, MidTrancheSlots),
        .. Enumerable.Repeat(LateTrancheUnlockTick, LateTrancheSlots),
    ];

    /// <summary>
    /// Whether a slot belongs to the late tranche — the bodies FIVE SLOTS was
    /// priced on. Where that skill is in the cell they keep its slower
    /// rebuild profile, so the arm still buys COUNT without buying TEMPO.
    /// </summary>
    public static bool IsLateTrancheSlot(
        bool explicitForwardFabrication,
        int companionIndex) =>
        companionIndex
        >= CompanionSlots(explicitForwardFabrication) - LateTrancheSlots;

    /// <summary>
    /// The map anchor ID for one companion slot. Slots that are fabricated
    /// rather than returned automatically never resolve one.
    /// </summary>
    public static string CompanionSpawnId(int teamId, int unitId) =>
        $"team-{teamId}-child-{unitId}";
}
