using BotArena.Sdk;

public enum TacticalMovementAuthority
{
    Tactical,
    Safety,
    Clearance,
}

/// <summary>
/// Pure fight-episode bounds shared by target selection and focused tests.
/// The distance is measured from the episode's original anchor, never from
/// the body's current tile, so yielding to another order cannot inch the
/// leash forward over successive ticks.
/// </summary>
public static class TacticalFightEpisode
{
    public static bool TargetIsReachable(
        int? anchorToTargetDistance,
        int attackRange,
        int chaseLimit) => anchorToTargetDistance is int distance
        && distance <= attackRange + chaseLimit;
}

/// <summary>
/// Authored importance of one selected order when allied bodies compete for
/// movement space. It never participates in order selection: source position
/// has already selected the order before this value reaches the movement
/// plane.
/// </summary>
public enum TacticalMovementUrgency
{
    Routine,
    Normal,
    Urgent,
}

/// <summary>
/// One body's fully resolved position on the allied movement plane. Lower
/// <see cref="Rank"/> moves first and receives right-of-way. The remaining
/// fields are evidence, not additional policy hidden at a call site.
/// </summary>
public sealed record TacticalMovementPriority(
    int UnitId,
    int Rank,
    TacticalMovementUrgency AuthoredUrgency,
    TacticalMovementUrgency EffectiveUrgency,
    int AlliedBlockDebt,
    int PassageLeaseTicksRemaining,
    int ActivatedTick,
    int? BlockedSince)
{
    public string Trace =>
        $"{AuthoredUrgency.ToString().ToLowerInvariant()}"
        + $"+b{AlliedBlockDebt}"
        + (PassageLeaseTicksRemaining > 0
            ? $"+passage{PassageLeaseTicksRemaining}"
            : "")
        + (EffectiveUrgency == AuthoredUrgency
            ? ""
            : $"=>{EffectiveUrgency.ToString().ToLowerInvariant()}");
}

/// <summary>
/// Resolves the compact pre-combat posture vocabulary against one authored
/// destination. Posture changes which legal goal tile a body approaches; it
/// never changes the body's order, fight policy, or any other body's position.
/// Keeping this transform pure makes west/east rotation and tie-breaking
/// independently testable from pathfinding.
/// </summary>
public static class TacticalPosturePlanner
{
    public static Position[] Resolve(
        IReadOnlyCollection<Position> goals,
        string? posture,
        Position ownReactor,
        Position enemyReactor)
    {
        Position[] values = [.. goals];
        if (values.Length <= 1 || posture is null or "line")
            return values;
        Func<Position, int> distance = posture == "front"
            ? point => point.ChebyshevDistance(enemyReactor)
            : point => point.ChebyshevDistance(ownReactor);
        int best = values.Min(distance);
        return values.Where(point => distance(point) == best)
            .OrderBy(point => point.Y)
            .ThenBy(point => point.X)
            .ToArray();
    }
}

/// <summary>
/// Resolves a chain of lower-precedence allied bodies between a mover and the
/// nearest body that can step into a real vacancy. The movement plane still
/// owns every actual step; this only propagates one existing right-of-way
/// request far enough for a vacancy to travel back through the queue.
/// </summary>
public static class TacticalClearancePlanner
{
    /// <summary>
    /// The shortest deterministic chain from <paramref name="firstBlocker"/>
    /// to a body with a free exit. Successor unit id breaks equal-length
    /// choices. An empty result means that the occupied component has no
    /// vacancy; no body is displaced speculatively in that case.
    /// </summary>
    public static int[] Resolve(
        int firstBlocker,
        IReadOnlyCollection<Node> nodes)
    {
        Dictionary<int, Node> byUnit = nodes.ToDictionary(
            node => node.UnitId);
        if (!byUnit.ContainsKey(firstBlocker))
            return [];

        var queue = new Queue<int>();
        var previous = new Dictionary<int, int?>
        {
            [firstBlocker] = null,
        };
        queue.Enqueue(firstBlocker);
        while (queue.Count > 0)
        {
            int unit = queue.Dequeue();
            Node node = byUnit[unit];
            if (node.HasExit)
                return Reconstruct(unit, previous);

            foreach (int successor in node.OccupiedSuccessors
                         .Where(byUnit.ContainsKey)
                         .Order())
            {
                if (previous.ContainsKey(successor))
                    continue;
                previous[successor] = unit;
                queue.Enqueue(successor);
            }
        }
        return [];
    }

