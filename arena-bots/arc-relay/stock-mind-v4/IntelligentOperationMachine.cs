/// <summary>
/// Pure deterministic scheduler for evaluation-grade operation cards. Game
/// facts and reachability enter through callbacks, keeping the state machine
/// independently testable and incapable of consulting hidden match state.
/// </summary>
internal sealed class IntelligentOperationMachine
{
    private readonly IntelligentOperationPlan[] _plans;
    private readonly Dictionary<string, Runtime> _states;
    private readonly Dictionary<int, OperationDirective> _directives = [];
    private readonly List<OperationTrace> _transitions = [];

    internal IntelligentOperationMachine(
        IEnumerable<IntelligentOperationPlan> plans)
    {
        _plans = plans.OrderBy(value => value.Priority)
            .ThenBy(value => value.Id, StringComparer.Ordinal).ToArray();
        _states = _plans.ToDictionary(
            value => value.Id,
            value => new Runtime(value),
            StringComparer.Ordinal);
    }

    internal IReadOnlyList<OperationTrace> Transitions => _transitions;
    internal OperationDirective? DirectiveFor(int unitId) =>
        _directives.GetValueOrDefault(unitId);
    internal OperationStateView State(string id) => View(_states[id]);
    internal string TraceSummary => _plans.Length == 0
        ? "ops=none"
        : string.Join(";", _plans.Select(plan =>
        {
            Runtime state = _states[plan.Id];
            string branch = state.Branch is null ? "" : $"/{state.Branch.Id}";
            string claims = state.Assignments.Count == 0
                ? "-"
                : string.Join(",", state.Assignments
                    .OrderBy(value => value.UnitId)
                    .Select(value => $"{value.UnitId}:{value.TaskId}"));
            string evidence = state.Evidence.Count == 0
                ? "-"
                : string.Join(",", state.Evidence);
            return $"{plan.Id}={state.Phase.ToString().ToLowerInvariant()}"
                + $"{branch}[{state.LastReason}|c={claims}|e={evidence}]";
        }));

    internal void Update(
        int tick,
        IReadOnlyList<OperationActor> actors,
        Func<IntelligentOperationPlan, OperationStateView,
            OperationCondition, OperationTruth> evaluate,
        Func<OperationActor, OperationTask, int, bool> feasible)
    {
        _directives.Clear();
        Dictionary<int, OperationActor> live = actors.ToDictionary(
            value => value.UnitId);
        var reserved = new Dictionary<int, string>();

        // Commitment is the lock boundary, regardless of card priority.
        foreach (IntelligentOperationPlan plan in _plans)
        {
            Runtime state = _states[plan.Id];
            if (state.Phase != OperationPhase.Commit)
                continue;
            foreach (OperationAssignment assignment in state.Assignments)
            {
                if (SameLife(live, assignment))
                    reserved[assignment.UnitId] = plan.Id;
            }
        }

        // Priority order lets a higher card claim bodies from preparation or
        // recovery. Committed claims above remain unavailable to every card.
        foreach (IntelligentOperationPlan plan in _plans)
        {
            Runtime state = _states[plan.Id];
            state.LastReason = "steady";
            state.Evidence.Clear();
            switch (state.Phase)
            {
                case OperationPhase.Dormant:
                    Dormant(tick, state, live, reserved, evaluate, feasible);
                    break;
                case OperationPhase.Prepare:
                    Prepare(tick, state, live, reserved, evaluate, feasible);
                    break;
                case OperationPhase.Commit:
                    Commit(tick, state, live, evaluate, feasible);
                    break;
                case OperationPhase.Recover:
                    Recover(tick, state, live, reserved, evaluate);
                    break;
            }
            if (state.Phase is OperationPhase.Prepare
                or OperationPhase.Recover)
            {
                foreach (OperationAssignment assignment in state.Assignments)
                {
                    if (SameLife(live, assignment))
                        reserved[assignment.UnitId] = plan.Id;
                }
            }
        }

        foreach (IntelligentOperationPlan plan in _plans)
        {
            Runtime state = _states[plan.Id];
            OperationTask[] tasks = Tasks(state);
            foreach (OperationAssignment assignment in state.Assignments
                         .OrderBy(value => value.UnitId))
            {
                if (!SameLife(live, assignment))
                    continue;
                OperationTask task = tasks.Single(value => string.Equals(
                    value.Id, assignment.TaskId, StringComparison.Ordinal));
                _directives[assignment.UnitId] = new OperationDirective(
                    plan.Id, state.Phase, state.Branch?.Id, task);
            }
        }
    }

