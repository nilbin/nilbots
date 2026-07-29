using BotArena.Sdk;

/// <summary>One legal trajectory and the tiles it sweeps, with impact timing.</summary>
internal sealed class ShotPlan
{
    public ShotPlan(ShotProgram program, bool usePayload, ProjectileHeading? heading)
    {
        Program = program;
        UsePayload = usePayload;
        Heading = heading;
    }

    public ShotProgram Program { get; }

    /// <summary>False when the action must be submitted without a payload.</summary>
    public bool UsePayload { get; }

    /// <summary>Absolute heading for eight-way aimed attacks; null for facing shots.</summary>
    public ProjectileHeading? Heading { get; }

    public List<(Position Tile, int Travel, int Offset)> Swept { get; } = [];

    /// <summary>
    /// Whether the programmed turn actually happens inside the tiles this
    /// trajectory really reaches. A bend the map eats — a wall in the way or a
    /// strict diagonal corner that refuses the turn — sweeps exactly the tiles
    /// the straight shot already sweeps, so committing to it buys nothing and
    /// merely spends the tempo of a curve.
    /// </summary>
    public bool BendRealized =>
        Program.BendCount > 0 && Swept.Count > Program.BendAfterTiles;

    /// <summary>Index into <see cref="Swept"/> of the first post-turn tile.</summary>
    public int FirstBentIndex =>
        Program.BendCount > 0 ? Program.BendAfterTiles : int.MaxValue;
}

/// <summary>
/// Trajectory algebra for a programmable projectile. The bend envelope, launch
/// phase, travel cadence, and corner rules are all read from the attack profile,
/// so the same code models my own fork and the opponent's guns.
/// </summary>
internal static class ForkPlanner
{
    private const int MaxPlans = 48;

    /// <summary>Tick offset at which a bolt reaches <paramref name="travel"/> tiles.</summary>
    public static int ImpactOffset(
        GenericActorRulesContract.AttackProfile attack,
        int travel)
    {
        var projectile = attack.Projectile;
        int launch = Math.Max(0, projectile.LaunchTiles);
        int perAdvance = Math.Max(1, projectile.TilesPerAdvance);
        int ticksPerAdvance = Math.Max(1, projectile.TicksPerAdvance);
        if (travel <= launch)
            return 0;
        int advances = (travel - launch + perAdvance - 1) / perAdvance;
        if (projectile.AdvancesOnLaunchTick)
            advances = Math.Max(0, advances - 1);
        return advances * ticksPerAdvance;
    }

    /// <summary>
    /// Every trajectory this attack profile can commit to from the given pose,
    /// truncated by walls and by the first body that stops a bolt.
    /// </summary>
    public static List<ShotPlan> Plans(
        Field field,
        Position origin,
        Direction facing,
        GenericActorRulesContract.AttackProfile attack,
        IReadOnlySet<Position> stoppers)
    {
        var plans = new List<ShotPlan>();
        var program = attack.ShotProgram;
        int maxTiles = attack.Projectile.MaxTravelTiles;

        if (attack.OmnidirectionalAim)
        {
            for (int sector = 0; sector < 8; sector++)
            {
                var heading = (ProjectileHeading)sector;
                var plan = new ShotPlan(ShotProgram.Straight, false, heading);
                TraceRay(field, origin, heading, maxTiles, attack, stoppers, plan);
                if (plan.Swept.Count > 0)
                    plans.Add(plan);
            }
            return plans;
        }

        bool straightWithoutPayload = !program.Enabled || program.PayloadOptional;
        bool zeroBendLegal = straightWithoutPayload || program.MinBendCount <= 0;
        if (zeroBendLegal)
        {
            var plan = new ShotPlan(
                ShotProgram.Straight,
                !straightWithoutPayload,
                null);
            Trace(field, origin, facing, plan.Program, maxTiles, attack, stoppers, plan);
            if (plan.Swept.Count > 0)
                plans.Add(plan);
        }

        if (!program.Enabled)
            return plans;

        // A trajectory whose turn never happens is not a curve; it is the
        // straight shot wearing a payload. Enumerating it lets an arbitrary
        // tie-break fire a "bend" into a strict corner, which is exactly the
        // commitment this doctrine claims never to spend.
        void Offer(ShotPlan plan)
        {
            if (plan.Swept.Count == 0)
                return;
            if (zeroBendLegal
                && plan.Program.BendCount > 0
                && !plan.BendRealized)
            {
                return;
            }
            plans.Add(plan);
        }

        int aimLow = Math.Max(-4, program.MinInitialAimSteps);
        int aimHigh = Math.Min(4, program.MaxInitialAimSteps);
        for (int aim = aimLow; aim <= aimHigh && plans.Count < MaxPlans; aim++)
        {
            if (aim != 0)
            {
                var aimOnly = program.AimOnlyProgram;
                var straight = new ShotPlan(
                    new ShotProgram(
                        aim,
                        aimOnly.BendDirection,
                        aimOnly.BendAfterTiles,
                        Math.Max(1, aimOnly.BendEveryTiles),
                        aimOnly.BendCount),
                    true,
                    null);
                Trace(
                    field, origin, facing, straight.Program, maxTiles, attack,
                    stoppers, straight);
                Offer(straight);
            }

            if (program.MaxBendCount <= 0)
                continue;

            foreach (int bendDirection in program.AllowedCurvedBendDirections)
            {
                if (bendDirection is not (-1 or 1))
                    continue;
                for (int after = Math.Max(1, program.MinBendAfterTiles);
                     after <= program.MaxBendAfterTiles && plans.Count < MaxPlans;
                     after++)
                {
                    for (int every = Math.Max(1, program.MinBendEveryTiles);
                         every <= Math.Max(1, program.MaxBendEveryTiles)
                             && plans.Count < MaxPlans;
                         every++)
                    {
                        int count = Math.Max(1, program.MinBendCount);
                        var plan = new ShotPlan(
                            new ShotProgram(aim, bendDirection, after, every, count),
                            true,
                            null);
                        Trace(
                            field, origin, facing, plan.Program, maxTiles, attack,
                            stoppers, plan);
                        Offer(plan);
                    }
                }
            }
        }
        return plans;
    }

