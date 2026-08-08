using BotArena.Sdk;

/// <summary>
/// ADVANCING WALL, fourth lineage. The wall is not the tiles we stand on — it
/// is the set of straight lanes our guns close. A bulwark body's gun fires along
/// its facing, so a body one tile off its own axis is unarmed no matter how much
/// health it has left. That is revision 2, and revision 3 priced the pendulum on
/// top of it: a hold clock, a weight-scaled election, and a forward rally each
/// decide what a tick of presence is worth. Both still stand.
///
/// What revision 4 adds is the KIT, and one sentence carries it: A LANE WE ARE
/// LOSING IS A LANE WE CAN TURN AROUND. Revision 3 had exactly two answers to a
/// duel it was losing — walk out of the envelope, or stand in it and be shot —
/// and its own notes record that standing was measured as a loss twice. The
/// declared shield is a third answer that did not exist before: contacts
/// arriving inside a guarded arc die and are RELAUNCHED from our tile along the
/// exactly reversed heading under our own team's ownership. So the doctrine
/// spends the kit on three decisions, each a contract read:
///
///  - <b>Raise</b> (<see cref="TryShield"/>): a batch we cannot dodge, or a lane
///    whose ledger has turned against us, becomes a shield instead of a wound —
///    but only where the route's own forbidden tile tags allow it, and only when
///    the ground is not thereby conceded.
///  - <b>Cycle</b> (<see cref="HoldTheShield"/>): the shield is dropped the tick
///    it stops paying — flanked, quiet, or needed as objective weight. Its
///    declared budget (<c>automaticReturn</c>, three deflections) is spent in
///    full when bolts keep coming, because the forced return costs the same
///    windup as our own.
///  - <b>Never poke an arc</b> (<see cref="Solutions"/>,
///    <see cref="FacingThatBeatsTheArc"/>): a shot that would arrive inside an
///    enemy guard is not a shot, it is a bolt handed to the enemy. Fire control
///    filters by ARRIVAL heading, so a bend — which changes where a bolt arrives
///    from without changing where it was fired — is what beats a locked arc, and
///    the flank is preferred to feeding a break.
///
/// Three revision-3 mechanical failures are repaired, all of them contract
/// reads that had drifted into assumptions: a parameterless same-life route
/// (<see cref="StartRoute"/>) is now startable, which is the only way a
/// fortified body ever stands up in a class arm; a bolt's cadence and damage are
/// read per projectile (<see cref="Threat"/>) instead of assumed to be one tick
/// and one health; and the ratchet hold is READ from the observation
/// (<see cref="Pendulum"/>) instead of inferred, which retires the blind spot
/// revision 3 documented at length.
///
/// Everything below is resolved from <see cref="GenericActorMatchStart.Contract"/>
/// and the per-tick legality mask. When the contract declares no anchor route,
/// no stance route, no fabrication action, or no hold at all, the corresponding
/// doctrine step does not fire and the body falls back to taking and holding
/// ground — which is what makes one artifact play the kit-off and kit-on arms.
/// </summary>
public sealed class MarchWall : IGenericActorBot
{
    private const int EndgameHoldWindow = 60;
    private const int FabricationPatienceTicks = 15;
    private const int PrimeReturnPatienceTicks = 12;
    private const int BlockedTileMemoryTicks = 6;
    private const int IdlePatienceTicks = 3;

    /// <summary>
    /// Ticks a raised shield is kept while nothing arrives inside its arc. A
    /// shield is not a posture, it is a reply; when there is nothing to reply to
    /// it is a body that cannot move, shoot, rotate or capture.
    /// </summary>
    private const int ShieldQuietPatienceTicks = 3;

    /// <summary>
    /// How far ahead a raise looks for bolts it could still turn. The shield
    /// completes at the end of the tick it is requested, AFTER combat, so a bolt
    /// landing this tick lands on a mobile body; only arrivals from the next tick
    /// on are deflectable.
    /// </summary>
    private const int ShieldHorizonTicks = 4;

    /// <summary>
    /// Idle patience inside our own live hold. The clock on protected ground
    /// runs whether or not anything happens on it, so a standoff we would
    /// normally sit through is a standoff we are paying for.
    /// </summary>
    private const int HeldGroundIdlePatienceTicks = 1;

