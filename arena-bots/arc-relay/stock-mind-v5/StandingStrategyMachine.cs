internal sealed class StandingStrategyMachine
{
    private readonly StandingStrategyPlan _plan;
    private readonly Dictionary<string, int> _streaks =
        new(StringComparer.Ordinal);
    private string _phaseId;
    private int _enteredTick;

    internal StandingStrategyMachine(StandingStrategyPlan plan, int startTick = 0)
    {
        _plan = plan;
        _phaseId = plan.InitialPhase;
        _enteredTick = startTick;
    }

    internal string PhaseId => _phaseId;
    internal int EnteredTick => _enteredTick;
    internal int TicksInPhase(int tick) => Math.Max(0, tick - _enteredTick);
    internal StandingPhasePlan Phase => _plan.Phases.Single(value =>
        string.Equals(value.Id, _phaseId, StringComparison.Ordinal));

    internal bool Advance(
        StandingSnapshot snapshot,
        Func<StandingCondition, StandingSnapshot, bool> evaluate)
    {
        StandingPhasePlan current = Phase;
        if (TicksInPhase(snapshot.Tick) < current.MinimumTicks)
        {
            _streaks.Clear();
            return false;
        }
        foreach (StandingTransitionPlan transition in current.Transitions
                     .OrderBy(value => value.Priority)
                     .ThenBy(value => value.To, StringComparer.Ordinal))
        {
            string key = $"{current.Id}->{transition.To}";
            StandingPhasePlan target = _plan.Phases.Single(value =>
                string.Equals(value.Id, transition.To, StringComparison.Ordinal));
            bool transitionApplies = transition.When.Length > 0
                && transition.When.Any(group => Matches(group, snapshot, evaluate));
            bool entryApplies = target.Entry.Length == 0
                || target.Entry.Any(group => Matches(group, snapshot, evaluate));
            bool applies = transitionApplies && entryApplies;
            int streak = applies ? _streaks.GetValueOrDefault(key) + 1 : 0;
            _streaks[key] = streak;
            if (streak < transition.StableTicks)
                continue;
            _phaseId = transition.To;
            _enteredTick = snapshot.Tick;
            _streaks.Clear();
            return true;
        }
        return false;
    }

    internal static bool Matches(
        StandingConditionGroup group,
        StandingSnapshot snapshot,
        Func<StandingCondition, StandingSnapshot, bool> evaluate) =>
        group.All.All(condition => evaluate(condition, snapshot))
        && (group.Any.Length == 0
            || group.Any.Any(condition => evaluate(condition, snapshot)));
}
