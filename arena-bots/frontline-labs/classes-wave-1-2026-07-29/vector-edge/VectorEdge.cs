using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// VectorEdge — a pressure duelist.
///
/// The doctrine is one sentence: the objective is the only thing worth
/// standing on, so every tick either takes ground, holds ground, or removes
/// the body contesting it. Fire is chosen by what the target can still do — a
/// straight bolt to suppress a corridor where nothing can step aside, a
/// committed bend where an open chamber offers lateral escapes. Retreat is a
/// last resort: with a bolt inbound and the objective under this body,
/// VectorEdge would rather trade a hit than concede the tile.
///
/// Nothing here assumes participant IDs, team IDs, slot counts, unlock ticks,
/// form names, action codes, map coordinates, or projectile constants. All of
/// it is resolved from the delivered contract and the per-tick legality mask.
/// </summary>
public sealed class VectorEdge : IGenericActorBot
{
    /// <summary>
    /// Expected value below which even a free tick is better spent elsewhere.
    /// It keeps a bolt that threatens nothing from looking like initiative.
    /// </summary>
    private const double FreeFire = 0.08;

    private Doctrine? _doctrine;
    private Position? _lastDodgeOrigin;
    private int _avoidDodgeOriginThroughTick = -1;

    /// <inheritdoc />
    public void StartLife(GenericActorMatchStart start)
    {
        _doctrine = Doctrine.Resolve(start);
        _lastDodgeOrigin = null;
        _avoidDodgeOriginThroughTick = -1;
    }

    /// <inheritdoc />
    public GenericActorDecision Tick(GenericActorContext context)
    {
        Doctrine? doctrine = _doctrine;
        if (doctrine is null)
            return Safe(context, "contract unavailable");
        try
        {
            return Decide(doctrine, context);
        }
        catch (Exception error)
        {
            // A bounded legal action always beats a runtime fault.
            return Safe(context, $"recovered: {error.GetType().Name}");
        }
    }

    private GenericActorDecision Decide(
        Doctrine doctrine,
        GenericActorContext context)
    {
        var field = new Field(doctrine, context);
        var solver = new ShotSolver(doctrine, field);

        // 1. More bodies is more pressure. Fabrication is only ever offered
        //    where the contract says it is legal, so this needs no geography.
        if (TryFabricate(doctrine, context) is { } fabrication)
            return fabrication;

        ShotPlan? shot = solver.Best(field.Facing, extraEnemyMoves: 0);
        double commit = CommitThreshold(field);
        Position? rally = RallyPoint(doctrine, field, context);

        // 2. An inbound bolt is a question about ground, not about health.
        if (TryAnswerIncoming(doctrine, field, context, shot, rally) is { } answer)
            return answer;

        // 3. A lone body with a slot waiting is worth a round trip: one
        //    conceded push buys a second gun for the rest of the match. Once
        //    that trip is on, it outranks trading shots at the front.
        if (rally is not null
            && TryReinforce(doctrine, field, context) is { } reinforce)
        {
            return reinforce;
        }

        // 4. A tick spent shooting is a tile not taken, so only a shot that
        //    genuinely threatens the target is worth the ground it costs.
        if (shot is not null && shot.Score >= commit)
            return shot.Decision;

        // 5. A spare body may fortify the approach the objective depends on.
        if (TryFortify(doctrine, field, context) is { } fortify)
            return fortify;

        // 6. Facing is the striker's aim. Buying it costs a tick; only do that
        //    when the shot it unlocks beats the step it replaces.
        if (TryAim(doctrine, field, context, solver, shot, commit) is { } aim)
            return aim;

        // 7. Already standing on the ground being scored: the tick is free, so
        //    a real bolt outranks shuffling away from a lane the enemy has
        //    only pointed down. Bends still have to be earned — free tempo
        //    does not make a committed trajectory a good one.
        ShotPlan? cheap = solver.Best(
            field.Facing,
            extraEnemyMoves: 0,
            allowCurved: false);
        if (field.OnObjective && cheap is not null && cheap.Score >= FreeFire)
            return cheap.Decision;

        // 8. Objective first: take the tile, or hold the tile.
        if (TryPressForward(doctrine, field, context, cheap is not null)
            is { } advance)
        {
            return advance;
        }

        // 9. Nothing left to spend the tick on: take the cheap shot, then the
        //    cheap degree of facing.
        if (cheap is not null && cheap.Score >= FreeFire)
            return cheap.Decision;
        if (TryAim(doctrine, field, context, solver, cheap, FreeFire) is { } free)
            return free;

        // 10. Keep the gun pointed where the next body will come from.
        return TryOrient(doctrine, field, context)
            ?? Safe(context, "holding the line");
    }

