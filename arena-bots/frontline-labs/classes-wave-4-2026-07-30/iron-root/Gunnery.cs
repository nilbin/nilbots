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
    /// <param name="Guarded">
    /// The target's declared form turns hostile contacts arriving inside its
    /// facing quadrant and launches them back under its own team's ownership.
    /// A shot into that arc is not a miss, it is a shot at yourself.
    /// </param>
    /// <param name="Facing">
    /// The target's facing, which for a guarding form is the arc it chose
    /// before the shield rose and cannot change while it is up.
    /// </param>
    /// <param name="FeedGuard">
    /// The doctrine has decided that breaking this guard is worth eating its
    /// return: fire into the arc deliberately. False means never poke it.
    /// </param>
    internal readonly record struct Target(
        Position Tile,
        (int Dx, int Dy) Drift,
        int Health,
        int Weight,
        bool Guarded = false,
        Direction Facing = Direction.North,
        bool FeedGuard = false);

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

        // TWO PASSES, and the order is the doctrine's answer to "when does a
        // bend beat a straight bolt".
        //
        // Pass one takes only shots whose ARRIVAL HEADING lands outside every
        // declared guard arc. That is the whole reason a curve is worth its
        // search: a straight ray into a raised shield is returned along the
        // exact reverse heading under the shield's ownership — the muzzle that
        // fired it is standing on that ray by construction — while a one-bend
        // program can enter the same tile travelling on a different heading and
        // simply hit. Going around a guard always works, because the arc was
        // chosen before the shield rose and cannot follow.
        //
        // Pass two exists because a guard is also BREAKABLE: its budget is a
        // declared counter, and feeding it is sometimes the plan. It runs only
        // for targets the caller has explicitly marked, so a shot into an arc
        // is always a decision and never an accident.
        for (int pass = 0; pass < 2; pass++)
        {
            bool allowCaught = pass == 1;
            foreach (Target target in targets)
            {
                if (allowCaught && !(target.Guarded && target.FeedGuard))
                    continue;
                int flight = FlightTicks(
                    attack,
                    context.Self.Position.ChebyshevDistance(target.Tile));
                Position lead = target.Tile.Offset(
                    target.Drift.Dx * flight,
                    target.Drift.Dy * flight);
                foreach (GenericActorActionLegality action in actions)
                {
                    GenericActorDecision? shot = Solve(
                            lens,
                            context,
                            attack,
                            action,
                            target,
                            target.Tile,
                            "target",
                            allowCaught)
                        ?? (lead == target.Tile
                            ? null
                            : Solve(
                                lens,
                                context,
                                attack,
                                action,
                                target,
                                lead,
                                "lead",
                                allowCaught));
                    if (shot is not null)
                        return shot;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Would a bolt arriving on <paramref name="arrival"/> be turned by this
    /// target's declared guard and sent back down the ray it came from?
    /// </summary>
    private static bool Caught(Target target, ProjectileHeading arrival) =>
        target.Guarded && ArenaGeometry.GuardCatches(target.Facing, arrival);

    /// <summary>
    /// The heading a programmed bolt is travelling on as it enters
    /// <paramref name="index"/> of its own previewed path — the heading the
    /// guard arc actually sees, which for any bent program is not the heading
    /// the shot left the muzzle on.
    /// </summary>
    private static ProjectileHeading ArrivalHeading(
        Position origin,
        IReadOnlyList<Position> path,
        int index)
    {
        Position from = index == 0 ? origin : path[index - 1];
        return ArenaGeometry.TryRay(
            from,
            path[index],
            out ProjectileHeading heading,
            out _)
            ? heading
            : ProjectileHeading.North;
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

            // Turning to lay the muzzle on a guarded arc spends a tick to set
            // up a shot that will be returned along this exact ray. Only worth
            // a rotation when breaking the guard is the plan.
            if (Caught(target, heading) && !target.FeedGuard)
                continue;
            foreach (Direction direction in ArenaGeometry.Cardinals)
            {
                if (direction == context.Self.Facing)
                    continue;

                // A fan wider than one heading does not need the muzzle exactly
                // on the target: any facing whose fan contains the heading
                // fires down it. The width is the declared projectile count.
                if (!InFan(attack, direction, heading))
                    continue;
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
        Target target,
        Position aimAt,
        string label,
        bool allowCaught)
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
        bool straightCaught = straight && Caught(target, heading);

        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = HeadingConstraint(action);
        if (headings is not null)
        {
            // An absolute-heading gun has one arrival heading per target tile
            // and no curve, so a guarded arc is simply a tile it may not shoot
            // — until the doctrine decides to break the guard.
            if (!straight
                || !headings.AllowedValues.Contains(heading)
                || (straightCaught && !allowCaught))
            {
                return null;
            }
            return new GenericActorDecision(
                action.ActionId,
                action.ActionCode,
                [
                    new GenericActorActionArgument.ProjectileHeadingArgument(
                        heading),
                ],
                straightCaught
                    ? $"feeding the guard on {label} {aimAt}"
                    : $"turret fire on {label} {aimAt}");
        }

        GenericActorActionLegality.ArgumentConstraint.ShotProgramConstraint?
            programs = ProgramConstraint(action);
        if (programs is null)
        {
            // No payload of any kind: the declared aim interpretation is the
            // body's own facing. A volley profile is exactly this case — the
            // fan is straight by construction and refuses programmed shots —
            // so the fan's own lanes are what has to line up.
            if (!straight || (straightCaught && !allowCaught))
                return null;
            return InFan(attack, context.Self.Facing, heading)
                ? GenericActorDecision.WithoutArguments(
                    action.ActionId,
                    action.ActionCode,
                    attack.Volley is null
                        ? $"straight fire on {label} {aimAt}"
                        : $"volley fan on {label} {aimAt}")
                : null;
        }

        GenericActorRulesContract.ShotProgramDefinition definition =
            attack.ShotProgram;
        if (straight && !straightCaught)
        {
            int offset = ArenaGeometry.SignedOctants(
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
            // No curve is available, so a caught straight ray is all there is.
            return straight && straightCaught && allowCaught && definition.PayloadOptional
                && context.Self.Facing.ToProjectileHeading() == heading
                ? GenericActorDecision.WithoutArguments(
                    action.ActionId,
                    action.ActionCode,
                    $"feeding the guard on {label} {aimAt}")
                : null;
        }
        return TryCurve(
            lens,
            context,
            attack,
            action,
            target,
            aimAt,
            label,
            allowCaught);
    }

    /// <summary>
    /// True when a heading is inside this gun's launch fan. An ordinary gun's
    /// fan is exactly its facing; a profile declaring a volley launches
    /// several bolts on adjacent headings, so a target one sector off the
    /// muzzle is still in the fan and needs no rotation. The width comes from
    /// the declared projectile count, never from the number three.
    /// </summary>
    private static bool InFan(
        GenericActorRulesContract.AttackProfile attack,
        Direction facing,
        ProjectileHeading heading)
    {
        int half = (attack.ProjectilesPerAttack - 1) / 2;
        return Math.Abs(
            ArenaGeometry.SignedOctants(facing.ToProjectileHeading(), heading))
            <= half;
    }

    /// <summary>
    /// Searches the declared curvature envelope for a program that lands on a
    /// target, replaying the engine's own path rule (including wall termination
    /// and strict diagonal corners) locally.
    ///
    /// <para>Two things are being searched for, and the second is new. The
    /// first is reach: a curve lands on tiles no straight ray from this tile
    /// can touch, which on a straight-only chassis is half of them. The second
    /// is ARRIVAL ANGLE: against a declared guard the bolt that matters is the
    /// one that enters the target tile travelling on a heading the arc does not
    /// cover, and a bend is the only way a facing-locked muzzle can change the
    /// angle it arrives on without walking there. Programs that arrive inside
    /// the arc are rejected outright unless the doctrine asked to feed it.</para>
    /// </summary>
    private static GenericActorDecision? TryCurve(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.AttackProfile attack,
        GenericActorActionLegality action,
        Target target,
        Position aimAt,
        string label,
        bool allowCaught)
    {
        GenericActorRulesContract.ShotProgramDefinition definition =
            attack.ShotProgram;
        GenericActorMapContract map = lens.Map;
        bool IsWall(Position tile) => !ArenaGeometry.IsOpen(map, tile);

        ShotProgram? best = null;
        int bestHit = int.MaxValue;
        bool bestCaught = true;
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
                                bool caught = Caught(
                                    target,
                                    ArrivalHeading(
                                        context.Self.Position,
                                        path,
                                        index));
                                if (caught && !allowCaught)
                                    break;

                                // An angle the arc does not cover beats a
                                // shorter flight that it does: landing late is
                                // a tempo cost, landing inside the arc is a
                                // bolt handed to the opponent.
                                bool better = best is null
                                    || (bestCaught && !caught)
                                    || (bestCaught == caught && index < bestHit);
                                if (better)
                                {
                                    bestHit = index;
                                    bestCaught = caught;
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
                bestCaught
                    ? $"bent feed into the guard on {label} {aimAt}"
                    : $"bent past the guard arc on {label} {aimAt}")
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
}
