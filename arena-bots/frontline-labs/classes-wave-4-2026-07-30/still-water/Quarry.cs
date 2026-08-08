using BotArena.Sdk;

/// <summary>
/// Life-scoped memory of where enemy bodies were and which way they were going.
/// A fresh body starts blank by design, so this only ever holds what this life
/// has personally seen through the team's frozen perception union.
/// </summary>
internal sealed class QuarryTracker
{
    private readonly Dictionary<ActorIdentity, Trace> _traces = [];

    public sealed class Trace
    {
        public Position Position { get; set; }

        public int DeltaX { get; set; }

        public int DeltaY { get; set; }

        public int LastSeenTick { get; set; } = int.MinValue;

        /// <summary>Consecutive observed ticks this body has not changed tile.</summary>
        public int StillTicks { get; set; }

        public int Health { get; set; }

        public string FormId { get; set; } = string.Empty;

        /// <summary>
        /// Last tick this body was seen to fire. Enemy cooldown is redacted, but
        /// the attack event is not: with the declared cooldown of the form that
        /// fired, this dates the earliest tick the gun can speak again. That is
        /// the whole window in which a blind windup is affordable.
        /// </summary>
        public int LastAttackTick { get; set; } = int.MinValue;

        /// <summary>
        /// Deflections this body has been seen to make since it was first
        /// observed. A guard with a declared budget breaks on the last one, and
        /// the break is a forced return — the punish window.
        /// </summary>
        public int Deflections { get; set; }
    }

    public void Observe(GenericActorContext context)
    {
        int teamId = context.Self.ActorId.TeamId;

        // A body that died is not a ghost worth respecting; forget it so the
        // posture logic can see that the ground in front is genuinely empty.
        foreach (var visible in context.VisibleEvents)
        {
            switch (visible.Payload)
            {
                case GenericActorContext.EventPayload.Destruction destroyed:
                    _traces.Remove(destroyed.ActorId);
                    break;
                case GenericActorContext.EventPayload.LifeRetired retired:
                    _traces.Remove(retired.ActorId);
                    break;
                case GenericActorContext.EventPayload.Attack fired
                    when fired.ActorId.TeamId != teamId:
                    Touch(fired.ActorId).LastAttackTick = visible.SourceTick;
                    break;
                // The guard is the deflection's TARGET: it is the body the bolt
                // arrived at. The returned bolt now belongs to its team and is
                // an ordinary hostile projectile in visibleProjectiles.
                case GenericActorContext.EventPayload.ProjectileDeflected shell
                    when shell.TargetActorId.TeamId != teamId:
                    Touch(shell.TargetActorId).Deflections++;
                    break;
            }
        }

        foreach (var enemy in context.Enemies)
        {
            if (!_traces.TryGetValue(enemy.ActorId, out Trace? trace))
            {
                trace = new Trace();
                _traces[enemy.ActorId] = trace;
            }
            else if (trace.LastSeenTick == context.Tick - 1)
            {
                trace.DeltaX = Math.Sign(enemy.Position.X - trace.Position.X);
                trace.DeltaY = Math.Sign(enemy.Position.Y - trace.Position.Y);
                trace.StillTicks = trace.Position == enemy.Position
                    ? trace.StillTicks + 1
                    : 0;
            }
            else
            {
                trace.DeltaX = 0;
                trace.DeltaY = 0;
                trace.StillTicks = 0;
            }
            trace.Position = enemy.Position;
            trace.LastSeenTick = context.Tick;
            trace.Health = enemy.Health;
            trace.FormId = enemy.FormId;
        }

        foreach (var actorId in _traces.Keys.ToList())
        {
            Trace trace = _traces[actorId];
            // A trace may exist because the body was HEARD firing rather than
            // seen, so age off the more recent of the two clocks, in long
            // arithmetic because the sentinel is int.MinValue.
            long fresh = Math.Max(
                (long)trace.LastSeenTick,
                trace.LastAttackTick);
            if (fresh > int.MinValue && context.Tick - fresh > 40)
                _traces.Remove(actorId);
        }
    }

    public Trace? Get(ActorIdentity actorId) =>
        _traces.TryGetValue(actorId, out Trace? trace) ? trace : null;

    private Trace Touch(ActorIdentity actorId)
    {
        if (!_traces.TryGetValue(actorId, out Trace? trace))
        {
            trace = new Trace();
            _traces[actorId] = trace;
        }
        return trace;
    }

    /// <summary>Last known tiles of enemies this life can no longer see.</summary>
    public List<(Position Position, int Age)> Ghosts(
        GenericActorContext context,
        int maxAge)
    {
        var visible = context.Enemies
            .Select(enemy => enemy.ActorId)
            .ToHashSet();
        var ghosts = new List<(Position, int)>();
        foreach (var pair in _traces)
        {
            if (visible.Contains(pair.Key)
                || pair.Value.LastSeenTick == int.MinValue)
            {
                continue;
            }
            int age = context.Tick - pair.Value.LastSeenTick;
            if (age >= 0 && age <= maxAge)
                ghosts.Add((pair.Value.Position, age));
        }
        return ghosts;
    }
}

/// <summary>
/// A per-tick forecast of one enemy body: how far it can walk, where its last
/// heading takes it, where the objective pulls it, and therefore how much a
/// trajectory that arrives on a given tile at a given tick is worth. This is
/// what turns a private bend into a prediction rather than a guess.
/// </summary>
internal sealed class EnemyForecast
{
    private readonly Field _field;
    private readonly int[] _depth;
    private readonly Position[] _drift;
    private readonly Position[] _approach;
    private readonly HashSet<Position> _objective;
    private readonly int _holdBase;
    private readonly bool _trustApproach;

