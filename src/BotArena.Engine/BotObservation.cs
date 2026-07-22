namespace BotArena.Engine;

public readonly record struct ObservedTile(Position Position, bool IsWall);

public readonly record struct ObservedBot(int Slot, Position Position, Direction Facing, int Health);

/// <summary>
/// Everything a bot is allowed to know on one tick (plan §4.5). Built from the pre-tick state;
/// never contains the full map or the opponent's pending action.
/// </summary>
public sealed class BotObservation
{
    public required int Tick { get; init; }
    public required int Slot { get; init; }
    public required Position Position { get; init; }
    public required Direction Facing { get; init; }
    public required int Health { get; init; }
    public required int Cooldown { get; init; }
    /// <summary>Null when the rules have no energy system (rules 0.1).</summary>
    public int? Energy { get; init; }
    public required ActionResult PreviousActionResult { get; init; }
    public required IReadOnlyList<ObservedTile> VisibleTiles { get; init; }
    public required IReadOnlyList<ObservedBot> VisibleEnemies { get; init; }
    /// <summary>Previous-tick events whose reference positions are inside the current field of view.</summary>
    public required IReadOnlyList<GameEvent> VisibleEvents { get; init; }
}

/// <summary>A bot's answer for one tick, as seen by the engine (runtime-neutral, plan §8).</summary>
public sealed record BotDecision
{
    public required BotAction Action { get; init; }
    public string? DebugMessage { get; init; }
    public bool Faulted { get; init; }
    public string? FaultMessage { get; init; }

    public static BotDecision Of(BotAction action, string? debug = null) =>
        new() { Action = action, DebugMessage = debug };

    public static BotDecision Fault(string message) =>
        new() { Action = BotAction.Wait, Faulted = true, FaultMessage = message };
}