    /// <summary>
    /// Whether a legal trajectory actually reaches the tile. The axis algebra
    /// selects the only candidate bend an envelope of one bend can use, and the
    /// engine's own path preview then decides it — a bend that a wall truncates
    /// or a strict corner refuses is not cover, however good the angle looks.
    /// </summary>
    public static bool CanCover(
        Field field,
        Position from,
        Position to,
        GenericActorRulesContract.AttackProfile attack)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        int far = Math.Max(Math.Abs(dx), Math.Abs(dy));
        int near = Math.Min(Math.Abs(dx), Math.Abs(dy));
        if (far == 0 || far > attack.Projectile.MaxTravelTiles)
            return false;

        bool strict = attack.Projectile.DiagonalCornersMustBeClear;
        if (attack.OmnidirectionalAim)
        {
            return (near == 0 || near == far)
                && field.ClearRay(from, to, strict);
        }
        if (near == 0)
            return field.ClearRay(from, to, strict);

        var program = attack.ShotProgram;
        if (!program.Enabled
            || program.MaxBendCount <= 0
            || program.MinBendEveryTiles > 1)
        {
            return false;
        }
        int bendAfter = far - near;
        if (bendAfter < Math.Max(1, program.MinBendAfterTiles)
            || bendAfter > program.MaxBendAfterTiles)
        {
            return false;
        }
        if (RequiredFacing(from, to) is not Direction facing)
            return false;

        int bendDirection = BendSign(facing, dx, dy);
        if (!program.AllowedCurvedBendDirections.Contains(bendDirection))
            return false;

        IReadOnlyList<Position> path = ShotPaths.Preview(
            from,
            facing,
            new ShotProgram(
                0,
                bendDirection,
                bendAfter,
                Math.Max(1, program.MinBendEveryTiles),
                Math.Max(1, program.MinBendCount)),
            attack.Projectile.MaxTravelTiles,
            field.IsWall);
        for (int index = 0; index < path.Count; index++)
        {
            if (path[index] == to)
                return true;
        }
        return false;
    }

    private static int BendSign(Direction facing, int dx, int dy) => facing switch
    {
        Direction.North => dx > 0 ? 1 : -1,
        Direction.South => dx > 0 ? -1 : 1,
        Direction.East => dy > 0 ? 1 : -1,
        _ => dy > 0 ? -1 : 1,
    };

    /// <summary>
    /// Cardinal facing a facing-relative shot needs to reach a tile: the
    /// dominant axis. An exact diagonal has no dominant axis, which is why a
    /// one-bend envelope can never cover it.
    /// </summary>
    public static Direction? RequiredFacing(Position from, Position to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        if (Math.Abs(dx) > Math.Abs(dy))
            return dx > 0 ? Direction.East : Direction.West;
        if (Math.Abs(dy) > Math.Abs(dx))
            return dy > 0 ? Direction.South : Direction.North;
        return null;
    }

    /// <summary>Reachability that also respects a facing-locked aim.</summary>
    public static bool CanCoverFrom(
        Field field,
        Position from,
        Position to,
        GenericActorRulesContract.AttackProfile attack,
        Direction facing)
    {
        if (attack.OmnidirectionalAim)
            return CanCover(field, from, to, attack);
        return RequiredFacing(from, to) == facing
            && CanCover(field, from, to, attack);
    }

    private static void Trace(
        Field field,
        Position origin,
        Direction facing,
        ShotProgram program,
        int maxTiles,
        GenericActorRulesContract.AttackProfile attack,
        IReadOnlySet<Position> stoppers,
        ShotPlan plan)
    {
        IReadOnlyList<Position> path = ShotPaths.Preview(
            origin,
            facing,
            program,
            maxTiles,
            field.IsWall);
        Collect(path, attack, stoppers, plan);
    }

    private static void TraceRay(
        Field field,
        Position origin,
        ProjectileHeading heading,
        int maxTiles,
        GenericActorRulesContract.AttackProfile attack,
        IReadOnlySet<Position> stoppers,
        ShotPlan plan)
    {
        (int dx, int dy) = heading.Vector();
        var path = new List<Position>();
        Position cursor = origin;
        bool strict = attack.Projectile.DiagonalCornersMustBeClear;
        for (int step = 0; step < maxTiles; step++)
        {
            Position next = cursor.Offset(dx, dy);
            if (field.IsWall(next))
                break;
            if (strict
                && dx != 0
                && dy != 0
                && (field.IsWall(cursor.Offset(dx, 0))
                    || field.IsWall(cursor.Offset(0, dy))))
            {
                break;
            }
            path.Add(next);
            cursor = next;
        }
        Collect(path, attack, stoppers, plan);
    }

    private static void Collect(
        IReadOnlyList<Position> path,
        GenericActorRulesContract.AttackProfile attack,
        IReadOnlySet<Position> stoppers,
        ShotPlan plan)
    {
        for (int index = 0; index < path.Count; index++)
        {
            int travel = index + 1;
            plan.Swept.Add((path[index], travel, ImpactOffset(attack, travel)));
            if (stoppers.Contains(path[index]))
                break;
        }
    }
}
