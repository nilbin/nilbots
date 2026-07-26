namespace BotArena.Engine;

/// <summary>
/// Binary objective presence by team. Unit counts are intentionally absent so
/// additional allied bodies can never multiply capture pressure.
/// </summary>
public readonly record struct FrontlineTeamPresence(
    bool Team0Present,
    bool Team1Present)
{
    public int? SoleTeamId => (Team0Present, Team1Present) switch
    {
        (true, false) => 0,
        (false, true) => 1,
        _ => null,
    };

    public static FrontlineTeamPresence FromOccupyingTeamIds(
        IEnumerable<int> teamIds)
    {
        ArgumentNullException.ThrowIfNull(teamIds);
        bool team0 = false;
        bool team1 = false;
        foreach (int teamId in teamIds)
        {
            switch (teamId)
            {
                case 0:
                    team0 = true;
                    break;
                case 1:
                    team1 = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(teamIds),
                        teamId,
                        "Frontline presence supports only team IDs 0 and 1.");
            }
        }
        return new FrontlineTeamPresence(team0, team1);
    }
}
