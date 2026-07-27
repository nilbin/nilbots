using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>Complete result of one mode-neutral generic joint tick.</summary>
public sealed record GenericActorMatchStepResult(
    int Tick,
    GenericActorMatchPreparedTick TickStart,
    GenericActorRuntimeTickResult RuntimeTick,
    ImmutableArray<GenericActorMatchActorResolution> ActionResolutions,
    ImmutableArray<GenericActorRuntimeObservation.ObservedEvent> Events,
    GenericActorWorldSnapshot PostState,
    bool IsCompleted,
    GenericActorMatchResult? Result);
