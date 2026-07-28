namespace BotArena.Engine.Tests;

public sealed class ActorTransitionDefinitionTests
{
    [Fact]
    public void SplitProofArmIsExplicitBoundedAndSetOrderIndependent()
    {
        SplitReplicationTransitionDefinition left = Split(
            ["prime-mobile", "scout-mobile"]);
        SplitReplicationTransitionDefinition right = Split(
            ["scout-mobile", "prime-mobile"]);

        Assert.Equal(
            ["prime-mobile", "scout-mobile"],
            left.SourceFormIds.ToArray());
        Assert.Equal(
            left.SourceFormIds.ToArray(),
            right.SourceFormIds.ToArray());
        Assert.Equal(2, left.DescendantCount);
        Assert.Equal(0, left.MaxSourceGeneration);
        Assert.True(left.RequireNoPriorSameLifeTransition);
        Assert.Equal(
            ActorReplicationHealthDefinition.DistributionKind
                .DivideCurrentHealthEquallyFloor,
            left.Health.Distribution);
        Assert.Equal(1, left.Health.MinimumHealthPerDescendant);
        Assert.Equal(2, left.MinimumSourceHealth);
        Assert.Equal(
            ActorReplicationHealthDefinition.RemainderKind.Discard,
            left.Health.Remainder);
        Assert.Equal(
            ActorReplicationHealthDefinition
                .ActorReplicationMaximumHealthKind
                .ClampDownToOutputFormMaximum,
            left.Health.MaximumHealth);
        Assert.Equal(
            [
                new ActorRelativePositionOffset(0, -1),
                new ActorRelativePositionOffset(0, 1),
                new ActorRelativePositionOffset(1, 0),
            ],
            left.CandidateOffsets.ToArray());
        Assert.True(left.ReservationIsAtomic);
        Assert.True(left.ConflictingBundlesAllBlock);
        Assert.True(left.InsufficientHealthBlocks);
        Assert.True(left.ReuseSourceSlotFirst);
        Assert.True(left.LethalSourceDamageCancels);
        Assert.False(left.SourceRetirementCountsAsDestruction);
        Assert.True(left.DescendantsUseFreshIsolatedRuntimes);
        Assert.False(left.DescendantsInheritPrivateMemory);
        Assert.Equal(
            SplitReplicationTransitionDefinition.SlotSelectionKind
                .SourceThenLowestCompatibleReadyDormantSameParticipant,
            left.SlotSelection);
        Assert.Equal(
            SplitReplicationTransitionDefinition.DescendantAssignmentKind
                .ZipSlotOrderToPositionOrder,
            left.DescendantAssignment);
        Assert.Equal(
            SplitReplicationTransitionDefinition
                .ActorReplicationConflictResolutionKind
                .BlockIntersectingClaimComponents,
            left.ConflictResolution);
        Assert.Equal(
            SplitReplicationTransitionDefinition.HealthEvaluationKind
                .CompletionTimeCurrentHealthCancelIfBelowMinimum,
            left.HealthEvaluation);
    }

    [Fact]
    public void CandidateOrderIsSemanticAndInputsAreSnapshotted()
    {
        var sources = new List<string> { "prime-mobile" };
        var offsets = new List<ActorRelativePositionOffset>
        {
            new(0, 1),
            new(0, -1),
        };

        SplitReplicationTransitionDefinition split = new(
            transitionId: "split-prime",
            actionId: "split",
            sourceFormIds: sources,
            outputFormId: "split-mobile",
            descendantCount: 2,
            maxSourceGeneration: 0,
            requireNoPriorSameLifeTransition: true,
            new ActorReplicationHealthDefinition(
                ActorReplicationHealthDefinition.DistributionKind
                    .DivideCurrentHealthEquallyFloor,
                minimumHealthPerDescendant: 1,
                ActorReplicationHealthDefinition.RemainderKind.Discard),
            candidateOffsets: offsets,
            Windup(durationTicks: 1));

        sources[0] = "mutated";
        offsets.Reverse();

        Assert.Equal(["prime-mobile"], split.SourceFormIds.ToArray());
        Assert.Equal(
            [
                new ActorRelativePositionOffset(0, 1),
                new ActorRelativePositionOffset(0, -1),
            ],
            split.CandidateOffsets.ToArray());
    }

