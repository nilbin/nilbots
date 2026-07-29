using BotArena.Sdk;

/// <summary>
/// Fire control. Every shot is selected from the current legality mask and the
/// declared attack profile, so the same code drives an absolute-heading turret,
/// a facing-locked straight gun, and a programmable one-bend mobile gun.
/// </summary>
internal static class Gunnery
{
    /// <summary>An enemy worth shooting, with a one-step lead estimate.</summary>
    /// <param name="Tile">Current observed tile.</param>
    /// <param name="Drift">Per-tick movement estimated from prior observations.</param>
    /// <param name="Health">Observed health; lower is finished off first.</param>
    /// <param name="Weight">Doctrine weight; higher is engaged first.</param>
    internal readonly record struct Target(
        Position Tile,
        (int Dx, int Dy) Drift,
        int Health,
        int Weight);

    private static readonly List<Position> RayScratch = [];

    /// <summary>
    /// Fires at the highest-weight reachable target, trying its current tile
    /// first and its predicted tile second. Returns <see langword="null"/> when
    /// no declared program reaches anything this tick.
    /// </summary>
    public static GenericActorDecision? TryFire(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.Form? form,
        List<Target> targets)
    {
        GenericActorRulesContract.AttackProfile? attack = lens.Attack(form);
        if (attack is null || targets.Count == 0)
            return null;

        List<GenericActorActionLegality> actions =
            lens.Available(context, GenericActorRulesContract.ActionKind.Attack);
        if (actions.Count == 0)
            return null;

        targets.Sort(static (left, right) =>
        {
            int weight = right.Weight.CompareTo(left.Weight);
            if (weight != 0)
                return weight;
            int health = left.Health.CompareTo(right.Health);
            if (health != 0)
                return health;
            int x = left.Tile.X.CompareTo(right.Tile.X);
            return x != 0 ? x : left.Tile.Y.CompareTo(right.Tile.Y);
        });

        foreach (Target target in targets)
        {
            int flight = FlightTicks(
                attack,
                context.Self.Position.ChebyshevDistance(target.Tile));
            Position lead = target.Tile.Offset(
                target.Drift.Dx * flight,
                target.Drift.Dy * flight);
            foreach (GenericActorActionLegality action in actions)
            {
                GenericActorDecision? shot =
                    Solve(lens, context, attack, action, target.Tile, "target")
                    ?? (lead == target.Tile
                        ? null
                        : Solve(lens, context, attack, action, lead, "lead"));
                if (shot is not null)
                    return shot;
            }
        }
        return null;
    }

    /// <summary>
    /// Denies a tile nobody is currently standing on. Suppression is free under
    /// the declared cadence (no energy cost, allied bodies are not hit by allied
    /// projectiles), so an idle covering gun keeps firing down the lane that
    /// crosses the most contested tiles rather than conceding them.
    /// </summary>
    public static GenericActorDecision? TrySuppress(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.Form? form,
        Position[] objectiveTiles)
    {
        GenericActorRulesContract.AttackProfile? attack = lens.Attack(form);
        if (attack is null || objectiveTiles.Length == 0)
            return null;

        List<GenericActorActionLegality> actions =
            lens.Available(context, GenericActorRulesContract.ActionKind.Attack);
        if (actions.Count == 0)
            return null;

        var goals = new HashSet<Position>(objectiveTiles);
        GenericActorDecision? best = null;
        int bestCovered = 0;
        foreach (GenericActorActionLegality action in actions)
        {
            GenericActorActionLegality.ArgumentConstraint
                .ProjectileHeadingConstraint? headings = HeadingConstraint(action);
            if (headings is null)
                continue;
            foreach (ProjectileHeading heading in headings.AllowedValues)
            {
                ArenaGeometry.WalkRay(
                    lens.Map,
                    context.Self.Position,
                    heading,
                    attack.Projectile.MaxTravelTiles,
                    attack.Projectile.DiagonalCornersMustBeClear,
                    RayScratch);
                int covered = 0;
                foreach (Position tile in RayScratch)
                {
                    if (goals.Contains(tile))
                        covered++;
                }
                if (covered > bestCovered)
                {
                    bestCovered = covered;
                    best = new GenericActorDecision(
                        action.ActionId,
                        action.ActionCode,
                        [
                            new GenericActorActionArgument
                                .ProjectileHeadingArgument(heading),
                        ],
                        "suppressing the objective lane");
                }
            }
        }
        return best;
    }

