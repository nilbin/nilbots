namespace BotArena.Engine;

/// <summary>
/// A named movement capability selected by forms. Declaring an Air profile
/// does not make Air runnable; admission must require an engine that
/// implements that movement layer's resolved semantics.
/// </summary>
public sealed record ActorMovementProfileDefinition
{
    public ActorMovementProfileDefinition(
        string id,
        ActorMovementLayer movementLayer,
        ActorMovementFacingCoupling facingCoupling =
            ActorMovementFacingCoupling.PreserveFacing,
        int moveOpportunitiesPerCycle = 1,
        int moveCycleTicks = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!Enum.IsDefined(movementLayer))
            throw new ArgumentOutOfRangeException(nameof(movementLayer));
        if (!Enum.IsDefined(facingCoupling))
            throw new ArgumentOutOfRangeException(nameof(facingCoupling));
        if (moveOpportunitiesPerCycle <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(moveOpportunitiesPerCycle));
        }
        if (moveCycleTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(moveCycleTicks));
        if (moveOpportunitiesPerCycle > moveCycleTicks)
        {
            throw new ArgumentException(
                "Move opportunities cannot exceed the movement cycle length.",
                nameof(moveOpportunitiesPerCycle));
        }

        Id = id;
        MovementLayer = movementLayer;
        FacingCoupling = facingCoupling;
        MoveOpportunitiesPerCycle = moveOpportunitiesPerCycle;
        MoveCycleTicks = moveCycleTicks;
    }

    public string Id { get; }
    public ActorMovementLayer MovementLayer { get; }

    /// <summary>
    /// How movement couples to body facing. <see
    /// cref="ActorMovementFacingCoupling.PreserveFacing"/> is the inert
    /// default and is omitted from canonical contract bytes, so adding the
    /// capability changes no existing fingerprint.
    /// </summary>
    public ActorMovementFacingCoupling FacingCoupling { get; }

    /// <summary>
    /// How many ticks in each <see cref="MoveCycleTicks"/> window expose a
    /// movement action. Opportunities never bank and a movement remains at
    /// most one adjacent tile. The inert/default rate is 1/1.
    /// </summary>
    public int MoveOpportunitiesPerCycle { get; }

    /// <summary>
    /// Length of the deterministic movement-availability window. Stable unit
    /// IDs stagger its phase so equal-rate squads do not all pause together.
    /// </summary>
    public int MoveCycleTicks { get; }
}
