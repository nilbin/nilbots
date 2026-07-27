namespace BotArena.Sdk;

/// <summary>
/// Stable identity of one independently executing body. <see cref="TeamId"/>
/// and <see cref="UnitId"/> survive destruction; <see cref="LifeId"/> changes
/// whenever that stable unit receives a fresh runtime and private memory.
/// </summary>
public sealed record ActorIdentity : IComparable<ActorIdentity>
{
    public ActorIdentity(int teamId, int unitId, int lifeId)
    {
        if (teamId < 0)
            throw new ArgumentOutOfRangeException(nameof(teamId));
        if (unitId < 0)
            throw new ArgumentOutOfRangeException(nameof(unitId));
        if (lifeId < 0)
            throw new ArgumentOutOfRangeException(nameof(lifeId));

        TeamId = teamId;
        UnitId = unitId;
        LifeId = lifeId;
    }

    public int TeamId { get; }
    public int UnitId { get; }
    public int LifeId { get; }

    public int CompareTo(ActorIdentity? other)
    {
        if (other is null)
            return 1;

        int team = TeamId.CompareTo(other.TeamId);
        if (team != 0)
            return team;

        int unit = UnitId.CompareTo(other.UnitId);
        return unit != 0 ? unit : LifeId.CompareTo(other.LifeId);
    }

    public override string ToString() => $"{TeamId}:{UnitId}:{LifeId}";
}

/// <summary>How allied sensors are combined before bot instances run.</summary>
public enum TeamPerceptionMode
{
    Individual = 0,
    ImmediateUnion = 1,
}

/// <summary>Why a fresh body and private-memory instance was created.</summary>
public enum ActorSpawnReason
{
    Initial = 0,
    Respawn = 1,
    Rebuild = 2,
    Fabrication = 3,
}

/// <summary>Public state of a stable allied unit slot.</summary>
public enum FrontlineLifecycleStatus
{
    Active = 0,
    Respawning = 1,
    Locked = 2,
    Ready = 3,
    FabricationQueued = 4,
    Rebuilding = 5,
}
