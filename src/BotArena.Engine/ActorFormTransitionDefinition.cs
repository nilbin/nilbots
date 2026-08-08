namespace BotArena.Engine;

/// <summary>
/// Data-driven same-life form transition. Form capabilities describe the
/// resulting movement, attack, vision, and objective behavior.
/// </summary>
public sealed record ActorFormTransitionDefinition
    : ActorSameLifeTransitionDefinition
{
    public ActorFormTransitionDefinition(
        string transitionId,
        string actionId,
        string sourceFormId,
        string targetFormId,
        ActorTransitionWindupDefinition windup,
        MemoryContinuityKind memoryContinuity,
        ActorSameLifeHealthDefinition health,
        ActorSameLifeCombatStateDefinition combatState,
        ActorSameLifePlacementDefinition placement,
        bool irreversibleForLife,
        ActorAutomaticReturnTriggerDefinition? automaticReturn = null,
        int cooldownTicks = 0)
        : base(
            transitionId,
            actionId,
            sourceFormId,
            targetFormId,
            windup,
            memoryContinuity,
            health,
            combatState,
            placement,
            irreversibleForLife,
            cooldownTicks)
    {
        AutomaticReturn = automaticReturn;
    }

    public override SameLifeTransitionKind Kind =>
        SameLifeTransitionKind.FormTransition;

    /// <summary>
    /// When declared, the engine also fires this route with no action the tick
    /// a typed source-form counter reaches the threshold. Null is the inert
    /// default and writes no canonical bytes.
    /// </summary>
    public ActorAutomaticReturnTriggerDefinition? AutomaticReturn { get; }
}