    private static int[] Reconstruct(
        int terminal,
        IReadOnlyDictionary<int, int?> previous)
    {
        var reversed = new List<int>();
        int? cursor = terminal;
        while (cursor is int unit)
        {
            reversed.Add(unit);
            cursor = previous[unit];
        }
        reversed.Reverse();
        return [.. reversed];
    }

    public sealed record Node(
        int UnitId,
        bool HasExit,
        IReadOnlyCollection<int> OccupiedSuccessors);
}

/// <summary>
/// Cross-tick ownership of an authored route's terrain course. The sheet owns
/// the route and the ordered rung; this governor owns only the deterministic
/// way from the body's current tile to the rung's current route target.
/// </summary>
/// <remarks>
/// A terrain-shortest path can have two equally good ways around a durable
/// obstacle. Re-running that choice from the next tile is not hysteresis: a
/// body can take one step south, discover that north is now cheaper, and undo
/// the step forever. The selected course therefore survives until the body
/// consumes it, the authored route cursor changes, the rung/route/life changes,
/// a real displacement takes the body off it, or the next tile becomes a
/// durable impossibility.
/// </remarks>
public sealed class TacticalRouteGovernor
{
    private readonly Dictionary<int, State> _states = [];

    public CourseStep Resolve(
        ActorIdentity actor,
        string orderId,
        string routeId,
        int lap,
        int waypoint,
        Position position,
        Position destination,
        Func<Position[]?> plan,
        Func<Position, bool> impossible)
    {
        State? prior = _states.GetValueOrDefault(actor.UnitId);
        string reason;
        State state;
        if (prior is null
            || prior.Actor != actor
            || !string.Equals(prior.OrderId, orderId,
                StringComparison.Ordinal)
            || !string.Equals(prior.RouteId, routeId,
                StringComparison.Ordinal)
            || prior.Lap != lap)
        {
            reason = "route-commit";
            state = Start(
                actor, orderId, routeId, lap, waypoint,
                position, destination, plan);
        }
        else if (prior.Waypoint != waypoint
            || prior.Destination != destination)
        {
            reason = "route-advance";
            state = Start(
                actor, orderId, routeId, lap, waypoint,
                position, destination, plan);
        }
        else if (prior.Detour.Length > 0)
        {
            state = prior;
            Position rejoin = state.Course[state.DetourRejoinIndex];
            if (position == rejoin)
            {
                reason = "route-detour-rejoined";
                state = state with
                {
                    NextIndex = Math.Max(
                        state.NextIndex,
                        state.DetourRejoinIndex + 1),
                    Detour = [],
                    DetourNextIndex = 0,
                    DetourRejoinIndex = -1,
                };
            }
            else
            {
                int current = CourseIndex(
                    state.Detour,
                    state.DetourNextIndex,
                    position);
                if (current < 0 || current + 1 < state.DetourNextIndex)
                {
                    reason = "route-rejoin";
                    state = Start(
                        actor, orderId, routeId, lap, waypoint,
                        position, destination, plan);
                }
                else
                {
                    reason = "route-detour-held";
                    state = state with
                    {
                        DetourNextIndex = Math.Max(
                            state.DetourNextIndex,
                            current + 1),
                    };
                }
            }
        }
        else
        {
            state = prior;
            int current = CourseIndex(state, position);
            if (current < 0 || current + 1 < state.NextIndex)
            {
                reason = "route-rejoin";
                state = Start(
                    actor, orderId, routeId, lap, waypoint,
                    position, destination, plan);
            }
            else
            {
                int nextIndex = Math.Max(state.NextIndex, current + 1);
                state = state with { NextIndex = nextIndex };
                reason = "route-held";
            }
        }

        Position? next = Next(state);
        if (next is Position blocked && impossible(blocked))
        {
            reason = "route-replan-impossible";
            state = Start(
                actor, orderId, routeId, lap, waypoint,
                position, destination, plan);
            next = Next(state);
        }

        _states[actor.UnitId] = state;
        CourseDisposition disposition = next is not null
            ? CourseDisposition.Course
            : state.Course.Length == 0
                ? CourseDisposition.NoPath
                : CourseDisposition.Arrived;
        string trace = next is Position tile
            ? $"{reason}-next-{tile.X}-{tile.Y}"
            : state.Course.Length == 0
                ? $"{reason}-no-path"
                : $"{reason}-arrived";
        return new CourseStep(next, trace, disposition);
    }

