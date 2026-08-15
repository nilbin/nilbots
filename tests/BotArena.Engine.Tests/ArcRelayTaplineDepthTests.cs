using System.Collections.Immutable;
using BotArena.Sdk;

namespace BotArena.Engine.Tests;

/// <summary>
/// Narrow mechanical pins for the six owner-approved Tapline depth mechanics
/// and the separately isolated durability question.
/// These are experiment-identity and boundary tests, not balance evidence.
/// </summary>
public sealed class ArcRelayTaplineDepthTests
{
    private static readonly Position NorthWell = new(11, 6);
    private static readonly Position CentreWell = new(15, 13);
    private static readonly Position SouthWell = new(19, 20);

    [Fact]
    public void Tapline3ControlIsFrozenAndEveryCandidateHasItsOwnIdentity()
    {
        const string frozenTapline3 =
            "0ed35d080ffc90fa796f0c3c95b4931e81d1bbbfc8be41ce71125a7bd3399415";
        ArcRelayLoopProfile[] candidates =
        [
            ArcRelayLoopProfile.TaplineDepthScore124,
            ArcRelayLoopProfile.TaplineDepthVeteranCommand,
            ArcRelayLoopProfile.TaplineDepthRespawnWave,
            ArcRelayLoopProfile.TaplineDepthRosterWildcard,
            ArcRelayLoopProfile.TaplineDepthDurableDeployments,
            ArcRelayLoopProfile.TaplineDepthCombatScale10,
            ArcRelayLoopProfile.TaplineDepthDurabilityPlus10,
            ArcRelayLoopProfile.TaplineDepthPack,
        ];

        string control = ActorContractFingerprint.ComputeRules(
            ArcRelayH0Definition.CreateRules(ArcRelayLoopProfile.Tapline3));
        Assert.Equal(frozenTapline3, control);
        Assert.Same(
            ArcRelayLoopProfile.Tapline3,
            ArcRelayLoopProfile.TaplineDepthControl);
        Assert.All(candidates, candidate =>
        {
            Assert.Same(candidate, ArcRelayLoopProfile.Resolve(candidate.Id));
            Assert.NotEqual(
                control,
                ActorContractFingerprint.ComputeRules(
                    ArcRelayH0Definition.CreateRules(candidate)));
        });
        Assert.Equal(
            candidates.Length,
            candidates.Select(candidate =>
                    ActorContractFingerprint.ComputeRules(
                        ArcRelayH0Definition.CreateRules(candidate)))
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Fact]
    public void Tapline4PromotesTheCombinedMechanicsUnderAProductIdentity()
    {
        ActorResolvedMatchDefinition candidate = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.TaplineDepthPack);
        ActorResolvedMatchDefinition product = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline4);

        Assert.Equal("arc-relay-tapline-04", product.Rules.RulesetId);
        Assert.Equal(candidate.Map.Id, product.Map.Id);
        Assert.True(candidate.Map.TileRows.SequenceEqual(product.Map.TileRows));

        Assert.Equal(
            profileShape(ArcRelayLoopProfile.TaplineDepthPack),
            profileShape(ArcRelayLoopProfile.Tapline4));

