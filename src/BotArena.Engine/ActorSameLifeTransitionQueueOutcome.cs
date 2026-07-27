namespace BotArena.Engine;

/// <summary>Queue-time result for one same-life transition request.</summary>
public sealed record ActorSameLifeTransitionQueueOutcome(
    ActorSameLifeTransitionRequest Request,
    ActorSameLifeTransitionReservation? Reservation,
    ActorSameLifeTransitionQueueOutcome.QueueBlockReason? Reason)
{
    public enum QueueBlockReason
    {
        SourceIdentityChanged = 0,
        SourceStateIneligible = 1,
        AlreadyPending = 2,
        PlacementInvalid = 3,
        IrreversibleReturn = 4,
    }
}
