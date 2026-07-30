using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Explicit generic tick ordering. Generation 3 discloses one supported
/// ordering rather than pretending arbitrary phase permutations are safe.
/// </summary>
public sealed record ActorTickResolutionDefinition
{
    private static readonly ImmutableArray<ActorTickResolutionPhase>
        SupportedPhases =
        [
            ActorTickResolutionPhase.ResolveTickStartLifecycle,
            ActorTickResolutionPhase.FreezeObservations,
            ActorTickResolutionPhase.CollectJointDecisions,
            ActorTickResolutionPhase.ValidateActions,
            ActorTickResolutionPhase.Rotate,
            ActorTickResolutionPhase.Move,
            ActorTickResolutionPhase.ReserveLifecycleActions,
            ActorTickResolutionPhase.AdvanceExistingProjectiles,
            ActorTickResolutionPhase.LaunchAttacksAndApplyDamage,
            ActorTickResolutionPhase.ApplyRuntimeFaults,
            ActorTickResolutionPhase.ResolvePostDamageLifecycle,
            ActorTickResolutionPhase.ResolveFaultEligibilityCompletion,
            ActorTickResolutionPhase.UpdateCooldownsAndResources,
            ActorTickResolutionPhase.UpdateMode,
            ActorTickResolutionPhase.CompleteDueSameLifeTransitions,
            ActorTickResolutionPhase.ResolveMatchCompletion,
        ];

    public ActorTickResolutionDefinition(
        bool observationsUsePreTickState,
        bool decisionsResolveAsJointStep,
        ActorDamageResolutionDefinition damageResolution,
        IReadOnlyList<ActorTickResolutionPhase> phases,
        CooldownClockKind cooldownClock =
            CooldownClockKind.AdvancesOnlyWithAnArmedForm)
    {
        if (!Enum.IsDefined(cooldownClock))
            throw new ArgumentOutOfRangeException(nameof(cooldownClock));
        CooldownClock = cooldownClock;
        if (!observationsUsePreTickState)
        {
            throw new ArgumentException(
                "Generation 3 supports only pre-tick actor observations.",
                nameof(observationsUsePreTickState));
        }
        if (!decisionsResolveAsJointStep)
        {
            throw new ArgumentException(
                "Generation 3 supports only joint-step decision resolution.",
                nameof(decisionsResolveAsJointStep));
        }
        ArgumentNullException.ThrowIfNull(damageResolution);
        ArgumentNullException.ThrowIfNull(phases);

        ActorTickResolutionPhase[] phaseSnapshot = [.. phases];
        if (phaseSnapshot.Any(phase => !Enum.IsDefined(phase)))
            throw new ArgumentOutOfRangeException(nameof(phases));
        if (!phaseSnapshot.SequenceEqual(SupportedPhases))
        {
            throw new ArgumentException(
                "Generation 3 tick phases must use the complete supported order.",
                nameof(phases));
        }

        ObservationsUsePreTickState = observationsUsePreTickState;
        DecisionsResolveAsJointStep = decisionsResolveAsJointStep;
        DamageResolution = damageResolution;
        Phases = phaseSnapshot.ToImmutableArray();
    }

    public bool ObservationsUsePreTickState { get; }
    public bool DecisionsResolveAsJointStep { get; }
    public ActorDamageResolutionDefinition DamageResolution { get; }
    public ImmutableArray<ActorTickResolutionPhase> Phases { get; }

    /// <summary>
    /// Whose clock a gun's cooldown runs on (DECISIONS #180). The
    /// historical rule ties it to the armed form: a life standing in a
    /// form without an attack profile keeps its remaining cooldown as
    /// inert state — time stops for the gun, the hidden stance tax a
    /// wave-6 author measured (entered at cooldown 3, still 2 at tick
    /// 100). AdvancesWithTime decrements every tick regardless of form,
    /// so a stance or windup no longer pauses recovery. The default is
    /// the historical rule and is omitted from canonical bytes, so every
    /// contract authored before this field keeps its exact fingerprints.
    /// </summary>
    public CooldownClockKind CooldownClock { get; }

    public enum CooldownClockKind
    {
        AdvancesOnlyWithAnArmedForm = 0,
        AdvancesWithTime = 1,
    }
    public MovementActionResolutionKind MovementActionResolution =>
        MovementActionResolutionKind
            .SubmittedAbsoluteCardinalOneTileFacingUnchanged;
    public RotationActionResolutionKind RotationActionResolution =>
        RotationActionResolutionKind
            .SetFacingToSubmittedAbsoluteCardinalPositionUnchanged;
    public ActionAdmissionKind ActionAdmission =>
        ActionAdmissionKind
            .UnknownOrMalformedFaultedOutOfFormRejectedPhysicalBlockedExplicitOverrides;
    public ActionFaultCountingKind ActionFaultCounting =>
        ActionFaultCountingKind.OnlyFaultedOutcomeIncrementsParticipantCounter;
    public MatchCompletionPrecedenceKind MatchCompletionPrecedence =>
        MatchCompletionPrecedenceKind
            .FaultEligibilityShortCircuitThenModeEarlyThenEligibleTimeout;

    public static ImmutableArray<ActorTickResolutionPhase>
        CreateSupportedPhases() => SupportedPhases;

    public enum MovementActionResolutionKind
    {
        SubmittedAbsoluteCardinalOneTileFacingUnchanged = 0,
    }

    public enum RotationActionResolutionKind
    {
        SetFacingToSubmittedAbsoluteCardinalPositionUnchanged = 0,
    }

    public enum ActionAdmissionKind
    {
        /// <summary>
        /// An unknown action code or malformed, missing, duplicate, wrong-type,
        /// or out-of-domain parameter is Faulted. A catalog action excluded by
        /// the current form's action mask is Rejected. A structurally valid
        /// permitted action stopped by occupancy, geometry, cooldown, energy,
        /// health, generation, readiness, or another authoritative state
        /// condition is Blocked. Explicit typed results carried by a declared
        /// action variant take precedence over the generic state outcome.
        /// Wait is parameterless and always structurally accepted.
        /// </summary>
        UnknownOrMalformedFaultedOutOfFormRejectedPhysicalBlockedExplicitOverrides
            = 0,
    }

    public enum ActionFaultCountingKind
    {
        /// <summary>
        /// Only a Faulted validation result increments the participant fault
        /// counter. Rejected and Blocked are observable gameplay outcomes and
        /// do not increment it.
        /// </summary>
        OnlyFaultedOutcomeIncrementsParticipantCounter = 0,
    }

    public enum MatchCompletionPrecedenceKind
    {
        /// <summary>
        /// Complete joint damage, apply the complete joint fault batch, then
        /// finalize every damage-caused destruction and its lifecycle state.
        /// Disqualification cleanup takes precedence over scheduling a return
        /// for a disqualified slot. Then resolve scoring-team eligibility. One
        /// eligible team wins and zero draw immediately, skipping every later
        /// phase. Otherwise finish the tick, check the mode's early terminal
        /// condition, then the maximum-tick timeout. Mode and timeout rankings
        /// consider eligible teams only; ineligible teams are appended tied at
        /// bottom.
        /// </summary>
        FaultEligibilityShortCircuitThenModeEarlyThenEligibleTimeout = 0,
    }
}
