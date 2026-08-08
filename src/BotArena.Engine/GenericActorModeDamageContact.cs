namespace BotArena.Engine;

/// <summary>
/// Mode-neutral scoring fact produced after the world has applied one
/// authoritative damage contact.
/// </summary>
internal sealed record GenericActorModeDamageContact
{
    public GenericActorModeDamageContact(
        int? sourceTeamId,
        int targetTeamId,
        long actualHealthRemoved,
        bool causedDestruction,
        Position targetPosition = default)
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
        TargetPosition = targetPosition;
    }

    public int? SourceTeamId { get; }
    public int TargetTeamId { get; }
    public long ActualHealthRemoved { get; }
    public bool CausedDestruction { get; }

    /// <summary>
    /// Where the target stood when the contact landed — after movement, so it
    /// is the tile the target occupies for the rest of the tick. A mode that
    /// scopes damage to a region reads it; a mode that does not ignores it.
    /// </summary>
    public Position TargetPosition { get; }

    /// <summary>
    /// True when the damage came from another scoring team. Environmental and
    /// same-team contacts are never hostile.
    /// </summary>
    public bool IsHostile =>
        SourceTeamId is int sourceTeamId && sourceTeamId != TargetTeamId;
}
