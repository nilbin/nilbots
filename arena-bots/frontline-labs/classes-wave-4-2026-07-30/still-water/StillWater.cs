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
///
/// Patience is a price, and the contract sets it. On a mean-reverting
/// frontline, ground given up comes back and a late push still lands; where
/// the capture definition declares a hold, ground stops coming back and the
/// same push is worth a position or worth nothing depending on whose hold is
/// running. Where returns rally to the front, a body is a renewable resource
/// rather than a spent one and conceding tiles to save it is a bad trade.
/// Where surplus objective weight scales capture pressure, the point stops
/// being a switch one body flips and becomes a contest bodies win. All four
/// facts are read from the resolved contract, so one artifact plays every arm
/// and the baseline is what it plays when none of them is declared.
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

        /// <summary>
        /// A fan is worth more than the next single bolt, and the ground can
        /// spare the body for the windup. The stance is a one-shot: enter, aim
        /// by rotating, fire, and the rule takes the body back out.
        /// </summary>
        Cast,
    }

    private readonly QuarryTracker _tracker = new();

    /// <summary>
    /// Destinations a joint step already refused, kept for a few ticks so a
    /// contested tile is not re-attempted every tick. Availability is a
    /// per-body fact; only the resolution tells you the tile was contested.
    /// </summary>
    private readonly Dictionary<Position, int> _refused = [];
    private Doctrine? _doctrine;
    private Ratchet? _ratchet;
    private Position? _previousTile;

    /// <summary>
    /// Tick this life last requested a stance. A same-life form change preserves
    /// private memory, so this survives into the stance and back out — which is
    /// what lets the stance ladder notice it has been sitting in an immobile
    /// form with nothing to shoot.
    /// </summary>
    private int _castRequestedTick = int.MinValue;

    public void StartLife(GenericActorMatchStart start)
    {
        _doctrine = new Doctrine(start);
        _ratchet = new Ratchet(_doctrine);
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        if (_doctrine is not Doctrine doctrine || _ratchet is not Ratchet ratchet)
            return SafeFallback(context);

        var book = new ActionBook(doctrine.Contract, context);
        _tracker.Observe(context);

        Field field = doctrine.Field;
        var self = context.Self;
        var attack = doctrine.Attack(self.FormId);
        var threat = new ThreatField(field, doctrine, context, _tracker);

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

        // The hold clock is READ: owner and expiry are published on the mode
        // observation and travel together. The old four-signal reconstruction
        // survives only as a fallback for a half-populated pair.
        ratchet.Observe(context, mode);
        var presence = ArenaBasics.ObjectivePresence(doctrine.Contract, context);
        int selfWeight = doctrine.Form(self.FormId)?.ObjectiveWeight ?? 0;

        HashSet<Position> blocked = OccupiedTiles(context, self);
        RememberRefusals(context, self, blocked);

        int[] objectiveCost = field.CostField(
            activeTiles.Length > 0 ? activeTiles : [self.Position],
            null);

        // The forecast horizon has to cover the slowest thing this body can
        // commit to. A cast lands its fan a whole windup later than a bolt, so
        // a horizon sized for the mobile gun would score every cast against a
        // prediction that has already run out.
        StanceRoute? fan = doctrine.Stances.BestFan(
            self.FormId,
            doctrine.BoltsPerAttack(self.FormId));
        StanceRoute? worn = doctrine.Stances.Wearing(self.FormId);
        int maxSteps = attack is null
            ? 2
            : ForkPlanner.ImpactOffset(attack, attack.Projectile.MaxTravelTiles) + 1;
        if (fan?.Gun is { } fanGun)
        {
            maxSteps = Math.Max(
                maxSteps,
                ForkPlanner.ImpactOffset(fanGun, fanGun.Projectile.MaxTravelTiles)
                    + fan.EntryWindup
                    + 2);
        }
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

        // Wearing a stance is a different game: no feet, one shot, and a rule
        // that takes the body out the moment it fires. It gets its own ladder
        // rather than a special case inside the mobile one.
        if (worn is not null)
        {
            return StanceTick(
                doctrine, context, book, field, threat, forecasts, objective,
                presence, worn, order);
        }

        Posture posture = ChoosePosture(
            doctrine, ratchet, context, mode, forecasts, objective, self,
            coupling, field.Cost(objectiveCost, self.Position));

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
            _ => [Station(doctrine, activeIndex, standoff, objective, attack, fan)],
        };
        // Bodies and bolts are transient: they are gone long before a route
        // planned around them would have arrived, and treating them as walls
        // surrenders routes exactly when bodies are densest — which is the
        // state a contract that rallies arrivals onto one region produces on
        // purpose. Only walls block the field; the transient set is applied to
        // the one step actually taken.
        int[] goalCost = field.CostField(goals, null);

        // Some resolved contracts hand companions over automatically and some
        // require the Prime to ask. Both routes are read from the mask, never
        // assumed: a body worth more than one bolt is always worth the tick.
        if (!threat.ImmediateImpact(self.Position)
            && Fabricate(doctrine, book) is { } companion)
        {
            return companion;
        }

        Shot? plan = PlanShot(
            context, doctrine, _tracker, book, attack, forecasts, field, objective);

        // A bolt that traverses this tile during the coming resolution outranks
        // every other consideration: a body that survives keeps firing.
        bool lethalHere = threat.ImmediateImpact(self.Position)
            || threat.OccupiedByBolt(self.Position);

        List<Option> options = BuildOptions(
            doctrine, ratchet, context, book, field, threat, forecasts, goalCost,
            objective, presence, selfWeight, posture, attack, blocked,
            _previousTile, coupling, order);
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

        // The cast ledger. A fan is several simultaneous bolts on adjacent
        // headings, and its entry is a blind commitment: the windup is wait-only,
        // so the body cannot dodge, and lethal damage cancels the change. It is
        // priced against what the mobile gun would have delivered over the same
        // window, and refused whenever the feet have somewhere better to be.
        if (fan is not null
            && gain < 1.5
            && Cast(
                doctrine, ratchet, context, book, field, threat, forecasts,
                objective, presence, mode, posture, fan, plan, order)
                is { } cast)
        {
            return cast;
        }

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
        Ratchet ratchet,
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

        // Inside a hold this team owns, an opposing capture that COMPLETES
        // inside the window is spent: their claim resets, the objective does
        // not move, and the presence they spent is gone. Letting them buy that
        // is better than contesting it. What is not harmless is a claim that
        // will still be standing when the window drops — it converts the
        // instant it does — and neither is one the tick cap will read, because
        // the territorial formula counts residual progress no matter whose hold
        // is running. A hold protects the front from moving, not the scoreboard
        // from being counted.
        bool defenceIsSpent =
            ratchet.EnemyPushWouldBeSpent(context.Tick, progress)
            && !ledgerClosing;

        // The point is only lost while somebody else stands on it alone.
        // Breaking that is the one thing worth walking into a bad range for.
        int contestTrigger = lastDitch ? threshold / 8 : threshold / 5;
        if (ledgerClosing)
            contestTrigger = 1;
        if (enemyClaims
            && !defenceIsSpent
            && progress >= Math.Max(1, contestTrigger))
        {
            return Posture.Contest;
        }

        // The protected window is the cheapest attacking tempo this contract
        // ever offers: for as long as it runs the worst case of a failed push
        // is a body, because the ground behind cannot be taken back. Spend it
        // on the next position rather than on guarding ground that is already
        // guaranteed.
        if (defenceIsSpent && (self.Health >= 2 || ratchet.RallyForward))
            return Posture.Seize;

        // The mirror of that bargain. Inside a hold the opposition owns, a
        // capture completed by this team is spent, so the only thing a body on
        // the point can still buy is denial — the holder is free to advance
        // AGAIN, and nothing but presence stops it.
        //
        // Whether that denial is worth standing in the open for is a declared
        // control policy rather than a preference. Where surplus objective
        // weight scales capture pressure, every body on the point subtracts
        // from their rate, denial is quantitative, and holding the ground pays
        // for the exposure. Where control is binary, one body already nulls
        // them whenever the baseline decides a contest is on, and forcing the
        // issue merely walks into the gun: measured on this lineage's own
        // replays, forcing it cost about 265 ticks of defensive life per match
        // on the binary arm and bought about 235 on the weight-scaled one. So
        // the arm that pays gets the override, and the arm that does not keeps
        // the doctrine that was already tuned for it.
        if (ratchet.WeightScales
            && ratchet.PushWouldBeSpent(
                context.Tick, reach, weClaim ? progress : 0))
        {
            return Posture.Contest;
        }

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

        // Withdrawing is an investment in a body, and the contract prices that
        // body. Where automatic returns land at home the investment is sound —
        // dying spends the respawn clock and then a long walk back. Where the
        // declared placement rallies returns onto this team's own side of the
        // active objective, the walk is gone: the body reappears in position,
        // so conceding the ground that keeps it alive pays a real price for a
        // saving that is no longer real.
        if (doctrine.RallyForward)
            retreatIsFree = retreatWorthTheTurn = false;

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

    /// <summary>
    /// The cast decision. Everything it reads is contract or observation: the
    /// windup and its wait-only policy, the tile tags that refuse the route, the
    /// stance gun's bolt count and cadence, the return route's attack budget,
    /// and the capture policy that decides whether the ground can spare a body
    /// for two ticks.
    ///
    /// <para>Three facts make this a narrow decision rather than a free upgrade.
    /// The fan is straight by construction, so it cannot reach anything a bend
    /// could have curved onto — its worth is that it fires <em>n</em> bolts at
    /// the same tick on adjacent headings, which is the one thing a sequence of
    /// single bolts cannot do against a body that re-decides between them, and
    /// its outer rays are the exact diagonals a one-bend envelope can never
    /// reach at all. The entry is blind: two ticks of wait-only with lethal
    /// damage cancelling, so it is only affordable outside a ready gun's
    /// envelope. And the stance is refused outright on transition-forbidden
    /// tiles, which on this map includes every objective tile and the whole
    /// central lane — so a cast is fired from the shoulder, across the point,
    /// never from on it.</para>
    /// </summary>
    private GenericActorDecision? Cast(
        Doctrine doctrine,
        Ratchet ratchet,
        GenericActorContext context,
        ActionBook book,
        Field field,
        ThreatField threat,
        List<EnemyForecast> forecasts,
        HashSet<Position> objective,
        (int OwnWeight, int EnemyWeight, bool SelfPresent) presence,
        GenericActorContext.ModeObservationState.Frontline? mode,
        Posture posture,
        StanceRoute fan,
        Shot? mobile,
        Direction[] order)
    {
        var self = context.Self;
        if (fan.Gun is not { } gun || forecasts.Count == 0)
            return null;

        // Ground first. A body that is holding, taking, or breaking a claim is
        // not available for two ticks of immobility, whatever the fan is worth.
        if (posture is Posture.Contest or Posture.Withdraw
            || presence.SelfPresent
            || objective.Contains(self.Position))
        {
            return null;
        }
        // Under a decay clock that only runs against an enemy standing ALONE,
        // an unopposed enemy on the point is actively eroding this team's claim.
        // Answering that needs feet, not a fan.
        if (ratchet.SoleDecayOnly
            && presence.EnemyWeight > 0
            && presence.OwnWeight == 0)
        {
            return null;
        }

        // The route has to be legal here and now: the action offered this tick,
        // the target form among its legal values, and a tile whose tags do not
        // refuse a transition. The mask is authoritative; the tag check is the
        // reason a station is chosen where a cast is possible at all.
        var transform = book.ById(fan.Entry.ActionId);
        if (transform is null
            || !ActionBook.FormTargets(transform).Contains(fan.TargetFormId)
            || doctrine.TransitionForbidden(self.Position))
        {
            return null;
        }
        // Cooldown survives the transition, so a hot gun means the stance would
        // arrive unable to fire. The windup itself burns some of it.
        if (self.Cooldown > fan.EntryWindup)
            return null;

        // A blind windup is a commitment, so it is priced as one: nothing already
        // in flight may reach this tile while the body cannot dodge, no ready gun
        // may already be pointed at it, and the body must be able to afford one
        // hit it did not see coming. A body one bolt from death never casts.
        if (fan.BlindWindup
            && (self.Health <= 1
                || !threat.SafeStandingFor(
                    self.Position,
                    fan.EntryWindup + 1,
                    self.Health)
                || threat.AimedByReadyMuzzle(self.Position)))
        {
            return null;
        }

        CastPlan best = Volley.Score(
            field, self.Position, self.Facing, fan, forecasts, objective,
            fan.EntryWindup);
        if (fan.CanRotate)
        {
            foreach (Direction direction in order)
            {
                if (direction == self.Facing)
                    continue;
                CastPlan turned = Volley.Score(
                    field, self.Position, direction, fan, forecasts, objective,
                    fan.EntryWindup + 1);
                // A turn inside the stance costs a tick, so it has to be worth
                // more than the facing already worn.
                if (turned.Value * 0.85 > best.Value)
                    best = turned;
            }
        }
        if (best.AnchoredRays <= 0)
            return null;

        double value = best.Value;
        // Sealing the front rank of a contested point is the fan's real job on
        // this contract: an opponent has to stand on those tiles to take them,
        // and three bolts deny a tile each instead of betting one bolt on one
        // guess. It is credited only while the point is actually contested.
        bool contested = presence.EnemyWeight > 0
            || (mode?.ClaimingTeamId is int claimer && claimer != doctrine.TeamId);
        if (contested)
            value += 0.8 * best.CoveredObjectiveTiles;

        // What a fan is FOR. The bolts are simultaneous and straight, so a fan
        // cannot reach anything a bend could have curved onto and it never
        // concentrates damage — each bolt spends its one damage on its own tile.
        // Its whole edge is breadth at a single arrival tick, which buys exactly
        // two things a cheaper single bolt cannot: two separate bodies answered
        // by one decision, or the front rank of a contested point denied a tile
        // at a time. Without one of those, three ticks of immobility and a long
        // cooldown are worse than the next ordinary bolt, and the ledger says so.
        bool worthTheStance = best.DistinctBodies >= 2
            || (contested
                && best.DistinctBodies >= 1
                && best.CoveredObjectiveTiles >= 2);
        if (!worthTheStance)
            return null;

        // Even then it has to beat the bolt it is displacing. The window is the
        // contract's — windup, the firing tick, the exit windup, and the cooldown
        // the stance gun leaves behind — but a body inside a fan cannot dodge
        // three ways at once, so the comparison is against the next bolt rather
        // than against every bolt of the window.
        double bar = Math.Max(1.15 * (mobile?.Value ?? 0), gun.ProjectilesPerAttack);
        if (value < bar)
            return null;

        _castRequestedTick = context.Tick;
        return book.FormTargeted(
            transform,
            fan.TargetFormId,
            $"Cast {fan.TargetFormId} fan {value:0.0} over {bar:0.0} "
                + $"bodies {best.DistinctBodies} seal {best.CoveredObjectiveTiles}");
    }

    /// <summary>
    /// Wearing the stance. There are no feet here: the form declares no movement
    /// action, so the only decisions are the facing, the shot, and leaving. The
    /// engine takes the body out on the declared attack budget, so firing is the
    /// exit — and sitting is pure loss, which is why an unaimed stance leaves of
    /// its own accord rather than waiting to be shot.
    /// </summary>
    private GenericActorDecision StanceTick(
        Doctrine doctrine,
        GenericActorContext context,
        ActionBook book,
        Field field,
        ThreatField threat,
        List<EnemyForecast> forecasts,
        HashSet<Position> objective,
        (int OwnWeight, int EnemyWeight, bool SelfPresent) presence,
        StanceRoute worn,
        Direction[] order)
    {
        var self = context.Self;
        // A windup still running offers nothing but wait; there is no decision.
        if (self.PendingSameLifeTransition is not null)
            return book.Fallback($"stance windup at {self.Position}");

        var exit = worn.Exit is null ? null : book.ById(worn.Exit.ActionId);
        var rotate = book.Rotate;
        var shoot = book.Attacks.Count > 0 ? book.Attacks[0] : null;

        CastPlan now = Volley.Score(
            field, self.Position, self.Facing, worn, forecasts, objective, 0);
        bool contested = presence.EnemyWeight > 0;
        double nowValue = now.Value + (contested ? 0.8 * now.CoveredObjectiveTiles : 0);

        // Fire when the fan arrives on something a prediction names. The entry is
        // already spent, so the bar here is lower than the bar to enter: a stance
        // that waits for the perfect fan is a stance that gets shot in an immobile
        // form. The rule then returns the body itself — one entry, one cast.
        if (shoot is not null
            && self.Cooldown == 0
            && now.AnchoredRays > 0
            && nowValue >= 1.0)
        {
            return book.Parameterless(
                shoot,
                $"fan {worn.BoltsPerAttack} bolts {self.Facing} value {nowValue:0.0}");
        }

        // Aim by rotating — the stance's only remaining verb besides the gun.
        if (rotate is not null && worn.CanRotate)
        {
            Direction? turn = null;
            double bestValue = nowValue;
            var legal = ActionBook.Directions(rotate);
            foreach (Direction direction in order)
            {
                if (direction == self.Facing || !legal.Contains(direction))
                    continue;
                CastPlan plan = Volley.Score(
                    field, self.Position, direction, worn, forecasts, objective, 1);
                double candidate = plan.Value
                    + (contested ? 0.8 * plan.CoveredObjectiveTiles : 0);
                if (plan.AnchoredRays > 0 && candidate > bestValue + 0.5)
                {
                    bestValue = candidate;
                    turn = direction;
                }
            }
            if (turn is Direction aim)
            {
                return book.Directional(
                    rotate,
                    aim,
                    $"stance aim {aim} value {bestValue:0.0}");
            }
        }

        // Leaving early is an ordinary decision, and the two reasons to take it
        // are both about being unable to move: something is coming that cannot
        // be dodged from here, or the fan has nothing to arrive on and the tick
        // is being spent standing in the open.
        bool trapped = !threat.SafeStandingFor(
            self.Position,
            worn.ExitWindup + 1,
            self.Health);
        bool idle = now.AnchoredRays == 0
            && context.Tick - _castRequestedTick > worn.EntryWindup + 2;
        if (exit is not null && (trapped || idle))
        {
            return book.Parameterless(
                exit,
                trapped
                    ? $"leaving the stance: bolt inbound at {self.Position}"
                    : $"leaving the stance: nothing to fire on at {self.Position}");
        }
        return book.Fallback($"stance aimed {self.Facing} at {self.Position}");
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
        GenericActorRulesContract.AttackProfile? attack,
        StanceRoute? fan)
    {
        Field field = doctrine.Field;
        Position edge = doctrine.NearEdge(activeIndex);
        int edgeDepth = doctrine.Project(edge);
        Position wanted = edge.Offset(
            (-doctrine.Forward.Dx * standoff)
                + (doctrine.Lateral.Dx * doctrine.LateralBias),
            (-doctrine.Forward.Dy * standoff)
                + (doctrine.Lateral.Dy * doctrine.LateralBias));
        // Where a fan exists, the station has to be a tile the fan can be raised
        // on at all — and on this map that is never the central lane, because the
        // whole of it is tagged transition-forbidden. So the ideal tile is only
        // taken directly when it also permits the route; otherwise the search
        // below prices the shoulder tiles that do.
        if (field.IsOpen(wanted)
            && !objective.Contains(wanted)
            && (fan is null || !doctrine.TransitionForbidden(wanted)))
        {
            return wanted;
        }

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

                // A tile the fan can be raised on, from which the fan crosses the
                // point, is worth holding: it is the only kind of tile from which
                // the objective can be sealed by more than one bolt at a time.
                if (fan is not null && !doctrine.TransitionForbidden(tile))
                {
                    score += 1.2;
                    int sealing = 0;
                    foreach (Direction facing in Field.Cardinals)
                    {
                        int crossings = 0;
                        foreach (var ray in Volley.Rays(field, tile, facing, fan))
                        {
                            foreach ((Position swept, int _, int _) in ray)
                            {
                                if (objective.Contains(swept))
                                {
                                    crossings++;
                                    break;
                                }
                            }
                        }
                        sealing = Math.Max(sealing, crossings);
                    }
                    score += 1.1 * sealing;
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
        Ratchet ratchet,
        GenericActorContext context,
        ActionBook book,
        Field field,
        ThreatField threat,
        List<EnemyForecast> forecasts,
        int[] goalCost,
        HashSet<Position> objective,
        (int OwnWeight, int EnemyWeight, bool SelfPresent) presence,
        int selfWeight,
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

        // A body that returns to the front is cheap to replace POSITIONALLY,
        // and it is tempting to conclude it is cheap to spend. It is not.
        // Capture wants uninterrupted sole presence, and a death interrupts it
        // no matter where the replacement appears — the return clock is the
        // same 18 ticks either way. Discounting danger on a rallying contract
        // was measured on this lineage's own replays at +193 ticks to breach as
        // the attacker; the discount is therefore deliberately not taken.
        double bandWeight = posture == Posture.Deny ? 1.1 : 0.0;

        // Weight already standing on the point that is not mine. Under a
        // weight-scaled control policy that is help; under a binary one it is
        // a body doing nothing a body is not already doing.
        int alliedOnPoint =
            presence.OwnWeight - (presence.SelfPresent ? selfWeight : 0);

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

            // Two bodies inside one enemy cone are two bodies one decision
            // answers — one fan, or one guarded quadrant, or one aimed lane.
            // Bearings are cheap to vary and expensive to share.
            //
            // This is the single most valuable rule in the revision and it is
            // also the most delicately tuned: measured against the rebuilt
            // predecessor over three independent five-seed sets, a weight of 1.5
            // wins 60 of 60 matches at about +20 mean territory, a weight of 2.0
            // or more saturates at about +13, and 1.2 or less is worse than not
            // having the rule at all. The response is NOT monotonic below 1.5,
            // which is disclosed rather than smoothed over: the coefficient
            // decides who wins the mid-game scramble on a nearly deterministic
            // board, so it is a tuning risk, not a law of the game.
            foreach (var ally in context.Allies)
            {
                if (threat.SharedCone(tile, ally.Position))
                    score -= 1.5;
            }

            // What a body on the point is worth is a declared control policy,
            // not a habit. Where surplus weight scales capture pressure, the
            // point is a contest and every additional weight on it is pressure;
            // where control is binary, the first body nulls the opposition and
            // the second one has merely stopped covering the approach.
            if (committed && objective.Contains(tile))
            {
                score += ratchet.WeightScales
                    ? 1.4 * selfWeight
                    : alliedOnPoint > 0 ? -0.8 : 0.6;
            }
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
        var rotate = book.Rotate;
        bool locked = coupling
            == GenericActorRulesContract.MovementFacingCoupling.FacingLocked;
        var legalSteps = ActionBook.Directions(move);
        var legalTurns = ActionBook.Directions(rotate);

        // Candidate steps are enumerated from map geometry, never seeded from
        // the movement mask. The mask answers "what is legal this tick", and
        // under a facing-locked arm that is exactly one direction — a search
        // seeded from it can only ever continue in a straight line, so the bot
        // stops turning rather than turning slowly. Equal-scoring steps are
        // broken by the contract's own front axis with the residual tie
        // randomised per life; an absolute compass order here would be a
        // systematic side bias on a mirror map.
        foreach (Direction direction in order)
        {
            (int dx, int dy) = direction.Vector();
            Position destination = self.Position.Offset(dx, dy);
            if (field.IsWall(destination) || blocked.Contains(destination))
                continue;
            Direction after = FacingAfter(direction);
            bool survives = Survives(destination, after);
            double value = Evaluate(destination, after, isStay: false, survives);

            if (move is not null && legalSteps.Contains(direction))
            {
                options.Add(
                    new Option(
                        book.Directional(
                            move,
                            direction,
                            $"{posture} step {direction}"),
                        value,
                        IsStay: false,
                        destination,
                        IsMove: true,
                        survives));
                continue;
            }

            if (rotate is null
                || direction == self.Facing
                || !legalTurns.Contains(direction))
            {
                continue;
            }

            // Open ground the mask refuses only because the chassis points
            // somewhere else. A turn does not move the body, so it is scored
            // where the body actually stands, wearing the facing it will have;
            // what the turn buys is the route the lane unlocks, worth exactly
            // the goal-cost it removes. Charging it a whole tick of route
            // instead would cancel that gain against itself and leave the
            // chassis standing still forever, which is the failure mode a
            // facing-locked arm punishes hardest.
            int laneGain = hereCost == int.MaxValue
                    || field.Cost(goalCost, destination) == int.MaxValue
                ? 0
                : Math.Max(0, hereCost - field.Cost(goalCost, destination));
            bool standSurvives = Survives(self.Position, direction);
            double steer =
                Evaluate(self.Position, direction, isStay: false, standSurvives)
                + (goalWeight * laneGain)
                - 0.25;
            options.Add(
                new Option(
                    book.Directional(
                        rotate,
                        direction,
                        $"{posture} turn {direction} to unlock the step"),
                    steer,
                    IsStay: false,
                    self.Position,
                    IsMove: false,
                    staySurvives));
        }

        if (rotate is not null && (attack is not null || locked))
        {
            int coveredNow = attack is null
                ? 0
                : CoveredBodies(
                    field, attack, self.Position, self.Facing, forecasts);
            foreach (Direction direction in order)
            {
                if (direction == self.Facing || !legalTurns.Contains(direction))
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
                // wheel as well as the gun. Route value is already priced by
                // the steering options above; what only this clause can say is
                // that the turn is the sole legal way off a tile a bolt is
                // about to reach, which the escape search cannot see because a
                // rotation does not move the body.
                if (locked && !staySurvives)
                {
                    (int dx, int dy) = direction.Vector();
                    Position lane = self.Position.Offset(dx, dy);
                    if (!field.IsWall(lane)
                        && !blocked.Contains(lane)
                        && Survives(lane, direction))
                    {
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
    ///
    /// <para>Two contract facts change what a good trajectory is. A body inside
    /// a wait-only windup cannot dodge and its transition is cancelled by lethal
    /// damage, so it is the cheapest target on the board and worth aiming at
    /// even when a livelier one is closer. And a form that declares a projectile
    /// guard <em>returns</em> a bolt that arrives inside its facing quadrant —
    /// team-flipped, along the exactly reversed heading, from its own tile — so
    /// poking one head-on is shooting yourself. The arrival heading is what the
    /// guard reads, not the shooter's tile, and a bend changes the arrival
    /// heading: this is the one place where a curve is not a tie-break but the
    /// whole shot. The guard's arc never tracks, so going around it always
    /// works, and the alternative to going around is deliberately feeding it the
    /// deflection that breaks it.</para>
    /// </summary>
    private static Shot? PlanShot(
        GenericActorContext context,
        Doctrine doctrine,
        QuarryTracker tracker,
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
            double aimedBonus = 0;
            Position aimed = context.Self.Position;
            Position previous = context.Self.Position;
            for (int index = 0; index < plan.Swept.Count; index++)
            {
                (Position tile, int _, int offset) = plan.Swept[index];
                int steps = offset + 1;
                double local = 0;
                double localBonus = 0;
                bool localAnchored = false;
                foreach (EnemyForecast forecast in forecasts)
                {
                    int weight = forecast.Weight(tile, steps);
                    if (weight <= 0)
                        continue;
                    double scaled = weight * forecast.Pressure(damage);
                    double bonus = 0;
                    if (forecast.State.Position == tile)
                    {
                        // A blind windup is a free hit and, where the contract
                        // says lethal damage cancels, it also undoes whatever
                        // that body was about to become.
                        if (forecast.WaitOnly)
                            bonus += 2.5;
                        if (forecast.Guarded)
                        {
                            bonus += GuardAdjustment(
                                doctrine,
                                tracker,
                                forecast,
                                previous,
                                tile,
                                damage);
                        }
                    }
                    if (scaled + bonus > local + localBonus)
                    {
                        local = scaled;
                        localBonus = bonus;
                        localAnchored = forecast.IsAnchored(tile, steps);
                    }
                }
                if (local <= 0)
                {
                    previous = tile;
                    continue;
                }
                touched++;
                if (local + localBonus > peak + aimedBonus)
                {
                    peak = local;
                    aimedBonus = localBonus;
                    aimed = tile;
                    aimedIndex = index;
                    anchored = localAnchored;
                }
                previous = tile;
            }
            if (peak <= 0)
                continue;

            double value = peak + aimedBonus + (0.25 * Math.Max(0, touched - 1));
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
            if (value <= 0)
                continue;

            if (best is null || value > best.Value.Value)
                best = new Shot(Build(legality, plan, aimed), value, anchored);
        }
        return best;
    }

    /// <summary>
    /// What firing into a guarded body is worth beyond the damage. Negative when
    /// the bolt would arrive inside the guarded quadrant and come straight back;
    /// positive when this contact is the one that spends the guard's declared
    /// deflection budget, because the break is a forced return with an exit and
    /// a fresh entry windup to punish.
    /// </summary>
    private static double GuardAdjustment(
        Doctrine doctrine,
        QuarryTracker tracker,
        EnemyForecast forecast,
        Position from,
        Position at,
        int damage)
    {
        if (!Deflects(from, at, forecast.State.Facing))
            return 0.6;

        int? budget = doctrine.Stances.Wearing(forecast.State.FormId)
            ?.DeflectionBudget;
        int seen = tracker.Get(forecast.State.ActorId)?.Deflections ?? 0;
        if (budget is int limit && seen + 1 >= limit)
        {
            // The third bolt shatters the shield into a forced return. Paying
            // one returned bolt to open that window is the trade the mechanic
            // is built around.
            return 1.5;
        }
        // Otherwise this bolt dies on the arc and a new one, owned by them,
        // starts on their tile heading exactly back down this lane.
        return -4.0 * damage;
    }

    /// <summary>
    /// Whether a bolt travelling from <paramref name="from"/> onto
    /// <paramref name="at"/> arrives inside the facing quadrant of a body
    /// standing there. The approach bearing is the reverse of the arrival
    /// heading, and a facing quadrant is the facing heading plus its two
    /// neighbours — which is why a flank or a rear contact damages normally and
    /// a bend that swings the last tile sideways is not a head-on poke.
    /// </summary>
    private static bool Deflects(Position from, Position at, Direction facing)
    {
        int dx = Math.Sign(at.X - from.X);
        int dy = Math.Sign(at.Y - from.Y);
        if (dx == 0 && dy == 0)
            return false;
        ProjectileHeading arrival = (dx, dy) switch
        {
            (0, -1) => ProjectileHeading.North,
            (1, -1) => ProjectileHeading.NorthEast,
            (1, 0) => ProjectileHeading.East,
            (1, 1) => ProjectileHeading.SouthEast,
            (0, 1) => ProjectileHeading.South,
            (-1, 1) => ProjectileHeading.SouthWest,
            (-1, 0) => ProjectileHeading.West,
            _ => ProjectileHeading.NorthWest,
        };
        int approach = ((int)arrival + 4) % 8;
        int guarded = (int)facing.ToProjectileHeading();
        int delta = Math.Abs(approach - guarded);
        return Math.Min(delta, 8 - delta) <= 1;
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
