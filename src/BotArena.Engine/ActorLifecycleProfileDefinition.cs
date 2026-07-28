namespace BotArena.Engine;

/// <summary>
/// One rules-owned destruction policy and delay. Automatic returns name their
/// target form; explicit-fabrication readiness and permanent dormancy do not
/// create a life when their delay completes.
/// </summary>
public sealed record ActorLifecycleProfileDefinition
{
    public ActorLifecycleProfileDefinition(
        string profileId,
        DestructionPolicyKind destructionPolicy,
        int delayTicks,
        string? automaticReturnFormId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        if (!Enum.IsDefined(destructionPolicy))
            throw new ArgumentOutOfRangeException(nameof(destructionPolicy));
        if (delayTicks < 0)
            throw new ArgumentOutOfRangeException(nameof(delayTicks));
        if (automaticReturnFormId is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(automaticReturnFormId);

        switch (destructionPolicy)
        {
            case DestructionPolicyKind.AutomaticRespawn
                when automaticReturnFormId is null:
                throw new ArgumentException(
                    "Automatic respawn must name its return form.",
                    nameof(automaticReturnFormId));
            case DestructionPolicyKind.ReadyForExplicitFabrication
                when automaticReturnFormId is not null:
                throw new ArgumentException(
                    "Explicit-fabrication readiness cannot name an automatic return form.",
                    nameof(automaticReturnFormId));
            case DestructionPolicyKind.PermanentlyDormant
                when automaticReturnFormId is not null:
                throw new ArgumentException(
                    "Permanent dormancy cannot name an automatic return form.",
                    nameof(automaticReturnFormId));
            case DestructionPolicyKind.PermanentlyDormant
                when delayTicks != 0:
                throw new ArgumentException(
                    "Permanent dormancy has no delayed transition.",
                    nameof(delayTicks));
        }

        ProfileId = profileId;
        DestructionPolicy = destructionPolicy;
        DelayTicks = delayTicks;
        AutomaticReturnFormId = automaticReturnFormId;
    }

    public string ProfileId { get; }
    public DestructionPolicyKind DestructionPolicy { get; }
    public int DelayTicks { get; }
    public string? AutomaticReturnFormId { get; }

    public enum DestructionPolicyKind
    {
        /// <summary>
        /// At the due tick, create a fresh life in the assigned respawn spawn
        /// using <see cref="AutomaticReturnFormId"/> while preserving the
        /// destroyed life's lineage generation.
        /// </summary>
        AutomaticRespawn = 0,

        /// <summary>
        /// At the due tick, make the stable slot eligible for an explicit
        /// Fabrication action. No life is created by the lifecycle clock.
        /// </summary>
        ReadyForExplicitFabrication = 1,

        /// <summary>The destroyed stable slot never becomes available.</summary>
        PermanentlyDormant = 2,
    }
}
