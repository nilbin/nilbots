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
        TickStartEvents)
{
    /// <summary>
    /// One frozen observation per ticking PARTICIPANT under the mind profile,
    /// and empty on the per-life profile. Exactly one of the two collections is
    /// populated: a match resolves one contract profile, and the profile is
    /// what decides whether decisions are collected per life or per mind
    /// (DECISIONS #191).
    /// <para>The team-shared union inside these is built once per team per
    /// tick and shared by reference, which is the O(N^2) -&gt; O(N) win the
    /// memo measured (§4.6).</para>
    /// </summary>
    public ImmutableArray<GenericMindRuntimeObservation> MindObservations
    { get; init; } = [];
}
