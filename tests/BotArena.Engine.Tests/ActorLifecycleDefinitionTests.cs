namespace BotArena.Engine.Tests;

public sealed class ActorLifecycleDefinitionTests
{
    [Fact]
    public void DeathmatchRespawnLivesInLifecycleProfilesAndSlotAssignments()
    {
        var automatic = new ActorLifecycleProfileDefinition(
            profileId: "deathmatch-respawn",
            ActorLifecycleProfileDefinition.DestructionPolicyKind
                .AutomaticRespawn,
            delayTicks: 3,
            automaticReturnFormId: "mobile");
        var ready = new ActorLifecycleProfileDefinition(
            profileId: "explicit-fabrication",
            ActorLifecycleProfileDefinition.DestructionPolicyKind
                .ReadyForExplicitFabrication,
            delayTicks: 5,
            automaticReturnFormId: null);
        ActorLifecycleProfileDefinition[] profiles = [ready, automatic];
        var lifecycle = new ActorLifecycleDefinition(profiles);
        profiles[0] = automatic;

        string[] allowedForms = ["turret", "mobile"];
        var activeSlot =
            new ActorUnitSlotLifecycleAssignmentDefinition(
                teamId: 0,
                unitId: 0,
                lifecycleProfileId: automatic.ProfileId,
                initialGeneration: 0,
                allowedForms,
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.ActiveAtTickZero,
                unlockTick: null,
                assignedRespawnSpawnId: "west-respawn");
        var dormantSlot =
            new ActorUnitSlotLifecycleAssignmentDefinition(
                teamId: 0,
                unitId: 1,
                lifecycleProfileId: ready.ProfileId,
                initialGeneration: null,
                allowedFormIds: ["mobile"],
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.DormantUnlockAtTick,
                unlockTick: 120,
                assignedRespawnSpawnId: null);
        allowedForms[0] = "changed";

        _ = new DeathmatchGameModeDefinition(
            new DeathmatchVictoryDefinition(
                killsToWin: 10,
                [
                    new ScoreRankingDefinition(
                        ScoreChannelDefinition.ChannelKind.Kills,
                        ScoreRankingDefinition.SortDirection.HigherWins),
                ]),
            [
                new ScoreChannelDefinition(
                    ScoreChannelDefinition.ChannelKind.Kills),
            ],
            DeathmatchScoringDefinition.RawHostileKillV1);

        Assert.Equal(
            ["deathmatch-respawn", "explicit-fabrication"],
            lifecycle.Profiles
                .Select(profile => profile.ProfileId)
                .ToArray());
        Assert.Equal(3, lifecycle.Profiles[0].DelayTicks);
        Assert.Equal(
            ActorLifecycleDefinition.DestructionClockKind
                .TickStartAtDestroyedTickPlusOnePlusProfileDelayCheckedArithmetic,
            lifecycle.DestructionClock);
        Assert.Equal(
            ActorLifecycleDefinition.GenerationSemanticsKind
                .AutomaticRespawnPreservesDestroyedGenerationFabricationAndReplicationUseSourcePlusOne,
            lifecycle.GenerationSemantics);
        Assert.Equal(
            ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                .AssignedSpawnPermanentlyReservedForSlotAgainstOtherActorsAndLifecycleClaims,
            lifecycle.AutomaticReturnPlacement);
        Assert.Equal(
            ActorLifecycleDefinition.NewLifeCombatStateKind
                .ZeroCooldownTargetMaximumEnergyNoPreviousAction,
            lifecycle.NewLifeCombatState);
        Assert.Equal(
            ActorLifecycleDefinition.NewLifeResourceClockKind
                .UsesMatchGlobalProfileCadenceStartingAfterCreationTickActions,
            lifecycle.NewLifeResourceClock);
        Assert.Equal(
            ActorLifecycleDefinition.OutputTileProjectileKind
                .DueCreationConsumesOccupantsByProjectileIdWithoutDamageBeforeSpawn,
            lifecycle.OutputTileProjectile);
        Assert.Equal(
            ["mobile", "turret"],
            activeSlot.AllowedFormIds.ToArray());
        Assert.Equal(0, activeSlot.InitialGeneration);
        Assert.Equal("west-respawn", activeSlot.AssignedRespawnSpawnId);
        Assert.Null(dormantSlot.InitialGeneration);
        Assert.Equal(120, dormantSlot.UnlockTick);
    }

    [Fact]
    public void LifecycleCatalogRejectsEmptyNullAndDuplicateProfiles()
    {
        ActorLifecycleProfileDefinition automatic = AutomaticProfile();

        Assert.Throws<ArgumentException>(() =>
            new ActorLifecycleDefinition([]));
        Assert.Throws<ArgumentException>(() =>
            new ActorLifecycleDefinition([automatic, automatic]));
        Assert.Throws<ArgumentException>(() =>
            new ActorLifecycleDefinition(
                [automatic, null!]));
    }

