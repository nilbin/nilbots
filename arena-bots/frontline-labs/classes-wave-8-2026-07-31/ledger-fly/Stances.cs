using BotArena.Sdk;

/// <summary>
/// The skill kit, priced from the contract on both sides of the board.
///
/// A stance in this doctrine is a same-life route into a form that <b>keeps its
/// objective weight</b> and adds either a fan (<c>volley</c> on its attack
/// profile) or a guard (<c>projectileGuard</c> on the form). That gate is the
/// banker's whole opinion about fortification: a route into a zero-weight body
/// is not a stance, it is a body deleted from the ledger, and this doctrine has
/// never paid that price. It is also why the same source is safe on a contract
/// whose only same-life route is an Anchor - the gate rejects it and the rung is
/// inert.
///
/// <para>The gate is deliberately about WEIGHT and not about reversibility, and
/// a contract that makes fortification a free round trip is exactly why. Read
/// <c>irreversibleForLife</c> rather than assuming: where it is false the way
/// back is unlimited, health maps by ratio in both directions, and the only
/// remaining price is the windup - so every argument against fortifying except
/// "a zero-weight body scores nothing" has been removed, and that one is the one
/// this doctrine was always making. The same test decides where an opposing
/// fortified body sits in our firing order.</para>
///
/// Both stances declare how much they are worth and leave when it is gone. The
/// return route carries <c>automaticReturn</c> with a counter and a threshold,
/// and the property is absent on every route the engine never fires itself, so
/// missing means "this stance has no budget and the exit is entirely mine".
/// Leaving early is still a decision; leaving late does not exist.
///
/// <para>The hostile half matters more than the friendly half for a chassis
/// that owns neither skill. Three readings:</para>
/// <list type="bullet">
/// <item><b>A guarded arc is a mirror.</b> A bolt that arrives inside a guard's
/// facing quadrant dies and a team-flipped copy launches from the guard's tile
/// straight back down the reversed heading. Poking a face is not a miss, it is a
/// self-inflicted hit, so lanes into the arc are refused - unless we are
/// deliberately feeding the third bolt, because the third deflection shatters
/// the shield into a forced return whose exit and re-entry windups are the
/// punish window.</item>
/// <item><b>A fan is three lanes, not one.</b> A body in a stance whose profile
/// declares <c>volley.projectileCount</c> greater than one will launch along its
/// facing and the adjacent headings at once, so clumping in any of them feeds
/// it. Those lanes are threat, before a single bolt exists.</item>
/// <item><b>A deflected bolt is an ordinary enemy projectile.</b> It appears in
/// <c>visibleProjectiles</c> owned by the guard's team, and every dodge and
/// blocking test in this bot keys on <c>OwnerTeamId</c>, so a return is hostile
/// by construction rather than by a special case.</item>
/// </list>
/// </summary>
internal sealed class Stances
{
    private readonly Dictionary<ActorIdentity, int> _deflections = [];
    private readonly List<Guarded> _guarded = [];
    private readonly List<FanSource> _fanCapable = [];
    private readonly HashSet<Position> _fanLanes = [];
    private int _reraiseAfterTick = -1;

    /// <summary>One visible enemy body whose form turns bolts on its arc.</summary>
    /// <param name="ActorId">The guarding life.</param>
    /// <param name="Tile">Where the arc is anchored.</param>
    /// <param name="Facing">Centre of the protected quadrant, fixed while raised.</param>
    /// <param name="Deflections">Deflections this life is observed to have made.</param>
    /// <param name="BreaksAt">
    /// Deflection count that shatters the shield, read from the return route's
    /// declared budget; null when the contract declares none.
    /// </param>
    public sealed record Guarded(
        ActorIdentity ActorId,
        Position Tile,
        Direction Facing,
        int Deflections,
        int? BreaksAt);

    /// <summary>Enemy bodies currently presenting a deflecting arc.</summary>
    public IReadOnlyList<Guarded> GuardedEnemies => _guarded;

    /// <summary>Tiles an enemy fan stance is already aimed through.</summary>
    public IReadOnlySet<Position> FanLanes => _fanLanes;

    /// <summary>Visible enemy bodies whose declared gun covers a spread.</summary>
    public IReadOnlyList<FanSource> FanCapable => _fanCapable;

