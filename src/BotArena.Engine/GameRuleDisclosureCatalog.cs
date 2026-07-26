using System.Collections.Frozen;

namespace BotArena.Engine;

/// <summary>
/// Explicit disclosure classification for every instance property on
/// <see cref="GameRules"/>. This is intentionally not derived with reflection:
/// adding a rule must make an explicit contract decision.
/// </summary>
public static class GameRuleDisclosureCatalog
{
    public static IReadOnlyDictionary<string, GameRuleDisclosure> Properties { get; } =
        new Dictionary<string, GameRuleDisclosure>(StringComparer.Ordinal)
        {
            [nameof(GameRules.RulesVersion)] = GameRuleDisclosure.InternalSeedMechanics,
            [nameof(GameRules.MaxTicks)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.MaxHealth)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.VisionRange)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.ShootCooldownTicks)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.DamagePerHit)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.FaultLimit)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.MaxDebugBytesPerTick)] = GameRuleDisclosure.RuntimeOnly,
            [nameof(GameRules.MaxDebugBytesPerMatch)] = GameRuleDisclosure.RuntimeOnly,
            [nameof(GameRules.SeedSpawnVariation)] = GameRuleDisclosure.InternalSeedMechanics,
            [nameof(GameRules.MaxEnergy)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.ShotEnergyCost)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.EnergyRegenTicks)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.ShotRange)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.AllowStrafe)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.ZoneControl)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.ZoneDominationTicks)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.ZoneExclusiveAccrual)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.ActiveZoneControl)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.ControlBySoleOccupancy)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.ControlPressureLimit)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.ControlPressureGain)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.ControlPressureDecayInterval)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.ControlOvertimeStartTick)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.ControlOvertimePressureLimit)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.ControlOvertimePressureGain)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.ControlOvertimeStopsDecay)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.SpawnLaneSafety)] = GameRuleDisclosure.InternalSeedMechanics,
            [nameof(GameRules.ZoneSpawnFairness)] = GameRuleDisclosure.InternalSeedMechanics,
            [nameof(GameRules.SpawnAttempts)] = GameRuleDisclosure.InternalSeedMechanics,
            [nameof(GameRules.ExhaustiveSpawns)] = GameRuleDisclosure.InternalSeedMechanics,
            [nameof(GameRules.ReplayZoneTallies)] = GameRuleDisclosure.ReplayOnly,
            [nameof(GameRules.VisionCone)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.HearingRadius)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.SeedProfile)] = GameRuleDisclosure.InternalSeedMechanics,
            [nameof(GameRules.ProjectileTicksPerTile)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.ProjectileTilesPerAdvance)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.AllowProgrammedShots)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.ProgrammedShotMaxInitialAimOctants)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.ProgrammedShotMaxBendAfterTiles)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.ProgrammedShotMaxBendEveryTiles)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.ProgrammedShotMaxBendCount)] = GameRuleDisclosure.PublicGameplay,
            [nameof(GameRules.ProgrammedShotLaunchTiles)] = GameRuleDisclosure.PublicGameplay,
        }.ToFrozenDictionary(StringComparer.Ordinal);
}
