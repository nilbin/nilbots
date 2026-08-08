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
/// Revision 2 changes one thing about that: the dodge model is measured
/// instead of assumed. A shot is still priced by where the target can be when
/// each path tile is occupied, but how willing that target is to leave its
/// tile is now counted rather than guessed — see <see cref="DodgeLedger"/>.
/// Against a body that steps off every bolt, the straight answer stops being
/// worth what it looks like, and the bend or the tile wins the tick instead.
///
/// The tick ledger underneath is unchanged in what it charges and repaired in
/// what it can price: aim can now be evaluated on a cooldown tick, which is
/// the tick on which turning is free, so the gun is laid there instead of
/// being paid for with a whole shot.
///
/// Nothing here assumes participant IDs, team IDs, slot counts, unlock ticks,
/// form names, action codes, map coordinates, projectile constants, or a
/// movement profile's facing coupling. All of it is resolved from the
/// delivered contract and the per-tick legality mask.
/// </summary>
public sealed class VectorEdge : IGenericActorBot
{
    /// <summary>
    /// Expected value below which even a free tick is better spent elsewhere.
    /// It keeps a bolt that threatens nothing from looking like initiative.
    /// </summary>
    private const double FreeFire = 0.08;

    /// <summary>
    /// A rotation taken while the gun is ready throws the shot away, so the
    /// aim it buys has to be worth more than one whole bolt.
    /// </summary>
    private const double HotAimMargin = 2.0;

    /// <summary>Absolute floor under which a ready tick is never spent turning.</summary>
    private const double HotAimFloor = 0.30;

    /// <summary>Aim bought on a cooldown tick still has to beat the current facing.</summary>
    private const double ColdAimGain = 1.25;

    /// <summary>Expected value below which laying the gun threatens nothing.</summary>
    private const double ColdAimFloor = 0.10;

    private Doctrine? _doctrine;
    private DodgeLedger _dodges = new();
    private Position? _lastDodgeOrigin;
    private int _avoidDodgeOriginThroughTick = -1;

