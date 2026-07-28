namespace BotArena.Engine;

/// <summary>
/// Match-wide runtime limits for the serializer-neutral actor rules catalog.
/// Topology cardinality belongs to the resolved match contract, not here.
/// </summary>
public sealed record ActorRulesLimits
{
    public ActorRulesLimits(
        int maxTicks,
        ActorRuntimeFaultDefinition runtimeFaults)
    {
        if (maxTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTicks));
        ArgumentNullException.ThrowIfNull(runtimeFaults);

        MaxTicks = maxTicks;
        RuntimeFaults = runtimeFaults;
    }

    public int MaxTicks { get; }
    public ActorRuntimeFaultDefinition RuntimeFaults { get; }
}