    private void Dormant(
        int tick,
        Runtime state,
        IReadOnlyDictionary<int, OperationActor> live,
        IReadOnlyDictionary<int, string> reserved,
        Func<IntelligentOperationPlan, OperationStateView,
            OperationCondition, OperationTruth> evaluate,
        Func<OperationActor, OperationTask, int, bool> feasible)
    {
        OperationTruth truth = EvaluateGroup(
            state.Plan.PrepareWhen, state, evaluate);
        if (truth == OperationTruth.False)
        {
            state.EdgeReady = true;
            state.ArmedTick = null;
            state.LastReason = "evidence-false-rearmed";
            return;
        }
        if (truth == OperationTruth.Unknown)
        {
            state.LastReason = "evidence-unknown";
            return;
        }
        if (!state.EdgeReady)
        {
            state.LastReason = "edge-not-rearmed";
            return;
        }
        if (tick < state.CooldownUntil)
        {
            state.LastReason = $"cooldown-until-{state.CooldownUntil}";
            return;
        }

        state.ArmedTick ??= tick;
        int deadline = state.ArmedTick.Value + state.Plan.PrepareDeadlineTicks;
        if (tick > deadline)
        {
            state.ArmedTick = null;
            state.EdgeReady = false;
            state.LastReason = "prepare-window-expired";
            return;
        }
        if (!Select(
                state.Plan.PrepareTasks,
                live,
                reserved,
                deadline - tick,
                feasible,
                null,
                out List<OperationAssignment> assignments))
        {
            state.LastReason = "armed-actors-unavailable";
            return;
        }
        state.Assignments = assignments;
        state.ArmedTick = null;
        state.EdgeReady = false;
        Transition(tick, state, OperationPhase.Prepare,
            "evidence-and-actors");
    }