    /// <summary>
    /// Tries one bounded traffic detour without changing the held route,
    /// waypoint, lap, or terrain course. A successful detour rejoins a later
    /// tile of that exact course; a failed attempt returns the held step with
    /// an explicit wait trace. The caller owns the transient-obstacle view and
    /// the authored corridor budget.
    /// </summary>
    public CourseStep TryTrafficDetour(
        ActorIdentity actor,
        Position position,
        int maximumExtraSteps,
        Func<Position, Position[]?> plan)
    {
        State? state = _states.GetValueOrDefault(actor.UnitId);
        if (state is null || state.Actor != actor || state.Course.Length == 0)
        {
            return new CourseStep(
                null,
                "route-detour-considered-wait-no-course",
                CourseDisposition.NoPath);
        }

        Position? held = Next(state);
        if (state.Detour.Length > 0)
        {
            return Step(
                state,
                "route-detour-considered-wait-active");
        }

        int current = CourseIndex(state, position);
        if (current < 0)
        {
            return Step(
                state,
                "route-detour-considered-wait-off-course");
        }

        int firstRejoin = Math.Max(state.NextIndex + 1, current + 2);
        for (int rejoinIndex = firstRejoin;
             rejoinIndex < state.Course.Length;
             rejoinIndex++)
        {
            Position rejoin = state.Course[rejoinIndex];
            Position[]? detour = plan(rejoin);
            if (detour is not { Length: > 1 }
                || detour[0] != position
                || detour[^1] != rejoin)
            {
                continue;
            }

            int heldSteps = rejoinIndex - current;
            int detourSteps = detour.Length - 1;
            if (detourSteps > heldSteps + Math.Max(0, maximumExtraSteps))
                continue;

            State committed = state with
            {
                Detour = detour,
                DetourNextIndex = 1,
                DetourRejoinIndex = rejoinIndex,
            };
            _states[actor.UnitId] = committed;
            Position next = detour[1];
            return new CourseStep(
                next,
                $"route-detour-commit-rejoin-{rejoin.X}-{rejoin.Y}"
                    + $"-next-{next.X}-{next.Y}",
                CourseDisposition.Course);
        }

        return held is Position tile
            ? new CourseStep(
                tile,
                $"route-detour-considered-wait-next-{tile.X}-{tile.Y}",
                CourseDisposition.Course)
            : Step(state, "route-detour-considered-wait");
    }

    /// <summary>
    /// Bounds a waypoint's arrival ring below the length of the next leg. A
    /// route corridor controls reflow around the line; it is not permission
    /// to swallow the whole leg and consume the next point without moving.
    /// Rings may overlap on a grid, but <see cref="ReachedWaypoint"/> still
    /// requires newly measured approach before either point can advance.
    /// </summary>
    public static int EffectiveArrivalRadius(
        int requested,
        Position waypoint,
        Position nextWaypoint)
    {
        int separation = waypoint.ChebyshevDistance(nextWaypoint);
        int leavesOneStep = Math.Max(0, separation - 1);
        return Math.Min(Math.Max(0, requested), leavesOneStep);
    }

    /// <summary>
    /// Proximity alone never consumes a route point. The body must either
    /// stand on it or have made new measured progress toward it since this
    /// cursor was selected. This makes a route a directional hint rather
    /// than an arrival ring that can lap a stationary body.
    /// </summary>
    public static bool ReachedWaypoint(
        int distance,
        int previousBestDistance,
        int effectiveArrivalRadius) =>
        distance == 0
        || distance < previousBestDistance
            && distance <= effectiveArrivalRadius;

    /// <summary>
    /// A rung change is a semantic interruption even when the body later
    /// returns to the same route. The caller observes the selected rung every
    /// tick and explicitly releases that old commitment here.
    /// </summary>
    public void Reset(int unitId) => _states.Remove(unitId);

    private static int CourseIndex(State state, Position position)
        => CourseIndex(state.Course, state.NextIndex, position);

    private static int CourseIndex(
        Position[] course,
        int nextIndex,
        Position position)
    {
        int heldIndex = nextIndex - 1;
        if (heldIndex >= 0
            && heldIndex < course.Length
            && course[heldIndex] == position)
        {
            return heldIndex;
        }
        if (nextIndex < course.Length
            && course[nextIndex] == position)
        {
            return nextIndex;
        }
        return Array.IndexOf(course, position);
    }

