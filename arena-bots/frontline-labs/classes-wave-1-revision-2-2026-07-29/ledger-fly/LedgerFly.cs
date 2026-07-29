using BotArena.Sdk;

/// <summary>
/// LedgerFly - the attrition banker, revision 2.
///
/// One body in this team is the bank: the slot the contract returns
/// automatically after destruction. Losing it pauses the economy, so it stays
/// behind the exchange, spends its ticks on the books rather than on duels, and
/// only walks onto an objective when nobody else is holding the line or the
/// clock has already decided the match. Children are the currency: they
/// contest, they trade, and the bank's fast rebuild wins the long clock.
///
/// The one doctrine change this revision makes is when the bank pays. Wave 1
/// lent reactively and priced solvency against the enemies it could see; a
/// facing-quadrant sensor could see almost none of them, so it sat on a Ready
/// slot for whole minutes of match time while the other side fielded more
/// bodies. Solvency is now priced against the opposing capacity the contract
/// declares, the target is one body clear of it, and the slot is spent on the
/// earliest tick the route allows. The float is the rebuild clock, not the
/// slot.
///
/// Nothing here is tuned to a particular arm. Fabrication routes, placement
/// offsets, unlock ticks, form stats, shot language, movement facing coupling,
/// and the ranking channel are read from the resolved contract and the per-tick
/// legality mask.
/// </summary>
public sealed class LedgerFly : IGenericActorBot
{
    private const int Standoff = 3;
    private const int SearchRadius = 12;

    private MatchLens? _lens;
    private Ledger _ledger = new();
    private bool _rotatedForPlacement;
    private Position? _dodgeOrigin;
    private int _avoidDodgeOriginThrough = -1;
    private Direction[] _order = Field.Steps;
    private Kinematics? _kin;

    /// <inheritdoc />
    public void StartLife(GenericActorMatchStart start)
    {
        _lens = new MatchLens(start);
        _ledger = new Ledger();
        _rotatedForPlacement = false;
        _dodgeOrigin = null;
        _avoidDodgeOriginThrough = -1;
        _order = Field.Steps;
        _kin = null;
    }

    /// <inheritdoc />
    public GenericActorDecision Tick(GenericActorContext context)
    {
        if (_lens is not MatchLens lens)
            return Fallback(context, "no contract");
        try
        {
            return Decide(lens, context);
        }
        catch (Exception)
        {
            return Fallback(context, "falling back to a legal action");
        }
    }

