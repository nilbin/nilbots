namespace BotArena.Engine;

/// <summary>
/// Closed vNext transition family in which the stable unit and runtime life
/// identity survive a form change.
/// </summary>
public abstract record ActorSameLifeTransitionDefinition
{
    internal ActorSameLifeTransitionDefinition(
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
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transitionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFormId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFormId);
        ArgumentNullException.ThrowIfNull(windup);
        if (!Enum.IsDefined(memoryContinuity))
            throw new ArgumentOutOfRangeException(nameof(memoryContinuity));
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(combatState);
        ArgumentNullException.ThrowIfNull(placement);

        TransitionId = transitionId;
        ActionId = actionId;
        SourceFormId = sourceFormId;
        TargetFormId = targetFormId;
        Windup = windup;
        MemoryContinuity = memoryContinuity;
        Health = health;
        CombatState = combatState;
        Placement = placement;
        IrreversibleForLife = irreversibleForLife;
    }

    public abstract SameLifeTransitionKind Kind { get; }
    public string TransitionId { get; }
    public string ActionId { get; }
    public string SourceFormId { get; }
    public string TargetFormId { get; }
    public ActorTransitionWindupDefinition Windup { get; }
    public MemoryContinuityKind MemoryContinuity { get; }
    public ActorSameLifeHealthDefinition Health { get; }
    public ActorSameLifeCombatStateDefinition CombatState { get; }
    public ActorSameLifePlacementDefinition Placement { get; }
    public bool IrreversibleForLife { get; }

    public enum SameLifeTransitionKind
    {
        FormTransition = 0,
    }

    public enum MemoryContinuityKind
    {
        PreservePrivateMemory = 0,
    }
}
