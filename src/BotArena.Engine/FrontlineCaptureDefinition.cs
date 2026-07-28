using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// One deterministic point in a Frontline capture-gain schedule. The first
/// phase starts at tick zero; later phases replace the gain from their start
/// tick onward.
/// </summary>
public sealed record FrontlineCaptureGainPhaseDefinition
{
    public FrontlineCaptureGainPhaseDefinition(
        string phaseId,
        int startsAtTick,
        int gainPerSoleTeamTick)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phaseId);
        if (startsAtTick < 0)
            throw new ArgumentOutOfRangeException(nameof(startsAtTick));
        if (gainPerSoleTeamTick <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(gainPerSoleTeamTick));
        }

        PhaseId = phaseId;
        StartsAtTick = startsAtTick;
        GainPerSoleTeamTick = gainPerSoleTeamTick;
    }

    public string PhaseId { get; }
    public int StartsAtTick { get; }
    public int GainPerSoleTeamTick { get; }
}

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
        int redeployPauseTicks,
        IEnumerable<FrontlineCaptureGainPhaseDefinition>? gainSchedule = null)
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
        FrontlineCaptureGainPhaseDefinition[] schedule =
            gainSchedule?.ToArray() ?? [];
        if (schedule.Any(phase => phase is null))
        {
            throw new ArgumentException(
                "Frontline capture gain schedule cannot contain null phases.",
                nameof(gainSchedule));
        }
        schedule = schedule
            .OrderBy(phase => phase.StartsAtTick)
            .ThenBy(phase => phase.PhaseId, StringComparer.Ordinal)
            .ToArray();
        if (schedule.Length > 0
            && (schedule[0].StartsAtTick != 0
                || schedule[0].GainPerSoleTeamTick != gainPerSoleTeamTick))
        {
            throw new ArgumentException(
                "A Frontline capture gain schedule must begin at tick zero with the declared base gain.",
                nameof(gainSchedule));
        }
        if (schedule
                .Select(phase => phase.PhaseId)
                .Distinct(StringComparer.Ordinal)
                .Count()
            != schedule.Length)
        {
            throw new ArgumentException(
                "Frontline capture gain phase IDs must be unique.",
                nameof(gainSchedule));
        }
        if (schedule
                .Select(phase => phase.StartsAtTick)
                .Distinct()
                .Count()
            != schedule.Length)
        {
            throw new ArgumentException(
                "Frontline capture gain phase start ticks must be unique.",
                nameof(gainSchedule));
        }

        Threshold = threshold;
        GainPerSoleTeamTick = gainPerSoleTeamTick;
        DecayAmount = decayAmount;
        DecayIntervalTicks = decayIntervalTicks;
        RedeployPauseTicks = redeployPauseTicks;
        GainSchedule = schedule.ToImmutableArray();
    }

    public int Threshold { get; }
    public int GainPerSoleTeamTick { get; }
    public ImmutableArray<FrontlineCaptureGainPhaseDefinition> GainSchedule
    {
        get;
    }
    public int DecayAmount { get; }
    public int DecayIntervalTicks { get; }
    public int RedeployPauseTicks { get; }

    /// <summary>
    /// Resolves the phase visible at one authoritative tick. Static rulesets
    /// expose a synthetic <c>default</c> phase without changing their
    /// canonical contract bytes.
    /// </summary>
    public FrontlineCaptureGainPhaseDefinition GainPhaseAtTick(int tick)
    {
        if (tick < 0)
            throw new ArgumentOutOfRangeException(nameof(tick));
        if (GainSchedule.IsDefaultOrEmpty)
        {
            return new FrontlineCaptureGainPhaseDefinition(
                "default",
                startsAtTick: 0,
                GainPerSoleTeamTick);
        }

        FrontlineCaptureGainPhaseDefinition active = GainSchedule[0];
        foreach (FrontlineCaptureGainPhaseDefinition phase in GainSchedule)
        {
            if (phase.StartsAtTick > tick)
                break;
            active = phase;
        }
        return active;
    }

    public ControlPolicyKind ControlPolicy =>
        ControlPolicyKind
            .BinaryPositiveWeightPerTeamNoStackingNonSoleAppliesConfiguredDecayOppositionErodesToNeutral;

    public TimeoutPolicyKind TimeoutPolicy =>
        TimeoutPolicyKind
            .SignedPositionThresholdPlusClaimZeroDrawNoTiebreakers;
    public TerritorialProgressFormulaKind TerritorialProgressFormula =>
        TerritorialProgressFormulaKind
            .PerTeamAdvanceDeltaTimesIndexOffsetTimesThresholdPlusSignedClaim;

    public CompletionPolicyKind CompletionPolicy =>
        CompletionPolicyKind.BaseBreachBeforeMaxTicks;

    public FrontlineInitialPositionKind InitialPosition =>
        FrontlineInitialPositionKind.CentreObjectiveIndex;
    public CaptureArithmeticKind CaptureArithmetic =>
        CaptureArithmeticKind
            .CheckedInt64AddCompareThresholdCompletesOnePushAndDiscardsOvershoot;
    public OppositionArithmeticKind OppositionArithmetic =>
        OppositionArithmeticKind
            .ErodeTowardZeroWithoutCarryingOvershootIntoOwnClaim;
    public DecayClockKind DecayClock =>
        DecayClockKind
            .ConsecutiveEmptyOrContestedTicksResetByAnySoleControl;
    public DisabledDecayKind DisabledDecay =>
        DisabledDecayKind.ZeroPairPreservesClaimAndKeepsClockZero;
    public RedeployPolicyKind RedeployPolicy =>
        RedeployPolicyKind
            .AdvanceImmediatelyResetClaimKeepWorldPauseThroughCapturePlusConfiguredTicksBreachSkipsPause;
    public RedeployTickArithmeticKind RedeployTickArithmetic =>
        RedeployTickArithmeticKind
            .CheckedInt64CaptureTickPlusOnePlusPauseRequireInt32;

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

    public enum TerritorialProgressFormulaKind
    {
        /// <summary>
        /// Score each eligible team independently as:
        /// teamAdvanceDelta * (activeObjectiveIndex - centreIndex) * Threshold
        /// plus Claim when that team owns the claim, minus Claim when another
        /// team owns it, or zero when neutral. Claim is a positive magnitude.
        /// Multiplication and addition use checked signed 64-bit arithmetic.
        /// Higher is always progress toward that team's opposing base.
        /// </summary>
        PerTeamAdvanceDeltaTimesIndexOffsetTimesThresholdPlusSignedClaim = 0,
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
        /// <summary>
        /// Add current claim and gain in checked signed 64-bit arithmetic,
        /// compare that value to the Int32 threshold, and either store the
        /// still-in-range claim or complete exactly one push. A completed push
        /// resets claim to zero and discards all overshoot.
        /// </summary>
        CheckedInt64AddCompareThresholdCompletesOnePushAndDiscardsOvershoot = 0,
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

    public enum RedeployPolicyKind
    {
        /// <summary>
        /// A non-breaching capture advances the active objective immediately,
        /// resets claimant, claim, and decay clock, and leaves every actor and
        /// projectile unchanged. Ignore objective control through tick
        /// captureTick + RedeployPauseTicks; control resumes at
        /// captureTick + 1 + RedeployPauseTicks. A base breach ends immediately
        /// and never enters redeploy pause.
        /// </summary>
        AdvanceImmediatelyResetClaimKeepWorldPauseThroughCapturePlusConfiguredTicksBreachSkipsPause
            = 0,
    }

    public enum RedeployTickArithmeticKind
    {
        /// <summary>
        /// Compute captureTick + 1 + RedeployPauseTicks in checked signed
        /// 64-bit arithmetic and require the result to fit the engine's signed
        /// 32-bit tick representation before admitting the ruleset.
        /// </summary>
        CheckedInt64CaptureTickPlusOnePlusPauseRequireInt32 = 0,
    }
}
