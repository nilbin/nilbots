using BotArena.Sdk;

internal enum TacticalTaskPhase
{
    Dormant,
    Active,
    Reintegrating,
}

internal sealed record TacticalTaskCandidate(
    int UnitId,
    ActorIdentity ActorId,
    string RoleId,
    string GroupId,
    string LocalState,
    string ClassId,
    bool CarriesCore,
    Position Position);

internal sealed record TacticalTaskDirective(
    string TaskId,
    TacticalTaskPhase Phase,
    string OrderId);

internal sealed record TacticalTaskAssignment(
    string AssignmentId,
    string OrderId,
    int UnitId,
    ActorIdentity ActorId);

internal sealed record TacticalTaskStateView(
    string TaskId,
    TacticalTaskPhase Phase,
    int PhaseStartedTick,
    string LastReason,
    IReadOnlyList<TacticalTaskAssignment> Assignments);

internal sealed record TacticalTaskTrace(
    int Tick,
    string TaskId,
    TacticalTaskPhase From,
    TacticalTaskPhase To,
    string Reason,
    int[] UnitIds);

/// <summary>
/// Pure deterministic scheduler for playbook-owned concurrent tasks. It owns
/// participant leases and lifecycle only; the playbook mind supplies causal
/// facts, distances, and the orders that execute each directive.
/// </summary>
internal sealed class TacticalTaskMachine
{
    private readonly TacticalPlaybookPackage.TacticalTask[] _plans;
    private readonly IReadOnlyDictionary<string, TacticalPlaybookPackage.Order>
        _orders;
    private readonly Dictionary<string, Runtime> _states;
    private readonly Dictionary<int, TacticalTaskDirective> _directives = [];
    private readonly List<TacticalTaskTrace> _transitions = [];

    internal TacticalTaskMachine(TacticalPlaybookPackage.Playbook playbook)
    {
        _plans = playbook.Coordination.Tasks
            .OrderBy(value => value.Priority)
            .ThenBy(value => value.TaskId, StringComparer.Ordinal)
            .ToArray();
        _orders = playbook.Orders.ToDictionary(
            value => value.OrderId,
            StringComparer.Ordinal);
        _states = _plans.ToDictionary(
            value => value.TaskId,
            value => new Runtime(value),
            StringComparer.Ordinal);
    }

    internal IReadOnlyList<TacticalTaskTrace> Transitions => _transitions;

    internal IReadOnlySet<int> LeasedUnitIds => _states.Values
        .Where(value => value.Phase != TacticalTaskPhase.Dormant)
        .SelectMany(value => value.Assignments)
        .Select(value => value.UnitId)
        .ToHashSet();

    internal TacticalTaskDirective? DirectiveFor(int unitId) =>
        _directives.GetValueOrDefault(unitId);

    internal TacticalTaskStateView State(string taskId) => View(_states[taskId]);

    internal string TraceSummary => _plans.Length == 0
        ? "tasks=none"
        : "tasks=" + string.Join(";", _plans.Select(plan =>
        {
            Runtime state = _states[plan.TaskId];
            string claims = state.Assignments.Count == 0
                ? "-"
                : string.Join(",", state.Assignments
                    .OrderBy(value => value.UnitId)
                    .Select(value => $"{value.UnitId}:{value.AssignmentId}"));
            return $"{plan.TaskId}="
                + state.Phase.ToString().ToLowerInvariant()
                + $"[{state.LastReason}|armed="
                + $"{(state.CompletionArmed ? 1 : 0)}|c={claims}]";
        }));

