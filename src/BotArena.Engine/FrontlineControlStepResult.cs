namespace BotArena.Engine;

/// <summary>Next objective state plus the optional discrete transition for this tick.</summary>
public sealed record FrontlineControlStepResult(
    FrontlineControlState State,
    FrontlineControlTransition? Transition);
