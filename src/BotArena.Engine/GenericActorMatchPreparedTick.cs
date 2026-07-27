using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Frozen pre-decision boundary for one generic actor match tick. Named
/// separately from the authoritative chronology tick-start record.
/// </summary>
public sealed record GenericActorMatchPreparedTick(
    int Tick,
    ImmutableArray<GenericActorRuntimeObservation> Observations,
    ImmutableArray<GenericActorRuntimeObservation.ObservedEvent>
        TickStartEvents);