    internal void Update(
        int tick,
        string phaseId,
        IReadOnlyList<TacticalTaskCandidate> candidates,
        Func<TacticalPlaybookPackage.ConditionGroup, bool> evaluate,
        Func<TacticalPlaybookPackage.TaskAssignment,
            TacticalTaskCandidate, int> distance)
    {
        _directives.Clear();
        Dictionary<int, TacticalTaskCandidate> live = candidates.ToDictionary(
            value => value.UnitId);

        var owners = new Dictionary<int, Runtime>();
        foreach (Runtime state in _states.Values
                     .Where(value => value.Phase != TacticalTaskPhase.Dormant)
                     .OrderBy(value => value.Plan.Priority)
                     .ThenBy(value => value.Plan.TaskId, StringComparer.Ordinal))
        {
            foreach (TacticalTaskAssignment assignment in state.Assignments)
            {
                if (SameLife(live, assignment))
                    owners.TryAdd(assignment.UnitId, state);
            }
        }

        foreach (TacticalPlaybookPackage.TacticalTask plan in _plans)
        {
            Runtime state = _states[plan.TaskId];
            state.LastReason = "steady";
            switch (state.Phase)
            {
                case TacticalTaskPhase.Active:
                    AdvanceActive(
                        tick, phaseId, state, live, owners, evaluate, distance);
                    break;
                case TacticalTaskPhase.Reintegrating:
                    AdvanceReintegration(tick, state, live, evaluate);
                    break;
                case TacticalTaskPhase.Dormant:
                    AdvanceDormant(
                        tick, phaseId, state, live, owners, evaluate, distance);
                    break;
            }
            RebuildOwners(owners, live);
        }

        foreach (TacticalPlaybookPackage.TacticalTask plan in _plans)
        {
            Runtime state = _states[plan.TaskId];
            if (state.Phase == TacticalTaskPhase.Active)
            {
                foreach (TacticalTaskAssignment assignment in state.Assignments)
                {
                    if (SameLife(live, assignment))
                    {
                        _directives[assignment.UnitId] = new(
                            plan.TaskId,
                            state.Phase,
                            assignment.OrderId);
                    }
                }
            }
            else if (state.Phase == TacticalTaskPhase.Reintegrating
                && string.Equals(
                    plan.Reintegration.Mode,
                    "release-orders",
                    StringComparison.Ordinal))
            {
                foreach (TacticalTaskAssignment assignment in state.Assignments)
                {
                    if (!live.TryGetValue(
                            assignment.UnitId,
                            out TacticalTaskCandidate? actor)
                        || actor.ActorId != assignment.ActorId)
                    {
                        continue;
                    }
                    string orderId = plan.Reintegration.OrderIds
                        .Select(value => _orders[value])
                        .Single(value => string.Equals(
                                value.GroupId,
                                actor.GroupId,
                                StringComparison.Ordinal)
                            && string.Equals(
                                value.LocalState,
                                actor.LocalState,
                                StringComparison.Ordinal))
                        .OrderId;
                    _directives[assignment.UnitId] = new(
                        plan.TaskId,
                        state.Phase,
                        orderId);
                }
            }
        }
    }

    private void AdvanceDormant(
        int tick,
        string phaseId,
        Runtime state,
        IReadOnlyDictionary<int, TacticalTaskCandidate> live,
        Dictionary<int, Runtime> owners,
        Func<TacticalPlaybookPackage.ConditionGroup, bool> evaluate,
        Func<TacticalPlaybookPackage.TaskAssignment,
            TacticalTaskCandidate, int> distance)
    {
        bool eligible = state.Plan.EligiblePhases.Contains(
            phaseId,
            StringComparer.Ordinal);
        bool triggered = eligible && MatchesAny(state.Plan.When, evaluate);
        if (!triggered)
        {
            state.TriggerStreak = 0;
            state.EdgeReady = true;
            state.LastReason = eligible
                ? "trigger-false-rearmed"
                : "phase-ineligible";
            return;
        }

        state.TriggerStreak++;
        if (state.TriggerStreak < state.Plan.TriggerStableTicks)
        {
            state.LastReason = $"trigger-stable-{state.TriggerStreak}"
                + $"-of-{state.Plan.TriggerStableTicks}";
            return;
        }
        if (tick < state.CooldownUntil)
        {
            state.LastReason = $"cooldown-until-{state.CooldownUntil}";
            return;
        }
        if (string.Equals(
                state.Plan.Activation,
                "rising-edge",
                StringComparison.Ordinal)
            && !state.EdgeReady)
        {
            state.LastReason = "edge-not-rearmed";
            return;
        }

        if (!TrySelect(
                state,
                live,
                owners,
                distance,
                allowPreemption: true,
                out List<TacticalTaskAssignment> selected,
                out HashSet<Runtime> preempted,
                out string selectionFailure))
        {
            state.LastReason = selectionFailure;
            return;
        }
        foreach (Runtime victim in preempted
                     .OrderBy(value => value.Plan.Priority)
                     .ThenBy(value => value.Plan.TaskId, StringComparer.Ordinal))
        {
            Preempt(tick, victim);
        }
        state.Assignments = selected;
        state.EdgeReady = false;
        state.TriggerStreak = 0;
        state.CompletionArmed = state.Plan.CompletionArmWhen is not
                { Length: > 0 }
            && state.Plan.CompletionArmMode is null;
        Transition(tick, state, TacticalTaskPhase.Active, "triggered");
    }

