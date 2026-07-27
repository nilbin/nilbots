namespace BotArena.Engine;

/// <summary>
/// Resolved random-stream and runtime-isolation semantics for generic actor
/// matches. These values are fingerprinted even when a schema generation
/// currently admits only one policy.
/// </summary>
public sealed record ActorSeedMechanicsDefinition
{
    public ActorSeedMechanicsDefinition(
        string seedProfileId,
        SeedDerivationKind seedDerivation,
        LifeIdentityAssignmentKind lifeIdentityAssignment,
        RuntimeLifetimeKind runtimeLifetime,
        PrivateMemoryKind privateMemory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seedProfileId);
        if (!Enum.IsDefined(seedDerivation))
            throw new ArgumentOutOfRangeException(nameof(seedDerivation));
        if (!Enum.IsDefined(lifeIdentityAssignment))
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifeIdentityAssignment));
        }
        if (!Enum.IsDefined(runtimeLifetime))
            throw new ArgumentOutOfRangeException(nameof(runtimeLifetime));
        if (!Enum.IsDefined(privateMemory))
            throw new ArgumentOutOfRangeException(nameof(privateMemory));

        SeedProfileId = seedProfileId;
        SeedDerivation = seedDerivation;
        LifeIdentityAssignment = lifeIdentityAssignment;
        RuntimeLifetime = runtimeLifetime;
        PrivateMemory = privateMemory;
    }

    /// <summary>
    /// Explicit comparison namespace. Experimental arms may deliberately share
    /// it to retain common random streams; because it is fingerprinted, this is
    /// never hidden ruleset-alias behavior.
    /// </summary>
    public string SeedProfileId { get; }
    public SeedDerivationKind SeedDerivation { get; }
    public LifeIdentityAssignmentKind LifeIdentityAssignment { get; }
    public RuntimeLifetimeKind RuntimeLifetime { get; }
    public PrivateMemoryKind PrivateMemory { get; }

    public enum SeedDerivationKind
    {
        /// <summary>
        /// Let H be unsigned FNV-1a-64 over UTF-8("actors:" +
        /// SeedProfileId), STEP be 0x9E3779B97F4A7C15, and Mix be the
        /// SplitMix64 finalizer with constants 0xBF58476D1CE4E5B9 and
        /// 0x94D049BB133111EB. In unchecked UInt64 arithmetic compute
        /// x=Mix(matchSeed XOR H), then successively
        /// x=Mix(x+STEP*(teamId+1)), x=Mix(x+STEP*(unitId+1)), and
        /// x=Mix(x+STEP*(lifeId+1)). IDs are non-negative Int32 values.
        /// </summary>
        MatchSeedProfileTeamUnitLifeMix64V1 = 0,
    }

    public enum LifeIdentityAssignmentKind
    {
        PerStableUnitMonotonicStartingAtZero = 0,
    }

    public enum RuntimeLifetimeKind
    {
        FreshRuntimePerLife = 0,
    }

    public enum PrivateMemoryKind
    {
        IsolatedPerRuntime = 0,
    }
}
