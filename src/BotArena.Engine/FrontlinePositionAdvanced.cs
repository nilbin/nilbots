namespace BotArena.Engine;

/// <summary>The active objective moved one position toward the opponent.</summary>
public sealed record FrontlinePositionAdvanced(
    int Tick,
    int TeamId,
    int FromPositionIndex,
    int ToPositionIndex)
    : FrontlineControlTransition(Tick, TeamId);
