using BotArena.Sdk;

/// <summary>
/// Firing geometry for a pose this body does not currently hold. FireControl
/// answers "what can I hit from here, now"; Lane answers "what could a gun
/// reach from THAT tile facing THAT way", which is the question a body has to
/// answer before it steps.
///
/// The whole revision rests on it. A straight-only chassis has exactly four
/// rays: an enemy one tile off any of them is unreachable no matter how much
/// health, range or patience the body has. Standing on a tile with no ray to a
/// body that has a ray to you is the losing move, and it is the move a
/// nearest-tile pathfinder makes every time.
/// </summary>
internal static class Lane
{
    private const int MaxProgramsPerPose = 96;

    /// <summary>
    /// Every tile a body of <paramref name="formId"/> could put a projectile on
    /// from <paramref name="from"/>, over the given facings, using its own
    /// declared aim and bend envelope. Walls terminate a path; other bodies are
    /// deliberately ignored, because this asks about reach rather than about
    /// this tick's exact interception.
    /// </summary>
    public static HashSet<Position> Reach(
        ContractView view,
        string formId,
        Position from,
        IReadOnlyList<Direction> facings,
        bool includeBends)
    {
        HashSet<Position> tiles = [];
        GenericActorRulesContract.AttackProfile? gun = view.Attack(formId);
        if (gun is null)
            return tiles;

        int maxTravel = Math.Max(1, gun.Projectile.MaxTravelTiles);
        Func<Position, bool> isWall = view.IsWall;

        // An omnidirectional gun ignores facing entirely: it names an absolute
        // eight-way heading. Model that as one traced ray per heading.
        if (gun.OmnidirectionalAim)
        {
            for (int heading = 0; heading < 8; heading++)
            {
                Add(
                    tiles,
                    isWall,
                    from,
                    Direction.North,
                    new ShotProgram(heading, 0, 0, 1, 0),
                    maxTravel);
            }
            return tiles;
        }

        int emitted = 0;
        foreach (Direction facing in facings)
        {
            foreach (ShotProgram program in Programs(gun, includeBends))
            {
                if (emitted++ >= MaxProgramsPerPose)
                    return tiles;
                Add(tiles, isWall, from, facing, program, maxTravel);
            }
        }
        return tiles;
    }

    /// <summary>
    /// True when a body of <paramref name="formId"/> standing at
    /// <paramref name="from"/> and facing <paramref name="facing"/> has a shot
    /// that lands on <paramref name="target"/>.
    /// </summary>
    public static bool Covers(
        ContractView view,
        string formId,
        Position from,
        Direction facing,
        Position target) =>
        Reach(view, formId, from, [facing], includeBends: true).Contains(target);

    /// <summary>
    /// The facing that opens a shot on <paramref name="target"/> from
    /// <paramref name="from"/>, preferring one this body already holds. Null
    /// when no facing does — the tile is simply not a firing position.
    /// </summary>
    public static Direction? FacingThatCovers(
        ContractView view,
        string formId,
        Position from,
        Direction preferred,
        Position target,
        IReadOnlyList<Direction> order)
    {
        if (Covers(view, formId, from, preferred, target))
            return preferred;
        foreach (Direction direction in order)
        {
            if (direction != preferred
                && Covers(view, formId, from, direction, target))
            {
                return direction;
            }
        }
        return null;
    }

    /// <summary>
    /// Tiles a visible enemy could put a bolt on. <paramref name="immediate"/>
    /// uses only the facing we can see, which is what it can do this tick;
    /// otherwise every facing, which is what it can do after one rotation.
    /// </summary>
    public static HashSet<Position> HostileReach(
        ContractView view,
        IEnumerable<GenericActorContext.ObservedEnemyState> enemies,
        bool immediate)
    {
        HashSet<Position> tiles = [];
        foreach (GenericActorContext.ObservedEnemyState enemy in enemies)
        {
            IReadOnlyList<Direction> facings =
                immediate ? [enemy.Facing] : Geometry.Cardinals;
            tiles.UnionWith(
                Reach(
                    view,
                    enemy.FormId,
                    enemy.Position,
                    facings,
                    includeBends: true));
        }
        return tiles;
    }

    private static IEnumerable<ShotProgram> Programs(
        GenericActorRulesContract.AttackProfile gun,
        bool includeBends)
    {
        GenericActorRulesContract.ShotProgramDefinition definition =
            gun.ShotProgram;
        if (!definition.Enabled)
        {
            yield return ShotProgram.Straight;
            yield break;
        }

        int minAim = Math.Min(
            definition.MinInitialAimSteps,
            definition.MaxInitialAimSteps);
        int maxAim = Math.Max(
            definition.MinInitialAimSteps,
            definition.MaxInitialAimSteps);
        maxAim = Math.Min(maxAim, minAim + 8);
        GenericActorRulesContract.AimOnlyShotProgramValue aimOnly =
            definition.AimOnlyProgram;

        for (int aim = minAim; aim <= maxAim; aim++)
        {
            yield return new ShotProgram(
                aim,
                aimOnly.BendDirection,
                aimOnly.BendAfterTiles,
                aimOnly.BendEveryTiles,
                aimOnly.BendCount);
            if (!includeBends)
                continue;
            foreach (int bend in definition.AllowedCurvedBendDirections)
            {
                for (int after = Math.Max(1, definition.MinBendAfterTiles);
                     after <= definition.MaxBendAfterTiles;
                     after++)
                {
                    yield return new ShotProgram(
                        aim,
                        bend,
                        after,
                        Math.Max(1, definition.MinBendEveryTiles),
                        Math.Max(1, definition.MinBendCount));
                }
            }
        }
    }

    private static void Add(
        HashSet<Position> tiles,
        Func<Position, bool> isWall,
        Position from,
        Direction facing,
        ShotProgram program,
        int maxTravel)
    {
        IReadOnlyList<Position> path;
        try
        {
            path = ShotPaths.Preview(from, facing, program, maxTravel, isWall);
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }
        foreach (Position tile in path)
            tiles.Add(tile);
    }
}
