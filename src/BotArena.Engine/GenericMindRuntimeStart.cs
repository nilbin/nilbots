namespace BotArena.Engine;

/// <summary>
/// Immutable initialization for one participant's mind, delivered once before
/// tick 0 (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §2.7). The mind
/// instance then lives the entire match; its fields ARE the persistent memory
/// and there is no Memory API to learn.
/// <para>
/// Two things are deliberately absent. The slot table's STATE is published
/// every tick in the observation instead (§13.2), and a life's ORIGIN moves
/// onto the body it belongs to — under the mind, an origin is a per-body fact
/// delivered on the tick that body first appears, not a start-time fact about
/// "me".
/// </para>
/// </summary>
/// <param name="MindRandomSeed">
/// Private stream, derived in the PARTICIPANT domain rather than the life
/// domain — one stream for the whole match instead of one per life.
/// </param>
/// <param name="TeamRandomSeed">
/// The DECISIONS #185 team seed, unchanged and still derived per SCORING TEAM;
/// only its consumer moved. Intra-mind it is pointless — a single mind does
/// not need to agree with itself — and it becomes load-bearing the day a 2v2
/// format is admitted. Record that so nobody deletes it in the meantime.
/// </param>
/// <param name="AlliedParticipantIds">
/// Other participants sharing this mind's scoring team, ascending. Empty in
/// head-to-head and in FFA-N; the 2v2 hook (§1.3).
/// </param>
public sealed record GenericMindRuntimeStart
{
    public required int SchemaVersion { get; init; }
    public required int RuntimeContractVersion { get; init; }
    public required int ParticipantId { get; init; }
    public required int TeamId { get; init; }
    public required ulong MindRandomSeed { get; init; }
    public required ulong TeamRandomSeed { get; init; }
    public required System.Collections.Immutable.ImmutableArray<int>
        AlliedParticipantIds
    { get; init; }
    public required ActorResolvedMatchDefinition Contract { get; init; }
}
