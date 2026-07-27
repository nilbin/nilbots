namespace BotArena.Engine;

/// <summary>
/// Rules-owned Frontline pressure, decay, and redeploy tuning. Its typed fixed
/// policies keep sole control, timeout ranking, and completion precedence
/// explicit rather than hiding them in a mode implementation.
/// </summary>
public sealed record FrontlineCaptureDefinition
{
    public FrontlineCaptureDefinition(
        int threshold,
        int gainPerSoleTeamTick,
        int decayAmount,
        int decayIntervalTicks,
        int redeployPauseTicks)
    {
        if (threshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(threshold));
        if (gainPerSoleTeamTick <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(gainPerSoleTeamTick));
        }
        bool decayDisabled = decayAmount == 0 && decayIntervalTicks == 0;
        bool decayEnabled = decayAmount > 0 && decayIntervalTicks > 0;
        if (!decayDisabled && !decayEnabled)
        {
            throw new ArgumentException(
                "Frontline decay amount and interval must both be zero to disable decay, or both be positive.",
                nameof(decayAmount));
        }
        if (redeployPauseTicks < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(redeployPauseTicks));
        }

        Threshold = threshold;
        GainPerSoleTeamTick = gainPerSoleTeamTick;
        DecayAmount = decayAmount;
        DecayIntervalTicks = decayIntervalTicks;
        RedeployPauseTicks = redeployPauseTicks;
    }

    public int Threshold { get; }
    public int GainPerSoleTeamTick { get; }
    public int DecayAmount { get; }
    public int DecayIntervalTicks { get; }
    public int RedeployPauseTicks { get; }

    public ControlPolicyKind ControlPolicy =>
        ControlPolicyKind
            .BinaryPositiveWeightPerTeamNoStackingNonSoleAppliesConfiguredDecayOppositionErodesToNeutral;

    public TimeoutPolicyKind TimeoutPolicy =>
        TimeoutPolicyKind
            .SignedPositionThresholdPlusClaimZeroDrawNoTiebreakers;

    public CompletionPolicyKind CompletionPolicy =>
        CompletionPolicyKind.BaseBreachBeforeMaxTicks;

    public FrontlineInitialPositionKind InitialPosition =>
        FrontlineInitialPositionKind.CentreObjectiveIndex;
    public CaptureArithmeticKind CaptureArithmetic =>
        CaptureArithmeticKind
            .CheckedAddThresholdCompletesOnePushAndDiscardsOvershoot;
    public OppositionArithmeticKind OppositionArithmetic =>
        OppositionArithmeticKind
            .ErodeTowardZeroWithoutCarryingOvershootIntoOwnClaim;
    public DecayClockKind DecayClock =>
        DecayClockKind
            .ConsecutiveEmptyOrContestedTicksResetByAnySoleControl;
    public DisabledDecayKind DisabledDecay =>
        DisabledDecayKind.ZeroPairPreservesClaimAndKeepsClockZero;

    public enum ControlPolicyKind
    {
        /// <summary>
        /// Any positive objective weight contributes one team presence.
        /// Additional bodies do not stack. Empty or contested presence decays
        /// the current claim; sole opposition first erodes it to neutral.
        /// </summary>
        BinaryPositiveWeightPerTeamNoStackingNonSoleAppliesConfiguredDecayOppositionErodesToNeutral = 0,
    }

    public enum TimeoutPolicyKind
    {
        SignedPositionThresholdPlusClaimZeroDrawNoTiebreakers = 0,
    }

    public enum CompletionPolicyKind
    {
        BaseBreachBeforeMaxTicks = 0,
    }

    public enum FrontlineInitialPositionKind
    {
        CentreObjectiveIndex = 0,
    }

    public enum CaptureArithmeticKind
    {
        CheckedAddThresholdCompletesOnePushAndDiscardsOvershoot = 0,
    }

    public enum OppositionArithmeticKind
    {
        ErodeTowardZeroWithoutCarryingOvershootIntoOwnClaim = 0,
    }

    public enum DecayClockKind
    {
        /// <summary>
        /// While a claim exists, each consecutive empty or contested tick
        /// advances the decay clock. At the interval, subtract once and reset
        /// the clock. Any sole-control tick resets it, including opposition.
        /// No claimant always has clock zero.
        /// </summary>
        ConsecutiveEmptyOrContestedTicksResetByAnySoleControl = 0,
    }

    public enum DisabledDecayKind
    {
        ZeroPairPreservesClaimAndKeepsClockZero = 0,
    }
}