    private static Position? Next(State state) =>
        state.Detour.Length > 0
            && state.DetourNextIndex < state.Detour.Length
                ? state.Detour[state.DetourNextIndex]
                : state.NextIndex < state.Course.Length
                    ? state.Course[state.NextIndex]
                    : null;

    private static CourseStep Step(State state, string reason)
    {
        Position? next = Next(state);
        return new CourseStep(
            next,
            next is Position tile
                ? $"{reason}-next-{tile.X}-{tile.Y}"
                : state.Course.Length == 0
                    ? $"{reason}-no-path"
                    : $"{reason}-arrived",
            next is not null
                ? CourseDisposition.Course
                : state.Course.Length == 0
                    ? CourseDisposition.NoPath
                    : CourseDisposition.Arrived);
    }

    private static State Start(
        ActorIdentity actor,
        string orderId,
        string routeId,
        int lap,
        int waypoint,
        Position position,
        Position destination,
        Func<Position[]?> plan)
    {
        Position[] course = plan() ?? [];
        if (course.Length > 0 && course[0] != position)
            course = [];
        return new State(
            actor,
            orderId,
            routeId,
            lap,
            waypoint,
            destination,
            course,
            course.Length > 1 ? 1 : course.Length,
            Detour: [],
            DetourNextIndex: 0,
            DetourRejoinIndex: -1);
    }

    public sealed record CourseStep(
        Position? Next,
        string Trace,
        CourseDisposition Disposition);

    public enum CourseDisposition
    {
        Course,
        Arrived,
        NoPath,
    }

    private sealed record State(
        ActorIdentity Actor,
        string OrderId,
        string RouteId,
        int Lap,
        int Waypoint,
        Position Destination,
        Position[] Course,
        int NextIndex,
        Position[] Detour,
        int DetourNextIndex,
        int DetourRejoinIndex);
}

/// <summary>
/// Bounded episode semantics for the executor-owned <c>recover</c> verb.
/// The sheet decides where recover sits in the ladder; this governor only
/// prevents one unchanged injury from holding that rung forever.
/// </summary>
public sealed class RecoveryEpisodeGovernor
{
    /// <summary>
    /// Eight tiles is the recover admission radius and ground healing costs
    /// three stationary ticks per health. Twenty-four ticks therefore pays
    /// for a full approach plus a substantial channel without becoming an
    /// indefinite tactical mode.
    /// </summary>
    public const int EpisodeTicks = 24;

    /// <summary>
    /// The same injury may try again after a bounded pause. A fresh health
    /// loss advances the injury revision and bypasses this gate immediately.
    /// </summary>
    public const int RetryCooldownTicks = 24;

    private readonly Dictionary<int, State> _states = [];

    public Admission Observe(
        ActorIdentity actor,
        int tick,
        int health,
        int maximumHealth,
        bool otherwiseEligible,
        Position? candidateTarget = null)
    {
        State state = _states.GetValueOrDefault(actor.UnitId)
            ?? State.For(actor, health);
        if (state.Actor != actor)
            state = State.For(actor, health);

        int injuryRevision = state.InjuryRevision
            + (health < state.LastHealth ? 1 : 0);
        state = state with
        {
            LastHealth = health,
            InjuryRevision = injuryRevision,
        };

        if (health >= maximumHealth)
        {
            state = state with
            {
                ActiveSince = null,
                CooldownUntil = 0,
                BlockedInjuryRevision = -1,
                LastStop = "whole",
                Target = null,
            };
            _states[actor.UnitId] = state;
            return new Admission(false, "whole", null);
        }

        if (state.ActiveSince is int started
            && tick - started >= EpisodeTicks)
        {
            state = Stop(state, tick, "timeout");
        }
        else if (state.ActiveSince is not null && !otherwiseEligible)
        {
            state = Stop(state, tick, "unsafe");
        }

        if (!otherwiseEligible)
        {
            _states[actor.UnitId] = state;
            return new Admission(
                false, state.LastStop ?? "ineligible", null);
        }

        bool sameInjuryCooling = tick < state.CooldownUntil
            && state.BlockedInjuryRevision == state.InjuryRevision;
        if (sameInjuryCooling)
        {
            _states[actor.UnitId] = state;
            return new Admission(
                false,
                $"cooldown-{state.LastStop ?? "retry"}@{state.CooldownUntil}",
                null);
        }

        if (state.ActiveSince is null)
        {
            state = state with
            {
                ActiveSince = tick,
                LastStop = null,
                Target = candidateTarget,
            };
        }
        _states[actor.UnitId] = state;
        return new Admission(
            true, $"episode@{state.ActiveSince}", state.Target);
    }

