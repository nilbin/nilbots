using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Geometry for a declared strike's REAL cone (owner ruling 2026-08-06,
/// superseding the three-spoke fan): the filled 90° wedge between the
/// resolved heading's two adjacent sectors, out to the gun's reach. A tile
/// belongs to the cone when it sits within ±45° of the central heading
/// (boundary inclusive, so the old outer spokes are still the cone's
/// edges), within Chebyshev range — the game's movement metric, which is
/// also what the 8-way ray tracer meant by "range" — and the canonical
/// strike line from the origin reaches it without crossing a wall. The
/// same line is the delivery path at maturation, so a lit tile is exactly
/// a hittable tile.
/// </summary>
internal static class GenericActorStrikeCone
{
    /// <summary>
    /// Every tile of the cone, in canonical order: Chebyshev ring
    /// ascending, then row-major (Y, then X). The order is a
    /// serialization/equality rule for the frozen telegraph, not gameplay.
    /// </summary>
    public static ImmutableArray<Position> Tiles(
        ActorMapDefinition map,
        Position origin,
        ProjectileHeading centralHeading,
        int range,
        bool diagonalCornersMustBeClear)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfNegative(range);
        (int ux, int uy) = centralHeading.Vector();
        var tiles = new List<Position>();
        for (int dy = -range; dy <= range; dy++)
        {
            for (int dx = -range; dx <= range; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;
                if (!WithinSector(dx, dy, ux, uy))
                    continue;
                Position tile = origin.Offset(dx, dy);
                ImmutableArray<Position> line = LineTo(
                    map,
                    origin,
                    tile,
                    diagonalCornersMustBeClear);
                if (line.Length == 0 || line[^1] != tile)
                    continue;
                tiles.Add(tile);
            }
        }
        tiles.Sort((a, b) =>
        {
            int ring = origin.ChebyshevDistance(a)
                .CompareTo(origin.ChebyshevDistance(b));
            if (ring != 0)
                return ring;
            int row = a.Y.CompareTo(b.Y);
            return row != 0 ? row : a.X.CompareTo(b.X);
        });
        return [.. tiles];
    }

    /// <summary>
    /// Whether offset (dx, dy) lies within ±45° of heading (ux, uy):
    /// dot ≥ |cross|, both scaled by the same magnitudes so the compare
    /// stays integral. The 45° boundary itself is inside the cone.
    /// </summary>
    public static bool WithinSector(int dx, int dy, int ux, int uy)
    {
        int dot = dx * ux + dy * uy;
        int cross = dx * uy - dy * ux;
        return dot >= Math.Abs(cross);
    }

    /// <summary>
    /// The canonical 8-connected strike line from origin toward target:
    /// integer Bresenham, stopping before a wall (and before a cut corner
    /// when the profile demands clear diagonals — the same rule the 8-way
    /// path tracer applies). The returned path excludes the origin; a
    /// blocked line is simply shorter than the target distance.
    /// </summary>
    public static ImmutableArray<Position> LineTo(
        ActorMapDefinition map,
        Position origin,
        Position target,
        bool diagonalCornersMustBeClear)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (origin == target)
            return [];
        var path = ImmutableArray.CreateBuilder<Position>();
        int x = origin.X;
        int y = origin.Y;
        int dx = Math.Abs(target.X - x);
        int dy = Math.Abs(target.Y - y);
        int sx = Math.Sign(target.X - x);
        int sy = Math.Sign(target.Y - y);
        int error = dx - dy;
        while (x != target.X || y != target.Y)
        {
            int doubled = 2 * error;
            int stepX = 0;
            int stepY = 0;
            if (doubled > -dy)
            {
                error -= dy;
                stepX = sx;
            }
            if (doubled < dx)
            {
                error += dx;
                stepY = sy;
            }
            Position from = new(x, y);
            Position next = new(x + stepX, y + stepY);
            if (map.IsWall(next)
                || stepX != 0
                && stepY != 0
                && diagonalCornersMustBeClear
                && (map.IsWall(from.Offset(stepX, 0))
                    || map.IsWall(from.Offset(0, stepY))))
            {
                break;
            }
            x = next.X;
            y = next.Y;
            path.Add(next);
        }
        return path.ToImmutable();
    }
}
