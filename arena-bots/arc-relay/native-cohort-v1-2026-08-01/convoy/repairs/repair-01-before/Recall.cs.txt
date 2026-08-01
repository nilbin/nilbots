using BotArena.Sdk;

/// <summary>
/// Match-long Core memory. Wells publish whether their one unresolved Core
/// still exists, while exact Core state is visibility-filtered; retaining the
/// last seen tile lets a convoy finish investigating a contact after sight is
/// broken without pretending that the remembered tile is current truth.
/// </summary>
internal sealed class Recall
{
    private readonly Dictionary<GenericActorContext.ArcRelayCoreId, Sighting>
        _cores = [];

    public void Observe(MindContext mind)
    {
        if (mind.Mode is not GenericActorContext.ModeObservationState.ArcRelay arc)
            return;

        HashSet<GenericActorContext.ArcRelayCoreId> outstanding = arc.Wells
            .Where(well => well.OutstandingCoreId is not null)
            .Select(well => well.OutstandingCoreId!)
            .ToHashSet();
        foreach (GenericActorContext.ArcRelayCoreId forgotten
                 in _cores.Keys.Where(core => !outstanding.Contains(core)).ToArray())
        {
            _cores.Remove(forgotten);
        }

        foreach (GenericActorContext.ArcRelayCoreState core in arc.VisibleCores)
            _cores[core.CoreId] = new Sighting(core, mind.Tick);
    }

    public GenericActorContext.ArcRelayCoreState? CoreFrom(string wellId) =>
        _cores.Values
            .Where(sighting => string.Equals(
                sighting.Core.CoreId.SourceWellId,
                wellId,
                StringComparison.Ordinal))
            .OrderByDescending(sighting => sighting.SeenAtTick)
            .ThenByDescending(sighting => sighting.Core.CoreId.SourceOrdinal)
            .Select(sighting => sighting.Core)
            .FirstOrDefault();

    private sealed record Sighting(
        GenericActorContext.ArcRelayCoreState Core,
        int SeenAtTick);
}
