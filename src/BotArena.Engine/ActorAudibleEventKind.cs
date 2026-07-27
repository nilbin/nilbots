namespace BotArena.Engine;

/// <summary>
/// World-event families that a vision profile may expose as redacted sounds.
/// This is independent of the frozen replay-v1 event enum.
/// </summary>
public enum ActorAudibleEventKind
{
    Attack = 0,
    Damage = 1,
    Destruction = 2,
}
