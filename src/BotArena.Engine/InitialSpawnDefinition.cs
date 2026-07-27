namespace BotArena.Engine;

/// <summary>A named, fully resolved vNext initial spawn.</summary>
public sealed record InitialSpawnDefinition
{
    public InitialSpawnDefinition(
        string spawnId,
        Position position,
        Direction facing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spawnId);
        if (!Enum.IsDefined(facing))
            throw new ArgumentOutOfRangeException(nameof(facing));

        SpawnId = spawnId;
        Position = position;
        Facing = facing;
    }

    public string SpawnId { get; }
    public Position Position { get; }
    public Direction Facing { get; }
}
