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
/// How hostile damage taken by the controlling team interrupts the ground it
/// is currently taking. The whole block is absent — and writes no canonical
/// bytes at all — on every ruleset that does not declare a capture channel,
/// which is what keeps every historical fingerprint byte-exact.
/// </summary>
/// <param name="Kind">Exact interrupt mechanism.</param>
/// <param name="RevertPerDamagePoint">
/// Work reverted per point of actual health removed. One under the shipped
/// channel: the damage amount is already authoritative and already published
/// per projectile, so the interrupt scales with the weapon for free.
/// </param>
/// <param name="Scope">Which bodies' damage reverts anything.</param>
/// <param name="Granularity">
/// Whether one hit reverts the controller's whole run or only the hit body's
/// contribution.
/// </param>
public sealed record FrontlineClaimInterruptDefinition(
    FrontlineClaimInterruptDefinition.ClaimInterruptKind Kind,
    int RevertPerDamagePoint,
    FrontlineClaimInterruptDefinition.ClaimInterruptScopeKind Scope,
    FrontlineClaimInterruptDefinition.ClaimInterruptGranularityKind
        Granularity)
{
    /// <summary>The shipped channel interrupt, spelled out.</summary>
    public static FrontlineClaimInterruptDefinition DamageRevertsWork { get; } =
        new(
            ClaimInterruptKind.DamageToControllerOnObjectiveRevertsWork,
            RevertPerDamagePoint: 1,
            ClaimInterruptScopeKind
                .ControllingTeamBodiesOnActiveObjectiveRegion,
            ClaimInterruptGranularityKind.WholeRun);

    public enum ClaimInterruptKind
    {
        /// <summary>
        /// Hostile damage taken by a controlling-team body inside the scope
        /// reverts the controller's work on THIS RUN by the damage amount,
        /// floored at zero work. Reverting work rather than the raw claim is
        /// what makes it impossible for being shot to complete a capture for
        /// the team doing the shooting: a full revert restores the position
        /// exactly as the controller found it and never goes past it.
        /// </summary>
        DamageToControllerOnObjectiveRevertsWork = 0,
    }

    public enum ClaimInterruptScopeKind
    {
        /// <summary>
        /// Only bodies of the controlling team standing on the active
        /// objective region. Damage absorbed off the objective reverts
        /// nothing, which is the single scoping choice that makes escorting a
        /// channeler the intended play: a screen absorbs bolts for free.
        /// </summary>
        ControllingTeamBodiesOnActiveObjectiveRegion = 0,
    }

    public enum ClaimInterruptGranularityKind
    {
        /// <summary>
        /// One hit reverts the controller's whole run, whichever in-scope body
        /// took it. Per-body reverting is degenerate under a capped
        /// multiplier — with three channelers and a cap of two, hitting one
        /// body leaves the multiplier at two and the hit has no effect at all
        /// — so it is a registered alternative rather than a shipped value.
        /// </summary>
        WholeRun = 0,
    }
}

