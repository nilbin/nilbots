using BotArena.Sdk;

/// <summary>
/// Still Water — a patient interceptor.
///
/// It refuses the closing duel. It walks to a station one bend's reach behind
/// the contested point, turns its gun across the approach, and lets the other
/// side spend tiles and tempo coming to it. Ground is currency it is happy to
/// pay: a tile given up that restores the standoff band is a good trade,
/// because inside that band one private bend covers the widest lateral fan a
/// striker owns, and a body dodging the visible line walks into the arc rather
/// than out of it. It takes the point last — once the opponent is worn down,
/// or once the clock makes territory the only thing left to buy.
/// </summary>
public sealed class StillWater : IGenericActorBot
{
    private enum Posture
    {
        Deny,
        Contest,
        Seize,
        Withdraw,

        /// <summary>
        /// A loaded gun with no legal line. Walls and strict corners can put a
        /// visible body outside every trajectory this chassis owns; the answer
        /// is to take the angle, not to spend bolts into a corner.
        /// </summary>
        Reposition,
    }

    private readonly QuarryTracker _tracker = new();

    /// <summary>
    /// Destinations a joint step already refused, kept for a few ticks so a
    /// contested tile is not re-attempted every tick. Availability is a
    /// per-body fact; only the resolution tells you the tile was contested.
    /// </summary>
    private readonly Dictionary<Position, int> _refused = [];
    private Doctrine? _doctrine;
    private Position? _previousTile;

