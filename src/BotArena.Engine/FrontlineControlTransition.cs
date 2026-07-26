namespace BotArena.Engine;

/// <summary>A typed, immutable objective transition emitted by one step.</summary>
public abstract record FrontlineControlTransition(int Tick, int TeamId);
