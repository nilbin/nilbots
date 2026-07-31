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
            : new GenericActorRuntimeFault(
                ParticipantId,
                ActorId,
                Stage,
                FaultCode,
                CumulativeFaultCount,
                DisqualificationTriggered);
}
