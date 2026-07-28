using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>Complete result of one generic Deathmatch joint tick.</summary>
public sealed record GenericDeathmatchStepResult(
    int Tick,
    GenericDeathmatchTickStart TickStart,
    GenericActorRuntimeTickResult RuntimeTick,
    ImmutableArray<GenericDeathmatchActorResolution> ActionResolutions,
    ImmutableArray<GenericActorRuntimeObservation.ObservedEvent> Events,
    DeathmatchScoreState Scores,
    bool IsCompleted,
    GenericDeathmatchResult? Result);
