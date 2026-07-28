namespace BotArena.Engine.Tests;

public sealed class FrontlineLabsQualificationDefinitionTests
{
    [Fact]
    public void EntryProbe_IsACompleteOpeningOnlyOneBendContract()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsQualificationDefinition.CreateEntryProbe();

        Assert.Equal(
            "frontline-qualification-1-entry-initiative",
            definition.Rules.RulesetId);
        Assert.Equal(120, definition.Rules.Limits.MaxTicks);
        Assert.Equal(
            "frontline-qualification-1-entry-initiative-map",
            definition.Map.Id);
        Assert.Equal(
            [
                new Position(8, 7),
                new Position(14, 7),
            ],
            definition.InitialDeployment.Spawns
                .OrderBy(spawn => spawn.Position.X)
                .Select(spawn => spawn.Position)
                .ToArray());
        Assert.Equal(2, definition.Topology.UnitSlots.Length);
        Assert.All(
            definition.Topology.UnitSlots,
            slot => Assert.Equal(0, slot.UnitId));
        Assert.All(
            definition.LifecycleAssignments,
            assignment =>
            {
                Assert.Equal(0, assignment.UnitId);
                Assert.Null(assignment.UnlockTick);
            });

        ActorAttackProfileDefinition mobile =
            definition.Rules.AttackProfiles.Single(profile =>
                profile.Id == "mobile-bolt");
        Assert.Equal(8, mobile.Projectile.MaxTravelTiles);
        Assert.Equal(2, mobile.Projectile.TilesPerAdvance);
        Assert.Equal(2, mobile.CooldownTicks);
        Assert.Equal(0, mobile.ShotProgram.MinInitialAimSteps);
        Assert.Equal(0, mobile.ShotProgram.MaxInitialAimSteps);
        Assert.Equal(1, mobile.ShotProgram.MaxBendCount);
        Assert.Equal(4, mobile.ShotProgram.MaxBendAfterTiles);
    }

    [Fact]
    public void EntryProbe_HasStableDistinctFingerprints()
    {
        ActorResolvedMatchDefinition first =
            FrontlineLabsQualificationDefinition.CreateEntryProbe();
        ActorResolvedMatchDefinition second =
            FrontlineLabsQualificationDefinition.CreateEntryProbe();
        ActorResolvedMatchDefinition experiment =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment();

        Assert.Equal(
            ActorContractFingerprint.ComputeRules(first.Rules),
            ActorContractFingerprint.ComputeRules(second.Rules));
        Assert.Equal(
            ActorContractFingerprint.ComputeMap(first.Map),
            ActorContractFingerprint.ComputeMap(second.Map));
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(first),
            ActorContractFingerprint.ComputeMatch(second));
        Assert.NotEqual(
            ActorContractFingerprint.ComputeMatch(experiment),
            ActorContractFingerprint.ComputeMatch(first));
    }

    [Fact]
    public void FoundationContractProbe_ExercisesDeclaredAutomaticActivation()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsQualificationDefinition
                .CreateContractAutoDeterminismProbe();
        var mode = Assert.IsType<FrontlineGameModeDefinition>(
            definition.Rules.GameMode);

        Assert.Equal(
            "frontline-qualification-2-contract-auto-determinism",
            definition.Rules.RulesetId);
        Assert.Equal(130, definition.Rules.Limits.MaxTicks);
        Assert.Equal(1000, mode.Capture.Threshold);
        Assert.Equal(4, definition.Topology.UnitSlots.Length);
        Assert.Equal(
            2,
            definition.LifecycleAssignments.Count(assignment =>
                assignment.InitialAvailability
                    == ActorUnitSlotLifecycleAssignmentDefinition
                        .InitialAvailabilityKind
                        .DormantAutomaticActivationAtTick
                && assignment.UnlockTick == 120));
        Assert.All(
            definition.Rules.Forms,
            form => Assert.DoesNotContain(
                form.AllowedActionIds,
                actionId => actionId is "fabricate" or "split"));
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(definition),
            ActorContractFingerprint.ComputeMatch(
                FrontlineLabsQualificationDefinition
                    .CreateContractAutoDeterminismProbe()));
    }

    [Fact]
    public void FundamentalsContractMatrix_UsesNonDefaultIdsAndBothChildren()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsQualificationDefinition
                .CreateContractMatrixProbe();
        var mode = Assert.IsType<FrontlineGameModeDefinition>(
            definition.Rules.GameMode);

        Assert.Equal(
            "frontline-qualification-3-contract-matrix",
            definition.Rules.RulesetId);
        Assert.Equal(24, definition.Rules.Limits.MaxTicks);
        Assert.Equal(1000, mode.Capture.Threshold);
        Assert.Equal(
            [7, 19],
            definition.Topology.Participants
                .Select(participant => participant.ParticipantId)
                .ToArray());
        Assert.Equal(6, definition.Topology.UnitSlots.Length);
        Assert.Equal(
            [4, 4, 8, 8],
            definition.LifecycleAssignments
                .Where(assignment => assignment.UnitId != 0)
                .OrderBy(assignment => assignment.UnlockTick)
                .Select(assignment => assignment.UnlockTick)
                .ToArray());
        Assert.Equal(
            [7, 19],
            definition.Topology.UnitSlots
                .Select(slot => slot.ControllerParticipantId)
                .Distinct()
                .Order()
                .ToArray());
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(definition),
            ActorContractFingerprint.ComputeMatch(
                FrontlineLabsQualificationDefinition
                    .CreateContractMatrixProbe()));
    }

    [Fact]
    public void FundamentalsPrimeOnlyProbes_AreShortOrdinaryContracts()
    {
        ActorResolvedMatchDefinition objective0 =
            FrontlineLabsQualificationDefinition
                .CreateObjectivePathProbe(0);
        ActorResolvedMatchDefinition objective1 =
            FrontlineLabsQualificationDefinition
                .CreateObjectivePathProbe(1);
        ActorResolvedMatchDefinition direct =
            FrontlineLabsQualificationDefinition
                .CreateDirectFireProbe();
        ActorResolvedMatchDefinition evade =
            FrontlineLabsQualificationDefinition
                .CreateStraightEvadeProbe();

        Assert.Equal(24, objective0.Rules.Limits.MaxTicks);
        Assert.Equal(20, direct.Rules.Limits.MaxTicks);
        Assert.Equal(12, evade.Rules.Limits.MaxTicks);
        foreach (ActorResolvedMatchDefinition definition
                 in new[] { objective0, objective1, direct, evade })
        {
            var mode = Assert.IsType<FrontlineGameModeDefinition>(
                definition.Rules.GameMode);
            Assert.Equal(1000, mode.Capture.Threshold);
            Assert.Equal(2, definition.Topology.UnitSlots.Length);
            Assert.All(
                definition.Topology.UnitSlots,
                slot => Assert.Equal(0, slot.UnitId));
            Assert.DoesNotContain(
                definition.Rules.Actions,
                action => action.Id is "fabricate" or "split");
            Assert.Equal(
                ActorContractFingerprint.ComputeMatch(definition),
                ActorContractFingerprint.ComputeMatch(
                    definition.Rules.RulesetId.EndsWith(
                        FrontlineLabsQualificationDefinition
                            .ObjectivePathProbeId,
                        StringComparison.Ordinal)
                        ? FrontlineLabsQualificationDefinition
                            .CreateObjectivePathProbe(
                                definition == objective0 ? 0 : 1)
                        : definition.Rules.RulesetId.EndsWith(
                            FrontlineLabsQualificationDefinition
                                .DirectFireProbeId,
                            StringComparison.Ordinal)
                            ? FrontlineLabsQualificationDefinition
                                .CreateDirectFireProbe()
                            : FrontlineLabsQualificationDefinition
                                .CreateStraightEvadeProbe()));
        }
        Assert.NotEqual(
            ActorContractFingerprint.ComputeMap(objective0.Map),
            ActorContractFingerprint.ComputeMap(objective1.Map));
    }

    [Fact]
    public void ManualFabricationProbe_MakesOneChildReadyAtTickZero()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsQualificationDefinition
                .CreateManualFabricationProbe();
        var mode = Assert.IsType<FrontlineGameModeDefinition>(
            definition.Rules.GameMode);

        Assert.Equal(20, definition.Rules.Limits.MaxTicks);
        Assert.Equal(1000, mode.Capture.Threshold);
        Assert.Contains(
            definition.Rules.Actions,
            action => action.Id == "fabricate");
        Assert.Equal(4, definition.Topology.UnitSlots.Length);
        Assert.DoesNotContain(
            definition.Topology.UnitSlots,
            slot => slot.UnitId == 2);
        Assert.Equal(
            2,
            definition.LifecycleAssignments.Count(assignment =>
                assignment.UnitId == 1
                && assignment.UnlockTick == 0
                && assignment.InitialAvailability
                    == ActorUnitSlotLifecycleAssignmentDefinition
                        .InitialAvailabilityKind.DormantUnlockAtTick));
    }

    [Fact]
    public void TacticalGeometryProbes_AreMirroredStableOrdinaryContracts()
    {
        (ActorResolvedMatchDefinition Definition,
            ActorResolvedMatchDefinition Repeat)[] definitions =
        [
            (
                FrontlineLabsQualificationDefinition
                    .CreateWallTerminatedBendProbe(0),
                FrontlineLabsQualificationDefinition
                    .CreateWallTerminatedBendProbe(0)
            ),
            (
                FrontlineLabsQualificationDefinition
                    .CreateWallTerminatedBendProbe(1),
                FrontlineLabsQualificationDefinition
                    .CreateWallTerminatedBendProbe(1)
            ),
            (
                FrontlineLabsQualificationDefinition
                    .CreateStrictCornerProbe(0),
                FrontlineLabsQualificationDefinition
                    .CreateStrictCornerProbe(0)
            ),
            (
                FrontlineLabsQualificationDefinition
                    .CreateStrictCornerProbe(1),
                FrontlineLabsQualificationDefinition
                    .CreateStrictCornerProbe(1)
            ),
            (
                FrontlineLabsQualificationDefinition
                    .CreateCooldownWindowProbe(0),
                FrontlineLabsQualificationDefinition
                    .CreateCooldownWindowProbe(0)
            ),
            (
                FrontlineLabsQualificationDefinition
                    .CreateCooldownWindowProbe(1),
                FrontlineLabsQualificationDefinition
                    .CreateCooldownWindowProbe(1)
            ),
            (
                FrontlineLabsQualificationDefinition
                    .CreateLocalFormSafetyProbe(0),
                FrontlineLabsQualificationDefinition
                    .CreateLocalFormSafetyProbe(0)
            ),
            (
                FrontlineLabsQualificationDefinition
                    .CreateLocalFormSafetyProbe(1),
                FrontlineLabsQualificationDefinition
                    .CreateLocalFormSafetyProbe(1)
            ),
        ];

        foreach ((ActorResolvedMatchDefinition definition,
                     ActorResolvedMatchDefinition repeat) in definitions)
        {
            var mode = Assert.IsType<FrontlineGameModeDefinition>(
                definition.Rules.GameMode);
            Assert.StartsWith(
                "frontline-qualification-4-",
                definition.Rules.RulesetId,
                StringComparison.Ordinal);
            Assert.Equal(1000, mode.Capture.Threshold);
            Assert.Equal(2, definition.Topology.UnitSlots.Length);
            Assert.Equal(
                ActorContractFingerprint.ComputeMatch(definition),
                ActorContractFingerprint.ComputeMatch(repeat));
        }
    }

    [Fact]
    public void TacticalStrictCorner_ExposesVisibleButInvalidCurvedIntercept()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsQualificationDefinition
                .CreateStrictCornerProbe(0);
        InitialSpawnDefinition tested = definition.InitialDeployment.Spawns
            .Single(spawn => spawn.SpawnId.EndsWith(
                "team-0",
                StringComparison.Ordinal));
        InitialSpawnDefinition controller =
            definition.InitialDeployment.Spawns.Single(spawn =>
                spawn.SpawnId.EndsWith(
                    "team-1",
                    StringComparison.Ordinal));

        Assert.Equal(new Position(10, 7), tested.Position);
        Assert.Equal(Direction.North, tested.Facing);
        Assert.Equal(new Position(9, 3), controller.Position);
        Assert.True(definition.Map.IsWall(new Position(10, 3)));
        Assert.Contains(
            definition.Map.Regions.Single(region =>
                region.RegionId == "frontline-position-2").Tiles,
            tile => tile == tested.Position);
    }

    [Fact]
    public void TacticalCadencePair_DiffersOnlyByDeclaredOddEvenRange()
    {
        ActorResolvedMatchDefinition range3 =
            FrontlineLabsQualificationDefinition
                .CreateCadenceParityProbe(0, 3);
        ActorResolvedMatchDefinition range4 =
            FrontlineLabsQualificationDefinition
                .CreateCadenceParityProbe(0, 4);

        ActorAttackProfileDefinition attack3 =
            range3.Rules.AttackProfiles.Single(profile =>
                profile.Id == "mobile-bolt");
        ActorAttackProfileDefinition attack4 =
            range4.Rules.AttackProfiles.Single(profile =>
                profile.Id == "mobile-bolt");
        Assert.Equal(3, attack3.Projectile.MaxTravelTiles);
        Assert.Equal(4, attack4.Projectile.MaxTravelTiles);
        Assert.Equal(
            ActorContractFingerprint.ComputeMap(range3.Map),
            ActorContractFingerprint.ComputeMap(range4.Map));
        Assert.Equal(
            ActorContractFingerprint.ComputeTopology(range3.Topology),
            ActorContractFingerprint.ComputeTopology(range4.Topology));
        Assert.NotEqual(
            ActorContractFingerprint.ComputeRules(range3.Rules),
            ActorContractFingerprint.ComputeRules(range4.Rules));
    }

    [Fact]
    public void TacticalLocalFormSafety_StartsAChildWithTurretAvailable()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsQualificationDefinition
                .CreateLocalFormSafetyProbe(1);
        PublicInitialLife tested = definition.Topology.InitialLives
            .Single(life => life.TeamId == 1);
        ActorFormDefinition child = definition.Rules.Forms.Single(form =>
            form.Id == "child-mobile");
        ActorFormDefinition turret = definition.Rules.Forms.Single(form =>
            form.Id == "turret");

        Assert.Equal("child-mobile", tested.FormId);
        Assert.Equal(1, child.ObjectiveWeight);
        Assert.Equal(0, turret.ObjectiveWeight);
        Assert.Contains("transform", child.AllowedActionIds);
        Assert.Contains(
            definition.Rules.SameLifeTransitions,
            transition =>
                transition.SourceFormId == "child-mobile"
                && transition.TargetFormId == "turret");
    }

}