    private readonly Dictionary<Position, int> _blockedUntilTick = [];
    private readonly HashSet<string> _countedDeflections = [];
    private ContractView? _view;
    private Pendulum? _pendulum;
    private AnchorPlanner.Site? _plannedSite;
    private int _plannedSiteTick = -1;
    private Position? _dodgeOrigin;
    private int _avoidDodgeOriginThroughTick = -1;
    private int _companionReadySinceTick = -1;
    private int _enemyWeightedSeen;
    private int _idleTicks;
    private string? _formId;
    private int _deflections;
    private int _quietStanceTicks;

    public void StartLife(GenericActorMatchStart start)
    {
        _view = new ContractView(start);
        _pendulum = new Pendulum(_view);
        _blockedUntilTick.Clear();
        _countedDeflections.Clear();
        _plannedSite = null;
        _plannedSiteTick = -1;
        _dodgeOrigin = null;
        _avoidDodgeOriginThroughTick = -1;
        _companionReadySinceTick = -1;
        _enemyWeightedSeen = 0;
        _idleTicks = 0;
        _formId = null;
        _deflections = 0;
        _quietStanceTicks = 0;
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        ContractView view = _view
            ?? throw new InvalidOperationException("StartLife was not called.");
        Pendulum pendulum = _pendulum
            ?? throw new InvalidOperationException("StartLife was not called.");
        pendulum.Observe(context);
        RememberForm(context);
        RememberDeflections(context);
        RememberBlockedTile(context);
        RememberEnemyStrength(view, context);
        RememberIdleness(view, context);

        // A committed same-life transition owns the tick; the declared pending
        // policy leaves nothing else legal.
        if (context.Self.PendingSameLifeTransition is not null)
            return Fallback(view, context, "committed to the transition windup");

        // Three shapes, told apart by what the form DECLARES rather than by any
        // name: a guarding form has no gun and answers with its arc; an immobile
        // form with a gun is an emplacement (a turret, or a fan mid-cast); and
        // anything that can move is marching.
        if (view.HasGuard(context.Self.FormId))
            return HoldTheShield(view, context);
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
        Dictionary<Position, FireControl.Shot> shots = Solutions(view, context);

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
            return StartRoute(
                view,
                context,
                route,
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

        return StartRoute(
            view,
            context,
            route,
            "front has moved on; mobilizing to re-anchor");
    }

    // ---------------------------------------------------------------- shield

    /// <summary>
    /// A raised shield, held or dropped. The form declares no attack profile and
    /// no movement, so there are exactly two decisions here: stay, or spend the
    /// return route's one-tick windup to become a body again.
    ///
    /// <para>Three things end a shield, and all three are read rather than
    /// timed. It is <b>flanked</b>: a body with a lane on our tile that the arc
    /// does not cover, which is the one situation in which the shield is strictly
    /// worse than being able to move. It is <b>quiet</b>: nothing has arrived
    /// inside the arc for <see cref="ShieldQuietPatienceTicks"/> ticks, so we are
    /// paying a body for a reply nobody is asking for. Or the objective needs
    /// <b>weight</b>: an enemy stands on the active objective with nothing of
    /// ours on it, which under a weight-scaled control policy is a claim being
    /// built at our expense.</para>
    ///
    /// <para>What does NOT end it is the budget. The declared
    /// <c>automaticReturn</c> spends itself on the third deflection and the
    /// engine's return costs the same windup our own would, so leaving early to
    /// "save" a deflection buys nothing and forfeits a bolt we could have sent
    /// back.</para>
    /// </summary>
    private GenericActorDecision HoldTheShield(
        ContractView view,
        GenericActorContext context)
    {
        GenericActorRulesContract.FormTransition? exit =
            Stance.ReturnRoute(view, context.Self.FormId);
        List<Threat.Inbound> inArc = InArc(view, context, minimumArrival: 1);
        bool arcIsWorking =
            inArc.Count > 0
            || context.Enemies.Any(enemy => AimsIntoOurArc(view, context, enemy));
        _quietStanceTicks = arcIsWorking ? 0 : _quietStanceTicks + 1;

        bool flanked = context.Enemies.Any(enemy =>
            !AimsIntoOurArc(view, context, enemy)
            && Lane.Covers(
                view,
                enemy.FormId,
                enemy.Position,
                enemy.Facing,
                context.Self.Position));

        (int ownWeight, int enemyWeight, _) =
            ArenaBasics.ObjectivePresence(view.Contract, context);
        bool weightIsNeeded = enemyWeight > 0 && ownWeight == 0;

        // The gun is the reason this chassis is on the board, so a cadence that
        // has come back is the shield's cue to leave — but only once the arc has
        // nothing left to answer. Dropping while a body is still aimed at us is
        // the one way to make the shield strictly worse than never raising it: we
        // pay the two windups AND eat the bolt.
        bool gunIsReady = !arcIsWorking && context.Self.Cooldown <= 1;

        if (exit is not null
            && (flanked
                || weightIsNeeded
                || gunIsReady
                || _quietStanceTicks >= ShieldQuietPatienceTicks))
        {
            GenericActorDecision? drop = StartRoute(
                view,
                context,
                exit,
                flanked
                    ? "flanked past the arc; dropping the shield"
                    : weightIsNeeded
                        ? "the objective needs weight; dropping the shield"
                        : gunIsReady
                            ? "cadence is back; dropping the shield to shoot"
                            : "nothing left for the arc to answer; dropping the shield");
            if (drop is not null)
                return drop;
        }

        return Fallback(
            view,
            context,
            $"holding the arc ({_deflections} returned)");
    }

    /// <summary>
    /// The three cases worth a shield, and nothing else. Each is a separate
    /// question about what the tick would otherwise be spent on, so each is
    /// measured on its own.
    /// </summary>
    private enum Shield
    {
        /// <summary>
        /// A batch that kills us and nowhere to step. Unconditional: a dead body
        /// holds no ground, and every other consideration is downstream of being
        /// alive.
        /// </summary>
        Brace,

        /// <summary>
        /// Somebody else is standing on the objective earning the claim, and fire
        /// is coming through us. A shield here is a SCREEN: bolts stop on the
        /// first enemy body in their path, and this one sends them back down the
        /// lane while the claim keeps building. This is the case the kit created —
        /// a body off the contested tiles that is nonetheless contributing.
        /// </summary>
        Screen,

        /// <summary>
        /// The shield rides the cooldown. This gun fires once every three ticks;
        /// the shield costs one tick to raise and one to drop, and the declared
        /// cooldown continuity is <c>preserve-remaining-ticks</c>, so those two
        /// ticks are spent out of a cadence that was not using them anyway. That
        /// is the whole economic case for the kit on this chassis: while the gun
        /// is cycling, a bolt aimed at us can be sent back for free.
        /// </summary>
        Cycle,

        /// <summary>
        /// A lane whose ledger has turned against us, where revision 3 could only
        /// walk away or be shot.
        /// </summary>
        Standoff,
    }

    /// <summary>
    /// Raise the shield. Every condition is a contract read, and the order
    /// matters: the route must exist, the TILE must allow it — on this map the
    /// route's own <c>transition-placement-forbidden</c> tags cover every
    /// objective tile and the whole central corridor, so a shield is never a way
    /// to hold contested ground — and the threat must arrive inside the arc we
    /// already face, because a shield cannot rotate and the arc is chosen by the
    /// facing we hold when we ask.
    /// </summary>
    private GenericActorDecision? TryShield(
        ContractView view,
        GenericActorContext context,
        GenericActorContext.ObservedEnemyState? threat,
        Shield why)
    {
        GenericActorRulesContract.FormTransition? route =
            Stance.GuardRoute(view, context.Self.FormId);
        if (route is null)
            return null;
        if (view.AnchorForbiddenTiles(route).Contains(context.Self.Position))
            return null;
        if (!view.AnchorTileSatisfiesRequirements(route, context.Self.Position))
            return null;

        // The shield completes after this tick's combat, so a bolt landing now is
        // not a bolt it can turn. Only arrivals from the next tick on count —
        // which, at the range this chassis actually duels at, is almost never a
        // bolt already in the air. A gun two tiles away launches one tile from
        // its own tile and sweeps the rest on its next advance, so the bolt is
        // visible for exactly one tick before it lands and the shield is a tick
        // late every time.
        //
        // So the trigger that matters is the one BEFORE the shot: a body whose
        // lane already covers our tile from inside the arc we face. That is not a
        // prediction about its intentions, it is a statement about geometry — it
        // is armed, aimed, and we cannot be hit from there by anything the arc
        // does not cover.
        List<Threat.Inbound> inArc = InArc(view, context, minimumArrival: 2);
        int arriving = inArc.Sum(bolt => bolt.Damage);
        bool aimed = threat is not null
            ? AimsIntoOurArc(view, context, threat)
            : context.Enemies.Any(enemy =>
                AimsIntoOurArc(view, context, enemy));
        if (arriving == 0 && !aimed)
            return null;

        // One guard shared by every case that is not survival: a shield spends
        // two ticks of tempo, and tempo is exactly what a team cannot spend while
        // the opposition stands alone on the objective earning a claim.
        if (why != Shield.Brace)
        {
            (int own, int enemy, _) =
                ArenaBasics.ObjectivePresence(view.Contract, context);
            if (enemy > 0 && own == 0)
                return null;
        }

        switch (why)
        {
            case Shield.Brace when arriving < context.Self.Health:
                return null;
            case Shield.Screen when arriving == 0 && !aimed:
                return null;
            case Shield.Cycle:
                // Only while the gun could not have fired anyway: the raise and
                // the drop have to come out of the cadence, not out of a shot.
                if (context.Self.Cooldown < 2)
                    return null;
                break;
            case Shield.Standoff:
                // A gun that can fire this tick should fire; the shield is for
                // the ticks a three-tick cadence leaves us holding a tile with
                // nothing to do but be shot at.
                if (context.Self.Cooldown <= 0
                    && threat is not null
                    && Solutions(view, context).ContainsKey(threat.Position))
                {
                    return null;
                }
                if (WeightedBodies(view, context) - 1 < 1)
                    return null;
                break;
            default:
                break;
        }

        return StartRoute(
            view,
            context,
            route,
            why switch
            {
                Shield.Brace => "raising the shield into a batch we cannot dodge",
                Shield.Screen =>
                    "screening the lane while the objective is held",
                Shield.Cycle => "riding the cooldown behind the shield",
                _ => "raising the shield on a lane we were losing",
            });
    }

    /// <summary>
    /// Enter a fan stance when the contract grants one. The fan aims by facing
    /// and is returned by its own budget the tick it fires, so the only decision
    /// is whether a target is already in front of us — there is no exit to
    /// author and no way to squat in the stance.
    ///
    /// <para>This chassis owns no fan, so this returns null on every arm it
    /// plays; it exists because the same stance machinery is what a fan needs and
    /// a contract-driven artifact should not care which class it is handed.</para>
    /// </summary>
    private GenericActorDecision? TryCast(
        ContractView view,
        GenericActorContext context,
        IReadOnlyList<Position> objective)
    {
        GenericActorRulesContract.FormTransition? route =
            Stance.VolleyRoute(view, context.Self.FormId);
        if (route is null)
            return null;
        if (view.AnchorForbiddenTiles(route).Contains(context.Self.Position))
            return null;
        if (!view.AnchorTileSatisfiesRequirements(route, context.Self.Position))
            return null;

        GenericActorRulesContract.AttackProfile? fan =
            view.Attack(route.TargetFormId);
        if (fan is null)
            return null;
        int reach = Math.Max(1, fan.Projectile.MaxTravelTiles);
        int windup = Math.Max(1, route.Windup.DurationTicks);

        // The windup is spent as a targetable body that may only wait, so a
        // batch that would land inside it cancels the cast rather than paying
        // for it.
        if (Threat.Damage(view, context, context.Self.Position, windup + 1)
            >= context.Self.Health)
        {
            return null;
        }

        // Worth a cast when the fan's lane already holds a body, and worth more
        // when it holds several — the extra bolts are the whole reason to stand
        // still for two ticks.
        int covered = Prioritized(view, context, objective)
            .Count(enemy =>
                Geometry.Chebyshev(enemy.Position, context.Self.Position) <= reach
                && Lane.Covers(
                    view,
                    route.TargetFormId,
                    context.Self.Position,
                    context.Self.Facing,
                    enemy.Position));
        return covered == 0
            ? null
            : StartRoute(view, context, route, "casting the fan down our lane");
    }

    /// <summary>
    /// Hostile bolts arriving at our tile inside the arc our current facing
    /// guards, no sooner than <paramref name="minimumArrival"/> ticks from now.
    /// </summary>
    private static List<Threat.Inbound> InArc(
        ContractView view,
        GenericActorContext context,
        int minimumArrival) =>
        Threat
            .Incoming(view, context, context.Self.Position, ShieldHorizonTicks)
            .Where(bolt =>
                bolt.TicksUntilArrival >= minimumArrival
                && Stance.GuardsAgainst(context.Self.Facing, bolt.Heading))
            .ToList();

    /// <summary>
    /// True when a body is aimed at our tile and EVERY heading it could arrive
    /// on is inside the arc we guard. This is the pre-shot question — the bolt
    /// does not exist yet — so it is asked of the shooter's declared envelope
    /// from its own pose rather than of a projectile.
    ///
    /// <para>The quantifier is the point, and it is the defensive half of the
    /// curve grammar. Against a straight-only gun the only arrival is the bearing
    /// between the two tiles, so this is exactly "it is aimed at us". Under a
    /// universal bend envelope the same body can put a bolt on our tile arriving
    /// from a bearing the arc does not cover — a bend goes AROUND an arc, which is
    /// what makes the arc worth less in the bend arm than without it — and a
    /// shield raised into that is two windups spent to be hit anyway. So the
    /// shield rises only where the geometry leaves the shooter no way out of the
    /// arc.</para>
    /// </summary>
    private static bool AimsIntoOurArc(
        ContractView view,
        GenericActorContext context,
        GenericActorContext.ObservedEnemyState enemy)
    {
        HashSet<ProjectileHeading> arrivals = Lane.Arrivals(
            view,
            enemy.FormId,
            enemy.Position,
            enemy.Facing,
            context.Self.Position);
        return arrivals.Count > 0
            && arrivals.All(heading =>
                Stance.GuardsAgainst(context.Self.Facing, heading));
    }

    // ---------------------------------------------------------------- mobile

    private GenericActorDecision March(
        ContractView view,
        GenericActorContext context)
    {
        int activeIndex = ActiveIndex(context);
        IReadOnlyList<Position> objective = view.ObjectiveTiles(activeIndex);
        HashSet<Position> objectiveTiles = objective.ToHashSet();
        Dictionary<Position, FireControl.Shot> shots = Solutions(view, context);
        int incoming = Threat.Damage(view, context, context.Self.Position, 1);

        // Bulwark bodies absorb; they step aside only from a killing batch — and
        // when there is nowhere to step, the shield is the step. Note the order:
        // a bolt that lands THIS tick cannot be deflected, so the shield here is
        // answering the rest of the batch, not the bolt that forced the question.
        if (incoming >= context.Self.Health)
        {
            GenericActorDecision? escape = Evade(
                view,
                context,
                objectiveTiles,
                allowLeavingObjective: true);
            if (escape is not null)
                return escape;
            GenericActorDecision? brace =
                TryShield(view, context, null, Shield.Brace);
            if (brace is not null)
                return brace;
        }

        GenericActorDecision? build = TryFabricate(view, context, objective);
        if (build is not null)
            return build;

        GenericActorDecision? fortify =
            TryAnchor(view, context, activeIndex, objective);
        if (fortify is not null)
            return fortify;

        GenericActorDecision? cast = TryCast(view, context, objective);
        if (cast is not null)
            return cast;

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

        // The cheapest shield there is: a bolt we can turn, arriving while the
        // gun is mid-cadence anyway.
        GenericActorDecision? cycle =
            TryShield(view, context, null, Shield.Cycle);
        if (cycle is not null)
            return cycle;

        // The screen. A body off the contested tiles whose ally is on them is
        // not choosing between crossing and trading — it is choosing between
        // eating a bolt on the way and sending it back. Asked here, before the
        // march, because a body that has already stepped is a body the lane has
        // already taxed.
        GenericActorDecision? screen = OwnWeightOnObjective(view, context) > 0
            ? TryShield(view, context, null, Shield.Screen)
            : null;
        if (screen is not null)
            return screen;

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
            // The kit's contribution to this exact decision. Revision 3 had two
            // answers to a lane it was losing and its own notes measured the
            // second one as a loss; a shield is the third, and it is tried first
            // because it keeps the tile AND sends the bolt back.
            GenericActorDecision? shield =
                TryShield(view, context, target, Shield.Standoff);
            if (shield is not null)
                return shield;

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
        Direction? fromHere = FacingThatBeatsTheArc(
            view,
            formId,
            here,
            context.Self.Facing,
            target,
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
            Direction? opens = FacingThatBeatsTheArc(
                view,
                formId,
                destination,
                after,
                target,
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
        return FacingThatBeatsTheArc(
            view,
            context.Self.FormId,
            destination,
            after,
            target,
            [after]) is not null
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
            && FacingThatBeatsTheArc(
                view,
                ally.FormId,
                ally.Position,
                ally.Facing,
                target,
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
                if (!Solutions(view, context, direction)
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
        if (Threat.Damage(view, context, context.Self.Position, windup + 1)
            >= context.Self.Health)
        {
            return null;
        }

        return StartRoute(
            view,
            context,
            route,
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

        // A ration cannot honestly demand more scorers than the other side is
        // able to put on the board at once, and how many that is comes from the
        // topology's own slot roster rather than from three. An asymmetric-slot
        // arm hands one side five: reading the count keeps the ration meaningful
        // there instead of unsatisfiable.
        if (view.OpposingSlotCount > 0)
            needed = Math.Min(needed, view.OpposingSlotCount);

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

    /// <summary>
    /// Objective weight our side currently has on the active objective, this body
    /// included. Read through the shared helper so a weight-zero form is counted
    /// as what it is: a gun that holds nothing.
    /// </summary>
    private static int OwnWeightOnObjective(
        ContractView view,
        GenericActorContext context)
    {
        (int own, _, _) = ArenaBasics.ObjectivePresence(view.Contract, context);
        return own;
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
        int here = Threat.Damage(view, context, context.Self.Position, 1);
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

            int threat = Threat.Damage(view, context, destination, 1);
            if (threat >= here)
                continue;

            bool opensLane = target is not null
                && FacingThatBeatsTheArc(
                    view,
                    context.Self.FormId,
                    destination,
                    view.FacingAfterStep(
                        context.Self.FormId,
                        context.Self.Facing,
                        direction),
                    target,
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

        // A visible tile now publishes the lifecycle claim standing on it. A
        // pending claim is a body about to exist there, either side's, so it is a
        // tile to route around rather than one to discover by being blocked.
        // (The permanent automatic-return anchors are already known from the
        // contract; this adds the queued ones, which no contract read can date.)
        foreach (GenericActorContext.ObservedTile visible in context.VisibleTiles)
        {
            if (visible.SpawnReservation is
                {
                    Kind: not GenericActorContext.SpawnReservationKind
                        .AutomaticReturn,
                }
                && visible.Position != context.Self.Position)
            {
                tiles.Add(visible.Position);
            }
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

    /// <summary>
    /// Start one declared same-life route through the action the route itself
    /// names, with exactly the payload that action declares.
    ///
    /// <para>This is a repair, and it is the most expensive assumption this
    /// lineage carried. Revision 3 searched every available transition action for
    /// a form-target constraint listing the form it wanted, and submitted that
    /// form as a payload. A route whose action takes NO parameters — the return
    /// leg of every stance and of every turret, declared as
    /// <c>parameterKinds: []</c> — matches nothing under that search, so a
    /// fortified body could never stand up in any class arm and a stance could
    /// never be left early. The route names its action; the action declares its
    /// parameters; read both.</para>
    /// </summary>
    private static GenericActorDecision? StartRoute(
        ContractView view,
        GenericActorContext context,
        GenericActorRulesContract.FormTransition route,
        string reason)
    {
        GenericActorActionLegality? action = context.ActionLegalities
            .FirstOrDefault(entry =>
                entry.Available
                && string.Equals(
                    entry.ActionId,
                    route.ActionId,
                    StringComparison.Ordinal));
        if (action is null)
            return null;

        GenericActorActionLegality.ArgumentConstraint.FormTargetConstraint?
            forms = action.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .FormTargetConstraint>()
                .SingleOrDefault();
        if (forms is null)
        {
            // No form to choose: the action IS the route. Only submit it when
            // there is nothing else the mask wants from us.
            return action.Constraints.IsEmpty
                ? GenericActorDecision.WithoutArguments(
                    action.ActionId,
                    action.ActionCode,
                    reason)
                : null;
        }
        return forms.AllowedFormIds.Contains(route.TargetFormId)
            ? new GenericActorDecision(
                action.ActionId,
                action.ActionCode,
                [
                    new GenericActorActionArgument.FormTargetArgument(
                        route.TargetFormId),
                ],
                reason)
            : null;
    }

    /// <summary>
    /// Fire control with the enemy's declared guards applied. A shot is rejected
    /// when it would ARRIVE inside a guarding body's arc, because that bolt does
    /// not damage it — it is relaunched from its tile along the reverse heading
    /// under its team's ownership, which is to say we shot ourselves and paid a
    /// three-tick cadence for the privilege.
    ///
    /// <para>The filter runs inside the tracer rather than over its results, so
    /// what survives is the cheapest ACCEPTED shot. That is the one place a bend
    /// earns its commitment against this class: the straight bolt and the bent
    /// bolt reach the same tile, and only one of them arrives from a bearing the
    /// arc does not cover. Feeding three bolts into an arc to force its break is
    /// the alternative the rules offer, and it is refused here: three of our
    /// bolts and three returns bought at our own cadence to win an exit windup,
    /// when going around the arc always works.</para>
    /// </summary>
    private static Dictionary<Position, FireControl.Shot> Solutions(
        ContractView view,
        GenericActorContext context,
        Direction? facingOverride = null)
    {
        Dictionary<Position, GenericActorContext.ObservedEnemyState> guards = [];
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            if (view.HasGuard(enemy.FormId))
                guards[enemy.Position] = enemy;
        }
        if (guards.Count == 0)
            return FireControl.Solutions(view, context, facingOverride);

        return FireControl.Solutions(
            view,
            context,
            facingOverride,
            (tile, heading) =>
                !guards.TryGetValue(
                    tile,
                    out GenericActorContext.ObservedEnemyState? guard)
                || !Stance.GuardsAgainst(guard.Facing, heading));
    }

    /// <summary>
    /// The facing from which this pose can hit a body — past its guard when it
    /// has one. Identical to <see cref="Lane.FacingThatCovers"/> for every
    /// unguarded target, which is every target on an arm without the kit.
    /// </summary>
    private static Direction? FacingThatBeatsTheArc(
        ContractView view,
        string formId,
        Position from,
        Direction preferred,
        GenericActorContext.ObservedEnemyState target,
        IReadOnlyList<Direction> order)
    {
        if (!view.HasGuard(target.FormId))
        {
            return Lane.FacingThatCovers(
                view,
                formId,
                from,
                preferred,
                target.Position,
                order);
        }
        if (Lane.CoversPastTheArc(view, formId, from, preferred, target))
            return preferred;
        foreach (Direction direction in order)
        {
            if (direction != preferred
                && Lane.CoversPastTheArc(view, formId, from, direction, target))
            {
                return direction;
            }
        }
        return null;
    }

    /// <summary>
    /// A same-life form change preserves this instance and its memory, so the
    /// stance bookkeeping has to notice the change itself. Every counter a stance
    /// owns is scoped to one entry, exactly as the declared budget is.
    /// </summary>
    private void RememberForm(GenericActorContext context)
    {
        if (string.Equals(_formId, context.Self.FormId, StringComparison.Ordinal))
            return;
        _formId = context.Self.FormId;
        _deflections = 0;
        _quietStanceTicks = 0;
        _countedDeflections.Clear();
    }

    /// <summary>
    /// Count what our own arc has turned. The deflection is published as its own
    /// event kind naming the shell, the shooter, the bolt that died and the bolt
    /// that was sent back, so this is a read rather than a guess — and the count
    /// is what tells a shield how much of its declared budget is left.
    /// </summary>
    private void RememberDeflections(GenericActorContext context)
    {
        foreach (GenericActorContext.ObservedEvent observed
                 in context.VisibleEvents)
        {
            if (observed.Kind
                    != GenericActorContext.EventKind.ProjectileDeflected
                || observed.Payload
                    is not GenericActorContext.EventPayload.ProjectileDeflected
                        deflection
                || deflection.TargetActorId != context.Self.ActorId
                || !_countedDeflections.Add(observed.EventHandle))
            {
                continue;
            }
            _deflections++;
        }
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
