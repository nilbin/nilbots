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
    Position Position,
    int Health = 1,
    int Level = 1,
    int StuckTicks = 0);

internal sealed record TacticalTaskDirective(
    string TaskId,
    TacticalTaskPhase Phase,
    string OrderId);

internal sealed record TacticalTaskAssignment(
    string AssignmentId,
    string OrderId,
    int UnitId,
    ActorIdentity ActorId,
    int StartedTick);

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

internal readonly record struct TacticalCandidateKey(
    string AssignmentId,
    ActorIdentity ActorId);

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
    private readonly HashSet<TacticalCandidateKey> _cohortAdmissions = [];

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

    /// <summary>Whether a task exists and is running right now. Safe on an
    /// unknown id: a ladder rung naming an order this sheet never generated
    /// is simply not a rung you can stand on.</summary>
    internal bool IsActive(string taskId) =>
        _states.TryGetValue(taskId, out Runtime? state)
        && state.Phase == TacticalTaskPhase.Active;

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
        Func<TacticalPlaybookPackage.ConditionGroup,
            TacticalTaskCandidate, int, bool> evaluateCandidate,
        Func<TacticalPlaybookPackage.TaskAssignment,
            TacticalTaskCandidate, int> distance)
    {
        _directives.Clear();
        Dictionary<int, TacticalTaskCandidate> live = candidates.ToDictionary(
            value => value.UnitId);
        ResolveCohortAdmissions(
            tick, phaseId, candidates, evaluate, evaluateCandidate, distance);
        ReleaseDepartedCohortMembers(tick, live);

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
                        tick, phaseId, state, live, owners, evaluate,
                        evaluateCandidate, distance);
                    break;
                case TacticalTaskPhase.Reintegrating:
                    AdvanceReintegration(tick, state, live, evaluate);
                    break;
                case TacticalTaskPhase.Dormant:
                    AdvanceDormant(
                        tick, phaseId, state, live, owners, evaluate,
                        evaluateCandidate, distance);
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
        Func<TacticalPlaybookPackage.ConditionGroup,
            TacticalTaskCandidate, int, bool> evaluateCandidate,
        Func<TacticalPlaybookPackage.TaskAssignment,
            TacticalTaskCandidate, int> distance)
    {
        RefreshCandidateWitnesses(
            tick, state, live, evaluateCandidate, reset: true);
        bool eligible = state.Plan.EligiblePhases.Contains(
            phaseId,
            StringComparer.Ordinal);
        bool candidateReady = !HasCandidateScope(state.Plan)
            || state.CandidateWitnesses.Count > 0;
        bool triggered = eligible
            && candidateReady
            && MatchesAny(state.Plan.When, evaluate);
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
                evaluateCandidate,
                tick,
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
        Func<TacticalPlaybookPackage.ConditionGroup,
            TacticalTaskCandidate, int, bool> evaluateCandidate,
        Func<TacticalPlaybookPackage.TaskAssignment,
            TacticalTaskCandidate, int> distance)
    {
        int elapsed = tick - state.PhaseStartedTick;
        HashSet<int> conditionReleased = RefreshCandidateWitnesses(
            tick, state, live, evaluateCandidate, reset: false);
        if (HasCandidateScope(state.Plan)
            && state.CandidateWitnesses.Count == 0)
        {
            BeginReintegration(tick, state, "candidate-condition");
            return;
        }
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
        if (conditionReleased.Count > 0)
        {
            state.Assignments.RemoveAll(
                assignment => conditionReleased.Contains(assignment.UnitId));
            if (!MinimumsHold(state))
            {
                BeginReintegration(tick, state, "candidate-condition");
                return;
            }
        }
        if (!state.Plan.EligiblePhases.Contains(
                phaseId,
                StringComparer.Ordinal))
        {
            BeginReintegration(tick, state, "phase-ineligible");
            return;
        }
        // An ARMED task with a release mode is progressing by definition -
        // its carrier is hauling and its exit is the release, so a timeout
        // firing mid-haul would strand the carrier between custody and a
        // reverted order (measured as the ab34 stuck-carrier surge,
        // DECISIONS #212). Timeouts exist to end the UNARMED idle case.
        if (elapsed >= state.Plan.TimeoutTicks
            && !(state.CompletionArmed
                && state.Plan.CompletionReleaseMode is not null))
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
            if (!Refresh(
                    state, live, owners, distance, evaluateCandidate, tick,
                    conditionReleased))
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
            TacticalTaskCandidate, int> distance,
        Func<TacticalPlaybookPackage.ConditionGroup,
            TacticalTaskCandidate, int, bool> evaluateCandidate,
        int tick,
        IReadOnlySet<int> conditionReleased)
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
            TacticalTaskCandidate[] candidates = Candidates(
                    assignment, live.Values)
                .Where(value => !claimed.Contains(value.UnitId))
                .Where(value => !conditionReleased.Contains(value.UnitId))
                .Where(value => !owners.TryGetValue(
                        value.UnitId,
                        out Runtime? owner)
                    || ReferenceEquals(owner, state))
                .Where(value => IsCandidateWitness(
                    state, assignment, value))
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
                    actor.ActorId,
                    tick));
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
        Func<TacticalPlaybookPackage.ConditionGroup,
            TacticalTaskCandidate, int, bool> evaluateCandidate,
        int tick,
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
            TacticalTaskCandidate[] candidates = Candidates(
                    assignment, live.Values)
                .Where(value => !claimed.Contains(value.UnitId))
                .Where(value => IsCandidateWitness(
                    claimant, assignment, value))
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
                    actor.ActorId,
                    tick));
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

    /// <summary>
    /// Who a task row may claim. Cross-group claims are the point (owner
    /// doctrine 2026-08-07, already recorded in the compiler's task
    /// validation): a task IS an explicit detachment, so a row naming a role
    /// takes that role wherever it currently stands. Filtering candidates by
    /// the ORDER's group and local state — which the runtime used to do —
    /// silently emptied every cross-group row: the ghost's `escort: medic`
    /// never once claimed a medic (its order lives in shadow-group, every
    /// medic in well-group), and assassin-promotion, whose min-1 row names
    /// the towline roles, never activated in any recorded match. Role,
    /// class, carrier and the distance selector are the eligibility gate;
    /// priority and <see cref="Claimable"/> decide who wins a contested body.
    /// </summary>
    private IEnumerable<TacticalTaskCandidate> Candidates(
        TacticalPlaybookPackage.TaskAssignment assignment,
        IEnumerable<TacticalTaskCandidate> candidates) => RawCandidates(
            assignment, candidates)
        .Where(value => assignment.Cohort is null
            || _cohortAdmissions.Contains(new(
                assignment.AssignmentId, value.ActorId)));

    private static IEnumerable<TacticalTaskCandidate> RawCandidates(
        TacticalPlaybookPackage.TaskAssignment assignment,
        IEnumerable<TacticalTaskCandidate> candidates) => candidates
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

    private static bool CandidateMatches(
        TacticalPlaybookPackage.ConditionGroup[]? conditions,
        TacticalTaskCandidate candidate,
        int modeTicks,
        Func<TacticalPlaybookPackage.ConditionGroup,
            TacticalTaskCandidate, int, bool> evaluate) =>
        conditions is not { Length: > 0 }
        || MatchesAny(
            conditions,
            group => evaluate(group, candidate, modeTicks));

    private static bool HasCandidateScope(
        TacticalPlaybookPackage.TacticalTask plan) =>
        plan.Assignments.Any(HasCandidateScope);

    private static bool HasCandidateScope(
        TacticalPlaybookPackage.TaskAssignment assignment) =>
        assignment.CandidateWhen is { Length: > 0 }
        || assignment.CandidateUntil is { Length: > 0 };

    private static bool IsCandidateWitness(
        Runtime state,
        TacticalPlaybookPackage.TaskAssignment assignment,
        TacticalTaskCandidate candidate) =>
        !HasCandidateScope(assignment)
        || state.CandidateWitnesses.Contains(new(
            assignment.AssignmentId, candidate.ActorId));

    /// <summary>
    /// Body-scoped mode conditions need a latch independent of participant
    /// ownership. A lower rung remains active while a higher rung leases its
    /// body, and a while/until hysteresis gap remains meaningful. Witnesses
    /// are exact lives; a respawn evaluates the mode afresh.
    /// </summary>
    private static HashSet<int> RefreshCandidateWitnesses(
        int tick,
        Runtime state,
        IReadOnlyDictionary<int, TacticalTaskCandidate> live,
        Func<TacticalPlaybookPackage.ConditionGroup,
            TacticalTaskCandidate, int, bool> evaluateCandidate,
        bool reset)
    {
        var releasedUnitIds = new HashSet<int>();
        if (!HasCandidateScope(state.Plan))
            return releasedUnitIds;
        if (reset)
        {
            state.CandidateWitnesses.Clear();
            state.ReleasedCandidates.Clear();
        }

        HashSet<ActorIdentity> liveActors = live.Values
            .Select(candidate => candidate.ActorId)
            .ToHashSet();
        state.CandidateWitnesses.RemoveWhere(
            key => !liveActors.Contains(key.ActorId));
        state.ReleasedCandidates.RemoveWhere(
            key => !liveActors.Contains(key.ActorId));

        foreach (TacticalPlaybookPackage.TaskAssignment assignment
                 in state.Plan.Assignments.Where(HasCandidateScope))
        foreach (TacticalTaskCandidate candidate in RawCandidates(
                     assignment, live.Values))
        {
            var key = new TacticalCandidateKey(
                assignment.AssignmentId, candidate.ActorId);
            if (state.ReleasedCandidates.Contains(key))
                continue;

            TacticalTaskAssignment? lease = state.Assignments
                .FirstOrDefault(value => string.Equals(
                        value.AssignmentId,
                        assignment.AssignmentId,
                        StringComparison.Ordinal)
                    && value.ActorId == candidate.ActorId);
            if (state.CandidateWitnesses.Contains(key))
            {
                int modeTicks = lease is null
                    ? 0
                    : tick - lease.StartedTick;
                if (assignment.CandidateUntil is { Length: > 0 }
                    && CandidateMatches(
                        assignment.CandidateUntil,
                        candidate,
                        modeTicks,
                        evaluateCandidate))
                {
                    state.CandidateWitnesses.Remove(key);
                    state.ReleasedCandidates.Add(key);
                    if (lease is not null)
                        releasedUnitIds.Add(candidate.UnitId);
                }
                continue;
            }

            if (CandidateMatches(
                    assignment.CandidateWhen,
                    candidate,
                    0,
                    evaluateCandidate))
            {
                state.CandidateWitnesses.Add(key);
            }
        }
        return releasedUnitIds;
    }

    private static int ClassRank(
        TacticalPlaybookPackage.TaskAssignment assignment,
        TacticalTaskCandidate candidate) => assignment.Classes.Length == 0
        ? 0
        : Array.IndexOf(assignment.Classes, candidate.ClassId);

    /// <summary>
    /// Resolve every authored cohort before task leases. Each live body walks
    /// its own eligible ladder from the top. A non-cohort rung settles it.
    /// Bodies meeting the same cohort rung are ranked together by distance to
    /// that rung's one destination, then stable unit id; winners settle and
    /// rejected bodies continue down their own ladders. Membership is
    /// recomputed every tick, with no cohort lifecycle or hidden sticky state.
    /// </summary>
    private void ResolveCohortAdmissions(
        int tick,
        string phaseId,
        IReadOnlyList<TacticalTaskCandidate> candidates,
        Func<TacticalPlaybookPackage.ConditionGroup, bool> evaluate,
        Func<TacticalPlaybookPackage.ConditionGroup,
            TacticalTaskCandidate, int, bool> evaluateCandidate,
        Func<TacticalPlaybookPackage.TaskAssignment,
            TacticalTaskCandidate, int> distance)
    {
        _cohortAdmissions.Clear();
        if (!_plans.SelectMany(plan => plan.Assignments)
                .Any(assignment => assignment.Cohort is not null))
        {
            return;
        }

        var ladders = new Dictionary<ActorIdentity,
            List<(TacticalPlaybookPackage.TacticalTask Plan,
                TacticalPlaybookPackage.TaskAssignment Assignment)>>();
        foreach (TacticalTaskCandidate candidate in candidates)
        {
            var ladder = new List<(
                TacticalPlaybookPackage.TacticalTask Plan,
                TacticalPlaybookPackage.TaskAssignment Assignment)>();
            foreach (TacticalPlaybookPackage.TacticalTask plan in _plans)
            {
                Runtime state = _states[plan.TaskId];
                if (!CohortRungIsEligible(
                        tick, phaseId, state, candidate, evaluate,
                        evaluateCandidate))
                {
                    continue;
                }
                foreach (TacticalPlaybookPackage.TaskAssignment assignment
                         in plan.Assignments)
                {
                    if (RawCandidates(assignment, [candidate]).Any())
                        ladder.Add((plan, assignment));
                }
            }
            ladders[candidate.ActorId] = ladder
                .OrderBy(entry => entry.Plan.Priority)
                .ThenBy(entry => entry.Plan.TaskId, StringComparer.Ordinal)
                .ThenBy(entry => entry.Assignment.AssignmentId,
                    StringComparer.Ordinal)
                .ToList();
        }

        var cursor = candidates.ToDictionary(
            candidate => candidate.ActorId, _ => 0);
        var settled = new HashSet<ActorIdentity>();
        while (settled.Count < candidates.Count)
        {
            var waiting = new Dictionary<string, List<(
                TacticalTaskCandidate Candidate,
                TacticalPlaybookPackage.TaskAssignment Assignment)>>(
                    StringComparer.Ordinal);
            foreach (TacticalTaskCandidate candidate in candidates
                         .OrderBy(value => value.UnitId))
            {
                if (settled.Contains(candidate.ActorId))
                    continue;
                List<(TacticalPlaybookPackage.TacticalTask Plan,
                    TacticalPlaybookPackage.TaskAssignment Assignment)>
                    ladder = ladders[candidate.ActorId];
                int index = cursor[candidate.ActorId];
                if (index >= ladder.Count)
                {
                    settled.Add(candidate.ActorId);
                    continue;
                }
                TacticalPlaybookPackage.TaskAssignment assignment =
                    ladder[index].Assignment;
                if (assignment.Cohort is not { } cohort)
                {
                    settled.Add(candidate.ActorId);
                    continue;
                }
                if (!waiting.TryGetValue(cohort.CohortId, out var members))
                {
                    members = [];
                    waiting.Add(cohort.CohortId, members);
                }
                members.Add((candidate, assignment));
            }
            if (waiting.Count == 0)
                break;

            foreach ((string cohortId, var members) in waiting
                         .OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                TacticalPlaybookPackage.CohortAdmission policy =
                    members[0].Assignment.Cohort!;
                if (members.Any(member => member.Assignment.Cohort is not
                        { } other
                    || other.Take != policy.Take
                    || other.Select != policy.Select
                    || other.DestinationKind != policy.DestinationKind
                    || other.DestinationTarget != policy.DestinationTarget))
                {
                    throw new InvalidDataException(
                        $"Cohort '{cohortId}' has inconsistent runtime rows.");
                }
                var winners = members
                    .OrderBy(member => distance(
                        member.Assignment, member.Candidate))
                    .ThenBy(member => member.Candidate.UnitId)
                    .Take(policy.Take)
                    .Select(member => member.Candidate.ActorId)
                    .ToHashSet();
                foreach ((TacticalTaskCandidate candidate,
                             TacticalPlaybookPackage.TaskAssignment assignment)
                         in members)
                {
                    if (winners.Contains(candidate.ActorId))
                    {
                        _cohortAdmissions.Add(new(
                            assignment.AssignmentId, candidate.ActorId));
                        settled.Add(candidate.ActorId);
                    }
                    else
                    {
                        cursor[candidate.ActorId]++;
                    }
                }
            }
        }
    }

    private bool CohortRungIsEligible(
        int tick,
        string phaseId,
        Runtime state,
        TacticalTaskCandidate candidate,
        Func<TacticalPlaybookPackage.ConditionGroup, bool> evaluate,
        Func<TacticalPlaybookPackage.ConditionGroup,
            TacticalTaskCandidate, int, bool> evaluateCandidate)
    {
        TacticalPlaybookPackage.TacticalTask plan = state.Plan;
        if (!plan.EligiblePhases.Contains(phaseId, StringComparer.Ordinal)
            || state.Phase == TacticalTaskPhase.Reintegrating)
        {
            return false;
        }
        TacticalTaskAssignment? lease = state.Assignments.FirstOrDefault(
            assignment => assignment.ActorId == candidate.ActorId);
        if (state.Phase == TacticalTaskPhase.Dormant)
        {
            if (tick < state.CooldownUntil
                || !MatchesAny(plan.When, evaluate))
            {
                return false;
            }
        }
        else if (MatchesAny(plan.FailWhen, evaluate))
        {
            return false;
        }
        return plan.Assignments.Any(assignment =>
            RawCandidates(assignment, [candidate]).Any()
            && (lease is not null
                ? assignment.CandidateUntil is not { Length: > 0 }
                    || !CandidateMatches(
                        assignment.CandidateUntil,
                        candidate,
                        tick - lease.StartedTick,
                        evaluateCandidate)
                : CandidateMatches(
                    assignment.CandidateWhen,
                    candidate,
                    0,
                    evaluateCandidate)));
    }

    private void ReleaseDepartedCohortMembers(
        int tick,
        IReadOnlyDictionary<int, TacticalTaskCandidate> live)
    {
        foreach (Runtime state in _states.Values.Where(value =>
                     value.Phase == TacticalTaskPhase.Active))
        {
            HashSet<string> cohortAssignments = state.Plan.Assignments
                .Where(assignment => assignment.Cohort is not null)
                .Select(assignment => assignment.AssignmentId)
                .ToHashSet(StringComparer.Ordinal);
            if (cohortAssignments.Count == 0)
                continue;
            state.Assignments.RemoveAll(lease =>
                cohortAssignments.Contains(lease.AssignmentId)
                && (!_cohortAdmissions.Contains(new(
                        lease.AssignmentId, lease.ActorId))
                    || !SameLife(live, lease)));
            if (!MinimumsHold(state))
                FinishRelease(
                    tick, state, "cohort-reselected", applyCooldown: false);
        }
    }

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

    private void FinishRelease(
        int tick,
        Runtime state,
        string reason,
        bool applyCooldown = true)
    {
        TacticalTaskPhase from = state.Phase;
        int[] unitIds = state.Assignments
            .Select(value => value.UnitId)
            .Order()
            .ToArray();
        state.Phase = TacticalTaskPhase.Dormant;
        state.PhaseStartedTick = tick;
        state.CooldownUntil = applyCooldown
            ? tick + state.Plan.CooldownTicks : tick;
        state.Assignments.Clear();
        state.CompletionArmed = false;
        state.ArmedCarrier = null;
        state.CandidateWitnesses.Clear();
        state.ReleasedCandidates.Clear();
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
        internal HashSet<TacticalCandidateKey> CandidateWitnesses { get; } = [];
        internal HashSet<TacticalCandidateKey> ReleasedCandidates { get; } = [];
    }
}
