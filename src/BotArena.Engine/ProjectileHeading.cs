namespace BotArena.Engine;

/// <summary>Eight-way projectile travel heading. Bot facing remains the separate,
/// cardinal-only <see cref="Direction"/> type.</summary>
public enum ProjectileHeading
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

public static class ProjectileHeadingExtensions
{
    public static (int Dx, int Dy) Vector(this ProjectileHeading heading) => heading switch
    {
        ProjectileHeading.North => (0, -1),
        ProjectileHeading.NorthEast => (1, -1),
        ProjectileHeading.East => (1, 0),
        ProjectileHeading.SouthEast => (1, 1),
        ProjectileHeading.South => (0, 1),
        ProjectileHeading.SouthWest => (-1, 1),
        ProjectileHeading.West => (-1, 0),
        ProjectileHeading.NorthWest => (-1, -1),
        _ => throw new ArgumentOutOfRangeException(nameof(heading)),
    };

    public static ProjectileHeading ToProjectileHeading(this Direction direction) =>
        (ProjectileHeading)((int)direction * 2);

    public static ProjectileHeading Turned(this ProjectileHeading heading, int octants) =>
        (ProjectileHeading)(((int)heading + octants % 8 + 8) % 8);

    /// <summary>
    /// The exactly opposite heading — four octants around. A deflecting
    /// projectile guard sends the bolt back along this heading, so the
    /// returned bolt retraces the approach vector rather than aiming.
    /// </summary>
    public static ProjectileHeading Reversed(this ProjectileHeading heading) =>
        heading.Turned(4);

    public static ProjectileHeading Between(Position from, Position to)
    {
        int dx = Math.Sign(to.X - from.X);
        int dy = Math.Sign(to.Y - from.Y);
        return (dx, dy) switch
        {
            (0, -1) => ProjectileHeading.North,
            (1, -1) => ProjectileHeading.NorthEast,
            (1, 0) => ProjectileHeading.East,
            (1, 1) => ProjectileHeading.SouthEast,
            (0, 1) => ProjectileHeading.South,
            (-1, 1) => ProjectileHeading.SouthWest,
            (-1, 0) => ProjectileHeading.West,
            (-1, -1) => ProjectileHeading.NorthWest,
            _ => throw new ArgumentException("Projectile path contains a zero-length step."),
        };
    }
}
