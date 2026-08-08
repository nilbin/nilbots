using BotArena.Sdk;

/// <summary>
/// A competent apprentice MIND: useful immediately, deliberately unsolved.
///
/// <para><b>The mental model, in four sentences.</b> Nilbots creates exactly
/// ONE of this object per match, for you, and it lives from before tick 0 to
/// the terminal tick. <see cref="Think"/> is called exactly once per tick,
/// unconditionally — including ticks on which you own no live body at all —
/// and <c>mind.Bodies</c> is every body you currently command. Your fields ARE
/// your memory: there is no memory API to learn, and nothing is cleared when a
/// body dies. You do not return a decision; you WRITE commands onto bodies, and
/// every live body you do not write to simply waits.</para>
///
/// <para>That last rule is the one to internalize, because it inverts the old
/// one: forgetting a body costs that body one tick, visibly, in the replay —
/// not the match. There is no key set to get right and no exception to catch.
/// Commanding a body that just died is <c>Rejected</c> and recorded, which is
/// forgivable on purpose; commanding the same body twice is a fault, which is
/// not, because it means you decided twice and did not notice.</para>
///
/// <para><b>What this scaffold does.</b> It reads the contract, assigns roles
/// over its bodies once per tick (Roles.cs), and then runs one small method per
/// role. It publishes each assignment with <c>SetRole</c>, which shows up under
/// the body in the replay viewer and — deliberately — to the opponent, who can
/// read it and be lied to. Start by editing Roles.cs; the role methods below
/// are the second thing to change and the tick loop itself is rarely the
/// thing that is wrong.</para>
/// </summary>
public sealed class BOTNAME : IGenericMindBot
{
    private readonly Recall _recall = new();

    /// <summary>
    /// The build order, authored ONCE, at match start. It outlives every body
    /// that executes it, which is the thing a per-life bot structurally could
    /// not do: the fabricator that decided to build died, and its successor
    /// started with empty memory and no idea what it had been in the middle of.
    /// </summary>
    private readonly Queue<int> _buildOrder = new();

    private GenericActorResolvedMatchContract? _contract;
    private int _teamId;

    /// <summary>
    /// A courier run that survives its executor. When the body carrying it
    /// dies, the run is handed to another body rather than abandoned.
    /// </summary>
    private CourierRun? _run;

    public void StartMatch(MindStart start)
    {
        _contract = start.Contract;
        _teamId = start.TeamId;

        // Slots you own, whether or not they are alive yet. Queueing them here
        // is the build order; a mind can decide a body's job before that body
        // exists.
        foreach (int unitId in start.Contract.Topology.UnitSlots
                     .Where(slot =>
                         slot.ControllerParticipantId == start.ParticipantId)
                     .Select(slot => slot.UnitId)
                     .OrderBy(unitId => unitId)
                     .Skip(1))
        {
            _buildOrder.Enqueue(unitId);
        }
    }

    public void Think(MindContext mind)
    {
        GenericActorResolvedMatchContract contract = _contract
            ?? throw new InvalidOperationException("StartMatch was not called.");

        // Memory first, every tick, even with no bodies: the enemy is still
        // moving and beliefs go stale fastest exactly when you cannot see.
        _recall.Observe(mind);
        if (mind.Bodies.IsEmpty)
        {
            // Turn-scoped diagnostics: a mind decides once per tick over the
            // whole army, so this rides the replay's mind turn rather than any
            // one body's command — which is the only place it COULD ride on a
            // tick with no bodies at all.
            mind.Debug.Write(
                $"no live bodies; {mind.Slots.Length} slots pending");
            return;
        }

        RoleMap roles = Roles.Assign(contract, mind, _recall);
        // One tile-claim set for the whole army. This is the entire
        // collision-avoidance system, and it replaces the several-hundred-line
        // "who goes first" machinery every per-life bot in the last cohort had
        // to write, because there is one decider now.
        var claims = ArenaBasics.Claims.ForTick(mind);

        // Hand the courier run to a live body when its carrier is gone. The
        // plan outlives the body; that is the point.
        if (_run is not null && !mind.TryBody(_run.UnitId, out _))
            _run = Reassign(mind, roles, _run);

        foreach (MindBody body in mind.Bodies)
        {
            Role role = roles[body];
            body.SetRole(Label(role));
            switch (role)
            {
                case Role.Channeler:
                    Channel(contract, mind, body, claims);
                    break;
                case Role.Screen:
                    Screen(contract, mind, body, roles.Channeler, claims);
                    break;
                case Role.Courier:
                    Courier(contract, mind, body, claims);
                    break;
                case Role.Builder:
                    Build(contract, mind, body, claims);
                    break;
                default:
                    Reserve(contract, mind, body, claims);
                    break;
            }
        }
    }

