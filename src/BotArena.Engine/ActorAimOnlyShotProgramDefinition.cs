namespace BotArena.Engine;

/// <summary>
/// Required non-aim fields for a zero-bend shot-program payload. The actual
/// initial aim remains the caller-selected value within the declared bounds.
/// </summary>
public sealed record ActorAimOnlyShotProgramDefinition
{
    public ActorAimOnlyShotProgramDefinition(
        int bendDirection,
        int bendAfterTiles,
        int bendEveryTiles,
        int bendCount)
    {
        if (bendDirection != 0)
            throw new ArgumentOutOfRangeException(nameof(bendDirection));
        if (bendAfterTiles < 0)
            throw new ArgumentOutOfRangeException(nameof(bendAfterTiles));
        if (bendEveryTiles <= 0)
            throw new ArgumentOutOfRangeException(nameof(bendEveryTiles));
        if (bendCount != 0)
            throw new ArgumentOutOfRangeException(nameof(bendCount));

        BendDirection = bendDirection;
        BendAfterTiles = bendAfterTiles;
        BendEveryTiles = bendEveryTiles;
        BendCount = bendCount;
    }

    public int BendDirection { get; }
    public int BendAfterTiles { get; }
    public int BendEveryTiles { get; }
    public int BendCount { get; }
}
