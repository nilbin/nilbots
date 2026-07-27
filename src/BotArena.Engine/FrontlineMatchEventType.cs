namespace BotArena.Engine;

/// <summary>Authoritative ordered facts emitted by a Frontline step.</summary>
public enum FrontlineMatchEventType
{
    Respawned = 0,
    Turn = 1,
    Move = 2,
    MoveBlocked = 3,
    Shot = 4,
    Damage = 5,
    Destroyed = 6,
    FrontlineProgressChanged = 7,
    FrontlinePositionAdvanced = 8,
    BaseBreached = 9,
}
