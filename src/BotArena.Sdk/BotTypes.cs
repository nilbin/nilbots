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
    StrafeLeft = 5,
    StrafeRight = 6,
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

    /// <summary>Move one tile perpendicular to your facing WITHOUT rotating (left of the
    /// facing vector). Only available when the active rules enable strafing (no shipped
    /// ruleset does yet — experiment arms only); under all other rules this becomes Wait
    /// with a Blocked result. Movement conflicts resolve exactly like MoveForward.</summary>
    public static BotAction StrafeLeft() => new(BotActionKind.StrafeLeft);

    /// <summary>Move one tile perpendicular to your facing WITHOUT rotating (right of
    /// the facing vector). See <see cref="StrafeLeft"/>.</summary>
    public static BotAction StrafeRight() => new(BotActionKind.StrafeRight);
}

public readonly record struct VisibleTile(Position Position, bool IsWall);

public sealed record VisibleEnemy(int Slot, Position Position, Direction Facing, int Health);

/// <summary>A bolt in flight on a tile you can see. Bolts occupy their tile — standing
/// on or stepping onto one is a hit — and advance along their direction on a fixed
/// cadence. A bolt never hits the bot that fired it. <c>TicksUntilAdvance</c> makes the
/// cadence computable: 1 means the bolt moves one tile along <c>Direction</c> THIS very
/// tick, immediately after movement resolves — so do not end this tick's move on its
/// next tile (nor on its current one: a bolt's tile is checked before AND after it
/// advances). <c>RemainingTiles</c> is how many more tiles it can advance before
/// despawning (−1 = uncapped); it is lethal on its final tile.</summary>
public sealed record VisibleProjectile(
    Position Position, Direction Direction, int OwnerSlot, int TicksUntilAdvance, int RemainingTiles);

/// <summary>Coarse 8-way bearing of a heard sound, relative to your position (not your
/// facing): the octant from you toward the source.</summary>
public enum SoundBearing
{
    North = 0,
    NorthEast = 1,
    East = 2,
    SouthEast = 3,
    South = 4,
    SouthWest = 5,
    West = 6,
    NorthWest = 7,
}

/// <summary>Coarse loudness band of a heard sound: Near is Chebyshev ≤ 2, Medium ≤ 5,
/// Far anything beyond that within the rules' hearing radius.</summary>
public enum SoundDistance
{
    Near = 0,
    Medium = 1,
    Far = 2,
}

/// <summary>A LOUD event (shot, damage, destruction, disqualification) from LAST tick
/// that happened beyond your sight but within hearing. Deliberately redacted: you learn
/// the kind, a coarse bearing, and a coarse distance band — never who, never exact
/// coordinates. Sound is a cue to investigate or evade, not a radar; it can also be a
/// decoy.</summary>
public sealed record HeardSound(VisibleEventKind Kind, SoundBearing Bearing, SoundDistance Distance);

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