    /// <summary>
    /// An actual higher-precedence safety displacement proves that the
    /// current beacon attempt and the body's threat picture disagree. End
    /// that attempt; do not let the next tick walk straight back into it.
    /// </summary>
    public void Interrupt(ActorIdentity actor, int tick, string reason)
    {
        if (!_states.TryGetValue(actor.UnitId, out State? state)
            || state.Actor != actor
            || state.ActiveSince is null)
        {
            return;
        }
        _states[actor.UnitId] = Stop(state, tick, reason);
    }

    internal bool IsActive(ActorIdentity actor) =>
        _states.TryGetValue(actor.UnitId, out State? state)
        && state.Actor == actor
        && state.ActiveSince is not null;

    public Position? Target(ActorIdentity actor) =>
        _states.TryGetValue(actor.UnitId, out State? state)
        && state.Actor == actor
        && state.ActiveSince is not null
            ? state.Target
            : null;

    internal int InjuryRevision(ActorIdentity actor) =>
        _states.TryGetValue(actor.UnitId, out State? state)
        && state.Actor == actor
            ? state.InjuryRevision
            : 0;

    private static State Stop(State state, int tick, string reason) =>
        state with
        {
            ActiveSince = null,
            CooldownUntil = tick + RetryCooldownTicks,
            BlockedInjuryRevision = state.InjuryRevision,
            LastStop = reason,
            Target = null,
        };

    public sealed record Admission(
        bool Admitted,
        string Reason,
        Position? Target);

    private sealed record State(
        ActorIdentity Actor,
        int LastHealth,
        int InjuryRevision,
        int? ActiveSince,
        int CooldownUntil,
        int BlockedInjuryRevision,
        string? LastStop,
        Position? Target)
    {
        internal static State For(ActorIdentity actor, int health) =>
            new(actor, health, 0, null, 0, -1, null, null);
    }
}

/// <summary>
/// Persistent right-of-way aging for high-level movement intents. A sheet
/// chooses the intent, destination and authored urgency. This governor
/// observes only whether the body makes strict map progress toward that stable
/// destination. A visible ally on the intended step is traffic denial;
/// repeated denial accrues debt, which eventually lets even routine traffic
/// pass a stream of fresh urgent traffic. It never invents participation,
/// destinations, or tactics.
/// </summary>
public sealed class TacticalTrafficGovernor
{
    /// <summary>
    /// Twelve denied ticks promote effective urgency by one tier. At the
    /// urgent ceiling the oldest denial wins, so a routine mover denied for 24
    /// ticks competes as urgent rather than remaining permanently subordinate.
    /// </summary>
    public const int DebtPerUrgencyTier = 12;

    /// <summary>Strict progress pays traffic debt down quickly without making
    /// one incidental step erase a long wait.</summary>
    public const int ProgressDebtPayment = 4;

    public const int MaximumDebt = 96;

    /// <summary>
    /// A mover that finally advances after allied denial keeps precedence
    /// among otherwise equivalent traffic for this many observations. This
    /// is deliberately short: it is enough to cross a body-width passage,
    /// while debt aging still hands the next opening to a longer-starved body.
    /// </summary>
    public const int PassageLeaseTicks = 4;

    /// <summary>
    /// A passage lease is progress-backed, not ownership of a tile. Two
    /// unsuccessful movement observations end it early so a stuck winner
    /// cannot become the new head of a queue.
    /// </summary>
    public const int PassageLeaseNoProgressLimit = 2;

    private readonly Dictionary<int, State> _states = [];

