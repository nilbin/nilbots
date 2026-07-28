namespace BotArena.Engine;

/// <summary>
/// A fully selected bounded-child bundle before joint family arbitration.
/// Once accepted, these captured facts are also the immutable pending work.
/// </summary>
public sealed record BoundedChildFabricationProvisionalReservation(
    ActorIdentity SourceActorId,
    int ParticipantId,
    int SourceGeneration,
    string SourceFormId,
    Position SourcePosition,
    Direction SourceFacing,
    string TransitionId,
    string OperationId,
    int TargetTeamId,
    int TargetUnitId,
    string TargetFormId,
    int TargetGeneration,
    int QueuedTick,
    int DueTick,
    Position ReservedPosition,
    Direction OutputFacing);