    private void AdvanceActive(
        int tick,
        string phaseId,
        Runtime state,
        IReadOnlyDictionary<int, TacticalTaskCandidate> live,
        Dictionary<int, Runtime> owners,
        Func<TacticalPlaybookPackage.ConditionGroup, bool> evaluate,
        Func<TacticalPlaybookPackage.TaskAssignment,
            TacticalTaskCandidate, int> distance)
    {
        int elapsed = tick - state.PhaseStartedTick;
        bool assignedParticipantCarriesCore = state.Assignments.Any(value =>
            SameLife(live, value) && live[value.UnitId].CarriesCore);
        bool alternateParticipantCarriesCore = live.Values.Any(value =>
            value.CarriesCore
            && state.Assignments.All(assignment =>
                assignment.ActorId != value.ActorId));
        TacticalTaskCandidate? firstFriendlyCarrier = live.Values
            .Where(value => value.CarriesCore)
            .OrderBy(value => value.ActorId.TeamId)
            .ThenBy(value => value.ActorId.UnitId)
            .ThenBy(value => value.ActorId.LifeId)
            .FirstOrDefault();
        bool retargetedCarrier = false;
        if (!state.CompletionArmed
            && string.Equals(
                state.Plan.CancellationMode,
                "alternate-carrier",
                StringComparison.Ordinal)
            && alternateParticipantCarriesCore)
        {
            BeginReintegration(tick, state, "alternate-carrier");
            return;
        }
        if (!state.CompletionArmed
            && string.Equals(
                state.Plan.CompletionArmMode,
                "assigned-carrier",
                StringComparison.Ordinal)
            && assignedParticipantCarriesCore)
        {
            state.CompletionArmed = true;
            state.ArmedCarrier = state.Assignments
                .Where(value => SameLife(live, value)
                    && live[value.UnitId].CarriesCore)
                .OrderBy(value => value.ActorId.TeamId)
                .ThenBy(value => value.ActorId.UnitId)
                .ThenBy(value => value.ActorId.LifeId)
                .Select(value => (ActorIdentity?)value.ActorId)
                .FirstOrDefault();
        }
        if (!state.CompletionArmed
            && string.Equals(
                state.Plan.CompletionArmMode,
                "any-friendly-carrier",
                StringComparison.Ordinal)
            && firstFriendlyCarrier is not null)
        {
            state.CompletionArmed = true;
            state.ArmedCarrier = firstFriendlyCarrier.ActorId;
        }
        if (!state.CompletionArmed
            && state.Plan.CompletionArmWhen is { Length: > 0 } armWhen
            && MatchesAny(armWhen, evaluate))
        {
            state.CompletionArmed = true;
        }
        bool armedCarrierStillCarries = state.ArmedCarrier is { } armedCarrier
            && live.Values.Any(value => value.ActorId == armedCarrier
                && value.CarriesCore);
        if (state.CompletionArmed
            && string.Equals(
                state.Plan.CompletionReleaseMode,
                "armed-carrier-retarget-or-loss",
                StringComparison.Ordinal)
            && !armedCarrierStillCarries
            && firstFriendlyCarrier is not null)
        {
            state.ArmedCarrier = firstFriendlyCarrier.ActorId;
            retargetedCarrier = true;
            armedCarrierStillCarries = true;
        }
        if (state.CompletionArmed
            && state.Plan.CompletionReleaseMode is
                "assigned-carrier-loss" or "armed-carrier-loss"
                    or "armed-carrier-retarget-or-loss"
            && !armedCarrierStillCarries)
        {
            BeginReintegration(
                tick,
                state,
                state.Plan.CompletionReleaseMode
                    == "assigned-carrier-loss"
                        ? "assigned-carrier-released"
                        : "armed-carrier-released");
            return;
        }
        if (state.CompletionArmed
            && elapsed >= state.Plan.MinimumTicks
            && MatchesAny(state.Plan.CompleteWhen, evaluate))
        {
            BeginReintegration(tick, state, "completed");
            return;
        }
        if (MatchesAny(state.Plan.FailWhen, evaluate))
        {
            BeginReintegration(tick, state, "failed-condition");
            return;
        }
        if (!state.Plan.EligiblePhases.Contains(
                phaseId,
                StringComparer.Ordinal))
        {
            BeginReintegration(tick, state, "phase-ineligible");
            return;
        }
        if (elapsed >= state.Plan.TimeoutTicks)
        {
            BeginReintegration(tick, state, "timeout");
            return;
        }

        // Selection predicates choose a life at activation; they do not keep
        // re-electing it every tick. A leased courier may pick up its Core and
        // the primary group's local state may reflow underneath the task. Only
        // loss of the exact life invokes the authored participant-loss policy.
        bool lostParticipant = state.Assignments.Any(value =>
            !SameLife(live, value));
        state.Assignments.RemoveAll(value => !SameLife(live, value));
        if (lostParticipant && string.Equals(
                state.Plan.ParticipantLoss,
                "abort",
                StringComparison.Ordinal))
        {
            BeginReintegration(tick, state, "participant-lost");
            return;
        }

        if (!RetainPrimaryForce(state, live, owners, distance))
        {
            BeginReintegration(tick, state, "primary-force-reserve");
            return;
        }

        if (string.Equals(
                state.Plan.ParticipantLoss,
                "replace",
                StringComparison.Ordinal))
        {
            if (!Refresh(state, live, owners, distance))
            {
                BeginReintegration(tick, state, "replacement-unavailable");
                return;
            }
        }
        else if (!MinimumsHold(state))
        {
            BeginReintegration(tick, state, "participant-minimum");
            return;
        }
        state.LastReason = retargetedCarrier
            ? "armed-carrier-retargeted"
            : lostParticipant
                ? "continued-after-loss"
                : "active";
    }

