using BotArena.Sdk;

/// <summary>
/// Chooses which of the contract's legal shot programs to commit to.
///
/// Every legal program is traced against the declared projectile rules and
/// scored against a dodge model of each visible enemy: a target that can only
/// step along a corridor is worth suppressing with a plain straight bolt, while
/// a target standing in an open chamber has lateral escapes that only a bent
/// path can cover. Bends must beat the straight answer by a margin before they
/// are taken, which is what keeps them a weapon rather than a habit.
/// </summary>
internal sealed class ShotSolver
{
    /// <summary>Path tiles arriving later than this are treated as noise.</summary>
    private const int MaxArrivalOffset = 3;
    /// <summary>Beyond this delay a target has too long to leave its tile.</summary>
    private const int InterceptHorizon = 2;
    /// <summary>A bend must beat the straight option by this ratio.</summary>
    private const double BendMargin = 1.15;
    /// <summary>Open orthogonal neighbours at or below which a tile is a corridor.</summary>
    private const int CorridorOpenness = 2;
    /// <summary>Upper bound on enumerated curved programs per attack action.</summary>
    private const int MaxCurvedPrograms = 384;

    private readonly Doctrine _doctrine;
    private readonly Field _field;
    private readonly DodgeLedger _dodges;
    private readonly GenericActorContext.ObservedEnemyState[] _enemies;
    private readonly Dictionary<(ActorIdentity Actor, Position From, int Moves),
        Dictionary<Position, double>> _reach = [];

    public ShotSolver(Doctrine doctrine, Field field, DodgeLedger dodges)
    {
        _doctrine = doctrine;
        _field = field;
        _dodges = dodges;
        _enemies = field.Context.Enemies
            .OrderBy(enemy => enemy.ActorId)
            .ToArray();
    }

    /// <summary>True when no visible enemy can be solved for.</summary>
    public bool HasTargets => _enemies.Length > 0;

    /// <summary>
    /// True when an attack action is legal this tick — the difference between
    /// a tick that costs a whole shot and one that costs nothing.
    /// </summary>
    public bool Ready => _field.Context.ActionLegalities.Any(action =>
        action.Available && IsAttack(action.ActionId));

    /// <summary>
    /// Best committed shot from a hypothetical facing.
    /// <paramref name="extraEnemyMoves"/> models the ticks a target gains
    /// before the shot is actually fired, so a rotate-then-fire plan is scored
    /// on the ground it will really cover.
    /// </summary>
    public ShotPlan? Best(
        Direction facing,
        int extraEnemyMoves,
        bool allowCurved = true) =>
        Solve(facing, extraEnemyMoves, allowCurved, ready: true, _field.Self);

    /// <summary>
    /// What the best shot from a facing would be worth if the gun were ready.
    ///
    /// This is the price of aim, and it has to be answerable on a tick that
    /// cannot fire: turning is free while the weapon is on cooldown and costs
    /// a whole shot while it is not, so the two ticks must be told apart. The
    /// declared attack envelope comes from the form's own action mask rather
    /// than from this tick's availability; only the decision that actually
    /// fires is gated on availability.
    /// </summary>
    public double Forecast(
        Direction facing,
        int extraEnemyMoves,
        bool allowCurved = true,
        Position? from = null) =>
        Solve(
            facing,
            extraEnemyMoves,
            allowCurved,
            ready: false,
            from ?? _field.Self)
            ?.Score ?? 0.0;

