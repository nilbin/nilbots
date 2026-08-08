namespace BotArena.Engine;

/// <summary>
/// Participant-scoped evidence for one mind fault. The policy is not new — it
/// is the EXISTING policy applied to a coarser unit. GAME-MODE-ARCHITECTURE.md
/// §9 already says runtime faults are participant-scoped across every
/// controlled slot, life and runtime stage, and the fault COUNTER is already
/// participant-scoped, so its allowance needs no change
/// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §4.7).
/// <para>
/// And under the shipped contract the blast radius does not change at all:
/// <c>FrontlineLabsDefinition</c> constructs
/// <c>faultsAllowedBeforeDisqualification: 0</c>, so the FIRST runtime fault of
/// any kind already disqualifies the participant and permanently dormants every
/// one of its slots. A per-life trap costs the entire participant the match
/// today; a mind trap costs exactly the same. There is no regression to price,
/// and the allowance stays at 0 for the mind profile because raising it would
/// be a silent difficulty change confounding the null pin.
/// </para>
/// </summary>
/// <param name="ActorId">
/// The canonically first own live body at the moment of the fault, or null
/// when the participant held none. The mind is the faulting unit; this names a
/// body only so the existing team-private fault event has something to point
/// at.
/// </param>
public sealed record GenericMindRuntimeFault(
    int ParticipantId,
    int TeamId,
    ActorIdentity? ActorId,
    GenericActorRuntimeFault.FaultStage Stage,
    string FaultCode,
    long CumulativeFaultCount,
    bool DisqualificationTriggered)
{
    /// <summary>
    /// Projects onto the shared per-life fault record so the session's
    /// existing team-private <c>RuntimeFault</c> event, replay projection, and
    /// disqualification path stay byte-identical. Null when the participant
    /// held no live body to attribute the fault to.
    /// </summary>
    public GenericActorRuntimeFault? ToActorFault() =>
        ActorId is null
            ? null
            : ToActorFault(ActorId);

    /// <summary>
    /// The same participant-scoped fault, attributed to ONE own live body.
    /// <para>
    /// A trapped mind stops every body it owns, and the per-life evidence
    /// contract requires each body's faulted turn to carry a fault record
    /// naming THAT body — <c>GenericActorMatchActorTurn</c> refuses a turn
    /// whose fault names someone else. The event-facing projection
    /// (<see cref="ToActorFault()"/>) deliberately keeps naming the canonically
    /// first body so the single team-private fault event stays byte-identical;
    /// this overload exists for the body fan-out, where the participant's one
    /// fault is restated per body. Participant ID, stage, code, cumulative
    /// count and the disqualification flag are the participant's and are
    /// copied unchanged, so N restatements are still ONE fault.
    /// </para>
    /// </summary>
    public GenericActorRuntimeFault ToActorFault(ActorIdentity body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return new GenericActorRuntimeFault(
            ParticipantId,
            body,
            Stage,
            FaultCode,
            CumulativeFaultCount,
            DisqualificationTriggered);
    }
}