    private void AdvanceReintegration(
        int tick,
        Runtime state,
        IReadOnlyDictionary<int, TacticalTaskCandidate> live,
        Func<TacticalPlaybookPackage.ConditionGroup, bool> evaluate)
    {
        state.Assignments.RemoveAll(value => !SameLife(live, value));
        bool complete = state.Assignments.Count == 0
            || MatchesAny(state.Plan.Reintegration.CompleteWhen, evaluate);
        bool timeout = tick - state.PhaseStartedTick
            >= state.Plan.Reintegration.TimeoutTicks;
        if (!complete && !timeout)
        {
            state.LastReason = "reintegrating";
            return;
        }
        FinishRelease(
            tick,
            state,
            complete ? "reintegration-complete" : "reintegration-timeout");
    }

    private bool Refresh(
        Runtime state,
        IReadOnlyDictionary<int, TacticalTaskCandidate> live,
        IReadOnlyDictionary<int, Runtime> owners,
        Func<TacticalPlaybookPackage.TaskAssignment,
            TacticalTaskCandidate, int> distance)
    {
        var selected = state.Assignments.ToList();
        var claimed = selected.Select(value => value.UnitId).ToHashSet();
        int otherLeases = owners.Count(value =>
            !ReferenceEquals(value.Value, state));
        int capacity = Math.Min(
            Math.Max(0, EffectiveMaximum(state.Plan) - selected.Count),
            Math.Max(
                0,
                live.Count
                    - otherLeases
                    - selected.Count
                    - state.Plan.MinimumPrimaryBodies));
        foreach (TacticalPlaybookPackage.TaskAssignment assignment
                 in state.Plan.Assignments)
        {
            int current = selected.Count(value => string.Equals(
                value.AssignmentId,
                assignment.AssignmentId,
                StringComparison.Ordinal));
            if (current >= assignment.Preferred)
                continue;
            TacticalPlaybookPackage.Order order = _orders[assignment.OrderId];
            TacticalTaskCandidate[] candidates = Candidates(
                    assignment, order, live.Values)
                .Where(value => !claimed.Contains(value.UnitId))
                .Where(value => !owners.TryGetValue(
                        value.UnitId,
                        out Runtime? owner)
                    || ReferenceEquals(owner, state))
                .Where(value => assignment.Distance.Maximum <= 0
                    || distance(assignment, value)
                        <= assignment.Distance.Maximum)
                .OrderBy(value => ClassRank(assignment, value))
                .ThenBy(value => distance(assignment, value))
                .ThenBy(value => value.UnitId)
                .Take(Math.Min(assignment.Preferred - current, capacity))
                .ToArray();
            foreach (TacticalTaskCandidate actor in candidates)
            {
                claimed.Add(actor.UnitId);
                selected.Add(new(
                    assignment.AssignmentId,
                    assignment.OrderId,
                    actor.UnitId,
                    actor.ActorId));
                capacity--;
            }
        }
        state.Assignments = selected;
        return MinimumsHold(state);
    }

