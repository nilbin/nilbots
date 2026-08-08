using BotArena.Sdk;

/// <summary>
/// Contract-driven fire control. Every shot is simulated locally against the
/// declared projectile geometry before it is submitted, so LedgerFly never
/// spends a cooldown on a bolt that a wall would eat. It resolves whichever
/// attack language the current form actually has: an absolute eight-way
/// heading, a facing-relative shot program with legal bends, or a payload-free
/// straight bolt along the body's facing.
///
/// <para><b>The launch heading is now a decision of its own.</b> Where the
/// declared shot program carries an initial-aim range, a bolt may leave the
/// muzzle 45 degrees off facing with no bend at all - an ordinary straight ray
/// down a diagonal - so a body covers three of the eight rays out of its tile
/// without turning. The bounds come from <c>minInitialAimSteps</c> /
/// <c>maxInitialAimSteps</c> and the inert curvature such a payload must repeat
/// comes from the contract's own <c>aimOnlyProgram</c>, so nothing is assumed;
/// where the bounds are zero the whole family is empty and this file behaves
/// exactly as it did before. Order of preference is cheapest geometry first:
/// straight, then the diagonal ray, then the curve.</para>
///
/// <para>Two things the bolt has to answer for since the kit exists. First, a
/// lane that ends inside a deflecting arc is a lane that shoots us: the bolt
/// dies and a team-flipped copy launches back down the reversed heading, so the
/// simulation now checks the heading <b>at contact</b> against every visible
/// guard and refuses the shot - unless it is the third bolt, which shatters the
/// shield instead of being handed back. Second, a bend is worth a real
/// preference rather than a fallback: under a facing-locked profile the facing
/// IS the movement lane, so a curve that reaches a body 45 degrees off the lane
/// buys the shot without spending the rotation that would also throw away the
/// step. The straight bolt is still tried first, because when both reach, the
/// straight one arrives sooner and through fewer corner tests.</para>
/// </summary>
internal static class Gunnery
{
    private const int MaxProgramCandidates = 512;

    /// <summary>Attempts a shot that provably reaches one of the targets.</summary>
    public static GenericActorDecision? TryFire(
        MatchLens lens,
        GenericActorContext context,
        IReadOnlyList<GenericActorContext.ObservedEnemyState> targets,
        bool allowCurved,
        Stances? stances = null)
    {
        foreach (GenericActorContext.ObservedEnemyState target in targets)
        {
            GenericActorDecision? shot = Solve(
                lens,
                context,
                context.Self.Facing,
                target.Position,
                allowCurved,
                $"trading fire with {target.ActorId}",
                stances);
            if (shot is not null)
                return shot;
        }
        return null;
    }

    /// <summary>
    /// Rotates onto a facing that opens a firing solution. Only worth a tick
    /// when the gun is loaded, so callers gate it on a zero cooldown. Facings
    /// are tried in the caller's per-tick preference order rather than a fixed
    /// compass order, so two equally good lanes do not always resolve the same
    /// absolute way on a mirror-symmetric map.
    /// </summary>
    public static GenericActorDecision? TryRotateToAim(
        MatchLens lens,
        GenericActorContext context,
        IReadOnlyList<GenericActorContext.ObservedEnemyState> targets,
        bool allowCurved,
        Direction[] order,
        Stances? stances = null)
    {
        GenericActorActionLegality? rotate = Available(
            lens,
            context,
            GenericActorRulesContract.ActionKind.Rotation);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = Constraint<GenericActorActionLegality
                .ArgumentConstraint.DirectionConstraint>(rotate);
        if (rotate is null || directions is null)
            return null;

        foreach (Direction facing in order)
        {
            if (facing == context.Self.Facing
                || !directions.AllowedValues.Contains(facing))
            {
                continue;
            }
            foreach (GenericActorContext.ObservedEnemyState target in targets)
            {
                if (Solve(
                        lens,
                        context,
                        facing,
                        target.Position,
                        allowCurved,
                        null,
                        stances) is null)
                {
                    continue;
                }
                return new GenericActorDecision(
                    rotate.ActionId,
                    rotate.ActionCode,
                    [new GenericActorActionArgument.DirectionArgument(facing)],
                    $"turning {facing} to open a lane on {target.ActorId}");
            }
        }
        return null;
    }

