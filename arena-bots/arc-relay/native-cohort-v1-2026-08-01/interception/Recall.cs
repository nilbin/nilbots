using BotArena.Sdk;

/// <summary>Participant-long memory of carrier sightings and loose Cores.</summary>
internal sealed class Recall
{
    private CarrierSighting? _carrier;

    public void Observe(MindContext mind, int ownTeamId)
    {
        GenericActorContext.ObservedEnemyState? carrier =
            ArenaBasics.VisibleEnemyCarrier(mind, ownTeamId);
        if (carrier is not null)
        {
            _carrier = new CarrierSighting(
                carrier.ActorId,
                carrier.Position,
                carrier.Health,
                carrier.ClassId,
                mind.Tick);
        }

        foreach (GenericActorContext.ObservedEvent observed in mind.VisibleEvents)
        {
            if (observed.Payload
                    is not GenericActorContext.EventPayload.ArcRelay arc)
            {
                continue;
            }
            if (arc.Fact is GenericActorContext.ArcRelayEvent.CoreDropped dropped
                && _carrier?.ActorId == dropped.SourceActorId)
            {
                _carrier = _carrier with
                {
                    Position = dropped.Position,
                    SeenAtTick = observed.SourceTick,
                };
            }
            else if (arc.Fact is GenericActorContext.ArcRelayEvent.CoreBanked banked
                && _carrier?.ActorId == banked.CarrierActorId)
            {
                _carrier = null;
            }
        }
    }

    public CarrierSighting? RecentCarrier(int tick, int maxAge = 8) =>
        _carrier is { } sighting && tick - sighting.SeenAtTick <= maxAge
            ? sighting
            : null;

    internal sealed record CarrierSighting(
        ActorIdentity ActorId,
        Position Position,
        int Health,
        string? ClassId,
        int SeenAtTick);
}
