using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// One REGISTERED composition: which chassis a participant's slots carry
/// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §9.5). Chassis identity
/// moves from the PARTICIPANT to the SLOT, so mono-class becomes the special
/// case rather than the only case, and a participant commands an army whose
/// bodies need not be the same thing.
/// <para>
/// The set is deliberately CLOSED. Free composition is 3 chassis over 8 slots
/// = 6,561 armies per side, which is not a countable balance read; the five
/// registered tokens are 15 unordered pairs, and each mixed one answers a
/// pre-registered question rather than filling a grid. Free composition stays
/// registered as the later <c>--compositions free</c> LEVEL with its own
/// population-sampling evaluation policy, and is not implemented here.
/// </para>
/// <para>
/// The three MONO compositions are byte-identical to today's class arms: a
/// mono composition declares no per-slot chassis at all, so it writes no
/// canonical bytes and every wave-1..8 cell stays comparable. That is the
/// load-bearing property of the whole mechanism.
/// </para>
/// </summary>
public sealed record FrontlineLabsComposition
{
    private FrontlineLabsComposition(
        string token,
        FrontlineLabsClassDefinition slotZero,
        ImmutableArray<FrontlineLabsClassDefinition> companionCycle)
    {
        Token = token;
        SlotZeroChassis = slotZero;
        CompanionCycle = companionCycle;
    }

    /// <summary>
    /// The registered token. It lives in the TOPOLOGY profile ID and never in
    /// the ruleset ID: the ruleset spells MECHANICS and the topology spells the
    /// ARMY, and the full game's ruleset ID is already at 60 of its 64
    /// characters (§9.7).
    /// </summary>
    public string Token { get; }

    /// <summary>
    /// The chassis on slot 0. It is also the composition's declared CLASS — the
    /// value that must agree with this team's side of <c>--classes</c> — so a
    /// composition is a departure from a declared class rather than a second
    /// matchmaking axis.
    /// </summary>
    public FrontlineLabsClassDefinition SlotZeroChassis { get; }

    /// <summary>
    /// The chassis the companion slots take, cycled in slot order (slot 1 takes
    /// entry 0, slot 2 entry 1, slot 3 entry 0 again, …). A cycle rather than a
    /// per-slot table because the LEGION roster's slot count is contract data
    /// that a tuning variant may move, and a composition must keep meaning the
    /// same army when it does.
    /// </summary>
    public ImmutableArray<FrontlineLabsClassDefinition> CompanionCycle { get; }

    /// <summary>Whether any slot in this composition carries a chassis other
    /// than slot 0's — i.e. whether it writes per-slot chassis bytes at
    /// all.</summary>
    public bool IsMixed =>
        CompanionCycle.Any(entry => entry.Id != SlotZeroChassis.Id);

    /// <summary>
    /// Every distinct chassis this composition fields, in canonical ordinal
    /// order. The rules catalog is built from the union of both teams' sets, so
    /// a mixed cell declares exactly the forms, profiles and routes it can
    /// actually put on the board.
    /// </summary>
    public ImmutableArray<FrontlineLabsClassDefinition> DistinctChassis =>
    [
        .. new[] { SlotZeroChassis }
            .Concat(CompanionCycle)
            .DistinctBy(entry => entry.Id)
            .OrderBy(entry => entry.Id, StringComparer.Ordinal),
    ];

    /// <summary>
    /// Whether this composition fields a FABRICATING chassis anywhere. It is a
    /// TEAM-level fact on purpose: it decides whether the roster's tranches are
    /// fabricated or activate automatically, and whether the team gets the
    /// fabricator's extra opening slot.
    /// <para>Reading it over the whole composition rather than over slot 0 is
    /// what makes both mixed presets answerable. <c>spearhead</c> asks whether
    /// a fabricator opening that BUILDS a mixed line beats mono, which needs
    /// fabricable tranches; <c>warden</c> asks whether the fabricator's
    /// monopoly survives being a companion, which needs the companion to have
    /// something to build. Keying on slot 0 would silence one or the
    /// other.</para>
    /// </summary>
    public bool Fabricates =>
        SlotZeroChassis.ExplicitForwardFabrication
        || CompanionCycle.Any(entry => entry.ExplicitForwardFabrication);