    private bool TrySelect(
        Runtime claimant,
        IReadOnlyDictionary<int, TacticalTaskCandidate> live,
        IReadOnlyDictionary<int, Runtime> owners,
        Func<TacticalPlaybookPackage.TaskAssignment,
            TacticalTaskCandidate, int> distance,
        bool allowPreemption,
        out List<TacticalTaskAssignment> selected,
        out HashSet<Runtime> preempted,
        out string failureReason)
    {
        selected = [];
        preempted = [];
        failureReason = "participants-unavailable";
        var claimed = new HashSet<int>();
        int capacity = Math.Min(
            EffectiveMaximum(claimant.Plan),
            Math.Max(
                0,
                live.Count - owners.Count
                    - claimant.Plan.MinimumPrimaryBodies));
        bool reserveBlocked = false;

        // Minimums are allocated for every assignment before any assignment
        // receives its preferred extras. This prevents an early editor row
        // from consuming the primary-force reserve needed by a later row.
        foreach (bool fillPreferred in new[] { false, true })
        foreach (TacticalPlaybookPackage.TaskAssignment assignment
                 in claimant.Plan.Assignments)
        {
            int target = fillPreferred
                ? assignment.Preferred
                : assignment.Minimum;
            int current = selected.Count(value => string.Equals(
                value.AssignmentId,
                assignment.AssignmentId,
                StringComparison.Ordinal));
            if (current >= target)
                continue;
            TacticalPlaybookPackage.Order order = _orders[assignment.OrderId];
            TacticalTaskCandidate[] candidates = Candidates(
                    assignment, order, live.Values)
                .Where(value => !claimed.Contains(value.UnitId))
                .Where(value => Claimable(
                    claimant, value.UnitId, owners, allowPreemption))
                .Where(value => assignment.Distance.Maximum <= 0
                    || distance(assignment, value)
                        <= assignment.Distance.Maximum)
                .OrderBy(value => ClassRank(assignment, value))
                .ThenBy(value => distance(assignment, value))
                .ThenBy(value => value.UnitId)
                .ToArray();
            foreach (TacticalTaskCandidate actor in candidates)
            {
                if (current >= target)
                    break;
                bool alreadyLeased = owners.ContainsKey(actor.UnitId);
                if (!alreadyLeased && capacity == 0)
                {
                    reserveBlocked = true;
                    continue;
                }
                claimed.Add(actor.UnitId);
                selected.Add(new(
                    assignment.AssignmentId,
                    assignment.OrderId,
                    actor.UnitId,
                    actor.ActorId));
                current++;
                if (!alreadyLeased)
                    capacity--;
                if (owners.TryGetValue(actor.UnitId, out Runtime? owner)
                    && !ReferenceEquals(owner, claimant))
                {
                    preempted.Add(owner);
                }
            }
            if (!fillPreferred && current < assignment.Minimum)
            {
                selected = [];
                preempted = [];
                failureReason = reserveBlocked
                    ? "primary-force-reserve"
                    : "participants-unavailable";
                return false;
            }
        }
        if (selected.Count < EffectiveMinimum(claimant.Plan))
        {
            selected = [];
            preempted = [];
            failureReason = reserveBlocked
                ? "primary-force-reserve"
                : "participants-unavailable";
            return false;
        }
        return true;
    }