    public void EndMatch(MindEnd end) => _ = end;

    /// <summary>
    /// Holds the point. Under the capture channel the claim is built by bodies
    /// that did NOT change tile, so holding still is the action — which is why
    /// it is stated explicitly with a reason rather than left to the pre-filled
    /// wait. A shot is free while standing still; a step is not.
    /// </summary>
    private void Channel(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        ArenaBasics.Claims claims)
    {
        Position[] objective = ArenaBasics.ActiveObjectiveTiles(contract, mind);
        if (!objective.Contains(body.Position))
        {
            if (ArenaBasics.TryDodge(contract, mind, body, claims))
                return;
            if (ArenaBasics.TryStepToward(
                    contract,
                    mind,
                    body,
                    objective,
                    claims,
                    "taking the point"))
            {
                return;
            }
            if (ArenaBasics.TryShoot(contract, mind, body))
                return;
            body.Hold("no route to the point");
            return;
        }

        // Standing on it. Shoot if the geometry is there — attacking never
        // breaks stillness — otherwise say the hold out loud.
        if (ArenaBasics.TryShoot(contract, mind, body))
            return;
        body.Hold("claiming");
    }

    /// <summary>
    /// Stands between the channeler and the shooting. Damage to a controller
    /// standing on the objective reverts its whole run under the channel, so a
    /// screen is not a bodyguard — it is the thing that makes the claim
    /// survivable, and it is the set-piece this profile exists to make
    /// authorable.
    /// </summary>
    private void Screen(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        MindBody? channeler,
        ArenaBasics.Claims claims)
    {
        if (channeler is null)
        {
            Reserve(contract, mind, body, claims);
            return;
        }

        // Where the damage comes from: the nearest visible enemy, and when
        // none is visible, the direction we are pushing INTO — which is where
        // the opposition is, whether or not anyone can see it. Screening only
        // when an enemy happens to be on screen would mean never screening
        // before contact, which is exactly when the escort has to already be
        // in place.
        Position threat = mind.Enemies
            .OrderBy(enemy =>
                channeler.Position.ChebyshevDistance(enemy.Position))
            .ThenBy(enemy => enemy.ActorId)
            .Select(enemy => enemy.Position)
            .FirstOrDefault(Forward(contract, channeler, _teamId));

        Position[] interpose = Interpose(channeler.Position, threat);
        if (!interpose.Contains(body.Position)
            && ArenaBasics.TryStepToward(
                contract,
                mind,
                body,
                interpose,
                claims,
                "screening"))
        {
            return;
        }
        if (ArenaBasics.TryShoot(contract, mind, body))
            return;
        body.Hold("screening");
    }

    /// <summary>
    /// Fetches scrap and banks it. The run is a field on the mind, so it
    /// survives the body executing it — the deep-carry game the last cohort
    /// mostly refused, because committing a body to a multi-life errand was
    /// strictly riskier than not when nothing could carry the plan.
    /// </summary>
    private void Courier(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        ArenaBasics.Claims claims)
    {
        if (ArenaBasics.TryDodge(contract, mind, body, claims))
            return;

        Position? pile = _recall.BestPile(mind);
        if (_run is null && pile is Position target)
            _run = new CourierRun(body.UnitId, target);
        if (_run is null)
        {
            Reserve(contract, mind, body, claims);
            return;
        }

        // Loaded bodies go home; empty ones go out. Home is where automatic
        // arrivals land, which the contract decides — never a constant.
        Position[] goal = body.CarriedScrap > 0
            ? ArenaBasics.ExpectedArrivalTiles(
                contract,
                mind,
                _teamId,
                body.UnitId)
            : [_run.Target];
        if (ArenaBasics.TryStepToward(
                contract,
                mind,
                body,
                goal,
                claims,
                body.CarriedScrap > 0 ? "banking" : "fetching"))
        {
            return;
        }
        if (ArenaBasics.TryShoot(contract, mind, body))
            return;
        body.Hold("courier idle");
    }

