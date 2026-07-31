using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Complete common-host output for one mind tick: one turn per non-disqualified
/// participant, and the FAN-OUT of those turns across every own live body.
/// <para>
/// The fan-out is what keeps the session unchanged. <c>PrepareTick()</c> ->
/// <c>Step()</c>, the 16 canonical tick phases, the re-entrancy guard, the
/// memoized prepared tick and the <c>(ParticipantId, ActorId)</c> ordering all
/// stay exactly as they are; what changes is that decisions are collected one
/// runtime per PARTICIPANT instead of one per life, and
/// <see cref="ToActorTickResult"/> hands the rest of the tick the same shape it
/// always received (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §2.4).
/// </para>
/// </summary>
public sealed record GenericMindRuntimeTickResult(
    int Tick,
    ImmutableArray<GenericMindRuntimeTurn> MindTurns,
    ImmutableArray<GenericActorRuntimeTurn> BodyTurns,
    ImmutableArray<GenericMindRuntimeFault> Faults,
    ImmutableArray<int> NewlyDisqualifiedParticipantIds)
{
    /// <summary>
    /// The per-life view the gameplay half of a tick consumes. A fault whose
    /// participant held no live body carries no actor identity and therefore
    /// no per-body event; its consequence travels in
    /// <see cref="NewlyDisqualifiedParticipantIds"/>, which is the fact that
    /// matters under the shipped allowance of zero.
    /// </summary>
    public GenericActorRuntimeTickResult ToActorTickResult() =>
        new(
            Tick,
            BodyTurns,
            [
                .. Faults
                    .Select(fault => fault.ToActorFault())
                    .Where(fault => fault is not null)
                    .Select(fault => fault!),
            ],
            NewlyDisqualifiedParticipantIds);
}
