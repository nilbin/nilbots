namespace BotArena.Engine;

/// <summary>
/// Stateless movement-rate schedule shared by legality projection, gameplay
/// validation, and replay chronology validation. A rate grants action
/// opportunities rather than stored movement points: waiting on one never
/// creates a future burst, and one accepted action still moves one tile.
/// </summary>
internal static class ActorMovementCadence
{
    public static bool IsMoveOpportunity(
        ActorMovementProfileDefinition profile,
        ActorIdentity actorId,
        int tick)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (tick < 0)
            throw new ArgumentOutOfRangeException(nameof(tick));

        int cycle = profile.MoveCycleTicks;
        int phase = ((tick % cycle) + (actorId.UnitId % cycle)) % cycle;
        return phase < profile.MoveOpportunitiesPerCycle;
    }
}
