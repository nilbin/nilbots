using BotArena.Sdk;

/// <summary>
/// ADVANCING WALL, third lineage. The wall is not the tiles we stand on — it
/// is the set of straight lanes our guns close. A bulwark body's gun fires
/// along its facing, so a body one tile off its own axis is unarmed no matter
/// how much health it has left. That is revision 2 and it stands.
///
/// What revision 3 adds is a price list. Revision 2 was authored for a frontline
/// that always came back, where one tick of presence was worth the same as any
/// other and the only question was whether we survived to spend it. Three
/// declared policies each falsify that, and all three are contract reads:
///
///  - <c>capture.ratchetHoldTicks</c> — a hold makes taken ground keepable and
///    makes a capture completed inside someone else's hold worthless, so what a
///    claim is worth is a clock rather than a constant. <see cref="Pendulum"/>
///    infers whose hold is live and how much of it is left.
///  - <c>capture.controlPolicy</c> — a weight-scaled policy turns the objective
///    from a switch into an election, so a weighted body is a vote instead of a
///    redundancy, and a body walked off it is a vote withdrawn.
///  - <c>lifecycle.automaticReturnPlacement</c> — a forward rally lands returns
///    beside the fight, so presence is already redundant on its own clock and
///    the surplus body can become the best gun this class owns.
///
/// Each of the three moves exactly one existing rule, and each was measured
/// against the rebuilt revision 2 on the arm that declares it. What is NOT here
/// is as deliberate: three further readings of the same three fields were
/// implemented, measured as losses, and removed. They are recorded in DX.md
/// rather than silently dropped, because every one of them reads as obviously
/// right.
///
/// Everything below is resolved from <see cref="GenericActorMatchStart.Contract"/>
/// and the per-tick legality mask. When the contract declares no anchor route,
/// no mobilize route, no fabrication action, or no hold at all, the
/// corresponding doctrine step simply does not fire and the body falls back to
/// taking and holding ground.
/// </summary>
public sealed class MarchWall : IGenericActorBot
{
    private const int EndgameHoldWindow = 60;
    private const int FabricationPatienceTicks = 15;
    private const int PrimeReturnPatienceTicks = 12;
    private const int BlockedTileMemoryTicks = 6;
    private const int IdlePatienceTicks = 3;

    /// <summary>
    /// Idle patience inside our own live hold. The clock on protected ground
    /// runs whether or not anything happens on it, so a standoff we would
    /// normally sit through is a standoff we are paying for.
    /// </summary>
    private const int HeldGroundIdlePatienceTicks = 1;

    private readonly Dictionary<Position, int> _blockedUntilTick = [];
    private ContractView? _view;
    private Pendulum? _pendulum;
    private AnchorPlanner.Site? _plannedSite;
    private int _plannedSiteTick = -1;
    private Position? _dodgeOrigin;
    private int _avoidDodgeOriginThroughTick = -1;
    private int _companionReadySinceTick = -1;
    private int _enemyWeightedSeen;
    private int _idleTicks;

    public void StartLife(GenericActorMatchStart start)
    {
        _view = new ContractView(start);
        _pendulum = new Pendulum(_view);
        _blockedUntilTick.Clear();
        _plannedSite = null;
        _plannedSiteTick = -1;
        _dodgeOrigin = null;
        _avoidDodgeOriginThroughTick = -1;
        _companionReadySinceTick = -1;
        _enemyWeightedSeen = 0;
        _idleTicks = 0;
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        ContractView view = _view
            ?? throw new InvalidOperationException("StartLife was not called.");
        Pendulum pendulum = _pendulum
            ?? throw new InvalidOperationException("StartLife was not called.");
        pendulum.Observe(context);
        RememberBlockedTile(context);
        RememberEnemyStrength(view, context);
        RememberIdleness(view, context);

        // A committed same-life transition owns the tick; the declared pending
        // policy leaves nothing else legal.
        if (context.Self.PendingSameLifeTransition is not null)
            return Fallback(view, context, "committed to the transition windup");

        return view.IsFortified(context.Self.FormId)
            ? HoldTheWall(view, context)
            : March(view, context);
    }