    public EnemyForecast(
        Field field,
        Doctrine doctrine,
        GenericActorContext.ObservedEnemyState enemy,
        QuarryTracker.Trace? trace,
        int[] objectiveCost,
        HashSet<Position> objective,
        int maxSteps)
    {
        _field = field;
        State = enemy;
        _objective = objective;
        MaxSteps = Math.Max(1, maxSteps);
        _depth = field.CostField([enemy.Position], null);

        var form = doctrine.Form(enemy.FormId);
        MaxHealth = form?.MaxHealth ?? enemy.Health;
        Guarded = doctrine.Guarded(enemy.FormId);
        FanBolts = doctrine.BoltsPerAttack(enemy.FormId);

        // Whether that body can still take a step is its form's declared action
        // mask, not its objective weight. A stance keeps weight 1 and cannot
        // move; revision 3 read the weight and therefore predicted a parked
        // stance as if it were about to walk, which is the single most valuable
        // prediction in the game to get right — an immobile body is an exact
        // aim point for as long as it stays immobile.
        bool canWalk = doctrine.FormAllows(
            enemy.FormId,
            GenericActorRulesContract.ActionKind.Movement);

        // A windup is wait-only on this contract's declared policy, so a body
        // inside one cannot move either — and lethal damage cancels it, which
        // makes the windup the cheapest kill window the ruleset offers.
        Winding = enemy.PendingSameLifeTransition is not null;
        WaitOnly = Winding
            && doctrine.WindupIsWaitOnly(
                enemy.PendingSameLifeTransition!.TransitionId);
        Mobile = canWalk && !WaitOnly;

        // A body that has been standing still is evidence, not noise: a shot
        // aimed where it already is beats a shot aimed where it might go.
        int still = trace?.StillTicks ?? 0;
        _holdBase = 5 + Math.Min(5, still);
        _trustApproach = still < 2;

        _drift = new Position[MaxSteps + 1];
        _approach = new Position[MaxSteps + 1];
        _drift[0] = enemy.Position;
        _approach[0] = enemy.Position;

        int dx = trace?.DeltaX ?? 0;
        int dy = trace?.DeltaY ?? 0;
        Position cursor = enemy.Position;
        for (int step = 1; step <= MaxSteps; step++)
        {
            Position next = cursor.Offset(dx, dy);
            if ((dx == 0 && dy == 0) || field.IsWall(next))
                next = cursor;
            _drift[step] = next;
            cursor = next;
        }

        cursor = enemy.Position;
        for (int step = 1; step <= MaxSteps; step++)
        {
            Position best = cursor;
            int bestCost = field.Cost(objectiveCost, cursor);
            foreach (Direction direction in doctrine.EnemyApproachOrder)
            {
                (int ndx, int ndy) = direction.Vector();
                Position candidate = cursor.Offset(ndx, ndy);
                if (field.IsWall(candidate))
                    continue;
                int cost = field.Cost(objectiveCost, candidate);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = candidate;
                }
            }
            _approach[step] = best;
            cursor = best;
        }
    }

    public GenericActorContext.ObservedEnemyState State { get; }

    public bool Mobile { get; }

    /// <summary>Whether a same-life transition windup is running on this body.</summary>
    public bool Winding { get; }

    /// <summary>
    /// Whether that windup forbids everything but waiting. Such a body cannot
    /// dodge, cannot shoot, and dies to lethal damage before the form arrives.
    /// </summary>
    public bool WaitOnly { get; }

    /// <summary>Whether this body deflects bolts arriving inside its facing quadrant.</summary>
    public bool Guarded { get; }

    /// <summary>Bolts one attack from this body's current form launches.</summary>
    public int FanBolts { get; }

    public int MaxHealth { get; }

    public int MaxSteps { get; }

    /// <summary>
    /// Plausibility that this body occupies <paramref name="tile"/> after
    /// <paramref name="steps"/> of its own decisions. Zero means unreachable.
    /// </summary>
    public int Weight(Position tile, int steps)
    {
        if (steps < 0 || steps > MaxSteps)
            return 0;
        int depth = _field.Cost(_depth, tile);
        if (depth == int.MaxValue)
            return 0;
        if (!Mobile)
            return tile == State.Position ? 12 : 0;
        if (depth > steps)
            return 0;

        int weight = 1;
        if (tile == State.Position)
            weight += Math.Max(2, _holdBase - steps);
        if (tile == _drift[steps] && _drift[steps] != State.Position)
            weight += Math.Max(1, 4 - steps);
        if (_trustApproach
            && tile == _approach[steps]
            && _approach[steps] != State.Position)
        {
            weight += 2;
        }
        if (_objective.Contains(tile))
            weight += 2;
        return weight;
    }

    /// <summary>
    /// Whether the tile is a named prediction — where the body is, where its
    /// last heading takes it, or where the objective pulls it — rather than
    /// merely somewhere it could wander. Speculative sweep is worth firing on
    /// an idle tick; it is not worth spending a loaded gun on.
    /// </summary>
    public bool IsAnchored(Position tile, int steps)
    {
        if (steps < 0 || steps > MaxSteps)
            return false;
        if (!Mobile)
            return tile == State.Position;
        return tile == State.Position
            || tile == _drift[steps]
            || (_trustApproach && tile == _approach[steps]);
    }

    /// <summary>Finishing bonus: a body one hit from death is worth chasing.</summary>
    public double Pressure(int damagePerHit) =>
        State.Health <= damagePerHit ? 2.0 : 1.0;
}