    /// <summary>
    /// Suppression rather than concession: with the gun loaded, no clean
    /// solution, and a known body in the lane we are guarding, put a bolt down
    /// that lane instead of conceding the tempo. The bolt must still travel a
    /// real distance and must not be eaten by an ally.
    /// </summary>
    public static GenericActorDecision? TrySuppress(
        MatchLens lens,
        GenericActorContext context,
        IReadOnlyList<Position> knownEnemyTiles)
    {
        if (knownEnemyTiles.Count == 0)
            return null;
        GenericActorRulesContract.AttackProfile? profile =
            lens.Attack(context.Self.FormId);
        if (profile is null)
            return null;

        HashSet<Position> allies = AllyTiles(context);
        foreach (GenericActorActionLegality action in AttackActions(lens, context))
        {
            foreach ((ProjectileHeading heading,
                      ProjectileHeading? absolute,
                      ShotProgram program) in Lanes(
                          action,
                          profile,
                          context.Self.Facing))
            {
                List<Position> path = Path(
                    lens,
                    context.Self.Position,
                    heading,
                    0,
                    0,
                    1,
                    0,
                    profile.Projectile.MaxTravelTiles,
                    profile.Projectile.DiagonalCornersMustBeClear);
                if (path.Count < 4
                    || (!lens.AlliedProjectilesPassAllies
                        && path.Any(allies.Contains)))
                {
                    continue;
                }
                if (!knownEnemyTiles.Any(enemy =>
                        path.Any(tile => tile.ChebyshevDistance(enemy) <= 2)))
                {
                    continue;
                }
                GenericActorDecision? decision = Build(
                    action,
                    profile,
                    absolute,
                    program,
                    "suppressing the contested lane");
                if (decision is not null)
                    return decision;
            }
        }
        return null;
    }

    /// <summary>
    /// Straight, un-bent lanes this gun can put a bolt down from a given facing
    /// WITHOUT spending a rotation, each with the payload that selects it: the
    /// facing lane first, then every declared initial aim offset, then — for an
    /// absolute-heading gun such as a turret — its whole declared heading set.
    ///
    /// <para>This is the geometry the aim arm actually changes. A straight-only
    /// mobile gun fires along one of four cardinals from its tile, so at any
    /// distance three quarters of the tiles are unreachable and a facing-locked
    /// body's lane is also its movement lane. With a ±1 launch offset the same
    /// body covers THREE of the eight rays out of its tile without turning, which
    /// is what makes crossfire from two bodies a real geometry instead of a
    /// coincidence.</para>
    /// </summary>
    private static List<(ProjectileHeading Heading,
        ProjectileHeading? Absolute,
        ShotProgram Program)> Lanes(
        GenericActorActionLegality action,
        GenericActorRulesContract.AttackProfile profile,
        Direction facing)
    {
        var lanes =
            new List<(ProjectileHeading, ProjectileHeading?, ShotProgram)>();
        var headings = Constraint<GenericActorActionLegality
            .ArgumentConstraint.ProjectileHeadingConstraint>(action);
        if (headings is not null)
        {
            foreach (ProjectileHeading heading in headings.AllowedValues)
                lanes.Add((heading, heading, new ShotProgram(0, 0, 0, 1, 0)));
            return lanes;
        }

        ProjectileHeading forward = facing.ToProjectileHeading();
        lanes.Add((forward, null, new ShotProgram(0, 0, 0, 1, 0)));

        var programs = Constraint<GenericActorActionLegality
            .ArgumentConstraint.ShotProgramConstraint>(action);
        if (programs is not { Allowed: true } || !profile.ShotProgram.Enabled)
            return lanes;
        foreach (int offset in AimOffsets(profile.ShotProgram))
        {
            if (offset == 0)
                continue;
            lanes.Add((
                forward.Turned(offset),
                null,
                new ShotProgram(offset, 0, 0, 1, 0)));
        }
        return lanes;
    }