    /// <summary>
    /// Works the build order. The role of the body being built was decided
    /// before it existed; the assignment applies on its first tick.
    /// </summary>
    private void Build(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        ArenaBasics.Claims claims)
    {
        int? next = _buildOrder.Count > 0 ? _buildOrder.Peek() : null;
        if (ArenaBasics.TryFabricate(contract, body, next))
        {
            if (_buildOrder.Count > 0)
                _buildOrder.Dequeue();
            return;
        }
        Reserve(contract, mind, body, claims);
    }

    /// <summary>
    /// Anything without a job: stay useful, stay alive, stay near the fight.
    /// </summary>
    private void Reserve(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        ArenaBasics.Claims claims)
    {
        if (ArenaBasics.TryDodge(contract, mind, body, claims))
            return;
        if (ArenaBasics.TryShoot(contract, mind, body))
            return;
        if (ArenaBasics.TryStepToward(
                contract,
                mind,
                body,
                ArenaBasics.ActiveObjectiveTiles(contract, mind),
                claims,
                "closing"))
        {
            return;
        }
        body.Hold("holding position");
    }

    /// <summary>Hands an orphaned run to another body, or drops it.</summary>
    private static CourierRun? Reassign(
        MindContext mind,
        RoleMap roles,
        CourierRun run)
    {
        MindBody? heir = mind.Bodies
            .Where(body => roles[body] is Role.Reserve or Role.Courier)
            .OrderBy(body => body.Position.ChebyshevDistance(run.Target))
            .ThenBy(body => body.UnitId)
            .FirstOrDefault();
        return heir is null ? null : run with { UnitId = heir.UnitId };
    }

    /// <summary>
    /// A point well ahead of the channeler along this team's own advance, used
    /// as the threat axis when nothing hostile is visible yet.
    /// </summary>
    private static Position Forward(
        GenericActorResolvedMatchContract contract,
        MindBody channeler,
        int teamId)
    {
        Direction? advance = ArenaBasics.AdvanceDirection(contract, teamId);
        if (advance is not Direction direction)
            return channeler.Position;
        (int dx, int dy) = direction.Vector();
        return channeler.Position.Offset(dx * 3, dy * 3);
    }

    /// <summary>The tiles between a protected body and a threat.</summary>
    private static Position[] Interpose(Position protectedTile, Position threat)
    {
        int dx = Math.Sign(threat.X - protectedTile.X);
        int dy = Math.Sign(threat.Y - protectedTile.Y);
        if (dx == 0 && dy == 0)
            return [protectedTile];
        return
        [
            protectedTile.Offset(dx, dy),
            protectedTile.Offset(dx, 0),
            protectedTile.Offset(0, dy),
        ];
    }

    /// <summary>
    /// The published label. Free vocabulary on purpose: the words you choose
    /// are your strategy made legible, and they are shown on your bodies AND
    /// on your visible bodies to the opponent — so a deliberately wrong label
    /// is a real move that costs nothing.
    /// </summary>
    private static string Label(Role role) =>
        role switch
        {
            Role.Channeler => "channeler",
            Role.Screen => "screen",
            Role.Courier => "courier",
            Role.Builder => "builder",
            _ => "reserve",
        };

    /// <param name="UnitId">The body currently executing the run.</param>
    /// <param name="Target">Where the scrap is.</param>
    private sealed record CourierRun(int UnitId, Position Target);
}
