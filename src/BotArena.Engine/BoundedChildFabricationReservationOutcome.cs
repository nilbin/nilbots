namespace BotArena.Engine;

/// <summary>Queue-time result for one bounded-child fabrication request.</summary>
public sealed record BoundedChildFabricationReservationOutcome(
    BoundedChildFabricationRequest Request,
    BoundedChildFabricationReservationOutcome
        .FabricationReservationOutcomeKind Outcome,
    BoundedChildFabricationReservationOutcome
        .FabricationReservationBlockReason? Reason,
    BoundedChildFabricationProvisionalReservation? Reservation)
{
    public enum FabricationReservationOutcomeKind
    {
        Reserved = 0,
        Blocked = 1,
        Rejected = 2,
        Faulted = 3,
    }

    public enum FabricationReservationBlockReason
    {
        SourceUnavailable = 0,
        SourceNotEligible = 1,
        TargetUnavailable = 2,
        InsufficientPositions = 3,
        ConflictingReservation = 4,
    }
}