    private ShotPlan? Solve(
        Direction facing,
        int extraEnemyMoves,
        bool allowCurved,
        bool ready,
        Position origin)
    {
        GenericActorRulesContract.AttackProfile? attack =
            _doctrine.AttackFor(_field.FormId);
        if (attack is null || _enemies.Length == 0)
            return null;

        GenericActorActionLegality[] actions = _field.Context.ActionLegalities
            .Where(action =>
                (ready ? action.Available : action.AllowedByForm)
                && IsAttack(action.ActionId))
            .OrderBy(action => action.ActionCode)
            .ToArray();
        if (actions.Length == 0)
            return null;

        ShotPlan? straight = null;
        ShotPlan? curved = null;
        foreach (GenericActorActionLegality action in actions)
        {
            GenericActorActionLegality.ArgumentConstraint
                .ProjectileHeadingConstraint? headings = action.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .FirstOrDefault();
            if (headings is not null)
            {
                foreach (ProjectileHeading heading in headings.AllowedValues)
                {
                    double score = ScorePath(
                        origin,
                        Trace(origin, attack, heading, 0, 0, 1, 0),
                        attack.Projectile,
                        extraEnemyMoves);
                    Keep(
                        ref straight,
                        new ShotPlan(
                            score,
                            Curved: false,
                            new GenericActorDecision(
                                action.ActionId,
                                action.ActionCode,
                                [
                                    new GenericActorActionArgument
                                        .ProjectileHeadingArgument(heading),
                                ],
                                $"heading fire {heading} ev={score:0.00}")));
                }
                continue;
            }

            GenericActorRulesContract.ShotProgramDefinition shots =
                attack.ShotProgram;
            ProjectileHeading forward = facing.ToProjectileHeading();
            bool payloadAllowed = shots.Enabled
                && action.Constraints
                    .OfType<GenericActorActionLegality.ArgumentConstraint
                        .ShotProgramConstraint>()
                    .FirstOrDefault() is { Allowed: true };

            if (shots.PayloadOptional)
            {
                GenericActorRulesContract.ShotProgramValue fallback =
                    shots.DefaultProgram;
                double score = ScorePath(
                    origin,
                    Trace(
                        origin,
                        attack,
                        forward.Turned(fallback.InitialAimOffset),
                        fallback.BendDirection,
                        fallback.BendAfterTiles,
                        fallback.BendEveryTiles,
                        fallback.BendCount),
                    attack.Projectile,
                    extraEnemyMoves);
                Keep(
                    ref straight,
                    new ShotPlan(
                        score,
                        Curved: false,
                        GenericActorDecision.WithoutArguments(
                            action.ActionId,
                            action.ActionCode,
                            $"straight suppression ev={score:0.00}")));
            }

            if (!payloadAllowed)
                continue;

            for (int aim = shots.MinInitialAimSteps;
                 aim <= shots.MaxInitialAimSteps;
                 aim++)
            {
                if (aim == 0 && shots.PayloadOptional)
                    continue;
                var program = new ShotProgram(
                    aim,
                    shots.AimOnlyProgram.BendDirection,
                    shots.AimOnlyProgram.BendAfterTiles,
                    shots.AimOnlyProgram.BendEveryTiles,
                    shots.AimOnlyProgram.BendCount);
                double score = ScorePath(
                    origin,
                    Trace(
                        origin,
                        attack,
                        forward.Turned(aim),
                        program.BendDirection,
                        program.BendAfterTiles,
                        program.BendEveryTiles,
                        program.BendCount),
                    attack.Projectile,
                    extraEnemyMoves);
                Keep(
                    ref straight,
                    new ShotPlan(
                        score,
                        Curved: false,
                        Fire(action, program, $"offset fire ev={score:0.00}")));
            }

            int enumerated = 0;
            if (!allowCurved)
                continue;
            for (int aim = shots.MinInitialAimSteps;
                 aim <= shots.MaxInitialAimSteps && enumerated < MaxCurvedPrograms;
                 aim++)
            {
                foreach (int bend in shots.AllowedCurvedBendDirections)
                {
                    for (int count = Math.Max(1, shots.MinBendCount);
                         count <= shots.MaxBendCount;
                         count++)
                    {
                        for (int after = Math.Max(1, shots.MinBendAfterTiles);
                             after <= shots.MaxBendAfterTiles;
                             after++)
                        {
                            int firstInterval = Math.Max(1, shots.MinBendEveryTiles);
                            int lastInterval = count <= 1
                                ? firstInterval
                                : shots.MaxBendEveryTiles;
                            for (int every = firstInterval;
                                 every <= lastInterval;
                                 every++)
                            {
                                if (enumerated++ >= MaxCurvedPrograms)
                                    break;
                                var program = new ShotProgram(
                                    aim,
                                    bend,
                                    after,
                                    every,
                                    count);
                                double score = ScorePath(
                                    origin,
                                    Trace(
                                        origin,
                                        attack,
                                        forward.Turned(aim),
                                        bend,
                                        after,
                                        every,
                                        count),
                                    attack.Projectile,
                                    extraEnemyMoves);
                                Keep(
                                    ref curved,
                                    new ShotPlan(
                                        score,
                                        Curved: true,
                                        Fire(
                                            action,
                                            program,
                                            $"bend {bend:+#;-#;0}@{after}"
                                            + $"x{count} ev={score:0.00}")));
                            }
                        }
                    }
                }
            }
        }

        bool corridorTarget = PrimaryTarget() is
            GenericActorContext.ObservedEnemyState primary
            && _doctrine.Openness(primary.Position) <= CorridorOpenness;
        double straightScore = straight?.Score ?? 0.0;
        // A bend is the only answer when no straight line exists at all; where
        // one does, the bend has to earn the commitment, and in a corridor the
        // target has nowhere to step aside to, so it never does.
        if (curved is not null
            && (straight is null
                || !corridorTarget
                && curved.Score > straightScore * BendMargin + 0.02))
        {
            return curved;
        }
        return straight is { Score: > 0.0 } ? straight : null;
    }