    public void Observe(int tick, IReadOnlyCollection<Sample> samples)
    {
        HashSet<int> live = samples.Select(sample => sample.Actor.UnitId)
            .ToHashSet();
        foreach (int stale in _states.Keys.Where(unit => !live.Contains(unit))
                     .ToArray())
        {
            _states.Remove(stale);
        }

        foreach (Sample sample in samples.OrderBy(value => value.Actor.UnitId))
        {
            State state = _states.GetValueOrDefault(sample.Actor.UnitId)
                ?? State.For(sample, tick);
            if (state.Actor != sample.Actor
                || !string.Equals(
                    state.IntentId, sample.IntentId, StringComparison.Ordinal)
                || state.Goal != sample.Goal
                || state.Urgency != sample.Urgency
                || state.ActivatedTick != sample.ActivatedTick)
            {
                state = State.For(sample, tick);
            }

            if (!sample.MayAge || sample.Distance is null)
            {
                _states[sample.Actor.UnitId] = State.For(sample, tick);
                continue;
            }

            bool progressed = sample.Distance < state.LastDistance;
            int noProgressMoves = progressed
                ? 0
                : state.NoProgressMoves + (sample.AttemptedMove ? 1 : 0);
            int debt = progressed
                ? Math.Max(0, state.AlliedBlockDebt - ProgressDebtPayment)
                : sample.BlockedByAlly
                    ? Math.Min(MaximumDebt, state.AlliedBlockDebt + 1)
                    : state.AlliedBlockDebt;
            int? blockedSince = debt == 0
                ? null
                : state.BlockedSince
                    ?? (sample.BlockedByAlly ? tick : null);

            int? passageLeaseUntil = state.PassageLeaseUntil;
            int passageLeaseNoProgress = state.PassageLeaseNoProgress;
            bool leaseActive = passageLeaseUntil is int leaseUntil
                && tick < leaseUntil;
            if (leaseActive)
            {
                passageLeaseNoProgress = progressed
                    ? 0
                    : passageLeaseNoProgress
                        + (sample.AttemptedMove ? 1 : 0);
                if (passageLeaseNoProgress >= PassageLeaseNoProgressLimit)
                {
                    passageLeaseUntil = null;
                    passageLeaseNoProgress = 0;
                    leaseActive = false;
                }
            }
            else
            {
                passageLeaseUntil = null;
                passageLeaseNoProgress = 0;
            }

            // Denial arms one future passage. Progress consumes that arm and
            // earns a bounded lease. A lease cannot renew itself: another
            // observed allied denial is required after it expires.
            bool passageLeaseArmed = state.PassageLeaseArmed;
            if (!leaseActive && sample.BlockedByAlly)
                passageLeaseArmed = true;
            if (!leaseActive && progressed && passageLeaseArmed)
            {
                passageLeaseUntil = tick + PassageLeaseTicks;
                passageLeaseNoProgress = 0;
                passageLeaseArmed = false;
            }

            _states[sample.Actor.UnitId] = new State(
                sample.Actor,
                sample.IntentId,
                sample.Goal,
                sample.Urgency,
                sample.ActivatedTick,
                sample.Distance.Value,
                noProgressMoves,
                debt,
                blockedSince,
                passageLeaseUntil,
                passageLeaseNoProgress,
                passageLeaseArmed,
                tick);
        }
    }

    /// <summary>
    /// Resolve the complete precedence list. Strength is authored tier plus
    /// allied-block debt; an earlier denial, then order activation, then unit
    /// id breaks exact ties. Every body receives a distinct rank so a contested
    /// tile always has one deterministic winner.
    /// </summary>
    public TacticalMovementPriority Priority(int unitId)
    {
        State state = _states[unitId];
        State[] ordered = _states.Values
            .OrderByDescending(EffectiveUrgency)
            // A full debt tier is starvation evidence and remains stronger
            // than a short passage lease. Within one tier, however, the body
            // that just broke through keeps the opening instead of being
            // immediately displaced by the next one-tick waiter.
            .ThenByDescending(DebtTier)
            .ThenByDescending(PassageLeaseActive)
            .ThenByDescending(value => value.AlliedBlockDebt)
            .ThenBy(value => value.BlockedSince ?? int.MaxValue)
            .ThenBy(value => value.ActivatedTick)
            .ThenBy(value => value.Actor.UnitId)
            .ToArray();
        int rank = Array.FindIndex(
            ordered, value => value.Actor.UnitId == unitId);
        return new TacticalMovementPriority(
            unitId,
            rank,
            state.Urgency,
            (TacticalMovementUrgency)EffectiveUrgency(state),
            state.AlliedBlockDebt,
            PassageLeaseRemaining(state),
            state.ActivatedTick,
            state.BlockedSince);
    }

