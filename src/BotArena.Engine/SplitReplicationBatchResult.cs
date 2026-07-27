using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>Canonical result of one joint Split reservation batch.</summary>
public sealed record SplitReplicationBatchResult(
    ImmutableArray<SplitReplicationReservationOutcome> Outcomes)
{
    public ImmutableArray<SplitReplicationReservation> Reservations =>
        Outcomes
            .Where(outcome => outcome.Reservation is not null)
            .Select(outcome => outcome.Reservation!)
            .ToImmutableArray();
}