/// <summary>
/// Rules-owned Frontline pressure, decay, and redeploy tuning. Its typed fixed
/// policies keep objective control, timeout ranking, and completion
/// precedence explicit rather than hiding them in a mode implementation.
/// </summary>
public sealed record FrontlineCaptureDefinition
{
    public FrontlineCaptureDefinition(
        int threshold,
        int gainPerSoleTeamTick,
        int decayAmount,
        int decayIntervalTicks,
        int redeployPauseTicks,
        IEnumerable<FrontlineCaptureGainPhaseDefinition>? gainSchedule = null,
        ControlPolicyKind controlPolicy =
            ControlPolicyKind
                .BinaryPositiveWeightPerTeamNoStackingNonSoleAppliesConfiguredDecayOppositionErodesToNeutral,
        DecayClockKind decayClock =
            DecayClockKind
                .ConsecutiveEmptyOrContestedTicksResetByAnySoleControl,
        RedeployPolicyKind redeployPolicy =
            RedeployPolicyKind
                .AdvanceImmediatelyResetClaimKeepWorldPauseThroughCapturePlusConfiguredTicksBreachSkipsPause,
        int ratchetHoldTicks = 0,
        int stationaryGainMultiplierCap = 0,
        int opposingErosionMultiplier = 0,
        FrontlineClaimInterruptDefinition? claimInterrupt = null)
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
        if (!Enum.IsDefined(controlPolicy))
            throw new ArgumentOutOfRangeException(nameof(controlPolicy));
        if (!Enum.IsDefined(decayClock))
            throw new ArgumentOutOfRangeException(nameof(decayClock));
        if (!Enum.IsDefined(redeployPolicy))
            throw new ArgumentOutOfRangeException(nameof(redeployPolicy));
        bool ratchet = redeployPolicy
            == RedeployPolicyKind
                .AdvanceImmediatelyThenDenyEnemyRegressionPastTheHighWaterMarkThroughConfiguredHoldTicks;
        if (ratchet ? ratchetHoldTicks <= 0 : ratchetHoldTicks != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ratchetHoldTicks),
                ratchetHoldTicks,
                "A hold duration is positive exactly when the redeploy policy holds a high-water mark; every other policy leaves it inert at zero.");
        }
        // The channel's three settings travel with the channel policy and
        // with nothing else: they are one coherent change to how ground is
        // taken and kept, so declaring any of them without the policy — or
        // the policy without them — is a contract that cannot be resolved.
        // Every other control policy leaves all three inert and absent, which
        // is what keeps historical rulesets byte-exact.
        bool channel = controlPolicy
            == ControlPolicyKind
                .StationaryClaimWeightVersusTotalDenialWeightScalesGainCappedOppositionErodesAtMultipleThenBuilds;
        bool channelDeclared =
            stationaryGainMultiplierCap > 0
            && opposingErosionMultiplier > 0
            && claimInterrupt is not null;
        bool channelInert =
            stationaryGainMultiplierCap == 0
            && opposingErosionMultiplier == 0
            && claimInterrupt is null;
        if (channel ? !channelDeclared : !channelInert)
        {
            throw new ArgumentException(
                "A stationary multiplier cap, an opposing erosion multiplier, and a claim interrupt are declared together exactly when the control policy channels a capture; every other policy leaves all three inert and absent.",
                nameof(controlPolicy));
        }
        if (claimInterrupt is not null
            && (!Enum.IsDefined(claimInterrupt.Kind)
                || !Enum.IsDefined(claimInterrupt.Scope)
                || !Enum.IsDefined(claimInterrupt.Granularity)
                || claimInterrupt.RevertPerDamagePoint <= 0))
        {
            throw new ArgumentException(
                "A Frontline claim interrupt names declared policies and reverts a positive amount per damage point.",
                nameof(claimInterrupt));
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
        ControlPolicy = controlPolicy;
        DecayClock = decayClock;
        RedeployPolicy = redeployPolicy;
        RatchetHoldTicks = ratchetHoldTicks;
        StationaryGainMultiplierCap = stationaryGainMultiplierCap;
        OpposingErosionMultiplier = opposingErosionMultiplier;
        ClaimInterrupt = claimInterrupt;
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
    /// How long a completed advance holds its position against enemy
    /// regression. Inert (zero) for every policy except
    /// <see cref="RedeployPolicyKind.AdvanceImmediatelyThenDenyEnemyRegressionPastTheHighWaterMarkThroughConfiguredHoldTicks"/>,
    /// and canonical bytes omit it while it is inert.
    /// </summary>
    public int RatchetHoldTicks { get; }

    /// <summary>
    /// The largest gain multiplier stationary surplus can buy. Inert (zero)
    /// for every control policy except the channel, and canonical bytes omit
    /// it while it is inert. The cap is what simultaneously honours "more
    /// bodies capture faster" and denies a body-count class a stacking
    /// payoff for its third, fourth, and fifth channeler: those bodies buy
    /// screens and denial weight, which is positional, not arithmetic.
    /// </summary>
    public int StationaryGainMultiplierCap { get; }

    /// <summary>
    /// How many times faster an opposing claim erodes than a fresh claim
    /// builds. Inert (zero) for every control policy except the channel, and
    /// canonical bytes omit it while it is inert. It is the one integer that
    /// prices recapture: flipping a standing claim costs
    /// <c>ceil(claim / (multiplier * gain)) + threshold</c> ticks against a
    /// fresh capture's <c>threshold</c>.
    /// </summary>
    public int OpposingErosionMultiplier { get; }

    /// <summary>
    /// How hostile damage interrupts the controller, or null when no
    /// interrupt is declared at all. Canonical bytes omit the whole block
    /// while it is absent.
    /// </summary>
    public FrontlineClaimInterruptDefinition? ClaimInterrupt { get; }

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

    public ControlPolicyKind ControlPolicy { get; }

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
    public DecayClockKind DecayClock { get; }
    public DisabledDecayKind DisabledDecay =>
        DisabledDecayKind.ZeroPairPreservesClaimAndKeepsClockZero;
    public RedeployPolicyKind RedeployPolicy { get; }
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

        /// <summary>
        /// Sum positive form objective weights per team. Equal weights are
        /// contested; otherwise the higher-weight team applies capture gain
        /// multiplied by the exact weight difference. Opposition still
        /// erodes only to neutral and discards overshoot.
        /// </summary>
        NetPositiveObjectiveWeightDifferenceScalesGainNonPositiveAppliesConfiguredDecayOppositionErodesToNeutral
            = 1,

        /// <summary>
        /// The capture channel. A team's CLAIM weight sums the positive form
        /// objective weights of its bodies that did not change tile this
        /// tick; its DENIAL weight sums all of them. The team whose claim
        /// weight strictly exceeds the opponent's denial weight controls,
        /// with the gain multiplied by that difference and capped at
        /// <see cref="StationaryGainMultiplierCap"/>; otherwise no team
        /// controls and the declared decay clock applies. While an opposing
        /// claim stands the controller erodes it at
        /// <see cref="OpposingErosionMultiplier"/> times gain, still only to
        /// neutral and still without carrying overshoot into its own claim;
        /// with no opposing claim it builds at gain. The declared
        /// <see cref="ClaimInterrupt"/> then reverts the controller's work on
        /// the current run.
        /// <para>Stillness is positional, not intentional: a body that
        /// requested a move and was blocked did not move, and rotating,
        /// shooting, or starting a transform never breaks it. A life with no
        /// previous position counts as stationary.</para>
        /// </summary>
        StationaryClaimWeightVersusTotalDenialWeightScalesGainCappedOppositionErodesAtMultipleThenBuilds
            = 2,
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

        /// <summary>
        /// Empty and contested ticks preserve the claim exactly and keep the
        /// decay clock at zero, so the only way a claim falls is the control
        /// policy's sole-opposition erosion — an enemy body standing alone on
        /// the objective. The declared decay amount and interval stay part of
        /// the ruleset and are what the baseline clock consumes; selecting
        /// this clock therefore differs from the baseline in exactly one
        /// canonical field, which is what makes it a measurable A/B arm
        /// rather than a retuning to the disabled zero pair.
        /// </summary>
        EmptyAndContestedTicksPreserveClaimEnemySoleErosionOnly = 1,
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

        /// <summary>
        /// Everything the baseline policy does, plus a high-water mark: a
        /// completed advance records the reached position, the advancing
        /// team, and a hold through captureTick + RatchetHoldTicks. While
        /// that hold stands, an enemy capture that would move the frontline
        /// back across the mark is denied — it resets claimant, claim, and
        /// decay clock exactly like a completed capture, but the objective
        /// does not move, no advance fact is emitted, and no redeploy pause
        /// begins, because nothing redeployed. The enemy must pay the
        /// threshold again after the hold expires. The holding team's own
        /// captures always convert and re-arm the mark forward; a base
        /// breach is terminal and is never denied.
        /// </summary>
        AdvanceImmediatelyThenDenyEnemyRegressionPastTheHighWaterMarkThroughConfiguredHoldTicks
            = 1,
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