    private Pendulum Clock =>
        _pendulum ?? throw new InvalidOperationException("StartLife was not called.");

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
    /// The wall advances by picking itself up. Two things call a segment back:
    /// the front moving out of its lanes, and the team running out of bodies
    /// that can take ground. The second is the one v1 never checked — a turret
    /// that cannot capture is worth nothing at all when it is the only thing
    /// left standing. When the contract declares no route back, the segment
    /// simply stands.
    /// </summary>
    private GenericActorDecision? TryMobilize(
        ContractView view,
        GenericActorContext context,
        IReadOnlyList<Position> objective)
    {
        GenericActorRulesContract.FormTransition? route =
            view.MobilizeRoute(context.Self.FormId);
        if (route is null)
            return null;

        int reach = view.Attack(context.Self.FormId)?.Projectile.MaxTravelTiles ?? 6;
        bool enemyClose = context.Enemies.Any(enemy =>
            Geometry.Chebyshev(enemy.Position, context.Self.Position) <= 2);

        // Nobody left who can score. A gun that cannot take ground is losing the
        // match every tick it stands there, so it stands up.
        if (WeightedBodies(view, context) == 0 && !enemyClose)
        {
            return Transform(
                view,
                context,
                route.TargetFormId,
                "no body left that can take ground; mobilizing");
        }

        // A turret that stands back up to return a vote to a weight-scaled
        // election reads well and was implemented here. It never once fired in
        // 64 measured matches — a fortified body's own sensors rarely reach the
        // active objective at all, so the presence read that would trigger it
        // is almost always empty — and an unmeasurable decision rule is not
        // worth shipping. Recorded in DX.md; the election is answered by mobile
        // bodies, which can see the tile they are voting on.
        if (objective.Count == 0)
            return null;
        if (Geometry.Coverage(view.IsWall, context.Self.Position, objective, reach) > 0)
            return null;

        // v1 also refused to stand up while any enemy was inside the turret's
        // own eight-tile reach, which on this map is most of the room: segments
        // were stranded behind a front that had already moved and spent the
        // rest of the match watching an empty lane. A fortified body is the
        // toughest thing we own; the bar for picking it up is that nothing is
        // in its face right now.
        if (context.Enemies.Any(enemy =>
                Geometry.Chebyshev(enemy.Position, context.Self.Position) <= 3))
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
        // rather than surrendering the tile. Leaving it is a wounded body's
        // move. Measured aside: pre-empting this dodge with a rotation onto the
        // lane reads well and loses — a gun on a three-tick cadence spends the
        // rotation, eats the bolt it did not step off, and is facing the wrong
        // way again by the time it can fire. Stepping onto the lane, below,
        // does the same job without giving up the dodge.
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

        // Ground first, then the duel. A qualification probe states the same
        // priority in one sentence — cross under fire and hold, rather than
        // stop in the approach and trade — and it is also how a wall advances:
        // the lane play below is for a body that has already taken its tile and
        // has nowhere better to be.
        GenericActorDecision? advance =
            MarchOrders(view, context, activeIndex, objective);
        if (advance is not null)
            return advance;

        GenericActorDecision? engage =
            FightOnTheAxis(view, context, objectiveTiles);
        if (engage is not null)
            return engage;

        return HoldTheLine(view, context, objective);
    }

