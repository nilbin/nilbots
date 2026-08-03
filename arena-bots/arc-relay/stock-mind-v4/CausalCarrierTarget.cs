using BotArena.Sdk;

/// <summary>
/// One exact, causally remembered carrier mission target. It never searches
/// hidden state or lets an unrelated Core satisfy the bound mission.
/// </summary>
internal sealed class CausalCarrierTarget
{
    internal CausalCarrierTarget(
        GenericActorContext.ArcRelayCoreId coreId,
        ActorIdentity carrier,
        Position lastSeenPosition,
        int lastSeenTick)
    {
        CoreId = coreId;
        Carrier = carrier;
        LastSeenPosition = lastSeenPosition;
        LastSeenTick = lastSeenTick;
    }

    internal GenericActorContext.ArcRelayCoreId CoreId { get; }
    internal ActorIdentity Carrier { get; private set; }
    internal Position LastSeenPosition { get; private set; }
    internal int LastSeenTick { get; private set; }

    internal void Observe(
        GenericActorContext.ArcRelayCoreState? core,
        IEnumerable<GenericActorContext.ObservedEnemyState> visibleEnemies,
        int ownTeamId,
        int tick)
    {
        if (core?.CarrierActorId is not { } carrier
            || carrier.TeamId == ownTeamId)
        {
            return;
        }
        GenericActorContext.ObservedEnemyState? enemy = visibleEnemies
            .SingleOrDefault(value => value.ActorId == carrier);
        if (enemy is null)
            return;
        Carrier = carrier;
        LastSeenPosition = enemy.Position;
        LastSeenTick = tick;
    }

    internal OperationTruth Success(
        IEnumerable<GenericActorContext.ArcRelayCoreState> visibleCores,
        int ownTeamId)
    {
        GenericActorContext.ArcRelayCoreState? core = visibleCores
            .SingleOrDefault(value => value.CoreId == CoreId);
        if (core is null)
            return OperationTruth.Unknown;
        return core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Loose
            || core.CarrierActorId?.TeamId == ownTeamId
                ? OperationTruth.True
                : OperationTruth.False;
    }

    internal OperationTruth Invalid(
        IEnumerable<GenericActorContext.ArcRelayCoreState> visibleCores,
        IEnumerable<GenericActorContext.ObservedEnemyState> visibleEnemies,
        int ownTeamId,
        int tick,
        int freshnessTicks,
        Func<Position, bool> insideMissionArea)
    {
        GenericActorContext.ArcRelayCoreState? core = visibleCores
            .SingleOrDefault(value => value.CoreId == CoreId);
        if (core is not null
            && (core.Disposition
                    == GenericActorContext.ArcRelayCoreDisposition.Loose
                || core.CarrierActorId?.TeamId == ownTeamId))
        {
            return OperationTruth.False;
        }
        if (core?.CarrierActorId is { } carrier
            && carrier.TeamId != ownTeamId
            && visibleEnemies.Any(value => value.ActorId == carrier))
        {
            return insideMissionArea(core.Position)
                ? OperationTruth.False
                : OperationTruth.True;
        }
        return tick - LastSeenTick > freshnessTicks
            ? OperationTruth.True
            : OperationTruth.False;
    }
}
