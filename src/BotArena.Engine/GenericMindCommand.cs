using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// One command a mind wrote onto one of its bodies. It is keyed by
/// <c>(unitId, lifeId)</c> WITHIN the commanding participant — there is no team
/// ID, because a mind can only command what it owns and the boundary should be
/// unspellable rather than merely checked
/// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §1.3, §2.4).
/// </summary>
/// <param name="RoleTag">
/// RESERVED (§12). Free-vocabulary lowercase kebab label, at most 24 UTF-8
/// bytes, non-authoritative: it cannot affect simulation state, is never an
/// action parameter, and the engine never branches on it. P2 ships
/// <c>SetRole</c>; P1 carries the field so the shape is fixed.
/// </param>
public sealed record GenericMindCommand(
    int UnitId,
    int LifeId,
    string ActionId,
    int ActionCode,
    ImmutableArray<GenericActorRuntimeActionArgument> Arguments,
    string? RoleTag = null,
    string? DebugMessage = null)
{
    /// <summary>The body this command addresses, inside a known team.</summary>
    public ActorIdentity ActorIdIn(int teamId) =>
        new(teamId, UnitId, LifeId);

    /// <summary>Projects the command onto the shared per-life reply shape.</summary>
    public GenericActorRuntimeDecision ToDecision() =>
        new(ActionId, ActionCode, Arguments, DebugMessage);
}