    /// <summary>
    /// The revision, in one method. A straight-firing chassis has four rays; an
    /// enemy off all of them is unreachable, and v1's answer to that was to
    /// stand still and lose five health without firing a shot. So: while the
    /// health-and-cadence ledger favours us, spend the tick getting onto the
    /// shared lane — turn into it, or step onto a tile that has it. When the
    /// ledger has turned against us, spend the tick leaving the envelope that
    /// is beating us instead of standing in it.
    /// </summary>
    private GenericActorDecision? FightOnTheAxis(
        ContractView view,
        GenericActorContext context,
        HashSet<Position> objectiveTiles)
    {
        GenericActorRulesContract.AttackProfile? gun =
            view.Attack(context.Self.FormId);
        if (gun is null || context.Enemies.IsEmpty)
            return null;

        int reach = Math.Max(1, gun.Projectile.MaxTravelTiles);
        GenericActorContext.ObservedEnemyState? target =
            Prioritized(view, context, [.. objectiveTiles])
                .FirstOrDefault(enemy =>
                    Geometry.Chebyshev(enemy.Position, context.Self.Position)
                        <= reach + 2);
        if (target is null)
            return null;

        Direction[] order = Navigation.Order(view, context);

        // A losing ledger buys a way out, not a shrug. When the exchange is
        // unwinnable we try to leave first — but a longer gun with a bend
        // envelope covers most of the room, so "nowhere safe" is the normal
        // answer and standing in it is strictly the worst one. Waiting a
        // stalemate out is also refused explicitly: two bodies that cannot
        // reach each other contest the objective forever and neither scores,
        // and the durable chassis is the one that should force that open.
        //
        // The hold moves that bar both ways. Ground we are protected on is
        // ground we are being paid for by the tick, so a standoff on it is
        // opened almost at once. Ground inside an opposing hold pays us nothing
        // for a claim, only for the denial — and a mutual null IS the denial,
        // so there is nothing to force and no reason to buy the exchange.
        bool bankable = Clock.ClaimIsBankable(context);
        int patience = Clock.OurGroundIsSafe(context.Tick)
            ? HeldGroundIdlePatienceTicks
            : IdlePatienceTicks;
        bool forced = bankable && _idleTicks >= patience;
        if (!forced && !TradeFavoursUs(view, context, target))
        {
            GenericActorDecision? leave =
                BreakContact(view, context, target, objectiveTiles, order);
            if (leave is not null)
                return leave;
        }

        return CloseTheLane(view, context, target, objectiveTiles, order)
            ?? (forced
                ? Navigation.Toward(
                    view,
                    context,
                    [target.Position],
                    Avoided(context),
                    $"forcing the stalemate open against {target.ActorId}")
                : null);
    }

    private GenericActorDecision? CloseTheLane(
        ContractView view,
        GenericActorContext context,
        GenericActorContext.ObservedEnemyState target,
        HashSet<Position> objectiveTiles,
        Direction[] order)
    {
        string formId = context.Self.FormId;
        Position here = context.Self.Position;

        // Already on the lane. Either the gun is cycling — in which case holding
        // the tile IS the play — or a turn puts the target in front of it.
        Direction? fromHere = Lane.FacingThatCovers(
            view,
            formId,
            here,
            context.Self.Facing,
            target.Position,
            order);
        if (fromHere is Direction facing)
        {
            if (facing != context.Self.Facing)
            {
                GenericActorDecision? turn = Navigation.Face(
                    view,
                    context,
                    facing,
                    $"turning the gun onto {target.ActorId}");
                if (turn is not null)
                    return turn;
            }

            // The gun is cycling. Walking down our own lane keeps it and
            // shortens the bolt's flight, which is the one thing that makes a
            // slow gun harder to step off; standing still just donates the tick.
            GenericActorDecision? press = PressTheLane(
                view,
                context,
                target,
                facing);
            return press ?? Fallback(view, context, "holding the firing lane");
        }

        HashSet<Position> occupied = Navigation.Occupied(view, context);
        HashSet<Position> hostile =
            Lane.HostileReach(view, context.Enemies, immediate: false);
        int distanceHere = Geometry.Chebyshev(here, target.Position);

        // Cover is never traded for a lane. A tile that gun cannot reach is a
        // tile we have already won: an enemy parked just outside our range is
        // also parked just outside its own, and the step that fixes our
        // geometry fixes theirs for free. Hold, and make them come.
        bool sheltered = Exposure(view, target, here) == 0;

        Direction? best = null;
        int bestScore = int.MinValue;
        foreach (Direction direction in order)
        {
            Position destination = Geometry.Step(here, direction);
            if (view.IsWall(destination)
                || occupied.Contains(destination)
                || view.ReservedSpawnTiles.Contains(destination)
                || Threat.InDeclaredPath(view, context, destination))
            {
                continue;
            }
            if (sheltered && Exposure(view, target, destination) > 0)
                continue;

            Direction after = view.FacingAfterStep(
                formId,
                context.Self.Facing,
                direction);
            Direction? opens = Lane.FacingThatCovers(
                view,
                formId,
                destination,
                after,
                target.Position,
                order);
            if (opens is null)
                continue;

            int score =
                (opens == after ? 40 : 0)
                + (objectiveTiles.Contains(destination) ? 30 : 0)
                - (hostile.Contains(destination) ? 8 : 0)
                - Math.Abs(
                    Geometry.Chebyshev(destination, target.Position)
                    - distanceHere);
            if (score <= bestScore)
                continue;
            bestScore = score;
            best = direction;
        }

        return best is Direction chosen
            ? Navigation.Step(
                view,
                context,
                chosen,
                $"stepping onto the lane against {target.ActorId}")
            : null;
    }