    /// <summary>Volley casts this life has committed.</summary>
    public int Casts { get; private set; }

    /// <summary>Guard stances this life has raised.</summary>
    public int Raises { get; private set; }

    /// <summary>Enemy shields this life has watched shatter.</summary>
    public int Breaks { get; private set; }

    /// <summary>
    /// Folds this tick's events and visible bodies into the hostile stance model.
    /// Deflection counts are per guarding life and reset with it, exactly as the
    /// engine's own counter does: it starts at zero when the life enters the form
    /// and never survives it.
    /// </summary>
    public void Observe(MatchLens lens, GenericActorContext context)
    {
        foreach (GenericActorContext.ObservedEvent visible
                 in context.VisibleEvents)
        {
            switch (visible.Payload)
            {
                case GenericActorContext.EventPayload.ProjectileDeflected
                    deflected:
                    _deflections[deflected.TargetActorId] =
                        _deflections.TryGetValue(
                            deflected.TargetActorId,
                            out int seen)
                            ? seen + 1
                            : 1;
                    break;
                // A stance the engine returned by itself publishes that fact:
                // the transition carries `automatic`, which is the shield's own
                // budget spending itself. A shell that just broke is a shell we
                // may punish, and its exit plus a fresh entry windup is the
                // window. The counter never survives the form, so drop it.
                case GenericActorContext.EventPayload.FormTransition transition
                    when lens.Guards(transition.FromFormId):
                    _deflections.Remove(transition.ActorId);
                    if (transition.ActorId.TeamId != lens.TeamId
                        && transition.Automatic
                        && visible.Kind
                            == GenericActorContext.EventKind
                                .FormTransitionStarted)
                    {
                        Breaks++;
                    }
                    break;
                case GenericActorContext.EventPayload.Destruction destruction:
                    _deflections.Remove(destruction.ActorId);
                    break;
                default:
                    break;
            }
        }

        _guarded.Clear();
        _fanLanes.Clear();
        _fanCapable.Clear();
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            if (lens.Guards(enemy.FormId))
            {
                _guarded.Add(
                    new Guarded(
                        enemy.ActorId,
                        enemy.Position,
                        enemy.Facing,
                        _deflections.TryGetValue(enemy.ActorId, out int count)
                            ? count
                            : 0,
                        lens.ReturnRoute(enemy.FormId)?.AutomaticReturn
                            ?.Threshold));
            }

            GenericActorRulesContract.AttackVolley? fan =
                lens.Volley(enemy.FormId);
            // WAVE 6. A fan this body can still ENTER is a spacing fact now, not
            // only a fan it is already standing in. The lanes below stay gated on
            // the raised stance - an unraised fan is aimed at nothing yet - but
            // "these two of our bodies are one cast of theirs" is true for as long
            // as the route exists, and a volley stance is two ticks of windup
            // away. Reading the ROUTE rather than the current form is the same
            // discipline this file already uses to decide its own raises, and it
            // is why the first version of the spacing line measured at exactly
            // zero: a striker is inside its stance for about two ticks per cast,
            // so a term gated on the form was a term that never fired.
            bool couldFan = false;
            int routeReach = 0;
            foreach (MatchLens.StanceRoute route
                     in lens.StanceRoutes(enemy.FormId))
            {
                if (!route.Fan)
                    continue;
                GenericActorRulesContract.AttackVolley? entered =
                    lens.Volley(route.TargetFormId);
                if (entered is null || entered.ProjectileCount <= 1)
                    continue;
                couldFan = true;
                routeReach = Math.Max(
                    routeReach,
                    lens.Attack(route.TargetFormId)?.Projectile.MaxTravelTiles
                        ?? 0);
                fan ??= entered;
            }
            if (fan is null || fan.ProjectileCount <= 1)
                continue;
            GenericActorRulesContract.AttackProfile? profile =
                lens.Attack(enemy.FormId);
            int reach = profile?.Projectile.MaxTravelTiles ?? 0;
            bool raised = lens.Volley(enemy.FormId) is not null;
            if (!raised)
            {
                // Not raised: the spread is real, the lanes are not.
                _fanCapable.Add(
                    new FanSource(
                        enemy.Position,
                        Math.Max(reach, routeReach),
                        Math.Max(1, (fan.ProjectileCount - 1) / 2)));
                _ = couldFan;
                continue;
            }
            // The fan is the facing lane plus the adjacent headings on both
            // sides, as many as the declared count spreads over. Straight by
            // construction - a volley profile refuses programmed shots - so the
            // lanes are exactly rays and can be walked.
            int wings = (fan.ProjectileCount - 1) / 2;
            ProjectileHeading centre = enemy.Facing.ToProjectileHeading();
            for (int offset = -wings; offset <= wings; offset++)
                AddRay(lens, enemy.Position, centre.Turned(offset), reach);
            // Wave 6: the fan is also a SPACING fact, and it is a different one
            // from its lanes. Its lanes say "this tile is aimed at"; its spread
            // says "these two tiles are aimed at TOGETHER", and one entry buys one
            // cast, so two of our bodies inside one spread are one decision of
            // theirs. The width is the declared count, never a constant.
            _fanCapable.Add(
                new FanSource(enemy.Position, reach, Math.Max(1, wings)));
        }
    }

    /// <summary>
    /// A visible enemy body whose declared gun fires a fan, with the reach and
    /// the sector half-width the contract gives it. It is the spread rather than
    /// the lanes: two of our bodies within <paramref name="Spread"/> sectors of
    /// each other, measured from this tile, are covered by ONE cast.
    /// </summary>
    /// <param name="Tile">Where the fan would launch from.</param>
    /// <param name="Reach">Declared travel of its bolts.</param>
    /// <param name="Spread">Sectors either side of the centre lane it covers.</param>
    public sealed record FanSource(Position Tile, int Reach, int Spread);

    /// <summary>
    /// Whether a bolt travelling <paramref name="heading"/> into
    /// <paramref name="guard"/>'s tile is turned back at us. The arc is the
    /// facing quadrant: the bearing from the guard out to the shooter lies
    /// within one 45-degree sector of the guard's facing. It never tracks, so
    /// going around it always works.
    /// </summary>
    public static bool ArcTurns(Guarded guard, ProjectileHeading heading)
    {
        // The bolt travels `heading`; it arrives FROM the reverse bearing.
        ProjectileHeading from = heading.Turned(4);
        return Sectors(guard.Facing.ToProjectileHeading(), from) <= 1;
    }

    /// <summary>
    /// True when one more bolt into this arc shatters the shield instead of
    /// being handed back - the only reason to aim at a face on purpose.
    /// </summary>
    public static bool OneBoltFromBreaking(Guarded guard) =>
        guard.BreaksAt is int threshold
        && threshold > 0
        && guard.Deflections >= threshold - 1;

    /// <summary>
    /// Whether a shot at <paramref name="target"/> whose bolt is travelling
    /// <paramref name="heading"/> is worth taking given every visible arc. A
    /// lane that ends inside an arc is refused unless it is the breaking bolt.
    /// </summary>
    public bool LaneWorthTaking(Position target, ProjectileHeading heading)
    {
        foreach (Guarded guard in _guarded)
        {
            if (guard.Tile != target)
                continue;
            if (!ArcTurns(guard, heading))
                return true;
            return OneBoltFromBreaking(guard);
        }
        return true;
    }

    /// <summary>
    /// The stance decision, or null when this chassis owns no weight-preserving
    /// stance, the mask refuses it this tick, or nothing on the board pays for
    /// it. Every gate is a contract read: the routes come from
    /// <c>sameLifeTransitions</c>, the fan shape from the target profile's
    /// <c>volley</c>, the guard from the target form's <c>projectileGuard</c>,
    /// and the budget from the return route's <c>automaticReturn</c>.
    /// </summary>
    /// <summary>
    /// Raise a guard on an approach the contested ground depends on.
    ///
    /// <para>The obvious gate - "raise it while standing on the objective" -
    /// is unreachable, and finding out why is worth stating. The stance keeps
    /// objective weight 1, so it reads as a body that holds scoring ground; but
    /// its route declares <c>forbiddenTileTags: [transition-placement-forbidden]</c>
    /// and on this map EVERY objective tile carries that tag - the same rule that
    /// stops an Anchor on the objective. So a guard is legal only BESIDE the
    /// ground it protects, which makes it an approach plug rather than a capture
    /// holder. The mask is what says so, tick by tick, and this method never
    /// second-guesses it: the route simply is not offered on a forbidden tile.
    /// </para>
    /// </summary>
    public GenericActorDecision? TryRaise(
        MatchLens lens,
        GenericActorContext context,
        IReadOnlySet<Position> objectiveTiles)
    {
        // Within one step of the contested region, so the arc covers an approach
        // to it. Off the chain entirely, any tile the mask allows will do.
        bool guardsApproach = objectiveTiles.Count == 0
            || objectiveTiles.Min(tile =>
                tile.ChebyshevDistance(context.Self.Position)) <= 2;
        foreach ((MatchLens.StanceRoute route, GenericActorActionLegality action)
                 in Usable(lens, context))
        {
            if (!route.Guard)
                continue;
            // An arc that goes up and down as bodies drift across its edge spends
            // its whole life in windups, and both windups are Wait-only. So a
            // dropped arc stays down for as long as the round trip would have
            // cost - the same number the contract declares, not a tuned one.
            if (context.Tick < _reraiseAfterTick)
                continue;
            if (!PaysForGuard(
                    lens,
                    context,
                    route.WindupTicks,
                    guardsApproach))
            {
                continue;
            }
            Raises++;
            return Decide(
                action,
                route,
                "raising the arc across the approach");
        }
        return null;
    }

    /// <summary>
    /// The offensive half: one entry buys exactly one cast, so it is taken only
    /// when the fan will be loaded by the time it is up and the spread is
    /// already earning.
    /// </summary>
    public GenericActorDecision? TryCast(
        MatchLens lens,
        GenericActorContext context,
        IReadOnlyList<GenericActorContext.ObservedEnemyState> targets)
    {
        GenericActorRulesContract.AttackProfile? gun =
            lens.Attack(context.Self.FormId);
        int reach = gun?.Projectile.MaxTravelTiles ?? 0;
        foreach ((MatchLens.StanceRoute route, GenericActorActionLegality action)
                 in Usable(lens, context))
        {
            if (!route.Fan)
                continue;
            // The windup is committed and Wait-only and the stance cannot be
            // squatted in, so entering with a cold gun spends the immobility
            // BEFORE the cast instead of around it. Enter only when the cooldown
            // will have run out by the time the fan is up. (Measured: without
            // this the stance was re-entered on the tick the previous cast
            // returned it, and stood immobile for three ticks of cooldown.)
            if (context.Self.Cooldown > route.WindupTicks)
                continue;
            if (!PaysForFan(lens, context, targets, reach))
                continue;
            Casts++;
            return Decide(
                action,
                route,
                $"entering the fan for one cast at {targets.Count} bodies");
        }
        return null;
    }

    /// <summary>
    /// Stance routes out of the current form that this doctrine will take and
    /// the mask allows this tick, each paired with its legality entry. An
    /// irreversible entry is a one-way trade of mobility for a single effect and
    /// is skipped.
    /// </summary>
    private static List<(MatchLens.StanceRoute Route,
        GenericActorActionLegality Action)> Usable(
        MatchLens lens,
        GenericActorContext context)
    {
        var usable =
            new List<(MatchLens.StanceRoute, GenericActorActionLegality)>();
        foreach (MatchLens.StanceRoute route
                 in lens.StanceRoutes(context.Self.FormId))
        {
            if (route.Irreversible)
                continue;
            if (Transform(context, route) is not GenericActorActionLegality
                action)
            {
                continue;
            }
            usable.Add((route, action));
        }
        return usable;
    }

    /// <summary>
    /// Leaving a stance early, which the contract keeps as our decision. The fan
    /// returns itself the tick it launches, so there is nothing to author there;
    /// the guard's budget only spends itself when bolts actually arrive, so a
    /// raised arc with nothing coming at it is a body standing still for free
    /// and is dropped. Null when we are not in a stance or the exit is refused.
    /// </summary>
    public GenericActorDecision? TryLeave(
        MatchLens lens,
        GenericActorContext context)
    {
        if (!lens.Guards(context.Self.FormId))
            return null;
        // Hysteresis, and it is not a nicety. Raising on "a shooter is in my arc
        // and my gun is cold" and dropping on "no bolt is in flight this exact
        // tick" produced 168 raises and 156 drops in one 500-tick match - a body
        // that spent most of the game in a windup. So the hold test is the
        // weaker one: an arc keeps standing while anything inside it can still
        // shoot, and drops only when the approach has actually gone quiet.
        if (BoltInArc(context, windupTicks: 0) || ShooterInArc(lens, context))
            return null;

        GenericActorRulesContract.FormTransition? exit =
            lens.ReturnRoute(context.Self.FormId);
        if (exit is null)
            return null;
        GenericActorActionLegality? action = context.ActionLegalities
            .FirstOrDefault(candidate =>
                candidate.Available
                && string.Equals(
                    candidate.ActionId,
                    exit.ActionId,
                    StringComparison.Ordinal));
        if (action is null)
            return null;

        var arguments = new List<GenericActorActionArgument>();
        GenericActorActionLegality.ArgumentConstraint.FormTargetConstraint?
            forms = action.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .FormTargetConstraint>()
                .FirstOrDefault();
        if (forms is not null)
        {
            if (!forms.AllowedFormIds.Contains(
                    exit.TargetFormId,
                    StringComparer.Ordinal))
            {
                return null;
            }
            arguments.Add(
                new GenericActorActionArgument.FormTargetArgument(
                    exit.TargetFormId));
        }
        _reraiseAfterTick = context.Tick
            + (2 * Math.Max(1, exit.Windup.DurationTicks));
        return new GenericActorDecision(
            action.ActionId,
            action.ActionCode,
            arguments,
            "dropping an arc nothing is shooting at");
    }

    private static bool PaysForFan(
        MatchLens lens,
        GenericActorContext context,
        IReadOnlyList<GenericActorContext.ObservedEnemyState> targets,
        int reach)
    {
        // One entry buys exactly one cast, and the windup is committed and
        // Wait-only, so the fan has to be worth the whole exchange rather than
        // one bolt. Two bodies inside the spread, or one already inside the
        // centre lane, is the bar.
        if (targets.Count == 0)
            return false;
        ProjectileHeading centre = context.Self.Facing.ToProjectileHeading();
        int inSpread = 0;
        bool centred = false;
        foreach (GenericActorContext.ObservedEnemyState target in targets)
        {
            if (context.Self.Position.ChebyshevDistance(target.Position) > reach)
                continue;
            for (int offset = -1; offset <= 1; offset++)
            {
                if (!Field.RayReaches(lens, context.Self.Position, target.Position))
                    continue;
                if (!Aligned(
                        context.Self.Position,
                        target.Position,
                        centre.Turned(offset)))
                {
                    continue;
                }
                inSpread++;
                centred |= offset == 0;
                break;
            }
        }
        return inSpread >= 2 || (inSpread == 1 && centred);
    }

    private static bool PaysForGuard(
        MatchLens lens,
        GenericActorContext context,
        int windupTicks,
        bool guardsApproach)
    {
        // An arc is a tile held against fire, so it is worth a windup only where
        // the tile is worth something and the fire is real or imminent. The
        // quadrant is chosen before the shield rises and cannot be turned
        // afterwards, so whatever we mean to turn has to be inside it already.
        if (!guardsApproach)
            return false;

        // Case one: a bolt is already in flight into the arc, and the windup
        // finishes before it lands.
        if (BoltInArc(context, windupTicks))
            return true;

        // Case two: the tempo tax. A body inside the arc with a clear lane onto
        // this tile is going to fire, and our own gun cannot answer it this tick
        // - so instead of trading a tick for nothing, make its own bolt the one
        // that lands. This is where the arc stops being a defensive option and
        // becomes a threat, and it is why poking a face is no longer free.
        return context.Self.Cooldown > 0 && ShooterInArc(lens, context);
    }

    /// <summary>
    /// A hostile bolt inside the facing quadrant whose exact arrival is later
    /// than <paramref name="windupTicks"/>. During a windup the life is still a
    /// targetable mobile body, so a shield that completes late is a shield
    /// raised behind the damage. Pass zero to ask "is one arriving at all".
    /// </summary>
    private static bool BoltInArc(
        GenericActorContext context,
        int windupTicks)
    {
        ProjectileHeading facing = context.Self.Facing.ToProjectileHeading();
        foreach (GenericActorContext.ObservedProjectile projectile
                 in context.VisibleProjectiles ?? [])
        {
            if (projectile.OwnerTeamId == context.Self.ActorId.TeamId)
                continue;
            if (ArenaBasics.Threat(projectile, context.Self.Position)
                is not ArenaBasics.Incoming incoming)
            {
                continue;
            }
            if (incoming.TicksUntilArrival <= windupTicks)
                continue;
            if (Sectors(facing, projectile.Heading.Turned(4)) <= 1)
                return true;
        }
        return false;
    }

    /// <summary>
    /// A visible enemy inside the facing quadrant, within its OWN declared gun
    /// reach, with a clear straight lane onto this tile - a body that can shoot
    /// us this tick and be shot by its own bolt for it.
    /// </summary>
    private static bool ShooterInArc(
        MatchLens lens,
        GenericActorContext context)
    {
        ProjectileHeading facing = context.Self.Facing.ToProjectileHeading();
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            GenericActorRulesContract.AttackProfile? gun =
                lens.Attack(enemy.FormId);
            if (gun is null)
                continue;
            if (enemy.Position.ChebyshevDistance(context.Self.Position)
                > gun.Projectile.MaxTravelTiles)
            {
                continue;
            }
            if (!Field.RayReaches(lens, enemy.Position, context.Self.Position))
                continue;
            if (!TryBearing(
                    context.Self.Position,
                    enemy.Position,
                    out ProjectileHeading bearing))
            {
                continue;
            }
            if (Sectors(facing, bearing) <= 1)
                return true;
        }
        return false;
    }

    private static bool TryBearing(
        Position from,
        Position to,
        out ProjectileHeading heading)
    {
        heading = default;
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        if (dx == 0 && dy == 0)
            return false;
        if (dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy))
            return false;
        heading = (Math.Sign(dx), Math.Sign(dy)) switch
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
        return true;
    }

    private static GenericActorDecision Decide(
        GenericActorActionLegality transform,
        MatchLens.StanceRoute route,
        string reason) =>
        new(
            transform.ActionId,
            transform.ActionCode,
            [
                new GenericActorActionArgument.FormTargetArgument(
                    route.TargetFormId),
            ],
            reason);

    private static GenericActorActionLegality? Transform(
        GenericActorContext context,
        MatchLens.StanceRoute route) =>
        context.ActionLegalities
            .FirstOrDefault(action =>
                action.Available
                && string.Equals(
                    action.ActionId,
                    route.ActionId,
                    StringComparison.Ordinal)
                && action.Constraints
                    .OfType<GenericActorActionLegality.ArgumentConstraint
                        .FormTargetConstraint>()
                    .Any(constraint =>
                        constraint.AllowedFormIds.Contains(
                            route.TargetFormId,
                            StringComparer.Ordinal)));

    private void AddRay(
        MatchLens lens,
        Position origin,
        ProjectileHeading heading,
        int tiles)
    {
        (int dx, int dy) = heading.Vector();
        Position cursor = origin;
        for (int step = 0; step < tiles; step++)
        {
            Position next = cursor.Offset(dx, dy);
            if (lens.IsWall(next))
                return;
            if (dx != 0
                && dy != 0
                && (lens.IsWall(cursor.Offset(dx, 0))
                    || lens.IsWall(cursor.Offset(0, dy))))
            {
                return;
            }
            cursor = next;
            _fanLanes.Add(cursor);
        }
    }

    private static bool Aligned(
        Position from,
        Position to,
        ProjectileHeading heading)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        if (dx == 0 && dy == 0)
            return false;
        if (dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy))
            return false;
        (int hx, int hy) = heading.Vector();
        return Math.Sign(dx) == hx && Math.Sign(dy) == hy;
    }

    private static int Sectors(ProjectileHeading from, ProjectileHeading to)
    {
        int difference = ((int)to - (int)from + 8) % 8;
        return Math.Min(difference, 8 - difference);
    }
}
