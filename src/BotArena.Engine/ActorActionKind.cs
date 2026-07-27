namespace BotArena.Engine;

/// <summary>Closed semantic action families understood by actor contracts.</summary>
public enum ActorActionKind
{
    Wait = 0,
    Movement = 1,
    Rotation = 2,
    Attack = 3,
    Fabrication = 4,
    SameLifeTransition = 5,
    Replication = 6,
}