    private void Prepare(
        int tick,
        Runtime state,
        IReadOnlyDictionary<int, OperationActor> live,
        IReadOnlyDictionary<int, string> reserved,
        Func<IntelligentOperationPlan, OperationStateView,
            OperationCondition, OperationTruth> evaluate,
        Func<OperationActor, OperationTask, int, bool> feasible)
    {
        if (AnyTrue(state.Plan.PrepareAbortAny, state, evaluate))
        {
            BeginRecovery(tick, state, RecoveryKind.Abort, live, feasible,
                "prepare-abort");
            return;
        }
        int deadline = state.PhaseStartedTick
            + state.Plan.PrepareDeadlineTicks;
        if (!RefreshPreparation(
                state, live, reserved, deadline - tick, feasible))
        {
            BeginRecovery(tick, state, RecoveryKind.Abort, live, feasible,
                "prepare-participant-minimum");
            return;
        }
        if (tick >= deadline)
        {
            BeginRecovery(tick, state, RecoveryKind.Abort, live, feasible,
                "prepare-deadline");
            return;
        }
        foreach (OperationBranch branch in state.Plan.Branches)
        {
            if (EvaluateGroup(branch.CommitWhen, state, evaluate)
                != OperationTruth.True)
            {
                continue;
            }
            var otherReservations = reserved
                .Where(pair => !string.Equals(
                    pair.Value, state.Plan.Id, StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            var prepared = state.Assignments.Select(value => value.UnitId)
                .ToHashSet();
            if (!Select(
                    branch.Tasks,
                    live,
                    otherReservations,
                    branch.DeadlineTicks,
                    feasible,
                    prepared,
                    out List<OperationAssignment> assignments))
            {
                state.LastReason = $"branch-{branch.Id}-actors-unavailable";
                continue;
            }
            state.Branch = branch;
            state.Assignments = assignments;
            Transition(tick, state, OperationPhase.Commit,
                $"branch-{branch.Id}");
            return;
        }
        state.LastReason = "preparing-no-branch";
    }

    private void Commit(
        int tick,
        Runtime state,
        IReadOnlyDictionary<int, OperationActor> live,
        Func<IntelligentOperationPlan, OperationStateView,
            OperationCondition, OperationTruth> evaluate,
        Func<OperationActor, OperationTask, int, bool> feasible)
    {
        OperationBranch branch = state.Branch
            ?? throw new InvalidOperationException("Commit has no branch.");
        // Normative tie-break: success, abort, actor minimum, deadline.
        if (AnyTrue(branch.SuccessAny, state, evaluate))
        {
            BeginRecovery(tick, state, RecoveryKind.Success, live, feasible,
                "mission-success");
            return;
        }
        if (AnyTrue(branch.AbortAny, state, evaluate))
        {
            BeginRecovery(tick, state, RecoveryKind.Abort, live, feasible,
                "mission-abort");
            return;
        }
        if (!CommittedMinimumsHold(state, live))
        {
            BeginRecovery(tick, state, RecoveryKind.Abort, live, feasible,
                "commit-participant-minimum");
            return;
        }
        if (tick >= state.PhaseStartedTick + branch.DeadlineTicks)
        {
            BeginRecovery(tick, state, RecoveryKind.Abort, live, feasible,
                "mission-deadline");
            return;
        }
        state.LastReason = $"committed-{branch.Id}";
    }

    private void Recover(
        int tick,
        Runtime state,
        IReadOnlyDictionary<int, OperationActor> live,
        IReadOnlyDictionary<int, string> reserved,
        Func<IntelligentOperationPlan, OperationStateView,
            OperationCondition, OperationTruth> evaluate)
    {
        state.Assignments.RemoveAll(value => !SameLife(live, value)
            || reserved.TryGetValue(value.UnitId, out string? owner)
            && !string.Equals(owner, state.Plan.Id, StringComparison.Ordinal));
        bool complete = state.Plan.Recovery.CompleteAll.Length == 0
            ? state.Assignments.Count == 0
            : state.Plan.Recovery.CompleteAll.All(condition =>
                Evaluate(condition, state, evaluate)
                    == OperationTruth.True);
        if (!complete && tick < state.PhaseStartedTick
                + state.Plan.Recovery.DeadlineTicks)
        {
            state.LastReason = $"recovering-{state.RecoveryKind!.Value.ToString().ToLowerInvariant()}";
            return;
        }
        string reason = complete
            ? "recovery-complete"
            : "recovery-deadline-baseline-release";
        OperationPhase from = state.Phase;
        state.Phase = OperationPhase.Dormant;
        state.PhaseStartedTick = tick;
        state.CooldownUntil = tick + state.Plan.CooldownTicks;
        state.Assignments.Clear();
        state.Branch = null;
        state.RecoveryKind = null;
        state.LastReason = reason;
        _transitions.Add(new OperationTrace(
            tick, state.Plan.Id, from, state.Phase, reason, null));
    }

    private bool RefreshPreparation(
        Runtime state,
        IReadOnlyDictionary<int, OperationActor> live,
        IReadOnlyDictionary<int, string> reserved,
        int remainingTicks,
        Func<OperationActor, OperationTask, int, bool> feasible)
    {
        var refreshed = new List<OperationAssignment>();
        foreach (OperationTask task in state.Plan.PrepareTasks)
        {
            List<OperationAssignment> prior = state.Assignments
                .Where(value => value.TaskId == task.Id)
                .Where(value => SameLife(live, value))
                .Where(value => !reserved.TryGetValue(
                        value.UnitId, out string? owner)
                    || owner == state.Plan.Id)
                .ToList();
            if (task.Resilience == ParticipantResilience.Essential
                && prior.Count < task.Minimum)
            {
                return false;
            }
            if (task.Resilience == ParticipantResilience.Replaceable
                && prior.Count < task.Minimum)
            {
                HashSet<int> used = refreshed.Concat(prior)
                    .Select(value => value.UnitId).ToHashSet();
                IEnumerable<OperationActor> replacements = Candidates(
                        task, live.Values)
                    .Where(value => !used.Contains(value.UnitId))
                    .Where(value => !reserved.ContainsKey(value.UnitId))
                    .Where(value => feasible(
                        value, task, Math.Max(0, remainingTicks)))
                    .OrderBy(value => value.UnitId);
                foreach (OperationActor actor in replacements
                             .Take(task.Minimum - prior.Count))
                {
                    prior.Add(new OperationAssignment(
                        task.Id, actor.UnitId, actor.LifeKey));
                }
            }
            if (task.Resilience != ParticipantResilience.Optional
                && prior.Count < task.Minimum)
            {
                return false;
            }
            refreshed.AddRange(prior);
        }
        state.Assignments = refreshed;
        return true;
    }

    private void BeginRecovery(
        int tick,
        Runtime state,
        RecoveryKind kind,
        IReadOnlyDictionary<int, OperationActor> live,
        Func<OperationActor, OperationTask, int, bool> feasible,
        string reason)
    {
        HashSet<int> survivors = state.Assignments
            .Where(value => SameLife(live, value))
            .Select(value => value.UnitId).ToHashSet();
        OperationTask[] tasks = kind == RecoveryKind.Success
            ? state.Plan.Recovery.OnSuccess
            : state.Plan.Recovery.OnAbort;
        _ = Select(
            tasks,
            live,
            new Dictionary<int, string>(),
            state.Plan.Recovery.DeadlineTicks,
            feasible,
            survivors,
            out List<OperationAssignment> assignments);
        state.RecoveryKind = kind;
        state.Assignments = assignments;
        Transition(tick, state, OperationPhase.Recover, reason);
    }

    private static bool Select(
        IReadOnlyList<OperationTask> tasks,
        IReadOnlyDictionary<int, OperationActor> live,
        IReadOnlyDictionary<int, string> reserved,
        int remainingTicks,
        Func<OperationActor, OperationTask, int, bool> feasible,
        IReadOnlySet<int>? allowed,
        out List<OperationAssignment> assignments)
    {
        assignments = [];
        var claimed = new HashSet<int>();
        foreach (OperationTask task in tasks)
        {
            OperationActor[] candidates = Candidates(task, live.Values)
                .Where(value => allowed is null || allowed.Contains(value.UnitId))
                .Where(value => !reserved.ContainsKey(value.UnitId))
                .Where(value => !claimed.Contains(value.UnitId))
                .Where(value => feasible(value, task, remainingTicks))
                .OrderBy(value => value.UnitId).ToArray();
            int take = task.Resilience == ParticipantResilience.Optional
                ? candidates.Length
                : task.Minimum;
            if (task.Resilience != ParticipantResilience.Optional
                && candidates.Length < task.Minimum)
            {
                assignments = [];
                return false;
            }
            foreach (OperationActor actor in candidates.Take(take))
            {
                claimed.Add(actor.UnitId);
                assignments.Add(new OperationAssignment(
                    task.Id, actor.UnitId, actor.LifeKey));
            }
        }
        return true;
    }

    private static IEnumerable<OperationActor> Candidates(
        OperationTask task,
        IEnumerable<OperationActor> actors) => actors
        .Where(value => task.PermitsCarrying || !value.CarriesCore)
        .Where(value => !task.RequiresCarrying || value.CarriesCore)
        .Where(value => task.CandidateUnitIds.Length == 0
            || task.CandidateUnitIds.Contains(value.UnitId))
        .Where(value => task.CandidateRoles.Length == 0
            || task.CandidateRoles.Contains(
                value.BaselineRole, StringComparer.Ordinal))
        .Where(value => task.CandidateClassIds.Length == 0
            || task.CandidateClassIds.Contains(
                value.ClassId, StringComparer.Ordinal));

    private static bool CommittedMinimumsHold(
        Runtime state,
        IReadOnlyDictionary<int, OperationActor> live) =>
        state.Branch!.Tasks.All(task =>
            task.Resilience == ParticipantResilience.Optional
            || state.Assignments.Count(value => value.TaskId == task.Id
                && SameLife(live, value)) >= task.Minimum);

    private static OperationTask[] Tasks(Runtime state) => state.Phase switch
    {
        OperationPhase.Prepare => state.Plan.PrepareTasks,
        OperationPhase.Commit => state.Branch!.Tasks,
        OperationPhase.Recover => state.RecoveryKind == RecoveryKind.Success
            ? state.Plan.Recovery.OnSuccess
            : state.Plan.Recovery.OnAbort,
        _ => [],
    };

    private static bool SameLife(
        IReadOnlyDictionary<int, OperationActor> live,
        OperationAssignment assignment) =>
        live.TryGetValue(assignment.UnitId, out OperationActor? actor)
        && actor.LifeKey == assignment.LifeKey;

    private static OperationTruth EvaluateGroup(
        OperationConditionGroup group,
        Runtime state,
        Func<IntelligentOperationPlan, OperationStateView,
            OperationCondition, OperationTruth> evaluate)
    {
        OperationTruth[] all = group.All.Select(value =>
            Evaluate(value, state, evaluate)).ToArray();
        if (all.Contains(OperationTruth.False))
            return OperationTruth.False;
        OperationTruth[] any = group.Any.Select(value =>
            Evaluate(value, state, evaluate)).ToArray();
        if (any.Length > 0 && any.All(value => value == OperationTruth.False))
            return OperationTruth.False;
        return all.All(value => value == OperationTruth.True)
            && (any.Length == 0 || any.Contains(OperationTruth.True))
                ? OperationTruth.True
                : OperationTruth.Unknown;
    }

    private static bool AnyTrue(
        IEnumerable<OperationCondition> conditions,
        Runtime state,
        Func<IntelligentOperationPlan, OperationStateView,
            OperationCondition, OperationTruth> evaluate) =>
        conditions.Any(value => Evaluate(value, state, evaluate)
            == OperationTruth.True);

    private static OperationTruth Evaluate(
        OperationCondition condition,
        Runtime state,
        Func<IntelligentOperationPlan, OperationStateView,
            OperationCondition, OperationTruth> evaluate)
    {
        OperationTruth truth = evaluate(state.Plan, View(state), condition);
        string code = truth switch
        {
            OperationTruth.True => "t",
            OperationTruth.False => "f",
            _ => "u",
        };
        state.Evidence.Add($"{condition.Fact}:{code}");
        return truth;
    }

    private void Transition(
        int tick,
        Runtime state,
        OperationPhase to,
        string reason)
    {
        OperationPhase from = state.Phase;
        state.Phase = to;
        state.PhaseStartedTick = tick;
        state.LastReason = reason;
        _transitions.Add(new OperationTrace(
            tick, state.Plan.Id, from, to, reason, state.Branch?.Id));
    }

    private static OperationStateView View(Runtime state) => new(
        state.Plan.Id,
        state.Phase,
        state.Branch?.Id,
        state.RecoveryKind,
        state.PhaseStartedTick,
        state.Assignments.ToArray());

    private sealed class Runtime
    {
        internal Runtime(IntelligentOperationPlan plan) => Plan = plan;
        internal IntelligentOperationPlan Plan { get; }
        internal OperationPhase Phase { get; set; }
        internal int PhaseStartedTick { get; set; }
        internal int CooldownUntil { get; set; }
        internal int? ArmedTick { get; set; }
        internal bool EdgeReady { get; set; } = true;
        internal OperationBranch? Branch { get; set; }
        internal RecoveryKind? RecoveryKind { get; set; }
        internal List<OperationAssignment> Assignments { get; set; } = [];
        internal List<string> Evidence { get; } = [];
        internal string LastReason { get; set; } = "initial";
    }
}