    public void StartLife(GenericActorMatchStart start)
    {
        _doctrine = new Doctrine(start);
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        if (_doctrine is not Doctrine doctrine)
            return SafeFallback(context);

        var book = new ActionBook(doctrine.Contract, context);
        _tracker.Observe(context);

        Field field = doctrine.Field;
        var self = context.Self;
        var attack = doctrine.Attack(self.FormId);
        var threat = new ThreatField(field, doctrine, context);

        // Whether a step is a free strafe, a committed turn, or illegal unless
        // you already point that way is a declared property of this form's
        // movement profile. Every place below that trades ground for a firing
        // line reads it instead of assuming the historical strafe.
        var coupling = doctrine.Coupling(self.FormId);
        Direction[] order = ArenaBasics.OrderedDirections(doctrine.Contract, context);

        var mode = context.Mode as GenericActorContext.ModeObservationState.Frontline;
        int activeIndex = mode?.ActivePositionIndex ?? -1;
        Position[] activeTiles = doctrine.TilesAt(activeIndex);
        var objective = activeTiles.ToHashSet();

        HashSet<Position> blocked = OccupiedTiles(context, self);
        RememberRefusals(context, self, blocked);

        int[] objectiveCost = field.CostField(
            activeTiles.Length > 0 ? activeTiles : [self.Position],
            null);

        int maxSteps = attack is null
            ? 2
            : ForkPlanner.ImpactOffset(attack, attack.Projectile.MaxTravelTiles) + 1;
        var forecasts = new List<EnemyForecast>();
        foreach (var enemy in context.Enemies)
        {
            forecasts.Add(
                new EnemyForecast(
                    field,
                    doctrine,
                    enemy,
                    _tracker.Get(enemy.ActorId),
                    objectiveCost,
                    objective,
                    maxSteps));
        }

        Posture posture = ChoosePosture(
            doctrine, context, mode, forecasts, objective, self, coupling,
            field.Cost(objectiveCost, self.Position));

        // Seeking an angle never means giving up ground already held: the
        // interceptor only walks for a line when it is not standing on the point.
        List<Position> angles = [];
        if (posture is Posture.Deny or Posture.Seize
            && attack is not null
            && forecasts.Count > 0
            && self.Cooldown <= 1
            && !objective.Contains(self.Position)
            && !AnyLine(field, attack, self.Position, forecasts))
        {
            angles = FiringAngles(field, attack, self.Position, forecasts, 3);
            if (angles.Count > 0)
                posture = Posture.Reposition;
        }

        // Conceding ground means restoring the band, not walking home; the
        // retreat line is deliberately only two tiles deeper than the station.
        int standoff = posture == Posture.Withdraw
            ? doctrine.StandBand + 2
            : doctrine.StandBand;

        Position[] goals = posture switch
        {
            Posture.Seize or Posture.Contest =>
                activeTiles.Length > 0 ? activeTiles : [self.Position],
            Posture.Reposition => angles.ToArray(),
            _ => [Station(doctrine, activeIndex, standoff, objective, attack)],
        };
        int[] goalCost = field.CostField(goals, blocked);
        if (field.Cost(goalCost, self.Position) == int.MaxValue)
            goalCost = field.CostField(goals, null);

        // Some resolved contracts hand companions over automatically and some
        // require the Prime to ask. Both routes are read from the mask, never
        // assumed: a body worth more than one bolt is always worth the tick.
        if (!threat.ImmediateImpact(self.Position)
            && Fabricate(doctrine, book) is { } companion)
        {
            return companion;
        }

        Shot? plan = PlanShot(
            context, book, attack, forecasts, field, objective);

        // A bolt that traverses this tile during the coming resolution outranks
        // every other consideration: a body that survives keeps firing.
        bool lethalHere = threat.ImmediateImpact(self.Position)
            || threat.OccupiedByBolt(self.Position);

        List<Option> options = BuildOptions(
            doctrine, context, book, field, threat, forecasts, goalCost,
            objective, posture, attack, blocked, _previousTile, coupling, order);
        _previousTile = self.Position;

        Option? best = null;
        Option? stay = null;
        foreach (Option option in options)
        {
            if (option.IsStay)
                stay = option;
            if (best is null || option.Score > best.Value.Score)
                best = option;
        }

        if (lethalHere)
        {
            // Prefer an escape that is still alive three ticks from now: a step
            // that only postpones the same bolt by one tile is not an evasion.
            Option? escape = null;
            foreach (Option option in options)
            {
                if (!option.IsMove
                    || threat.ImmediateImpact(option.Tile)
                    || threat.OccupiedByBolt(option.Tile))
                {
                    continue;
                }
                if (escape is { Survives: true } && !option.Survives)
                    continue;
                if (escape is null
                    || (option.Survives && !escape.Value.Survives)
                    || option.Score > escape.Value.Score)
                {
                    escape = option;
                }
            }
            // Step off the line when stepping off costs little. In a corridor
            // every tile is the line, and giving the ground back to a bolt that
            // will simply arrive again is a loop, not an evasion.
            if (escape is { } stepAside
                && (best is null
                    || best.Value.IsStay
                    || stepAside.Score + 3.0 >= best.Value.Score))
            {
                return stepAside.Decision;
            }
            if (best is { IsStay: false } anyMove)
                return anyMove.Decision;
        }

        double stayScore = stay?.Score ?? double.MinValue;
        double gain = best is { IsStay: false } candidate
            ? candidate.Score - stayScore
            : 0;

        // Tempo rule: spend the tick on the gun when the trajectory actually
        // arrives on a predicted body, or when walking would not have bought
        // anything anyway. Speculative sweep never outranks repositioning.
        // A bolt is a commitment, not a guess. Still Water only fires when the
        // trajectory arrives on a tile some prediction actually names; a curve
        // that a wall or a strict corner will eat is a wasted tempo beat, and
        // tempo is the whole point of standing off.
        if (plan is { Anchored: true } shot && (shot.Value >= 2.5 || gain < 0.75))
            return shot.Decision;

        if (best is { } chosen && !chosen.IsStay)
            return chosen.Decision;

        if (plan is { Anchored: true } holdingShot)
            return holdingShot.Decision;

        return best?.Decision
            ?? book.Fallback($"still water at {self.Position}");
    }

    private readonly record struct Option(
        GenericActorDecision Decision,
        double Score,
        bool IsStay,
        Position Tile,
        bool IsMove,
        bool Survives);

    private readonly record struct Shot(
        GenericActorDecision Decision,
        double Value,
        bool Anchored);

    private static GenericActorDecision SafeFallback(GenericActorContext context)
    {
        foreach (var legality in context.ActionLegalities
                     .Where(entry => entry.Available && entry.Constraints.IsEmpty)
                     .OrderBy(entry => entry.ActionId, StringComparer.Ordinal))
        {
            return GenericActorDecision.WithoutArguments(
                legality.ActionId,
                legality.ActionCode,
                "no resolved contract");
        }
        foreach (var legality in context.ActionLegalities.Where(e => e.Available))
        {
            var directions = legality.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
            if (directions is not null && !directions.AllowedValues.IsEmpty)
            {
                return new GenericActorDecision(
                    legality.ActionId,
                    legality.ActionCode,
                    [
                        new GenericActorActionArgument.DirectionArgument(
                            directions.AllowedValues[0]),
                    ],
                    "no resolved contract");
            }
        }
        return GenericActorDecision.WithoutArguments("wait", 0, "no action offered");
    }