    /// <summary>
    /// Turns onto a facing whose lane is worth suppressing. A straight-only
    /// chassis fires exactly where it looks, so a body holding a contested tile
    /// whose current facing has no usable lane has a loaded gun and nothing to
    /// do with it - in wave 1 that tick became `wait` while the other side kept
    /// shooting. A rotation does not move the body, so it costs a tick and no
    /// extra exposure, and the shot lands on the next one.
    /// </summary>
    public static GenericActorDecision? TryRotateToSuppress(
        MatchLens lens,
        GenericActorContext context,
        IReadOnlyList<Position> knownEnemyTiles,
        Direction[] order)
    {
        if (knownEnemyTiles.Count == 0)
            return null;
        GenericActorRulesContract.AttackProfile? profile =
            lens.Attack(context.Self.FormId);
        GenericActorActionLegality? rotate = Available(
            lens,
            context,
            GenericActorRulesContract.ActionKind.Rotation);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = Constraint<GenericActorActionLegality
                .ArgumentConstraint.DirectionConstraint>(rotate);
        if (profile is null || rotate is null || directions is null)
            return null;

        HashSet<Position> allies = AllyTiles(context);
        List<GenericActorActionLegality> guns = AttackActions(lens, context);
        foreach (Direction facing in order)
        {
            if (facing == context.Self.Facing
                || !directions.AllowedValues.Contains(facing))
            {
                continue;
            }
            // Every lane the new facing would open, launch offsets included -
            // otherwise a rotation is judged on a third of what it actually
            // buys, and the body turns further than it needs to.
            foreach (GenericActorActionLegality gun in guns)
            {
                foreach ((ProjectileHeading heading, _, ShotProgram _) in Lanes(
                             gun,
                             profile,
                             facing))
                {
                    List<Position> path = Path(
                        lens,
                        context.Self.Position,
                        heading,
                        0,
                        0,
                        1,
                        0,
                        profile.Projectile.MaxTravelTiles,
                        profile.Projectile.DiagonalCornersMustBeClear);
                    if (path.Count < 4
                        || (!lens.AlliedProjectilesPassAllies
                            && path.Any(allies.Contains)))
                    {
                        continue;
                    }
                    if (!knownEnemyTiles.Any(enemy =>
                            path.Any(tile => tile.ChebyshevDistance(enemy) <= 2)))
                    {
                        continue;
                    }
                    return new GenericActorDecision(
                        rotate.ActionId,
                        rotate.ActionCode,
                        [new GenericActorActionArgument.DirectionArgument(facing)],
                        $"turning {facing} onto a lane worth suppressing");
                }
            }
        }
        return null;
    }

    /// <summary>Whether any attack action is available this tick.</summary>
    public static bool GunLoaded(MatchLens lens, GenericActorContext context) =>
        AttackActions(lens, context).Count > 0;