    private GenericActorDecision Decide(
        MatchLens lens,
        GenericActorContext context)
    {
        _ledger.Observe(lens, context);

        // Equal-length routes and equal-score tiles must not break on an
        // absolute compass preference: on a mirror-symmetric map both teams
        // share it and the advancing side wins the tie every time. One ordered
        // preference per tick - advance first, retreat last, laterals decided
        // by this life's deterministic stream - is used by every search below.
        _order = ArenaBasics.OrderedDirections(lens.Contract, context);
        _kin = new Kinematics(lens, context);

        // A windup owns the tick: the legality mask allows nothing else.
        if (context.Self.PendingSameLifeTransition is not null)
            return Fallback(context, "committed to a form change");

        Position[] objective = lens.ActiveObjective(context);
        HashSet<Position> objectiveTiles = objective.ToHashSet();
        bool onObjective = objectiveTiles.Contains(context.Self.Position);
        HashSet<Position> blocked = Field.Blocked(lens, context);
        HashSet<Position> threatened = Field.Threatened(lens, context, 2);
        GenericActorActionLegality? move = Field.MoveAction(lens, context);
        HashSet<Direction> steps = Field.LegalSteps(move);

        bool endgame = Endgame(lens, context);
        bool frontManned = FrontManned(lens, context, objectiveTiles);
        bool holdBack = lens.IsBankSlot && frontManned && !endgame;

        // 1. Nothing is worth a body: leave a bolt's path, preferring a tile
        //    that keeps our objective presence rather than conceding the region.
        if (threatened.Contains(context.Self.Position) && move is not null)
        {
            GenericActorDecision? evade = Evade(
                lens,
                context,
                move,
                steps,
                blocked,
                threatened,
                objectiveTiles,
                onObjective);
            if (evade is not null)
            {
                _dodgeOrigin = context.Self.Position;
                _avoidDodgeOriginThrough = context.Tick + 1;
                return evade;
            }
        }

        // 2. The books come before the duel: the bank queues a replacement.
        if (lens.IsBankSlot)
        {
            GenericActorDecision? economy = Economy(
                lens,
                context,
                move,
                blocked,
                threatened,
                objectiveTiles);
            if (economy is not null)
                return economy;
        }

        // 3. Fire only at a solution we have already simulated.
        List<GenericActorContext.ObservedEnemyState> targets =
            Targets(lens, context);
        bool loaded = Gunnery.GunLoaded(lens, context);
        if (loaded && targets.Count > 0)
        {
            GenericActorDecision? shot = Gunnery.TryFire(
                lens,
                context,
                targets,
                allowCurved: true);
            if (shot is not null)
                return shot;
        }

        // 4. No lane: turn into one when the turn is free (we are holding) or
        //    the body is close enough that a lost tick is cheaper than a miss.
        if (loaded
            && targets.Count > 0
            && (onObjective || Nearest(context, targets) <= 4))
        {
            GenericActorDecision? aim = Gunnery.TryRotateToAim(
                lens,
                context,
                targets,
                allowCurved: true,
                _order);
            if (aim is not null)
                return aim;
        }

        // 5. Suppress rather than concede while we are the ones holding, and if
        //    the facing we are holding has no lane worth a bolt, turn onto one
        //    that has. A rotation does not move the body, so the tile we are
        //    contesting is never given up to buy the shot.
        if (loaded && onObjective && _ledger.KnownEnemyTiles.Count > 0)
        {
            GenericActorDecision? suppress = Gunnery.TrySuppress(
                lens,
                context,
                _ledger.KnownEnemyTiles);
            if (suppress is not null)
                return suppress;
            // Only while nothing is actually in sight. A rotation also swings
            // the facing quadrant this body sees through, and trading a live
            // contact for a remembered one is how a holder loses the exchange
            // it was already watching.
            if (targets.Count == 0)
            {
                GenericActorDecision? turn = Gunnery.TryRotateToSuppress(
                    lens,
                    context,
                    _ledger.KnownEnemyTiles,
                    _order);
                if (turn is not null)
                    return turn;
            }
        }

        // 6. Walk: the bank to its standoff, everyone else onto the objective.
        if (move is not null)
        {
            GenericActorDecision? walk = holdBack
                ? Stage(
                    lens,
                    context,
                    move,
                    steps,
                    blocked,
                    threatened,
                    objectiveTiles)
                : Contest(
                    lens,
                    context,
                    move,
                    steps,
                    blocked,
                    threatened,
                    objective,
                    onObjective,
                    preferFarEdge: !lens.IsBankSlot || endgame);
            if (walk is not null)
                return walk;
        }

        // 7. The empty-gun tick. Fire cooldown is two, so roughly every second
        //    combat tick has no shot to take, and in wave 1 those ticks became
        //    `wait` - the body stood still in the lane it had just been shot
        //    down. Step 6 could not help: its goal set was already satisfied.
        //    So spend the tick on footwork instead, inside the objective region
        //    when we are holding it, which breaks the straight or one-bend lane
        //    without conceding a tile of presence.
        if (move is not null)
        {
            GenericActorDecision? footwork = Posture(
                lens,
                context,
                move,
                steps,
                blocked,
                threatened,
                objectiveTiles,
                onObjective);
            if (footwork is not null)
                return footwork;
        }

        // 8. Idle at the standoff: keep the approach inside our facing quadrant
        //    so the bank sees the exchange coming and is already aimed at it.
        GenericActorDecision? watch = FaceAnchor(lens, context);
        if (watch is not null)
            return watch;

        return Fallback(
            context,
            holdBack ? "banking the position" : "holding the objective");
    }

