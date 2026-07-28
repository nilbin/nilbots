using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>Canonical result of one joint bounded-child reservation batch.</summary>
public sealed record BoundedChildFabricationBatchResult(
    ImmutableArray<BoundedChildFabricationReservationOutcome> Outcomes)
{
    public ImmutableArray<BoundedChildFabricationProvisionalReservation>
        Reservations =>
        Outcomes
            .Where(outcome => outcome.Reservation is not null)
            .Select(outcome => outcome.Reservation!)
            .ToImmutableArray();
}
