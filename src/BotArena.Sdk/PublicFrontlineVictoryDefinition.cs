using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>Exact movement and terminal semantics for ordered Frontline positions.</summary>
public sealed record PublicFrontlineVictoryDefinition(
    PublicFrontlineInitialPositionPolicy InitialPosition,
    ImmutableArray<PublicFrontlineTeamAdvance> TeamAdvances,
    PublicFrontlineCompletionPrecedence CompletionPrecedence,
    PublicFrontlineTimeoutResolution TimeoutResolution);
