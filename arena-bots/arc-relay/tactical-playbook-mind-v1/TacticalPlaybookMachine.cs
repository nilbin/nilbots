internal sealed class TacticalPlaybookMachine
{
    private readonly TacticalPlaybookPackage.Playbook _playbook;
    private readonly Dictionary<string, LocalRuntime> _locals =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _globalStreaks =
        new(StringComparer.Ordinal);
    private string _phaseId;
    private int _phaseEnteredTick;

    internal TacticalPlaybookMachine(
        TacticalPlaybookPackage.Playbook playbook,
        int startTick = 0)
    {
        _playbook = playbook;
        _phaseId = playbook.Coordination.InitialPhase;
        _phaseEnteredTick = startTick;
        foreach (TacticalPlaybookPackage.Group group in playbook.Groups)
        {
            _locals[group.GroupId] = new LocalRuntime(
                group.LocalStateMachine.InitialState,
                startTick,
                new Dictionary<string, int>(StringComparer.Ordinal));
        }
    }

    internal string PhaseId => _phaseId;
    internal int PhaseEnteredTick => _phaseEnteredTick;
    internal TacticalPlaybookPackage.Phase Phase =>
        _playbook.Coordination.Phases.Single(value =>
            string.Equals(value.PhaseId, _phaseId, StringComparison.Ordinal));

    internal string LocalState(string groupId) =>
        _locals.GetValueOrDefault(groupId)?.StateId
        ?? throw new InvalidDataException($"Unknown tactical group '{groupId}'.");

    internal bool AdvanceGlobal(
        int tick,
        Func<TacticalPlaybookPackage.Condition, bool> evaluate)
    {
        TacticalPlaybookPackage.Phase phase = Phase;
        bool beforeMinimum = tick - _phaseEnteredTick < phase.MinimumTicks;
        foreach (TacticalPlaybookPackage.Transition transition in
                 phase.Transitions.OrderBy(value => value.Priority)
                     .ThenBy(value => value.To, StringComparer.Ordinal))
        {
            if (beforeMinimum && !string.Equals(
                    transition.MinimumPolicy,
                    "interrupt",
                    StringComparison.Ordinal))
            {
                continue;
            }
            string key = $"{phase.PhaseId}->{transition.To}";
            bool applies = transition.When.Any(group =>
                Matches(group, evaluate));
            int streak = applies
                ? _globalStreaks.GetValueOrDefault(key) + 1
                : 0;
            _globalStreaks[key] = streak;
            if (streak < transition.StableTicks)
                continue;
            _phaseId = transition.To;
            _phaseEnteredTick = tick;
            _globalStreaks.Clear();
            return true;
        }
        return false;
    }

    internal bool AdvanceLocal(
        TacticalPlaybookPackage.Group group,
        int tick,
        Func<TacticalPlaybookPackage.Condition, bool> evaluate)
    {
        LocalRuntime runtime = _locals[group.GroupId];
        TacticalPlaybookPackage.LocalState state =
            group.LocalStateMachine.States.Single(value =>
                string.Equals(value.StateId, runtime.StateId,
                    StringComparison.Ordinal));
        bool beforeMinimum = tick - runtime.EnteredTick < state.MinimumTicks;
        foreach (TacticalPlaybookPackage.Transition transition in
                 state.Transitions.OrderBy(value => value.Priority)
                     .ThenBy(value => value.To, StringComparer.Ordinal))
        {
            if (beforeMinimum && !string.Equals(
                    transition.MinimumPolicy,
                    "interrupt",
                    StringComparison.Ordinal))
            {
                continue;
            }
            string key = $"{state.StateId}->{transition.To}";
            bool applies = transition.When.Any(groupValue =>
                Matches(groupValue, evaluate));
            int streak = applies
                ? runtime.Streaks.GetValueOrDefault(key) + 1
                : 0;
            runtime.Streaks[key] = streak;
            if (streak < transition.StableTicks)
                continue;
            _locals[group.GroupId] = new LocalRuntime(
                transition.To,
                tick,
                new Dictionary<string, int>(StringComparer.Ordinal));
            return true;
        }
        return false;
    }

    internal static bool Matches(
        TacticalPlaybookPackage.ConditionGroup group,
        Func<TacticalPlaybookPackage.Condition, bool> evaluate) =>
        group.All.All(evaluate)
        && (group.Any.Length == 0 || group.Any.Any(evaluate));

    private sealed record LocalRuntime(
        string StateId,
        int EnteredTick,
        Dictionary<string, int> Streaks);
}
