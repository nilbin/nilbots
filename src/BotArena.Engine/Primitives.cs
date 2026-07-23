namespace BotArena.Engine;

public readonly record struct Position(int X, int Y)
{
    public Position Offset(int dx, int dy) => new(X + dx, Y + dy);

    public int ChebyshevDistance(Position other) =>
        Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

    public override string ToString() => $"({X},{Y})";
}

/// <summary>Y grows downward, matching map tile-row order. North is up (-Y).</summary>
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

public enum BotAction
{
    Wait = 0,
    MoveForward = 1,
    TurnLeft = 2,
    TurnRight = 3,
    Shoot = 4,
    /// <summary>Move one tile perpendicular to facing without rotating (rules with
    /// AllowStrafe; otherwise validates to Wait/Blocked). Values 5-6 are additive —
    /// pre-0.3 artifacts never emit them (RULES-0.3-DESIGN §B).</summary>
    StrafeLeft = 5,
    StrafeRight = 6,
}

public enum ActionResult
{
    /// <summary>No previous action (tick 0).</summary>
    None = 0,
    Success = 1,
    Blocked = 2,
    OnCooldown = 3,
    Faulted = 4,
}

public enum BotStatus
{
    Active = 0,
    Destroyed = 1,
    Disqualified = 2,
}
