using BotArena.Sdk;

/// <summary>
/// The skill arc-light is built around. A volley cast is one whole decision
/// sequence, not an action: transform in over a declared windup that permits
/// nothing but waiting, aim by rotating, shoot once, and be returned by the
/// engine because the route's declared budget is one attack. You cannot squat in
/// the stance and you cannot dodge inside its windup, so the cast is priced
/// before it is paid.
///
/// <para>Everything here is read from the contract: the route, its windup, the
/// target form's fan width, the budget counter and threshold, and the objective
/// weight on both sides of the change. In the kit-off cell there is no such
/// route and every method below declines, which is how one artifact plays both
/// cells.</para>
///
/// <para>The one thing that makes the volley cheap on a keel contract is that
/// the stance keeps objective weight 1. Casting from on top of the objective
/// costs no capture pressure at all, so arc-light prefers to fire from the
/// ground it is already holding rather than walking somewhere to shoot.</para>
/// </summary>
internal sealed class ArcStance
{
    private readonly ArcFacts _facts;
    private readonly GenericActorContext _context;
    private readonly ArcThreat _threat;
    private readonly ArcGun _gun;

    public ArcStance(
        ArcFacts facts,
        GenericActorContext context,
        ArcThreat threat,
        ArcGun gun)
    {
        _facts = facts;
        _context = context;
        _threat = threat;
        _gun = gun;
    }

