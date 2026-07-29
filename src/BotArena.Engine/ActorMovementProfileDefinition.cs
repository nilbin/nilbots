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
            ActorMovementFacingCoupling.PreserveFacing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!Enum.IsDefined(movementLayer))
            throw new ArgumentOutOfRangeException(nameof(movementLayer));
        if (!Enum.IsDefined(facingCoupling))
            throw new ArgumentOutOfRangeException(nameof(facingCoupling));

        Id = id;
        MovementLayer = movementLayer;
        FacingCoupling = facingCoupling;
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
}
