namespace BotArena.Engine.Tests;

public class RulesFingerprintTests
{
    public static IEnumerable<object[]> PublicGameplayMutations()
    {
        yield return Case(nameof(GameRules.MaxTicks), rules => rules with { MaxTicks = 501 });
        yield return Case(nameof(GameRules.MaxHealth), rules => rules with { MaxHealth = 4 });
        yield return Case(nameof(GameRules.VisionRange), rules => rules with { VisionRange = 7 });
        yield return Case(nameof(GameRules.ShootCooldownTicks), rules => rules with { ShootCooldownTicks = 3 });
        yield return Case(nameof(GameRules.DamagePerHit), rules => rules with { DamagePerHit = 2 });
        yield return Case(nameof(GameRules.FaultLimit), rules => rules with { FaultLimit = 4 });
        yield return Case(nameof(GameRules.MaxEnergy), rules => rules with { MaxEnergy = 7 });
        yield return Case(nameof(GameRules.ShotEnergyCost), rules => rules with { ShotEnergyCost = 3 });
        yield return Case(nameof(GameRules.EnergyRegenTicks), rules => rules with { EnergyRegenTicks = 4 });
        yield return Case(nameof(GameRules.ShotRange), rules => rules with { ShotRange = 9 });
        yield return Case(nameof(GameRules.AllowStrafe), rules => rules with { AllowStrafe = false });
        yield return Case(nameof(GameRules.ZoneControl), rules => rules with { ZoneControl = false });
        yield return Case(nameof(GameRules.ZoneDominationTicks), rules => rules with { ZoneDominationTicks = 151 });
        yield return Case(nameof(GameRules.ZoneExclusiveAccrual), rules => rules with { ZoneExclusiveAccrual = false });
        yield return Case(nameof(GameRules.ActiveZoneControl), rules => rules with { ActiveZoneControl = false });
        yield return Case(nameof(GameRules.ControlBySoleOccupancy), rules => rules with { ControlBySoleOccupancy = false });
        yield return Case(nameof(GameRules.ControlPressureLimit), rules => rules with { ControlPressureLimit = 101 });
        yield return Case(nameof(GameRules.ControlPressureGain), rules => rules with { ControlPressureGain = 2 });
        yield return Case(nameof(GameRules.ControlPressureDecayInterval), rules => rules with { ControlPressureDecayInterval = 3 });
        yield return Case(nameof(GameRules.ControlOvertimeStartTick), rules => rules with { ControlOvertimeStartTick = 201 });
        yield return Case(nameof(GameRules.ControlOvertimePressureLimit), rules => rules with { ControlOvertimePressureLimit = 11 });
        yield return Case(nameof(GameRules.ControlOvertimePressureGain), rules => rules with { ControlOvertimePressureGain = 3 });
        yield return Case(nameof(GameRules.ControlOvertimeStopsDecay), rules => rules with { ControlOvertimeStopsDecay = false });
        yield return Case(nameof(GameRules.VisionCone), rules => rules with { VisionCone = false });
        yield return Case(nameof(GameRules.HearingRadius), rules => rules with { HearingRadius = 9 });
        yield return Case(nameof(GameRules.ProjectileTicksPerTile), rules => rules with { ProjectileTicksPerTile = 2 });
        yield return Case(nameof(GameRules.ProjectileTilesPerAdvance), rules => rules with { ProjectileTilesPerAdvance = 3 });
        yield return Case(nameof(GameRules.AllowProgrammedShots), rules => rules with { AllowProgrammedShots = false });
        yield return Case(nameof(GameRules.ProgrammedShotMaxInitialAimOctants), rules => rules with { ProgrammedShotMaxInitialAimOctants = 2 });
        yield return Case(nameof(GameRules.ProgrammedShotMaxBendAfterTiles), rules => rules with { ProgrammedShotMaxBendAfterTiles = 5 });
        yield return Case(nameof(GameRules.ProgrammedShotMaxBendEveryTiles), rules => rules with { ProgrammedShotMaxBendEveryTiles = 4 });
        yield return Case(nameof(GameRules.ProgrammedShotMaxBendCount), rules => rules with { ProgrammedShotMaxBendCount = 4 });
        yield return Case(nameof(GameRules.ProgrammedShotLaunchTiles), rules => rules with { ProgrammedShotLaunchTiles = 2 });
    }

    public static IEnumerable<object[]> EffectiveSeedMechanicsMutations()
    {
        yield return SeedCase(
            nameof(GameRules.RulesVersion),
            GameRules.V0_1,
            rules => rules with { RulesVersion = "different-fallback-namespace" });
        yield return SeedCase(
            nameof(GameRules.SeedProfile),
            GameRules.V0_1 with { SeedProfile = "seed-profile-a" },
            rules => rules with { SeedProfile = "seed-profile-b" });
        yield return SeedCase(
            nameof(GameRules.SeedSpawnVariation),
            GameRules.V0_1 with { SeedProfile = "fixed-seeds" },
            rules => rules with { SeedSpawnVariation = true });
        yield return SeedCase(
            nameof(GameRules.SpawnLaneSafety),
            GameRules.V0_3 with { SeedProfile = "fixed-seeds" },
            rules => rules with { SpawnLaneSafety = false });
        yield return SeedCase(
            nameof(GameRules.ZoneSpawnFairness),
            GameRules.V0_4 with { SeedProfile = "fixed-seeds" },
            rules => rules with { ZoneSpawnFairness = false });
        yield return SeedCase(
            nameof(GameRules.SpawnAttempts),
            GameRules.V0_3 with
            {
                SeedProfile = "fixed-seeds",
                ExhaustiveSpawns = false,
            },
            rules => rules with { SpawnAttempts = rules.SpawnAttempts + 1 });
        yield return SeedCase(
            nameof(GameRules.ExhaustiveSpawns),
            GameRules.V0_3 with
            {
                SeedProfile = "fixed-seeds",
                ExhaustiveSpawns = false,
            },
            rules => rules with { ExhaustiveSpawns = true });
    }

