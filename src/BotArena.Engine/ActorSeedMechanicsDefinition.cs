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
        /// Domain-separated mix of the match seed, declared seed profile, and
        /// team/unit/life identity.
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
