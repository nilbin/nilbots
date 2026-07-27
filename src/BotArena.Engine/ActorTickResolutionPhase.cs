namespace BotArena.Engine;

/// <summary>
/// Generic actor tick phases. Variant-specific details inside a phase remain
/// closed schema semantics; maps cannot inject phases or executable behavior.
/// </summary>
public enum ActorTickResolutionPhase
{
    ResolveTickStartLifecycle = 0,
    FreezeObservations = 1,
    CollectJointDecisions = 2,
    ValidateActions = 3,
    Rotate = 4,
    Move = 5,
    ReserveLifecycleActions = 6,
    AdvanceExistingProjectiles = 7,
    LaunchAttacksAndApplyDamage = 8,
    ApplyRuntimeFaults = 9,
    ResolvePostDamageLifecycle = 10,
    ResolveFaultEligibilityCompletion = 11,
    UpdateCooldownsAndResources = 12,
    UpdateMode = 13,
    CompleteDueSameLifeTransitions = 14,
    ResolveMatchCompletion = 15,
}
