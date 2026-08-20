using BotArena.Sdk;

/// <summary>
/// The prior decision's ordinary Fight selections plus still-pending strikes
/// are the one authoritative coordinator view of local combat. Conditions
/// read this snapshot at the next normal decision boundary; no parallel
/// combat lifecycle exists.
/// </summary>
internal static class TacticalZoneFightState
{
    internal sealed record ActiveFight(
        ActorIdentity Fighter,
        Position FighterPosition,
        Position TargetPosition,
        string Path);

    internal static int Count(
        int teamId,
        Func<Position, bool> inZone,
        IEnumerable<ActiveFight> activeFights,
        IEnumerable<GenericActorContext.ArcRelayPendingStrike> pendingStrikes)
    {
        HashSet<ActorIdentity> counted = [];
        foreach (ActiveFight fight in activeFights)
        {
            if (fight.Fighter.TeamId == teamId
                && (inZone(fight.FighterPosition)
                    || inZone(fight.TargetPosition)))
            {
                counted.Add(fight.Fighter);
            }
        }
        foreach (GenericActorContext.ArcRelayPendingStrike strike
                 in pendingStrikes)
        {
            if (strike.Shooter.TeamId == teamId
                && (inZone(strike.Origin)
                    || strike.Tiles.Any(inZone)))
            {
                counted.Add(strike.Shooter);
            }
        }
        return counted.Count;
    }
}
