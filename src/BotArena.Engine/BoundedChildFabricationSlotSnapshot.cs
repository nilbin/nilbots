namespace BotArena.Engine;

/// <summary>Dynamic availability of one bounded stable fabrication slot.</summary>
public sealed record BoundedChildFabricationSlotSnapshot(
    int TeamId,
    int UnitId,
    BoundedChildFabricationSlotSnapshot.FabricationSlotState State,
    ActorIdentity? ActiveActorId)
{
    public enum FabricationSlotState
    {
        Active = 0,
        Ready = 1,
        Unavailable = 2,
        Reserved = 3,
        PermanentlyDormant = 4,
    }
}
