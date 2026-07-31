namespace BotArena.Engine;

/// <summary>
/// A stable, team-local body slot controlled by one submitted participant.
/// A participant may control more than one slot.
/// </summary>
/// <param name="ClassId">
/// The chassis this slot's bodies carry, or null on a ruleset that declares no
/// compositions (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §9.2).
/// <para>
/// Chassis identity moves from the PARTICIPANT to the SLOT so a participant can
/// command a composition and mono-class becomes the special case. The binding
/// point already existed — <c>ActorUnitSlotLifecycleAssignmentDefinition</c> is
/// keyed <c>(teamId, unitId)</c> and already carries a lifecycle profile and
/// allowed forms per slot — so this is a rewiring, not a capability.
/// </para>
/// <para>
/// It is emitted under the #156 additive-canonical discipline: the canonical
/// writer emits <c>classId</c> on a slot only when it is non-null, and both
/// mirrors reject an explicit null, so every existing contract keeps
/// byte-identical topology and match fingerprints and pinned
/// <c>frontline-labs-1</c> is untouched.
/// </para>
/// </param>
public sealed record PublicUnitSlot(
    int TeamId,
    int UnitId,
    int ControllerParticipantId,
    string? ClassId = null);
