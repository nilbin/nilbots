namespace BotArena.Engine;

/// <summary>One stable Arc Core source and its public production schedule.</summary>
public sealed record ArcRelayWellScheduleDefinition
{
    public ArcRelayWellScheduleDefinition(
        string wellId,
        int firstBirthTick,
        int cadenceTicks,
        int finalBirthTick)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wellId);
        if (firstBirthTick < 0)
            throw new ArgumentOutOfRangeException(nameof(firstBirthTick));
        if (cadenceTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(cadenceTicks));
        if (finalBirthTick < firstBirthTick
            || (finalBirthTick - firstBirthTick) % cadenceTicks != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(finalBirthTick),
                "The final birth must lie on this Well's cadence.");
        }

        WellId = wellId;
        FirstBirthTick = firstBirthTick;
        CadenceTicks = cadenceTicks;
        FinalBirthTick = finalBirthTick;
    }

    public string WellId { get; }
    public int FirstBirthTick { get; }
    public int CadenceTicks { get; }
    public int FinalBirthTick { get; }
}