    private void RememberRefusals(
        GenericActorContext context,
        GenericActorContext.ObservedSelfState self,
        HashSet<Position> blocked)
    {
        if (self.PreviousActionResolution is
            {
                Outcome: GenericActorActionResolution.ActionOutcome.Blocked,
            } previous)
        {
            var argument = previous.AcceptedAction.Arguments
                .OfType<GenericActorActionArgument.DirectionArgument>()
                .FirstOrDefault();
            if (argument is not null)
            {
                (int dx, int dy) = argument.Value.Vector();
                _refused[self.Position.Offset(dx, dy)] = context.Tick + 3;
            }
        }

        foreach (Position tile in _refused.Keys.ToList())
        {
            if (_refused[tile] <= context.Tick)
                _refused.Remove(tile);
            else
                blocked.Add(tile);
        }
    }

    private static HashSet<Position> OccupiedTiles(
        GenericActorContext context,
        GenericActorContext.ObservedSelfState self)
    {
        var blocked = new HashSet<Position>();
        foreach (var ally in context.Allies)
        {
            if (!ally.ActorId.Equals(self.ActorId))
                blocked.Add(ally.Position);
        }
        foreach (var enemy in context.Enemies)
            blocked.Add(enemy.Position);

        if (self.PreviousActionResolution is
            {
                Outcome: GenericActorActionResolution.ActionOutcome.Blocked,
            } previous)
        {
            var argument = previous.AcceptedAction.Arguments
                .OfType<GenericActorActionArgument.DirectionArgument>()
                .FirstOrDefault();
            if (argument is not null)
            {
                (int dx, int dy) = argument.Value.Vector();
                blocked.Add(self.Position.Offset(dx, dy));
            }
        }
        return blocked;
    }

