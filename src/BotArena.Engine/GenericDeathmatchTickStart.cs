using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Frozen pre-decision boundary for one generic Deathmatch tick.
/// </summary>
public sealed record GenericDeathmatchTickStart(
    int Tick,
    ImmutableArray<GenericActorRuntimeObservation> Observations,
    ImmutableArray<GenericActorRuntimeObservation.ObservedEvent>
        TickStartEvents);
