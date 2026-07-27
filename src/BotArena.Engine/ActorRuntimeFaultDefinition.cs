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
    public AccumulationScopeKind AccumulationScope =>
        AccumulationScopeKind
            .ParticipantAcrossAllSlotsLivesAndRuntimeStages;
    public FaultingDecisionKind FaultingDecision =>
        FaultingDecisionKind.ReplaceExactActorDecisionWithWait;
    public ApplicationTimingKind ApplicationTiming =>
        ApplicationTimingKind
            .AfterDamageBeforeModeUpdateUsingCompleteJointFaultBatch;
    public ThresholdKind Threshold =>
        ThresholdKind
            .DisqualifyWhenCumulativeCountExceedsAllowedCount;
    public ParticipantDispositionKind ParticipantDisposition =>
        ParticipantDispositionKind
            .RetireAllActiveLivesAndPermanentlyDormantAllOwnedSlots;
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

    public enum FaultingDecisionKind
    {
        ReplaceExactActorDecisionWithWait = 0,
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
        /// dormant. Other participants on the scoring team may continue.
        /// </summary>
        RetireAllActiveLivesAndPermanentlyDormantAllOwnedSlots = 0,
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
        /// immediately; zero eligible teams draw. With two or more eligible
        /// teams, normal mode play continues.
        /// </summary>
        AfterFaultPhaseOneEligibleTeamWinsZeroEligibleTeamsDraw = 0,
    }

    public enum FinalRankingKind
    {
        /// <summary>
        /// A fully disqualified team cannot win by retaining an earlier score.
        /// Eligible teams are ranked by the mode; ineligible teams are placed
        /// below all of them.
        /// </summary>
        IneligibleTeamsRankBelowEveryEligibleTeamAndTieAtBottom = 0,
    }
}
