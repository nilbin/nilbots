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
    GenericActorMatchResult? Result)
{
    /// <summary>
    /// One turn per ticking participant under the mind profile, and empty on
    /// the per-life profile. P3 projects these into the replay's
    /// <c>mindTurns</c>; until then they are the authoritative record of what
    /// each mind was handed (its fuel budget and live-body count), what it
    /// commanded, and which commands the host Rejected.
    /// </summary>
    public ImmutableArray<GenericMindRuntimeTurn> MindTurns { get; init; } =
        [];
}