    /// <inheritdoc />
    public void StartLife(GenericActorMatchStart start)
    {
        _doctrine = Doctrine.Resolve(start);
        _dodges = new DodgeLedger();
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
        _dodges.Observe(doctrine, context);
        var field = new Field(doctrine, context);
        var solver = new ShotSolver(doctrine, field, _dodges);

        // 1. More bodies is more pressure. Fabrication is only ever offered
        //    where the contract says it is legal, so this needs no geography.
        if (TryFabricate(doctrine, context) is { } fabrication)
            return fabrication;

        // The bolt in hand — null for the whole cooldown window, which is
        // exactly what makes those ticks cheap. The straight-only answer is
        // tracked separately: a committed trajectory has to be earned, and
        // cheap tempo is not the same thing as a good reason to bend.
        ShotPlan? shot = solver.Best(field.Facing, extraEnemyMoves: 0);
        ShotPlan? straight = solver.Best(
            field.Facing,
            extraEnemyMoves: 0,
            allowCurved: false);
        Position? rally = RallyPoint(doctrine, field, context);

        // 2. An inbound bolt is a question about ground, not about health.
        if (TryAnswerIncoming(doctrine, field, context, solver, shot, rally)
            is { } answer)
        {
            return answer;
        }

        // 3. A lone body with a slot waiting is worth a round trip: one
        //    conceded push buys a second gun for the rest of the match. Once
        //    that trip is on, it outranks trading shots at the front.
        if (rally is not null
            && TryReinforce(doctrine, field, context) is { } reinforce)
        {
            return reinforce;
        }

        // 4. Plan the step first: what a tick is worth is what the step it
        //    displaces would buy, and a tick with no step left in it is free.
        March march = PlanMarch(doctrine, field, context, solver, straight);

        // 5. Fire. A committed trajectory always has to clear the price of the
        //    ground it costs; the straight answer may also be taken on a tick
        //    that had nothing else to spend.
        double positional = PositionalThreshold(field);
        if (shot is not null && shot.Score >= positional)
            return shot.Decision;
        double commit = CommitThreshold(field, march);
        if (straight is not null && straight.Score >= commit)
            return straight.Decision;

        // 6. A spare body may fortify the approach the objective depends on.
        if (TryFortify(doctrine, field, context) is { } fortify)
            return fortify;

        // 7. Facing is the striker's aim and its sight quadrant at once. Buy
        //    it on the last tick of the cooldown window, where it is free.
        if (TryLayGun(doctrine, field, context, solver, straight, march)
            is { } aim)
        {
            return aim;
        }

        // 8. Objective first: take the tile, or hold the tile.
        if (march.Decision is not null)
            return march.Decision;

        // 9. Nothing left to spend the tick on: take the cheap shot, then the
        //    cheap degree of facing.
        if (straight is not null && straight.Score >= FreeFire)
            return straight.Decision;
        if (TryLayGun(doctrine, field, context, solver, straight, March.None)
            is { } free)
        {
            return free;
        }

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
        ShotSolver solver,
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

        // Under a facing-locked profile the movement mask offers the current
        // facing and nothing else, so a sideways dodge is a rotation this tick
        // and a step the next — two ticks against a bolt that lands on the
        // first one. The escape set collapses on its own, which is the reason
        // to read the mask instead of assuming four cardinals.
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

        // Where a step is also a turn, a dodge spends the aim and the sight
        // quadrant along with the tile. Price that before taking it: a bolt in
        // hand worth more than every post-dodge answer is worth the hit,
        // because the dodge would cost the shot and the ground together.
        double bestAfter = pool.Max(candidate =>
            ReaimValue(field, solver, candidate.Direction));
        if (field.MoveTurns
            && field.OnObjective
            && field.Health > 1
            && shot is not null
            && shot.Score >= bestAfter)
        {
            return shot.Decision;
        }

        Direction chosen = pool
            .OrderByDescending(candidate =>
                rally is null && field.IsObjective(candidate.Tile))
            .ThenBy(candidate => field.ThreatAt(candidate.Tile) ?? int.MaxValue)
            .ThenBy(candidate => field.InPredictedLane(candidate.Tile) ? 1 : 0)
            .ThenBy(candidate => rally is Position point
                ? candidate.Tile.ChebyshevDistance(point)
                : field.DistanceToObjective(candidate.Tile))
            .ThenByDescending(candidate =>
                ReaimValue(field, solver, candidate.Direction))
            .ThenBy(candidate => Array.IndexOf(field.Order, candidate.Direction))
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
    /// What the gun would be worth after stepping <paramref name="step"/> —
    /// from the tile it lands on, through the facing that step leaves behind.
    /// Under an uncoupled profile the facing term drops out and only the tile
    /// moves, so the same expression serves every arm.
    /// </summary>
    private static double ReaimValue(
        Field field,
        ShotSolver solver,
        Direction step)
    {
        (int dx, int dy) = step.Vector();
        return solver.Forecast(
            field.FacingAfter(step),
            extraEnemyMoves: 1,
            from: field.Self.Offset(dx, dy));
    }

    /// <summary>
    /// Expected value a straight shot must reach before it is worth the tick.
    /// The ground is priced exactly as revision 1 priced it; the only change
    /// is that a tick with no step left in it is recognized as free rather
    /// than charged for a march that was never going to happen.
    /// </summary>
    private static double CommitThreshold(Field field, March march)
    {
        // No step to displace: any bolt that threatens something wins.
        if (march.Decision is null)
            return FreeFire;

        return PositionalThreshold(field);
    }

    /// <summary>
    /// What the contested ground is worth this tick — revision 1's whole fire
    /// policy, kept intact as the bar a committed trajectory must clear.
    /// </summary>
    private static double PositionalThreshold(Field field) =>
        field.OnObjective && field.EnemyOnObjective ? 0.28
        : field.OnObjective ? 0.38
        : field.ControlPaused ? 0.42
        : field.IsCapturer ? 0.58 : 0.46;

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

    /// <summary>
    /// Lays the gun, priced against the cadence.
    ///
    /// The measured failure of revision 1 lives here: the solver could only
    /// answer "what would I hit facing there?" on a tick when it could also
    /// fire, so every turn was paid for with a whole shot — and two of every
    /// five such turns never produced one, because the life ended first. The
    /// forecast now works on a cooldown tick, so the gun is laid on the last
    /// tick of the window, where the shot it buys is the very next one.
    /// </summary>
    private static GenericActorDecision? TryLayGun(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        ShotSolver solver,
        ShotPlan? shot,
        March march)
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

        bool ready = solver.Ready;
        int wait = ready ? 1 : Math.Max(1, field.Cooldown);
        double floor;
        if (ready)
        {
            // A ready gun is a whole bolt. Turning throws it away.
            floor = Math.Max(HotAimFloor, (shot?.Score ?? 0.0) * HotAimMargin);
        }
        else if (march.Decision is null)
        {
            floor = FreeFire;
        }
        else if (march.TakesGround || field.Cooldown > 1)
        {
            // Either the step is itself the score, or the window still has
            // room: the gun can be laid on its last tick without costing this
            // one a tile.
            return null;
        }
        else
        {
            floor = ColdAimFloor;
        }

        double held = solver.Forecast(field.Facing, wait);
        Direction? best = null;
        double bestScore = 0.0;
        foreach (Direction direction in field.Order)
        {
            if (direction == field.Facing
                || !directions.AllowedValues.Contains(direction))
            {
                continue;
            }
            double plan = solver.Forecast(direction, wait);
            if (plan > bestScore)
            {
                bestScore = plan;
                best = direction;
            }
        }

        if (best is not Direction facing
            || bestScore < floor
            || bestScore <= held * ColdAimGain)
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
        HashSet<Direction> plannable = Travelable(
            doctrine,
            field,
            context,
            directions);
        Direction? step = field.StepToward(
            sources,
            plannable,
            Contested(field, context))
            ?? field.StepToward(sources, plannable);
        if (step is not Direction chosen)
            return null;
        return Travel(
            field,
            move,
            doctrine,
            context,
            chosen,
            $"falling back to build a second gun via {chosen}")
            ?.Decision;
    }

