using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// One participant's complete tick under the mind profile: the budget it was
/// handed, every command it wrote with that command's admission outcome, the
/// own live bodies its decision map resolved over, and its fault if it had one
/// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §5.1).
/// <para>
/// <see cref="ResolvedBodies"/> covers EVERY own live body, including bodies
/// the mind did not command — those carry the synthetic <c>Wait</c>. That
/// preserves the property the per-life format had, that every body's tick is
/// accounted for, which both the replay validator and the ML story depend on.
/// P3 projects this record into the replay's <c>mindTurns</c>.
/// </para>
/// </summary>
/// <param name="TickFuelBudget">
/// <c>250M + 200M x liveOwnBodies</c>, a pure function of authoritative
/// tick-start state (§4.2). P1 computes and records it; P2's WASM runtime is
/// what actually meters against it.
/// </param>
/// <param name="RejectedIntents">
/// RESERVED (§11.3). Any submitted intent is recorded here and Rejected —
/// non-fatal — until a format with allied minds is admitted.
/// </param>
public sealed record GenericMindRuntimeTurn(
    int ParticipantId,
    int TeamId,
    long TickFuelBudget,
    int LiveOwnBodyCount,
    ImmutableArray<GenericMindCommandResolution> Commands,
    ImmutableArray<ActorIdentity> ResolvedBodies,
    ImmutableArray<GenericMindDeclaredIntent> RejectedIntents,
    GenericMindRuntimeFault? RuntimeFault);
