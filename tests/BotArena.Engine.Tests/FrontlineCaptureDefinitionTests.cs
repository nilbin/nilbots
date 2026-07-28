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
        int redeployPauseTicks = 3) =>
        new(
            threshold,
            gainPerSoleTeamTick,
            decayAmount,
            decayIntervalTicks,
            redeployPauseTicks);

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
