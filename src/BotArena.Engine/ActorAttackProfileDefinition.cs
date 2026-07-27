namespace BotArena.Engine;

/// <summary>
/// A named attack capability selected by forms. Projectile, cadence, energy,
/// and programmed-shot behavior are resolved here rather than inherited from
/// a global mutable rules object.
/// </summary>
public sealed record ActorAttackProfileDefinition
{
    public ActorAttackProfileDefinition(
        string id,
        bool omnidirectionalAim,
        ActorProjectileDefinition projectile,
        int cooldownTicks,
        int maxEnergy,
        int attackEnergyCost,
        int energyRegenerationIntervalTicks,
        int energyRegenerationAmount,
        ActorShotProgramDefinition shotProgram)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(projectile);
        ArgumentNullException.ThrowIfNull(shotProgram);
        if (cooldownTicks < 0)
            throw new ArgumentOutOfRangeException(nameof(cooldownTicks));
        if (maxEnergy < 0)
            throw new ArgumentOutOfRangeException(nameof(maxEnergy));
        if (attackEnergyCost < 0 || attackEnergyCost > maxEnergy)
            throw new ArgumentOutOfRangeException(nameof(attackEnergyCost));
        if (energyRegenerationIntervalTicks < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(energyRegenerationIntervalTicks));
        }
        if (energyRegenerationAmount < 0
            || energyRegenerationAmount > maxEnergy)
        {
            throw new ArgumentOutOfRangeException(
                nameof(energyRegenerationAmount));
        }
        if ((energyRegenerationIntervalTicks == 0)
            != (energyRegenerationAmount == 0))
        {
            throw new ArgumentException(
                "Energy regeneration cadence and amount must both be zero or " +
                "both be positive.");
        }
        if (maxEnergy == 0
            && (attackEnergyCost != 0
                || energyRegenerationIntervalTicks != 0
                || energyRegenerationAmount != 0))
        {
            throw new ArgumentException(
                "An attack without an energy pool cannot consume or regenerate energy.");
        }
        if (projectile.LaunchTiles != shotProgram.LaunchTiles)
        {
            throw new ArgumentException(
                "Projectile and programmed-shot launch distances must agree.",
                nameof(shotProgram));
        }
        if (projectile.DiagonalCornersMustBeClear
            != shotProgram.DiagonalCornersMustBeClear)
        {
            throw new ArgumentException(
                "Projectile and programmed-shot corner policies must agree.",
                nameof(shotProgram));
        }

        Id = id;
        OmnidirectionalAim = omnidirectionalAim;
        Projectile = projectile;
        CooldownTicks = cooldownTicks;
        MaxEnergy = maxEnergy;
        AttackEnergyCost = attackEnergyCost;
        EnergyRegenerationIntervalTicks = energyRegenerationIntervalTicks;
        EnergyRegenerationAmount = energyRegenerationAmount;
        ShotProgram = shotProgram;
    }

    public string Id { get; }
    public bool OmnidirectionalAim { get; }
    public ActorProjectileDefinition Projectile { get; }
    public int CooldownTicks { get; }
    public int MaxEnergy { get; }
    public int AttackEnergyCost { get; }
    public int EnergyRegenerationIntervalTicks { get; }
    public int EnergyRegenerationAmount { get; }
    public EnergyRegenerationClockKind EnergyRegenerationClock =>
        EnergyRegenerationClockKind
            .CompletedMatchTickModuloIntervalEqualsZero;
    public EnergyUpdateOrderKind EnergyUpdateOrder =>
        EnergyUpdateOrderKind
            .AttackCostThenCadenceRegenerationCappedToMaximum;
    public AttackAvailabilityKind AttackAvailability =>
        AttackAvailabilityKind
            .PreTickCooldownZeroAndEnergyAtLeastCost;
    public CooldownUpdateKind CooldownUpdate =>
        CooldownUpdateKind
            .SuccessfulAttackSetsConfiguredTicksOtherwiseSubtractOneFloorZero;
    public ActorShotProgramDefinition ShotProgram { get; }

    public enum EnergyRegenerationClockKind
    {
        /// <summary>
        /// At the end of executed tick T, regeneration is due exactly when
        /// (T + 1) modulo the configured interval is zero. The clock is global
        /// match time, not life age or time since spending.
        /// </summary>
        CompletedMatchTickModuloIntervalEqualsZero = 0,
    }

    public enum EnergyUpdateOrderKind
    {
        /// <summary>
        /// Pay a successful attack's cost first, then apply any regeneration
        /// due on that same tick, capped to the profile maximum.
        /// </summary>
        AttackCostThenCadenceRegenerationCappedToMaximum = 0,
    }

    public enum AttackAvailabilityKind
    {
        PreTickCooldownZeroAndEnergyAtLeastCost = 0,
    }

    public enum CooldownUpdateKind
    {
        /// <summary>
        /// At the end-of-tick resource phase, a successful attack assigns the
        /// configured cooldown without decrementing it that tick. Every other
        /// active tick subtracts one, floored at zero.
        /// </summary>
        SuccessfulAttackSetsConfiguredTicksOtherwiseSubtractOneFloorZero = 0,
    }
}
