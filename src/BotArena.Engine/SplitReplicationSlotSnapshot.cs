namespace BotArena.Engine;

/// <summary>Dynamic availability of one topology-owned stable unit slot.</summary>
public sealed record SplitReplicationSlotSnapshot(
    int TeamId,
    int UnitId,
    SplitReplicationSlotSnapshot.SplitSlotState State,
    ActorIdentity? ActiveActorId)
{
    public enum SplitSlotState
    {
        Active = 0,
        Ready = 1,
        Unavailable = 2,
        Reserved = 3,
        PermanentlyDormant = 4,
    }
}
