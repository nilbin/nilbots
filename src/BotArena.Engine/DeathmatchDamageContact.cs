namespace BotArena.Engine;

/// <summary>
/// One authoritative actual-health-removal result from the already-canonical
/// joint-tick damage batch. <see cref="SourceTeamId"/> is the scoring team of
/// the firing life captured when the attack was created; it remains valid
/// after that life is destroyed or retired. Null represents unattributed
/// damage.
/// </summary>
public sealed record DeathmatchDamageContact
{
    public DeathmatchDamageContact(
        int? sourceTeamId,
        int targetTeamId,
        long actualHealthRemoved,
        bool causedDestruction)
    {
        if (sourceTeamId < 0)
            throw new ArgumentOutOfRangeException(nameof(sourceTeamId));
        if (targetTeamId < 0)
            throw new ArgumentOutOfRangeException(nameof(targetTeamId));
        if (actualHealthRemoved < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actualHealthRemoved));
        }
        if (causedDestruction && actualHealthRemoved == 0)
        {
            throw new ArgumentException(
                "A damage-caused destruction must remove positive actual health.",
                nameof(causedDestruction));
        }

        SourceTeamId = sourceTeamId;
        TargetTeamId = targetTeamId;
        ActualHealthRemoved = actualHealthRemoved;
        CausedDestruction = causedDestruction;
    }

    public int? SourceTeamId { get; }
    public int TargetTeamId { get; }
    public long ActualHealthRemoved { get; }
    public bool CausedDestruction { get; }
}