    private static int EffectiveUrgency(State state) => Math.Min(
        (int)TacticalMovementUrgency.Urgent,
        (int)state.Urgency + state.AlliedBlockDebt / DebtPerUrgencyTier);

    private static int DebtTier(State state) =>
        state.AlliedBlockDebt / DebtPerUrgencyTier;

    private static bool PassageLeaseActive(State state) =>
        PassageLeaseRemaining(state) > 0;

    private static int PassageLeaseRemaining(State state) => Math.Max(
        0,
        (state.PassageLeaseUntil ?? state.ObservedTick) - state.ObservedTick);

    internal string TraceSummary
    {
        get
        {
            string[] active = _states.Values
                .Where(state => state.AlliedBlockDebt > 0
                    || PassageLeaseActive(state))
                .OrderByDescending(EffectiveUrgency)
                .ThenByDescending(DebtTier)
                .ThenByDescending(PassageLeaseActive)
                .ThenByDescending(state => state.AlliedBlockDebt)
                .ThenBy(state => state.BlockedSince)
                .ThenBy(state => state.Actor.UnitId)
                .Select(state => $"{state.Actor.UnitId}@{state.BlockedSince}"
                    + $"/{state.Urgency.ToString().ToLowerInvariant()}"
                    + $"/m{state.NoProgressMoves}/b{state.AlliedBlockDebt}"
                    + (PassageLeaseRemaining(state) > 0
                        ? $"/passage{PassageLeaseRemaining(state)}"
                        : ""))
                .ToArray();
            return active.Length == 0
                ? "traffic=clear"
                : "traffic=" + string.Join(",", active);
        }
    }

    public sealed record Sample(
        ActorIdentity Actor,
        string IntentId,
        Position Goal,
        int? Distance,
        bool AttemptedMove,
        bool BlockedByAlly,
        bool MayAge,
        TacticalMovementUrgency Urgency,
        int ActivatedTick);

    private sealed record State(
        ActorIdentity Actor,
        string IntentId,
        Position Goal,
        TacticalMovementUrgency Urgency,
        int ActivatedTick,
        int LastDistance,
        int NoProgressMoves,
        int AlliedBlockDebt,
        int? BlockedSince,
        int? PassageLeaseUntil,
        int PassageLeaseNoProgress,
        bool PassageLeaseArmed,
        int ObservedTick)
    {
        internal static State For(Sample sample, int tick) => new(
            sample.Actor,
            sample.IntentId,
            sample.Goal,
            sample.Urgency,
            sample.ActivatedTick,
            sample.Distance ?? int.MaxValue,
            0,
            0,
            null,
            null,
            0,
            false,
            tick);
    }
}

/// <summary>
/// A deterministic circuit breaker for repeated A-B-A-B motion. One reversal
/// can be a sensible response to a moving target; an unbounded sequence is two
/// executor authorities arguing. After the second consecutive reversal, the
/// just-vacated tile is unavailable to every movement authority for a short,
/// fixed window. A first safety or make-way displacement remains soft: it
/// lends its origin only against ordinary tactical snap-back. If that same
/// authority also reverses repeatedly, the hard circuit breaker applies.
/// </summary>
public sealed class TacticalOscillationGovernor
{
    public const int ReversalsBeforeYield = 2;
    public const int YieldTicks = 3;

    private readonly Dictionary<int, State> _states = [];
    private readonly List<string> _denials = [];

    public void Observe(int tick, IReadOnlyCollection<Sample> samples)
    {
        _denials.Clear();
        HashSet<int> live = samples.Select(sample => sample.Actor.UnitId)
            .ToHashSet();
        foreach (int stale in _states.Keys.Where(unit => !live.Contains(unit))
                     .ToArray())
        {
            _states.Remove(stale);
        }

        foreach (Sample sample in samples.OrderBy(value => value.Actor.UnitId))
        {
            State state = _states.GetValueOrDefault(sample.Actor.UnitId)
                ?? State.For(sample.Actor);
            if (state.Actor != sample.Actor)
                state = State.For(sample.Actor);

            if (sample.MovedLastTick
                && sample.PreviousPosition is Position from
                && from != sample.Position)
            {
                bool reversal = state.LastFrom == sample.Position
                    && state.LastTo == from;
                int reversals = reversal ? state.Reversals + 1 : 0;
                Position? hardBlockedReturn = state.HardBlockedReturn;
                int hardBlockedUntil = state.HardBlockedUntil;
                if (reversals >= ReversalsBeforeYield)
                {
                    hardBlockedReturn = from;
                    hardBlockedUntil = tick + YieldTicks;
                    reversals = 0;
                }
                state = state with
                {
                    LastFrom = from,
                    LastTo = sample.Position,
                    Reversals = reversals,
                    HardBlockedReturn = hardBlockedReturn,
                    HardBlockedUntil = hardBlockedUntil,
                };
            }
            else
            {
                state = state with { Reversals = 0 };
            }

            if (tick >= state.SoftBlockedUntil)
            {
                state = state with
                {
                    SoftBlockedReturn = null,
                    SoftBlockedUntil = 0,
                };
            }
            if (tick >= state.HardBlockedUntil)
            {
                state = state with
                {
                    HardBlockedReturn = null,
                    HardBlockedUntil = 0,
                };
            }
            _states[sample.Actor.UnitId] = state;
        }
    }

