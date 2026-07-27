namespace BotArena.Engine;

/// <summary>Final cumulative result for one scoring team.</summary>
public sealed record FrontlineTeamMatchResult(
    int TeamId,
    FrontlineTeamOutcome Outcome,
    int ActiveHealth,
    long DamageDealt,
    IReadOnlyList<FrontlineUnitMatchResult> Units);
