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
        string? automaticReturnFormId,
        string? rootFactorySeedFormId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        if (!Enum.IsDefined(destructionPolicy))
            throw new ArgumentOutOfRangeException(nameof(destructionPolicy));
        if (delayTicks < 0)
            throw new ArgumentOutOfRangeException(nameof(delayTicks));
        if (automaticReturnFormId is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(automaticReturnFormId);
        if (rootFactorySeedFormId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootFactorySeedFormId);
            if (destructionPolicy
                != DestructionPolicyKind.ReadyForExplicitFabrication)
            {
                throw new ArgumentException(
                    "A root factory only bootstraps a slot whose bodies are "
                    + "placed by explicit fabrication; every other policy "
                    + "already returns one by itself.",
                    nameof(rootFactorySeedFormId));
            }
        }

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
        RootFactorySeedFormId = rootFactorySeedFormId;
    }

    public string ProfileId { get; }
    public DestructionPolicyKind DestructionPolicy { get; }
    public int DelayTicks { get; }
    public string? AutomaticReturnFormId { get; }

    /// <summary>
    /// THE ROOT FACTORY (owner ruling, DECISIONS #194). A slot on this profile
    /// is normally placed only by an explicit fabrication, which needs a live
    /// body — so once a participant's LAST body dies, nothing can ever place
    /// one again. When this form ID is declared, the participant's HOME BASE
    /// acts as the root factory instead: a structure, not a body, seeds exactly
    /// ONE life of this form on the lowest-numbered slot that owns a home
    /// spawn, after this profile's ordinary <see cref="DelayTicks"/>, at no
    /// cost and with no action spent.
    /// <para>
    /// Null on every profile that declares no bootstrap — which is every
    /// profile shipped before this arm — so the canonical writer emits no
    /// bytes and every historical rules fingerprint holds. Null therefore also
    /// spells the registered ALTERNATIVE arm: total body loss is elimination.
    /// That arm is coherent and sharper and the owner parked it; this field
    /// exists so choosing it later is a null rather than a rework.
    /// </para>
    /// <para>
    /// It bootstraps only. Active fabrication still requires a live body, the
    /// seed carries no scrap cost and no upgrade of its own, and the clock is
    /// cancelled the moment the participant gains a body by any other route —
    /// so a team that is merely between bodies never gets a free one.
    /// </para>
    /// </summary>
    public string? RootFactorySeedFormId { get; }

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