    public Admission Admit(
        ActorIdentity actor,
        int tick,
        Position destination,
        TacticalMovementAuthority authority)
    {
        if (!_states.TryGetValue(actor.UnitId, out State? state)
            || state.Actor != actor)
        {
            return new Admission(true, "clear");
        }

        string? reason = state.HardBlockedReturn == destination
            && tick < state.HardBlockedUntil
                ? $"oscillation-yield@{state.HardBlockedUntil}"
                    + $"-avoid-{destination.X}-{destination.Y}"
                : authority == TacticalMovementAuthority.Tactical
                    && state.SoftBlockedReturn == destination
                    && tick < state.SoftBlockedUntil
                        ? $"movement-lease@{state.SoftBlockedUntil}"
                            + $"-avoid-{destination.X}-{destination.Y}"
                        : null;
        if (reason is null)
            return new Admission(true, "clear");
        _denials.Add($"{actor.UnitId}:{reason}");
        return new Admission(false, reason);
    }

    /// <summary>
    /// A successful make-way step lends the vacated tile to its requester;
    /// a safety step lends it to the danger just escaped. Both use the same
    /// short window as the reversal breaker. Without this, the body's
    /// unchanged formation target can pull it straight back on the next tick,
    /// turning politeness or evasion into A-B-A-B churn.
    /// </summary>
    public void NoteCommitted(
        ActorIdentity actor,
        int tick,
        Position from,
        TacticalMovementAuthority authority)
    {
        if (authority is not (TacticalMovementAuthority.Clearance
                or TacticalMovementAuthority.Safety)
            || !_states.TryGetValue(actor.UnitId, out State? state)
            || state.Actor != actor)
        {
            return;
        }
        _states[actor.UnitId] = state with
        {
            SoftBlockedReturn = from,
            SoftBlockedUntil = Math.Max(
                state.SoftBlockedUntil, tick + YieldTicks),
        };
    }

    public string TraceSummary
    {
        get
        {
            string[] active = _states.Values
                .Where(state => state.SoftBlockedReturn is not null
                    || state.HardBlockedReturn is not null)
                .OrderBy(state => state.Actor.UnitId)
                .Select(state => $"{state.Actor.UnitId}:"
                    + (state.SoftBlockedReturn is Position soft
                        ? $"s{soft.X}-{soft.Y}@{state.SoftBlockedUntil}"
                        : "s-")
                    + "/"
                    + (state.HardBlockedReturn is Position hard
                        ? $"h{hard.X}-{hard.Y}@{state.HardBlockedUntil}"
                        : "h-"))
                .ToArray();
            string holds = active.Length == 0
                ? "motion=clear"
                : "motion=" + string.Join(",", active);
            return _denials.Count == 0
                ? holds
                : holds + "/denied=" + string.Join(",", _denials);
        }
    }

    public sealed record Sample(
        ActorIdentity Actor,
        Position Position,
        Position? PreviousPosition,
        bool MovedLastTick);

    public sealed record Admission(bool Admitted, string Reason);

    private sealed record State(
        ActorIdentity Actor,
        Position? LastFrom,
        Position? LastTo,
        int Reversals,
        Position? SoftBlockedReturn,
        int SoftBlockedUntil,
        Position? HardBlockedReturn,
        int HardBlockedUntil)
    {
        internal static State For(ActorIdentity actor) =>
            new(actor, null, null, 0, null, 0, null, 0);
    }
}