    private Posture ChoosePosture(
        Doctrine doctrine,
        GenericActorContext context,
        GenericActorContext.ModeObservationState.Frontline? mode,
        List<EnemyForecast> forecasts,
        HashSet<Position> objective,
        GenericActorContext.ObservedSelfState self,
        GenericActorRulesContract.MovementFacingCoupling coupling,
        int objectiveReach)
    {
        int threshold = doctrine.Capture?.Threshold ?? 15;
        bool enemyClaims = mode?.ClaimingTeamId is int claimer
            && claimer != doctrine.TeamId;
        bool weClaim = mode?.ClaimingTeamId == doctrine.TeamId;
        int progress = mode?.CaptureProgress ?? 0;
        int index = mode?.ActivePositionIndex ?? -1;
        int remaining = doctrine.MaxTicks - context.Tick;
        int reach = objectiveReach == int.MaxValue
            ? doctrine.StandBand
            : objectiveReach;

        // The last position in either direction ends the match. Patience is a
        // way of arriving at that moment healthy, not a reason to decline it.
        bool matchPoint = index >= 0
            && !doctrine.HasPosition(index + doctrine.IndexDelta);
        bool lastDitch = index >= 0
            && !doctrine.HasPosition(index - doctrine.IndexDelta);

        // The ledger. At the tick cap the ranking is signed territory plus the
        // residual claim on the live point, and the only thing that removes an
        // opposing claim is a body standing there. Standing off is a bet that
        // there is still time to buy the ground back later; once the clock can
        // no longer pay for the erosion plus the walk, that bet has already
        // lost and a single adverse point of progress is the whole margin.
        // Both of this lineage's recorded defeats were decided inside the
        // one-and-two-point dead band the old trigger deliberately ignored.
        bool ledgerClosing = enemyClaims
            && remaining
                <= doctrine.TicksToNeutralise(progress, context.Tick) + reach + 4;

        // The point is only lost while somebody else stands on it alone.
        // Breaking that is the one thing worth walking into a bad range for.
        int contestTrigger = lastDitch ? threshold / 8 : threshold / 5;
        if (ledgerClosing)
            contestTrigger = 1;
        if (enemyClaims && progress >= Math.Max(1, contestTrigger))
            return Posture.Contest;

        if (matchPoint && self.Health >= 2)
            return Posture.Seize;

        int budget = doctrine.TicksToCapture(context.Tick) + doctrine.StandBand + 6;
        (long mine, long theirs) = Territory(context, doctrine.TeamId);
        bool ahead = mine > theirs;
        if (remaining <= budget * (ahead ? 1 : 2))
            return Posture.Seize;

        bool contact = forecasts.Count > 0
            || Heard(context)
            || _tracker.Ghosts(context, 8).Count > 0;
        if (!contact)
            return Posture.Seize;

        bool enemyOnPoint = false;
        foreach (EnemyForecast forecast in forecasts)
            enemyOnPoint |= objective.Contains(forecast.State.Position);
        if (weClaim && !enemyOnPoint)
            return Posture.Seize;

        // Standing off is a bargain with the enemy's gun, not a refusal of
        // ground. It only pays while a body is actually on the point or one
        // step from it, where entering would buy a mutual stalemate instead of
        // progress. Being merely shot at is not a reason to decline the ground.
        bool occupied = false;
        foreach (EnemyForecast forecast in forecasts)
        {
            foreach (Position tile in objective)
            {
                if (forecast.State.Position.ChebyshevDistance(tile) <= 1)
                {
                    occupied = true;
                    break;
                }
            }
            if (occupied)
                break;
        }
        if (!occupied)
            return Posture.Seize;

        bool worn = forecasts.Count > 0 && self.Health >= 2;
        foreach (EnemyForecast forecast in forecasts)
            worn &= forecast.State.Health <= 1;
        if (worn)
            return Posture.Seize;

        int nearest = int.MaxValue;
        foreach (EnemyForecast forecast in forecasts)
        {
            nearest = Math.Min(
                nearest,
                self.Position.ChebyshevDistance(forecast.State.Position));
        }

        // Giving ground is only cheap while a step keeps the gun where it was.
        // Under a coupling arm a retreat turns the muzzle away — or, when
        // movement is locked to facing, costs a whole tick to turn before the
        // first tile is even paid for. Then a hurt body is worth more holding
        // its line and firing than walking backwards blind, so the withdrawal
        // clause narrows to a genuine breach of the band and disappears
        // entirely when turning is the price of every step.
        bool retreatIsFree = coupling
            == GenericActorRulesContract.MovementFacingCoupling.PreserveFacing;
        bool retreatWorthTheTurn = coupling
                == GenericActorRulesContract.MovementFacingCoupling
                    .FaceMovementDirection
            && nearest < doctrine.StandBand;
        if (self.Health <= 1
            && self.Cooldown > 0
            && nearest <= doctrine.OpposingAnyRange
            && (retreatIsFree || retreatWorthTheTurn))
        {
            return Posture.Withdraw;
        }

        return Posture.Deny;
    }

    /// <summary>
    /// Requests a companion when the current mask offers a fabrication action
    /// with a legal stable-slot target. Own slots first, then whatever the
    /// contract declares legal; absence of the action is simply no companion.
    /// </summary>
    private static GenericActorDecision? Fabricate(Doctrine doctrine, ActionBook book)
    {
        foreach (var legality in book.All(
                     GenericActorRulesContract.ActionKind.Fabrication))
        {
            var targets = legality.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint>()
                .SingleOrDefault();
            if (targets is null || targets.AllowedValues.IsEmpty)
                continue;

            GenericActorActionArgument.UnitTarget chosen = targets.AllowedValues[0];
            bool found = false;
            foreach (var candidate in targets.AllowedValues)
            {
                if (candidate.TeamId != doctrine.TeamId)
                    continue;
                if (!found || candidate.UnitId < chosen.UnitId)
                {
                    chosen = candidate;
                    found = true;
                }
            }
            return new GenericActorDecision(
                legality.ActionId,
                legality.ActionCode,
                [new GenericActorActionArgument.UnitTargetArgument(chosen)],
                $"raising companion {chosen.TeamId}:{chosen.UnitId}");
        }
        return null;
    }

