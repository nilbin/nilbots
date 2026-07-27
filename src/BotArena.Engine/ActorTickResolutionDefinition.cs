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
            ActorTickResolutionPhase.UpdateCooldownsAndResources,
            ActorTickResolutionPhase.UpdateMode,
            ActorTickResolutionPhase.CompleteDueSameLifeTransitions,
            ActorTickResolutionPhase.ResolveMatchCompletion,
        ];

    public ActorTickResolutionDefinition(
        bool observationsUsePreTickState,
        bool decisionsResolveAsJointStep,
        ActorDamageResolutionDefinition damageResolution,
        IReadOnlyList<ActorTickResolutionPhase> phases)
    {
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

    public static ImmutableArray<ActorTickResolutionPhase>
        CreateSupportedPhases() => SupportedPhases;
}
