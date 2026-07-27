namespace BotArena.Engine;

/// <summary>Final cumulative result for one scoring team.</summary>
public sealed record FrontlineTeamMatchResult(
    int TeamId,
    FrontlineTeamOutcome Outcome,
    int FinalHealth,
    long DamageDealt,
    FrontlineLifecycleStatus FinalLifecycleStatus);
