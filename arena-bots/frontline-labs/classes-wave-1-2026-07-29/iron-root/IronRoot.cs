using BotArena.Sdk;

/// <summary>
/// IRON ROOT — the FORTRESS ROTATOR doctrine.
///
/// One body fortifies forward: it walks to a legal covering tile beside the
/// active objective and commits to the transform windup only in a window where
/// nothing can punish it. Every other body stays mobile, screens the scoring
/// surface, and contests rather than concedes. When the front rotates or the
/// fortress stops covering anything, it spends its single return, walks to the
/// new line, and roots again.
///
/// Nothing below names a rule. Forms, routes, windups, reach, cadence,
/// objectives, unlock ticks, tile legality, and action codes are all read from
/// the resolved contract and the current legality mask, so the same policy runs
/// on a contract with a reversible prime fortress, on one where only a
/// fabricated child may anchor, and on one with no anchor at all.
/// </summary>
public sealed class IronRoot : IGenericActorBot
{
    /// <summary>Ticks of clear air that count as simply "safe".</summary>
    private const int SafeHorizon = 4;

    private ContractLens? _lens;
    private readonly Dictionary<string, (Position Tile, int Tick)> _seen =
        new(StringComparer.Ordinal);

    private List<Position> _sites = [];
    private List<Position> _overwatch = [];
    private int _planIndex = -1;
    private int _planRange = -1;
    private int _bestCoverage;

    private int _siteFloor = 1;
    private int _lastHealth = -1;
    private int _lastDamageTick = int.MinValue / 4;
    private int _staticSinceTick = int.MaxValue;
    private string? _veto;
    private Position? _blockedTile;
    private int _blockedThroughTick = -1;
    private Position? _dodgeOrigin;
    private int _dodgeThroughTick = -1;
    private readonly Dictionary<Position, int> _refusals = [];
    private readonly HashSet<Position> _denied = [];
    private int _refusalsClearedTick;

    public void StartLife(GenericActorMatchStart start)
    {
        _lens = new ContractLens(start);
        _seen.Clear();
        _sites = [];
        _overwatch = [];
        _planIndex = -1;
        _planRange = -1;
        _bestCoverage = 0;
        _lastHealth = -1;
        _lastDamageTick = int.MinValue / 4;
        _staticSinceTick = int.MaxValue;
        _veto = null;
        _blockedTile = null;
        _blockedThroughTick = -1;
        _dodgeOrigin = null;
        _dodgeThroughTick = -1;
        _refusals.Clear();
        _denied.Clear();
        _refusalsClearedTick = 0;
        _siteFloor = 1;
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        try
        {
            return Decide(context);
        }
        catch (Exception)
        {
            // A bounded legal action always beats a fault.
            return SafeAction(context, "falling back to a legal action");
        }
    }

    private GenericActorDecision Decide(GenericActorContext context)
    {
        ContractLens? lens = _lens;
        if (lens is null)
            return SafeAction(context, "no contract");

        Observe(context);

        // A committed windup is wait-only by declaration; do not fight it.
        if (context.Self.PendingSameLifeTransition is not null)
            return SafeAction(context, "riding out the transform windup");

        GenericActorRulesContract.Form? form = lens.Form(context.Self.FormId);
        var mode = context.Mode
            as GenericActorContext.ModeObservationState.Frontline;
        int activeIndex = mode?.ActivePositionIndex ?? -1;
        Position[] active = lens.ObjectiveTiles(activeIndex);
        List<Gunnery.Target> targets = BuildTargets(lens, context, active);

        return lens.IsStatic(context.Self.FormId)
            ? FortressTick(lens, context, form, mode, active, targets)
            : FieldTick(lens, context, form, mode, active, targets, activeIndex);
    }

    // ---------------------------------------------------------------- rooted

    private GenericActorDecision FortressTick(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.Form? form,
        GenericActorContext.ModeObservationState.Frontline? mode,
        Position[] active,
        List<Gunnery.Target> targets)
    {
        if (_staticSinceTick == int.MaxValue)
            _staticSinceTick = context.Tick;

        int reach = lens.Reach(context.Self.FormId);
        bool strict = lens.Attack(form)?.Projectile.DiagonalCornersMustBeClear
            ?? true;
        int coverage = FortressPlan.Coverage(
            lens.Map,
            context.Self.Position,
            active,
            reach,
            strict);

        GenericActorDecision? mobilize = TryMobilize(lens, context, mode, coverage);
        if (mobilize is not null)
            return mobilize;

        GenericActorDecision? shot = Gunnery.TryFire(lens, context, form, targets);
        if (shot is not null)
            return shot;

        // Suppression beats concession: an idle gun that covers the scoring
        // surface keeps firing down it while the objective is not ours.
        bool pressed = mode is not null
            && (mode.ClaimingTeamId is int claimant && claimant != lens.TeamId
                || mode.CaptureProgress > 0 && mode.ClaimingTeamId != lens.TeamId);
        if (pressed || context.Enemies.Length > 0 || HeardTrouble(context, 3))
        {
            GenericActorDecision? suppress =
                Gunnery.TrySuppress(lens, context, form, active);
            if (suppress is not null)
                return suppress;
        }
        return SafeAction(context, "rooted and watching");
    }

