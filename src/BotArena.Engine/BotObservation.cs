namespace BotArena.Engine;

public readonly record struct ObservedTile(Position Position, bool IsWall);

public readonly record struct ObservedBot(int Slot, Position Position, Direction Facing, int Health);

/// <summary><c>TilesPerAdvance</c>: ordered tile substeps resolved on each advance;
/// intermediate walls and bots are never skipped. <c>TicksUntilAdvance</c>: engine
/// steps until this bolt advances — 1 means it advances THIS tick, immediately after
/// movement resolves.
/// <c>RemainingTiles</c>: further tiles it can advance before despawning (−1 = uncapped).
/// Dodge timing is computable, never measured (RULES-0.5-DESIGN §H item 2).</summary>
public readonly record struct ObservedProjectile(
    Position Position, Direction Direction, int OwnerSlot, int TilesPerAdvance,
    int TicksUntilAdvance, int RemainingTiles, ProjectileHeading? Heading = null);

/// <summary>A loud event beyond sight, redacted to type + coarse bearing octant
/// (<see cref="Hearing.BearingOctant"/>) + coarse distance band
/// (<see cref="Hearing.DistanceBand"/>) — never exact coordinates.</summary>
public readonly record struct HeardSound(GameEventType Type, int Bearing, int Distance);

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
    /// <summary>Arena dimensions — static per match, sent so bots can bound pathfinding
    /// without discovering edges by walking into them (gen-3 finding).</summary>
    public int MapWidth { get; init; }
    public int MapHeight { get; init; }
    /// <summary>Zone-control tiles; null when rules.ZoneControl is off.</summary>
    public IReadOnlyList<Position>? ZoneTiles { get; init; }
    /// <summary>Zone scores (mine, theirs) — public like any scoreboard. Null when off.</summary>
    public int? MyZoneTicks { get; init; }
    public int? EnemyZoneTicks { get; init; }
    /// <summary>Shared signed control pressure (positive = slot 0, negative = slot 1)
    /// and its current absolute domination limit. The limit may decrease when rules
    /// enter overtime. Null under passive zone rules.</summary>
    public int? ControlPressure { get; init; }
    public int? ControlPressureLimit { get; init; }
    public required ActionResult PreviousActionResult { get; init; }
    public required IReadOnlyList<ObservedTile> VisibleTiles { get; init; }
    public required IReadOnlyList<ObservedBot> VisibleEnemies { get; init; }
    /// <summary>Bolts in flight on visible tiles; null when these rules have instant
    /// shots (no projectile system).</summary>
    public IReadOnlyList<ObservedProjectile>? VisibleProjectiles { get; init; }
    /// <summary>Private programmed-shot action envelope; null when unsupported.</summary>
    public ShotProgramLimits? ShotPrograms { get; init; }
    /// <summary>Previous-tick events whose reference positions are inside the current field of view.</summary>
    public required IReadOnlyList<GameEvent> VisibleEvents { get; init; }
    /// <summary>Previous-tick LOUD events beyond sight but within the hearing radius,
    /// redacted to sounds; null when the rules have no hearing. Sighted events are
    /// delivered in <see cref="VisibleEvents"/> instead, never in both.</summary>
    public IReadOnlyList<HeardSound>? HeardSounds { get; init; }
}

/// <summary>A bot's answer for one tick, as seen by the engine (runtime-neutral, plan §8).</summary>
public sealed record BotDecision
{
    public required BotAction Action { get; init; }
    /// <summary>Private immutable projectile program. Null means the straight
    /// historical shot. It is never copied into an opponent observation.</summary>
    public ShotProgram? ShotProgram { get; init; }
    public string? DebugMessage { get; init; }
    public bool Faulted { get; init; }
    public string? FaultMessage { get; init; }

    public static BotDecision Of(BotAction action, string? debug = null) =>
        new() { Action = action, DebugMessage = debug };

    public static BotDecision Shoot(ShotProgram program, string? debug = null) =>
        new() { Action = BotAction.Shoot, ShotProgram = program, DebugMessage = debug };

    public static BotDecision Fault(string message) =>
        new() { Action = BotAction.Wait, Faulted = true, FaultMessage = message };
}
