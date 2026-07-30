namespace BotArena.Engine;

/// <summary>
/// Explicit wire IDs for every closed enum reachable from the generation-3
/// contract. Canonical bytes never depend on enum member names or reflection.
/// </summary>
internal static class ActorContractCanonicalIds
{
    public static string Id(Enum value) => value switch
    {
        Direction.North => "north",
        Direction.East => "east",
        Direction.South => "south",
        Direction.West => "west",

        GameModeDefinition.GameModeDefinitionKind.Frontline => "frontline",
        GameModeDefinition.GameModeDefinitionKind.Deathmatch => "deathmatch",
        VictoryDefinition.VictoryDefinitionKind.Frontline => "frontline",
        VictoryDefinition.VictoryDefinitionKind.Deathmatch => "deathmatch",
        ScoreChannelDefinition.ChannelKind.Kills => "kills",
        ScoreChannelDefinition.ChannelKind.Deaths => "deaths",
        ScoreChannelDefinition.ChannelKind.DamageDealt => "damage-dealt",
        ScoreChannelDefinition.ChannelKind.ActiveHealth => "active-health",
        ScoreChannelDefinition.ChannelKind.TerritorialProgress =>
            "territorial-progress",
        ScoreChannelDefinition.ValueDomain.NonNegative => "non-negative",
        ScoreChannelDefinition.ValueDomain.Signed => "signed",
        ScoreRankingDefinition.SortDirection.HigherWins => "higher-wins",
        ScoreRankingDefinition.SortDirection.LowerWins => "lower-wins",
        DeathmatchVictoryDefinition.TerminalTickPrecedenceKind
                .KillLimitAfterCompleteJointTickBeforeMaxTickTimeout =>
            "kill-limit-after-complete-joint-tick-before-max-tick-timeout",
        DeathmatchScoringDefinition.DeathIncrementKind
                .OneRawDeathToDestroyedActorTeamPerDamageCausedDestruction =>
            "one-raw-death-to-destroyed-actor-team-per-damage-caused-destruction",
        DeathmatchScoringDefinition.KillIncrementKind
                .OneRawKillToExactHostileHealthToZeroDamageSourceTeam =>
            "one-raw-kill-to-exact-hostile-health-to-zero-damage-source-team",
        DeathmatchScoringDefinition.AlliedFinalDamageKind
                .VictimTeamDeathNoKill =>
            "victim-team-death-no-kill",
        DeathmatchScoringDefinition.DamageDealtIncrementKind
                .HostileActualHealthRemovedToExactSourceTeam =>
            "hostile-actual-health-removed-to-exact-source-team",
        DeathmatchScoringDefinition.ActiveHealthSnapshotKind
                .TerminalSumAcrossActiveTeamLives =>
            "terminal-sum-across-active-team-lives",
        DeathmatchScoringDefinition.NonDamageRetirementKind
                .ReplicationRetirementAddsNeitherDeathNorKill =>
            "replication-retirement-adds-neither-death-nor-kill",
        DeathmatchScoringDefinition.EarlyKillLimitResolutionKind
                .CompleteJointTickThenHighestRawKillsWinTiedTopDraw =>
            "complete-joint-tick-then-highest-raw-kills-win-tied-top-draw",
        FrontlineCaptureDefinition.ControlPolicyKind
                  .BinaryPositiveWeightPerTeamNoStackingNonSoleAppliesConfiguredDecayOppositionErodesToNeutral =>
              "binary-positive-weight-per-team-no-stacking-non-sole-applies-configured-decay-opposition-erodes-to-neutral",
        FrontlineCaptureDefinition.ControlPolicyKind
                  .NetPositiveObjectiveWeightDifferenceScalesGainNonPositiveAppliesConfiguredDecayOppositionErodesToNeutral =>
              "net-positive-objective-weight-difference-scales-gain-non-positive-applies-configured-decay-opposition-erodes-to-neutral",
        FrontlineCaptureDefinition.TimeoutPolicyKind
                .SignedPositionThresholdPlusClaimZeroDrawNoTiebreakers =>
            "signed-position-threshold-plus-claim-zero-draw-no-tiebreakers",
        FrontlineCaptureDefinition.TerritorialProgressFormulaKind
                .PerTeamAdvanceDeltaTimesIndexOffsetTimesThresholdPlusSignedClaim =>
            "per-team-advance-delta-times-index-offset-times-threshold-plus-signed-claim",
        FrontlineCaptureDefinition.CompletionPolicyKind
                .BaseBreachBeforeMaxTicks =>
            "base-breach-before-max-ticks",
        FrontlineCaptureDefinition.FrontlineInitialPositionKind
                .CentreObjectiveIndex =>
            "centre-objective-index",
        FrontlineCaptureDefinition.CaptureArithmeticKind
                .CheckedInt64AddCompareThresholdCompletesOnePushAndDiscardsOvershoot =>
            "checked-int64-add-compare-threshold-completes-one-push-and-discards-overshoot",
        FrontlineCaptureDefinition.OppositionArithmeticKind
                .ErodeTowardZeroWithoutCarryingOvershootIntoOwnClaim =>
            "erode-toward-zero-without-carrying-overshoot-into-own-claim",
        FrontlineCaptureDefinition.DecayClockKind
                .ConsecutiveEmptyOrContestedTicksResetByAnySoleControl =>
            "consecutive-empty-or-contested-ticks-reset-by-any-sole-control",
        FrontlineCaptureDefinition.DecayClockKind
                .EmptyAndContestedTicksPreserveClaimEnemySoleErosionOnly =>
            "empty-and-contested-ticks-preserve-claim-enemy-sole-erosion-only",
        FrontlineCaptureDefinition.DisabledDecayKind
                .ZeroPairPreservesClaimAndKeepsClockZero =>
            "zero-pair-preserves-claim-and-keeps-clock-zero",
        FrontlineCaptureDefinition.RedeployPolicyKind
                .AdvanceImmediatelyResetClaimKeepWorldPauseThroughCapturePlusConfiguredTicksBreachSkipsPause =>
            "advance-immediately-reset-claim-keep-world-pause-through-capture-plus-configured-ticks-breach-skips-pause",
        FrontlineCaptureDefinition.RedeployPolicyKind
                .AdvanceImmediatelyThenDenyEnemyRegressionPastTheHighWaterMarkThroughConfiguredHoldTicks =>
            "advance-immediately-then-deny-enemy-regression-past-the-high-water-mark-through-configured-hold-ticks",
        FrontlineCaptureDefinition.RedeployTickArithmeticKind
                .CheckedInt64CaptureTickPlusOnePlusPauseRequireInt32 =>
            "checked-int64-capture-tick-plus-one-plus-pause-require-int32",

        MatchFormatDefinition.MatchFormatDefinitionKind.HeadToHead =>
            "head-to-head",
        MatchFormatDefinition.MatchFormatDefinitionKind.FreeForAll =>
            "free-for-all",
        MatchFormatDefinition.MatchFormatDefinitionKind.Teams => "teams",
        ActorModeMapBindingDefinition.ActorModeMapBindingDefinitionKind
                .Deathmatch =>
            "deathmatch",
        ActorModeMapBindingDefinition.ActorModeMapBindingDefinitionKind
                .Frontline =>
            "frontline",
        FrontlineTeamAdvanceDefinition.ObjectiveAdvanceDirection
                .TowardLowerIndex =>
            "toward-lower-index",
        FrontlineTeamAdvanceDefinition.ObjectiveAdvanceDirection
                .TowardHigherIndex =>
            "toward-higher-index",

        ActorMovementLayer.Ground => "ground",
        ActorMovementLayer.Air => "air",
        ActorMovementFacingCoupling.PreserveFacing => "preserve-facing",
        ActorMovementFacingCoupling.FaceMovementDirection =>
            "face-movement-direction",
        ActorMovementFacingCoupling.FacingLocked => "facing-locked",
        ActorFormProjectileGuardKind.None => "none",
        ActorFormProjectileGuardKind.FacingQuadrantContactsDeflected =>
            "facing-quadrant-contacts-deflected",
        ActorAttackVolleyDefinition.VolleySpreadKind.SharedResolvedHeading =>
            "shared-resolved-heading",
        ActorAttackVolleyDefinition.VolleySpreadKind
                .SymmetricAdjacentHeadingFanAscendingSignedSectorOffset =>
            "symmetric-adjacent-heading-fan-ascending-signed-sector-offset",
        ActorAttackVolleyDefinition.IdentityOrderKind
                .ContiguousAscendingInLaunchOrder =>
            "contiguous-ascending-in-launch-order",
        ActorAutomaticReturnTriggerDefinition.AutomaticReturnCounterKind
                .AttacksIssuedSinceEnteringSourceForm =>
            "attacks-issued-since-entering-source-form",
        ActorAutomaticReturnTriggerDefinition.AutomaticReturnCounterKind
                .ProjectilesDeflectedSinceEnteringSourceForm =>
            "projectiles-deflected-since-entering-source-form",
        ActorMapRegionDefinition.RegionKind.Objective => "objective",
        ActorMapRegionDefinition.RegionKind.TransitionPlacement =>
            "transition-placement",
        ActorMapTileTagDefinition.TileTagKind
                .TransitionPlacementForbidden =>
            "transition-placement-forbidden",
        ActorMapTileTagDefinition.TileTagKind.SpawnProtected =>
            "spawn-protected",

        ActorRuntimeFaultDefinition.AccumulationScopeKind
                .ParticipantAcrossAllSlotsLivesAndRuntimeStages =>
            "participant-across-all-slots-lives-and-runtime-stages",
        ActorRuntimeFaultDefinition.FaultCounterArithmeticKind
                .SignedInt64SaturatingAtAllowedPlusOne =>
            "signed-int64-saturating-at-allowed-plus-one",
        ActorRuntimeFaultDefinition.FaultingDecisionKind
                .ReplaceExactActorDecisionWithWait =>
            "replace-exact-actor-decision-with-wait",
        ActorRuntimeFaultDefinition.RuntimeStageRecoveryKind
                .CreateStartOrExecuteFailureDiscardsInstanceSyntheticWaitRetryFreshOnceNextActiveTick =>
            "create-start-or-execute-failure-discards-instance-synthetic-wait-retry-fresh-once-next-active-tick",
        ActorRuntimeFaultDefinition.ReplayFaultRepresentationKind
                .StageTaggedHostFaultNoRuntimeReplyAcceptedSyntheticWait =>
            "stage-tagged-host-fault-no-runtime-reply-accepted-synthetic-wait",
        ActorRuntimeFaultDefinition.FaultBatchEventOrderKind
                .ParticipantThenActorIdentityThenCreateStartTickValidationStage =>
            "participant-then-actor-identity-then-create-start-tick-validation-stage",
        ActorRuntimeFaultDefinition.ApplicationTimingKind
                .AfterDamageBeforeModeUpdateUsingCompleteJointFaultBatch =>
            "after-damage-before-mode-update-using-complete-joint-fault-batch",
        ActorRuntimeFaultDefinition.ThresholdKind
                .DisqualifyWhenCumulativeCountExceedsAllowedCount =>
            "disqualify-when-cumulative-count-exceeds-allowed-count",
        ActorRuntimeFaultDefinition.ParticipantDispositionKind
                .RetireAllActiveLivesAndPermanentlyDormantAllOwnedSlots =>
            "retire-all-active-lives-and-permanently-dormant-all-owned-slots",
        ActorRuntimeFaultDefinition.PendingWorkDispositionKind
                .CancelAllOwnedClocksBundlesAndTransitionsReleaseEveryClaim =>
            "cancel-all-owned-clocks-bundles-and-transitions-release-every-claim",
        ActorRuntimeFaultDefinition.CancellationEventOrderKind
                .ClocksByTargetSlotThenBundlesByFamilySourceTransitionAndTarget =>
            "clocks-by-target-slot-then-bundles-by-family-source-transition-and-target",
        ActorRuntimeFaultDefinition.OwnedProjectileDispositionKind
                .RemoveAfterJointDamageByProjectileIdWithoutContactOrScore =>
            "remove-after-joint-damage-by-projectile-id-without-contact-or-score",
        ActorRuntimeFaultDefinition.ScoreDispositionKind
                .RetirementAddsNoKillOrDeath =>
            "retirement-adds-no-kill-or-death",
        ActorRuntimeFaultDefinition.ScoringTeamEligibilityKind
                .EligibleWhileAnyNonDisqualifiedParticipantRemains =>
            "eligible-while-any-non-disqualified-participant-remains",
        ActorRuntimeFaultDefinition.MatchCompletionKind
                .AfterFaultPhaseOneEligibleTeamWinsZeroEligibleTeamsDraw =>
            "after-fault-phase-one-eligible-team-wins-zero-eligible-teams-draw",
        ActorRuntimeFaultDefinition.FinalRankingKind
                .IneligibleTeamsRankBelowEveryEligibleTeamAndTieAtBottom =>
            "ineligible-teams-rank-below-every-eligible-team-and-tie-at-bottom",

        ActorSeedMechanicsDefinition.SeedDerivationKind
                .MatchSeedProfileTeamUnitLifeMix64V1 =>
            "match-seed-profile-team-unit-life-mix64-v1",
        ActorSeedMechanicsDefinition.LifeIdentityAssignmentKind
                .PerStableUnitMonotonicStartingAtZero =>
            "per-stable-unit-monotonic-starting-at-zero",
        ActorSeedMechanicsDefinition.RuntimeLifetimeKind
                .FreshRuntimePerLife =>
            "fresh-runtime-per-life",
        ActorSeedMechanicsDefinition.PrivateMemoryKind.IsolatedPerRuntime =>
            "isolated-per-runtime",

        ActorLifecycleDefinition.DestructionClockKind
                .TickStartAtDestroyedTickPlusOnePlusProfileDelayCheckedArithmetic =>
            "tick-start-at-destroyed-tick-plus-one-plus-profile-delay-checked-arithmetic",
        ActorLifecycleDefinition.NewLifeSemanticsKind
                .FreshRuntimeEmptyMemoryDeterministicSeedTargetFormMaximumMonotonicLifeIdCanActOnCreationTick =>
            "fresh-runtime-empty-memory-deterministic-seed-target-form-maximum-monotonic-life-id-can-act-on-creation-tick",
        ActorLifecycleDefinition.NewLifeCombatStateKind
                .ZeroCooldownTargetMaximumEnergyNoPreviousAction =>
            "zero-cooldown-target-maximum-energy-no-previous-action",
        ActorLifecycleDefinition.NewLifeResourceClockKind
                .UsesMatchGlobalProfileCadenceStartingAfterCreationTickActions =>
            "uses-match-global-profile-cadence-starting-after-creation-tick-actions",
        ActorLifecycleDefinition.GenerationSemanticsKind
                .AutomaticRespawnPreservesDestroyedGenerationFabricationAndReplicationUseSourcePlusOne =>
            "automatic-respawn-preserves-destroyed-generation-fabrication-and-replication-use-source-plus-one",
        ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                .AssignedSpawnPermanentlyReservedForSlotAgainstOtherActorsAndLifecycleClaims =>
            "assigned-spawn-permanently-reserved-for-slot-against-other-actors-and-lifecycle-claims",
        ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                .OwnSideChainAdjacentObjectiveTileThenAssignedSpawn =>
            "own-side-chain-adjacent-objective-tile-then-assigned-spawn",
        ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                .OwnSideChainAdjacentObjectiveTileInTeamAdvanceOrderThenAssignedSpawn =>
            "own-side-chain-adjacent-objective-tile-in-team-advance-order-then-assigned-spawn",
        ActorLifecycleDefinition.TickStartLifecycleOrderKind
                .DueTickThenReturnsAndReadinessThenFabricationThenReplicationCanonicalActorOrder =>
            "due-tick-then-returns-and-readiness-then-fabrication-then-replication-canonical-actor-order",
        ActorLifecycleDefinition.OutputTileProjectileKind
                .DueCreationConsumesOccupantsByProjectileIdWithoutDamageBeforeSpawn =>
            "due-creation-consumes-occupants-by-projectile-id-without-damage-before-spawn",
        ActorLifecycleProfileDefinition.DestructionPolicyKind
                .AutomaticRespawn =>
            "automatic-respawn",
        ActorLifecycleProfileDefinition.DestructionPolicyKind
                .ReadyForExplicitFabrication =>
            "ready-for-explicit-fabrication",
        ActorLifecycleProfileDefinition.DestructionPolicyKind
                .PermanentlyDormant =>
            "permanently-dormant",

        ActorVisionDistanceMetric.Chebyshev => "chebyshev",
        ActorVisionShape.Omnidirectional => "omnidirectional",
        ActorVisionShape.FacingQuadrant => "facing-quadrant",
        ActorLineOfSightModel.CornerStrictSupercover =>
            "corner-strict-supercover",
        ActorHearingBearingModel.Disabled => "disabled",
        ActorHearingBearingModel.EightOctantsStrictTwoToOneCardinalV1 =>
            "eight-octants-strict-two-to-one-cardinal-v1",
        ActorVisionProfileDefinition.HearingDistanceBandModelKind
                .ChebyshevInclusiveOrderedUpperBoundsThenFinalRadiusBand =>
            "chebyshev-inclusive-ordered-upper-bounds-then-final-radius-band",
        ActorAudibleEventKind.Attack => "attack",
        ActorAudibleEventKind.Damage => "damage",
        ActorAudibleEventKind.Destruction => "destruction",

        ActorProjectileMode.InstantRay => "instant-ray",
        ActorProjectileMode.Discrete => "discrete",
        ActorShotHeadingModel.EightWayClockwiseModuloV1 =>
            "eight-way-clockwise-modulo-v1",
        ActorAttackProfileDefinition.EnergyRegenerationClockKind
                .CompletedMatchTickModuloIntervalEqualsZero =>
            "completed-match-tick-modulo-interval-equals-zero",
        ActorAttackProfileDefinition.EnergyUpdateOrderKind
                .AttackCostThenCadenceRegenerationCappedToMaximum =>
            "attack-cost-then-cadence-regeneration-capped-to-maximum",
        ActorAttackProfileDefinition.EnergyArithmeticKind
                .CheckedInt64ThenClampToMaximum =>
            "checked-int64-then-clamp-to-maximum",
        ActorAttackProfileDefinition.AttackAvailabilityKind
                .PreTickCooldownZeroAndEnergyAtLeastCost =>
            "pre-tick-cooldown-zero-and-energy-at-least-cost",
        ActorAttackProfileDefinition.CooldownUpdateKind
                .SuccessfulAttackSetsConfiguredTicksOtherwiseSubtractOneFloorZero =>
            "successful-attack-sets-configured-ticks-otherwise-subtract-one-floor-zero",
        ActorAttackProfileDefinition.AimInterpretationKind
                .CurrentFacingStraight =>
            "current-facing-straight",
        ActorAttackProfileDefinition.AimInterpretationKind
                .CurrentFacingPlusRelativeEightWayShotProgram =>
            "current-facing-plus-relative-eight-way-shot-program",
        ActorAttackProfileDefinition.AimInterpretationKind
                .AbsoluteSubmittedEightWayHeadingFacingUnchanged =>
            "absolute-submitted-eight-way-heading-facing-unchanged",

        ActorActionKind.Wait => "wait",
        ActorActionKind.Movement => "movement",
        ActorActionKind.Rotation => "rotation",
        ActorActionKind.Attack => "attack",
        ActorActionKind.Fabrication => "fabrication",
        ActorActionKind.SameLifeTransition => "same-life-transition",
        ActorActionKind.Replication => "replication",
        ActorActionParameterKind.ShotProgram => "shot-program",
        ActorActionParameterKind.Direction => "direction",
        ActorActionParameterKind.UnitTarget => "unit-target",
        ActorActionParameterKind.FormTarget => "form-target",
        ActorActionParameterKind.ProjectileHeading => "projectile-heading",
        ActorActionRejectionResult.Blocked => "blocked",
        ActorActionRejectionResult.Faulted => "faulted",
        ActorActionRejectionResult.Rejected => "rejected",

        ActorFabricationTransitionDefinition.FabricationTransitionKind
                .BoundedChild =>
            "bounded-child",
        BoundedChildFabricationDefinition.TargetSlotKind
                .ExplicitReadyDormantSameParticipant =>
            "explicit-ready-dormant-same-participant",
        BoundedChildFabricationDefinition.ActorFabricationCandidateSnapshotKind
                .PostMovementPreLifecycleQueueSnapshot =>
            "post-movement-pre-lifecycle-queue-snapshot",
        BoundedChildFabricationDefinition.ActorFabricationPositionSelectionKind
                .FirstEligibleDeclaredOffset =>
            "first-eligible-declared-offset",
        BoundedChildFabricationDefinition.ActorFabricationClaimScopeKind
                .SelectedTargetSlotAndTile =>
            "selected-target-slot-and-tile",
        BoundedChildFabricationDefinition
                .ActorFabricationConflictResolutionKind
                .BlockIntersectingLifecycleClaimComponents =>
            "block-intersecting-lifecycle-claim-components",
        BoundedChildFabricationDefinition.SourceDispositionKind
                .SourceLifeSurvives =>
            "source-life-survives",
        BoundedChildFabricationDefinition.ChildInitialStateKind
                .FreshRuntimeEmptyMemoryTargetFormDefaultsCanActOnCreationTick =>
            "fresh-runtime-empty-memory-target-form-defaults-can-act-on-creation-tick",
        BoundedChildFabricationDefinition.OutputFacingKind
                .ParticipantOutputRegionAssignmentFacing =>
            "participant-output-region-assignment-facing",
        BoundedChildFabricationDefinition.CandidateReferenceKind
                .QueueTimeSourcePose =>
            "queue-time-source-pose",
        BoundedChildFabricationDefinition.LineageKind
                .ParentIsFabricatingLifeSourceGenerationPlusOne =>
            "parent-is-fabricating-life-source-generation-plus-one",
        BoundedChildFabricationDefinition.OutputHealthKind
                .TargetFormMaximum =>
            "target-form-maximum",
        BoundedChildFabricationDefinition.SpawnReasonKind
                .FabricationOrRebuildFromTargetSlotHistory =>
            "fabrication-or-rebuild-from-target-slot-history",
        BoundedChildFabricationDefinition.ActorFabricationOffsetArithmeticKind
                .CheckedInt64ThenMapBounds =>
            "checked-int64-then-map-bounds",
        BoundedChildFabricationDefinition.OutstandingBundleKind
                .SourceMayQueueMultipleBundlesLimitedByDistinctReservedTargetsAndTiles =>
            "source-may-queue-multiple-bundles-limited-by-distinct-reserved-targets-and-tiles",
        ActorFabricationDelayDefinition.SourceBehaviorKind
                .UnchangedActiveLifeCanAct =>
            "unchanged-active-life-can-act",
        ActorFabricationDelayDefinition.SourceDeathKind
                .DoesNotCancelQueuedFabrication =>
            "does-not-cancel-queued-fabrication",
        ActorFabricationDelayDefinition.SourceRetirementKind
                .DoesNotCancelQueuedFabricationExceptParticipantDisqualification =>
            "does-not-cancel-queued-fabrication-except-participant-disqualification",
        ActorFabricationDelayDefinition.ReservationKind
                .TargetSlotAndTileBlockUntilCompletion =>
            "target-slot-and-tile-block-until-completion",
        ActorFabricationDelayDefinition.ActorFabricationCompletionKind
                .TickStartAfterDuration =>
            "tick-start-after-duration",
        ActorFabricationDelayDefinition.TickArithmeticKind.CheckedAddition =>
            "checked-addition",

        ActorSameLifeTransitionDefinition.SameLifeTransitionKind
                .FormTransition =>
            "form-transition",
        ActorSameLifeTransitionDefinition.MemoryContinuityKind
                .PreservePrivateMemory =>
            "preserve-private-memory",
        ActorTransitionWindupDefinition.PendingActionKind.WaitOnly =>
            "wait-only",
        ActorTransitionWindupDefinition.SourceFormKind.RetainSourceForm =>
            "retain-source-form",
        ActorTransitionWindupDefinition.TargetabilityKind
                .TargetableAndOccupiesTile =>
            "targetable-and-occupies-tile",
        ActorTransitionWindupDefinition.LethalDamageKind.CancelTransition =>
            "cancel-transition",
        ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                .TickStartAfterDuration =>
            "tick-start-after-duration",
        ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                .EndOfStartedTickPlusDurationMinusOneAfterModeUpdate =>
            "end-of-started-tick-plus-duration-minus-one-after-mode-update",
        ActorTransitionWindupDefinition.PlacementReferenceKind.QueueTimePose =>
            "queue-time-pose",
        ActorSameLifeHealthDefinition.HealthPolicyKind
                .PreserveCurrentCappedToTargetMaximum =>
            "preserve-current-capped-to-target-maximum",
        ActorSameLifeHealthDefinition.HealthPolicyKind
                .AddFlatCappedToTargetMaximum =>
            "add-flat-capped-to-target-maximum",
        ActorSameLifeHealthDefinition.HealthPolicyKind.SetToTargetMaximum =>
            "set-to-target-maximum",
        ActorSameLifeHealthDefinition.HealthPolicyKind
                .PreserveRatioFloorMinimumOne =>
            "preserve-ratio-floor-minimum-one",
        ActorSameLifeHealthDefinition.EvaluationKind
                .CompletionTimePreTransitionHealth =>
            "completion-time-pre-transition-health",
        ActorSameLifeHealthDefinition.ArithmeticKind
                .CheckedInt64ThenClampToTargetMaximum =>
            "checked-int64-then-clamp-to-target-maximum",
        ActorSameLifeHealthDefinition.PreserveRatioFormulaKind
                .FloorCurrentTimesTargetMaximumDividedBySourceMaximumThenMinimumOne =>
            "floor-current-times-target-maximum-divided-by-source-maximum-then-minimum-one",
        ActorSameLifeCombatStateDefinition.CooldownContinuityKind
                .PreserveRemainingTicks =>
            "preserve-remaining-ticks",
        ActorSameLifeCombatStateDefinition.EnergyContinuityKind
                .PreserveCurrentCappedToTargetMaximumMissingSourcePoolBecomesZero =>
            "preserve-current-capped-to-target-maximum-missing-source-pool-becomes-zero",
        ActorSameLifePlacementDefinition.PositionContinuityKind
                .SameOccupiedGroundTile =>
            "same-occupied-ground-tile",
        ActorSameLifePlacementDefinition.LegalityEvaluationKind
                .QueueAndCompletionTileTags =>
            "queue-and-completion-tile-tags",
        ActorSameLifePlacementDefinition.FailedCompletionKind
                .CancelAndRemainInSourceForm =>
            "cancel-and-remain-in-source-form",

        ActorReplicationTransitionDefinition.ReplicationTransitionKind.Split =>
            "split",
        ActorReplicationHealthDefinition.DistributionKind
                .DivideCurrentHealthEquallyFloor =>
            "divide-current-health-equally-floor",
        ActorReplicationHealthDefinition.RemainderKind.Discard => "discard",
        ActorReplicationHealthDefinition.ActorReplicationMaximumHealthKind
                .ClampDownToOutputFormMaximum =>
            "clamp-down-to-output-form-maximum",
        SplitReplicationTransitionDefinition
                .ActorReplicationCandidateSnapshotKind
                .PostMovementPreLifecycleReservationSnapshot =>
            "post-movement-pre-lifecycle-reservation-snapshot",
        SplitReplicationTransitionDefinition
                .ActorReplicationPositionSelectionKind
                .FirstEligibleDeclaredOffsets =>
            "first-eligible-declared-offsets",
        SplitReplicationTransitionDefinition.SlotSelectionKind
                .SourceThenLowestCompatibleReadyDormantSameParticipant =>
            "source-then-lowest-compatible-ready-dormant-same-participant",
        SplitReplicationTransitionDefinition.DescendantAssignmentKind
                .ZipSlotOrderToPositionOrder =>
            "zip-slot-order-to-position-order",
        SplitReplicationTransitionDefinition.ActorReplicationClaimScopeKind
                .SelectedSlotsAndTilesOnly =>
            "selected-slots-and-tiles-only",
        SplitReplicationTransitionDefinition
                .ActorReplicationConflictResolutionKind
                .BlockIntersectingClaimComponents =>
            "block-intersecting-claim-components",
        SplitReplicationTransitionDefinition.HealthEvaluationKind
                .CompletionTimeCurrentHealthCancelIfBelowMinimum =>
            "completion-time-current-health-cancel-if-below-minimum",
        SplitReplicationTransitionDefinition.ActorReplicationOffsetArithmeticKind
                .CheckedInt64ThenMapBounds =>
            "checked-int64-then-map-bounds",

        ActorTeamPerceptionDefinition.PerceptionKind.Individual =>
            "individual",
        ActorTeamPerceptionDefinition.PerceptionKind.ImmediateUnion =>
            "immediate-union",
        ActorTeamPerceptionDefinition.SnapshotKind.FrozenPreTickState =>
            "frozen-pre-tick-state",
        ActorTeamPerceptionDefinition.SameTickDecisionSharingKind.None =>
            "none",
        ActorTeamPerceptionDefinition.ObservationProvenanceKind
                .ExactObservedByActorIdentities =>
            "exact-observed-by-actor-identities",
        ActorCollisionDefinition.AlliedProjectileContactKind.PassThrough =>
            "pass-through",
        ActorCollisionDefinition.AlliedProjectileContactKind
                .BlockWithoutDamage =>
            "block-without-damage",
        ActorCollisionDefinition.AlliedProjectileContactKind
                .DamageAndBlock =>
            "damage-and-block",
        ActorCollisionDefinition.MovementResolutionKind
                .ExclusiveGroundOccupancyBlockConnectedConflictingClaims =>
            "exclusive-ground-occupancy-block-connected-conflicting-claims",
        ActorCollisionDefinition.ProjectileTraversalResolutionKind
                .CanonicalProjectileIdOrderedTileSubstepsWallsThenActors =>
            "canonical-projectile-id-ordered-tile-substeps-walls-then-actors",
        ActorCollisionDefinition.ActorProjectileContactTimingKind
                .MovementDestinationContactsThenExistingTraversalThenLaunchPath =>
            "movement-destination-contacts-then-existing-traversal-then-launch-path",
        ActorCollisionDefinition.MovementDestinationProjectileResultKind
                .NonPassingContactBlocksAtOriginConsumesProjectileByIdAndQueuesDamage =>
            "non-passing-contact-blocks-at-origin-consumes-projectile-by-id-and-queues-damage",
        ActorCollisionDefinition.AlliedMovementDestinationOverrideKind
                .PassThroughDoesNotBlockOrConsumeOtherwiseUseContactPolicy =>
            "pass-through-does-not-block-or-consume-otherwise-use-contact-policy",

        ActorDamageResolutionDefinition.ContactBatchKind
                .CollectAllValidContactsBeforeAnyHealthMutation =>
            "collect-all-valid-contacts-before-any-health-mutation",
        ActorDamageResolutionDefinition.PerTargetApplicationOrderKind
                .AttributedSourceTeamUnitLifeThenProjectileIdThenContactOrdinalUnattributedLast =>
            "attributed-source-team-unit-life-then-projectile-id-then-contact-ordinal-unattributed-last",
        ActorDamageResolutionDefinition.ProjectileIdentityAssignmentKind
                .MatchWideInt64StartingAtZeroCanonicalSourceActorThenAttackOrdinal =>
            "match-wide-int64-starting-at-zero-canonical-source-actor-then-attack-ordinal",
        ActorDamageResolutionDefinition.ContactOrdinalAssignmentKind
                .MovementDestinationThenExistingTraversalThenNewLaunchPathSubsteps =>
            "movement-destination-then-existing-traversal-then-new-launch-path-substeps",
        ActorDamageResolutionDefinition.HealthApplicationKind
                .SequentialActualHealthRemovedCappedToRemainingHealth =>
            "sequential-actual-health-removed-capped-to-remaining-health",
        ActorDamageResolutionDefinition.DestructionAttributionKind
                .FirstOrderedContactReducingPositiveHealthToZero =>
            "first-ordered-contact-reducing-positive-health-to-zero",
        ActorDamageResolutionDefinition.EventOrderKind
                .TargetTeamUnitLifeThenPerTargetApplicationOrder =>
            "target-team-unit-life-then-per-target-application-order",
        ActorTickResolutionDefinition.MovementActionResolutionKind
                .SubmittedAbsoluteCardinalOneTileFacingUnchanged =>
            "submitted-absolute-cardinal-one-tile-facing-unchanged",
        ActorTickResolutionDefinition.RotationActionResolutionKind
                .SetFacingToSubmittedAbsoluteCardinalPositionUnchanged =>
            "set-facing-to-submitted-absolute-cardinal-position-unchanged",
        ActorTickResolutionDefinition.ActionAdmissionKind
                .UnknownOrMalformedFaultedOutOfFormRejectedPhysicalBlockedExplicitOverrides =>
            "unknown-or-malformed-faulted-out-of-form-rejected-physical-blocked-explicit-overrides",
        ActorTickResolutionDefinition.ActionFaultCountingKind
                .OnlyFaultedOutcomeIncrementsParticipantCounter =>
            "only-faulted-outcome-increments-participant-counter",
        ActorTickResolutionDefinition.MatchCompletionPrecedenceKind
                .FaultEligibilityShortCircuitThenModeEarlyThenEligibleTimeout =>
            "fault-eligibility-short-circuit-then-mode-early-then-eligible-timeout",
        ActorTickResolutionDefinition.CooldownClockKind
                .AdvancesOnlyWithAnArmedForm =>
            "advances-only-with-an-armed-form",
        ActorTickResolutionDefinition.CooldownClockKind.AdvancesWithTime =>
            "advances-with-time",
        ActorTickResolutionPhase.ResolveTickStartLifecycle =>
            "resolve-tick-start-lifecycle",
        ActorTickResolutionPhase.FreezeObservations => "freeze-observations",
        ActorTickResolutionPhase.CollectJointDecisions =>
            "collect-joint-decisions",
        ActorTickResolutionPhase.ValidateActions => "validate-actions",
        ActorTickResolutionPhase.Rotate => "rotate",
        ActorTickResolutionPhase.Move => "move",
        ActorTickResolutionPhase.ReserveLifecycleActions =>
            "reserve-lifecycle-actions",
        ActorTickResolutionPhase.AdvanceExistingProjectiles =>
            "advance-existing-projectiles",
        ActorTickResolutionPhase.LaunchAttacksAndApplyDamage =>
            "launch-attacks-and-apply-damage",
        ActorTickResolutionPhase.ApplyRuntimeFaults =>
            "apply-runtime-faults",
        ActorTickResolutionPhase.ResolveFaultEligibilityCompletion =>
            "resolve-fault-eligibility-completion",
        ActorTickResolutionPhase.ResolvePostDamageLifecycle =>
            "resolve-post-damage-lifecycle",
        ActorTickResolutionPhase.UpdateCooldownsAndResources =>
            "update-cooldowns-and-resources",
        ActorTickResolutionPhase.UpdateMode => "update-mode",
        ActorTickResolutionPhase.CompleteDueSameLifeTransitions =>
            "complete-due-same-life-transitions",
        ActorTickResolutionPhase.ResolveMatchCompletion =>
            "resolve-match-completion",

        ActorUnitSlotLifecycleAssignmentDefinition.InitialAvailabilityKind
                .ActiveAtTickZero =>
            "active-at-tick-zero",
        ActorUnitSlotLifecycleAssignmentDefinition.InitialAvailabilityKind
                .DormantUnlockAtTick =>
            "dormant-unlock-at-tick",
        ActorUnitSlotLifecycleAssignmentDefinition.InitialAvailabilityKind
                .DormantAutomaticActivationAtTick =>
            "dormant-automatic-activation-at-tick",

        _ => throw new ArgumentOutOfRangeException(
            nameof(value),
            value,
            "The enum value has no canonical generation-3 contract ID."),
    };
}