    private GenericActorDecision? TryMobilize(
        ContractLens lens,
        GenericActorContext context,
        GenericActorContext.ModeObservationState.Frontline? mode,
        int coverage)
    {
        GenericActorRulesContract.FormTransition? route =
            lens.ReverseRoute(context.Self.FormId);
        if (route is null)
            return null;

        int rooted = context.Tick - _staticSinceTick;
        int mobileAllies = MobileAllyCount(lens, context);
        bool enemyPressing = mode is not null
            && (mode.ClaimingTeamId is int claimant && claimant != lens.TeamId
                || mode.CaptureProgress > 0 && mode.ClaimingTeamId != lens.TeamId);
        bool endgame = context.Tick
            >= lens.MaxTicks - Math.Max(30, lens.CaptureThreshold * 2);

        // The single return is spent on exactly two things: a lane that no
        // longer crosses the objective, and a scoring surface that nobody of
        // ours can stand on while the clock or the opponent is taking it.
        bool rotate = coverage == 0 && rooted >= 2;
        bool needBody = mobileAllies == 0
            && (enemyPressing || endgame)
            && rooted >= 3;
        if (!rotate && !needBody)
            return null;

        return BuildTransition(
            context,
            route,
            rotate
                ? "front rotated: unrooting to re-fortify"
                : "unrooting to hold the scoring surface");
    }

    // ---------------------------------------------------------------- mobile

    private GenericActorDecision FieldTick(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.Form? form,
        GenericActorContext.ModeObservationState.Frontline? mode,
        Position[] active,
        List<Gunnery.Target> targets,
        int activeIndex)
    {
        _staticSinceTick = int.MaxValue;

        // Union requirement: an explicitly fabricated companion is taken the
        // moment the mask offers one, exactly like a declared automatic one.
        GenericActorDecision? fabricate = TryFabricate(lens, context);
        if (fabricate is not null)
            return fabricate;

        var objective = new HashSet<Position>(active);
        bool holding = objective.Contains(context.Self.Position);
        EnsurePlan(lens, context, activeIndex);

        // A fortress that cannot be relieved is a fortress that concedes the
        // scoring surface, so the role is only taken while somebody else can
        // stand on it.
        ActorIdentity? fortressActor = FortressActor(lens, context);
        bool worth = WorthRooting(lens, context, AnchorWindup(lens, context));
        bool fortress = worth
            && fortressActor is not null
            && fortressActor == context.Self.ActorId;
        HashSet<Position> goals = SelectGoals(
            lens,
            context,
            fortress,
            objective,
            worth ? fortressActor : null);
        bool stationed = goals.Count == 0 || goals.Contains(context.Self.Position);

        GenericActorDecision? shot = Gunnery.TryFire(lens, context, form, targets);

        // Respond to a bolt that lands within a couple of ticks — or to one
        // that is further out while this tile has no perpendicular exit at all.
        // A walled duel lane kills on the tick you run out of room, not on the
        // tick the bolt gets close, so the trap has to be left early.
        int clock = TicksToImpact(lens, context, context.Self.Position);
        bool trapped = clock <= SafeHorizon
            && Outs(lens, context, context.Self.Position) == 0;
        if (clock <= 2 || trapped)
        {
            // Objective-preserving response: while we are the body on the
            // scoring surface and can answer, we answer instead of stepping off.
            bool answer = holding && context.Self.Health > 1 && shot is not null;
            if (!answer)
            {
                GenericActorDecision? dodge =
                    TryDodge(lens, context, objective, goals, holding);
                if (dodge is not null)
                    return dodge;
            }
        }
        if (shot is not null)
            return shot;

        if (fortress)
        {
            GenericActorDecision? anchor = TryAnchor(lens, context, active);
            if (anchor is not null)
                return anchor;
        }

        GenericActorDecision? turn = TryAlign(
            lens,
            context,
            form,
            targets,
            active,
            activeIndex,
            stationed);
        if (turn is not null)
            return turn;

        if (!stationed)
        {
            GenericActorDecision? step = TryStep(lens, context, goals);
            if (step is not null)
                return step;
        }
        _ = mode;
        return SafeAction(
            context,
            holding
                ? "holding the scoring surface"
                : fortress
                    ? $"on overwatch: {_veto ?? "waiting"}"
                    : "screening");
    }

