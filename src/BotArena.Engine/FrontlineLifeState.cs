namespace BotArena.Engine;

/// <summary>Mutable simulation state of one active, life-qualified actor.</summary>
public sealed class FrontlineLifeState
{
    internal FrontlineLifeState(
        FrontlineActorId actorId,
        string formId,
        Position position,
        Direction facing,
        int health,
        int spawnedAtTick,
        int energy = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formId);
        ActorId = actorId;
        FormId = formId;
        Position = position;
        Facing = facing;
        Health = health;
        SpawnedAtTick = spawnedAtTick;
        Energy = energy;
    }

    public FrontlineActorId ActorId { get; }
    public string FormId { get; internal set; }
    public FrontlinePendingFormTransition? PendingFormTransition
    {
        get;
        internal set;
    }
    public Position Position { get; internal set; }
    public Direction Facing { get; internal set; }
    public int Health { get; internal set; }
    public int Cooldown { get; internal set; }
    public int Energy { get; internal set; }
    public long DamageDealt { get; internal set; }
    public ActionResult LastActionResult { get; internal set; } = ActionResult.None;
    public int SpawnedAtTick { get; }
}
