using BotArena.Sdk;

/// <summary>
/// The skill arc-light is built around, re-priced for the arm it now plays. A
/// volley cast is one whole decision sequence, not an action: transform in over
/// a declared windup that permits nothing but waiting, shoot once, and be
/// returned by the engine because the route's declared budget is one attack. You
/// cannot squat in the stance and you cannot dodge inside its windup, so the
/// cast is priced before it is paid.
///
/// <para>Two contract facts moved under this doctrine since wave 4, and both are
/// read rather than remembered.</para>
/// <list type="number">
/// <item><b>The cast has no position problem any more.</b> The stance entry
/// route's own <c>placement</c> declares an EMPTY forbidden-tag set on this arm,
/// so the fan may rise on an objective tile and in the central corridor. Wave 4
/// intersected the MAP's transition-forbidden tag instead and therefore refused
/// 112 legal tiles, including every tile it was trying to deny. The route is
/// asked now, and the walk to a shoulder post is gone with the constraint that
/// invented it.</item>
/// <item><b>The fan is paid for in BODIES, and the price is arithmetic.</b> The
/// stance costs entry windup + one attack + the return's windup; the ordinary
/// gun would fire one aimed bolt per cadence in that window. So the fan must
/// connect with <c>ceil(cycle / cadence)</c> distinct bodies to break even — two
/// on the measured arm. That single derived number is what turns the volley from
/// the losing trade wave 4 measured in a duel into the right answer against a
/// four-slot swarm, without changing one line of its pricing philosophy.</item>
/// </list>
/// </summary>
internal sealed class ArcStance
{
    private readonly ArcFacts _facts;
    private readonly GenericActorContext _context;
    private readonly ArcThreat _threat;
    private readonly ArcGun _gun;
    private readonly ArcMemory _memory;
    private readonly ArcTraffic _traffic;

    public ArcStance(
        ArcFacts facts,
        GenericActorContext context,
        ArcThreat threat,
        ArcGun gun,
        ArcMemory memory,
        ArcTraffic traffic)
    {
        _facts = facts;
        _context = context;
        _threat = threat;
        _gun = gun;
        _memory = memory;
        _traffic = traffic;
    }

    /// <summary>
    /// Why the last <see cref="TryEnter"/> call declined. Bounded diagnostic
    /// only; it never affects a decision, and it is the difference between
    /// "the bot did not adopt the skill" and "the skill was priced and refused".
    /// </summary>
    public string Veto { get; private set; } = "unasked";

    /// <summary>
    /// True when the cast <see cref="TryEnter"/> just returned is forecast to
    /// KILL at least one body rather than merely to damage it. The one condition
    /// under which the cast is allowed above the aimed bolt: a bolt that removes
    /// health and a fan that removes a body are not the same purchase, and the
    /// second one also removes that body's objective weight for a whole respawn
    /// clock.
    /// </summary>
    public bool Executes { get; private set; }

    /// <summary>
    /// True when this body is currently inside a stance: a form whose only way
    /// out is the parameterless return route the engine also fires on its own.
    /// </summary>
    public bool InStance =>
        _facts.StanceBudget(_context.Self.FormId) is not null;

    /// <summary>
    /// The declared budget of the stance this body is standing in, or null when
    /// it is not in one.
    /// </summary>
    public GenericActorRulesContract.AutomaticReturnTrigger? Budget =>
        _facts.StanceBudget(_context.Self.FormId);

