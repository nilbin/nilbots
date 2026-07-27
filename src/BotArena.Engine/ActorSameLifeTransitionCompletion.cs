namespace BotArena.Engine;

/// <summary>
/// Completion-time result for one pending same-life form transition.
/// </summary>
public sealed record ActorSameLifeTransitionCompletion(
    ActorSameLifeTransitionReservation Reservation,
    ActorSameLifeTransitionCompletion.CompletionOutcomeKind Outcome,
    ActorSameLifeTransitionCompletion.CancellationReason? Reason,
    ActorSameLifeTransitionCompletion.CompletedState? State)
{
    public enum CompletionOutcomeKind
    {
        Completed = 0,
        Cancelled = 1,
    }

    public enum CancellationReason
    {
        SourceUnavailable = 0,
        SourceIdentityChanged = 1,
        SourceStateChanged = 2,
        PlacementInvalid = 3,
    }

    public sealed record CompletedState(
        string FormId,
        Position Position,
        Direction Facing,
        int Health,
        int Cooldown,
        int? Energy);
}