    /// <summary>Whether any facing from this tile already has a legal line.</summary>
    private static bool AnyLine(
        Field field,
        GenericActorRulesContract.AttackProfile attack,
        Position from,
        List<EnemyForecast> forecasts)
    {
        foreach (EnemyForecast forecast in forecasts)
        {
            foreach (Direction facing in Field.Cardinals)
            {
                if (ForkPlanner.CanCoverFrom(
                        field, from, forecast.State.Position, attack, facing))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Nearby tiles from which some facing does have a legal trajectory onto a
    /// visible body. Bounded by <paramref name="radius"/> so seeking an angle
    /// never becomes a march.
    /// </summary>
    private static List<Position> FiringAngles(
        Field field,
        GenericActorRulesContract.AttackProfile attack,
        Position from,
        List<EnemyForecast> forecasts,
        int radius)
    {
        var angles = new List<Position>();
        int[] reach = field.CostField([from], null);
        for (int y = Math.Max(0, from.Y - radius);
             y <= Math.Min(field.Height - 1, from.Y + radius);
             y++)
        {
            for (int x = Math.Max(0, from.X - radius);
                 x <= Math.Min(field.Width - 1, from.X + radius);
                 x++)
            {
                var tile = new Position(x, y);
                int cost = field.Cost(reach, tile);
                if (cost <= 0 || cost > radius)
                    continue;
                if (AnyLine(field, attack, tile, forecasts))
                    angles.Add(tile);
            }
        }
        return angles;
    }

    private static bool Heard(GenericActorContext context)
    {
        var sounds = context.HeardSounds ?? [];
        foreach (var sound in sounds)
        {
            if (sound.Distance <= 1)
                return true;
        }
        return false;
    }

    private static (long Mine, long Theirs) Territory(
        GenericActorContext context,
        int teamId)
    {
        long mine = 0;
        long theirs = long.MinValue;
        foreach (var team in context.Scoreboard.Teams)
        {
            long value = 0;
            foreach (var score in team.Scores)
                value += score.Value;
            if (team.TeamId == teamId)
                mine += value;
            else
                theirs = Math.Max(theirs, value);
        }
        return (mine, theirs == long.MinValue ? 0 : theirs);
    }

    /// <summary>
    /// The interception station: a tile roughly one standoff from the near edge
    /// of the contested point, from which the gun already covers the point, and
    /// offset laterally by this slot's rank so allied bodies hold different fans
    /// instead of stacking one line. When the front sits against my own base
    /// there is no ground left behind it, so the search accepts a flanking tile
    /// beside the point rather than walking into the back wall.
    /// </summary>
    private static Position Station(
        Doctrine doctrine,
        int activeIndex,
        int standoff,
        HashSet<Position> objective,
        GenericActorRulesContract.AttackProfile? attack)
    {
        Field field = doctrine.Field;
        Position edge = doctrine.NearEdge(activeIndex);
        int edgeDepth = doctrine.Project(edge);
        Position wanted = edge.Offset(
            (-doctrine.Forward.Dx * standoff)
                + (doctrine.Lateral.Dx * doctrine.LateralBias),
            (-doctrine.Forward.Dy * standoff)
                + (doctrine.Lateral.Dy * doctrine.LateralBias));
        if (field.IsOpen(wanted) && !objective.Contains(wanted))
            return wanted;

        int minimum = Math.Max(2, standoff - 3);
        Position best = default;
        double bestScore = double.MinValue;
        bool found = false;

        for (int dy = -standoff; dy <= standoff; dy++)
        {
            for (int dx = -standoff; dx <= standoff; dx++)
            {
                Position tile = edge.Offset(dx, dy);
                int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                if (distance < minimum || distance > standoff)
                    continue;
                if (!field.IsOpen(tile) || objective.Contains(tile))
                    continue;

                double score = -1.0 * (Math.Abs(tile.X - wanted.X)
                    + Math.Abs(tile.Y - wanted.Y));
                if (doctrine.Project(tile) > edgeDepth)
                    score -= 4.0;

                if (attack is not null)
                {
                    int covered = 0;
                    foreach (Position point in objective)
                    {
                        if (covered >= 2)
                            break;
                        if (ForkPlanner.CanCover(field, tile, point, attack))
                            covered++;
                    }
                    score += 1.0 * covered;
                }

                if (!found || score > bestScore)
                {
                    best = tile;
                    bestScore = score;
                    found = true;
                }
            }
        }

        return found
            ? best
            : field.NearestOpen(edge, 4, tile => !objective.Contains(tile));
    }

    private static List<Option> BuildOptions(
        Doctrine doctrine,
        GenericActorContext context,
        ActionBook book,
        Field field,
        ThreatField threat,
        List<EnemyForecast> forecasts,
        int[] goalCost,
        HashSet<Position> objective,
        Posture posture,
        GenericActorRulesContract.AttackProfile? attack,
        HashSet<Position> blocked,
        Position? previousTile,
        GenericActorRulesContract.MovementFacingCoupling coupling,
        Direction[] order)
    {
        var self = context.Self;
        var options = new List<Option>();

        bool Survives(Position tile, Direction facing) =>
            !threat.HasBolts
            || threat.Survivable(tile, facing, coupling, blocked);

        // Under a face-movement arm the step itself is the turn, so a candidate
        // tile must be judged with the facing it actually leaves you in.
        Direction FacingAfter(Direction moved) =>
            coupling
                == GenericActorRulesContract.MovementFacingCoupling
                    .FaceMovementDirection
                ? moved
                : self.Facing;

        double goalWeight = posture switch
        {
            Posture.Seize => 1.7,
            Posture.Contest => 2.1,
            Posture.Reposition => 2.4,
            Posture.Withdraw => 1.2,
            _ => 1.0,
        };
        double dangerWeight = posture switch
        {
            Posture.Contest => 0.7,
            Posture.Seize => 0.9,
            Posture.Reposition => 0.35,
            Posture.Withdraw => 2.0,
            _ => 1.3,
        };
        double bandWeight = posture == Posture.Deny ? 1.1 : 0.0;

        int hereCost = field.Cost(goalCost, self.Position);
        bool committed = posture is Posture.Seize or Posture.Contest
            or Posture.Reposition;

        double Evaluate(Position tile, Direction facing, bool isStay, bool survives)
        {
            int cost = field.Cost(goalCost, tile);
            double score = -goalWeight * (cost == int.MaxValue ? 40 : cost);
            score -= dangerWeight * threat.Danger(tile);

            // A tile you cannot leave before the bolt arrives is not still
            // water, it is a coffin. This outranks every positional argument
            // the doctrine owns, including crossing a choke on purpose.
            if (!survives)
                score -= 30.0;

            // A choke cannot be dodged, only crossed. Once the decision to take
            // the ground is made, giving the tile back to a bolt that will keep
            // arriving is not caution — it is an infinite loop.
            if (committed
                && !isStay
                && hereCost != int.MaxValue
                && cost > hereCost)
            {
                score -= 6.0;
            }
            if (!isStay && tile == previousTile)
                score -= 1.5;
            score += 1.1 * Coverage(
                field, attack, tile, facing, forecasts, objective, posture);
            score -= bandWeight * BandPenalty(doctrine, tile, forecasts);
            if (!isStay && threat.OccupiedByBolt(tile))
                score -= 20.0;
            if (posture == Posture.Deny && objective.Contains(tile))
                score -= 2.0;
            if (!isStay)
                score -= 0.15;
            return score;
        }

        bool staySurvives = Survives(self.Position, self.Facing);
        double stayScore = Evaluate(
            self.Position, self.Facing, isStay: true, staySurvives);
        options.Add(
            new Option(
                book.Fallback($"holding station at {self.Position}"),
                stayScore,
                IsStay: true,
                self.Position,
                IsMove: false,
                staySurvives));

        var move = book.Move;
        if (move is not null)
        {
            // Equal-scoring steps are broken by the contract's own front axis
            // with the residual tie randomised per life; an absolute compass
            // order here would be a systematic side bias on a mirror map.
            var legal = ActionBook.Directions(move);
            foreach (Direction direction in order)
            {
                if (!legal.Contains(direction))
                    continue;
                (int dx, int dy) = direction.Vector();
                Position destination = self.Position.Offset(dx, dy);
                if (field.IsWall(destination) || blocked.Contains(destination))
                    continue;
                Direction after = FacingAfter(direction);
                bool survives = Survives(destination, after);
                options.Add(
                    new Option(
                        book.Directional(
                            move,
                            direction,
                            $"{posture} step {direction}"),
                        Evaluate(destination, after, isStay: false, survives),
                        IsStay: false,
                        destination,
                        IsMove: true,
                        survives));
            }
        }

        var rotate = book.Rotate;
        bool locked = coupling
            == GenericActorRulesContract.MovementFacingCoupling.FacingLocked;
        if (rotate is not null && (attack is not null || locked))
        {
            int coveredNow = attack is null
                ? 0
                : CoveredBodies(
                    field, attack, self.Position, self.Facing, forecasts);
            var legal = ActionBook.Directions(rotate);
            foreach (Direction direction in order)
            {
                if (direction == self.Facing || !legal.Contains(direction))
                    continue;
                double bonus = 0;
                if (attack is not null)
                {
                    int coveredThen = CoveredBodies(
                        field, attack, self.Position, direction, forecasts);
                    bonus = 1.7 * (coveredThen - coveredNow);
                    if (coveredThen == coveredNow && forecasts.Count == 0)
                        bonus += FacesFront(doctrine, direction) ? 0.45 : -0.2;
                }

                // When movement is locked to facing, a turn is the steering
                // wheel as well as the gun: it is the only way to open a lane,
                // and the only way off a tile a bolt is about to reach.
                if (locked)
                {
                    (int dx, int dy) = direction.Vector();
                    Position lane = self.Position.Offset(dx, dy);
                    if (!field.IsWall(lane) && !blocked.Contains(lane))
                    {
                        if (field.Cost(goalCost, lane) < hereCost)
                            bonus += 1.2;
                        if (!staySurvives && Survives(lane, direction))
                            bonus += 6.0;
                    }
                }

                if (bonus <= 0.01)
                    continue;
                options.Add(
                    new Option(
                        book.Directional(
                            rotate,
                            direction,
                            $"opening the fan {direction}"),
                        stayScore + bonus - 0.05,
                        IsStay: false,
                        self.Position,
                        IsMove: false,
                        staySurvives));
            }
        }
        return options;
    }

    private static bool FacesFront(Doctrine doctrine, Direction direction)
    {
        (int dx, int dy) = direction.Vector();
        return dx == doctrine.Forward.Dx && dy == doctrine.Forward.Dy;
    }

    private static int CoveredBodies(
        Field field,
        GenericActorRulesContract.AttackProfile attack,
        Position from,
        Direction facing,
        List<EnemyForecast> forecasts)
    {
        int covered = 0;
        foreach (EnemyForecast forecast in forecasts)
        {
            if (ForkPlanner.CanCoverFrom(
                    field, from, forecast.State.Position, attack, facing))
            {
                covered++;
            }
        }
        return covered;
    }

    private static double Coverage(
        Field field,
        GenericActorRulesContract.AttackProfile? attack,
        Position from,
        Direction facing,
        List<EnemyForecast> forecasts,
        HashSet<Position> objective,
        Posture posture)
    {
        if (attack is null)
            return 0;

        double value = 1.5 * CoveredBodies(field, attack, from, facing, forecasts);
        if (posture == Posture.Seize)
            return value;

        int tiles = 0;
        foreach (Position tile in objective)
        {
            if (tiles >= 4)
                break;
            if (ForkPlanner.CanCoverFrom(field, from, tile, attack, facing))
                tiles++;
        }
        return value + (0.55 * tiles);
    }

    /// <summary>
    /// The doctrine's whole positional argument in one number: distance to the
    /// nearest enemy, measured against the band where one bend is worth most.
    /// Closing is penalised harder than yielding, which is what makes conceding
    /// ground the cheap option.
    /// </summary>
    private static double BandPenalty(
        Doctrine doctrine,
        Position tile,
        List<EnemyForecast> forecasts)
    {
        if (forecasts.Count == 0)
            return 0;
        int nearest = int.MaxValue;
        foreach (EnemyForecast forecast in forecasts)
        {
            nearest = Math.Min(
                nearest,
                tile.ChebyshevDistance(forecast.State.Position));
        }
        int delta = nearest - doctrine.StandBand;
        return delta < 0 ? -delta * 1.6 : delta * 0.7;
    }

    /// <summary>
    /// Picks the trajectory whose swept tiles best match where the enemy will
    /// actually be when the bolt arrives. A bend wins ties because its heading
    /// stays a lie until the tile it turns on.
    /// </summary>
    private static Shot? PlanShot(
        GenericActorContext context,
        ActionBook book,
        GenericActorRulesContract.AttackProfile? attack,
        List<EnemyForecast> forecasts,
        Field field,
        HashSet<Position> objective)
    {
        if (attack is null || forecasts.Count == 0 || book.Attacks.Count == 0)
            return null;

        var stoppers = new HashSet<Position>();
        foreach (EnemyForecast forecast in forecasts)
            stoppers.Add(forecast.State.Position);

        List<ShotPlan> plans = ForkPlanner.Plans(
            field, context.Self.Position, context.Self.Facing, attack, stoppers);
        int damage = attack.Projectile.DamagePerHit;

        Shot? best = null;
        foreach (ShotPlan plan in plans)
        {
            var legality = Match(book, plan);
            if (legality is null)
                continue;

            double peak = 0;
            int touched = 0;
            bool anchored = false;
            int aimedIndex = -1;
            Position aimed = context.Self.Position;
            for (int index = 0; index < plan.Swept.Count; index++)
            {
                (Position tile, int _, int offset) = plan.Swept[index];
                int steps = offset + 1;
                double local = 0;
                bool localAnchored = false;
                foreach (EnemyForecast forecast in forecasts)
                {
                    int weight = forecast.Weight(tile, steps);
                    if (weight <= 0)
                        continue;
                    double scaled = weight * forecast.Pressure(damage);
                    if (scaled > local)
                    {
                        local = scaled;
                        localAnchored = forecast.IsAnchored(tile, steps);
                    }
                }
                if (local <= 0)
                    continue;
                touched++;
                if (local > peak)
                {
                    peak = local;
                    aimed = tile;
                    aimedIndex = index;
                    anchored = localAnchored;
                }
            }
            if (peak <= 0)
                continue;

            double value = peak + (0.25 * Math.Max(0, touched - 1));
            // A bend is only a lie worth telling when the tile it is aimed at
            // lies past the turn. A curve whose target sits before the bend —
            // or whose bend a wall or a strict corner eats — sweeps exactly
            // what the straight shot sweeps and merely spends the commitment.
            if (plan.BendRealized && aimedIndex >= plan.FirstBentIndex)
                value += 0.5;
            if (objective.Contains(aimed))
                value += 0.4;
            if (anchored)
                value += 1.5;

            if (best is null || value > best.Value.Value)
                best = new Shot(Build(legality, plan, aimed), value, anchored);
        }
        return best;
    }

    private static GenericActorActionLegality? Match(ActionBook book, ShotPlan plan)
    {
        foreach (var legality in book.Attacks)
        {
            if (plan.Heading is ProjectileHeading heading)
            {
                var headings = legality.Constraints
                    .OfType<GenericActorActionLegality.ArgumentConstraint
                        .ProjectileHeadingConstraint>()
                    .SingleOrDefault();
                if (headings is not null && headings.AllowedValues.Contains(heading))
                    return legality;
                continue;
            }

            var programs = legality.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ShotProgramConstraint>()
                .SingleOrDefault();
            if (plan.UsePayload)
            {
                if (programs is { Allowed: true })
                    return legality;
                continue;
            }
            bool wantsHeading = legality.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .Any();
            if (!wantsHeading)
                return legality;
        }
        return null;
    }

    private static GenericActorDecision Build(
        GenericActorActionLegality legality,
        ShotPlan plan,
        Position aimed)
    {
        string reason = plan.Program.BendCount > 0
            ? $"bend {plan.Program.BendDirection} after "
                + $"{plan.Program.BendAfterTiles} onto {aimed}"
            : $"straight onto {aimed}";

        if (plan.Heading is ProjectileHeading heading)
        {
            return new GenericActorDecision(
                legality.ActionId,
                legality.ActionCode,
                [new GenericActorActionArgument.ProjectileHeadingArgument(heading)],
                reason);
        }
        if (!plan.UsePayload)
        {
            return GenericActorDecision.WithoutArguments(
                legality.ActionId,
                legality.ActionCode,
                reason);
        }
        return new GenericActorDecision(
            legality.ActionId,
            legality.ActionCode,
            [new GenericActorActionArgument.ShotProgramArgument(plan.Program)],
            reason);
    }
}
