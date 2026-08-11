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
        Func<OrdersPackage.ConditionExpression, int, bool> evaluate)
    {
        var result = new List<EligibleLeaf>();
        foreach (OrdersPackage.OrderNode node in role.Orders)
            Visit(actor, node, tick, evaluate, result);
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
        State state = _states.GetValueOrDefault(key) ?? new State(false, tick, 0);
        _states[key] = Exit(state, node, tick);
    }

    private void Visit(
        ActorIdentity actor,
        OrdersPackage.OrderNode node,
        int tick,
        Func<OrdersPackage.ConditionExpression, int, bool> evaluate,
        List<EligibleLeaf> result)
    {
        (ActorIdentity, string) key = (actor, node.Path);
        State state = _states.GetValueOrDefault(key) ?? new State(false, tick, 0);
        int elapsed = state.Active ? tick - state.StartedTick : 0;
        bool left = false;
        if (state.Active)
        {
            bool timedOut = node.Timeout is int timeout && elapsed >= timeout;
            bool until = node.Until is not null && evaluate(node.Until, elapsed);
            bool whenEnded = node.Until is null
                && node.When is not null
                && !evaluate(node.When, elapsed);
            if (timedOut || until || whenEnded)
            {
                state = Exit(state, node, tick);
                left = true;
            }
        }
        if (!state.Active
            && !left
            && tick >= state.ReadyTick
            && (node.When is null || evaluate(node.When, 0)))
        {
            state = new State(true, tick, state.ReadyTick);
        }
        _states[key] = state;
        if (!state.Active)
        {
            DeactivateChildren(actor, node.Children, tick);
            return;
        }
        int orderTicks = tick - state.StartedTick;
        if (node.Action is not null)
        {
            result.Add(new EligibleLeaf(node, orderTicks));
            return;
        }
        foreach (OrdersPackage.OrderNode child in node.Children)
            Visit(actor, child, tick, evaluate, result);
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
            ReadyTick: checked(tick + 1 + node.Cooldown));

    internal sealed record EligibleLeaf(
        OrdersPackage.OrderNode Node,
        int OrderTicks);

    private sealed record State(bool Active, int StartedTick, int ReadyTick);
}
