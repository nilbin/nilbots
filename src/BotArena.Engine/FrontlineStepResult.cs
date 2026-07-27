namespace BotArena.Engine;

/// <summary>
/// Complete authoritative result of one Frontline joint step. Tick-start
/// lifecycle events live only on <see cref="TickStart"/>; <see cref="Events"/>
/// contains resolution-phase facts and never duplicates them.
/// </summary>
public sealed record FrontlineStepResult(
    int Tick,
    FrontlineTickStart TickStart,
    IReadOnlyList<FrontlineActionResolution> ActionResolutions,
    IReadOnlyList<FrontlineMatchEvent> Events,
    IReadOnlyList<FrontlineProjectileTraversal> ProjectileTraversals,
    FrontlineControlState Control,
    bool MatchCompleted,
    FrontlineMatchResult? Result);