    /// <summary>
    /// Why the last <see cref="TryEnter"/> call declined. Bounded diagnostic
    /// only; it never affects a decision, and it is the difference between
    /// "the bot did not adopt the skill" and "the skill was priced and refused".
    /// </summary>
    public string Veto { get; private set; } = "unasked";

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
    /// What to do while inside the stance. Aim by rotating, fire when a lane
    /// covers something the target could actually be standing on, and leave
    /// early through the ordinary return when the cast has stopped being worth
    /// the immobility. Leaving early is a perfectly ordinary decision; staying
    /// past the budget is not possible.
    /// </summary>
    public GenericActorDecision? Act(int ticksInStance)
    {
        if (!InStance)
            return null;

        ArcGun.Shot? shot = _gun.Best();
        if (shot is not null && shot.Score > 0)
            return shot.Decision;

        // The fan is straight along facing plus its declared adjacent headings,
        // so aiming is a rotation. Only spend it when the new lane set covers
        // more than the current one.
        int current = _gun.FanCoverage(
            _context.Self.FormId,
            _context.Self.Facing,
            _context.Self.Position,
            ticksFromNow: 0);
        GenericActorActionLegality? rotate = Legality(
            GenericActorRulesContract.ActionKind.Rotation);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            headings = rotate?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        if (rotate is not null && headings is not null)
        {
            Direction? best = null;
            int bestCover = current;
            foreach (Direction facing in headings.AllowedValues)
            {
                if (facing == _context.Self.Facing)
                    continue;
                int cover = _gun.FanCoverage(
                    _context.Self.FormId,
                    facing,
                    _context.Self.Position,
                    ticksFromNow: 1);
                if (cover > bestCover)
                {
                    bestCover = cover;
                    best = facing;
                }
            }
            if (best is Direction turn)
            {
                return new GenericActorDecision(
                    rotate.ActionId,
                    rotate.ActionCode,
                    [new GenericActorActionArgument.DirectionArgument(turn)],
                    $"swinging the fan onto {turn}");
            }
        }

        // Bolts block movement and a contact hurts, so a fan laid across ground
        // the opposition wants is worth firing even with no predicted body on a
        // lane: three simultaneous bolts close three tiles. The budget is one
        // attack either way, so an unfired cast is a pure loss — fire it rather
        // than walk the commitment back.
        if (Denial() is GenericActorDecision denial)
            return denial;

        // Nothing at all to fire at. An immobile body is a target, so return
        // through the route's own action rather than waiting for a budget that
        // only an attack can spend.
        bool stuck = _context.Enemies.IsEmpty
            || ticksInStance >= 2
            || _threat.Threatened(_context.Self.Position, 2);
        if (stuck && Leave() is GenericActorDecision leave)
            return leave;
        return null;
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

        foreach (ProjectileHeading heading in _gun.FanHeadings(
                     _context.Self.FormId,
                     _context.Self.Facing))
        {
            Position cursor = _context.Self.Position;
            for (int tile = 0; tile < profile.Projectile.MaxTravelTiles; tile++)
            {
                cursor = ArcBoard.Step(cursor, heading);
                if (_facts.IsWall(cursor))
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

    /// <summary>A tile and facing worth walking to in order to cast from.</summary>
    /// <param name="Tile">Where the cast is legal and the lanes are worth it.</param>
    /// <param name="Facing">Heading whose fan covers the most enemy ground.</param>
    /// <param name="Value">Discounted lane coverage after the walk and windup.</param>
    /// <param name="Steps">Wall-only steps from this body's tile.</param>
    public sealed record CastPost(
        Position Tile,
        Direction Facing,
        int Value,
        int Steps);

    /// <summary>
    /// The cast has a POSITION, and finding it is most of the skill.
    ///
    /// <para>A stance is an ordinary same-life transition, so it obeys the
    /// route's declared placement tags — and on this map every objective tile
    /// carries the transition-forbidden tag, along with the whole central
    /// corridor. The fan therefore cannot be cast from the ground it is meant to
    /// deny; it is cast from the shoulder beside it, where three headings still
    /// rake the objective cluster. Nothing here is a coordinate: the forbidden
    /// set comes from the map tags and the lanes from the target form's own
    /// volley shape.</para>
    ///
    /// <para>Leaving the objective for the shoulder is only affordable because
    /// this contract's decay clock runs for an enemy standing ALONE, so the
    /// caller must confirm that stepping off does not hand the enemy sole
    /// presence.</para>
    /// </summary>
    public CastPost? BestPost(
        int radius,
        IReadOnlySet<Position> hot,
        IReadOnlySet<Position> blocked)
    {
        GenericActorRulesContract.FormTransition? route =
            _facts.FanStanceRoute(_context.Self.FormId);
        if (route is null || _context.Enemies.IsEmpty)
            return null;
        int commit = ArcFacts.CommitTicks(route);

        var distances = new Dictionary<Position, int>
        {
            [_context.Self.Position] = 0,
        };
        var frontier = new List<Position> { _context.Self.Position };
        for (int step = 1; step <= radius; step++)
        {
            var next = new List<Position>();
            foreach (Position tile in frontier)
            {
                foreach (Direction direction in ArcBoard.Cardinals)
                {
                    Position candidate = ArcBoard.Step(tile, direction);
                    if (_facts.Impassable(candidate)
                        || distances.ContainsKey(candidate)
                        || (step == 1 && blocked.Contains(candidate)))
                    {
                        continue;
                    }
                    distances[candidate] = step;
                    next.Add(candidate);
                }
            }
            frontier = next;
        }

        CastPost? best = null;
        foreach ((Position tile, int steps) in distances)
        {
            if (_facts.TransitionForbidden.Contains(tile))
                continue;
            foreach (Direction facing in ArcBoard.Cardinals)
            {
                int coverage = _gun.FanCoverage(
                    route.TargetFormId,
                    facing,
                    tile,
                    commit + steps);
                if (coverage <= 0)
                    continue;
                // Coverage is measured in lane crossings, and a single lane
                // crosses a facing-locked body's ray at most once — so three
                // lanes is a small integer, not a big score. Weight it against
                // the walk rather than against an invented constant.
                int value = coverage
                    - steps
                    - (hot.Contains(tile) ? 2 : 0)
                    - (_threat.Threatened(tile, commit + steps + 1) ? 12 : 0);
                if (best is null || value > best.Value)
                    best = new CastPost(tile, facing, value, steps);
            }
        }
        return best;
    }

    /// <summary>
    /// Enter a fan stance, or decline. Four gates, each a contract read:
    /// the route must exist and be legal this tick; the windup permits only
    /// waiting, so no hostile bolt may be able to reach this tile before the
    /// stance is usable; the change must not cost objective weight that is
    /// currently load-bearing; and the fan must cover more of what the enemy can
    /// reach than the ordinary gun's best arc already does.
    /// </summary>
    public GenericActorDecision? TryEnter(ArcKeel keel, ArcGun.Shot? aimed)
    {
        Veto = "no-route";
        GenericActorRulesContract.FormTransition? route =
            _facts.FanStanceRoute(_context.Self.FormId);
        if (route is null || _context.Enemies.IsEmpty)
            return null;
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

        int commit = ArcFacts.CommitTicks(route);
        Veto = "windup-unsafe";
        if (_threat.Threatened(_context.Self.Position, commit + 1))
            return null;
        Veto = "weight";

        // Objective weight is the whole reason a stance is cheap or ruinous.
        if (_facts.ObjectiveWeight(route.TargetFormId)
                < _facts.ObjectiveWeight(_context.Self.FormId)
            && keel.PresenceIsLoadBearing)
        {
            return null;
        }

        int fan = _gun.FanCoverage(
            route.TargetFormId,
            _context.Self.Facing,
            _context.Self.Position,
            commit);
        int rotated = 0;
        foreach (Direction facing in ArcBoard.Cardinals)
        {
            rotated = Math.Max(
                rotated,
                _gun.FanCoverage(
                    route.TargetFormId,
                    facing,
                    _context.Self.Position,
                    commit + 1));
        }
        int best = Math.Max(fan, rotated);
        Veto = "no-lane";
        if (best <= 0)
            return null;

        // The fan competes against the ordinary gun, so the bar is what the
        // ordinary gun would do rather than an invented constant. This branch is
        // only reached when no aimed bolt was worth firing this tick, so what is
        // left to beat is the arc the gun will have once its cooldown drains —
        // and three simultaneous lanes beat one lane whenever the target's
        // reachable set spans more than that one lane.
        int nearest = _context.Enemies.Min(enemy =>
            _context.Self.Position.ChebyshevDistance(enemy.Position));
        int reach = _facts.Attack(route.TargetFormId)?.Projectile.MaxTravelTiles
            ?? 8;
        Veto = "out-of-reach";
        if (nearest > reach - 1)
            return null;

        int ordinary = _gun.LaneValue(
            _context.Self.Facing,
            _context.Self.Position,
            ticksFromNow: commit);
        bool clumped = _context.Enemies.Count(enemy =>
            _context.Self.Position.ChebyshevDistance(enemy.Position) <= 5) >= 2;
        int aimedValue = aimed?.Score ?? 0;
        // Measured, not assumed: priced loosely, the cast LOSES to an otherwise
        // identical build that never casts — two immobile windup ticks plus the
        // stance gun's slower cadence are worth more than three bolts that only
        // graze. It pays only when the fan leaves the target nowhere to step
        // (the pressure score's forced-hit term) or when one fan reaches two
        // bodies, and only at knife range where the lanes have not yet diverged.
        // The fan's own niche, and the one case where it costs nothing: this
        // chassis declares an initial aim range of ZERO and a bend cannot start
        // before the first tile, so a body inside the near diagonal cannot be
        // shot at all by the ordinary gun. When the gun has no arc and the fan
        // does, the cast is not a tempo trade — it is the only weapon.
        bool blindSpot = aimedValue == 0
            && ordinary == 0
            && best > 0
            && nearest <= 3;
        Veto = $"lane{best}v{ordinary}a{aimedValue}n{nearest}b{(blindSpot ? 1 : 0)}";
        // Measured pricing, kept deliberately narrow: a striker that searches its
        // whole bend envelope per tick delivers three AIMED bolts in the time a cast
        // delivers three unaimed ones, so a generally-permissive cast loses to an
        // otherwise identical build that never casts. What survives measurement is
        // the blind spot and the multi-body fan.
        if (!blindSpot
            && !(clumped && best >= 12 && nearest <= 3))
            return null;
        Veto = "cast";

        return new GenericActorDecision(
            transform.ActionId,
            transform.ActionCode,
            [
                new GenericActorActionArgument.FormTargetArgument(
                    route.TargetFormId),
            ],
            $"casting {route.TargetFormId} over {commit} committed ticks");
    }

    /// <summary>
    /// A fortified route that trades objective weight away — the classless
    /// profile's anchor. Taken only when this team's presence on the objective
    /// survives without this body, because a body that fortifies before relief
    /// exists has deleted its own scoring presence.
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
        if (route is null)
            return null;

        // Relief must already exist: enough allied weight on the objective
        // without this body, and a real target to cover.
        int weightWithoutMe = keel.OwnWeight
            - (keel.SelfPresent
                ? _facts.ObjectiveWeight(_context.Self.FormId)
                : 0);
        if (weightWithoutMe <= keel.EnemyWeight
            || _context.Allies.Length < 1
            || _context.Enemies.IsEmpty
            || _facts.TransitionForbidden.Contains(_context.Self.Position)
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
