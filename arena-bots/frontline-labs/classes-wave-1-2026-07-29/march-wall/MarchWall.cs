using BotArena.Sdk;

/// <summary>
/// ADVANCING WALL. Companion bodies anchor into turrets at the chokes around a
/// front we already hold, so the fortified line creeps forward one objective at
/// a time. The Prime never joins the wall casually: it stays mobile behind it,
/// takes the hits its durability is for, and is the body that finishes a push.
/// It fortifies only when one tile decides the match, and pays the long visible
/// windup for it.
///
/// Everything below is resolved from <see cref="GenericActorMatchStart.Contract"/>
/// and the per-tick legality mask. When the contract declares no anchor route,
/// no mobilize route, or no fabrication action, the corresponding doctrine step
/// simply does not fire and the body falls back to taking and holding ground.
/// </summary>
public sealed class MarchWall : IGenericActorBot
{
    private const int EndgameHoldWindow = 60;
    private const int FabricationPatienceTicks = 15;
    private const int PrimeReturnPatienceTicks = 12;
    private const int BlockedTileMemoryTicks = 6;

    private readonly Dictionary<Position, int> _blockedUntilTick = [];
    private ContractView? _view;
    private AnchorPlanner.Site? _plannedSite;
    private int _plannedSiteTick = -1;
    private Position? _dodgeOrigin;
    private int _avoidDodgeOriginThroughTick = -1;
    private int _companionReadySinceTick = -1;

    public void StartLife(GenericActorMatchStart start)
    {
        _view = new ContractView(start);
        _blockedUntilTick.Clear();
        _plannedSite = null;
        _plannedSiteTick = -1;
        _dodgeOrigin = null;
        _avoidDodgeOriginThroughTick = -1;
        _companionReadySinceTick = -1;
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        ContractView view = _view
            ?? throw new InvalidOperationException("StartLife was not called.");
        RememberBlockedTile(context);

        // A committed same-life transition owns the tick; the declared pending
        // policy leaves nothing else legal.
        if (context.Self.PendingSameLifeTransition is not null)
            return Fallback(view, context, "committed to the transition windup");

        return view.IsFortified(context.Self.FormId)
            ? HoldTheWall(view, context)
            : March(view, context);
    }

    // ---------------------------------------------------------------- turret

    private GenericActorDecision HoldTheWall(
        ContractView view,
        GenericActorContext context)
    {
        int activeIndex = ActiveIndex(context);
        IReadOnlyList<Position> objective = view.ObjectiveTiles(activeIndex);
        Dictionary<Position, FireControl.Shot> shots =
            FireControl.Solutions(view, context);

        foreach (GenericActorContext.ObservedEnemyState enemy
                 in Prioritized(view, context, objective))
        {
            if (shots.TryGetValue(enemy.Position, out FireControl.Shot? shot))
                return FireControl.Decision(shot, $"suppressing {enemy.ActorId}");
        }

        // A wall segment concedes nothing: deny the tiles they are walking into.
        // Only an uncommitted straight bolt that arrives no sooner than they
        // could — a curve is a commitment, and it is spent on real bodies.
        GenericActorRulesContract.AttackProfile? gun =
            view.Attack(context.Self.FormId);
        if (gun is not null)
        {
            foreach (GenericActorContext.ObservedEnemyState enemy
                     in Prioritized(view, context, objective))
            {
                foreach (Position tile in Predicted(view, enemy, objective))
                {
                    if (!shots.TryGetValue(tile, out FireControl.Shot? shot)
                        || shot.Bends != 0)
                    {
                        continue;
                    }
                    int arrival = FireControl.ArrivalOffset(
                        gun.Projectile,
                        shot.PathLength);
                    if (Geometry.Manhattan(enemy.Position, tile) > arrival)
                        continue;
                    return FireControl.Decision(shot, "denying the approach");
                }
            }
        }

        GenericActorDecision? mobilize =
            TryMobilize(view, context, objective);
        if (mobilize is not null)
            return mobilize;

        return Fallback(view, context, "holding the fortified front");
    }