    private bool RetainPrimaryForce(
        Runtime state,
        IReadOnlyDictionary<int, TacticalTaskCandidate> live,
        IReadOnlyDictionary<int, Runtime> owners,
        Func<TacticalPlaybookPackage.TaskAssignment,
            TacticalTaskCandidate, int> distance)
    {
        int otherLeases = owners.Count(value =>
            !ReferenceEquals(value.Value, state));
        int allowed = Math.Max(
            0,
            live.Count - otherLeases - state.Plan.MinimumPrimaryBodies);
        allowed = Math.Min(allowed, EffectiveMaximum(state.Plan));
        while (state.Assignments.Count > allowed)
        {
            TacticalTaskAssignment? released = null;
            foreach (TacticalPlaybookPackage.TaskAssignment plan
                     in state.Plan.Assignments.Reverse())
            {
                TacticalTaskAssignment[] held = state.Assignments
                    .Where(value => string.Equals(
                        value.AssignmentId,
                        plan.AssignmentId,
                        StringComparison.Ordinal))
                    .ToArray();
                if (held.Length <= plan.Minimum)
                    continue;
                released = held
                    .Select(value => (Lease: value, Actor: live[value.UnitId]))
                    .OrderByDescending(value => ClassRank(plan, value.Actor))
                    .ThenByDescending(value => distance(plan, value.Actor))
                    .ThenByDescending(value => value.Lease.UnitId)
                    .Select(value => value.Lease)
                    .First();
                break;
            }
            if (released is null)
                return false;
            state.Assignments.Remove(released);
        }
        return true;
    }

    private static bool Claimable(
        Runtime claimant,
        int unitId,
        IReadOnlyDictionary<int, Runtime> owners,
        bool allowPreemption)
    {
        if (!owners.TryGetValue(unitId, out Runtime? owner)
            || ReferenceEquals(owner, claimant))
        {
            return true;
        }
        return allowPreemption
            && claimant.Plan.Priority < owner.Plan.Priority
            && string.Equals(
                owner.Plan.Preemption,
                "higher-priority",
                StringComparison.Ordinal);
    }

    private static IEnumerable<TacticalTaskCandidate> Candidates(
        TacticalPlaybookPackage.TaskAssignment assignment,
        TacticalPlaybookPackage.Order order,
        IEnumerable<TacticalTaskCandidate> candidates) => candidates
        .Where(value => string.Equals(
            value.GroupId, order.GroupId, StringComparison.Ordinal))
        .Where(value => string.Equals(
            value.LocalState, order.LocalState, StringComparison.Ordinal))
        .Where(value => assignment.Roles.Length == 0
            || assignment.Roles.Contains(value.RoleId, StringComparer.Ordinal))
        .Where(value => assignment.Classes.Length == 0
            || assignment.Classes.Contains(
                value.ClassId,
                StringComparer.Ordinal))
        .Where(value => assignment.Carrier switch
        {
            "allow" => true,
            "forbid" => !value.CarriesCore,
            "require" => value.CarriesCore,
            _ => throw new InvalidDataException(
                $"Unknown task carrier selector '{assignment.Carrier}'."),
        });

    private static int ClassRank(
        TacticalPlaybookPackage.TaskAssignment assignment,
        TacticalTaskCandidate candidate) => assignment.Classes.Length == 0
        ? 0
        : Array.IndexOf(assignment.Classes, candidate.ClassId);

    private bool MinimumsHold(Runtime state) =>
        state.Assignments.Count >= EffectiveMinimum(state.Plan)
        && state.Assignments.Count <= EffectiveMaximum(state.Plan)
        && state.Plan.Assignments.All(
            assignment => state.Assignments.Count(value => string.Equals(
                value.AssignmentId,
                assignment.AssignmentId,
                StringComparison.Ordinal)) >= assignment.Minimum);