    /// <summary>
    /// One tile of footwork that improves the body's posture without giving up
    /// what it is standing on. Candidates are the single legal steps only, so
    /// it can never wander off a contested region, and while we hold the
    /// objective the candidate set is restricted to objective tiles. A move is
    /// taken only on a clear improvement, which is what stops the shuffle from
    /// oscillating between two equally poor tiles.
    /// </summary>
    private GenericActorDecision? Posture(
        MatchLens lens,
        GenericActorContext context,
        GenericActorActionLegality move,
        IReadOnlySet<Direction> steps,
        IReadOnlySet<Position> blocked,
        IReadOnlySet<Position> threatened,
        IReadOnlySet<Position> objectiveTiles,
        bool onObjective)
    {
        const int Margin = 2;
        Position anchor = _ledger.ExchangeAnchor(lens, context);
        int bestScore = PostureScore(
            lens,
            context,
            context.Self.Position,
            anchor,
            objectiveTiles,
            threatened,
            onObjective);
        Direction? chosen = null;

        foreach (Direction direction in _order)
        {
            if (!steps.Contains(direction))
                continue;
            (int dx, int dy) = direction.Vector();
            Position destination = context.Self.Position.Offset(dx, dy);
            if (lens.IsWall(destination)
                || blocked.Contains(destination)
                || threatened.Contains(destination))
            {
                continue;
            }
            // Holding the region is worth more than any posture gain.
            if (onObjective && !objectiveTiles.Contains(destination))
                continue;
            if (_dodgeOrigin is Position origin
                && context.Tick <= _avoidDodgeOriginThrough
                && destination == origin)
            {
                continue;
            }

            int score = PostureScore(
                lens,
                context,
                destination,
                anchor,
                objectiveTiles,
                threatened,
                onObjective);
            score += _kin?.TurnAwayPenalty(
                context.Self.Position,
                direction,
                anchor,
                3) ?? 0;
            if (score + Margin <= bestScore)
            {
                bestScore = score;
                chosen = direction;
            }
        }

        return chosen is Direction step
            ? Field.Step(move, step, "footwork off the covered tile")
            : null;
    }

    private int PostureScore(
        MatchLens lens,
        GenericActorContext context,
        Position tile,
        Position anchor,
        IReadOnlySet<Position> objectiveTiles,
        IReadOnlySet<Position> threatened,
        bool onObjective)
    {
        int score = Field.Covered(lens, context, tile, _ledger.KnownEnemyTiles) * 10;
        score += threatened.Contains(tile) ? 12 : 0;
        if (!onObjective && objectiveTiles.Count > 0)
        {
            score += objectiveTiles.Min(
                objectiveTile => objectiveTile.ChebyshevDistance(tile));
        }
        if (lens.IsBankSlot)
        {
            score += Math.Abs(tile.ChebyshevDistance(anchor) - Standoff);
        }
        // A tile that already holds a lane on a body we know about is worth
        // standing on once the gun reloads.
        GenericActorRulesContract.AttackProfile? profile =
            lens.Attack(context.Self.FormId);
        int range = profile?.Projectile.MaxTravelTiles ?? 0;
        foreach (Position enemy in _ledger.KnownEnemyTiles)
        {
            if (tile.ChebyshevDistance(enemy) <= range
                && Field.RayReaches(lens, tile, enemy))
            {
                score -= 4;
                break;
            }
        }
        return score;
    }