    private static int AnchorWindup(
        ContractLens lens,
        GenericActorContext context)
    {
        GenericActorRulesContract.FormTransition? route =
            lens.AnchorRoute(context.Self.FormId);
        return route is null ? 1 : Math.Max(1, route.Windup.DurationTicks);
    }

    private static GenericActorDecision? TryFabricate(
        ContractLens lens,
        GenericActorContext context)
    {
        foreach (GenericActorActionLegality action in lens.Available(
                     context,
                     GenericActorRulesContract.ActionKind.Fabrication))
        {
            foreach (GenericActorActionLegality.ArgumentConstraint constraint
                     in action.Constraints)
            {
                if (constraint is not GenericActorActionLegality
                        .ArgumentConstraint.UnitTargetConstraint units
                    || units.AllowedValues.IsEmpty)
                {
                    continue;
                }
                GenericActorActionArgument.UnitTarget target =
                    units.AllowedValues[0];
                return new GenericActorDecision(
                    action.ActionId,
                    action.ActionCode,
                    [new GenericActorActionArgument.UnitTargetArgument(target)],
                    $"raising companion {target.TeamId}:{target.UnitId}");
            }
        }
        return null;
    }

    private GenericActorDecision? TryAnchor(
        ContractLens lens,
        GenericActorContext context,
        Position[] active)
    {
        GenericActorRulesContract.FormTransition? route =
            lens.AnchorRoute(context.Self.FormId);
        if (route is null || active.Length == 0)
            return null;
        if (lens.TransitionForbidden.Contains(context.Self.Position))
            return null;

        int reach = lens.Reach(route.TargetFormId);
        bool strict =
            lens.Attack(lens.Form(route.TargetFormId))
                ?.Projectile.DiagonalCornersMustBeClear
            ?? true;
        int coverage = FortressPlan.Coverage(
            lens.Map,
            context.Self.Position,
            active,
            reach,
            strict);
        if (coverage <= 0 || coverage < _siteFloor)
        {
            _veto = "not a covering tile";
            return null;
        }

        var mode = context.Mode
            as GenericActorContext.ModeObservationState.Frontline;

        // Never root into a line that is about to move. Whoever completes the
        // capture, the scoring surface rotates away and the lanes stop meaning
        // anything, which is how a one-use return gets spent on nothing.
        if (mode is not null
            && lens.CaptureThreshold > 0
            && mode.CaptureProgress >= lens.CaptureThreshold - 3)
        {
            _veto = "front about to rotate";
            return null;
        }

        // While the objective is not accruing for us, a slightly riskier window
        // is the better trade: the stalemate itself is the thing being paid for.
        bool gaining = mode is not null && mode.ClaimingTeamId == lens.TeamId;
        int margin = gaining ? 2 : 1;

        int windup = Math.Max(1, route.Windup.DurationTicks);
        int hits = ExpectedWindupHits(lens, context, windup);
        if (context.Self.Health - hits < margin)
        {
            _veto = $"windup would cost {hits}";
            return null;
        }

        _veto = null;
        return BuildTransition(
            context,
            route,
            $"rooting: {coverage} covered objective tiles");
    }