    private static int EffectiveMinimum(
        TacticalPlaybookPackage.TacticalTask plan) => Math.Max(
        plan.MinimumParticipants,
        plan.Assignments.Sum(value => value.Minimum));

    private static int EffectiveMaximum(
        TacticalPlaybookPackage.TacticalTask plan) => plan.MaximumParticipants > 0
        ? plan.MaximumParticipants
        : plan.Assignments.Sum(value => value.Maximum);

    private void BeginReintegration(
        int tick,
        Runtime state,
        string reason)
    {
        if (string.Equals(
                state.Plan.Reintegration.Mode,
                "primary-order",
                StringComparison.Ordinal))
        {
            FinishRelease(tick, state, reason + "-primary-order");
            return;
        }
        Transition(tick, state, TacticalTaskPhase.Reintegrating, reason);
    }

    private void FinishRelease(int tick, Runtime state, string reason)
    {
        TacticalTaskPhase from = state.Phase;
        int[] unitIds = state.Assignments
            .Select(value => value.UnitId)
            .Order()
            .ToArray();
        state.Phase = TacticalTaskPhase.Dormant;
        state.PhaseStartedTick = tick;
        state.CooldownUntil = tick + state.Plan.CooldownTicks;
        state.Assignments.Clear();
        state.CompletionArmed = false;
        state.ArmedCarrier = null;
        state.LastReason = reason;
        _transitions.Add(new(
            tick, state.Plan.TaskId, from, state.Phase, reason, unitIds));
    }

    private void Preempt(int tick, Runtime state)
    {
        FinishRelease(tick, state, "preempted-by-higher-priority");
    }

    private void Transition(
        int tick,
        Runtime state,
        TacticalTaskPhase to,
        string reason)
    {
        TacticalTaskPhase from = state.Phase;
        state.Phase = to;
        state.PhaseStartedTick = tick;
        state.LastReason = reason;
        _transitions.Add(new(
            tick,
            state.Plan.TaskId,
            from,
            to,
            reason,
            state.Assignments.Select(value => value.UnitId).Order().ToArray()));
    }

    private void RebuildOwners(
        Dictionary<int, Runtime> owners,
        IReadOnlyDictionary<int, TacticalTaskCandidate> live)
    {
        owners.Clear();
        foreach (Runtime state in _states.Values
                     .Where(value => value.Phase != TacticalTaskPhase.Dormant)
                     .OrderBy(value => value.Plan.Priority)
                     .ThenBy(value => value.Plan.TaskId, StringComparer.Ordinal))
        {
            foreach (TacticalTaskAssignment assignment in state.Assignments)
            {
                if (SameLife(live, assignment))
                    owners.TryAdd(assignment.UnitId, state);
            }
        }
    }

    private static bool MatchesAny(
        IReadOnlyCollection<TacticalPlaybookPackage.ConditionGroup> groups,
        Func<TacticalPlaybookPackage.ConditionGroup, bool> evaluate) =>
        groups.Count > 0 && groups.Any(evaluate);

    private static bool SameLife(
        IReadOnlyDictionary<int, TacticalTaskCandidate> live,
        TacticalTaskAssignment assignment) => live.TryGetValue(
            assignment.UnitId,
            out TacticalTaskCandidate? actor)
        && actor.ActorId == assignment.ActorId;

    private static TacticalTaskStateView View(Runtime state) => new(
        state.Plan.TaskId,
        state.Phase,
        state.PhaseStartedTick,
        state.LastReason,
        state.Assignments.ToArray());

    private sealed class Runtime
    {
        internal Runtime(TacticalPlaybookPackage.TacticalTask plan) =>
            Plan = plan;

        internal TacticalPlaybookPackage.TacticalTask Plan { get; }
        internal TacticalTaskPhase Phase { get; set; }
        internal int PhaseStartedTick { get; set; }
        internal int CooldownUntil { get; set; }
        internal int TriggerStreak { get; set; }
        internal bool CompletionArmed { get; set; }
        internal ActorIdentity? ArmedCarrier { get; set; }
        internal bool EdgeReady { get; set; } = true;
        internal string LastReason { get; set; } = "initial";
        internal List<TacticalTaskAssignment> Assignments { get; set; } = [];
    }
}
