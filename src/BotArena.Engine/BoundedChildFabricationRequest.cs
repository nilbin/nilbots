namespace BotArena.Engine;

/// <summary>
/// One structurally admitted explicit bounded-child creation request.
/// </summary>
public sealed record BoundedChildFabricationRequest(
    ActorIdentity SourceActorId,
    string TransitionId,
    string OperationId,
    int TargetTeamId,
    int TargetUnitId);
