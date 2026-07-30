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

    public ArcStance(
        ArcFacts facts,
        GenericActorContext context,
        ArcThreat threat,
        ArcGun gun,
        ArcMemory memory)
    {
        _facts = facts;
        _context = context;
        _threat = threat;
        _gun = gun;
        _memory = memory;
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

        (int bodies, int _) = _gun.FanForecast(
            _context.Self.FormId,
            _context.Self.Facing,
            _context.Self.Position,
            ticksFromNow: 0);
        if (bodies > 0 && Fire() is GenericActorDecision connected)
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
        if (stuck && Leave() is GenericActorDecision leave)
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

        (int bodies, int supply) = _gun.FanForecast(
            _context.Self.FormId,
            _context.Self.Facing,
            _context.Self.Position,
            ticksFromNow: 0);
        int bestValue = bodies + supply;
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
            (int turned, int turnedSupply) = _gun.FanForecast(
                _context.Self.FormId,
                facing,
                _context.Self.Position,
                ticksFromNow: 1);
            int cover = _gun.FanCoverage(
                _context.Self.FormId,
                facing,
                _context.Self.Position,
                ticksFromNow: 1);
            int value = turned + turnedSupply;
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

        Veto = "placement";
        if (!_facts.PlacementAllows(route, _context.Self.Position))
            return null;

        int commit = ArcFacts.CommitTicks(route);
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
        (int bodies, int supply) = _gun.FanForecast(
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
        int value = bodies + (bodies >= 2 && supply > 0 ? 1 : 0);
        int guns = _threat.Bearing(_context.Self.Position);
        Veto = $"pay{bodies}s{supply}r{required}g{guns}";

        if (value < required)
            return null;
        // A gun already pointed at this tile is a windup the opposition gets to
        // shoot into, so the surplus over break-even IS the budget for loaded
        // guns: none at break-even, one bolt of slack per extra body hit. A
        // three-body fan against two bearings is a trade a 3-health chassis wins;
        // a two-body fan against one is not.
        if (guns > Math.Max(0, value - required))
            return null;

        int cycle = _facts.StanceCycleTicks(route);
        if (_memory.LastCastTile == _context.Self.Position
            && _context.Tick - _memory.LastCastTick < cycle * 2
            && value <= required)
        {
            Veto = "resquat";
            return null;
        }

        // The aimed bolt still wins ties: it lands this tick, it is aimed rather
        // than pointed, and it leaves the body free to step.
        if (aimed is not null && value <= required && aimed.Score >= 60)
        {
            Veto = "aimed-better";
            return null;
        }

        Veto = "cast";
        return new GenericActorDecision(
            transform.ActionId,
            transform.ActionCode,
            [
                new GenericActorActionArgument.FormTargetArgument(
                    route.TargetFormId),
            ],
            $"casting into {value} bearings over {commit} committed ticks");
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
        if (!_facts.PlacementAllows(route, _context.Self.Position))
            return null;
        int commit = ArcFacts.CommitTicks(route);
        int required = _facts.RequiredFanHits(route, _context.Self.FormId);
        (int here, int hereSupply) = _gun.FanForecast(
            route.TargetFormId,
            _context.Self.Facing,
            _context.Self.Position,
            commit);
        if (here + hereSupply >= required)
            return null;

        Direction? best = null;
        int bestValue = here + hereSupply;
        foreach (Direction facing in ArcBoard.Cardinals)
        {
            if (facing == _context.Self.Facing)
                continue;
            (int bodies, int supply) = _gun.FanForecast(
                route.TargetFormId,
                facing,
                _context.Self.Position,
                commit + 1);
            if (bodies + supply > bestValue)
            {
                bestValue = bodies + supply;
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
        if (route is null)
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
        if (route is null)
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
