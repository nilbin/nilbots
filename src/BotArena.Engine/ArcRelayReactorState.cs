namespace BotArena.Engine;

/// <summary>One scoring team's public reactor and charge state.</summary>
public sealed record ArcRelayReactorState(
    int TeamId,
    Position Position,
    int ChargePips,
    int IntegritySegments);
