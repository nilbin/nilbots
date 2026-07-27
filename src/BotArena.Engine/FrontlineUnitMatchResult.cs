namespace BotArena.Engine;

/// <summary>Terminal state of one stable team-local unit slot.</summary>
public sealed record FrontlineUnitMatchResult(
    int TeamId,
    int UnitId,
    string FormId,
    FrontlineLifecycleStatus LifecycleStatus,
    FrontlineActorId? ActiveActorId,
    int Health,
    long DamageDealt);