    [Fact]
    public void LifecycleProfilesRejectIncoherentPolicyShapes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorLifecycleProfileDefinition(
                "invalid",
                (ActorLifecycleProfileDefinition.DestructionPolicyKind)99,
                delayTicks: 0,
                automaticReturnFormId: null));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorLifecycleProfileDefinition(
                "invalid",
                ActorLifecycleProfileDefinition.DestructionPolicyKind
                    .AutomaticRespawn,
                delayTicks: -1,
                automaticReturnFormId: "mobile"));
        Assert.Throws<ArgumentException>(() =>
            new ActorLifecycleProfileDefinition(
                "invalid",
                ActorLifecycleProfileDefinition.DestructionPolicyKind
                    .AutomaticRespawn,
                delayTicks: 0,
                automaticReturnFormId: null));
        Assert.Throws<ArgumentException>(() =>
            new ActorLifecycleProfileDefinition(
                "invalid",
                ActorLifecycleProfileDefinition.DestructionPolicyKind
                    .ReadyForExplicitFabrication,
                delayTicks: 0,
                automaticReturnFormId: "mobile"));
        Assert.Throws<ArgumentException>(() =>
            new ActorLifecycleProfileDefinition(
                "invalid",
                ActorLifecycleProfileDefinition.DestructionPolicyKind
                    .PermanentlyDormant,
                delayTicks: 1,
                automaticReturnFormId: null));
        Assert.Throws<ArgumentException>(() =>
            new ActorLifecycleProfileDefinition(
                "invalid",
                ActorLifecycleProfileDefinition.DestructionPolicyKind
                    .PermanentlyDormant,
                delayTicks: 0,
                automaticReturnFormId: "mobile"));
    }

    [Fact]
    public void SlotAssignmentsRequireCoherentActiveAndDormantShapes()
    {
        Assert.Throws<ArgumentException>(() =>
            Assignment(
                initialGeneration: null,
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.ActiveAtTickZero,
                unlockTick: null));
        Assert.Throws<ArgumentException>(() =>
            Assignment(
                initialGeneration: 0,
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.ActiveAtTickZero,
                unlockTick: 1));
        Assert.Throws<ArgumentException>(() =>
            Assignment(
                initialGeneration: null,
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.DormantUnlockAtTick,
                unlockTick: null));
        Assert.Throws<ArgumentException>(() =>
            Assignment(
                initialGeneration: 1,
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.DormantUnlockAtTick,
                unlockTick: 2));

        ActorUnitSlotLifecycleAssignmentDefinition tickZeroReady =
            Assignment(
                initialGeneration: null,
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.DormantUnlockAtTick,
                unlockTick: 0);

        Assert.Equal(0, tickZeroReady.UnlockTick);

        ActorUnitSlotLifecycleAssignmentDefinition automaticActivation =
            CreateAssignment(
                initialGeneration: 0,
                initialAvailability:
                    ActorUnitSlotLifecycleAssignmentDefinition
                        .InitialAvailabilityKind
                        .DormantAutomaticActivationAtTick,
                unlockTick: 120);
        Assert.Equal(0, automaticActivation.InitialGeneration);
        Assert.Equal(
            "west-respawn",
            automaticActivation.AssignedRespawnSpawnId);

        Assert.Throws<ArgumentException>(() =>
            CreateAssignment(
                initialGeneration: null,
                initialAvailability:
                    ActorUnitSlotLifecycleAssignmentDefinition
                        .InitialAvailabilityKind
                        .DormantAutomaticActivationAtTick,
                unlockTick: 120));
        Assert.Throws<ArgumentException>(() =>
            CreateAssignment(
                initialGeneration: 0,
                initialAvailability:
                    ActorUnitSlotLifecycleAssignmentDefinition
                        .InitialAvailabilityKind
                        .DormantAutomaticActivationAtTick,
                unlockTick: null));
        Assert.Throws<ArgumentException>(() =>
            CreateAssignment(
                initialGeneration: 0,
                initialAvailability:
                    ActorUnitSlotLifecycleAssignmentDefinition
                        .InitialAvailabilityKind
                        .DormantAutomaticActivationAtTick,
                unlockTick: 120,
                assignedRespawnSpawnId: null));
    }

    [Fact]
    public void SlotAssignmentsRejectInvalidIdentityFormsAndEnums()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAssignment(teamId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAssignment(unitId: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAssignment(initialGeneration: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAssignment(
                initialAvailability:
                    (ActorUnitSlotLifecycleAssignmentDefinition
                        .InitialAvailabilityKind)99));
        Assert.Throws<ArgumentException>(() =>
            CreateAssignment(allowedFormIds: []));
        Assert.Throws<ArgumentException>(() =>
            CreateAssignment(allowedFormIds: ["mobile", "mobile"]));
        Assert.Throws<ArgumentException>(() =>
            CreateAssignment(assignedRespawnSpawnId: " "));
    }

    private static ActorLifecycleProfileDefinition AutomaticProfile() =>
        new(
            "deathmatch-respawn",
            ActorLifecycleProfileDefinition.DestructionPolicyKind
                .AutomaticRespawn,
            delayTicks: 3,
            automaticReturnFormId: "mobile");

    private static ActorUnitSlotLifecycleAssignmentDefinition Assignment(
        int? initialGeneration,
        ActorUnitSlotLifecycleAssignmentDefinition.InitialAvailabilityKind
            initialAvailability,
        int? unlockTick) =>
        CreateAssignment(
            initialGeneration: initialGeneration,
            initialAvailability: initialAvailability,
            unlockTick: unlockTick);

    private static ActorUnitSlotLifecycleAssignmentDefinition CreateAssignment(
        int teamId = 0,
        int unitId = 0,
        int? initialGeneration = 0,
        IEnumerable<string>? allowedFormIds = null,
        ActorUnitSlotLifecycleAssignmentDefinition.InitialAvailabilityKind
            initialAvailability =
                ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.ActiveAtTickZero,
        int? unlockTick = null,
        string? assignedRespawnSpawnId = "west-respawn") =>
        new(
            teamId,
            unitId,
            lifecycleProfileId: "deathmatch-respawn",
            initialGeneration,
            allowedFormIds ?? ["mobile"],
            initialAvailability,
            unlockTick,
            assignedRespawnSpawnId);
}
