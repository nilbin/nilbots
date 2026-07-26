namespace BotArena.Engine;

/// <summary>A team captured from the opponent-facing edge and won immediately.</summary>
public sealed record FrontlineBaseBreached(
    int Tick,
    int TeamId,
    int BreachedFromPositionIndex)
    : FrontlineControlTransition(Tick, TeamId);