    /// <summary>The chassis on one slot of this composition.</summary>
    public FrontlineLabsClassDefinition ChassisForSlot(int unitId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(unitId);
        if (unitId == 0 || CompanionCycle.IsEmpty)
            return SlotZeroChassis;
        return CompanionCycle[(unitId - 1) % CompanionCycle.Length];
    }

    /// <summary>
    /// The per-slot chassis ID a topology publishes for one slot, or null when
    /// the composition is mono — a mono composition writes no per-slot chassis
    /// so it keeps today's exact topology bytes.
    /// </summary>
    public string? PublishedChassisIdForSlot(int unitId) =>
        IsMixed ? ChassisForSlot(unitId).Id : null;

    /// <summary>striker mono — the control, byte-identical to today.</summary>
    public static FrontlineLabsComposition Striker { get; } =
        Mono(FrontlineLabsClassDefinition.Striker);

    /// <summary>bulwark mono — the control, byte-identical to today.</summary>
    public static FrontlineLabsComposition Bulwark { get; } =
        Mono(FrontlineLabsClassDefinition.Bulwark);

    /// <summary>fabricator mono — the control, byte-identical to today.</summary>
    public static FrontlineLabsComposition Fabricator { get; } =
        Mono(FrontlineLabsClassDefinition.Fabricator);

    /// <summary>
    /// SPEARHEAD: a fabricator on slot 0 building a striker/bulwark line. The
    /// composition the mind's API most obviously enables, and the one that
    /// asks the base question — does mixing beat mono at all?
    /// </summary>
    public static FrontlineLabsComposition Spearhead { get; } =
        new(
            "spearhead",
            FrontlineLabsClassDefinition.Fabricator,
            [
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsClassDefinition.Bulwark,
            ]);

    /// <summary>
    /// WARDEN: a bulwark on slot 0 with fabricator and striker companions. It
    /// asks whether the fabricator's monopoly survives being a companion — a
    /// fabricating body that is not slot 0 tests whether the verb's value is
    /// the verb or the chassis.
    /// </summary>
    public static FrontlineLabsComposition Warden { get; } =
        new(
            "warden",
            FrontlineLabsClassDefinition.Bulwark,
            [
                FrontlineLabsClassDefinition.Fabricator,
                FrontlineLabsClassDefinition.Striker,
            ]);

    /// <summary>Every registered composition, in canonical token order.</summary>
    public static ImmutableArray<FrontlineLabsComposition> All { get; } =
        [Bulwark, Fabricator, Spearhead, Striker, Warden];

    /// <summary>
    /// The composition a class plays when none is declared: its own chassis
    /// everywhere. This is what makes the whole mechanism inert by default —
    /// every existing cell resolves to a mono composition and writes the bytes
    /// it always wrote.
    /// </summary>
    public static FrontlineLabsComposition MonoFor(
        FrontlineLabsClassDefinition entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        // Built from the PASSED chassis rather than looked up, because a
        // tuning variant (the five-slot rebuild clocks) hands this method a
        // modified record and the composition must carry that record forward
        // rather than the pristine registered one.
        return Mono(entry);
    }

    /// <summary>
    /// This composition with every chassis re-resolved through
    /// <paramref name="resolve"/>. It exists so an arm that TUNES a class —
    /// the five-slot rebuild clocks are the only one today — reaches the
    /// chassis inside a composition as well as the pair's own entries.
    /// </summary>
    public FrontlineLabsComposition WithChassis(
        Func<FrontlineLabsClassDefinition, FrontlineLabsClassDefinition>
            resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        return new FrontlineLabsComposition(
            Token,
            resolve(SlotZeroChassis),
            [.. CompanionCycle.Select(resolve)]);
    }

    public static FrontlineLabsComposition Parse(string token) =>
        All.FirstOrDefault(entry => entry.Token == token)
        ?? throw new ArgumentException(
            $"Unknown Frontline Labs composition '{token}'. Registered "
            + "compositions: "
            + string.Join(", ", All.Select(entry => entry.Token))
            + ". Free composition is a later registered level, not an "
            + "unregistered cell.",
            nameof(token));

    private static FrontlineLabsComposition Mono(
        FrontlineLabsClassDefinition entry) =>
        new(entry.Id, entry, [entry]);
}