    /// <summary>
    /// The step this tick would otherwise take, and whether that step is worth
    /// a shot. Planning it before the fire decision is what makes the fire
    /// decision honest: a tick is worth exactly what it buys.
    /// </summary>
    private March PlanMarch(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        ShotSolver solver,
        ShotPlan? straight)
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
            return March.None;

        HashSet<Direction> plannable = Travelable(
            doctrine,
            field,
            context,
            directions);
        if (field.OnObjective)
        {
            return Reseat(
                doctrine,
                field,
                context,
                solver,
                straight,
                move,
                plannable);
        }

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

        List<Direction> steps = field.StepsToward(goals, plannable, avoid);
        if (steps.Count == 0)
            steps = field.StepsToward(goals, plannable);
        if (steps.Count == 0)
            return March.None;

        Direction chosen = Prefer(field, solver, steps);
        return Travel(
            field,
            move,
            doctrine,
            context,
            chosen,
            $"pressing the objective via {chosen}")
            ?? March.None;
    }

    /// <summary>
    /// Directions this life may plan a route through. Under a facing-locked
    /// profile only the current facing is offered to a move, so a route step
    /// in any other direction is a rotation this tick and a step the next; the
    /// rotation domain is therefore the honest travel domain.
    /// </summary>
    private static HashSet<Direction> Travelable(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint
            movement)
    {
        if (!field.MoveLocked)
            return movement.AllowedValues.ToHashSet();
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            turns = Available(
                doctrine,
                context,
                GenericActorRulesContract.ActionKind.Rotation)
                ?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .FirstOrDefault();
        return turns is null
            ? movement.AllowedValues.ToHashSet()
            : turns.AllowedValues.ToHashSet();
    }

    /// <summary>
    /// Turns a chosen travel direction into the action that actually starts
    /// it. Where the profile allows that step it is the step; where only the
    /// facing may move, it is the turn that makes the step legal next tick.
    /// </summary>
    private static March? Travel(
        Field field,
        GenericActorActionLegality move,
        Doctrine doctrine,
        GenericActorContext context,
        Direction chosen,
        string reason)
    {
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = move.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .FirstOrDefault();
        (int dx, int dy) = chosen.Vector();
        bool gains = field.IsObjective(field.Self.Offset(dx, dy));
        if (directions is not null && directions.AllowedValues.Contains(chosen))
        {
            return new March(
                new GenericActorDecision(
                    move.ActionId,
                    move.ActionCode,
                    [new GenericActorActionArgument.DirectionArgument(chosen)],
                    reason),
                gains);
        }

        if (!field.MoveLocked)
            return null;
        GenericActorActionLegality? rotate = Available(
            doctrine,
            context,
            GenericActorRulesContract.ActionKind.Rotation);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            turns = rotate?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .FirstOrDefault();
        if (rotate is null
            || turns is null
            || !turns.AllowedValues.Contains(chosen))
        {
            return null;
        }
        return new March(
            new GenericActorDecision(
                rotate.ActionId,
                rotate.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(chosen)],
                $"turning onto the route — {chosen}"),
            gains);
    }

    /// <summary>
    /// Picks between equally short first steps. Where a step is also a turn,
    /// the route and the aim are one decision, so the tie goes to the step
    /// that leaves the gun looking at something.
    /// </summary>
    private static Direction Prefer(
        Field field,
        ShotSolver solver,
        List<Direction> steps)
    {
        if (steps.Count == 1 || !field.MoveTurns)
            return steps[0];
        Direction best = steps[0];
        double bestScore = ReaimValue(field, solver, best);
        for (int index = 1; index < steps.Count; index++)
        {
            double score = ReaimValue(field, solver, steps[index]);
            if (score > bestScore)
            {
                bestScore = score;
                best = steps[index];
            }
        }
        return best;
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

    private static March Reseat(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        ShotSolver solver,
        ShotPlan? straight,
        GenericActorActionLegality move,
        HashSet<Direction> allowed)
    {
        // Already on the objective, so the only steps worth taking are the
        // ones that keep it. Where a step is also a turn, the seat and the
        // facing are one choice, so seats are ranked by what they could fire.
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
            .OrderByDescending(candidate =>
                ReaimValue(field, solver, candidate.Direction))
            .ThenBy(candidate => Array.IndexOf(field.Order, candidate.Direction))
            .ToArray();
        if (seats.Length == 0)
            return March.None;

        // Holding ground with a body in sight and no line to it is the one
        // thing a duelist must never settle for: take the seat that has one.
        int reach = doctrine.AttackFor(field.FormId)
            ?.Projectile.MaxTravelTiles ?? 0;
        // A bolt this life can fire on this very tick — not one it could fire
        // if the gun were ready. Holding a seat because of a shot that is two
        // ticks away is how a body stays in a lane it should have left.
        bool hasShot = straight is not null;
        if (!hasShot
            && reach > 0
            && !context.Enemies.IsEmpty
            && !HasLane(doctrine, field.Self, context, reach))
        {
            foreach ((Direction direction, Position tile) in seats)
            {
                if (!HasLane(doctrine, tile, context, reach))
                    continue;
                return Travel(
                    field,
                    move,
                    doctrine,
                    context,
                    direction,
                    $"taking the firing seat {direction}")
                    ?? March.None;
            }
        }

        // Otherwise only shuffle to leave a lane a visible enemy is already
        // pointing down, and never at the cost of a shot of our own.
        if (hasShot || !field.InPredictedLane(field.Self))
            return March.None;
        foreach ((Direction direction, Position tile) in seats)
        {
            if (field.InPredictedLane(tile))
                continue;
            return Travel(
                field,
                move,
                doctrine,
                context,
                direction,
                $"shifting off the enemy lane to {direction}")
                ?? March.None;
        }
        return March.None;
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
        // Track the nearest body when nothing better has claimed the tick.
        // Facing is perception as well as aim: a contact that leaves the sight
        // quadrant stops arriving in the observation at all, and every shot
        // after that is fired at a memory.
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

/// <summary>
/// The step a tick would otherwise take, and whether that step is itself the
/// score. Fire is priced against this: only ground worth taking outbids a gun
/// that is ready now.
/// </summary>
internal readonly record struct March(
    GenericActorDecision? Decision,
    bool TakesGround)
{
    /// <summary>No step worth taking this tick.</summary>
    public static readonly March None = new(null, false);
}
