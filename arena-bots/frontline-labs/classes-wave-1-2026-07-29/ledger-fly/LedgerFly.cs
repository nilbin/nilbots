using BotArena.Sdk;

/// <summary>
/// LedgerFly - the attrition banker.
///
/// One body in this team is the bank: the slot the contract returns
/// automatically after destruction. Losing it pauses the economy, so it stays
/// behind the exchange, spends its ticks on the books rather than on duels, and
/// only walks onto an objective when nobody else is holding the line or the
/// clock has already decided the match. Companions are lent reactively - to
/// settle a body we just lost or a deficit the other side has opened - and are
/// dropped as close as the declared placement offsets allow to where the last
/// exchange actually happened. Children are the currency: they contest, they
/// trade, and the bank's fast rebuild wins the long clock.
///
/// Nothing here is tuned to a particular arm. Fabrication routes, placement
/// offsets, unlock ticks, form stats, shot language, and the ranking channel
/// are read from the resolved contract and the per-tick legality mask.
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

    /// <inheritdoc />
    public void StartLife(GenericActorMatchStart start)
    {
        _lens = new MatchLens(start);
        _ledger = new Ledger();
        _rotatedForPlacement = false;
        _dodgeOrigin = null;
        _avoidDodgeOriginThrough = -1;
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
                allowCurved: true);
            if (aim is not null)
                return aim;
        }

        // 5. Suppress rather than concede while we are the ones holding.
        if (loaded && onObjective && targets.Count > 0)
        {
            GenericActorDecision? suppress = Gunnery.TrySuppress(
                lens,
                context,
                _ledger.KnownEnemyTiles);
            if (suppress is not null)
                return suppress;
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

        // 7. Idle at the standoff: keep the approach inside our facing quadrant
        //    so the bank sees the exchange coming and is already aimed at it.
        GenericActorDecision? watch = FaceAnchor(lens, context);
        if (watch is not null)
            return watch;

        return Fallback(
            context,
            holdBack ? "banking the position" : "holding the objective");
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
        FabricationRoute? route = lens.FabricationFor(context.Self.FormId);
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
                anchor);

            if (!_rotatedForPlacement
                && best is (Direction facing, int distance)
                && facing != context.Self.Facing
                && !threatened.Contains(context.Self.Position)
                && (current is null
                    || current.Value.ChebyshevDistance(anchor) - distance >= 2))
            {
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
                $"settling the ledger with {slot.TeamId}:{slot.UnitId}");
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
                + (Field.Covered(lens, context, tile) * 6)
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
        return Field.Step(move, step, "banking behind the exchange");
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
        int score = Math.Abs(tile.ChebyshevDistance(anchor) - Standoff) * 3;
        score += Field.Covered(lens, context, tile) * 10;
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
            reach = Field.Explore(lens, context.Self.Position, blocked, steps);
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
        return Field.Step(move, step, reason);
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
        return Field.Explore(lens, context.Self.Position, avoid, steps);
    }

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
        foreach (Direction direction in Field.Steps)
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
            score += Field.Covered(lens, context, destination) * 4;
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

        GenericActorActionLegality? any =
            context.ActionLegalities.FirstOrDefault();
        return any is null
            ? new GenericActorDecision("wait", 0, [], reason)
            : GenericActorDecision.WithoutArguments(
                any.ActionId,
                any.ActionCode,
                reason);
    }
}
