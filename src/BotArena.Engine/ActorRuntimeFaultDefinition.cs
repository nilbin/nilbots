namespace BotArena.Engine;

/// <summary>
/// Participant-owned runtime-fault semantics for variable actor counts. The
/// same bot artifact may control several simultaneous lives, so fault state is
/// never inferred from a body index.
/// </summary>
public sealed record ActorRuntimeFaultDefinition
{
    public ActorRuntimeFaultDefinition(int faultsAllowedBeforeDisqualification)
    {
        if (faultsAllowedBeforeDisqualification < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(faultsAllowedBeforeDisqualification));
        }

        FaultsAllowedBeforeDisqualification =
            faultsAllowedBeforeDisqualification;
    }

    public int FaultsAllowedBeforeDisqualification { get; }
    public long DisqualificationFaultCount =>
        (long)FaultsAllowedBeforeDisqualification + 1;
    public AccumulationScopeKind AccumulationScope =>
        AccumulationScopeKind
            .ParticipantAcrossAllSlotsLivesAndRuntimeStages;
    public FaultCounterArithmeticKind FaultCounterArithmetic =>
        FaultCounterArithmeticKind
            .SignedInt64SaturatingAtAllowedPlusOne;
    public FaultingDecisionKind FaultingDecision =>
        FaultingDecisionKind.ReplaceExactActorDecisionWithWait;
    public RuntimeStageRecoveryKind RuntimeStageRecovery =>
        RuntimeStageRecoveryKind
            .CreateStartOrExecuteFailureDiscardsInstanceSyntheticWaitRetryFreshOnceNextActiveTick;
    public ReplayFaultRepresentationKind ReplayFaultRepresentation =>
        ReplayFaultRepresentationKind
            .StageTaggedHostFaultNoRuntimeReplyAcceptedSyntheticWait;
    public FaultBatchEventOrderKind FaultBatchEventOrder =>
        FaultBatchEventOrderKind
            .ParticipantThenActorIdentityThenCreateStartTickValidationStage;
    public ApplicationTimingKind ApplicationTiming =>
        ApplicationTimingKind
            .AfterDamageBeforeModeUpdateUsingCompleteJointFaultBatch;
    public ThresholdKind Threshold =>
        ThresholdKind
            .DisqualifyWhenCumulativeCountExceedsAllowedCount;
    public ParticipantDispositionKind ParticipantDisposition =>
        ParticipantDispositionKind
            .RetireAllActiveLivesAndPermanentlyDormantAllOwnedSlots;
    public PendingWorkDispositionKind PendingWorkDisposition =>
        PendingWorkDispositionKind
            .CancelAllOwnedClocksBundlesAndTransitionsReleaseEveryClaim;
    public CancellationEventOrderKind CancellationEventOrder =>
        CancellationEventOrderKind
            .ClocksByTargetSlotThenBundlesByFamilySourceTransitionAndTarget;
    public OwnedProjectileDispositionKind OwnedProjectileDisposition =>
        OwnedProjectileDispositionKind
            .RemoveAfterJointDamageByProjectileIdWithoutContactOrScore;
    public ScoreDispositionKind ScoreDisposition =>
        ScoreDispositionKind.RetirementAddsNoKillOrDeath;
    public ScoringTeamEligibilityKind ScoringTeamEligibility =>
        ScoringTeamEligibilityKind
            .EligibleWhileAnyNonDisqualifiedParticipantRemains;
    public MatchCompletionKind MatchCompletion =>
        MatchCompletionKind
            .AfterFaultPhaseOneEligibleTeamWinsZeroEligibleTeamsDraw;
    public FinalRankingKind FinalRanking =>
        FinalRankingKind
            .IneligibleTeamsRankBelowEveryEligibleTeamAndTieAtBottom;

    public enum AccumulationScopeKind
    {
        /// <summary>
        /// Runtime creation, life startup, tick execution, and decision
        /// validation faults increment one participant counter shared by all
        /// current and future lives controlled by that participant.
        /// </summary>
        ParticipantAcrossAllSlotsLivesAndRuntimeStages = 0,
    }

    public enum FaultCounterArithmeticKind
    {
        /// <summary>
        /// Count in signed 64-bit arithmetic and stop at
        /// FaultsAllowedBeforeDisqualification + 1. This remains representable
        /// even when the configured allowance is Int32.MaxValue.
        /// </summary>
        SignedInt64SaturatingAtAllowedPlusOne = 0,
    }

    public enum FaultingDecisionKind
    {
        ReplaceExactActorDecisionWithWait = 0,
    }

    public enum RuntimeStageRecoveryKind
    {
        /// <summary>
        /// A creation, StartLife, or tick-execution failure discards the
        /// runtime. The authoritative life stays active and submits a
        /// synthetic Wait for that tick. If still eligible, the host makes
        /// exactly one fresh create-and-start attempt before that life's next
        /// active decision opportunity, using the original life start and
        /// deterministic seed. No partial runtime memory survives. A
        /// decision-validation fault retains the otherwise healthy instance.
        /// </summary>
        CreateStartOrExecuteFailureDiscardsInstanceSyntheticWaitRetryFreshOnceNextActiveTick
            = 0,
    }

    public enum ReplayFaultRepresentationKind
    {
        /// <summary>
        /// Create/start failures have no fabricated runtime reply. Replay
        /// records a stage-tagged host fault and the accepted synthetic Wait.
        /// Tick-execution failures likewise have no accepted runtime reply;
        /// validation failures retain the malformed submitted reply. Both
        /// record Wait as the accepted decision.
        /// </summary>
        StageTaggedHostFaultNoRuntimeReplyAcceptedSyntheticWait = 0,
    }

    public enum FaultBatchEventOrderKind
    {
        /// <summary>
        /// Sort the complete joint batch by participant ID, then actor
        /// team/unit/life identity, then the fixed stage order runtime-create,
        /// StartLife, tick-execution, decision-validation. Host callback or
        /// collection arrival order is never replay order.
        /// </summary>
        ParticipantThenActorIdentityThenCreateStartTickValidationStage = 0,
    }

    public enum ApplicationTimingKind
    {
        /// <summary>
        /// Collect the complete joint tick's faults. Faulting actors submit
        /// Wait, damage resolves, then participant disqualifications are
        /// applied before the mode update.
        /// </summary>
        AfterDamageBeforeModeUpdateUsingCompleteJointFaultBatch = 0,
    }

    public enum ThresholdKind
    {
        /// <summary>
        /// Zero means no faults are tolerated; the first fault disqualifies.
        /// </summary>
        DisqualifyWhenCumulativeCountExceedsAllowedCount = 0,
    }

    public enum ParticipantDispositionKind
    {
        /// <summary>
        /// All active lives owned by the participant retire without
        /// destruction and every owned stable slot becomes permanently
        /// dormant. A life already reduced to zero by the completed damage
        /// batch still receives its attributed damage-destruction facts, but
        /// no automatic return is scheduled for its now-dormant slot. Other
        /// participants on the scoring team may continue.
        /// </summary>
        RetireAllActiveLivesAndPermanentlyDormantAllOwnedSlots = 0,
    }

    public enum PendingWorkDispositionKind
    {
        /// <summary>
        /// Cancel every owned initial-unlock, automatic-return, rebuild,
        /// fabrication, replication, and same-life transition before later
        /// lifecycle phases can run. Each cancellation releases all target
        /// slot and tile claims. No cancelled operation may recreate an owned
        /// life after disqualification.
        /// </summary>
        CancelAllOwnedClocksBundlesAndTransitionsReleaseEveryClaim = 0,
    }

    public enum CancellationEventOrderKind
    {
        /// <summary>
        /// Emit clock cancellations by target team/unit. Then emit
        /// fabrication, replication, and same-life cancellations in that
        /// family order, each by source team/unit/life, transition ID, and
        /// target unit where present. Release claims with their cancellation,
        /// then retire active lives in actor-identity order.
        /// </summary>
        ClocksByTargetSlotThenBundlesByFamilySourceTransitionAndTarget = 0,
    }

    public enum OwnedProjectileDispositionKind
    {
        /// <summary>
        /// Contacts already collected in the joint damage batch remain valid.
        /// After that damage and owned-life retirement, remove every surviving
        /// projectile fired by the participant in projectile-ID order. Removal
        /// creates no contact, damage, kill, or death.
        /// </summary>
        RemoveAfterJointDamageByProjectileIdWithoutContactOrScore = 0,
    }

    public enum ScoreDispositionKind
    {
        RetirementAddsNoKillOrDeath = 0,
    }

    public enum ScoringTeamEligibilityKind
    {
        /// <summary>
        /// One participant faulting out does not eliminate a multi-participant
        /// team. A scoring team becomes ineligible only when none of its
        /// participants remain eligible.
        /// </summary>
        EligibleWhileAnyNonDisqualifiedParticipantRemains = 0,
    }

    public enum MatchCompletionKind
    {
        /// <summary>
        /// After the joint fault phase, exactly one eligible scoring team wins
        /// immediately; zero eligible teams draw. This result short-circuits
        /// every later tick phase. With two or more eligible teams, normal
        /// mode play continues and every mode terminal or timeout comparison
        /// considers eligible teams only.
        /// </summary>
        AfterFaultPhaseOneEligibleTeamWinsZeroEligibleTeamsDraw = 0,
    }

    public enum FinalRankingKind
    {
        /// <summary>
        /// A fully disqualified team cannot win by retaining an earlier score.
        /// Eligible teams alone are ranked by the mode; ineligible teams are
        /// placed below all of them and tie with one another at bottom.
        /// </summary>
        IneligibleTeamsRankBelowEveryEligibleTeamAndTieAtBottom = 0,
    }
}