    /// <summary>
    /// What to do while inside the stance. The budget is one attack and an
    /// unfired cast is a pure loss, so this fires as soon as any lane connects,
    /// rotates once when a different bearing connects better, and otherwise
    /// leaves early rather than standing immobile for nothing.
    /// </summary>
    public GenericActorDecision? Act(int ticksInStance)
    {
        if (!InStance)
            return null;

        ArcGun.Shot? shot = _gun.Best();
        if (shot is not null && shot.Score > 0)
            return shot.Decision;

        ArcGun.Fan fan = _gun.FanForecast(
            _context.Self.FormId,
            _context.Self.Facing,
            _context.Self.Position,
            ticksFromNow: 0);
        if (fan.Bodies > 0 && Fire() is GenericActorDecision connected)
            return connected;

        if (Swing(ticksInStance) is GenericActorDecision turn)
            return turn;

        // Bolts block movement and a contact hurts, so a fan laid across ground
        // the opposition wants is worth firing even with no predicted body on a
        // lane: three simultaneous bolts close three tiles. The budget is one
        // attack either way, so an unfired cast is a pure loss.
        if (Denial() is GenericActorDecision denial)
            return denial;

        bool stuck = _context.Enemies.IsEmpty
            || ticksInStance >= 2
            || _threat.Threatened(_context.Self.Position, 2);
        if (!stuck)
            return null;

        // V3 — the charge is already spent. Frequency is priced on the ENTRY,
        // so by the time this body is standing in the stance the whole route
        // cooldown is running whether or not a bolt leaves. The engine's own
        // return spends the identical exit windup a mobilize would, so firing is
        // free and leaving unfired throws the charge away. Wave 6 leaves here —
        // measured entering and walking straight back out on the next tick —
        // because its Leave sat above an unforecast shot, and on a 4-tick,
        // uncharged, damage-1 fan that was nearly free. It is not free now.
        if (ArcRules.EntryClockIsACharge
            && Fire() is GenericActorDecision spend)
        {
            return spend;
        }

        if (Leave() is GenericActorDecision leave)
            return leave;
        return null;
    }

