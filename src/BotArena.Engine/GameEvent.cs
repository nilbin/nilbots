
namespace BotArena.Engine;

public enum GameEventType
{
    Turn = 0,
    Move = 1,
    MoveBlocked = 2,
    Shot = 3,
    Damage = 4,
    Destroyed = 5,
    Fault = 6,
    Disqualified = 7,
}

/// <summary>
/// Flat, serialization-friendly authoritative event record. Unused fields stay null and are
/// omitted from the canonical replay JSON. Flat beats a class hierarchy here: the canonical
/// hash and the TypeScript viewer both need one stable shape.
/// </summary>
public sealed record GameEvent
{
    public required GameEventType Type { get; init; }
    public int? Slot { get; init; }
    public int? FromX { get; init; }
    public int? FromY { get; init; }
    public int? ToX { get; init; }
    public int? ToY { get; init; }
    public Direction? FromFacing { get; init; }
    public Direction? ToFacing { get; init; }
    public int? HitSlot { get; init; }
    public int? TargetSlot { get; init; }
    public int? Amount { get; init; }
    public int? NewHealth { get; init; }
    public string? Message { get; init; }

    public static GameEvent Turn(int slot, Position at, Direction from, Direction to) => new()
    {
        Type = GameEventType.Turn, Slot = slot, FromX = at.X, FromY = at.Y, FromFacing = from, ToFacing = to,
    };

    public static GameEvent Move(int slot, Position from, Position to) => new()
    {
        Type = GameEventType.Move, Slot = slot, FromX = from.X, FromY = from.Y, ToX = to.X, ToY = to.Y,
    };

    public static GameEvent MoveBlocked(int slot, Position from, Position attempted) => new()
    {
        Type = GameEventType.MoveBlocked, Slot = slot,
        FromX = from.X, FromY = from.Y, ToX = attempted.X, ToY = attempted.Y,
    };

    public static GameEvent Shot(int slot, Position from, Position end, int? hitSlot) => new()
    {
        Type = GameEventType.Shot, Slot = slot,
        FromX = from.X, FromY = from.Y, ToX = end.X, ToY = end.Y, HitSlot = hitSlot,
    };

    public static GameEvent Damage(int targetSlot, int bySlot, Position at, int amount, int newHealth) => new()
    {
        Type = GameEventType.Damage, Slot = bySlot, TargetSlot = targetSlot,
        FromX = at.X, FromY = at.Y, Amount = amount, NewHealth = newHealth,
    };

    public static GameEvent Destroyed(int slot, Position at) => new()
    {
        Type = GameEventType.Destroyed, Slot = slot, FromX = at.X, FromY = at.Y,
    };

    public static GameEvent Fault(int slot, string message) => new()
    {
        Type = GameEventType.Fault, Slot = slot, Message = message,
    };

    public static GameEvent Disqualified(int slot) => new()
    {
        Type = GameEventType.Disqualified, Slot = slot,
    };

    /// <summary>Positions used when filtering events into a bot's visible-events list.</summary>
    public IEnumerable<Position> ReferencePositions()
    {
        if (FromX is int fx && FromY is int fy)
            yield return new Position(fx, fy);
        if (ToX is int tx && ToY is int ty)
            yield return new Position(tx, ty);
    }
}
