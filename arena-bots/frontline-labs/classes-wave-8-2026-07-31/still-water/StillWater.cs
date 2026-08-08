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
    /// The channel's private history: which allied bodies held their tile, and
    /// where the current controller's run began. Both are life-scoped, so a
    /// fresh body starts blank and degrades to the conservative reading.
    /// </summary>
    private readonly ChannelMemory _channel = new();

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
        Direction[] order = ArenaBasics.OrderedDirections(
            doctrine.Contract,
            context,
            ChannelRules.TeamTieBreak ? context.TeamRandom : context.Random);

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

        // The channel and the economy. Both are read from declared fields that
        // are inert-omitted on a ruleset without the arm, so both objects
        // answer "nothing here" for a match that does not carry the mechanic
        // and no decision below has to know which cell it is playing.
        _channel.Observe(context, mode);
        var channel = new ChannelState(
            doctrine, context, _channel, _tracker, objective, mode);
        var economy = new Economy(doctrine, context, mode);
        HashSet<Position> bankTiles = economy.BankTiles();

        // Drawn before any branch on private state, because agreement holds
        // only while every teammate draws the same values in the same order
        // within a tick. The lane assignment is a shared plan; a private draw
        // would give each body its own lane and no escort for any of them.
        int laneDraw = context.TeamRandom.NextInt(0, 1 << 20);

        HashSet<Position> blocked = OccupiedTiles(context, self);
        RememberRefusals(context, self, blocked);

        int[] objectiveCost = field.CostField(
            activeTiles.Length > 0 ? activeTiles : [self.Position],
            null);

        // The coordination layer. Every rule below is a function of the frozen
        // team-perception union alone, so each of my bodies derives the same
        // right-of-way convention without a channel to agree over — which is the
        // only kind of agreement this contract permits, since a life never sees
        // an ally's current action and starts every new body with empty memory.
        var convoy = new Convoy(field, doctrine, context, objectiveCost, activeTiles);

        // The forecast horizon has to cover the slowest thing this body can
        // commit to. A cast lands its fan a whole windup later than a bolt, so
        // a horizon sized for the mobile gun would score every cast against a
        // prediction that has already run out.
        StanceRoute? fan = doctrine.Stances.BestFan(
            self.FormId,
            doctrine.BoltsPerAttack(self.FormId));
        StanceRoute? worn = doctrine.Stances.Wearing(self.FormId);

        // The entry clock. Slot-scoped, so it outlives this body and no life can
        // reconstruct it from its own history; published, so none has to.
        EntryClock entryClock = fan is null
            ? EntryClock.Open
            : EntryClock.ForSelf(context, fan);
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
            _ =>
            [
                Station(
                    doctrine, activeIndex, standoff, objective, attack, fan,
                    convoy),
            ],
        };

        // THE LANE. One body runs the deposits and the rest hold the front, and
        // which body that is has to be a decision the whole team makes the same
        // way, because two couriers is a forfeited front and none is a
        // forfeited economy. There is no channel to agree over — only the
        // frozen observation and the shared random stream, and this uses both.
        Position[]? lane = Courier(
            doctrine, context, economy, channel, bankTiles, laneDraw, posture);
        if (lane is { Length: > 0 })
            goals = lane;
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
            context, doctrine, _tracker, book, attack, forecasts, field, objective,
            channel);

        // A bolt that traverses this tile during the coming resolution outranks
        // every other consideration: a body that survives keeps firing.
        bool lethalHere = threat.ImmediateImpact(self.Position)
            || threat.OccupiedByBolt(self.Position);

        List<Option> options = BuildOptions(
            doctrine, ratchet, context, book, field, threat, forecasts, goalCost,
            objective, presence, selfWeight, posture, attack, blocked,
            _previousTile, coupling, order, convoy, channel, economy, bankTiles);
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
        // headings. How much the feet have to be worth before they outrank it is
        // a function of how long the cast keeps them still, and that is declared:
        // the entry windup plus the exit windup. Wave 6's constant encoded a
        // three-tick cast; on a one-tick entry the same rule lets the feet win
        // far too often.
        double castIdle = fan is null ? 0 : fan.EntryWindup + fan.ExitWindup;
        double feetBar = fan is null || !Salvo.CheapEntry
            ? 1.5
            : Math.Max(1.0, 3.5 - (0.75 * castIdle));
        if (fan is not null
            && gain < feetBar
            && Cast(
                doctrine, ratchet, context, book, field, threat, forecasts,
                objective, presence, mode, posture, fan, plan, order, convoy,
                entryClock, channel)
                is { } cast)
        {
            return cast;
        }

        // THE STORE. A purchase costs this body its action for the tick, which
        // is the same price fabrication pays, so it is spent on a tick the
        // ladder was going to spend on nothing: no bolt worth firing, no step
        // worth taking. It does NOT cost stillness — the tile does not change —
        // so a body channelling a point can buy while it channels, which is the
        // cheapest tick in the match to spend on it.
        if (Invest(economy, book, gain, plan) is { } purchase)
            return purchase;

        // Tempo rule: spend the tick on the gun when the trajectory actually
        // arrives on a predicted body, or when walking would not have bought
        // anything anyway. Speculative sweep never outranks repositioning.
        // A bolt is a commitment, not a guess. Still Water only fires when the
        // trajectory arrives on a tile some prediction actually names; a curve
        // that a wall or a strict corner will eat is a wasted tempo beat, and
        // tempo is the whole point of standing off.
        //
        // RULE 5 — AND THE GUN IS HELD FOR A REOPENING STANCE. The entry is
        // refused while the attack cooldown exceeds the entry windup, so a bolt
        // fired on the wrong tick shuts a stance window the published clock says
        // is about to open. A speculative bolt costs nothing to skip; a
        // prediction worth firing on is worth the window.
        // RULE C2, AS A THRESHOLD. A bolt whose contact reverts the controlling
        // enemy's live run is not a better GUESS; it is the only verb this
        // doctrine owns that moves the objective number without standing on
        // the tile, and what it competes against is one step of route. The soft
        // bonus in the shot ladder cannot express that, because the ladder is
        // denominated in prediction weight and one point of progress will never
        // clear a bar written in those units however much the point is worth.
        bool revertsTheRun = ChannelRules.Interrupt
            && plan is { Anchored: true, Reverts: true };

        bool holdForStance = Salvo.ClockTempo
            && fan is not null
            && forecasts.Count > 0
            && !revertsTheRun
            && entryClock.TicksUntilOpen(context.Tick) <= 1
            && plan is { } candidateShot
            && WouldCloseTheStance(doctrine, self.FormId, fan)
            && candidateShot.Value < 2.5;
        if (!holdForStance
            && plan is { Anchored: true } shot
            && (shot.Value >= 2.5 || gain < 0.75 || revertsTheRun))
        {
            return shot.Decision;
        }

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

    /// <param name="Reverts">
    /// Whether this trajectory's aimed tile is one where a landing bolt reverts
    /// the controlling enemy's run. That is a THRESHOLD, not a quantity: the
    /// bolt either takes its damage class off their claim or it does not, and
    /// the doctrine's whole account of what it is doing on this arm is that
    /// such a bolt outranks the tile it could have walked to instead.
    /// </param>
    private readonly record struct Shot(
        GenericActorDecision Decision,
        double Value,
        bool Anchored,
        bool Reverts);

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
    /// Tiles a four-way chassis walks between two tiles ignoring walls — the
    /// lower bound every schedule question needs. The movement profile grants
    /// one tile per tick on the cardinals, so this is ticks as well as tiles,
    /// and a facing-locked arm only ever makes it longer.
    /// </summary>
    private static int Walk(Position from, Position to) =>
        Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y);

    /// <summary>
    /// RULE E1 — THE STORE, PLAYED FROM THE MASK. The legality names every track
    /// whose next tier the standing bank covers and no cap forbids, so the bot
    /// never prices the ladder: it ranks what it is offered by what the declared
    /// effect would close against the two published catalogs, and spends an
    /// otherwise idle tick. Absent action, absent store, or an economy whose
    /// declared purchase mode is not a verb all return null and cost nothing.
    /// </summary>
    private static GenericActorDecision? Invest(
        Economy economy,
        ActionBook book,
        double gain,
        Shot? plan)
    {
        if (!LedgerRules.Invest || !economy.StoreIsAVerb)
            return null;
        // The tick has to be genuinely idle. A step that buys route, or a bolt
        // that arrives on a named body, is worth more than a tier that will
        // still be affordable next tick — the bank does not expire.
        if (gain >= 0.75 || plan is { Anchored: true, Value: >= 2.5 })
            return null;

        var legality = book.First(
            GenericActorRulesContract.ActionKind.ModeInvestment);
        if (legality is null)
            return null;
        if (economy.BestTrack(legality) is not string track)
            return null;
        return new GenericActorDecision(
            legality.ActionId,
            legality.ActionCode,
            [new GenericActorActionArgument.UpgradeTrackArgument(track)],
            $"investing {track} from bank {economy.Bank}");
    }

    /// <summary>
    /// RULE E3 — THE LANE. Goals for the one body the team has agreed runs the
    /// deposits, or null for everyone else and for every match without an
    /// economy.
    ///
    /// <para>Three facts make this a team decision rather than a private one.
    /// The supply is a fixed pot, so a second courier buys nothing and costs a
    /// body. A courier is a body the front does not have, and on a channel
    /// ruleset the front feels that immediately. And there is no channel to
    /// agree over — only the frozen observation every teammate receives, and
    /// the shared random stream. So WHO goes is the last unit rank, which every
    /// body can compute from the topology, and WHICH lane is a draw from
    /// <c>TeamRandom</c>, which every body reproduces exactly and the opponent
    /// cannot predict.</para>
    /// </summary>
    private static Position[]? Courier(
        Doctrine doctrine,
        GenericActorContext context,
        Economy economy,
        ChannelState channel,
        HashSet<Position> bankTiles,
        int laneDraw,
        Posture posture)
    {
        if (!LedgerRules.Courier || !economy.Declared)
            return null;
        // Whether a body can be spared at all is a declared number, not a
        // preference. See ChannelState.MarginalBodyIsFree.
        if (!channel.MarginalBodyIsFree)
            return null;
        // The front comes first: a standing enemy claim is a fire this body is
        // needed on, whatever the metronome says.
        if (channel.EnemyClaimStands || posture is Posture.Contest)
            return null;
        // WHO GOES. The rearmost body that is actually ALIVE, and only while
        // the team can spare one — which every teammate computes identically
        // from the same frozen observation, so exactly one body ever answers
        // yes and no body has to be told. Slot rank alone would nominate a slot
        // that has not unlocked yet and leave the whole cadence unworked; the
        // live set is what the front actually has.
        var self = context.Self;
        int live = 1;
        int rearmost = self.ActorId.UnitId;
        foreach (var ally in context.Allies)
        {
            live++;
            rearmost = Math.Max(rearmost, ally.ActorId.UnitId);
        }
        if (rearmost != self.ActorId.UnitId)
            return null;
        // A team of one normally keeps its only body on the front. It may still
        // go when the front is genuinely quiet — nothing claimed, nothing
        // visible, nothing standing on the point — because the deposits are on
        // a metronome and a quiet front is the only time the walk is free. That
        // relaxation is what makes the arm playable at all in a mirror, where
        // both primes are dead on the first deposit tick and the strict
        // two-body gate leaves every scheduled deposit standing until it
        // expires. It is affordable ONLY under the surplus cap tested above:
        // ungated, it was measured losing the fabricator cell outright.
        bool quiet = !channel.EnemyClaimStands
            && context.Enemies.Length == 0
            && channel.EnemyTotal == 0;
        if (live < 2 && !quiet)
            return null;
        // Carrying: the load is only worth what it banks, and the walk was the
        // price. Nothing else the body can do with it.
        if (self.CarriedScrap > 0 && bankTiles.Count > 0)
        {
            bool full = self.CarriedScrap >= economy.CarryCapacity;
            bool nothingLeft = economy.Piles.Count == 0;
            if (full || nothingLeft)
                return bankTiles.ToArray();
        }

        // A pile that is actually standing outranks a deposit that is merely
        // due, and among standing piles the biggest one that is still there
        // when this body could arrive.
        //
        Position? target = null;
        double bestValue = 0;
        foreach (var pile in economy.Piles)
        {
            int walk = Walk(pile.Position, self.Position);
            if (context.Tick + walk >= pile.ExpiresAtTick)
                continue;
            double value = pile.Amount - (0.08 * walk);
            if (target is null || value > bestValue)
            {
                target = pile.Position;
                bestValue = value;
            }
        }
        if (target is Position standing)
            return [standing];

        // Nothing standing: walk toward the lane the team drew, arriving about
        // when the metronome does. Both the address and the schedule are static
        // contract data, so this is a plan rather than a reaction.
        Position[] sites = economy.VeinSites.ToArray();
        if (sites.Length == 0)
            return null;
        Position site = sites[laneDraw % sites.Length];
        if (economy.NextDepositTick(context.Tick) is not int due)
            return null;
        int distance = Walk(site, self.Position);
        return due - context.Tick <= distance + 2 ? [site] : null;
    }

    /// <summary>
    /// The cast decision. Everything it reads is contract or observation: the
    /// windup and its wait-only policy, the tile tags that refuse the route, the
    /// stance gun's bolt count and cadence, the return route's attack budget,
    /// and the capture policy that decides whether the ground can spare a body
    /// for two ticks.
    ///
    /// <para>What makes this a decision rather than a habit is that all of its
    /// prices are declared and all of them moved. The fan is straight by
    /// construction, so it still cannot reach anything a bend could have curved
    /// onto. What it can do is fire <em>n</em> bolts at one tick on adjacent
    /// headings — the one thing a sequence of single bolts cannot do against a
    /// body that re-decides between them — and, where its declared damage
    /// exceeds the mobile gun's, cross a kill threshold the mobile bolt does
    /// not reach. Against that stands one price the old ledger never had: the
    /// entry route may declare a slot-scoped cooldown, so casts are spaced by
    /// the ENTRY and the clock is public. See <see cref="Salvo"/>.</para>
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
        Direction[] order,
        Convoy convoy,
        EntryClock clock,
        ChannelState channel)
    {
        var self = context.Self;
        if (fan.Gun is not { } gun || forecasts.Count == 0)
            return null;

        // RULE 1. The clock is slot-scoped and survives this body's death, so a
        // life born inside the window cannot infer it — it is read or ignored,
        // and ignoring it spends the tick on a refused request.
        if (Salvo.EntryClockRead && clock.Live)
            return null;

        // RULE 6. A better-ranked sibling whose own clock is open and whose arcs
        // overlap mine will answer this window; my entry is worth more eight
        // ticks from now than as the second half of a doubled fan.
        if (Salvo.CastStagger && DeferringToSibling(context, doctrine, convoy, fan))
            return null;

        // GROUND FIRST — but "first" is not "instead of", and which one it is
        // depends on a declared weight, not on a habit. This stance keeps its
        // objective weight, so a body that raises it on the point is still
        // holding the point: it stops walking, not scoring. Wave 4 refused every
        // cast from contested ground because the map refused the tile anyway, so
        // the two rules were indistinguishable and only one of them was real.
        // With placement open they separate, and the honest rule is the weight.
        int selfWeight = doctrine.Form(self.FormId)?.ObjectiveWeight ?? 0;
        bool keepsGround = fan.Target.ObjectiveWeight >= selfWeight;
        bool onPoint = objective.Contains(self.Position);
        if (!keepsGround && (presence.SelfPresent || onPoint))
            return null;
        // Withdrawing is a decision to spend tiles saving a body; a blind windup
        // is the opposite of that whatever it covers. And breaking a claim this
        // body is not standing on needs feet, not a fan.
        if (posture is Posture.Withdraw)
            return null;
        if (posture is Posture.Contest && !onPoint)
            return null;
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
        // the target form among its legal values, and a tile THIS ROUTE's own
        // placement rules admit. The mask is authoritative; the placement read is
        // per-route, because a ground arm empties the route's forbidden-tag list
        // while leaving every tag on the map exactly where it was.
        var transform = book.ById(fan.Entry.ActionId);
        if (transform is null
            || !ActionBook.FormTargets(transform).Contains(fan.TargetFormId)
            || !doctrine.PlacementAdmits(fan.Entry.Placement, self.Position))
        {
            return null;
        }
        // Cooldown survives the transition, so a hot gun means the stance would
        // arrive unable to fire. The windup itself burns some of it.
        if (self.Cooldown > fan.EntryWindup)
            return null;

        // A blind windup is a commitment, and RULE 2 prices it by its declared
        // length instead of by reputation. The window the body cannot walk out of
        // is the entry windup, the tick that fires, and the exit windup — all
        // three read from the routes — so that is the window everything already
        // in flight is checked against, and it is a tick longer than wave 6
        // checked because wave 6 stopped at the entry.
        //
        // What changes in the other direction is the ready-muzzle clause. Wave 6
        // refused any tile a loaded enemy gun COULD cover, which on a widened
        // envelope is most of the board in front of you, and that single clause is
        // most of why the predecessor cast roughly once a match. The comment beside
        // it already said the right rule — the body must be able to afford one hit
        // it did not see coming — so that is now what it does, priced off the worst
        // damage the opposing catalog declares rather than off the mere existence
        // of a muzzle.
        int pinnedTicks = Salvo.CheapEntry
            ? fan.EntryWindup + 1 + fan.ExitWindup
            : fan.EntryWindup + 1;
        if (fan.BlindWindup
            && (self.Health <= 1
                || !threat.SafeStandingFor(
                    self.Position,
                    pinnedTicks,
                    self.Health)))
        {
            return null;
        }
        if (fan.BlindWindup && threat.AimedByReadyMuzzle(self.Position))
        {
            if (!Salvo.CheapEntry || self.Health <= WorstIncomingBolt(doctrine))
                return null;
        }

        int mobileDamage = doctrine.Attack(self.FormId)?.Projectile.DamagePerHit ?? 1;
        CastPlan best = Volley.Score(
            field, self.Position, self.Facing, fan, forecasts, objective,
            fan.EntryWindup, doctrine, _tracker, mobileDamage, channel);
        if (fan.CanRotate)
        {
            foreach (Direction direction in order)
            {
                if (direction == self.Facing)
                    continue;
                CastPlan turned = Volley.Score(
                    field, self.Position, direction, fan, forecasts, objective,
                    fan.EntryWindup + 1, doctrine, _tracker, mobileDamage,
                    channel);
                // A turn inside the stance costs a tick, so it has to be worth
                // more than the facing already worn.
                if (turned.Value * 0.85 > best.Value)
                    best = turned;
            }
        }
        if (best.AnchoredRays <= 0)
            return null;

        // RULE 4. A guarded body sends the arriving bolt back from its own tile
        // along the exactly reversed heading, carrying THIS bolt's damage class —
        // so a doubled fan bolt is a doubled bolt coming home, and a fan can feed
        // three of them at once. The trade is only worth taking when the contact
        // is the one that spends the guard's declared budget; otherwise the fan
        // goes somewhere else, which is always available because the arc never
        // tracks.
        if (Salvo.GuardAwareFan
            && best.DeflectedRays > best.BreakingRays
            && best.DeflectedRays * gun.Projectile.DamagePerHit >= self.Health)
        {
            return null;
        }

        double value = best.Value;
        // And short of lethal, a deflection is priced rather than forbidden: the
        // bolt dies on the arc, a doubled one comes home along the exact reverse
        // heading, and it is dodgeable — but the fan is choosing between facings
        // and the arc never tracks, so the facing that does not feed the shell is
        // always available. The contact that spends the guard's last declared
        // deflection is the exception the mechanic is built around, and it is
        // credited the same way the mobile planner credits it.
        if (Salvo.GuardAwareFan)
        {
            value -= 2.0 * gun.Projectile.DamagePerHit
                * (best.DeflectedRays - best.BreakingRays);
            value += 1.5 * best.BreakingRays;
        }
        // Sealing the front rank of a contested point is the fan's real job on
        // this contract: an opponent has to stand on those tiles to take them,
        // and three bolts deny a tile each instead of betting one bolt on one
        // guess. It is credited only while the point is actually contested.
        bool contested = presence.EnemyWeight > 0
            || (mode?.ClaimingTeamId is int claimer && claimer != doctrine.TeamId);
        if (contested)
            value += 0.8 * best.CoveredObjectiveTiles;

        // RULE C2 — THE FAN IS AN INTERRUPT. This is the wave-8 re-reading of
        // what a fan is FOR, and it inverts wave 7's finding. Wave 7 measured
        // the re-armed fan as a damage verb and concluded that a territorial
        // doctrine should cast sparingly, because damage that does not convert
        // to ground is noise on both sides of the ledger. On a channel ruleset
        // the damage IS ground: hostile damage to the controlling team's bodies
        // on the objective reverts that team's work at the declared rate, so a
        // fan landing several bolts on a channelling opponent is the largest
        // single territorial swing this chassis can produce, and it produces it
        // without standing on the tile.
        value += ChannelRules.InterruptWeight * best.InterruptProgress;

        // WHAT A FAN IS FOR, RE-PRICED FOR THE LAUNCH OFFSETS.
        //
        // The bolts are simultaneous and straight, so a fan still cannot reach
        // anything a curve could have reached and still never concentrates
        // damage. Its edge was always breadth at one arrival tick. What changed is
        // the alternative: the mobile gun now launches down THREE lanes from the
        // same facing, so "the fan covers a lane the single bolt cannot" is
        // simply false — every ray of the fan is a lane the ordinary gun can aim
        // down for one tick of nothing. The fan's remaining, real edge is
        // SIMULTANEITY: it denies its rays at the same instant, against a body
        // that would otherwise re-decide between two single bolts.
        //
        // So one body is no longer worth a stance in the open at all, and the
        // two-body rule stays strict. The new case the open ground adds is
        // narrower and better: standing on the point, where the stance keeps its
        // objective weight, the capture clock keeps running through the windup,
        // and the fan seals the tiles an attacker has to stand on to take it.
        // Wave 4 could not express that case because the map refused the tile.
        //
        // The on-point case is gated on a STEADY body — one this life has watched
        // hold a tile, or one whose form cannot move at all — and that gate is
        // the re-pricing that actually paid. A fan aimed at a body still choosing
        // its tile is a bet placed two ticks before the dice land: measured
        // without the gate, 51% of entries reached the stance with nothing left
        // to fire on and walked back out having spent four ticks of immobility on
        // nothing. Requiring evidence rather than a prediction cuts that to 41%
        // and turns a 25-23-2 record against the rebuilt predecessor into
        // 30-9-11 across five independent seed sets. That is what patience means
        // here: the blind commitment is affordable only when something else has
        // already stopped moving.
        bool holdingPoint = onPoint && keepsGround;
        bool worthTheStance = best.DistinctBodies >= 2
            || (contested
                && best.DistinctBodies >= 1
                && best.CoveredObjectiveTiles >= 2)
            || (holdingPoint
                && best.SteadyBodies >= 1
                && best.CoveredObjectiveTiles >= 1);

        // RULE 3. Two cases the old arithmetic could not express, and both of
        // them are ONE body.
        //
        // A KILL RAY is a body whose observed health is at or below the fan's
        // declared damage and above the mobile gun's. That is not "more damage";
        // it is a threshold. The mobile bolt leaves that body alive to keep
        // holding ground and keep shooting, and the difference between wounding
        // and removing it is the whole of what the re-armed bolt buys.
        //
        // A HEDGED BODY is one still choosing its tile that two or more rays can
        // reach. Wave 6 required the opposite — a STEADY body — because the entry
        // took two ticks and a fan aimed at a mover was a bet placed before the
        // dice landed. At a one-tick entry the fan resolves one tick behind the
        // bolt, and the single thing three simultaneous lanes are for is the body
        // that has not committed to one of them. Requiring steadiness there threw
        // away the fan's only structural advantage over the gun.
        if (Salvo.KillThreshold)
            worthTheStance |= best.KillRays > 0;
        if (Salvo.HedgeFan)
            worthTheStance |= best.HedgedBodies > 0 && best.AnchoredRays > 0;
        // And the case the channel adds, which is one body and is still worth
        // the stance: a body of the controlling team standing on the point.
        // The two-body rule exists because a fan cannot concentrate damage —
        // but a revert does not need concentration, it needs contact, and one
        // ray of contact takes its damage class off their run.
        if (ChannelRules.Interrupt)
            worthTheStance |= best.InterruptProgress > 0 && best.AnchoredRays > 0;
        if (!worthTheStance)
            return null;

        // Even then it has to beat the bolt it is displacing. The window is the
        // contract's — windup, the firing tick, the exit windup, and the cooldown
        // the stance gun leaves behind — but a body inside a fan cannot dodge
        // three ways at once, so the comparison is against the next bolt rather
        // than against every bolt of the window.
        //
        // The margin the displaced bolt is owed scales with how many lanes it can
        // choose from, read off the mobile gun's own declared aim range. A gun
        // with one lane is a guess and 15% is a fair tax on displacing it; a gun
        // that may launch 45 degrees either way is three guesses for the same
        // tick, and a fan has to be half again better than that to be worth two
        // ticks of standing still. On a straight-aim arm the number reverts to
        // wave 4's by construction.
        double lanes = MobileLanes(doctrine, self.FormId);
        double bar;
        if (!Salvo.RepricedBar)
        {
            bar = Math.Max(
                (1.0 + (0.15 * lanes)) * (mobile?.Value ?? 0),
                gun.ProjectilesPerAttack);
        }
        else
        {
            bar = RepricedBar(doctrine, fan, gun, self.FormId, lanes, mobile);
        }
        if (value < bar)
            return null;

        _castRequestedTick = context.Tick;
        return book.FormTargeted(
            transform,
            fan.TargetFormId,
            $"Cast {fan.TargetFormId} fan {value:0.0} over {bar:0.0} "
                + $"bodies {best.DistinctBodies} kill {best.KillRays} "
                + $"hedge {best.HedgedBodies} seal {best.CoveredObjectiveTiles} "
                + $"deflect {best.DeflectedRays}/{best.BreakingRays}"
                + (holdingPoint ? " onPoint" : string.Empty));
    }

    /// <summary>
    /// THE RE-DERIVED BAR. Wave 6 charged the fan a fixed 15%-per-lane premium
    /// over the bolt it displaced, and floored it at the fan's own bolt count.
    /// Both numbers encoded prices the contract has since changed, so both are
    /// recomputed from what the contract declares now.
    ///
    /// <para>The premium was a tax on BREADTH: a mobile gun that may launch down
    /// several lanes from one facing makes several guesses for the price of one
    /// tick, and displacing that deserved a margin. But the fan covers lanes of
    /// its own — as many as it launches bolts — so the tax is only owed on the
    /// lanes the fan gives up, and it is zero when the fan is at least as broad
    /// as the gun. What the fan still owes is TEMPO: the ticks it spends unable
    /// to move beyond the one the displaced bolt also costs, plus whatever the
    /// stance gun's own cooldown adds over the mobile gun's. On an arm where the
    /// stance gun recovers faster than the mobile one, that second term is zero
    /// and the cast is free of gun tempo entirely.</para>
    ///
    /// <para>The floor was the raw bolt count, which silently assumed every bolt
    /// was worth one mobile bolt. Divide it by the declared damage ratio and it
    /// becomes what it always meant: the prediction weight a cast must reach
    /// before it is worth walking out of, expressed in mobile-bolt equivalents.
    /// On an arm where the two guns hit alike this reverts to wave 6's number by
    /// construction.</para>
    /// </summary>
    private static double RepricedBar(
        Doctrine doctrine,
        StanceRoute fan,
        GenericActorRulesContract.AttackProfile gun,
        string formId,
        double lanes,
        Shot? mobile)
    {
        var mobileGun = doctrine.Attack(formId);
        double mobileDamage = Math.Max(1, mobileGun?.Projectile.DamagePerHit ?? 1);
        double fanDamage = Math.Max(1, gun.Projectile.DamagePerHit);
        double rays = fan.FanOffsets.Length;
        int gunTax = Math.Max(
            0,
            gun.CooldownTicks - (mobileGun?.CooldownTicks ?? gun.CooldownTicks));
        int idleTicks = Math.Max(0, fan.EntryWindup + fan.ExitWindup - 1);

        double margin = 1.0
            + (0.15 * Math.Max(0, lanes - rays))
            + (Salvo.IdleTickTax * (idleTicks + gunTax));
        double floor = Math.Max(1.0, gun.ProjectilesPerAttack * mobileDamage / fanDamage);
        return Math.Max(margin * (mobile?.Value ?? 0), floor);
    }

    /// <summary>
    /// RULE 6 — CAST STAGGER. Every ally publishes its own slot's route clocks,
    /// so "can that sibling cast this tick" is a read rather than a guess. Two
    /// fans opened on the same tick into overlapping arcs waste one of them: a
    /// diverging fan lands at most one bolt on one body whatever else is in the
    /// air, and the second entry buys nothing the first did not already deny.
    /// The convoy order decides who goes; a body only ever defers upward, so the
    /// leader always casts and no cycle of mutual deference can form.
    /// </summary>
    private static bool DeferringToSibling(
        GenericActorContext context,
        Doctrine doctrine,
        Convoy convoy,
        StanceRoute fan)
    {
        if (convoy.MyRank == 0 || context.Allies.IsEmpty)
            return false;
        var self = context.Self;
        foreach (var ally in context.Allies)
        {
            if (ally.ActorId.Equals(self.ActorId))
                continue;
            if (convoy.RankOf(ally.ActorId) is not int rank || rank >= convoy.MyRank)
                continue;
            // Only a sibling that could actually raise the same stance this tick
            // is a sibling worth waiting for.
            if (doctrine.Stances.BestFan(
                    ally.FormId,
                    doctrine.BoltsPerAttack(ally.FormId)) is not { } theirs)
            {
                continue;
            }
            if (EntryClock.ForAlly(context, ally, theirs).Live)
                continue;
            if (ally.Cooldown > theirs.EntryWindup)
                continue;
            // Arc overlap, measured the way the fan is measured: two cones share
            // a body when the sector between the two facings is inside the sum of
            // their half-widths and the bodies are close enough for both fans to
            // reach the same ground.
            int half = Math.Max(1, fan.FanOffsets.Length / 2)
                + Math.Max(1, theirs.FanOffsets.Length / 2);
            int mine = (int)self.Facing.ToProjectileHeading();
            int other = (int)ally.Facing.ToProjectileHeading();
            int delta = Math.Abs(mine - other);
            int sectors = Math.Min(delta, 8 - delta);
            int reach = theirs.Gun?.Projectile.MaxTravelTiles ?? 0;
            if (sectors <= half
                && self.Position.ChebyshevDistance(ally.Position) <= reach)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The most damage one contact from the opposing catalog can do, read across
    /// every opposing form's own attack profile. A re-armed special is exactly
    /// the case this exists for: the other side's fan bolt need not agree with
    /// its mobile bolt, and "can I afford one surprise hit" is a question about
    /// the worst one.
    /// </summary>
    private static int WorstIncomingBolt(Doctrine doctrine)
    {
        int worst = 1;
        foreach (string formId in doctrine.OpposingFormIds)
        {
            int damage = doctrine.Attack(formId)?.Projectile.DamagePerHit ?? 1;
            if (damage > worst)
                worst = damage;
        }
        return worst;
    }

    /// <summary>
    /// Whether firing the mobile gun this tick would leave the body unable to
    /// request the stance on the next one. The entry is refused while the attack
    /// cooldown exceeds the entry windup, and the cooldown a successful attack
    /// sets is the firing gun's own declared value — so this is arithmetic on two
    /// contract numbers, not a guess about the engine.
    /// </summary>
    private static bool WouldCloseTheStance(
        Doctrine doctrine,
        string formId,
        StanceRoute fan)
    {
        int afterFiring = doctrine.Attack(formId)?.CooldownTicks ?? 0;
        return afterFiring > fan.EntryWindup;
    }

    /// <summary>
    /// How many distinct launch lanes this form's mobile gun owns from one
    /// facing, read off its declared initial-aim range. One on a straight-aim
    /// arm, three where a bolt may leave at 45 degrees either side.
    /// </summary>
    private static double MobileLanes(Doctrine doctrine, string formId)
    {
        if (doctrine.Attack(formId) is not { } attack)
            return 1;
        var program = attack.ShotProgram;
        if (!program.Enabled)
            return 1;
        int low = Math.Min(0, program.MinInitialAimSteps);
        int high = Math.Max(0, program.MaxInitialAimSteps);
        return Math.Max(1, high - low + 1);
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
        int mobileDamage = doctrine.Attack(worn.SourceFormId)
            ?.Projectile.DamagePerHit ?? 1;

        CastPlan now = Volley.Score(
            field, self.Position, self.Facing, worn, forecasts, objective, 0,
            doctrine, _tracker, mobileDamage);
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
                    field, self.Position, direction, worn, forecasts, objective, 1,
                    doctrine, _tracker, mobileDamage);
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

        // RULE 7 — THE STANCE IS COMMITTED, AND THE CONTRACT IS WHY. Firing is
        // itself the exit: the automatic return spends the same windup a
        // requested return spends, so leaving with the gun loaded costs exactly
        // what shooting costs and additionally throws away the entry — and where
        // the entry declares a cooldown, the entry is the scarce thing. Nor is
        // there a gun-tempo reason to hold: on an arm where the stance gun's
        // declared cooldown is at or below the mobile gun's, the fan leaves the
        // body no colder than the bolt would have. So a loaded stance shoots at
        // whatever the fan can plausibly reach rather than walking out, and only
        // a stance that CANNOT shoot ever mobilizes.
        if (Salvo.StanceCommit
            && shoot is not null
            && self.Cooldown == 0
            && (trapped || idle)
            && (worn.Gun?.CooldownTicks ?? int.MaxValue)
                <= (doctrine.Attack(worn.SourceFormId)?.CooldownTicks ?? int.MaxValue))
        {
            return book.Parameterless(
                shoot,
                $"fan {worn.BoltsPerAttack} bolts {self.Facing} on the way out "
                    + $"(the return is free) value {nowValue:0.0}");
        }
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
    ///
    /// <para>The lateral bias is a per-slot CONSTANT, which was enough while the
    /// preferred tile was always available and is not enough once it is not: a
    /// sibling standing on my station, or one whose route runs through it, sends
    /// me into the fallback search, and the fallback search had no idea a sibling
    /// existed. So a taken station is refused here, at the source, rather than
    /// walked to and queued behind — and a station is never accepted inside a
    /// one-tile corridor, because a standoff doctrine holds its station for
    /// dozens of ticks and a corridor held for dozens of ticks is a wall across
    /// my own team's only route to the point.</para>
    /// </summary>
    private static Position Station(
        Doctrine doctrine,
        int activeIndex,
        int standoff,
        HashSet<Position> objective,
        GenericActorRulesContract.AttackProfile? attack,
        StanceRoute? fan,
        Convoy convoy)
    {
        Field field = doctrine.Field;
        Position edge = doctrine.NearEdge(activeIndex);
        int edgeDepth = doctrine.Project(edge);
        Position wanted = edge.Offset(
            (-doctrine.Forward.Dx * standoff)
                + (doctrine.Lateral.Dx * doctrine.LateralBias),
            (-doctrine.Forward.Dy * standoff)
                + (doctrine.Lateral.Dy * doctrine.LateralBias));
        // Where a fan exists the station prefers a tile the fan can actually be
        // raised on — which is a per-route question, and on an open-ground arm the
        // answer is "anywhere". Wave 4 needed this clause because the map's
        // transition tags covered the whole central lane, which is exactly where a
        // standoff striker's station wants to be; when the route stops refusing
        // those tiles the clause stops steering and the station goes back to
        // being chosen on firing geometry alone.
        if (field.IsOpen(wanted)
            && !objective.Contains(wanted)
            && !convoy.StationTaken(wanted)
            && !(Convoy.ChokeEtiquette && convoy.Crowded && convoy.IsChoke(wanted))
            && (fan is null
                || doctrine.PlacementAdmits(fan.Entry.Placement, wanted)))
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
                if (convoy.StationTaken(tile))
                    continue;

                double score = -1.0 * (Math.Abs(tile.X - wanted.X)
                    + Math.Abs(tile.Y - wanted.Y));
                if (doctrine.Project(tile) > edgeDepth)
                    score -= 4.0;
                // Holding a corridor is how one body walls the whole band off.
                if (Convoy.ChokeEtiquette && convoy.Crowded && convoy.IsChoke(tile))
                    score -= 5.0;

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
                if (fan is not null
                    && doctrine.PlacementAdmits(fan.Entry.Placement, tile))
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
        Direction[] order,
        Convoy convoy,
        ChannelState channel,
        Economy economy,
        HashSet<Position> bankTiles)
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
                field, attack, tile, facing, forecasts, objective, posture,
                convoy);
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
                // The answer wave 5 forgot to count. A guarded form returns the
                // bolt that hits it back down the bearing it arrived on, so two
                // of my bodies on one ray out of it are both on the return's
                // lane — and the bolt that reaches the second one is mine.
                else if (convoy.SharesDeflectionLane(tile, ally.Position))
                    score -= 1.5;
            }

            // The coordination layer. Route claims and corridor precedence are
            // charged on the tile, not on the doctrine, so a posture that would
            // otherwise walk through a sibling simply finds the neighbouring
            // tile scores better.
            score -= convoy.LaneCost(tile, isStay);
            score -= convoy.ChokeCost(tile, isStay);
            score -= convoy.RallyCost(tile);

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

            // THE CHANNEL. Two halves of one arithmetic, kept apart because
            // they are separate claims about the game and are attributed
            // separately. The first says stillness is what CAPTURES, and it is
            // the only rule in this doctrine that pays a body for not moving.
            // The second says presence is what DENIES — every body counts,
            // moving or not — which is why a defender may kite on the point and
            // an attacker may not. Neither test asks what the body is doing:
            // rotating, shooting and entering a stance all keep the tile, and
            // the tile is the whole rule.
            if (ChannelRules.StillClaim)
                score += ChannelRules.ProgressWeight * channel.Build(tile);
            if (ChannelRules.DenyPresence)
                score -= ChannelRules.ProgressWeight * channel.Deny(tile);

            // THE ESCORT. A body on the firing line to a channelling teammate,
            // off the objective, eats the bolt that would otherwise revert the
            // whole run. It is worth about what the run loses per bolt.
            if (ChannelRules.Screen)
            {
                score += ChannelRules.ProgressWeight
                    * channel.ScreenValue(
                        field, tile, forecasts, doctrine.OpposingAnyRange);
            }

            // THE ASSAY. Stepping onto a pile banks one instantly at the tile
            // with no transport, and a carried load banks in full on the home
            // pad. Both are ordinary tile value, so they compete with route and
            // danger on the same ladder rather than overriding them — but they
            // only compete at ALL where the contract says a body's presence on
            // the point has stopped paying. Where net weight scales the capture
            // rate, one tile of detour is a real cut in that rate, and a pile
            // worth one scrap is not worth it. Same declared fact as the lane.
            if (channel.MarginalBodyIsFree)
                score += economy.TileValue(tile, bankTiles);

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
            double coveredNow = attack is null
                ? 0
                : CoverScore(
                    field, attack, self.Position, self.Facing, forecasts, convoy);
            foreach (Direction direction in order)
            {
                if (direction == self.Facing || !legalTurns.Contains(direction))
                    continue;
                double bonus = 0;
                if (attack is not null)
                {
                    double coveredThen = CoverScore(
                        field, attack, self.Position, direction, forecasts,
                        convoy);
                    bonus = 1.7 * (coveredThen - coveredNow);
                    if (forecasts.Count == 0)
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

    /// <summary>
    /// How well a pose covers the visible bodies. This used to be a COUNT, and a
    /// count was the right shape while a facing owned one lane: either the
    /// dominant axis pointed at you or nothing did. Three launch lanes end that.
    /// Almost every pose now covers almost every body somehow, so counting them
    /// returns the same number for every facing and the rotation logic goes flat
    /// exactly when it matters most.
    ///
    /// <para>What still differs is HOW. A lane or an aim-only diagonal arrives on
    /// the shortest path the chassis owns and commits nothing; a curve arrives
    /// later, spends the one bend, and can be eaten by a wall or a strict corner
    /// on the way. So cover is scored by kind, and turning is a choice between
    /// qualities of cover rather than between cover and none.</para>
    /// </summary>
    private static double CoverScore(
        Field field,
        GenericActorRulesContract.AttackProfile attack,
        Position from,
        Direction facing,
        List<EnemyForecast> forecasts,
        Convoy convoy)
    {
        double score = 0;
        foreach (EnemyForecast forecast in forecasts)
        {
            double quality = Quality(
                field, attack, from, facing, forecast.State.Position);
            score += quality
                - convoy.SiblingAlreadyCovers(
                    attack,
                    forecast.State.Position,
                    quality);
        }
        return score;
    }

    private static double Quality(
        Field field,
        GenericActorRulesContract.AttackProfile attack,
        Position from,
        Direction facing,
        Position target) =>
        ForkPlanner.CoverKind(field, from, target, attack, facing) switch
        {
            ForkPlanner.Cover.Direct => 1.0,
            ForkPlanner.Cover.Curved => 0.6,
            _ => 0.0,
        };

    private static double Coverage(
        Field field,
        GenericActorRulesContract.AttackProfile? attack,
        Position from,
        Direction facing,
        List<EnemyForecast> forecasts,
        HashSet<Position> objective,
        Posture posture,
        Convoy convoy)
    {
        if (attack is null)
            return 0;

        double value = 1.5 * CoverScore(
            field, attack, from, facing, forecasts, convoy);
        if (posture == Posture.Seize)
            return value;

        double tiles = 0;
        foreach (Position tile in objective)
        {
            if (tiles >= 4)
                break;
            tiles += Quality(field, attack, from, facing, tile);
        }
        return value + (0.55 * Math.Min(4.0, tiles));
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
        HashSet<Position> objective,
        ChannelState channel)
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

            // RULE C5 — NEVER FEED A MIRROR. A bolt stops on the FIRST enemy
            // body its lane crosses, so that body is the only one that can
            // deflect it, whatever the plan was aiming at further down. If that
            // contact arrives inside a declared projectile guard's quadrant the
            // bolt does not merely fail — it comes back from that tile along
            // the exactly reversed heading, owned by their team, carrying this
            // bolt's own damage class. That is a self-inflicted hit, and no
            // prediction weight further down the lane can be worth one. The
            // single exception is the contact that spends the guard's last
            // declared deflection, which is a forced return with an exit and a
            // fresh entry windup to punish.
            if (ChannelRules.MirrorRefusal
                && FeedsAMirror(doctrine, tracker, plan, forecasts, context))
            {
                continue;
            }

            double peak = 0;
            int touched = 0;
            bool anchored = false;
            bool reverts = false;
            bool localReverts = false;
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
                localReverts = false;
                foreach (EnemyForecast forecast in forecasts)
                {
                    int weight = forecast.Weight(tile, steps);
                    if (weight <= 0)
                        continue;
                    double scaled = weight * forecast.Pressure(damage);
                    double bonus = 0;

                    // RULE C2 — A BOLT ONTO THE POINT IS TERRITORY. Damage to a
                    // body of the controlling team standing on the active
                    // objective reverts that team's work on the current run, at
                    // the declared rate and bounded by what the run has put on.
                    // That is the one way this doctrine moves the objective
                    // number without standing on the tile, and it is why a
                    // standoff has a game on this arm at all.
                    bonus += ChannelRules.InterruptWeight
                        * channel.InterruptValue(tile, damage);

                    // RULE E4 — A LOADED CARRIER IS A TARGET. The load is
                    // published on every visible enemy, and a killed carrier
                    // drops its wreck plus its whole load on one tile, for
                    // whoever did the killing. So a body's worth is its
                    // pressure plus what removing it spills.
                    if (LedgerRules.Intercept
                        && forecast.State.CarriedScrap > 0
                        && forecast.State.Health <= damage)
                    {
                        bonus += LedgerRules.CarrierWeight
                            * forecast.State.CarriedScrap;
                    }

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
                        localReverts = channel.InterruptValue(tile, damage) > 0;
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
                    reverts = localReverts;
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
            {
                best = new Shot(
                    Build(legality, plan, aimed), value, anchored, reverts);
            }
        }
        return best;
    }

    /// <summary>
    /// Whether this trajectory's FIRST enemy contact is a guarded body that
    /// would return the bolt. A projectile stops on the first enemy actor it
    /// meets, so only that body can deflect, and the arrival bearing at that
    /// tile is the reverse of the step that reached it. The contact that spends
    /// a guard's last declared deflection is exempt: that one is a forced
    /// return with a punish window, which is the trade the mechanic is built
    /// around.
    /// </summary>
    private static bool FeedsAMirror(
        Doctrine doctrine,
        QuarryTracker tracker,
        ShotPlan plan,
        List<EnemyForecast> forecasts,
        GenericActorContext context)
    {
        Position previous = context.Self.Position;
        foreach ((Position tile, int _, int _) in plan.Swept)
        {
            foreach (EnemyForecast forecast in forecasts)
            {
                if (forecast.State.Position != tile)
                    continue;
                if (!forecast.Guarded
                    || !Deflects(previous, tile, forecast.State.Facing))
                {
                    return false;
                }
                int? budget = doctrine.Stances
                    .Wearing(forecast.State.FormId)?.DeflectionBudget;
                int seen = tracker.Get(forecast.State.ActorId)?.Deflections ?? 0;
                return budget is not int limit || seen + 1 < limit;
            }
            previous = tile;
        }
        return false;
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
