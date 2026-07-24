namespace BotArena.Sdk;

/// <summary>Eight-way projectile travel heading. Bot facing remains cardinal.</summary>
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
}

/// <summary>
/// Private immutable path selected when firing. InitialAimOffset and BendDirection
/// are measured in 45-degree octants: -1 is left, +1 is right. A bend occurs after
/// BendAfterTiles, repeats every BendEveryTiles, and is applied BendCount times.
/// </summary>
public readonly record struct ShotProgram(
    int InitialAimOffset,
    int BendDirection,
    int BendAfterTiles,
    int BendEveryTiles,
    int BendCount)
{
    public static ShotProgram Straight => new(0, 0, 0, 1, 0);
}

/// <summary>Public action envelope for the active programmed-shot rules.</summary>
public sealed record ShotProgramLimits(
    int MaxInitialAimOctants,
    int MaxBendAfterTiles,
    int MaxBendEveryTiles,
    int MaxBendCount,
    int MaxPathTiles,
    int LaunchTiles,
    int TilesPerAdvance)
{
    /// <summary>Checks the same numeric and canonical-straight constraints as the host.</summary>
    public bool IsValid(ShotProgram program)
    {
        if (program.InitialAimOffset < -MaxInitialAimOctants
            || program.InitialAimOffset > MaxInitialAimOctants)
            return false;
        if (program.BendCount == 0)
            return program.BendDirection == 0
                && program.BendAfterTiles == 0
                && program.BendEveryTiles == 1;
        return program.BendDirection is -1 or 1
            && program.BendAfterTiles >= 1
            && program.BendAfterTiles <= MaxBendAfterTiles
            && program.BendEveryTiles >= 1
            && program.BendEveryTiles <= MaxBendEveryTiles
            && program.BendCount >= 1
            && program.BendCount <= MaxBendCount;
    }
}

/// <summary>Deterministic local preview of the path the engine will use.</summary>
public static class ShotPaths
{
    /// <param name="isWall">
    /// Wall-memory predicate. Null treats every tile as open. Diagonal movement uses
    /// strict corners: either orthogonally adjacent wall blocks that step.
    /// </param>
    public static IReadOnlyList<Position> Preview(
        Position origin,
        Direction facing,
        ShotProgram program,
        int maxPathTiles,
        Func<Position, bool>? isWall = null)
    {
        if (program.BendCount > 0
            && (program.BendDirection is not (-1 or 1)
                || program.BendAfterTiles < 1
                || program.BendEveryTiles < 1))
            throw new ArgumentOutOfRangeException(
                nameof(program),
                "A bending preview needs direction ±1 and positive bend timing.");

        var path = new List<Position>();
        var position = origin;
        var heading = facing.ToProjectileHeading().Turned(program.InitialAimOffset);
        int turns = 0;
        for (int tilesMoved = 0; tilesMoved < Math.Max(0, maxPathTiles); tilesMoved++)
        {
            bool shouldBend = turns < program.BendCount
                && tilesMoved >= program.BendAfterTiles
                && (tilesMoved - program.BendAfterTiles) % program.BendEveryTiles == 0;
            if (shouldBend)
            {
                heading = heading.Turned(program.BendDirection);
                turns++;
            }

            var (dx, dy) = heading.Vector();
            var next = position.Offset(dx, dy);
            if (isWall?.Invoke(next) == true)
                break;
            if (dx != 0 && dy != 0
                && (isWall?.Invoke(position.Offset(dx, 0)) == true
                    || isWall?.Invoke(position.Offset(0, dy)) == true))
                break;

            position = next;
            path.Add(position);
        }
        return path.ToArray();
    }
}
