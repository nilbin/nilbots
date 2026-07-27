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
        bool irreversibleForLife)
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
            irreversibleForLife)
    {
    }

    public override SameLifeTransitionKind Kind =>
        SameLifeTransitionKind.FormTransition;
}
