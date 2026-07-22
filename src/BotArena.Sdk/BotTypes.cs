namespace BotArena.Sdk;

public readonly record struct Position(int X, int Y)
{
    public Position Offset(int dx, int dy) => new(X + dx, Y + dy);

    public int ChebyshevDistance(Position other) =>
        Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

    public override string ToString() => $"({X},{Y})";
}

public enum Direction
{
    North = 0,
    East = 1,
    South = 2,
    West = 3,
}

public static class DirectionExtensions
{
    public static (int Dx, int Dy) Vector(this Direction direction) => direction switch
    {
        Direction.North => (0, -1),
        Direction.East => (1, 0),
        Direction.South => (0, 1),
        Direction.West => (-1, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };

    public static Direction TurnedRight(this Direction direction) => (Direction)(((int)direction + 1) % 4);

    public static Direction TurnedLeft(this Direction direction) => (Direction)(((int)direction + 3) % 4);
}

public enum ActionResult
{
    None = 0,
    Success = 1,
    Blocked = 2,
    OnCooldown = 3,
    Faulted = 4,
}

public enum BotActionKind
{
    Wait = 0,
    MoveForward = 1,
    TurnLeft = 2,
    TurnRight = 3,
    Shoot = 4,
}

public readonly record struct BotAction(BotActionKind Kind);

/// <summary>Action factories — the only supported way to construct actions (plan §7).</summary>
public static class Actions
{
    public static BotAction Wait() => new(BotActionKind.Wait);
    public static BotAction MoveForward() => new(BotActionKind.MoveForward);
    public static BotAction TurnLeft() => new(BotActionKind.TurnLeft);
    public static BotAction TurnRight() => new(BotActionKind.TurnRight);
    public static BotAction Shoot() => new(BotActionKind.Shoot);
}

public readonly record struct VisibleTile(Position Position, bool IsWall);

public sealed record VisibleEnemy(int Slot, Position Position, Direction Facing, int Health);

public enum VisibleEventKind
{
    Turn,
    Move,
    MoveBlocked,
    Shot,
    Damage,
    Destroyed,
    Fault,
    Disqualified,
}

/// <summary>
/// Something that happened LAST tick, delivered when part of it was within your vision.
/// <c>Slot</c> is the ACTING bot: the mover/turner, the shooter — for <c>Damage</c> it is
/// the bot that DEALT the damage, not the victim (compare it with <see cref="BotContext.Slot"/>
/// to attribute). <c>Position</c> is the event's primary tile: where the actor stood
/// (Turn/Move-from/Shot origin), or the victim's tile for Damage/Destroyed.
/// </summary>
public sealed record VisibleEvent(VisibleEventKind Kind, int? Slot, Position Position);