        static (int Respawn, int Cadence, int FirstBeat, int Births,
            bool RelativeCharge, bool VeteranCommand, int WavePeriod,
            int WaveMinimumDead, int DistinctClasses, bool Deployments,
            int CombatScale) profileShape(ArcRelayLoopProfile value)
        {
            return (
                value.RespawnDelayTicks,
                value.WellCadenceTicks,
                value.FirstGlobalBeatTicks,
                value.ScheduledBirthRounds,
                value.TeamRelativeCoreCharge,
                value.VeteranCommand,
                value.RespawnWavePeriodTicks,
                value.RespawnMinimumDeadTicks,
                value.MinimumDistinctClasses,
                value.DurableDeployments,
                value.CombatMagnitudeScale);
        }
    }

    [Fact]
    public void CombatScalePublishesEveryResolvedMagnitudeAndRoundTrips()
    {
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.TaplineDepthCombatScale10);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            definition.Rules.GameMode);

        Assert.Equal(5, mode.VeterancyDamagePerPoint);
        Assert.Equal(10, mode.VeterancyDamageBonusCap);
        Assert.True(mode.VeterancyBonusAfterRearArc);
        Assert.Equal(10, mode.VitalityHealthPerPoint);
        Assert.Equal(10, mode.HealZoneHealthPerActivation);
        Assert.All(
            definition.Rules.AttackProfiles.Where(profile =>
                profile.Id is "short-fast" or "medium-steady"
                    or "long-slow"),
            profile => Assert.Equal(10, profile.Projectile.DamagePerHit));
        Assert.Equal(
            10,
            definition.Rules.AttackProfiles.Single(profile =>
                profile.Id == "sentinel-bolt").Projectile.DamagePerHit);

        AssertHull(definition, 30,
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Lantern,
            ArcRelayLaunchClassIds.Mortar,
            ArcRelayLaunchClassIds.Switchback,
            ArcRelayLaunchClassIds.Longshot,
            ArcRelayLaunchClassIds.Veil);
        AssertHull(definition, 40,
            ArcRelayLaunchClassIds.Towline,
            ArcRelayLaunchClassIds.Patchbay,
            ArcRelayLaunchClassIds.Minesmith,
            ArcRelayLaunchClassIds.Hush,
            ArcRelayLaunchClassIds.Relay,
            ArcRelayLaunchClassIds.Sunder,
            ArcRelayLaunchClassIds.Nest);
        AssertHull(definition, 50,
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Mason,
            ArcRelayLaunchClassIds.Repulsor);

        Assert.Equal(10, mode.Signatures
            .OfType<ArcRelaySignatureDefinition.FallingStar>()
            .Single().Damage);
        ArcRelaySignatureDefinition.TripNode node = mode.Signatures
            .OfType<ArcRelaySignatureDefinition.TripNode>().Single();
        Assert.Equal((10, 20), (node.Hull, node.TriggerDamage));
        Assert.Equal(20, mode.Signatures
            .OfType<ArcRelaySignatureDefinition.RailLine>()
            .Single().Damage);
        ArcRelaySignatureDefinition.SentinelSeed2 sentinel = mode.Signatures
            .OfType<ArcRelaySignatureDefinition.SentinelSeed2>().Single();
        Assert.Equal((20, 10), (sentinel.Hull, sentinel.Damage));
        ArcRelaySignatureDefinition.RepairBeam repair = mode.Signatures
            .OfType<ArcRelaySignatureDefinition.RepairBeam>().Single();
        Assert.Equal(
            (10, 20),
            (repair.HullPerRepair, repair.MaxHullPerActivation));
        Assert.Equal(10, mode.Signatures
            .OfType<ArcRelaySignatureDefinition.TargetPaint2>()
            .Single().BonusDamage);
        Assert.Equal(30, mode.Signatures
            .OfType<ArcRelaySignatureDefinition.HardlightBlock>()
            .Single().Hull);
        Assert.Equal(3, mode.Signatures
            .OfType<ArcRelaySignatureDefinition.PrismWall>()
            .Single().ContactCapacity);

        string canonical = ActorContractManifestSerializer.ToCanonicalJson(
            definition);
        Assert.Contains("\"veterancyDamagePerPoint\":5", canonical);
        Assert.Contains("\"veterancyDamageBonusCap\":10", canonical);
        Assert.Contains("\"veterancyBonusAfterRearArc\":true", canonical);
        Assert.Contains("\"vitalityHealthPerPoint\":10", canonical);
        Assert.Contains("\"healZoneHealthPerActivation\":10", canonical);
        var parsed = Assert.IsType<GenericActorRulesContract.ArcRelayGameMode>(
            ActorCanonicalContractReader.Parse(canonical).Rules.GameMode);
        Assert.Equal(5, parsed.VeterancyDamagePerPoint);
        Assert.Equal(10, parsed.VeterancyDamageBonusCap);
        Assert.True(parsed.VeterancyBonusAfterRearArc);
        Assert.Equal(10, parsed.VitalityHealthPerPoint);
        Assert.Equal(10, parsed.HealZoneHealthPerActivation);

        string control = ActorContractManifestSerializer.ToCanonicalJson(
            ArcRelayH0Definition.Create(
                loopProfile: ArcRelayLoopProfile.Tapline3));
        Assert.DoesNotContain("veterancyDamagePerPoint", control);
        Assert.DoesNotContain("healZoneHealthPerActivation", control);
    }

    [Fact]
    public void DurabilityPlusAddsOneBaselineHitWithoutEnteringThePack()
    {
        ActorResolvedMatchDefinition scale = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.TaplineDepthCombatScale10);
        ActorResolvedMatchDefinition durable = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.TaplineDepthDurabilityPlus10);
        ActorResolvedMatchDefinition pack = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.TaplineDepthPack);

        AssertHull(durable, 40, ArcRelayLaunchClassIds.Kestrel);
        AssertHull(durable, 50, ArcRelayLaunchClassIds.Towline);
        AssertHull(durable, 60, ArcRelayLaunchClassIds.Mason);
        AssertHull(pack, 30, ArcRelayLaunchClassIds.Kestrel);
        AssertHull(pack, 40, ArcRelayLaunchClassIds.Towline);
        AssertHull(pack, 50, ArcRelayLaunchClassIds.Mason);

        foreach ((string classId, int baselineHits, int durableHits) in new[]
                 {
                     (ArcRelayLaunchClassIds.Kestrel, 3, 4),
                     (ArcRelayLaunchClassIds.Towline, 4, 5),
                     (ArcRelayLaunchClassIds.Mason, 5, 6),
                 })
        {
            Assert.Equal(baselineHits, HitsToKill(Hull(scale, classId), 10));
            Assert.Equal(durableHits, HitsToKill(Hull(durable, classId), 10));
        }
    }

    [Fact]
    public void CombinedDepthReplayRoundTripsTheScaledCombatContract()
    {
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.TaplineDepthPack);
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(definition);
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(definition, factories),
            matchSeed: 20_260_814UL);
        session.Run();

        ReplayV3 replay = ReplayV3Projection.Project(session.Chronology);
        string replayJson = ReplayV3Serializer.ToJson(replay);
        ReplayV3 reread = ReplayV3Serializer.ReadCanonicalComplete(replayJson);
        GenericActorResolvedMatchContract contract =
            ActorCanonicalContractReader.Parse(
                reread.Header.Contract.CanonicalJson);
        var mode = Assert.IsType<
            GenericActorRulesContract.ArcRelayGameMode>(
                contract.Rules.GameMode);

        Assert.Equal(
            "arc-relay-tapline-depth-pack-02",
            reread.Header.GameRulesVersion);
        Assert.Equal(
            ReplayV3Serializer.ComputeHash(replay),
            ReplayV3Serializer.ComputeHash(reread));
        Assert.Equal(5, mode.VeterancyDamagePerPoint);
        Assert.Equal(10, mode.VeterancyDamageBonusCap);
        Assert.True(mode.VeterancyBonusAfterRearArc);
        Assert.Equal(10, mode.VitalityHealthPerPoint);
        Assert.Equal(10, mode.HealZoneHealthPerActivation);
        Assert.Equal(
            [30, 40, 50],
            contract.Rules.Forms
                .Where(form => form.Id.StartsWith(
                    ArcRelayH0Definition.FormPrefix,
                    StringComparison.Ordinal))
                .Select(form => form.MaxHealth)
                .Distinct()
                .Order()
                .ToArray());
    }

    [Theory]
    [InlineData(0, false, 0, 10)]
    [InlineData(1, false, 0, 15)]
    [InlineData(2, false, 0, 20)]
    [InlineData(3, false, 0, 20)]
    [InlineData(4, false, 0, 20)]
    [InlineData(0, true, 0, 20)]
    [InlineData(1, true, 0, 25)]
    [InlineData(2, true, 0, 30)]
    [InlineData(4, true, 0, 30)]
    [InlineData(1, true, 10, 35)]
    public void CombatScaleOrdersRearThenCappedVeteranThenPaint(
        int damagePoints,
        bool rearArc,
        int paint,
        int expected)
    {
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.TaplineDepthCombatScale10).GameMode);

        Assert.Equal(
            expected,
            ArcRelayCombatMagnitude.ProjectileDamage(
                mode,
                baseDamage: 10,
                damagePoints,
                rearArc,
                targetPaintBonus: paint));
    }

    [Fact]
    public void RepeatedVitalityAddsCurrentLifeHeadroomWithoutRescue()
    {
        ArcRelayActorMatchModeDriver driver = Driver(
            ArcRelayLoopProfile.TaplineDepthCombatScale10);
        ActorIdentity veteran = ActorIdentity.FromTeamUnitLife(0, 0, 0);

        for (int ordinal = 0; ordinal < 8; ordinal++)
        {
            driver.HandleDestructions(10 + ordinal,
            [
                new FrontlineScrapDestruction(
                    ActorIdentity.FromTeamUnitLife(1, ordinal, 0),
                    new Position(10, 10),
                    veteran),
            ]);
            if (ordinal % 2 == 1)
                Assert.True(driver.TryInvest(30 + ordinal, veteran, "vitality"));
        }

        Assert.Equal(40, driver.StatModifiersFor(veteran).MaxHealthDelta);
        Assert.Equal(
            40,
            FrontlineScrapEconomyDefinition.HeadroomOn(
                driver.GameMode,
                FrontlineScrapEconomyDefinition.UpgradeEffectKind
                    .SpawnMaxHealthDelta));
        Assert.Equal(
            4,
            FrontlineScrapEconomyDefinition.HeadroomOn(
                driver.GameMode,
                FrontlineScrapEconomyDefinition.UpgradeEffectKind
                    .MobileAttackTravelTilesDelta));
        // The driver publishes only a maximum-health modifier. It owns no
        // reference to standing health, so investing cannot rescue that life.
        Assert.Equal(90, 50 + driver.StatModifiersFor(veteran).MaxHealthDelta);
    }

    [Fact]
    public void Score124PublishesExactValuesAndEnoughScheduledSupply()
    {
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.TaplineDepthScore124);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            definition.Rules.GameMode);

        Assert.Equal(1, mode.OwnWellCoreCharge);
        Assert.Equal(2, mode.CentreWellCoreCharge);
        Assert.Equal(4, mode.EnemyWellCoreCharge);
        Assert.Equal(
            [(25, 65, 545), (50, 65, 570), (50, 65, 570)],
            mode.Wells.Select(well => (
                well.FirstBirthTick,
                well.CadenceTicks,
                well.FinalBirthTick)));
        Assert.All(mode.Wells, well => Assert.Equal(
            9,
            (well.FinalBirthTick - well.FirstBirthTick)
                / well.CadenceTicks + 1));
        Assert.Equal(6, mode.WellBirthJitterTicks);

        string canonical = ActorContractManifestSerializer.ToCanonicalJson(
            definition);
        Assert.Contains(
            "\"teamRelativeCoreCharge\":{\"ownWell\":1,\"centreWell\":2,\"enemyWell\":4}",
            canonical,
            StringComparison.Ordinal);
        GenericActorResolvedMatchContract parsed =
            ActorCanonicalContractReader.Parse(canonical);
        var parsedMode = Assert.IsType<
            GenericActorRulesContract.ArcRelayGameMode>(
                parsed.Rules.GameMode);
        Assert.Equal(1, parsedMode.OwnWellCoreCharge);
        Assert.Equal(2, parsedMode.CentreWellCoreCharge);
        Assert.Equal(4, parsedMode.EnemyWellCoreCharge);
    }

    [Fact]
    public void Tapline5DelaysOnlyTheCentreOpeningScheduleToTheOuterBeat()
    {
        ActorResolvedMatchDefinition previous = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline4);
        ActorResolvedMatchDefinition candidate = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline5);
        var previousMode = Assert.IsType<ArcRelayGameModeDefinition>(
            previous.Rules.GameMode);
        var candidateMode = Assert.IsType<ArcRelayGameModeDefinition>(
            candidate.Rules.GameMode);

        Assert.Same(ArcRelayLoopProfile.Tapline5, ArcRelayLoopProfile.Current);
        Assert.Equal("arc-relay-tapline-05", candidate.Rules.RulesetId);
        Assert.Equal(previous.Map.Id, candidate.Map.Id);
        Assert.True(previous.Map.TileRows.SequenceEqual(candidate.Map.TileRows));
        Assert.Equal(
            [(25, 65, 545), (50, 65, 570), (50, 65, 570)],
            previousMode.Wells.Select(well => (
                well.FirstBirthTick,
                well.CadenceTicks,
                well.FinalBirthTick)));
        Assert.Equal(
            [(50, 65, 570), (50, 65, 570), (50, 65, 570)],
            candidateMode.Wells.Select(well => (
                well.FirstBirthTick,
                well.CadenceTicks,
                well.FinalBirthTick)));
        Assert.Equal(previousMode.WellBirthJitterTicks,
            candidateMode.WellBirthJitterTicks);
        Assert.Equal(previousMode.CentreWellCoreCharge,
            candidateMode.CentreWellCoreCharge);
        Assert.Equal(previousMode.Wells.Count(), candidateMode.Wells.Count());
    }

    [Theory]
    [InlineData(0, "north", 1)]
    [InlineData(0, "centre", 2)]
    [InlineData(0, "south", 4)]
    [InlineData(1, "north", 4)]
    [InlineData(1, "centre", 2)]
    [InlineData(1, "south", 1)]
    public void CoreBankValueRotatesWithTheScoringTeam(
        int teamId,
        string wellId,
        int expectedCharge)
    {
        ArcRelayEvent.CoreBanked bank = BankOne(teamId, wellId);
        Assert.Equal(expectedCharge, bank.ChargeAwarded);
    }

    [Fact]
    public void TwoPlusFourResolvesTwoOrderedPulsesAndNoFullRemainder()
    {
        var driver = Driver(ArcRelayLoopProfile.TaplineDepthScore124);
        ActorIdentity carrier = ActorIdentity.FromTeamUnitLife(0, 0, 0);

        int centrePickup = PickUp(driver, carrier, "centre");
        GenericActorModeTickResult centreBank = BankAtOwnReactor(
            driver, carrier, centrePickup + 1);
        ArcRelayEvent.CoreBanked first = Facts(centreBank.ModeEvents)
            .OfType<ArcRelayEvent.CoreBanked>().Single();
        Assert.Equal(2, first.ChargeAwarded);
        Assert.Equal(2, first.ChargePips);
        Assert.Empty(Facts(centreBank.ModeEvents).OfType<ArcRelayEvent.Pulse>());
        Assert.Equal(2, Reactor(driver, 0).ChargePips);

        int farPickup = PickUp(driver, carrier, "south", centrePickup + 2);
        GenericActorModeTickResult farBank = BankAtOwnReactor(
            driver, carrier, farPickup + 1);
        ArcRelayEvent[] facts = Facts(farBank.ModeEvents);
        ArcRelayEvent.CoreBanked second = facts
            .OfType<ArcRelayEvent.CoreBanked>().Single();
        ArcRelayEvent.Pulse[] pulses = facts
            .OfType<ArcRelayEvent.Pulse>().ToArray();

        Assert.Equal(4, second.ChargeAwarded);
        Assert.Collection(
            pulses,
            pulse =>
            {
                Assert.Equal(1, pulse.PulseOrdinal);
                Assert.Equal(2, pulse.OpposingReactorIntegrity);
            },
            pulse =>
            {
                Assert.Equal(2, pulse.PulseOrdinal);
                Assert.Equal(1, pulse.OpposingReactorIntegrity);
            });
        Assert.Equal(0, Reactor(driver, 0).ChargePips);
        Assert.Equal(1, Reactor(driver, 1).IntegritySegments);
    }

    [Fact]
    public void TerminalBankStillConsumesEveryCrossedThreshold()
    {
        var driver = Driver(ArcRelayLoopProfile.TaplineDepthScore124);
        ActorIdentity carrier = ActorIdentity.FromTeamUnitLife(0, 0, 0);

        // Two far banks produce 2 Pulses and 2 remainder. A third far bank
        // crosses two more thresholds; the reactor dies on the first, but the
        // scoring reactor must not retain an unresolved full threshold.
        for (int ordinal = 0; ordinal < 2; ordinal++)
        {
            int pickup = PickUp(
                driver,
                carrier,
                "south",
                ordinal == 0 ? 0 : 70);
            BankAtOwnReactor(driver, carrier, pickup + 1);
        }
        Assert.Equal(1, Reactor(driver, 1).IntegritySegments);
        Assert.Equal(2, Reactor(driver, 0).ChargePips);

        int thirdPickup = PickUp(driver, carrier, "south", 135);
        GenericActorModeTickResult terminal = BankAtOwnReactor(
            driver, carrier, thirdPickup + 1);
        ArcRelayEvent.Pulse[] pulses = Facts(terminal.ModeEvents)
            .OfType<ArcRelayEvent.Pulse>().ToArray();

        Assert.Equal(2, pulses.Length);
        Assert.Equal([3, 4], pulses.Select(pulse => pulse.PulseOrdinal));
        Assert.Equal([0, 0], pulses.Select(pulse =>
            pulse.OpposingReactorIntegrity));
        Assert.Equal(0, Reactor(driver, 0).ChargePips);
        Assert.Equal(0, Reactor(driver, 1).IntegritySegments);
    }

    [Theory]
    [InlineData(1, 2, true)]
    [InlineData(1, 3, false)]
    [InlineData(0, 3, true)]
    [InlineData(0, 4, true)]
    [InlineData(0, 2, false)]
    public void ReplayValidationAllowsPulseOverkillOnlyAfterDestruction(
        int opposingIntegrity,
        long scoredPulses,
        bool expected)
    {
        Assert.Equal(
            expected,
            ReplayV3Serializer.ArcRelayPulseFactsAgree(
                pulsesToDestroyReactor: 3,
                opposingIntegrity,
                scoredPulses));
    }

    [Theory]
    [InlineData(13, 30)]
    [InlineData(14, 30)]
    [InlineData(15, 60)]
    [InlineData(43, 60)]
    [InlineData(44, 60)]
    [InlineData(45, 90)]
    public void RespawnWaveEligibilityPinsTheOneTickCutoff(
        int destructionTick,
        int expectedDueTick)
    {
        Assert.Equal(
            expectedDueTick,
            ArcRelayRespawnWaveClock.DueTick(
                destructionTick,
                periodTicks: 30,
                minimumDeadTicks: 15));
    }

    [Fact]
    public void RespawnWaveHasThePromisedDowntimeDistributionAndSharedPhase()
    {
        int[] completeDeadTicks = Enumerable.Range(0, 30)
            .Select(destructionTick =>
                ArcRelayRespawnWaveClock.DueTick(
                    destructionTick, 30, 15)
                - destructionTick - 1)
            .ToArray();

        Assert.Equal(15, completeDeadTicks.Min());
        Assert.Equal(44, completeDeadTicks.Max());
        Assert.Equal(29.5, completeDeadTicks.Average());
        Assert.Equal(
            ArcRelayRespawnWaveClock.DueTick(22, 30, 15),
            ArcRelayRespawnWaveClock.DueTick(22, 30, 15));
    }

    [Fact]
    public void RespawnWavePublicStateAndContractRoundTrip()
    {
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.TaplineDepthRespawnWave);
        string contractJson = ActorContractManifestSerializer.ToCanonicalJson(
            definition);
        var contractMode = Assert.IsType<
            GenericActorRulesContract.ArcRelayGameMode>(
                ActorCanonicalContractReader.Parse(contractJson)
                    .Rules.GameMode);
        Assert.Equal(30, contractMode.RespawnWavePeriodTicks);
        Assert.Equal(15, contractMode.RespawnMinimumDeadTicks);

        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(definition);
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(definition, factories),
            matchSeed: 20_260_814UL);
        session.Run();

        ReplayV3 replay = ReplayV3Projection.Project(session.Chronology);
        string replayJson = ReplayV3Serializer.ToJson(replay);
        ReplayV3 reread = ReplayV3Serializer.ReadCanonicalComplete(replayJson);
        Assert.Equal(
            ReplayV3Serializer.ComputeHash(replay),
            ReplayV3Serializer.ComputeHash(reread));
        var initial = Assert.IsType<ReplayV3.ModeState.ArcRelay>(
            reread.Ticks[0].TickStart.State.Mode);
        Assert.Equal(30, initial.NextRespawnWaveTick);
        Assert.Equal(
            [(0, 8, 0), (1, 8, 0)],
            initial.RespawnWaveTeams.Select(team => (
                team.TeamId,
                team.LiveBodies,
                team.RespawnsNextWave)));

        var observed = Assert.IsType<
            GenericActorRuntimeObservation.ModeObservationState.ArcRelay>(
                factories[0].Observations[0].Mode);
        Assert.Equal(30, observed.NextRespawnWaveTick);
        Assert.Equal(
            [(0, 8, 0), (1, 8, 0)],
            observed.RespawnWaveTeams.Select(team => (
                team.TeamId,
                team.LiveBodies,
                team.RespawnsNextWave)));
    }

    [Fact]
    public void RosterWildcardAllowsExactlyOnePairAndLeavesTapline3Alone()
    {
        string[] eightDistinct =
        [
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Towline,
            ArcRelayLaunchClassIds.Patchbay,
            ArcRelayLaunchClassIds.Lantern,
            ArcRelayLaunchClassIds.Mortar,
            ArcRelayLaunchClassIds.Minesmith,
            ArcRelayLaunchClassIds.Hush,
        ];
        string[] onePair = [.. eightDistinct];
        onePair[7] = ArcRelayLaunchClassIds.Kestrel;
        string[] twoPairs = [.. onePair];
        twoPairs[6] = ArcRelayLaunchClassIds.Palisade;
        string[] triple = [.. onePair];
        triple[6] = ArcRelayLaunchClassIds.Kestrel;

        ArcRelayH0Definition.Create(
            eightDistinct,
            eightDistinct,
            loopProfile: ArcRelayLoopProfile.TaplineDepthRosterWildcard);
        ArcRelayH0Definition.Create(
            onePair,
            onePair,
            loopProfile: ArcRelayLoopProfile.TaplineDepthRosterWildcard);
        Assert.Throws<ActorResolvedMatchValidationException>(() =>
            ArcRelayH0Definition.Create(
                twoPairs,
                eightDistinct,
                loopProfile: ArcRelayLoopProfile.TaplineDepthRosterWildcard));
        Assert.Throws<ArgumentException>(() =>
            ArcRelayH0Definition.Create(
                triple,
                eightDistinct,
                loopProfile: ArcRelayLoopProfile.TaplineDepthRosterWildcard));

        // Historical max-two sheets remain valid on the unchanged profile.
        ArcRelayH0Definition.Create(
            twoPairs,
            twoPairs,
            loopProfile: ArcRelayLoopProfile.Tapline3);
    }

    [Fact]
    public void DurableBarrierHasThirtyTicksRealHullAndStableSlotReplacement()
    {
        string[] composition =
        [
            ArcRelayLaunchClassIds.Mason,
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Towline,
            ArcRelayLaunchClassIds.Patchbay,
            ArcRelayLaunchClassIds.Lantern,
            ArcRelayLaunchClassIds.Mortar,
            ArcRelayLaunchClassIds.Hush,
        ];
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            composition,
            composition,
            loopProfile: ArcRelayLoopProfile.TaplineDepthDurableDeployments);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            definition.Rules.GameMode);
        var runtime = new ArcRelaySignatureRuntime(definition, mode);
        ActorIdentity mason = ActorIdentity.FromTeamUnitLife(0, 0, 0);
        Position source = new(8, 8);
        Position firstTile = source.Offset(1, 0);
        ArcRelaySignatureRuntime.Life[] lives =
        [
            new(mason, source, 5, 5),
        ];

        runtime.Start(
            10,
            mason,
            source,
            "hardlight-block",
            [new GenericActorRuntimeActionArgument.PositionTargetArgument(
                firstTile)],
            lives);
        ArcRelaySignatureState barrier = Assert.Single(runtime.Project(10));
        Assert.Equal(40, barrier.EndsAtTick);
        Assert.Equal(3, barrier.RemainingCapacity);
        Assert.Equal(3, barrier.MaximumCapacity);

        Assert.True(runtime.TryConsumeProjectile(
            firstTile,
            projectileTeamId: 1,
            tick: 11,
            damage: 2,
            out GenericActorModeEvent? damaged));
        Assert.Equal("damaged-projectile", Changed(damaged!)?.Reason);
        Assert.Equal(1, Assert.Single(runtime.Project(11)).RemainingCapacity);

        ActorIdentity respawned = ActorIdentity.FromTeamUnitLife(0, 0, 1);
        Position secondTile = source.Offset(0, 1);
        ArcRelaySignatureRuntime.TickResult replaced = runtime.Start(
            24,
            respawned,
            source,
            "hardlight-block",
            [new GenericActorRuntimeActionArgument.PositionTargetArgument(
                secondTile)],
            [new ArcRelaySignatureRuntime.Life(respawned, source, 5, 5)]);
        Assert.Contains(replaced.Events, value =>
            Changed(value)?.Reason == "replaced");
        barrier = Assert.Single(runtime.Project(24));
        Assert.Equal(respawned, barrier.OwnerActorId);
        Assert.Equal(secondTile, Assert.Single(barrier.Positions));
    }

    [Fact]
    public void VeteranCommandPublishesExactVisibleLevelOnlyInItsCandidate()
    {
        ActorIdentity killer = ActorIdentity.FromTeamUnitLife(0, 0, 0);
        ActorIdentity victimOne = ActorIdentity.FromTeamUnitLife(1, 0, 0);
        ActorIdentity victimTwo = ActorIdentity.FromTeamUnitLife(1, 1, 0);

        foreach ((ArcRelayLoopProfile profile, bool publishes) in new[]
                 {
                     (ArcRelayLoopProfile.TaplineDepthVeteranCommand, true),
                     (ArcRelayLoopProfile.Tapline3, false),
                 })
        {
            ArcRelayActorMatchModeDriver driver = Driver(profile);
            driver.HandleDestructions(10,
            [
                new FrontlineScrapDestruction(
                    victimOne, new Position(10, 10), killer),
            ]);
            driver.HandleDestructions(12,
            [
                new FrontlineScrapDestruction(
                    victimTwo, new Position(10, 11), killer),
            ]);

            GenericActorModeProjection projection = driver.Project(
                World(
                    Life(killer, new Position(10, 9)),
                    Life(victimOne, new Position(10, 10))));
            Assert.Equal(
                publishes ? 2 : 1,
                projection.VeterancyLevelByActor
                    .GetValueOrDefault(killer, 1));
        }
    }

    private static ArcRelayEvent.CoreBanked BankOne(
        int teamId,
        string wellId)
    {
        var driver = Driver(ArcRelayLoopProfile.TaplineDepthScore124);
        ActorIdentity carrier = ActorIdentity.FromTeamUnitLife(teamId, 0, 0);
        int pickup = PickUp(driver, carrier, wellId);
        return Facts(BankAtOwnReactor(driver, carrier, pickup + 1).ModeEvents)
            .OfType<ArcRelayEvent.CoreBanked>()
            .Single();
    }

    private static void AssertHull(
        ActorResolvedMatchDefinition definition,
        int expected,
        params string[] classIds) =>
        Assert.All(classIds, classId => Assert.Equal(
            expected,
            Hull(definition, classId)));

    private static int Hull(
        ActorResolvedMatchDefinition definition,
        string classId) =>
        definition.Rules.Forms.Single(form =>
            form.Id == ArcRelayH0Definition.FormPrefix + classId)
            .MaxHealth;

    private static int HitsToKill(int health, int damage) =>
        (health + damage - 1) / damage;

    private static int PickUp(
        ArcRelayActorMatchModeDriver driver,
        ActorIdentity carrier,
        string wellId,
        int startTick = 0)
    {
        Position well = wellId switch
        {
            "north" => NorthWell,
            "centre" => CentreWell,
            "south" => SouthWell,
            _ => throw new ArgumentOutOfRangeException(nameof(wellId)),
        };
        for (int tick = startTick; tick < startTick + 100; tick++)
        {
            driver.PrepareTick(tick, World(Life(carrier, well)));
            if (State(driver).VisibleCores.Any(core =>
                    core.CoreId.SourceWellId == wellId
                    && core.CarrierActorId == carrier))
            {
                return tick;
            }
        }
        throw new Xunit.Sdk.XunitException(
            $"No {wellId} Core was picked up in the search window.");
    }

    private static GenericActorModeTickResult BankAtOwnReactor(
        ArcRelayActorMatchModeDriver driver,
        ActorIdentity carrier,
        int tick)
    {
        Position reactor = Reactor(driver, carrier.TeamId).Position;
        return driver.ApplyJointTick(
            World(Life(carrier, reactor)),
            new GenericActorModeTickInput(tick, [], []));
    }

    private static ArcRelayActorMatchModeDriver Driver(
        ArcRelayLoopProfile profile) =>
        new(
            ArcRelayH0Definition.Create(loopProfile: profile),
            matchSeed: 0);

    private static ArcRelayReactorState Reactor(
        ArcRelayActorMatchModeDriver driver,
        int teamId) =>
        State(driver).Reactors.Single(reactor => reactor.TeamId == teamId);

    private static ArcRelayEvent[] Facts(
        IEnumerable<GenericActorModeEvent> events) =>
        events.Select(value => Assert.IsType<
                GenericActorRuntimeObservation.EventPayload.ArcRelay>(
                value.Payload).Fact)
            .ToArray();

    private static ArcRelayEvent.SignatureChanged? Changed(
        GenericActorModeEvent value) => Assert.IsType<
                GenericActorRuntimeObservation.EventPayload.ArcRelay>(
                value.Payload)
            .Fact as ArcRelayEvent.SignatureChanged;

    private static GenericActorRuntimeObservation.ModeObservationState.ArcRelay
        State(ArcRelayActorMatchModeDriver driver) =>
        Assert.IsType<GenericActorModeState.ArcRelay>(driver.State).State;

    private static GenericActorModeActiveLife Life(
        ActorIdentity actor,
        Position position) =>
        new(actor, "arc-body-kestrel", position, health: 3);

    private static GenericActorModeWorldView World(
        params GenericActorModeActiveLife[] lives) =>
        new(
            new Dictionary<int, long>
            {
                [0] = lives.Where(value => value.ActorId.TeamId == 0)
                    .Sum(value => value.Health),
                [1] = lives.Where(value => value.ActorId.TeamId == 1)
                    .Sum(value => value.Health),
            },
            [0, 1],
            lives.ToImmutableArray());
}
