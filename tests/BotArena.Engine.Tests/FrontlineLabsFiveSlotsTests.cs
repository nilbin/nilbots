using BotArena.ActorContracts;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins FABRICATOR FIVE SLOTS: the asymmetric slot topology and its new
/// profile ID and fingerprint (the owner-approved amendment of DECISIONS
/// #153's same-topology reading), the graded unlock and rebuild schedule that
/// buys COUNT without buying TEMPO, and the five bodies actually reaching the
/// field on that schedule.
/// </summary>
public sealed class FrontlineLabsFiveSlotsTests
{
    private static ActorResolvedMatchDefinition Arm(
        FrontlineLabsClassDefinition? opponent = null) =>
        FrontlineLabsSkillArmTestFixture.Arm(
            FrontlineLabsClassDefinition.Fabricator,
            opponent ?? FrontlineLabsClassDefinition.Fabricator,
            FrontlineLabsSkillKit.FabricatorFiveSlots);

    [Fact]
    public void OnlyTheFabricatorSideGrowsAndTheOpponentKeepsThree()
    {
        ActorResolvedMatchDefinition arm = Arm(
            FrontlineLabsClassDefinition.Striker);

        Assert.Equal(
            5,
            arm.Topology.UnitSlots.Count(slot => slot.TeamId == 0));
        Assert.Equal(
            3,
            arm.Topology.UnitSlots.Count(slot => slot.TeamId == 1));
        Assert.Equal(
            [0, 1, 2, 3, 4],
            arm.Topology.UnitSlots
                .Where(slot => slot.TeamId == 0)
                .Select(slot => slot.UnitId)
                .Order());
        // Slot counts are contract data, and the label follows the contract.
        Assert.Equal(
            FrontlineLabsDefinition.AsymmetricSlotsTopologyProfileId,
            FrontlineLabsDefinition.TopologyProfileIdFor(arm.Topology));
        Assert.Equal(
            FrontlineLabsDefinition.TopologyProfileId,
            FrontlineLabsDefinition.TopologyProfileIdFor(
                FrontlineLabsDefinition.Create().Topology));
    }

    [Fact]
    public void AFabricatorMirrorGrowsBothSidesSymmetrically()
    {
        ActorResolvedMatchDefinition arm = Arm();

        Assert.Equal(10, arm.Topology.UnitSlots.Length);
        Assert.Equal(
            FrontlineLabsDefinition.AsymmetricSlotsTopologyProfileId,
            FrontlineLabsDefinition.TopologyProfileIdFor(arm.Topology));
    }

    [Fact]
    public void TheExtraSlotsUnlockLaterAndRebuildSlower()
    {
        ActorResolvedMatchDefinition arm = Arm();
        FrontlineLabsClassDefinition fabricator =
            FrontlineLabsClassDefinition.Fabricator;

        // The declared schedule continues the class's own 120-tick cadence.
        Assert.Equal(
            [60, 180, 300, 420],
            arm.LifecycleAssignments
                .Where(assignment =>
                    assignment.TeamId == 0 && assignment.UnitId > 0)
                .OrderBy(assignment => assignment.UnitId)
                .Select(assignment => assignment.UnlockTick!.Value));
        // Extra slots unlock strictly after the originals, and every one of
        // them stays inside a 500-tick match.
        Assert.True(
            arm.LifecycleAssignments
                .Where(assignment =>
                    assignment.TeamId == 0 && assignment.UnitId >= 3)
                .All(assignment =>
                    assignment.UnlockTick
                    > fabricator.SecondChildUnlockTick
                    && assignment.UnlockTick < arm.Rules.Limits.MaxTicks));

        // COUNT without TEMPO: the extra slots rebuild on the slower profile.
        Assert.Equal(
            15,
            Profile(arm, fabricator.ChildLifecycleProfileId).DelayTicks);
        Assert.Equal(
            30,
            Profile(arm, fabricator.ExtraChildLifecycleProfileId).DelayTicks);
        foreach (ActorUnitSlotLifecycleAssignmentDefinition assignment in
                 arm.LifecycleAssignments.Where(item =>
                     item.TeamId == 0 && item.UnitId >= 3))
        {
            Assert.Equal(
                fabricator.ExtraChildLifecycleProfileId,
                assignment.LifecycleProfileId);
            // The Fabricator's verb is unchanged: extra bodies are still
            // explicitly fabricated, never free.
            Assert.Equal(
                ActorLifecycleProfileDefinition.DestructionPolicyKind
                    .ReadyForExplicitFabrication,
                Profile(arm, assignment.LifecycleProfileId)
                    .DestructionPolicy);
            Assert.Null(assignment.AssignedRespawnSpawnId);
        }
    }