    private static GenericActorDecision? Solve(
        MatchLens lens,
        GenericActorContext context,
        Direction facing,
        Position target,
        bool allowCurved,
        string? reason,
        Stances? stances = null)
    {
        GenericActorRulesContract.AttackProfile? profile =
            lens.Attack(context.Self.FormId);
        if (profile is null)
            return null;

        Position origin = context.Self.Position;
        if (origin.ChebyshevDistance(target) > profile.Projectile.MaxTravelTiles)
            return null;

        HashSet<Position> allies = AllyTiles(context);
        HashSet<Position> enemies = context.Enemies
            .Select(enemy => enemy.Position)
            .ToHashSet();
        bool alliesBlock = !lens.AlliedProjectilesPassAllies;
        int maxTiles = profile.Projectile.MaxTravelTiles;
        bool strict = profile.Projectile.DiagonalCornersMustBeClear;

        foreach (GenericActorActionLegality action in AttackActions(lens, context))
        {
            var headings = Constraint<GenericActorActionLegality
                .ArgumentConstraint.ProjectileHeadingConstraint>(action);
            if (headings is not null)
            {
                foreach (ProjectileHeading heading in headings.AllowedValues)
                {
                    if (!Reaches(
                            lens,
                            origin,
                            heading,
                            0,
                            0,
                            1,
                            0,
                            maxTiles,
                            strict,
                            target,
                            enemies,
                            allies,
                            alliesBlock,
                            out ProjectileHeading arrival)
                        || !Worth(stances, target, arrival))
                    {
                        continue;
                    }
                    return Build(
                        action,
                        profile,
                        heading,
                        new ShotProgram(0, 0, 0, 1, 0),
                        reason ?? "aimed fire");
                }
                continue;
            }

            ProjectileHeading forward = facing.ToProjectileHeading();
            if (Reaches(
                    lens,
                    origin,
                    forward,
                    0,
                    0,
                    1,
                    0,
                    maxTiles,
                    strict,
                    target,
                    enemies,
                    allies,
                    alliesBlock,
                    out ProjectileHeading straightArrival)
                && Worth(stances, target, straightArrival))
            {
                return Build(
                    action,
                    profile,
                    null,
                    new ShotProgram(0, 0, 0, 1, 0),
                    reason ?? "straight fire");
            }

            var programs = Constraint<GenericActorActionLegality
                .ArgumentConstraint.ShotProgramConstraint>(action);
            if (programs is not { Allowed: true }
                || !profile.ShotProgram.Enabled)
            {
                continue;
            }

            foreach (ShotProgram program in Programs(profile.ShotProgram))
            {
                // A diagonal LAUNCH is not a curve: the aim-only family is a
                // straight ray on another heading, so a caller that refuses
                // curvature still gets its off-axis lanes.
                if (!allowCurved && program.BendCount > 0)
                    continue;
                ProjectileHeading start =
                    forward.Turned(program.InitialAimOffset);
                if (!Reaches(
                        lens,
                        origin,
                        start,
                        program.BendDirection,
                        program.BendAfterTiles,
                        program.BendEveryTiles,
                        program.BendCount,
                        maxTiles,
                        strict,
                        target,
                        enemies,
                        allies,
                        alliesBlock,
                        out ProjectileHeading curvedArrival)
                    || !Worth(stances, target, curvedArrival))
                {
                    continue;
                }
                return Build(
                    action,
                    profile,
                    null,
                    program,
                    reason ?? "curved intercept");
            }
        }
        return null;
    }

    /// <summary>
    /// Every payload this gun may legally submit that is not the plain straight
    /// bolt, cheapest geometry first.
    ///
    /// <para>The first family is new and it is the whole reason a facing-locked
    /// body is no longer a one-lane weapon: an <b>aim-only</b> program launches
    /// at 45 degrees off facing with <b>zero bends</b>, so the bolt is an
    /// ordinary straight ray down a diagonal. The declared bounds
    /// (<c>minInitialAimSteps</c>/<c>maxInitialAimSteps</c>) say how far the
    /// launch may swing, and the contract's own <c>aimOnlyProgram</c> carries the
    /// inert curvature such a payload must repeat - so the sentinel values are
    /// read, never invented (see <see cref="Build"/>). Where the bounds are zero
    /// this family is empty and nothing changes.</para>
    ///
    /// <para>Then the curved family, offsets combined with the one bend, which is
    /// what revision 4 already fired. Straight first, then the diagonal ray,
    /// then the curve: fewer corner tests and an earlier arrival win the tie.</para>
    /// </summary>
    private static IEnumerable<ShotProgram> Programs(
        GenericActorRulesContract.ShotProgramDefinition definition)
    {
        int emitted = 0;
        int[] offsets = AimOffsets(definition);
        foreach (int offset in offsets)
        {
            if (offset == 0)
                continue;
            if (++emitted > MaxProgramCandidates)
                yield break;
            yield return new ShotProgram(offset, 0, 0, 1, 0);
        }
        for (int count = Math.Max(1, definition.MinBendCount);
             count <= definition.MaxBendCount;
             count++)
        {
            for (int after = Math.Max(1, definition.MinBendAfterTiles);
                 after <= definition.MaxBendAfterTiles;
                 after++)
            {
                for (int every = Math.Max(1, definition.MinBendEveryTiles);
                     every <= definition.MaxBendEveryTiles;
                     every++)
                {
                    foreach (int offset in offsets)
                    {
                        foreach (int bend in definition.AllowedCurvedBendDirections)
                        {
                            if (bend is not (-1 or 1))
                                continue;
                            if (++emitted > MaxProgramCandidates)
                                yield break;
                            yield return new ShotProgram(
                                offset,
                                bend,
                                after,
                                every,
                                count);
                        }
                    }
                }
            }
        }
    }

