namespace BotArena.Engine;

/// <summary>
/// Canonical pending state for a queued same-life form transition.
/// </summary>
public sealed record ActorSameLifeTransitionReservation(
    ActorIdentity SourceActorId,
    int ParticipantId,
    int SourceGeneration,
    string SourceFormId,
    string TargetFormId,
    Position SourcePosition,
    Direction SourceFacing,
    string TransitionId,
    string OperationId,
    int StartedTick,
    int DueTick);