    /// <summary>
    /// The wall advances by picking itself up. Once the front has moved out of
    /// this segment's reach and nothing is left to shoot, revert to a mobile
    /// body and walk to the new choke. When the contract declares no route back,
    /// the segment simply stands.
    /// </summary>
    private static GenericActorDecision? TryMobilize(
        ContractView view,
        GenericActorContext context,
        IReadOnlyList<Position> objective)
    {
        GenericActorRulesContract.FormTransition? route =
            view.MobilizeRoute(context.Self.FormId);
        if (route is null || objective.Count == 0)
            return null;

        int reach = view.Attack(context.Self.FormId)?.Projectile.MaxTravelTiles ?? 6;
        if (Geometry.Coverage(view.IsWall, context.Self.Position, objective, reach) > 0)
            return null;
        if (objective.Min(tile =>
                Geometry.Chebyshev(context.Self.Position, tile)) <= reach)
        {
            return null;
        }
        if (context.Enemies.Any(enemy =>
                Geometry.Chebyshev(enemy.Position, context.Self.Position) <= reach))
        {
            return null;
        }

        return Transform(
            view,
            context,
            route.TargetFormId,
            "front has moved on; mobilizing to re-anchor");
    }

    // ---------------------------------------------------------------- mobile

    private GenericActorDecision March(
        ContractView view,
        GenericActorContext context)
    {
        int activeIndex = ActiveIndex(context);
        IReadOnlyList<Position> objective = view.ObjectiveTiles(activeIndex);
        HashSet<Position> objectiveTiles = objective.ToHashSet();
        Dictionary<Position, FireControl.Shot> shots =
            FireControl.Solutions(view, context);
        int incoming = Threat.Hits(view, context, context.Self.Position, 1);

        // Bulwark bodies absorb; they step aside only from a killing batch.
        if (incoming >= context.Self.Health)
        {
            GenericActorDecision? escape = Evade(
                view,
                context,
                objectiveTiles,
                allowLeavingObjective: true);
            if (escape is not null)
                return escape;
        }

        GenericActorDecision? build = TryFabricate(view, context, objective);
        if (build is not null)
            return build;

        GenericActorDecision? fortify =
            TryAnchor(view, context, activeIndex, objective);
        if (fortify is not null)
            return fortify;

        foreach (GenericActorContext.ObservedEnemyState enemy
                 in Prioritized(view, context, objective))
        {
            if (shots.TryGetValue(enemy.Position, out FireControl.Shot? shot))
                return FireControl.Decision(shot, $"direct fire on {enemy.ActorId}");
        }

        // Objective-preserving response: sidestep inside the contested region
        // rather than surrendering the tile. Leaving it is a wounded body's move.
        if (incoming > 0)
        {
            GenericActorDecision? sidestep = Evade(
                view,
                context,
                objectiveTiles,
                allowLeavingObjective: context.Self.Health <= 1);
            if (sidestep is not null)
                return sidestep;
        }

        GenericActorDecision? advance =
            MarchOrders(view, context, activeIndex, objective);
        if (advance is not null)
            return advance;

        return HoldTheLine(view, context, objective);
    }

    private GenericActorDecision? MarchOrders(
        ContractView view,
        GenericActorContext context,
        int activeIndex,
        IReadOnlyList<Position> objective)
    {
        IEnumerable<Position> avoid = Avoided(context);

        GenericActorRulesContract.FormTransition? anchor =
            view.AnchorRoute(context.Self.FormId);
        if (anchor is not null)
        {
            AnchorPlanner.Site? site =
                PlannedSite(view, context, anchor, activeIndex);
            if (site is not null
                && site.Position != context.Self.Position
                && FortifyPermitted(view, context, site))
            {
                GenericActorDecision? toSite = Navigation.Toward(
                    view,
                    context,
                    [site.Position],
                    avoid,
                    "marching to the choke to extend the wall");
                if (toSite is not null)
                    return toSite;
            }
        }

        if (objective.Count > 0)
        {
            return Navigation.Toward(
                view,
                context,
                objective,
                avoid,
                "taking the contested position");
        }

        GenericActorContext.ObservedEnemyState? nearest = context.Enemies
            .OrderBy(enemy =>
                Geometry.Chebyshev(enemy.Position, context.Self.Position))
            .ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault();
        return nearest is null
            ? null
            : Navigation.Toward(
                view,
                context,
                [nearest.Position],
                avoid,
                "closing on the nearest enemy");
    }

