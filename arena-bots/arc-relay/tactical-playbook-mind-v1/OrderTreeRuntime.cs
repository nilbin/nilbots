using BotArena.Sdk;

/// <summary>
/// Per-body lifecycle for the authored order tree. Priority is source order;
/// pre-emption never rewrites state, and every clock is an authoritative match
/// tick rather than an executor counter.
/// </summary>
internal sealed class OrderTreeRuntime
{
    private readonly Dictionary<(ActorIdentity Actor, string Path), State> _states = [];

    public IReadOnlyList<EligibleLeaf> Eligible(
        ActorIdentity actor,
        OrdersPackage.Role role,
        int tick,
        Func<OrdersPackage.ConditionExpression, int, ConditionEvaluation> evaluate)
    {
        var result = new List<EligibleLeaf>();
        foreach (OrdersPackage.OrderNode node in role.Orders)
            Visit(actor, node, tick, evaluate, result, inheritedEvidence: null);
        return result;
    }

    public void Prune(IReadOnlySet<ActorIdentity> live)
    {
        foreach ((ActorIdentity actor, string path) in _states.Keys
                     .Where(key => !live.Contains(key.Actor)).ToArray())
            _states.Remove((actor, path));
    }

    public void Complete(
        ActorIdentity actor,
        OrdersPackage.OrderNode node,
        int tick)
    {
        (ActorIdentity, string) key = (actor, node.Path);
        State state = _states.GetValueOrDefault(key)
            ?? new State(false, tick, 0, null);
        _states[key] = Exit(state, node, tick);
    }

    private void Visit(
        ActorIdentity actor,
        OrdersPackage.OrderNode node,
        int tick,
        Func<OrdersPackage.ConditionExpression, int, ConditionEvaluation> evaluate,
        List<EligibleLeaf> result,
        string? inheritedEvidence)
    {
        (ActorIdentity, string) key = (actor, node.Path);
        State state = _states.GetValueOrDefault(key)
            ?? new State(false, tick, 0, null);
        int elapsed = state.Active ? tick - state.StartedTick : 0;
        string? currentEvidence = state.EvidenceTrace;
        bool left = false;
        if (state.Active)
        {
            bool timedOut = node.Timeout is int timeout && elapsed >= timeout;
            ConditionEvaluation? untilEvaluation = node.Until is not null
                ? evaluate(node.Until, elapsed)
                : null;
            ConditionEvaluation? whenEvaluation = node.Until is null
                    && node.When is not null
                ? evaluate(node.When, elapsed)
                : null;
            currentEvidence = MergeEvidence(
                state.EvidenceTrace,
                untilEvaluation?.Trace ?? whenEvaluation?.Trace);
            bool until = untilEvaluation?.Value == true;
            bool whenEnded = whenEvaluation is { Value: false };
            if (timedOut || until || whenEnded)
            {
                state = Exit(state, node, tick);
                left = true;
            }
        }
        ConditionEvaluation entry = !state.Active
                && !left
                && tick >= state.ReadyTick
            ? node.When is null
                ? ConditionEvaluation.True
                : evaluate(node.When, 0)
            : ConditionEvaluation.False;
        if (!state.Active
            && !left
            && tick >= state.ReadyTick
            && entry.Value)
        {
            state = new State(true, tick, state.ReadyTick, entry.Trace);
            currentEvidence = entry.Trace;
        }
        _states[key] = state;
        if (!state.Active)
        {
            DeactivateChildren(actor, node.Children, tick);
            return;
        }
        int orderTicks = tick - state.StartedTick;
        string? evidence = MergeEvidence(inheritedEvidence, currentEvidence);
        if (node.Action is not null)
        {
            result.Add(new EligibleLeaf(
                node, orderTicks, state.StartedTick, evidence));
            return;
        }
        foreach (OrdersPackage.OrderNode child in node.Children)
            Visit(actor, child, tick, evaluate, result, evidence);
    }

    private void DeactivateChildren(
        ActorIdentity actor,
        IReadOnlyCollection<OrdersPackage.OrderNode> children,
        int tick)
    {
        foreach (OrdersPackage.OrderNode child in children)
        {
            (ActorIdentity, string) key = (actor, child.Path);
            if (_states.TryGetValue(key, out State? state) && state.Active)
                _states[key] = Exit(state, child, tick);
            DeactivateChildren(actor, child.Children, tick);
        }
    }

    private static State Exit(
        State state,
        OrdersPackage.OrderNode node,
        int tick) => new(
            Active: false,
            StartedTick: state.StartedTick,
            ReadyTick: checked(tick + 1 + node.Cooldown),
            EvidenceTrace: null);

    private static string? MergeEvidence(string? first, string? second)
    {
        if (string.IsNullOrEmpty(first))
            return second;
        if (string.IsNullOrEmpty(second) || first.Contains(
                second, StringComparison.Ordinal))
            return first;
        return $"{first}+{second}";
    }

    internal readonly record struct ConditionEvaluation(bool Value, string? Trace)
    {
        public static ConditionEvaluation True => new(true, null);
        public static ConditionEvaluation False => new(false, null);
    }

    internal sealed record EligibleLeaf(
        OrdersPackage.OrderNode Node,
        int OrderTicks,
        int ActivatedTick,
        string? EvidenceTrace);

    private sealed record State(
        bool Active,
        int StartedTick,
        int ReadyTick,
        string? EvidenceTrace);
}
