namespace BotArena.Engine.Tests;

public sealed class FrontlineCaptureDefinitionTests
{
    [Fact]
    public void ExposesFixedPoliciesAndEnabledOrDisabledDecay()
    {
        FrontlineCaptureDefinition enabled = Capture(
            decayAmount: 2,
            decayIntervalTicks: 4);
        FrontlineCaptureDefinition disabled = Capture(
            decayAmount: 0,
            decayIntervalTicks: 0);

        Assert.Equal(10, enabled.Threshold);
        Assert.Equal(1, enabled.GainPerSoleTeamTick);
        Assert.Equal(3, enabled.RedeployPauseTicks);
        Assert.Equal(
            FrontlineCaptureDefinition.ControlPolicyKind
                .BinaryPositiveWeightPerTeamNoStackingNonSoleAppliesConfiguredDecayOppositionErodesToNeutral,
            enabled.ControlPolicy);
        Assert.Equal(
            FrontlineCaptureDefinition.TimeoutPolicyKind
                .SignedPositionThresholdPlusClaimZeroDrawNoTiebreakers,
            enabled.TimeoutPolicy);
        Assert.Equal(
            FrontlineCaptureDefinition.TerritorialProgressFormulaKind
                .PerTeamAdvanceDeltaTimesIndexOffsetTimesThresholdPlusSignedClaim,
            enabled.TerritorialProgressFormula);
        Assert.Equal(
            FrontlineCaptureDefinition.CompletionPolicyKind
                .BaseBreachBeforeMaxTicks,
            enabled.CompletionPolicy);
        Assert.Equal(
            FrontlineCaptureDefinition.FrontlineInitialPositionKind
                .CentreObjectiveIndex,
            enabled.InitialPosition);
        Assert.Equal(
            FrontlineCaptureDefinition.CaptureArithmeticKind
                .CheckedInt64AddCompareThresholdCompletesOnePushAndDiscardsOvershoot,
            enabled.CaptureArithmetic);
        Assert.Equal(
            FrontlineCaptureDefinition.DecayClockKind
                .ConsecutiveEmptyOrContestedTicksResetByAnySoleControl,
            enabled.DecayClock);
        Assert.Equal(
            FrontlineCaptureDefinition.DisabledDecayKind
                .ZeroPairPreservesClaimAndKeepsClockZero,
            disabled.DisabledDecay);
        Assert.Equal(
            FrontlineCaptureDefinition.RedeployPolicyKind
                .AdvanceImmediatelyResetClaimKeepWorldPauseThroughCapturePlusConfiguredTicksBreachSkipsPause,
            enabled.RedeployPolicy);
        Assert.Equal(
            FrontlineCaptureDefinition.RedeployTickArithmeticKind
                .CheckedInt64CaptureTickPlusOnePlusPauseRequireInt32,
            enabled.RedeployTickArithmetic);
        Assert.Equal(0, disabled.DecayAmount);
        Assert.Equal(0, disabled.DecayIntervalTicks);
    }