    /// <summary>
    /// For a facing-locked gun, the cardinal facing that would put a target on
    /// the muzzle next tick. Returns <see langword="null"/> when the current
    /// facing already works or nothing is lined up.
    /// </summary>
    public static Direction? AlignmentTurn(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.Form? form,
        List<Target> targets)
    {
        GenericActorRulesContract.AttackProfile? attack = lens.Attack(form);
        if (attack is null || attack.OmnidirectionalAim || targets.Count == 0)
            return null;

        Direction? best = null;
        int bestScore = int.MinValue;
        foreach (Target target in targets)
        {
            if (!ArenaGeometry.TryRay(
                    context.Self.Position,
                    target.Tile,
                    out ProjectileHeading heading,
                    out int distance))
            {
                continue;
            }
            if (distance > attack.Projectile.MaxTravelTiles
                || !ArenaGeometry.ClearRay(
                    lens.Map,
                    context.Self.Position,
                    target.Tile,
                    attack.Projectile.DiagonalCornersMustBeClear))
            {
                continue;
            }
            foreach (Direction direction in ArenaGeometry.Cardinals)
            {
                if (direction == context.Self.Facing
                    || direction.ToProjectileHeading() != heading)
                {
                    continue;
                }
                int score = target.Weight * 100 - distance;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = direction;
                }
            }
        }
        return best;
    }

    private static int FlightTicks(
        GenericActorRulesContract.AttackProfile attack,
        int distance)
    {
        int perAdvance = Math.Max(1, attack.Projectile.TilesPerAdvance);
        int launch = Math.Max(0, attack.Projectile.LaunchTiles);
        int remaining = Math.Max(0, distance - launch);
        int advances = (remaining + perAdvance - 1) / perAdvance;
        return Math.Clamp(
            advances * Math.Max(1, attack.Projectile.TicksPerAdvance),
            0,
            4);
    }

    private static GenericActorDecision? Solve(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.AttackProfile attack,
        GenericActorActionLegality action,
        Position aimAt,
        string label)
    {
        if (!ArenaGeometry.IsOpen(lens.Map, aimAt))
            return null;

        int range = attack.Projectile.MaxTravelTiles;
        bool strict = attack.Projectile.DiagonalCornersMustBeClear;
        Position origin = context.Self.Position;
        bool straight = ArenaGeometry.TryRay(
                origin,
                aimAt,
                out ProjectileHeading heading,
                out int distance)
            && distance <= range
            && ArenaGeometry.ClearRay(lens.Map, origin, aimAt, strict);

        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = HeadingConstraint(action);
        if (headings is not null)
        {
            if (!straight || !headings.AllowedValues.Contains(heading))
                return null;
            return new GenericActorDecision(
                action.ActionId,
                action.ActionCode,
                [
                    new GenericActorActionArgument.ProjectileHeadingArgument(
                        heading),
                ],
                $"turret fire on {label} {aimAt}");
        }

        GenericActorActionLegality.ArgumentConstraint.ShotProgramConstraint?
            programs = ProgramConstraint(action);
        if (programs is null)
        {
            // No payload of any kind: the declared aim interpretation is the
            // body's own facing.
            return straight && context.Self.Facing.ToProjectileHeading() == heading
                ? GenericActorDecision.WithoutArguments(
                    action.ActionId,
                    action.ActionCode,
                    $"straight fire on {label} {aimAt}")
                : null;
        }

        GenericActorRulesContract.ShotProgramDefinition definition =
            attack.ShotProgram;
        if (straight)
        {
            int offset = SignedOctants(
                context.Self.Facing.ToProjectileHeading(),
                heading);
            if (offset == 0 && definition.PayloadOptional)
            {
                return GenericActorDecision.WithoutArguments(
                    action.ActionId,
                    action.ActionCode,
                    $"straight fire on {label} {aimAt}");
            }
            if (programs.Allowed
                && definition.Enabled
                && offset >= definition.MinInitialAimSteps
                && offset <= definition.MaxInitialAimSteps)
            {
                GenericActorRulesContract.AimOnlyShotProgramValue aimOnly =
                    definition.AimOnlyProgram;
                return new GenericActorDecision(
                    action.ActionId,
                    action.ActionCode,
                    [
                        new GenericActorActionArgument.ShotProgramArgument(
                            new ShotProgram(
                                offset,
                                aimOnly.BendDirection,
                                aimOnly.BendAfterTiles,
                                aimOnly.BendEveryTiles,
                                aimOnly.BendCount)),
                    ],
                    $"aimed fire on {label} {aimAt}");
            }
        }

        if (!programs.Allowed
            || !definition.Enabled
            || definition.MaxBendCount < 1)
        {
            return null;
        }
        return TryCurve(lens, context, attack, action, aimAt, label);
    }

    /// <summary>
    /// Searches the declared curvature envelope for a program that lands on a
    /// target no straight ray can reach, replaying the engine's own path rule
    /// (including wall termination and strict diagonal corners) locally.
    /// </summary>
    private static GenericActorDecision? TryCurve(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.AttackProfile attack,
        GenericActorActionLegality action,
        Position aimAt,
        string label)
    {
        GenericActorRulesContract.ShotProgramDefinition definition =
            attack.ShotProgram;
        GenericActorMapContract map = lens.Map;
        bool IsWall(Position tile) => !ArenaGeometry.IsOpen(map, tile);

        ShotProgram? best = null;
        int bestHit = int.MaxValue;
        for (int aim = definition.MinInitialAimSteps;
             aim <= definition.MaxInitialAimSteps;
             aim++)
        {
            foreach (int bendDirection in definition.AllowedCurvedBendDirections)
            {
                if (bendDirection == 0)
                    continue;
                for (int after = definition.MinBendAfterTiles;
                     after <= definition.MaxBendAfterTiles;
                     after++)
                {
                    for (int every = Math.Max(1, definition.MinBendEveryTiles);
                         every <= definition.MaxBendEveryTiles;
                         every++)
                    {
                        for (int count = Math.Max(1, definition.MinBendCount);
                             count <= definition.MaxBendCount;
                             count++)
                        {
                            var program = new ShotProgram(
                                aim,
                                bendDirection,
                                after,
                                every,
                                count);
                            IReadOnlyList<Position> path = ShotPaths.Preview(
                                context.Self.Position,
                                context.Self.Facing,
                                program,
                                attack.Projectile.MaxTravelTiles,
                                IsWall);
                            for (int index = 0; index < path.Count; index++)
                            {
                                if (path[index] != aimAt)
                                    continue;
                                if (index < bestHit)
                                {
                                    bestHit = index;
                                    best = program;
                                }
                                break;
                            }
                        }
                    }
                }
            }
        }

        return best is ShotProgram chosen
            ? new GenericActorDecision(
                action.ActionId,
                action.ActionCode,
                [new GenericActorActionArgument.ShotProgramArgument(chosen)],
                $"curved intercept on {label} {aimAt}")
            : null;
    }

    private static GenericActorActionLegality.ArgumentConstraint
        .ProjectileHeadingConstraint? HeadingConstraint(
            GenericActorActionLegality action)
    {
        foreach (GenericActorActionLegality.ArgumentConstraint constraint
                 in action.Constraints)
        {
            if (constraint is GenericActorActionLegality.ArgumentConstraint
                .ProjectileHeadingConstraint headings)
            {
                return headings;
            }
        }
        return null;
    }

    private static GenericActorActionLegality.ArgumentConstraint
        .ShotProgramConstraint? ProgramConstraint(
            GenericActorActionLegality action)
    {
        foreach (GenericActorActionLegality.ArgumentConstraint constraint
                 in action.Constraints)
        {
            if (constraint is GenericActorActionLegality.ArgumentConstraint
                .ShotProgramConstraint programs)
            {
                return programs;
            }
        }
        return null;
    }

    private static int SignedOctants(ProjectileHeading from, ProjectileHeading to)
    {
        int difference = ((int)to - (int)from + 8) % 8;
        return difference > 4 ? difference - 8 : difference;
    }
}
