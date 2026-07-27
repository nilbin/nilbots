namespace BotArena.Engine;

/// <summary>Queue-time result for one requested Split.</summary>
public sealed record SplitReplicationReservationOutcome(
    SplitReplicationRequest Request,
    SplitReplicationReservationOutcome.SplitReservationOutcomeKind Outcome,
    SplitReplicationReservationOutcome.SplitReservationBlockReason? Reason,
    SplitReplicationReservation? Reservation)
{
    public enum SplitReservationOutcomeKind
    {
        Reserved = 0,
        Blocked = 1,
    }

    public enum SplitReservationBlockReason
    {
        SourceUnavailable = 0,
        SourceNotEligible = 1,
        InsufficientHealth = 2,
        InsufficientSlots = 3,
        InsufficientPositions = 4,
        ConflictingReservation = 5,
    }
}
