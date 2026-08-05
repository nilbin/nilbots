namespace BotArena.Engine;

/// <summary>
/// One Core currently visible to the observation audience. An outstanding
/// Core whose tile and carrier are outside team vision is intentionally
/// absent even though its public Well still names the stable ID.
/// </summary>
public sealed record ArcRelayCoreState
{
    /// <summary>
    /// Charge this Core banks for (1 historically; base 2 under the
    /// charge-value primitive, up to the ripening cap).
    /// </summary>
    public int ChargeValue { get; init; } = 1;

    public ArcRelayCoreState(
        ArcRelayCoreId coreId,
        Position position,
        CoreDisposition disposition,
        ActorIdentity? carrierActorId,
        int nextRelocationTick,
        Position? flightTarget = null,
        int? flightCompletesAtTick = null)
    {
        if (!Enum.IsDefined(disposition))
            throw new ArgumentOutOfRangeException(nameof(disposition));
        if (nextRelocationTick < 0)
            throw new ArgumentOutOfRangeException(nameof(nextRelocationTick));
        if ((disposition == CoreDisposition.Carried)
            != (carrierActorId is not null))
        {
            throw new ArgumentException(
                "Only a carried Core names a carrier.",
                nameof(carrierActorId));
        }
        if ((disposition == CoreDisposition.InFlight)
            != (flightTarget is not null && flightCompletesAtTick is not null))
        {
            throw new ArgumentException(
                "Only a flying Core names a target and completion tick.",
                nameof(flightTarget));
        }
        if (flightCompletesAtTick < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(flightCompletesAtTick));
        }

        CoreId = coreId;
        Position = position;
        Disposition = disposition;
        CarrierActorId = carrierActorId;
        NextRelocationTick = nextRelocationTick;
        FlightTarget = flightTarget;
        FlightCompletesAtTick = flightCompletesAtTick;
    }

    public ArcRelayCoreId CoreId { get; }
    public Position Position { get; }
    public CoreDisposition Disposition { get; }
    public ActorIdentity? CarrierActorId { get; }
    public int NextRelocationTick { get; }
    public Position? FlightTarget { get; }
    public int? FlightCompletesAtTick { get; }

    public enum CoreDisposition
    {
        Loose = 0,
        Carried = 1,
        InFlight = 2,
    }
}
