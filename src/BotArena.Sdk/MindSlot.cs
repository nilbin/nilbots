using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>
/// One of the mind's own stable unit slots, published EVERY TICK rather than
/// only at start.
///
/// <para>A slot is not a body. It persists while successive body lives become
/// active, pending, ready or permanently dormant, and
/// <see cref="MindBody.UnitId"/> is the handle that joins the two. That is why
/// a plan keyed by unit ID survives the death of the body executing it — the
/// thing per-life bots structurally could not do.</para>
///
/// <para>Publishing the table every tick is what makes "I have no bodies" an
/// ordinary state rather than a blind one: on a tick where every body is dead,
/// <see cref="State"/> still tells you the exact tick each one returns.</para>
/// </summary>
public sealed record MindSlot
{
    /// <summary>Creates one own-slot observation.</summary>
    /// <param name="unitId">Stable team-local unit identifier.</param>
    /// <param name="state">Current slot lifecycle state.</param>
    /// <param name="classId">
    /// The chassis this slot's bodies carry, or <see langword="null"/> when the
    /// ruleset declares no compositions.
    /// </param>
    /// <param name="candidateClassIds">
    /// Reserved. Empty means the slot's chassis is fixed, which is the only
    /// kind currently admitted.
    /// </param>
    /// <param name="selectedClassId">
    /// Reserved. Always <see langword="null"/> while every slot's chassis is
    /// fixed.
    /// </param>
    public MindSlot(
        int unitId,
        GenericActorContext.UnitSlotState state,
        string? classId = null,
        ImmutableArray<string> candidateClassIds = default,
        string? selectedClassId = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(unitId);
        ArgumentNullException.ThrowIfNull(state);
        UnitId = unitId;
        State = state;
        ClassId = classId is null
            ? null
            : GenericActorDynamicValueRules.SemanticId(
                classId,
                nameof(classId));
        CandidateClassIds = candidateClassIds.IsDefault
            ? []
            : candidateClassIds;
        SelectedClassId = selectedClassId is null
            ? null
            : GenericActorDynamicValueRules.SemanticId(
                selectedClassId,
                nameof(selectedClassId));
    }

    /// <summary>
    /// The stable team-local handle. It survives every death in this slot, and
    /// it is the correct key for any plan that must outlive a body.
    /// </summary>
    public int UnitId { get; }

    /// <summary>
    /// Current lifecycle state — the existing closed union, unchanged:
    /// <c>Active</c>, <c>AvailabilityPending</c>, <c>AutomaticReturnPending</c>,
    /// <c>Ready</c>, <c>FabricationPending</c>, <c>ReplicationPending</c> or
    /// <c>PermanentlyDormant</c>. The pending variants carry the exact tick the
    /// slot becomes a body again.
    /// </summary>
    public GenericActorContext.UnitSlotState State { get; }

    /// <summary>
    /// The chassis this slot's bodies carry, or <see langword="null"/> on a
    /// ruleset that declares no compositions. Under a mixed composition your
    /// army's capability set is per BODY, not per team — read each body's
    /// legality mask rather than assuming your class's shape.
    /// </summary>
    public string? ClassId { get; }

    /// <summary>
    /// Reserved for chassis chosen at activation. EMPTY means this slot's
    /// chassis is fixed, which is the only kind currently admitted, so a mind
    /// never has to branch on whether the mechanic exists.
    /// </summary>
    public ImmutableArray<string> CandidateClassIds { get; }

    /// <summary>
    /// Reserved for chassis chosen at activation: the chassis actually
    /// selected. Always <see langword="null"/> while every slot is fixed.
    /// </summary>
    public string? SelectedClassId { get; }
}
