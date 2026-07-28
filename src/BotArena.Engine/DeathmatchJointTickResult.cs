namespace BotArena.Engine;

/// <summary>
/// Result of applying one complete joint-tick damage batch. A non-null
/// <see cref="KillLimitStandings"/> means the optional kill limit completed
/// the match; its null winner represents a completed tied-top draw.
/// </summary>
public sealed class DeathmatchJointTickResult
{
    internal DeathmatchJointTickResult(
        DeathmatchScoreState scoreState,
        TeamStandings? killLimitStandings)
    {
        ArgumentNullException.ThrowIfNull(scoreState);

        ScoreState = scoreState;
        KillLimitStandings = killLimitStandings;
    }

    public DeathmatchScoreState ScoreState { get; }
    public TeamStandings? KillLimitStandings { get; }
    public bool KillLimitCompleted => KillLimitStandings is not null;
    public int? WinnerTeamId => KillLimitStandings?.WinnerTeamId;
}
