namespace BotArena.Engine;

/// <summary>Canonical integer path generator shared by engine validation/tests.
/// The future path is authoritative replay data but never part of bot observation.</summary>
public static class ProgrammedProjectilePath
{
    public static IReadOnlyList<Position> Trace(
        ArenaMap map,
        Position origin,
        Direction facing,
        ShotProgram program,
        GameRules rules)
    {
        if (!rules.IsValidShotProgram(program))
            throw new ArgumentOutOfRangeException(nameof(program), "Shot program is outside the active rules.");

        var path = new List<Position>();
        var position = origin;
        var heading = facing.ToProjectileHeading().Turned(program.InitialAimOffset);
        int turns = 0;
        int range = Math.Max(0, rules.ShotRange);
        for (int tilesMoved = 0; tilesMoved < range; tilesMoved++)
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
            if (map.IsWall(next))
                break;
            // Strict diagonal corners match visibility: the center-to-center
            // segment may not clip either orthogonally adjacent wall.
            if (dx != 0 && dy != 0
                && (map.IsWall(position.Offset(dx, 0))
                    || map.IsWall(position.Offset(0, dy))))
                break;

            position = next;
            path.Add(position);
        }
        return path.ToArray();
    }
}
