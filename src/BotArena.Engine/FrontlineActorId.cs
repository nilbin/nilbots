namespace BotArena.Engine;

/// <summary>
/// Stable identity of one independently executing Frontline life. Canonical
/// ordering is team, then stable team-local unit, then monotonically increasing
/// life.
/// </summary>
public readonly record struct FrontlineActorId(
    int TeamId,
    int UnitId,
    int LifeId) : IComparable<FrontlineActorId>
{
    public int CompareTo(FrontlineActorId other)
    {
        int team = TeamId.CompareTo(other.TeamId);
        if (team != 0)
            return team;

        int unit = UnitId.CompareTo(other.UnitId);
        return unit != 0 ? unit : LifeId.CompareTo(other.LifeId);
    }

    public override string ToString() => $"{TeamId}:{UnitId}:{LifeId}";
}
