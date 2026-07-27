namespace BotArena.Engine;

/// <summary>
/// Match-contract binding from a rules-owned region role to one concrete map
/// region for a submitted participant. This lets reusable rules express
/// "own fabrication pad" without putting team identity into a neutral map.
/// </summary>
public sealed record ActorParticipantRegionAssignmentDefinition
{
    public ActorParticipantRegionAssignmentDefinition(
        int participantId,
        string regionRoleId,
        string mapRegionId,
        Direction facing)
    {
        if (participantId < 0)
            throw new ArgumentOutOfRangeException(nameof(participantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(regionRoleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapRegionId);
        if (!Enum.IsDefined(facing))
            throw new ArgumentOutOfRangeException(nameof(facing));

        ParticipantId = participantId;
        RegionRoleId = regionRoleId;
        MapRegionId = mapRegionId;
        Facing = facing;
    }

    public int ParticipantId { get; }
    public string RegionRoleId { get; }
    public string MapRegionId { get; }
    public Direction Facing { get; }
}