    /// <summary>
    /// Standing on the ground we came for with no shot and nowhere better to
    /// be. A mobile body does not spend bolts on guesses; it turns, because on
    /// a contract with no aim offset the facing is the whole firing envelope.
    /// </summary>
    private GenericActorDecision HoldTheLine(
        ContractView view,
        GenericActorContext context,
        IReadOnlyList<Position> objective)
    {
        GenericActorContext.ObservedEnemyState? target =
            Prioritized(view, context, objective).FirstOrDefault();
        int reach = view.Attack(context.Self.FormId)?.Projectile.MaxTravelTiles ?? 0;
        if (target is not null
            && Geometry.Chebyshev(target.Position, context.Self.Position) <= reach)
        {
            foreach (Direction direction in Geometry.Cardinals)
            {
                if (direction == context.Self.Facing)
                    continue;
                if (!FireControl.Solutions(view, context, direction)
                        .ContainsKey(target.Position))
                {
                    continue;
                }
                GenericActorDecision? rotation = Navigation.Face(
                    view,
                    context,
                    direction,
                    $"turning the gun onto {target.ActorId}");
                if (rotation is not null)
                    return rotation;
            }
        }

        Position watch = target?.Position ?? view.EnemyReference;
        GenericActorDecision? watchward = Navigation.Face(
            view,
            context,
            Navigation.Toward(context.Self.Position, watch),
            "facing the approach");
        return watchward ?? Fallback(view, context, "holding the position");
    }

    // ------------------------------------------------------------- doctrine

    private GenericActorDecision? TryAnchor(
        ContractView view,
        GenericActorContext context,
        int activeIndex,
        IReadOnlyList<Position> objective)
    {
        GenericActorRulesContract.FormTransition? route =
            view.AnchorRoute(context.Self.FormId);
        if (route is null || objective.Count == 0)
            return null;

        AnchorPlanner.Site? site =
            PlannedSite(view, context, route, activeIndex);
        if (site is null || site.Position != context.Self.Position)
            return null;
        if (!FortifyPermitted(view, context, site))
            return null;

        // Local transform safety: lethal damage cancels the change, so do not
        // start a windup a visible batch is already going to finish.
        int windup = Math.Max(1, route.Windup.DurationTicks);
        if (Threat.Hits(view, context, context.Self.Position, windup + 1)
            >= context.Self.Health)
        {
            return null;
        }

        return Transform(
            view,
            context,
            route.TargetFormId,
            view.IsPrimeSlot
                ? "fortifying to hold the decisive position"
                : "anchoring this choke into the wall");
    }

    /// <summary>
    /// Companions build the wall freely, but never leave the team with no body
    /// that can take ground. The Prime is not a wall segment: it fortifies only
    /// when the match turns on holding one place — a lead to run out, or an
    /// enemy one push from a breach.
    /// </summary>
    /// <summary>One site evaluation per tick; the ladder consults it twice.</summary>
    private AnchorPlanner.Site? PlannedSite(
        ContractView view,
        GenericActorContext context,
        GenericActorRulesContract.FormTransition route,
        int activeIndex)
    {
        if (_plannedSiteTick != context.Tick)
        {
            _plannedSite =
                AnchorPlanner.Choose(view, context, route, activeIndex);
            _plannedSiteTick = context.Tick;
        }
        return _plannedSite;
    }

    private static bool FortifyPermitted(
        ContractView view,
        GenericActorContext context,
        AnchorPlanner.Site site)
    {
        if (!view.IsPrimeSlot)
        {
            if (context.Allies.Any(ally => view.ObjectiveWeight(ally.FormId) > 0))
                return true;
            return context.TeamUnits.Any(slot =>
                slot.State is GenericActorContext.UnitSlotState
                        .AutomaticReturnPending pending
                    && pending.DueTick <= context.Tick + PrimeReturnPatienceTicks);
        }

        if (site.Coverage < 2)
            return false;
        int maxHealth =
            view.Form(context.Self.FormId)?.MaxHealth ?? context.Self.Health;
        if (context.Self.Health * 2 < maxHealth)
            return false;

        int push = SignedPush(view, context);
        bool endgameLead =
            context.Tick >= view.MaxTicks - EndgameHoldWindow && push > 0;
        bool lastDitch = push <= -Math.Max(1, view.PushesToBreach - 1);
        return endgameLead || lastDitch;
    }

