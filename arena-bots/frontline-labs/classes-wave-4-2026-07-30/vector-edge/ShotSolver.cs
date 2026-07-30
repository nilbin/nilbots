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

    /// <summary>
    /// Tempo charged for putting a bolt into a raised guard's face. The bolt
    /// dies on the arc and a bolt of the other team's launches from that tile
    /// along the exactly reversed heading, so the shot buys no damage and hands
    /// back a projectile that has to be dodged. It is a bill, not a hit — hence
    /// a tax rather than a health cost.
    /// </summary>
    private const double DeflectTax = 0.55;

    /// <summary>Extra tax when this life cannot step out of the return.</summary>
    private const double PinnedDeflectTax = 0.45;

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
        int extraEnemyMoves) =>
        ScorePaths(origin, [path], projectile, extraEnemyMoves);

    /// <summary>
    /// Expected value of one attack's whole launch, which may be several bolts.
    /// A body can only be hit once by damage this attack deals, so coverage is
    /// capped per enemy across every ray rather than per ray — three bolts that
    /// all sweep the same tile are worth one bolt, and three bolts that sweep
    /// three tiles a target might occupy are worth what the fan is actually
    /// for.
    /// </summary>
    private double ScorePaths(
        Position origin,
        List<List<Position>> paths,
        GenericActorRulesContract.Projectile projectile,
        int extraEnemyMoves)
    {
        double total = 0.0;
        double tax = 0.0;
        var taxed = new HashSet<ActorIdentity>();
        foreach (GenericActorContext.ObservedEnemyState enemy in _enemies)
        {
            double covered = 0.0;
            bool intercepted = false;
            foreach (List<Position> path in paths)
            {
                if (path.Count == 0)
                    continue;
                (int limit, double penalty, ActorIdentity? guard) =
                    Deflection(origin, path);
                if (guard is not null && taxed.Add(guard))
                    tax += penalty;
                // The bolt died on the arc, so nothing past the shield is
                // covered — and the shield itself took nothing.
                if (guard is not null && guard.Equals(enemy.ActorId))
                    continue;
                for (int index = 0; index < limit; index++)
                {
                    int offset = Doctrine.ArrivalOffset(projectile, index);
                    if (offset > MaxArrivalOffset)
                        break;

                    // A path that arrives on the tile the body is standing on
                    // is an interception rather than a sweep — but only against
                    // a target that cannot react. One that is watching will
                    // simply not be there, and pricing that shot as an
                    // interception is how a duelist stops walking and starts
                    // trading. A body locked in a windup cannot react at all,
                    // whether it is watching or not.
                    if (!intercepted
                        && offset <= InterceptHorizon
                        && path[index] == enemy.Position
                        && (!Watched(enemy, origin) || Frozen(enemy, offset)))
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
            }
            if (covered > 0.0)
                total += Math.Min(1.0, covered) * Priority(enemy);
        }
        return Math.Max(0.0, total - tax);
    }

    /// <summary>
    /// Where along a traced path a raised guard eats the bolt, and what that
    /// costs. A guard deflects only contacts arriving inside its facing
    /// quadrant and it cannot rotate while raised, so the test is pure
    /// geometry between the shield's fixed arc and the tile the bolt arrives
    /// from — which is exactly why a bend that curls around one lands normally
    /// and a straight bolt into its face never does.
    /// </summary>
    private (int Limit, double Penalty, ActorIdentity? Guard) Deflection(
        Position origin,
        List<Position> path)
    {
        if (!_doctrine.Skills.AnyGuard)
            return (path.Count, 0.0, null);
        for (int index = 0; index < path.Count; index++)
        {
            foreach (GenericActorContext.ObservedEnemyState enemy in _enemies)
            {
                if (enemy.Position != path[index]
                    || !_doctrine.Skills.Guards(enemy.FormId))
                {
                    continue;
                }
                Position approach = index == 0 ? origin : path[index - 1];
                if (!Doctrine.InQuadrant(
                        enemy.Position,
                        enemy.Facing,
                        approach))
                {
                    // Flank or rear: the shield is irrelevant on this bearing.
                    continue;
                }
                double penalty = DeflectTax
                    + (_field.InStance ? PinnedDeflectTax : 0.0);
                return (index, penalty, enemy.ActorId);
            }
        }
        return (path.Count, 0.0, null);
    }

    /// <summary>
    /// True when a windup still holds this body still at the tick a bolt would
    /// arrive. The due tick is published on the observed enemy, so this is a
    /// read rather than a model.
    /// </summary>
    private bool Frozen(
        GenericActorContext.ObservedEnemyState enemy,
        int offset) =>
        _field.FrozenUntil(enemy.ActorId) is int due
        && _field.Context.Tick + offset <= due;

    /// <summary>
    /// What a fan launched from <paramref name="origin"/> along
    /// <paramref name="facing"/> would be worth, using the stance gun's own
    /// projectile and its own declared spread. Its rays diverge, so it covers
    /// bearings a single cardinal bolt cannot reach at all — including the
    /// diagonals a striker's gun can never open on, because its shot program
    /// declares no initial aim offset and its earliest bend is a tile out.
    /// </summary>
    public double Fan(
        string stanceFormId,
        Direction facing,
        int extraEnemyMoves,
        Position origin)
    {
        GenericActorRulesContract.AttackProfile? attack =
            _doctrine.AttackFor(stanceFormId);
        if (attack is null || _enemies.Length == 0)
            return 0.0;
        ProjectileHeading forward = facing.ToProjectileHeading();
        var paths = new List<List<Position>>();
        foreach (int lane in _doctrine.Skills.FanFor(stanceFormId))
        {
            paths.Add(Ballistics.Trace(
                _doctrine,
                origin,
                forward.Turned(lane),
                bendDirection: 0,
                bendAfterTiles: 0,
                bendEveryTiles: 1,
                bendCount: 0,
                attack.Projectile.MaxTravelTiles,
                attack.Projectile.DiagonalCornersMustBeClear));
        }
        return ScorePaths(origin, paths, attack.Projectile, extraEnemyMoves);
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
    /// Every tile this body could stand on within <paramref name="moves"/>
    /// ticks, under the movement coupling its own form declares.
    ///
    /// <para>This is the correction the phase-2 arm demands. Under a
    /// preserve-facing or face-movement profile any cardinal is a legal step, so
    /// the reachable set is an ordinary ball and a bolt has to cover a whole
    /// ring of sidesteps. Under <c>facing-locked</c> it is nothing like a ball:
    /// the movement mask offers the current facing and nothing else, so a body
    /// can only run down the line it is already on — and a sidestep costs it a
    /// rotation tick first. Two ticks of that reach five tiles, not thirteen.
    /// Pricing a shot against the ball in a locked arm undervalues every
    /// straight bolt on the board and overvalues every bend, which is precisely
    /// the trade this doctrine exists to get right.</para>
    ///
    /// <para>The search is over pose, not position — a state is a tile and a
    /// facing, a tick is either a rotation or a step along the current facing —
    /// so it is the exact legality the contract describes rather than an
    /// approximation of it.</para>
    /// </summary>
    private List<Position> Reachable(
        GenericActorContext.ObservedEnemyState enemy,
        int moves)
    {
        var reached = new List<Position> { enemy.Position };
        if (Coupling(enemy)
            != GenericActorRulesContract.MovementFacingCoupling.FacingLocked)
        {
            var seen = new HashSet<Position> { enemy.Position };
            var frontier = new List<Position> { enemy.Position };
            for (int step = 0; step < moves; step++)
            {
                var next = new List<Position>();
                foreach (Position tile in frontier)
                {
                    foreach (Direction direction in Field.Cardinals)
                    {
                        (int dx, int dy) = direction.Vector();
                        Position neighbour = tile.Offset(dx, dy);
                        if (_doctrine.IsOpen(neighbour) && seen.Add(neighbour))
                        {
                            next.Add(neighbour);
                            reached.Add(neighbour);
                        }
                    }
                }
                frontier = next;
            }
            return reached;
        }

        var poses = new HashSet<(Position Tile, Direction Facing)>
        {
            (enemy.Position, enemy.Facing),
        };
        var tiles = new HashSet<Position> { enemy.Position };
        var wave = new List<(Position Tile, Direction Facing)>
        {
            (enemy.Position, enemy.Facing),
        };
        for (int step = 0; step < moves; step++)
        {
            var next = new List<(Position Tile, Direction Facing)>();
            foreach ((Position tile, Direction facing) in wave)
            {
                // A rotation spends the tick and moves nothing.
                foreach (Direction turn in Field.Cardinals)
                {
                    if (turn != facing && poses.Add((tile, turn)))
                        next.Add((tile, turn));
                }
                // A step is only ever legal along the current facing.
                (int dx, int dy) = facing.Vector();
                Position ahead = tile.Offset(dx, dy);
                if (_doctrine.IsOpen(ahead) && poses.Add((ahead, facing)))
                {
                    next.Add((ahead, facing));
                    if (tiles.Add(ahead))
                        reached.Add(ahead);
                }
            }
            wave = next;
        }
        return reached;
    }

    /// <summary>
    /// How a step changes this body's facing, read from its own form's movement
    /// profile rather than from this life's.
    /// </summary>
    private GenericActorRulesContract.MovementFacingCoupling Coupling(
        GenericActorContext.ObservedEnemyState enemy) =>
        _doctrine.CouplingFor(enemy.FormId);

    /// <summary>
    /// True when this body cannot leave its tile for the whole window a shot
    /// needs. Two contract facts say so: a published windup due tick, and a
    /// form whose own action mask declares no movement at all — which is what
    /// every stance in this kit is.
    /// </summary>
    private bool Immobile(
        GenericActorContext.ObservedEnemyState enemy,
        int moves)
    {
        GenericActorRulesContract.Form? form =
            _doctrine.FormFor(enemy.FormId);
        if (form is not null
            && !form.AllowedActionIds.Any(actionId =>
                _doctrine.Contract.Rules.Actions.Any(action =>
                    action.Kind
                        == GenericActorRulesContract.ActionKind.Movement
                    && string.Equals(
                        action.Id,
                        actionId,
                        StringComparison.Ordinal))))
        {
            return true;
        }
        return _field.FrozenUntil(enemy.ActorId) is int due
            && _field.Context.Tick + moves <= due;
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

        // A body inside a wait-only windup, or in a stance form its own action
        // mask gives no movement to, is not choosing to hold still — it cannot
        // move. Both facts are contract reads, and both collapse the whole
        // distribution onto one tile, which is the largest single correction
        // available to a shot model on this board.
        if (Immobile(enemy, moves))
        {
            var certain = new Dictionary<Position, double>
            {
                [enemy.Position] = 1.0,
            };
            _reach[key] = certain;
            return certain;
        }

        var weights = new Dictionary<Position, double>();
        int originDistance = _field.DistanceToObjective(enemy.Position);

        // A target that cannot see the shooter has no reason to be anywhere
        // other than where it already is. One that can see it is priced on
        // what bodies this life has actually watched do, not on an assumption
        // that survives being wrong for four hundred ticks. Where a step is also
        // a turn, the target's sidestep spends its own aim and sight quadrant,
        // so it takes fewer of them; the arm sets that prior and the ledger
        // settles it.
        //
        // Facing-locked used to sit in the same prior at triple weight, which
        // was a guess standing in for geometry. It is geometry: the reachable
        // set below is now built from the target's own declared coupling, so the
        // prior no longer has to carry what the map already says.
        double priorScale = _field.MoveTurns
            ? 1.8
            : Coupling(enemy)
                == GenericActorRulesContract.MovementFacingCoupling.FacingLocked
                ? 1.4
                : 1.0;
        double inertia = Watched(enemy, shooter)
            ? 2.5 * _dodges.WatchedInertiaFactor(priorScale)
            : 7.0 * priorScale;

        foreach (Position tile in Reachable(enemy, moves))
        {
            double weight = 1.0;
            if (tile == enemy.Position)
                weight *= inertia;
            if (_field.IsObjective(tile))
                weight *= 2.5;
            else if (_field.DistanceToObjective(tile) < originDistance)
                weight *= 1.4;
            weights[tile] = weight;
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
