namespace BotArena.Engine;

/// <summary>Lifecycle of a stable Frontline unit slot.</summary>
public enum FrontlineLifecycleStatus
{
    Active = 0,
    Respawning = 1,
    Locked = 2,
    Ready = 3,
    FabricationQueued = 4,
    Rebuilding = 5,
}