    /// <summary>
    /// One rotation inside the stance, when another bearing reaches more bodies.
    /// Spent at most once per entry and never with a bolt arriving: a stance tick
    /// is an immobile tick and the budget does not grow to pay for it.
    /// </summary>
    private GenericActorDecision? Swing(int ticksInStance)
    {
        GenericActorActionLegality? rotate = Legality(
            GenericActorRulesContract.ActionKind.Rotation);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            headings = rotate?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        if (rotate is null
            || headings is null
            || ticksInStance >= 1
            || _threat.Threatened(_context.Self.Position, 1))
        {
            return null;
        }

        ArcGun.Fan here = _gun.FanForecast(
            _context.Self.FormId,
            _context.Self.Facing,
            _context.Self.Position,
            ticksFromNow: 0);
        int bestValue = here.Bodies + here.Supply + here.Kills + here.OnPoint;
        int bestCover = _gun.FanCoverage(
            _context.Self.FormId,
            _context.Self.Facing,
            _context.Self.Position,
            ticksFromNow: 0);
        Direction? best = null;
        foreach (Direction facing in headings.AllowedValues)
        {
            if (facing == _context.Self.Facing)
                continue;
            ArcGun.Fan turned = _gun.FanForecast(
                _context.Self.FormId,
                facing,
                _context.Self.Position,
                ticksFromNow: 1);
            int cover = _gun.FanCoverage(
                _context.Self.FormId,
                facing,
                _context.Self.Position,
                ticksFromNow: 1);
            int value = turned.Bodies
                + turned.Supply
                + turned.Kills
                + turned.OnPoint;
            if (value > bestValue || (value == bestValue && cover > bestCover))
            {
                bestValue = value;
                bestCover = cover;
                best = facing;
            }
        }
        return best is Direction turn
            ? new GenericActorDecision(
                rotate.ActionId,
                rotate.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(turn)],
                $"swinging the fan onto {turn}")
            : null;
    }

    private GenericActorDecision? Fire()
    {
        GenericActorActionLegality? attack = Legality(
            GenericActorRulesContract.ActionKind.Attack);
        return attack is null || !attack.Constraints.IsEmpty
            ? null
            : GenericActorDecision.WithoutArguments(
                attack.ActionId,
                attack.ActionCode,
                "spending the cast on a bearing that connects");
    }

    /// <summary>
    /// Fire the fan for ground denial rather than for a predicted hit: legal this
    /// tick, and at least one lane crossing either a tile the opposition can
    /// reach or a tile of the objective under contest.
    /// </summary>
    private GenericActorDecision? Denial()
    {
        GenericActorActionLegality? attack = Legality(
            GenericActorRulesContract.ActionKind.Attack);
        if (attack is null || !attack.Constraints.IsEmpty)
            return null;

        GenericActorRulesContract.AttackProfile? profile =
            _facts.Attack(_context.Self.FormId);
        if (profile is null)
            return null;
        var wanted = _context.Mode
            is GenericActorContext.ModeObservationState.Frontline mode
            ? _facts.ObjectiveTiles(mode.ActivePositionIndex).ToHashSet()
            : [];
        foreach (Position tile in _context.Enemies.Select(enemy => enemy.Position))
            wanted.Add(tile);
        if (wanted.Count == 0)
            return null;

        // V4. A lane that reaches a guard's face terminates there in a returned
        // bolt of the fan's own damage class, so the ground beyond it is not
        // denied and the tile it stops on is a bill.
        var deflectors = new HashSet<Position>();
        if (ArcRules.FanRespectsGuards)
        {
            foreach (GenericActorContext.ObservedEnemyState enemy
                     in _context.Enemies)
            {
                if (_threat.WouldReturnFire(enemy, _context.Self.Position))
                    deflectors.Add(enemy.Position);
            }
        }

        foreach (ProjectileHeading heading in _gun.FanHeadings(
                     _context.Self.FormId,
                     _context.Self.Facing))
        {
            Position cursor = _context.Self.Position;
            for (int tile = 0; tile < profile.Projectile.MaxTravelTiles; tile++)
            {
                cursor = ArcBoard.Step(cursor, heading);
                if (_facts.IsWall(cursor) || deflectors.Contains(cursor))
                    break;
                if (wanted.Contains(cursor))
                {
                    return GenericActorDecision.WithoutArguments(
                        attack.ActionId,
                        attack.ActionCode,
                        "laying the fan across contested ground");
                }
            }
        }
        return null;
    }

    /// <summary>The parameterless return out of a stance, when it is legal.</summary>
    public GenericActorDecision? Leave()
    {
        GenericActorRulesContract.FormTransition? route =
            _facts.ReturnRoute(_context.Self.FormId);
        if (route is null)
            return null;
        GenericActorActionLegality? action = _context.ActionLegalities
            .FirstOrDefault(candidate =>
                candidate.Available
                && string.Equals(
                    candidate.ActionId,
                    route.ActionId,
                    StringComparison.Ordinal));
        return action is null || !action.Constraints.IsEmpty
            ? null
            : GenericActorDecision.WithoutArguments(
                action.ActionId,
                action.ActionCode,
                "dropping the stance early");
    }

    /// <summary>
    /// A cast this body should commit to, or null. Every gate is a contract read
    /// rather than a constant:
    /// <list type="number">
    /// <item>the route exists, the legality mask offers its target form, and the
    /// route's OWN placement accepts this tile;</item>
    /// <item>no hostile bolt in flight can reach the tile before the stance is
    /// usable, because the windup permits only waiting;</item>
    /// <item>no enemy gun BEARS on the tile — a loaded gun is not a bolt, and
    /// the bolt-only test is what left bodies dying mid-windup;</item>
    /// <item>the change must not shed objective weight that is load-bearing;</item>
    /// <item>the fan must connect with at least
    /// <c>ceil(stance cycle / gun cadence)</c> distinct bodies — the contract's
    /// own tempo arithmetic, which is two on this arm;</item>
    /// <item>and it must not be the tile this life last cast from inside two
    /// cycles, because a caster that re-enters where it just stood is a
    /// stationary target with a published windup.</item>
    /// </list>
    /// </summary>
    public GenericActorDecision? TryEnter(ArcKeel keel, ArcGun.Shot? aimed)
    {
        Executes = false;
        Veto = "no-route";
        GenericActorRulesContract.FormTransition? route =
            _facts.FanStanceRoute(_context.Self.FormId);
        if (route is null || _context.Enemies.IsEmpty)
            return null;

        // V3 — ask the published clock before anything else. The entry route
        // declares a cooldown scoped to this UNIT SLOT: it survives my death,
        // so a body born inside the window cannot infer it from its own history
        // and must not try. A request inside the window is an ordinary Blocked,
        // which under a facing lock is a whole tick of aim and a whole tick of
        // ground given away for a fact the observation already published.
        if (ArcFacts.RouteReadyAt(_context, route) is int readyAt
            && _context.Tick < readyAt)
        {
            Veto = $"clock{readyAt - _context.Tick}";
            return null;
        }

        // V3 — a charge that cannot be fired is a charge thrown away. The body
        // carries ONE cooldown counter, the stance preserves its remaining
        // ticks, and on this arm it advances with time in every form, so the
        // stance is usable one tick after the request and the gun must be at or
        // below one tick then. Entering under a hot gun buys an immobile body
        // and spends the whole route clock for nothing.
        if (ArcRules.EntryClockIsACharge
            && _context.Self.Cooldown > ArcFacts.CommitTicks(route))
        {
            Veto = $"hot{_context.Self.Cooldown}";
            return null;
        }

        Veto = "illegal";
        GenericActorActionLegality? transform = _context.ActionLegalities
            .FirstOrDefault(candidate =>
                candidate.Available
                && string.Equals(
                    candidate.ActionId,
                    route.ActionId,
                    StringComparison.Ordinal));
        GenericActorActionLegality.ArgumentConstraint.FormTargetConstraint?
            forms = transform?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .FormTargetConstraint>()
                .SingleOrDefault();
        if (transform is null
            || forms is null
            || !forms.AllowedFormIds.Contains(
                route.TargetFormId,
                StringComparer.Ordinal))
        {
            return null;
        }

        Veto = "placement";
        if (!_facts.PlacementAllows(route, _context.Self.Position))
            return null;

        int commit = ArcFacts.CommitTicks(route);

        // C5 — THE WAVE-6 GATE, and the one this wave was assigned to me by
        // name. A stance is immobile for its whole declared cycle, so entering
        // one does not merely risk this body: it converts this body into TERRAIN
        // for that many ticks. Wave 5 priced the cast against the enemy's bodies
        // (FanForecast), against the enemy's loaded guns (Bearing), and against
        // its own tempo (RequiredFanHits) — and never once against its own
        // team's routes. On an open-ground arm that omission has a specific
        // shape: the fan may now rise anywhere, including in the two-tile
        // doorway on the central row that every one of my reinforcements has to
        // walk through, and a four-tick cast there seals it.
        Veto = "own-path";
        if (!_traffic.MayCommit(_context.Self.Position, _facts.StanceCycleTicks(route)))
            return null;

        Veto = "windup-bolt";
        if (_threat.Threatened(_context.Self.Position, commit + 1))
            return null;

        Veto = "weight";
        if (_facts.ObjectiveWeight(route.TargetFormId)
                < _facts.ObjectiveWeight(_context.Self.FormId)
            && keel.PresenceIsLoadBearing)
        {
            return null;
        }

        int required = _facts.RequiredFanHits(route, _context.Self.FormId);
        ArcGun.Fan fan = _gun.FanForecast(
            route.TargetFormId,
            _context.Self.Facing,
            _context.Self.Position,
            commit);
        // A supply body is worth an extra bolt of credit — but only on a bearing
        // that is already paying for itself. Removing the one body that can
        // rebuild the swarm is worth more than removing one of its products, and
        // the fan is the only weapon that reaches the supply AND its escort in a
        // single action; crediting it on a single-body bearing would just be the
        // permissive pricing wave 4 measured losing.
        //
        // V2 adds the only other credit in the doctrine, and it is not a
        // heuristic: a body whose health is at or below the fan bolt's declared
        // damage does not survive the bolt. Removing a body removes its gun, its
        // objective weight and — if it is the supply — every rebuild behind it,
        // for a whole respawn clock. That is worth strictly more than the health
        // an aimed bolt would have removed from a body that lives.
        // W3 adds the wave's third credit, and it is the biggest number in the
        // doctrine. The interrupt is scoped to controlling-team bodies standing
        // ON the active objective and it reverts the whole RUN, so a lane into a
        // channeler is worth the fan bolt's damage in progress as well as in
        // health. Against the declared threshold of 8, three such bodies at
        // damage 2 revert 6 — three quarters of a capture, in one action, from a
        // body that is about to be returned to mobility anyway. Each on-point
        // body is credited as one extra forecast body, which is the same unit
        // the break-even is denominated in.
        int value = fan.Bodies
            + (fan.Bodies >= 2 && fan.Supply > 0 ? 1 : 0)
            + (ArcRules.FanExecutes ? fan.Kills : 0)
            + (ArcRules.ChannelersArePrey ? fan.OnPoint : 0);
        int guns = _threat.Bearing(_context.Self.Position);
        Veto = $"pay{fan.Bodies}s{fan.Supply}k{fan.Kills}"
            + $"p{fan.OnPoint}r{required}g{guns}";

        if (value < required)
            return null;

        // A gun already pointed at this tile is a windup the opposition gets to
        // shoot into, so the surplus over break-even IS the budget for loaded
        // guns. V5 adds the term wave 6 could not have: the immobile exposure is
        // the entry windup plus the cast tick, and what the body can afford is a
        // question about its health against the hardest bolt visible rather than
        // a constant tuned for a two-tick telegraph. One survivable contact buys
        // one bearing of tolerance — and exactly one, because a second contact
        // inside the same window is a dead striker.
        int slack = Math.Max(0, value - required);
        if (ArcRules.EntryBearingBudget && _context.Self.Health > WorstBolt())
            slack++;
        if (guns > slack)
            return null;

        int cycle = _facts.StanceCycleTicks(route);
        if (_memory.LastCastTile == _context.Self.Position
            && _context.Tick - _memory.LastCastTick < cycle * 2
            && value <= required)
        {
            Veto = "resquat";
            return null;
        }

        Executes = ArcRules.FanExecutes && fan.Kills > 0;

        // The aimed bolt still wins ties: it lands this tick, it is aimed rather
        // than pointed, and it leaves the body free to step. It does NOT win the
        // tie against a kill — an aimed bolt that leaves a body standing and a
        // fan that removes it are not the same purchase, and the fan no longer
        // taxes the gun on the way out, so the bolt this refuses is deferred by
        // about two ticks rather than given up.
        if (aimed is not null
            && value <= required
            && aimed.Score >= 60
            && !Executes)
        {
            Veto = "aimed-better";
            return null;
        }

        Veto = Executes ? "execute" : "cast";
        return new GenericActorDecision(
            transform.ActionId,
            transform.ActionCode,
            [
                new GenericActorActionArgument.FormTargetArgument(
                    route.TargetFormId),
            ],
            $"casting into {value} bearings ({fan.Kills} lethal) over {commit}"
                + " committed ticks");
    }

    /// <summary>
    /// The damage one contact from the hardest gun currently visible would cost,
    /// read from each enemy's own declared projectile rather than assumed. A
    /// deflecting shell returns the class of the bolt it caught, and a fan bolt
    /// is not an ordinary bolt, so "one contact" is no longer one health.
    /// </summary>
    private int WorstBolt()
    {
        int worst = 1;
        foreach (GenericActorContext.ObservedEnemyState enemy in _context.Enemies)
        {
            worst = Math.Max(worst, _facts.Damage(enemy.FormId));
            // A body one tick from a fan stance is a fan, not a gun.
            if (enemy.PendingSameLifeTransition?.TargetFormId is string pending)
                worst = Math.Max(worst, _facts.Damage(pending));
        }
        return worst;
    }

    /// <summary>
    /// The rotation that would turn a declined cast into a qualifying one. A fan
    /// aims by facing and a stance tick cannot dodge, so the aim is bought
    /// OUTSIDE the stance, on a tick where this body can still step. Returns null
    /// unless the swing genuinely buys the required number of bodies.
    /// </summary>
    public Direction? CastBearing()
    {
        GenericActorRulesContract.FormTransition? route =
            _facts.FanStanceRoute(_context.Self.FormId);
        if (route is null || _context.Enemies.IsEmpty)
            return null;
        // V3: a rotation is a whole tick under a facing lock, so buying a
        // bearing for a route the published clock is holding shut spends the
        // tick and the aim on a cast that cannot happen.
        if (ArcFacts.RouteHeld(_context, route))
            return null;
        if (!_facts.PlacementAllows(route, _context.Self.Position))
            return null;
        // C5: no point buying the bearing for a cast the coordination gate is
        // going to refuse — that rotation is a whole tick under a facing lock.
        if (!_traffic.MayCommit(
                _context.Self.Position,
                _facts.StanceCycleTicks(route)))
        {
            return null;
        }
        int commit = ArcFacts.CommitTicks(route);
        int required = _facts.RequiredFanHits(route, _context.Self.FormId);
        ArcGun.Fan here = _gun.FanForecast(
            route.TargetFormId,
            _context.Self.Facing,
            _context.Self.Position,
            commit);
        int Value(ArcGun.Fan fan) =>
            fan.Bodies
            + fan.Supply
            + (ArcRules.FanExecutes ? fan.Kills : 0)
            + (ArcRules.ChannelersArePrey ? fan.OnPoint : 0);
        if (Value(here) >= required)
            return null;

        Direction? best = null;
        int bestValue = Value(here);
        foreach (Direction facing in ArcBoard.Cardinals)
        {
            if (facing == _context.Self.Facing)
                continue;
            ArcGun.Fan turned = _gun.FanForecast(
                route.TargetFormId,
                facing,
                _context.Self.Position,
                commit + 1);
            if (Value(turned) > bestValue)
            {
                bestValue = Value(turned);
                best = facing;
            }
        }
        return bestValue >= required ? best : null;
    }

    /// <summary>
    /// Raise a deflecting stance, when this chassis has one and a bolt is already
    /// on its way into the quadrant it would protect.
    ///
    /// <para>Dead code on a striker — no route out of a striker form leads to a
    /// guarding form, so this returns null before it reads anything else. It
    /// exists because the artifact is measured on chassis it was not written for,
    /// and because the guard is published as <c>projectileGuard</c> on a form
    /// rather than as a class name: the shield preserves objective weight, the
    /// quadrant is chosen before it rises and cannot be rotated afterwards, and
    /// the budget is deflections rather than attacks. All four of those are read
    /// here, so the same code raises a shield into an incoming poke and declines
    /// when the bolt is coming from the flank the shield does not cover.</para>
    /// </summary>
    public GenericActorDecision? TryGuard()
    {
        GenericActorRulesContract.FormTransition? route =
            _facts.GuardStanceRoute(_context.Self.FormId);
        if (route is null || ArcFacts.RouteHeld(_context, route))
            return null;

        int commit = ArcFacts.CommitTicks(route);
        // A shield is raised against a BEARING, never against a bolt, and the
        // contract is what says so: bolts advance two tiles per tick, so by the
        // time one is visible and inbound it is one tick out and a windup-1
        // shield cannot finish in front of it. Measured: gating this on an
        // arriving bolt raised the shell exactly zero times in sixteen matches.
        // The quadrant is chosen before the shield rises and cannot be turned
        // afterwards, so the question is which arc the loaded guns are in.
        if (_threat.Bearing(_context.Self.Position, _context.Self.Facing) == 0)
            return null;
        // Flank and rear contacts hurt normally, and the stance cannot move,
        // shoot or turn: a bolt already inbound from outside the guarded arc
        // makes this the worst tick of the match to become immobile.
        if (_threat.Incoming(_context.Self.Position) is { } bolt
            && bolt.Ticks <= commit
            && !_threat.ArrivesInQuadrant(
                _context.Self.Position,
                _context.Self.Facing))
        {
            return null;
        }
        if (!_facts.PlacementAllows(route, _context.Self.Position))
            return null;
        // C5: a guard cannot move, shoot or rotate and its budget is DEFLECTIONS
        // rather than ticks, so there is no upper bound on how long it stands
        // there. That makes a shell the most expensive thing this doctrine can
        // put in a doorway, and the same gate answers it.
        if (!_traffic.MayCommit(
                _context.Self.Position,
                _facts.StanceCycleTicks(route)))
        {
            return null;
        }

        GenericActorActionLegality? transform = _context.ActionLegalities
            .FirstOrDefault(candidate =>
                candidate.Available
                && string.Equals(
                    candidate.ActionId,
                    route.ActionId,
                    StringComparison.Ordinal));
        GenericActorActionLegality.ArgumentConstraint.FormTargetConstraint?
            forms = transform?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .FormTargetConstraint>()
                .SingleOrDefault();
        if (transform is null
            || forms is null
            || !forms.AllowedFormIds.Contains(
                route.TargetFormId,
                StringComparer.Ordinal))
        {
            return null;
        }
        return new GenericActorDecision(
            transform.ActionId,
            transform.ActionCode,
            [
                new GenericActorActionArgument.FormTargetArgument(
                    route.TargetFormId),
            ],
            $"raising {route.TargetFormId} into the arriving bolt");
    }

    /// <summary>
    /// A fortified route that trades objective weight away — the classless
    /// profile's anchor, and on a chassis that has one, the open game's turret.
    /// Taken only when this team's presence on the objective survives without
    /// this body, because a body that fortifies before relief exists has deleted
    /// its own scoring presence. The relief bar is lower when the contract
    /// declares the return route reversible: an unlimited cycle is a loan, a
    /// one-way commitment is a sale, and <c>irreversibleForLife</c> says which
    /// one this arm is selling.
    /// </summary>
    public GenericActorDecision? TryFortify(ArcKeel keel)
    {
        GenericActorRulesContract.FormTransition? route = _facts
            .RoutesFrom(_context.Self.FormId)
            .Where(candidate =>
                _facts.ObjectiveWeight(candidate.TargetFormId)
                    < _facts.ObjectiveWeight(_context.Self.FormId)
                && _facts.StanceBudget(candidate.TargetFormId) is null)
            .OrderBy(candidate => candidate.TransitionId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (route is null || ArcFacts.RouteHeld(_context, route))
            return null;

        bool reversible = _facts.ReturnRoute(route.TargetFormId) is { } back
            && !back.IrreversibleForLife;
        int weightWithoutMe = keel.OwnWeight
            - (keel.SelfPresent
                ? _facts.ObjectiveWeight(_context.Self.FormId)
                : 0);
        if (weightWithoutMe <= keel.EnemyWeight
            || _context.Allies.Length < (reversible ? 1 : 2)
            || _context.Enemies.IsEmpty
            || !_facts.PlacementAllows(route, _context.Self.Position)
            // C5: a fortified body is stationary for the rest of its life on a
            // one-way route and until it chooses to mobilize on a cycling one,
            // so it is the longest commitment in the contract. Fortifying
            // BEHIND RELIEF was already the rule; fortifying out of the relief's
            // WAY is the part wave 5 was missing.
            || !_traffic.MayCommit(
                _context.Self.Position,
                _facts.StanceCycleTicks(route))
            || _threat.Threatened(
                _context.Self.Position,
                ArcFacts.CommitTicks(route) + 1))
        {
            return null;
        }

        GenericActorActionLegality? transform = _context.ActionLegalities
            .FirstOrDefault(candidate =>
                candidate.Available
                && string.Equals(
                    candidate.ActionId,
                    route.ActionId,
                    StringComparison.Ordinal));
        GenericActorActionLegality.ArgumentConstraint.FormTargetConstraint?
            forms = transform?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .FormTargetConstraint>()
                .SingleOrDefault();
        if (transform is null
            || forms is null
            || !forms.AllowedFormIds.Contains(
                route.TargetFormId,
                StringComparer.Ordinal))
        {
            return null;
        }
        return new GenericActorDecision(
            transform.ActionId,
            transform.ActionCode,
            [
                new GenericActorActionArgument.FormTargetArgument(
                    route.TargetFormId),
            ],
            $"fortifying into {route.TargetFormId} behind relief");
    }

    private GenericActorActionLegality? Legality(
        GenericActorRulesContract.ActionKind kind)
    {
        HashSet<string> ids = _facts.Contract.Rules.Actions
            .Where(action => action.Kind == kind)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        return _context.ActionLegalities
            .Where(action => action.Available && ids.Contains(action.ActionId))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