    /// <summary>
    /// One step down our own lane, toward the body on the other end of it.
    /// </summary>
    private static GenericActorDecision? PressTheLane(
        ContractView view,
        GenericActorContext context,
        GenericActorContext.ObservedEnemyState target,
        Direction facing)
    {
        if (context.Self.Cooldown <= 0)
            return null;
        Position here = context.Self.Position;
        if (Geometry.Chebyshev(here, target.Position) <= 2)
            return null;

        Direction step = Navigation.Toward(here, target.Position);
        Position destination = Geometry.Step(here, step);
        if (view.IsWall(destination)
            || Navigation.Occupied(view, context).Contains(destination)
            || view.ReservedSpawnTiles.Contains(destination)
            || Threat.InDeclaredPath(view, context, destination))
        {
            return null;
        }

        Direction after =
            view.FacingAfterStep(context.Self.FormId, facing, step);
        return Lane.Covers(
            view,
            context.Self.FormId,
            destination,
            after,
            target.Position)
            ? Navigation.Step(
                view,
                context,
                step,
                $"pressing down the lane at {target.ActorId}")
            : null;
    }

    /// <summary>
    /// The ledger says we lose this exchange. Standing in a gun's envelope with
    /// no answer is how v1 fed its Prime to longer-ranged fire one hit at a
    /// time. A bend envelope covers too much of the room for "step outside it"
    /// to be a real option, so this asks the weaker question that has an answer:
    /// which neighbouring tile can that body reach from fewer of its facings,
    /// and does it keep us on the contested ground.
    /// </summary>
    private GenericActorDecision? BreakContact(
        ContractView view,
        GenericActorContext context,
        GenericActorContext.ObservedEnemyState target,
        HashSet<Position> objectiveTiles,
        Direction[] order)
    {
        Position here = context.Self.Position;
        int exposureHere = Exposure(view, target, here);
        if (exposureHere == 0)
            return null;

        int maxHealth = view.MaxHealth(context.Self.FormId);
        bool mayLeaveObjective = context.Self.Health * 2 <= maxHealth;
        HashSet<Position> occupied = Navigation.Occupied(view, context);

        Direction? best = null;
        int bestScore = 0;
        foreach (Direction direction in order)
        {
            Position destination = Geometry.Step(here, direction);
            if (view.IsWall(destination)
                || occupied.Contains(destination)
                || view.ReservedSpawnTiles.Contains(destination)
                || Threat.InDeclaredPath(view, context, destination))
            {
                continue;
            }
            if (objectiveTiles.Contains(here)
                && !mayLeaveObjective
                && !objectiveTiles.Contains(destination))
            {
                continue;
            }

            int exposure = Exposure(view, target, destination);
            if (exposure >= exposureHere)
                continue;

            int score =
                (exposureHere - exposure) * 20
                + (objectiveTiles.Contains(destination) ? 30 : 0)
                + Geometry.Chebyshev(destination, target.Position);
            if (score <= bestScore)
                continue;
            bestScore = score;
            best = direction;
        }

        return best is Direction chosen
            ? Navigation.Step(
                view,
                context,
                chosen,
                $"breaking contact with {target.ActorId}")
            : null;
    }

