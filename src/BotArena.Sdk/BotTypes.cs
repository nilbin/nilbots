using System.ComponentModel;

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
    /// <summary>Research-arm only (see <see cref="Actions.StrafeLeft"/>); inert under
    /// every shipped ruleset. The value is pinned: it travels on the wire and appears
    /// in historical replays.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    StrafeLeft = 5,
    /// <summary>Research-arm only (see <see cref="Actions.StrafeRight"/>); inert under
    /// every shipped ruleset.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    StrafeRight = 6,
}

public readonly record struct BotAction(BotActionKind Kind, ShotProgram? ShotProgram = null);

/// <summary>Action factories — the only supported way to construct actions (plan §7).</summary>
public static class Actions
{
    public static BotAction Wait() => new(BotActionKind.Wait);
    public static BotAction MoveForward() => new(BotActionKind.MoveForward);
    public static BotAction TurnLeft() => new(BotActionKind.TurnLeft);
    public static BotAction TurnRight() => new(BotActionKind.TurnRight);
    public static BotAction Shoot() => new(BotActionKind.Shoot);
    /// <summary>Fire a privately programmed immutable path. If
    /// <see cref="BotContext.ShotPrograms"/> is null, the host safely blocks it.</summary>
    public static BotAction Shoot(ShotProgram program) => new(BotActionKind.Shoot, program);

    /// <summary>NOT AVAILABLE in the shipped game. Move one tile perpendicular to your
    /// facing without rotating — implemented only for the held `strafe` research arm.
    /// Under every shipped ruleset the engine converts this to Wait and reports
    /// <see cref="ActionResult.Blocked"/> on the following tick, which is
    /// indistinguishable from a move into a wall: bots that call it lose the tick and
    /// misdiagnose why. Facing is your only movement axis — a sidestep costs a turn
    /// then a move.</summary>
    [Obsolete("Strafing is not enabled by any shipped ruleset: this becomes Wait with a Blocked result. " +
        "Turn, then move. (Research arm `--rules strafe` only.)")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static BotAction StrafeLeft() => new(BotActionKind.StrafeLeft);

    /// <summary>NOT AVAILABLE in the shipped game. See <see cref="StrafeLeft"/>.</summary>
    [Obsolete("Strafing is not enabled by any shipped ruleset: this becomes Wait with a Blocked result. " +
        "Turn, then move. (Research arm `--rules strafe` only.)")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static BotAction StrafeRight() => new(BotActionKind.StrafeRight);
}

public readonly record struct VisibleTile(Position Position, bool IsWall);

public sealed record VisibleEnemy(int Slot, Position Position, Direction Facing, int Health);

/// <summary>A bolt in flight on a tile you can see. Bolts occupy their tile — standing
/// on or stepping onto one is a hit — and advance on a fixed cadence. A bolt never hits
/// its owner. <c>TicksUntilAdvance</c> 1 means it advances THIS tick after movement.
/// <c>TilesPerAdvance</c> is its ordered substep count; every intermediate tile can hit,
/// so speed-two never tunnels. <c>RemainingTiles</c> is further range (−1 = uncapped).
/// <c>Direction</c> is the cardinal launch direction retained for compatibility;
/// use <see cref="Heading"/> for the exact currently manifested eight-way heading.
/// Under programmed-shot rules a still-private committed bend may change a future
/// substep, so current heading is evidence rather than a promise of the whole path.</summary>
public sealed record VisibleProjectile(
    Position Position, Direction Direction, int OwnerSlot, int TilesPerAdvance,
    int TicksUntilAdvance, int RemainingTiles,
    ProjectileHeading? ProgrammedHeading = null)
{
    /// <summary>Exact currently revealed heading. Future programmed bends stay private.</summary>
    public ProjectileHeading Heading => ProgrammedHeading ?? Direction.ToProjectileHeading();
}

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

/// <summary>A LOUD event (shot, damage, destruction) from LAST tick that happened
/// beyond your sight but within hearing. Deliberately redacted: you learn the kind, a
/// coarse bearing, and a coarse distance band — never who, never exact coordinates.
/// Sound is a cue to investigate or evade, not a radar; it can also be a decoy.</summary>
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