    private static int[] AimOffsets(
        GenericActorRulesContract.ShotProgramDefinition definition)
    {
        var offsets = new List<int>();
        for (int offset = definition.MinInitialAimSteps;
             offset <= definition.MaxInitialAimSteps;
             offset++)
        {
            offsets.Add(offset);
        }
        if (offsets.Count == 0)
            offsets.Add(0);
        offsets.Sort((left, right) =>
        {
            int byMagnitude = Math.Abs(left).CompareTo(Math.Abs(right));
            return byMagnitude != 0 ? byMagnitude : left.CompareTo(right);
        });
        return [.. offsets];
    }

    private static GenericActorDecision? Build(
        GenericActorActionLegality action,
        GenericActorRulesContract.AttackProfile profile,
        ProjectileHeading? heading,
        ShotProgram program,
        string reason)
    {
        if (heading is ProjectileHeading absolute)
        {
            return new GenericActorDecision(
                action.ActionId,
                action.ActionCode,
                [
                    new GenericActorActionArgument.ProjectileHeadingArgument(
                        absolute),
                ],
                reason);
        }

        var programs = Constraint<GenericActorActionLegality
            .ArgumentConstraint.ShotProgramConstraint>(action);
        bool straight = program.BendCount == 0 && program.InitialAimOffset == 0;
        if (programs is not { Allowed: true })
            return straight ? Payloadless(action, reason) : null;
        if (straight && profile.ShotProgram.PayloadOptional)
            return Payloadless(action, reason);
        if (!profile.ShotProgram.Enabled)
            return straight ? Payloadless(action, reason) : null;

        GenericActorRulesContract.AimOnlyShotProgramValue aimOnly =
            profile.ShotProgram.AimOnlyProgram;
        ShotProgram payload = program.BendCount == 0
            ? new ShotProgram(
                program.InitialAimOffset,
                aimOnly.BendDirection,
                aimOnly.BendAfterTiles,
                aimOnly.BendEveryTiles,
                aimOnly.BendCount)
            : program;
        return new GenericActorDecision(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.ShotProgramArgument(payload)],
            reason);
    }

    private static GenericActorDecision Payloadless(
        GenericActorActionLegality action,
        string reason) =>
        GenericActorDecision.WithoutArguments(
            action.ActionId,
            action.ActionCode,
            reason);

    /// <summary>
    /// Whether a bolt arriving at <paramref name="target"/> along
    /// <paramref name="arrival"/> is worth firing. A guard turns the bolt back
    /// down the reverse of its own heading, so a lane into the arc is a shot at
    /// ourselves; the exception is the bolt that shatters the shield. Inert
    /// whenever no visible form declares a guard, which is every contract
    /// without the kit.
    /// </summary>
    private static bool Worth(
        Stances? stances,
        Position target,
        ProjectileHeading arrival) =>
        stances is null || stances.LaneWorthTaking(target, arrival);

    private static bool Reaches(
        MatchLens lens,
        Position origin,
        ProjectileHeading start,
        int bendDirection,
        int bendAfter,
        int bendEvery,
        int bendCount,
        int maxTiles,
        bool strictCorners,
        Position target,
        IReadOnlySet<Position> enemies,
        IReadOnlySet<Position> allies,
        bool alliesBlock,
        out ProjectileHeading arrival)
    {
        arrival = start;
        foreach ((Position tile, ProjectileHeading heading) in Traverse(
                     lens,
                     origin,
                     start,
                     bendDirection,
                     bendAfter,
                     bendEvery,
                     bendCount,
                     maxTiles,
                     strictCorners))
        {
            if (tile == target)
            {
                arrival = heading;
                return true;
            }
            if (alliesBlock && allies.Contains(tile))
                return false;
            if (enemies.Contains(tile))
                return false;
        }
        return false;
    }

    private static List<Position> Path(
        MatchLens lens,
        Position origin,
        ProjectileHeading start,
        int bendDirection,
        int bendAfter,
        int bendEvery,
        int bendCount,
        int maxTiles,
        bool strictCorners)
    {
        var path = new List<Position>();
        foreach ((Position tile, ProjectileHeading _) in Traverse(
                     lens,
                     origin,
                     start,
                     bendDirection,
                     bendAfter,
                     bendEvery,
                     bendCount,
                     maxTiles,
                     strictCorners))
        {
            path.Add(tile);
        }
        return path;
    }

    /// <summary>
    /// The declared projectile geometry, tile by tile, with the heading the bolt
    /// is travelling as it enters each tile — which is what a guard's arc is
    /// tested against, and which a bend makes different from the launch heading.
    /// </summary>
    private static List<(Position Tile, ProjectileHeading Heading)> Traverse(
        MatchLens lens,
        Position origin,
        ProjectileHeading start,
        int bendDirection,
        int bendAfter,
        int bendEvery,
        int bendCount,
        int maxTiles,
        bool strictCorners)
    {
        var path = new List<(Position, ProjectileHeading)>();
        Position position = origin;
        ProjectileHeading heading = start;
        int bends = 0;
        int interval = Math.Max(1, bendEvery);
        for (int moved = 0; moved < Math.Max(0, maxTiles); moved++)
        {
            if (bends < bendCount
                && moved >= bendAfter
                && (moved - bendAfter) % interval == 0)
            {
                heading = heading.Turned(bendDirection);
                bends++;
            }

            (int dx, int dy) = heading.Vector();
            Position next = position.Offset(dx, dy);
            if (lens.IsWall(next))
                break;
            if (strictCorners
                && dx != 0
                && dy != 0
                && (lens.IsWall(position.Offset(dx, 0))
                    || lens.IsWall(position.Offset(0, dy))))
            {
                break;
            }
            position = next;
            path.Add((position, heading));
        }
        return path;
    }

    private static List<GenericActorActionLegality> AttackActions(
        MatchLens lens,
        GenericActorContext context)
    {
        HashSet<string> ids = ActionIds(
            lens,
            GenericActorRulesContract.ActionKind.Attack);
        return context.ActionLegalities
            .Where(action => action.Available && ids.Contains(action.ActionId))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .ToList();
    }

    private static GenericActorActionLegality? Available(
        MatchLens lens,
        GenericActorContext context,
        GenericActorRulesContract.ActionKind kind)
    {
        HashSet<string> ids = ActionIds(lens, kind);
        return context.ActionLegalities
            .Where(action => action.Available && ids.Contains(action.ActionId))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static HashSet<string> ActionIds(
        MatchLens lens,
        GenericActorRulesContract.ActionKind kind) =>
        lens.Contract.Rules.Actions
            .Where(action => action.Kind == kind)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);

    private static T? Constraint<T>(GenericActorActionLegality? action)
        where T : GenericActorActionLegality.ArgumentConstraint =>
        action?.Constraints.OfType<T>().FirstOrDefault();

    private static HashSet<Position> AllyTiles(GenericActorContext context) =>
        context.Allies.Select(ally => ally.Position).ToHashSet();
}
