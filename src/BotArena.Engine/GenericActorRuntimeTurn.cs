namespace BotArena.Engine;

/// <summary>
/// One common-host result in a canonically ordered joint actor batch.
/// Gameplay validation may still reject or block a structurally admitted
/// action after this boundary.
/// </summary>
public sealed record GenericActorRuntimeTurn(
    int ParticipantId,
    ActorIdentity ActorId,
    GenericActorRuntimeDecision? SubmittedDecision,
    GenericActorRuntimeDecision AcceptedDecision,
    GenericActorRuntimeActionResolution.ActionOutcome AdmissionOutcome,
    GenericActorRuntimeFault? RuntimeFault);
