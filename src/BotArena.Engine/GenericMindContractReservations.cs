using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// THE RESERVED CONTRACT-SIDE ALLOCATION for compositions and
/// chassis-at-activation
/// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §9.2, §10.1, §10.2;
/// DECISIONS #191 P0). Its wire counterpart is
/// <c>BotArena.Sdk.GenericMindWireFieldIds</c>.
/// <para>
/// Two different lifetimes live here and the difference matters. Per-slot
/// <c>classId</c> SHIPS in P1: it is additive under the #156 canonical
/// discipline, emitted only when a ruleset declares compositions, so every
/// existing contract keeps byte-identical topology and match fingerprints. The
/// <c>slotChassis</c> block, the <c>class-target</c> parameter kind, and the
/// <c>ClassTargetConstraint</c> are RESERVED AND REFUSED: the shape is
/// allocated so FOUNDRY is later a numbers-and-switch change rather than a
/// rework, and nothing executes.
/// </para>
/// </summary>
public static class GenericMindContractReservations
{
    /// <summary>
    /// Canonical topology property carrying a slot's declared chassis
    /// (§9.2). Written only when the ruleset declares compositions; both
    /// mirrors reject an explicit null so the absence has ONE encoding.
    /// </summary>
    public const string UnitSlotClassIdProperty = "classId";

    /// <summary>
    /// RESERVED canonical property naming the tagged chassis-selection block
    /// on a unit slot (§10.1). The canonical writer omits the whole block on
    /// a ruleset without compositions, and v1 hardcodes
    /// <see cref="SlotChassisFixedKind"/> everywhere.
    /// </summary>
    public const string SlotChassisProperty = "slotChassis";

    /// <summary>RESERVED member of <see cref="SlotChassisProperty"/>.</summary>
    public const string SlotChassisKindProperty = "kind";

    /// <summary>RESERVED member; required when the kind is <c>fixed</c>.</summary>
    public const string SlotChassisFixedClassIdProperty = "fixedClassId";

    /// <summary>
    /// RESERVED member; required, ORDERED, when the kind is
    /// <c>chosen-at-activation</c>. Entry zero is the declared default,
    /// because an automatic activation resolves at tick start inside
    /// <c>ApplyInitialUnlocks</c> where no bot is running and none can be —
    /// the alternative (a mid-lifecycle-phase callback) would break the
    /// frozen-observation invariant (§10.2).
    /// </summary>
    public const string SlotChassisCandidateClassIdsProperty =
        "candidateClassIds";

    /// <summary>RESERVED member; null in v1.</summary>
    public const string SlotChassisSelectionActionIdProperty =
        "selectionActionId";

    /// <summary>The only chassis-selection kind v1 admits.</summary>
    public const string SlotChassisFixedKind = "fixed";

    /// <summary>
    /// RESERVED chassis-selection kind. The resolved-match validator REFUSES
    /// it with a typed unsupported result; P9 flips that refusal to admission
    /// if the FOUNDRY direction is taken (§10.3).
    /// </summary>
    public const string SlotChassisChosenAtActivationKind =
        "chosen-at-activation";

    /// <summary>
    /// RESERVED ordinal on <see cref="ActorActionParameterKind"/> for the
    /// <c>class-target</c> argument <c>fabricate</c> may later declare
    /// (§10.2). The value is allocated but the member is deliberately NOT
    /// added: adding a variant to the bounded tagged union is a schema change,
    /// while reserving the ordinal is free. <c>ReservesTheNextParameterKind</c>
    /// pins that nothing else takes it.
    /// </summary>
    public const int ReservedClassTargetParameterKindOrdinal = 6;

    /// <summary>Canonical ID the reserved parameter kind will carry.</summary>
    public const string ReservedClassTargetParameterKindCanonicalId =
        "class-target";

    /// <summary>
    /// RESERVED legality-mask constraint paired with the reserved parameter
    /// kind: <c>ClassTargetConstraint(ImmutableArray&lt;string&gt;
    /// AllowedClassIds)</c> (§10.2).
    /// </summary>
    public const string ReservedClassTargetConstraintName =
        "ClassTargetConstraint";

    /// <summary>
    /// The registered composition set v1 pre-registers (§9.5). Three monos —
    /// byte-identical to today's class arms, which is the load-bearing
    /// property that keeps every wave-1..8 cell comparable — plus the two
    /// mixed presets that are the only new cells. Free composition is a later
    /// LEVEL with its own evaluation policy, never shipped alongside the
    /// profile.
    /// </summary>
    public static ImmutableArray<string> RegisteredCompositionTokens { get; } =
        ["bulwark", "fabricator", "spearhead", "striker", "warden"];

    /// <summary>
    /// Composition tokens that describe a MIXED army, i.e. the ones that make
    /// a topology profile ID differ from the counts-only label. A mono
    /// composition's token is its chassis ID and writes no per-slot
    /// <c>classId</c> at all, so it keeps today's exact bytes.
    /// </summary>
    public static ImmutableArray<string> RegisteredMixedCompositionTokens
    { get; } = ["spearhead", "warden"];

    /// <summary>
    /// Composition tokens must stay one word and at most ten characters,
    /// because the topology profile ID has its own 64-byte semantic-ID cap and
    /// the ruleset ID is already at 60 of 64 for the full game (§9.7).
    /// </summary>
    public const int MaxCompositionTokenLength = 10;

    /// <summary>
    /// RESERVED (§11.1). Declared-intent tag cap. The engine never delivers an
    /// intent and Rejects every submitted one; the bound exists so a document
    /// cannot smuggle an unbounded string through the reserved field.
    /// </summary>
    public const int MaxIntentTagUtf8Bytes = 32;

    /// <summary>
    /// RESERVED (§11.1). At most eight declared intents leave a mind per tick.
    /// </summary>
    public const int MaxDeclaredIntentsPerTick = 8;
}
