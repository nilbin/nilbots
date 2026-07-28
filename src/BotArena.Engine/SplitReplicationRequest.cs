namespace BotArena.Engine;

/// <summary>One structurally admitted Split action in a joint decision batch.</summary>
public sealed record SplitReplicationRequest(
    ActorIdentity SourceActorId,
    string TransitionId,
    string OperationId);
