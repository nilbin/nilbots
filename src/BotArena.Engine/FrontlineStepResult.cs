namespace BotArena.Engine;

/// <summary>Complete authoritative result of one Frontline joint step.</summary>
public sealed record FrontlineStepResult(
    int Tick,
    FrontlineTickStart TickStart,
    IReadOnlyList<FrontlineActionResolution> ActionResolutions,
    IReadOnlyList<FrontlineMatchEvent> Events,
    IReadOnlyList<FrontlineProjectileTraversal> ProjectileTraversals,
    FrontlineControlState Control,
    bool MatchCompleted,
    FrontlineMatchResult? Result);