    [Fact]
    public void SameLifeFormTransitionCarriesContinuityAndHealthPolicy()
    {
        var anchor = new ActorFormTransitionDefinition(
            transitionId: "anchor-prime",
            actionId: "anchor",
            sourceFormId: "prime-mobile",
            targetFormId: "prime-turret",
            SameLifeWindup(durationTicks: 2),
            ActorSameLifeTransitionDefinition.MemoryContinuityKind
                .PreservePrivateMemory,
            new ActorSameLifeHealthDefinition(
                ActorSameLifeHealthDefinition.HealthPolicyKind
                    .AddFlatCappedToTargetMaximum,
                flatHealthGain: 2),
            ActorSameLifeCombatStateDefinition.PreserveWithoutRefillV1,
            new ActorSameLifePlacementDefinition(
                ActorSameLifePlacementDefinition.PositionContinuityKind
                    .SameOccupiedGroundTile,
                ActorSameLifePlacementDefinition.LegalityEvaluationKind
                    .QueueAndCompletionTileTags,
                requiredTileTags: [],
                forbiddenTileTags:
                [
                    ActorMapTileTagDefinition.TileTagKind
                        .TransitionPlacementForbidden,
                ],
                ActorSameLifePlacementDefinition.FailedCompletionKind
                    .CancelAndRemainInSourceForm),
            irreversibleForLife: true);

        Assert.Equal(
            ActorSameLifeTransitionDefinition.SameLifeTransitionKind
                .FormTransition,
            anchor.Kind);
        Assert.Equal("prime-mobile", anchor.SourceFormId);
        Assert.Equal("prime-turret", anchor.TargetFormId);
        Assert.Equal(2, anchor.Health.FlatHealthGain);
        Assert.Equal(
            ActorSameLifeHealthDefinition.PreserveRatioFormulaKind
                .FloorCurrentTimesTargetMaximumDividedBySourceMaximumThenMinimumOne,
            anchor.Health.PreserveRatioFormula);
        Assert.Equal(
            ActorSameLifeCombatStateDefinition.CooldownContinuityKind
                .PreserveRemainingTicks,
            anchor.CombatState.CooldownContinuity);
        Assert.Equal(
            [
                ActorMapTileTagDefinition.TileTagKind
                    .TransitionPlacementForbidden,
            ],
            anchor.Placement.ForbiddenTileTags.ToArray());
        Assert.True(anchor.IrreversibleForLife);
    }

    [Fact]
    public void FabricationPreservesSourceAndCreatesFreshBoundedChild()
    {
        var fabrication = new BoundedChildFabricationDefinition(
            transitionId: "fabricate-child",
            actionId: "fabricate",
            sourceFormIds: ["prime-mobile"],
            outputFormId: "child-mobile",
            sourceRegionRoleId: "own-fabrication-pad",
            outputRegionRoleId: "own-fabrication-pad",
            requiredSourceTileTags:
            [
                ActorMapTileTagDefinition.TileTagKind.SpawnProtected,
            ],
            requiredOutputTileTags:
            [
                ActorMapTileTagDefinition.TileTagKind.SpawnProtected,
            ],
            forbiddenOutputTileTags:
            [
                ActorMapTileTagDefinition.TileTagKind
                    .TransitionPlacementForbidden,
            ],
            candidateOffsets:
            [
                new(0, -1),
                new(0, 1),
            ],
            new ActorFabricationDelayDefinition(durationTicks: 1),
            ActorActionRejectionResult.Blocked);
        var regionAssignment =
            new ActorParticipantRegionAssignmentDefinition(
                participantId: 7,
                regionRoleId: "own-fabrication-pad",
                mapRegionId: "west-fabrication-pad",
                Direction.East);

        Assert.Equal(
            ActorFabricationTransitionDefinition.FabricationTransitionKind
                .BoundedChild,
            fabrication.Kind);
        Assert.Equal(
            BoundedChildFabricationDefinition.TargetSlotKind
                .ExplicitReadyDormantSameParticipant,
            fabrication.TargetSlot);
        Assert.Equal(
            BoundedChildFabricationDefinition.SourceDispositionKind
                .SourceLifeSurvives,
            fabrication.SourceDisposition);
        Assert.Equal(
            BoundedChildFabricationDefinition
                .ActorFabricationConflictResolutionKind
                .BlockIntersectingLifecycleClaimComponents,
            fabrication.ConflictResolution);
        Assert.Equal(
            ActorFabricationDelayDefinition.SourceBehaviorKind
                .UnchangedActiveLifeCanAct,
            fabrication.Delay.SourceBehavior);
        Assert.Equal(
            ActorFabricationDelayDefinition.SourceDeathKind
                .DoesNotCancelQueuedFabrication,
            fabrication.Delay.SourceDeath);
        Assert.Equal(
            ActorFabricationDelayDefinition.SourceRetirementKind
                .DoesNotCancelQueuedFabricationExceptParticipantDisqualification,
            fabrication.Delay.SourceRetirement);
        Assert.Equal(
            BoundedChildFabricationDefinition.OutputFacingKind
                .ParticipantOutputRegionAssignmentFacing,
            fabrication.OutputFacing);
        Assert.Equal(Direction.East, regionAssignment.Facing);
    }