    private GenericActorDecision? FaceAnchor(
        MatchLens lens,
        GenericActorContext context)
    {
        Position anchor = _ledger.ExchangeAnchor(lens, context);
        int dx = anchor.X - context.Self.Position.X;
        int dy = anchor.Y - context.Self.Position.Y;
        if (dx == 0 && dy == 0)
            return null;
        Direction desired = Math.Abs(dx) >= Math.Abs(dy)
            ? dx > 0 ? Direction.East : Direction.West
            : dy > 0 ? Direction.South : Direction.North;
        if (desired == context.Self.Facing)
            return null;
        GenericActorActionLegality? rotate = Rotation(lens, context, desired);
        return rotate is null
            ? null
            : new GenericActorDecision(
                rotate.ActionId,
                rotate.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(desired)],
                "watching the exchange");
    }

    private GenericActorDecision? Economy(
        MatchLens lens,
        GenericActorContext context,
        GenericActorActionLegality? move,
        IReadOnlySet<Position> blocked,
        IReadOnlySet<Position> threatened,
        IReadOnlySet<Position> objectiveTiles)
    {
        // The template's capability digest is the cheap precondition: no
        // explicit fabrication route for this form (an automatic-companion arm,
        // or a chassis without the verb) means the whole economy rung is inert
        // and the body falls through to the trader ladder unchanged.
        (bool _, bool _, bool fabricationRoute, int? _) =
            ArenaBasics.Capabilities(lens.Contract, context);
        FabricationRoute? route = fabricationRoute
            ? lens.FabricationFor(context.Self.FormId)
            : null;
        if (route is null || !_ledger.WantsReplacement(lens, context))
        {
            _rotatedForPlacement = false;
            return null;
        }

        GenericActorActionLegality? fabricate = context.ActionLegalities
            .Where(action =>
                action.Available
                && string.Equals(
                    action.ActionId,
                    route.ActionId,
                    StringComparison.Ordinal))
            .FirstOrDefault();
        var targets = fabricate?.Constraints
            .OfType<GenericActorActionLegality.ArgumentConstraint
                .UnitTargetConstraint>()
            .FirstOrDefault();

        if (fabricate is not null
            && targets is not null
            && !targets.AllowedValues.IsEmpty)
        {
            Position anchor = _ledger.ExchangeAnchor(lens, context);
            Position? current = route.Predict(
                lens,
                context.Self.Position,
                context.Self.Facing,
                blocked);
            (Direction Facing, int Distance)? best = route.BestFacing(
                lens,
                context.Self.Position,
                context.Self.Facing,
                blocked,
                anchor,
                _order);

            // Placement is evaluated against queue-time facing. Under a profile
            // that turns the body on a successful step, the approach direction
            // already IS the queue-time facing, so buying it with a separate
            // rotation is usually a wasted tick: prefer to walk into the pose,
            // and only pay the rotation when a step in that direction is not
            // available. Under a locked profile the rotation has to be paid
            // either way, so it stays exactly as cheap as it was.
            if (!_rotatedForPlacement
                && best is (Direction facing, int distance)
                && facing != context.Self.Facing
                && !threatened.Contains(context.Self.Position)
                && (current is null
                    || current.Value.ChebyshevDistance(anchor) - distance >= 2))
            {
                if (_kin is { TurnsOnMove: true }
                    && move is not null
                    && Field.LegalSteps(move).Contains(facing))
                {
                    (int fx, int fy) = facing.Vector();
                    Position ahead = context.Self.Position.Offset(fx, fy);
                    if (!lens.IsWall(ahead)
                        && !blocked.Contains(ahead)
                        && !threatened.Contains(ahead))
                    {
                        return Field.Step(
                            move,
                            facing,
                            "stepping into the pose the replacement needs");
                    }
                }

                GenericActorActionLegality? rotate =
                    Rotation(lens, context, facing);
                if (rotate is not null)
                {
                    _rotatedForPlacement = true;
                    return new GenericActorDecision(
                        rotate.ActionId,
                        rotate.ActionCode,
                        [
                            new GenericActorActionArgument.DirectionArgument(
                                facing),
                        ],
                        "turning so the replacement lands on the exchange");
                }
            }

            GenericActorActionArgument.UnitTarget slot = targets.AllowedValues
                .OrderBy(target => target.TeamId)
                .ThenBy(target => target.UnitId)
                .First();
            _rotatedForPlacement = false;
            _ledger.RecordQueued();
            return new GenericActorDecision(
                fabricate.ActionId,
                fabricate.ActionCode,
                [new GenericActorActionArgument.UnitTargetArgument(slot)],
                $"paying {slot.TeamId}:{slot.UnitId} toward "
                    + $"{_ledger.SolvencyTarget} bodies vs {lens.EnemyClassLabel}");
        }

        // The route exists and a slot is waiting, but we are standing off its
        // declared source region: walk back to the counter.
        if (move is null
            || route.SourceTiles.Count == 0
            || route.SourceTiles.Contains(context.Self.Position)
            || !HasReadySlot(lens, context))
        {
            return null;
        }

        Position exchange = _ledger.ExchangeAnchor(lens, context);
        return Walk(
            lens,
            context,
            move,
            Field.LegalSteps(move),
            blocked,
            threatened,
            route.SourceTiles,
            tile =>
                (tile.ChebyshevDistance(exchange) * 2)
                + (Field.Covered(lens, context, tile, _ledger.KnownEnemyTiles)
                    * 6)
                + (objectiveTiles.Contains(tile) ? 4 : 0),
            "returning to the fabrication counter");
    }

    private GenericActorDecision? Stage(
        MatchLens lens,
        GenericActorContext context,
        GenericActorActionLegality move,
        IReadOnlySet<Direction> steps,
        IReadOnlySet<Position> blocked,
        IReadOnlySet<Position> threatened,
        IReadOnlySet<Position> objectiveTiles)
    {
        Position anchor = _ledger.ExchangeAnchor(lens, context);
        FabricationRoute? route = lens.FabricationFor(context.Self.FormId);
        Field.Reach reach = Reachable(lens, context, blocked, threatened, steps);

        Position bestTile = context.Self.Position;
        int bestScore = StagingScore(
            lens,
            context,
            context.Self.Position,
            anchor,
            objectiveTiles,
            threatened,
            route,
            0);
        foreach (KeyValuePair<Position, int> entry in reach.Distance)
        {
            if (entry.Value > SearchRadius)
                continue;
            int score = StagingScore(
                lens,
                context,
                entry.Key,
                anchor,
                objectiveTiles,
                threatened,
                route,
                entry.Value);
            if (reach.FirstStep.TryGetValue(entry.Key, out Direction first))
            {
                score += _kin?.TurnAwayPenalty(
                    context.Self.Position,
                    first,
                    anchor,
                    2) ?? 0;
            }
            if (score < bestScore
                || (score == bestScore && Before(entry.Key, bestTile)))
            {
                bestScore = score;
                bestTile = entry.Key;
            }
        }

        if (bestTile == context.Self.Position
            || !reach.FirstStep.TryGetValue(bestTile, out Direction step))
        {
            return null;
        }
        return _kin?.Step(context, move, step, "banking behind the exchange")
            ?? Field.Step(move, step, "banking behind the exchange");
    }

    private int StagingScore(
        MatchLens lens,
        GenericActorContext context,
        Position tile,
        Position anchor,
        IReadOnlySet<Position> objectiveTiles,
        IReadOnlySet<Position> threatened,
        FabricationRoute? route,
        int distance)
    {
        // Re-priced retreat: when a step turns the body, every tile of standoff
        // is also a tile of lost facing, so the bank stands one tile closer and
        // keeps the exchange inside its quadrant instead of walking backwards
        // out of its own vision cone.
        int standoff = _kin is { TurnsOnMove: true } ? Standoff - 1 : Standoff;
        int score = Math.Abs(tile.ChebyshevDistance(anchor) - standoff) * 3;
        score += Field.Covered(lens, context, tile, _ledger.KnownEnemyTiles) * 10;
        score += threatened.Contains(tile) ? 12 : 0;
        score += objectiveTiles.Contains(tile) ? 8 : 0;
        score += distance;
        score += tile.ChebyshevDistance(lens.HomeAnchor) / 3;
        if (route is not null && route.SourceTiles.Contains(tile))
            score -= 3;
        if (_dodgeOrigin is Position origin
            && context.Tick <= _avoidDodgeOriginThrough
            && tile == origin)
        {
            score += 6;
        }
        return score;
    }

    private GenericActorDecision? Contest(
        MatchLens lens,
        GenericActorContext context,
        GenericActorActionLegality move,
        IReadOnlySet<Direction> steps,
        IReadOnlySet<Position> blocked,
        IReadOnlySet<Position> threatened,
        Position[] objective,
        bool onObjective,
        bool preferFarEdge)
    {
        if (objective.Length == 0 || onObjective)
            return null;

        return Walk(
            lens,
            context,
            move,
            steps,
            blocked,
            threatened,
            objective.ToHashSet(),
            tile => preferFarEdge
                ? -tile.ChebyshevDistance(lens.HomeAnchor)
                : tile.ChebyshevDistance(lens.HomeAnchor),
            "entering the contested position");
    }

    private GenericActorDecision? Walk(
        MatchLens lens,
        GenericActorContext context,
        GenericActorActionLegality move,
        IReadOnlySet<Direction> steps,
        IReadOnlySet<Position> blocked,
        IReadOnlySet<Position> threatened,
        IReadOnlySet<Position> goals,
        Func<Position, int> preference,
        string reason)
    {
        if (goals.Count == 0)
            return null;
        Field.Reach reach = Reachable(lens, context, blocked, threatened, steps);
        Position? goal = Pick(reach, goals, preference);
        if (goal is null)
        {
            reach = Field.Explore(
                lens,
                context.Self.Position,
                blocked,
                RouteSteps(steps),
                RouteOrder);
            goal = Pick(reach, goals, preference);
        }
        if (goal is null)
        {
            // Nothing in the goal set is reachable: close the distance instead.
            Position focus = goals
                .OrderBy(tile => tile.ChebyshevDistance(context.Self.Position))
                .ThenBy(tile => tile.Y)
                .ThenBy(tile => tile.X)
                .First();
            goal = reach.Distance.Keys
                .OrderBy(tile => tile.ChebyshevDistance(focus))
                .ThenBy(tile => reach.Distance[tile])
                .ThenBy(tile => tile.Y)
                .ThenBy(tile => tile.X)
                .Cast<Position?>()
                .FirstOrDefault();
        }
        if (goal is not Position destination
            || !reach.FirstStep.TryGetValue(destination, out Direction step))
        {
            return null;
        }
        return _kin?.Step(context, move, step, reason)
            ?? Field.Step(move, step, reason);
    }

    private static Position? Pick(
        Field.Reach reach,
        IReadOnlySet<Position> goals,
        Func<Position, int> preference) =>
        goals
            .Where(reach.Distance.ContainsKey)
            .OrderBy(tile => reach.Distance[tile])
            .ThenBy(preference)
            .ThenBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .Cast<Position?>()
            .FirstOrDefault();

    private Field.Reach Reachable(
        MatchLens lens,
        GenericActorContext context,
        IReadOnlySet<Position> blocked,
        IReadOnlySet<Position> threatened,
        IReadOnlySet<Direction> steps)
    {
        var avoid = blocked.ToHashSet();
        avoid.UnionWith(threatened);
        if (_dodgeOrigin is Position origin
            && context.Tick <= _avoidDodgeOriginThrough)
        {
            avoid.Add(origin);
        }
        return Field.Explore(
            lens,
            context.Self.Position,
            avoid,
            RouteSteps(steps),
            RouteOrder);
    }

    /// <summary>
    /// First steps a route may plan on. A facing-locked profile offers only the
    /// current facing to a movement action, but every cardinal is still
    /// physically reachable - it just costs a rotation first. Planning against
    /// the legality mask there would make three quarters of the map look
    /// unreachable and pin the body facing whichever way it happened to spawn;
    /// the route is planned on the geometry and paid for at emit time.
    /// </summary>
    private IReadOnlySet<Direction> RouteSteps(IReadOnlySet<Direction> steps) =>
        _kin is { MoveLocked: true } ? Field.Steps.ToHashSet() : steps;

    /// <summary>
    /// Tie-break order for route search. Randomising equal-length ties is what
    /// keeps a mirror-symmetric map fair, but on a locked profile it would also
    /// let two equally short routes alternate every tick and spend the whole
    /// match rotating between them, so there the current facing is tried first
    /// and the randomised laterals only break the remaining ties.
    /// </summary>
    private Direction[] RouteOrder =>
        _kin is { MoveLocked: true }
            ? [
                _kin.Facing,
                .. _order.Where(direction => direction != _kin.Facing),
            ]
            : _order;

    private GenericActorDecision? Evade(
        MatchLens lens,
        GenericActorContext context,
        GenericActorActionLegality move,
        IReadOnlySet<Direction> steps,
        IReadOnlySet<Position> blocked,
        IReadOnlySet<Position> threatened,
        IReadOnlySet<Position> objectiveTiles,
        bool onObjective)
    {
        Direction? chosen = null;
        int bestScore = int.MaxValue;
        Position anchor = _ledger.ExchangeAnchor(lens, context);
        // Only directions the mask actually offers: under a facing-locked
        // profile a rotation is not a dodge, it is a tick spent standing in
        // the bolt's path.
        foreach (Direction direction in _order)
        {
            if (!steps.Contains(direction))
                continue;
            (int dx, int dy) = direction.Vector();
            Position destination = context.Self.Position.Offset(dx, dy);
            if (lens.IsWall(destination)
                || blocked.Contains(destination)
                || threatened.Contains(destination))
            {
                continue;
            }

            int score = 0;
            if (onObjective && !objectiveTiles.Contains(destination))
                score += 20;
            if (objectiveTiles.Count > 0)
            {
                score += objectiveTiles
                    .Min(tile => tile.ChebyshevDistance(destination));
            }
            score += Field.Covered(
                lens,
                context,
                destination,
                _ledger.KnownEnemyTiles) * 4;
            score += _kin?.TurnAwayPenalty(
                context.Self.Position,
                direction,
                anchor,
                2) ?? 0;
            if (lens.IsBankSlot)
            {
                score += Math.Max(
                    0,
                    6 - destination.ChebyshevDistance(lens.HomeAnchor));
            }
            if (score < bestScore)
            {
                bestScore = score;
                chosen = direction;
            }
        }
        return chosen is Direction step
            ? Field.Step(move, step, "stepping off the bolt's path")
            : null;
    }

    private static List<GenericActorContext.ObservedEnemyState> Targets(
        MatchLens lens,
        GenericActorContext context)
    {
        GenericActorRulesContract.AttackProfile? profile =
            lens.Attack(context.Self.FormId);
        int damage = profile?.Projectile.DamagePerHit ?? 1;
        return context.Enemies
            .OrderBy(enemy => enemy.Health <= damage ? 0 : 1)
            .ThenBy(enemy => lens.IsEnemyBankUnit(enemy.ActorId.UnitId) ? 0 : 1)
            .ThenBy(enemy => enemy.Health)
            .ThenBy(enemy =>
                context.Self.Position.ChebyshevDistance(enemy.Position))
            .ThenBy(enemy => enemy.ActorId)
            .ToList();
    }

    private static int Nearest(
        GenericActorContext context,
        List<GenericActorContext.ObservedEnemyState> targets) =>
        targets.Count == 0
            ? int.MaxValue
            : targets.Min(enemy =>
                context.Self.Position.ChebyshevDistance(enemy.Position));

    private static bool FrontManned(
        MatchLens lens,
        GenericActorContext context,
        IReadOnlySet<Position> objectiveTiles)
    {
        if (objectiveTiles.Count == 0)
            return false;
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (lens.IsAlliedBankUnit(ally.ActorId.UnitId))
                continue;
            if (objectiveTiles.Any(tile =>
                    tile.ChebyshevDistance(ally.Position) <= 2))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasReadySlot(
        MatchLens lens,
        GenericActorContext context) =>
        context.TeamUnits.Any(slot =>
            slot.TeamId == lens.TeamId
            && slot.State.Kind == GenericActorContext.UnitSlotStateKind.Ready);

    private static GenericActorActionLegality? Rotation(
        MatchLens lens,
        GenericActorContext context,
        Direction facing)
    {
        HashSet<string> ids = lens.Contract.Rules.Actions
            .Where(action =>
                action.Kind == GenericActorRulesContract.ActionKind.Rotation)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        return context.ActionLegalities
            .Where(action =>
                action.Available
                && ids.Contains(action.ActionId)
                && action.Constraints
                    .OfType<GenericActorActionLegality.ArgumentConstraint
                        .DirectionConstraint>()
                    .Any(constraint =>
                        constraint.AllowedValues.Contains(facing)))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool Endgame(MatchLens lens, GenericActorContext context)
    {
        long mine = lens.Score(context, lens.TeamId);
        long best = context.Scoreboard.Teams
            .Where(team => team.TeamId != lens.TeamId)
            .Select(team => lens.Score(context, team.TeamId))
            .DefaultIfEmpty(0)
            .Max();
        int remaining = lens.MaxTicks - context.Tick;
        return mine <= best && remaining <= lens.CaptureThreshold * 3;
    }

    private static bool Before(Position left, Position right) =>
        left.Y < right.Y || (left.Y == right.Y && left.X < right.X);

    private static GenericActorDecision Fallback(
        GenericActorContext context,
        string reason)
    {
        foreach (GenericActorActionLegality action in context.ActionLegalities)
        {
            if (action.Available
                && string.Equals(
                    action.ActionId,
                    "wait",
                    StringComparison.Ordinal))
            {
                return GenericActorDecision.WithoutArguments(
                    action.ActionId,
                    action.ActionCode,
                    reason);
            }
        }

        foreach (GenericActorActionLegality action in context.ActionLegalities
                     .Where(entry => entry.Available)
                     .OrderBy(entry => entry.ActionCode))
        {
            var arguments = new List<GenericActorActionArgument>();
            bool usable = true;
            foreach (GenericActorActionLegality.ArgumentConstraint constraint
                     in action.Constraints)
            {
                switch (constraint)
                {
                    case GenericActorActionLegality.ArgumentConstraint
                        .ShotProgramConstraint:
                        break;
                    case GenericActorActionLegality.ArgumentConstraint
                        .DirectionConstraint direction
                        when !direction.AllowedValues.IsEmpty:
                        arguments.Add(
                            new GenericActorActionArgument.DirectionArgument(
                                direction.AllowedValues[0]));
                        break;
                    case GenericActorActionLegality.ArgumentConstraint
                        .UnitTargetConstraint unit
                        when !unit.AllowedValues.IsEmpty:
                        arguments.Add(
                            new GenericActorActionArgument.UnitTargetArgument(
                                unit.AllowedValues[0]));
                        break;
                    case GenericActorActionLegality.ArgumentConstraint
                        .FormTargetConstraint form
                        when !form.AllowedFormIds.IsEmpty:
                        arguments.Add(
                            new GenericActorActionArgument.FormTargetArgument(
                                form.AllowedFormIds[0]));
                        break;
                    case GenericActorActionLegality.ArgumentConstraint
                        .ProjectileHeadingConstraint heading
                        when !heading.AllowedValues.IsEmpty:
                        arguments.Add(
                            new GenericActorActionArgument
                                .ProjectileHeadingArgument(
                                heading.AllowedValues[0]));
                        break;
                    default:
                        usable = false;
                        break;
                }
                if (!usable)
                    break;
            }
            if (usable)
            {
                return new GenericActorDecision(
                    action.ActionId,
                    action.ActionCode,
                    arguments,
                    reason);
            }
        }

        // Last resort: the template's own never-throw wait resolution.
        return context.ActionLegalities.IsEmpty
            ? new GenericActorDecision("wait", 0, [], reason)
            : ArenaBasics.Wait(context, reason);
    }
}