    /// <summary>
    /// How many of a body's four facings put a bolt on one tile. Zero is cover;
    /// one is a lane it has to commit a rotation to; four is open ground.
    /// </summary>
    private static int Exposure(
        ContractView view,
        GenericActorContext.ObservedEnemyState enemy,
        Position tile) =>
        Geometry.Cardinals.Count(facing =>
            Lane.Covers(view, enemy.FormId, enemy.Position, facing, tile));

    /// <summary>
    /// Ticks to kill, both ways, from declared health, damage and cadence. A
    /// bulwark's whole case for a mutual lane is that this number favours it;
    /// when it stops doing so the lane is a losing tile and nothing else.
    /// </summary>
    private static bool TradeFavoursUs(
        ContractView view,
        GenericActorContext context,
        GenericActorContext.ObservedEnemyState target)
    {
        GenericActorRulesContract.AttackProfile? mine =
            view.Attack(context.Self.FormId);
        if (mine is null)
            return false;
        GenericActorRulesContract.AttackProfile? theirs =
            view.Attack(target.FormId);
        if (theirs is null)
            return true;

        int ours = TicksToKill(mine, target.Health);
        int helpers = context.Allies.Count(ally =>
            view.Attack(ally.FormId) is not null
            && Lane.FacingThatCovers(
                view,
                ally.FormId,
                ally.Position,
                ally.Facing,
                target.Position,
                Geometry.Cardinals) is not null);
        if (helpers > 0)
            ours /= helpers + 1;

        // A near-even race is a race the durable class takes: our body returns
        // on a declared clock and theirs has to be re-earned, and a refused
        // exchange on contested ground scores nothing for anybody. Pricing the
        // two returns against each other instead — declared delay plus the walk
        // back from the declared arrival — reads well and was measured as a
        // loss on every arm; see DX.md.
        return ours * 4 <= TicksToKill(theirs, context.Self.Health) * 5;
    }

    private static int TicksToKill(
        GenericActorRulesContract.AttackProfile gun,
        int health)
    {
        int damage = Math.Max(1, gun.Projectile.DamagePerHit);
        int hits = (health + damage - 1) / damage;
        return hits * Math.Max(1, gun.CooldownTicks);
    }

