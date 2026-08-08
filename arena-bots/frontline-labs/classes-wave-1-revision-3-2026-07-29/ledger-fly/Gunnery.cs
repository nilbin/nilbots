using BotArena.Sdk;

/// <summary>
/// Contract-driven fire control. Every shot is simulated locally against the
/// declared projectile geometry before it is submitted, so LedgerFly never
/// spends a cooldown on a bolt that a wall would eat. It resolves whichever
/// attack language the current form actually has: an absolute eight-way
/// heading, a facing-relative shot program with legal bends, or a payload-free
/// straight bolt along the body's facing.
/// </summary>
internal static class Gunnery
{
    private const int MaxProgramCandidates = 512;

    /// <summary>Attempts a shot that provably reaches one of the targets.</summary>
    public static GenericActorDecision? TryFire(
        MatchLens lens,
        GenericActorContext context,
        IReadOnlyList<GenericActorContext.ObservedEnemyState> targets,
        bool allowCurved)
    {
        foreach (GenericActorContext.ObservedEnemyState target in targets)
        {
            GenericActorDecision? shot = Solve(
                lens,
                context,
                context.Self.Facing,
                target.Position,
                allowCurved,
                $"trading fire with {target.ActorId}");
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
        Direction[] order)
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
                        null) is null)
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
            var headings = Constraint<GenericActorActionLegality
                .ArgumentConstraint.ProjectileHeadingConstraint>(action);
            IEnumerable<ProjectileHeading> candidates = headings is null
                ? [context.Self.Facing.ToProjectileHeading()]
                : headings.AllowedValues;
            foreach (ProjectileHeading heading in candidates)
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
                    headings is null ? null : heading,
                    new ShotProgram(0, 0, 0, 1, 0),
                    "suppressing the contested lane");
                if (decision is not null)
                    return decision;
            }
        }
        return null;
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
        foreach (Direction facing in order)
        {
            if (facing == context.Self.Facing
                || !directions.AllowedValues.Contains(facing))
            {
                continue;
            }
            List<Position> path = Path(
                lens,
                context.Self.Position,
                facing.ToProjectileHeading(),
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
        string? reason)
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
                            alliesBlock))
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
                    alliesBlock))
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
            if (!allowCurved
                || programs is not { Allowed: true }
                || !profile.ShotProgram.Enabled)
            {
                continue;
            }

            foreach (ShotProgram program in CurvedPrograms(profile.ShotProgram))
            {
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
                        alliesBlock))
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

    private static IEnumerable<ShotProgram> CurvedPrograms(
        GenericActorRulesContract.ShotProgramDefinition definition)
    {
        int emitted = 0;
        int[] offsets = AimOffsets(definition);
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
        bool alliesBlock)
    {
        List<Position> path = Path(
            lens,
            origin,
            start,
            bendDirection,
            bendAfter,
            bendEvery,
            bendCount,
            maxTiles,
            strictCorners);
        foreach (Position tile in path)
        {
            if (tile == target)
                return true;
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
            path.Add(position);
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