    /// <summary>
    /// Explicit fabrication when the contract has it: the wall needs bodies, so
    /// the Prime walks back to its declared source region for a Ready slot. It
    /// refuses only while it is the single weighted body on a contested
    /// objective, and even then only for a bounded number of ticks. Under a
    /// contract whose companions activate automatically this does nothing.
    /// </summary>
    private GenericActorDecision? TryFabricate(
        ContractView view,
        GenericActorContext context,
        IReadOnlyList<Position> objective)
    {
        if (view.FabricationTransition is null)
        {
            _companionReadySinceTick = -1;
            return null;
        }

        HashSet<string> fabricationIds =
            view.ActionIds(GenericActorRulesContract.ActionKind.Fabrication);
        foreach (GenericActorActionLegality action in context.ActionLegalities
                     .Where(entry =>
                         entry.Available
                         && fabricationIds.Contains(entry.ActionId))
                     .OrderBy(entry => entry.ActionId, StringComparer.Ordinal))
        {
            GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
                targets = action.Constraints
                    .OfType<GenericActorActionLegality.ArgumentConstraint
                        .UnitTargetConstraint>()
                    .SingleOrDefault();
            if (targets is null || targets.AllowedValues.IsEmpty)
                continue;

            GenericActorActionArgument.UnitTarget target =
                targets.AllowedValues[0];
            _companionReadySinceTick = -1;
            return new GenericActorDecision(
                action.ActionId,
                action.ActionCode,
                [new GenericActorActionArgument.UnitTargetArgument(target)],
                $"raising companion {target.TeamId}:{target.UnitId}");
        }

        bool slotReady = context.TeamUnits.Any(slot =>
            slot.State is GenericActorContext.UnitSlotState.Ready);
        if (!slotReady)
        {
            _companionReadySinceTick = -1;
            return null;
        }

        GenericActorRulesContract.Form? form = view.Form(context.Self.FormId);
        if (form is null || !form.AllowedActionIds.Any(fabricationIds.Contains))
            return null;
        if (_companionReadySinceTick < 0)
            _companionReadySinceTick = context.Tick;

        if (context.Tick - _companionReadySinceTick < FabricationPatienceTicks
            && SoleDefenderOfAContestedObjective(view, context, objective))
        {
            return null;
        }

        IReadOnlyList<Position> pads = view.FabricationSourceTiles();
        return pads.Count == 0
            ? null
            : Navigation.Toward(
                view,
                context,
                pads,
                Avoided(context),
                "returning to the pad to raise a companion");
    }

    private static bool SoleDefenderOfAContestedObjective(
        ContractView view,
        GenericActorContext context,
        IReadOnlyList<Position> objective)
    {
        if (objective.Count == 0)
            return false;
        if (context.Allies.Any(ally => view.ObjectiveWeight(ally.FormId) > 0))
            return false;
        if (objective.Min(tile =>
                Geometry.Chebyshev(context.Self.Position, tile)) > 2)
        {
            return false;
        }
        return context.Enemies.Any(enemy =>
            objective.Min(tile => Geometry.Chebyshev(enemy.Position, tile)) <= 3);
    }

    // -------------------------------------------------------------- helpers

