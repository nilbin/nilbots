namespace BotArena.Engine;

/// <summary>
/// Health distribution for fresh descendant lives. The minimum is an
/// eligibility threshold: replication blocks unless the divided result meets
/// it for every descendant. It is never a clamp that can create health.
/// </summary>
public sealed record ActorReplicationHealthDefinition
{
    public ActorReplicationHealthDefinition(
        DistributionKind distribution,
        int minimumHealthPerDescendant,
        RemainderKind remainder)
    {
        if (!Enum.IsDefined(distribution))
            throw new ArgumentOutOfRangeException(nameof(distribution));
        if (minimumHealthPerDescendant <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumHealthPerDescendant));
        }
        if (!Enum.IsDefined(remainder))
            throw new ArgumentOutOfRangeException(nameof(remainder));

        Distribution = distribution;
        MinimumHealthPerDescendant = minimumHealthPerDescendant;
        Remainder = remainder;
    }

    public DistributionKind Distribution { get; }

    /// <summary>
    /// Minimum health each divided result must already have. A source below
    /// the corresponding total threshold cannot replicate.
    /// </summary>
    public int MinimumHealthPerDescendant { get; }
    public RemainderKind Remainder { get; }
    public ActorReplicationMaximumHealthKind MaximumHealth =>
        ActorReplicationMaximumHealthKind.ClampDownToOutputFormMaximum;

    public enum DistributionKind
    {
        DivideCurrentHealthEquallyFloor = 0,
    }

    public enum RemainderKind
    {
        Discard = 0,
    }

    public enum ActorReplicationMaximumHealthKind
    {
        /// <summary>
        /// After division and the minimum-health eligibility check, each
        /// result is capped to the output form's maximum. This may discard
        /// health but can never create it.
        /// </summary>
        ClampDownToOutputFormMaximum = 0,
    }
}
