using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Complete common-host output for one exact active-life decision batch.
/// Newly disqualified participants are reported only after every active life
/// has received its canonical decision opportunity.
/// </summary>
public sealed record GenericActorRuntimeTickResult(
    int Tick,
    ImmutableArray<GenericActorRuntimeTurn> Turns,
    ImmutableArray<GenericActorRuntimeFault> Faults,
    ImmutableArray<int> NewlyDisqualifiedParticipantIds);