    [Theory]
    [MemberData(nameof(PublicGameplayMutations))]
    public void EveryPublicGameplayMutation_ChangesRulesFingerprint(
        string propertyName,
        Func<GameRules, GameRules> mutate)
    {
        GameRules baseline = AllCapabilitiesRules();

        string before = PublicRulesManifestFactory.CreateRules(baseline).RulesFingerprint;
        string after = PublicRulesManifestFactory.CreateRules(mutate(baseline)).RulesFingerprint;

        Assert.NotEqual(before, after);
        Assert.Equal(
            GameRuleDisclosure.PublicGameplay,
            GameRuleDisclosureCatalog.Properties[propertyName]);
    }

    [Fact]
    public void ProjectionCases_CoverEveryPublicGameplayProperty()
    {
        string[] expected = GameRuleDisclosureCatalog.Properties
            .Where(pair => pair.Value == GameRuleDisclosure.PublicGameplay)
            .Select(pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] actual = PublicGameplayMutations()
            .Select(row => Assert.IsType<string>(row[0]))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(EffectiveSeedMechanicsMutations))]
    public void EveryEffectiveSeedMechanicsMutation_ChangesRulesFingerprint(
        string propertyName,
        GameRules baseline,
        Func<GameRules, GameRules> mutate)
    {
        string before = PublicRulesManifestFactory.CreateRules(baseline).RulesFingerprint;
        string after = PublicRulesManifestFactory.CreateRules(mutate(baseline)).RulesFingerprint;

        Assert.NotEqual(before, after);
        Assert.Equal(
            GameRuleDisclosure.InternalSeedMechanics,
            GameRuleDisclosureCatalog.Properties[propertyName]);
    }

    [Fact]
    public void SeedCases_CoverEveryInternalSeedMechanicsProperty()
    {
        string[] expected = GameRuleDisclosureCatalog.Properties
            .Where(pair => pair.Value == GameRuleDisclosure.InternalSeedMechanics)
            .Select(pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] actual = EffectiveSeedMechanicsMutations()
            .Select(row => Assert.IsType<string>(row[0]))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RuntimeDebugAndReplayValues_DoNotChangeRulesFingerprint()
    {
        GameRules baseline = GameRules.Current;
        string expected = PublicRulesManifestFactory.CreateRules(baseline).RulesFingerprint;
        GameRules operationalVariant = baseline with
        {
            MaxDebugBytesPerTick = baseline.MaxDebugBytesPerTick + 1,
            MaxDebugBytesPerMatch = baseline.MaxDebugBytesPerMatch + 1,
            ReplayZoneTallies = !baseline.ReplayZoneTallies,
        };

        Assert.Equal(
            expected,
            PublicRulesManifestFactory.CreateRules(operationalVariant).RulesFingerprint);
    }

    [Fact]
    public void AliasWithExplicitSeedNamespace_DoesNotChangeRulesFingerprint()
    {
        GameRules baseline = GameRules.Current;
        Assert.NotNull(baseline.SeedProfile);

        GameRules alias = baseline with { RulesVersion = "same-content-alias" };

        Assert.Equal(
            PublicRulesManifestFactory.CreateRules(baseline).RulesFingerprint,
            PublicRulesManifestFactory.CreateRules(alias).RulesFingerprint);
    }

    [Fact]
    public void EffectiveSeedNamespace_ChangesRulesFingerprint()
    {
        GameRules baseline = GameRules.V0_1;
        Assert.Null(baseline.SeedProfile);

        GameRules newNamespace = baseline with { RulesVersion = "different-seed-namespace" };

        Assert.NotEqual(
            PublicRulesManifestFactory.CreateRules(baseline).RulesFingerprint,
            PublicRulesManifestFactory.CreateRules(newNamespace).RulesFingerprint);
    }

    private static object[] Case(string propertyName, Func<GameRules, GameRules> mutate) =>
        [propertyName, mutate];

    private static object[] SeedCase(
        string propertyName,
        GameRules baseline,
        Func<GameRules, GameRules> mutate) =>
        [propertyName, baseline, mutate];

    private static GameRules AllCapabilitiesRules() => GameRules.Current with
    {
        MaxEnergy = 6,
        ShotEnergyCost = 2,
        EnergyRegenTicks = 3,
        AllowStrafe = true,
        ZoneDominationTicks = 150,
        ZoneExclusiveAccrual = true,
        ControlOvertimeStopsDecay = true,
    };
}