    [Fact]
    public void RejectsInvalidCaptureAndPartialDecayValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Capture(threshold: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Capture(gainPerSoleTeamTick: 0));
        Assert.Throws<ArgumentException>(() =>
            Capture(decayAmount: 0, decayIntervalTicks: 1));
        Assert.Throws<ArgumentException>(() =>
            Capture(decayAmount: 1, decayIntervalTicks: 0));
        Assert.Throws<ArgumentException>(() =>
            Capture(decayAmount: -1, decayIntervalTicks: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Capture(redeployPauseTicks: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Capture(
                controlPolicy:
                    (FrontlineCaptureDefinition.ControlPolicyKind)99));
    }

    [Fact]
    public void GainScheduleIsCanonicalAndResolvesByAuthoritativeTick()
    {
        FrontlineCaptureDefinition capture = Capture(
            gainSchedule:
            [
                new("late", startsAtTick: 300, gainPerSoleTeamTick: 2),
                new("opening", startsAtTick: 0, gainPerSoleTeamTick: 1),
            ]);

        Assert.Equal(
            ["opening", "late"],
            capture.GainSchedule.Select(phase => phase.PhaseId));
        Assert.Equal("opening", capture.GainPhaseAtTick(299).PhaseId);
        Assert.Equal(1, capture.GainPhaseAtTick(299).GainPerSoleTeamTick);
        Assert.Equal("late", capture.GainPhaseAtTick(300).PhaseId);
        Assert.Equal(2, capture.GainPhaseAtTick(500).GainPerSoleTeamTick);
        Assert.Equal("default", Capture().GainPhaseAtTick(9).PhaseId);

        Assert.Throws<ArgumentException>(() =>
            Capture(gainSchedule:
            [
                new("late", startsAtTick: 1, gainPerSoleTeamTick: 2),
            ]));
        Assert.Throws<ArgumentException>(() =>
            Capture(gainSchedule:
            [
                new("opening", startsAtTick: 0, gainPerSoleTeamTick: 2),
            ]));
        Assert.Throws<ArgumentException>(() =>
            Capture(gainSchedule:
            [
                new("same", startsAtTick: 0, gainPerSoleTeamTick: 1),
                new("same", startsAtTick: 2, gainPerSoleTeamTick: 2),
            ]));
        Assert.Throws<ArgumentException>(() =>
            Capture(gainSchedule:
            [
                new("opening", startsAtTick: 0, gainPerSoleTeamTick: 1),
                new("late", startsAtTick: 0, gainPerSoleTeamTick: 2),
            ]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Capture().GainPhaseAtTick(-1));
    }

    [Fact]
    public void FrontlineModeRequiresCaptureAndMatchingBreachGeometry()
    {
        FrontlineVictoryDefinition victory = Victory(pushesToBreach: 3);
        ScoreChannelDefinition[] catalog =
        [
            new(
                ScoreChannelDefinition.ChannelKind.TerritorialProgress),
        ];
        FrontlineCaptureDefinition capture = Capture();
        var mode = new FrontlineGameModeDefinition(
            victory,
            [.. catalog],
            frontlinePositionCount: 5,
            capture);

        Assert.Same(capture, mode.Capture);
        Assert.Equal(
            ScoreChannelDefinition.ChannelKind.TerritorialProgress,
            mode.ScoreCatalog.Single().Channel);
        Assert.Equal(
            ScoreChannelDefinition.ChannelKind.TerritorialProgress,
            mode.FrontlineVictory.TimeoutRanking.Single().Channel);
        Assert.Throws<ArgumentNullException>(() =>
            new FrontlineGameModeDefinition(
                victory,
                [.. catalog],
                frontlinePositionCount: 5,
                capture: null!));
        Assert.Throws<ArgumentException>(() =>
            new FrontlineGameModeDefinition(
                Victory(pushesToBreach: 4),
                [.. catalog],
                frontlinePositionCount: 5,
                capture: Capture()));
    }

    private static FrontlineCaptureDefinition Capture(
        int threshold = 10,
        int gainPerSoleTeamTick = 1,
        int decayAmount = 1,
        int decayIntervalTicks = 2,
        int redeployPauseTicks = 3,
        IEnumerable<FrontlineCaptureGainPhaseDefinition>?
            gainSchedule = null,
        FrontlineCaptureDefinition.ControlPolicyKind controlPolicy =
            FrontlineCaptureDefinition.ControlPolicyKind
                .BinaryPositiveWeightPerTeamNoStackingNonSoleAppliesConfiguredDecayOppositionErodesToNeutral) =>
        new(
            threshold,
            gainPerSoleTeamTick,
            decayAmount,
            decayIntervalTicks,
            redeployPauseTicks,
            gainSchedule,
            controlPolicy);

    private static FrontlineVictoryDefinition Victory(int pushesToBreach) =>
        new(
            pushesToBreach,
            [
                new ScoreRankingDefinition(
                    ScoreChannelDefinition.ChannelKind
                        .TerritorialProgress,
                    ScoreRankingDefinition.SortDirection.HigherWins),
            ]);
}
