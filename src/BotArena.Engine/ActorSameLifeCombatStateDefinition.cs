namespace BotArena.Engine;

/// <summary>
/// Combat-resource continuity at completion of a same-life form transition.
/// The transition never grants a free shot or a full energy refill.
/// </summary>
public sealed record ActorSameLifeCombatStateDefinition
{
    public static ActorSameLifeCombatStateDefinition PreserveWithoutRefillV1
    {
        get;
    } = new(
        CooldownContinuityKind.PreserveRemainingTicks,
        EnergyContinuityKind
            .PreserveCurrentCappedToTargetMaximumMissingSourcePoolBecomesZero);

    public ActorSameLifeCombatStateDefinition(
        CooldownContinuityKind cooldownContinuity,
        EnergyContinuityKind energyContinuity)
    {
        if (!Enum.IsDefined(cooldownContinuity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(cooldownContinuity));
        }
        if (!Enum.IsDefined(energyContinuity))
            throw new ArgumentOutOfRangeException(nameof(energyContinuity));

        CooldownContinuity = cooldownContinuity;
        EnergyContinuity = energyContinuity;
    }

    public CooldownContinuityKind CooldownContinuity { get; }
    public EnergyContinuityKind EnergyContinuity { get; }

    public enum CooldownContinuityKind
    {
        /// <summary>
        /// Preserve the remaining cooldown after the normal transition-tick
        /// resource update, even when it exceeds the target profile cadence.
        /// A target form without an attack retains the value as inert state.
        /// </summary>
        PreserveRemainingTicks = 0,
    }

    public enum EnergyContinuityKind
    {
        /// <summary>
        /// Preserve current energy and clamp it to the target profile maximum.
        /// A source without a pool contributes zero; a target without a pool
        /// stores no active energy value.
        /// </summary>
        PreserveCurrentCappedToTargetMaximumMissingSourcePoolBecomesZero = 0,
    }
}