    [Fact]
    public void InvalidTransitionShapesFailClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorSeedMechanicsDefinition(
                seedProfileId: "split-comparison-1",
                (ActorSeedMechanicsDefinition.SeedDerivationKind)99,
                ActorSeedMechanicsDefinition.LifeIdentityAssignmentKind
                    .PerStableUnitMonotonicStartingAtZero,
                ActorSeedMechanicsDefinition.RuntimeLifetimeKind
                    .FreshRuntimePerLife,
                ActorSeedMechanicsDefinition.PrivateMemoryKind
                    .IsolatedPerRuntime));
        Assert.Throws<ArgumentException>(() => Split(
            ["prime-mobile", "prime-mobile"]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorReplicationHealthDefinition(
                ActorReplicationHealthDefinition.DistributionKind
                    .DivideCurrentHealthEquallyFloor,
                minimumHealthPerDescendant: 0,
                ActorReplicationHealthDefinition.RemainderKind.Discard));
        Assert.Throws<ArgumentException>(() =>
            NewSplit(
                descendantCount: 3,
                [new(0, -1), new(0, 1)]));
        Assert.Throws<ArgumentException>(() =>
            NewSplit(
                descendantCount: 2,
                [new(0, -1), new(0, -1)]));
        Assert.Throws<ArgumentException>(() =>
            new BoundedChildFabricationDefinition(
                transitionId: "fabricate-overlap",
                actionId: "fabricate",
                sourceFormIds: ["prime-mobile"],
                outputFormId: "child-mobile",
                sourceRegionRoleId: "source-pad",
                outputRegionRoleId: "output-pad",
                requiredSourceTileTags: [],
                requiredOutputTileTags: [],
                forbiddenOutputTileTags: [],
                candidateOffsets: [new(0, 0)],
                new ActorFabricationDelayDefinition(1),
                ActorActionRejectionResult.Blocked));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SplitReplicationTransitionDefinition(
                transitionId: "split-overflow",
                actionId: "split",
                sourceFormIds: ["prime-mobile"],
                outputFormId: "split-mobile",
                descendantCount: 2,
                maxSourceGeneration: 0,
                requireNoPriorSameLifeTransition: true,
                new ActorReplicationHealthDefinition(
                    ActorReplicationHealthDefinition.DistributionKind
                        .DivideCurrentHealthEquallyFloor,
                    minimumHealthPerDescendant: int.MaxValue,
                    ActorReplicationHealthDefinition.RemainderKind.Discard),
                candidateOffsets: [new(0, -1), new(0, 1)],
                Windup(durationTicks: 1)));
        Assert.Throws<ArgumentException>(() =>
            new ActorSameLifeHealthDefinition(
                ActorSameLifeHealthDefinition.HealthPolicyKind
                    .PreserveCurrentCappedToTargetMaximum,
                flatHealthGain: 1));
    }

    private static SplitReplicationTransitionDefinition Split(
        IReadOnlyCollection<string> sourceFormIds) =>
        new(
            transitionId: "split-prime",
            actionId: "split",
            sourceFormIds,
            outputFormId: "split-mobile",
            descendantCount: 2,
            maxSourceGeneration: 0,
            requireNoPriorSameLifeTransition: true,
            new ActorReplicationHealthDefinition(
                ActorReplicationHealthDefinition.DistributionKind
                    .DivideCurrentHealthEquallyFloor,
                minimumHealthPerDescendant: 1,
                ActorReplicationHealthDefinition.RemainderKind.Discard),
            candidateOffsets:
            [
                new(0, -1),
                new(0, 1),
                new(1, 0),
            ],
            Windup(durationTicks: 1));

    private static SplitReplicationTransitionDefinition NewSplit(
        int descendantCount,
        IReadOnlyList<ActorRelativePositionOffset> offsets) =>
        new(
            transitionId: "split-prime",
            actionId: "split",
            sourceFormIds: ["prime-mobile"],
            outputFormId: "split-mobile",
            descendantCount,
            maxSourceGeneration: 0,
            requireNoPriorSameLifeTransition: true,
            new ActorReplicationHealthDefinition(
                ActorReplicationHealthDefinition.DistributionKind
                    .DivideCurrentHealthEquallyFloor,
                minimumHealthPerDescendant: 1,
                ActorReplicationHealthDefinition.RemainderKind.Discard),
            candidateOffsets: offsets,
            Windup(durationTicks: 1));

    private static ActorTransitionWindupDefinition Windup(
        int durationTicks) =>
        new(
            durationTicks,
            ActorTransitionWindupDefinition.PendingActionKind.WaitOnly,
            ActorTransitionWindupDefinition.SourceFormKind.RetainSourceForm,
            ActorTransitionWindupDefinition.TargetabilityKind
                .TargetableAndOccupiesTile,
            ActorTransitionWindupDefinition.LethalDamageKind.CancelTransition,
            ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                .TickStartAfterDuration,
            ActorTransitionWindupDefinition.PlacementReferenceKind
                .QueueTimePose);

    private static ActorTransitionWindupDefinition SameLifeWindup(
        int durationTicks) =>
        new(
            durationTicks,
            ActorTransitionWindupDefinition.PendingActionKind.WaitOnly,
            ActorTransitionWindupDefinition.SourceFormKind.RetainSourceForm,
            ActorTransitionWindupDefinition.TargetabilityKind
                .TargetableAndOccupiesTile,
            ActorTransitionWindupDefinition.LethalDamageKind.CancelTransition,
            ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                .EndOfStartedTickPlusDurationMinusOneAfterModeUpdate,
            ActorTransitionWindupDefinition.PlacementReferenceKind
                .QueueTimePose);
}