    private GenericActorDecision? MarchOrders(
        ContractView view,
        GenericActorContext context,
        int activeIndex,
        IReadOnlyList<Position> objective)
    {
        IEnumerable<Position> avoid = Avoided(context);

        // Approach discipline: while a body is outside its own reach it gains
        // nothing from a lane and can only be shot down one, so tiles inside a
        // longer gun's envelope are a soft penalty on the route. Inside our own
        // reach the penalty lifts, because that is where we want the exchange.
        int reach =
            view.Attack(context.Self.FormId)?.Projectile.MaxTravelTiles ?? 0;
        GenericActorContext.ObservedEnemyState[] distant = context.Enemies
            .Where(enemy =>
                Geometry.Chebyshev(enemy.Position, context.Self.Position) > reach)
            .ToArray();
        // Caution is priced against what it protects, and on ground we are
        // protected on it protects nothing: an advance completed inside our own
        // hold cannot be undone, so the ticks of the hold are exactly the ticks
        // to spend crossing. The soft penalty comes off entirely there.
        HashSet<Position> approach =
            distant.Length == 0 || Clock.OurGroundIsSafe(context.Tick)
                ? []
                : Lane.HostileReach(view, distant, immediate: false);

        GenericActorRulesContract.FormTransition? anchor =
            view.AnchorRoute(context.Self.FormId);
        if (anchor is not null && !ElectionNeedsThisBody(view, context))
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
                    "marching to the choke to extend the wall",
                    approach);
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
                "taking the contested position",
                approach);
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
                "closing on the nearest enemy",
                approach);
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
            foreach (Direction direction in Navigation.Order(view, context))
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

    /// <summary>
    /// Fortification is rationed by presence. A turret is the best gun this
    /// chassis owns and objective weight zero, which means every anchor trades
    /// a scoring body for a denying one. v1 let a companion anchor whenever any
    /// other weighted ally existed, and duly spent both of them: the wall held
    /// ground it could no longer take. The rule now is that the team keeps at
    /// least one body that can capture, and never more guns than scorers —
    /// except with a lead already banked, where denial IS the win condition.
    /// </summary>
    private bool FortifyPermitted(
        ContractView view,
        GenericActorContext context,
        AnchorPlanner.Site site)
    {
        int weightedAfter = WeightedBodies(view, context) - 1;
        int turretsAfter = 1 + context.Allies.Count(ally =>
            view.ObjectiveWeight(ally.FormId) == 0);
        int push = SignedPush(view, context);
        bool endgameLead =
            context.Tick >= view.MaxTicks - EndgameHoldWindow && push > 0;

        if (weightedAfter < 1 && !endgameLead)
            return false;

        // The ration itself. Every anchor trades a scoring body for a denying
        // one; what that trade costs is declared rather than habitual.
        //
        //  - The election. Under a weight-scaled control policy a body on the
        //    objective IS capture pressure, so the roster must still match what
        //    the other side has shown us. Under a binary policy it is only
        //    insurance against the one body being killed or displaced — and
        //    when arrivals rally onto our own-side objective that insurance is
        //    already written by the return clock, so the surplus body is free
        //    to become the best gun this class owns.
        int needed = view.SurplusWeightScalesGain || !view.ArrivalsRallyForward
            ? Math.Max(1, _enemyWeightedSeen)
            : 1;

        if (!view.IsPrimeSlot)
        {
            // Never fewer capturing bodies than the arm says we need, and never
            // more guns than scorers. A lead already banked is the one case
            // where denial alone wins, and there the ration lifts.
            bool matching = weightedAfter >= needed;
            if (!endgameLead && (!matching || turretsAfter > weightedAfter))
                return false;
            if (site.Coverage < 1)
                return false;
            if (context.Allies.Any(ally => view.ObjectiveWeight(ally.FormId) > 0))
                return true;
            return context.TeamUnits.Any(slot =>
                slot.State is GenericActorContext.UnitSlotState
                        .AutomaticReturnPending pending
                    && pending.DueTick <= context.Tick + PrimeReturnPatienceTicks);
        }

        if (site.Coverage < 2)
            return false;
        int maxHealth = view.MaxHealth(context.Self.FormId);
        if (context.Self.Health * 2 < maxHealth)
            return false;

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
    /// Under a weight-scaled control policy the objective is an election and
    /// the weighted bodies standing on it are the votes: being outweighed does
    /// not merely null the tick, it erodes a claim we already own. A body that
    /// walks off to extend the wall while the count is level or against us is a
    /// vote withdrawn. Under a binary policy the same walk is free, because the
    /// second body on the tile was never adding to the claim — which is exactly
    /// why one doctrine cannot answer this without reading the policy.
    /// </summary>
    private static bool ElectionNeedsThisBody(
        ContractView view,
        GenericActorContext context)
    {
        int weight = view.ObjectiveWeight(context.Self.FormId);
        if (!view.SurplusWeightScalesGain || weight <= 0)
            return false;
        (int own, int enemy, bool selfPresent) =
            ArenaBasics.ObjectivePresence(view.Contract, context);
        int withoutUs = own - (selfPresent ? weight : 0);
        return enemy > 0 && withoutUs <= enemy;
    }

    /// <summary>Bodies on our side that can still take and hold ground.</summary>
    private static int WeightedBodies(
        ContractView view,
        GenericActorContext context) =>
        (view.ObjectiveWeight(context.Self.FormId) > 0 ? 1 : 0)
        + context.Allies.Count(ally => view.ObjectiveWeight(ally.FormId) > 0);

    /// <summary>
    /// Consecutive ticks this body spent doing nothing. Two bodies that cannot
    /// reach each other on the same objective is a real and stable state of
    /// this rule set — control is contested, progress decays to zero, and the
    /// match runs out the clock. Counting it is how the doctrine notices.
    /// </summary>
    private void RememberIdleness(
        ContractView view,
        GenericActorContext context)
    {
        HashSet<string> waitIds =
            view.ActionIds(GenericActorRulesContract.ActionKind.Wait);
        bool waited =
            context.Self.PreviousActionResolution is { } previous
            && waitIds.Contains(previous.AcceptedAction.ActionId);
        _idleTicks = waited ? _idleTicks + 1 : 0;
    }

    private void RememberEnemyStrength(
        ContractView view,
        GenericActorContext context)
    {
        int weighted = context.Enemies
            .Where(enemy => view.ObjectiveWeight(enemy.FormId) > 0)
            .Select(enemy => enemy.ActorId)
            .Distinct()
            .Count();
        if (weighted > _enemyWeightedSeen)
            _enemyWeightedSeen = weighted;
    }

    /// <summary>
    /// Step off the bolt that lands this tick. The wall does not retreat from
    /// ground it holds: while the batch is survivable a body on the objective
    /// only sidesteps inside the contested region, and absorbs the hit when
    /// there is nowhere in it to stand. Among equally safe tiles it prefers one
    /// that leaves the gun pointing somewhere useful.
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

        HashSet<Position> occupied = Navigation.Occupied(view, context);
        HashSet<Position> bolts = Threat.BoltTiles(context);
        HashSet<Position> corridor = Threat.Sweep(view, context, 2);
        bool holding = objectiveTiles.Contains(context.Self.Position);
        int here = Threat.Hits(view, context, context.Self.Position, 1);
        GenericActorContext.ObservedEnemyState? target =
            Prioritized(view, context, [.. objectiveTiles]).FirstOrDefault();

        Direction? best = null;
        int bestScore = int.MinValue;
        foreach (Direction direction in Navigation.Order(view, context))
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

            bool opensLane = target is not null
                && Lane.FacingThatCovers(
                    view,
                    context.Self.FormId,
                    destination,
                    view.FacingAfterStep(
                        context.Self.FormId,
                        context.Self.Facing,
                        direction),
                    target.Position,
                    Geometry.Cardinals) is not null;

            int score = -threat * 100
                + (objectiveTiles.Contains(destination) ? 40 : 0)
                + (opensLane ? 25 : 0)
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
    /// satisfied from this tick's mask. The shared helper covers the first two
    /// cases; the argument synthesis below is the last resort under a catalog
    /// that declares no wait at all.
    /// </summary>
    private static GenericActorDecision Fallback(
        ContractView view,
        GenericActorContext context,
        string reason)
    {
        HashSet<string> waitIds =
            view.ActionIds(GenericActorRulesContract.ActionKind.Wait);
        if (context.ActionLegalities.Any(action =>
                waitIds.Contains(action.ActionId)
                || string.Equals(action.ActionId, "wait", StringComparison.Ordinal)))
        {
            return ArenaBasics.Wait(context, reason);
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

        return ArenaBasics.Wait(context, reason);
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
