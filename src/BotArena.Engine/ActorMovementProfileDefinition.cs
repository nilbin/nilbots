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
        ActorMovementLayer movementLayer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!Enum.IsDefined(movementLayer))
            throw new ArgumentOutOfRangeException(nameof(movementLayer));

        Id = id;
        MovementLayer = movementLayer;
    }

    public string Id { get; }
    public ActorMovementLayer MovementLayer { get; }
}
