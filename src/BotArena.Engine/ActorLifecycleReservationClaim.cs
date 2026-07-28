using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// One provisional lifecycle bundle's complete atomic claim set. The
/// operation ID is the shared arbitration identity across creation families.
/// </summary>
internal sealed record ActorLifecycleReservationClaim
{
    public ActorLifecycleReservationClaim(
        string operationId,
        ActorLifecycleReservationFamily family,
        IEnumerable<ActorLifecycleSlotClaim> slots,
        IEnumerable<Position> tiles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (!Enum.IsDefined(family))
            throw new ArgumentOutOfRangeException(nameof(family));
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(tiles);

        ActorLifecycleSlotClaim[] slotSnapshot = [.. slots];
        Position[] tileSnapshot = [.. tiles];
        if (slotSnapshot.Length == 0
            || tileSnapshot.Length == 0
            || slotSnapshot.Any(slot =>
                slot.TeamId < 0 || slot.UnitId < 0)
            || slotSnapshot.Distinct().Count() != slotSnapshot.Length
            || tileSnapshot.Distinct().Count() != tileSnapshot.Length)
        {
            throw new ArgumentException(
                "Lifecycle claims require non-empty, unique valid slots and tiles.");
        }

        OperationId = operationId;
        Family = family;
        Slots = slotSnapshot
            .OrderBy(slot => slot.TeamId)
            .ThenBy(slot => slot.UnitId)
            .ToImmutableArray();
        Tiles = tileSnapshot
            .OrderBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .ToImmutableArray();
    }

    public string OperationId { get; }
    public ActorLifecycleReservationFamily Family { get; }
    public ImmutableArray<ActorLifecycleSlotClaim> Slots { get; }
    public ImmutableArray<Position> Tiles { get; }
}

internal readonly record struct ActorLifecycleSlotClaim(
    int TeamId,
    int UnitId);

internal enum ActorLifecycleReservationFamily
{
    Fabrication = 0,
    Replication = 1,
}