    [Fact]
    public void EveryUnlockBecomesReadyAndTheFifthBodyReachesTheField()
    {
        ActorResolvedMatchDefinition arm = Arm();
        GenericActorMatchChronology chronology =
            FrontlineLabsSkillArmTestFixture.Run(
                arm,
                (_, observation) =>
                {
                    if (observation.Self.ActorId.UnitId != 0)
                        return GenericDeathmatchSessionTestFixture.Wait();
                    GenericActorRuntimeObservation.ObservedUnitSlot? ready =
                        observation.TeamUnits
                            .Where(slot => slot.State
                                is GenericActorRuntimeObservation.UnitSlotState
                                    .Ready)
                            .OrderBy(slot => slot.UnitId)
                            .FirstOrDefault();
                    return ready is not null
                        && FrontlineLabsSkillArmTestFixture.Allows(
                            observation,
                            "fabricate")
                        ? GenericDeathmatchSessionTestFixture.Fabricate(
                            ready.TeamId,
                            ready.UnitId)
                        : GenericDeathmatchSessionTestFixture.Wait();
                });

        int[] spawnTicks = [.. Enumerable.Range(1, 4).Select(unitId =>
            SpawnTick(chronology, teamId: 0, unitId))];

        // All four children reach the field, in slot order, on the graded
        // schedule and inside the match.
        Assert.Equal(spawnTicks.Order(), spawnTicks);
        Assert.True(
            spawnTicks[0] >= 60 && spawnTicks[1] >= 180,
            $"early children spawned too soon: {string.Join(", ", spawnTicks)}");
        Assert.True(
            spawnTicks[2] >= 300 && spawnTicks[3] >= 420,
            $"late children spawned too soon: {string.Join(", ", spawnTicks)}");
        Assert.True(
            spawnTicks[3] < arm.Rules.Limits.MaxTicks,
            "the fifth body never reached the field");
        // Five distinct bodies stood on the field at once for team 0.
        Assert.Equal(
            5,
            chronology.Ticks[spawnTicks[3]].PostState.ActiveLives
                .Count(life => life.ActorId.TeamId == 0));
    }

    [Fact]
    public void TheArmMintsANewTopologyFingerprintAndIdentity()
    {
        ActorResolvedMatchDefinition arm = Arm(
            FrontlineLabsClassDefinition.Striker);
        ActorResolvedMatchDefinition baseline =
            FrontlineLabsDefinition.CreateClassesExperiment(
                FrontlineLabsClassDefinition.Fabricator,
                FrontlineLabsClassDefinition.Striker);

        Assert.Equal(
            "frontline-labs-1-fabricator-vs-striker-slot5",
            arm.Rules.RulesetId);
        Assert.True(arm.Rules.RulesetId.Length <= 64);
        Assert.NotEqual(
            ActorContractFingerprint.ComputeTopology(baseline.Topology),
            ActorContractFingerprint.ComputeTopology(arm.Topology));
        Assert.NotEqual(
            ActorContractFingerprint.ComputeMatch(baseline),
            ActorContractFingerprint.ComputeMatch(arm));
        // The map is held constant so the factor is the slot count alone.
        Assert.Equal(
            ActorContractFingerprint.ComputeMap(baseline.Map),
            ActorContractFingerprint.ComputeMap(arm.Map));

        GenericActorCanonicalContractValidation validation =
            GenericActorCanonicalContractValidator.Validate(
                ActorContractManifestSerializer.ToCanonicalJson(arm));
        Assert.Equal(arm.Rules.RulesetId, validation.RulesetId);
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(arm),
            validation.MatchContractFingerprint);
    }

    private static int SpawnTick(
        GenericActorMatchChronology chronology,
        int teamId,
        int unitId) =>
        chronology.Ticks
            .SelectMany(frame => frame.TickStart.Events
                .Concat(frame.Events)
                .Select(item => (frame.Tick, item)))
            .Where(entry => entry.item.Kind
                == GenericActorRuntimeObservation.EventKind.LifeSpawned)
            .Where(entry =>
                ((GenericActorRuntimeObservation.EventPayload.LifeSpawned)
                    entry.item.Payload).ActorId is
                    { } actor
                && actor.TeamId == teamId
                && actor.UnitId == unitId)
            .Select(entry => entry.Tick)
            .First();

    private static ActorLifecycleProfileDefinition Profile(
        ActorResolvedMatchDefinition definition,
        string profileId) =>
        definition.Rules.Lifecycle.Profiles.Single(
            profile => profile.ProfileId == profileId);
}
