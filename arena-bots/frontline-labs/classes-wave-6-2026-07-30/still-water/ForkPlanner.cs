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
    /// Whether a legal trajectory from ANY cardinal facing reaches the tile.
    /// </summary>
    public static bool CanCover(
        Field field,
        Position from,
        Position to,
        GenericActorRulesContract.AttackProfile attack)
    {
        if (attack.OmnidirectionalAim)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            int far = Math.Max(Math.Abs(dx), Math.Abs(dy));
            int near = Math.Min(Math.Abs(dx), Math.Abs(dy));
            return far > 0
                && far <= attack.Projectile.MaxTravelTiles
                && (near == 0 || near == far)
                && field.ClearRay(
                    from,
                    to,
                    attack.Projectile.DiagonalCornersMustBeClear);
        }
        foreach (Direction facing in Field.Cardinals)
        {
            if (CanCoverFrom(field, from, to, attack, facing))
                return true;
        }
        return false;
    }

    /// <summary>
    /// THE INTERCEPTION TABLE, rebuilt for launch offsets.
    ///
    /// <para>A facing-relative gun that may offset its launch by one 45-degree
    /// sector fires down THREE lanes from one facing, and each of those lanes may
    /// still spend the one bend. The reachable set is no longer "the facing lane,
    /// or a bend off the dominant axis"; written in the facing frame — forward
    /// component <c>f</c>, rightward component <c>g</c> — it is the union of five
    /// families, and every one of them is a different tactical shot:</para>
    ///
    /// <list type="bullet">
    /// <item><b>lane</b> (aim 0, no bend): <c>g = 0</c>. Fastest arrival.</item>
    /// <item><b>slip</b> (aim ±1, no bend): <c>|g| = f</c>. The aim-only diagonal
    /// — legal with ZERO bends, and the exact tile family a one-bend envelope
    /// could never reach, because a diagonal has no dominant axis to bend
    /// from.</item>
    /// <item><b>fork</b> (aim 0, one bend after <c>f - |g|</c>): the wave-4 shot,
    /// still the deepest lie, still bounded by the bend window.</item>
    /// <item><b>flatten</b> (aim ±1 outward, bend back inward after <c>|g|</c>):
    /// a shallow diagonal that straightens. It reaches long, barely-offset tiles
    /// the fork cannot, because the fork would need a bend after
    /// <c>f - |g|</c> tiles and that is past the window.</item>
    /// <item><b>hook</b> (aim ±1 outward, bend the same way after <c>f</c>): a
    /// near-90-degree turn that reaches tiles MORE lateral than forward — a
    /// family the wave-4 table declared unreachable from this facing
    /// outright.</item>
    /// </list>
    ///
    /// <para>Each family names exactly one candidate program, so the whole table
    /// is five constructions and at most five verifications. The verification is
    /// the engine's own path rule, walked without allocating: a bend a wall
    /// truncates or a strict corner refuses is not cover, however good the angle
    /// looks.</para>
    /// </summary>
    public static bool CanCoverFrom(
        Field field,
        Position from,
        Position to,
        GenericActorRulesContract.AttackProfile attack,
        Direction facing) =>
        CoverKind(field, from, to, attack, facing) != Cover.None;

    /// <summary>
    /// How a trajectory reaches the tile, not merely whether one does. A lane or
    /// a slip commits nothing and arrives on the shortest path this chassis has;
    /// a curve spends the bend, arrives later, and can be eaten by a corner. Once
    /// a facing covers a body three ways, "covered" stops being a decision and
    /// the QUALITY of the cover is what a rotation is actually choosing between.
    /// </summary>
    public enum Cover
    {
        None = 0,

        /// <summary>Reached by a curve: later arrival, one commitment spent.</summary>
        Curved = 1,

        /// <summary>Reached with zero bends — the facing lane or an aim-only diagonal.</summary>
        Direct = 2,
    }

    public static Cover CoverKind(
        Field field,
        Position from,
        Position to,
        GenericActorRulesContract.AttackProfile attack,
        Direction facing)
    {
        if (attack.OmnidirectionalAim)
        {
            return CanCover(field, from, to, attack) ? Cover.Direct : Cover.None;
        }

        (int fx, int fy) = facing.Vector();
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;

        // The facing frame. "Right" is the facing turned one quarter the same way
        // the heading sectors turn, so a positive bend direction and a positive
        // lateral offset mean the same side of the body.
        int forward = (dx * fx) + (dy * fy);
        int right = (dx * -fy) + (dy * fx);
        int lateral = Math.Abs(right);
        int side = Math.Sign(right);

        // Every trajectory's first tile is one step along the launch heading, and
        // all three launch headings carry a forward component. A tile that is not
        // in front of this facing is not reachable from it at any curvature.
        if (forward <= 0)
            return Cover.None;

        int travel = attack.Projectile.MaxTravelTiles;
        var program = attack.ShotProgram;
        bool programmable = program.Enabled;
        int aimLow = programmable ? program.MinInitialAimSteps : 0;
        int aimHigh = programmable ? program.MaxInitialAimSteps : 0;
        bool mayAim = side != 0 && aimLow <= side && side <= aimHigh;
        bool mayBend = programmable && program.MaxBendCount > 0;
        int firstBend = Math.Max(1, program.MinBendAfterTiles);
        int lastBend = program.MaxBendAfterTiles;
        int every = Math.Max(1, program.MinBendEveryTiles);
        int bends = Math.Max(1, program.MinBendCount);

        // lane: straight down the facing.
        if (lateral == 0
            && forward <= travel
            && Reaches(field, from, facing, ShotProgram.Straight, travel, to))
        {
            return Cover.Direct;
        }

        // slip: the aim-only diagonal. Zero bends, so it is legal wherever the
        // launch offset itself is, which is the point of the whole arm.
        if (mayAim && lateral == forward && forward <= travel)
        {
            var aimOnly = program.AimOnlyProgram;
            var slip = new ShotProgram(
                side,
                aimOnly.BendDirection,
                aimOnly.BendAfterTiles,
                Math.Max(1, aimOnly.BendEveryTiles),
                aimOnly.BendCount);
            if (Reaches(field, from, facing, slip, travel, to))
                return Cover.Direct;
        }

        if (!mayBend || lateral == 0)
            return Cover.None;

        bool Curves(int aim, int bendDirection, int after)
        {
            if (after < firstBend || after > lastBend)
                return false;
            if (!program.AllowedCurvedBendDirections.Contains(bendDirection))
                return false;
            var curve = new ShotProgram(aim, bendDirection, after, every, bends);
            return Reaches(field, from, facing, curve, travel, to);
        }

        // fork: cardinal launch, one bend toward the target.
        if (lateral < forward
            && forward <= travel
            && Curves(0, side, forward - lateral))
        {
            return Cover.Curved;
        }

        // flatten: launch diagonally outward, then bend back onto the cardinal.
        if (mayAim
            && lateral < forward
            && forward <= travel
            && Curves(side, -side, lateral))
        {
            return Cover.Curved;
        }

        // hook: launch diagonally, then bend the same way onto the lateral. This
        // is how a facing reaches ground that is more to the side than in front.
        if (mayAim
            && lateral > forward
            && lateral <= travel
            && Curves(side, side, forward))
        {
            return Cover.Curved;
        }

        return Cover.None;
    }

    /// <summary>
    /// The engine's own path rule, walked in place. Allocation-free because the
    /// interception table asks this question thousands of times a tick.
    /// </summary>
    private static bool Reaches(
        Field field,
        Position origin,
        Direction facing,
        ShotProgram program,
        int maxTiles,
        Position target)
    {
        ProjectileHeading heading = facing
            .ToProjectileHeading()
            .Turned(program.InitialAimOffset);
        Position cursor = origin;
        int turns = 0;
        for (int moved = 0; moved < maxTiles; moved++)
        {
            if (turns < program.BendCount
                && moved >= program.BendAfterTiles
                && (moved - program.BendAfterTiles)
                    % Math.Max(1, program.BendEveryTiles) == 0)
            {
                heading = heading.Turned(program.BendDirection);
                turns++;
            }
            (int dx, int dy) = heading.Vector();
            Position next = cursor.Offset(dx, dy);
            if (field.IsWall(next))
                return false;
            if (dx != 0
                && dy != 0
                && (field.IsWall(cursor.Offset(dx, 0))
                    || field.IsWall(cursor.Offset(0, dy))))
            {
                return false;
            }
            if (next == target)
                return true;
            cursor = next;
        }
        return false;
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
