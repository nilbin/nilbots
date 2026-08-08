using BotArena.Sdk;

/// <summary>
/// Deterministic local replay of the declared projectile path rule. It mirrors
/// the contract's launch, per-advance, wall-termination, and strict-diagonal
/// semantics so a shot is only ever committed when its own tiles are known.
/// </summary>
internal static class Ballistics
{
    /// <summary>
    /// Traces the tiles a projectile would enter, in order, stopping on the
    /// declared travel budget or the first wall the contract would consume it
    /// against.
    /// </summary>
    public static List<Position> Trace(
        Doctrine doctrine,
        Position origin,
        ProjectileHeading startHeading,
        int bendDirection,
        int bendAfterTiles,
        int bendEveryTiles,
        int bendCount,
        int maxTiles,
        bool diagonalCornersMustBeClear)
    {
        var path = new List<Position>();
        Position position = origin;
        ProjectileHeading heading = startHeading;
        int bendsApplied = 0;
        int interval = Math.Max(1, bendEveryTiles);
        for (int travelled = 0; travelled < maxTiles; travelled++)
        {
            if (bendsApplied < bendCount
                && travelled >= bendAfterTiles
                && (travelled - bendAfterTiles) % interval == 0)
            {
                heading = heading.Turned(bendDirection);
                bendsApplied++;
            }

            (int dx, int dy) = heading.Vector();
            Position next = position.Offset(dx, dy);
            if (doctrine.IsWall(next))
                break;
            if (diagonalCornersMustBeClear
                && dx != 0
                && dy != 0
                && (doctrine.IsWall(position.Offset(dx, 0))
                    || doctrine.IsWall(position.Offset(0, dy))))
            {
                break;
            }
            position = next;
            path.Add(position);
        }
        return path;
    }
}
