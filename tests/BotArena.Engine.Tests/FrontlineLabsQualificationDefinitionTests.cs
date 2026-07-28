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
}