    /// <summary>
    /// Step off the bolt that lands this tick. The wall does not retreat from
    /// ground it holds: while the batch is survivable a body on the objective
    /// only sidesteps inside the contested region, and absorbs the hit when
    /// there is nowhere in it to stand.
    /// </summary>
    private GenericActorDecision? Evade(
        ContractView view,
        GenericActorContext context,
        HashSet<Position> objectiveTiles,
        bool allowLeavingObjective)
    {
        GenericActorActionLegality? move = Navigation.MoveAction(view, context);
        if (move is null)
            return null;
        IReadOnlyList<Direction> allowed = Navigation.AllowedDirections(move);
        if (allowed.Count == 0)
            return null;

        HashSet<Position> occupied = Navigation.Occupied(context);
        HashSet<Position> bolts = Threat.BoltTiles(context);
        HashSet<Position> corridor = Threat.Sweep(view, context, 2);
        bool holding = objectiveTiles.Contains(context.Self.Position);
        int here = Threat.Hits(view, context, context.Self.Position, 1);

        Direction? best = null;
        int bestScore = int.MinValue;
        foreach (Direction direction in Geometry.Cardinals)
        {
            if (!allowed.Contains(direction))
                continue;
            Position destination = Geometry.Step(context.Self.Position, direction);
            if (view.IsWall(destination)
                || occupied.Contains(destination)
                || bolts.Contains(destination))
            {
                continue;
            }
            if (holding
                && !allowLeavingObjective
                && !objectiveTiles.Contains(destination))
            {
                continue;
            }

            int threat = Threat.Hits(view, context, destination, 1);
            if (threat >= here)
                continue;

            int score = -threat * 100
                + (objectiveTiles.Contains(destination) ? 40 : 0)
                - (corridor.Contains(destination) ? 20 : 0)
                - (objectiveTiles.Count == 0
                    ? 0
                    : objectiveTiles.Min(tile =>
                        Geometry.Chebyshev(destination, tile)));
            if (score <= bestScore)
                continue;
            bestScore = score;
            best = direction;
        }

        if (best is not Direction chosen)
            return null;

        _dodgeOrigin = context.Self.Position;
        _avoidDodgeOriginThroughTick = context.Tick + 1;
        return new GenericActorDecision(
            move.ActionId,
            move.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(chosen)],
            $"stepping off the shot toward {chosen}");
    }

    private static IEnumerable<GenericActorContext.ObservedEnemyState> Prioritized(
        ContractView view,
        GenericActorContext context,
        IReadOnlyList<Position> objective) =>
        context.Enemies
            .OrderByDescending(enemy => view.ObjectiveWeight(enemy.FormId) > 0)
            .ThenBy(enemy => objective.Count == 0
                ? 0
                : objective.Min(tile => Geometry.Chebyshev(enemy.Position, tile)))
            .ThenBy(enemy => enemy.Health)
            .ThenBy(enemy =>
                Geometry.Chebyshev(enemy.Position, context.Self.Position))
            .ThenBy(enemy => enemy.ActorId);

    /// <summary>Tiles an enemy plausibly steps onto next: forward, or objective-ward.</summary>
    private static IEnumerable<Position> Predicted(
        ContractView view,
        GenericActorContext.ObservedEnemyState enemy,
        IReadOnlyList<Position> objective)
    {
        var tiles = new List<Position>();
        Position forward = Geometry.Step(enemy.Position, enemy.Facing);
        if (view.IsOpen(forward))
            tiles.Add(forward);

        if (objective.Count > 0)
        {
            int current =
                objective.Min(tile => Geometry.Chebyshev(enemy.Position, tile));
            foreach (Direction direction in Geometry.Cardinals)
            {
                Position candidate = Geometry.Step(enemy.Position, direction);
                if (!view.IsOpen(candidate) || tiles.Contains(candidate))
                    continue;
                if (objective.Min(tile => Geometry.Chebyshev(candidate, tile))
                    < current)
                {
                    tiles.Add(candidate);
                }
            }
        }
        return tiles;
    }

    /// <summary>
    /// A movement the joint step refused is evidence about the map that the
    /// legality mask cannot give: reserved deployment tiles, a body that is not
    /// going to move, a lane two bodies keep claiming. Remember it briefly so
    /// the search routes around it instead of retrying the same step forever.
    /// </summary>
    private void RememberBlockedTile(GenericActorContext context)
    {
        if (context.Self.PreviousActionResolution is not
            {
                Outcome: GenericActorActionResolution.ActionOutcome.Blocked,
            } previous)
        {
            return;
        }
        GenericActorActionArgument.DirectionArgument? direction =
            previous.AcceptedAction.Arguments
                .OfType<GenericActorActionArgument.DirectionArgument>()
                .SingleOrDefault();
        if (direction is null)
            return;
        _blockedUntilTick[Geometry.Step(context.Self.Position, direction.Value)] =
            context.Tick + BlockedTileMemoryTicks;
    }

    private IEnumerable<Position> Avoided(GenericActorContext context)
    {
        var tiles = new List<Position>();
        if (_dodgeOrigin is Position origin
            && context.Tick <= _avoidDodgeOriginThroughTick)
        {
            tiles.Add(origin);
        }
        foreach ((Position tile, int until) in _blockedUntilTick)
        {
            if (until >= context.Tick && tile != context.Self.Position)
                tiles.Add(tile);
        }
        return tiles;
    }

    private static int ActiveIndex(GenericActorContext context) =>
        context.Mode is GenericActorContext.ModeObservationState.Frontline mode
            ? mode.ActivePositionIndex
            : -1;

    /// <summary>Objective positions gained in our own advance direction.</summary>
    private static int SignedPush(ContractView view, GenericActorContext context)
    {
        int active = ActiveIndex(context);
        if (active < 0)
            return 0;
        return (active - view.PositionCount / 2) * Math.Sign(view.AdvanceDelta);
    }

    private static GenericActorDecision? Transform(
        ContractView view,
        GenericActorContext context,
        string targetFormId,
        string reason)
    {
        HashSet<string> transitionIds =
            view.ActionIds(GenericActorRulesContract.ActionKind.SameLifeTransition);
        foreach (GenericActorActionLegality action in context.ActionLegalities
                     .Where(entry =>
                         entry.Available
                         && transitionIds.Contains(entry.ActionId))
                     .OrderBy(entry => entry.ActionId, StringComparer.Ordinal))
        {
            GenericActorActionLegality.ArgumentConstraint.FormTargetConstraint?
                forms = action.Constraints
                    .OfType<GenericActorActionLegality.ArgumentConstraint
                        .FormTargetConstraint>()
                    .SingleOrDefault();
            if (forms is null || !forms.AllowedFormIds.Contains(targetFormId))
                continue;
            return new GenericActorDecision(
                action.ActionId,
                action.ActionCode,
                [new GenericActorActionArgument.FormTargetArgument(targetFormId)],
                reason);
        }
        return null;
    }

    /// <summary>
    /// Always one bounded legal action. Wait when the catalog offers it,
    /// otherwise any available action whose declared argument domains can be
    /// satisfied from this tick's mask.
    /// </summary>
    private static GenericActorDecision Fallback(
        ContractView view,
        GenericActorContext context,
        string reason)
    {
        HashSet<string> waitIds =
            view.ActionIds(GenericActorRulesContract.ActionKind.Wait);
        foreach (GenericActorActionLegality action in context.ActionLegalities)
        {
            if (action.Available && waitIds.Contains(action.ActionId))
            {
                return GenericActorDecision.WithoutArguments(
                    action.ActionId,
                    action.ActionCode,
                    reason);
            }
        }

        foreach (GenericActorActionLegality action in context.ActionLegalities
                     .Where(entry => entry.Available))
        {
            List<GenericActorActionArgument>? arguments = Arguments(action);
            if (arguments is null)
                continue;
            return new GenericActorDecision(
                action.ActionId,
                action.ActionCode,
                arguments,
                reason);
        }

        GenericActorActionLegality? any = context.ActionLegalities.FirstOrDefault();
        return any is null
            ? GenericActorDecision.WithoutArguments("wait", 0, reason)
            : GenericActorDecision.WithoutArguments(
                any.ActionId,
                any.ActionCode,
                reason);
    }

    private static List<GenericActorActionArgument>? Arguments(
        GenericActorActionLegality action)
    {
        var arguments = new List<GenericActorActionArgument>();
        foreach (GenericActorActionLegality.ArgumentConstraint constraint
                 in action.Constraints)
        {
            switch (constraint)
            {
                case GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint directions:
                    if (directions.AllowedValues.IsEmpty)
                        return null;
                    arguments.Add(
                        new GenericActorActionArgument.DirectionArgument(
                            directions.AllowedValues[0]));
                    break;
                case GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint headings:
                    if (headings.AllowedValues.IsEmpty)
                        return null;
                    arguments.Add(
                        new GenericActorActionArgument.ProjectileHeadingArgument(
                            headings.AllowedValues[0]));
                    break;
                case GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint units:
                    if (units.AllowedValues.IsEmpty)
                        return null;
                    arguments.Add(
                        new GenericActorActionArgument.UnitTargetArgument(
                            units.AllowedValues[0]));
                    break;
                case GenericActorActionLegality.ArgumentConstraint
                    .FormTargetConstraint forms:
                    if (forms.AllowedFormIds.IsEmpty)
                        return null;
                    arguments.Add(
                        new GenericActorActionArgument.FormTargetArgument(
                            forms.AllowedFormIds[0]));
                    break;
                default:
                    break;
            }
        }
        return arguments;
    }
}