    private static GenericActorDecision? TryFabricate(
        Doctrine doctrine,
        GenericActorContext context)
    {
        GenericActorActionLegality? action = Available(
            doctrine,
            context,
            GenericActorRulesContract.ActionKind.Fabrication);
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint>()
                .FirstOrDefault();
        if (action is null || targets is null || targets.AllowedValues.IsEmpty)
            return null;

        GenericActorActionArgument.UnitTarget target = targets.AllowedValues
            .OrderBy(value => value.TeamId)
            .ThenBy(value => value.UnitId)
            .First();
        return new GenericActorDecision(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.UnitTargetArgument(target)],
            $"building pressure at {target.TeamId}:{target.UnitId}");
    }

    /// <summary>
    /// The fabrication source this life should be walking to, or
    /// <see langword="null"/> when its place is at the front. Recomputed every
    /// tick from the contract and the frozen observation, so the trip ends the
    /// moment it stops being worth taking.
    /// </summary>
    private static Position? RallyPoint(
        Doctrine doctrine,
        Field field,
        GenericActorContext context)
    {
        if (doctrine.FabricationSourceTiles.IsEmpty
            || field.AlliedObjectiveBodies >= 2
            || field.EnemyAdvancesRemaining < 2
            || doctrine.FabricationSourceTiles.Contains(field.Self))
        {
            return null;
        }

        HashSet<string> fabricationIds = doctrine.Contract.Rules.Actions
            .Where(action =>
                action.Kind
                    == GenericActorRulesContract.ActionKind.Fabrication)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        bool canFabricate = context.ActionLegalities.Any(action =>
            action.AllowedByForm && fabricationIds.Contains(action.ActionId));
        bool slotWaiting = context.TeamUnits.Any(slot =>
            slot.State.Kind == GenericActorContext.UnitSlotStateKind.Ready);
        if (!canFabricate || !slotWaiting)
            return null;

        return doctrine.FabricationSourceTiles
            .OrderBy(tile => field.Self.ChebyshevDistance(tile))
            .ThenBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .First();
    }

    private GenericActorDecision? TryAnswerIncoming(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        ShotPlan? shot,
        Position? rally)
    {
        if (field.ThreatAt(field.Self) is not 0)
            return null;

        GenericActorActionLegality? move = Available(
            doctrine,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = move?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .FirstOrDefault();

        (Direction Direction, Position Tile)[] escapes =
            move is null || directions is null
                ? []
                : directions.AllowedValues
                    .Select(direction =>
                    {
                        (int dx, int dy) = direction.Vector();
                        return (
                            Direction: direction,
                            Tile: field.Self.Offset(dx, dy));
                    })
                    .Where(candidate =>
                        field.CanEnter(candidate.Tile)
                        && field.ThreatAt(candidate.Tile) is not 0)
                    .ToArray();

        (Direction Direction, Position Tile)[] preferred = field.OnObjective
            ? escapes
                .Where(candidate => field.IsObjective(candidate.Tile))
                .ToArray()
            : escapes;

        // Suppression over concession: while this body owns an objective tile
        // and can survive the hit, answering the shot keeps ground that
        // stepping aside would surrender.
        if (field.OnObjective && preferred.Length == 0 && field.Health > 1)
        {
            if (shot is not null)
                return shot.Decision;
            if (TryAimAtNearest(doctrine, field, context) is { } turn)
                return turn;
            return Safe(context, "absorbing a hit to keep the objective");
        }

        (Direction Direction, Position Tile)[] pool =
            preferred.Length > 0 ? preferred : escapes;
        if (pool.Length == 0 || move is null)
            return shot?.Decision;

        Direction chosen = pool
            .OrderByDescending(candidate =>
                rally is null && field.IsObjective(candidate.Tile))
            .ThenBy(candidate => field.ThreatAt(candidate.Tile) ?? int.MaxValue)
            .ThenBy(candidate => field.InPredictedLane(candidate.Tile) ? 1 : 0)
            .ThenBy(candidate => rally is Position point
                ? candidate.Tile.ChebyshevDistance(point)
                : field.DistanceToObjective(candidate.Tile))
            .ThenBy(candidate => candidate.Direction)
            .First()
            .Direction;
        _lastDodgeOrigin = field.Self;
        _avoidDodgeOriginThroughTick = context.Tick + 1;
        return new GenericActorDecision(
            move.ActionId,
            move.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(chosen)],
            $"slipping the bolt toward {chosen}");
    }

    /// <summary>
    /// Expected value a shot must reach before it is worth the tile the tick
    /// would otherwise have bought. Driving a body off the objective is the
    /// cheapest ground there is, so that case buys in early.
    /// </summary>
    private static double CommitThreshold(Field field)
    {
        if (field.OnObjective && field.EnemyOnObjective)
            return 0.28;
        if (field.OnObjective)
            return 0.38;
        if (field.ControlPaused)
            return 0.42;
        return field.IsCapturer ? 0.58 : 0.46;
    }

    private static GenericActorDecision? TryFortify(
        Doctrine doctrine,
        Field field,
        GenericActorContext context)
    {
        GenericActorActionLegality? action = Available(
            doctrine,
            context,
            GenericActorRulesContract.ActionKind.SameLifeTransition);
        GenericActorActionLegality.ArgumentConstraint.FormTargetConstraint?
            forms = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .FormTargetConstraint>()
                .FirstOrDefault();
        if (action is null || forms is null || forms.AllowedFormIds.IsEmpty)
            return null;

        GenericActorRulesContract.Form? current =
            doctrine.FormFor(field.FormId);
        int distance = field.DistanceToObjective(field.Self);
        if (field.IsCapturer
            || field.AlliedObjectiveBodies < 2
            || distance < 1
            || distance > 3
            || field.ControlPaused
            || field.ThreatAt(field.Self) is not null
            || current is null
            || field.Health < current.MaxHealth
            || doctrine.TransitionForbidden.Contains(field.Self))
        {
            return null;
        }

        // Only a genuinely tougher, objective-neutral emplacement is worth the
        // mobility this life gives up for the rest of the match.
        GenericActorRulesContract.Form? target = forms.AllowedFormIds
            .Select(doctrine.FormFor)
            .Where(form =>
                form is not null
                && form.ObjectiveWeight <= 0
                && form.MaxHealth > current.MaxHealth
                && form.AttackProfileId is not null)
            .OrderByDescending(form => form!.MaxHealth)
            .ThenBy(form => form!.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (target is null)
            return null;

        return new GenericActorDecision(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.FormTargetArgument(target.Id)],
            $"emplacing as {target.Id} to lock the approach");
    }

    private static GenericActorDecision? TryAim(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        ShotSolver solver,
        ShotPlan? current,
        double required)
    {
        GenericActorRulesContract.AttackProfile? attack =
            doctrine.AttackFor(field.FormId);
        if (!solver.HasTargets
            || attack is null
            || !context.Enemies.Any(enemy =>
                field.Self.ChebyshevDistance(enemy.Position)
                    <= attack.Projectile.MaxTravelTiles))
        {
            return null;
        }
        GenericActorActionLegality? rotate = Available(
            doctrine,
            context,
            GenericActorRulesContract.ActionKind.Rotation);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = rotate?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .FirstOrDefault();
        if (rotate is null || directions is null)
            return null;

        double currentScore = current?.Score ?? 0.0;
        Direction? best = null;
        double bestScore = 0.0;
        foreach (Direction direction in directions.AllowedValues)
        {
            if (direction == field.Facing)
                continue;
            ShotPlan? plan = solver.Best(direction, extraEnemyMoves: 1);
            if (plan is not null && plan.Score > bestScore)
            {
                bestScore = plan.Score;
                best = direction;
            }
        }

        if (best is not Direction facing
            || bestScore < required
            || bestScore <= currentScore * 1.4)
        {
            return null;
        }
        return new GenericActorDecision(
            rotate.ActionId,
            rotate.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(facing)],
            $"laying the gun {facing} ev={bestScore:0.00}");
    }

    /// <summary>
    /// Walks back to a declared fabrication source, planning around whatever
    /// tiles other bodies are about to claim.
    /// </summary>
    private static GenericActorDecision? TryReinforce(
        Doctrine doctrine,
        Field field,
        GenericActorContext context)
    {
        GenericActorActionLegality? move = Available(
            doctrine,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = move?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .FirstOrDefault();
        if (move is null || directions is null)
            return null;

        var sources = doctrine.FabricationSourceTiles.ToHashSet();
        Direction? step = field.StepToward(
            sources,
            directions.AllowedValues.ToHashSet(),
            Contested(field, context))
            ?? field.StepToward(sources, directions.AllowedValues.ToHashSet());
        if (step is not Direction chosen)
            return null;
        return new GenericActorDecision(
            move.ActionId,
            move.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(chosen)],
            $"falling back to build a second gun via {chosen}");
    }

    private GenericActorDecision? TryPressForward(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        bool hasShot)
    {
        GenericActorActionLegality? move = Available(
            doctrine,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = move?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .FirstOrDefault();
        if (move is null || directions is null || field.Objective.IsEmpty)
            return null;

        var allowed = directions.AllowedValues.ToHashSet();
        if (field.OnObjective)
            return Reseat(doctrine, field, context, move, allowed, hasShot);

        // Stacking allies on one objective tile buys nothing, so aim at the
        // free tiles first and fall back to the whole region. A body that is
        // not the nearest to an objective an ally already holds takes the
        // supporting ring instead, where it covers the approaches.
        var free = new HashSet<Position>();
        foreach (Position tile in field.Objective)
        {
            if (!field.IsOccupied(tile) || tile == field.Self)
                free.Add(tile);
        }
        HashSet<Position> goals =
            !field.IsCapturer && field.AllyOnObjective && !field.EnemyOnObjective
                ? field.Ring(1, 2)
                : free.Count > 0
                    ? free
                    : field.Objective.ToHashSet();
        if (goals.Count == 0)
            goals = field.Objective.ToHashSet();

        var avoid = new HashSet<Position>();
        foreach (Direction direction in Field.Cardinals)
        {
            (int dx, int dy) = direction.Vector();
            Position tile = field.Self.Offset(dx, dy);
            if (field.ThreatAt(tile) is 0)
                avoid.Add(tile);
        }
        if (_lastDodgeOrigin is Position origin
            && context.Tick <= _avoidDodgeOriginThroughTick)
        {
            avoid.Add(origin);
        }

        // Two bodies walking into the same tile simply block each other, which
        // spends a tick for nothing. Every life sees the same frozen picture,
        // so the one whose identity sorts later yields the contested step —
        // no shared state, no negotiation, no deadlock.
        foreach (Position tile in Contested(field, context))
            avoid.Add(tile);

        Direction? step = field.StepToward(goals, allowed, avoid)
            ?? field.StepToward(goals, allowed);
        if (step is not Direction chosen)
            return null;
        return new GenericActorDecision(
            move.ActionId,
            move.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(chosen)],
            $"pressing the objective via {chosen}");
    }

    /// <summary>
    /// Tiles another body is about to claim: every step that takes a
    /// higher-priority ally, or any visible enemy, strictly closer to the
    /// objective it is walking toward.
    /// </summary>
    private static HashSet<Position> Contested(
        Field field,
        GenericActorContext context)
    {
        var claimed = new HashSet<Position>();
        ActorIdentity mine = context.Self.ActorId;
        var origins = new List<Position>();
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (ally.ActorId.CompareTo(mine) < 0)
                origins.Add(ally.Position);
        }
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
            origins.Add(enemy.Position);

        foreach (Position origin in origins)
        {
            int distance = field.DistanceToObjective(origin);
            foreach (Direction direction in Field.Cardinals)
            {
                (int dx, int dy) = direction.Vector();
                Position tile = origin.Offset(dx, dy);
                if (field.DistanceToObjective(tile) < distance)
                    claimed.Add(tile);
            }
        }
        return claimed;
    }

    private static GenericActorDecision? Reseat(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        GenericActorActionLegality move,
        HashSet<Direction> allowed,
        bool hasShot)
    {
        // Already on the objective, so the only steps worth taking are the
        // ones that keep it.
        (Direction Direction, Position Tile)[] seats = allowed
            .Select(direction =>
            {
                (int dx, int dy) = direction.Vector();
                return (
                    Direction: direction,
                    Tile: field.Self.Offset(dx, dy));
            })
            .Where(candidate =>
                field.IsObjective(candidate.Tile)
                && field.CanEnter(candidate.Tile)
                && field.ThreatAt(candidate.Tile) is not 0)
            .OrderBy(candidate => candidate.Direction)
            .ToArray();
        if (seats.Length == 0)
            return null;

        // Holding ground with a body in sight and no line to it is the one
        // thing a duelist must never settle for: take the seat that has one.
        int reach = doctrine.AttackFor(field.FormId)
            ?.Projectile.MaxTravelTiles ?? 0;
        if (!hasShot
            && reach > 0
            && !context.Enemies.IsEmpty
            && !HasLane(doctrine, field.Self, context, reach))
        {
            foreach ((Direction direction, Position tile) in seats)
            {
                if (!HasLane(doctrine, tile, context, reach))
                    continue;
                return new GenericActorDecision(
                    move.ActionId,
                    move.ActionCode,
                    [new GenericActorActionArgument.DirectionArgument(direction)],
                    $"taking the firing seat {direction}");
            }
        }

        // Otherwise only shuffle to leave a lane a visible enemy is already
        // pointing down, and never at the cost of a shot of our own.
        if (hasShot || !field.InPredictedLane(field.Self))
            return null;
        foreach ((Direction direction, Position tile) in seats)
        {
            if (field.InPredictedLane(tile))
                continue;
            return new GenericActorDecision(
                move.ActionId,
                move.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(direction)],
                $"shifting off the enemy lane to {direction}");
        }
        return null;
    }

    /// <summary>
    /// True when some visible enemy sits on an unobstructed cardinal line from
    /// the tile, inside the declared travel budget — the shape a straight bolt
    /// can actually use.
    /// </summary>
    private static bool HasLane(
        Doctrine doctrine,
        Position from,
        GenericActorContext context,
        int reach)
    {
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            int dx = enemy.Position.X - from.X;
            int dy = enemy.Position.Y - from.Y;
            if (dx != 0 && dy != 0)
                continue;
            int distance = Math.Abs(dx) + Math.Abs(dy);
            if (distance == 0 || distance > reach)
                continue;
            int stepX = Math.Sign(dx);
            int stepY = Math.Sign(dy);
            Position cursor = from;
            bool clear = true;
            for (int step = 0; step < distance; step++)
            {
                cursor = cursor.Offset(stepX, stepY);
                if (cursor != enemy.Position && doctrine.IsWall(cursor))
                {
                    clear = false;
                    break;
                }
            }
            if (clear)
                return true;
        }
        return false;
    }

    private static GenericActorDecision? TryOrient(
        Doctrine doctrine,
        Field field,
        GenericActorContext context)
    {
        if (TryAimAtNearest(doctrine, field, context) is { } atEnemy)
            return atEnemy;

        // No contact: face the side the next body must arrive from, so the
        // opening shot of the next duel is already lined up.
        ImmutableArray<Position> ahead =
            doctrine.TilesAt(field.ActiveIndex + doctrine.AdvanceDelta);
        ImmutableArray<Position> anchor =
            ahead.IsEmpty ? field.Objective : ahead;
        return anchor.IsEmpty
            ? null
            : Turn(
                doctrine,
                field,
                context,
                Centroid(anchor),
                "watching the approach");
    }

    private static GenericActorDecision? TryAimAtNearest(
        Doctrine doctrine,
        Field field,
        GenericActorContext context)
    {
        GenericActorContext.ObservedEnemyState? nearest = context.Enemies
            .OrderBy(enemy => field.Self.ChebyshevDistance(enemy.Position))
            .ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault();
        return nearest is null
            ? null
            : Turn(
                doctrine,
                field,
                context,
                nearest.Position,
                "tracking contact");
    }

    private static GenericActorDecision? Turn(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        Position target,
        string reason)
    {
        GenericActorActionLegality? rotate = Available(
            doctrine,
            context,
            GenericActorRulesContract.ActionKind.Rotation);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = rotate?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .FirstOrDefault();
        if (rotate is null || directions is null)
            return null;

        int dx = target.X - field.Self.X;
        int dy = target.Y - field.Self.Y;
        Direction wanted = Math.Abs(dx) >= Math.Abs(dy)
            ? dx >= 0 ? Direction.East : Direction.West
            : dy >= 0 ? Direction.South : Direction.North;
        if (wanted == field.Facing || !directions.AllowedValues.Contains(wanted))
            return null;
        return new GenericActorDecision(
            rotate.ActionId,
            rotate.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(wanted)],
            $"{reason} — facing {wanted}");
    }

    private static Position Centroid(ImmutableArray<Position> tiles)
    {
        int x = 0;
        int y = 0;
        foreach (Position tile in tiles)
        {
            x += tile.X;
            y += tile.Y;
        }
        return new Position(x / tiles.Length, y / tiles.Length);
    }

    private static GenericActorActionLegality? Available(
        Doctrine doctrine,
        GenericActorContext context,
        GenericActorRulesContract.ActionKind kind)
    {
        HashSet<string> ids = doctrine.Contract.Rules.Actions
            .Where(action => action.Kind == kind)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        return context.ActionLegalities
            .Where(action => action.Available && ids.Contains(action.ActionId))
            .OrderBy(action => action.ActionCode)
            .FirstOrDefault();
    }

    private static GenericActorDecision Safe(
        GenericActorContext context,
        string reason)
    {
        GenericActorActionLegality? fallback = context.ActionLegalities
            .Where(action => action.Available && action.Constraints.IsEmpty)
            .OrderBy(action => action.ActionCode)
            .FirstOrDefault()
            ?? context.ActionLegalities
                .Where(action => action.Available)
                .OrderBy(action => action.ActionCode)
                .FirstOrDefault()
            ?? context.ActionLegalities
                .OrderBy(action => action.ActionCode)
                .FirstOrDefault();
        return fallback is null
            ? GenericActorDecision.WithoutArguments("wait", 0, reason)
            : GenericActorDecision.WithoutArguments(
                fallback.ActionId,
                fallback.ActionCode,
                reason);
    }
}