    /// <summary>
    /// The windup is a visible, punishable commitment, so it is priced rather
    /// than merely feared: how much damage can actually land on this tile before
    /// the transition completes? A muzzle only counts when it can occupy a tile
    /// with a real firing lane onto us in time, at its own declared cadence.
    /// </summary>
    private int ExpectedWindupHits(
        ContractLens lens,
        GenericActorContext context,
        int windup)
    {
        int hits = 0;
        if (context.Tick - _lastDamageTick <= 2)
            hits++;
        if (HeardTrouble(context, 2))
            hits++;

        if (Threatened(lens, context, context.Self.Position))
            hits++;

        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            GenericActorRulesContract.AttackProfile? attack =
                lens.Attack(lens.Form(enemy.FormId));
            if (attack is null)
                continue;
            HashSet<Position> lanes = FortressPlan.FiringTilesOn(
                lens.Map,
                context.Self.Position,
                attack.Projectile.MaxTravelTiles,
                attack.Projectile.DiagonalCornersMustBeClear);
            bool rooted = lens.IsStatic(enemy.FormId);
            bool canPunish;
            if (rooted)
            {
                canPunish = lanes.Contains(enemy.Position);
            }
            else
            {
                int closest = int.MaxValue;
                foreach (Position lane in lanes)
                {
                    closest = Math.Min(
                        closest,
                        enemy.Position.ChebyshevDistance(lane));
                }
                canPunish = closest <= Math.Max(0, windup - 1);
            }
            if (!canPunish)
                continue;
            int shots = 1
                + Math.Max(0, windup - 1) / Math.Max(1, attack.CooldownTicks);
            hits += shots * Math.Max(1, attack.Projectile.DamagePerHit);
        }
        return hits;
    }

    /// <summary>
    /// A fortress cannot capture. Rooting is only worth it once somebody else
    /// can stand on the scoring surface, or is declared to arrive before the
    /// windup and a short settling period are over.
    /// </summary>
    private static bool WorthRooting(
        ContractLens lens,
        GenericActorContext context,
        int windup)
    {
        if (MobileAllyCount(lens, context) > 0)
            return true;

        int horizon = windup + Math.Max(8, lens.RedeployPauseTicks * 2);
        foreach (GenericActorContext.ObservedUnitSlot slot in context.TeamUnits)
        {
            int due = slot.State switch
            {
                GenericActorContext.UnitSlotState.AvailabilityPending pending =>
                    pending.DueTick,
                GenericActorContext.UnitSlotState.AutomaticReturnPending returning
                    => returning.DueTick,
                GenericActorContext.UnitSlotState.LifecyclePending lifecycle =>
                    lifecycle.DueTick,
                _ => int.MaxValue,
            };
            if (due != int.MaxValue && due - context.Tick <= horizon)
                return true;
        }
        return false;
    }

    private static GenericActorDecision? TryAlign(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.Form? form,
        List<Gunnery.Target> targets,
        Position[] active,
        int activeIndex,
        bool stationed)
    {
        List<GenericActorActionLegality> rotations = lens.Available(
            context,
            GenericActorRulesContract.ActionKind.Rotation);
        if (rotations.Count == 0)
            return null;

        Direction? desired =
            Gunnery.AlignmentTurn(lens, context, form, targets)
            ?? (stationed ? IdleFacing(lens, context, active, activeIndex) : null);
        if (desired is not Direction direction || direction == context.Self.Facing)
            return null;

        foreach (GenericActorActionLegality rotation in rotations)
        {
            foreach (GenericActorActionLegality.ArgumentConstraint constraint
                     in rotation.Constraints)
            {
                if (constraint is GenericActorActionLegality.ArgumentConstraint
                        .DirectionConstraint directions
                    && directions.AllowedValues.Contains(direction))
                {
                    return new GenericActorDecision(
                        rotation.ActionId,
                        rotation.ActionCode,
                        [
                            new GenericActorActionArgument.DirectionArgument(
                                direction),
                        ],
                        $"laying the muzzle {direction}");
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Standing on station with nothing to shoot, a facing-locked gun is laid
    /// on the lane that matters: from the scoring surface, toward the direction
    /// the opponent must advance from; from overwatch, onto the surface itself.
    /// </summary>
    private static Direction? IdleFacing(
        ContractLens lens,
        GenericActorContext context,
        Position[] active,
        int activeIndex)
    {
        GenericActorRulesContract.AttackProfile? attack =
            lens.Attack(lens.Form(context.Self.FormId));
        if (attack is null || attack.OmnidirectionalAim || active.Length == 0)
            return null;

        Position[] ahead = Contains(active, context.Self.Position)
            ? lens.ObjectiveTiles(lens.NextObjectiveIndex(activeIndex))
            : active;
        Position focus = ArenaGeometry.Centroid(
            ahead.Length > 0 ? ahead : active);
        int dx = focus.X - context.Self.Position.X;
        int dy = focus.Y - context.Self.Position.Y;
        if (dx == 0 && dy == 0)
            return null;
        return Math.Abs(dx) >= Math.Abs(dy)
            ? dx >= 0 ? Direction.East : Direction.West
            : dy >= 0 ? Direction.South : Direction.North;
    }

    /// <summary>
    /// Stations, not a scrum. Allied bodies would otherwise all path onto the
    /// same scoring tiles, block each other every tick, and screen nothing, so
    /// each body takes a distinct post derived from the same frozen observation:
    /// the fortress takes a covering tile, the senior screen takes the surface,
    /// and the rest take ranked overwatch.
    /// </summary>
    private HashSet<Position> SelectGoals(
        ContractLens lens,
        GenericActorContext context,
        bool fortress,
        HashSet<Position> objective,
        ActorIdentity? fortressActor)
    {
        if (fortress && _sites.Count > 0)
        {
            var sites = new HashSet<Position>();
            foreach (Position site in _sites)
                sites.Add(site);
            return sites;
        }

        HashSet<Position> source =
            lens.FabricationSourceTiles(context.Self.FormId);
        if (source.Count > 0
            && ReadySlotExists(context)
            && !UrgentHold(lens, context, objective))
        {
            return source;
        }

        if (objective.Count == 0)
            return [];

        HashSet<Position> hot = FortressPlan.HotTiles(lens, context);
        int rank = ScreenRank(lens, context, fortressActor);
        if (rank > 0 && _overwatch.Count > 0)
        {
            Position post = _overwatch[Math.Min(rank - 1, _overwatch.Count - 1)];
            return [post];
        }

        // Prefer a scoring tile that is not already swept by an enemy gun; if
        // every tile is, take the surface anyway rather than concede it.
        var cool = new HashSet<Position>();
        foreach (Position tile in objective)
        {
            if (!hot.Contains(tile))
                cool.Add(tile);
        }
        return cool.Count > 0 ? cool : objective;
    }

    /// <summary>
    /// This body's index among the allied bodies that are currently screening,
    /// in canonical identity order. Rank zero holds the scoring surface.
    /// </summary>
    private static int ScreenRank(
        ContractLens lens,
        GenericActorContext context,
        ActorIdentity? fortressActor)
    {
        int rank = 0;
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (lens.IsStatic(ally.FormId)
                || ally.PendingSameLifeTransition is not null
                || ally.ActorId == fortressActor)
            {
                continue;
            }
            if (ally.ActorId.CompareTo(context.Self.ActorId) < 0)
                rank++;
        }
        return rank;
    }

    private static bool UrgentHold(
        ContractLens lens,
        GenericActorContext context,
        HashSet<Position> objective)
    {
        if (!objective.Contains(context.Self.Position))
            return false;
        if (context.Mode
            is not GenericActorContext.ModeObservationState.Frontline mode)
        {
            return false;
        }
        return mode.ClaimingTeamId == lens.TeamId && mode.CaptureProgress > 0;
    }

    private static bool ReadySlotExists(GenericActorContext context)
    {
        foreach (GenericActorContext.ObservedUnitSlot slot in context.TeamUnits)
        {
            if (slot.State is GenericActorContext.UnitSlotState.Ready)
                return true;
        }
        return false;
    }

    private GenericActorDecision? TryStep(
        ContractLens lens,
        GenericActorContext context,
        HashSet<Position> goals)
    {
        if (!TryMovement(lens, context, out GenericActorActionLegality? move,
                out GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint? directions))
        {
            return null;
        }

        HashSet<Position> blocked = OccupiedTiles(lens, context);
        Direction? step = ArenaGeometry.FirstStep(
            lens.Map,
            context.Self.Position,
            goals,
            blocked,
            new HashSet<Direction>(directions!.AllowedValues));
        if (step is not Direction direction)
            return null;

        return new GenericActorDecision(
            move!.ActionId,
            move.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(direction)],
            $"advancing {direction}");
    }

    private GenericActorDecision? TryDodge(
        ContractLens lens,
        GenericActorContext context,
        HashSet<Position> objective,
        HashSet<Position> goals,
        bool holding)
    {
        if (!TryMovement(lens, context, out GenericActorActionLegality? move,
                out GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint? directions))
        {
            return null;
        }

        HashSet<Position> blocked = OccupiedTiles(lens, context);
        HashSet<Position> hot = FortressPlan.HotTiles(lens, context);
        Position[] posts = [.. goals];
        Direction? best = null;
        int bestScore = Score(
            lens,
            context,
            context.Self.Position,
            objective,
            posts,
            hot,
            holding,
            standingStill: true);
        foreach (Direction direction in directions!.AllowedValues)
        {
            Position destination =
                ArenaGeometry.Step(context.Self.Position, direction);
            if (!ArenaGeometry.IsOpen(lens.Map, destination)
                || blocked.Contains(destination))
            {
                continue;
            }
            int score = Score(
                lens,
                context,
                destination,
                objective,
                posts,
                hot,
                holding,
                standingStill: false);
            if (score > bestScore)
            {
                bestScore = score;
                best = direction;
            }
        }
        if (best is not Direction chosen)
            return null;

        // Do not walk straight back into the tile the shot was aimed at.
        _dodgeOrigin = context.Self.Position;
        _dodgeThroughTick = context.Tick + 1;
        return new GenericActorDecision(
            move!.ActionId,
            move.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(chosen)],
            $"slipping the shot {chosen}");
    }

    /// <summary>
    /// Ranks a tile under fire. Time on the clock dominates, then having
    /// somewhere left to go — a tile inside a walled lane with two on-lane
    /// neighbours is a trap even when this tick is survivable — and only then
    /// the errand and the scoring surface.
    /// </summary>
    private static int Score(
        ContractLens lens,
        GenericActorContext context,
        Position tile,
        HashSet<Position> objective,
        Position[] posts,
        HashSet<Position> hot,
        bool holding,
        bool standingStill)
    {
        // Safety saturates: three ticks of clear air is as good as ten, so the
        // errand still decides between two survivable tiles. Evasion that also
        // abandons the scoring surface is how a duel is won and a match lost.
        int clock = TicksToImpact(lens, context, tile);
        int safety = Math.Min(clock, 3);
        int score = 400 * safety
            + 40 * Math.Min(Outs(lens, context, tile), 3);

        if (objective.Contains(tile))
            score += 200;
        else if (holding)
            score -= 60;
        if (!hot.Contains(tile))
            score += 20;
        score -= 20 * ArenaGeometry.NearestDistance(tile, posts);
        if (standingStill)
            score -= 5;   // break exact ties toward actually moving
        return score;
    }

    /// <summary>Open neighbours of a tile that no bolt is about to sweep.</summary>
    private static int Outs(
        ContractLens lens,
        GenericActorContext context,
        Position tile)
    {
        int outs = 0;
        foreach (Direction direction in ArenaGeometry.Cardinals)
        {
            Position neighbour = ArenaGeometry.Step(tile, direction);
            if (ArenaGeometry.IsOpen(lens.Map, neighbour)
                && TicksToImpact(lens, context, neighbour) > SafeHorizon)
            {
                outs++;
            }
        }
        return outs;
    }

    private static bool TryMovement(
        ContractLens lens,
        GenericActorContext context,
        out GenericActorActionLegality? move,
        out GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions)
    {
        move = null;
        directions = null;
        foreach (GenericActorActionLegality candidate in lens.Available(
                     context,
                     GenericActorRulesContract.ActionKind.Movement))
        {
            foreach (GenericActorActionLegality.ArgumentConstraint constraint
                     in candidate.Constraints)
            {
                if (constraint is GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint allowed)
                {
                    move = candidate;
                    directions = allowed;
                    return !allowed.AllowedValues.IsEmpty;
                }
            }
        }
        return false;
    }

    // ------------------------------------------------------------- awareness

    private void Observe(GenericActorContext context)
    {
        if (_lastHealth >= 0 && context.Self.Health < _lastHealth)
            _lastDamageTick = context.Tick;
        _lastHealth = context.Self.Health;

        // A tile can be individually legal and still never enterable — a
        // permanently reserved return spawn is the classic case. Counting
        // refusals turns "blocked again" into "stop routing through here",
        // and the periodic reset keeps a transient body from closing a lane
        // for the rest of the life.
        if (context.Tick - _refusalsClearedTick >= 50)
        {
            _refusals.Clear();
            _denied.Clear();
            _refusalsClearedTick = context.Tick;
        }
        if (context.Self.PreviousActionResolution
            is { Outcome: GenericActorActionResolution.ActionOutcome.Blocked }
                prior)
        {
            foreach (GenericActorActionArgument argument
                     in prior.AcceptedAction.Arguments)
            {
                if (argument
                    is not GenericActorActionArgument.DirectionArgument direction)
                {
                    continue;
                }
                Position refused = ArenaGeometry.Step(
                    context.Self.Position,
                    direction.Value);
                _blockedTile = refused;
                _blockedThroughTick = context.Tick;
                _refusals.TryGetValue(refused, out int count);
                _refusals[refused] = count + 1;
                if (count + 1 >= 3)
                    _denied.Add(refused);
            }
        }

        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
            _seen[enemy.ActorId.ToString()] = (enemy.Position, context.Tick);
    }

    private List<Gunnery.Target> BuildTargets(
        ContractLens lens,
        GenericActorContext context,
        Position[] active)
    {
        var targets = new List<Gunnery.Target>();
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            bool onSurface = Contains(active, enemy.Position);
            int near = active.Length == 0
                ? 99
                : ArenaGeometry.NearestDistance(enemy.Position, active);
            int weight = onSurface ? 6 : near <= 2 ? 4 : 2;
            if (lens.IsStatic(enemy.FormId))
                weight -= 3;
            if (enemy.PendingSameLifeTransition is not null)
                weight += 4;   // a visible windup is the cheapest kill available
            targets.Add(new Gunnery.Target(
                enemy.Position,
                Drift(enemy, context.Tick),
                enemy.Health,
                weight));
        }
        return targets;
    }

    private (int Dx, int Dy) Drift(
        GenericActorContext.ObservedEnemyState enemy,
        int tick)
    {
        if (!_seen.TryGetValue(enemy.ActorId.ToString(), out var previous)
            || previous.Tick != tick - 1)
        {
            return (0, 0);
        }
        return (
            Math.Clamp(enemy.Position.X - previous.Tile.X, -1, 1),
            Math.Clamp(enemy.Position.Y - previous.Tile.Y, -1, 1));
    }

    /// <summary>
    /// Ticks until the soonest hostile projectile occupies this tile, or
    /// <see cref="int.MaxValue"/> when none ever does. Counting the clock
    /// instead of testing a fixed radius is what stops a body walking two tiles
    /// deeper into a walled lane because the bolt was not "close enough" yet.
    /// </summary>
    private static int TicksToImpact(
        ContractLens lens,
        GenericActorContext context,
        Position tile)
    {
        int soonest = int.MaxValue;
        foreach (GenericActorContext.ObservedProjectile projectile
                 in context.VisibleProjectiles ?? [])
        {
            if (projectile.OwnerTeamId == lens.TeamId)
                continue;
            if (projectile.Position == tile)
                return 0;
            if (!ArenaGeometry.TryRay(
                    projectile.Position,
                    tile,
                    out ProjectileHeading heading,
                    out int distance)
                || heading != projectile.Heading)
            {
                continue;
            }
            if (projectile.RemainingTiles >= 0
                && distance > projectile.RemainingTiles)
            {
                continue;
            }
            if (!ArenaGeometry.ClearRay(
                    lens.Map,
                    projectile.Position,
                    tile,
                    true))
            {
                continue;
            }
            int perAdvance = Math.Max(1, projectile.TilesPerAdvance);
            int advances = (distance + perAdvance - 1) / perAdvance;
            int ticks = Math.Max(1, projectile.TicksUntilAdvance)
                + (advances - 1) * lens.FastestProjectileCadence;
            soonest = Math.Min(soonest, ticks);
        }
        return soonest;
    }

    private static bool Threatened(
        ContractLens lens,
        GenericActorContext context,
        Position tile) =>
        TicksToImpact(lens, context, tile) <= 2;

    private static bool HeardTrouble(GenericActorContext context, int window)
    {
        foreach (GenericActorContext.ObservedSound sound
                 in context.HeardSounds ?? [])
        {
            bool violent = sound.Kind
                is GenericActorContext.EventKind.Attack
                or GenericActorContext.EventKind.Damage
                or GenericActorContext.EventKind.Destruction;
            if (violent
                && sound.Distance <= 1
                && context.Tick - sound.SourceTick <= window)
            {
                return true;
            }
        }
        return false;
    }

    private static int MobileAllyCount(
        ContractLens lens,
        GenericActorContext context)
    {
        int count = 0;
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (!lens.IsStatic(ally.FormId))
                count++;
        }
        return count;
    }

    /// <summary>
    /// Which body holds the fortress role, decided identically by every allied
    /// life from the same frozen observation: a body that is already rooted or
    /// mid-windup keeps it, otherwise it goes to the tick-zero slot, otherwise
    /// to the lowest stable slot that has an anchor route at all.
    /// </summary>
    private static ActorIdentity? FortressActor(
        ContractLens lens,
        GenericActorContext context)
    {
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (lens.IsStatic(ally.FormId)
                || ally.PendingSameLifeTransition is not null)
            {
                return ally.ActorId;
            }
        }

        ActorIdentity? best = null;
        int bestRank = int.MaxValue;
        void Consider(ActorIdentity actor, string formId)
        {
            if (lens.AnchorRoute(formId) is null)
                return;
            int rank = actor.UnitId == lens.PrimeUnitId ? 0 : 1;
            if (best is null
                || rank < bestRank
                || rank == bestRank && actor.CompareTo(best) < 0)
            {
                best = actor;
                bestRank = rank;
            }
        }

        Consider(context.Self.ActorId, context.Self.FormId);
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
            Consider(ally.ActorId, ally.FormId);
        return best;
    }

    private void EnsurePlan(
        ContractLens lens,
        GenericActorContext context,
        int activeIndex)
    {
        GenericActorRulesContract.FormTransition? route =
            lens.AnchorRoute(context.Self.FormId);
        int reach = route is null
            ? lens.Reach(context.Self.FormId)
            : lens.Reach(route.TargetFormId);
        if (_planIndex == activeIndex && _planRange == reach)
            return;

        Position[] active = lens.ObjectiveTiles(activeIndex);
        bool strict = route is null
            || (lens.Attack(lens.Form(route.TargetFormId))
                ?.Projectile.DiagonalCornersMustBeClear ?? true);
        List<Position> ranked = FortressPlan.RankSites(
            lens,
            active,
            reach,
            strict,
            lens.HomeAnchor);
        _bestCoverage = ranked.Count == 0
            ? 0
            : FortressPlan.Coverage(lens.Map, ranked[0], active, reach, strict);

        // Only the top coverage tier is a fortress site. Walking to a tile that
        // sees half the surface and rooting there is how a fortress becomes
        // scenery; the body would rather keep walking to a real one.
        int floor = Math.Max(1, _bestCoverage - 1);
        _sites = [];
        foreach (Position site in ranked)
        {
            if (FortressPlan.Coverage(lens.Map, site, active, reach, strict)
                >= floor)
            {
                _sites.Add(site);
            }
        }
        _siteFloor = floor;

        // Screens do not transform, so their posts are not restricted to
        // transform-legal tiles; they only have to see the surface.
        int screenReach = lens.Reach(context.Self.FormId);
        _overwatch = FortressPlan.RankSites(
            lens,
            active,
            screenReach,
            strict,
            lens.HomeAnchor,
            transformableOnly: false);
        _planIndex = activeIndex;
        _planRange = reach;
    }

    private HashSet<Position> OccupiedTiles(
        ContractLens lens,
        GenericActorContext context)
    {
        var blocked = new HashSet<Position>();
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
            blocked.Add(ally.Position);
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
            blocked.Add(enemy.Position);
        foreach (GenericActorContext.ObservedProjectile projectile
                 in context.VisibleProjectiles ?? [])
        {
            if (projectile.OwnerTeamId != lens.TeamId)
                blocked.Add(projectile.Position);
        }
        if (_blockedTile is Position tile
            && context.Tick <= _blockedThroughTick + 1)
        {
            blocked.Add(tile);
        }
        if (_dodgeOrigin is Position vacated && context.Tick <= _dodgeThroughTick)
            blocked.Add(vacated);
        foreach (Position denied in _denied)
            blocked.Add(denied);
        return blocked;
    }

    // -------------------------------------------------------------- plumbing

    private static bool Contains(Position[] tiles, Position tile)
    {
        foreach (Position candidate in tiles)
        {
            if (candidate == tile)
                return true;
        }
        return false;
    }

    private static GenericActorDecision? BuildTransition(
        GenericActorContext context,
        GenericActorRulesContract.FormTransition route,
        string reason)
    {
        GenericActorActionLegality? action = context.Action(route.ActionId);
        if (action is null || !action.Available)
            return null;

        foreach (GenericActorActionLegality.ArgumentConstraint constraint
                 in action.Constraints)
        {
            if (constraint is not GenericActorActionLegality.ArgumentConstraint
                .FormTargetConstraint forms)
            {
                continue;
            }
            if (!forms.AllowedFormIds.Contains(route.TargetFormId))
                return null;
            return new GenericActorDecision(
                action.ActionId,
                action.ActionCode,
                [
                    new GenericActorActionArgument.FormTargetArgument(
                        route.TargetFormId),
                ],
                reason);
        }
        return action.Constraints.Length == 0
            ? GenericActorDecision.WithoutArguments(
                action.ActionId,
                action.ActionCode,
                reason)
            : null;
    }

    /// <summary>
    /// A guaranteed bounded, legal reply. It prefers a declared wait, then any
    /// available action whose arguments can be satisfied from its own mask.
    /// </summary>
    private GenericActorDecision SafeAction(
        GenericActorContext context,
        string reason)
    {
        GenericActorActionLegality? fallback = null;
        foreach (GenericActorActionLegality action in context.ActionLegalities)
        {
            if (!action.Available)
                continue;
            if (_lens?.KindOf(action.ActionId)
                    == GenericActorRulesContract.ActionKind.Wait
                && action.Constraints.Length == 0)
            {
                return GenericActorDecision.WithoutArguments(
                    action.ActionId,
                    action.ActionCode,
                    reason);
            }
            if (fallback is null && action.Constraints.Length == 0)
                fallback = action;
        }
        if (fallback is not null)
        {
            return GenericActorDecision.WithoutArguments(
                fallback.ActionId,
                fallback.ActionCode,
                reason);
        }

        foreach (GenericActorActionLegality action in context.ActionLegalities)
        {
            if (!action.Available)
                continue;
            List<GenericActorActionArgument>? arguments = Satisfy(action);
            if (arguments is not null)
            {
                return new GenericActorDecision(
                    action.ActionId,
                    action.ActionCode,
                    arguments,
                    reason);
            }
        }
        throw new InvalidOperationException("No available action was offered.");
    }

    private static List<GenericActorActionArgument>? Satisfy(
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
                case GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint headings:
                    if (headings.AllowedValues.IsEmpty)
                        return null;
                    arguments.Add(
                        new GenericActorActionArgument
                            .ProjectileHeadingArgument(
                                headings.AllowedValues[0]));
                    break;
                default:
                    break;   // shot programs are optional payloads
            }
        }
        return arguments;
    }
}