    private static void Keep(ref ShotPlan? best, ShotPlan candidate)
    {
        if (candidate.Score <= 0.0)
            return;
        if (best is null || candidate.Score > best.Score)
            best = candidate;
    }

    private static GenericActorDecision Fire(
        GenericActorActionLegality action,
        ShotProgram program,
        string note) =>
        new(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.ShotProgramArgument(program)],
            note);

    private bool IsAttack(string actionId) =>
        _doctrine.Contract.Rules.Actions.Any(action =>
            action.Kind == GenericActorRulesContract.ActionKind.Attack
            && string.Equals(action.Id, actionId, StringComparison.Ordinal));

    private List<Position> Trace(
        Position origin,
        GenericActorRulesContract.AttackProfile attack,
        ProjectileHeading heading,
        int bendDirection,
        int bendAfterTiles,
        int bendEveryTiles,
        int bendCount) =>
        Ballistics.Trace(
            _doctrine,
            origin,
            heading,
            bendDirection,
            bendAfterTiles,
            bendEveryTiles,
            bendCount,
            attack.Projectile.MaxTravelTiles,
            attack.Projectile.DiagonalCornersMustBeClear);

    private GenericActorContext.ObservedEnemyState? PrimaryTarget()
    {
        GenericActorContext.ObservedEnemyState? best = null;
        double bestScore = double.NegativeInfinity;
        foreach (GenericActorContext.ObservedEnemyState enemy in _enemies)
        {
            double score = Priority(enemy)
                - 0.05 * _field.Self.ChebyshevDistance(enemy.Position);
            if (score > bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }
        return best;
    }

    private double ScorePath(
        Position origin,
        List<Position> path,
        GenericActorRulesContract.Projectile projectile,
        int extraEnemyMoves)
    {
        if (path.Count == 0)
            return 0.0;
        double total = 0.0;
        foreach (GenericActorContext.ObservedEnemyState enemy in _enemies)
        {
            double covered = 0.0;
            bool intercepted = false;
            for (int index = 0; index < path.Count; index++)
            {
                int offset = Doctrine.ArrivalOffset(projectile, index);
                if (offset > MaxArrivalOffset)
                    break;

                // A path that arrives on the tile the body is standing on is
                // an interception rather than a sweep — but only against a
                // target that cannot see the gun. One that is watching will
                // simply not be there, and pricing that shot as an
                // interception is how a duelist stops walking and starts
                // trading.
                if (!intercepted
                    && offset <= InterceptHorizon
                    && path[index] == enemy.Position
                    && !Watched(enemy, origin))
                {
                    covered += 0.14;
                    intercepted = true;
                }
                covered += Likelihood(
                    enemy,
                    origin,
                    path[index],
                    offset + 1 + extraEnemyMoves);
            }
            if (covered > 0.0)
                total += Math.Min(1.0, covered) * Priority(enemy);
        }
        return total;
    }

    private double Priority(GenericActorContext.ObservedEnemyState enemy)
    {
        GenericActorRulesContract.Form? form = _doctrine.FormFor(enemy.FormId);
        int maxHealth = Math.Max(1, form?.MaxHealth ?? enemy.Health);
        double priority = 1.0
            + 0.7 * (1.0 - Math.Min(1.0, enemy.Health / (double)maxHealth));
        if (_field.IsObjective(enemy.Position))
            priority += 0.9;
        if ((form?.ObjectiveWeight ?? 1) <= 0)
            priority -= 0.4;
        return Math.Max(0.1, priority);
    }

    /// <summary>
    /// True when the target's declared sight envelope contains this shooter,
    /// so the shot is something it could react to at all.
    /// </summary>
    private bool Watched(
        GenericActorContext.ObservedEnemyState enemy,
        Position shooter)
    {
        GenericActorRulesContract.VisionProfile? vision =
            _doctrine.VisionFor(enemy.FormId);
        return vision is null
            || _doctrine.CanSee(
                vision,
                enemy.Position,
                enemy.Facing,
                shooter);
    }

    private double Likelihood(
        GenericActorContext.ObservedEnemyState enemy,
        Position shooter,
        Position tile,
        int moves)
    {
        Dictionary<Position, double> distribution = Reach(
            enemy,
            shooter,
            Math.Clamp(moves, 0, MaxArrivalOffset + 2));
        return distribution.TryGetValue(tile, out double weight)
            ? weight
            : 0.0;
    }

    private Dictionary<Position, double> Reach(
        GenericActorContext.ObservedEnemyState enemy,
        Position shooter,
        int moves)
    {
        (ActorIdentity, Position, int) key = (enemy.ActorId, shooter, moves);
        if (_reach.TryGetValue(key, out Dictionary<Position, double>? cached))
            return cached;

        var weights = new Dictionary<Position, double>();
        var frontier = new List<Position> { enemy.Position };
        var seen = new HashSet<Position> { enemy.Position };
        int originDistance = _field.DistanceToObjective(enemy.Position);

        // A target that cannot see the shooter has no reason to be anywhere
        // other than where it already is. One that can see it is priced on
        // what bodies this life has actually watched do, not on an assumption
        // that survives being wrong for four hundred ticks.
        // Under a coupled movement profile the target's own sidestep is
        // expensive: face-movement turns its gun and its sight quadrant away
        // from whatever it was watching, and facing-locked does not offer the
        // sidestep at all — it is a rotation and then a step. A stickier
        // target is a target a straight bolt can still find, which is why the
        // bend has to earn its commitment all over again in those arms. The
        // arm sets the prior; what the bodies here actually do settles it.
        double priorScale = _field.MoveTurns
            ? 1.8
            : _field.MoveLocked ? 3.0 : 1.0;
        double inertia = Watched(enemy, shooter)
            ? 2.5 * _dodges.WatchedInertiaFactor(priorScale)
            : 7.0 * priorScale;

        for (int step = 0; step <= moves; step++)
        {
            var next = new List<Position>();
            foreach (Position tile in frontier)
            {
                double weight = 1.0;
                if (tile == enemy.Position)
                    weight *= inertia;
                if (_field.IsObjective(tile))
                    weight *= 2.5;
                else if (_field.DistanceToObjective(tile) < originDistance)
                    weight *= 1.4;
                weights[tile] = weight;
                if (step == moves)
                    continue;
                foreach (Direction direction in Field.Cardinals)
                {
                    (int dx, int dy) = direction.Vector();
                    Position neighbour = tile.Offset(dx, dy);
                    if (_doctrine.IsOpen(neighbour) && seen.Add(neighbour))
                        next.Add(neighbour);
                }
            }
            frontier = next;
        }

        double sum = weights.Values.Sum();
        if (sum > 0.0)
        {
            foreach (Position tile in weights.Keys.ToArray())
                weights[tile] /= sum;
        }
        _reach[key] = weights;
        return weights;
    }
}

/// <summary>One evaluated shot: its expected value and the decision that fires it.</summary>
internal sealed record ShotPlan(
    double Score,
    bool Curved,
    GenericActorDecision Decision);
