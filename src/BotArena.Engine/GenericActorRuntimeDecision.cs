using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>Raw generic actor reply before common-host admission.</summary>
public sealed record GenericActorRuntimeDecision(
    string ActionId,
    int ActionCode,
    ImmutableArray<GenericActorRuntimeActionArgument> Arguments,
    string? DebugMessage);
