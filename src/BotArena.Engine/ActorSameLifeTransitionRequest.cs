namespace BotArena.Engine;

/// <summary>One structurally admitted same-life transition action.</summary>
public sealed record ActorSameLifeTransitionRequest(
    ActorIdentity SourceActorId,
    string TransitionId,
    string OperationId);
