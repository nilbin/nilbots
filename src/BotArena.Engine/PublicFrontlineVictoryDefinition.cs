using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Exact initial-position, advance-direction, terminal-precedence, and
/// timeout-result semantics for the ordered Frontline positions.
/// </summary>
public sealed record PublicFrontlineVictoryDefinition(
    PublicFrontlineInitialPositionPolicy InitialPosition,
    ImmutableArray<PublicFrontlineTeamAdvance> TeamAdvances,
    PublicFrontlineCompletionPrecedence CompletionPrecedence,
    PublicFrontlineTimeoutResolution TimeoutResolution);
